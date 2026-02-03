using System.Security.Principal;

namespace StickyTerm.Helpers;

/// <summary>
/// Helper for checking administrator privileges.
/// </summary>
public static class ElevationHelper
{
    /// <summary>
    /// Checks if the current process is running with administrator privileges.
    /// </summary>
    public static bool IsRunningAsAdmin()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}
