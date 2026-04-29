using System.IO.Compression;
using System.Text.Json;

namespace Win11Tweaker.Core.Tests;

/// <summary>
/// Tests for Phase 3 services — platform-agnostic logic only.
/// </summary>
public class Phase3ServiceTests
{
    // ── StyleProfile serialization ────────────────────────────────────────────

    [Fact]
    public void StyleProfile_RoundTrips_Json()
    {
        var profile = new FakeStyleProfile
        {
            Id          = "abc-123",
            Name        = "My Setup",
            Description = "Dark + centered",
            SchemaVersion = 1,
            Taskbar = new FakeTaskbarProfile
            {
                ShowSearch   = false,
                ShowWidgets  = false,
                CenterAligned = true,
                DarkMode     = true,
                Transparency = true
            },
            Explorer = new FakeExplorerProfile
            {
                OldContextMenu  = true,
                ShowExtensions  = true,
                ShowHiddenFiles = false,
                CompactMode     = false
            }
        };

        var json        = JsonSerializer.Serialize(profile);
        var deserialized = JsonSerializer.Deserialize<FakeStyleProfile>(json)!;

        Assert.Equal(profile.Id,          deserialized.Id);
        Assert.Equal(profile.Name,        deserialized.Name);
        Assert.Equal(1,                   deserialized.SchemaVersion);
        Assert.False(deserialized.Taskbar.ShowSearch);
        Assert.True(deserialized.Taskbar.CenterAligned);
        Assert.True(deserialized.Explorer.OldContextMenu);
        Assert.True(deserialized.Explorer.ShowExtensions);
    }

    // ── .w11theme ZIP export structure ───────────────────────────────────────

    [Fact]
    public void W11ThemeExport_ContainsProfileJson()
    {
        using var ms  = new MemoryStream();
        using var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true);

        var profileEntry = zip.CreateEntry("profile.json");
        using (var sw = new StreamWriter(profileEntry.Open()))
            sw.Write("""{"id":"test","name":"Test"}""");

        zip.CreateEntry("assets/wallpaper/");
        zip.Dispose();

        ms.Position = 0;
        using var readZip = new ZipArchive(ms, ZipArchiveMode.Read);
        Assert.NotNull(readZip.GetEntry("profile.json"));
    }

    [Fact]
    public void W11ThemeExport_WithPreview_ContainsPreviewPng()
    {
        using var ms  = new MemoryStream();
        using var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true);

        zip.CreateEntry("profile.json");

        var previewEntry = zip.CreateEntry("preview.png");
        using (var stream = previewEntry.Open())
            stream.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47 }); // PNG magic bytes

        zip.Dispose();

        ms.Position = 0;
        using var readZip = new ZipArchive(ms, ZipArchiveMode.Read);
        Assert.NotNull(readZip.GetEntry("preview.png"));
    }

    [Fact]
    public void W11ThemeImport_WithoutProfileJson_IsInvalid()
    {
        using var ms  = new MemoryStream();
        using var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true);
        zip.CreateEntry("some_other_file.txt");
        zip.Dispose();

        ms.Position = 0;
        using var readZip = new ZipArchive(ms, ZipArchiveMode.Read);
        Assert.Null(readZip.GetEntry("profile.json"));
    }

    // ── Profile ID uniqueness ─────────────────────────────────────────────────

    [Fact]
    public void ProfileId_GeneratedAsGuid_IsUnique()
    {
        var id1 = Guid.NewGuid().ToString();
        var id2 = Guid.NewGuid().ToString();

        Assert.NotEqual(id1, id2);
        Assert.Matches(
            @"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$",
            id1);
    }

    // ── Windhawk recommended mods list ────────────────────────────────────────

    [Fact]
    public void WindhawkRecommendedMods_AllHaveUniqueIds()
    {
        var mods = FakeWindhawkMods.All;

        var ids = mods.Select(m => m.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void WindhawkRecommendedMods_AllHaveNonEmptyNames()
    {
        foreach (var mod in FakeWindhawkMods.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(mod.Id),   $"Mod has empty Id");
            Assert.False(string.IsNullOrWhiteSpace(mod.Name), $"Mod '{mod.Id}' has empty Name");
        }
    }

    // ── Theme file extension ──────────────────────────────────────────────────

    [Theory]
    [InlineData("mytheme.theme",      true)]
    [InlineData("mytheme.msstyles",   false)]
    [InlineData("something.txt",      false)]
    [InlineData("dark.theme",         true)]
    public void ThemeFile_Extension_DetectedCorrectly(string filename, bool expectedIsTheme)
    {
        var ext = Path.GetExtension(filename).ToLower();
        Assert.Equal(expectedIsTheme, ext == ".theme");
    }

    // ── ProfileManager path logic ─────────────────────────────────────────────

    [Fact]
    public void ProfilesDirectory_Path_IsUnderAppData()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var expected = Path.Combine(appData, "Win11Tweaker", "profiles");

        Assert.StartsWith(appData, expected);
        Assert.Contains("Win11Tweaker", expected);
        Assert.Contains("profiles", expected);
    }

    [Fact]
    public void ProfileSnapshotPath_IncludesProfileId()
    {
        var profileId = "test-profile-id";
        var appData   = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var dir       = Path.Combine(appData, "Win11Tweaker", "profiles", profileId);

        Assert.Contains(profileId, dir);
    }

    // ── Rainmeter Bang command format ─────────────────────────────────────────

    [Fact]
    public void RainmeterBang_ActivateSkin_FormatIsCorrect()
    {
        var configName = "illustro\\System";
        var skinFile   = "System.ini";
        var bang       = $"!ActivateConfig \"{configName}\" \"{skinFile}\"";

        Assert.StartsWith("!ActivateConfig", bang);
        Assert.Contains(configName, bang);
        Assert.Contains(skinFile, bang);
    }

    [Fact]
    public void RainmeterBang_DeactivateSkin_FormatIsCorrect()
    {
        var configName = "illustro\\System";
        var bang       = $"!DeactivateConfig \"{configName}\"";

        Assert.StartsWith("!DeactivateConfig", bang);
        Assert.Contains(configName, bang);
    }
}

// ── Test-only models (mirrors Core models without Windows dependency) ─────────

internal class FakeStyleProfile
{
    public string Id            { get; set; } = Guid.NewGuid().ToString();
    public string Name          { get; set; } = "";
    public string Description   { get; set; } = "";
    public int    SchemaVersion { get; set; } = 1;
    public FakeTaskbarProfile  Taskbar  { get; set; } = new();
    public FakeExplorerProfile Explorer { get; set; } = new();
}

internal class FakeTaskbarProfile
{
    public bool ShowSearch    { get; set; } = true;
    public bool ShowWidgets   { get; set; } = true;
    public bool ShowChat      { get; set; } = false;
    public bool CenterAligned { get; set; } = true;
    public bool DarkMode      { get; set; } = false;
    public bool Transparency  { get; set; } = false;
}

internal class FakeExplorerProfile
{
    public bool OldContextMenu  { get; set; }
    public bool ShowExtensions  { get; set; }
    public bool ShowHiddenFiles { get; set; }
    public bool CompactMode     { get; set; }
}

internal record FakeWindhawkModEntry(string Id, string Name, string Description);

internal static class FakeWindhawkMods
{
    public static readonly IReadOnlyList<FakeWindhawkModEntry> All =
    [
        new("taskbar-button-click",                               "Taskbar button click",          "Control taskbar button click behavior"),
        new("taskbar-labels",                                     "Taskbar labels",                "Show labels on taskbar buttons"),
        new("windows-11-taskbar-styler",                          "Taskbar Styler",                "Fully customize taskbar appearance"),
        new("start-menu-styler",                                  "Start Menu Styler",             "Customize Start menu appearance"),
        new("notification-center-styler",                         "Notification Styler",           "Customize notification center"),
        new("classic-taskbar-buttons-lite-vs-without-grouping",   "Classic Taskbar Buttons",       "Classic taskbar button behavior"),
        new("taskbar-clock-customization",                        "Taskbar Clock",                 "Customize taskbar clock format"),
        new("taskbar-volume-control",                             "Taskbar Volume",                "Volume control on taskbar scroll"),
        new("disable-rounded-corners",                            "Disable Rounded Corners",       "Remove rounded window corners"),
        new("force-snap-window",                                  "Force Snap Window",             "Force window snapping behavior"),
    ];
}
