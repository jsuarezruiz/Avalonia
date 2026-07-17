using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Animation;
using Avalonia.Controls.Platform;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.UnitTests;
using Xunit;

namespace Avalonia.Controls.UnitTests;

public class ContentDialogTests : ScopedTestBase
{
    [Fact]
    public async Task Show_And_Hide_Use_Owner_Overlay_And_Raise_Lifecycle_Events()
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var window = CreateWindow();
            var dialog = new ContentDialog
            {
                Title = "Title",
                CloseButtonContent = "Close",
                Transition = null
            };
            var opening = 0;
            var opened = 0;
            var closing = 0;
            var closed = 0;
            dialog.Opening += (_, _) => opening++;
            dialog.Opened += (_, _) => opened++;
            dialog.Closing += (_, _) => closing++;
            dialog.Closed += (_, _) => closed++;

            var resultTask = dialog.ShowAsync(window, TestContext.Current.CancellationToken);
            var overlay = OverlayLayer.GetOverlayLayer(window)!;

            Assert.True(dialog.IsOpen);
            Assert.Contains(dialog, overlay.Children);
            Assert.Equal(1, opening);
            Assert.Equal(1, opened);

            dialog.Hide();
            RunJobs();

            Assert.Equal(ContentDialogResult.None, await resultTask);
            Assert.False(dialog.IsOpen);
            Assert.DoesNotContain(dialog, overlay.Children);
            Assert.Equal(1, closing);
            Assert.Equal(1, closed);
        }
    }

    [Fact]
    public void Show_Rejects_A_Second_Modal_On_The_Same_Owner()
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var window = CreateWindow();
            var first = new ContentDialog { Transition = null };
            var second = new ContentDialog { Transition = null };

            _ = first.ShowAsync(window, TestContext.Current.CancellationToken);

            Assert.Throws<InvalidOperationException>(() =>
            {
                _ = second.ShowAsync(window, TestContext.Current.CancellationToken);
            });

            first.Hide();
            RunJobs();
        }
    }

    [Fact]
    public async Task Different_Owners_Have_Independent_Modal_Slots()
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var firstWindow = CreateWindow();
            var secondWindow = CreateWindow();
            var first = new ContentDialog { Transition = null };
            var second = new ContentDialog { Transition = null };

            var firstTask = first.ShowAsync(firstWindow, TestContext.Current.CancellationToken);
            var secondTask = second.ShowAsync(secondWindow, TestContext.Current.CancellationToken);

            Assert.True(first.IsOpen);
            Assert.True(second.IsOpen);

            first.Hide();
            second.Hide();
            RunJobs();
            await Task.WhenAll(firstTask, secondTask);
        }
    }

    [Fact]
    public async Task Modal_Slot_Is_Held_Through_The_Closing_Transition()
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var window = CreateWindow();
            var first = new ContentDialog { Transition = new ClosingGateTransition() };
            var second = new ContentDialog { Transition = null };
            var firstTask = first.ShowAsync(window, TestContext.Current.CancellationToken);
            RunJobs();

            first.Hide();
            RunJobs();

            Assert.Throws<InvalidOperationException>(() =>
            {
                _ = second.ShowAsync(window, TestContext.Current.CancellationToken);
            });

            ((ClosingGateTransition)first.Transition!).ClosingCompleted.SetResult(null);
            RunJobs();
            await firstTask;
        }
    }

    [Fact]
    public async Task Native_Message_Dialog_Cannot_Open_Over_A_Bottom_Sheet()
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var nativeProvider = new ControllableMessageDialogProvider();
            AvaloniaLocator.CurrentMutable.Bind<INativeMessageDialogProviderFactory>()
                .ToConstant(new TestMessageDialogProviderFactory(nativeProvider));
            var window = CreateWindow();
            var sheet = new BottomSheet { Transition = null };
            var sheetTask = sheet.ShowAsync(window, TestContext.Current.CancellationToken);
            var options = new MessageDialogOptions(
                "Continue?",
                new MessageDialogAction("close", "Close") { IsCancel = true });

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                window.MessageDialogs.ShowAsync(options, TestContext.Current.CancellationToken));

            sheet.Hide();
            RunJobs();
            await sheetTask;
        }
    }

    [Fact]
    public async Task External_Message_Provider_Uses_The_Same_Modal_Slot()
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var window = CreateWindow();
            var inner = new ControllableMessageDialogProvider();
            var provider = new CoordinatedMessageDialogProvider(window, inner);
            var options = new MessageDialogOptions(
                "Continue?",
                new MessageDialogAction("continue", "Continue"));

            var firstTask = provider.ShowAsync(options, TestContext.Current.CancellationToken);
            Assert.NotNull(ManagedModalCoordinator.GetActiveModal(window));

            var dialog = new ContentDialog { Transition = null };
            Assert.Throws<InvalidOperationException>(() =>
            {
                _ = dialog.ShowAsync(window, TestContext.Current.CancellationToken);
            });
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                provider.ShowAsync(options, TestContext.Current.CancellationToken));

            inner.Complete("continue");
            RunJobs();
            Assert.Equal("continue", (await firstTask).ActionId);
            Assert.Null(ManagedModalCoordinator.GetActiveModal(window));
        }
    }

    [Fact]
    public async Task External_Message_Provider_Cannot_Return_An_Unknown_Action()
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var window = CreateWindow();
            var inner = new ControllableMessageDialogProvider();
            var provider = new CoordinatedMessageDialogProvider(window, inner);
            var options = new MessageDialogOptions(
                "Continue?",
                new MessageDialogAction("continue", "Continue"));

            var task = provider.ShowAsync(options, TestContext.Current.CancellationToken);
            inner.Complete("unknown");
            RunJobs();

            await Assert.ThrowsAsync<InvalidOperationException>(() => task);
            Assert.Null(ManagedModalCoordinator.GetActiveModal(window));
        }
    }

    [Fact]
    public async Task PreCanceled_External_Message_Does_Not_Invoke_Provider_Or_Take_The_Modal_Slot()
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var window = CreateWindow();
            var inner = new ControllableMessageDialogProvider();
            var provider = new CoordinatedMessageDialogProvider(window, inner);
            var options = new MessageDialogOptions(
                "Continue?",
                new MessageDialogAction("continue", "Continue"));
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            var task = provider.ShowAsync(options, cancellation.Token);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
            Assert.Equal(0, inner.CallCount);
            Assert.Null(ManagedModalCoordinator.GetActiveModal(window));
        }
    }

    [Fact]
    public async Task Owner_Close_Returns_OwnerClosed_For_An_External_Message_Provider()
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var window = CreateWindow();
            var inner = new CancellableMessageDialogProvider();
            var provider = new CoordinatedMessageDialogProvider(window, inner);
            var options = new MessageDialogOptions(
                "Continue?",
                new MessageDialogAction("continue", "Continue"));

            var task = provider.ShowAsync(options, TestContext.Current.CancellationToken);
            window.Close();
            RunJobs();

            Assert.Equal(MessageDialogDismissReason.OwnerClosed, (await task).DismissReason);
            Assert.True(inner.CancellationToken.IsCancellationRequested);
            Assert.Null(ManagedModalCoordinator.GetActiveModal(window));
        }
    }

    [Fact]
    public async Task Owner_Close_Returns_OwnerClosed_For_A_Native_Message_Dialog()
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var nativeProvider = new CancellableMessageDialogProvider();
            AvaloniaLocator.CurrentMutable.Bind<INativeMessageDialogProviderFactory>()
                .ToConstant(new TestMessageDialogProviderFactory(nativeProvider));
            var window = CreateWindow();
            var options = new MessageDialogOptions(
                "Continue?",
                new MessageDialogAction("continue", "Continue"));

            var task = window.MessageDialogs.ShowAsync(options, TestContext.Current.CancellationToken);
            window.Close();

            Assert.Equal(MessageDialogDismissReason.OwnerClosed, (await task).DismissReason);
        }
    }

    [Fact]
    public async Task Primary_Button_Executes_Command_And_Returns_Primary()
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var window = CreateWindow();
            var executed = 0;
            var dialog = new ContentDialog
            {
                PrimaryButtonContent = "Save",
                PrimaryButtonCommand = new TestCommand(() => executed++),
                Transition = null
            };

            var resultTask = dialog.ShowAsync(window, TestContext.Current.CancellationToken);
            var button = dialog.GetTemplateDescendants()
                .OfType<Button>()
                .Single(x => x.Name == "PART_PrimaryButton");

            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            RunJobs();

            Assert.Equal(ContentDialogResult.Primary, await resultTask);
            Assert.Equal(1, executed);
        }
    }

    [Fact]
    public async Task Closing_Can_Be_Canceled_And_Retried()
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var window = CreateWindow();
            var dialog = new ContentDialog { Transition = null };
            var cancel = true;
            dialog.Closing += (_, e) => e.Cancel = cancel;

            var resultTask = dialog.ShowAsync(window, TestContext.Current.CancellationToken);
            dialog.Hide(ContentDialogResult.Close);
            RunJobs();

            Assert.True(dialog.IsOpen);
            Assert.False(resultTask.IsCompleted);

            cancel = false;
            dialog.Hide(ContentDialogResult.Close);
            RunJobs();

            Assert.Equal(ContentDialogResult.Close, await resultTask);
        }
    }

    [Fact]
    public async Task Closing_Deferral_Delays_Close()
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var window = CreateWindow();
            var dialog = new ContentDialog { Transition = null };
            DialogDeferral? deferral = null;
            dialog.Closing += (_, e) => deferral = e.GetDeferral();

            var resultTask = dialog.ShowAsync(window, TestContext.Current.CancellationToken);
            dialog.Hide();
            RunJobs();

            Assert.True(dialog.IsOpen);
            Assert.False(resultTask.IsCompleted);

            deferral!.Complete();
            RunJobs();

            Assert.Equal(ContentDialogResult.None, await resultTask);
        }
    }

    [Fact]
    public async Task Cancellation_Force_Closes_And_Releases_The_Owner()
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var window = CreateWindow();
            var cancellation = new CancellationTokenSource();
            var first = new ContentDialog { Transition = null };
            ContentDialogClosedEventArgs? closed = null;
            first.Closed += (_, e) => closed = e;
            var firstTask = first.ShowAsync(window, cancellation.Token);

            cancellation.Cancel();
            RunJobs();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => firstTask);
            Assert.Equal(ContentDialogDismissReason.Cancellation, closed!.DismissReason);
            Assert.False(first.IsOpen);

            var second = new ContentDialog { Transition = null };
            var secondTask = second.ShowAsync(window, TestContext.Current.CancellationToken);
            second.Hide();
            RunJobs();
            await secondTask;
        }
    }

    [Fact]
    public async Task Owner_Close_Force_Closes_Without_Raising_Closing()
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var window = CreateWindow();
            var dialog = new ContentDialog { Transition = null };
            var closing = 0;
            ContentDialogClosedEventArgs? closed = null;
            dialog.Closing += (_, _) => closing++;
            dialog.Closed += (_, e) => closed = e;

            var task = dialog.ShowAsync(window, TestContext.Current.CancellationToken);
            window.Close();

            Assert.Equal(ContentDialogResult.None, await task);
            Assert.Equal(0, closing);
            Assert.Equal(ContentDialogDismissReason.OwnerClosed, closed!.DismissReason);
        }
    }

    [Fact]
    public void Dialog_Can_Be_Reused_After_It_Closes()
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var window = CreateWindow();
            var dialog = new ContentDialog { Transition = null };

            var first = dialog.ShowAsync(window, TestContext.Current.CancellationToken);
            dialog.Hide(ContentDialogResult.Primary);
            RunJobsUntil(first);
#pragma warning disable xUnit1031 // RunJobsUntil completed both operations before reading their results.
            Assert.Equal(ContentDialogResult.Primary, first.Result);

            var second = dialog.ShowAsync(window, TestContext.Current.CancellationToken);
            dialog.Hide(ContentDialogResult.Secondary);
            RunJobsUntil(second);
            Assert.Equal(ContentDialogResult.Secondary, second.Result);
#pragma warning restore xUnit1031
        }
    }

    [Fact]
    public async Task Message_Dialog_Validates_Action_Contract_Before_Provider_Lookup()
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var window = CreateWindow();
            var options = new MessageDialogOptions(
                "Invalid",
                new MessageDialogAction("same", "One"),
                new MessageDialogAction("same", "Two"));

            await Assert.ThrowsAsync<ArgumentException>(() =>
                window.MessageDialogs.ShowAsync(options, TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public async Task Native_Message_Dialog_Uses_The_Registered_Provider()
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var nativeProvider = new ControllableMessageDialogProvider();
            AvaloniaLocator.CurrentMutable.Bind<INativeMessageDialogProviderFactory>()
                .ToConstant(new TestMessageDialogProviderFactory(nativeProvider));
            var window = CreateWindow();
            var options = new MessageDialogOptions(
                "Continue?",
                new MessageDialogAction("continue", "Continue"));

            Assert.True(window.MessageDialogs.IsSupported);

            var task = window.MessageDialogs.ShowAsync(options, TestContext.Current.CancellationToken);

            Assert.Equal(1, nativeProvider.CallCount);
            Assert.NotNull(ManagedModalCoordinator.GetActiveModal(window));

            nativeProvider.Complete("continue");
            RunJobs();

            Assert.Equal("continue", (await task).ActionId);
            Assert.Null(ManagedModalCoordinator.GetActiveModal(window));
        }
    }

    [Fact]
    public async Task Native_Message_Dialog_Does_Not_Fall_Back_When_Unavailable()
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var window = CreateWindow();
            var options = new MessageDialogOptions(
                "Continue?",
                new MessageDialogAction("continue", "Continue"));

            Assert.False(window.MessageDialogs.IsSupported);
            await Assert.ThrowsAsync<NotSupportedException>(() =>
                window.MessageDialogs.ShowAsync(options, TestContext.Current.CancellationToken));
            Assert.Null(ManagedModalCoordinator.GetActiveModal(window));
        }
    }

    [Fact]
    public void TopLevel_Close_Disposes_A_Factory_Message_Dialog_Provider()
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var nativeProvider = new DisposableMessageDialogProvider();
            AvaloniaLocator.CurrentMutable.Bind<INativeMessageDialogProviderFactory>()
                .ToConstant(new TestMessageDialogProviderFactory(nativeProvider));
            var window = CreateWindow();

            _ = window.MessageDialogs;
            window.Close();

            Assert.True(nativeProvider.IsDisposed);
            Assert.False(window.MessageDialogs.IsSupported);
        }
    }

    [Fact]
    public async Task Coordinated_Provider_Defers_Disposal_Until_Active_Show_Ends()
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var nativeProvider = new ControllableDisposableMessageDialogProvider();
            var window = CreateWindow();
            var provider = new CoordinatedMessageDialogProvider(window, nativeProvider);
            var options = new MessageDialogOptions(
                "Continue?",
                new MessageDialogAction("continue", "Continue"));

            var task = provider.ShowAsync(options, TestContext.Current.CancellationToken);
            provider.Dispose();

            Assert.False(nativeProvider.IsDisposed);
            nativeProvider.Complete("continue");
            RunJobs();
            Assert.Equal("continue", (await task).ActionId);
            Assert.True(nativeProvider.IsDisposed);
        }
    }

    [Fact]
    public async Task Escape_Uses_The_Dialog_Cancel_Behavior()
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var window = CreateWindow();
            var dialog = new ContentDialog
            {
                CancelButton = ContentDialogButton.None,
                Transition = null
            };
            ContentDialogClosedEventArgs? closed = null;
            dialog.Closed += (_, e) => closed = e;

            var task = dialog.ShowAsync(window, TestContext.Current.CancellationToken);
            dialog.RaiseEvent(new KeyEventArgs
            {
                RoutedEvent = InputElement.KeyDownEvent,
                Key = Key.Escape
            });
            RunJobs();

            Assert.Equal(ContentDialogResult.None, await task);
            Assert.Equal(ContentDialogDismissReason.Escape, closed!.DismissReason);
        }
    }

    [Fact]
    public async Task Escape_Invokes_The_Configured_Cancel_Action_Without_Losing_The_Dismiss_Reason()
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var window = CreateWindow();
            var executed = 0;
            var dialog = new ContentDialog
            {
                CloseButtonContent = "Cancel",
                CloseButtonCommand = new TestCommand(() => executed++),
                CancelButton = ContentDialogButton.Close,
                Transition = null
            };
            ContentDialogClosedEventArgs? closed = null;
            dialog.Closed += (_, e) => closed = e;

            var task = dialog.ShowAsync(window, TestContext.Current.CancellationToken);
            dialog.RaiseEvent(new KeyEventArgs
            {
                RoutedEvent = InputElement.KeyDownEvent,
                Key = Key.Escape
            });
            RunJobs();

            Assert.Equal(ContentDialogResult.Close, await task);
            Assert.Equal(1, executed);
            Assert.Equal(ContentDialogDismissReason.Escape, closed!.DismissReason);
        }
    }

    [Fact]
    public async Task System_Back_Is_Consumed_By_The_Active_Dialog()
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var window = CreateWindow();
            var dialog = new ContentDialog
            {
                CancelButton = ContentDialogButton.None,
                Transition = null
            };
            ContentDialogClosedEventArgs? closed = null;
            dialog.Closed += (_, e) => closed = e;

            var task = dialog.ShowAsync(window, TestContext.Current.CancellationToken);
            var args = new RoutedEventArgs(TopLevel.BackRequestedEvent);
            window.RaiseEvent(args);
            RunJobs();

            Assert.True(args.Handled);
            Assert.Equal(ContentDialogResult.None, await task);
            Assert.Equal(ContentDialogDismissReason.SystemBack, closed!.DismissReason);
        }
    }

    [Fact]
    public async Task System_Back_Invokes_The_Configured_Cancel_Action_Without_Losing_The_Dismiss_Reason()
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var window = CreateWindow();
            var executed = 0;
            var dialog = new ContentDialog
            {
                CloseButtonContent = "Cancel",
                CloseButtonCommand = new TestCommand(() => executed++),
                CancelButton = ContentDialogButton.Close,
                Transition = null
            };
            ContentDialogClosedEventArgs? closed = null;
            dialog.Closed += (_, e) => closed = e;

            var task = dialog.ShowAsync(window, TestContext.Current.CancellationToken);
            var args = new RoutedEventArgs(TopLevel.BackRequestedEvent);
            window.RaiseEvent(args);
            RunJobs();

            Assert.True(args.Handled);
            Assert.Equal(ContentDialogResult.Close, await task);
            Assert.Equal(1, executed);
            Assert.Equal(ContentDialogDismissReason.SystemBack, closed!.DismissReason);
        }
    }

    [Fact]
    public async Task Escape_And_System_Back_Can_Be_Enabled_Independently()
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var window = CreateWindow();
            var dialog = new ContentDialog
            {
                CancelButton = ContentDialogButton.None,
                IsEscapeEnabled = true,
                IsSystemBackEnabled = false,
                Transition = null
            };

            var task = dialog.ShowAsync(window, TestContext.Current.CancellationToken);
            var args = new RoutedEventArgs(TopLevel.BackRequestedEvent);
            window.RaiseEvent(args);
            RunJobs();

            Assert.True(args.Handled);
            Assert.False(task.IsCompleted);

            dialog.RaiseEvent(new KeyEventArgs
            {
                RoutedEvent = InputElement.KeyDownEvent,
                Key = Key.Escape
            });
            RunJobs();

            Assert.Equal(ContentDialogResult.None, await task);
        }
    }

    [Fact]
    public async Task Backdrop_Press_Light_Dismisses_When_Enabled()
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var window = CreateWindow();
            var dialog = new ContentDialog
            {
                IsLightDismissEnabled = true,
                Transition = null
            };
            ContentDialogClosedEventArgs? closed = null;
            dialog.Closed += (_, e) => closed = e;

            var task = dialog.ShowAsync(window, TestContext.Current.CancellationToken);
            var backdrop = dialog.GetTemplateDescendants()
                .OfType<Control>()
                .Single(x => x.Name == "PART_Backdrop");
            var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
            backdrop.RaiseEvent(new PointerPressedEventArgs(
                backdrop,
                pointer,
                backdrop,
                default,
                timestamp: 1,
                new PointerPointProperties(
                    RawInputModifiers.LeftMouseButton,
                    PointerUpdateKind.LeftButtonPressed),
                KeyModifiers.None));
            RunJobs();

            Assert.Equal(ContentDialogResult.None, await task);
            Assert.Equal(ContentDialogDismissReason.LightDismiss, closed!.DismissReason);
        }
    }

    [Fact]
    public async Task Focus_Is_Restored_To_The_Previous_Owner_Element()
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var original = new Button { Content = "Original" };
            var window = CreateWindow(original);
            original.Focus();
            Assert.Same(original, window.FocusManager.GetFocusedElement());

            var dialog = new ContentDialog { Content = new TextBox(), Transition = null };
            var task = dialog.ShowAsync(window, TestContext.Current.CancellationToken);
            Assert.NotSame(original, window.FocusManager.GetFocusedElement());

            dialog.Hide();
            RunJobs();

            Assert.Same(original, window.FocusManager.GetFocusedElement());
            await task;
        }
    }

    [Fact]
    public void Opened_Exception_Cleans_The_Overlay_And_Owner_Slot()
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var window = CreateWindow();
            var failing = new ContentDialog { Transition = null };
            failing.Opened += (_, _) => throw new TestException();

            Assert.Throws<TestException>(() =>
            {
                _ = failing.ShowAsync(window, TestContext.Current.CancellationToken);
            });

            var overlay = OverlayLayer.GetOverlayLayer(window)!;
            Assert.DoesNotContain(failing, overlay.Children);

            var next = new ContentDialog { Transition = null };
            _ = next.ShowAsync(window, TestContext.Current.CancellationToken);
            next.Hide();
            RunJobs();
        }
    }

    [Fact]
    public async Task Opened_Handler_Can_Request_Close_Then_Throw_Without_Poisoning_Reuse()
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var window = CreateWindow();
            var dialog = new ContentDialog { Transition = null };
            EventHandler handler = (_, _) =>
            {
                dialog.Hide();
                throw new TestException();
            };
            dialog.Opened += handler;

            Assert.Throws<TestException>(() =>
            {
                _ = dialog.ShowAsync(window, TestContext.Current.CancellationToken);
            });
            RunJobs();

            dialog.Opened -= handler;
            var retry = dialog.ShowAsync(window, TestContext.Current.CancellationToken);
            dialog.Hide();
            RunJobs();
            await retry;
        }
    }

    [Fact]
    public async Task Escape_Can_Close_During_The_Opening_Transition()
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var window = CreateWindow();
            var transition = new ControllableTransition();
            var dialog = new ContentDialog
            {
                CancelButton = ContentDialogButton.None,
                Transition = transition
            };
            ContentDialogClosedEventArgs? closed = null;
            dialog.Closed += (_, e) => closed = e;
            var task = dialog.ShowAsync(window, TestContext.Current.CancellationToken);
            RunJobs();

            dialog.RaiseEvent(new KeyEventArgs
            {
                RoutedEvent = InputElement.KeyDownEvent,
                Key = Key.Escape
            });
            RunJobs();

            Assert.True(transition.Calls[0].CancellationToken.IsCancellationRequested);
            transition.ClosingCompleted.SetResult(null);
            RunJobs();
            await task;
            Assert.Equal(ContentDialogDismissReason.Escape, closed!.DismissReason);
        }
    }

    [Fact]
    public async Task Canceled_Close_From_Opening_Still_Attaches_And_Opens()
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var window = CreateWindow();
            var cancelClose = true;
            var dialog = new ContentDialog { Transition = null };
            dialog.Opening += (_, _) => dialog.Hide();
            dialog.Closing += (_, e) => e.Cancel = cancelClose;

            var task = dialog.ShowAsync(window, TestContext.Current.CancellationToken);
            RunJobs();

            Assert.True(dialog.IsOpen);
            Assert.False(task.IsCompleted);
            Assert.Contains(dialog, OverlayLayer.GetOverlayLayer(window)!.Children);

            cancelClose = false;
            dialog.Hide();
            RunJobs();
            await task;
        }
    }

    [Fact]
    public async Task Opened_Exception_After_A_Transition_Faults_Show_And_Releases_The_Owner()
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var window = CreateWindow();
            var dialog = new ContentDialog { Transition = new ClosingGateTransition() };
            dialog.Opened += (_, _) => throw new TestException();

            var task = dialog.ShowAsync(window, TestContext.Current.CancellationToken);
            RunJobs();

            await Assert.ThrowsAsync<TestException>(() => task);
            Assert.False(dialog.IsOpen);
            Assert.Null(ManagedModalCoordinator.GetActiveModal(window));
        }
    }

    [Fact]
    public async Task Action_Handler_Exception_Does_Not_Leave_The_Dialog_Pending()
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var window = CreateWindow();
            var dialog = new ContentDialog
            {
                PrimaryButtonContent = "Continue",
                Transition = null
            };
            EventHandler<ContentDialogButtonClickEventArgs> handler = (_, _) => throw new TestException();
            dialog.PrimaryButtonClick += handler;
            var task = dialog.ShowAsync(window, TestContext.Current.CancellationToken);
            var button = dialog.GetTemplateDescendants()
                .OfType<Button>()
                .Single(x => x.Name == "PART_PrimaryButton");

            Assert.Throws<TestException>(() =>
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
            dialog.PrimaryButtonClick -= handler;
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            RunJobs();

            Assert.Equal(ContentDialogResult.Primary, await task);
        }
    }

    [Fact]
    public async Task Closing_Handler_Exception_Does_Not_Leave_The_Dialog_Closing()
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var window = CreateWindow();
            var dialog = new ContentDialog { Transition = null };
            EventHandler<ContentDialogClosingEventArgs> handler = (_, _) => throw new TestException();
            dialog.Closing += handler;
            var task = dialog.ShowAsync(window, TestContext.Current.CancellationToken);

            Assert.Throws<TestException>(() => dialog.Hide());
            Assert.True(dialog.IsOpen);
            dialog.Closing -= handler;
            dialog.Hide();
            RunJobs();

            await task;
            Assert.False(dialog.IsOpen);
        }
    }

    [Fact]
    public async Task Reused_Transition_Is_Awaited_For_Opening_And_Closing()
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var window = CreateWindow();
            var transition = new ControllableTransition();
            var dialog = new ContentDialog { Transition = transition };
            var opened = 0;
            var closed = 0;
            dialog.Opened += (_, _) => opened++;
            dialog.Closed += (_, _) => closed++;

            var task = dialog.ShowAsync(window, TestContext.Current.CancellationToken);
            RunJobs();

            var opening = Assert.Single(transition.Calls);
            Assert.Null(opening.From);
            Assert.NotNull(opening.To);
            Assert.True(opening.Forward);
            Assert.Equal(0, opened);

            CompleteOnWorkerThread(transition.OpeningCompleted);
            RunJobs();
            Assert.Equal(1, opened);

            dialog.Hide(ContentDialogResult.Primary);
            RunJobs();
            Assert.False(task.IsCompleted);
            Assert.Equal(0, closed);
            Assert.Equal(2, transition.Calls.Count);
            var closing = transition.Calls[1];
            Assert.NotNull(closing.From);
            Assert.Null(closing.To);
            Assert.False(closing.Forward);

            CompleteOnWorkerThread(transition.ClosingCompleted);
            RunJobs();
            Assert.Equal(ContentDialogResult.Primary, await task);
            Assert.Equal(1, closed);
        }
    }

    [Fact]
    public async Task Dialog_Surface_Is_Transparent_Until_Opening_Transition_Starts()
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var window = CreateWindow();
            var transition = new ControllableTransition();
            var dialog = new ContentDialog { Transition = transition };

            var task = dialog.ShowAsync(window, TestContext.Current.CancellationToken);
            var surface = dialog.GetTemplateDescendants()
                .OfType<Control>()
                .Single(x => x.Name == "PART_DialogSurface");

            Assert.Equal(0, surface.Opacity);

            RunJobs();

            Assert.Equal(1, surface.Opacity);
            Assert.Single(transition.Calls);

            transition.OpeningCompleted.SetResult(null);
            RunJobs();
            dialog.Hide();
            transition.ClosingCompleted.SetResult(null);
            RunJobs();
            await task;
        }
    }

    [Fact]
    public async Task Retemplate_Before_Queued_Opening_Transition_Restarts_With_Current_Surface()
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var window = CreateWindow();
            var transition = new ControllableTransition();
            var dialog = new ContentDialog { Transition = transition };
            var opened = 0;
            dialog.Opened += (_, _) => opened++;

            var task = dialog.ShowAsync(window, TestContext.Current.CancellationToken);
            Control? replacementSurface = null;
            dialog.Template = new FuncControlTemplate<ContentDialog>((_, scope) =>
            {
                replacementSurface = new Border { Name = "PART_DialogSurface" };
                scope.Register("PART_DialogSurface", replacementSurface);
                return replacementSurface;
            });
            dialog.ApplyTemplate();

            Assert.NotNull(replacementSurface);
            Assert.Equal(0, replacementSurface.Opacity);

            RunJobs();

            var opening = Assert.Single(transition.Calls);
            Assert.Same(replacementSurface, opening.To);
            Assert.Equal(1, replacementSurface.Opacity);
            Assert.Equal(0, opened);

            transition.OpeningCompleted.SetResult(null);
            RunJobs();
            Assert.Equal(1, opened);

            dialog.Transition = null;
            dialog.Hide();
            RunJobs();
            await task;
        }
    }

    [Fact]
    public async Task Retemplate_During_Opening_Transition_Cancels_And_Restarts_With_Current_Surface()
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var window = CreateWindow();
            var transition = new ControllableTransition();
            var dialog = new ContentDialog { Transition = transition };
            var task = dialog.ShowAsync(window, TestContext.Current.CancellationToken);
            RunJobs();

            var firstOpening = Assert.Single(transition.Calls);
            Control? replacementSurface = null;
            dialog.Template = new FuncControlTemplate<ContentDialog>((_, scope) =>
            {
                replacementSurface = new Border { Name = "PART_DialogSurface" };
                scope.Register("PART_DialogSurface", replacementSurface);
                return replacementSurface;
            });
            dialog.ApplyTemplate();

            Assert.True(firstOpening.CancellationToken.IsCancellationRequested);
            Assert.NotNull(replacementSurface);
            Assert.Equal(0, replacementSurface.Opacity);

            RunJobs();

            Assert.Equal(2, transition.Calls.Count);
            Assert.Same(replacementSurface, transition.Calls[1].To);
            transition.OpeningCompleted.SetResult(null);
            RunJobs();
            Assert.True(dialog.IsOpen);

            dialog.Transition = null;
            dialog.Hide();
            RunJobs();
            await task;
        }
    }

    [Fact]
    public async Task Immediate_Close_Does_Not_Run_Stale_Opening_Visual_State()
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var window = CreateWindow();
            var transition = new ControllableTransition();
            var dialog = new ContentDialog { Transition = transition };

            var task = dialog.ShowAsync(window, TestContext.Current.CancellationToken);
            var surface = dialog.GetTemplateDescendants()
                .OfType<Control>()
                .Single(x => x.Name == "PART_DialogSurface");
            Assert.Equal(0, surface.Opacity);

            dialog.Hide();
            RunJobs();

            Assert.Equal(0, surface.Opacity);
            Assert.Single(transition.Calls);
            Assert.NotNull(transition.Calls[0].From);

            transition.ClosingCompleted.SetResult(null);
            RunJobs();
            await task;
            Assert.Equal(1, surface.Opacity);
        }
    }

    [Fact]
    public async Task Caller_Cancellation_Cancels_An_Active_Transition_And_Closes_Immediately()
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var window = CreateWindow();
            var transition = new ControllableTransition();
            using var cancellation = new CancellationTokenSource();
            var dialog = new ContentDialog { Transition = transition };

            var task = dialog.ShowAsync(window, cancellation.Token);
            RunJobs();
            cancellation.Cancel();
            RunJobs();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
            Assert.True(transition.Calls[0].CancellationToken.IsCancellationRequested);
            Assert.False(dialog.IsOpen);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Canceled_Custom_Transition_Late_Fault_Is_Observed_Without_Affecting_Reuse(
        bool cancelDuringClose)
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var window = CreateWindow();
            var transition = new IgnoringCancellationTransition();
            using var cancellation = new CancellationTokenSource();
            var dialog = new ContentDialog { Transition = transition };
            Exception? loggedException = null;
            using var logged = new ManualResetEventSlim();
            using var logSink = TestLogSink.Start((level, area, source, template, values) =>
            {
                if (ReferenceEquals(source, dialog) &&
                    template == "ContentDialog transition threw an unhandled exception: {Exception}" &&
                    values.FirstOrDefault() is Exception exception)
                {
                    loggedException = exception;
                    logged.Set();
                }
            });

            var firstTask = dialog.ShowAsync(window, cancellation.Token);
            RunJobs();

            if (cancelDuringClose)
            {
                CompleteOnWorkerThread(transition.OpeningCompletion);
                RunJobs();
                Assert.True(dialog.IsOpen);
                dialog.Hide();
                RunJobs();
            }

            cancellation.Cancel();
            RunJobs();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => firstTask);
            Assert.True((cancelDuringClose
                ? transition.ClosingCancellationToken
                : transition.OpeningCancellationToken).IsCancellationRequested);

            dialog.Transition = null;
            var secondTask = dialog.ShowAsync(window, TestContext.Current.CancellationToken);
            RunJobs();
            Assert.True(dialog.IsOpen);

            var lateException = new TestException();
            (cancelDuringClose
                ? transition.ClosingCompletion
                : transition.OpeningCompletion).SetException(lateException);

            Assert.True(logged.Wait(TimeSpan.FromSeconds(5)));
            Assert.Same(lateException, loggedException);
            Assert.True(dialog.IsOpen);
            Assert.False(secondTask.IsCompleted);

            dialog.Hide();
            RunJobs();
            await secondTask;
        }
    }

    [Fact]
    public async Task Throwing_Transition_Cancellation_Callback_Cannot_Block_Forced_Cleanup()
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var window = CreateWindow();
            using var cancellation = new CancellationTokenSource();
            var dialog = new ContentDialog { Transition = new ThrowingCancellationTransition() };

            var task = dialog.ShowAsync(window, cancellation.Token);
            RunJobs();
            cancellation.Cancel();
            RunJobs();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
            Assert.False(dialog.IsOpen);
            Assert.Null(ManagedModalCoordinator.GetActiveModal(window));
        }
    }

    [Fact]
    public async Task Queued_Cancellation_From_A_Previous_Show_Cannot_Close_A_Reused_Dialog()
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var window = CreateWindow();
            using var cancellation = new CancellationTokenSource();
            var transition = new ClosingGateTransition();
            var dialog = new ContentDialog { Transition = transition };
            Task<ContentDialogResult>? secondTask = null;
            var reopened = false;
            dialog.Closed += (_, _) =>
            {
                if (reopened)
                    return;

                reopened = true;
                dialog.Transition = null;
                secondTask = dialog.ShowAsync(window, TestContext.Current.CancellationToken);
            };

            var firstTask = dialog.ShowAsync(window, cancellation.Token);
            RunJobs();
            dialog.Hide(ContentDialogResult.Primary);
            RunJobs();

            var worker = new Thread(() =>
            {
                transition.ClosingCompleted.SetResult(null);
                cancellation.Cancel();
            });
            worker.Start();
            Assert.True(worker.Join(TimeSpan.FromSeconds(5)));
            RunJobs();

            Assert.Equal(ContentDialogResult.Primary, await firstTask);
            Assert.True(dialog.IsOpen);
            Assert.NotNull(secondTask);
            Assert.False(secondTask!.IsCompleted);

            dialog.Hide();
            RunJobs();
            await secondTask;
        }
    }

    [Fact]
    public void Canceled_Deferral_Releases_Its_Completion_Callback()
    {
        var (deferral, captured) = CreateCanceledDeferral();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.False(captured.IsAlive);
        deferral.Complete();
        RunJobs();
        GC.KeepAlive(deferral);
    }

    [Fact]
    public void Deferral_Cannot_Be_Requested_After_The_Event_Handler_Returns()
    {
        var completed = false;
        var manager = new DialogDeferralManager(() => completed = true);

        manager.CompleteInitialDeferral();

        Assert.Throws<InvalidOperationException>(() => manager.GetDeferral());
        RunJobs();
        Assert.True(completed);
    }

    private static Window CreateWindow(object? content = null)
    {
        var window = new Window
        {
            Width = 800,
            Height = 600,
            Content = content ?? new Border()
        };
        window.Show();
        window.LayoutManager.ExecuteLayoutPass();
        return window;
    }

    private static void RunJobs()
        => Dispatcher.UIThread.RunJobs(null, TestContext.Current.CancellationToken);

    private static void RunJobsUntil(Task task)
    {
        var timeout = Stopwatch.StartNew();
        while (!task.IsCompleted)
        {
            RunJobs();
            if (timeout.Elapsed > TimeSpan.FromSeconds(5))
                throw new TimeoutException("Timed out waiting for dispatcher work to complete.");
            Thread.Yield();
        }

        RunJobs();
    }

    private static void CompleteOnWorkerThread(TaskCompletionSource<object?> completion)
    {
        var worker = new Thread(() => completion.SetResult(null));
        worker.Start();
        if (!worker.Join(TimeSpan.FromSeconds(5)))
            throw new TimeoutException("Timed out completing a transition from a worker thread.");
    }

    private sealed class TestCommand : ICommand
    {
        private readonly Action _execute;

        public TestCommand(Action execute)
        {
            _execute = execute;
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => _execute();

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }
    }

    private sealed class ControllableMessageDialogProvider : INativeMessageDialogProvider
    {
        private readonly TaskCompletionSource<MessageDialogResult> _completion = new();

        public int CallCount { get; private set; }

        public Task<MessageDialogResult> ShowAsync(
            MessageDialogOptions options,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return _completion.Task;
        }

        public void Complete(string actionId)
            => _completion.SetResult(new MessageDialogResult(
                actionId,
                MessageDialogDismissReason.Action));
    }

    private sealed class CancellableMessageDialogProvider : INativeMessageDialogProvider
    {
        private readonly TaskCompletionSource<MessageDialogResult> _completion = new();

        public CancellationToken CancellationToken { get; private set; }

        public Task<MessageDialogResult> ShowAsync(
            MessageDialogOptions options,
            CancellationToken cancellationToken = default)
        {
            CancellationToken = cancellationToken;
            cancellationToken.Register(() => _completion.TrySetCanceled(cancellationToken));
            return _completion.Task;
        }
    }

    private sealed class DisposableMessageDialogProvider : INativeMessageDialogProvider, IDisposable
    {
        public bool IsDisposed { get; private set; }

        public Task<MessageDialogResult> ShowAsync(
            MessageDialogOptions options,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public void Dispose() => IsDisposed = true;
    }

    private sealed class ControllableDisposableMessageDialogProvider : INativeMessageDialogProvider, IDisposable
    {
        private readonly TaskCompletionSource<MessageDialogResult> _completion = new();

        public bool IsDisposed { get; private set; }

        public Task<MessageDialogResult> ShowAsync(
            MessageDialogOptions options,
            CancellationToken cancellationToken = default)
            => _completion.Task;

        public void Complete(string actionId) =>
            _completion.SetResult(new MessageDialogResult(
                actionId,
                MessageDialogDismissReason.Action));

        public void Dispose() => IsDisposed = true;
    }

    private sealed class TestMessageDialogProviderFactory : INativeMessageDialogProviderFactory
    {
        private readonly INativeMessageDialogProvider _provider;

        public TestMessageDialogProviderFactory(INativeMessageDialogProvider provider)
        {
            _provider = provider;
        }

        public INativeMessageDialogProvider CreateProvider(TopLevel topLevel) => _provider;
    }

    private sealed class ControllableTransition : IPageTransition
    {
        public TaskCompletionSource<object?> OpeningCompleted { get; } = new();
        public TaskCompletionSource<object?> ClosingCompleted { get; } = new();
        public System.Collections.Generic.List<TransitionCall> Calls { get; } = new();

        public Task Start(Visual? from, Visual? to, bool forward, CancellationToken cancellationToken)
        {
            Calls.Add(new TransitionCall(from, to, forward, cancellationToken));
            return from is null ? OpeningCompleted.Task : ClosingCompleted.Task;
        }
    }

    private readonly record struct TransitionCall(
        Visual? From,
        Visual? To,
        bool Forward,
        CancellationToken CancellationToken);

    private sealed class ThrowingCancellationTransition : IPageTransition
    {
        private readonly TaskCompletionSource<object?> _never = new();

        public Task Start(Visual? from, Visual? to, bool forward, CancellationToken cancellationToken)
        {
            cancellationToken.Register(() => throw new TestException());
            return _never.Task;
        }
    }

    private sealed class IgnoringCancellationTransition : IPageTransition
    {
        public TaskCompletionSource<object?> OpeningCompletion { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<object?> ClosingCompletion { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public CancellationToken OpeningCancellationToken { get; private set; }

        public CancellationToken ClosingCancellationToken { get; private set; }

        public Task Start(Visual? from, Visual? to, bool forward, CancellationToken cancellationToken)
        {
            if (from is null)
            {
                OpeningCancellationToken = cancellationToken;
                return OpeningCompletion.Task;
            }

            ClosingCancellationToken = cancellationToken;
            return ClosingCompletion.Task;
        }
    }

    private sealed class ClosingGateTransition : IPageTransition
    {
        public TaskCompletionSource<object?> ClosingCompleted { get; } = new();

        public Task Start(Visual? from, Visual? to, bool forward, CancellationToken cancellationToken)
            => from is null ? Task.CompletedTask : ClosingCompleted.Task;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (DialogDeferral Deferral, WeakReference Captured) CreateCanceledDeferral()
    {
        var captured = new object();
        var weakReference = new WeakReference(captured);
        var manager = new DialogDeferralManager(() => GC.KeepAlive(captured));
        var deferral = manager.GetDeferral();
        manager.CompleteInitialDeferral();
        manager.Cancel();
        return (deferral, weakReference);
    }

    private sealed class TestException : Exception
    {
    }
}
