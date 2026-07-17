using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;

namespace Avalonia.Browser.Interop;

internal static partial class SystemNotificationHelper
{
    [JSImport("SystemNotificationBridge.isSupported", AvaloniaModule.MainModuleName)]
    public static partial bool IsSupported();

    [JSImport("SystemNotificationBridge.getPermissionStatus", AvaloniaModule.MainModuleName)]
    public static partial int GetPermissionStatus();

    [JSImport("SystemNotificationBridge.requestPermission", AvaloniaModule.MainModuleName)]
    public static partial Task<int> RequestPermission();

    [JSImport("SystemNotificationBridge.show", AvaloniaModule.MainModuleName)]
    public static partial Task Show(string id, string? title, string message);

    [JSImport("SystemNotificationBridge.remove", AvaloniaModule.MainModuleName)]
    public static partial Task Remove(string id);

}
