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
    private double _current_progress;
    private bool _is_working, _is_fetching_log, _is_updating;
    private int? _latest_build;
    private HttpClient _httpClient;

    private const string JENKINS_BASE_URL = "https://ci.86box.net/job/86Box";
    private const string JENKINS_LASTBUILD = JENKINS_BASE_URL + "/lastSuccessfulBuild";
    private const string ZIPFILE_ROMS = "Roms.zip";
    private const string ROMS_COMMITS_URL = "https://api.github.com/repos/86Box/roms/commits";

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

    public double Progress
    {
        get => _current_progress;
        set
        {
            Dispatcher.UIThread.Post(() => this.RaiseAndSetIfChanged(ref _current_progress, value));
        }
    }

    public SourceCache<JenkinsBase.Artifact, string> Artifacts = new(s => s.FileName);

    public void Update86Box(JenkinsBase.Artifact build, int number, bool update_roms)
    {
        IsWorking = true;
        IsUpdating = true;
        Progress = 0;
        if (_httpClient == null)
            _httpClient = new HttpClient();
        AddLog($"Downloading artifact: {build.FileName}");
        string url = $"{JENKINS_BASE_URL}/{number}/artifact/{build.RelativePath}";

        ThreadPool.QueueUserWorkItem(async o =>
        {
            AddLog("Connecting to: " + url);
            long total_bytes_read = 0;
            long bytes_to_read;
            long estimated_bytes_to_read;

            try
            {
                var artifact_zip_data = new MemoryStream();

                using (var response = await _httpClient.GetAsync(url))
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
                            artifact_zip_data.Write(buffer, 0, bytes_read);
                            total_bytes_read += bytes_read;
                            Progress = (double)total_bytes_read / estimated_bytes_to_read;
                        }
                    }
                }

                AddLog($"Finished downloading 86Box artifact");

                if (update_roms)
                {
                    AddLog("");
                    AddLog("Downloading latest ROMs");
                }
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
                        Error("Failed to contact server");
                        Stop();
                        return;
                    }

                    using (var json_str = await response.Content.ReadAsStreamAsync())
                    {
                        //AddLog($"Parsing {FolderSizeCalculator.ConvertBytesToReadableSize(json_str.Length)} of data");
                        job = JsonSerializer.Deserialize<JenkinsBuild>(json_str);
                        if (job.Artifacts == null || job.Number < 6507 || job.Url == null || job.Artifacts.Count < 1)
                        {
                            Error($"Parsing failed, invalid response");
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
                Error($"Fetching of changelog for build {c} failed, aborting");
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
}
