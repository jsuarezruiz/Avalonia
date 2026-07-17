using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls.Notifications;
using Avalonia.Logging;
using UserNotifications;

namespace Avalonia.iOS;

internal sealed class IOSSystemNotificationManager : ISystemNotificationManager
{
    private static UNUserNotificationCenter Center => UNUserNotificationCenter.Current;
#if !TVOS
    private readonly SystemNotificationIdTracker _notificationIds = new();
    private readonly UNUserNotificationCenterDelegate? _foregroundPresentationDelegate;
#endif

    public IOSSystemNotificationManager()
    {
#if !TVOS
        // Apple requires this delegate to be assigned before launch completes. Respect an
        // application-provided delegate; applications that install one own foreground policy.
        if (Center.Delegate is null)
        {
            _foregroundPresentationDelegate = new ForegroundPresentationDelegate();
            Center.Delegate = _foregroundPresentationDelegate;
        }
#endif
    }

    public bool IsSupported => !OperatingSystem.IsTvOS();

    public async ValueTask<SystemNotificationPermissionStatus> GetPermissionStatusAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsSupported)
            return SystemNotificationPermissionStatus.Unsupported;

        var settings = await WaitAndObserveOnCancellationAsync(
            Center.GetNotificationSettingsAsync(),
            cancellationToken,
            "query permission");
        return ToPermissionStatus(settings.AuthorizationStatus);
    }

    public async ValueTask<SystemNotificationPermissionStatus> RequestPermissionAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsSupported)
            return SystemNotificationPermissionStatus.Unsupported;

        var current = await GetPermissionStatusAsync(cancellationToken);
        if (current != SystemNotificationPermissionStatus.NotDetermined)
            return current;

        await WaitAndObserveOnCancellationAsync(
            Center.RequestAuthorizationAsync(UNAuthorizationOptions.Alert),
            cancellationToken,
            "request permission");
        return await GetPermissionStatusAsync(cancellationToken);
    }

    public async ValueTask ShowAsync(
        SystemNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);
        cancellationToken.ThrowIfCancellationRequested();

#if TVOS
        throw new NotSupportedException("System notifications are not supported on tvOS.");
#else
        if (!IsSupported)
            throw new NotSupportedException("System notifications are not supported on tvOS.");
        if (await GetPermissionStatusAsync(cancellationToken) != SystemNotificationPermissionStatus.Granted)
        {
            throw new UnauthorizedAccessException(
                "System-notification permission has not been granted. Call RequestPermissionAsync first.");
        }

        using var content = new UNMutableNotificationContent
        {
            Body = notification.Message
        };
        if (!string.IsNullOrEmpty(notification.Title))
            content.Title = notification.Title;

        using var request = UNNotificationRequest.FromIdentifier(notification.Id, content, null);
        var addTask = Center.AddNotificationRequestAsync(request);
        try
        {
            await addTask.WaitAsync(cancellationToken);
            _notificationIds.MarkShown(notification.Id);
        }
        catch (OperationCanceledException)
        {
            _ = RemoveAfterCanceledSubmissionAsync(addTask, notification.Id);
            throw;
        }
#endif
    }

    public ValueTask RemoveAsync(string notificationId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(notificationId);
        cancellationToken.ThrowIfCancellationRequested();
#if TVOS
        throw new NotSupportedException("System notifications are not supported on tvOS.");
#else
        EnsureSupported();
        Center.RemovePendingNotificationRequests(new[] { notificationId });
        Center.RemoveDeliveredNotifications(new[] { notificationId });
        _notificationIds.MarkRemoved(notificationId);
        return ValueTask.CompletedTask;
#endif
    }

    public ValueTask RemoveAllKnownAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
#if TVOS
        throw new NotSupportedException("System notifications are not supported on tvOS.");
#else
        EnsureSupported();
        return _notificationIds.RemoveAllAsync(RemoveAsync, cancellationToken);
#endif
    }

    private void EnsureSupported()
    {
        if (!IsSupported)
            throw new NotSupportedException("System notifications are not supported on tvOS.");
    }

    private static async Task<TResult> WaitAndObserveOnCancellationAsync<TResult>(
        Task<TResult> operation,
        CancellationToken cancellationToken,
        string operationName)
    {
        try
        {
            return await operation.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _ = ObserveCanceledOperationAsync(operation, operationName);
            throw;
        }
    }

    private static async Task ObserveCanceledOperationAsync(Task operation, string operationName)
    {
        try
        {
            await operation.ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            Logger.TryGet(LogEventLevel.Warning, LogArea.IOSPlatform)?.Log(
                null,
                "A canceled system-notification operation ({Operation}) later failed: {Exception}",
                operationName,
                exception);
        }
    }

#if !TVOS
    private sealed class ForegroundPresentationDelegate : UNUserNotificationCenterDelegate
    {
        public override void WillPresentNotification(
            UNUserNotificationCenter center,
            UNNotification notification,
            Action<UNNotificationPresentationOptions> completionHandler)
        {
            var options = OperatingSystem.IsIOSVersionAtLeast(14) ||
                          OperatingSystem.IsMacCatalystVersionAtLeast(14)
                ? UNNotificationPresentationOptions.Banner | UNNotificationPresentationOptions.List
#pragma warning disable CA1422 // Alert is the supported foreground option on iOS 13.
                : UNNotificationPresentationOptions.Alert;
#pragma warning restore CA1422
            completionHandler(options);
        }
    }

    private static async Task RemoveAfterCanceledSubmissionAsync(Task submission, string notificationId)
    {
        try
        {
            await submission.ConfigureAwait(false);
            Center.RemovePendingNotificationRequests(new[] { notificationId });
            Center.RemoveDeliveredNotifications(new[] { notificationId });
        }
        catch (Exception exception)
        {
            Logger.TryGet(LogEventLevel.Warning, LogArea.IOSPlatform)?.Log(
                null,
                "A canceled system-notification submission failed during cleanup: {Exception}",
                exception);
        }
    }
#endif

    private static SystemNotificationPermissionStatus ToPermissionStatus(UNAuthorizationStatus status) => status switch
    {
        UNAuthorizationStatus.NotDetermined => SystemNotificationPermissionStatus.NotDetermined,
        UNAuthorizationStatus.Denied => SystemNotificationPermissionStatus.Denied,
        UNAuthorizationStatus.Authorized or
            UNAuthorizationStatus.Provisional or
            UNAuthorizationStatus.Ephemeral => SystemNotificationPermissionStatus.Granted,
        _ => SystemNotificationPermissionStatus.Unsupported
    };
}
