using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Foundation;
using UIKit;

namespace Avalonia.iOS;

internal sealed class IOSMessageDialogProvider : INativeMessageDialogProvider
{
    private readonly Func<UIViewController?> _controller;

    public IOSMessageDialogProvider(Func<UIViewController?> controller)
    {
        _controller = controller;
    }

    public async Task<MessageDialogResult> ShowAsync(
        MessageDialogOptions options,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!NSThread.IsMain)
            throw new InvalidOperationException("Native iOS message dialogs must be shown on the main thread.");

        var controller = _controller() ??
            throw new InvalidOperationException("The owning iOS view controller is not available.");
        if (controller.ViewIfLoaded?.Window is null)
            throw new InvalidOperationException("The owning iOS view controller is not visible.");
        if (controller.PresentedViewController is not null)
            throw new InvalidOperationException("The owning iOS view controller is already presenting a surface.");

        var message = string.IsNullOrEmpty(options.Detail)
            ? options.Message
            : $"{options.Message}\n\n{options.Detail}";
        using var alert = UIAlertController.Create(
            options.Title ?? string.Empty,
            message,
            UIAlertControllerStyle.Alert);
        var completion = new TaskCompletionSource<MessageDialogResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var outcomeSelected = 0;

        void CompleteAfterDismissal(MessageDialogResult result)
        {
            if (Interlocked.CompareExchange(ref outcomeSelected, 1, 0) != 0)
                return;

            if (alert.PresentingViewController is null)
                completion.TrySetResult(result);
            else
                alert.DismissViewController(true, () => completion.TrySetResult(result));
        }

        void CancelAfterDismissal()
        {
            if (Interlocked.CompareExchange(ref outcomeSelected, 1, 0) != 0)
                return;

            if (alert.PresentingViewController is null)
                completion.TrySetCanceled(cancellationToken);
            else
                alert.DismissViewController(
                    true,
                    () => completion.TrySetCanceled(cancellationToken));
        }

        UIAlertAction? preferredAction = null;
        foreach (var action in options.Actions)
        {
            var nativeAction = UIAlertAction.Create(
                action.Text,
                action.IsCancel
                    ? UIAlertActionStyle.Cancel
                    : action.IsDestructive
                        ? UIAlertActionStyle.Destructive
                        : UIAlertActionStyle.Default,
                _ => CompleteAfterDismissal(new MessageDialogResult(
                    action.Id,
                    MessageDialogDismissReason.Action)));
            alert.AddAction(nativeAction);

            if (action.IsDefault && !action.IsCancel)
                preferredAction = nativeAction;
        }

        if (preferredAction is not null)
            alert.PreferredAction = preferredAction;

        // Wait until UIKit has completed presentation before allowing cancellation
        // to dismiss the alert. Otherwise cancellation in the small interval after
        // PresentViewController can observe no presenting controller, complete the
        // task, and leave an alert that appears after its managed wrapper is gone.
        var presentationCompletion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        controller.PresentViewController(
            alert,
            true,
            () => presentationCompletion.TrySetResult());
        await presentationCompletion.Task;

        using var cancellationRegistration = cancellationToken.Register(() =>
            controller.InvokeOnMainThread(CancelAfterDismissal));

        return await completion.Task;
    }
}
