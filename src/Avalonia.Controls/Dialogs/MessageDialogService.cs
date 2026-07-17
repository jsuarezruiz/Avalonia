using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls.Platform;

namespace Avalonia.Controls;

/// <summary>
/// Exposes one native message-dialog provider for an owning top level while
/// applying Avalonia's shared modal coordination and result validation.
/// </summary>
internal sealed class MessageDialogService : IMessageDialogService, IDisposable
{
    private readonly CoordinatedMessageDialogProvider? _provider;
    private readonly bool _disposeProvider;

    public MessageDialogService(
        TopLevel owner,
        INativeMessageDialogProvider? provider,
        bool disposeProvider)
    {
        _provider = provider is null
            ? null
            : new CoordinatedMessageDialogProvider(owner, provider);
        _disposeProvider = disposeProvider;
    }

    public bool IsSupported => _provider is not null;

    public void Dispose()
    {
        if (_disposeProvider)
            _provider?.Dispose();
    }

    public Task<MessageDialogResult> ShowAsync(
        MessageDialogOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        MessageDialogValidation.Validate(options);

        return (_provider ?? throw new NotSupportedException(
            "Native message dialogs are not supported for this TopLevel."))
            .ShowAsync(options, cancellationToken);
    }
}
