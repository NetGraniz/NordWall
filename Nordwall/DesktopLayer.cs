using System.Runtime.InteropServices;

namespace Nordwall;

/// <summary>
/// Uses the community-known, undocumented WorkerW layout. Windows does not
/// guarantee this interface; fail closed instead of covering desktop icons.
/// </summary>
internal static class DesktopLayer
{
    private const int GwlStyle = -16;
    private const int GwlExStyle = -20;
    private const long WsChild = 0x40000000L;
    private const long WsPopup = unchecked((long)0x80000000);
    private const long WsExLayered = 0x00080000L;
    private const uint LwaAlpha = 0x00000002;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpFrameChanged = 0x0020;
    private const uint SwpShowWindow = 0x0040;
    private const uint SmtoAbortIfHung = 0x0002;
    private const uint SpawnWorkerW = 0x052C;

    private readonly record struct DesktopHost(IntPtr Parent, IntPtr Behind, bool RequiresLayered);

    public static bool TryAttach(IntPtr windowHandle, Size size, out string failureReason)
    {
        DesktopHost? host = FindDesktopHost(out failureReason);
        if (host is null)
            return false;

        long style = GetWindowLongPtr(windowHandle, GwlStyle).ToInt64();
        style = (style | WsChild) & ~WsPopup;
        SetWindowLongPtr(windowHandle, GwlStyle, new IntPtr(style));

        if (host.Value.RequiresLayered)
        {
            // Windows 11's raised desktop uses a layered icon view. Its wallpaper
            // surface must also be composited as a fully opaque layered child.
            long exStyle = GetWindowLongPtr(windowHandle, GwlExStyle).ToInt64();
            SetWindowLongPtr(windowHandle, GwlExStyle, new IntPtr(exStyle | WsExLayered));
            if (!SetLayeredWindowAttributes(windowHandle, 0, 255, LwaAlpha))
            {
                failureReason = $"Cannot enable layered rendering (Win32 {Marshal.GetLastWin32Error()}).";
                return false;
            }
        }

        SetParent(windowHandle, host.Value.Parent);
        if (GetParent(windowHandle) != host.Value.Parent)
        {
            failureReason = $"Cannot attach to the desktop (Win32 {Marshal.GetLastWin32Error()}).";
            return false;
        }

        // When parented to Progman, place the wallpaper strictly *behind* the
        // SHELLDLL_DefView icon window. Never allow a normal overlay above icons.
        if (!SetWindowPos(windowHandle, host.Value.Behind, 0, 0, size.Width, size.Height,
                SwpNoActivate | SwpFrameChanged | SwpShowWindow))
        {
            failureReason = $"Cannot position the wallpaper (Win32 {Marshal.GetLastWin32Error()}).";
            return false;
        }

        failureReason = string.Empty;
        return true;
    }

    public static void Resize(IntPtr windowHandle, Size size)
    {
        SetWindowPos(windowHandle, IntPtr.Zero, 0, 0, size.Width, size.Height,
            SwpNoActivate | SwpFrameChanged | SwpShowWindow);
    }

    private static DesktopHost? FindDesktopHost(out string reason)
    {
        IntPtr progman = FindWindow("Progman", null);
        if (progman == IntPtr.Zero)
        {
            reason = "Progman was not found. Windows Explorer may not be running.";
            return null;
        }

        // On Windows 11 24H2+, the background WorkerW is a *child* of Progman.
        // In this layout, lParam=1 is significant; 0xD/0 can leave it absent.
        SendMessageTimeout(progman, SpawnWorkerW, new IntPtr(0xD), new IntPtr(1),
            SmtoAbortIfHung, 1000, out _);

        DesktopHost? modernHost = FindModernHost(progman);
        if (modernHost is not null)
        {
            reason = string.Empty;
            return modernHost;
        }

        IntPtr legacyWorker = FindWorkerNextToDesktopIcons();
        if (legacyWorker != IntPtr.Zero)
        {
            reason = string.Empty;
            return new DesktopHost(legacyWorker, IntPtr.Zero, false);
        }

        // Keep the older message variant for Windows 10 and older Windows 11.
        SendMessageTimeout(progman, SpawnWorkerW, IntPtr.Zero, IntPtr.Zero,
            SmtoAbortIfHung, 1000, out _);

        modernHost = FindModernHost(progman);
        if (modernHost is not null)
        {
            reason = string.Empty;
            return modernHost;
        }

        legacyWorker = FindWorkerNextToDesktopIcons();
        if (legacyWorker != IntPtr.Zero)
        {
            reason = string.Empty;
            return new DesktopHost(legacyWorker, IntPtr.Zero, false);
        }

        // Last resort for the raised desktop: use Progman, but only when its
        // icon view is a direct child that we can safely order above our window.
        IntPtr iconView = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
        if (iconView != IntPtr.Zero)
        {
            reason = string.Empty;
            return new DesktopHost(progman, iconView, true);
        }

        reason = "No compatible WorkerW or Progman icon layer was found.";
        return null;
    }

    private static DesktopHost? FindModernHost(IntPtr progman)
    {
        IntPtr icons = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
        IntPtr worker = FindWindowEx(progman, IntPtr.Zero, "WorkerW", null);

        // Requiring both prevents attaching to an unrelated WorkerW window.
        if (icons != IntPtr.Zero && worker != IntPtr.Zero)
            return new DesktopHost(worker, IntPtr.Zero, true);

        return null;
    }

    private static IntPtr FindWorkerNextToDesktopIcons()
    {
        IntPtr result = IntPtr.Zero;

        EnumWindows((topLevelWindow, _) =>
        {
            IntPtr defView = FindWindowEx(topLevelWindow, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (defView == IntPtr.Zero)
                return true;

            // The WorkerW immediately following the icon host is the wallpaper layer.
            result = FindWindowEx(IntPtr.Zero, topLevelWindow, "WorkerW", null);
            return result == IntPtr.Zero;
        }, IntPtr.Zero);

        return result;
    }

    private delegate bool EnumWindowsCallback(IntPtr window, IntPtr parameter);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string className, string? windowName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr childAfter,
        string className, string? windowName);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr parameter);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(IntPtr window, uint message,
        IntPtr wParam, IntPtr lParam, uint flags, uint timeout, out IntPtr result);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetLayeredWindowAttributes(IntPtr window, uint colorKey,
        byte alpha, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetParent(IntPtr child, IntPtr newParent);

    [DllImport("user32.dll")]
    private static extern IntPtr GetParent(IntPtr child);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter,
        int x, int y, int width, int height, uint flags);
}
