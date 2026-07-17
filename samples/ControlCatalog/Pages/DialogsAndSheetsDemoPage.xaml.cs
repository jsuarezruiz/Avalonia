using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ControlCatalog.Pages
{
    public partial class DialogsAndSheetsDemoPage : ContentPage
    {
        private readonly ProductFlowShowcaseLauncher _showcaseLauncher = new();

        private static readonly (string Group, string Title, string Description, Func<UserControl> Factory)[] Demos =
        {
            ("Dialogs", "ContentDialog",
                "Test arbitrary content, actions, light dismiss, and built-in or custom transitions.",
                () => new ContentDialogFirstLookPage()),
            ("Dialogs", "Focus & Keyboard",
                "Verify initial focus, cyclic navigation, default and cancel actions, announcements, and focus restoration.",
                () => new ContentDialogFocusPage()),
            ("Dialogs", "Commands & Binding",
                "Drive actions, parameters, and CanExecute from a bound view model.",
                () => new ContentDialogCommandsPage()),
            ("Dialogs", "Async Deferrals",
                "Use a button-click deferral for asynchronous validation while keeping the dialog and its modal owner state consistent.",
                () => new ContentDialogDeferralsPage()),
            ("Dialogs", "Modal Coordination",
                "Test caller cancellation, same-owner rejection, explicit sequencing, reuse, and independent TopLevel slots.",
                () => new ModalCoordinationPage()),

            ("Platform", "Window Dialogs",
                "Compare modeless, modal, owned, decorated, and taskbar-hidden Window behavior.",
                () => new WindowDialogsPage()),
            ("Platform", "File & Folder Pickers",
                "Test storage filters, suggested types, bookmarks, start locations, and launchers.",
                () => new StoragePickersPage()),

            ("Native", "Message Dialog",
                "Use the TopLevel-scoped native message contract with stable action IDs and no managed fallback.",
                () => new MessageDialogPage()),

            ("Edge-attached", "Bottom Sheet",
                "Present arbitrary content at detents, return a value and dismissal reason, and customize its vertical IPageTransition.",
                () => new BottomSheetDemoPage()),

            ("Native", "System Notifications",
                "Request permission, show or replace by stable ID, and remove native operating-system notifications.",
                () => new SystemNotificationPage()),

            ("Testing", "Adaptive Stress",
                "Resize long, large-text, RTL, and scrollable dialog content without clipping.",
                () => new ContentDialogAdaptivePage()),
        };

        private (string Group, string Title, string Description, Func<TopLevel, Task> Action)[] Showcases =>
        [
            ("Showcases", "System Notification",
                "Send an export-complete notification directly through the operating system.",
                owner => _showcaseLauncher.ShowAsync(ProductFlow.SystemNotification, owner)),
            ("Showcases", "Native Confirmation",
                "Confirm a destructive action with the platform's native message dialog.",
                owner => _showcaseLauncher.ShowAsync(ProductFlow.NativeConfirmation, owner)),
            ("Showcases", "Success Sheet",
                "Confirm completed workspace setup in an illustrated bottom sheet.",
                owner => _showcaseLauncher.ShowAsync(ProductFlow.SuccessSheet, owner)),
            ("Showcases", "Account Switcher",
                "Choose an account in an interactive selection dialog.",
                owner => _showcaseLauncher.ShowAsync(ProductFlow.AccountSwitcher, owner)),
            ("Showcases", "Onboarding",
                "Run a three-step workspace setup in one stateful dialog.",
                owner => _showcaseLauncher.ShowAsync(ProductFlow.Onboarding, owner)),
            ("Showcases", "Gift Card",
                "Open a custom-styled reward dialog with an authored scale-and-lift transition.",
                owner => _showcaseLauncher.ShowAsync(ProductFlow.GiftCard, owner)),
        ];

        public DialogsAndSheetsDemoPage()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object? sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;
            await SampleNav.PushAsync(
                NavigationDemoHelper.CreateGalleryHomePage(SampleNav, Demos, Showcases),
                null);
        }
    }
}
