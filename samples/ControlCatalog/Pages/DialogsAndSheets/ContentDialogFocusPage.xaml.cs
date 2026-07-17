using System;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace ControlCatalog.Pages
{
    /// <summary>Demonstrates managed-dialog focus, keyboard, and accessibility behavior.</summary>
    public partial class ContentDialogFocusPage : UserControl
    {
        public ContentDialogFocusPage()
        {
            InitializeComponent();
        }

        private async void OnShowDialog(object? sender, RoutedEventArgs e)
        {
            if (TopLevel.GetTopLevel(this) is not { } owner)
                return;

            // Clicking the launcher focuses the button. Move focus deliberately so
            // the sample can verify the element restored by the dialog lifecycle.
            FocusMarkerBox.Focus(NavigationMethod.Unspecified);

            var nameBox = new TextBox { Text = "Avery", PlaceholderText = "Display name" };
            var emailBox = new TextBox { Text = "avery@example.com", PlaceholderText = "Email address" };
            var updates = new CheckBox { Content = "Send product updates", IsChecked = true };
            AutomationProperties.SetName(nameBox, "Display name");
            AutomationProperties.SetName(emailBox, "Email address");

            var announcement = new TextBlock
            {
                Text = "Use Tab to confirm focus remains inside this dialog.",
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.7
            };
            AutomationProperties.SetLiveSetting(announcement, AutomationLiveSetting.Polite);

            var dialog = new ContentDialog
            {
                Title = "Keyboard and focus playground",
                Content = new StackPanel
                {
                    Spacing = 10,
                    MaxWidth = 440,
                    Children = { nameBox, emailBox, updates, announcement }
                },
                PrimaryButtonContent = "Save",
                SecondaryButtonContent = "Later",
                CloseButtonContent = "Cancel",
                DefaultButton = DefaultButtonBox.SelectedIndex switch
                {
                    0 => ContentDialogButton.Primary,
                    1 => ContentDialogButton.Secondary,
                    _ => ContentDialogButton.None
                },
                CancelButton = ContentDialogButton.Close,
                IsEscapeEnabled = EscapeCheck.IsChecked == true,
                IsSystemBackEnabled = SystemBackCheck.IsChecked == true,
                IsLightDismissEnabled = LightDismissCheck.IsChecked == true
            };

            ContentDialogClosedEventArgs? closed = null;
            dialog.Opened += (_, _) =>
            {
                var focused = owner.FocusManager?.GetFocusedElement();
                announcement.Text = $"Initial focus: {DescribeFocus(focused)}. Tab navigation is cyclic.";
            };
            dialog.Closed += (_, args) => closed = args;

            var result = await dialog.ShowAsync(owner);
            var restored = ReferenceEquals(owner.FocusManager?.GetFocusedElement(), FocusMarkerBox);
            StatusText.Text =
                $"Dialog closed with {result}. Dismissal reason: {closed?.DismissReason}. " +
                $"Focus restoration {(restored ? "succeeded" : "failed")}.";
        }

        private static string DescribeFocus(IInputElement? element)
        {
            if (element is not Control control)
                return element?.GetType().Name ?? "none";

            return AutomationProperties.GetName(control) ??
                (control is Button { Content: string text } ? text : control.GetType().Name);
        }
    }
}
