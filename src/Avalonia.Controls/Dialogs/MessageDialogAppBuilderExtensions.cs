using System;
using Avalonia.Controls;
using Avalonia.Controls.Platform;

namespace Avalonia;

/// <summary>Provides application-level message-dialog configuration.</summary>
public static class MessageDialogAppBuilderExtensions
{
    /// <summary>
    /// Registers an application-provided native message-dialog implementation.
    /// The factory is invoked once for each <see cref="TopLevel"/> that accesses
    /// its <see cref="TopLevel.MessageDialogs"/> service.
    /// </summary>
    /// <remarks>
    /// A returned provider that implements <see cref="IDisposable"/> is disposed
    /// when its owning top level closes.
    /// </remarks>
    public static AppBuilder UseNativeMessageDialogProvider(
        this AppBuilder builder,
        Func<TopLevel, INativeMessageDialogProvider> providerFactory)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(providerFactory);

        return builder.AfterSetup(_ =>
            AvaloniaLocator.CurrentMutable.Bind<INativeMessageDialogProviderFactory>()
                .ToConstant(new DelegateNativeMessageDialogProviderFactory(providerFactory)));
    }

    private sealed class DelegateNativeMessageDialogProviderFactory : INativeMessageDialogProviderFactory
    {
        private readonly Func<TopLevel, INativeMessageDialogProvider> _providerFactory;

        public DelegateNativeMessageDialogProviderFactory(
            Func<TopLevel, INativeMessageDialogProvider> providerFactory)
        {
            _providerFactory = providerFactory;
        }

        public INativeMessageDialogProvider CreateProvider(TopLevel topLevel)
            => _providerFactory(topLevel) ??
               throw new InvalidOperationException("The native message-dialog factory returned null.");
    }
}
