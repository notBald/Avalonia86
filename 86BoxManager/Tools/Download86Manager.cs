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

    public void Update86Box(JenkinsBase.Artifact build, int number, bool update_roms, Func<(string, List<ExtractedFile>), bool> files)
    {
        IsWorking = true;
        IsUpdating = true;
        Progress = 0;

        AddLog($"Downloading artifact: {build.FileName}");
        string url = $"{JENKINS_BASE_URL}/{number}/artifact/{build.RelativePath}";

        ThreadPool.QueueUserWorkItem(async o =>
        {
            using var httpClient = GetHttpClient();

            AddLog("Connecting to: " + url);
            long total_bytes_read = 0;
            long bytes_to_read;
            long estimated_bytes_to_read;

            try
            {
                var zip_data = new MemoryStream();

                using (var response = await httpClient.GetAsync(url))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        Error("Failed to contact server");
                        Stop();
                        return;
                    }

                    bytes_to_read = response.Content.Headers.ContentLength ?? 40 * 1024 * 1024;
                    estimated_bytes_to_read = bytes_to_read;
                    if (update_roms)
                    {
                        estimated_bytes_to_read *= 3;
                    }

                    AddLog($"Downloading {FolderSizeCalculator.ConvertBytesToReadableSize(bytes_to_read)}");
                    using (var zip = await response.Content.ReadAsStreamAsync())
                    {
                        var buffer = new byte[81920];
                        int bytes_read;

                        while ((bytes_read = await zip.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            zip_data.Write(buffer, 0, bytes_read);
                            total_bytes_read += bytes_read;
                            Progress = (double)total_bytes_read / estimated_bytes_to_read;
                        }
                    }
                }

                //This is a quick opperation, so I won't bother with having a progress bar or doing it on antoher thread, etc.
                if (build.FileName.EndsWith(".zip", StringComparison.InvariantCultureIgnoreCase))
                {
                    AddLog($"Finished downloading 86Box artifact - Verifying");
                    var box_files = ExtractFilesFromZip(zip_data);

                    if (!files(("86box", box_files)))
                    {
                        Stop();
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
                            Stop();
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

                    if (!files(("86box.AppImage", l)))
                    {
                        Stop();
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

                    using (var response = await httpClient.GetAsync(ROMS_ZIP_URL))
                    {
                        if (!response.IsSuccessStatusCode)
                        {
                            Error("Failed to contact server");
                            Stop();
                            return;
                        }

                        var roms_bytes = response.Content.Headers.ContentLength ?? 80 * 1024 * 1024;
                        bytes_to_read += roms_bytes;
                        AddLog($"Downloading {FolderSizeCalculator.ConvertBytesToReadableSize(roms_bytes)}");

                        using (var zip = await response.Content.ReadAsStreamAsync())
                        {
                            var buffer = new byte[81920];
                            int bytes_read;

                            while ((bytes_read = await zip.ReadAsync(buffer, 0, buffer.Length)) > 0)
                            {
                                zip_data.Write(buffer, 0, bytes_read);
                                total_bytes_read += bytes_read;
                                Progress = (double)total_bytes_read / estimated_bytes_to_read;
                            }
                        }
                    }

                    //This is a quick opperation, so I won't bother with having a progress bar or doing it on antoher thread, etc.
                    AddLog($"Finished downloading ROM files - Verifying");
                    var box_files = ExtractFilesFromZip(zip_data);

                    if (!files(("ROMs", box_files)))
                    {
                        Stop();
                        return;
                    }
                }

                AddLog($" -- Job done -- ");
                Stop();
            }
            catch (Exception e)
            {
                Error(e.Message);
            }
            finally
            {
                Stop();
            }
        });
    }

    private List<ExtractedFile> ExtractFilesFromZip(MemoryStream zipStream)
    {
        var extractedFiles = new List<ExtractedFile>();

        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Read, true))
        {
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
                        Stop();
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
                    Stop();
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
                        Stop();
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
                    Stop();
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
                Stop();
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

    private void Stop()
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsFetching = false;
            IsWorking = false;
            IsUpdating = false;
        });
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
