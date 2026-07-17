using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls.Notifications;
using Avalonia.UnitTests;
using Xunit;

namespace Avalonia.Controls.UnitTests;

public class SystemNotificationTests : ScopedTestBase
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_Rejects_Invalid_Id(string? id)
    {
        Assert.Throws<ArgumentException>(() => new SystemNotification(id!, "Message"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_Rejects_Invalid_Message(string? message)
    {
        Assert.Throws<ArgumentException>(() => new SystemNotification("id", message!));
    }

    [Fact]
    public void Constructor_Preserves_Data()
    {
        var notification = new SystemNotification("export-complete", "The archive is ready.")
        {
            Title = "Export complete"
        };

        Assert.Equal("export-complete", notification.Id);
        Assert.Equal("Export complete", notification.Title);
        Assert.Equal("The archive is ready.", notification.Message);
    }

    [Fact]
    public async Task Unsupported_Manager_Reports_Unsupported_And_Rejects_Operations()
    {
        var manager = UnsupportedSystemNotificationManager.Instance;

        Assert.False(manager.IsSupported);
        Assert.Equal(
            SystemNotificationPermissionStatus.Unsupported,
            await manager.GetPermissionStatusAsync(TestContext.Current.CancellationToken));
        Assert.Equal(
            SystemNotificationPermissionStatus.Unsupported,
            await manager.RequestPermissionAsync(TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<NotSupportedException>(
            () => manager.ShowAsync(
                new SystemNotification("id", "Message"),
                TestContext.Current.CancellationToken).AsTask());
        await Assert.ThrowsAsync<NotSupportedException>(
            () => manager.RemoveAsync("id", TestContext.Current.CancellationToken).AsTask());
        await Assert.ThrowsAsync<NotSupportedException>(
            () => manager.RemoveAllKnownAsync(TestContext.Current.CancellationToken).AsTask());
    }

    [Fact]
    public async Task Unsupported_Manager_Honors_Cancellation()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var manager = UnsupportedSystemNotificationManager.Instance;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => manager.GetPermissionStatusAsync(source.Token).AsTask());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => manager.RequestPermissionAsync(source.Token).AsTask());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => manager.ShowAsync(new SystemNotification("id", "Message"), source.Token).AsTask());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => manager.RemoveAsync("id", source.Token).AsTask());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => manager.RemoveAllKnownAsync(source.Token).AsTask());
    }

    [Fact]
    public void Application_Uses_Unsupported_Manager_When_Platform_Has_No_Service()
    {
        using (UnitTestApplication.Start())
        {
            var manager = Application.Current!.SystemNotifications;

            Assert.Same(UnsupportedSystemNotificationManager.Instance, manager);
            Assert.False(manager.IsSupported);
        }
    }

    [Fact]
    public void Application_Resolves_And_Caches_Platform_Manager()
    {
        using (UnitTestApplication.Start())
        {
            var manager = new TestSystemNotificationManager();
            Assert.Same(
                UnsupportedSystemNotificationManager.Instance,
                Application.Current!.SystemNotifications);
            AvaloniaLocator.CurrentMutable
                .Bind<ISystemNotificationManager>()
                .ToConstant(manager);

            var service = Application.Current.SystemNotifications;
            Assert.IsType<CoordinatedSystemNotificationManager>(service);
            Assert.Same(service, Application.Current.SystemNotifications);
            Assert.Same(manager, Application.Current.TryGetFeature(typeof(ISystemNotificationManager)));
        }
    }

    [Fact]
    public async Task Application_Publishes_One_Coordinator_During_Concurrent_First_Access()
    {
        using (UnitTestApplication.Start())
        {
            AvaloniaLocator.CurrentMutable
                .Bind<ISystemNotificationManager>()
                .ToConstant(new TestSystemNotificationManager());

            var reads = Enumerable.Range(0, 32)
                .Select(_ => Task.Run(() => Application.Current!.SystemNotifications))
                .ToArray();
            var services = await Task.WhenAll(reads);

            Assert.All(services, service => Assert.Same(services[0], service));
        }
    }

    [Fact]
    public async Task Coordinator_Shares_One_Permission_Prompt_With_Independent_Cancellation()
    {
        var inner = new DelayedPermissionSystemNotificationManager();
        var manager = new CoordinatedSystemNotificationManager(inner);
        using var cancellation = new CancellationTokenSource();

        var canceledWait = manager.RequestPermissionAsync(cancellation.Token).AsTask();
        var completedWait = manager.RequestPermissionAsync(
            TestContext.Current.CancellationToken).AsTask();
        await inner.Entered.WaitAsync(TestContext.Current.CancellationToken);

        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => canceledWait);
        Assert.Equal(1, inner.RequestCount);

        inner.Complete();
        Assert.Equal(SystemNotificationPermissionStatus.Granted, await completedWait);
        Assert.Equal(1, inner.RequestCount);
    }

    [Fact]
    public async Task Coordinator_Allows_A_New_Permission_Request_After_A_Failure()
    {
        var inner = new RetryPermissionSystemNotificationManager();
        var manager = new CoordinatedSystemNotificationManager(inner);

        await Assert.ThrowsAsync<TestException>(
            () => manager.RequestPermissionAsync(TestContext.Current.CancellationToken).AsTask());

        Assert.Equal(
            SystemNotificationPermissionStatus.Granted,
            await manager.RequestPermissionAsync(TestContext.Current.CancellationToken));
        Assert.Equal(2, inner.RequestCount);
    }

    [Fact]
    public async Task Coordinator_Keeps_Canceled_Show_Cleanup_Ahead_Of_Newer_Replacement()
    {
        var inner = new SequencedSystemNotificationManager();
        var manager = new CoordinatedSystemNotificationManager(inner);
        using var cancellation = new CancellationTokenSource();
        var first = manager.ShowAsync(
            new SystemNotification("shared", "first"),
            cancellation.Token).AsTask();

        await inner.FirstShowEntered.WaitAsync(TestContext.Current.CancellationToken);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);

        var second = manager.ShowAsync(
            new SystemNotification("shared", "second"),
            TestContext.Current.CancellationToken).AsTask();
        Assert.Equal(1, inner.ShowCount);

        inner.ReleaseFirstShow();
        await second;

        Assert.Equal(new[]
        {
            "show:first:start",
            "show:first:end",
            "remove:shared",
            "show:second:start",
            "show:second:end"
        }, inner.Operations);
    }

    [Fact]
    public async Task Coordinator_Does_Not_Submit_An_Operation_Canceled_While_Queued()
    {
        var inner = new SequencedSystemNotificationManager();
        var manager = new CoordinatedSystemNotificationManager(inner);
        var first = manager.ShowAsync(
            new SystemNotification("first", "first"),
            TestContext.Current.CancellationToken).AsTask();
        await inner.FirstShowEntered.WaitAsync(TestContext.Current.CancellationToken);

        using var cancellation = new CancellationTokenSource();
        var queued = manager.RemoveAsync("queued", cancellation.Token).AsTask();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued);

        inner.ReleaseFirstShow();
        await first;

        Assert.DoesNotContain("remove:queued", inner.Operations);
    }

    [Fact]
    public async Task Id_Tracker_Removes_Only_Ids_Accepted_By_The_Manager_Instance()
    {
        var tracker = new SystemNotificationIdTracker();
        var removed = new List<string>();
        tracker.MarkShown("first");
        tracker.MarkShown("second");
        tracker.MarkShown("first");

        await tracker.RemoveAllAsync((id, _) =>
        {
            removed.Add(id);
            tracker.MarkRemoved(id);
            return ValueTask.CompletedTask;
        }, TestContext.Current.CancellationToken);

        Assert.Equal(new[] { "first", "second" }, removed.OrderBy(x => x));
        Assert.Empty(tracker.Snapshot());
    }

    [Fact]
    public async Task Id_Tracker_Preserves_Unremoved_Ids_After_A_Failure()
    {
        var tracker = new SystemNotificationIdTracker();
        tracker.MarkShown("first");
        tracker.MarkShown("second");

        await Assert.ThrowsAsync<TestException>(() => tracker.RemoveAllAsync((id, _) =>
        {
            if (id == "first")
                return ValueTask.FromException(new TestException());

            tracker.MarkRemoved(id);
            return ValueTask.CompletedTask;
        }, TestContext.Current.CancellationToken).AsTask());

        Assert.Contains("first", tracker.Snapshot());
    }

    private sealed class TestSystemNotificationManager : ISystemNotificationManager
    {
        public bool IsSupported => true;

        public ValueTask<SystemNotificationPermissionStatus> GetPermissionStatusAsync(
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(SystemNotificationPermissionStatus.Granted);

        public ValueTask<SystemNotificationPermissionStatus> RequestPermissionAsync(
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(SystemNotificationPermissionStatus.Granted);

        public ValueTask ShowAsync(
            SystemNotification notification,
            CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask RemoveAsync(
            string notificationId,
            CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask RemoveAllKnownAsync(CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;
    }

    private sealed class SequencedSystemNotificationManager : ISystemNotificationManager
    {
        private readonly TaskCompletionSource _firstShowEntered = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _releaseFirstShow = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int _showCount;

        public bool IsSupported => true;

        public Task FirstShowEntered => _firstShowEntered.Task;

        public int ShowCount => Volatile.Read(ref _showCount);

        public List<string> Operations { get; } = new();

        public void ReleaseFirstShow() => _releaseFirstShow.TrySetResult();

        public ValueTask<SystemNotificationPermissionStatus> GetPermissionStatusAsync(
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(SystemNotificationPermissionStatus.Granted);

        public ValueTask<SystemNotificationPermissionStatus> RequestPermissionAsync(
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(SystemNotificationPermissionStatus.Granted);

        public async ValueTask ShowAsync(
            SystemNotification notification,
            CancellationToken cancellationToken = default)
        {
            var show = Interlocked.Increment(ref _showCount);
            Operations.Add($"show:{notification.Message}:start");
            if (show == 1)
            {
                _firstShowEntered.TrySetResult();
                await _releaseFirstShow.Task.WaitAsync(cancellationToken);
            }

            Operations.Add($"show:{notification.Message}:end");
        }

        public ValueTask RemoveAsync(
            string notificationId,
            CancellationToken cancellationToken = default)
        {
            Operations.Add($"remove:{notificationId}");
            return ValueTask.CompletedTask;
        }

        public ValueTask RemoveAllKnownAsync(CancellationToken cancellationToken = default)
        {
            Operations.Add("remove:all");
            return ValueTask.CompletedTask;
        }
    }

    private sealed class DelayedPermissionSystemNotificationManager : ISystemNotificationManager
    {
        private readonly TaskCompletionSource _entered = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<SystemNotificationPermissionStatus> _completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int _requestCount;

        public bool IsSupported => true;
        public Task Entered => _entered.Task;
        public int RequestCount => Volatile.Read(ref _requestCount);

        public void Complete() =>
            _completion.TrySetResult(SystemNotificationPermissionStatus.Granted);

        public ValueTask<SystemNotificationPermissionStatus> GetPermissionStatusAsync(
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(SystemNotificationPermissionStatus.NotDetermined);

        public ValueTask<SystemNotificationPermissionStatus> RequestPermissionAsync(
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _requestCount);
            _entered.TrySetResult();
            return new ValueTask<SystemNotificationPermissionStatus>(_completion.Task);
        }

        public ValueTask ShowAsync(
            SystemNotification notification,
            CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask RemoveAsync(
            string notificationId,
            CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask RemoveAllKnownAsync(CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;
    }

    private sealed class RetryPermissionSystemNotificationManager : ISystemNotificationManager
    {
        private int _requestCount;

        public bool IsSupported => true;
        public int RequestCount => Volatile.Read(ref _requestCount);

        public ValueTask<SystemNotificationPermissionStatus> GetPermissionStatusAsync(
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(SystemNotificationPermissionStatus.NotDetermined);

        public ValueTask<SystemNotificationPermissionStatus> RequestPermissionAsync(
            CancellationToken cancellationToken = default) =>
            Interlocked.Increment(ref _requestCount) == 1
                ? ValueTask.FromException<SystemNotificationPermissionStatus>(new TestException())
                : ValueTask.FromResult(SystemNotificationPermissionStatus.Granted);

        public ValueTask ShowAsync(
            SystemNotification notification,
            CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask RemoveAsync(
            string notificationId,
            CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask RemoveAllKnownAsync(CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;
    }

    private sealed class TestException : Exception
    {
    }
}
