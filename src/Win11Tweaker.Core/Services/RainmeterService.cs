#if WINDOWS
using System.Diagnostics;
using System.IO.Compression;
using Microsoft.Win32;

namespace Win11Tweaker.Core.Services;

public class RainmeterSkin
{
    public string ConfigName  { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Path        { get; set; } = "";
    public bool   IsActive    { get; set; }
}

public class RainmeterService
{
    private string? _exePath;

    private static readonly string[] SearchPaths =
    [
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Rainmeter", "Rainmeter.exe"),
        @"C:\Program Files\Rainmeter\Rainmeter.exe",
    ];

    private static string SkinsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Rainmeter", "Skins");

    // ── Detection & install ───────────────────────────────────────────────────

    public bool IsInstalled()
    {
        _exePath = SearchPaths.FirstOrDefault(File.Exists);
        if (_exePath != null) return true;

        // Also check registry
        using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Rainmeter");
        var installPath = key?.GetValue("InstallLocation") as string;
        if (installPath != null)
        {
            var candidate = Path.Combine(installPath, "Rainmeter.exe");
            if (File.Exists(candidate)) { _exePath = candidate; return true; }
        }
        return false;
    }

    public async Task<bool> Install()
    {
        try
        {
            var psi = new ProcessStartInfo("winget",
                "install --id Rainmeter.Rainmeter --silent --accept-package-agreements --accept-source-agreements")
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

    // ── Skin management ───────────────────────────────────────────────────────

    /// <summary>
    /// Install a .rmskin package (it's a ZIP with a renamed extension).
    /// Extracts to ~/Documents/Rainmeter/Skins/
    /// </summary>
    public void InstallSkin(string rmskinPath)
    {
        if (!File.Exists(rmskinPath))
            throw new FileNotFoundException("Skin file not found.", rmskinPath);

        Directory.CreateDirectory(SkinsDirectory);

        // .rmskin files have a footer appended — strip it by reading as ZIP
        // (The standard Rainmeter installer handles .rmskin natively, but we can
        //  also just extract as ZIP for simple skins without scripts)
        try
        {
            ZipFile.ExtractToDirectory(rmskinPath, SkinsDirectory, overwriteFiles: true);
        }
        catch
        {
            // If ZIP extraction fails, try to run Rainmeter's own installer
            if (_exePath != null)
                Process.Start(new ProcessStartInfo(rmskinPath) { UseShellExecute = true });
        }
    }

    /// <summary>List all installed skin configs (one per top-level skin folder).</summary>
    public List<RainmeterSkin> GetInstalledSkins()
    {
        var skins = new List<RainmeterSkin>();
        if (!Directory.Exists(SkinsDirectory)) return skins;

        foreach (var skinDir in Directory.GetDirectories(SkinsDirectory))
        {
            var configName = Path.GetFileName(skinDir);
            // Each subfolder is a variant/ini
            var iniFiles = Directory.GetFiles(skinDir, "*.ini", SearchOption.AllDirectories);
            foreach (var ini in iniFiles)
            {
                var relativePath = Path.GetRelativePath(SkinsDirectory, ini);
                skins.Add(new RainmeterSkin
                {
                    ConfigName  = configName,
                    DisplayName = $"{configName}\\{Path.GetFileNameWithoutExtension(ini)}",
                    Path        = ini
                });
            }
        }
        return skins;
    }

    // ── Bang commands ─────────────────────────────────────────────────────────

    /// <summary>Send a Rainmeter !Bang command (e.g. "!ActivateConfig Skin Name.ini")</summary>
    public async Task SendBang(string bang)
    {
        if (_exePath == null && !IsInstalled())
            throw new InvalidOperationException("Rainmeter is not installed.");

        var psi = new ProcessStartInfo(_exePath!, bang)
        {
            UseShellExecute = false,
            CreateNoWindow  = true
        };
        var p = Process.Start(psi)!;
        await p.WaitForExitAsync();
    }

    public Task ActivateSkin(string configName, string iniFile) =>
        SendBang($"!ActivateConfig \"{configName}\" \"{iniFile}\"");

    public Task DeactivateSkin(string configName) =>
        SendBang($"!DeactivateConfig \"{configName}\"");

    public Task RefreshAll() => SendBang("!RefreshApp");

    public Task LoadLayout(string layoutName) =>
        SendBang($"!LoadLayout \"{layoutName}\"");

    public Task Quit() => SendBang("!Quit");

    /// <summary>Launch Rainmeter GUI.</summary>
    public void Launch()
    {
        if (_exePath != null)
            Process.Start(new ProcessStartInfo(_exePath) { UseShellExecute = true });
    }
}
#endif
