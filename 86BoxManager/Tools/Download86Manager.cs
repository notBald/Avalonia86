using Avalonia.Threading;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DynamicData;

namespace _86BoxManager.Tools;

public class Download86Manager : ReactiveObject
{
    private bool _is_working, _is_fetching_log;
    private int? _latest_build;
    private HttpClient _httpClient;

    private const string ZIPFILE_86BOX = "86Box.zip";
    private const string ZIPFILE_ROMS = "Roms.zip";
    private const string JENKINS_BASE_URL = "https://ci.86box.net/job/86Box";
    private const string JENKINS_LASTBUILD = JENKINS_BASE_URL + "/lastSuccessfulBuild";

    public event Action<string> Log;

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

    public int? LatestBuild
    {
        get => _latest_build;
        private set
        {
            //Using Invoke to give the dialog a chance to read from the non
            //thread safe hashsets.
            Dispatcher.UIThread.Invoke(() =>
            {
                this.RaiseAndSetIfChanged(ref _latest_build, value);
            });
        }
    }

    public SourceCache<JenkinsBase.Artifact, string> Artifacts = new(s => s.FileName);

    public void FetchMetadata(int current_build)
    {
        IsWorking = true;
        IsFetching = true;
        _httpClient = new HttpClient();
        string url = $"{JENKINS_LASTBUILD}/api/json";
        AddLog("Fetching list of builds");

        ThreadPool.QueueUserWorkItem(async o =>
        {
            JenkinsBuild job;
            AddLog("Connecting to: "+url);
            try
            {
                using (var response = await _httpClient.GetAsync(url))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        AddLog("Failed to contact server");
                        Stop();
                        return;
                    }

                    using (var json_str = await response.Content.ReadAsStreamAsync())
                    {
                        //AddLog($"Parsing {FolderSizeCalculator.ConvertBytesToReadableSize(json_str.Length)} of data");
                        job = JsonSerializer.Deserialize<JenkinsBuild>(json_str);
                        if (job.Artifacts == null || job.Number < 6507 || job.Url == null || job.Artifacts.Count < 1)
                        {
                            AddLog($"Parsing failed, invalid response");
                            Stop();
                            return;
                        }
                    }
                }

                //Note, SourceCache is thread safe
                Artifacts.Clear();
                Artifacts.AddOrUpdate(job.Artifacts);

                //Note, must be done after upating Artifacts.
                LatestBuild = job.Number;

                //AddLog($"Latest build is {job.Number}");
                var changelog = await FetchChangelog(job, current_build);

                if (changelog.Count == 0)
                    AddLog($" -- There was no enteries in the changelog --");
                else if (changelog.Count == 1)
                    AddLog($" -- There was 1 entery in the changelog --");
                else
                    AddLog($" -- Changelog has {changelog.Count} enteries --");
            }
            finally 
            {
                Stop();
            }
        });
    }

    private async Task<List<string>> FetchChangelog(JenkinsBuild build, int from)
    {
        List<string> changelog = new List<string>();

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

            try { sucess = await FetchChangelog(c, changelog); }
            catch
            { }

            if (!sucess)
            {
                AddLog($"Fetching of changelog for build {c} failed, aborting");
                break;
            }
        }

        return changelog;
    }

    private async Task<bool> FetchChangelog(int build, List<string> changelog)
    {
        string url = $"{JENKINS_BASE_URL}/{build}/api/json";
        JenkinsRun job;
        using (var response = await _httpClient.GetAsync(url))
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

    private class DownloadWorker
    {
        public event Action<string> Aborted;
        public event Action<string> Initiated;
        public event Action<string> Extracting;
        public event Action<long, long> DownloadUpdated;
        public event Action TaskCompleted;

        private string _sourceUrl;
        private string _targetFile;
        private HttpClient _httpClient;

        public DownloadWorker(string source, string target)
        {
            _sourceUrl = source;
            _targetFile = target;
            _httpClient = new HttpClient();
        }

        public async Task Run()
        {
            if (string.IsNullOrEmpty(_sourceUrl)) throw new ArgumentException("No source url specified.");
            if (string.IsNullOrEmpty(_targetFile)) throw new ArgumentException("No target file specified.");

            Initiated?.Invoke("Please wait...");

            try
            {
                var response = await _httpClient.GetAsync(_sourceUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                var canReportProgress = totalBytes != -1;

                using (var fileStream = new FileStream(_targetFile, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var httpStream = await response.Content.ReadAsStreamAsync())
                {
                    var buffer = new byte[81920];
                    var totalRead = 0L;
                    int bytesRead;

                    while ((bytesRead = await httpStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead);
                        totalRead += bytesRead;

                        if (canReportProgress)
                        {
                            DownloadUpdated?.Invoke(totalRead, totalBytes);
                        }
                    }
                }

                Extracting?.Invoke("Extracting...");
                ExtractFiles(_targetFile);
                TaskCompleted?.Invoke();
            }
            catch (Exception ex)
            {
                Aborted?.Invoke(ex.Message);
            }
        }

        private void ExtractFiles(string filePath)
        {
            try
            {
                if (filePath.Contains(ZIPFILE_86BOX))
                {
                    ZipFile.ExtractToDirectory(filePath, ".");
                    File.Delete(filePath);
                }
                else if (filePath.Contains(ZIPFILE_ROMS))
                {
                    using (var archive = ZipFile.OpenRead(filePath))
                    {
                        foreach (var entry in archive.Entries)
                        {
                            if (entry.FullName != "roms-master/")
                            {
                                var destinationPath = Path.GetFullPath(Path.Combine(".", entry.FullName.Replace("roms-master/", "roms/")));
                                entry.ExtractToFile(destinationPath, true);
                            }
                        }
                    }
                    File.Delete(filePath);
                }
            }
            catch (Exception ex)
            {
                Aborted?.Invoke(ex.Message);
            }
        }
    }

    public sealed class JenkinsRun : JenkinsBase
    {
        // Additional properties specific to JenkinsRun can be added here
    }

    public sealed class JenkinsBuild : JenkinsBase
    {
        // Additional properties specific to JenkinsBuild can be added here
    }
}
