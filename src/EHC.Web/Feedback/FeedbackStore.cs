using System.Text.RegularExpressions;
using Umbraco.Cms.Infrastructure.Scoping;

namespace EHC.Web.Feedback;

public sealed class FeedbackOptions
{
    /// <summary>Answers older than this are deleted daily.</summary>
    public int RetentionMonths { get; set; } = 12;
}

public sealed class PageScore
{
    public Guid PageKey { get; set; }
    public int Yes { get; set; }
    public int No { get; set; }
}

public sealed class ReasonCount
{
    public Guid PageKey { get; set; }
    public string Reason { get; set; } = "";
    public int Count { get; set; }
}

public interface IFeedbackStore
{
    Task AddAsync(FeedbackDto answer);
    IReadOnlyList<PageScore> Scores(DateTime sinceUtc);
    IReadOnlyList<ReasonCount> Reasons(DateTime sinceUtc);
    IReadOnlyList<FeedbackDto> Comments(DateTime sinceUtc, Guid? pageKey, int take);
    IReadOnlyList<FeedbackDto> All(DateTime sinceUtc);
    int DeleteOlderThan(DateTime cutoffUtc);
}

public sealed class FeedbackStore(IScopeProvider scopes) : IFeedbackStore
{
    public async Task AddAsync(FeedbackDto answer)
    {
        using var scope = scopes.CreateScope();
        await scope.Database.InsertAsync(answer);
        scope.Complete();
    }

    public IReadOnlyList<PageScore> Scores(DateTime sinceUtc)
    {
        using var scope = scopes.CreateScope(autoComplete: true);
        return scope.Database.Fetch<PageScore>(
            $"SELECT pageKey AS PageKey, SUM(CASE WHEN helpful = 1 THEN 1 ELSE 0 END) AS Yes, SUM(CASE WHEN helpful = 1 THEN 0 ELSE 1 END) AS No " +
            $"FROM {FeedbackDto.Table} WHERE createdUtc >= @0 GROUP BY pageKey", sinceUtc);
    }

    public IReadOnlyList<ReasonCount> Reasons(DateTime sinceUtc)
    {
        using var scope = scopes.CreateScope(autoComplete: true);
        return scope.Database.Fetch<ReasonCount>(
            $"SELECT pageKey AS PageKey, reason AS Reason, COUNT(*) AS Count FROM {FeedbackDto.Table} " +
            $"WHERE createdUtc >= @0 AND reason IS NOT NULL GROUP BY pageKey, reason", sinceUtc);
    }

    public IReadOnlyList<FeedbackDto> Comments(DateTime sinceUtc, Guid? pageKey, int take)
    {
        using var scope = scopes.CreateScope(autoComplete: true);
        var sql = scope.SqlContext.Sql().SelectAll().From<FeedbackDto>()
            .Where<FeedbackDto>(x => x.CreatedUtc >= sinceUtc && x.Comment != null);
        if (pageKey is { } key) sql = sql.Where<FeedbackDto>(x => x.PageKey == key);
        sql = sql.OrderByDescending<FeedbackDto>(x => x.CreatedUtc);
        return scope.Database.SkipTake<FeedbackDto>(0, Math.Clamp(take, 1, 500), sql);
    }

    public IReadOnlyList<FeedbackDto> All(DateTime sinceUtc)
    {
        using var scope = scopes.CreateScope(autoComplete: true);
        var sql = scope.SqlContext.Sql().SelectAll().From<FeedbackDto>()
            .Where<FeedbackDto>(x => x.CreatedUtc >= sinceUtc)
            .OrderByDescending<FeedbackDto>(x => x.CreatedUtc);
        return scope.Database.Fetch<FeedbackDto>(sql);
    }

    public int DeleteOlderThan(DateTime cutoffUtc)
    {
        using var scope = scopes.CreateScope();
        var n = scope.Database.Execute($"DELETE FROM {FeedbackDto.Table} WHERE createdUtc < @0", cutoffUtc);
        scope.Complete();
        return n;
    }
}

/// <summary>Input rules for answers: fixed reasons, and comments trimmed, shortened and stripped of contact / id numbers.</summary>
public static partial class FeedbackText
{
    public const int MaxComment = 250;
    public const string Removed = "[…]";
    public static readonly string[] Reasons = ["outdated", "unclear", "missing", "broken", "other"];

    public static string? Reason(string? value) => value is not null && Reasons.Contains(value) ? value : null;

    /// <summary>
    /// Cleans a visitor comment. Visitors may type phone, national id or medical record numbers or e-mail addresses even
    /// when asked not to: runs of 6+ digits (any script, with spaces / dashes) and e-mail addresses are removed.
    /// </summary>
    public static string? Comment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var s = new string(value.Where(ch => !char.IsControl(ch) || ch == '\n').ToArray());
        s = Whitespace().Replace(s, " ").Trim();
        s = Email().Replace(s, Removed);
        s = LongNumber().Replace(s, Removed);
        if (s.Length > MaxComment) s = s[..MaxComment].TrimEnd() + "…";
        return s.Length == 0 ? null : s;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"[^\s@]+@[^\s@]+\.[^\s@]+")]
    private static partial Regex Email();

    [GeneratedRegex(@"\+?\p{Nd}(?:[\s\-\.]?\p{Nd}){5,}")]
    private static partial Regex LongNumber();
}
