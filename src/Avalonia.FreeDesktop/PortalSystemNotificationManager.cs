using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls.Notifications;
using Avalonia.Logging;
using Tmds.DBus.Protocol;
using Tmds.DBus.SourceGenerator;

namespace Avalonia.FreeDesktop;

internal sealed class PortalSystemNotificationManager : ISystemNotificationManager
{
    private readonly SystemNotificationIdTracker _notificationIds = new();
    private readonly OrgFreedesktopPortalNotificationProxy? _portal;
    private int _portalState;

    public PortalSystemNotificationManager()
    {
        if (DBusHelper.DefaultConnection is { } connection)
        {
            _portal = new OrgFreedesktopPortalNotificationProxy(
                connection,
                "org.freedesktop.portal.Desktop",
                "/org/freedesktop/portal/desktop");
        }
    }

    public bool IsSupported => _portal is not null;

    public async ValueTask<SystemNotificationPermissionStatus> GetPermissionStatusAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_portal is null)
            return SystemNotificationPermissionStatus.Unsupported;
        if (Volatile.Read(ref _portalState) > 0)
            return SystemNotificationPermissionStatus.Granted;

        try
        {
            var operation = _portal.GetVersionPropertyAsync();
            try
            {
                await operation.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _ = ObserveCanceledProbeAsync(operation);
                throw;
            }

            Volatile.Write(ref _portalState, 1);
            return SystemNotificationPermissionStatus.Granted;
        }
        catch (DBusErrorReplyException)
        {
            // Portal availability can change when the desktop service restarts. Do
            // not cache a transient D-Bus failure as a process-lifetime decision.
            return SystemNotificationPermissionStatus.Unsupported;
        }
    }

    public ValueTask<SystemNotificationPermissionStatus> RequestPermissionAsync(
        CancellationToken cancellationToken = default) =>
        GetPermissionStatusAsync(cancellationToken);

    public async ValueTask ShowAsync(
        SystemNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);
        cancellationToken.ThrowIfCancellationRequested();

        var portal = await GetPortalAsync(cancellationToken);
        var data = new Dictionary<string, VariantValue>
        {
            ["body"] = VariantValue.String(notification.Message),
            ["priority"] = VariantValue.String("normal")
        };
        if (!string.IsNullOrEmpty(notification.Title))
            data["title"] = VariantValue.String(notification.Title);

        var operation = portal.AddNotificationAsync(notification.Id, data);
        try
        {
            await operation.WaitAsync(cancellationToken);
            _notificationIds.MarkShown(notification.Id);
        }
        catch (OperationCanceledException)
        {
            _ = RemoveAfterCanceledSubmissionAsync(operation, portal, notification.Id);
            throw;
        }
    }

    public async ValueTask RemoveAsync(
        string notificationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(notificationId);
        cancellationToken.ThrowIfCancellationRequested();

        var portal = await GetPortalAsync(cancellationToken);
        var operation = portal.RemoveNotificationAsync(notificationId);
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

    private async ValueTask<OrgFreedesktopPortalNotificationProxy> GetPortalAsync(
        CancellationToken cancellationToken)
    {
        if (await GetPermissionStatusAsync(cancellationToken) == SystemNotificationPermissionStatus.Granted &&
            _portal is not null)
        {
            return _portal;
        }

        throw new NotSupportedException(
            "The org.freedesktop.portal.Notification service is not available.");
    }

    private async Task RemoveAfterCanceledSubmissionAsync(
        Task submission,
        OrgFreedesktopPortalNotificationProxy portal,
        string notificationId)
    {
        try
        {
            await submission.ConfigureAwait(false);
            await portal.RemoveNotificationAsync(notificationId).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            LogCleanupFailure(notificationId, exception);
        }
    }

    private async Task ObserveCanceledProbeAsync(Task operation)
    {
        try
        {
            await operation.ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            Logger.TryGet(LogEventLevel.Warning, LogArea.FreeDesktopPlatform)?.Log(
                this,
                "A canceled system-notification portal probe later failed: {Exception}",
                exception);
        }
    }

    private async Task ObserveCanceledRemovalAsync(Task removal, string notificationId)
    {
        try
        {
            await removal.ConfigureAwait(false);
            _notificationIds.MarkRemoved(notificationId);
        }
        catch (Exception exception)
        {
            LogCleanupFailure(notificationId, exception);
        }
    }

    private void LogCleanupFailure(string notificationId, Exception exception)
    {
        Logger.TryGet(LogEventLevel.Warning, LogArea.FreeDesktopPlatform)?.Log(
            this,
            "System-notification cleanup for '{NotificationId}' failed: {Exception}",
            notificationId,
            exception);
    }
}
