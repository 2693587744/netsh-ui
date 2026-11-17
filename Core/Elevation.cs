using System.Runtime.InteropServices;
using System.Security.Principal;

namespace NetshUi.Core;

/// <summary>管理员权限检测与重启提权。大量 netsh set/add/delete 需要管理员权限。</summary>
internal static class Elevation
{
#pragma warning disable CA1416
    public static bool IsElevated
    {
        get
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }
    }
#pragma warning restore CA1416

    /// <summary>以管理员身份重新启动自身；用户取消 UAC 时静默返回 false。</summary>
    public static bool RestartElevated()
    {
        if (Environment.ProcessPath is not { Length: > 0 } executable) return false;

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(executable)
            {
                UseShellExecute = true,
                Verb = "runas",
            });
            return true;
        }
        catch
        {
            return false;
        }
    }
}
