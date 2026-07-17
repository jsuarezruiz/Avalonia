using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace ControlCatalog.Pages
{
    /// <summary>Demonstrates TopLevel-scoped modal ownership and explicit sequencing.</summary>
    public partial class ModalCoordinationPage : UserControl
    {
        private readonly Queue<string> _statusLines = new();
        private bool _scenarioRunning;

        public ModalCoordinationPage()
        {
            InitializeComponent();

            var supportsMultipleWindows =
                Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime;
            TwoOwnersButton.IsEnabled = supportsMultipleWindows;
            OwnerCloseButton.IsEnabled = supportsMultipleWindows;
            TwoOwnerAvailabilityText.Text = supportsMultipleWindows
                ? "Available: this application is using the classic desktop lifetime."
                : "Requires the classic desktop multi-window lifetime; single-view targets still use the same per-TopLevel contract.";
            OwnerCloseAvailabilityText.Text = TwoOwnerAvailabilityText.Text;
            AutomationProperties.SetHelpText(TwoOwnersButton, TwoOwnerAvailabilityText.Text);
            AutomationProperties.SetHelpText(OwnerCloseButton, OwnerCloseAvailabilityText.Text);
        }

        private async void OnCancellation(object? sender, RoutedEventArgs e) =>
            await RunScenarioAsync("Caller cancellation", RunCancellationAsync);

        private async void OnConcurrency(object? sender, RoutedEventArgs e) =>
            await RunScenarioAsync("Concurrent request", RunConcurrencyAsync);

        private async void OnSequence(object? sender, RoutedEventArgs e) =>
            await RunScenarioAsync("Explicit sequence", RunSequenceAsync);

        private async void OnReuse(object? sender, RoutedEventArgs e) =>
            await RunScenarioAsync("Dialog reuse", RunReuseAsync);

        private async void OnClosedChain(object? sender, RoutedEventArgs e) =>
            await RunScenarioAsync("Closed-handler chain", RunClosedChainAsync);

        private async void OnOwnerClose(object? sender, RoutedEventArgs e) =>
            await RunScenarioAsync("Owner close", RunOwnerCloseAsync);

        private async void OnTwoOwners(object? sender, RoutedEventArgs e) =>
            await RunScenarioAsync("Independent owners", RunTwoOwnersAsync);

        private async Task RunScenarioAsync(string name, Func<TopLevel, Task> scenario)
        {
            if (_scenarioRunning)
                return;

            if (TopLevel.GetTopLevel(this) is not { } owner)
            {
                AppendStatus($"{name}: no attached TopLevel.");
                return;
            }

            _scenarioRunning = true;
            SetScenarioButtonsEnabled(false);
            AppendStatus($"{name}: started.");

            try
            {
                await scenario(owner);
            }
            catch (Exception exception)
            {
                AppendStatus($"{name}: {exception.GetType().Name}: {exception.Message}");
            }
            finally
            {
                _scenarioRunning = false;
                SetScenarioButtonsEnabled(true);
            }
        }

        private async Task RunCancellationAsync(TopLevel owner)
        {
            using var cancellation = new CancellationTokenSource();
            var cancelOperation = new Button
            {
                Content = "Cancel ShowAsync",
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            cancelOperation.Classes.Add("accent");

            var dialog = new ContentDialog
            {
                Title = "Caller-owned cancellation",
                Content = new StackPanel
                {
                    Spacing = 12,
                    MaxWidth = 420,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "This button cancels the token supplied to ShowAsync. The surface is force-closed and the task completes as canceled.",
                            TextWrapping = TextWrapping.Wrap
                        },
                        cancelOperation
                    }
                },
                CloseButtonContent = "Close normally",
                CancelButton = ContentDialogButton.Close
            };
            cancelOperation.Click += (_, _) => cancellation.Cancel();

            try
            {
                var result = await dialog.ShowAsync(owner, cancellation.Token);
                AppendStatus($"Caller cancellation: closed normally with {result}.");
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                AppendStatus("Caller cancellation: task canceled and modal slot released.");
            }
        }

        private async Task RunConcurrencyAsync(TopLevel owner)
        {
            var modalTypes = new List<string> { "ContentDialog", "BottomSheet" };
            if (owner.MessageDialogs.IsSupported)
                modalTypes.Add("Native message dialog");

            var outcome = new TextBlock
            {
                Text = "First verify rejection while this dialog owns the slot. Then use the primary action to release the slot and open the selected surface.",
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.7
            };
            AutomationProperties.SetLiveSetting(outcome, AutomationLiveSetting.Polite);
            var attempt = new Button
            {
                Content = "Verify the second modal is rejected",
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            attempt.Classes.Add("accent");
            var competingType = new ComboBox
            {
                ItemsSource = modalTypes,
                SelectedIndex = 1,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            AutomationProperties.SetName(competingType, "Competing modal type");
            var first = new ContentDialog
            {
                Title = "Active modal slot",
                Content = new StackPanel
                {
                    Spacing = 12,
                    MaxWidth = 420,
                    Children = { outcome, competingType, attempt }
                },
                PrimaryButtonContent = "Release and open selected",
                CloseButtonContent = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                CancelButton = ContentDialogButton.Close
            };

            attempt.Click += async (_, _) =>
            {
                attempt.IsEnabled = false;
                try
                {
                    var requestedType = competingType.SelectedItem?.ToString() ?? "modal";
                    await ShowSelectedModalAsync(owner, competingType.SelectedIndex);
                    outcome.Text = $"Error: the competing {requestedType} opened unexpectedly.";
                    AppendStatus("Concurrent request: unexpectedly succeeded.");
                }
                catch (InvalidOperationException exception)
                {
                    outcome.Text = $"Correctly rejected: {exception.Message} Release the slot to open it normally.";
                    AppendStatus("Concurrent request: rejected; first modal remains active.");
                }
                finally
                {
                    attempt.IsEnabled = true;
                }
            };

            var result = await first.ShowAsync(owner);
            if (result != ContentDialogResult.Primary)
            {
                AppendStatus($"Concurrent request: stopped after the first modal ({result}).");
                return;
            }

            var selectedIndex = competingType.SelectedIndex;
            var selectedType = competingType.SelectedItem?.ToString() ?? "modal";
            AppendStatus($"Concurrent request: slot released; opening {selectedType}.");
            await ShowSelectedModalAsync(owner, selectedIndex);
            AppendStatus($"Concurrent request: {selectedType} opened and completed normally.");
        }

        private static async Task ShowSelectedModalAsync(TopLevel owner, int selectedIndex)
        {
            switch (selectedIndex)
            {
                case 1:
                {
                    var close = new Button
                    {
                        Content = "Close sheet",
                        HorizontalAlignment = HorizontalAlignment.Stretch
                    };
                    var sheet = new BottomSheet
                    {
                        Header = "Selected BottomSheet",
                        Content = new StackPanel
                        {
                            Spacing = 12,
                            Children =
                            {
                                new TextBlock
                                {
                                    Text = "The first modal released the slot before this sheet opened.",
                                    TextWrapping = TextWrapping.Wrap
                                },
                                close
                            }
                        }
                    };
                    close.Click += (_, _) => sheet.Hide();
                    await sheet.ShowAsync(owner);
                    break;
                }
                case 2:
                    await owner.MessageDialogs.ShowAsync(new MessageDialogOptions(
                        "The first modal released the slot before this native dialog opened.",
                        new MessageDialogAction("close", "Close") { IsCancel = true })
                    {
                        Title = "Selected message dialog"
                    });
                    break;
                default:
                    await new ContentDialog
                    {
                        Title = "Selected ContentDialog",
                        Content = "The first modal released the slot before this dialog opened.",
                        CloseButtonContent = "Close"
                    }.ShowAsync(owner);
                    break;
            }
        }

        private async Task RunSequenceAsync(TopLevel owner)
        {
            var first = CreateStepDialog(
                "Sequence · step 1",
                "The second dialog is created only after this ShowAsync operation completes.",
                "Continue");
            var firstResult = await first.ShowAsync(owner);
            if (firstResult != ContentDialogResult.Primary)
            {
                AppendStatus($"Explicit sequence: stopped after step 1 ({firstResult}).");
                return;
            }

            AppendStatus("Explicit sequence: step 1 released the slot; opening step 2.");
            var second = CreateStepDialog(
                "Sequence · step 2",
                "This modal acquired the same TopLevel slot after the first dialog fully closed.",
                "Finish");
            var secondResult = await second.ShowAsync(owner);
            AppendStatus($"Explicit sequence: completed with {secondResult}.");
        }

        private async Task RunReuseAsync(TopLevel owner)
        {
            var dialog = CreateStepDialog(
                "Reusable dialog · first show",
                "ShowAsync may be called again after this operation and its closing transition complete.",
                "Show again");
            var firstResult = await dialog.ShowAsync(owner);
            if (firstResult != ContentDialogResult.Primary)
            {
                AppendStatus($"Dialog reuse: stopped after first show ({firstResult}).");
                return;
            }

            dialog.Title = "Reusable dialog · second show";
            dialog.Content = new TextBlock
            {
                Text = "This is the same ContentDialog instance with updated content.",
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 420
            };
            dialog.PrimaryButtonContent = "Finish";

            var secondResult = await dialog.ShowAsync(owner);
            AppendStatus($"Dialog reuse: second show completed with {secondResult}.");
        }

        private async Task RunClosedChainAsync(TopLevel owner)
        {
            var first = CreateStepDialog(
                "Closed chain · step 1",
                "Choose Continue. The next ShowAsync call begins inside this dialog's Closed handler.",
                "Continue");
            var second = CreateStepDialog(
                "Closed chain · step 2",
                "This dialog opened after the first surface detached and released the owner slot.",
                "Finish");
            Task<ContentDialogResult>? secondTask = null;

            first.Closed += (_, args) =>
            {
                AppendStatus($"Closed-handler chain: first closed with {args.DismissReason}.");
                if (args.Result == ContentDialogResult.Primary)
                    secondTask = second.ShowAsync(owner);
            };

            var firstResult = await first.ShowAsync(owner);
            if (firstResult != ContentDialogResult.Primary)
            {
                AppendStatus($"Closed-handler chain: stopped after step 1 ({firstResult}).");
                return;
            }

            if (secondTask is null)
                throw new InvalidOperationException("The Closed handler did not start the second dialog.");

            var secondResult = await secondTask;
            AppendStatus($"Closed-handler chain: second dialog completed with {secondResult}.");
        }

        private async Task RunOwnerCloseAsync(TopLevel owner)
        {
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime)
            {
                AppendStatus("Owner close: unavailable for this application lifetime.");
                return;
            }

            var secondaryOwner = new Window
            {
                Title = "Disposable modal owner",
                Width = 480,
                Height = 300,
                MinWidth = 360,
                MinHeight = 240,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Content = new TextBlock
                {
                    Text = "Closing this window must force-clean its active modal.",
                    Margin = new Thickness(24),
                    TextWrapping = TextWrapping.Wrap,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };

            try
            {
                if (!await ShowAndWaitForOpenAsync(secondaryOwner))
                {
                    AppendStatus("Owner close: the secondary window closed before opening.");
                    return;
                }

                var closeOwner = new Button
                {
                    Content = "Close the owner window",
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                closeOwner.Classes.Add("accent");
                var dialog = new ContentDialog
                {
                    Title = "Owner lifecycle",
                    Content = new StackPanel
                    {
                        Spacing = 12,
                        MaxWidth = 420,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = "This does not request a normal dialog close. It closes the owning TopLevel.",
                                TextWrapping = TextWrapping.Wrap
                            },
                            closeOwner
                        }
                    }
                };
                ContentDialogClosedEventArgs? closed = null;
                dialog.Closed += (_, args) => closed = args;
                closeOwner.Click += (_, _) => secondaryOwner.Close();

                var result = await dialog.ShowAsync(secondaryOwner);
                AppendStatus($"Owner close: result {result}; reason {closed?.DismissReason}.");
            }
            finally
            {
                if (secondaryOwner.IsVisible)
                    secondaryOwner.Close();
            }
        }

        private async Task RunTwoOwnersAsync(TopLevel owner)
        {
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime)
            {
                AppendStatus("Independent owners: unavailable for this application lifetime.");
                return;
            }

            var secondaryOwner = new Window
            {
                Title = "Second modal owner",
                Width = 480,
                Height = 300,
                MinWidth = 360,
                MinHeight = 240,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Content = new Border
                {
                    Padding = new Thickness(24),
                    Child = new StackPanel
                    {
                        Spacing = 8,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = "Independent TopLevel",
                                FontSize = 20,
                                FontWeight = FontWeight.SemiBold,
                                HorizontalAlignment = HorizontalAlignment.Center
                            },
                            new TextBlock
                            {
                                Text = "This window owns its own modal slot.",
                                Opacity = 0.7,
                                TextWrapping = TextWrapping.Wrap,
                                TextAlignment = TextAlignment.Center
                            }
                        }
                    }
                }
            };

            try
            {
                if (!await ShowAndWaitForOpenAsync(secondaryOwner))
                {
                    AppendStatus("Independent owners: the secondary window closed before opening.");
                    return;
                }

                using var cancellation = new CancellationTokenSource();
                try
                {
                    var primaryDialog = new ContentDialog
                    {
                        Title = "Primary owner dialog",
                        Content = "This modal belongs only to the catalog window.",
                        CloseButtonContent = "Close primary",
                        CancelButton = ContentDialogButton.Close
                    };
                    var secondaryDialog = new ContentDialog
                    {
                        Title = "Secondary owner dialog",
                        Content = "This modal independently belongs to the second window.",
                        CloseButtonContent = "Close secondary",
                        CancelButton = ContentDialogButton.Close
                    };

                    var primaryTask = primaryDialog.ShowAsync(owner, cancellation.Token);
                    var secondaryTask = secondaryDialog.ShowAsync(secondaryOwner, cancellation.Token);
                    AppendStatus("Independent owners: both modal slots acquired successfully.");
                    var results = await Task.WhenAll(primaryTask, secondaryTask);
                    AppendStatus($"Independent owners: completed with {results[0]} and {results[1]}.");
                }
                catch
                {
                    cancellation.Cancel();
                    throw;
                }
            }
            finally
            {
                if (secondaryOwner.IsVisible)
                    secondaryOwner.Close();
            }
        }

        private static ContentDialog CreateStepDialog(string title, string message, string primaryText) =>
            new()
            {
                Title = title,
                Content = new TextBlock
                {
                    Text = message,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 420
                },
                PrimaryButtonContent = primaryText,
                CloseButtonContent = "Stop",
                DefaultButton = ContentDialogButton.Primary,
                CancelButton = ContentDialogButton.Close
            };

        private static async Task<bool> ShowAndWaitForOpenAsync(Window window)
        {
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            void OnOpened(object? sender, EventArgs args) => completion.TrySetResult(true);
            void OnClosed(object? sender, EventArgs args) => completion.TrySetResult(false);

            window.Opened += OnOpened;
            window.Closed += OnClosed;
            try
            {
                window.Show();
                return await completion.Task;
            }
            finally
            {
                window.Opened -= OnOpened;
                window.Closed -= OnClosed;
            }
        }

        private void SetScenarioButtonsEnabled(bool enabled)
        {
            CancellationButton.IsEnabled = enabled;
            ConcurrencyButton.IsEnabled = enabled;
            SequenceButton.IsEnabled = enabled;
            ReuseButton.IsEnabled = enabled;
            ClosedChainButton.IsEnabled = enabled;
            OwnerCloseButton.IsEnabled = enabled &&
                Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime;
            TwoOwnersButton.IsEnabled = enabled &&
                Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime;
        }

        private void AppendStatus(string message)
        {
            const int maximumLines = 9;
            if (_statusLines.Count == maximumLines)
                _statusLines.Dequeue();

            _statusLines.Enqueue($"• {message}");
            StatusText.Text = string.Join(Environment.NewLine, _statusLines);
        }
    }
}
