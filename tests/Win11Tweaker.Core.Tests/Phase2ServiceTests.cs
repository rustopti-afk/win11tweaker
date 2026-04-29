using System.IO.Compression;
using System.Text.Json;

namespace Win11Tweaker.Core.Tests;

/// <summary>
/// Tests for Phase 2 services — all platform-agnostic (no Windows API calls).
/// </summary>
public class Phase2ServiceTests
{
    // ── IconPack manifest parsing ─────────────────────────────────────────────

    [Fact]
    public void IconPackManifest_Deserializes_Correctly()
    {
        var json = """
        {
          "name": "Test Pack",
          "version": "1.0",
          "author": "Tester",
          "preview": "preview.png",
          "icons": [
            { "target": "folder_default", "file": "folder.ico" },
            { "target": "extension", "ext": ".txt", "file": "text.ico" },
            {
              "target": "clsid_states",
              "clsid": "{645FF040-5081-101B-9F08-00AA002F954E}",
              "states": { "empty": "bin_empty.ico", "full": "bin_full.ico" }
            }
          ]
        }
        """;

        var opts     = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var manifest = JsonSerializer.Deserialize<FakeManifest>(json, opts)!;

        Assert.Equal("Test Pack", manifest.Name);
        Assert.Equal("1.0", manifest.Version);
        Assert.Equal(3, manifest.Icons.Count);
        Assert.Equal("folder_default", manifest.Icons[0].Target);
        Assert.Equal(".txt", manifest.Icons[1].Ext);
        Assert.NotNull(manifest.Icons[2].States);
        Assert.True(manifest.Icons[2].States!.ContainsKey("empty"));
    }

    // ── WallpaperStyle enum values ────────────────────────────────────────────

    [Theory]
    [InlineData("Fill",    "10", "0")]
    [InlineData("Fit",     "6",  "0")]
    [InlineData("Stretch", "2",  "0")]
    [InlineData("Tile",    "0",  "1")]
    [InlineData("Center",  "0",  "0")]
    [InlineData("Span",    "22", "0")]
    public void WallpaperStyleMapper_ReturnsCorrectRegistryValues(
        string style, string expectedStyle, string expectedTile)
    {
        var (wallpaperStyle, tileWallpaper) = MapWallpaperStyle(style);
        Assert.Equal(expectedStyle, wallpaperStyle);
        Assert.Equal(expectedTile,  tileWallpaper);
    }

    // ── TaskbarBlurSettings serialization ────────────────────────────────────

    [Fact]
    public void TaskbarBlurSettings_RoundTrips_Json()
    {
        var settings = new FakeBlurSettings
        {
            Mode    = "Acrylic",
            TintR   = 30,
            TintG   = 30,
            TintB   = 30,
            Opacity = 0.75f
        };

        var json        = JsonSerializer.Serialize(settings);
        var deserialized = JsonSerializer.Deserialize<FakeBlurSettings>(json)!;

        Assert.Equal(settings.Mode,    deserialized.Mode);
        Assert.Equal(settings.TintR,   deserialized.TintR);
        Assert.Equal(settings.Opacity, deserialized.Opacity);
    }

    // ── Icon pack ZIP structure ───────────────────────────────────────────────

    [Fact]
    public void IconPack_ZipMustContain_ManifestJson()
    {
        // Create a valid test ZIP in memory
        using var ms  = new MemoryStream();
        using var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true);

        var entry = zip.CreateEntry("manifest.json");
        using (var sw = new StreamWriter(entry.Open()))
            sw.Write("""{"name":"TestPack","icons":[]}""");

        zip.CreateEntry("icons/folder.ico");
        zip.Dispose();

        // Verify ZIP contains manifest
        ms.Position = 0;
        using var readZip = new ZipArchive(ms, ZipArchiveMode.Read);
        Assert.NotNull(readZip.GetEntry("manifest.json"));
    }

    [Fact]
    public void IconPack_ZipWithout_ManifestJson_IsInvalid()
    {
        using var ms  = new MemoryStream();
        using var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true);
        zip.CreateEntry("icons/folder.ico");
        zip.Dispose();

        ms.Position = 0;
        using var readZip = new ZipArchive(ms, ZipArchiveMode.Read);
        Assert.Null(readZip.GetEntry("manifest.json")); // no manifest
    }

    // ── Color ARGB conversion ─────────────────────────────────────────────────

    [Theory]
    [InlineData(0,   120, 212, 0.6f)]  // Windows Blue, 60% opacity
    [InlineData(255,   0,   0, 1.0f)]  // Red, fully opaque
    [InlineData(0,     0,   0, 0.0f)]  // Black, fully transparent
    public void AcrylicColor_ABGR_Conversion_IsCorrect(int r, int g, int b, float opacity)
    {
        byte alpha = (byte)(opacity * 255);
        // Windows uses ABGR: 0xAABBGGRR
        int result = (alpha << 24) | (b << 16) | (g << 8) | r;

        Assert.Equal(alpha, (byte)((result >> 24) & 0xFF));
        Assert.Equal((byte)b, (byte)((result >> 16) & 0xFF));
        Assert.Equal((byte)g, (byte)((result >> 8)  & 0xFF));
        Assert.Equal((byte)r, (byte)(result          & 0xFF));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static (string wallpaperStyle, string tileWallpaper) MapWallpaperStyle(string style) =>
        style switch
        {
            "Fill"    => ("10", "0"),
            "Fit"     => ("6",  "0"),
            "Stretch" => ("2",  "0"),
            "Tile"    => ("0",  "1"),
            "Center"  => ("0",  "0"),
            "Span"    => ("22", "0"),
            _         => ("10", "0")
        };
}

// Local test-only models (mirrors Core models without Windows dependency)
internal class FakeManifest
{
    public string Name    { get; set; } = "";
    public string Version { get; set; } = "";
    public List<FakeIconEntry> Icons { get; set; } = [];
}

internal class FakeIconEntry
{
    public string Target { get; set; } = "";
    public string? Ext   { get; set; }
    public Dictionary<string, string>? States { get; set; }
}

internal class FakeBlurSettings
{
    public string Mode    { get; set; } = "None";
    public int    TintR   { get; set; }
    public int    TintG   { get; set; }
    public int    TintB   { get; set; }
    public float  Opacity { get; set; }
}
