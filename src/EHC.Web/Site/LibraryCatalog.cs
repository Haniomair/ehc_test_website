using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Extensions;

namespace EHC.Web.Site;

/// <summary>
/// Health library: body systems (body map and filters), audiences, the fixed section order of condition and test pages,
/// and search that matches names, "also known as" words and summaries the way people type them (Arabic letter forms
/// and diacritics ignored). Names are dictionary items EHC.Library.System.{key} / EHC.Library.Audience.{key}.
/// </summary>
public static partial class LibraryCatalog
{
    /// <summary>A body system; X/Y: its point on the body map in % of the figure (null: listed beside the figure).</summary>
    public sealed record BodySystem(string Key, string Icon, double? X = null, double? Y = null)
    {
        public bool OnMap => X is not null && Y is not null;
    }

    /// <summary>Stored keys of the "EHC - Body systems" dropdown, in display order.</summary>
    public static readonly IReadOnlyList<BodySystem> Systems =
    [
        new("brain", "brain", 50, 6.8),
        new("lungs", "lungs", 42, 26.4),
        new("heart", "heart", 56.5, 29.2),
        new("digestive", "stomach", 56, 37.8),
        new("kidneys", "kidney", 43.5, 40.6),
        new("reproductive", "female", 50, 48.6),
        new("bones", "bone", 39, 75),
        new("eyes", "eye"),
        new("ent", "ear"),
        new("mental", "mind"),
        new("hormones", "hormone"),
        new("blood", "drop"),
        new("skin", "skin"),
        new("infections", "virus"),
        new("cancer", "onc"),
    ];

    public static readonly IReadOnlyList<string> Audiences = ["children", "women", "men", "elderly", "pregnancy"];

    public static readonly IReadOnlyDictionary<string, string> AudienceIcons = new Dictionary<string, string>
    {
        ["children"] = "baby", ["women"] = "female", ["men"] = "users", ["elderly"] = "hand", ["pregnancy"] = "heart",
    };

    /// <summary>Section kinds ("EHC - Library section kind") and the page type each lists.</summary>
    public static readonly IReadOnlyDictionary<string, string> KindTypes = new Dictionary<string, string>
    {
        ["conditions"] = "healthCondition", ["tests"] = "healthTest", ["living"] = "healthArticle",
    };

    /// <summary>Condition page sections in order: property alias and dictionary key (EHC.Library.Section.{key}).
    /// "help" is the When to get help block, placed after Symptoms.</summary>
    public static readonly IReadOnlyList<string> ConditionSections = ["overview", "symptoms", "help", "causes", "diagnosis", "treatment", "prevention", "livingWith"];

    public static readonly IReadOnlyList<string> TestSections = ["about", "why", "prepare", "during", "results", "risks"];

    public static BodySystem? System(string? key) => Systems.FirstOrDefault(s => s.Key == key);

    public static string IconFor(string? systemKey) => System(systemKey)?.Icon ?? "book";

    /// <summary>Values of a multiple dropdown, limited to the known keys.</summary>
    public static IReadOnlyList<string> SystemsOf(IPublishedContent page) =>
        Keys(page, "bodySystems").Where(k => System(k) is not null).ToList();

    public static IReadOnlyList<string> AudiencesOf(IPublishedContent page) =>
        Keys(page, "audiences").Where(Audiences.Contains).ToList();

    private static IEnumerable<string> Keys(IPublishedContent page, string alias) =>
        page.HasProperty(alias) ? page.Value<IEnumerable<string>>(alias) ?? [] : [];

    /// <summary>Non-empty, trimmed lines of a text area ("one item per line").</summary>
    public static IReadOnlyList<string> Lines(string? text) =>
        (text ?? "").Split('\n').Select(l => l.Trim().TrimStart('-', '•', '*').Trim()).Where(l => l.Length > 0).ToList();

    /// <summary>"Also known as" words, split on commas (Arabic or Latin).</summary>
    public static IReadOnlyList<string> AlsoKnownAs(IPublishedContent page) =>
        (page.Value<string>("alsoKnownAs") ?? "").Split([',', '،', ';', '؛'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    [GeneratedRegex(@"[ً-ْـ]")]
    private static partial Regex ArabicMarks();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    /// <summary>Lower case, Arabic diacritics and tatweel removed, alef / taa marbuta / alef maqsura forms unified.</summary>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var s = ArabicMarks().Replace(text.Trim().ToLowerInvariant(), "");
        var b = new StringBuilder(s.Length);
        foreach (var c in s)
        {
            b.Append(c switch { 'أ' or 'إ' or 'آ' or 'ٱ' => 'ا', 'ة' => 'ه', 'ى' => 'ي', _ => c });
        }
        return Spaces().Replace(b.ToString(), " ");
    }

    /// <summary>Search match on the name, "also known as" and the summary (intro). Also matches without the Arabic article.</summary>
    public static int Score(IPublishedContent page, string normalizedQuery)
    {
        if (normalizedQuery.Length == 0) return 0;
        var q = normalizedQuery.StartsWith("ال", StringComparison.Ordinal) && normalizedQuery.Length > 3 ? normalizedQuery[2..] : normalizedQuery;
        var name = Normalize(page.Name);
        if (name.StartsWith(normalizedQuery, StringComparison.Ordinal) || name.Contains(" " + normalizedQuery, StringComparison.Ordinal)) return 4;
        if (name.Contains(q, StringComparison.Ordinal)) return 3;
        if (AlsoKnownAs(page).Any(a => Normalize(a).Contains(q, StringComparison.Ordinal))) return 2;
        return Normalize(page.Value<string>("intro")).Contains(q, StringComparison.Ordinal) ? 1 : 0;
    }

    /// <summary>Library pages below <paramref name="library"/> matching the query, best first.</summary>
    public static IReadOnlyList<IPublishedContent> Search(IPublishedContent library, string? query, int max = 50)
    {
        var q = Normalize(query);
        if (q.Length < 2) return [];
        var compare = CultureInfo.CurrentUICulture.CompareInfo;
        return Items(library)
            .Select(p => (p, s: Score(p, q)))
            .Where(x => x.s > 0)
            .OrderByDescending(x => x.s).ThenBy(x => x.p.Name, Comparer<string?>.Create((a, b) => compare.Compare(a, b, CompareOptions.IgnoreCase)))
            .Take(max).Select(x => x.p).ToList();
    }

    /// <summary>Every visible condition, test and article in the library (any depth).</summary>
    public static IEnumerable<IPublishedContent> Items(IPublishedContent library) =>
        library.Descendants().Where(d => d.IsVisible() && KindTypes.Values.Contains(d.ContentType.Alias));

    /// <summary>The section kind of a page type ("conditions", "tests", "living").</summary>
    public static string KindOf(IPublishedContent page) =>
        KindTypes.FirstOrDefault(k => k.Value == page.ContentType.Alias).Key ?? "living";

    /// <summary>The library's section of a kind, if there is one.</summary>
    public static IPublishedContent? Section(IPublishedContent library, string kind) =>
        library.Children().FirstOrDefault(c => c.ContentType.Alias == "healthLibrarySection" && c.Value<string>("sectionKind") == kind && c.IsVisible());
}
