using System.Text.Json.Serialization;

namespace Win11Tweaker.Core.Profile;

/// <summary>
/// Complete snapshot of all user customizations.
/// Serialized as profile.json inside a .w11theme ZIP archive.
/// </summary>
public class StyleProfile
{
    public string Id                { get; set; } = Guid.NewGuid().ToString("N");
    public string Name              { get; set; } = "My Theme";
    public string Description       { get; set; } = "";
    public string Author            { get; set; } = "";
    public int    SchemaVersion     { get; set; } = 1;
    public string WindowsBuildMin   { get; set; } = "22000"; // Win11 baseline
    public DateTime CreatedAt       { get; set; } = DateTime.UtcNow;

    public TaskbarProfile   Taskbar    { get; set; } = new();
    public ExplorerProfile  Explorer   { get; set; } = new();
    public AppearanceProfile Appearance { get; set; } = new();
    public WallpaperProfile Wallpaper  { get; set; } = new();
    public BlurProfile      Blur       { get; set; } = new();
    public FontProfile      Fonts      { get; set; } = new();
    public ToolsProfile     Tools      { get; set; } = new();

    // Raw registry tweaks for power users / future features
    public List<RawRegistryTweak> CustomTweaks { get; set; } = [];
}

// ── Sub-profiles ──────────────────────────────────────────────────────────────

public class TaskbarProfile
{
    public bool ShowSearch      { get; set; } = true;
    public bool ShowWidgets     { get; set; } = true;
    public bool ShowChat        { get; set; } = true;
    public bool CenterAligned   { get; set; } = true;
    public bool SmallIcons      { get; set; } = false;
    public bool AutoHide        { get; set; } = false;
    public bool DarkMode        { get; set; } = true;
    public bool Transparency    { get; set; } = true;
}

public class ExplorerProfile
{
    public bool OldContextMenu      { get; set; } = false;
    public bool ShowExtensions      { get; set; } = false;
    public bool ShowHiddenFiles     { get; set; } = false;
    public bool CompactMode         { get; set; } = false;
    public bool LaunchToThisPC      { get; set; } = true;
    public bool DesktopIconsVisible { get; set; } = true;
}

public class AppearanceProfile
{
    public int  AccentColorArgb       { get; set; } = unchecked((int)0xFF0078D4);
    public bool ShowColorOnTaskbar    { get; set; } = false;
    public string? CustomThemePath   { get; set; }
    public string? ThemeFileName     { get; set; } // relative to assets/themes/
}

public class WallpaperProfile
{
    // "static" | "solid" | "lively"
    public string Mode              { get; set; } = "static";
    public string? LocalPath        { get; set; }
    public string? AssetFileName    { get; set; } // relative to assets/wallpaper/
    public string  Style            { get; set; } = "Fill";
    public int     SolidColorR      { get; set; } = 30;
    public int     SolidColorG      { get; set; } = 30;
    public int     SolidColorB      { get; set; } = 30;
}

public class BlurProfile
{
    // "None" | "Blur" | "Acrylic"
    public string Mode      { get; set; } = "None";
    public int    TintR     { get; set; } = 0;
    public int    TintG     { get; set; } = 0;
    public int    TintB     { get; set; } = 0;
    public float  Opacity   { get; set; } = 0.6f;
}

public class FontProfile
{
    public string? SegoeUiReplacement { get; set; }
    public List<string> InstalledFontAssets { get; set; } = []; // relative to assets/fonts/
}

public class ToolsProfile
{
    public List<string> WindhawkMods  { get; set; } = [];
    public string?      RainmeterLayout { get; set; }
}

public class RawRegistryTweak
{
    public string Hive      { get; set; } = "HKCU";
    public string Key       { get; set; } = "";
    public string ValueName { get; set; } = "";
    public string Value     { get; set; } = "";
    public string Kind      { get; set; } = "DWord";
}
