using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using uSync.BackOffice;
using uSync.BackOffice.Configuration;
using uSync.BackOffice.SyncHandlers;
using uSync.BackOffice.SyncHandlers.Models;
using uSync.Core;

namespace EHC.Web.Composers;

/// <summary>
/// Development convenience (Ehc:USync:AutoImport): imports the uSync "Settings" group (document, element and data types,
/// templates, languages, dictionary) once after start-up and again whenever a file under uSync/ changes, so schema changes
/// made in the files apply without uSync › Settings › Import or a restart. Content and media are never imported here.
/// Changes are debounced and one import runs at a time; uSync only touches items whose file differs from the database.
/// </summary>
public sealed class USyncAutoImport(
    ISyncService sync, ISyncConfigService syncConfig, ISyncHandlerFactory handlers, IOptions<uSyncSettings> settings, IConfiguration config, IHostEnvironment env,
    IRuntimeState runtime, ILogger<USyncAutoImport> logger) : IDisposable
{
    // folders of the Content group (and uSync's own bookkeeping): changes there never start an import
    private static readonly string[] Ignored = ["Content", "Media", "Domains", "ContentTemplates", "History"];
    private static readonly TimeSpan Quiet = TimeSpan.FromSeconds(2);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private FileSystemWatcher? _watcher;
    private Timer? _timer;
    private volatile bool _pending;

    public void Start()
    {
        if (_watcher is not null || !env.IsDevelopment() || !config.GetValue("Ehc:USync:AutoImport", false) || runtime.Level != RuntimeLevel.Run) return;
        var root = Path.Combine(env.ContentRootPath, "uSync");
        if (!Directory.Exists(root)) return;
        try
        {
            _timer = new Timer(_ => _ = RunAsync("files changed"), null, Timeout.Infinite, Timeout.Infinite);
            _watcher = new FileSystemWatcher(root, "*.config") { IncludeSubdirectories = true, NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size };
            _watcher.Changed += OnChange;
            _watcher.Created += OnChange;
            _watcher.Deleted += OnChange;
            _watcher.Renamed += OnChange;
            _watcher.EnableRaisingEvents = true;
            _ = Task.Run(() => RunAsync("start-up"));
            logger.LogInformation("uSync auto-import is on (development): Settings are imported when files in {Folder} change", root);
        }
        catch (Exception e) { logger.LogError(e, "uSync auto-import could not start; use uSync › Settings › Import"); }
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _timer?.Dispose();
    }

    private void OnChange(object sender, FileSystemEventArgs e)
    {
        var parts = e.FullPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (parts.Any(p => Ignored.Contains(p, StringComparer.OrdinalIgnoreCase))) return;
        _timer?.Change(Quiet, Timeout.InfiniteTimeSpan);   // wait until the writes settle
    }

    private async Task RunAsync(string reason)
    {
        if (!await _gate.WaitAsync(0)) { _pending = true; return; }   // one import at a time; run again afterwards
        try
        {
            do
            {
                _pending = false;
                // the regular import (what uSync › Settings › Import runs); the start-up import only runs once per start
                var options = new SyncHandlerOptions { Group = "Settings", Set = settings.Value.DefaultSet, Action = HandlerActions.Import };
                var actions = (await sync.ImportAsync(syncConfig.GetFolders(), false, handlers.GetValidHandlers(options), options, new uSyncCallbacks(null, null))).ToList();
                var changed = actions.Where(a => a.Change is ChangeType.Create or ChangeType.Import or ChangeType.Update or ChangeType.Delete or ChangeType.Removed).ToList();
                var failed = actions.Where(a => !a.Success || a.Change is ChangeType.Fail or ChangeType.ImportFail).ToList();
                if (changed.Count > 0 || failed.Count > 0)
                {
                    logger.LogWarning("uSync auto-import ({Reason}): {Changed} change(s) applied: {Items}", reason, changed.Count,
                        string.Join(", ", changed.Take(12).Select(a => $"{a.ItemType} {a.Name}")));
                }
                foreach (var f in failed) { logger.LogError(f.Exception, "uSync auto-import failed for {Item}: {Message}", f.Name, f.Message); }
                reason = "files changed";
            }
            while (_pending);
        }
        catch (Exception e) { logger.LogError(e, "uSync auto-import failed; use uSync › Settings › Import"); }
        finally { _gate.Release(); }
    }
}

/// <summary>Starts / stops the singleton <see cref="USyncAutoImport"/> with the application.</summary>
public sealed class USyncAutoImportHandler(USyncAutoImport auto)
    : INotificationHandler<UmbracoApplicationStartedNotification>, INotificationHandler<UmbracoApplicationStoppingNotification>
{
    public void Handle(UmbracoApplicationStartedNotification notification) => auto.Start();
    public void Handle(UmbracoApplicationStoppingNotification notification) => auto.Dispose();
}
