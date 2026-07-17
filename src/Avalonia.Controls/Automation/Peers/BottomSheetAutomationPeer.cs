using System;
using Avalonia.Automation;
using Avalonia.Automation.Provider;
using Avalonia.Controls;

namespace Avalonia.Automation.Peers;

/// <summary>Exposes a <see cref="BottomSheet"/> to automation clients.</summary>
public class BottomSheetAutomationPeer : ContentControlAutomationPeer, IExpandCollapseProvider
{
    /// <summary>Initializes a bottom-sheet automation peer.</summary>
    public BottomSheetAutomationPeer(BottomSheet owner)
        : base(owner)
    {
        owner.PropertyChanged += OwnerPropertyChanged;
    }

    /// <summary>Gets the control represented by this peer.</summary>
    public new BottomSheet Owner => (BottomSheet)base.Owner;

    protected override AutomationControlType GetAutomationControlTypeCore()
        => AutomationControlType.Pane;

    protected override string GetClassNameCore() => nameof(BottomSheet);

    protected override string? GetNameCore()
    {
        var result = AutomationProperties.GetName(Owner);
        if (string.IsNullOrWhiteSpace(result))
            result = Owner.Header?.ToString();
        if (string.IsNullOrWhiteSpace(result))
            result = base.GetNameCore();
        return result;
    }

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;

    /// <inheritdoc/>
    public ExpandCollapseState ExpandCollapseState => GetState(Owner.SelectedDetent);

    /// <inheritdoc/>
    public bool ShowsMenu => false;

    /// <inheritdoc/>
    public void Expand()
    {
        EnsureEnabled();
        var detents = Owner.GetDetentsByHeight();
        if (detents.Count > 0)
            Owner.SelectedDetent = detents[^1];
    }

    /// <inheritdoc/>
    public void Collapse()
    {
        EnsureEnabled();
        var detents = Owner.GetDetentsByHeight();
        if (detents.Count > 0)
            Owner.SelectedDetent = detents[0];
    }

    private void OwnerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == BottomSheet.SelectedDetentProperty)
        {
            RaisePropertyChangedEvent(
                ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
                GetState(e.OldValue as BottomSheetDetent),
                GetState(e.NewValue as BottomSheetDetent));
        }
    }

    private ExpandCollapseState GetState(BottomSheetDetent? detent)
    {
        var detents = Owner.GetDetentsByHeight();
        if (detents.Count <= 1)
            return ExpandCollapseState.LeafNode;

        var index = detent is null ? -1 : detents.IndexOf(detent);
        if (index <= 0)
            return ExpandCollapseState.Collapsed;
        return index == detents.Count - 1
            ? ExpandCollapseState.Expanded
            : ExpandCollapseState.PartiallyExpanded;
    }
}
