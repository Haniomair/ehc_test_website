using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Services;

namespace EHC.Web.Stats;

/// <summary>
/// Page views wait here and are written in batches every few seconds, so a busy moment costs one insert instead of one
/// per visitor. When the queue is full (database unavailable) new page views are dropped rather than slowing the site.
/// </summary>
public sealed class StatsQueue
{
    private readonly Channel<StatsHitDto> _channel = Channel.CreateBounded<StatsHitDto>(
        new BoundedChannelOptions(20_000) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });

    public bool TryAdd(StatsHitDto hit) => _channel.Writer.TryWrite(hit);

    public ChannelReader<StatsHitDto> Reader => _channel.Reader;
}

public sealed class StatsWriter(StatsQueue queue, IStatsStore store, IRuntimeState runtime, ILogger<StatsWriter> logger) : BackgroundService
{
    private static readonly TimeSpan Every = TimeSpan.FromSeconds(5);
    private const int BatchSize = 1000;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await Task.Delay(Every, stoppingToken); }
            catch (OperationCanceledException) { /* last flush below */ }
            Flush();
        }
    }

    private void Flush()
    {
        if (runtime.Level != RuntimeLevel.Run) return;
        var batch = new List<StatsHitDto>(BatchSize);
        while (queue.Reader.TryRead(out var hit))
        {
            batch.Add(hit);
            if (batch.Count == BatchSize) Write(batch);
        }
        Write(batch);
    }

    private void Write(List<StatsHitDto> batch)
    {
        if (batch.Count == 0) return;
        try
        {
            store.Add(batch);
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Visitor statistics: {Count} page views could not be saved", batch.Count);
        }
        batch.Clear();
    }
}
