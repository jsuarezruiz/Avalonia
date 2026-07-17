using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Native.Interop;
using Avalonia.Threading;

namespace Avalonia.Native;

internal sealed class AvaloniaNativeMessageDialogProvider : INativeMessageDialogProvider, IDisposable
{
    private readonly IAvnMessageDialogProvider _native;
    private volatile bool _disposed;

    public AvaloniaNativeMessageDialogProvider(IAvnMessageDialogProvider native)
    {
        _native = native;
    }

    public async Task<MessageDialogResult> ShowAsync(
        MessageDialogOptions options,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        var defaultActionIndex = -1;
        var cancelActionIndex = -1;
        var destructiveActionMask = 0;
        for (var index = 0; index < options.Actions.Count; index++)
        {
            var action = options.Actions[index];
            if (action.IsDefault)
                defaultActionIndex = index;
            if (action.IsCancel)
                cancelActionIndex = index;
            if (action.IsDestructive)
                destructiveActionMask |= 1 << index;
        }

        using var actions = new AvnStringArray(System.Linq.Enumerable.Select(
            options.Actions,
            action => action.Text));
        using var events = new MessageDialogEvents();

        _native.ShowMessageDialog(
            options.Title ?? string.Empty,
            options.Message,
            options.Detail ?? string.Empty,
            (AvnMessageDialogIcon)options.Icon,
            actions,
            defaultActionIndex,
            cancelActionIndex,
            destructiveActionMask,
            events);

        using var cancellationRegistration = cancellationToken.Register(() =>
        {
            if (_disposed)
                return;

            if (Dispatcher.UIThread.CheckAccess())
                _native.CancelMessageDialog();
            else
                Dispatcher.UIThread.Post(() =>
                {
                    if (!_disposed)
                        _native.CancelMessageDialog();
                });
        });

        var selectedIndex = await events.Task;
        cancellationToken.ThrowIfCancellationRequested();

        if (selectedIndex >= 0 && selectedIndex < options.Actions.Count)
        {
            return new MessageDialogResult(
                options.Actions[selectedIndex].Id,
                MessageDialogDismissReason.Action);
        }

        return cancelActionIndex >= 0
            ? new MessageDialogResult(
                options.Actions[cancelActionIndex].Id,
                MessageDialogDismissReason.Action)
            : new MessageDialogResult(null, MessageDialogDismissReason.Dismissed);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        try
        {
            _native.CancelMessageDialog();
        }
        finally
        {
            _native.Dispose();
        }
    }

    private sealed class MessageDialogEvents : NativeCallbackBase, IAvnMessageDialogEvents
    {
        private readonly TaskCompletionSource<int> _completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<int> Task => _completion.Task;

        public void Completed(int actionIndex) => _completion.TrySetResult(actionIndex);
    }
}
