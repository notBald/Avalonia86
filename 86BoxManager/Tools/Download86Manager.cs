using _86BoxManager.Xplat;
using Avalonia.Threading;
using DynamicData;
using DynamicData.Kernel;
using ReactiveUI;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using static _86BoxManager.Tools.ProgressCalculator;

namespace _86BoxManager.Tools;

public class Download86Manager : ReactiveObject
{
    private double _current_progress;
    private bool _is_working, _is_fetching_log, _is_updating;
    private int? _latest_build;
    private DateTime? _last_commit;

    private const string JENKINS_BASE_URL = "https://ci.86box.net/job/86Box";
    private const string JENKINS_LASTBUILD = JENKINS_BASE_URL + "/lastSuccessfulBuild";
    private const string ROMS_URL = "https://api.github.com/repos/86Box/roms/";
    private const string ROMS_COMMITS_URL = $"{ROMS_URL}commits";
    private const string ROMS_ZIP_URL = $"https://github.com/86Box/roms/archive/refs/heads/master.zip";

    public event Action<string> Log;
    public event Action<string> ErrorLog;
    public static class Operation
    {
        public const int Download86Box = 0;
        public const int VerifyExtract86Box = 1;
        public const int Move86BoxToArchive = 2;
        public const int MoveROMsToArchive = 3;
        public const int Store86BoxToDisk = 4;
        public const int DownloadROMs = 5;
        public const int ExtractROMs = 6;
        public const int WriteROMsToDisk = 7;
    }

    /// <summary>
    /// Download manager is running
    /// </summary>
    /// <remarks>
    /// Note how we don't dispatch. That's important. The implementation depends
    /// on this not being set from a background thread. If you need to change
    /// this from a background thead, make sure to use "invoke" instea of post.
    /// </remarks>
    public bool IsWorking
    {
        get => _is_working;
        private set
        {
            if (value && _is_working)
                throw new Exception("Can't do two jobs");
            this.RaiseAndSetIfChanged(ref _is_working, value);
        }
    }
    public bool IsFetching
    {
        get => _is_fetching_log;
        private set => this.RaiseAndSetIfChanged(ref _is_fetching_log, value);
    }

    public bool IsUpdating
    {
        get => _is_updating;
        private set => this.RaiseAndSetIfChanged(ref _is_updating, value);
    }

    public int? LatestBuild
    {
        get => _latest_build;
        private set
        {
            //Invoke isn't strickly needed, but avoids potential race conditions.
            Dispatcher.UIThread.Invoke(() =>
            {
                this.RaiseAndSetIfChanged(ref _latest_build, value);
            });
        }
    }

    public DateTime? LatestRomCommit
    {
        get => _last_commit;
        set
        {
            //Invoke isn't strickly needed, but avoids potential race conditions.
            Dispatcher.UIThread.Invoke(() =>
            {
                this.RaiseAndSetIfChanged(ref _last_commit, value);
            });
        }
    }

    public double Progress
    {
        get => _current_progress;
        set
        {
            Dispatcher.UIThread.Post(() => this.RaiseAndSetIfChanged(ref _current_progress, value));
        }
    }

    public SourceCache<JenkinsBase.Artifact, string> Artifacts = new(s => s.FileName);

    private HttpClient GetHttpClient()
    {
        var h = new HttpClient();
        h.DefaultRequestHeaders.Add("User-Agent", "Avalonia86");
        return h;
    }

    public delegate bool HandleFiles(string name, List<ExtractedFile> files, ProgressCalculator calc);

    /// <summary>
    /// Downloads new 86Box
    /// </summary>
    /// <param name="build"></param>
    /// <param name="number"></param>
    /// <param name="update_roms"></param>
    /// <param name="files"></param>
    /// <remarks>
    ///About the progress bar
    ///  1. Download 86Box
    ///  2. Verify/Extract 86Box
    ///  3. Move 86Box to archive
    ///  4. Move ROMs to archive
    ///  5. Store 86Box to disk
    ///  6. Download ROMs
    ///  7. Extract ROMs
    ///  8. Write ROMs to disk
    ///
    ///Some of these operation can in theory be done in parallel.
    ///
    /// 33% will be dedicated to 1.
    ///  3% goes to 2.
    ///  1% goes to 3, 4 and 5.
    /// 20% go to 6, 7, 8 each.
    /// </remarks>
    public void Update86Box(JenkinsBase.Artifact build, int number, bool update_roms, HandleFiles files)
    {
        //Since  this is done on the UI thread, it's thread safe, as the other
        //thread will never set this false until we're off the UI thread.
        //if (!IsWorking) //<-- Need to make this function capable of running in parralell before uncommenting this
            IsWorking = true;
        IsUpdating = true;
        Progress = 0;

        AddLog($"Downloading artifact: {build.FileName}");
        string url = $"{JENKINS_BASE_URL}/{number}/artifact/{build.RelativePath}";

        //Todo: Adjust this to only include the operations that will actually be done. Don't
        //      remove enteries in the array, just set them to zero.
        var calc = new ProgressCalculator(
        [
            33, //Download86Box
            3,  //VerifyExtract86Box
            1,  //Move86BoxToArchive
            1,  //MoveROMsToArchive
            1,  //Store86BoxToDisk
            21, //DownloadROMs
            20, //ExtractROMs
            20  //WriteROMsToDisk
        ]);

        ThreadPool.QueueUserWorkItem(async o =>
        {
            using var httpClient = GetHttpClient();

            AddLog("Connecting to: " + url);

            try
            {
                var zip_data = new MemoryStream();

                using (var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        Error("Failed to contact server");
                        return;
                    }

                    var total_bytes_to_read = response.Content.Headers.ContentLength ?? 40 * 1024 * 1024;

                    AddLog($"Downloading {FolderSizeCalculator.ConvertBytesToReadableSize(total_bytes_to_read)}");
                    using (var zip = await response.Content.ReadAsStreamAsync())
                    {
                        var buffer = new byte[81920];
                        int bytes_read;
                        int total_bytes_read = 0;

                        while ((bytes_read = await zip.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            zip_data.Write(buffer, 0, bytes_read);
                            total_bytes_read += bytes_read;
                            Progress = calc.CalculateProgress(Operation.Download86Box, total_bytes_read, total_bytes_to_read);
                        }
                    }
                }

                //This is a quick opperation, so I won't bother with having a progress bar or doing it on antoher thread, etc.
                if (build.FileName.EndsWith(".zip", StringComparison.InvariantCultureIgnoreCase))
                {
                    AddLog($"Finished downloading 86Box artifact - Verifying");
                    var box_files = ExtractFilesFromZip(Operation.VerifyExtract86Box, zip_data, calc);

                    if (!files("86box", box_files, calc))
                    {
                        return;
                    }
                }
                else if (build.FileName.EndsWith(".AppImage", StringComparison.InvariantCultureIgnoreCase)) 
                {
                    AddLog($"Finished downloading 86Box artifact - Verifying:");
                    try
                    {
                        zip_data.Position = 0;

                        //var test = Platforms.RequestManager(System.Runtime.InteropServices.OSPlatform.Linux);
                        //var vii = test.Get86BoxInfo(zip_data);

                        var vi = Platforms.Manager.Get86BoxInfo(zip_data);
                        if (vi == null)
                        {
                            Error(" - AppImage is not valid");
                            return;
                        }

                        AddLog($" - 86Box version {vi.FileMajorPart}.{vi.FileMinorPart}.{vi.FileBuildPart} - Build: {vi.FilePrivatePart}");
                    } catch { AddLog(" - Skipping validation"); }

                    zip_data.Position = 0;
                    var ef = new ExtractedFile() { FileData = zip_data, FilePath = build.FileName };
                    var l = new List<ExtractedFile>
                    {
                        new ExtractedFile() { FilePath = "", FileData = new MemoryStream() },
                        ef
                    };

                    if (!files("86box.AppImage", l, calc))
                    {
                        return;
                    }
                }
                else 
                {
                    throw new NotSupportedException(build.FileName);
                }

                if (update_roms)
                {
                    AddLog("");
                    AddLog("Downloading latest ROMs");
                    AddLog("Connecting to: " + ROMS_ZIP_URL);
                    zip_data.Position = 0;

                    using (var response = await httpClient.GetAsync(ROMS_ZIP_URL, HttpCompletionOption.ResponseHeadersRead))
                    {
                        if (!response.IsSuccessStatusCode)
                        {
                            Error("Failed to contact server");
                            return;
                        }

                        var roms_bytes = response.Content.Headers.ContentLength ?? 80 * 1024 * 1024;
                        AddLog($"Downloading {FolderSizeCalculator.ConvertBytesToReadableSize(roms_bytes)}");

                        using (var zip = await response.Content.ReadAsStreamAsync())
                        {
                            var buffer = new byte[81920];
                            int bytes_read;
                            int total_bytes_read = 0;

                            while ((bytes_read = await zip.ReadAsync(buffer, 0, buffer.Length)) > 0)
                            {
                                zip_data.Write(buffer, 0, bytes_read);
                                total_bytes_read += bytes_read;
                                Progress = calc.CalculateProgress(Operation.DownloadROMs, total_bytes_read , roms_bytes);
                            }
                        }
                    }

                    //This is a quick opperation, so I won't bother with having a progress bar or doing it on antoher thread, etc.
                    AddLog($"Finished downloading ROM files - Verifying");
                    zip_data.Position = 0;
                    //Linux gets a valid Zip file, extraction just fails for some reason. Todo: Use the other Zip libary.
                    //File.WriteAllBytes(Environment.GetEnvironmentVariable("HOME") + "/zip.zip", zip_data.ToArray());
                    var box_files = ExtractFilesFromZip(Operation.ExtractROMs, zip_data, calc);

                    if (!files("ROMs", box_files, calc))
                    {
                        return;
                    }
                }

                AddLog($" -- Job done -- ");
            }
            catch (Exception e)
            {
                Error(e.Message);
            }
            finally
            {
                Dispatcher.UIThread.Post(() =>
                {
                    //Since we are on the UI thread and IsWorking is only
                    //flipped on the UI thread, this is thead safe to do.
                    IsUpdating = false;
                    IsWorking = IsFetching;
                });
            }
        });
    }

    private List<ExtractedFile> ExtractFilesFromZip(int operation, MemoryStream zipStream, ProgressCalculator calc)
    {
        var extractedFiles = new List<ExtractedFile>();

        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Read, true))
        {
            var total = archive.Entries.Count;
            int nr = 0;
            Progress = calc.CalculateProgress(operation, nr++, total);

            foreach (var entry in archive.Entries)
            {
                using (var entryStream = entry.Open())
                {
                    var memoryStream = new MemoryStream();
                    entryStream.CopyTo(memoryStream);
                    memoryStream.Position = 0; // Reset the position to the beginning

                    extractedFiles.Add(new ExtractedFile
                    {
                        FilePath = entry.FullName,
                        FileData = memoryStream
                    });
                }

                Progress = calc.CalculateProgress(operation, nr++, total);
            }
        }

        return extractedFiles;
    }

    public void FetchMetadata(int current_build)
    {
        const string jenkins_url = $"{JENKINS_LASTBUILD}/api/json";
        const string github_url = $"{ROMS_COMMITS_URL}?per_page=1";

        IsWorking = true;
        IsFetching = true;        

        ThreadPool.QueueUserWorkItem(async o =>
        {
            using var httpClient = GetHttpClient();

            try
            {
                GithubCommit[] gjob;

                AddLog("Connecting to: " + ROMS_COMMITS_URL);
                using (var response = await httpClient.GetAsync(github_url))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        Error("Failed to contact server");
                        return;
                    }

                    using (var json_str = await response.Content.ReadAsStreamAsync())
                    {
                        gjob = JsonSerializer.Deserialize<GithubCommit[]>(json_str);
                    }
                }

                if (gjob.Length == 0 || gjob[0].Commit == null || gjob[0].Commit.Committer == null)
                {
                    Error($"Parsing failed, invalid response");
                    return;
                }

                var date = gjob[0].Commit.Committer.Date;
                AddLog("ROMs last updated: " + date.ToString("d", CultureInfo.CurrentCulture));
                AddLog("");
                LatestRomCommit = date;

                JenkinsBuild job;
                AddLog("Connecting to: " + jenkins_url);
                using (var response = await httpClient.GetAsync(jenkins_url))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        Error("Failed to contact server");
                        return;
                    }

                    using (var json_str = await response.Content.ReadAsStreamAsync())
                    {
                        //AddLog($"Parsing {FolderSizeCalculator.ConvertBytesToReadableSize(json_str.Length)} of data");
                        job = JsonSerializer.Deserialize<JenkinsBuild>(json_str);
                    }
                }

                if (job.Artifacts == null || job.Number < 6507 || job.Url == null || job.Artifacts.Count < 1)
                {
                    Error($"Parsing failed, invalid response");
                    return;
                }

                //Note, SourceCache is thread safe
                Artifacts.Clear();
                Artifacts.AddOrUpdate(job.Artifacts);

                //Note, must be done after upating Artifacts.
                LatestBuild = job.Number;

                //AddLog($"Latest build is {job.Number}");
                var changelog = await FetchChangelog(job, current_build, httpClient);

                if (changelog.Count == 0)
                    AddLog($" -- There was no enteries in the changelog --");
                else if (changelog.Count == 1)
                    AddLog($" -- There was 1 entery in the changelog --");
                else
                    AddLog($" -- Changelog has {changelog.Count} enteries --");
            }
            catch (Exception e)
            {
                Error(e.Message);
            }
            finally 
            {
                Dispatcher.UIThread.Post(() =>
                {
                    //Since we are on the UI thread and IsWorking is only
                    //flipped on the UI thread, this is thead safe to do.
                    IsFetching = false;
                    IsWorking = IsUpdating;
                });
            }
        });
    }

    private async Task<List<string>> FetchChangelog(JenkinsBuild build, int from, HttpClient httpClient)
    {
        List<string> changelog = new List<string>();

        if (from == -1)
            from = build.Number - 1;

        if (build.Number < from)
        {
            AddLog($"Skipping fetching changelog: local build newer than server build");
            return changelog;
        }

        if (build.Number == from)
        {
            AddLog($"Skipping fetching changelog: local build is same as server build");
            return changelog;
        }

        AddLog($"Fetching changelog going from {from} to {build.Number}");
        AddLog($" -- Changelog start --");
        for (int c = from + 1; c <= build.Number; c++)
        {
            bool sucess = false;

            try { sucess = await FetchChangelog(c, changelog, httpClient); }
            catch
            { }

            if (!sucess)
            {
                Error($"Fetching of changelog for build {c} failed, aborting");
                break;
            }
        }

        return changelog;
    }

    private async Task<bool> FetchChangelog(int build, List<string> changelog, HttpClient httpClient)
    {
        string url = $"{JENKINS_BASE_URL}/{build}/api/json";
        JenkinsRun job;
        using (var response = await httpClient.GetAsync(url))
        {
            response.EnsureSuccessStatusCode();
            using (var json_str = await response.Content.ReadAsStreamAsync())
            {
                job = JsonSerializer.Deserialize<JenkinsRun>(json_str);
            }
        }

        if (job.ChangeSets == null || job.ChangeSets.Count == 0)
            return false;
        var cs = job.ChangeSets[0];
        
        if (cs.Items != null)
        {
            foreach (var change in cs.Items)
            {
                if (!string.IsNullOrWhiteSpace(change.Msg))
                {
                    changelog.Add(change.Msg);
                    AddLog($"{change.Author?.FullName ?? "Unknown"}: {change.Msg}");
                }
            }
        }

        return true;
    }

    private void AddLog(string s)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (Log != null)
                Log(s);
        });
    }

    private void Error(string s)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (ErrorLog != null)
                ErrorLog(s);
        });
    }

    public sealed class JenkinsRun : JenkinsBase
    {
        // Additional properties specific to JenkinsRun can be added here
    }

    public sealed class JenkinsBuild : JenkinsBase
    {
        // Additional properties specific to JenkinsBuild can be added here
    }

    public class ExtractedFile
    {
        public string FilePath { get; set; }
        public MemoryStream FileData { get; set; }

        public override string ToString()
        {
            return FilePath;
        }
    }
}
