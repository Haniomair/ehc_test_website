namespace EHC.Web.Blocks;

/// <summary>
/// Plain-text table typed by editors: one row per line, cells separated by "|" (or tabs, when pasted from Excel).
/// Rows are padded to the widest row; blank lines are ignored. Cells are plain text and encoded by Razor.
/// </summary>
public sealed record TableData(IReadOnlyList<string> Header, IReadOnlyList<IReadOnlyList<string>> Rows)
{
    public const int MaxRows = 200;
    public const int MaxColumns = 12;

    public bool IsEmpty => Header.Count == 0 && Rows.Count == 0;

    public static TableData Parse(string? text, bool firstRowIsHeader)
    {
        if (string.IsNullOrWhiteSpace(text)) return new([], []);

        var lines = text.Replace("\r\n", "\n").Split('\n')
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Take(MaxRows + 1)
            .Select(l => (IReadOnlyList<string>)l.Split(l.Contains('\t') ? '\t' : '|').Take(MaxColumns).Select(c => c.Trim()).ToList())
            .ToList();

        var width = lines.Max(l => l.Count);
        var padded = lines.Select(l => (IReadOnlyList<string>)l.Concat(Enumerable.Repeat("", width - l.Count)).ToList()).ToList();

        return firstRowIsHeader
            ? new(padded[0], padded.Skip(1).Take(MaxRows).ToList())
            : new([], padded.Take(MaxRows).ToList());
    }
}
