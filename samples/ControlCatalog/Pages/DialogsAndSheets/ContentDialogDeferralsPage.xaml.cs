using System;
using System.Threading.Tasks;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ControlCatalog.Pages
{
    /// <summary>Demonstrates asynchronous dialog action validation.</summary>
    public partial class ContentDialogDeferralsPage : UserControl
    {
        public ContentDialogDeferralsPage()
        {
            InitializeComponent();
        }

        private async void OnShowDialog(object? sender, RoutedEventArgs e)
        {
            if (TopLevel.GetTopLevel(this) is not { } owner)
                return;

            var editor = new TextBox
            {
                Text = InitialValueBox.Text,
                PlaceholderText = "Required value"
            };
            AutomationProperties.SetName(editor, "Value to validate");
            var validationText = new TextBlock
            {
                Text = "Press Save to validate.",
                Opacity = 0.7,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            };
            var dialog = new ContentDialog
            {
                Title = "Validate before closing",
                Content = new StackPanel
                {
                    Spacing = 10,
                    Children = { editor, validationText }
                },
                PrimaryButtonContent = "Save",
                CloseButtonContent = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                CancelButton = ContentDialogButton.Close
            };

            dialog.PrimaryButtonClick += async (_, args) =>
            {
                var deferral = args.GetDeferral();
                try
                {
                    validationText.Text = "Validating asynchronously…";
                    await Task.Delay(650);

                    if (RequireValueCheck.IsChecked == true &&
                        string.IsNullOrWhiteSpace(editor.Text))
                    {
                        args.Cancel = true;
                        validationText.Text = "Validation failed. Enter a value and try again.";
                    }
                    else
                    {
                        validationText.Text = "Validation succeeded.";
                    }
                }
                finally
                {
                    deferral.Complete();
                }
            };
            dialog.Closing += async (_, args) =>
            {
                if (DeferClosingCheck.IsChecked != true)
                    return;

                var deferral = args.GetDeferral();
                try
                {
                    validationText.Text = $"Finalizing {args.DismissReason} close…";
                    await Task.Delay(400);
                }
                finally
                {
                    deferral.Complete();
                }
            };

            try
            {
                StatusText.Text = "Dialog open…";
                var result = await dialog.ShowAsync(owner);
                StatusText.Text =
                    $"Dialog closed with {result}. Validated value: “{editor.Text}”.";
            }
            catch (InvalidOperationException exception)
            {
                StatusText.Text = exception.Message;
            }
        }
    }
}
