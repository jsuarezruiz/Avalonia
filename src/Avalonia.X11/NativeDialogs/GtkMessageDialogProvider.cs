using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Platform.Interop;
using static Avalonia.X11.Interop.Glib;
using static Avalonia.X11.NativeDialogs.Gtk;

namespace Avalonia.X11.NativeDialogs;

internal sealed class GtkMessageDialogProvider : INativeMessageDialogProvider
{
    private const int FirstActionResponse = 1000;
    private readonly X11Window _window;

    public GtkMessageDialogProvider(X11Window window)
    {
        _window = window;
    }

    public async Task<MessageDialogResult> ShowAsync(
        MessageDialogOptions options,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!await StartGtk())
            throw new NotSupportedException("GTK 3 is not available for native message dialogs.");

        return await await RunOnGlibThread(() => ShowCore(options, cancellationToken));
    }

    private Task<MessageDialogResult> ShowCore(
        MessageDialogOptions options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IntPtr dialog;
        using (var format = new Utf8Buffer("%s"))
        using (var message = new Utf8Buffer(options.Message))
        {
            dialog = gtk_message_dialog_new(
                IntPtr.Zero,
                GtkDialogFlags.Modal | GtkDialogFlags.DestroyWithParent,
                ToGtkMessageType(options.Icon),
                GtkButtonsType.None,
                format,
                message);
        }

        if (dialog == IntPtr.Zero)
            throw new InvalidOperationException("GTK did not create a native message dialog.");

        UpdateParent(dialog);

        if (!string.IsNullOrEmpty(options.Title))
        {
            using var title = new Utf8Buffer(options.Title);
            gtk_window_set_title(dialog, title);
        }

        if (!string.IsNullOrEmpty(options.Detail))
        {
            using var format = new Utf8Buffer("%s");
            using var detail = new Utf8Buffer(options.Detail);
            gtk_message_dialog_format_secondary_text(dialog, format, detail);
        }

        for (var index = 0; index < options.Actions.Count; index++)
        {
            using var text = new Utf8Buffer(options.Actions[index].Text);
            gtk_dialog_add_button_with_id(dialog, text, FirstActionResponse + index);
        }

        var defaultIndex = IndexOf(options.Actions, action => action.IsDefault);
        if (defaultIndex >= 0)
            gtk_dialog_set_default_response(dialog, FirstActionResponse + defaultIndex);

        var completion = new TaskCompletionSource<MessageDialogResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var signalSubscriptions = new List<IDisposable>();
        var cancellationRegistration = default(CancellationTokenRegistration);
        var cleanedUp = false;

        void Cleanup()
        {
            if (cleanedUp)
                return;

            cleanedUp = true;
            cancellationRegistration.Dispose();
            foreach (var subscription in signalSubscriptions)
                subscription.Dispose();
            signalSubscriptions.Clear();
            gtk_widget_destroy(dialog);
        }

        MessageDialogResult DismissedResult()
        {
            var cancel = options.Actions.FirstOrDefault(action => action.IsCancel);
            return cancel is null
                ? new MessageDialogResult(null, MessageDialogDismissReason.Dismissed)
                : new MessageDialogResult(cancel.Id, MessageDialogDismissReason.Action);
        }

        signalSubscriptions.Add(ConnectSignal<signal_dialog_response>(
            dialog,
            "response",
            (_, response, _) =>
            {
                MessageDialogResult result;
                var actionIndex = (int)response - FirstActionResponse;
                if (actionIndex >= 0 && actionIndex < options.Actions.Count)
                {
                    result = new MessageDialogResult(
                        options.Actions[actionIndex].Id,
                        MessageDialogDismissReason.Action);
                }
                else
                {
                    result = DismissedResult();
                }

                if (completion.TrySetResult(result))
                    Cleanup();
            }));

        signalSubscriptions.Add(ConnectSignal<signal_generic>(
            dialog,
            "close",
            (_, _) =>
            {
                if (completion.TrySetResult(DismissedResult()))
                    Cleanup();
            }));

        cancellationRegistration = cancellationToken.Register(() =>
            g_idle_add_once(() =>
            {
                if (completion.TrySetCanceled(cancellationToken))
                    Cleanup();
            }));

        gtk_window_present(dialog);
        return completion.Task;
    }

    private void UpdateParent(IntPtr dialog)
    {
        if (_window.Handle is not { } handle)
            return;

        gtk_widget_realize(dialog);
        var dialogWindow = gtk_widget_get_window(dialog);
        var parent = GetForeignWindow(handle.Handle);
        if (parent == IntPtr.Zero)
            return;

        try
        {
            if (dialogWindow != IntPtr.Zero)
                gdk_window_set_transient_for(dialogWindow, parent);
        }
        finally
        {
            g_object_unref(parent);
        }
    }

    private static int IndexOf(
        IReadOnlyList<MessageDialogAction> actions,
        Func<MessageDialogAction, bool> predicate)
    {
        for (var index = 0; index < actions.Count; index++)
        {
            if (predicate(actions[index]))
                return index;
        }

        return -1;
    }

    private static GtkMessageType ToGtkMessageType(MessageDialogIcon icon)
        => icon switch
        {
            MessageDialogIcon.Warning => GtkMessageType.Warning,
            MessageDialogIcon.Error => GtkMessageType.Error,
            MessageDialogIcon.Question => GtkMessageType.Question,
            MessageDialogIcon.None => GtkMessageType.Other,
            _ => GtkMessageType.Info
        };
}
