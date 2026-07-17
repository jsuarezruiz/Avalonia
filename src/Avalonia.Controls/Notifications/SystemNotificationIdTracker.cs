using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Avalonia.Controls.Notifications;

/// <summary>
/// Tracks the notification IDs accepted by one manager instance and provides
/// consistent, instance-scoped bulk removal for platform implementations.
/// </summary>
internal sealed class SystemNotificationIdTracker
{
    private readonly object _sync = new();
    private readonly HashSet<string> _ids = new(StringComparer.Ordinal);

    public void MarkShown(string notificationId)
    {
        lock (_sync)
            _ids.Add(notificationId);
    }

    public void MarkRemoved(string notificationId)
    {
        lock (_sync)
            _ids.Remove(notificationId);
    }

    public string[] Snapshot()
    {
        lock (_sync)
            return _ids.ToArray();
    }

    public async ValueTask RemoveAllAsync(
        Func<string, CancellationToken, ValueTask> removeAsync,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(removeAsync);
        cancellationToken.ThrowIfCancellationRequested();

        // Keep removals sequential. Some native notification services serialize
        // their own state and do not benefit from an unbounded request fan-out.
        foreach (var notificationId in Snapshot())
            await removeAsync(notificationId, cancellationToken);
    }
}
