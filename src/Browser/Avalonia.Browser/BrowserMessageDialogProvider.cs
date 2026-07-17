using System;
using System.Linq;
using System.Runtime.InteropServices.JavaScript;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Browser.Interop;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Threading;

namespace Avalonia.Browser;

internal sealed class BrowserMessageDialogProvider : INativeMessageDialogProvider
{
    private readonly JSObject _container;

    public BrowserMessageDialogProvider(JSObject container)
    {
        _container = container;
    }

    public async Task<MessageDialogResult> ShowAsync(
        MessageDialogOptions options,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var actions = options.Actions.Select(action => action.Text).ToArray();
        var roles = options.Actions.Select(action => string.Join(' ', new[]
        {
            action.IsDefault ? "default" : null,
            action.IsCancel ? "cancel" : null,
            action.IsDestructive ? "destructive" : null
        }.Where(role => role is not null))).ToArray();

        cancellationToken.ThrowIfCancellationRequested();
        var showTask = MessageDialogHelper.Show(
            _container,
            options.Title,
            options.Message,
            options.Detail,
            actions,
            roles);

        using var cancellationRegistration = cancellationToken.Register(() =>
        {
            if (Dispatcher.UIThread.CheckAccess())
                MessageDialogHelper.Dismiss(_container);
            else
                Dispatcher.UIThread.Post(() => MessageDialogHelper.Dismiss(_container));
        });

        var selectedIndex = await showTask;
        cancellationToken.ThrowIfCancellationRequested();

        return selectedIndex >= 0 && selectedIndex < options.Actions.Count
            ? new MessageDialogResult(
                options.Actions[selectedIndex].Id,
                MessageDialogDismissReason.Action)
            : new MessageDialogResult(null, MessageDialogDismissReason.Dismissed);
    }
}
