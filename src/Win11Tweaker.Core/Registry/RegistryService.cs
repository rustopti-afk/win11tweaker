#if WINDOWS
using Microsoft.Win32;

namespace Win11Tweaker.Core.Registry;

/// <summary>
/// Wrapper around Windows Registry with typed read/write and safe error handling.
/// </summary>
public class RegistryService
{
    public object? GetValue(RegistryHive hive, string keyPath, string valueName)
    {
        using var key = OpenKey(hive, keyPath, writable: false);
        return key?.GetValue(valueName);
    }

    public RegistryValueKind? GetValueKind(RegistryHive hive, string keyPath, string valueName)
    {
        using var key = OpenKey(hive, keyPath, writable: false);
        if (key == null) return null;
        try { return key.GetValueKind(valueName); }
        catch (IOException) { return null; }
    }

    public void SetValue(RegistryHive hive, string keyPath, string valueName,
        object value, RegistryValueKind kind)
    {
        using var key = OpenOrCreateKey(hive, keyPath);
        key.SetValue(valueName, value, kind);
    }

    public void DeleteValue(RegistryHive hive, string keyPath, string valueName)
    {
        using var key = OpenKey(hive, keyPath, writable: true);
        key?.DeleteValue(valueName, throwOnMissingValue: false);
    }

    public void DeleteKey(RegistryHive hive, string keyPath)
    {
        var (baseKey, subPath) = SplitPath(hive, keyPath);
        baseKey.DeleteSubKeyTree(subPath, throwOnMissingSubKey: false);
    }

    public bool KeyExists(RegistryHive hive, string keyPath)
    {
        using var key = OpenKey(hive, keyPath, writable: false);
        return key != null;
    }

    private static RegistryKey OpenKey(RegistryHive hive, string keyPath, bool writable)
    {
        var root = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
        return root.OpenSubKey(keyPath, writable)!;
    }

    private static RegistryKey OpenOrCreateKey(RegistryHive hive, string keyPath)
    {
        var root = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
        return root.CreateSubKey(keyPath, writable: true);
    }

    private static (RegistryKey baseKey, string subPath) SplitPath(RegistryHive hive, string keyPath)
    {
        var root = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
        var lastSlash = keyPath.LastIndexOf('\\');
        if (lastSlash < 0) return (root, keyPath);
        var parent = root.OpenSubKey(keyPath[..lastSlash], writable: true)!;
        return (parent, keyPath[(lastSlash + 1)..]);
    }
}
#endif
