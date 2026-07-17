using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Threading;

namespace Avalonia.Win32;

internal sealed class Win32MessageDialogProvider : INativeMessageDialogProvider
{
    private const int FirstActionId = 1000;
    private const int CancelButtonId = 2;
    private const uint TaskDialogCreated = 0;
    private const uint TaskDialogDestroyed = 5;
    private const uint TaskDialogClickButton = 0x0400 + 102;
    private readonly IntPtr _owner;

    public Win32MessageDialogProvider(IntPtr owner)
    {
        _owner = owner;
    }

    public Task<MessageDialogResult> ShowAsync(
        MessageDialogOptions options,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled<MessageDialogResult>(cancellationToken);

        var completion = new TaskCompletionSource<MessageDialogResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        Dispatcher.UIThread.Post(() => ShowCore(options, cancellationToken, completion));
        return completion.Task;
    }

    private void ShowCore(
        MessageDialogOptions options,
        CancellationToken cancellationToken,
        TaskCompletionSource<MessageDialogResult> completion)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            completion.TrySetCanceled(cancellationToken);
            return;
        }

        var allocations = new List<IntPtr>();
        var state = new DialogState();
        TaskDialogCallback callback = (dialog, notification, _, _, _) =>
        {
            if (notification == TaskDialogCreated)
            {
                Volatile.Write(ref state.DialogHandle, dialog);
                if (Volatile.Read(ref state.CancellationRequested) != 0)
                    PostMessage(dialog, TaskDialogClickButton, new IntPtr(CancelButtonId), IntPtr.Zero);
            }
            else if (notification == TaskDialogDestroyed)
            {
                Volatile.Write(ref state.DialogHandle, IntPtr.Zero);
            }

            return 0;
        };

        try
        {
            var nativeButtons = new TaskDialogButton[options.Actions.Count];
            for (var index = 0; index < options.Actions.Count; index++)
            {
                nativeButtons[index] = new TaskDialogButton
                {
                    ButtonId = FirstActionId + index,
                    ButtonText = AllocateString(options.Actions[index].Text, allocations)
                };
            }

            var buttonsSize = Marshal.SizeOf<TaskDialogButton>() * nativeButtons.Length;
            var buttons = Marshal.AllocHGlobal(buttonsSize);
            allocations.Add(buttons);
            for (var index = 0; index < nativeButtons.Length; index++)
            {
                Marshal.StructureToPtr(
                    nativeButtons[index],
                    IntPtr.Add(buttons, index * Marshal.SizeOf<TaskDialogButton>()),
                    false);
            }

            var defaultActionId = 0;
            for (var index = 0; index < options.Actions.Count; index++)
            {
                if (options.Actions[index].IsDefault)
                {
                    defaultActionId = FirstActionId + index;
                    break;
                }
            }

            var configuration = new TaskDialogConfiguration
            {
                Size = (uint)Marshal.SizeOf<TaskDialogConfiguration>(),
                Parent = _owner,
                Flags = TaskDialogFlags.AllowDialogCancellation |
                    TaskDialogFlags.PositionRelativeToWindow |
                    TaskDialogFlags.SizeToContent,
                WindowTitle = AllocateString(options.Title, allocations),
                MainIcon = GetIcon(options.Icon),
                MainInstruction = AllocateString(options.Message, allocations),
                Content = AllocateString(options.Detail, allocations),
                ButtonCount = (uint)nativeButtons.Length,
                Buttons = buttons,
                DefaultButton = defaultActionId,
                Callback = callback
            };

            using var cancellationRegistration = cancellationToken.Register(() =>
            {
                Volatile.Write(ref state.CancellationRequested, 1);
                var dialog = Volatile.Read(ref state.DialogHandle);
                if (dialog != IntPtr.Zero)
                    PostMessage(dialog, TaskDialogClickButton, new IntPtr(CancelButtonId), IntPtr.Zero);
            });

            var result = TaskDialogIndirect(
                in configuration,
                out var selectedButton,
                out _,
                out _);
            Volatile.Write(ref state.DialogHandle, IntPtr.Zero);
            cancellationToken.ThrowIfCancellationRequested();

            if (result < 0)
                Marshal.ThrowExceptionForHR(result);

            var actionIndex = selectedButton - FirstActionId;
            if (actionIndex >= 0 && actionIndex < options.Actions.Count)
            {
                completion.TrySetResult(new MessageDialogResult(
                    options.Actions[actionIndex].Id,
                    MessageDialogDismissReason.Action));
            }
            else
            {
                MessageDialogAction? cancelAction = null;
                foreach (var action in options.Actions)
                {
                    if (action.IsCancel)
                    {
                        cancelAction = action;
                        break;
                    }
                }

                completion.TrySetResult(cancelAction is null
                    ? new MessageDialogResult(null, MessageDialogDismissReason.Dismissed)
                    : new MessageDialogResult(cancelAction.Id, MessageDialogDismissReason.Action));
            }
        }
        catch (OperationCanceledException)
        {
            completion.TrySetCanceled(cancellationToken);
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
        }
        finally
        {
            Volatile.Write(ref state.DialogHandle, IntPtr.Zero);
            GC.KeepAlive(callback);
            foreach (var allocation in allocations)
                Marshal.FreeHGlobal(allocation);
        }
    }

    private static IntPtr AllocateString(string? value, ICollection<IntPtr> allocations)
    {
        if (string.IsNullOrEmpty(value))
            return IntPtr.Zero;

        var result = Marshal.StringToHGlobalUni(value);
        allocations.Add(result);
        return result;
    }

    private static IntPtr GetIcon(MessageDialogIcon icon)
        => icon switch
        {
            MessageDialogIcon.Warning => new IntPtr(-1),
            MessageDialogIcon.Error => new IntPtr(-2),
            MessageDialogIcon.Information or MessageDialogIcon.Success => new IntPtr(-3),
            _ => IntPtr.Zero
        };

    [Flags]
    private enum TaskDialogFlags : uint
    {
        AllowDialogCancellation = 0x0008,
        PositionRelativeToWindow = 0x1000,
        SizeToContent = 0x01000000
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TaskDialogButton
    {
        public int ButtonId;
        public IntPtr ButtonText;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TaskDialogConfiguration
    {
        public uint Size;
        public IntPtr Parent;
        public IntPtr Instance;
        public TaskDialogFlags Flags;
        public uint CommonButtons;
        public IntPtr WindowTitle;
        public IntPtr MainIcon;
        public IntPtr MainInstruction;
        public IntPtr Content;
        public uint ButtonCount;
        public IntPtr Buttons;
        public int DefaultButton;
        public uint RadioButtonCount;
        public IntPtr RadioButtons;
        public int DefaultRadioButton;
        public IntPtr VerificationText;
        public IntPtr ExpandedInformation;
        public IntPtr ExpandedControlText;
        public IntPtr CollapsedControlText;
        public IntPtr FooterIcon;
        public IntPtr Footer;
        public TaskDialogCallback Callback;
        public IntPtr CallbackData;
        public uint Width;
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int TaskDialogCallback(
        IntPtr dialog,
        uint notification,
        IntPtr wParam,
        IntPtr lParam,
        IntPtr callbackData);

    private sealed class DialogState
    {
        public IntPtr DialogHandle;
        public int CancellationRequested;
    }

    [DllImport("comctl32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int TaskDialogIndirect(
        in TaskDialogConfiguration configuration,
        out int button,
        out int radioButton,
        out int verificationFlagChecked);

    [DllImport("user32.dll", EntryPoint = "PostMessageW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(
        IntPtr window,
        uint message,
        IntPtr wParam,
        IntPtr lParam);
}
