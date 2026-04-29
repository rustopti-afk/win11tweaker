using System.IO.Compression;
using System.Text.Json;

namespace Win11Tweaker.Core.Profile;

public class ProfileManager
{
    private readonly string _profilesDir;
    private readonly string _activeProfilePath;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ProfileManager()
    {
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Win11Tweaker");

        _profilesDir       = Path.Combine(appData, "profiles");
        _activeProfilePath = Path.Combine(appData, "active_profile.json");

        Directory.CreateDirectory(_profilesDir);
    }

    // ── CRUD ──────────────────────────────────────────────────────────────────

    public async Task SaveProfile(StyleProfile profile)
    {
        var path = Path.Combine(_profilesDir, $"{profile.Id}.json");
        var json = JsonSerializer.Serialize(profile, JsonOpts);
        await File.WriteAllTextAsync(path, json);
    }

    public async Task<StyleProfile?> LoadProfile(string id)
    {
        var path = Path.Combine(_profilesDir, $"{id}.json");
        if (!File.Exists(path)) return null;
        var json = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<StyleProfile>(json, JsonOpts);
    }

    public List<StyleProfile> GetAllProfiles()
    {
        var profiles = new List<StyleProfile>();
        if (!Directory.Exists(_profilesDir)) return profiles;

        foreach (var file in Directory.GetFiles(_profilesDir, "*.json"))
        {
            try
            {
                var json    = File.ReadAllText(file);
                var profile = JsonSerializer.Deserialize<StyleProfile>(json, JsonOpts);
                if (profile != null) profiles.Add(profile);
            }
            catch { }
        }
        return profiles.OrderByDescending(p => p.CreatedAt).ToList();
    }

    public void DeleteProfile(string id)
    {
        var path = Path.Combine(_profilesDir, $"{id}.json");
        if (File.Exists(path)) File.Delete(path);

        // Also delete associated assets
        var assetsDir = Path.Combine(_profilesDir, id);
        if (Directory.Exists(assetsDir))
            Directory.Delete(assetsDir, recursive: true);
    }

    // ── Active profile ────────────────────────────────────────────────────────

    public async Task SetActiveProfile(string id)
    {
        await File.WriteAllTextAsync(_activeProfilePath, id);
    }

    public async Task<StyleProfile?> GetActiveProfile()
    {
        if (!File.Exists(_activeProfilePath)) return null;
        var id = (await File.ReadAllTextAsync(_activeProfilePath)).Trim();
        return await LoadProfile(id);
    }

    // ── Export: save as .w11theme ZIP ─────────────────────────────────────────

    /// <summary>
    /// Export a profile to a .w11theme file (ZIP archive).
    /// Bundles: profile.json + wallpaper + fonts + theme files.
    /// </summary>
    public async Task Export(StyleProfile profile, string outputPath,
        string? screenshotPath = null)
    {
        using var zip = ZipFile.Open(outputPath, ZipArchiveMode.Create);

        // 1. profile.json
        var json = JsonSerializer.Serialize(profile, JsonOpts);
        AddTextEntry(zip, "profile.json", json);

        // 2. Screenshot / preview
        if (screenshotPath != null && File.Exists(screenshotPath))
            zip.CreateEntryFromFile(screenshotPath, "preview.png");

        // 3. Wallpaper asset
        if (profile.Wallpaper.LocalPath != null && File.Exists(profile.Wallpaper.LocalPath))
        {
            var ext  = Path.GetExtension(profile.Wallpaper.LocalPath);
            var dest = $"assets/wallpaper/wallpaper{ext}";
            zip.CreateEntryFromFile(profile.Wallpaper.LocalPath, dest);

            // Update relative path in profile
            profile.Wallpaper.AssetFileName = $"wallpaper{ext}";
        }

        // 4. Font assets
        foreach (var fontPath in profile.Fonts.InstalledFontAssets
            .Where(File.Exists))
        {
            zip.CreateEntryFromFile(fontPath,
                $"assets/fonts/{Path.GetFileName(fontPath)}");
        }

        // 5. Theme file
        if (profile.Appearance.CustomThemePath != null &&
            File.Exists(profile.Appearance.CustomThemePath))
        {
            zip.CreateEntryFromFile(profile.Appearance.CustomThemePath,
                $"assets/themes/{Path.GetFileName(profile.Appearance.CustomThemePath)}");
        }

        await Task.CompletedTask;
    }

    // ── Import: load from .w11theme ZIP ──────────────────────────────────────

    /// <summary>
    /// Import a .w11theme file and store assets locally.
    /// Returns the imported profile (not yet applied).
    /// </summary>
    public async Task<StyleProfile> Import(string zipPath)
    {
        using var zip = ZipFile.OpenRead(zipPath);

        // 1. Read profile.json
        var profileEntry = zip.GetEntry("profile.json")
            ?? throw new InvalidDataException("Invalid .w11theme: missing profile.json");

        StyleProfile profile;
        using (var stream = profileEntry.Open())
        using (var reader = new StreamReader(stream))
        {
            var json = await reader.ReadToEndAsync();
            profile  = JsonSerializer.Deserialize<StyleProfile>(json, JsonOpts)
                       ?? throw new InvalidDataException("Invalid profile.json");
        }

        // Generate a new ID to avoid collisions with existing profiles
        profile.Id = Guid.NewGuid().ToString("N");

        var assetsDir = Path.Combine(_profilesDir, profile.Id);
        Directory.CreateDirectory(assetsDir);

        // 2. Extract all assets
        foreach (var entry in zip.Entries.Where(e => e.FullName.StartsWith("assets/")))
        {
            var localPath = Path.Combine(assetsDir, entry.FullName.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
            entry.ExtractToFile(localPath, overwrite: true);
        }

        // 3. Extract preview
        var preview = zip.GetEntry("preview.png");
        if (preview != null)
        {
            var previewPath = Path.Combine(assetsDir, "preview.png");
            preview.ExtractToFile(previewPath, overwrite: true);
        }

        // 4. Fix absolute paths in profile to point to extracted assets
        if (profile.Wallpaper.AssetFileName != null)
        {
            profile.Wallpaper.LocalPath = Path.Combine(
                assetsDir, "assets", "wallpaper", profile.Wallpaper.AssetFileName);
        }

        if (profile.Appearance.ThemeFileName != null)
        {
            profile.Appearance.CustomThemePath = Path.Combine(
                assetsDir, "assets", "themes", profile.Appearance.ThemeFileName);
        }

        // 5. Save profile locally
        await SaveProfile(profile);
        return profile;
    }

    // ── Snapshot current state → profile ─────────────────────────────────────

    /// <summary>
    /// Capture the current system state into a new StyleProfile.
    /// Reads actual registry values to populate the profile fields.
    /// </summary>
    public static StyleProfile SnapshotCurrent(string name = "Current State")
    {
        var profile = new StyleProfile
        {
            Name      = name,
            CreatedAt = DateTime.UtcNow
        };

#if WINDOWS
        using var personalize = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        using var advanced = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced");
        using var search = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Search");

        // Taskbar
        profile.Taskbar.ShowSearch = (int?)search?.GetValue("SearchboxTaskbarMode") != 0;
        profile.Taskbar.ShowWidgets = (int?)advanced?.GetValue("TaskbarDa") != 0;
        profile.Taskbar.ShowChat    = (int?)advanced?.GetValue("TaskbarMn") != 0;
        profile.Taskbar.CenterAligned = (int?)advanced?.GetValue("TaskbarAl", 1) == 1;
        profile.Taskbar.SmallIcons    = (int?)advanced?.GetValue("TaskbarSi") == 0;
        profile.Taskbar.DarkMode      = (int?)personalize?.GetValue("AppsUseLightTheme") == 0;
        profile.Taskbar.Transparency  = (int?)personalize?.GetValue("EnableTransparency", 1) == 1;

        // Appearance
        var accentColor = (int?)personalize?.GetValue("AccentColor");
        if (accentColor.HasValue) profile.Appearance.AccentColorArgb = accentColor.Value;
        profile.Appearance.ShowColorOnTaskbar = (int?)personalize?.GetValue("ColorPrevalence") == 1;

        // Explorer
        profile.Explorer.OldContextMenu = Microsoft.Win32.Registry.CurrentUser
            .OpenSubKey(@"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32") != null;
        profile.Explorer.ShowExtensions  = (int?)advanced?.GetValue("HideFileExt") == 0;
        profile.Explorer.ShowHiddenFiles = (int?)advanced?.GetValue("Hidden") == 1;
        profile.Explorer.CompactMode     = (int?)advanced?.GetValue("UseCompactMode") == 1;
#endif
        return profile;
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private static void AddTextEntry(ZipArchive zip, string entryName, string content)
    {
        var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(content);
    }
}
