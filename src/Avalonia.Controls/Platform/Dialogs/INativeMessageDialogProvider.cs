using System.Threading;
using System.Threading.Tasks;

namespace Avalonia.Controls.Platform;

/// <summary>
/// Displays a native text-and-action message dialog for a specific top level.
/// Implementations must observe cancellation requests, dismiss the presented
/// surface, and complete the returned task promptly. If the request's text or
/// actions cannot be represented, the provider must fail before presenting a
/// surface. Semantic icon and destructive-style hints may be ignored where the
/// native platform does not provide an idiomatic representation.
/// </summary>
public interface INativeMessageDialogProvider
{
    /// <summary>Shows a native message dialog.</summary>
    /// <param name="options">The text, semantic icon, and actions to present.</param>
    /// <param name="cancellationToken">A token that requests dismissal and cancels the returned task.</param>
    /// <returns>The selected action or a dismissal result.</returns>
    Task<MessageDialogResult> ShowAsync(
        MessageDialogOptions options,
        CancellationToken cancellationToken = default);
}

internal interface INativeMessageDialogProviderFactory
{
    INativeMessageDialogProvider CreateProvider(TopLevel topLevel);
}
