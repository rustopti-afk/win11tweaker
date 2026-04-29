#if WINDOWS
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Win11Tweaker.Core.Services;

public enum ThemeType { System, Custom }

public class ThemeInfo
{
    public string Name        { get; set; } = "";
    public string Path        { get; set; } = "";
    public ThemeType Type     { get; set; }
    public string? PreviewPath { get; set; }
}

/// <summary>
/// Manages Windows visual themes (.theme + .msstyles).
/// Custom unsigned themes require SecureUxTheme to bypass signature check.
/// </summary>
public class ThemeService
{
    // SecureUxTheme marker — if this registry value exists, patching is done
    private const string SutMarkerKey  = @"SOFTWARE\SecureUxTheme";
    private const string SutMarkerVal  = "Installed";
    private const string SystemThemeDir = @"C:\Windows\Resources\Themes";

    // ── SecureUxTheme ─────────────────────────────────────────────────────────

    public bool IsSecureUxThemeInstalled()
    {
        using var key = Registry.LocalMachine.OpenSubKey(SutMarkerKey);
        return key?.GetValue(SutMarkerVal) != null;
    }

    /// <summary>
    /// Install SecureUxTheme (requires admin — call via ElevatedHelperClient).
    /// Downloads latest release from GitHub and runs the installer.
    /// </summary>
    public async Task<bool> InstallSecureUxTheme(string secureUxExePath)
    {
        if (!File.Exists(secureUxExePath)) return false;

        try
        {
            // SecureUxTheme installer accepts /install flag
            var psi = new ProcessStartInfo(secureUxExePath, "/install")
            {
                UseShellExecute = true,
                Verb = "runas"
            };
            var process = Process.Start(psi)!;
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch { return false; }
    }

    // ── Theme enumeration ─────────────────────────────────────────────────────

    /// <summary>Get all .theme files from system and user theme directories.</summary>
    public List<ThemeInfo> GetInstalledThemes()
    {
        var themes = new List<ThemeInfo>();
        var dirs = new[]
        {
            SystemThemeDir,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft", "Windows", "Themes")
        };

        foreach (var dir in dirs)
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var file in Directory.GetFiles(dir, "*.theme", SearchOption.TopDirectoryOnly))
            {
                themes.Add(new ThemeInfo
                {
                    Name = Path.GetFileNameWithoutExtension(file),
                    Path = file,
                    Type = ThemeType.System,
                    PreviewPath = FindThemePreview(file)
                });
            }
        }
        return themes;
    }

    /// <summary>
    /// Apply a .theme file. For unsigned .msstyles themes,
    /// SecureUxTheme must be installed first.
    /// </summary>
    public async Task<bool> ApplyTheme(string themePath)
    {
        if (!File.Exists(themePath)) return false;

        try
        {
            // Windows opens .theme files with the theme service via rundll32
            var psi = new ProcessStartInfo("rundll32.exe",
                $"themecpl.dll,OpenThemeAction \"{themePath}\"")
            {
                UseShellExecute = false
            };
            var process = Process.Start(psi)!;
            await process.WaitForExitAsync();
            return true;
        }
        catch
        {
            // Fallback: open via shell (prompts user to apply)
            Process.Start(new ProcessStartInfo(themePath) { UseShellExecute = true });
            return true;
        }
    }

    /// <summary>
    /// Install a custom .msstyles theme pack (folder with .msstyles + resources).
    /// Copies to user themes directory and generates a .theme file.
    /// </summary>
    public string InstallCustomTheme(string msstylesPath)
    {
        if (!File.Exists(msstylesPath))
            throw new FileNotFoundException("Theme file not found.", msstylesPath);

        var userThemesDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "Windows", "Themes");
        Directory.CreateDirectory(userThemesDir);

        var themeName    = Path.GetFileNameWithoutExtension(msstylesPath);
        var themeDestDir = Path.Combine(userThemesDir, themeName);
        Directory.CreateDirectory(themeDestDir);

        // Copy .msstyles and its resource folder (if exists)
        var destMsstyles = Path.Combine(themeDestDir, Path.GetFileName(msstylesPath));
        File.Copy(msstylesPath, destMsstyles, overwrite: true);

        var resourceDir = Path.Combine(
            Path.GetDirectoryName(msstylesPath)!, themeName + "_Resources");
        if (Directory.Exists(resourceDir))
        {
            var destResources = Path.Combine(themeDestDir, themeName + "_Resources");
            CopyDirectory(resourceDir, destResources);
        }

        // Generate .theme file pointing to the .msstyles
        var themeFilePath = Path.Combine(userThemesDir, themeName + ".theme");
        var themeContent  = GenerateThemeFile(themeName, destMsstyles);
        File.WriteAllText(themeFilePath, themeContent);

        return themeFilePath;
    }

    private static string GenerateThemeFile(string name, string msstylesPath) => $"""
        [Theme]
        DisplayName={name}
        ThemeId={{{Guid.NewGuid()}}}

        [VisualStyles]
        Path={msstylesPath}
        ColorStyle=NormalColor
        Size=NormalSize
        ColorizationColor=0X6B74B8FC
        Transparency=1

        [boot]
        SCRNSAVE.EXE=

        [MasterThemeSelector]
        MTSM=RJSPBS
        """;

    private static string? FindThemePreview(string themePath)
    {
        // Look for preview.png/jpg in same directory as .theme file
        var dir = Path.GetDirectoryName(themePath)!;
        var name = Path.GetFileNameWithoutExtension(themePath);
        return new[] { "preview.png", "preview.jpg", $"{name}.png", $"{name}.jpg" }
            .Select(f => Path.Combine(dir, f))
            .FirstOrDefault(File.Exists);
    }

    private static void CopyDirectory(string src, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var file in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(src, file);
            var destFile = Path.Combine(dest, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);
            File.Copy(file, destFile, overwrite: true);
        }
    }
}
#endif
