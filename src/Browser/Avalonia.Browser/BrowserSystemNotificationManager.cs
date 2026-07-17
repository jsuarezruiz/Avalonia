using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Browser.Interop;
using Avalonia.Controls.Notifications;
using Avalonia.Logging;

namespace Avalonia.Browser;

internal sealed class BrowserSystemNotificationManager : ISystemNotificationManager
{
    private readonly SystemNotificationIdTracker _notificationIds = new();

    public bool IsSupported => SystemNotificationHelper.IsSupported();

    public ValueTask<SystemNotificationPermissionStatus> GetPermissionStatusAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(ToPermissionStatus(SystemNotificationHelper.GetPermissionStatus()));
    }

    public async ValueTask<SystemNotificationPermissionStatus> RequestPermissionAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!IsSupported)
            return SystemNotificationPermissionStatus.Unsupported;

        var operation = SystemNotificationHelper.RequestPermission();
        try
        {
            var status = await operation.WaitAsync(cancellationToken);
            return ToPermissionStatus(status);
        }
        catch (OperationCanceledException)
        {
            _ = ObserveCanceledOperationAsync(operation, "permission");
            throw;
        }
    }

    public async ValueTask ShowAsync(
        SystemNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureGranted();
        var title = notification.Title ?? Application.Current?.Name ?? "Notification";
        var operation = SystemNotificationHelper.Show(notification.Id, title, notification.Message);
        try
        {
            await operation.WaitAsync(cancellationToken);
            _notificationIds.MarkShown(notification.Id);
        }
        catch (OperationCanceledException)
        {
            _ = RemoveAfterCanceledSubmissionAsync(operation, notification.Id);
            throw;
        }
    }

    public async ValueTask RemoveAsync(string notificationId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(notificationId);
        cancellationToken.ThrowIfCancellationRequested();

        if (!IsSupported)
            throw new NotSupportedException("This browser does not support system notifications.");

        var operation = SystemNotificationHelper.Remove(notificationId);
        try
        {
            await operation.WaitAsync(cancellationToken);
            _notificationIds.MarkRemoved(notificationId);
        }
        catch (OperationCanceledException)
        {
            _ = ObserveCanceledRemovalAsync(operation, notificationId);
            throw;
        }
    }

    public ValueTask RemoveAllKnownAsync(CancellationToken cancellationToken = default) =>
        _notificationIds.RemoveAllAsync(RemoveAsync, cancellationToken);

    private void EnsureGranted()
    {
        if (!IsSupported)
            throw new NotSupportedException("This browser does not support system notifications.");

        if (ToPermissionStatus(SystemNotificationHelper.GetPermissionStatus()) !=
            SystemNotificationPermissionStatus.Granted)
        {
            throw new UnauthorizedAccessException(
                "System-notification permission has not been granted. Call RequestPermissionAsync first.");
        }
    }

    private static SystemNotificationPermissionStatus ToPermissionStatus(int status) => status switch
    {
        1 => SystemNotificationPermissionStatus.NotDetermined,
        2 => SystemNotificationPermissionStatus.Denied,
        3 => SystemNotificationPermissionStatus.Granted,
        _ => SystemNotificationPermissionStatus.Unsupported
    };

    private static async Task RemoveAfterCanceledSubmissionAsync(Task submission, string notificationId)
    {
        try
        {
            await submission.ConfigureAwait(false);
            await SystemNotificationHelper.Remove(notificationId).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            LogCanceledOperationFailure(notificationId, exception);
        }
    }

    private static async Task ObserveCanceledOperationAsync(Task operation, string? notificationId)
    {
        try
        {
            await operation.ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            LogCanceledOperationFailure(notificationId, exception);
        }
    }

    private async Task ObserveCanceledRemovalAsync(Task operation, string notificationId)
    {
        try
        {
            await operation.ConfigureAwait(false);
            _notificationIds.MarkRemoved(notificationId);
        }
        catch (Exception exception)
        {
            LogCanceledOperationFailure(notificationId, exception);
        }
    }

    private static void LogCanceledOperationFailure(string? notificationId, Exception exception)
    {
        Logger.TryGet(LogEventLevel.Warning, LogArea.BrowserPlatform)?.Log(
            null,
            "A canceled browser system-notification operation for '{NotificationId}' failed: {Exception}",
            notificationId ?? "all",
            exception);
    }
}
