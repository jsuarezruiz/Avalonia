using System;
using System.Threading.Tasks;
using AndroidX.Activity.Result;

namespace Avalonia.Android.Platform;

internal static class AndroidNotificationPermissionRequest
{
    private static readonly object s_sync = new();
    private static TaskCompletionSource<bool>? s_pending;

    public static Task<bool> Launch(ActivityResultLauncher launcher, string permission)
    {
        ArgumentNullException.ThrowIfNull(launcher);
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);

        TaskCompletionSource<bool> completion;
        lock (s_sync)
        {
            if (s_pending is not null)
                return s_pending.Task;

            completion = s_pending = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        }

        try
        {
            using var input = new Java.Lang.String(permission);
            launcher.Launch(input);
            return completion.Task;
        }
        catch
        {
            lock (s_sync)
            {
                if (ReferenceEquals(s_pending, completion))
                    s_pending = null;
            }

            throw;
        }
    }

    public static void Complete(bool granted)
    {
        TaskCompletionSource<bool>? completion;
        lock (s_sync)
        {
            completion = s_pending;
            s_pending = null;
        }

        completion?.TrySetResult(granted);
    }
}
