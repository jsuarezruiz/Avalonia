using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Platform;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Avalonia.Controls;

/// <summary>
/// Presents a modal, edge-attached surface that can rest at one or more detents.
/// </summary>
[PseudoClasses(":open", ":dragging")]
[TemplatePart("PART_DragHandle", typeof(Control))]
public class BottomSheet : HeaderedContentControl
{
    /// <summary>Defines the <see cref="SelectedDetent"/> property.</summary>
    public static readonly StyledProperty<BottomSheetDetent?> SelectedDetentProperty =
        AvaloniaProperty.Register<BottomSheet, BottomSheetDetent?>(
            nameof(SelectedDetent),
            coerce: CoerceSelectedDetent);

    /// <summary>Defines the <see cref="IsLightDismissEnabled"/> property.</summary>
    public static readonly StyledProperty<bool> IsLightDismissEnabledProperty =
        AvaloniaProperty.Register<BottomSheet, bool>(nameof(IsLightDismissEnabled), true);

    /// <summary>Defines the <see cref="IsEscapeEnabled"/> property.</summary>
    public static readonly StyledProperty<bool> IsEscapeEnabledProperty =
        AvaloniaProperty.Register<BottomSheet, bool>(nameof(IsEscapeEnabled), true);

    /// <summary>Defines the <see cref="IsSystemBackEnabled"/> property.</summary>
    public static readonly StyledProperty<bool> IsSystemBackEnabledProperty =
        AvaloniaProperty.Register<BottomSheet, bool>(nameof(IsSystemBackEnabled), true);

    /// <summary>Defines the <see cref="IsDragEnabled"/> property.</summary>
    public static readonly StyledProperty<bool> IsDragEnabledProperty =
        AvaloniaProperty.Register<BottomSheet, bool>(nameof(IsDragEnabled), true);

    /// <summary>Defines the <see cref="IsDragDismissEnabled"/> property.</summary>
    public static readonly StyledProperty<bool> IsDragDismissEnabledProperty =
        AvaloniaProperty.Register<BottomSheet, bool>(nameof(IsDragDismissEnabled), true);

    /// <summary>Defines the <see cref="Transition"/> property.</summary>
    public static readonly StyledProperty<IPageTransition?> TransitionProperty =
        AvaloniaProperty.Register<BottomSheet, IPageTransition?>(nameof(Transition));

    /// <summary>Defines the read-only <see cref="ActualSheetHeight"/> property.</summary>
    public static readonly DirectProperty<BottomSheet, double> ActualSheetHeightProperty =
        AvaloniaProperty.RegisterDirect<BottomSheet, double>(
            nameof(ActualSheetHeight), o => o.ActualSheetHeight);

    /// <summary>Defines the read-only <see cref="IsOpen"/> property.</summary>
    public static readonly DirectProperty<BottomSheet, bool> IsOpenProperty =
        AvaloniaProperty.RegisterDirect<BottomSheet, bool>(nameof(IsOpen), o => o.IsOpen);

    /// <summary>Defines the read-only <see cref="SafeAreaPadding"/> property.</summary>
    public static readonly DirectProperty<BottomSheet, Thickness> SafeAreaPaddingProperty =
        AvaloniaProperty.RegisterDirect<BottomSheet, Thickness>(
            nameof(SafeAreaPadding), o => o.SafeAreaPadding);

    private const double FlickVelocity = 800;
    private readonly BottomSheetDetentCollection _detents;
    private ContentDialog? _host;
    private Control? _dragHandle;
    private TaskCompletionSource<BottomSheetResult>? _resultCompletion;
    private BottomSheetDismissReason? _programmaticReason;
    private object? _pendingValue;
    private bool _pendingHasValue;
    private ContentDialogDismissReason? _acceptedHostDismissReason;
    private BottomSheetDismissReason? _acceptedDismissReason;
    private object? _acceptedValue;
    private bool _acceptedHasValue;
    private Point _dragStartPoint;
    private Point _lastDragPoint;
    private long _lastDragTimestamp;
    private double _lastDragVelocity;
    private double _dragStartHeight;
    private double _dragMaximumHeight;
    private double? _dragHeight;
    private double _naturalHeight;
    private double _actualSheetHeight;
    private Thickness _safeAreaPadding;
    private IInsetsManager? _insetsManager;
    private bool _isDragging;
    private bool _isOpen;
    private bool _dragMeasureQueued;

    /// <summary>Initializes a new instance of the <see cref="BottomSheet"/> class.</summary>
    public BottomSheet()
    {
        _detents = new BottomSheetDetentCollection(this);
        _detents.Add(BottomSheetDetent.Content);
        _detents.Add(BottomSheetDetent.Expanded);
        _detents.CollectionChanged += DetentsChanged;
    }

    /// <summary>Gets the available detents.</summary>
    public IAvaloniaList<BottomSheetDetent> Detents => _detents;

    /// <summary>Gets or sets the currently selected detent.</summary>
    public BottomSheetDetent? SelectedDetent
    {
        get => GetValue(SelectedDetentProperty);
        set => SetValue(SelectedDetentProperty, value);
    }

    /// <summary>Gets or sets whether pressing the backdrop dismisses the sheet.</summary>
    public bool IsLightDismissEnabled
    {
        get => GetValue(IsLightDismissEnabledProperty);
        set => SetValue(IsLightDismissEnabledProperty, value);
    }

    /// <summary>Gets or sets whether Escape dismisses the sheet.</summary>
    public bool IsEscapeEnabled
    {
        get => GetValue(IsEscapeEnabledProperty);
        set => SetValue(IsEscapeEnabledProperty, value);
    }

    /// <summary>Gets or sets whether a platform system-back request dismisses the sheet.</summary>
    public bool IsSystemBackEnabled
    {
        get => GetValue(IsSystemBackEnabledProperty);
        set => SetValue(IsSystemBackEnabledProperty, value);
    }

    /// <summary>Gets or sets whether pointer dragging the handle changes detents.</summary>
    public bool IsDragEnabled
    {
        get => GetValue(IsDragEnabledProperty);
        set => SetValue(IsDragEnabledProperty, value);
    }

    /// <summary>Gets or sets whether dragging below the smallest detent can dismiss the sheet.</summary>
    public bool IsDragDismissEnabled
    {
        get => GetValue(IsDragDismissEnabledProperty);
        set => SetValue(IsDragDismissEnabledProperty, value);
    }

    /// <summary>
    /// Gets or sets the reusable visual transition used to open and close the sheet.
    /// A null local value disables lifecycle transitions.
    /// </summary>
    public IPageTransition? Transition
    {
        get => GetValue(TransitionProperty);
        set => SetValue(TransitionProperty, value);
    }

    /// <summary>Gets the currently arranged sheet height.</summary>
    public double ActualSheetHeight
    {
        get => _actualSheetHeight;
        private set => SetAndRaise(ActualSheetHeightProperty, ref _actualSheetHeight, value);
    }

    /// <summary>Gets whether the sheet is open.</summary>
    public bool IsOpen
    {
        get => _isOpen;
        private set => SetAndRaise(IsOpenProperty, ref _isOpen, value);
    }

    /// <summary>Gets the safe-area padding supplied by the owning top level.</summary>
    public Thickness SafeAreaPadding
    {
        get => _safeAreaPadding;
        private set => SetAndRaise(SafeAreaPaddingProperty, ref _safeAreaPadding, value);
    }

    /// <summary>Occurs before the sheet is attached to its owner.</summary>
    public event EventHandler? Opening;

    /// <summary>Occurs after the sheet's opening transition completes.</summary>
    public event EventHandler? Opened;

    /// <summary>Occurs before the sheet closes and can be canceled or deferred.</summary>
    public event EventHandler<BottomSheetClosingEventArgs>? Closing;

    /// <summary>Occurs after the sheet is detached from its owner.</summary>
    public event EventHandler<BottomSheetClosedEventArgs>? Closed;

    /// <summary>Occurs when <see cref="SelectedDetent"/> changes.</summary>
    public event EventHandler<BottomSheetDetentChangedEventArgs>? SelectedDetentChanged;

    /// <summary>Shows the bottom sheet over the specified top level.</summary>
    /// <param name="owner">The top level that owns the modal sheet.</param>
    /// <param name="cancellationToken">A token that force-closes the sheet and cancels the returned task.</param>
    /// <returns>The result and reason produced when the sheet closes.</returns>
    public Task<BottomSheetResult> ShowAsync(
        TopLevel owner,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(owner);
        Dispatcher.UIThread.VerifyAccess();

        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled<BottomSheetResult>(cancellationToken);

        if (_resultCompletion is not null || _host is { IsOpen: true })
            throw new InvalidOperationException("The BottomSheet is already open or closing.");

        ValidateDetents();

        if (SelectedDetent is null)
            SetCurrentValue(SelectedDetentProperty, _detents[0]);

        if (!_detents.Contains(SelectedDetent!))
            throw new InvalidOperationException("SelectedDetent must be present in Detents.");

        EnsureHost();
        var resultCompletion = _resultCompletion = new TaskCompletionSource<BottomSheetResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingValue = null;
        _pendingHasValue = false;
        _programmaticReason = null;
        ResetAcceptedClose();
        _host!.IsLightDismissEnabled = IsLightDismissEnabled;
        _host.IsEscapeEnabled = IsEscapeEnabled;
        _host.IsSystemBackEnabled = IsSystemBackEnabled;

        AttachInsets(owner);
        try
        {
            var hostTask = _host.ShowAsync(owner, cancellationToken);
            if (hostTask.IsCanceled)
            {
                ResetFailedShow(resultCompletion);
            }
            return AwaitResultAsync(hostTask, resultCompletion);
        }
        catch
        {
            ResetFailedShow(resultCompletion);
            throw;
        }
    }

    private async Task<BottomSheetResult> AwaitResultAsync(
        Task<ContentDialogResult> hostTask,
        TaskCompletionSource<BottomSheetResult> resultCompletion)
    {
        try
        {
            await hostTask;
            return await resultCompletion.Task;
        }
        catch
        {
            // Normal forced closure (including caller cancellation) performs all
            // cleanup synchronously through HostClosed before the host task
            // completes. Avoid posting redundant cleanup back to the UI thread:
            // doing so can unnecessarily delay the canceled task and its caller.
            if (ReferenceEquals(_resultCompletion, resultCompletion))
            {
                if (Dispatcher.UIThread.CheckAccess())
                    ResetFailedShow(resultCompletion);
                else
                    await Dispatcher.UIThread.InvokeAsync(() => ResetFailedShow(resultCompletion));
            }
            throw;
        }
    }

    private void ResetFailedShow(TaskCompletionSource<BottomSheetResult> resultCompletion)
    {
        if (ReferenceEquals(_resultCompletion, resultCompletion))
            _resultCompletion = null;
        _pendingValue = null;
        _pendingHasValue = false;
        _programmaticReason = null;
        ResetAcceptedClose();
        ResetDragState();
        DetachInsets();
        IsOpen = false;
        PseudoClasses.Set(":open", false);
    }

    /// <summary>Closes the sheet with no result value.</summary>
    public void Hide() => HideCore(null, false);

    /// <summary>Closes the sheet with an application-defined result value.</summary>
    public void Hide(object? result) => HideCore(result, true);

    private void HideCore(object? result, bool hasValue)
    {
        Dispatcher.UIThread.VerifyAccess();

        // The Opening event runs before ContentDialog.IsOpen changes. Use the active
        // operation as the guard so an Opening handler can still dismiss the sheet.
        if (_host is null || _resultCompletion is null)
            return;

        _pendingValue = result;
        _pendingHasValue = hasValue;
        _programmaticReason = BottomSheetDismissReason.Programmatic;
        _host.Hide();
    }

    /// <summary>Raises the <see cref="Opening"/> event.</summary>
    protected virtual void OnOpening() => Opening?.Invoke(this, EventArgs.Empty);

    /// <summary>Raises the <see cref="Opened"/> event.</summary>
    protected virtual void OnOpened() => Opened?.Invoke(this, EventArgs.Empty);

    /// <summary>Raises the <see cref="Closing"/> event.</summary>
    protected virtual void OnClosing(BottomSheetClosingEventArgs e) => Closing?.Invoke(this, e);

    /// <summary>Raises the <see cref="Closed"/> event.</summary>
    protected virtual void OnClosed(BottomSheetClosedEventArgs e) => Closed?.Invoke(this, e);

    /// <summary>Raises the <see cref="SelectedDetentChanged"/> event.</summary>
    protected virtual void OnSelectedDetentChanged(BottomSheetDetentChangedEventArgs e)
        => SelectedDetentChanged?.Invoke(this, e);

    protected override Size MeasureOverride(Size availableSize)
    {
        var owner = TopLevel.GetTopLevel(this);
        var availableWidth = double.IsFinite(availableSize.Width)
            ? availableSize.Width
            : owner?.ClientSize.Width ?? 0;
        var availableHeight = owner?.ClientSize.Height ?? availableSize.Height;

        if (!double.IsFinite(availableHeight) || availableHeight <= 0)
            return base.MeasureOverride(availableSize);

        if (_dragHeight is { } dragHeight)
        {
            var constrainedHeight = Math.Clamp(dragHeight, 0, availableHeight);
            ActualSheetHeight = constrainedHeight;
            var constrained = base.MeasureOverride(new Size(availableWidth, constrainedHeight));
            return new Size(Math.Min(constrained.Width, availableWidth), constrainedHeight);
        }

        var natural = base.MeasureOverride(new Size(availableWidth, double.PositiveInfinity));
        _naturalHeight = natural.Height;
        var height = ResolveHeight(SelectedDetent ?? _detents.FirstOrDefault(), availableHeight, natural.Height);
        height = Math.Clamp(height, 0, availableHeight);
        ActualSheetHeight = height;

        _ = base.MeasureOverride(new Size(availableWidth, height));
        return new Size(Math.Min(natural.Width, availableWidth), height);
    }

    protected override AutomationPeer OnCreateAutomationPeer()
        => new BottomSheetAutomationPeer(this);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        UnsubscribeDragHandle();
        base.OnApplyTemplate(e);

        _dragHandle = e.NameScope.Find<Control>("PART_DragHandle");
        if (_dragHandle is not null)
        {
            _dragHandle.Focusable = true;
            _dragHandle.PointerPressed += DragStarted;
            _dragHandle.PointerMoved += DragMoved;
            _dragHandle.PointerReleased += DragEnded;
            _dragHandle.PointerCaptureLost += DragCaptureLost;
            _dragHandle.KeyDown += DragHandleKeyDown;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SelectedDetentProperty && change.NewValue is BottomSheetDetent detent)
        {
            _dragHeight = null;
            InvalidateMeasure();
            OnSelectedDetentChanged(
                new BottomSheetDetentChangedEventArgs(change.OldValue as BottomSheetDetent, detent));
        }
        else if (change.Property == TransitionProperty && _host is not null)
        {
            _host.Transition = change.GetNewValue<IPageTransition?>();
        }
        else if (change.Property == IsLightDismissEnabledProperty && _host is not null)
        {
            _host.IsLightDismissEnabled = change.GetNewValue<bool>();
        }
        else if (change.Property == IsEscapeEnabledProperty && _host is not null)
        {
            _host.IsEscapeEnabled = change.GetNewValue<bool>();
        }
        else if (change.Property == IsSystemBackEnabledProperty && _host is not null)
        {
            _host.IsSystemBackEnabled = change.GetNewValue<bool>();
        }
    }

    private void EnsureHost()
    {
        if (_host is not null)
            return;

        if (Parent is not null || VisualParent is not null)
        {
            throw new InvalidOperationException(
                "A BottomSheet must not have a logical or visual parent when it is first shown.");
        }

        _host = new ContentDialog
        {
            Content = this,
            CancelButton = ContentDialogButton.None
        };
        _host.Classes.Add("bottom-sheet");
        if (IsSet(TransitionProperty))
            _host.Transition = Transition;
        _host.Opening += HostOpening;
        _host.Opened += HostOpened;
        _host.Closing += HostClosing;
        _host.Closed += HostClosed;
    }

    private void HostOpening(object? sender, EventArgs e)
    {
        IsOpen = true;
        PseudoClasses.Set(":open", true);
        OnOpening();
    }

    private void HostOpened(object? sender, EventArgs e)
    {
        InvalidateMeasure();
        OnOpened();
    }

    private void HostClosing(object? sender, ContentDialogClosingEventArgs e)
    {
        var reason = MapReason(e.DismissReason);
        var value = reason == BottomSheetDismissReason.Programmatic ? _pendingValue : null;
        var hasValue = reason == BottomSheetDismissReason.Programmatic && _pendingHasValue;
        _acceptedHostDismissReason = e.DismissReason;
        _acceptedDismissReason = reason;
        _acceptedValue = value;
        _acceptedHasValue = hasValue;
        OnClosing(new BottomSheetClosingEventArgs(
            _acceptedValue,
            _acceptedHasValue,
            _acceptedDismissReason.Value,
            e));
    }

    private void HostClosed(object? sender, ContentDialogClosedEventArgs e)
    {
        var acceptedClose = _acceptedHostDismissReason == e.DismissReason;
        var reason = acceptedClose && _acceptedDismissReason is { } acceptedReason
            ? acceptedReason
            : MapReason(e.DismissReason);
        var value = acceptedClose ? _acceptedValue : null;
        var hasValue = acceptedClose && _acceptedHasValue;
        var result = new BottomSheetResult(value, hasValue, reason);
        var resultCompletion = _resultCompletion;
        _resultCompletion = null;
        _pendingValue = null;
        _pendingHasValue = false;
        _programmaticReason = null;
        ResetAcceptedClose();
        ResetDragState();
        DetachInsets();
        IsOpen = false;
        PseudoClasses.Set(":open", false);
        try
        {
            OnClosed(new BottomSheetClosedEventArgs(result));
        }
        finally
        {
            resultCompletion?.TrySetResult(result);
        }
    }

    private BottomSheetDismissReason MapReason(ContentDialogDismissReason reason)
    {
        if (reason == ContentDialogDismissReason.Programmatic && _programmaticReason is { } explicitReason)
            return explicitReason;

        return reason switch
        {
            ContentDialogDismissReason.LightDismiss => BottomSheetDismissReason.LightDismiss,
            ContentDialogDismissReason.Cancellation => BottomSheetDismissReason.Cancellation,
            ContentDialogDismissReason.Escape => BottomSheetDismissReason.Escape,
            ContentDialogDismissReason.SystemBack => BottomSheetDismissReason.SystemBack,
            ContentDialogDismissReason.OwnerClosed => BottomSheetDismissReason.OwnerClosed,
            _ => BottomSheetDismissReason.Programmatic
        };
    }

    private void DragStarted(object? sender, PointerPressedEventArgs e)
    {
        if (!IsDragEnabled || !IsOpen || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        _isDragging = true;
        _dragStartPoint = GetDragPosition(e);
        _lastDragPoint = _dragStartPoint;
        _dragStartHeight = ActualSheetHeight;
        _lastDragTimestamp = Stopwatch.GetTimestamp();
        _lastDragVelocity = 0;
        _dragHeight = ActualSheetHeight;
        var ownerHeight = TopLevel.GetTopLevel(this)?.ClientSize.Height ?? ActualSheetHeight;
        _dragMaximumHeight = ResolveMaximumDetentHeight(ownerHeight);
        PseudoClasses.Set(":dragging", true);
        e.Pointer.Capture(_dragHandle);
        e.Handled = true;
    }

    private void DragMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDragging)
            return;

        var point = GetDragPosition(e);
        var timestamp = Stopwatch.GetTimestamp();
        var elapsed = Stopwatch.GetElapsedTime(_lastDragTimestamp, timestamp).TotalSeconds;
        if (elapsed > 0)
            _lastDragVelocity = (point.Y - _lastDragPoint.Y) / elapsed;

        _dragHeight = Math.Clamp(
            _dragStartHeight - (point.Y - _dragStartPoint.Y),
            0,
            _dragMaximumHeight);
        _lastDragPoint = point;
        _lastDragTimestamp = timestamp;
        QueueDragMeasure();
        e.Handled = true;
    }

    private void DragEnded(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isDragging)
            return;

        var point = GetDragPosition(e);
        var elapsed = Stopwatch.GetElapsedTime(_lastDragTimestamp).TotalSeconds;
        var releaseDelta = point.Y - _lastDragPoint.Y;
        var velocity = elapsed > 0 && Math.Abs(releaseDelta) > double.Epsilon
            ? releaseDelta / elapsed
            : elapsed <= 0.12 ? _lastDragVelocity : 0;
        FinishDrag(velocity);
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void DragCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (_isDragging)
            FinishDrag(0);
    }

    private void FinishDrag(double velocity)
    {
        _isDragging = false;
        _dragMaximumHeight = 0;
        _lastDragVelocity = 0;
        PseudoClasses.Set(":dragging", false);

        var ownerHeight = TopLevel.GetTopLevel(this)?.ClientSize.Height ?? ActualSheetHeight;
        var resolved = ResolveDetents(ownerHeight).OrderBy(x => x.Height).ToArray();
        var current = _dragHeight ?? ActualSheetHeight;

        if (resolved.Length == 0)
        {
            _dragHeight = null;
            return;
        }

        var smallest = resolved[0].Height;
        if (IsDragDismissEnabled &&
            (current < smallest * 0.6 || (velocity > FlickVelocity && current <= smallest * 1.1)))
        {
            _pendingValue = null;
            _pendingHasValue = false;
            _programmaticReason = BottomSheetDismissReason.DragDismiss;
            _dragHeight = null;
            _host?.Hide();
            return;
        }

        ResolvedDetent selected;
        if (Math.Abs(velocity) > FlickVelocity)
        {
            selected = velocity > 0
                ? resolved.LastOrDefault(x => x.Height < current) ?? resolved[0]
                : resolved.FirstOrDefault(x => x.Height > current) ?? resolved[^1];
        }
        else
        {
            selected = resolved.MinBy(x => Math.Abs(x.Height - current))!;
        }

        _dragHeight = null;
        SetCurrentValue(SelectedDetentProperty, selected.Detent);
        InvalidateMeasure();
    }

    private double ResolveMaximumDetentHeight(double availableHeight)
    {
        var maximum = 0d;
        foreach (var detent in _detents)
            maximum = Math.Max(maximum, ResolveHeight(detent, availableHeight, _naturalHeight));

        return maximum > 0 ? maximum : availableHeight;
    }

    private IEnumerable<ResolvedDetent> ResolveDetents(double availableHeight)
    {
        foreach (var detent in _detents)
        {
            var height = ResolveHeight(detent, availableHeight, _naturalHeight);
            yield return new ResolvedDetent(detent, height);
        }
    }

    internal List<BottomSheetDetent> GetDetentsByHeight()
    {
        var availableHeight = TopLevel.GetTopLevel(this)?.ClientSize.Height ?? ActualSheetHeight;
        if (!double.IsFinite(availableHeight) || availableHeight <= 0)
            availableHeight = 1;

        return ResolveDetents(availableHeight)
            .OrderBy(detent => detent.Height)
            .Select(detent => detent.Detent)
            .ToList();
    }

    private static double ResolveHeight(
        BottomSheetDetent? detent,
        double availableHeight,
        double contentHeight)
    {
        if (detent is null)
            return Math.Min(availableHeight, contentHeight);

        var height = detent.GetHeight(availableHeight, contentHeight);
        if (!double.IsFinite(height) || height < 0 ||
            (height == 0 && !ReferenceEquals(detent, BottomSheetDetent.Content)))
        {
            throw new InvalidOperationException(
                $"Bottom-sheet detent '{detent.Id}' returned an invalid height.");
        }

        return Math.Min(availableHeight, height);
    }

    private void QueueDragMeasure()
    {
        if (_dragMeasureQueued)
            return;

        _dragMeasureQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _dragMeasureQueued = false;
            if (_isDragging && _dragHeight is not null)
                InvalidateMeasure();
        }, DispatcherPriority.Render);
    }

    private static BottomSheetDetent? CoerceSelectedDetent(
        AvaloniaObject instance,
        BottomSheetDetent? value)
    {
        var sheet = (BottomSheet)instance;
        if (!sheet.IsOpen)
            return value;

        if (sheet._detents.Count == 0)
            throw new InvalidOperationException("An open BottomSheet must define at least one detent.");

        if (value is null)
            return sheet._detents[0];

        if (!sheet._detents.Contains(value))
            throw new InvalidOperationException("SelectedDetent must be present in Detents.");

        return value;
    }

    private void ValidateDetents(double availableHeight = 0, double contentHeight = 0)
        => ValidateDetents(_detents, availableHeight, contentHeight);

    private static void ValidateDetents(
        IEnumerable<BottomSheetDetent> detents,
        double availableHeight,
        double contentHeight)
    {
        var materialized = detents as IReadOnlyCollection<BottomSheetDetent> ?? detents.ToArray();
        if (materialized.Count == 0)
            throw new InvalidOperationException("A BottomSheet must define at least one detent.");
        if (materialized.Any(x => x is null))
            throw new InvalidOperationException("BottomSheet detents cannot contain null.");
        if (materialized.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != materialized.Count)
            throw new InvalidOperationException("BottomSheet detent IDs must be unique.");

        if (double.IsFinite(availableHeight) && availableHeight > 0)
        {
            foreach (var detent in materialized)
                _ = ResolveHeight(detent, availableHeight, contentHeight);
        }
    }

    private void ValidateLiveDetentMutation(IEnumerable<BottomSheetDetent> detents)
    {
        if (!IsOpen)
            return;

        var availableHeight = TopLevel.GetTopLevel(this)?.ClientSize.Height ?? 0;
        if (!double.IsFinite(availableHeight) || availableHeight <= 0)
            availableHeight = Math.Max(ActualSheetHeight, 1);
        ValidateDetents(detents, availableHeight, _naturalHeight);
    }

    private void DetentsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _dragHeight = null;

        if (_detents.Count > 0 &&
            (SelectedDetent is null || !_detents.Contains(SelectedDetent)))
        {
            SetCurrentValue(SelectedDetentProperty, _detents[0]);
        }

        InvalidateMeasure();
    }

    private void ResetAcceptedClose()
    {
        _acceptedHostDismissReason = null;
        _acceptedDismissReason = null;
        _acceptedValue = null;
        _acceptedHasValue = false;
    }

    private void AttachInsets(TopLevel owner)
    {
        DetachInsets();
        _insetsManager = owner.InsetsManager;
        if (_insetsManager is not null)
        {
            SafeAreaPadding = _insetsManager.SafeAreaPadding;
            _insetsManager.SafeAreaChanged += InsetsManagerSafeAreaChanged;
        }
    }

    private void DetachInsets()
    {
        if (_insetsManager is not null)
            _insetsManager.SafeAreaChanged -= InsetsManagerSafeAreaChanged;

        _insetsManager = null;
        SafeAreaPadding = default;
    }

    private void InsetsManagerSafeAreaChanged(object? sender, SafeAreaChangedArgs e)
    {
        SafeAreaPadding = e.SafeAreaPadding;
        InvalidateMeasure();
    }

    private Point GetDragPosition(PointerEventArgs e)
    {
        Visual relativeTo = (Visual?)TopLevel.GetTopLevel(this) ?? this;
        return e.GetPosition(relativeTo);
    }

    private void ResetDragState()
    {
        _isDragging = false;
        _dragHeight = null;
        _dragMaximumHeight = 0;
        _lastDragVelocity = 0;
        PseudoClasses.Set(":dragging", false);
    }

    private void UnsubscribeDragHandle()
    {
        if (_dragHandle is null)
            return;

        _dragHandle.PointerPressed -= DragStarted;
        _dragHandle.PointerMoved -= DragMoved;
        _dragHandle.PointerReleased -= DragEnded;
        _dragHandle.PointerCaptureLost -= DragCaptureLost;
        _dragHandle.KeyDown -= DragHandleKeyDown;
    }

    private void DragHandleKeyDown(object? sender, KeyEventArgs e)
    {
        var detents = GetDetentsByHeight();
        if (detents.Count == 0)
            return;

        var current = SelectedDetent is { } selected ? detents.IndexOf(selected) : 0;
        if (current < 0)
            current = 0;
        var next = e.Key switch
        {
            Key.Up or Key.PageUp => Math.Min(current + 1, detents.Count - 1),
            Key.Down or Key.PageDown => Math.Max(current - 1, 0),
            Key.Home => 0,
            Key.End => detents.Count - 1,
            _ => current
        };

        if (next == current && e.Key is not (Key.Home or Key.End))
            return;

        e.Handled = true;
        SetCurrentValue(SelectedDetentProperty, detents[next]);
    }

    private sealed record ResolvedDetent(BottomSheetDetent Detent, double Height);

    private sealed class BottomSheetDetentCollection : IAvaloniaList<BottomSheetDetent>
    {
        private readonly BottomSheet _owner;
        private readonly AvaloniaList<BottomSheetDetent> _items = new();

        public BottomSheetDetentCollection(BottomSheet owner)
        {
            _owner = owner;
        }

        public event NotifyCollectionChangedEventHandler? CollectionChanged
        {
            add => _items.CollectionChanged += value;
            remove => _items.CollectionChanged -= value;
        }

        public event PropertyChangedEventHandler? PropertyChanged
        {
            add => _items.PropertyChanged += value;
            remove => _items.PropertyChanged -= value;
        }

        public int Count => _items.Count;

        public bool IsReadOnly => false;

        public BottomSheetDetent this[int index]
        {
            get => _items[index];
            set
            {
                ValidateFuture(index, 1, new[] { value });
                _items[index] = value;
            }
        }

        public void Add(BottomSheetDetent item)
        {
            ValidateFuture(Count, 0, new[] { item });
            _items.Add(item);
        }

        public void AddRange(IEnumerable<BottomSheetDetent> items)
            => InsertRange(Count, items);

        public void Insert(int index, BottomSheetDetent item)
        {
            ValidateFuture(index, 0, new[] { item });
            _items.Insert(index, item);
        }

        public void InsertRange(int index, IEnumerable<BottomSheetDetent> items)
        {
            ArgumentNullException.ThrowIfNull(items);
            var materialized = items.ToArray();
            ValidateFuture(index, 0, materialized);
            _items.InsertRange(index, materialized);
        }

        public void Clear()
        {
            _owner.ValidateLiveDetentMutation(Array.Empty<BottomSheetDetent>());
            _items.Clear();
        }

        public bool Remove(BottomSheetDetent item)
        {
            var index = IndexOf(item);
            if (index >= 0)
                ValidateFuture(index, 1, Array.Empty<BottomSheetDetent>());
            return _items.Remove(item);
        }

        public void RemoveAll(IEnumerable<BottomSheetDetent> items)
        {
            ArgumentNullException.ThrowIfNull(items);
            var removed = new HashSet<BottomSheetDetent>(items);
            _owner.ValidateLiveDetentMutation(this.Where(item => !removed.Contains(item)));
            _items.RemoveAll(removed);
        }

        public void RemoveAt(int index)
        {
            ValidateFuture(index, 1, Array.Empty<BottomSheetDetent>());
            _items.RemoveAt(index);
        }

        public void RemoveRange(int index, int count)
        {
            ValidateFuture(index, count, Array.Empty<BottomSheetDetent>());
            _items.RemoveRange(index, count);
        }

        public bool Contains(BottomSheetDetent item) => _items.Contains(item);

        public void CopyTo(BottomSheetDetent[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);

        public IEnumerator<BottomSheetDetent> GetEnumerator() => _items.GetEnumerator();

        public int IndexOf(BottomSheetDetent item) => _items.IndexOf(item);

        public void Move(int oldIndex, int newIndex) => _items.Move(oldIndex, newIndex);

        public void MoveRange(int oldIndex, int count, int newIndex) =>
            _items.MoveRange(oldIndex, count, newIndex);

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        private void ValidateFuture(
            int index,
            int removeCount,
            IReadOnlyCollection<BottomSheetDetent> inserted)
        {
            if (!_owner.IsOpen)
                return;

            var prospective = _items.ToList();
            if (removeCount > 0)
                prospective.RemoveRange(index, removeCount);
            prospective.InsertRange(index, inserted);
            _owner.ValidateLiveDetentMutation(prospective);
        }
    }
}
