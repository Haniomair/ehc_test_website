using System.Text.RegularExpressions;
using NPoco;
using Umbraco.Cms.Infrastructure.Migrations;
using Umbraco.Cms.Infrastructure.Persistence.DatabaseAnnotations;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Extensions;

namespace EHC.Web.Stats;

/// <summary>
/// One click, from visitors who accepted optional cookies only: the clicked element (a structural path such as
/// "#acc-1a2b3c4d>button:nth-of-type(2)"), where inside it (percent of its width and height) and the layout (device by
/// viewport width). No visitor id, nothing typed, no text of the page.
/// </summary>
[TableName(Table)]
[PrimaryKey("id", AutoIncrement = true)]
[ExplicitColumns]
public sealed class StatsClickDto
{
    public const string Table = "ehcStatsClick";

    [Column("id")]
    [PrimaryKeyColumn(AutoIncrement = true)]
    public int Id { get; set; }

    [Column("createdUtc")]
    [Index(IndexTypes.NonClustered, Name = "IX_ehcStatsClick_createdUtc")]
    public DateTime CreatedUtc { get; set; }

    [Column("pageKey")]
    [Index(IndexTypes.NonClustered, Name = "IX_ehcStatsClick_pageKey")]
    public Guid PageKey { get; set; }

    [Column("culture")]
    [Length(10)]
    public string Culture { get; set; } = "";

    [Column("device")]
    [Length(10)]
    public string Device { get; set; } = "";

    [Column("selector")]
    [Length(300)]
    public string Selector { get; set; } = "";

    /// <summary>0–100 from the element's left edge.</summary>
    [Column("x")]
    public int X { get; set; }

    /// <summary>0–100 from the element's top edge.</summary>
    [Column("y")]
    public int Y { get; set; }
}

/// <summary>How far down one page view got (percent of the page height that was on screen at some point).</summary>
[TableName(Table)]
[PrimaryKey("id", AutoIncrement = true)]
[ExplicitColumns]
public sealed class StatsScrollDto
{
    public const string Table = "ehcStatsScroll";

    [Column("id")]
    [PrimaryKeyColumn(AutoIncrement = true)]
    public int Id { get; set; }

    [Column("createdUtc")]
    [Index(IndexTypes.NonClustered, Name = "IX_ehcStatsScroll_createdUtc")]
    public DateTime CreatedUtc { get; set; }

    [Column("pageKey")]
    [Index(IndexTypes.NonClustered, Name = "IX_ehcStatsScroll_pageKey")]
    public Guid PageKey { get; set; }

    [Column("culture")]
    [Length(10)]
    public string Culture { get; set; } = "";

    [Column("device")]
    [Length(10)]
    public string Device { get; set; } = "";

    [Column("depth")]
    public int Depth { get; set; }
}

public sealed class AddStatsHeatTables(IMigrationContext context) : AsyncMigrationBase(context)
{
    protected override Task MigrateAsync()
    {
        if (!TableExists(StatsClickDto.Table))
        {
            Create.Table<StatsClickDto>().Do();
        }
        if (!TableExists(StatsScrollDto.Table))
        {
            Create.Table<StatsScrollDto>().Do();
        }
        return Task.CompletedTask;
    }
}

public static partial class HeatRules
{
    public const int MaxClicksPerView = 50;
    /// <summary>Scroll reach is reported in steps of this many percent (0, 5, …, 100).</summary>
    public const int ReachStep = 5;

    // "body" or "#id", then up to 12 ">tag:nth-of-type(n)" steps — exactly what components/heat.js builds
    [GeneratedRegex(@"^(body|#[A-Za-z][A-Za-z0-9_-]{0,80})(>[a-z][a-z0-9-]{0,30}:nth-of-type\([1-9][0-9]{0,2}\)){0,12}$")]
    private static partial Regex SelectorPattern();

    public static bool ValidSelector(string? selector) => selector is { Length: <= 300 } && SelectorPattern().IsMatch(selector);

    /// <summary>Share of page views (percent) that reached each depth 0, 5, …, 100, from the views per exact depth.</summary>
    public static int[] Reach(IReadOnlyDictionary<int, int> viewsByDepth)
    {
        var total = viewsByDepth.Values.Sum();
        var reach = new int[100 / ReachStep + 1];
        if (total == 0) return reach;
        for (var i = 0; i < reach.Length; i++)
        {
            var depth = i * ReachStep;
            reach[i] = (int)Math.Round(100.0 * viewsByDepth.Where(v => v.Key >= depth).Sum(v => v.Value) / total);
        }
        return reach;
    }
}

public sealed record HeatClick(string Selector, int X, int Y, int Count);
public sealed record HeatPage(Guid PageKey, string Culture, int Views, int Clicks);

public interface IStatsHeat
{
    void Add(StatsScrollDto? scroll, IReadOnlyCollection<StatsClickDto> clicks);
    IReadOnlyList<HeatPage> Pages(DateTime fromUtc, DateTime toUtc, int take);
    IReadOnlyList<HeatClick> Clicks(Guid pageKey, string culture, string device, DateTime fromUtc, DateTime toUtc, int take);
    IReadOnlyDictionary<int, int> Depths(Guid pageKey, string culture, string device, DateTime fromUtc, DateTime toUtc);
    int DeleteBefore(DateTime cutoffUtc);
}

public sealed class StatsHeat(IScopeProvider scopes) : IStatsHeat
{
    private const string Range = "createdUtc >= @0 AND createdUtc < @1";
    private const string Page = "pageKey = @2 AND culture = @3 AND device = @4";

    public void Add(StatsScrollDto? scroll, IReadOnlyCollection<StatsClickDto> clicks)
    {
        using var scope = scopes.CreateScope();
        if (scroll is not null) scope.Database.Insert(scroll);
        if (clicks.Count > 0) scope.Database.InsertBulk(clicks);
        scope.Complete();
    }

    public IReadOnlyList<HeatPage> Pages(DateTime fromUtc, DateTime toUtc, int take)
    {
        using var scope = scopes.CreateScope(autoComplete: true);
        var db = scope.Database;
        var clicks = db.Fetch<PageCount>(
                $"SELECT pageKey AS PageKey, culture AS Culture, COUNT(*) AS N FROM {StatsClickDto.Table} WHERE {Range} GROUP BY pageKey, culture", fromUtc, toUtc)
            .ToDictionary(r => (r.PageKey, r.Culture), r => r.N);
        return db.Fetch<PageCount>(
                $"SELECT pageKey AS PageKey, culture AS Culture, COUNT(*) AS N FROM {StatsScrollDto.Table} WHERE {Range} GROUP BY pageKey, culture", fromUtc, toUtc)
            .Select(r => new HeatPage(r.PageKey, r.Culture, r.N, clicks.GetValueOrDefault((r.PageKey, r.Culture))))
            .OrderByDescending(p => p.Views)
            .Take(take)
            .ToList();
    }

    public IReadOnlyList<HeatClick> Clicks(Guid pageKey, string culture, string device, DateTime fromUtc, DateTime toUtc, int take)
    {
        using var scope = scopes.CreateScope(autoComplete: true);
        return scope.Database.Query<ClickGroup>(
                $"SELECT selector AS Selector, x AS X, y AS Y, COUNT(*) AS N FROM {StatsClickDto.Table} WHERE {Range} AND {Page} GROUP BY selector, x, y ORDER BY COUNT(*) DESC",
                fromUtc, toUtc, pageKey, culture, device)
            .Take(take)
            .Select(c => new HeatClick(c.Selector, c.X, c.Y, c.N))
            .ToList();
    }

    public IReadOnlyDictionary<int, int> Depths(Guid pageKey, string culture, string device, DateTime fromUtc, DateTime toUtc)
    {
        using var scope = scopes.CreateScope(autoComplete: true);
        return scope.Database.Fetch<DepthCount>(
                $"SELECT depth AS Depth, COUNT(*) AS N FROM {StatsScrollDto.Table} WHERE {Range} AND {Page} GROUP BY depth",
                fromUtc, toUtc, pageKey, culture, device)
            .ToDictionary(d => d.Depth, d => d.N);
    }

    public int DeleteBefore(DateTime cutoffUtc)
    {
        using var scope = scopes.CreateScope();
        var n = scope.Database.Execute($"DELETE FROM {StatsClickDto.Table} WHERE createdUtc < @0", cutoffUtc)
              + scope.Database.Execute($"DELETE FROM {StatsScrollDto.Table} WHERE createdUtc < @0", cutoffUtc);
        scope.Complete();
        return n;
    }

    private sealed class PageCount
    {
        public Guid PageKey { get; set; }
        public string Culture { get; set; } = "";
        public int N { get; set; }
    }

    private sealed class ClickGroup
    {
        public string Selector { get; set; } = "";
        public int X { get; set; }
        public int Y { get; set; }
        public int N { get; set; }
    }

    private sealed class DepthCount
    {
        public int Depth { get; set; }
        public int N { get; set; }
    }
}
