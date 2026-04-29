using System.Text.Json;
using Win11Tweaker.Core.Changes;
using Microsoft.Extensions.Logging;

namespace Win11Tweaker.Core.Backup;

public record ApplyResult(bool Success, string? Error = null)
{
    public static ApplyResult Ok() => new(true);
    public static ApplyResult Fail(string error) => new(false, error);
}

/// <summary>
/// Central coordinator for all system changes.
/// Every change is snapshotted before apply and written to disk,
/// so rollback works even after a crash.
/// </summary>
public class ChangeTracker
{
    private readonly string _snapshotsDir;
    private readonly string _changeLogPath;
    private readonly ILogger<ChangeTracker> _logger;
    private readonly List<(SystemChange Change, ChangeSnapshot Snapshot)> _history = [];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ChangeTracker(ILogger<ChangeTracker> logger)
    {
        _logger = logger;
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Win11Tweaker");

        _snapshotsDir = Path.Combine(appData, "backups", "snapshots");
        _changeLogPath = Path.Combine(appData, "changes.jsonl");

        Directory.CreateDirectory(_snapshotsDir);
        Directory.CreateDirectory(Path.GetDirectoryName(_changeLogPath)!);
    }

    public IReadOnlyList<(SystemChange Change, ChangeSnapshot Snapshot)> History => _history;

    /// <summary>
    /// Snapshots current state, applies the change, and tracks it for rollback.
    /// If apply throws — automatically restores the snapshot.
    /// </summary>
    public async Task<ApplyResult> ApplyWithBackup(SystemChange change)
    {
        _logger.LogInformation("Applying change: {Id} — {Description}", change.Id, change.Description);

        ChangeSnapshot snapshot;
        try
        {
            snapshot = await change.CaptureSnapshot();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to capture snapshot for {Id}", change.Id);
            return ApplyResult.Fail($"Snapshot failed: {ex.Message}");
        }

        // Persist snapshot to disk before touching the system
        await PersistSnapshot(change.Id, snapshot);

        try
        {
            await change.Apply();
            _history.Add((change, snapshot));
            await AppendToChangeLog(change);
            _logger.LogInformation("Change applied: {Id}", change.Id);
            return ApplyResult.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Apply failed for {Id}, rolling back", change.Id);
            try
            {
                await change.Rollback(snapshot);
            }
            catch (Exception rollbackEx)
            {
                _logger.LogCritical(rollbackEx, "ROLLBACK ALSO FAILED for {Id}", change.Id);
            }
            await DeleteSnapshot(change.Id);
            return ApplyResult.Fail(ex.Message);
        }
    }

    /// <summary>Undo the most recent change.</summary>
    public async Task<bool> RollbackLast()
    {
        if (_history.Count == 0) return false;

        var (change, snapshot) = _history[^1];
        try
        {
            await change.Rollback(snapshot);
            _history.RemoveAt(_history.Count - 1);
            await DeleteSnapshot(change.Id);
            _logger.LogInformation("Rolled back: {Id}", change.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rollback failed for {Id}", change.Id);
            return false;
        }
    }

    /// <summary>Undo all changes in reverse order (newest first).</summary>
    public async Task RollbackAll()
    {
        foreach (var (change, snapshot) in _history.AsEnumerable().Reverse())
        {
            try { await change.Rollback(snapshot); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RollbackAll: failed on {Id}, continuing", change.Id);
            }
        }
        _history.Clear();
    }

    private async Task PersistSnapshot(string changeId, ChangeSnapshot snapshot)
    {
        var path = Path.Combine(_snapshotsDir, $"{changeId}.json");
        var json = JsonSerializer.Serialize(snapshot, JsonOptions);
        await File.WriteAllTextAsync(path, json);
    }

    private Task DeleteSnapshot(string changeId)
    {
        var path = Path.Combine(_snapshotsDir, $"{changeId}.json");
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private async Task AppendToChangeLog(SystemChange change)
    {
        var entry = JsonSerializer.Serialize(new
        {
            change.Id,
            change.Timestamp,
            change.Category,
            change.Description,
            change.RiskLevel
        });
        await File.AppendAllTextAsync(_changeLogPath, entry + Environment.NewLine);
    }
}
