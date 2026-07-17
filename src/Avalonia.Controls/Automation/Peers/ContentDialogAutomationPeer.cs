using Avalonia.Automation;
using Avalonia.Controls;

namespace Avalonia.Automation.Peers;

/// <summary>
/// Exposes a <see cref="ContentDialog"/> to automation clients.
/// </summary>
public class ContentDialogAutomationPeer : ContentControlAutomationPeer
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContentDialogAutomationPeer"/> class.
    /// </summary>
    public ContentDialogAutomationPeer(ContentDialog owner)
        : base(owner)
    {
    }

    /// <summary>Gets the control represented by this peer.</summary>
    public new ContentDialog Owner => (ContentDialog)base.Owner;

    protected override AutomationControlType GetAutomationControlTypeCore()
        => AutomationControlType.Window;

    protected override string GetClassNameCore() => nameof(ContentDialog);

    protected override string? GetNameCore()
    {
        var result = AutomationProperties.GetName(Owner);

        if (string.IsNullOrWhiteSpace(result))
            result = Owner.Title?.ToString();

        if (string.IsNullOrWhiteSpace(result))
            result = base.GetNameCore();

        return result;
    }

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;
}
