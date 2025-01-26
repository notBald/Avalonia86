using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Avalonia86.Tools;

public abstract class JenkinsBase
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

        public override string ToString()
        {
            return FileName;
        }
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
