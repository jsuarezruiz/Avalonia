using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls.Platform;
using Avalonia.Logging;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Avalonia.Controls;

/// <summary>
/// Applies the per-owner modal policy to platform and application message-dialog providers.
/// </summary>
internal sealed class CoordinatedMessageDialogProvider : INativeMessageDialogProvider, IDisposable
{
    private readonly TopLevel _owner;
    private readonly INativeMessageDialogProvider _inner;
    private readonly MessageDialogModalHost _modalHost = new();
    private readonly object _lifetimeSync = new();
    private bool _showActive;
    private bool _disposeRequested;
    private bool _innerDisposed;

    public CoordinatedMessageDialogProvider(TopLevel owner, INativeMessageDialogProvider inner)
    {
        _owner = owner;
        _inner = inner;
    }

    public async Task<MessageDialogResult> ShowAsync(
        MessageDialogOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        MessageDialogValidation.Validate(options);
        Dispatcher.UIThread.VerifyAccess();
        if (cancellationToken.IsCancellationRequested)
            return await Task.FromCanceled<MessageDialogResult>(cancellationToken);

        if (_owner.GetPresentationSource() is null)
            throw new InvalidOperationException("The owning TopLevel is not attached to a presentation source.");

        BeginShow();
        var modalAcquired = false;
        try
        {
            ManagedModalCoordinator.Acquire(_owner, _modalHost);
            modalAcquired = true;
            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var ownerClosed = 0;

            void OwnerClosed(object? sender, EventArgs args)
            {
                Interlocked.Exchange(ref ownerClosed, 1);
                try
                {
                    linkedCancellation.Cancel();
                }
                catch (Exception exception)
                {
                    Logger.TryGet(LogEventLevel.Error, LogArea.Control)
                        ?.Log(_owner, "Message-dialog cancellation callback threw while its owner was closing: {Exception}", exception);
                }
            }

            _owner.Closed += OwnerClosed;
            try
            {
                MessageDialogResult result;
                try
                {
                    result = await _inner.ShowAsync(options, linkedCancellation.Token) ??
                        throw new InvalidOperationException("The message-dialog provider returned no result.");
                }
                catch (OperationCanceledException) when (
                    Volatile.Read(ref ownerClosed) != 0 && !cancellationToken.IsCancellationRequested)
                {
                    return new MessageDialogResult(null, MessageDialogDismissReason.OwnerClosed);
                }

                if (Volatile.Read(ref ownerClosed) != 0 &&
                    result.DismissReason != MessageDialogDismissReason.Action)
                    return new MessageDialogResult(null, MessageDialogDismissReason.OwnerClosed);

                MessageDialogValidation.ValidateResult(options, result);
                return result;
            }
            finally
            {
                _owner.Closed -= OwnerClosed;
            }
        }
        finally
        {
            try
            {
                if (modalAcquired && Dispatcher.UIThread.CheckAccess())
                {
                    ManagedModalCoordinator.Release(_owner, _modalHost);
                }
                else if (modalAcquired)
                {
                    await Dispatcher.UIThread.InvokeAsync(
                        () => ManagedModalCoordinator.Release(_owner, _modalHost));
                }
            }
            finally
            {
                EndShow();
            }
        }
    }

    public void Dispose()
    {
        IDisposable? disposable = null;
        lock (_lifetimeSync)
        {
            if (_disposeRequested)
                return;

            _disposeRequested = true;
            if (!_showActive && !_innerDisposed)
            {
                _innerDisposed = true;
                disposable = _inner as IDisposable;
            }
        }

        DisposeInner(disposable);
    }

    private void BeginShow()
    {
        lock (_lifetimeSync)
        {
            ObjectDisposedException.ThrowIf(_disposeRequested, this);
            if (_showActive)
                throw new InvalidOperationException("The native message-dialog provider is already showing a dialog.");

            _showActive = true;
        }
    }

    private void EndShow()
    {
        IDisposable? disposable = null;
        lock (_lifetimeSync)
        {
            _showActive = false;
            if (_disposeRequested && !_innerDisposed)
            {
                _innerDisposed = true;
                disposable = _inner as IDisposable;
            }
        }

        DisposeInner(disposable);
    }

    private void DisposeInner(IDisposable? disposable)
    {
        if (disposable is null)
            return;

        try
        {
            disposable.Dispose();
        }
        catch (Exception exception)
        {
            Logger.TryGet(LogEventLevel.Error, LogArea.Control)
                ?.Log(_owner, "Native message-dialog provider disposal failed: {Exception}", exception);
        }
    }

    private sealed class MessageDialogModalHost : Control
    {
    }
}
