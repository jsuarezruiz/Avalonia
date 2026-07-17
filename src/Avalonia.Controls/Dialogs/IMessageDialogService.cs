using System.Threading;
using System.Threading.Tasks;

namespace Avalonia.Controls;

/// <summary>
/// Presents owner-scoped message dialogs using the owning top level's native
/// platform surface.
/// </summary>
public interface IMessageDialogService
{
    /// <summary>
    /// Gets whether a native message-dialog provider is available for this owner.
    /// Presentation can still fail if a dynamic native facility becomes unavailable.
    /// </summary>
    bool IsSupported { get; }

    /// <summary>Shows a message dialog.</summary>
    /// <param name="options">The message-dialog request.</param>
    /// <param name="cancellationToken">A token that dismisses the dialog and cancels the returned task.</param>
    /// <returns>The selected action or dismissal reason.</returns>
    Task<MessageDialogResult> ShowAsync(
        MessageDialogOptions options,
        CancellationToken cancellationToken = default);
}
