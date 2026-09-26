using System.Diagnostics;
using System.Runtime.InteropServices;

namespace FishLegs;

// macOS only delivers gamepad button/stick data to apps granted "Input Monitoring".
// Without it, controllers still show up as connected but every input reads as idle.
static partial class MacPermissions
{
    public enum Access { Granted, Denied, Unknown, NotApplicable }

    const string IOKit = "/System/Library/Frameworks/IOKit.framework/IOKit";
    const uint ListenEvent = 1;   // kIOHIDRequestTypeListenEvent

    [LibraryImport(IOKit)]
    private static partial uint IOHIDCheckAccess(uint requestType);

    [LibraryImport(IOKit)]
    [return: MarshalAs(UnmanagedType.U1)]
    private static partial bool IOHIDRequestAccess(uint requestType);

    public static Access InputMonitoring()
    {
        if (!OperatingSystem.IsMacOS()) return Access.NotApplicable;
        try
        {
            return IOHIDCheckAccess(ListenEvent) switch { 0 => Access.Granted, 1 => Access.Denied, _ => Access.Unknown };
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException) { return Access.NotApplicable; }
    }

    public static bool Blocked => InputMonitoring() is Access.Denied or Access.Unknown;

    /// <summary>Shows the macOS permission prompt (only the first time; afterwards it's a Settings toggle).</summary>
    public static void Request()
    {
        if (InputMonitoring() != Access.Unknown) return;
        try { IOHIDRequestAccess(ListenEvent); }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException) { }
    }

    public static void OpenSettings()
    {
        if (!OperatingSystem.IsMacOS()) return;
        try { Process.Start("open", "x-apple.systempreferences:com.apple.preference.security?Privacy_ListenEvent"); }
        catch { }
    }

    // The permission belongs to whichever app launched us (e.g. the terminal running `dotnet run`).
    public static string HostApp()
    {
        string term = Environment.GetEnvironmentVariable("TERM_PROGRAM");
        return term switch
        {
            null or "" => "this app",
            "Apple_Terminal" => "Terminal",
            "iTerm.app" => "iTerm",
            "vscode" => "Visual Studio Code",
            "ghostty" => "Ghostty",
            _ => term,
        };
    }

    public const string Why = "macOS is blocking controller input";
    public static string Fix => $"Enable {HostApp()} in System Settings > Privacy & Security > Input Monitoring, then quit and reopen {HostApp()}.";
}
