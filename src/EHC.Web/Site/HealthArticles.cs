using System.Text.RegularExpressions;

namespace EHC.Web.Site;

/// <summary>Health library (patient education articles): topic keys and reading time.</summary>
public static partial class HealthArticles
{
    /// <summary>Topic keys stored by the "EHC - Health topic" dropdown; names are dictionary items EHC.Health.Topic.{key}.</summary>
    public static readonly IReadOnlyList<string> Topics =
        ["chronic", "women", "children", "nutrition", "mental", "medicines", "vaccination", "elderly", "seasonal", "prevention"];

    public static readonly IReadOnlyDictionary<string, string> TopicIcons = new Dictionary<string, string>
    {
        ["chronic"] = "heart", ["women"] = "users", ["children"] = "baby", ["nutrition"] = "flame", ["mental"] = "bulb",
        ["medicines"] = "lab", ["vaccination"] = "shield", ["elderly"] = "hand", ["seasonal"] = "sun", ["prevention"] = "target",
    };

    public static string IconFor(string? topic) => topic is not null && TopicIcons.TryGetValue(topic, out var i) ? i : "book";

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tags();

    [GeneratedRegex(@"\S+")]
    private static partial Regex Words();

    /// <summary>Minutes to read the article body (HTML), at 180 words a minute; at least 1.</summary>
    public static int ReadingMinutes(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return 1;
        var words = Words().Count(Tags().Replace(html, " "));
        return Math.Max(1, (int)Math.Round(words / 180.0, MidpointRounding.AwayFromZero));
    }
}
