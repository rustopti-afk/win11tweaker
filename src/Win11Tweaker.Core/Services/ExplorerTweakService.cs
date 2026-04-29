#if WINDOWS
using Microsoft.Win32;
using Win11Tweaker.Core.Backup;
using Win11Tweaker.Core.Changes;
using Win11Tweaker.Core.Registry;

namespace Win11Tweaker.Core.Services;

public class ExplorerTweakService
{
    private readonly ChangeTracker _tracker;
    private readonly RegistryService _registry;

    private const string AdvancedKey =
        @"Software\Microsoft\Windows\CurrentVersionxplorer\Advanced";
    private const string OldContextMenuClsid =
        @"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32";

    public ExplorerTweakService(ChangeTracker tracker, RegistryService registry)
    {
        _tracker = tracker;
        _registry = registry;
    }

    /// <summary>
    /// Restores the Windows 10-style full context menu (removes the "Show more options" step).
    /// Setting InprocServer32 default value to empty string tricks shell into using old menu.
    /// </summary>
    public Task<ApplyResult> SetOldContextMenu(bool enabled)
    {
        if (enabled)
        {
            var change = new RegistryChange(_registry)
            {
                Category = "explorer",
                Description = "Restore Windows 10 context menu",
                RiskLevel = RiskLevel.Safe,
                RequiresExplorerRestart = true,
                Hive = RegistryHive.CurrentUser,
                KeyPath = OldContextMenuClsid,
                ValueName = string.Empty, // default value
                NewValue = string.Empty,
                ValueKind = RegistryValueKind.String
            };
            return _tracker.ApplyWithBackup(change);
        }
        else
        {
            // Remove the key entirely to restore Win11 menu
            _registry.DeleteKey(RegistryHive.CurrentUser,
                @"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}");
            TaskbarTweakService.RestartExplorer();
            return Task.FromResult(ApplyResult.Ok());
        }
    }

    public Task<ApplyResult> SetShowFileExtensions(bool show) =>
        ApplyDword("Show file extensions", "explorer",
            AdvancedKey, "HideFileExt", show ? 0 : 1, restartExplorer: true);

    public Task<ApplyResult> SetShowHiddenFiles(bool show) =>
        ApplyDword("Show hidden files", "explorer",
            AdvancedKey, "Hidden", show ? 1 : 2, restartExplorer: true);

    public Task<ApplyResult> SetCompactMode(bool compact) =>
        ApplyDword("Explorer compact mode", "explorer",
            AdvancedKey, "UseCompactMode", compact ? 1 : 0, restartExplorer: true);

    public Task<ApplyResult> SetLaunchToThisPC(bool thisPC) =>
        ApplyDword("Open Explorer to This PC", "explorer",
            AdvancedKey, "LaunchTo", thisPC ? 1 : 2);

    public Task<ApplyResult> SetDesktopIconsVisible(bool visible) =>
        ApplyDword($"{(visible ? "Show" : "Hide")} desktop icons", "explorer",
            AdvancedKey, "HideIcons", visible ? 0 : 1, restartExplorer: true);

    private async Task<ApplyResult> ApplyDword(
        string description, string category,
        string keyPath, string valueName, int value,
        bool restartExplorer = false)
    {
        var change = new RegistryChange(_registry)
        {
            Category = category,
            Description = description,
            RiskLevel = RiskLevel.Safe,
            RequiresExplorerRestart = restartExplorer,
            Hive = RegistryHive.CurrentUser,
            KeyPath = keyPath,
            ValueName = valueName,
            NewValue = value,
            ValueKind = RegistryValueKind.DWord
        };

        var result = await _tracker.ApplyWithBackup(change);
        if (result.Success && restartExplorer)
            TaskbarTweakService.RestartExplorer();
        return result;
    }
}
#endif
