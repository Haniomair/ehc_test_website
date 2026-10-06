namespace EHC.Web.Stats;

public sealed record StatsRange(IReadOnlyList<StatsRow> Rows, IReadOnlyList<StatsDayRow> Days)
{
    public StatsRow Total => Rows.FirstOrDefault(r => r.Dimension == StatsStore.Total) ?? new StatsRow(StatsStore.Total, "", 0, 0, 0);
}

/// <summary>
/// Totals for a range of days: finished days come from the stored daily totals, the rest (today, and any day the
/// rollup job has not reached yet) are counted live from the page views.
/// </summary>
public sealed class StatsReports(IStatsStore store, StatsCalendar calendar)
{
    public StatsRange Load(DateOnly from, DateOnly to, bool totalOnly = false)
    {
        var today = calendar.Today;
        if (to > today) to = today;
        var sums = new Dictionary<(string, string), StatsRow>();
        var days = new Dictionary<DateOnly, StatsDayRow>();

        var rolled = store.RolledUpTo();
        if (rolled is { } done && from <= done)
        {
            var end = to < done ? to : done;
            foreach (var row in store.DailySums(from, end, totalOnly)) Add(sums, row);
            foreach (var day in store.DailyTotals(from, end)) days[day.Day] = day;
        }

        var live = rolled is { } r && r >= from ? r.AddDays(1) : from;
        if (live <= to && store.FirstHitUtc() is { } first)
        {
            var firstDay = calendar.DayOf(first);
            if (live < firstDay) live = firstDay;
            for (var day = live; day <= to; day = day.AddDays(1))
            {
                var rows = store.Aggregate(calendar.StartUtc(day), calendar.StartUtc(day.AddDays(1)), totalOnly);
                foreach (var row in rows) Add(sums, row);
                var total = rows.First(x => x.Dimension == StatsStore.Total);
                days[day] = new StatsDayRow(day, total.Views, total.Visits, total.Visitors);
            }
        }

        var series = new List<StatsDayRow>();
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            series.Add(days.TryGetValue(day, out var d) ? d : new StatsDayRow(day, 0, 0, 0));
        }
        return new StatsRange(sums.Values.ToList(), series);
    }

    private static void Add(Dictionary<(string, string), StatsRow> sums, StatsRow row)
    {
        var key = (row.Dimension, row.Value);
        sums[key] = sums.TryGetValue(key, out var s)
            ? s with { Views = s.Views + row.Views, Visits = s.Visits + row.Visits, Visitors = s.Visitors + row.Visitors }
            : row;
    }
}
