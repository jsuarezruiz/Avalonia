using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ControlCatalog.Pages
{
    /// <summary>Demonstrates the TopLevel-scoped message-dialog provider.</summary>
    public partial class MessageDialogPage : UserControl
    {
        public MessageDialogPage()
        {
            InitializeComponent();
            IconBox.ItemsSource = Enum.GetValues<MessageDialogIcon>();
            IconBox.SelectedItem = MessageDialogIcon.Warning;
        }

        private async void OnShowMessage(object? sender, RoutedEventArgs e)
        {
            if (TopLevel.GetTopLevel(this) is not { } owner)
                return;

            var actions = ArchiveCheck.IsChecked == true
                ? new[]
                {
                    new MessageDialogAction("cancel", "Cancel") { IsCancel = true },
                    new MessageDialogAction("archive", "Archive"),
                    new MessageDialogAction("delete", "Delete")
                    {
                        IsDefault = true,
                        IsDestructive = DestructiveCheck.IsChecked == true
                    }
                }
                : new[]
                {
                    new MessageDialogAction("cancel", "Cancel") { IsCancel = true },
                    new MessageDialogAction("delete", "Delete")
                    {
                        IsDefault = true,
                        IsDestructive = DestructiveCheck.IsChecked == true
                    }
                };
            var options = new MessageDialogOptions(
                MessageBox.Text ?? string.Empty,
                actions)
            {
                Title = TitleBox.Text,
                Detail = DetailBox.Text,
                Icon = IconBox.SelectedItem is MessageDialogIcon icon
                    ? icon
                    : MessageDialogIcon.None
            };

            try
            {
                if (!owner.MessageDialogs.IsSupported)
                {
                    StatusText.Text = "Native message dialogs are not supported by this TopLevel.";
                    return;
                }

                StatusText.Text = "Message dialog open…";
                var result = await owner.MessageDialogs.ShowAsync(options);
                StatusText.Text = result.ActionId is { } actionId
                    ? $"Selected action “{actionId}”. Dismissal reason: {result.DismissReason}."
                    : $"No action selected. Dismissal reason: {result.DismissReason}.";
            }
            catch (InvalidOperationException exception)
            {
                StatusText.Text = exception.Message;
            }
            catch (NotSupportedException exception)
            {
                StatusText.Text = exception.Message;
            }
        }
    }
}
