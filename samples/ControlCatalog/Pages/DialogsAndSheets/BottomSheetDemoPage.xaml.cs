using System;
using System.Threading.Tasks;
using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ControlCatalog.Pages
{
    /// <summary>Demonstrates bottom-sheet detents, gestures, and results.</summary>
    public partial class BottomSheetDemoPage : UserControl
    {
        public BottomSheetDemoPage()
        {
            InitializeComponent();
        }

        private async void OnShowSheet(object? sender, RoutedEventArgs e)
        {
            if (TopLevel.GetTopLevel(this) is not { } owner)
                return;

            var sheet = new BottomSheet
            {
                Header = "Choose a detent",
                IsLightDismissEnabled = LightDismissCheck.IsChecked == true,
                IsDragEnabled = DragCheck.IsChecked == true,
                IsDragDismissEnabled = DragDismissCheck.IsChecked == true
            };
            ApplyTransition(sheet);
            var compactDetent = BottomSheetDetent.FromHeight("compact", 240);
            var threeQuarterDetent = BottomSheetDetent.FromRatio("three-quarters", 0.75);
            var comfortableDetent = new ComfortableBottomSheetDetent();
            sheet.Detents.Insert(1, compactDetent);
            sheet.Detents.Insert(2, BottomSheetDetent.Medium);
            sheet.Detents.Insert(3, threeQuarterDetent);
            sheet.Detents.Insert(4, comfortableDetent);
            sheet.SelectedDetent = InitialDetentBox.SelectedIndex switch
            {
                0 => BottomSheetDetent.Content,
                2 => BottomSheetDetent.Expanded,
                3 => compactDetent,
                4 => threeQuarterDetent,
                5 => comfortableDetent,
                _ => BottomSheetDetent.Medium
            };

            var currentDetent = new TextBlock
            {
                Text = $"Selected: {sheet.SelectedDetent.Id}",
                FontWeight = Avalonia.Media.FontWeight.SemiBold
            };
            AutomationProperties.SetLiveSetting(currentDetent, AutomationLiveSetting.Polite);
            var closeStatus = new TextBlock
            {
                Text = "Close validation has not run.",
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                Opacity = 0.7
            };
            AutomationProperties.SetLiveSetting(closeStatus, AutomationLiveSetting.Polite);
            var noteLabel = new TextBlock
            {
                Text = "Result value",
                FontWeight = Avalonia.Media.FontWeight.SemiBold
            };
            var noteBox = new TextBox
            {
                Text = "Saved from the bottom sheet",
                PlaceholderText = "Value returned by Hide"
            };
            AutomationProperties.SetLabeledBy(noteBox, noteLabel);
            var contentButton = new Button { Content = "Content detent" };
            var mediumButton = new Button { Content = "Medium detent" };
            var compactButton = new Button { Content = "Compact detent" };
            var threeQuarterButton = new Button { Content = "75% detent" };
            var comfortableButton = new Button { Content = "Custom detent" };
            var expandedButton = new Button { Content = "Expanded detent" };
            var doneButton = new Button
            {
                Content = "Done",
                MinHeight = 40,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                Classes = { "accent" }
            };

            contentButton.Click += (_, _) => sheet.SelectedDetent = BottomSheetDetent.Content;
            compactButton.Click += (_, _) => sheet.SelectedDetent = compactDetent;
            mediumButton.Click += (_, _) => sheet.SelectedDetent = BottomSheetDetent.Medium;
            threeQuarterButton.Click += (_, _) => sheet.SelectedDetent = threeQuarterDetent;
            comfortableButton.Click += (_, _) => sheet.SelectedDetent = comfortableDetent;
            expandedButton.Click += (_, _) => sheet.SelectedDetent = BottomSheetDetent.Expanded;
            doneButton.Click += (_, _) =>
                sheet.Hide($"{noteBox.Text} · {sheet.SelectedDetent?.Id}");
            sheet.SelectedDetentChanged += (_, args) => currentDetent.Text = $"Selected: {args.NewDetent.Id}";

            var canceledOnce = false;
            sheet.Closing += async (_, args) =>
            {
                if (CancelFirstCloseCheck.IsChecked == true && !canceledOnce)
                {
                    canceledOnce = true;
                    args.Cancel = true;
                    closeStatus.Text = $"Canceled the first {args.DismissReason} close. Try again.";
                    return;
                }

                if (DeferClosingCheck.IsChecked != true)
                {
                    closeStatus.Text = $"Accepted {args.DismissReason} close without a deferral.";
                    return;
                }

                var deferral = args.GetDeferral();
                try
                {
                    closeStatus.Text = $"Deferring {args.DismissReason} close…";
                    await Task.Delay(400);
                    closeStatus.Text = $"Completed {args.DismissReason} close deferral.";
                }
                finally
                {
                    deferral.Complete();
                }
            };

            sheet.Content = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    new TextBlock
                    {
                        Text = "Drag the handle or use these buttons. The same instance can move between detents without closing.",
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                        Opacity = 0.7
                    },
                    currentDetent,
                    new WrapPanel
                    {
                        ItemSpacing = 8,
                        LineSpacing = 8,
                        Children =
                        {
                            contentButton,
                            compactButton,
                            mediumButton,
                            threeQuarterButton,
                            comfortableButton,
                            expandedButton
                        }
                    },
                    new StackPanel
                    {
                        Spacing = 5,
                        Children = { noteLabel, noteBox }
                    },
                    closeStatus,
                    new Separator(),
                    new TextBlock
                    {
                        Text = "Expanded content remains constrained to the owning TopLevel's safe available bounds.",
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap
                    },
                    doneButton
                }
            };

            try
            {
                StatusText.Text = "Bottom sheet open…";
                var result = await sheet.ShowAsync(owner);
                StatusText.Text =
                    $"Bottom sheet closed by {result.DismissReason}. " +
                    $"Returned value: “{result.Value ?? "none"}”.";
            }
            catch (InvalidOperationException exception)
            {
                StatusText.Text = exception.Message;
            }
        }

        private sealed class ComfortableBottomSheetDetent : BottomSheetDetent
        {
            public ComfortableBottomSheetDetent()
                : base("comfortable")
            {
            }

            protected override double GetHeight(double availableHeight, double contentHeight) =>
                Math.Min(availableHeight, Math.Max(280, Math.Min(contentHeight, availableHeight * 0.66)));
        }

        private void ApplyTransition(BottomSheet sheet)
        {
            switch (TransitionBox.SelectedIndex)
            {
                case 1:
                    sheet.Transition = null;
                    break;
                case 2:
                    sheet.Transition = new PageSlide(
                        TimeSpan.FromMilliseconds(500),
                        PageSlide.SlideAxis.Vertical);
                    break;
                case 3:
                    sheet.Transition = new CrossFade(TimeSpan.FromMilliseconds(400));
                    break;
                case 4:
                    sheet.Transition = new CompositePageTransition
                    {
                        PageTransitions =
                        {
                            new CrossFade(TimeSpan.FromMilliseconds(350)),
                            new PageSlide(TimeSpan.FromMilliseconds(350), PageSlide.SlideAxis.Vertical)
                        }
                    };
                    break;
            }
        }
    }
}
