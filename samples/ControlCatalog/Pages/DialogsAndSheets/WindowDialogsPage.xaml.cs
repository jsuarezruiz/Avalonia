using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;

namespace ControlCatalog.Pages
{
    /// <summary>Demonstrates desktop Window ownership and modality.</summary>
    public partial class WindowDialogsPage : UserControl
    {
        public WindowDialogsPage()
        {
            InitializeComponent();
        }

        private void OnOpenDecoratedWindow(object? sender, RoutedEventArgs e)
        {
            TryOpenWindow(() => new DecoratedWindow().Show(), "Opened a modeless decorated window.");
        }

        private async void OnOpenDecoratedDialog(object? sender, RoutedEventArgs e)
        {
            if (TryGetOwner() is not { } owner)
                return;

            try
            {
                StatusText.Text = "Modal decorated window open…";
                await new DecoratedWindow().ShowDialog(owner);
                StatusText.Text = "Modal decorated window closed.";
            }
            catch (Exception exception)
            {
                ReportFailure(exception);
            }
        }

        private async void OnOpenDialog(object? sender, RoutedEventArgs e)
        {
            await ShowPlainDialogAsync(showInTaskbar: true);
        }

        private async void OnOpenDialogWithoutTaskbarIcon(object? sender, RoutedEventArgs e)
        {
            await ShowPlainDialogAsync(showInTaskbar: false);
        }

        private void OnOpenOwnedWindow(object? sender, RoutedEventArgs e)
        {
            ShowOwnedWindow(showInTaskbar: true);
        }

        private void OnOpenOwnedWindowWithoutTaskbarIcon(object? sender, RoutedEventArgs e)
        {
            ShowOwnedWindow(showInTaskbar: false);
        }

        private async System.Threading.Tasks.Task ShowPlainDialogAsync(bool showInTaskbar)
        {
            if (TryGetOwner() is not { } owner)
                return;

            try
            {
                var window = CreateSampleWindow("Modal dialog");
                window.ShowInTaskbar = showInTaskbar;
                StatusText.Text = showInTaskbar
                    ? "Modal dialog open…"
                    : "Modal dialog without a taskbar icon open…";
                await window.ShowDialog(owner);
                StatusText.Text = "Modal dialog closed.";
            }
            catch (Exception exception)
            {
                ReportFailure(exception);
            }
        }

        private void ShowOwnedWindow(bool showInTaskbar)
        {
            if (TryGetOwner() is not { } owner)
                return;

            TryOpenWindow(() =>
            {
                var window = CreateSampleWindow("Owned modeless window");
                window.ShowInTaskbar = showInTaskbar;
                window.Show(owner);
            }, showInTaskbar
                ? "Opened an owned modeless window."
                : "Opened an owned modeless window without a taskbar icon.");
        }

        private Window? TryGetOwner()
        {
            if (TopLevel.GetTopLevel(this) is Window owner)
                return owner;

            StatusText.Text = "This example requires a desktop Window owner.";
            return null;
        }

        private void TryOpenWindow(Action action, string successMessage)
        {
            try
            {
                action();
                StatusText.Text = successMessage;
            }
            catch (Exception exception)
            {
                ReportFailure(exception);
            }
        }

        private void ReportFailure(Exception exception)
        {
            StatusText.Text = $"Window could not be opened: {exception.Message}";
        }

        private static Window CreateSampleWindow(string title)
        {
            var closeButton = new Button
            {
                Content = "Close",
                HorizontalAlignment = HorizontalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                IsDefault = true,
                MinWidth = 100
            };
            var nestedDialogButton = new Button
            {
                Content = "Open nested dialog",
                HorizontalAlignment = HorizontalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                MinWidth = 160
            };
            var window = new Window
            {
                Title = title,
                Height = 240,
                Width = 360,
                MinHeight = 220,
                MinWidth = 320,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new StackPanel
                {
                    Margin = new Avalonia.Thickness(24),
                    Spacing = 16,
                    VerticalAlignment = VerticalAlignment.Center,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "This is a secondary Avalonia Window.",
                            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                            HorizontalAlignment = HorizontalAlignment.Center
                        },
                        closeButton,
                        nestedDialogButton
                    }
                }
            };

            closeButton.Click += (_, _) => window.Close();
            nestedDialogButton.Click += (_, _) =>
            {
                var dialog = CreateSampleWindow("Nested modal dialog");
                _ = dialog.ShowDialog(window);
            };

            return window;
        }
    }
}
