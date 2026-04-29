#if WINDOWS
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;

namespace Win11Tweaker.Core.Services;

public class WindhawkMod
{
    public string Id          { get; set; } = "";
    public string Name        { get; set; } = "";
    public string Description { get; set; } = "";
    public string Version     { get; set; } = "";
    public bool   IsInstalled { get; set; }
}

/// <summary>
/// Windhawk recommended mods for Win11 customization.
/// Each entry: (id, display name, description)
/// </summary>
public static class WindhawkRecommendedMods
{
    public static readonly WindhawkMod[] All =
    [
        new() { Id = "taskbar-button-click",
                Name = "Taskbar Button Click",
                Description = "Customize taskbar button click behavior (middle-click to close, etc.)" },

        new() { Id = "taskbar-labels",
                Name = "Taskbar Labels",
                Description = "Show text labels next to icons in the taskbar" },

        new() { Id = "windows-11-taskbar-styler",
                Name = "Taskbar Styler",
                Description = "Deep visual customization of the taskbar with CSS-like rules" },

        new() { Id = "windows-11-start-menu-styler",
                Name = "Start Menu Styler",
                Description = "Customize Start Menu appearance with custom CSS rules" },

        new() { Id = "windows-11-notification-center-styler",
                Name = "Notification Center Styler",
                Description = "Style the notification center and Quick Settings panel" },

        new() { Id = "classic-taskbar-buttons-lite-vs-without-grouping",
                Name = "Taskbar No Grouping",
                Description = "Disable taskbar button grouping — show one button per window" },

        new() { Id = "taskbar-clock-customization",
                Name = "Taskbar Clock Customization",
                Description = "Customize the taskbar clock format and style" },

        new() { Id = "taskbar-volume-control",
                Name = "Taskbar Volume Control",
                Description = "Control volume by scrolling on the taskbar" },

        new() { Id = "disable-rounded-corners",
                Name = "Disable Rounded Corners",
                Description = "Remove rounded corners from all windows (sharp Win10 style)" },

        new() { Id = "force-snap-window",
                Name = "Force Snap Window",
                Description = "Force snap assist for windows that try to opt out" },
    ];
}

public class WindhawkService : IDisposable
{
    // Windhawk exposes a local REST API at localhost:3248
    private const string ApiBase = "http://localhost:3248";

    private readonly HttpClient _http;
    private string? _exePath;
    private bool _disposed;

    public WindhawkService()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
    }

    // ── Detection & install ───────────────────────────────────────────────────

    public bool IsInstalled()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Windhawk", "windhawk.exe"),
            @"C:\Program Files\Windhawk\windhawk.exe",
        };
        _exePath = candidates.FirstOrDefault(File.Exists);

        if (_exePath != null) return true;

        using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Windhawk");
        var loc = key?.GetValue("InstallLocation") as string;
        if (loc == null) return false;

        var candidate = Path.Combine(loc, "windhawk.exe");
        if (!File.Exists(candidate)) return false;
        _exePath = candidate;
        return true;
    }

    public async Task<bool> Install()
    {
        try
        {
            var psi = new ProcessStartInfo("winget",
                "install --id Windhawk.Windhawk --silent --accept-package-agreements --accept-source-agreements")
            {
                UseShellExecute = false,
                CreateNoWindow  = true
            };
            var p = Process.Start(psi)!;
            await p.WaitForExitAsync();
            return p.ExitCode == 0 && IsInstalled();
        }
        catch { return false; }
    }

    // ── API: installed mods ───────────────────────────────────────────────────

    /// <summary>
    /// Returns list of currently installed mods via Windhawk REST API.
    /// Windhawk must be running for this to work.
    /// </summary>
    public async Task<List<WindhawkMod>> GetInstalledMods()
    {
        try
        {
            var json = await _http.GetStringAsync($"{ApiBase}/mods");
            var items = JsonSerializer.Deserialize<List<WindhawkApiMod>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];

            return items.Select(m => new WindhawkMod
            {
                Id          = m.Id ?? "",
                Name        = m.Name ?? m.Id ?? "",
                Description = m.Description ?? "",
                Version     = m.Version ?? "",
                IsInstalled = true
            }).ToList();
        }
        catch
        {
            // Windhawk not running or API not available
            return [];
        }
    }

    /// <summary>Check which recommended mods are currently installed.</summary>
    public async Task<WindhawkMod[]> GetRecommendedModsWithStatus()
    {
        var installed = await GetInstalledMods();
        var installedIds = installed.Select(m => m.Id).ToHashSet();

        return WindhawkRecommendedMods.All
            .Select(m => m with { IsInstalled = installedIds.Contains(m.Id) })
            .ToArray();
    }

    // ── Mod install / uninstall ───────────────────────────────────────────────

    /// <summary>Install a mod via Windhawk CLI.</summary>
    public async Task<bool> InstallMod(string modId)
    {
        if (_exePath == null && !IsInstalled()) return false;

        try
        {
            var psi = new ProcessStartInfo(_exePath!, $"--install-mod {modId}")
            {
                UseShellExecute = false,
                CreateNoWindow  = true
            };
            var p = Process.Start(psi)!;
            await p.WaitForExitAsync();
            return p.ExitCode == 0;
        }
        catch { return false; }
    }

    public async Task<bool> UninstallMod(string modId)
    {
        if (_exePath == null && !IsInstalled()) return false;

        try
        {
            var psi = new ProcessStartInfo(_exePath!, $"--uninstall-mod {modId}")
            {
                UseShellExecute = false,
                CreateNoWindow  = true
            };
            var p = Process.Start(psi)!;
            await p.WaitForExitAsync();
            return p.ExitCode == 0;
        }
        catch { return false; }
    }

    /// <summary>Open Windhawk GUI to the mod page.</summary>
    public void OpenModPage(string modId)
    {
        Process.Start(new ProcessStartInfo(
            $"https://windhawk.net/mods/{modId}") { UseShellExecute = true });
    }

    /// <summary>Launch Windhawk GUI.</summary>
    public void Launch()
    {
        if (_exePath != null)
            Process.Start(new ProcessStartInfo(_exePath) { UseShellExecute = true });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _http.Dispose();
        _disposed = true;
    }

    // Internal API response model
    private record WindhawkApiMod(
        [property: JsonPropertyName("id")]          string? Id,
        [property: JsonPropertyName("name")]        string? Name,
        [property: JsonPropertyName("description")] string? Description,
        [property: JsonPropertyName("version")]     string? Version
    );
}
#endif
