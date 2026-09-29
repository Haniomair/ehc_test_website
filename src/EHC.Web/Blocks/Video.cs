using System.Text.RegularExpressions;

namespace EHC.Web.Blocks;

public static partial class Video
{
    /// <summary>
    /// The 11-character video id from a YouTube link (watch, youtu.be, shorts, embed, live) or a bare id.
    /// Anything else returns null, so an editor typo never reaches the page as a frame source.
    /// </summary>
    public static string? YouTubeId(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var s = input.Trim();
        if (IdOnly().IsMatch(s)) return s;
        if (!Uri.TryCreate(s, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http")) return null;

        var host = uri.Host.ToLowerInvariant();
        if (host.StartsWith("www.")) host = host[4..];
        if (host.StartsWith("m.")) host = host[2..];

        string? id = null;
        if (host == "youtu.be")
        {
            id = uri.AbsolutePath.Trim('/');
        }
        else if (host is "youtube.com" or "youtube-nocookie.com")
        {
            var parts = uri.AbsolutePath.Trim('/').Split('/');
            if (parts is ["watch"])
            {
                id = System.Web.HttpUtility.ParseQueryString(uri.Query)["v"];
            }
            else if (parts.Length == 2 && parts[0] is "embed" or "shorts" or "live" or "v")
            {
                id = parts[1];
            }
        }
        return id is not null && IdOnly().IsMatch(id) ? id : null;
    }

    [GeneratedRegex("^[A-Za-z0-9_-]{11}$")]
    private static partial Regex IdOnly();
}
