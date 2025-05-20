using Mono.Unix.Native;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Avalonia86.Tools;

public class GithubCommit
{
    [JsonPropertyName("sha")]
    public string Sha { get; set; }

    [JsonPropertyName("node_id")]
    public string NodeId { get; set; }

    [JsonPropertyName("commit")]
    public CommitDetails Commit { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; }

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; }

    [JsonPropertyName("comments_url")]
    public string CommentsUrl { get; set; }

    [JsonPropertyName("author")]
    public User Author { get; set; }

    [JsonPropertyName("committer")]
    public User Committer { get; set; }

    [JsonPropertyName("parents")]
    public Parent[] Parents { get; set; }

    public class CommitDetails
    {
        [JsonPropertyName("author")]
        public AuthorDetails Author { get; set; }

        [JsonPropertyName("committer")]
        public AuthorDetails Committer { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; }

        [JsonPropertyName("tree")]
        public Tree Tree { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("comment_count")]
        public int CommentCount { get; set; }

        [JsonPropertyName("verification")]
        public Verification Verification { get; set; }
    }

    public class AuthorDetails
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("email")]
        public string Email { get; set; }

        [JsonPropertyName("date")]
        public DateTime Date { get; set; }
    }

    public class Tree
    {
        [JsonPropertyName("sha")]
        public string Sha { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }
    }

    public class Verification
    {
        [JsonPropertyName("verified")]
        public bool Verified { get; set; }

        [JsonPropertyName("reason")]
        public string Reason { get; set; }

        [JsonPropertyName("signature")]
        public string Signature { get; set; }

        [JsonPropertyName("payload")]
        public string Payload { get; set; }

        [JsonPropertyName("verified_at")]
        public DateTime? VerifiedAt { get; set; }
    }

    public class User
    {
        [JsonPropertyName("login")]
        public string Login { get; set; }

        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("node_id")]
        public string NodeId { get; set; }

        [JsonPropertyName("avatar_url")]
        public string AvatarUrl { get; set; }

        [JsonPropertyName("gravatar_id")]
        public string GravatarId { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; }

        [JsonPropertyName("followers_url")]
        public string FollowersUrl { get; set; }

        [JsonPropertyName("following_url")]
        public string FollowingUrl { get; set; }

        [JsonPropertyName("gists_url")]
        public string GistsUrl { get; set; }

        [JsonPropertyName("starred_url")]
        public string StarredUrl { get; set; }

        [JsonPropertyName("subscriptions_url")]
        public string SubscriptionsUrl { get; set; }

        [JsonPropertyName("organizations_url")]
        public string OrganizationsUrl { get; set; }

        [JsonPropertyName("repos_url")]
        public string ReposUrl { get; set; }

        [JsonPropertyName("events_url")]
        public string EventsUrl { get; set; }

        [JsonPropertyName("received_events_url")]
        public string ReceivedEventsUrl { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("user_view_type")]
        public string UserViewType { get; set; }

        [JsonPropertyName("site_admin")]
        public bool SiteAdmin { get; set; }
    }

    public class Parent
    {
        [JsonPropertyName("sha")]
        public string Sha { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; }
    }
}