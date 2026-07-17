using System;

namespace Avalonia.Controls;

/// <summary>Describes why a bottom sheet was dismissed.</summary>
public enum BottomSheetDismissReason
{
    /// <summary>The sheet was hidden by application code.</summary>
    Programmatic,

    /// <summary>The show operation was canceled by its cancellation token.</summary>
    Cancellation,

    /// <summary>The user pressed the backdrop.</summary>
    LightDismiss,

    /// <summary>The user pressed Escape.</summary>
    Escape,

    /// <summary>The platform requested backward navigation.</summary>
    SystemBack,

    /// <summary>The user dragged the sheet below its dismissal threshold.</summary>
    DragDismiss,

    /// <summary>The owning top level closed or the modal host was detached.</summary>
    OwnerClosed
}

/// <summary>Represents the result of a bottom sheet.</summary>
public sealed class BottomSheetResult
{
    internal BottomSheetResult(
        object? value,
        bool hasValue,
        BottomSheetDismissReason dismissReason)
    {
        Value = value;
        HasValue = hasValue;
        DismissReason = dismissReason;
    }

    /// <summary>Gets whether the sheet produced an application result value.</summary>
    public bool HasValue { get; }

    /// <summary>Gets the optional application result value.</summary>
    public object? Value { get; }

    /// <summary>Gets why the sheet was dismissed.</summary>
    public BottomSheetDismissReason DismissReason { get; }
}

/// <summary>Provides data for a bottom-sheet close request.</summary>
public sealed class BottomSheetClosingEventArgs : EventArgs
{
    private readonly ContentDialogClosingEventArgs _inner;

    internal BottomSheetClosingEventArgs(
        object? value,
        bool hasValue,
        BottomSheetDismissReason dismissReason,
        ContentDialogClosingEventArgs inner)
    {
        Value = value;
        HasValue = hasValue;
        DismissReason = dismissReason;
        _inner = inner;
    }

    /// <summary>Gets the proposed result value.</summary>
    public object? Value { get; }

    /// <summary>Gets whether the close operation proposes an application result value.</summary>
    public bool HasValue { get; }

    /// <summary>Gets the proposed dismissal reason.</summary>
    public BottomSheetDismissReason DismissReason { get; }

    /// <summary>Gets or sets whether the close should be canceled.</summary>
    public bool Cancel
    {
        get => _inner.Cancel;
        set => _inner.Cancel = value;
    }

    /// <summary>
    /// Gets a deferral for asynchronous close validation. A deferral must be
    /// requested before the event handler returns.
    /// </summary>
    public DialogDeferral GetDeferral() => _inner.GetDeferral();
}

/// <summary>Provides data after a bottom sheet closes.</summary>
public sealed class BottomSheetClosedEventArgs : EventArgs
{
    internal BottomSheetClosedEventArgs(BottomSheetResult result)
    {
        Result = result;
    }

    /// <summary>Gets the bottom-sheet result.</summary>
    public BottomSheetResult Result { get; }
}

/// <summary>Provides data when the selected detent changes.</summary>
public sealed class BottomSheetDetentChangedEventArgs : EventArgs
{
    internal BottomSheetDetentChangedEventArgs(BottomSheetDetent? oldDetent, BottomSheetDetent newDetent)
    {
        OldDetent = oldDetent;
        NewDetent = newDetent;
    }

    /// <summary>Gets the previously selected detent, or null if none was selected.</summary>
    public BottomSheetDetent? OldDetent { get; }

    /// <summary>Gets the newly selected detent.</summary>
    public BottomSheetDetent NewDetent { get; }
}
