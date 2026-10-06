using System.Globalization;
using NPoco;
using Umbraco.Cms.Infrastructure.Migrations;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Infrastructure.Persistence.DatabaseAnnotations;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Extensions;

namespace EHC.Web.Stats;

/// <summary>A funnel: two to eight pages in order, defined by editors in the Statistics section.</summary>
[TableName(Table)]
[PrimaryKey("id", AutoIncrement = true)]
[ExplicitColumns]
public sealed class StatsFunnelDto
{
    public const string Table = "ehcStatsFunnel";

    [Column("id")]
    [PrimaryKeyColumn(AutoIncrement = true)]
    public int Id { get; set; }

    [Column("name")]
    [Length(100)]
    public string Name { get; set; } = "";

    /// <summary>Page keys in order, comma separated.</summary>
    [Column("steps")]
    [Length(400)]
    public string Steps { get; set; } = "";

    /// <summary>First day with results (the oldest page views still available when the steps were last saved).</summary>
    [Column("since")]
    public DateTime Since { get; set; }

    [Column("updatedUtc")]
    public DateTime UpdatedUtc { get; set; }
}

/// <summary>Visitors who reached each step of a funnel on one day; written when the day is rolled up and kept.</summary>
[TableName(Table)]
[PrimaryKey("id", AutoIncrement = true)]
[ExplicitColumns]
public sealed class StatsFunnelDailyDto
{
    public const string Table = "ehcStatsFunnelDaily";

    [Column("id")]
    [PrimaryKeyColumn(AutoIncrement = true)]
    public int Id { get; set; }

    [Column("funnelId")]
    [Index(IndexTypes.NonClustered, Name = "IX_ehcStatsFunnelDaily_funnelId")]
    public int FunnelId { get; set; }

    [Column("day")]
    public DateTime Day { get; set; }

    /// <summary>1-based step number.</summary>
    [Column("step")]
    public int Step { get; set; }

    [Column("visitors")]
    public int Visitors { get; set; }
}

public sealed class AddStatsFunnelTables(IMigrationContext context) : AsyncMigrationBase(context)
{
    protected override Task MigrateAsync()
    {
        if (!TableExists(StatsFunnelDto.Table))
        {
            Create.Table<StatsFunnelDto>().Do();
        }
        if (!TableExists(StatsFunnelDailyDto.Table))
        {
            Create.Table<StatsFunnelDailyDto>().Do();
        }
        return Task.CompletedTask;
    }
}

public sealed record StatsFunnel(int Id, string Name, IReadOnlyList<Guid> Steps, DateOnly Since);

public static class FunnelMath
{
    public const int MinSteps = 2;
    public const int MaxSteps = 8;

    /// <summary>
    /// Visitors reaching each step: one visitor's page views in time order (grouped per visitor) must include the steps
    /// in order; other pages in between are fine. A visitor who reached step 3 also counts for steps 1 and 2.
    /// </summary>
    public static int[] Progress(IEnumerable<(string Visitor, Guid Page)> orderedHits, IReadOnlyList<Guid> steps)
    {
        var reached = new int[steps.Count];
        string? current = null;
        var stage = 0;
        foreach (var (visitor, page) in orderedHits)
        {
            if (visitor != current)
            {
                Count();
                current = visitor;
                stage = 0;
            }
            if (stage < steps.Count && page == steps[stage]) stage++;
        }
        Count();
        return reached;

        void Count()
        {
            for (var i = 0; i < stage; i++) reached[i]++;
        }
    }

    /// <summary>Null when valid, otherwise what is wrong (shown to the editor).</summary>
    public static string? Validate(string? name, IReadOnlyList<Guid> steps)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100) return "Give the funnel a name of up to 100 characters.";
        if (steps.Count is < MinSteps or > MaxSteps) return $"A funnel has {MinSteps} to {MaxSteps} steps.";
        if (steps.Any(s => s == Guid.Empty)) return "Choose a page for every step.";
        for (var i = 1; i < steps.Count; i++)
        {
            if (steps[i] == steps[i - 1]) return "Two steps in a row cannot be the same page.";
        }
        return null;
    }
}

public interface IStatsFunnels
{
    IReadOnlyList<StatsFunnel> All();
    StatsFunnel? Get(int id);
    StatsFunnel Save(int? id, string name, IReadOnlyList<Guid> steps);
    bool Delete(int id);
    /// <summary>Stores every funnel's results for a finished day (called by the rollup job before the day is marked done).</summary>
    void SaveDay(DateOnly day);
    /// <summary>Visitors per step over [from, to]: stored days plus live counts for days not rolled up yet.</summary>
    int[] Report(StatsFunnel funnel, DateOnly from, DateOnly to);
}

public sealed class StatsFunnels(IScopeProvider scopes, IStatsStore store, StatsCalendar calendar, TimeProvider clock) : IStatsFunnels
{
    public IReadOnlyList<StatsFunnel> All()
    {
        using var scope = scopes.CreateScope(autoComplete: true);
        return scope.Database.Fetch<StatsFunnelDto>($"SELECT * FROM {StatsFunnelDto.Table} ORDER BY name").Select(Map).ToList();
    }

    public StatsFunnel? Get(int id)
    {
        using var scope = scopes.CreateScope(autoComplete: true);
        var dto = scope.Database.Fetch<StatsFunnelDto>($"SELECT * FROM {StatsFunnelDto.Table} WHERE id = @0", id).FirstOrDefault();
        return dto is null ? null : Map(dto);
    }

    public StatsFunnel Save(int? id, string name, IReadOnlyList<Guid> steps)
    {
        var rolled = store.RolledUpTo();
        var first = store.FirstHitUtc() is { } f ? calendar.DayOf(f) : calendar.Today;
        using var scope = scopes.CreateScope();
        var db = scope.Database;
        var dto = id is { } existing
            ? db.Fetch<StatsFunnelDto>($"SELECT * FROM {StatsFunnelDto.Table} WHERE id = @0", existing).FirstOrDefault()
              ?? throw new KeyNotFoundException()
            : new StatsFunnelDto();
        var stepsChanged = dto.Steps != Join(steps);
        dto.Name = name.Trim();
        dto.Steps = Join(steps);
        dto.UpdatedUtc = clock.GetUtcNow().UtcDateTime;
        if (dto.Id == 0 || stepsChanged) dto.Since = first.ToDateTime(TimeOnly.MinValue);
        db.Save(dto);

        if (stepsChanged)
        {
            // results only make sense for the current steps: recount every finished day that still has page views
            db.Execute($"DELETE FROM {StatsFunnelDailyDto.Table} WHERE funnelId = @0", dto.Id);
            var funnel = Map(dto);
            if (rolled is { } done)
            {
                for (var day = first; day <= done; day = day.AddDays(1)) Write(db, funnel, day, Count(db, funnel, day));
            }
        }
        scope.Complete();
        return Map(dto);
    }

    public bool Delete(int id)
    {
        using var scope = scopes.CreateScope();
        scope.Database.Execute($"DELETE FROM {StatsFunnelDailyDto.Table} WHERE funnelId = @0", id);
        var n = scope.Database.Execute($"DELETE FROM {StatsFunnelDto.Table} WHERE id = @0", id);
        scope.Complete();
        return n > 0;
    }

    public void SaveDay(DateOnly day)
    {
        var funnels = All();
        if (funnels.Count == 0) return;
        using var scope = scopes.CreateScope();
        foreach (var funnel in funnels)
        {
            scope.Database.Execute($"DELETE FROM {StatsFunnelDailyDto.Table} WHERE funnelId = @0 AND day = @1", funnel.Id, day.ToDateTime(TimeOnly.MinValue));
            Write(scope.Database, funnel, day, Count(scope.Database, funnel, day));
        }
        scope.Complete();
    }

    public int[] Report(StatsFunnel funnel, DateOnly from, DateOnly to)
    {
        var today = calendar.Today;
        if (to > today) to = today;
        if (from < funnel.Since) from = funnel.Since;
        var totals = new int[funnel.Steps.Count];
        if (from > to) return totals;

        using var scope = scopes.CreateScope(autoComplete: true);
        var rolled = store.RolledUpTo();
        if (rolled is { } done && from <= done)
        {
            var end = to < done ? to : done;
            foreach (var row in scope.Database.Fetch<StepSum>(
                         $"SELECT step AS Step, SUM(visitors) AS Visitors FROM {StatsFunnelDailyDto.Table} WHERE funnelId = @0 AND day >= @1 AND day <= @2 GROUP BY step",
                         funnel.Id, from.ToDateTime(TimeOnly.MinValue), end.ToDateTime(TimeOnly.MinValue)))
            {
                if (row.Step >= 1 && row.Step <= totals.Length) totals[row.Step - 1] += row.Visitors;
            }
        }
        var live = rolled is { } r && r >= from ? r.AddDays(1) : from;
        for (var day = live; day <= to; day = day.AddDays(1))
        {
            var counts = Count(scope.Database, funnel, day);
            for (var i = 0; i < totals.Length; i++) totals[i] += counts[i];
        }
        return totals;
    }

    /// <summary>Counts one day from the single page views (only the funnel's pages are read).</summary>
    private int[] Count(IUmbracoDatabase db, StatsFunnel funnel, DateOnly day)
    {
        var hits = db.Query<VisitorPage>(
            $"SELECT visitor AS Visitor, pageKey AS PageKey FROM {StatsHitDto.Table} WHERE createdUtc >= @0 AND createdUtc < @1 AND pageKey IN (@2) ORDER BY visitor, createdUtc, id",
            calendar.StartUtc(day), calendar.StartUtc(day.AddDays(1)), funnel.Steps.Distinct().ToArray());
        return FunnelMath.Progress(hits.Select(h => (h.Visitor, h.PageKey)), funnel.Steps);
    }

    private static void Write(IUmbracoDatabase db, StatsFunnel funnel, DateOnly day, int[] counts)
    {
        var date = day.ToDateTime(TimeOnly.MinValue);
        var rows = counts.Select((n, i) => new StatsFunnelDailyDto { FunnelId = funnel.Id, Day = date, Step = i + 1, Visitors = n }).Where(r => r.Visitors > 0).ToList();
        if (rows.Count > 0) db.InsertBulk(rows);
    }

    private static string Join(IEnumerable<Guid> steps) => string.Join(',', steps.Select(s => s.ToString("D", CultureInfo.InvariantCulture)));

    private static StatsFunnel Map(StatsFunnelDto dto) => new(
        dto.Id,
        dto.Name,
        dto.Steps.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Guid.Parse).ToList(),
        DateOnly.FromDateTime(dto.Since));

    private sealed class VisitorPage
    {
        public string Visitor { get; set; } = "";
        public Guid PageKey { get; set; }
    }

    private sealed class StepSum
    {
        public int Step { get; set; }
        public int Visitors { get; set; }
    }
}
