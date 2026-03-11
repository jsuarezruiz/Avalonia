using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace ControlCatalog.Pages;

public partial class CareCompanionAppPage : UserControl
{
    static readonly Color Primary = Color.Parse("#137fec");
    static readonly Color PrimaryLight = Color.Parse("#e0f0ff");
    static readonly Color BgLight = Color.Parse("#f6f7f8");
    static readonly Color TextDark = Color.Parse("#111827");
    static readonly Color TextMuted = Color.Parse("#64748b");
    static readonly Color CardBg = Colors.White;
    static readonly Color SuccessGreen = Color.Parse("#10b981");

    ScrollViewer? _infoPanel;

    public CareCompanionAppPage()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        _infoPanel = this.FindControl<ScrollViewer>("InfoPanel");
        UpdateInfoVisibility();
        BuildShowcase();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoundsProperty)
        {
            UpdateInfoVisibility();
        }
    }

    void UpdateInfoVisibility()
    {
        if (_infoPanel != null)
        {
            _infoPanel.IsVisible = Bounds.Width >= 650;
        }
    }

    void BuildShowcase()
    {
        var carousel = this.FindControl<Carousel>("ShowcaseCarousel")!;

        carousel.Items.Add(BuildWelcomePage(carousel));
        carousel.Items.Add(BuildTrackPage(carousel));
        carousel.Items.Add(BuildGetStartedPage(carousel));
    }

    Border BuildWelcomePage(Carousel carousel)
    {
        var root = new Border { Background = new SolidColorBrush(CardBg) };

        var skipBtn = StyledButton("Skip", Brushes.Transparent, new SolidColorBrush(TextMuted),
            32, new CornerRadius(999));
        skipBtn.HorizontalAlignment = HorizontalAlignment.Right;
        skipBtn.Margin = new Thickness(0, 4, 8, 0);
        skipBtn.Click += (_, _) => carousel.SelectedIndex = 2;

        var illGrad = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
        };
        illGrad.GradientStops.Add(new GradientStop(Color.Parse("#dbeafe"), 0));
        illGrad.GradientStops.Add(new GradientStop(Color.Parse("#93c5fd"), 0.5));
        illGrad.GradientStops.Add(new GradientStop(Color.Parse("#3b82f6"), 1));

        var illPanel = new Panel { Background = illGrad };

        illPanel.Children.Add(new Border
        {
            Width = 160,
            Height = 160,
            CornerRadius = new CornerRadius(80),
            Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        });

        illPanel.Children.Add(new Border
        {
            Width = 80,
            Height = 80,
            CornerRadius = new CornerRadius(20),
            Background = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            BoxShadow = BoxShadows.Parse("0 8 24 0 #0000001a"),
            Child = SvgIcon(
                "M12 21.593c-5.63-5.539-11-10.297-11-14.402 0-3.791 3.068-5.191 5.281-5.191 1.312 0 4.151.501 5.719 4.457 1.59-3.968 4.464-4.447 5.726-4.447 2.54 0 5.274 1.621 5.274 5.181 0 4.069-5.136 8.625-11 14.402z",
                38, Color.Parse("#3b82f6")),
        });

        illPanel.Children.Add(new Border
        {
            Background = Brushes.White,
            CornerRadius = new CornerRadius(999),
            Padding = new Thickness(10, 6),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(24, 0, 0, 24),
            BoxShadow = BoxShadows.Parse("0 4 12 0 #0000001a"),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Children =
                {
                    new Border
                    {
                        Width = 8,
                        Height = 8,
                        CornerRadius = new CornerRadius(4),
                        Background = new SolidColorBrush(SuccessGreen),
                        VerticalAlignment = VerticalAlignment.Center,
                    },
                    Txt("Your health, simplified", 10, FontWeight.SemiBold, TextDark),
                },
            },
        });

        illPanel.Children.Add(new Border
        {
            Width = 44,
            Height = 44,
            CornerRadius = new CornerRadius(22),
            Background = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 20, 28, 0),
            Child = SvgIcon("M19 13h-6v6h-2v-6H5v-2h6V5h2v6h6v2z", 20, Colors.White),
        });

        var imgCard = new Border
        {
            Height = 210,
            CornerRadius = new CornerRadius(20),
            ClipToBounds = true,
            Margin = new Thickness(20, 6, 20, 0),
            Child = illPanel,
        };

        var textArea = new StackPanel { Margin = new Thickness(28, 20, 28, 0), Spacing = 10 };
        var titleStack = new StackPanel { Spacing = 2, HorizontalAlignment = HorizontalAlignment.Center };
        titleStack.Children.Add(Txt("Welcome to Your", 26, FontWeight.Bold, TextDark, align: TextAlignment.Center));
        titleStack.Children.Add(Txt("Care Companion", 28, FontWeight.ExtraBold, Primary, align: TextAlignment.Center));
        textArea.Children.Add(titleStack);
        textArea.Children.Add(Txt(
            "We are here to support you through every step of your treatment journey. Track symptoms, manage appointments, and stay connected.",
            13, FontWeight.Normal, TextMuted, align: TextAlignment.Center, wrap: TextWrapping.Wrap));

        var nextRow = new StackPanel
        {
            Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Spacing = 6,
        };
        nextRow.Children.Add(Txt("Next", 15, FontWeight.SemiBold, Colors.White));
        nextRow.Children.Add(SvgIcon("M12 4l-1.41 1.41L16.17 11H4v2h12.17l-5.58 5.59L12 20l8-8z", 12, Colors.White));

        var nextBtn = StyledButton(nextRow, new SolidColorBrush(Primary), Brushes.White, 52, new CornerRadius(999));
        nextBtn.HorizontalAlignment = HorizontalAlignment.Stretch;
        nextBtn.Click += (_, _) => carousel.SelectedIndex = 1;

        var nextBtnWrap = ShadowWrap(nextBtn, Primary);
        nextBtnWrap.HorizontalAlignment = HorizontalAlignment.Stretch;

        var bottomArea = new StackPanel { Margin = new Thickness(24, 16, 24, 20), Spacing = 20 };
        bottomArea.Children.Add(nextBtnWrap);

        var middleStack = new StackPanel { Spacing = 0 };
        middleStack.Children.Add(imgCard);
        middleStack.Children.Add(textArea);

        var middleScroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden, Content = middleStack,
        };

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        Grid.SetRow(skipBtn, 0);
        Grid.SetRow(middleScroll, 1);
        Grid.SetRow(bottomArea, 2);
        grid.Children.Add(skipBtn);
        grid.Children.Add(middleScroll);
        grid.Children.Add(bottomArea);

        root.Child = grid;
        return root;
    }

    Border BuildTrackPage(Carousel carousel)
    {
        var root = new Border { Background = new SolidColorBrush(CardBg) };

        var skipBtn = StyledButton("Skip", Brushes.Transparent, new SolidColorBrush(TextMuted),
            32, new CornerRadius(999));
        skipBtn.HorizontalAlignment = HorizontalAlignment.Right;
        skipBtn.Margin = new Thickness(0, 4, 8, 0);
        skipBtn.Click += (_, _) => carousel.SelectedIndex = 2;

        var illGrad = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
        };
        illGrad.GradientStops.Add(new GradientStop(Color.Parse("#0ea5e9"), 0));
        illGrad.GradientStops.Add(new GradientStop(Color.Parse("#6366f1"), 1));

        var illPanel = new Panel { Background = illGrad };

        int[] barH = { 48, 72, 40, 96, 64, 80, 56 };
        string[] barD = { "M", "T", "W", "T", "F", "S", "S" };
        var chartInner = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Spacing = 6,
            Margin = new Thickness(0, 0, 0, 10),
        };
        for (int ci = 0; ci < barH.Length; ci++)
        {
            var barCol = new StackPanel { Spacing = 3, VerticalAlignment = VerticalAlignment.Bottom };
            barCol.Children.Add(new Border
            {
                Width = 20,
                Height = barH[ci],
                CornerRadius = new CornerRadius(5, 5, 0, 0),
                Background = new SolidColorBrush(ci == 3 ? Colors.White : Color.FromArgb(160, 255, 255, 255)),
                VerticalAlignment = VerticalAlignment.Bottom,
            });
            barCol.Children.Add(Txt(barD[ci], 9, FontWeight.Medium, Colors.White, 0.7, align: TextAlignment.Center));
            chartInner.Children.Add(barCol);
        }

        illPanel.Children.Add(new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(14, 14, 14, 6),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = chartInner,
        });

        illPanel.Children.Add(new Border
        {
            Background = Brushes.White,
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(10, 7),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(22, 20, 0, 0),
            BoxShadow = BoxShadows.Parse("0 4 12 0 #0000001a"),
            Child = new StackPanel
            {
                Spacing = 1,
                Children =
                {
                    Txt("Weekly Score", 9, FontWeight.SemiBold, TextMuted),
                    Txt("\u2191 18%", 13, FontWeight.Bold, Color.Parse("#0ea5e9")),
                },
            },
        });

        illPanel.Children.Add(new Border
        {
            Width = 36,
            Height = 36,
            CornerRadius = new CornerRadius(18),
            Background = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 22, 24, 0),
            Child = SvgIcon(
                "M16 6l2.29 2.29-4.88 4.88-4-4L2 16.59 3.41 18l6-6 4 4 6.3-6.29L22 12V6z",
                16, Colors.White),
        });

        var imgCard = new Border
        {
            Height = 210,
            CornerRadius = new CornerRadius(20),
            ClipToBounds = true,
            Margin = new Thickness(20, 6, 20, 0),
            Child = illPanel,
        };

        var textArea = new StackPanel { Margin = new Thickness(28, 10, 28, 0), Spacing = 10 };
        textArea.Children.Add(Txt("Track and Understand", 24, FontWeight.Bold, TextDark,
            align: TextAlignment.Center, wrap: TextWrapping.Wrap));
        textArea.Children.Add(Txt(
            "Easily log your symptoms and side effects to share with your medical team for better care.",
            13, FontWeight.Normal, TextMuted, align: TextAlignment.Center, wrap: TextWrapping.Wrap));

        var backRow = new StackPanel
        {
            Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Spacing = 6,
        };
        backRow.Children.Add(SvgIcon("M20 11H7.83l5.59-5.59L12 4l-8 8 8 8 1.41-1.41L7.83 13H20v-2z", 12, TextDark));
        backRow.Children.Add(Txt("Back", 15, FontWeight.SemiBold, TextDark));

        var nextRow = new StackPanel
        {
            Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Spacing = 6,
        };
        nextRow.Children.Add(Txt("Next", 15, FontWeight.SemiBold, Colors.White));
        nextRow.Children.Add(SvgIcon("M12 4l-1.41 1.41L16.17 11H4v2h12.17l-5.58 5.59L12 20l8-8z", 12, Colors.White));

        var backBtn = StyledButton(backRow, new SolidColorBrush(Color.Parse("#f3f4f6")),
            new SolidColorBrush(TextDark), 52, new CornerRadius(999));
        backBtn.HorizontalAlignment = HorizontalAlignment.Stretch;
        backBtn.Click += (_, _) => carousel.SelectedIndex = 0;

        var nextBtn = StyledButton(nextRow, new SolidColorBrush(Primary), Brushes.White, 52, new CornerRadius(999));
        nextBtn.HorizontalAlignment = HorizontalAlignment.Stretch;
        nextBtn.Click += (_, _) => carousel.SelectedIndex = 2;

        var nextBtnWrap = ShadowWrap(nextBtn, Primary);
        nextBtnWrap.HorizontalAlignment = HorizontalAlignment.Stretch;

        var navGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,16,*") };
        Grid.SetColumn(backBtn, 0);
        Grid.SetColumn(nextBtnWrap, 2);
        navGrid.Children.Add(backBtn);
        navGrid.Children.Add(nextBtnWrap);

        var bottomArea = new StackPanel { Margin = new Thickness(24, 16, 24, 20), Spacing = 20 };
        bottomArea.Children.Add(navGrid);

        var middleStack = new StackPanel { Spacing = 0 };
        middleStack.Children.Add(imgCard);
        middleStack.Children.Add(textArea);

        var middleScroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden, Content = middleStack,
        };

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        Grid.SetRow(skipBtn, 0);
        Grid.SetRow(middleScroll, 1);
        Grid.SetRow(bottomArea, 2);
        grid.Children.Add(skipBtn);
        grid.Children.Add(middleScroll);
        grid.Children.Add(bottomArea);

        root.Child = grid;
        return root;
    }

    Border BuildGetStartedPage(Carousel carousel)
    {
        var root = new Border { Background = new SolidColorBrush(CardBg) };

        var illPanel = new Panel { Background = new SolidColorBrush(Color.Parse("#eef4ff")) };

        illPanel.Children.Add(new Border
        {
            Width = 72,
            Height = 72,
            CornerRadius = new CornerRadius(36),
            Background = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 32),
            BoxShadow = BoxShadows.Parse("0 4 16 0 #0000001a"),
            Child = SvgIcon(
                "M19 3H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zm-5 14H7v-2h7v2zm3-4H7v-2h10v2zm0-4H7V7h10v2z",
                32, Primary),
        });

        illPanel.Children.Add(new Border
        {
            Width = 36,
            Height = 36,
            CornerRadius = new CornerRadius(18),
            Background = new SolidColorBrush(SuccessGreen),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 22, 44, 0),
            Child = SvgIcon("M20 2H4c-1.1 0-2 .9-2 2v18l4-4h14c1.1 0 2-.9 2-2V4c0-1.1-.9-2-2-2z", 17, Colors.White),
        });

        illPanel.Children.Add(new Border
        {
            Width = 30,
            Height = 30,
            CornerRadius = new CornerRadius(15),
            Background = new SolidColorBrush(Color.Parse("#8b5cf6")),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 20, 0),
            Child = SvgIcon(
                "M16 11c1.66 0 2.99-1.34 2.99-3S17.66 5 16 5c-1.66 0-3 1.34-3 3s1.34 3 3 3zm-8 0c1.66 0 2.99-1.34 2.99-3S9.66 5 8 5C6.34 5 5 6.34 5 8s1.34 3 3 3zm0 2c-2.33 0-7 1.17-7 3.5V19h14v-2.5c0-2.33-4.67-3.5-7-3.5zm8 0c-.29 0-.62.02-.97.05 1.16.84 1.97 1.97 1.97 3.45V19h6v-2.5c0-2.33-4.67-3.5-7-3.5z",
                15, Colors.White),
        });

        var avatarGrad = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
        };
        avatarGrad.GradientStops.Add(new GradientStop(Color.Parse("#93c5fd"), 0));
        avatarGrad.GradientStops.Add(new GradientStop(Primary, 1));
        illPanel.Children.Add(new Border
        {
            Width = 40,
            Height = 40,
            CornerRadius = new CornerRadius(20),
            Background = avatarGrad,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(32, -20, 0, 0),
            Child = SvgIcon(
                "M12 2C9.243 2 7 4.243 7 7s2.243 5 5 5 5-2.243 5-5-2.243-5-5-5zM12 14c-5.523 0-10 3.582-10 8a1 1 0 001 1h18a1 1 0 001-1c0-4.418-4.477-8-10-8z",
                22, Colors.White),
        });

        var illCard = new Border
        {
            Height = 210,
            CornerRadius = new CornerRadius(20),
            ClipToBounds = true,
            Margin = new Thickness(20, 16, 20, 0),
            Child = illPanel,
        };

        var textArea = new StackPanel { Margin = new Thickness(28, 20, 28, 0), Spacing = 10 };
        textArea.Children.Add(Txt("Stay Informed and Connected", 24, FontWeight.Bold, TextDark,
            align: TextAlignment.Center, wrap: TextWrapping.Wrap));
        textArea.Children.Add(Txt(
            "Access expert resources and manage your appointments all in one place.",
            13, FontWeight.Normal, TextMuted, align: TextAlignment.Center, wrap: TextWrapping.Wrap));

        var gsRow = new StackPanel
        {
            Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Spacing = 6,
        };
        gsRow.Children.Add(Txt("Get Started", 15, FontWeight.SemiBold, Colors.White));
        gsRow.Children.Add(SvgIcon("M12 4l-1.41 1.41L16.17 11H4v2h12.17l-5.58 5.59L12 20l8-8z", 12, Colors.White));

        var getStartedBtn = StyledButton(gsRow, new SolidColorBrush(Primary), Brushes.White, 52, new CornerRadius(999));
        getStartedBtn.HorizontalAlignment = HorizontalAlignment.Stretch;

        var getStartedWrap = ShadowWrap(getStartedBtn, Primary);
        getStartedWrap.HorizontalAlignment = HorizontalAlignment.Stretch;

        var loginRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        loginRow.Children.Add(new TextBlock
        {
            Text = "Already have an account? ",
            FontSize = 13,
            Foreground = new SolidColorBrush(TextMuted),
            VerticalAlignment = VerticalAlignment.Center,
        });
        loginRow.Children.Add(new TextBlock
        {
            Text = "Log In",
            FontSize = 13,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Primary),
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = new Cursor(StandardCursorType.Hand),
        });

        var bottomArea = new StackPanel { Margin = new Thickness(24, 16, 24, 20), Spacing = 16 };
        bottomArea.Children.Add(getStartedWrap);
        bottomArea.Children.Add(loginRow);

        var middleStack = new StackPanel { Spacing = 0 };
        middleStack.Children.Add(illCard);
        middleStack.Children.Add(textArea);

        var middleScroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden, Content = middleStack,
        };

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        Grid.SetRow(middleScroll, 0);
        Grid.SetRow(bottomArea, 1);
        grid.Children.Add(middleScroll);
        grid.Children.Add(bottomArea);

        root.Child = grid;
        return root;
    }

    static TextBlock Txt(string text, double size, FontWeight weight, Color color,
        double opacity = 1, TextAlignment align = TextAlignment.Left,
        TextWrapping wrap = TextWrapping.NoWrap)
        => new TextBlock
        {
            Text = text,
            FontSize = size,
            FontWeight = weight,
            Foreground = new SolidColorBrush(color),
            Opacity = opacity,
            TextAlignment = align,
            TextWrapping = wrap,
        };

    static Button StyledButton(object content, IBrush bg, IBrush fg, double height,
        CornerRadius radius, Thickness margin = default,
        IBrush? border = null, Thickness borderThick = default)
    {
        var btn = new Button
        {
            Content = content,
            Background = bg,
            Foreground = fg,
            Height = height,
            CornerRadius = radius,
            Margin = margin,
            Padding = new Thickness(16, 0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            BorderBrush = border,
            BorderThickness = borderThick,
        };

        var over = new Style(x => x.OfType<Button>().Class(":pointerover").Descendant().OfType<ContentPresenter>());
        over.Setters.Add(new Setter(ContentPresenter.BackgroundProperty, bg));
        over.Setters.Add(new Setter(ContentPresenter.ForegroundProperty, fg));
        btn.Styles.Add(over);

        var press = new Style(x => x.OfType<Button>().Class(":pressed").Descendant().OfType<ContentPresenter>());
        press.Setters.Add(new Setter(ContentPresenter.BackgroundProperty, bg));
        press.Setters.Add(new Setter(ContentPresenter.ForegroundProperty, fg));
        btn.Styles.Add(press);

        return btn;
    }

    static PathIcon SvgIcon(string data, double size, Color color)
        => new PathIcon
        {
            Data = Geometry.Parse(data),
            Width = size,
            Height = size,
            Foreground = new SolidColorBrush(color),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

    static Border ShadowWrap(Control ctrl, Color shadowColor)
        => new Border
        {
            CornerRadius = new CornerRadius(999),
            BoxShadow = new BoxShadows(new BoxShadow
            {
                OffsetX = 0,
                OffsetY = 4,
                Blur = 16,
                Color = Color.FromArgb(60, shadowColor.R, shadowColor.G, shadowColor.B),
            }),
            Child = ctrl,
        };
}
