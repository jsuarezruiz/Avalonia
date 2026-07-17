using System.Linq;
using System.Threading.Tasks;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Threading;
using Avalonia.UnitTests;
using Xunit;

namespace Avalonia.Controls.UnitTests.Automation;

public class DialogAndSheetAutomationPeerTests : ScopedTestBase
{
    [Fact]
    public void ContentDialog_Uses_Window_Role_And_Title_Name()
    {
        var dialog = new ContentDialog { Title = "Save changes" };
        var peer = Assert.IsType<ContentDialogAutomationPeer>(
            ControlAutomationPeer.CreatePeerForElement(dialog));

        Assert.Equal(AutomationControlType.Window, peer.GetAutomationControlType());
        Assert.Equal("Save changes", peer.GetName());
    }

    [Fact]
    public void ContentDialog_Explicit_Name_Takes_Precedence()
    {
        var dialog = new ContentDialog { Title = "Visible title" };
        AutomationProperties.SetName(dialog, "Accessible title");
        var peer = ControlAutomationPeer.CreatePeerForElement(dialog);

        Assert.Equal("Accessible title", peer.GetName());
    }

    [Fact]
    public void BottomSheet_Uses_Pane_Role_Header_Name_And_Detent_Pattern()
    {
        var sheet = new BottomSheet { Header = "Filters" };
        sheet.Detents.Insert(1, BottomSheetDetent.Medium);
        sheet.SelectedDetent = sheet.Detents[1];
        var peer = Assert.IsType<BottomSheetAutomationPeer>(
            ControlAutomationPeer.CreatePeerForElement(sheet));
        var provider = Assert.IsAssignableFrom<IExpandCollapseProvider>(peer);

        Assert.Equal(AutomationControlType.Pane, peer.GetAutomationControlType());
        Assert.Equal("Filters", peer.GetName());
        Assert.Equal(ExpandCollapseState.PartiallyExpanded, provider.ExpandCollapseState);

        provider.Expand();
        Assert.Same(sheet.Detents[^1], sheet.SelectedDetent);
        Assert.Equal(ExpandCollapseState.Expanded, provider.ExpandCollapseState);

        provider.Collapse();
        Assert.Same(sheet.Detents[0], sheet.SelectedDetent);
        Assert.Equal(ExpandCollapseState.Collapsed, provider.ExpandCollapseState);
    }

    [Fact]
    public void Single_Detent_BottomSheet_Is_A_Leaf_Node()
    {
        var sheet = new BottomSheet();
        sheet.Detents.Remove(BottomSheetDetent.Expanded);
        sheet.SelectedDetent = BottomSheetDetent.Content;
        var peer = Assert.IsType<BottomSheetAutomationPeer>(
            ControlAutomationPeer.CreatePeerForElement(sheet));
        var provider = Assert.IsAssignableFrom<IExpandCollapseProvider>(peer);

        Assert.Equal(ExpandCollapseState.LeafNode, provider.ExpandCollapseState);
    }

    [Fact]
    public async Task Window_Automation_Exposes_Only_The_Active_Modal()
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var background = new Button { Content = "Background" };
            var window = new Window
            {
                Width = 640,
                Height = 480,
                Content = background
            };
            window.Show();
            window.LayoutManager.ExecuteLayoutPass();

            var windowPeer = ControlAutomationPeer.CreatePeerForElement(window);
            _ = windowPeer.GetChildren();
            var dialog = new ContentDialog
            {
                Title = "Active dialog",
                Content = new TextBlock { Text = "Modal content" },
                Transition = null
            };

            var show = dialog.ShowAsync(window, TestContext.Current.CancellationToken);

            var child = Assert.Single(windowPeer.GetChildren());
            Assert.Same(dialog, Assert.IsType<ContentDialogAutomationPeer>(child).Owner);
            Assert.DoesNotContain(
                windowPeer.GetChildren().OfType<ControlAutomationPeer>(),
                peer => ReferenceEquals(peer.Owner, background));

            dialog.Hide();
            Dispatcher.UIThread.RunJobs(null, TestContext.Current.CancellationToken);
            await show;
        }
    }
}
