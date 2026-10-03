namespace EHC.Web.Site;

/// <summary>
/// Which health-calendar entry is "now": the one whose month label (as editors type it, Arabic or English, full or
/// short) is the current month. Editors no longer have to move the "now" flag every month.
/// </summary>
public static class CalendarMonth
{
    // index 0 = January; Arabic Gregorian names with common spellings (alef/hamza forms are normalised below)
    private static readonly string[][] Names =
    [
        ["january", "jan", "يناير"],
        ["february", "feb", "فبراير"],
        ["march", "mar", "مارس"],
        ["april", "apr", "ابريل"],
        ["may", "مايو"],
        ["june", "jun", "يونيو", "يونيه"],
        ["july", "jul", "يوليو", "يوليه"],
        ["august", "aug", "اغسطس"],
        ["september", "sep", "sept", "سبتمبر"],
        ["october", "oct", "اكتوبر"],
        ["november", "nov", "نوفمبر"],
        ["december", "dec", "ديسمبر"],
    ];

    /// <summary>The month (1–12) a label names, or null when it isn't a month name.</summary>
    public static int? Parse(string? label)
    {
        if (string.IsNullOrWhiteSpace(label)) { return null; }
        var key = label.Trim().ToLowerInvariant().Replace('أ', 'ا').Replace('إ', 'ا').Replace('آ', 'ا').TrimEnd('.');
        for (var i = 0; i < Names.Length; i++)
        {
            if (Array.IndexOf(Names[i], key) >= 0) { return i + 1; }
        }
        return null;
    }

    /// <summary>Index of the entry for today's month, or -1 (then the editors' own "current" flags apply).</summary>
    public static int CurrentIndex(IReadOnlyList<string?> monthLabels, DateOnly today)
    {
        for (var i = 0; i < monthLabels.Count; i++)
        {
            if (Parse(monthLabels[i]) == today.Month) { return i; }
        }
        return -1;
    }
}
