using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace ControlCatalog.Pages
{
    internal enum ProductFlow
    {
        SystemNotification,
        NativeConfirmation,
        SuccessSheet,
        AccountSwitcher,
        Onboarding,
        GiftCard
    }

    internal sealed class ProductFlowShowcaseLauncher
    {
        private const int SystemNotificationShowcaseSlots = 8;
        private static int s_systemNotificationSequence;

        internal async Task ShowAsync(ProductFlow flow, TopLevel owner)
        {
            switch (flow)
            {
                case ProductFlow.SystemNotification:
                    await ShowSystemNotification();
                    break;
                case ProductFlow.NativeConfirmation:
                    await ShowNativeConfirmation(owner);
                    break;
                case ProductFlow.SuccessSheet:
                    await ShowSuccess(owner);
                    break;
                case ProductFlow.AccountSwitcher:
                    await ShowAccountSwitcher(owner);
                    break;
                case ProductFlow.Onboarding:
                    await ShowOnboarding(owner);
                    break;
                case ProductFlow.GiftCard:
                    await ShowGiftCard(owner);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(flow));
            }
        }

        private static async Task ShowSystemNotification()
        {
            var manager = Application.Current?.SystemNotifications ??
                throw new InvalidOperationException("The Avalonia application is not available.");
            if (!manager.IsSupported)
                throw new NotSupportedException("System notifications are not supported on this platform.");

            var permission = await manager.GetPermissionStatusAsync();
            if (permission == SystemNotificationPermissionStatus.NotDetermined)
                permission = await manager.RequestPermissionAsync();
            if (permission == SystemNotificationPermissionStatus.Unsupported)
                throw new NotSupportedException("System notifications are not supported on this platform.");
            if (permission != SystemNotificationPermissionStatus.Granted)
            {
                throw new UnauthorizedAccessException(
                    $"System-notification permission is {permission}.");
            }

            var slot = (uint)Interlocked.Increment(ref s_systemNotificationSequence) %
                SystemNotificationShowcaseSlots;
            await manager.ShowAsync(new SystemNotification(
                $"catalog.export-complete.showcase-{slot}",
                "Marketing-kit.zip is ready to share.")
            {
                Title = "Design export complete"
            });
        }

        private static async Task ShowNativeConfirmation(TopLevel owner)
        {
            if (!owner.MessageDialogs.IsSupported)
                throw new NotSupportedException("Native message dialogs are not supported by this TopLevel.");

            await owner.MessageDialogs.ShowAsync(new MessageDialogOptions(
                "The local project and its generated files will be removed from this device.",
                new[]
                {
                    new MessageDialogAction("cancel", "Cancel") { IsCancel = true },
                    new MessageDialogAction("delete", "Delete")
                    {
                        IsDefault = true,
                        IsDestructive = true
                    }
                })
            {
                Title = "Delete local project?",
                Detail = "This action cannot be undone.",
                Icon = MessageDialogIcon.Warning
            });
        }

        private async Task ShowSuccess(TopLevel owner)
        {
            var done = AccentButton("Done", "#14B8A6");
            var content = new StackPanel { Spacing = 16, HorizontalAlignment = HorizontalAlignment.Center, MaxWidth = 430 };
            content.Children.Add(Avatar("✓", "#14B8A6", 82, 34));
            content.Children.Add(new TextBlock { Text = "Workspace is ready", FontSize = 24, FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center });
            content.Children.Add(new TextBlock { Text = "Your team, permissions, and starter project have been configured. Invite collaborators whenever you're ready.", TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, Opacity = 0.7 });
            content.Children.Add(done);
            var sheet = CreateSheet("Setup complete", content);
            done.Click += (_, _) => sheet.Hide("Workspace created");
            await sheet.ShowAsync(owner);
        }

        private async Task ShowAccountSwitcher(TopLevel owner)
        {
            var accounts = new StackPanel
            {
                Spacing = 8,
                MinWidth = 240,
                MaxWidth = 420,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            accounts.Children.Add(new TextBlock { Text = "Continue as", Opacity = 0.65 });
            accounts.Children.Add(AccountChoice("JS", "Javier Suárez", "Personal · javier@example.com", true));
            accounts.Children.Add(AccountChoice("AV", "Avalonia UI", "Organization · 18 projects"));
            accounts.Children.Add(AccountChoice("AG", "Aster Grove", "Organization · 6 projects"));
            var dialog = new ContentDialog
            {
                Title = "Switch account",
                Content = accounts,
                PrimaryButtonContent = "Continue",
                CloseButtonContent = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                CancelButton = ContentDialogButton.Close
            };
            await dialog.ShowAsync(owner);
        }

        private async Task ShowOnboarding(TopLevel owner)
        {
            var name = new TextBox { PlaceholderText = "e.g. Product Design", Text = "Avalonia Gallery" };
            var template = new ComboBox { ItemsSource = new[] { "Starter workspace", "Product roadmap", "Blank workspace" }, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
            var invite = new TextBox { PlaceholderText = "teammate@example.com" };
            var stepLabel = new TextBlock { FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush("#3B82F6") };
            var progress = new ProgressBar { Minimum = 0, Maximum = 3, Height = 5 };
            AutomationProperties.SetName(progress, "Onboarding progress");
            var heading = new TextBlock { FontSize = 22, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap };
            AutomationProperties.SetHeadingLevel(heading, 2);
            AutomationProperties.SetLiveSetting(heading, AutomationLiveSetting.Polite);
            var description = new TextBlock { TextWrapping = TextWrapping.Wrap, Opacity = 0.7 };
            var stepBody = new StackPanel { Spacing = 12 };
            var content = new StackPanel
            {
                Spacing = 16,
                MinWidth = 240,
                MaxWidth = 440,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            content.Children.Add(stepLabel);
            content.Children.Add(progress);
            content.Children.Add(heading);
            content.Children.Add(description);
            content.Children.Add(stepBody);
            var dialog = new ContentDialog
            {
                Title = "Welcome aboard",
                Content = content,
                PrimaryButtonContent = "Next",
                SecondaryButtonContent = "Skip setup",
                CloseButtonContent = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                CancelButton = ContentDialogButton.Close
            };
            var step = 1;

            void RenderStep()
            {
                stepLabel.Text = $"STEP {step} OF 3";
                progress.Value = step;
                stepBody.Children.Clear();

                switch (step)
                {
                    case 1:
                        heading.Text = "Create your workspace";
                        description.Text = "Give the team a shared home. You can change every setting later.";
                        stepBody.Children.Add(Labeled("Workspace name", name));
                        stepBody.Children.Add(Labeled("Start from", template));
                        dialog.PrimaryButtonContent = "Next";
                        break;
                    case 2:
                        heading.Text = "Choose what matters";
                        description.Text = "Personalize the starter experience. These choices only affect the initial workspace.";
                        stepBody.Children.Add(new CheckBox { Content = "Plan and track product work", IsChecked = true });
                        stepBody.Children.Add(new CheckBox { Content = "Review designs with the team", IsChecked = true });
                        stepBody.Children.Add(new CheckBox { Content = "Publish documentation" });
                        stepBody.Children.Add(new CheckBox { Content = "Automate release checklists" });
                        dialog.PrimaryButtonContent = "Next";
                        break;
                    default:
                        heading.Text = "Bring your team along";
                        description.Text = "Invite someone now, or leave this empty and share an invitation link later.";
                        stepBody.Children.Add(Labeled("Teammate email (optional)", invite));
                        stepBody.Children.Add(new CheckBox { Content = "Send me a short getting-started guide", IsChecked = true });
                        stepBody.Children.Add(new Border
                        {
                            Padding = new Avalonia.Thickness(12),
                            CornerRadius = new Avalonia.CornerRadius(10),
                            Background = Brush("#123B82F6"),
                            Child = new TextBlock
                            {
                                Text = $"Ready to create “{name.Text}” from the {template.SelectedItem?.ToString()?.ToLowerInvariant()} template.",
                                TextWrapping = TextWrapping.Wrap
                            }
                        });
                        dialog.PrimaryButtonContent = "Create workspace";
                        break;
                }
            }

            dialog.PrimaryButtonClick += (_, args) =>
            {
                if (step >= 3)
                    return;

                args.Cancel = true;
                step++;
                RenderStep();
            };
            RenderStep();
            await dialog.ShowAsync(owner);
        }

        private async Task ShowGiftCard(TopLevel owner)
        {
            using var animationCancellation = new CancellationTokenSource();
            var highContrast = owner.GetPlatformSettings()?.GetColorValues().ContrastPreference ==
                ColorContrastPreference.High;
            var content = new Grid
            {
                MinWidth = 260,
                MaxWidth = 430,
                Margin = new Thickness(0, 45, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
                ClipToBounds = false
            };

            var blob1 = new Border
            {
                Width = 220,
                Height = 220,
                CornerRadius = new CornerRadius(110),
                Background = new SolidColorBrush(Color.Parse("#FFD700"), 0.1),
                Margin = new Thickness(-220, -120, 0, 0),
                Effect = new BlurEffect { Radius = 70 }
            };
            var blob2 = new Border
            {
                Width = 180,
                Height = 180,
                CornerRadius = new CornerRadius(90),
                Background = new SolidColorBrush(Color.Parse("#FF8C00"), 0.1),
                Margin = new Thickness(200, 180, 0, 0),
                Effect = new BlurEffect { Radius = 60 },
                RenderTransform = new TranslateTransform()
            };
            AutomationProperties.SetAccessibilityView(blob1, AccessibilityView.Raw);
            AutomationProperties.SetAccessibilityView(blob2, AccessibilityView.Raw);

            var shimmer = new Border
            {
                Width = 600,
                Height = 600,
                Margin = new Thickness(-500, -150, 0, 0),
                RenderTransform = new TransformGroup
                {
                    Children =
                    {
                        new RotateTransform(30),
                        new TranslateTransform()
                    }
                },
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
                    GradientStops =
                    {
                        new GradientStop(Colors.Transparent, 0.4),
                        new GradientStop(Color.Parse("#1AFFFFFF"), 0.5),
                        new GradientStop(Colors.Transparent, 0.6)
                    }
                }
            };
            AutomationProperties.SetAccessibilityView(shimmer, AccessibilityView.Raw);

            var claim = new Button
            {
                Content = "Claim Now",
                Background = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
                    GradientStops =
                    {
                        new GradientStop(Color.Parse("#FFD700"), 0),
                        new GradientStop(Color.Parse("#FFA500"), 1)
                    }
                },
                Foreground = Brushes.Black,
                CornerRadius = new CornerRadius(27),
                Height = 54,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                FontWeight = FontWeight.Bold,
                FontSize = 16
            };
            claim.Classes.Add("gift-card-claim");
            AutomationProperties.SetHelpText(claim, "Adds fifty dollars to your gift-card balance and closes this dialog.");
            var claimGlow = new Border
            {
                CornerRadius = new CornerRadius(27),
                BoxShadow = BoxShadows.Parse("0 4 15 0 #66FFA500"),
                Margin = new Thickness(10, 10, 10, 0),
                Child = claim
            };
            var cardContent = new StackPanel
            {
                Spacing = 24,
                Children =
                {
                    new StackPanel
                    {
                        Spacing = 6,
                        Children =
                        {
                            new TextBlock { Text = "EXCLUSIVE REWARD", FontSize = 10, FontWeight = FontWeight.Black, Foreground = Brush("#FFD700"), HorizontalAlignment = HorizontalAlignment.Center, LetterSpacing = 4 },
                            new TextBlock { Text = "Gift card ready!", FontSize = 26, FontWeight = FontWeight.Bold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center }
                        }
                    },
                    new Border { Height = 1, Background = Brushes.White, Opacity = 0.08, Margin = new Thickness(40, 0) },
                    new TextBlock { Text = "$50.00", FontSize = 64, FontWeight = FontWeight.Black, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center },
                    new TextBlock
                    {
                        Text = "This digital gift card is yours. Use it on your next purchase.",
                        TextWrapping = TextWrapping.Wrap,
                        TextAlignment = TextAlignment.Center,
                        Foreground = Brushes.White,
                        Opacity = 0.58,
                        FontSize = 14,
                        Margin = new Thickness(15, 0)
                    },
                    claimGlow
                }
            };
            var close = new Button
            {
                Content = "×",
                Width = 44,
                Height = 44,
                Padding = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                FontSize = 24,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 14, 14, 0)
            };
            close.Classes.Add("gift-card-close");
            AutomationProperties.SetName(close, "Close gift card");

            var card = new Border
            {
                MinWidth = 260,
                MaxWidth = 350,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                CornerRadius = new CornerRadius(32),
                ClipToBounds = true,
                BorderBrush = highContrast ? Brushes.White : Brushes.Transparent,
                BorderThickness = highContrast ? new Thickness(2) : new Thickness(0),
                Background = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                    GradientStops =
                    {
                        new GradientStop(Color.Parse("#0F0F0F"), 0),
                        new GradientStop(Color.Parse("#252525"), 1)
                    }
                },
                BoxShadow = BoxShadows.Parse("0 20 60 0 #99000000, inset 0 0 20 0 #4CFFD700"),
                Child = new Panel
                {
                    Children =
                    {
                        shimmer,
                        new Border { Padding = new Thickness(24, 70, 24, 32), Child = cardContent },
                        close
                    }
                }
            };

            var halo = new Border
            {
                Width = 110,
                Height = 110,
                CornerRadius = new CornerRadius(55),
                Background = Brush("#FFD700"),
                Opacity = 0.2,
                Margin = new Thickness(0, -55, 0, 0),
                VerticalAlignment = VerticalAlignment.Top,
                Effect = new BlurEffect { Radius = 40 }
            };
            AutomationProperties.SetAccessibilityView(halo, AccessibilityView.Raw);
            var iconContainer = new Border
            {
                Width = 90,
                Height = 90,
                CornerRadius = new CornerRadius(45),
                Background = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                    GradientStops =
                    {
                        new GradientStop(Color.Parse("#FFD700"), 0),
                        new GradientStop(Color.Parse("#FFA500"), 1)
                    }
                },
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, -45, 0, 0),
                BoxShadow = BoxShadows.Parse("0 10 25 0 #7F000000"),
                Child = new PathIcon
                {
                    Data = StreamGeometry.Parse("M12.75,2.25C11.5,2.25 10.5,3.25 10.5,4.5V5.25H3V21.75H21V5.25H13.5V4.5C13.5,3.25 12.5,2.25 11.25,2.25C11,2.25 10.75,2.25 10.5,2.25M11.25,3.75C11.75,3.75 12,4.1 12,4.5V5.25H10.5V4.5C10.5,4.1 10.75,3.75 11.25,3.75M4.5,6.75H10.5V11.25H4.5V6.75M13.5,6.75H19.5V11.25H13.5V6.75M4.5,12.75H10.5V20.25H4.5V12.75M13.5,12.75H19.5V20.25H13.5V12.75Z"),
                    Width = 40,
                    Height = 40,
                    Foreground = Brushes.Black
                }
            };
            AutomationProperties.SetAccessibilityView(iconContainer, AccessibilityView.Raw);
            content.Children.Add(blob1);
            content.Children.Add(blob2);
            content.Children.Add(card);
            content.Children.Add(halo);
            content.Children.Add(iconContainer);

            var dialog = new ContentDialog
            {
                Content = content,
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(32),
                IsLightDismissEnabled = true,
                CancelButton = ContentDialogButton.Close,
                Transition = new ScaleLiftPageTransition
                {
                    Duration = TimeSpan.FromMilliseconds(520),
                    ExitDuration = TimeSpan.FromMilliseconds(240),
                    EntranceScale = 0.84,
                    EntranceOffset = 48,
                    OvershootScale = 1.02,
                    ExitScale = 0.94,
                    ExitOffset = 28
                }
            };
            dialog.Classes.Add("chromeless");
            AutomationProperties.SetName(dialog, "Gift card ready");
            AutomationProperties.SetHelpText(dialog, "An exclusive fifty-dollar digital gift card.");
            claim.Click += (_, _) => dialog.Hide(ContentDialogResult.Primary);
            close.Click += (_, _) => dialog.Hide();

            var blobPulse = new Animation
            {
                Duration = TimeSpan.FromSeconds(4),
                IterationCount = new IterationCount(2),
                PlaybackDirection = PlaybackDirection.Alternate,
                Children =
                {
                    new KeyFrame { Cue = new Cue(0), Setters = { new Setter(Visual.OpacityProperty, 0.05) } },
                    new KeyFrame { Cue = new Cue(1), Setters = { new Setter(Visual.OpacityProperty, 0.15) } }
                }
            };
            var blobFloat = new Animation
            {
                Duration = TimeSpan.FromSeconds(5),
                IterationCount = new IterationCount(2),
                PlaybackDirection = PlaybackDirection.Alternate,
                Children =
                {
                    new KeyFrame { Cue = new Cue(0), Setters = { new Setter(TranslateTransform.XProperty, -20d), new Setter(TranslateTransform.YProperty, -20d) } },
                    new KeyFrame { Cue = new Cue(1), Setters = { new Setter(TranslateTransform.XProperty, 20d), new Setter(TranslateTransform.YProperty, 20d) } }
                }
            };
            var haloPulse = new Animation
            {
                Duration = TimeSpan.FromSeconds(2),
                IterationCount = new IterationCount(2),
                PlaybackDirection = PlaybackDirection.Alternate,
                Children =
                {
                    new KeyFrame { Cue = new Cue(0), Setters = { new Setter(Visual.OpacityProperty, 0.1) } },
                    new KeyFrame { Cue = new Cue(1), Setters = { new Setter(Visual.OpacityProperty, 0.3) } }
                }
            };
            var shimmerSweep = new Animation
            {
                Duration = TimeSpan.FromSeconds(4),
                Easing = new CubicEaseOut(),
                Children =
                {
                    new KeyFrame { Cue = new Cue(0), Setters = { new Setter(TranslateTransform.XProperty, 0d) } },
                    new KeyFrame { Cue = new Cue(0.5), Setters = { new Setter(TranslateTransform.XProperty, 1000d) } },
                    new KeyFrame { Cue = new Cue(1), Setters = { new Setter(TranslateTransform.XProperty, 1000d) } }
                }
            };
            var animationTasks = Array.Empty<Task>();
            try
            {
                // ShowAsync attaches the dialog before returning its task. Start the
                // decorative loops only after that point so detached visuals do not
                // consume animation ticks or enter at an arbitrary animation phase.
                var showTask = dialog.ShowAsync(owner);
                if (!highContrast)
                {
                    animationTasks = new[]
                    {
                        RunAnimationLoopAsync(blobPulse, blob1, animationCancellation.Token),
                        RunAnimationLoopAsync(blobFloat, blob2, animationCancellation.Token),
                        RunAnimationLoopAsync(haloPulse, halo, animationCancellation.Token),
                        RunAnimationLoopAsync(shimmerSweep, shimmer, animationCancellation.Token)
                    };
                }

                await showTask;
            }
            finally
            {
                animationCancellation.Cancel();
                try
                {
                    await Task.WhenAll(animationTasks);
                }
                catch (OperationCanceledException) when (animationCancellation.IsCancellationRequested)
                {
                }
            }
        }

        private static async Task RunAnimationLoopAsync(
            Animation animation,
            Animatable target,
            CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
                await animation.RunAsync(target, cancellationToken);
        }

        private static BottomSheet CreateSheet(string header, Control content) => new()
        {
            Header = header,
            Content = content,
            IsLightDismissEnabled = true
        };

        private static Border Avatar(string text, string color, double size, double fontSize = 14)
        {
            var avatar = new Border
            {
                Width = size,
                Height = size,
                CornerRadius = new Avalonia.CornerRadius(size / 2),
                Background = Brush(color),
                Child = new TextBlock { Text = text, Foreground = Brushes.White, FontSize = fontSize, FontWeight = FontWeight.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
            };
            AutomationProperties.SetAccessibilityView(avatar, AccessibilityView.Raw);
            return avatar;
        }

        private static Button AccentButton(string text, string color) => new()
        {
            Content = text,
            Background = Brush(color),
            Foreground = Brushes.White,
            CornerRadius = new Avalonia.CornerRadius(9),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            MinHeight = 40
        };

        private static Border AccountChoice(string initials, string title, string subtitle, bool selected = false)
        {
            var radio = new RadioButton { GroupName = "real-account", IsChecked = selected, VerticalAlignment = VerticalAlignment.Center };
            AutomationProperties.SetName(radio, $"{title}, {subtitle}");
            var copy = new StackPanel
            {
                Spacing = 2,
                Children =
                {
                    new TextBlock { Text = title, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap },
                    new TextBlock { Text = subtitle, FontSize = 12, Opacity = 0.65, TextWrapping = TextWrapping.Wrap }
                }
            };
            AutomationProperties.SetAccessibilityView(copy, AccessibilityView.Raw);
            var avatar = Avatar(initials, selected ? "#10B981" : "#64748B", 38);
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 12, Children = { avatar, copy, radio } };
            Grid.SetColumn(copy, 1);
            Grid.SetColumn(radio, 2);
            var border = new Border
            {
                Child = grid,
                Padding = new Avalonia.Thickness(12),
                CornerRadius = new Avalonia.CornerRadius(12),
                BorderThickness = new Avalonia.Thickness(1)
            };

            void UpdateSelectionVisual()
            {
                var isSelected = radio.IsChecked == true;
                avatar.Background = Brush(isSelected ? "#10B981" : "#64748B");
                border.BorderBrush = Brush(isSelected ? "#6010B981" : "#307F8C9A");
                border.Background = Brush(isSelected ? "#1010B981" : "#087F8C9A");
            }

            radio.IsCheckedChanged += (_, _) => UpdateSelectionVisual();
            UpdateSelectionVisual();
            border.PointerPressed += (_, e) =>
            {
                if (e.Source is not RadioButton)
                    radio.IsChecked = true;
            };
            return border;
        }

        private static StackPanel Labeled(string label, Control control)
        {
            var labelControl = new TextBlock { Text = label, FontWeight = FontWeight.SemiBold, FontSize = 12 };
            AutomationProperties.SetLabeledBy(control, labelControl);
            return new StackPanel { Spacing = 5, Children = { labelControl, control } };
        }

        private static SolidColorBrush Brush(string color) => new(Color.Parse(color));

    }
}
