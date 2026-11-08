using System.Runtime.InteropServices;
using System.Text;

namespace Nordwall;

/// <summary>
/// Detects ordinary maximized windows and borderless/exclusive fullscreen
/// windows without hooks or polling other processes' internal state.
/// </summary>
internal static class FullscreenDetector
{
    private const int DwmwaExtendedFrameBounds = 9;
    private const int DwmwaCloaked = 14;
    private const int GwlExStyle = -20;
    private const long WsExToolWindow = 0x00000080;
    private const int EdgeTolerance = 12;

    public static HashSet<string> FindCoveredDisplays(IEnumerable<Screen> displays)
    {
        Screen[] screens = displays.ToArray();
        var covered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        EnumWindows((handle, _) =>
        {
            if (!IsWindowVisible(handle) || IsIconic(handle))
                return true;

            GetWindowThreadProcessId(handle, out uint processId);
            if (processId == (uint)Environment.ProcessId)
                return true;

            if ((GetWindowLongPtr(handle, GwlExStyle).ToInt64() & WsExToolWindow) != 0)
                return true;

            if (DwmGetCloaked(handle, DwmwaCloaked, out int cloaked, sizeof(int)) == 0
                && cloaked != 0)
                return true;

            var className = new StringBuilder(128);
            GetClassName(handle, className, className.Capacity);
            if (className.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd"
                or "Shell_SecondaryTrayWnd")
                return true;

            NativeRect bounds;
            if (DwmGetRect(handle, DwmwaExtendedFrameBounds, out bounds,
                    Marshal.SizeOf<NativeRect>()) != 0 && !GetWindowRect(handle, out bounds))
                return true;

            Rectangle window = Rectangle.FromLTRB(
                bounds.Left, bounds.Top, bounds.Right, bounds.Bottom);

            foreach (Screen display in screens)
            {
                // Fullscreen games usually cover Bounds; normal maximized
                // windows cover WorkingArea, leaving room for the taskbar.
                if (Covers(window, display.Bounds)
                    || (IsZoomed(handle) && Covers(window, display.WorkingArea)))
                    covered.Add(display.DeviceName);
            }

            return covered.Count != screens.Length;
        }, IntPtr.Zero);

        return covered;
    }

    private static bool Covers(Rectangle window, Rectangle display) =>
        display.Width > 0 && display.Height > 0
        && window.Left <= display.Left + EdgeTolerance
        && window.Top <= display.Top + EdgeTolerance
        && window.Right >= display.Right - EdgeTolerance
        && window.Bottom >= display.Bottom - EdgeTolerance;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private delegate bool EnumWindowsCallback(IntPtr window, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr handle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr handle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsZoomed(IntPtr handle);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr handle, int index);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr handle, StringBuilder name, int maxLength);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr handle, out NativeRect bounds);

    [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")]
    private static extern int DwmGetRect(IntPtr handle, int attribute,
        out NativeRect bounds, int size);

    [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")]
    private static extern int DwmGetCloaked(IntPtr handle, int attribute,
        out int cloaked, int size);
}
