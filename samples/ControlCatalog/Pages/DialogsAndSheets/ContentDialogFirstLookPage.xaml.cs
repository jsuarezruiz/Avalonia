using System;
using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ControlCatalog.Pages
{
    /// <summary>Demonstrates the basic managed content-dialog contract.</summary>
    public partial class ContentDialogFirstLookPage : UserControl
    {
        public ContentDialogFirstLookPage()
        {
            InitializeComponent();
        }

        private async void OnShowDialog(object? sender, RoutedEventArgs e)
        {
            if (TopLevel.GetTopLevel(this) is not { } owner)
                return;

            var valueBox = new TextBox
            {
                Text = "Sample value",
                PlaceholderText = "Value to save"
            };
            AutomationProperties.SetName(valueBox, "Value to save");
            var eventText = new TextBlock
            {
                Text = "Waiting for an action…",
                Opacity = 0.7,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            };
            var dialog = new ContentDialog
            {
                Title = TitleBox.Text,
                Content = new StackPanel
                {
                    Spacing = 10,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = BodyBox.Text,
                            TextWrapping = Avalonia.Media.TextWrapping.Wrap
                        },
                        valueBox,
                        eventText
                    }
                },
                PrimaryButtonContent = "Save",
                SecondaryButtonContent = "Don't save",
                CloseButtonContent = "Cancel",
                DefaultButton = DefaultActionBox.SelectedIndex switch
                {
                    0 => ContentDialogButton.Primary,
                    1 => ContentDialogButton.Secondary,
                    _ => ContentDialogButton.None
                },
                CancelButton = ContentDialogButton.Close,
                IsLightDismissEnabled = LightDismissCheck.IsChecked == true,
                IsEscapeEnabled = EscapeCheck.IsChecked == true,
                IsSystemBackEnabled = SystemBackCheck.IsChecked == true
            };
            ApplyTransition(dialog);

            dialog.Opening += (_, _) => StatusText.Text = "Opening transition…";
            dialog.Opened += (_, _) => StatusText.Text = "Opened after transition completion.";
            dialog.Closing += (_, _) => StatusText.Text = "Closing transition…";
            ContentDialogClosedEventArgs? closed = null;
            dialog.Closed += (_, args) =>
            {
                closed = args;
                StatusText.Text =
                    $"Dialog closed with {args.Result}. Dismissal reason: {args.DismissReason}.";
            };
            dialog.PrimaryButtonClick += (_, _) => eventText.Text = "PrimaryButtonClick → Save";
            dialog.SecondaryButtonClick += (_, _) => eventText.Text = "SecondaryButtonClick → Don't save";
            dialog.CloseButtonClick += (_, _) => eventText.Text = "CloseButtonClick → Cancel";

            try
            {
                StatusText.Text = "Dialog open…";
                var result = await dialog.ShowAsync(owner);
                StatusText.Text =
                    $"Dialog closed with {result}. Dismissal reason: {closed?.DismissReason}. " +
                    $"Value: “{valueBox.Text}”.";
            }
            catch (InvalidOperationException exception)
            {
                StatusText.Text = exception.Message;
            }
        }

        private void ApplyTransition(ContentDialog dialog)
        {
            switch (TransitionBox.SelectedIndex)
            {
                case 1:
                    dialog.Transition = null;
                    break;
                case 2:
                    dialog.Transition = new CrossFade(TimeSpan.FromMilliseconds(500));
                    break;
                case 3:
                    dialog.Transition = new PageSlide(
                        TimeSpan.FromMilliseconds(400),
                        PageSlide.SlideAxis.Vertical);
                    break;
                case 4:
                    dialog.Transition = new CompositePageTransition
                    {
                        PageTransitions =
                        {
                            new CrossFade(TimeSpan.FromMilliseconds(350)),
                            new PageSlide(TimeSpan.FromMilliseconds(350), PageSlide.SlideAxis.Vertical)
                        }
                    };
                    break;
                case 5:
                    dialog.Transition = new ScaleLiftPageTransition();
                    break;
            }
        }
    }
}
