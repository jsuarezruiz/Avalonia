using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Animation;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.UnitTests;
using Xunit;

namespace Avalonia.Controls.UnitTests;

public class BottomSheetTests : ScopedTestBase
{
    [Fact]
    public async Task Show_And_Hide_Share_The_Modal_Lifecycle()
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var window = CreateWindow();
            var sheet = new BottomSheet
            {
                Header = "Options",
                Content = new Border(),
                Transition = null
            };
            var opening = 0;
            var opened = 0;
            var closing = 0;
            var closed = 0;
            sheet.Opening += (_, _) => opening++;
            sheet.Opened += (_, _) => opened++;
            sheet.Closing += (_, _) => closing++;
            sheet.Closed += (_, _) => closed++;

            var task = sheet.ShowAsync(window, TestContext.Current.CancellationToken);

            Assert.True(sheet.IsOpen);
            Assert.IsType<ContentDialog>(ManagedModalCoordinator.GetActiveModal(window));
            Assert.Equal(1, opening);
            Assert.Equal(1, opened);

            sheet.Hide("accepted");
            RunJobs();

            var result = await task;
            Assert.True(result.HasValue);
            Assert.Equal("accepted", result.Value);
            Assert.Equal(BottomSheetDismissReason.Programmatic, result.DismissReason);
            Assert.Equal(1, closing);
            Assert.Equal(1, closed);
        }
    }

    [Fact]
    public async Task Show_Can_Complete_Without_A_Value()
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var window = CreateWindow();
            var sheet = new BottomSheet { Transition = null };

            var task = sheet.ShowAsync(window, TestContext.Current.CancellationToken);
            sheet.Hide();
            RunJobs();

            var result = await task;
            Assert.False(result.HasValue);
            Assert.Null(result.Value);
            Assert.Equal(BottomSheetDismissReason.Programmatic, result.DismissReason);
        }
    }

    [Fact]
    public async Task Explicit_Null_Is_A_Result_Value()
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var window = CreateWindow();
            var sheet = new BottomSheet { Transition = null };
            BottomSheetClosingEventArgs? closing = null;
            sheet.Closing += (_, e) => closing = e;

            var task = sheet.ShowAsync(window, TestContext.Current.CancellationToken);
            sheet.Hide(null);
            RunJobs();

            var result = await task;
            Assert.True(result.HasValue);
            Assert.Null(result.Value);
            Assert.True(closing!.HasValue);
            Assert.Null(closing.Value);
        }
    }

    [Fact]
    public async Task Opening_Handler_Can_Hide_The_Sheet()
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var window = CreateWindow();
            var sheet = new BottomSheet { Transition = null };
            sheet.Opening += (_, _) => sheet.Hide("closed while opening");

            var task = sheet.ShowAsync(window, TestContext.Current.CancellationToken);
            RunJobs();

            var result = await task;
            Assert.Equal("closed while opening", result.Value);
            Assert.Equal(BottomSheetDismissReason.Programmatic, result.DismissReason);
            Assert.False(sheet.IsOpen);
        }
    }

    [Fact]
    public async Task Cancellation_Is_Reported_To_Closed_While_The_Show_Task_Is_Canceled()
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var window = CreateWindow();
            var sheet = new BottomSheet { Transition = null };
            using var cancellation = new CancellationTokenSource();
            BottomSheetClosedEventArgs? closed = null;
            sheet.Closed += (_, e) => closed = e;

            var task = sheet.ShowAsync(window, cancellation.Token);
            cancellation.Cancel();
            RunJobs();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
            Assert.Equal(BottomSheetDismissReason.Cancellation, closed!.Result.DismissReason);
            Assert.False(closed.Result.HasValue);
        }
    }

    [Fact]
    public void Sheet_And_Dialog_Use_One_Modal_Slot_Per_Owner()
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var window = CreateWindow();
            var sheet = new BottomSheet { Transition = null };
            _ = sheet.ShowAsync(window, TestContext.Current.CancellationToken);

            var dialog = new ContentDialog { Transition = null };
            Assert.Throws<InvalidOperationException>(() =>
            {
                _ = dialog.ShowAsync(window, TestContext.Current.CancellationToken);
            });

            sheet.Hide();
            RunJobs();
        }
    }

    [Fact]
    public async Task Closing_Can_Be_Canceled_And_Retried_With_A_New_Value()
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var window = CreateWindow();
            var sheet = new BottomSheet { Transition = null };
            var cancel = true;
            sheet.Closing += (_, e) => e.Cancel = cancel;

            var task = sheet.ShowAsync(window, TestContext.Current.CancellationToken);
            sheet.Hide("first");
            RunJobs();

            Assert.True(sheet.IsOpen);
            Assert.False(task.IsCompleted);

            cancel = false;
            sheet.Hide("second");
            RunJobs();

            Assert.Equal("second", (await task).Value);
        }
    }

    [Fact]
    public async Task Closing_Deferral_Delays_Close()
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var window = CreateWindow();
            var sheet = new BottomSheet { Transition = null };
            DialogDeferral? deferral = null;
            sheet.Closing += (_, e) => deferral = e.GetDeferral();

            var task = sheet.ShowAsync(window, TestContext.Current.CancellationToken);
            sheet.Hide();
            RunJobs();

            Assert.True(sheet.IsOpen);
            Assert.False(task.IsCompleted);

            deferral!.Complete();
            RunJobs();

            Assert.Equal(BottomSheetDismissReason.Programmatic, (await task).DismissReason);
        }
    }

    [Fact]
    public async Task Reentrant_Hide_During_Closing_Deferral_Does_Not_Replace_Result()
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var window = CreateWindow();
            var sheet = new BottomSheet { Transition = null };
            DialogDeferral? deferral = null;
            sheet.Closing += (_, e) => deferral = e.GetDeferral();

            var task = sheet.ShowAsync(window, TestContext.Current.CancellationToken);
            sheet.Hide("first");
            RunJobs();

            sheet.Hide("ignored");
            deferral!.Complete();
            RunJobs();

            Assert.Equal("first", (await task).Value);
        }
    }

    [Fact]
    public void Show_Validates_Detents()
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var window = CreateWindow();
            var empty = new BottomSheet();
            empty.Detents.Clear();
            Assert.Throws<InvalidOperationException>(() =>
            {
                _ = empty.ShowAsync(window, TestContext.Current.CancellationToken);
            });

            var duplicate = new BottomSheet();
            duplicate.Detents.Clear();
            duplicate.Detents.Add(BottomSheetDetent.FromRatio("same", 0.25));
            duplicate.Detents.Add(BottomSheetDetent.FromRatio("same", 0.75));
            Assert.Throws<InvalidOperationException>(() =>
            {
                _ = duplicate.ShowAsync(window, TestContext.Current.CancellationToken);
            });
        }
    }

    [Fact]
    public void Detents_Resolve_Content_Fixed_And_Proportional_Heights()
    {
        Assert.Equal(240, BottomSheetDetent.Content.GetHeight(800, 240));
        Assert.Equal(180, BottomSheetDetent.FromHeight("compact", 180).GetHeight(800, 500));
        Assert.Equal(400, BottomSheetDetent.Medium.GetHeight(800, 240));
        Assert.Equal(600, BottomSheetDetent.FromRatio("three-quarters", 0.75).GetHeight(800, 240));
    }

    [Fact]
    public void Empty_Content_Detent_Does_Not_Throw_During_Measure()
    {
        using (UnitTestApplication.Start(TestServices.StyledWindow))
        {
            var sheet = new BottomSheet();

            sheet.Measure(new Size(400, 800));

            Assert.True(double.IsFinite(sheet.DesiredSize.Height));
        }
    }

    [Fact]
    public async Task First_Show_Measures_Content_Before_Validating_Custom_Detent()
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var window = CreateWindow();
            var detent = new ContentRelativeDetent();
            var sheet = new BottomSheet
            {
                Content = new Border { Height = 240 },
                Transition = null
            };
            sheet.Detents.Clear();
            sheet.Detents.Add(detent);
            sheet.SelectedDetent = detent;

            var task = sheet.ShowAsync(window, TestContext.Current.CancellationToken);
            window.LayoutManager.ExecuteLayoutPass();

            Assert.True(detent.LastContentHeight > 0);
            Assert.Equal(detent.LastContentHeight / 2, sheet.ActualSheetHeight);
            sheet.Hide();
            RunJobs();
            await task;
        }
    }

    [Fact]
    public async Task Live_Detent_Mutations_Preserve_A_Valid_State()
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var window = CreateWindow();
            var sheet = new BottomSheet { Transition = null };
            var task = sheet.ShowAsync(window, TestContext.Current.CancellationToken);

            Assert.Throws<InvalidOperationException>(() => sheet.Detents.Clear());
            Assert.Equal(2, sheet.Detents.Count);

            Assert.Throws<InvalidOperationException>(() =>
                sheet.Detents.Add(BottomSheetDetent.FromRatio("content", 0.25)));
            Assert.Equal(2, sheet.Detents.Count);

            Assert.Throws<InvalidOperationException>(() =>
                sheet.Detents.Add(new InvalidDetent()));
            Assert.Equal(2, sheet.Detents.Count);

            sheet.SelectedDetent = null;
            Assert.NotNull(sheet.SelectedDetent);
            Assert.Contains(sheet.SelectedDetent, sheet.Detents);

            sheet.Hide();
            RunJobs();
            await task;
        }
    }

    [Fact]
    public async Task Invalid_Indexer_Replacement_Is_Not_Observable()
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var window = CreateWindow();
            var sheet = new BottomSheet { Transition = null };
            var task = sheet.ShowAsync(window, TestContext.Current.CancellationToken);
            var original = sheet.Detents[1];
            var changes = 0;
            sheet.Detents.CollectionChanged += (_, _) => changes++;

            Assert.Throws<InvalidOperationException>(() =>
                sheet.Detents[1] = BottomSheetDetent.FromRatio("content", 0.5));

            Assert.Same(original, sheet.Detents[1]);
            Assert.Equal(0, changes);
            sheet.Hide();
            RunJobs();
            await task;
        }
    }

    [Fact]
    public async Task Live_RemoveAll_Is_Validated_Before_Changing_The_Collection()
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var window = CreateWindow();
            var sheet = new BottomSheet { Transition = null };
            var original = sheet.Detents.ToArray();
            var task = sheet.ShowAsync(window, TestContext.Current.CancellationToken);

            Assert.Throws<InvalidOperationException>(() => sheet.Detents.RemoveAll(original));
            Assert.Equal(original, sheet.Detents);

            sheet.Hide();
            RunJobs();
            await task;
        }
    }

    [Fact]
    public void Removing_The_Selected_Detent_Selects_The_First_Remaining_Detent()
    {
        var sheet = new BottomSheet { SelectedDetent = BottomSheetDetent.Expanded };

        sheet.Detents.Remove(BottomSheetDetent.Expanded);

        Assert.Same(BottomSheetDetent.Content, sheet.SelectedDetent);
    }

    [Fact]
    public void Sheet_Can_Be_Reused()
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var window = CreateWindow();
            var sheet = new BottomSheet { Transition = null };

            var first = sheet.ShowAsync(window, TestContext.Current.CancellationToken);
            sheet.Hide(1);
            RunJobsUntil(first);
#pragma warning disable xUnit1031 // RunJobsUntil completed both operations before reading their results.
            Assert.Equal(1, first.Result.Value);

            var second = sheet.ShowAsync(window, TestContext.Current.CancellationToken);
            sheet.Hide(2);
            RunJobsUntil(second);
            Assert.Equal(2, second.Result.Value);
#pragma warning restore xUnit1031
        }
    }

    [Fact]
    public async Task Show_Returns_An_Application_Result()
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var window = CreateWindow();
            var sheet = new BottomSheet
            {
                Transition = null,
                SelectedDetent = BottomSheetDetent.Content
            };
            var selectedChanges = 0;
            sheet.SelectedDetentChanged += (_, _) => selectedChanges++;

            var task = sheet.ShowAsync(window, TestContext.Current.CancellationToken);
            sheet.SelectedDetent = BottomSheetDetent.Expanded;
            sheet.Hide("accepted");
            RunJobs();

            var result = await task;
            Assert.Equal("accepted", result.Value);
            Assert.Equal(BottomSheetDismissReason.Programmatic, result.DismissReason);
            Assert.Equal(1, selectedChanges);
        }
    }

    [Fact]
    public async Task Second_Show_Does_Not_Replace_The_Active_Operation()
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var window = CreateWindow();
            var sheet = new BottomSheet { Transition = null };
            var first = sheet.ShowAsync(window, TestContext.Current.CancellationToken);

            Assert.Throws<InvalidOperationException>(() =>
            {
                _ = sheet.ShowAsync(window, TestContext.Current.CancellationToken);
            });

            sheet.Hide("first");
            RunJobs();
            Assert.Equal("first", (await first).Value);
        }
    }

    [Fact]
    public async Task Dismiss_Settings_Changed_While_Open_Are_Forwarded_To_The_Host()
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var window = CreateWindow();
            var sheet = new BottomSheet { Transition = null };
            var task = sheet.ShowAsync(window, TestContext.Current.CancellationToken);
            var host = Assert.IsType<ContentDialog>(ManagedModalCoordinator.GetActiveModal(window));

            sheet.IsLightDismissEnabled = false;
            sheet.IsEscapeEnabled = false;
            sheet.IsSystemBackEnabled = false;

            Assert.False(host.IsLightDismissEnabled);
            Assert.False(host.IsEscapeEnabled);
            Assert.False(host.IsSystemBackEnabled);

            sheet.Hide();
            RunJobs();
            await task;
        }
    }

    [Fact]
    public async Task Reopening_From_Closed_Does_Not_Overwrite_The_Previous_Result()
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var window = CreateWindow();
            var sheet = new BottomSheet { Transition = null };
            Task<BottomSheetResult>? secondTask = null;
            var reopened = false;
            sheet.Closed += (_, _) =>
            {
                if (reopened)
                    return;

                reopened = true;
                secondTask = sheet.ShowAsync(window, TestContext.Current.CancellationToken);
            };

            var firstTask = sheet.ShowAsync(window, TestContext.Current.CancellationToken);
            sheet.Hide("first");
            RunJobs();

            Assert.Equal("first", (await firstTask).Value);
            Assert.True(sheet.IsOpen);
            Assert.NotNull(secondTask);

            sheet.Hide("second");
            RunJobs();
            Assert.Equal("second", (await secondTask!).Value);
        }
    }

    [Fact]
    public async Task Opened_Exception_After_Transition_Releases_State_And_Allows_Reuse()
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var window = CreateWindow();
            var sheet = new BottomSheet { Transition = new RecordingTransition() };
            EventHandler handler = (_, _) => throw new TestException();
            sheet.Opened += handler;

            var failedTask = sheet.ShowAsync(window, TestContext.Current.CancellationToken);
            RunJobsUntil(failedTask);

            await Assert.ThrowsAsync<TestException>(() => failedTask);
            Assert.False(sheet.IsOpen);
            Assert.Null(ManagedModalCoordinator.GetActiveModal(window));

            sheet.Opened -= handler;
            sheet.Transition = null;
            var retryTask = sheet.ShowAsync(window, TestContext.Current.CancellationToken);
            sheet.Hide();
            RunJobs();
            await retryTask;
        }
    }

    [Fact]
    public void Drag_Handle_Keyboard_Changes_Detents()
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var handle = new Border { Name = "PART_DragHandle" };
            var sheet = new BottomSheet();
            sheet.Detents.Insert(1, BottomSheetDetent.Medium);
            sheet.SelectedDetent = sheet.Detents[0];
            sheet.Template = new FuncControlTemplate<BottomSheet>((_, scope) =>
                handle.RegisterInNameScope(scope));
            sheet.ApplyTemplate();

            Assert.True(handle.Focusable);
            handle.RaiseEvent(new KeyEventArgs
            {
                RoutedEvent = InputElement.KeyDownEvent,
                Key = Key.Up,
                Source = handle
            });
            Assert.Same(sheet.Detents[1], sheet.SelectedDetent);

            handle.RaiseEvent(new KeyEventArgs
            {
                RoutedEvent = InputElement.KeyDownEvent,
                Key = Key.End,
                Source = handle
            });
            Assert.Same(sheet.Detents[^1], sheet.SelectedDetent);

            handle.RaiseEvent(new KeyEventArgs
            {
                RoutedEvent = InputElement.KeyDownEvent,
                Key = Key.Home,
                Source = handle
            });
            Assert.Same(sheet.Detents[0], sheet.SelectedDetent);
        }
    }

    [Fact]
    public void Drag_Handle_Keyboard_Uses_Resolved_Height_Not_Collection_Order()
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var handle = new Border { Name = "PART_DragHandle" };
            var sheet = new BottomSheet();
            sheet.Detents.Clear();
            sheet.Detents.Add(BottomSheetDetent.Expanded);
            sheet.Detents.Add(BottomSheetDetent.Content);
            sheet.Detents.Add(BottomSheetDetent.Medium);
            sheet.SelectedDetent = BottomSheetDetent.Medium;
            sheet.Template = new FuncControlTemplate<BottomSheet>((_, scope) =>
                handle.RegisterInNameScope(scope));
            sheet.ApplyTemplate();

            handle.RaiseEvent(new KeyEventArgs
            {
                RoutedEvent = InputElement.KeyDownEvent,
                Key = Key.Up,
                Source = handle
            });
            Assert.Same(BottomSheetDetent.Expanded, sheet.SelectedDetent);

            handle.RaiseEvent(new KeyEventArgs
            {
                RoutedEvent = InputElement.KeyDownEvent,
                Key = Key.Home,
                Source = handle
            });
            Assert.Same(BottomSheetDetent.Content, sheet.SelectedDetent);
        }
    }

    [Fact]
    public async Task Transition_Is_Forwarded_To_The_Modal_Host_For_Open_And_Close()
    {
        using (UnitTestApplication.Start(TestServices.FocusableWindow))
        {
            var window = CreateWindow();
            var transition = new RecordingTransition();
            var sheet = new BottomSheet { Transition = transition };

            var task = sheet.ShowAsync(window, TestContext.Current.CancellationToken);
            RunJobs();
            Assert.Single(transition.Calls);
            Assert.Null(transition.Calls[0].From);
            Assert.NotNull(transition.Calls[0].To);
            Assert.True(transition.Calls[0].Forward);

            sheet.Hide();
            RunJobs();
            Assert.Equal(2, transition.Calls.Count);
            Assert.NotNull(transition.Calls[1].From);
            Assert.Null(transition.Calls[1].To);
            Assert.False(transition.Calls[1].Forward);
            Assert.Equal(BottomSheetDismissReason.Programmatic, (await task).DismissReason);
        }
    }

    private static Window CreateWindow()
    {
        var window = new Window
        {
            Width = 800,
            Height = 600,
            Content = new Border()
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

    private sealed class RecordingTransition : IPageTransition
    {
        public System.Collections.Generic.List<TransitionCall> Calls { get; } = new();

        public Task Start(Visual? from, Visual? to, bool forward, CancellationToken cancellationToken)
        {
            Calls.Add(new TransitionCall(from, to, forward));
            return Task.CompletedTask;
        }
    }

    private sealed class InvalidDetent : BottomSheetDetent
    {
        public InvalidDetent()
            : base("invalid")
        {
        }

        protected internal override double GetHeight(double availableHeight, double contentHeight)
            => double.NaN;
    }

    private sealed class ContentRelativeDetent : BottomSheetDetent
    {
        public ContentRelativeDetent()
            : base("content-relative")
        {
        }

        public double LastContentHeight { get; private set; }

        protected internal override double GetHeight(double availableHeight, double contentHeight)
        {
            LastContentHeight = contentHeight;
            return contentHeight / 2;
        }
    }

    private readonly record struct TransitionCall(Visual? From, Visual? To, bool Forward);

    private sealed class TestException : Exception
    {
    }
}
