using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Interactivity;

namespace ControlCatalog.Pages
{
    public partial class SystemNotificationPage : UserControl
    {
        private CancellationTokenSource? _lifetimeCancellation;

        public SystemNotificationPage()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private ISystemNotificationManager Manager =>
            Application.Current?.SystemNotifications ??
            throw new InvalidOperationException("The Avalonia application is not available.");

        private async void OnLoaded(object? sender, RoutedEventArgs e)
        {
            _lifetimeCancellation?.Dispose();
            _lifetimeCancellation = new CancellationTokenSource();
            await RefreshAsync(_lifetimeCancellation.Token);
        }

        private void OnUnloaded(object? sender, RoutedEventArgs e)
        {
            _lifetimeCancellation?.Cancel();
            _lifetimeCancellation?.Dispose();
            _lifetimeCancellation = null;
        }

        private async void OnRequestPermission(object? sender, RoutedEventArgs e) =>
            await RunAsync(async () =>
            {
                var status = await Manager.RequestPermissionAsync(GetCancellationToken());
                StatusText.Text = $"Permission result: {status}.";
            });

        private async void OnShow(object? sender, RoutedEventArgs e) =>
            await RunAsync(async () =>
            {
                var notification = new SystemNotification(
                    IdBox.Text ?? string.Empty,
                    MessageBox.Text ?? string.Empty)
                {
                    Title = string.IsNullOrWhiteSpace(TitleBox.Text) ? null : TitleBox.Text
                };
                await Manager.ShowAsync(notification, GetCancellationToken());
                StatusText.Text = $"System notification '{notification.Id}' submitted. Reuse the ID to replace it.";
            });

        private async void OnRemove(object? sender, RoutedEventArgs e) =>
            await RunAsync(async () =>
            {
                var id = IdBox.Text ?? string.Empty;
                await Manager.RemoveAsync(id, GetCancellationToken());
                StatusText.Text = $"Removal requested for '{id}'.";
            });

        private async void OnRemoveAll(object? sender, RoutedEventArgs e) =>
            await RunAsync(async () =>
            {
                await Manager.RemoveAllKnownAsync(GetCancellationToken());
                StatusText.Text = "All system notifications known to this application instance were removed.";
            });

        private async void OnRefresh(object? sender, RoutedEventArgs e) =>
            await RunAsync(() => Task.CompletedTask);

        private async Task RunAsync(Func<Task> operation)
        {
            try
            {
                await operation();
            }
            catch (OperationCanceledException) when (_lifetimeCancellation is null)
            {
                // The sample was navigated away from while a native operation was pending.
            }
            catch (Exception exception)
            {
                StatusText.Text = $"{exception.GetType().Name}: {exception.Message}";
            }
            finally
            {
                if (_lifetimeCancellation is not null)
                    await RefreshAsync(_lifetimeCancellation.Token);
            }
        }

        private async Task RefreshAsync(CancellationToken cancellationToken)
        {
            var manager = Manager;
            var isSupported = manager.IsSupported;
            SupportText.Text = isSupported ? "Native provider available" : "Unavailable in this host";
            RequestPermissionButton.IsEnabled = false;
            ShowButton.IsEnabled = false;
            RemoveButton.IsEnabled = isSupported;
            RemoveAllButton.IsEnabled = isSupported;
            if (!isSupported)
            {
                PermissionText.Text = "Not available";
                StatusText.Text = OperatingSystem.IsMacOS()
                    ? "macOS system notifications require a packaged .app. Run samples/ControlCatalog.Desktop/bundle.sh --run."
                    : "This platform or application host does not provide system notifications.";
                return;
            }

            try
            {
                var permission = await manager.GetPermissionStatusAsync(cancellationToken);
                PermissionText.Text = permission.ToString();
                RequestPermissionButton.IsEnabled = permission == SystemNotificationPermissionStatus.NotDetermined;
                ShowButton.IsEnabled = permission == SystemNotificationPermissionStatus.Granted;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                PermissionText.Text = $"Error: {exception.Message}";
                RequestPermissionButton.IsEnabled = false;
                ShowButton.IsEnabled = false;
            }
        }

        private CancellationToken GetCancellationToken() =>
            _lifetimeCancellation?.Token ?? CancellationToken.None;
    }
}
