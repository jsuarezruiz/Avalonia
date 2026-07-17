using System;

namespace Avalonia.Controls;

/// <summary>
/// Identifies one of the standard actions in a <see cref="ContentDialog"/>.
/// </summary>
public enum ContentDialogButton
{
    /// <summary>No action.</summary>
    None,
    /// <summary>The primary action.</summary>
    Primary,
    /// <summary>The secondary action.</summary>
    Secondary,
    /// <summary>The close or cancel action.</summary>
    Close
}

/// <summary>
/// Identifies the action that completed a <see cref="ContentDialog"/>.
/// </summary>
public enum ContentDialogResult
{
    /// <summary>No action completed the dialog.</summary>
    None,
    /// <summary>The primary action completed the dialog.</summary>
    Primary,
    /// <summary>The secondary action completed the dialog.</summary>
    Secondary,
    /// <summary>The close action completed the dialog.</summary>
    Close
}

/// <summary>
/// Describes why a content dialog was dismissed.
/// </summary>
public enum ContentDialogDismissReason
{
    /// <summary>A dialog action was invoked.</summary>
    Action,
    /// <summary>The dialog was hidden programmatically.</summary>
    Programmatic,
    /// <summary>The show operation was canceled by its cancellation token.</summary>
    Cancellation,
    /// <summary>The user pressed Escape.</summary>
    Escape,
    /// <summary>The platform requested backward navigation.</summary>
    SystemBack,
    /// <summary>The user pressed the backdrop.</summary>
    LightDismiss,
    /// <summary>The owning top level closed or the dialog was detached.</summary>
    OwnerClosed
}

/// <summary>
/// Provides data for a content-dialog action event.
/// </summary>
public sealed class ContentDialogButtonClickEventArgs : EventArgs
{
    private readonly DialogDeferralManager _deferrals;

    internal ContentDialogButtonClickEventArgs(
        ContentDialogButton button,
        DialogDeferralManager deferrals)
    {
        Button = button;
        _deferrals = deferrals;
    }

    /// <summary>Gets the action that was invoked.</summary>
    public ContentDialogButton Button { get; }

    /// <summary>Gets or sets whether the action should keep the dialog open.</summary>
    public bool Cancel { get; set; }

    /// <summary>
    /// Gets a deferral for asynchronous action validation. A deferral must be
    /// requested before the event handler returns.
    /// </summary>
    public DialogDeferral GetDeferral() => _deferrals.GetDeferral();
}

/// <summary>
/// Provides data for the <see cref="ContentDialog.Closing"/> event.
/// </summary>
public sealed class ContentDialogClosingEventArgs : EventArgs
{
    private readonly DialogDeferralManager _deferrals;

    internal ContentDialogClosingEventArgs(
        ContentDialogResult result,
        ContentDialogDismissReason dismissReason,
        DialogDeferralManager deferrals)
    {
        Result = result;
        DismissReason = dismissReason;
        _deferrals = deferrals;
    }

    /// <summary>Gets the result proposed for the close operation.</summary>
    public ContentDialogResult Result { get; }

    /// <summary>Gets the reason for the close operation.</summary>
    public ContentDialogDismissReason DismissReason { get; }

    /// <summary>Gets or sets whether the close operation should be canceled.</summary>
    public bool Cancel { get; set; }

    /// <summary>
    /// Gets a deferral for asynchronous close validation. A deferral must be
    /// requested before the event handler returns.
    /// </summary>
    public DialogDeferral GetDeferral() => _deferrals.GetDeferral();
}

/// <summary>
/// Provides data for the <see cref="ContentDialog.Closed"/> event.
/// </summary>
public sealed class ContentDialogClosedEventArgs : EventArgs
{
    internal ContentDialogClosedEventArgs(
        ContentDialogResult result,
        ContentDialogDismissReason dismissReason)
    {
        Result = result;
        DismissReason = dismissReason;
    }

    /// <summary>Gets the result returned by the dialog.</summary>
    public ContentDialogResult Result { get; }

    /// <summary>Gets the reason the dialog was dismissed.</summary>
    public ContentDialogDismissReason DismissReason { get; }
}
