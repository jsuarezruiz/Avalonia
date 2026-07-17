using System.Threading;
using System.Threading.Tasks;

namespace Avalonia.Controls.Notifications;

/// <summary>
/// Delivers notifications through the operating system's notification service.
/// </summary>
/// <remarks>
/// This service is application-scoped and never falls back to Avalonia-rendered UI.
/// Permission must be requested explicitly before calling <see cref="ShowAsync"/> on
/// platforms that require it. The service returned by
/// <see cref="Application.SystemNotifications"/> serializes delivery and removal so
/// same-ID replacement and cancellation cleanup cannot overtake one another.
/// On macOS, the application must run from an application bundle with a valid
/// bundle identifier before the native notification service is available.
/// </remarks>
public interface ISystemNotificationManager
{
    /// <summary>
    /// Gets whether a system-notification provider is available for this application
    /// and platform. The provider can still report <see cref="SystemNotificationPermissionStatus.Unsupported"/>
    /// if a dynamic operating-system service becomes unavailable.
    /// </summary>
    bool IsSupported { get; }

    /// <summary>Gets the current operating-system notification permission.</summary>
    /// <param name="cancellationToken">A token that cancels the permission query.</param>
    /// <returns>The current permission status.</returns>
    ValueTask<SystemNotificationPermissionStatus> GetPermissionStatusAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Requests operating-system notification permission.</summary>
    /// <param name="cancellationToken">A token that cancels waiting for the permission request.</param>
    /// <returns>The permission status after the request completes.</returns>
    ValueTask<SystemNotificationPermissionStatus> RequestPermissionAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Shows or replaces a system notification. A notification with the same
    /// <see cref="SystemNotification.Id"/> replaces the previous one.
    /// </summary>
    /// <param name="notification">The notification data to deliver.</param>
    /// <param name="cancellationToken">
    /// A token that cancels waiting for delivery. If cancellation wins after native
    /// submission, an accepted notification is removed before the next mutation begins.
    /// </param>
    ValueTask ShowAsync(
        SystemNotification notification,
        CancellationToken cancellationToken = default);

    /// <summary>Removes the system notification with the specified ID.</summary>
    /// <param name="notificationId">The stable application-defined notification ID.</param>
    /// <param name="cancellationToken">A token that cancels waiting for removal.</param>
    ValueTask RemoveAsync(
        string notificationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes every system notification submitted by this manager during the current
    /// application process.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels waiting for bulk removal.</param>
    ValueTask RemoveAllKnownAsync(CancellationToken cancellationToken = default);
}
