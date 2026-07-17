using System;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace ControlCatalog.Pages
{
    /// <summary>Exercises ContentDialog layout under localization and resizing stress.</summary>
    public partial class ContentDialogAdaptivePage : UserControl
    {
        public ContentDialogAdaptivePage()
        {
            InitializeComponent();
        }

        private async void OnShowDialog(object? sender, RoutedEventArgs e)
        {
            if (TopLevel.GetTopLevel(this) is not { } owner)
                return;

            var longCopy = LongCopyCheck.IsChecked == true;
            var rightToLeft = RtlCheck.IsChecked == true;
            var fields = new StackPanel { Spacing = 10, MaxWidth = 520 };
            fields.Children.Add(new TextBlock
            {
                Text = rightToLeft
                    ? "غيّر حجم النافذة أثناء فتح مربع الحوار للتحقق من التفاف النص وترتيب الأزرار وحركة لوحة المفاتيح."
                    : longCopy
                        ? "Resize the application from a compact phone-like width to a wide desktop layout. Every sentence, field, and action must remain readable and reachable without horizontal clipping."
                        : "Resize the owner while this dialog is open.",
                TextWrapping = TextWrapping.Wrap
            });

            AddField(fields, rightToLeft ? "اسم مساحة العمل" : "Workspace name", "Aster workspace");
            AddField(fields, rightToLeft ? "البريد الإلكتروني" : "Contact email", "team@example.com");

            if (ManyFieldsCheck.IsChecked == true)
            {
                AddField(fields, rightToLeft ? "القسم" : "Department", "Product design");
                AddField(fields, rightToLeft ? "المنطقة الزمنية" : "Time zone", "Europe/Madrid");
                AddField(fields, rightToLeft ? "رمز المشروع" : "Project code", "AST-2048");
                fields.Children.Add(new CheckBox
                {
                    Content = rightToLeft
                        ? "إرسال ملخص أسبوعي إلى جميع المشاركين"
                        : "Send a weekly summary to every project participant",
                    IsChecked = true
                });
                fields.Children.Add(new TextBlock
                {
                    Text = longCopy
                        ? "This final paragraph intentionally contains enough localized-style copy to verify wrapping, scrolling, and focus visibility when the available height becomes constrained."
                        : "All content remains vertically scrollable.",
                    TextWrapping = TextWrapping.Wrap,
                    Opacity = 0.7
                });
            }

            var scroll = new ScrollViewer
            {
                Content = fields,
                MaxHeight = 380,
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled
            };
            var dialog = new ContentDialog
            {
                Title = rightToLeft
                    ? "مراجعة إعدادات مساحة العمل قبل المتابعة"
                    : longCopy
                        ? "Review the workspace configuration before continuing"
                        : "Adaptive dialog",
                Content = scroll,
                PrimaryButtonContent = rightToLeft
                    ? "حفظ الإعدادات والمتابعة"
                    : longCopy ? "Save configuration and continue" : "Save",
                SecondaryButtonContent = rightToLeft
                    ? "العودة إلى الإعدادات السابقة"
                    : longCopy ? "Return to previous settings" : "Back",
                CloseButtonContent = rightToLeft ? "إلغاء" : "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                CancelButton = ContentDialogButton.Close,
                FlowDirection = rightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
                FontSize = LargeTextCheck.IsChecked == true ? 20 : 14
            };

            ContentDialogClosedEventArgs? closed = null;
            dialog.Closed += (_, args) => closed = args;
            var result = await dialog.ShowAsync(owner);
            StatusText.Text =
                $"Dialog closed with {result}. Dismissal reason: {closed?.DismissReason}. " +
                $"Owner size: {owner.Bounds.Width:0} × {owner.Bounds.Height:0}.";
        }

        private static void AddField(StackPanel panel, string label, string value)
        {
            var labelControl = new TextBlock
            {
                Text = label,
                FontWeight = FontWeight.SemiBold,
                TextWrapping = TextWrapping.Wrap
            };
            var editor = new TextBox { Text = value };
            AutomationProperties.SetLabeledBy(editor, labelControl);
            panel.Children.Add(new StackPanel
            {
                Spacing = 5,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Children = { labelControl, editor }
            });
        }
    }
}
