using Avalonia.Threading;
using ReactiveUI;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
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

    public event Action<string> Log;

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
            Dispatcher.UIThread.Post(() =>
            {
                this.RaiseAndSetIfChanged(ref _latest_build, value);
            });
        }
    }

    public void FetchMetadata(int current_build)
    {
        IsWorking = true;
        IsFetching = true;
        _httpClient = new HttpClient();
        string url = $"{JENKINS_BASE_URL}/api/json";
        AddLog("Fetching list of builds");

        ThreadPool.QueueUserWorkItem(async o =>
        {
            JenkinsJob job;
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
                        job = JsonSerializer.Deserialize<JenkinsJob>(json_str);
                        if (job.LastSuccessfulBuild == null || job.LastSuccessfulBuild.Number < 6505 || job.LastSuccessfulBuild.Url == null)
                        {
                            AddLog($"Parsing failed, invalid response");
                            Stop();
                            return;
                        }
                        Debug.WriteLine(job.Builds.Count);
                    }
                }

                LatestBuild = job.LastCompletedBuild.Number;
                //AddLog($"Latest build is {job.LastSuccessfulBuild.Number}");
                var changelog = await FetchChangelog(job.LastSuccessfulBuild, current_build);

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

    private async Task<List<string>> FetchChangelog(JenkinsJob.Build build, int from)
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

    private class JenkinsJob
    {
        [JsonPropertyName("_class")]
        public string Class { get; set; }

        [JsonPropertyName("actions")]
        public List<JsonAction> Actions { get; set; }
        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; }

        [JsonPropertyName("fullDisplayName")]
        public string FullDisplayName { get; set; }

        [JsonPropertyName("fullName")]
        public string FullName { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("buildable")]
        public bool Buildable { get; set; }

        [JsonPropertyName("builds")]
        public List<Build> Builds { get; set; }

        [JsonPropertyName("color")]
        public string Color { get; set; }

        [JsonPropertyName("firstBuild")]
        public Build FirstBuild { get; set; }

        [JsonPropertyName("healthReport")]
        public List<HealthReport> healthReport { get; set; }

        [JsonPropertyName("keepDependencies")]
        public bool KeepDependencies { get; set; }

        [JsonPropertyName("lastBuild")]
        public Build LastBuild { get; set; }

        [JsonPropertyName("lastCompletedBuild")]
        public Build LastCompletedBuild { get; set; }

        [JsonPropertyName("lastFailedBuild")]
        public Build LastFailedBuild { get; set; }

        [JsonPropertyName("lastStableBuild")]
        public Build LastStableBuild { get; set; }

        [JsonPropertyName("lastSuccessfulBuild")]
        public Build LastSuccessfulBuild { get; set; }

        [JsonPropertyName("lastUnstableBuild")]
        public Build LastUnstableBuild { get; set; }

        [JsonPropertyName("lastUnsuccessfulBuild")]
        public Build LastUnsuccessfulBuild { get; set; }

        [JsonPropertyName("nextBuildNumber")]
        public int NextBuildNumber { get; set; }

        [JsonPropertyName("property")]
        public List<ParameterDefinitions> Property { get; set; }

        [JsonPropertyName("concurrentBuild")]
        public bool ConcurrentBuild { get; set; }

        [JsonPropertyName("inQueue")]
        public bool InQueue { get; set; }

        [JsonPropertyName("queueItem")]
        public object QueueItem { get; set; }

        [JsonPropertyName("resumeBlocked")]
        public bool ResumeBlocked { get; set; }

        public class JsonAction
        {
            [JsonPropertyName("_class")]
            public string Class { get; set; }
        }

        public class Build
        {
            [JsonPropertyName("_class")]
            public string Class { get; set; }

            [JsonPropertyName("number")]
            public int Number { get; set; }

            [JsonPropertyName("url")]
            public string Url { get; set; }
        }

        public class HealthReport
        {
            [JsonPropertyName("description")]
            public string Description { get; set; }

            [JsonPropertyName("iconClassName")]
            public string IconClassName { get; set; }

            [JsonPropertyName("iconUrl")]
            public string IconUrl { get; set; }

            [JsonPropertyName("score")]
            public int Score { get; set; }
        }

        public class ParameterDefinitions
        {
            [JsonPropertyName("_class")]
            public string Class { get; set; }

            [JsonPropertyName("parameterDefinitions")]
            public List<ParameterDefinition> parameterDefinitions { get; set; }
        }

        public class ParameterDefinition
        {
            [JsonPropertyName("_class")]
            public string Class { get; set; }

            [JsonPropertyName("defaultParameterValue")]
            public DefaultParameterValue DefaultParameterValue { get; set; }

            [JsonPropertyName("description")]
            public string Description { get; set; }

            [JsonPropertyName("name")]
            public string Name { get; set; }

            [JsonPropertyName("type")]
            public string Type { get; set; }
        }

        public class DefaultParameterValue
        {
            [JsonPropertyName("_class")]
            public string Class { get; set; }

            [JsonPropertyName("name")]
            public string Name { get; set; }

            [JsonPropertyName("value")]
            public string Value { get; set; }
        }
    }

    private class JenkinsRun
    {
        [JsonPropertyName("_class")]
        public string Class { get; set; }

        [JsonPropertyName("actions")]
        public List<Action> Actions { get; set; }

        [JsonPropertyName("artifacts")]
        public List<Artifact> Artifacts { get; set; }

        [JsonPropertyName("building")]
        public bool Building { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; }

        [JsonPropertyName("duration")]
        public long Duration { get; set; }

        [JsonPropertyName("estimatedDuration")]
        public long EstimatedDuration { get; set; }

        [JsonPropertyName("executor")]
        public object Executor { get; set; }

        [JsonPropertyName("fullDisplayName")]
        public string FullDisplayName { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("keepLog")]
        public bool KeepLog { get; set; }

        [JsonPropertyName("number")]
        public int Number { get; set; }

        [JsonPropertyName("queueId")]
        public int QueueId { get; set; }

        [JsonPropertyName("result")]
        public string Result { get; set; }

        [JsonPropertyName("timestamp")]
        public long Timestamp { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("changeSets")]
        public List<ChangeSet> ChangeSets { get; set; }

        [JsonPropertyName("culprits")]
        public List<Culprit> Culprits { get; set; }

        [JsonPropertyName("inProgress")]
        public bool InProgress { get; set; }

        [JsonPropertyName("nextBuild")]
        public BuildReference NextBuild { get; set; }

        [JsonPropertyName("previousBuild")]
        public BuildReference PreviousBuild { get; set; }

        public class Action
        {
            [JsonPropertyName("_class")]
            public string Class { get; set; }

            [JsonPropertyName("causes")]
            public List<Cause> Causes { get; set; }

            [JsonPropertyName("parameters")]
            public List<Parameter> Parameters { get; set; }

            [JsonPropertyName("buildsByBranchName")]
            public Dictionary<string, Build> BuildsByBranchName { get; set; }

            [JsonPropertyName("lastBuiltRevision")]
            public Revision LastBuiltRevision { get; set; }

            [JsonPropertyName("remoteUrls")]
            public List<string> RemoteUrls { get; set; }

            [JsonPropertyName("scmName")]
            public string ScmName { get; set; }
        }

        public class Cause
        {
            [JsonPropertyName("_class")]
            public string Class { get; set; }

            [JsonPropertyName("shortDescription")]
            public string ShortDescription { get; set; }
        }

        public class Parameter
        {
            [JsonPropertyName("_class")]
            public string Class { get; set; }

            [JsonPropertyName("name")]
            public string Name { get; set; }

            [JsonPropertyName("value")]
            public string Value { get; set; }
        }

        public class Build
        {
            [JsonPropertyName("_class")]
            public string Class { get; set; }

            [JsonPropertyName("buildNumber")]
            public int BuildNumber { get; set; }

            [JsonPropertyName("buildResult")]
            public object BuildResult { get; set; }

            [JsonPropertyName("marked")]
            public Revision Marked { get; set; }

            [JsonPropertyName("revision")]
            public Revision Revision { get; set; }
        }

        public class Revision
        {
            [JsonPropertyName("SHA1")]
            public string Sha1 { get; set; }

            [JsonPropertyName("branch")]
            public List<Branch> Branch { get; set; }
        }

        public class Branch
        {
            [JsonPropertyName("SHA1")]
            public string Sha1 { get; set; }

            [JsonPropertyName("name")]
            public string Name { get; set; }
        }

        public class Artifact
        {
            [JsonPropertyName("displayPath")]
            public string DisplayPath { get; set; }

            [JsonPropertyName("fileName")]
            public string FileName { get; set; }

            [JsonPropertyName("relativePath")]
            public string RelativePath { get; set; }
        }

        public class ChangeSet
        {
            [JsonPropertyName("_class")]
            public string Class { get; set; }

            [JsonPropertyName("items")]
            public List<ChangeSetItem> Items { get; set; }

            [JsonPropertyName("kind")]
            public string Kind { get; set; }
        }

        public class ChangeSetItem
        {
            [JsonPropertyName("_class")]
            public string Class { get; set; }

            [JsonPropertyName("affectedPaths")]
            public List<string> AffectedPaths { get; set; }

            [JsonPropertyName("commitId")]
            public string CommitId { get; set; }

            [JsonPropertyName("timestamp")]
            public long Timestamp { get; set; }

            [JsonPropertyName("author")]
            public Author Author { get; set; }

            [JsonPropertyName("authorEmail")]
            public string AuthorEmail { get; set; }

            [JsonPropertyName("comment")]
            public string Comment { get; set; }

            [JsonPropertyName("date")]
            public string Date { get; set; }

            [JsonPropertyName("id")]
            public string Id { get; set; }

            [JsonPropertyName("msg")]
            public string Msg { get; set; }

            [JsonPropertyName("paths")]
            public List<Path> Paths { get; set; }
        }

        public class Author
        {
            [JsonPropertyName("absoluteUrl")]
            public string AbsoluteUrl { get; set; }

            [JsonPropertyName("fullName")]
            public string FullName { get; set; }
        }

        public class Path
        {
            [JsonPropertyName("editType")]
            public string EditType { get; set; }

            [JsonPropertyName("file")]
            public string File { get; set; }
        }

        public class Culprit
        {
            [JsonPropertyName("absoluteUrl")]
            public string AbsoluteUrl { get; set; }

            [JsonPropertyName("fullName")]
            public string FullName { get; set; }
        }

        public class BuildReference
        {
            [JsonPropertyName("number")]
            public int Number { get; set; }

            [JsonPropertyName("url")]
            public string Url { get; set; }
        }
    }
}
