#if WINDOWS
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Win11Tweaker.Core.Backup;
using Win11Tweaker.Core.Changes;
using Win11Tweaker.Core.Registry;

namespace Win11Tweaker.Core.Services;

public class TaskbarTweakService
{
    private readonly ChangeTracker _tracker;
    private readonly RegistryService _registry;

    private const string SearchKey =
        @"Software\Microsoft\Windows\CurrentVersion\Search";
    private const string ExplorerAdvancedKey =
        @"Software\Microsoft\Windows\CurrentVersionxplorer\Advanced";
    private const string PersonalizeKey =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    public TaskbarTweakService(ChangeTracker tracker, RegistryService registry)
    {
        _tracker = tracker;
        _registry = registry;
    }

    // ── Visibility tweaks ──────────────────────────────────────────────────

    public Task<ApplyResult> SetSearchButtonVisible(bool visible) =>
        Apply("Hide/Show Search Button", "taskbar",
            RegistryHive.CurrentUser, SearchKey,
            "SearchboxTaskbarMode", visible ? 1 : 0,
            restartExplorer: true);

    public Task<ApplyResult> SetWidgetsButtonVisible(bool visible) =>
        Apply("Hide/Show Widgets Button", "taskbar",
            RegistryHive.CurrentUser, ExplorerAdvancedKey,
            "TaskbarDa", visible ? 1 : 0,
            restartExplorer: true);

    public Task<ApplyResult> SetChatButtonVisible(bool visible) =>
        Apply("Hide/Show Chat (Teams) Button", "taskbar",
            RegistryHive.CurrentUser, ExplorerAdvancedKey,
            "TaskbarMn", visible ? 1 : 0,
            restartExplorer: true);

    // ── Position & size ───────────────────────────────────────────────────

    public Task<ApplyResult> SetTaskbarAlignment(TaskbarAlignment alignment) =>
        Apply("Set Taskbar Alignment", "taskbar",
            RegistryHive.CurrentUser, ExplorerAdvancedKey,
            "TaskbarAl", (int)alignment,
            restartExplorer: true);

    public Task<ApplyResult> SetSmallIcons(bool small) =>
        Apply("Set Taskbar Icon Size", "taskbar",
            RegistryHive.CurrentUser, ExplorerAdvancedKey,
            "TaskbarSi", small ? 0 : 1,
            restartExplorer: true);

    // ── Auto-hide ─────────────────────────────────────────────────────────

    public Task<ApplyResult> SetAutoHide(bool enabled)
    {
        // Auto-hide is stored as bit 3 in the StuckRects3 binary blob.
        // Easiest reliable approach: toggle via SystemParametersInfo.
        var change = new RegistryChange(_registry)
        {
            Category = "taskbar",
            Description = $"{(enabled ? "Enable" : "Disable")} Taskbar Auto-hide",
            RiskLevel = RiskLevel.Safe,
            RequiresExplorerRestart = false,
            Hive = RegistryHive.CurrentUser,
            KeyPath = @"Software\Microsoft\Windows\CurrentVersionxplorer\StuckRects3",
            ValueName = "Settings",
            NewValue = GetStuckRectsWithAutoHide(enabled),
            ValueKind = RegistryValueKind.Binary
        };
        return _tracker.ApplyWithBackup(change);
    }

    // ── Appearance ────────────────────────────────────────────────────────

    public async Task<ApplyResult> SetDarkMode(bool dark)
    {
        var change = new MultiRegistryChange(
        [
            new RegistryChange(_registry)
            {
                Category = "appearance",
                Description = $"Set {(dark ? "Dark" : "Light")} Mode (Apps)",
                Hive = RegistryHive.CurrentUser,
                KeyPath = PersonalizeKey,
                ValueName = "AppsUseLightTheme",
                NewValue = dark ? 0 : 1,
                ValueKind = RegistryValueKind.DWord
            },
            new RegistryChange(_registry)
            {
                Category = "appearance",
                Description = $"Set {(dark ? "Dark" : "Light")} Mode (System)",
                Hive = RegistryHive.CurrentUser,
                KeyPath = PersonalizeKey,
                ValueName = "SystemUsesLightTheme",
                NewValue = dark ? 0 : 1,
                ValueKind = RegistryValueKind.DWord
            }
        ])
        {
            Category = "appearance",
            Description = $"Enable {(dark ? "Dark" : "Light")} Mode",
            RiskLevel = RiskLevel.Safe
        };

        return await _tracker.ApplyWithBackup(change);
    }

    public Task<ApplyResult> SetTransparencyEnabled(bool enabled) =>
        Apply("Toggle Transparency Effects", "appearance",
            RegistryHive.CurrentUser, PersonalizeKey,
            "EnableTransparency", enabled ? 1 : 0);

    public Task<ApplyResult> SetAccentColor(int colorArgb) =>
        Apply("Set Accent Color", "appearance",
            RegistryHive.CurrentUser, PersonalizeKey,
            "AccentColor", colorArgb);

    public Task<ApplyResult> SetColorPrevalence(bool showColorOnTaskbar) =>
        Apply("Show accent color on taskbar", "appearance",
            RegistryHive.CurrentUser, PersonalizeKey,
            "ColorPrevalence", showColorOnTaskbar ? 1 : 0,
            restartExplorer: true);

    // ── Explorer restart ──────────────────────────────────────────────────

    public static void RestartExplorer()
    {
        foreach (var p in Process.GetProcessesByName("explorer"))
        {
            p.Kill();
            p.WaitForExit(3000);
        }
        // Explorer auto-restarts via Windows shell restart mechanism.
        // If not — launch it manually.
        Task.Delay(1000).ContinueWith(_ =>
        {
            if (Process.GetProcessesByName("explorer").Length == 0)
                Process.Start("explorer.exe");
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private async Task<ApplyResult> Apply(
        string description, string category,
        RegistryHive hive, string keyPath, string valueName, int value,
        bool restartExplorer = false)
    {
        var change = new RegistryChange(_registry)
        {
            Category = category,
            Description = description,
            RiskLevel = RiskLevel.Safe,
            RequiresExplorerRestart = restartExplorer,
            Hive = hive,
            KeyPath = keyPath,
            ValueName = valueName,
            NewValue = value,
            ValueKind = RegistryValueKind.DWord
        };

        var result = await _tracker.ApplyWithBackup(change);
        if (result.Success && restartExplorer)
            RestartExplorer();
        return result;
    }

    private static byte[] GetStuckRectsWithAutoHide(bool enable)
    {
        // Read existing blob and flip the auto-hide bit (byte index 8, bit 0x08)
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersionxplorer\StuckRects3");
        var existing = key?.GetValue("Settings") as byte[] ?? new byte[40];

        var blob = (byte[])existing.Clone();
        if (enable)
            blob[8] |= 0x08;
        else
            blob[8] &= unchecked((byte)~0x08);

        return blob;
    }
}

public enum TaskbarAlignment { Left = 0, Center = 1 }
#endif
