using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Logging;

namespace Avalonia.Controls.Notifications;

/// <summary>
/// Serializes notification mutations so replacement, removal, bulk removal, and
/// cancellation cleanup cannot overtake one another in asynchronous providers.
/// </summary>
internal sealed class CoordinatedSystemNotificationManager : ISystemNotificationManager
{
    private readonly ISystemNotificationManager _inner;
    private readonly SemaphoreSlim _mutationGate = new(1, 1);
    private readonly object _permissionSync = new();
    private Task<SystemNotificationPermissionStatus>? _permissionRequest;

    public CoordinatedSystemNotificationManager(ISystemNotificationManager inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public bool IsSupported => _inner.IsSupported;

    public ValueTask<SystemNotificationPermissionStatus> GetPermissionStatusAsync(
        CancellationToken cancellationToken = default) =>
        _inner.GetPermissionStatusAsync(cancellationToken);

    public ValueTask<SystemNotificationPermissionStatus> RequestPermissionAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Task<SystemNotificationPermissionStatus> request;
        TaskCompletionSource<SystemNotificationPermissionStatus>? completion = null;
        lock (_permissionSync)
        {
            // A completed request is no longer a flight to share. Clearing it here
            // also avoids a retry racing the asynchronous cleanup in the worker.
            if (_permissionRequest?.IsCompleted == true)
                _permissionRequest = null;

            if (_permissionRequest is null)
            {
                completion = new TaskCompletionSource<SystemNotificationPermissionStatus>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                request = _permissionRequest = completion.Task;
            }
            else
            {
                request = _permissionRequest;
            }
        }

        // Start native work outside the synchronization lock. Apart from keeping
        // the critical section small, this handles providers that complete their
        // ValueTask synchronously without re-entering permission-state cleanup.
        if (completion is not null)
            _ = RequestPermissionCoreAsync(completion);

        return cancellationToken.CanBeCanceled
            ? new ValueTask<SystemNotificationPermissionStatus>(request.WaitAsync(cancellationToken))
            : new ValueTask<SystemNotificationPermissionStatus>(request);
    }

    public ValueTask ShowAsync(
        SystemNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        return RunMutationAsync(
            () => _inner.ShowAsync(notification, CancellationToken.None),
            () => _inner.RemoveAsync(notification.Id, CancellationToken.None),
            "show",
            cancellationToken);
    }

    public ValueTask RemoveAsync(
        string notificationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(notificationId);

        return RunMutationAsync(
            () => _inner.RemoveAsync(notificationId, CancellationToken.None),
            null,
            "remove",
            cancellationToken);
    }

    public ValueTask RemoveAllKnownAsync(CancellationToken cancellationToken = default) =>
        RunMutationAsync(
            () => _inner.RemoveAllKnownAsync(CancellationToken.None),
            null,
            "remove all known",
            cancellationToken);

    private async Task RequestPermissionCoreAsync(
        TaskCompletionSource<SystemNotificationPermissionStatus> completion)
    {
        try
        {
            completion.TrySetResult(await _inner.RequestPermissionAsync(CancellationToken.None));
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
            // Every waiting caller may have canceled independently before the
            // native prompt settles. Observe the shared fault here as well so it
            // cannot surface later as an unobserved task exception.
            _ = completion.Task.Exception;
        }
        finally
        {
            lock (_permissionSync)
            {
                if (ReferenceEquals(_permissionRequest, completion.Task))
                    _permissionRequest = null;
            }
        }
    }

    private ValueTask RunMutationAsync(
        Func<ValueTask> mutation,
        Func<ValueTask>? canceledCleanup,
        string operationName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var state = new OperationState();
        var cancellationRegistration = cancellationToken.Register(
            () => state.Cancel(cancellationToken));
        _ = RunMutationCoreAsync(
            mutation,
            canceledCleanup,
            operationName,
            cancellationToken,
            cancellationRegistration,
            state);
        return new ValueTask(state.Task);
    }

    private async Task RunMutationCoreAsync(
        Func<ValueTask> mutation,
        Func<ValueTask>? canceledCleanup,
        string operationName,
        CancellationToken cancellationToken,
        CancellationTokenRegistration cancellationRegistration,
        OperationState state)
    {
        var gateAcquired = false;
        try
        {
            await _mutationGate.WaitAsync(cancellationToken);
            gateAcquired = true;

            // Cancellation while queued must not start a native operation. Once a
            // mutation starts, it runs to completion without caller cancellation so
            // the gate continues to order any native work that cannot be canceled.
            if (state.IsCanceled)
                return;

            await mutation();

            // Completion and cancellation race through one atomic outcome. If
            // cancellation won, a successful Show must be removed before the next
            // queued mutation can start.
            if (!state.TryComplete() && state.IsCanceled && canceledCleanup is not null)
                await canceledCleanup();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            state.Cancel(cancellationToken);
        }
        catch (Exception exception)
        {
            if (!state.TryFail(exception))
            {
                Logger.TryGet(LogEventLevel.Warning, LogArea.Control)?.Log(
                    this,
                    "A canceled system-notification '{Operation}' operation later failed: {Exception}",
                    operationName,
                    exception);
            }
        }
        finally
        {
            if (gateAcquired)
                _mutationGate.Release();
            cancellationRegistration.Dispose();
        }
    }

    private sealed class OperationState
    {
        private const int Pending = 0;
        private const int Canceled = 1;
        private const int Completed = 2;

        private readonly TaskCompletionSource<object?> _completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int _outcome;

        public Task Task => _completion.Task;

        public bool IsCanceled => Volatile.Read(ref _outcome) == Canceled;

        public void Cancel(CancellationToken cancellationToken)
        {
            if (Interlocked.CompareExchange(ref _outcome, Canceled, Pending) == Pending)
                _completion.TrySetCanceled(cancellationToken);
        }

        public bool TryComplete()
        {
            if (Interlocked.CompareExchange(ref _outcome, Completed, Pending) != Pending)
                return false;

            _completion.TrySetResult(null);
            return true;
        }

        public bool TryFail(Exception exception)
        {
            if (Interlocked.CompareExchange(ref _outcome, Completed, Pending) != Pending)
                return false;

            _completion.TrySetException(exception);
            return true;
        }
    }
}
