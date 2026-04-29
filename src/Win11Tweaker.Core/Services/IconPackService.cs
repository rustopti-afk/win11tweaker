#if WINDOWS
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;

namespace Win11Tweaker.Core.Services;

// ── Manifest model ────────────────────────────────────────────────────────────

public class IconPackManifest
{
    [JsonPropertyName("name")]        public string Name    { get; set; } = "";
    [JsonPropertyName("version")]     public string Version { get; set; } = "1.0";
    [JsonPropertyName("author")]      public string Author  { get; set; } = "";
    [JsonPropertyName("preview")]     public string Preview { get; set; } = "";
    [JsonPropertyName("icons")]       public List<IconEntry> Icons { get; set; } = [];
}

public class IconEntry
{
    // "clsid" | "extension" | "folder_default"
    [JsonPropertyName("target")]  public string Target    { get; set; } = "";
    [JsonPropertyName("file")]    public string File      { get; set; } = "";

    // For target=clsid
    [JsonPropertyName("clsid")]   public string? Clsid    { get; set; }
    [JsonPropertyName("states")]  public Dictionary<string, string>? States { get; set; }

    // For target=extension
    [JsonPropertyName("ext")]     public string? Extension { get; set; }

    // For target=folder_default — applies to all folders
    [JsonPropertyName("index")]   public int Index { get; set; }
}

// Known CLSIDs for common system icons
public static class SystemClsids
{
    public const string RecycleBinFull  = "{645FF040-5081-101B-9F08-00AA002F954E}";
    public const string ThisPC          = "{20D04FE0-3AEA-1069-A2D8-08002B30309D}";
    public const string UserFilesFolder = "{59031A47-3F72-44A7-89C5-5595FE6B30EE}";
    public const string Network         = "{F02C1A0D-BE21-4350-88B0-7367FC96EF3C}";
}

// ── Service ───────────────────────────────────────────────────────────────────

public class IconPackService
{
    private readonly string _packsDir;

    public IconPackService()
    {
        _packsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Win11Tweaker", "icon_packs");
        Directory.CreateDirectory(_packsDir);
    }

    /// <summary>
    /// Load icon pack from a .zip file.
    /// Extracts to %AppData%\Win11Tweaker\icon_packs\{packName}\
    /// and returns the parsed manifest.
    /// </summary>
    public IconPackManifest LoadPack(string zipPath)
    {
        var packName = Path.GetFileNameWithoutExtension(zipPath);
        var extractDir = Path.Combine(_packsDir, packName);

        if (Directory.Exists(extractDir))
            Directory.Delete(extractDir, recursive: true);

        ZipFile.ExtractToDirectory(zipPath, extractDir);

        var manifestPath = Path.Combine(extractDir, "manifest.json");
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException("manifest.json not found in icon pack ZIP.");

        var json = File.ReadAllText(manifestPath);
        return JsonSerializer.Deserialize<IconPackManifest>(json)
               ?? throw new InvalidDataException("Invalid manifest.json");
    }

    /// <summary>
    /// Apply all icons from the pack.
    /// Returns count of successfully applied icons.
    /// </summary>
    public int ApplyPack(IconPackManifest manifest, string packDir)
    {
        int applied = 0;
        foreach (var entry in manifest.Icons)
        {
            try
            {
                switch (entry.Target)
                {
                    case "clsid"          when entry.Clsid != null:
                        ApplyClsidIcon(entry.Clsid, Path.Combine(packDir, entry.File));
                        applied++;
                        break;

                    case "clsid_states"   when entry.Clsid != null && entry.States != null:
                        ApplyClsidStates(entry.Clsid, entry.States, packDir);
                        applied++;
                        break;

                    case "extension"      when entry.Extension != null:
                        ApplyExtensionIcon(entry.Extension, Path.Combine(packDir, entry.File));
                        applied++;
                        break;

                    case "folder_default":
                        ApplyDefaultFolderIcon(Path.Combine(packDir, entry.File));
                        applied++;
                        break;
                }
            }
            catch { /* one bad icon shouldn't stop the rest */ }
        }

        // Notify shell to refresh icons
        SHChangeNotify(0x8000000, 0, IntPtr.Zero, IntPtr.Zero);
        return applied;
    }

    // ── Icon application methods ──────────────────────────────────────────────

    private static void ApplyClsidIcon(string clsid, string iconPath)
    {
        // HKCU\Software\Classes\CLSID\{...}\DefaultIcon
        using var key = Registry.CurrentUser.CreateSubKey(
            $@"Software\Classes\CLSID\{clsid}\DefaultIcon");
        key.SetValue("", iconPath, RegistryValueKind.String);
    }

    private static void ApplyClsidStates(string clsid, Dictionary<string, string> states, string packDir)
    {
        // Recycle bin has "empty" and "full" states
        // HKCU\Software\Classes\CLSID\{645FF040...}\DefaultIcon
        //   default = full icon
        //   "empty" = empty icon
        using var key = Registry.CurrentUser.CreateSubKey(
            $@"Software\Classes\CLSID\{clsid}\DefaultIcon");

        if (states.TryGetValue("full", out var full))
            key.SetValue("", Path.Combine(packDir, full), RegistryValueKind.String);
        if (states.TryGetValue("empty", out var empty))
            key.SetValue("empty", Path.Combine(packDir, empty), RegistryValueKind.String);
    }

    private static void ApplyExtensionIcon(string ext, string iconPath)
    {
        // HKCU\Software\Classes\.<ext>\DefaultIcon
        var extKey = ext.StartsWith('.') ? ext : $".{ext}";
        using var key = Registry.CurrentUser.CreateSubKey(
            $@"Software\Classes\{extKey}\DefaultIcon");
        key.SetValue("", iconPath, RegistryValueKind.String);
    }

    private static void ApplyDefaultFolderIcon(string iconPath)
    {
        // Changes the default icon for all folders without a custom Desktop.ini
        using var key = Registry.CurrentUser.CreateSubKey(
            @"Software\Microsoft\Windows\Shell\Bags\AllFolders\Shell");
        key.SetValue("Icon", iconPath, RegistryValueKind.String);
    }

    /// <summary>Apply a custom icon to a specific folder via Desktop.ini</summary>
    public static void ApplyFolderIcon(string folderPath, string iconPath)
    {
        // Make folder read-write temporarily
        var iniPath = Path.Combine(folderPath, "Desktop.ini");

        var lines = new[]
        {
            "[.ShellClassInfo]",
            $"IconResource={iconPath},0",
            "IconIndex=0",
            "[ViewState]",
            "Mode=",
            "Vid=",
            "FolderType=Generic"
        };

        File.WriteAllLines(iniPath, lines);

        // Set system+hidden attributes on Desktop.ini (required by Explorer)
        File.SetAttributes(iniPath, FileAttributes.Hidden | FileAttributes.System);

        // Mark folder as having custom icon
        var dirInfo = new DirectoryInfo(folderPath);
        dirInfo.Attributes |= FileAttributes.ReadOnly; // Explorer reads this as "has desktop.ini"

        SHChangeNotify(0x00002000, 0x0005, folderPath, null!); // SHCNE_UPDATEDIR
    }

    /// <summary>Get list of all installed icon packs.</summary>
    public List<(string Name, string Dir, string? PreviewPath)> GetInstalledPacks()
    {
        var result = new List<(string, string, string?)>();
        if (!Directory.Exists(_packsDir)) return result;

        foreach (var dir in Directory.GetDirectories(_packsDir))
        {
            var manifestPath = Path.Combine(dir, "manifest.json");
            if (!File.Exists(manifestPath)) continue;

            try
            {
                var json = File.ReadAllText(manifestPath);
                var manifest = JsonSerializer.Deserialize<IconPackManifest>(json);
                if (manifest == null) continue;

                var previewPath = manifest.Preview != null
                    ? Path.Combine(dir, manifest.Preview)
                    : null;

                result.Add((manifest.Name, dir, previewPath));
            }
            catch { }
        }
        return result;
    }

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(int wEventId, int uFlags, IntPtr dwItem1, IntPtr dwItem2);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern void SHChangeNotify(int wEventId, int uFlags, string dwItem1, string dwItem2);
}
#endif
