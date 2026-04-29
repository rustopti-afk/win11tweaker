#if WINDOWS
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Win11Tweaker.Core.Services;

public enum WallpaperStyle { Fill, Fit, Stretch, Tile, Center, Span }

public class WallpaperService
{
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool SystemParametersInfo(
        int uAction, int uParam, string lpvParam, int fuWinIni);

    private const int SPI_SETDESKWALLPAPER = 0x0014;
    private const int SPIF_UPDATEINIFILE   = 0x01;
    private const int SPIF_SENDCHANGE      = 0x02;

    // ── Static wallpaper ──────────────────────────────────────────────────────

    /// <summary>Set a static image as wallpaper (any monitor).</summary>
    public void SetWallpaper(string imagePath, WallpaperStyle style = WallpaperStyle.Fill)
    {
        if (!File.Exists(imagePath))
            throw new FileNotFoundException("Wallpaper file not found.", imagePath);

        SetWallpaperStyle(style);
        SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, imagePath,
            SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
    }

    /// <summary>Set solid color background (no image).</summary>
    public void SetSolidColor(System.Drawing.Color color)
    {
        // Clear wallpaper first
        SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, "", SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);

        // Set background color in registry
        using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Colors", writable: true)!;
        key.SetValue("Background", $"{color.R} {color.G} {color.B}", RegistryValueKind.String);

        // Also update via HKCU Desktop
        using var desk = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", writable: true)!;
        desk.SetValue("Wallpaper", "", RegistryValueKind.String);
    }

    private static void SetWallpaperStyle(WallpaperStyle style)
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", writable: true)!;

        var (wallpaperStyle, tileWallpaper) = style switch
        {
            WallpaperStyle.Fill    => ("10", "0"),
            WallpaperStyle.Fit     => ("6",  "0"),
            WallpaperStyle.Stretch => ("2",  "0"),
            WallpaperStyle.Tile    => ("0",  "1"),
            WallpaperStyle.Center  => ("0",  "0"),
            WallpaperStyle.Span    => ("22", "0"),
            _                      => ("10", "0")
        };

        key.SetValue("WallpaperStyle", wallpaperStyle, RegistryValueKind.String);
        key.SetValue("TileWallpaper", tileWallpaper, RegistryValueKind.String);
    }

    // ── Multi-monitor via IDesktopWallpaper COM ───────────────────────────────

    [ComImport, Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F93B")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDesktopWallpaper
    {
        void SetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string? monitorID,
                          [MarshalAs(UnmanagedType.LPWStr)] string  wallpaper);
        [return: MarshalAs(UnmanagedType.LPWStr)]
        string GetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string? monitorID);
        [return: MarshalAs(UnmanagedType.LPWStr)]
        string GetMonitorDevicePathAt(uint monitorIndex);
        uint GetMonitorDevicePathCount();
        void GetMonitorRECT(string monitorID, out RECT displayRect);
        void SetBackgroundColor(uint color);
        uint GetBackgroundColor();
        void SetPosition(DesktopSlideshowDirection position);
        void GetPosition(out DesktopSlideshowDirection position);
        void SetSlideshow(object? items);
        void GetSlideshow(out object? items);
        void SetSlideshowOptions(int options, uint slideshowTick);
        void GetSlideshowOptions(out int options, out uint slideshowTick);
        void AdvanceSlideshow(string? monitorID, int direction);
        void GetStatus(out int state);
        void Enable(bool enable);
    }

    private enum DesktopSlideshowDirection { Forward = 0, Backward = 1 }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [ComImport, Guid("C2CF3110-460E-4fc1-B9D0-8A1C0C9CC4BD")]
    private class DesktopWallpaperClass { }

    /// <summary>Set different wallpaper per monitor.</summary>
    public void SetPerMonitorWallpaper(string[] imagePaths)
    {
        var wallpaper = (IDesktopWallpaper)new DesktopWallpaperClass();
        uint count = wallpaper.GetMonitorDevicePathCount();

        for (uint i = 0; i < count && i < (uint)imagePaths.Length; i++)
        {
            var monitorId = wallpaper.GetMonitorDevicePathAt(i);
            if (File.Exists(imagePaths[i]))
                wallpaper.SetWallpaper(monitorId, imagePaths[i]);
        }
    }

    // ── Lively Wallpaper integration ──────────────────────────────────────────

    /// <summary>Returns path to lively.exe if installed, null otherwise.</summary>
    public string? FindLively()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", "Lively Wallpaper", "Lively.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Lively Wallpaper", "Lively.exe"),
            @"C:\Program Files\Lively Wallpaper\Lively.exe"
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    public bool IsLivelyInstalled() => FindLively() != null;

    /// <summary>Install Lively Wallpaper via winget (runs silently).</summary>
    public async Task<bool> InstallLively()
    {
        try
        {
            var psi = new ProcessStartInfo("winget",
                "install --id rocksdanister.LivelyWallpaper --silent --accept-package-agreements --accept-source-agreements")
            {
                UseShellExecute = false,
                CreateNoWindow  = true
            };
            var process = Process.Start(psi)!;
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch { return false; }
    }

    /// <summary>
    /// Set an animated wallpaper via Lively CLI.
    /// Supports: .mp4, .gif, .html, .url (Shadertoy), local .exe
    /// </summary>
    public async Task<bool> SetLivelyWallpaper(string filePath, int monitor = 0)
    {
        var lively = FindLively();
        if (lively == null) return false;

        var psi = new ProcessStartInfo(lively,
            $"setwp --monitor {monitor} --path \"{filePath}\"")
        {
            UseShellExecute = false,
            CreateNoWindow  = true
        };
        var process = Process.Start(psi)!;
        await process.WaitForExitAsync();
        return process.ExitCode == 0;
    }

    /// <summary>Stop all Lively wallpapers and return to static.</summary>
    public async Task StopLively()
    {
        var lively = FindLively();
        if (lively == null) return;

        var psi = new ProcessStartInfo(lively, "closewp --monitor -1")
        {
            UseShellExecute = false,
            CreateNoWindow  = true
        };
        var process = Process.Start(psi)!;
        await process.WaitForExitAsync();
    }
}
#endif
