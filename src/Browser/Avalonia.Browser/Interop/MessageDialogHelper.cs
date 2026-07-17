using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;

namespace Avalonia.Browser.Interop;

internal static partial class MessageDialogHelper
{
    [JSImport("MessageDialog.isSupported", AvaloniaModule.MainModuleName)]
    public static partial bool IsSupported();

    [JSImport("MessageDialog.show", AvaloniaModule.MainModuleName)]
    public static partial Task<int> Show(
        JSObject container,
        string? title,
        string message,
        string? detail,
        string[] actions,
        string[] roles);

    [JSImport("MessageDialog.dismiss", AvaloniaModule.MainModuleName)]
    public static partial void Dismiss(JSObject container);
}
