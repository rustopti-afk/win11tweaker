#if WINDOWS
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Win11Tweaker.Core.Services;

public class FontService
{
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern int AddFontResource(string lpFileName);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern bool RemoveFontResource(string lpFileName);

    [DllImport("user32.dll")]
    private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

    private const int HWND_BROADCAST = 0xffff;
    private const int WM_FONTCHANGE  = 0x001D;

    private readonly string _userFontsDir;

    public FontService()
    {
        _userFontsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "Windows", "Fonts");
        Directory.CreateDirectory(_userFontsDir);
    }

    /// <summary>
    /// Install a font for the current user (no admin required).
    /// Copies to %LocalAppData%\Microsoft\Windows\Fonts\ and registers in HKCU.
    /// </summary>
    public void InstallFont(string fontPath)
    {
        if (!File.Exists(fontPath))
            throw new FileNotFoundException("Font file not found.", fontPath);

        var ext = Path.GetExtension(fontPath).ToLowerInvariant();
        if (ext is not ".ttf" and not ".otf" and not ".ttc")
            throw new ArgumentException("Unsupported font format. Use .ttf, .otf, or .ttc");

        var destPath = Path.Combine(_userFontsDir, Path.GetFileName(fontPath));
        File.Copy(fontPath, destPath, overwrite: true);

        // Register in HKCU Fonts
        var fontName = GetFontName(destPath);
        using var key = Registry.CurrentUser.CreateSubKey(
            @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts");
        key.SetValue(fontName, destPath, RegistryValueKind.String);

        // Load into current session
        AddFontResource(destPath);
        SendMessage(new IntPtr(HWND_BROADCAST), WM_FONTCHANGE, 0, 0);
    }

    /// <summary>
    /// Replace Segoe UI system font via FontSubstitutes registry key.
    /// Does NOT require admin — works per-user.
    /// Requires restart to take full effect.
    /// </summary>
    public void ReplaceSegoeUi(string fontName)
    {
        using var key = Registry.CurrentUser.CreateSubKey(
            @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\FontSubstitutes");
        key.SetValue("Segoe UI", fontName, RegistryValueKind.String);
    }

    /// <summary>Restore original Segoe UI font.</summary>
    public void RestoreSegoeUi()
    {
        using var key = Registry.CurrentUser.OpenSubKey(
            @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\FontSubstitutes", writable: true);
        key?.DeleteValue("Segoe UI", throwOnMissingValue: false);
    }

    /// <summary>Get all fonts installed for the current user.</summary>
    public List<string> GetInstalledUserFonts()
    {
        var fonts = new List<string>();
        using var key = Registry.CurrentUser.OpenSubKey(
            @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts");
        if (key == null) return fonts;

        foreach (var name in key.GetValueNames())
            fonts.Add(name);
        return fonts;
    }

    // Gets font name from the file using GDI (simplified — uses filename as fallback)
    private static string GetFontName(string fontPath)
    {
        var fileName = Path.GetFileNameWithoutExtension(fontPath);
        var ext      = Path.GetExtension(fontPath);
        return $"{fileName} (TrueType)";
    }
}
#endif
