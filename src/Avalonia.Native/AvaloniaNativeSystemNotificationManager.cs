using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls.Notifications;
using Avalonia.Logging;
using Avalonia.Native.Interop;

namespace Avalonia.Native;

internal sealed class AvaloniaNativeSystemNotificationManager : ISystemNotificationManager, IDisposable
{
    private readonly IAvnSystemNotificationProvider _native;
    private readonly SystemNotificationIdTracker _notificationIds = new();
    private volatile bool _disposed;

    public AvaloniaNativeSystemNotificationManager(IAvnSystemNotificationProvider native)
    {
        _native = native;
    }

    public bool IsSupported => !_disposed && _native.IsSupported() != 0;

    public async ValueTask<SystemNotificationPermissionStatus> GetPermissionStatusAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        var result = await InvokeAsync(_native.GetPermissionStatus, cancellationToken);
        ThrowIfNativeError(result.ErrorCode, "query notification permission");
        return ToPermissionStatus(result.Result);
    }

    public async ValueTask<SystemNotificationPermissionStatus> RequestPermissionAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        var result = await InvokeAsync(_native.RequestPermission, cancellationToken);
        ThrowIfNativeError(result.ErrorCode, "request notification permission");
        return ToPermissionStatus(result.Result);
    }

    public async ValueTask ShowAsync(
        SystemNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);
        EnsureNotDisposed();
        cancellationToken.ThrowIfCancellationRequested();

        var permission = await GetPermissionStatusAsync(cancellationToken);
        if (permission == SystemNotificationPermissionStatus.Unsupported)
            ThrowNotSupported();
        if (permission != SystemNotificationPermissionStatus.Granted)
        {
            throw new UnauthorizedAccessException(
                "System-notification permission has not been granted. Call RequestPermissionAsync first.");
        }

        CallbackEvents? callback = new();
        try
        {
            _native.ShowSystemNotification(
                notification.Id,
                notification.Title ?? string.Empty,
                notification.Message,
                callback);
            var result = await callback.Task.WaitAsync(cancellationToken);
            if (result.ErrorCode == 1)
            {
                throw new UnauthorizedAccessException(
                    "macOS does not currently allow this application to post system notifications.");
            }
            ThrowIfNativeError(result.ErrorCode, "show a system notification");
            if (result.Result == 0)
                throw new InvalidOperationException("macOS did not accept the system notification.");
            _notificationIds.MarkShown(notification.Id);
        }
        catch (OperationCanceledException)
        {
            var pendingCallback = callback;
            callback = null;
            _ = RemoveAfterCanceledSubmissionAsync(pendingCallback, notification.Id);
            throw;
        }
        finally
        {
            callback?.Dispose();
        }
    }

    public ValueTask RemoveAsync(string notificationId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(notificationId);
        EnsureNotDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        EnsureSupported();
        _native.RemoveSystemNotification(notificationId);
        _notificationIds.MarkRemoved(notificationId);
        return ValueTask.CompletedTask;
    }

    public ValueTask RemoveAllKnownAsync(CancellationToken cancellationToken = default)
    {
        EnsureNotDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        EnsureSupported();
        return _notificationIds.RemoveAllAsync(RemoveAsync, cancellationToken);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _native.Dispose();
    }

    private async Task<CallbackResult> InvokeAsync(
        Action<IAvnSystemNotificationEvents> operation,
        CancellationToken cancellationToken)
    {
        CallbackEvents? callback = new();
        try
        {
            operation(callback);
            return await callback.Task.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            var pendingCallback = callback;
            callback = null;
            _ = DisposeCallbackWhenCompletedAsync(pendingCallback);
            throw;
        }
        finally
        {
            callback?.Dispose();
        }
    }

    private void EnsureNotDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    private void EnsureSupported()
    {
        if (!IsSupported)
            ThrowNotSupported();
    }

    private static void ThrowNotSupported() =>
        throw new NotSupportedException(
            "System notifications require a supported macOS application bundle.");

    private async Task RemoveAfterCanceledSubmissionAsync(
        CallbackEvents callback,
        string notificationId)
    {
        try
        {
            await callback.Task.ConfigureAwait(false);
            if (!_disposed)
                _native.RemoveSystemNotification(notificationId);
        }
        catch (Exception exception)
        {
            Logger.TryGet(LogEventLevel.Warning, LogArea.macOSPlatform)?.Log(
                this,
                "A canceled system-notification submission failed during cleanup: {Exception}",
                exception);
        }
        finally
        {
            callback.Dispose();
        }
    }

    private static async Task DisposeCallbackWhenCompletedAsync(CallbackEvents callback)
    {
        try
        {
            await callback.Task.ConfigureAwait(false);
        }
        finally
        {
            callback.Dispose();
        }
    }

    private static void ThrowIfNativeError(int errorCode, string operation)
    {
        if (errorCode != 0)
        {
            throw new InvalidOperationException(
                $"macOS failed to {operation} (native error {errorCode}).");
        }
    }

    private static SystemNotificationPermissionStatus ToPermissionStatus(int status) => status switch
    {
        (int)AvnSystemNotificationPermission.SystemNotificationPermissionNotDetermined =>
            SystemNotificationPermissionStatus.NotDetermined,
        (int)AvnSystemNotificationPermission.SystemNotificationPermissionDenied =>
            SystemNotificationPermissionStatus.Denied,
        (int)AvnSystemNotificationPermission.SystemNotificationPermissionGranted =>
            SystemNotificationPermissionStatus.Granted,
        _ => SystemNotificationPermissionStatus.Unsupported
    };

    private readonly record struct CallbackResult(int Result, int ErrorCode);

    private sealed class CallbackEvents : NativeCallbackBase, IAvnSystemNotificationEvents
    {
        private readonly TaskCompletionSource<CallbackResult> _completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<CallbackResult> Task => _completion.Task;

        public void Completed(int result, int errorCode) =>
            _completion.TrySetResult(new CallbackResult(result, errorCode));
    }
}
