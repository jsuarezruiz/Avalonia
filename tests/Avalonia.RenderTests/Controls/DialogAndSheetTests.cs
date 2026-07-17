using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Themes.Simple;
using Xunit;

#if AVALONIA_SKIA
namespace Avalonia.Skia.RenderTests
#else
namespace Avalonia.Direct2D1.RenderTests.Controls
#endif
{
    public class DialogAndSheetRenderTests : TestBase
    {
        public DialogAndSheetRenderTests()
            : base(@"Controls\DialogAndSheet")
        {
        }

        private static Style TextFontStyle(double fontSize = 14) => new Style(x => x.OfType<TextBlock>())
        {
            Setters =
            {
                new Setter(TextBlock.FontFamilyProperty, TestFontFamily),
                new Setter(TextBlock.FontSizeProperty, fontSize)
            }
        };

        private static Style ButtonFontStyle(double fontSize = 14) => new Style(x => x.OfType<Button>())
        {
            Setters =
            {
                new Setter(Button.FontFamilyProperty, TestFontFamily),
                new Setter(Button.FontSizeProperty, fontSize)
            }
        };

        [Fact]
        public async Task ContentDialog_Simple_Light_Regular()
        {
            var target = CreateDialogTarget(520, 360);
            target.Styles.Add(new SimpleTheme());
            target.Styles.Add(TextFontStyle());
            target.Styles.Add(ButtonFontStyle());

            await RenderToFile(target);
            CompareImages(skipImmediate: true);
        }

        [Fact]
        public async Task ContentDialog_Fluent_Dark_Narrow_Rtl()
        {
            var target = CreateDialogTarget(340, 430);
            target.FlowDirection = FlowDirection.RightToLeft;
            target.RequestedThemeVariant = ThemeVariant.Dark;
            target.Styles.Add(new FluentTheme());
            target.Styles.Add(TextFontStyle());
            target.Styles.Add(ButtonFontStyle());

            await RenderToFile(target);
            CompareImages(skipImmediate: true);
        }

        [Fact]
        public async Task BottomSheet_Simple_Light_Narrow_LargeText()
        {
            var target = CreateSheetTarget(340, 420);
            target.Styles.Add(new SimpleTheme());
            target.Styles.Add(TextFontStyle(20));
            target.Styles.Add(ButtonFontStyle(20));

            await RenderToFile(target);
            CompareImages(skipImmediate: true);
        }

        [Fact]
        public async Task BottomSheet_Fluent_Dark_HighDpi()
        {
            var target = CreateHighDpiSheetTarget();
            target.RequestedThemeVariant = ThemeVariant.Dark;
            target.Styles.Add(new FluentTheme());
            target.Styles.Add(TextFontStyle());
            target.Styles.Add(ButtonFontStyle());

            await RenderToFile(target, dpi: 192);
            CompareImages(skipImmediate: true);
        }

        private static ThemeVariantScope CreateDialogTarget(double width, double height) => new ThemeVariantScope
        {
            Width = width,
            Height = height,
            Child = new Border
            {
                Background = Brushes.White,
                Child = new ContentDialog
                {
                    Title = "Delete project?",
                    Content = new TextBlock
                    {
                        Text = "The project and its local files will be removed. This action cannot be undone.",
                        TextWrapping = TextWrapping.Wrap
                    },
                    CloseButtonContent = "Cancel",
                    PrimaryButtonContent = "Delete",
                    DefaultButton = ContentDialogButton.Primary,
                    Transition = null
                }
            }
        };

        private static ThemeVariantScope CreateSheetTarget(double width, double height) => new ThemeVariantScope
        {
            Width = width,
            Height = height,
            Child = new Border
            {
                Background = Brushes.White,
                Child = new BottomSheet
                {
                    Height = 248,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Header = "Delivery options",
                    Content = new StackPanel
                    {
                        Spacing = 12,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = "Choose when your order should arrive.",
                                TextWrapping = TextWrapping.Wrap
                            },
                            new Button
                            {
                                HorizontalAlignment = HorizontalAlignment.Stretch,
                                HorizontalContentAlignment = HorizontalAlignment.Center,
                                Content = "Tomorrow, 9:00–12:00"
                            }
                        }
                    },
                    Transition = null
                }
            }
        };

        private static ThemeVariantScope CreateHighDpiSheetTarget() => new ThemeVariantScope
        {
            Width = 520,
            Height = 400,
            Child = new Border
            {
                Background = Brushes.White,
                Child = new BottomSheet
                {
                    Width = 260,
                    Height = 180,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    Header = "Delivery options",
                    Content = new TextBlock
                    {
                        Text = "Choose when your order should arrive.",
                        TextWrapping = TextWrapping.Wrap
                    },
                    Transition = null
                }
            }
        };
    }
}
