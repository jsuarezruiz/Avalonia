using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Avalonia.Threading;

[assembly: UsesPermission("android.permission.POST_NOTIFICATIONS")]

namespace Avalonia.Android.Platform;

internal sealed class AndroidSystemNotificationManager : ISystemNotificationManager
{
    private const string PermissionPreferences = "avalonia.system-notifications";
    private const string PermissionRequestedKey = "permission-requested";
    private const string PostNotificationsPermission = "android.permission.POST_NOTIFICATIONS";
    private readonly SystemNotificationIdTracker _notificationIds = new();
    private int _channelEnsured;

    private static global::Android.Content.Context Context =>
        global::Android.App.Application.Context ??
        throw new InvalidOperationException("The Android application context is not available.");

    public bool IsSupported => true;

    public ValueTask<SystemNotificationPermissionStatus> GetPermissionStatusAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(GetPermissionStatus());
    }

    public async ValueTask<SystemNotificationPermissionStatus> RequestPermissionAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!OperatingSystem.IsAndroidVersionAtLeast(33))
            return GetPermissionStatus();

        if (Context.CheckSelfPermission(PostNotificationsPermission) == Permission.Granted)
            return GetPermissionStatus();

        Task<bool>? nativeRequest = null;
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var activity = GetCurrentActivity();
            nativeRequest = activity.RequestNotificationPermissionAsync(PostNotificationsPermission);
        });

        // Persist that the prompt was launched before awaiting its result. The
        // activity-result task is process-local and does not survive process death.
        SetPermissionRequested();
        _ = ObservePermissionRequestCompletionAsync(nativeRequest!);
        await nativeRequest!.WaitAsync(cancellationToken);
        return GetPermissionStatus();
    }

    public ValueTask ShowAsync(
        SystemNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);
        cancellationToken.ThrowIfCancellationRequested();

        if (GetPermissionStatus() != SystemNotificationPermissionStatus.Granted)
        {
            throw new UnauthorizedAccessException(
                "System-notification permission has not been granted. Call RequestPermissionAsync first.");
        }

        EnsureChannel();

        var context = Context;
        var options = GetOptions();
        var smallIcon = options.SystemNotificationSmallIconResourceId;
        if (smallIcon == 0)
            smallIcon = context.ApplicationInfo?.Icon ?? 0;
        if (smallIcon == 0)
        {
            throw new InvalidOperationException(
                "Android system notifications require the application manifest to define a valid icon.");
        }

        var nativeId = GetNativeId(notification.Id);
        using var builder = new NotificationCompat.Builder(context, GetChannelId(options));
        using var bigTextStyle = new NotificationCompat.BigTextStyle();
        bigTextStyle.BigText(notification.Message);
        builder.SetSmallIcon(smallIcon);
        builder.SetContentTitle(notification.Title ?? GetApplicationLabel(context));
        builder.SetContentText(notification.Message);
        builder.SetStyle(bigTextStyle);
        builder.SetPriority(NotificationCompat.PriorityDefault);
        builder.SetAutoCancel(true);

        using var launchIntent = context.PackageManager?.GetLaunchIntentForPackage(context.PackageName!);
        if (launchIntent is not null)
        {
            launchIntent.AddFlags(ActivityFlags.ClearTop | ActivityFlags.SingleTop);
            var flags = PendingIntentFlags.UpdateCurrent;
            if (OperatingSystem.IsAndroidVersionAtLeast(23))
                flags |= PendingIntentFlags.Immutable;
            using var pendingIntent = PendingIntent.GetActivity(context, nativeId, launchIntent, flags);
            builder.SetContentIntent(pendingIntent);
        }

        using var nativeNotification = builder.Build() ??
            throw new InvalidOperationException("Android failed to create the system notification.");
        GetNotificationManager().Notify(notification.Id, nativeId, nativeNotification);
        _notificationIds.MarkShown(notification.Id);
        return ValueTask.CompletedTask;
    }

    public ValueTask RemoveAsync(string notificationId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(notificationId);
        cancellationToken.ThrowIfCancellationRequested();
        GetNotificationManager().Cancel(notificationId, GetNativeId(notificationId));
        _notificationIds.MarkRemoved(notificationId);
        return ValueTask.CompletedTask;
    }

    public ValueTask RemoveAllKnownAsync(CancellationToken cancellationToken = default) =>
        _notificationIds.RemoveAllAsync(RemoveAsync, cancellationToken);

    private static SystemNotificationPermissionStatus GetPermissionStatus()
    {
        var context = Context;

        if (OperatingSystem.IsAndroidVersionAtLeast(33) &&
            context.CheckSelfPermission(PostNotificationsPermission) != Permission.Granted)
        {
            return WasPermissionRequested()
                ? SystemNotificationPermissionStatus.Denied
                : SystemNotificationPermissionStatus.NotDetermined;
        }

        if (!GetNotificationManager().AreNotificationsEnabled())
            return SystemNotificationPermissionStatus.Denied;

        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            var manager = (NotificationManager?)context.GetSystemService(
                global::Android.Content.Context.NotificationService);
            using var channel = manager?.GetNotificationChannel(GetChannelId(GetOptions()));
            if (channel?.Importance == NotificationImportance.None)
                return SystemNotificationPermissionStatus.Denied;
        }

        return SystemNotificationPermissionStatus.Granted;
    }

    private static AvaloniaActivity GetCurrentActivity()
    {
        if (AvaloniaLocator.Current.GetService<IActivatableLifetime>() is AndroidActivatableLifetime
        {
                CurrentMainActivity: AvaloniaActivity activity
            } && !activity.IsFinishing && !activity.IsDestroyed)
        {
            return activity;
        }

        throw new InvalidOperationException(
            "Android notification permission can only be requested while an AvaloniaActivity is available.");
    }

    private static async Task ObservePermissionRequestCompletionAsync(Task<bool> nativeRequest)
    {
        try
        {
            await nativeRequest.ConfigureAwait(false);
        }
        catch
        {
            // Delivery errors are observed by the caller. Cancellation only stops that
            // caller from waiting; the activity-result registration remains valid.
        }
    }

    private void EnsureChannel()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26) || Volatile.Read(ref _channelEnsured) != 0)
            return;

        var context = Context;
        var options = GetOptions();
        var channelId = GetChannelId(options);
        var manager = (NotificationManager?)context.GetSystemService(global::Android.Content.Context.NotificationService);
        if (manager is null)
            throw new InvalidOperationException("The Android notification service is not available.");

        using var channel = new NotificationChannel(
            channelId,
            string.IsNullOrWhiteSpace(options.SystemNotificationChannelName)
                ? GetApplicationLabel(context)
                : options.SystemNotificationChannelName,
            NotificationImportance.Default);
        if (!string.IsNullOrWhiteSpace(options.SystemNotificationChannelDescription))
            channel.Description = options.SystemNotificationChannelDescription;
        // Re-registering an existing ID is safe and lets the application refresh its
        // localized channel name and description once per process.
        manager.CreateNotificationChannel(channel);
        Volatile.Write(ref _channelEnsured, 1);
    }

    private static AndroidPlatformOptions GetOptions() =>
        AndroidPlatform.Options ??
        AvaloniaLocator.Current.GetService<AndroidPlatformOptions>() ??
        new AndroidPlatformOptions();

    private static string GetChannelId(AndroidPlatformOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.SystemNotificationChannelId))
        {
            throw new InvalidOperationException(
                $"{nameof(AndroidPlatformOptions)}.{nameof(AndroidPlatformOptions.SystemNotificationChannelId)} " +
                "cannot be empty or whitespace.");
        }

        return options.SystemNotificationChannelId;
    }

    private static NotificationManagerCompat GetNotificationManager() =>
        NotificationManagerCompat.From(Context) ??
        throw new InvalidOperationException("The Android notification manager is not available.");

    private static string GetApplicationLabel(global::Android.Content.Context context)
    {
        var packageManager = context.PackageManager;
        return packageManager is null
            ? "Notification"
            : context.ApplicationInfo?.LoadLabel(packageManager)?.ToString() ?? "Notification";
    }

    private static int GetNativeId(string id)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(id));
        var result = BitConverter.ToInt32(hash, 0) & int.MaxValue;
        return result == 0 ? 1 : result;
    }

    private static bool WasPermissionRequested() => Context
        .GetSharedPreferences(PermissionPreferences, FileCreationMode.Private)!
        .GetBoolean(PermissionRequestedKey, false);

    private static void SetPermissionRequested()
    {
        var persisted = Context
            .GetSharedPreferences(PermissionPreferences, FileCreationMode.Private)!
            .Edit()!
            .PutBoolean(PermissionRequestedKey, true)!
            .Commit();
        if (!persisted)
        {
            throw new InvalidOperationException(
                "Android could not persist the notification-permission request state.");
        }
    }
}
