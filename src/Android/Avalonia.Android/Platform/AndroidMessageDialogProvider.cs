using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.OS;
using Avalonia.Controls;
using Avalonia.Controls.Platform;

namespace Avalonia.Android.Platform;

internal sealed class AndroidMessageDialogProvider : INativeMessageDialogProvider
{
    private readonly Activity _activity;

    public AndroidMessageDialogProvider(Activity activity)
    {
        _activity = activity;
    }

    public async Task<MessageDialogResult> ShowAsync(
        MessageDialogOptions options,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (Looper.MyLooper() != Looper.MainLooper)
            throw new InvalidOperationException("Native Android message dialogs must be shown on the main thread.");
        if (_activity.IsFinishing || _activity.IsDestroyed)
            throw new InvalidOperationException("The owning Android activity is not available.");

        var dismissal = new DialogDismissal();
        using var builder = new AlertDialog.Builder(_activity);

        if (!string.IsNullOrEmpty(options.Title))
            builder.SetTitle(options.Title);

        builder.SetMessage(string.IsNullOrEmpty(options.Detail)
            ? options.Message
            : $"{options.Message}\n\n{options.Detail}");

        var buttons = AssignButtons(options.Actions);
        foreach (var pair in buttons)
        {
            var action = pair.Value;
            EventHandler<DialogClickEventArgs> handler = (_, _) =>
                dismissal.TrySetResult(new MessageDialogResult(
                    action.Id,
                    MessageDialogDismissReason.Action));

            switch (pair.Key)
            {
                case DialogButtonType.Positive:
                    builder.SetPositiveButton(action.Text, handler);
                    break;
                case DialogButtonType.Negative:
                    builder.SetNegativeButton(action.Text, handler);
                    break;
                case DialogButtonType.Neutral:
                    builder.SetNeutralButton(action.Text, handler);
                    break;
            }
        }

        var cancelAction = options.Actions.FirstOrDefault(action => action.IsCancel);
        using var cancelListener = new CancelListener(() =>
        {
            dismissal.TrySetResult(cancelAction is null
                ? new MessageDialogResult(null, MessageDialogDismissReason.Dismissed)
                : new MessageDialogResult(cancelAction.Id, MessageDialogDismissReason.Action));
        });
        using var dismissListener = new DismissListener(dismissal.Complete);
        builder.SetOnCancelListener(cancelListener);
        builder.SetOnDismissListener(dismissListener);

        using var dialog = builder.Create() ??
            throw new InvalidOperationException("Android did not create a native message dialog.");
        dialog.Show();

        var defaultAction = options.Actions.FirstOrDefault(action => action.IsDefault);
        if (defaultAction is not null)
        {
            var defaultButton = buttons.First(pair => ReferenceEquals(pair.Value, defaultAction)).Key;
            dialog.GetButton((int)defaultButton)?.RequestFocus();
        }

        using var cancellationRegistration = cancellationToken.Register(() =>
            _activity.RunOnUiThread(() =>
            {
                if (dismissal.TrySetCanceled(cancellationToken))
                {
                    if (dialog.IsShowing)
                        dialog.Dismiss();
                    else
                        dismissal.Complete();
                }
            }));

        return await dismissal.Task;
    }

    private static Dictionary<DialogButtonType, MessageDialogAction> AssignButtons(
        IReadOnlyList<MessageDialogAction> actions)
    {
        var result = new Dictionary<DialogButtonType, MessageDialogAction>();
        var cancel = actions.FirstOrDefault(action => action.IsCancel);
        var defaultAction = actions.FirstOrDefault(action => action.IsDefault);

        if (cancel is not null)
            result.Add(DialogButtonType.Negative, cancel);
        if (defaultAction is not null && !ReferenceEquals(defaultAction, cancel))
            result.Add(DialogButtonType.Positive, defaultAction);

        foreach (var action in actions)
        {
            if (result.ContainsValue(action))
                continue;

            var slot = !result.ContainsKey(DialogButtonType.Positive)
                ? DialogButtonType.Positive
                : !result.ContainsKey(DialogButtonType.Negative)
                    ? DialogButtonType.Negative
                    : DialogButtonType.Neutral;
            result.Add(slot, action);
        }

        return result;
    }

    private sealed class CancelListener : Java.Lang.Object, IDialogInterfaceOnCancelListener
    {
        private readonly Action _onCancel;

        public CancelListener(Action onCancel)
        {
            _onCancel = onCancel;
        }

        public void OnCancel(IDialogInterface? dialog) => _onCancel();
    }

    private sealed class DismissListener : Java.Lang.Object, IDialogInterfaceOnDismissListener
    {
        private readonly Action _onDismiss;

        public DismissListener(Action onDismiss)
        {
            _onDismiss = onDismiss;
        }

        public void OnDismiss(IDialogInterface? dialog) => _onDismiss();
    }

    private sealed class DialogDismissal
    {
        private readonly object _sync = new();
        private readonly TaskCompletionSource<MessageDialogResult> _completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private MessageDialogResult? _result;
        private CancellationToken _cancellationToken;
        private bool _hasOutcome;
        private bool _isCanceled;
        private bool _isCompleted;

        public Task<MessageDialogResult> Task => _completion.Task;

        public bool TrySetResult(MessageDialogResult result)
        {
            lock (_sync)
            {
                if (_hasOutcome)
                    return false;

                _hasOutcome = true;
                _result = result;
                return true;
            }
        }

        public bool TrySetCanceled(CancellationToken cancellationToken)
        {
            lock (_sync)
            {
                if (_hasOutcome)
                    return false;

                _hasOutcome = true;
                _isCanceled = true;
                _cancellationToken = cancellationToken;
                return true;
            }
        }

        public void Complete()
        {
            MessageDialogResult? result;
            CancellationToken cancellationToken;
            bool isCanceled;

            lock (_sync)
            {
                if (_isCompleted)
                    return;

                _isCompleted = true;
                result = _result;
                cancellationToken = _cancellationToken;
                isCanceled = _isCanceled;
            }

            if (isCanceled)
            {
                _completion.TrySetCanceled(cancellationToken);
            }
            else
            {
                _completion.TrySetResult(result ??
                    new MessageDialogResult(null, MessageDialogDismissReason.Dismissed));
            }
        }
    }
}
