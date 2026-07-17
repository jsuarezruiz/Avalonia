using System;

namespace Avalonia.Controls.Notifications;

/// <summary>Describes a data-only notification delivered by the operating system.</summary>
public sealed class SystemNotification
{
    /// <summary>Initializes a system notification.</summary>
    /// <param name="id">
    /// A stable application-defined ID. Showing the same ID replaces the prior notification.
    /// </param>
    /// <param name="message">The notification message.</param>
    public SystemNotification(string id, string message)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("A system-notification ID cannot be empty or whitespace.", nameof(id));
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("A system-notification message cannot be empty or whitespace.", nameof(message));

        Id = id;
        Message = message;
    }

    /// <summary>Gets the stable application-defined notification ID.</summary>
    public string Id { get; }

    /// <summary>Gets the notification message.</summary>
    public string Message { get; }

    /// <summary>Gets or sets the optional notification title.</summary>
    public string? Title { get; init; }
}

/// <summary>Describes the application's operating-system notification permission.</summary>
public enum SystemNotificationPermissionStatus
{
    /// <summary>The current platform or application configuration does not support notifications.</summary>
    Unsupported,

    /// <summary>The user has not made a permission decision.</summary>
    NotDetermined,

    /// <summary>The user or operating system denied notification delivery.</summary>
    Denied,

    /// <summary>The application may deliver notifications.</summary>
    Granted
}
