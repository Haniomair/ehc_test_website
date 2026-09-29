using System.Globalization;

namespace EHC.Web.Site;

/// <summary>
/// Letter grouping for the A–Z index section. Arabic: hamza forms of alef count as ا and the article "ال" is ignored
/// (الأطفال → أطفال → ا); English: A–Z, anything else under "#". Optional generic words ("مستشفى", "مركز صحي",
/// "Hospital"…) can be skipped so facility names group by their own name rather than all under م / H.
/// </summary>
public static class AzIndex
{
    public static readonly IReadOnlyList<string> ArabicLetters =
        ["ا", "ب", "ت", "ث", "ج", "ح", "خ", "د", "ذ", "ر", "ز", "س", "ش", "ص", "ض", "ط", "ظ", "ع", "غ", "ف", "ق", "ك", "ل", "م", "ن", "ه", "و", "ي"];

    public static readonly IReadOnlyList<string> LatinLetters = Enumerable.Range('A', 26).Select(c => ((char)c).ToString()).ToList();

    public const string Other = "#";

    /// <summary>Leading words skipped for facility names (longest first).</summary>
    public static readonly IReadOnlyList<string> FacilityWords =
    [
        "مركز الرعاية الصحية الأولية", "مركز الرعاية الصحية", "مركز صحي", "مستشفى", "مجمع", "مركز",
        "Primary Health Care Center", "Health Center", "Hospital", "Medical Complex", "Complex", "Center", "Centre",
    ];

    public static IReadOnlyList<string> Letters(bool arabic) => arabic ? ArabicLetters : LatinLetters;

    /// <summary>The text used for sorting and grouping: generic leading words and the Arabic article removed.</summary>
    public static string SortText(string name, bool arabic, IReadOnlyList<string>? skipWords = null)
    {
        var s = (name ?? "").Trim();
        if (skipWords is not null)
        {
            foreach (var w in skipWords.OrderByDescending(w => w.Length))
            {
                if (s.Length > w.Length + 1 && s.StartsWith(w + " ", StringComparison.OrdinalIgnoreCase)) { s = s[(w.Length + 1)..].TrimStart(); break; }
            }
        }
        if (arabic && s.Length > 3 && s.StartsWith("ال", StringComparison.Ordinal)) s = s[2..];
        if (!arabic && s.Length > 4 && (s.StartsWith("Al-", StringComparison.OrdinalIgnoreCase) || s.StartsWith("Al ", StringComparison.OrdinalIgnoreCase))) s = s[3..];
        if (!arabic && s.Length > 4 && s.StartsWith("The ", StringComparison.OrdinalIgnoreCase)) s = s[4..];
        return s.TrimStart('(', '"', '«', ' ', '-');
    }

    /// <summary>Index letter for a name (see <see cref="SortText"/>).</summary>
    public static string LetterOf(string name, bool arabic, IReadOnlyList<string>? skipWords = null)
    {
        var s = SortText(name, arabic, skipWords);
        if (s.Length == 0) return Other;
        var c = s[0];
        if (c is 'أ' or 'إ' or 'آ' or 'ٱ' or 'ء') return "ا";
        if (c == 'ى') return "ي";
        if (c == 'ة') return "ه";
        var letter = arabic ? c.ToString() : char.ToUpperInvariant(c).ToString();
        // an English name in the Arabic list (or the other way round) still gets a group
        if (ArabicLetters.Contains(letter) || LatinLetters.Contains(letter)) return letter;
        return Other;
    }

    public sealed record Entry(string Name, string Url, string? Note);

    public sealed record Group(string Letter, IReadOnlyList<Entry> Entries);

    /// <summary>Entries grouped by letter in alphabet order (other scripts after, "#" last), sorted within each group.</summary>
    public static IReadOnlyList<Group> Build(IEnumerable<Entry> entries, bool arabic, IReadOnlyList<string>? skipWords = null)
    {
        var compare = (arabic ? new CultureInfo("ar-SA") : CultureInfo.InvariantCulture).CompareInfo;
        var own = Letters(arabic);
        var foreign = arabic ? LatinLetters : ArabicLetters;
        int Rank(string letter) => letter == Other ? 1000 : own.Contains(letter) ? own.ToList().IndexOf(letter) : 100 + foreign.ToList().IndexOf(letter);

        return entries
            .GroupBy(e => LetterOf(e.Name, arabic, skipWords))
            .OrderBy(g => Rank(g.Key))
            .Select(g => new Group(g.Key, g.OrderBy(e => SortText(e.Name, arabic, skipWords), Comparer<string>.Create((a, b) => compare.Compare(a, b, CompareOptions.IgnoreCase))).ToList()))
            .ToList();
    }
}
