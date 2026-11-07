using Microsoft.Win32;
using System.Security;

namespace Nordwall;

/// <summary>
/// Per-user opt-in startup. It does not require administrator rights and
/// never modifies the machine-wide startup settings.
/// </summary>
internal static class StartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string EntryName = "Nordwall";

    public static bool IsEnabled()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(EntryName) is string command && !string.IsNullOrWhiteSpace(command);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException or IOException)
        {
            return false;
        }
    }

    public static void SetEnabled(bool enabled)
    {
        if (enabled)
        {
            string executablePath = Environment.ProcessPath ?? Application.ExecutablePath;
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath)
                ?? throw new InvalidOperationException("Unable to open the current user's startup key.");
            key.SetValue(EntryName, $"\"{executablePath}\"", RegistryValueKind.String);
        }
        else
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            key?.DeleteValue(EntryName, throwOnMissingValue: false);
        }
    }
}
