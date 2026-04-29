#if WINDOWS
using System.Runtime.InteropServices;
using System.Text;

namespace Win11Tweaker.Core.WinApi;

// Undocumented Windows API for taskbar/window composition effects.
// Works on Win10 1803+ and all Win11 builds tested as of 26100.

internal enum AccentState
{
    Disabled               = 0,
    EnableGradient         = 1,
    EnableTransparent      = 2,
    EnableBlurBehind       = 3,   // Aero blur
    EnableAcrylicBlurBehind = 4,  // Acrylic (Win10 1803+)
    EnableHostBackdrop     = 5,
    Invalid                = 6
}

[StructLayout(LayoutKind.Sequential)]
internal struct AccentPolicy
{
    public AccentState AccentState;
    public int         AccentFlags;
    public int         GradientColor;  // ABGR: 0xAABBGGRR
    public int         AnimationId;
}

[StructLayout(LayoutKind.Sequential)]
internal struct WindowCompositionAttribData
{
    public int    Attrib;  // 19 = WCA_ACCENT_POLICY
    public IntPtr pvData;
    public int    cbData;
}

internal static class NativeMethods
{
    [DllImport("user32.dll")]
    internal static extern bool SetWindowCompositionAttribute(
        IntPtr hwnd, ref WindowCompositionAttribData data);

    [DllImport("user32.dll")]
    internal static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    internal delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int GetClassName(IntPtr hwnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    internal static extern bool IsWindowVisible(IntPtr hwnd);
}

/// <summary>
/// Applies Acrylic/Blur effects to the Windows taskbar via SetWindowCompositionAttribute.
/// Changes are in-memory only — lost when Explorer restarts, so we re-apply via a watchdog timer.
/// </summary>
public class TaskbarBlurApi
{
    private const int WCA_ACCENT_POLICY = 19;

    /// <summary>
    /// Apply acrylic blur to all taskbar windows (primary + secondary monitors).
    /// tintColor format: System.Drawing.Color or pass ARGB int directly.
    /// opacity: 0.0 (fully transparent) .. 1.0 (opaque)
    /// </summary>
    public static void ApplyAcrylic(System.Drawing.Color tintColor, float opacity)
    {
        byte alpha = (byte)(opacity * 255);
        // Windows expects ABGR order (not ARGB)
        int gradientColor = (alpha << 24) | (tintColor.B << 16) | (tintColor.G << 8) | tintColor.R;

        foreach (var hwnd in GetTaskbarHandles())
            SetAccentPolicy(hwnd, AccentState.EnableAcrylicBlurBehind, gradientColor);
    }

    /// <summary>Apply Aero glass blur (lighter, no tint).</summary>
    public static void ApplyBlur()
    {
        foreach (var hwnd in GetTaskbarHandles())
            SetAccentPolicy(hwnd, AccentState.EnableBlurBehind, 0);
    }

    /// <summary>Remove all effects — return to solid taskbar.</summary>
    public static void RemoveEffects()
    {
        foreach (var hwnd in GetTaskbarHandles())
            SetAccentPolicy(hwnd, AccentState.Disabled, 0);
    }

    private static void SetAccentPolicy(IntPtr hwnd, AccentState state, int gradientColor)
    {
        var policy = new AccentPolicy
        {
            AccentState   = state,
            AccentFlags   = 2,
            GradientColor = gradientColor
        };

        int size = Marshal.SizeOf(policy);
        var ptr  = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(policy, ptr, false);
            var data = new WindowCompositionAttribData
            {
                Attrib  = WCA_ACCENT_POLICY,
                pvData  = ptr,
                cbData  = size
            };
            NativeMethods.SetWindowCompositionAttribute(hwnd, ref data);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    /// <summary>Returns HWNDs for all taskbar windows (one per monitor).</summary>
    public static List<IntPtr> GetTaskbarHandles()
    {
        var handles = new List<IntPtr>();
        NativeMethods.EnumWindows((hwnd, _) =>
        {
            if (!NativeMethods.IsWindowVisible(hwnd)) return true;
            var sb = new StringBuilder(256);
            NativeMethods.GetClassName(hwnd, sb, 256);
            var cls = sb.ToString();
            if (cls is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd")
                handles.Add(hwnd);
            return true;
        }, IntPtr.Zero);
        return handles;
    }
}
#endif
