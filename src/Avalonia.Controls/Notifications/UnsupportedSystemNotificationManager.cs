using System;
using System.Threading;
using System.Threading.Tasks;

namespace Avalonia.Controls.Notifications;

internal sealed class UnsupportedSystemNotificationManager : ISystemNotificationManager
{
    public static UnsupportedSystemNotificationManager Instance { get; } = new();

    private UnsupportedSystemNotificationManager()
    {
    }

    public bool IsSupported => false;

    public ValueTask<SystemNotificationPermissionStatus> GetPermissionStatusAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(SystemNotificationPermissionStatus.Unsupported);
    }

    public ValueTask<SystemNotificationPermissionStatus> RequestPermissionAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(SystemNotificationPermissionStatus.Unsupported);
    }

    public ValueTask ShowAsync(
        SystemNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromException(new NotSupportedException(
            "System notifications are not supported by the current application platform."));
    }

    public ValueTask RemoveAsync(
        string notificationId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(notificationId))
            throw new ArgumentException("A system-notification ID cannot be empty or whitespace.", nameof(notificationId));

        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromException(new NotSupportedException(
            "System notifications are not supported by the current application platform."));
    }

    public ValueTask RemoveAllKnownAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromException(new NotSupportedException(
            "System notifications are not supported by the current application platform."));
    }
}
