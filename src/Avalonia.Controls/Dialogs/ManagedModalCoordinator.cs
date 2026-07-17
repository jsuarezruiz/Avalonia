using System;
using Avalonia.Automation.Peers;

namespace Avalonia.Controls;

internal sealed class ManagedModalCoordinator : AvaloniaObject
{
    private static readonly AttachedProperty<Control?> s_activeModalProperty =
        AvaloniaProperty.RegisterAttached<ManagedModalCoordinator, TopLevel, Control?>("ActiveModal");

    private ManagedModalCoordinator()
    {
    }

    public static void Acquire(TopLevel owner, Control modal)
    {
        if (owner.GetValue(s_activeModalProperty) is not null)
        {
            throw new InvalidOperationException(
                "The TopLevel already has an active modal surface.");
        }

        owner.SetValue(s_activeModalProperty, modal);
        InvalidateAutomation(owner);
    }

    public static void Release(TopLevel owner, Control modal)
    {
        if (ReferenceEquals(owner.GetValue(s_activeModalProperty), modal))
        {
            owner.ClearValue(s_activeModalProperty);
            InvalidateAutomation(owner);
        }
    }

    internal static Control? GetActiveModal(TopLevel owner) => owner.GetValue(s_activeModalProperty);

    private static void InvalidateAutomation(TopLevel owner) =>
        (ControlAutomationPeer.FromElement(owner) as ControlAutomationPeer)?
        .InvalidateChildrenForModal();
}
