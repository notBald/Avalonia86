using Avalonia86.Core;
using Avalonia86.Views;
using Avalonia86.Xplat;
using Avalonia.Threading;
using DynamicData;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Avalonia86.Tools;

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

    private static class Operation
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

    public abstract class LogJob
    {
        public event Action<string> Log;
        public event Action<string> ErrorLog;

        public void AddLog(string s)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (Log != null)
                    Log(s);
            });
        }

        public void Error(string s)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (ErrorLog != null)
                    ErrorLog(s);
            });
        }
    }

    public sealed class DownloadJob : LogJob
    {
        public JenkinsBase.Artifact Build { get; private set; }
        public readonly int Number;

        public bool Move86BoxToArchive { get; set; }
        public bool DownloadROMs { get; set; }

        public string ArchiveName { get; set; }
        public string ArchivePath { get; set; }
        public bool PreserveROMs { get; set; }
        public string ArchiveVersion { get; set; }
        public string ArchiveComment { get; set; }

        //Beware. Not a thread safe object
        public ExeModel CurrentExe { get; set; }

        public event Action Update;

        public void FireUpdate() 
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                Update?.Invoke();
            });
        }

        public DownloadJob(JenkinsBase.Artifact build, int build_number)
        {
            Build = build;
            Number = build_number;
        }
    }

    public sealed class FetchJob : LogJob
    {
        public readonly int BuildNr;

        public FetchJob(int buildNr)
        {
            BuildNr = buildNr;
        }
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
    /// </remarks>
    public void Update86Box(DownloadJob job)
    {
        //Since  this is done on the UI thread, it's thread safe, as the other
        //thread will never set this false until we're off the UI thread.
        if (!IsWorking)
            IsWorking = true;
        IsUpdating = true;
        Progress = 0;

        job.AddLog($"Downloading artifact: {job.Build.FileName}");
        string url = $"{JENKINS_BASE_URL}/{job.Number}/artifact/{job.Build.RelativePath}";

        //The progress bar is split into sections. Each section is taking a percentage out
        //of one hundred.
        double[] prog = [
            26, //Download86Box
            3,  //VerifyExtract86Box
            1,  //Move86BoxToArchive
            1,  //MoveROMsToArchive
            1,  //Store86BoxToDisk
            38, //DownloadROMs
            15, //ExtractROMs
            15  //WriteROMsToDisk
        ];

        //Removes jobs that won't be done. Note, this isn't perfect, it might
        //decide to skip a job for one reason or another. But, this is what
        //it hopes to do.
        if (!job.Move86BoxToArchive)
        {
            prog[Operation.Move86BoxToArchive] = 0;
            prog[Operation.MoveROMsToArchive] = 0;
        }
        if (!job.PreserveROMs)
            prog[Operation.MoveROMsToArchive] = 0;
        if (!job.DownloadROMs)
        {
            prog[Operation.DownloadROMs] = 0;
            prog[Operation.ExtractROMs] = 0;
            prog[Operation.WriteROMsToDisk] = 0;
            prog[Operation.MoveROMsToArchive] = 0;
        }

        //Adjusts the progress bar so that it adds up to 100
        {
            double total = 0;
            foreach(var val in prog)
                total += val;

            if (total < 100)
            {
                for(int c=0; c < prog.Length; c++)
                    prog[c] = prog[c] / total * 100;
            }
        }

        var vm_exe = job.CurrentExe.VMExe;
        var calc = new ProgressCalculator(prog);
        ThreadPool.QueueUserWorkItem(async o =>
        {
            using var httpClient = GetHttpClient();

            job.AddLog("Connecting to: " + url);

            try
            {
                var zip_data = new MemoryStream();

                using (var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        job.Error("Failed to contact server");
                        return;
                    }

                    var total_bytes_to_read = response.Content.Headers.ContentLength ?? 40 * 1024 * 1024;

                    job.AddLog($"Downloading {FolderSizeCalculator.ConvertBytesToReadableSize(total_bytes_to_read)}");
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

                if (job.Build.FileName.EndsWith(".zip", StringComparison.InvariantCultureIgnoreCase))
                {
                    job.AddLog($"Finished downloading 86Box artifact - Verifying");
                    var box_files = ExtractFilesFromZip(Operation.VerifyExtract86Box, zip_data, calc);

                    if (!store_86_files(box_files, calc, job))
                    {
                        return;
                    }
                }
                else if (job.Build.FileName.EndsWith(".AppImage", StringComparison.InvariantCultureIgnoreCase)) 
                {
                    job.AddLog($"Finished downloading 86Box artifact - Verifying:");
                    try
                    {
                        zip_data.Position = 0;

                        var vi = Platforms.Manager.Get86BoxInfo(zip_data);
                        if (vi == null)
                        {
                            job.Error(" - AppImage is not valid");
                            return;
                        }

                        job.AddLog($" - 86Box version {vi.FileMajorPart}.{vi.FileMinorPart}.{vi.FileBuildPart} - Build: {vi.FilePrivatePart}");
                    } catch { job.AddLog(" - Skipping validation"); }

                    zip_data.Position = 0;
                    var ef = new ExtractedFile() { FileData = zip_data, FilePath = job.Build.FileName };
                    var l = new List<ExtractedFile>
                    {
                        new ExtractedFile() { FilePath = "", FileData = new MemoryStream() },
                        ef
                    };

                    if (!store_86_files(l, calc, job, true))
                    {
                        return;
                    }

                    //Removes the old app image.
                    if (!string.Equals(job.Build.FileName, Path.GetFileName(vm_exe)) &&
                        File.Exists(vm_exe))
                    {
                        job.AddLog("Removing: " + Path.GetFileName(vm_exe));
                        File.Delete(vm_exe);
                    }
                }
                else 
                {
                    throw new NotSupportedException(job.Build.FileName);
                }

                if (job.DownloadROMs)
                {
                    job.AddLog("");
                    job.AddLog("Downloading latest ROMs");
                    job.AddLog("Connecting to: " + ROMS_ZIP_URL);
                    //Do not reuse memory stream. Linux fails to unzip.
                    zip_data = new MemoryStream();

                    using (var response = await httpClient.GetAsync(ROMS_ZIP_URL, HttpCompletionOption.ResponseHeadersRead))
                    {
                        if (!response.IsSuccessStatusCode)
                        {
                            job.Error("Failed to contact server");
                            return;
                        }

                        var roms_bytes = response.Content.Headers.ContentLength ?? 80 * 1024 * 1024;
                        job.AddLog($"Downloading {FolderSizeCalculator.ConvertBytesToReadableSize(roms_bytes)}");

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
                    job.AddLog($"Finished downloading ROM files - Verifying");
                    zip_data.Position = 0;
                    var box_files = ExtractFilesFromZip(Operation.ExtractROMs, zip_data, calc);

                    if (!store_rom_files(box_files, calc, job))
                    {
                        return;
                    }
                }

                job.AddLog($" -- Job done -- ");
            }
            catch (Exception e)
            {
                job.Error(e.Message);
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

    private bool store_rom_files(List<ExtractedFile> files, ProgressCalculator calc, DownloadJob job)
    {
        string rom_dir = null;

        //The propper way of doing this is to collect all this info first. For now, we
        //disable the settings tab and anything that can change state while proceesing
        //is going on.
        Dispatcher.UIThread.Invoke(() =>
        {
            rom_dir = job.CurrentExe.VMRoms;
        });
        try
        {
            job.AddLog($"Writing {files.Count} ROMs to: {rom_dir}");
            Directory.CreateDirectory(rom_dir);
            int strip = 0;
            if (files.Count > 0)
            {
                strip = files[0].FilePath.Length;
            }

            for (int c = 0; c < files.Count; c++)
            {
                var file = files[c];

                if (file.FileData.Length == 0)
                    continue;

                var dest = Path.Combine(rom_dir, file.FilePath.Substring(strip));
                var dest_dir = Path.GetDirectoryName(dest);
                if (!Directory.Exists(dest_dir))
                    Directory.CreateDirectory(dest_dir);
                else if (File.Exists(dest))
                    File.Delete(dest);
                File.WriteAllBytes(dest, file.FileData.ToArray());

                Progress = calc.CalculateProgress(Download86Manager.Operation.WriteROMsToDisk, c, files.Count);
            }

            Progress = calc.CalculateProgress(Download86Manager.Operation.WriteROMsToDisk, 1, 1);

            job.AddLog($"ROMs has been updated.");
            job.FireUpdate();
            
        }
        catch (Exception e) { job.Error("Failed to update ROMs: " + e.Message); }

        return true;
    }

    private bool store_86_files(List<ExtractedFile> files, ProgressCalculator calc, DownloadJob job, bool set_executable = false)
    {
        string store_path = null;
        string vm_exe = null;
        string rom_dir = null;

        //The propper way of doing this is to collect all this info first. For now, we
        //disable the settings tab and anything that can change state while proceesing
        //is going on.
        Dispatcher.UIThread.Invoke(() =>
        {
            vm_exe = job.CurrentExe.VMExe;
            if (!File.Exists(vm_exe))
                store_path = AppSettings.Settings.EXEdir;
            else
                store_path = Path.GetDirectoryName(vm_exe);
            rom_dir = job.CurrentExe.VMRoms;
        });


        if (!string.IsNullOrEmpty(job.ArchivePath) && !string.IsNullOrEmpty(job.ArchiveName))
        {
            var path = FolderHelper.EnsureUniqueFolderName(job.ArchivePath, job.ArchiveName);

            job.AddLog($"Archiving to: {path}");

            try
            {
                //Move files
                Directory.CreateDirectory(path);
                var dir = Path.GetDirectoryName(vm_exe);
                var dir_files = Directory.GetFiles(dir);
                for (int c = 0; c < dir_files.Length; c++)
                {
                    var fname = Path.GetFileName(dir_files[c]);
                    File.Move(dir_files[c], Path.Combine(path, fname));
                    job.AddLog($" - {fname}");

                    Progress = calc.CalculateProgress(Operation.Move86BoxToArchive, c, dir_files.Length);
                }

                if (job.PreserveROMs && Directory.Exists(rom_dir))
                {
                    var dest_dir = Path.Combine(path, "roms");

                    //If we're downloading new roms, move them.
                    if (job.DownloadROMs)
                    {
                        try
                        {
                            job.AddLog("Moving ROMs to archive");
                            job.AddLog(" - Source: " + rom_dir);
                            job.AddLog(" - Dest: " + dest_dir);
                            Directory.Move(rom_dir, dest_dir);
                            Progress = calc.CalculateProgress(Download86Manager.Operation.MoveROMsToArchive, 0, 1);
                        }
                        catch { job.Error("Failed to archive roms"); }
                    }
                    else
                    {
                        //Not downloading new roms, don't archive. 
                        //try
                        //{
                        //    _m.AddToUpdateLog("Copy ROMs to archive");
                        //    _m.AddToUpdateLog(" - Source: " + rom_dir);
                        //    _m.AddToUpdateLog(" - Dest: " + dest_dir);
                        //    string[] files = Directory.GetFiles(rom_dir, "*.*", SearchOption.AllDirectories);
                        //    _m.AddToUpdateLog(" - Files to copy: " + files.Length);

                        //    Directory.CreateDirectory(dest_dir);
                        //    foreach (string file in files)
                        //    {
                        //        // Determine the destination path
                        //        string relativePath = file.Substring(rom_dir.Length + 1);
                        //        string destFile = IOPath.Combine(dest_dir, relativePath);

                        //        // Ensure the destination subdirectory exists
                        //        Directory.CreateDirectory(IOPath.GetDirectoryName(destFile));

                        //        // Copy the file
                        //        File.Copy(file, destFile, true); // true to overwrite existing files
                        //    }
                        //}
                        //catch { _m.ErrorToUpdateLog("Failed to archive roms"); }
                    }
                }

                //Adds a new entery
                Dispatcher.UIThread.Invoke(() =>
                {
                    using (var t = AppSettings.Settings.BeginTransaction())
                    {
                        string exe_name = Path.Combine((string)path, "86Box.exe");
                        AppSettings.Settings.AddExe(job.ArchiveName, exe_name, null, job.ArchiveComment, job.ArchiveVersion, job.CurrentExe.Arch, job.CurrentExe.Build, false);
                        t.Commit();
                    }

                    job.AddLog($"Entery for {job.ArchiveName} created");
                    job.AddLog($"");
                });

                Progress = calc.CalculateProgress(Download86Manager.Operation.Move86BoxToArchive, 1, 1);
            }
            catch (Exception e)
            {
                job.Error("Error: " + e.Message);
                job.Error("Failed to arhive current version - stopping.");
                return false;
            }
        }

        job.AddLog($"Writing new 86Box to: {store_path}");

        for (int c = 0; c < files.Count; c++)
        {
            Progress = calc.CalculateProgress(Download86Manager.Operation.Store86BoxToDisk, c, files.Count);

            var file = files[c];

            if (file.FileData.Length == 0)
                continue;

            var dest = Path.Combine(store_path, file.FilePath);
            var dest_dir = Path.GetDirectoryName(dest);
            if (!Directory.Exists(dest_dir))
                Directory.CreateDirectory(dest_dir);
            else if (File.Exists(dest))
                File.Delete(dest);
            job.AddLog($" - {Path.GetFileName(dest)}");
            File.WriteAllBytes(dest, file.FileData.ToArray());

            if (set_executable)
            {
                if (!Platforms.Shell.SetExecutable(dest))
                    job.Error($"Failed to set {file.FilePath} executable.");
            }
        }
        Progress = calc.CalculateProgress(Download86Manager.Operation.Store86BoxToDisk, 1, 1);

        job.AddLog($"86Box has been updated.");

        Dispatcher.UIThread.Invoke(() =>
        {
            job.CurrentExe.Build = LatestBuild.Value.ToString();
            job.CurrentExe.RaisePropertyChanged(nameof(ExeModel.Build));
            job.CurrentExe.Version = "Unknown";
            job.CurrentExe.RaisePropertyChanged(nameof(ExeModel.Version));
            job.CurrentExe.Name = "Latest";
            job.CurrentExe.RaisePropertyChanged(nameof(ExeModel.Name));
        });

        return true;
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

    public void FetchMetadata(FetchJob fjob)
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
                GithubCommit[] gjob = null;

                fjob.AddLog("Connecting to: " + ROMS_COMMITS_URL);
                using (var response = await httpClient.GetAsync(github_url))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                        {
                            IEnumerable<string> values;
                            if (response.Headers.TryGetValues("X-RateLimit-Remaining", out values))
                            {
                                foreach (var value in values)
                                {
                                    if (value == "0")
                                    {
                                        fjob.Error("GitHub rate limit exceeded for your IP address. Please try again later.");
                                        return;
                                    }
                                }
                            }
                        }
                        fjob.Error("Failed to contact server, ROM download feature will likely not function");
                    }
                    else
                    {
                        using (var json_str = await response.Content.ReadAsStreamAsync())
                        {
                            gjob = JsonSerializer.Deserialize<GithubCommit[]>(json_str);
                        }
                    }
                }

                if (gjob != null && (gjob.Length == 0 || gjob[0].Commit == null || gjob[0].Commit.Committer == null))
                {
                    fjob.Error($"Parsing failed, invalid response");
                    gjob = null;
                }

                if (gjob != null)
                {
                    var date = gjob[0].Commit.Committer.Date;
                    fjob.AddLog("ROMs last updated: " + date.ToString("d", CultureInfo.CurrentCulture));
                    LatestRomCommit = date;
                }

                fjob.AddLog("");
                

                JenkinsBuild job;
                fjob.AddLog("Connecting to: " + jenkins_url);
                using (var response = await httpClient.GetAsync(jenkins_url))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        fjob.Error("Failed to contact server");
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
                    fjob.Error($"Parsing failed, invalid response");
                    return;
                }

                //Note, SourceCache is thread safe
                Artifacts.Clear();
                Artifacts.AddOrUpdate(job.Artifacts);

                //Note, must be done after upating Artifacts.
                LatestBuild = job.Number;

                //AddLog($"Latest build is {job.Number}");
                var changelog = await FetchChangelog(job, fjob, httpClient);

                if (changelog.Count == 0)
                    fjob.AddLog($" -- There was no enteries in the changelog --");
                else if (changelog.Count == 1)
                    fjob.AddLog($" -- There was 1 entery in the changelog --");
                else
                    fjob.AddLog($" -- Changelog has {changelog.Count} enteries --");
            }
            catch (Exception e)
            {
                fjob.Error(e.Message);
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

    private async Task<List<string>> FetchChangelog(JenkinsBuild build, FetchJob fjob, HttpClient httpClient)
    {
        List<string> changelog = new List<string>();
        int from = fjob.BuildNr;

        if (from == -1)
            from = build.Number - 1;

        if (build.Number < from)
        {
            fjob.AddLog($"Skipping fetching changelog: local build newer than server build");
            return changelog;
        }

        if (build.Number == from)
        {
            fjob.AddLog($"Skipping fetching changelog: local build is same as server build");
            return changelog;
        }

        //Limit how many we fetch
        if (build.Number - from > 50)
            from = build.Number - 50;

        fjob.AddLog($"Fetching changelog going from {from} to {build.Number}");
        fjob.AddLog($" -- Changelog start --");
        for (int c = from + 1; c <= build.Number; c++)
        {
            bool sucess = false;

            try { sucess = await FetchChangelog(c, changelog, httpClient, fjob); }
            catch
            { }

            if (!sucess)
            {
                fjob.Error($"Fetching of changelog for build {c} failed, aborting");
                break;
            }
        }

        return changelog;
    }

    private async Task<bool> FetchChangelog(int build, List<string> changelog, HttpClient httpClient, FetchJob fjob)
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

        if (job.ChangeSets == null)
            return false;

        if (job.ChangeSets.Count == 0)
        {
            //Some builds have no changes
            return true;
        }

        var cs = job.ChangeSets[0];
        
        if (cs.Items != null)
        {
            foreach (var change in cs.Items)
            {
                if (!string.IsNullOrWhiteSpace(change.Msg))
                {
                    changelog.Add(change.Msg);
                    fjob.AddLog($"{change.Author?.FullName ?? "Unknown"}: {change.Msg}");
                }
            }
        }

        return true;
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
