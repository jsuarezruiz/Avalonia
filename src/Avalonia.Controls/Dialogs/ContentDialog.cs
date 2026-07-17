using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Animation;
using Avalonia.Automation.Peers;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Logging;
using Avalonia.Reactive;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Avalonia.Controls;

/// <summary>
/// Presents arbitrary content in an asynchronous modal dialog owned by a <see cref="TopLevel"/>.
/// </summary>
[PseudoClasses(
    ":open",
    ":opening",
    ":closing",
    ":primary",
    ":secondary",
    ":close",
    ":primary-default",
    ":secondary-default",
    ":close-default")]
[TemplatePart("PART_Backdrop", typeof(Control))]
[TemplatePart("PART_DialogSurface", typeof(Control))]
[TemplatePart("PART_PrimaryButton", typeof(Button))]
[TemplatePart("PART_SecondaryButton", typeof(Button))]
[TemplatePart("PART_CloseButton", typeof(Button))]
public class ContentDialog : ContentControl, IFocusScope
{
    /// <summary>Defines the <see cref="Title"/> property.</summary>
    public static readonly StyledProperty<object?> TitleProperty =
        AvaloniaProperty.Register<ContentDialog, object?>(nameof(Title));

    /// <summary>Defines the <see cref="TitleTemplate"/> property.</summary>
    public static readonly StyledProperty<IDataTemplate?> TitleTemplateProperty =
        AvaloniaProperty.Register<ContentDialog, IDataTemplate?>(nameof(TitleTemplate));

    /// <summary>Defines the <see cref="PrimaryButtonContent"/> property.</summary>
    public static readonly StyledProperty<object?> PrimaryButtonContentProperty =
        AvaloniaProperty.Register<ContentDialog, object?>(nameof(PrimaryButtonContent));

    /// <summary>Defines the <see cref="SecondaryButtonContent"/> property.</summary>
    public static readonly StyledProperty<object?> SecondaryButtonContentProperty =
        AvaloniaProperty.Register<ContentDialog, object?>(nameof(SecondaryButtonContent));

    /// <summary>Defines the <see cref="CloseButtonContent"/> property.</summary>
    public static readonly StyledProperty<object?> CloseButtonContentProperty =
        AvaloniaProperty.Register<ContentDialog, object?>(nameof(CloseButtonContent));

    /// <summary>Defines the <see cref="PrimaryButtonCommand"/> property.</summary>
    public static readonly StyledProperty<ICommand?> PrimaryButtonCommandProperty =
        AvaloniaProperty.Register<ContentDialog, ICommand?>(nameof(PrimaryButtonCommand));

    /// <summary>Defines the <see cref="SecondaryButtonCommand"/> property.</summary>
    public static readonly StyledProperty<ICommand?> SecondaryButtonCommandProperty =
        AvaloniaProperty.Register<ContentDialog, ICommand?>(nameof(SecondaryButtonCommand));

    /// <summary>Defines the <see cref="CloseButtonCommand"/> property.</summary>
    public static readonly StyledProperty<ICommand?> CloseButtonCommandProperty =
        AvaloniaProperty.Register<ContentDialog, ICommand?>(nameof(CloseButtonCommand));

    /// <summary>Defines the <see cref="PrimaryButtonCommandParameter"/> property.</summary>
    public static readonly StyledProperty<object?> PrimaryButtonCommandParameterProperty =
        AvaloniaProperty.Register<ContentDialog, object?>(nameof(PrimaryButtonCommandParameter));

    /// <summary>Defines the <see cref="SecondaryButtonCommandParameter"/> property.</summary>
    public static readonly StyledProperty<object?> SecondaryButtonCommandParameterProperty =
        AvaloniaProperty.Register<ContentDialog, object?>(nameof(SecondaryButtonCommandParameter));

    /// <summary>Defines the <see cref="CloseButtonCommandParameter"/> property.</summary>
    public static readonly StyledProperty<object?> CloseButtonCommandParameterProperty =
        AvaloniaProperty.Register<ContentDialog, object?>(nameof(CloseButtonCommandParameter));

    /// <summary>Defines the <see cref="IsPrimaryButtonEnabled"/> property.</summary>
    public static readonly StyledProperty<bool> IsPrimaryButtonEnabledProperty =
        AvaloniaProperty.Register<ContentDialog, bool>(nameof(IsPrimaryButtonEnabled), true);

    /// <summary>Defines the <see cref="IsSecondaryButtonEnabled"/> property.</summary>
    public static readonly StyledProperty<bool> IsSecondaryButtonEnabledProperty =
        AvaloniaProperty.Register<ContentDialog, bool>(nameof(IsSecondaryButtonEnabled), true);

    /// <summary>Defines the <see cref="IsCloseButtonEnabled"/> property.</summary>
    public static readonly StyledProperty<bool> IsCloseButtonEnabledProperty =
        AvaloniaProperty.Register<ContentDialog, bool>(nameof(IsCloseButtonEnabled), true);

    /// <summary>Defines the <see cref="DefaultButton"/> property.</summary>
    public static readonly StyledProperty<ContentDialogButton> DefaultButtonProperty =
        AvaloniaProperty.Register<ContentDialog, ContentDialogButton>(
            nameof(DefaultButton),
            validate: Enum.IsDefined);

    /// <summary>Defines the <see cref="CancelButton"/> property.</summary>
    public static readonly StyledProperty<ContentDialogButton> CancelButtonProperty =
        AvaloniaProperty.Register<ContentDialog, ContentDialogButton>(
            nameof(CancelButton),
            ContentDialogButton.Close,
            validate: Enum.IsDefined);

    /// <summary>Defines the <see cref="IsLightDismissEnabled"/> property.</summary>
    public static readonly StyledProperty<bool> IsLightDismissEnabledProperty =
        AvaloniaProperty.Register<ContentDialog, bool>(nameof(IsLightDismissEnabled));

    /// <summary>Defines the <see cref="IsEscapeEnabled"/> property.</summary>
    public static readonly StyledProperty<bool> IsEscapeEnabledProperty =
        AvaloniaProperty.Register<ContentDialog, bool>(nameof(IsEscapeEnabled), true);

    /// <summary>Defines the <see cref="IsSystemBackEnabled"/> property.</summary>
    public static readonly StyledProperty<bool> IsSystemBackEnabledProperty =
        AvaloniaProperty.Register<ContentDialog, bool>(nameof(IsSystemBackEnabled), true);

    /// <summary>Defines the <see cref="Transition"/> property.</summary>
    public static readonly StyledProperty<IPageTransition?> TransitionProperty =
        AvaloniaProperty.Register<ContentDialog, IPageTransition?>(nameof(Transition));

    /// <summary>Defines the read-only <see cref="IsOpen"/> property.</summary>
    public static readonly DirectProperty<ContentDialog, bool> IsOpenProperty =
        AvaloniaProperty.RegisterDirect<ContentDialog, bool>(nameof(IsOpen), o => o.IsOpen);

    private Control? _backdrop;
    private Control? _dialogSurface;
    private Button? _primaryButton;
    private Button? _secondaryButton;
    private Button? _closeButton;
    private TopLevel? _owner;
    private OverlayLayer? _overlayLayer;
    private IInputElement? _previousFocus;
    private TaskCompletionSource<ContentDialogResult>? _completion;
    private CancellationTokenRegistration _cancellationRegistration;
    private IDisposable? _ownerSizeSubscription;
    private CancellationTokenSource? _transitionCancellation;
    private DialogDeferralManager? _actionDeferrals;
    private DialogDeferralManager? _closingDeferrals;
    private DialogState _state;
    private bool _isRemoving;
    private bool _isClosing;
    private bool _isActionPending;
    private bool _isOpen;
    private bool _openingTransitionCompleted;
    private bool _openedRaised;
    private double? _dialogSurfaceInitialOpacity;
    private int _openingTransitionGeneration;

    static ContentDialog()
    {
        KeyboardNavigation.TabNavigationProperty.OverrideDefaultValue<ContentDialog>(
            KeyboardNavigationMode.Cycle);
        FocusableProperty.OverrideDefaultValue<ContentDialog>(true);
    }

    /// <summary>Initializes a new instance of the <see cref="ContentDialog"/> class.</summary>
    public ContentDialog()
    {
        UpdateActionPseudoClasses();
    }

    /// <summary>Gets or sets the title shown above the dialog content.</summary>
    public object? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Gets or sets the template used to display <see cref="Title"/>.</summary>
    public IDataTemplate? TitleTemplate
    {
        get => GetValue(TitleTemplateProperty);
        set => SetValue(TitleTemplateProperty, value);
    }

    /// <summary>Gets or sets the primary action content.</summary>
    public object? PrimaryButtonContent
    {
        get => GetValue(PrimaryButtonContentProperty);
        set => SetValue(PrimaryButtonContentProperty, value);
    }

    /// <summary>Gets or sets the secondary action content.</summary>
    public object? SecondaryButtonContent
    {
        get => GetValue(SecondaryButtonContentProperty);
        set => SetValue(SecondaryButtonContentProperty, value);
    }

    /// <summary>Gets or sets the close action content.</summary>
    public object? CloseButtonContent
    {
        get => GetValue(CloseButtonContentProperty);
        set => SetValue(CloseButtonContentProperty, value);
    }

    /// <summary>Gets or sets the command invoked by the primary action.</summary>
    public ICommand? PrimaryButtonCommand
    {
        get => GetValue(PrimaryButtonCommandProperty);
        set => SetValue(PrimaryButtonCommandProperty, value);
    }

    /// <summary>Gets or sets the command invoked by the secondary action.</summary>
    public ICommand? SecondaryButtonCommand
    {
        get => GetValue(SecondaryButtonCommandProperty);
        set => SetValue(SecondaryButtonCommandProperty, value);
    }

    /// <summary>Gets or sets the command invoked by the close action.</summary>
    public ICommand? CloseButtonCommand
    {
        get => GetValue(CloseButtonCommandProperty);
        set => SetValue(CloseButtonCommandProperty, value);
    }

    /// <summary>Gets or sets the primary command parameter.</summary>
    public object? PrimaryButtonCommandParameter
    {
        get => GetValue(PrimaryButtonCommandParameterProperty);
        set => SetValue(PrimaryButtonCommandParameterProperty, value);
    }

    /// <summary>Gets or sets the secondary command parameter.</summary>
    public object? SecondaryButtonCommandParameter
    {
        get => GetValue(SecondaryButtonCommandParameterProperty);
        set => SetValue(SecondaryButtonCommandParameterProperty, value);
    }

    /// <summary>Gets or sets the close command parameter.</summary>
    public object? CloseButtonCommandParameter
    {
        get => GetValue(CloseButtonCommandParameterProperty);
        set => SetValue(CloseButtonCommandParameterProperty, value);
    }

    /// <summary>Gets or sets whether the primary action is enabled.</summary>
    public bool IsPrimaryButtonEnabled
    {
        get => GetValue(IsPrimaryButtonEnabledProperty);
        set => SetValue(IsPrimaryButtonEnabledProperty, value);
    }

    /// <summary>Gets or sets whether the secondary action is enabled.</summary>
    public bool IsSecondaryButtonEnabled
    {
        get => GetValue(IsSecondaryButtonEnabledProperty);
        set => SetValue(IsSecondaryButtonEnabledProperty, value);
    }

    /// <summary>Gets or sets whether the close action is enabled.</summary>
    public bool IsCloseButtonEnabled
    {
        get => GetValue(IsCloseButtonEnabledProperty);
        set => SetValue(IsCloseButtonEnabledProperty, value);
    }

    /// <summary>Gets or sets the action invoked by Enter when no child handles it.</summary>
    public ContentDialogButton DefaultButton
    {
        get => GetValue(DefaultButtonProperty);
        set => SetValue(DefaultButtonProperty, value);
    }

    /// <summary>Gets or sets the action invoked by Escape and system-back.</summary>
    public ContentDialogButton CancelButton
    {
        get => GetValue(CancelButtonProperty);
        set => SetValue(CancelButtonProperty, value);
    }

    /// <summary>Gets or sets whether pressing the backdrop dismisses the dialog.</summary>
    public bool IsLightDismissEnabled
    {
        get => GetValue(IsLightDismissEnabledProperty);
        set => SetValue(IsLightDismissEnabledProperty, value);
    }

    /// <summary>Gets or sets whether Escape can dismiss the dialog.</summary>
    public bool IsEscapeEnabled
    {
        get => GetValue(IsEscapeEnabledProperty);
        set => SetValue(IsEscapeEnabledProperty, value);
    }

    /// <summary>Gets or sets whether a platform system-back request can dismiss the dialog.</summary>
    public bool IsSystemBackEnabled
    {
        get => GetValue(IsSystemBackEnabledProperty);
        set => SetValue(IsSystemBackEnabledProperty, value);
    }

    /// <summary>
    /// Gets or sets the reusable visual transition run when the dialog opens and closes.
    /// A null value disables lifecycle transitions.
    /// </summary>
    public IPageTransition? Transition
    {
        get => GetValue(TransitionProperty);
        set => SetValue(TransitionProperty, value);
    }

    /// <summary>Gets whether the dialog is currently open.</summary>
    public bool IsOpen
    {
        get => _isOpen;
        private set => SetAndRaise(IsOpenProperty, ref _isOpen, value);
    }

    /// <summary>Occurs before the dialog is attached to its owner.</summary>
    public event EventHandler? Opening;

    /// <summary>Occurs after the dialog has been attached and its opening transition completes.</summary>
    public event EventHandler? Opened;

    /// <summary>Occurs when the primary action is requested.</summary>
    public event EventHandler<ContentDialogButtonClickEventArgs>? PrimaryButtonClick;

    /// <summary>Occurs when the secondary action is requested.</summary>
    public event EventHandler<ContentDialogButtonClickEventArgs>? SecondaryButtonClick;

    /// <summary>Occurs when the close action is requested.</summary>
    public event EventHandler<ContentDialogButtonClickEventArgs>? CloseButtonClick;

    /// <summary>Occurs before the dialog closes and can be canceled or deferred.</summary>
    public event EventHandler<ContentDialogClosingEventArgs>? Closing;

    /// <summary>Occurs after the closing transition completes and the dialog is detached.</summary>
    public event EventHandler<ContentDialogClosedEventArgs>? Closed;

    /// <summary>
    /// Shows the dialog over the specified top level.
    /// </summary>
    /// <param name="owner">The top level that owns the modal dialog.</param>
    /// <param name="cancellationToken">A token that force-closes and cancels the operation.</param>
    public Task<ContentDialogResult> ShowAsync(
        TopLevel owner,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(owner);
        Dispatcher.UIThread.VerifyAccess();

        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled<ContentDialogResult>(cancellationToken);

        if (_state != DialogState.Closed)
            throw new InvalidOperationException("The ContentDialog is already open or closing.");

        if (Parent is not null || VisualParent is not null)
        {
            throw new InvalidOperationException(
                "A ContentDialog must not have a logical or visual parent when ShowAsync is called.");
        }

        if (owner.GetPresentationSource() is null)
            throw new InvalidOperationException("The owning TopLevel is not attached to a presentation source.");

        var overlayLayer = OverlayLayer.GetOverlayLayer(owner) ??
            throw new InvalidOperationException("The owning TopLevel does not provide an OverlayLayer.");

        ManagedModalCoordinator.Acquire(owner, this);

        try
        {
            _owner = owner;
            _overlayLayer = overlayLayer;
            var completion = _completion = new TaskCompletionSource<ContentDialogResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _state = DialogState.Opening;
            _openingTransitionCompleted = false;
            _openedRaised = false;
            _previousFocus = owner.FocusManager.GetFocusedElement();

            owner.Closed += OwnerClosed;
            owner.BackRequested += OwnerBackRequested;
            _ownerSizeSubscription = owner.GetObservable(TopLevel.ClientSizeProperty)
                .Subscribe(_ =>
                {
                    InvalidateMeasure();
                    _overlayLayer?.InvalidateArrange();
                });

            if (cancellationToken.CanBeCanceled)
            {
                _cancellationRegistration = cancellationToken.Register(() =>
                    Dispatcher.UIThread.Post(() => ForceClose(cancellationToken, completion)));
            }

            OnOpening();

            if (_state == DialogState.Opening)
                overlayLayer.Children.Add(this);

            return completion.Task;
        }
        catch
        {
            AbortOpen();
            throw;
        }
    }

    /// <summary>Closes the dialog without an action result.</summary>
    public void Hide() => Hide(ContentDialogResult.None);

    /// <summary>Closes the dialog with the specified result.</summary>
    public void Hide(ContentDialogResult result)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (!Enum.IsDefined(result))
            throw new ArgumentOutOfRangeException(nameof(result));

        RequestClose(result, ContentDialogDismissReason.Programmatic);
    }

    /// <summary>Raises the <see cref="Opening"/> event.</summary>
    protected virtual void OnOpening() => Opening?.Invoke(this, EventArgs.Empty);

    /// <summary>Raises the <see cref="Opened"/> event.</summary>
    protected virtual void OnOpened() => Opened?.Invoke(this, EventArgs.Empty);

    /// <summary>Raises the <see cref="PrimaryButtonClick"/> event.</summary>
    protected virtual void OnPrimaryButtonClick(ContentDialogButtonClickEventArgs e)
        => PrimaryButtonClick?.Invoke(this, e);

    /// <summary>Raises the <see cref="SecondaryButtonClick"/> event.</summary>
    protected virtual void OnSecondaryButtonClick(ContentDialogButtonClickEventArgs e)
        => SecondaryButtonClick?.Invoke(this, e);

    /// <summary>Raises the <see cref="CloseButtonClick"/> event.</summary>
    protected virtual void OnCloseButtonClick(ContentDialogButtonClickEventArgs e)
        => CloseButtonClick?.Invoke(this, e);

    /// <summary>Raises the <see cref="Closing"/> event.</summary>
    protected virtual void OnClosing(ContentDialogClosingEventArgs e)
        => Closing?.Invoke(this, e);

    /// <summary>Raises the <see cref="Closed"/> event.</summary>
    protected virtual void OnClosed(ContentDialogClosedEventArgs e)
        => Closed?.Invoke(this, e);

    protected override AutomationPeer OnCreateAutomationPeer()
        => new ContentDialogAutomationPeer(this);

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_owner is { } owner)
        {
            var ownerSize = owner.ClientSize;
            _ = base.MeasureOverride(ownerSize);
            return ownerSize;
        }

        return base.MeasureOverride(availableSize);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        UnsubscribeTemplateParts();
        base.OnApplyTemplate(e);

        _backdrop = e.NameScope.Find<Control>("PART_Backdrop");
        _dialogSurface = e.NameScope.Find<Control>("PART_DialogSurface");
        _primaryButton = e.NameScope.Find<Button>("PART_PrimaryButton");
        _secondaryButton = e.NameScope.Find<Button>("PART_SecondaryButton");
        _closeButton = e.NameScope.Find<Button>("PART_CloseButton");

        if (_backdrop is not null)
            _backdrop.PointerPressed += BackdropPointerPressed;
        if (_primaryButton is not null)
            _primaryButton.Click += PrimaryButtonClicked;
        if (_secondaryButton is not null)
            _secondaryButton.Click += SecondaryButtonClicked;
        if (_closeButton is not null)
            _closeButton.Click += CloseButtonClicked;

        UpdateDefaultButtonClasses();

        // A theme or template can change while the opening callback is queued or
        // while its transition is running. Restart against the current template
        // part so the dialog cannot remain permanently in the Opening state.
        if (_state == DialogState.Opening && IsOpen && !_openingTransitionCompleted)
        {
            CancelActiveTransition();
            ScheduleOpeningTransition(propagateOpenedException: false);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (_state != DialogState.Opening)
            return;

        ApplyTemplate();
        IsOpen = true;
        PseudoClasses.Set(":open", true);
        PseudoClasses.Set(":opening", true);
        PseudoClasses.Set(":closing", false);
        UpdateActionPseudoClasses();
        SetInitialFocus();

        ScheduleOpeningTransition(propagateOpenedException: true);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        if (!_isRemoving && _state is DialogState.Opening or DialogState.Open or DialogState.Closing)
            CompleteClose(ContentDialogResult.None, ContentDialogDismissReason.OwnerClosed, default, false);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Handled || _state is not (DialogState.Opening or DialogState.Open))
            return;

        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            if (IsEscapeEnabled)
            {
                if (CancelButton == ContentDialogButton.None ||
                    !TryBeginButtonAction(CancelButton, ContentDialogDismissReason.Escape))
                    RequestClose(ContentDialogResult.None, ContentDialogDismissReason.Escape);
            }
        }
        else if (e.Key == Key.Enter &&
                 _state == DialogState.Open &&
                 DefaultButton != ContentDialogButton.None)
        {
            e.Handled = TryBeginButtonAction(DefaultButton);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == PrimaryButtonContentProperty ||
            change.Property == SecondaryButtonContentProperty ||
            change.Property == CloseButtonContentProperty ||
            change.Property == DefaultButtonProperty)
        {
            UpdateActionPseudoClasses();
        }

        if (change.Property == TitleProperty)
        {
            if (change.OldValue is ILogical oldLogical)
                LogicalChildren.Remove(oldLogical);
            if (change.NewValue is ILogical newLogical)
                LogicalChildren.Add(newLogical);
        }
    }

    private void PrimaryButtonClicked(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _ = TryBeginButtonAction(ContentDialogButton.Primary);
    }

    private void SecondaryButtonClicked(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _ = TryBeginButtonAction(ContentDialogButton.Secondary);
    }

    private void CloseButtonClicked(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _ = TryBeginButtonAction(ContentDialogButton.Close);
    }

    private void BackdropPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (IsLightDismissEnabled && ReferenceEquals(e.Source, sender))
        {
            e.Handled = true;
            RequestClose(ContentDialogResult.None, ContentDialogDismissReason.LightDismiss);
        }
    }

    private bool TryBeginButtonAction(
        ContentDialogButton button,
        ContentDialogDismissReason dismissReason = ContentDialogDismissReason.Action)
    {
        if (_state != DialogState.Open || _isClosing || _isActionPending || !CanInvoke(button))
            return false;

        _isActionPending = true;
        ContentDialogButtonClickEventArgs? args = null;
        DialogDeferralManager? currentDeferrals = null;
        var deferrals = new DialogDeferralManager(() =>
        {
            if (ReferenceEquals(_actionDeferrals, currentDeferrals))
                _actionDeferrals = null;

            _isActionPending = false;

            if (_state != DialogState.Open || args is null || args.Cancel)
                return;

            ExecuteCommand(button);
            RequestClose(ToResult(button), dismissReason);
        });
        currentDeferrals = deferrals;
        _actionDeferrals = deferrals;

        args = new ContentDialogButtonClickEventArgs(button, deferrals);

        try
        {
            switch (button)
            {
                case ContentDialogButton.Primary:
                    OnPrimaryButtonClick(args);
                    break;
                case ContentDialogButton.Secondary:
                    OnSecondaryButtonClick(args);
                    break;
                case ContentDialogButton.Close:
                    OnCloseButtonClick(args);
                    break;
            }
        }
        catch
        {
            if (ReferenceEquals(_actionDeferrals, deferrals))
                _actionDeferrals = null;
            deferrals.Cancel();
            _isActionPending = false;
            throw;
        }

        deferrals.CompleteInitialDeferral();
        return true;
    }

    private void RequestClose(ContentDialogResult result, ContentDialogDismissReason reason)
    {
        if (_state is not (DialogState.Opening or DialogState.Open) || _isClosing)
            return;

        var previousState = _state;
        _isClosing = true;
        _state = DialogState.Closing;
        ContentDialogClosingEventArgs? args = null;
        DialogDeferralManager? currentDeferrals = null;
        var deferrals = new DialogDeferralManager(() =>
        {
            if (ReferenceEquals(_closingDeferrals, currentDeferrals))
                _closingDeferrals = null;

            if (_state != DialogState.Closing || args is null)
                return;

            if (args.Cancel)
            {
                _isClosing = false;
                PseudoClasses.Set(":closing", false);
                if (!_openedRaised && !_openingTransitionCompleted)
                {
                    _state = DialogState.Opening;
                    PseudoClasses.Set(":opening", true);
                    AttachAfterCanceledPreAttachClose();
                }
                else if (!_openedRaised)
                {
                    _state = DialogState.Opening;
                    FinishOpening();
                }
                else
                {
                    _state = DialogState.Open;
                }
                return;
            }

            BeginClose(result, reason);
        });
        currentDeferrals = deferrals;
        _closingDeferrals = deferrals;

        args = new ContentDialogClosingEventArgs(result, reason, deferrals);
        try
        {
            OnClosing(args);
        }
        catch
        {
            if (ReferenceEquals(_closingDeferrals, deferrals))
                _closingDeferrals = null;
            deferrals.Cancel();
            if (_state == DialogState.Closing)
                _state = previousState;
            _isClosing = false;
            throw;
        }
        deferrals.CompleteInitialDeferral();
    }

    private void ForceClose(
        CancellationToken cancellationToken,
        TaskCompletionSource<ContentDialogResult> expectedCompletion)
    {
        if (_state == DialogState.Closed || !ReferenceEquals(_completion, expectedCompletion))
            return;

        CompleteClose(
            ContentDialogResult.None,
            ContentDialogDismissReason.Cancellation,
            cancellationToken,
            true);
    }

    private void OwnerClosed(object? sender, EventArgs e)
    {
        if (_state != DialogState.Closed)
            CompleteClose(ContentDialogResult.None, ContentDialogDismissReason.OwnerClosed, default, false);
    }

    private void OwnerBackRequested(object? sender, RoutedEventArgs e)
    {
        if (_state == DialogState.Closed)
            return;

        e.Handled = true;
        if (IsSystemBackEnabled)
        {
            if (CancelButton == ContentDialogButton.None ||
                !TryBeginButtonAction(CancelButton, ContentDialogDismissReason.SystemBack))
                RequestClose(ContentDialogResult.None, ContentDialogDismissReason.SystemBack);
        }
    }

    private void CompleteClose(
        ContentDialogResult result,
        ContentDialogDismissReason reason,
        CancellationToken cancellationToken,
        bool canceled)
    {
        if (_state == DialogState.Closed)
            return;

        CancelPendingDeferrals();
        CancelActiveTransition();

        var owner = _owner;
        var overlayLayer = _overlayLayer;
        var completion = _completion;
        var previousFocus = _previousFocus;

        _state = DialogState.Closed;
        _isClosing = false;
        _isActionPending = false;
        IsOpen = false;
        PseudoClasses.Set(":open", false);
        PseudoClasses.Set(":opening", false);
        PseudoClasses.Set(":closing", false);

        _cancellationRegistration.Dispose();
        _ownerSizeSubscription?.Dispose();
        _ownerSizeSubscription = null;
        RestoreDialogSurfaceOpacity();

        if (owner is not null)
        {
            owner.Closed -= OwnerClosed;
            owner.BackRequested -= OwnerBackRequested;
        }

        RemoveFromOverlay(overlayLayer);

        if (owner is not null)
        {
            ManagedModalCoordinator.Release(owner, this);
            RestoreFocus(owner, previousFocus);
        }

        _owner = null;
        _overlayLayer = null;
        _previousFocus = null;
        _completion = null;
        _openingTransitionCompleted = false;
        _openedRaised = false;

        try
        {
            OnClosed(new ContentDialogClosedEventArgs(result, reason));
        }
        catch (Exception exception)
        {
            Logger.TryGet(LogEventLevel.Error, LogArea.Control)
                ?.Log(this, "ContentDialog closed handler threw an exception: {Exception}", exception);
        }
        finally
        {
            if (canceled)
                completion?.TrySetCanceled(cancellationToken);
            else
                completion?.TrySetResult(result);
        }
    }

    private void AbortOpen()
    {
        var owner = _owner;
        var overlayLayer = _overlayLayer;
        var previousFocus = _previousFocus;

        CancelPendingDeferrals();
        CancelActiveTransition();

        _state = DialogState.Closed;
        _isClosing = false;
        _isActionPending = false;
        IsOpen = false;
        PseudoClasses.Set(":open", false);
        PseudoClasses.Set(":opening", false);
        PseudoClasses.Set(":closing", false);
        _cancellationRegistration.Dispose();
        _ownerSizeSubscription?.Dispose();
        _ownerSizeSubscription = null;
        RestoreDialogSurfaceOpacity();

        if (owner is not null)
        {
            owner.Closed -= OwnerClosed;
            owner.BackRequested -= OwnerBackRequested;
            ManagedModalCoordinator.Release(owner, this);
            RestoreFocus(owner, previousFocus);
        }

        RemoveFromOverlay(overlayLayer);

        _owner = null;
        _overlayLayer = null;
        _previousFocus = null;
        _completion = null;
        _openingTransitionCompleted = false;
        _openedRaised = false;
    }

    private void ScheduleOpeningTransition(bool propagateOpenedException)
    {
        if (_state != DialogState.Opening)
            return;

        var generation = unchecked(++_openingTransitionGeneration);
        RestoreDialogSurfaceOpacity();

        if (Transition is { } transition && _dialogSurface is { } target)
        {
            var initialOpacity = target.Opacity;
            _dialogSurfaceInitialOpacity = initialOpacity;
            target.Opacity = 0;
            Dispatcher.UIThread.Post(
                () => StartOpeningTransition(transition, target, initialOpacity, generation),
                DispatcherPriority.Loaded);
            return;
        }

        _openingTransitionCompleted = true;
        try
        {
            FinishOpening();
        }
        catch (Exception exception)
        {
            if (propagateOpenedException)
                throw;

            FailOpen(exception);
        }
    }

    private void StartOpeningTransition(
        IPageTransition transition,
        Control target,
        double initialOpacity,
        int generation)
    {
        if (_state != DialogState.Opening || generation != _openingTransitionGeneration)
            return;

        if (!ReferenceEquals(target, _dialogSurface))
        {
            ScheduleOpeningTransition(propagateOpenedException: false);
            return;
        }

        target.Opacity = initialOpacity;
        _ = RunOpeningTransitionAsync(transition, target);
    }

    private void AttachAfterCanceledPreAttachClose()
    {
        if (_overlayLayer is { } overlayLayer &&
            VisualRoot is null &&
            !overlayLayer.Children.Contains(this))
        {
            overlayLayer.Children.Add(this);
        }
    }

    private async Task RunOpeningTransitionAsync(IPageTransition transition, Control target)
    {
        var cancellation = ReplaceTransitionCancellation();
        Task? transitionTask = null;
        try
        {
            transitionTask = transition.Start(null, target, true, cancellation.Token);
            await transitionTask.WaitAsync(cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            if (transitionTask is not null)
                ObserveCanceledTransition(transitionTask);
        }
        catch (Exception exception)
        {
            LogTransitionException(exception);
        }

        if (Dispatcher.UIThread.CheckAccess())
            CompleteOpeningTransition(cancellation);
        else
            await Dispatcher.UIThread.InvokeAsync(() => CompleteOpeningTransition(cancellation));
    }

    private void FinishOpening()
    {
        if (_state != DialogState.Opening)
            return;

        _state = DialogState.Open;
        PseudoClasses.Set(":opening", false);
        if (!_openedRaised)
        {
            _openedRaised = true;
            OnOpened();
        }
    }

    private void BeginClose(ContentDialogResult result, ContentDialogDismissReason reason)
    {
        PseudoClasses.Set(":opening", false);
        PseudoClasses.Set(":closing", true);
        CancelActiveTransition();

        if (Transition is not { } transition || _dialogSurface is not { } target)
        {
            CompleteClose(result, reason, default, false);
            return;
        }

        _ = RunClosingTransitionAsync(transition, target, result, reason);
    }

    private async Task RunClosingTransitionAsync(
        IPageTransition transition,
        Control target,
        ContentDialogResult result,
        ContentDialogDismissReason reason)
    {
        var cancellation = ReplaceTransitionCancellation();
        Task? transitionTask = null;
        try
        {
            transitionTask = transition.Start(target, null, false, cancellation.Token);
            await transitionTask.WaitAsync(cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            if (transitionTask is not null)
                ObserveCanceledTransition(transitionTask);
        }
        catch (Exception exception)
        {
            LogTransitionException(exception);
        }

        if (Dispatcher.UIThread.CheckAccess())
            CompleteClosingTransition(cancellation, result, reason);
        else
            await Dispatcher.UIThread.InvokeAsync(() =>
                CompleteClosingTransition(cancellation, result, reason));
    }

    private void CompleteOpeningTransition(CancellationTokenSource cancellation)
    {
        if (!ReferenceEquals(_transitionCancellation, cancellation))
            return;

        _transitionCancellation = null;
        cancellation.Dispose();
        _openingTransitionCompleted = true;
        if (_state != DialogState.Opening)
            return;

        try
        {
            FinishOpening();
        }
        catch (Exception exception)
        {
            FailOpen(exception);
        }
    }

    private void CompleteClosingTransition(
        CancellationTokenSource cancellation,
        ContentDialogResult result,
        ContentDialogDismissReason reason)
    {
        if (!ReferenceEquals(_transitionCancellation, cancellation) || _state != DialogState.Closing)
            return;

        _transitionCancellation = null;
        cancellation.Dispose();
        try
        {
            CompleteClose(result, reason, default, false);
        }
        catch (Exception exception)
        {
            Logger.TryGet(LogEventLevel.Error, LogArea.Control)
                ?.Log(this, "ContentDialog closed handler threw an exception: {Exception}", exception);
        }
    }

    private CancellationTokenSource ReplaceTransitionCancellation()
    {
        CancelActiveTransition();
        return _transitionCancellation = new CancellationTokenSource();
    }

    private void CancelActiveTransition()
    {
        var cancellation = _transitionCancellation;
        _transitionCancellation = null;
        if (cancellation is null)
            return;

        try
        {
            cancellation.Cancel();
        }
        catch (Exception exception)
        {
            Logger.TryGet(LogEventLevel.Error, LogArea.Control)
                ?.Log(this, "ContentDialog transition cancellation callback threw an exception: {Exception}", exception);
        }
        finally
        {
            cancellation.Dispose();
        }
    }

    private void CancelPendingDeferrals()
    {
        var actionDeferrals = _actionDeferrals;
        var closingDeferrals = _closingDeferrals;
        _actionDeferrals = null;
        _closingDeferrals = null;
        actionDeferrals?.Cancel();
        closingDeferrals?.Cancel();
    }

    private void FailOpen(Exception exception)
    {
        var completion = _completion;
        AbortOpen();
        completion?.TrySetException(exception);
    }

    private void LogTransitionException(Exception exception)
    {
        Logger.TryGet(LogEventLevel.Error, LogArea.Control)
            ?.Log(this, "ContentDialog transition threw an unhandled exception: {Exception}", exception);
    }

    private void ObserveCanceledTransition(Task transitionTask)
    {
        // WaitAsync lets forced cleanup proceed even when an application-provided
        // transition ignores cancellation. Keep observing the abandoned task so a
        // later fault cannot surface as an unobserved task exception. Use a weak
        // source reference so a transition that never completes cannot retain the
        // dialog after it has been removed from its owner.
        _ = ObserveCanceledTransitionAsync(
            transitionTask,
            new WeakReference<ContentDialog>(this));
    }

    private static async Task ObserveCanceledTransitionAsync(
        Task transitionTask,
        WeakReference<ContentDialog> source)
    {
        try
        {
            await transitionTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            if (source.TryGetTarget(out var dialog))
            {
                dialog.LogTransitionException(exception);
            }
            else
            {
                Logger.TryGet(LogEventLevel.Error, LogArea.Control)
                    ?.Log(null, "ContentDialog transition threw an unhandled exception after cancellation: {Exception}", exception);
            }
        }
    }

    private void RemoveFromOverlay(OverlayLayer? overlayLayer)
    {
        _isRemoving = true;
        try
        {
            if (overlayLayer?.Children.Contains(this) == true)
                overlayLayer.Children.Remove(this);
        }
        catch (Exception exception)
        {
            Logger.TryGet(LogEventLevel.Error, LogArea.Control)
                ?.Log(this, "ContentDialog could not detach from its overlay: {Exception}", exception);
        }
        finally
        {
            _isRemoving = false;
        }
    }

    private void SetInitialFocus()
    {
        var requested = GetButton(DefaultButton);

        if (requested is { IsEffectivelyVisible: true, IsEffectivelyEnabled: true })
        {
            requested.Focus(NavigationMethod.Unspecified);
            return;
        }

        var first = FocusManager.FindFirstFocusableElement(this);
        if (first is not null && !ReferenceEquals(first, this))
            first.Focus(NavigationMethod.Unspecified);
        else
            Focus(NavigationMethod.Unspecified);
    }

    private bool CanInvoke(ContentDialogButton button)
    {
        var target = GetButton(button);
        return HasActionContent(button) &&
               target is { IsEffectivelyVisible: true, IsEffectivelyEnabled: true };
    }

    private Button? GetButton(ContentDialogButton button) => button switch
    {
        ContentDialogButton.Primary => _primaryButton,
        ContentDialogButton.Secondary => _secondaryButton,
        ContentDialogButton.Close => _closeButton,
        _ => null
    };

    private void ExecuteCommand(ContentDialogButton button)
    {
        var (command, parameter) = button switch
        {
            ContentDialogButton.Primary => (PrimaryButtonCommand, PrimaryButtonCommandParameter),
            ContentDialogButton.Secondary => (SecondaryButtonCommand, SecondaryButtonCommandParameter),
            ContentDialogButton.Close => (CloseButtonCommand, CloseButtonCommandParameter),
            _ => (null, null)
        };

        if (command?.CanExecute(parameter) == true)
            command.Execute(parameter);
    }

    private bool HasActionContent(ContentDialogButton button) => button switch
    {
        ContentDialogButton.Primary => HasContent(PrimaryButtonContent),
        ContentDialogButton.Secondary => HasContent(SecondaryButtonContent),
        ContentDialogButton.Close => HasContent(CloseButtonContent),
        _ => false
    };

    private void UpdateActionPseudoClasses()
    {
        var hasPrimary = HasContent(PrimaryButtonContent);
        var hasSecondary = HasContent(SecondaryButtonContent);
        var hasClose = HasContent(CloseButtonContent);

        PseudoClasses.Set(":primary", hasPrimary);
        PseudoClasses.Set(":secondary", hasSecondary);
        PseudoClasses.Set(":close", hasClose);
        PseudoClasses.Set(":primary-default", hasPrimary && DefaultButton == ContentDialogButton.Primary);
        PseudoClasses.Set(":secondary-default", hasSecondary && DefaultButton == ContentDialogButton.Secondary);
        PseudoClasses.Set(":close-default", hasClose && DefaultButton == ContentDialogButton.Close);
        UpdateDefaultButtonClasses();
    }

    private void UpdateDefaultButtonClasses()
    {
        SetAccentClass(_primaryButton, HasContent(PrimaryButtonContent) && DefaultButton == ContentDialogButton.Primary);
        SetAccentClass(_secondaryButton, HasContent(SecondaryButtonContent) && DefaultButton == ContentDialogButton.Secondary);
        SetAccentClass(_closeButton, HasContent(CloseButtonContent) && DefaultButton == ContentDialogButton.Close);
    }

    private static void SetAccentClass(Button? button, bool value)
    {
        if (button is null)
            return;

        if (value)
            button.Classes.Add("accent");
        else
            button.Classes.Remove("accent");
    }

    private void UnsubscribeTemplateParts()
    {
        RestoreDialogSurfaceOpacity();
        if (_backdrop is not null)
            _backdrop.PointerPressed -= BackdropPointerPressed;
        if (_primaryButton is not null)
            _primaryButton.Click -= PrimaryButtonClicked;
        if (_secondaryButton is not null)
            _secondaryButton.Click -= SecondaryButtonClicked;
        if (_closeButton is not null)
            _closeButton.Click -= CloseButtonClicked;

        _dialogSurface = null;
    }

    private void RestoreDialogSurfaceOpacity()
    {
        if (_dialogSurface is { } target && _dialogSurfaceInitialOpacity is { } opacity)
            target.Opacity = opacity;

        _dialogSurfaceInitialOpacity = null;
    }

    private static bool HasContent(object? value)
        => value is not null && (value is not string text || !string.IsNullOrEmpty(text));

    private static ContentDialogResult ToResult(ContentDialogButton button) => button switch
    {
        ContentDialogButton.Primary => ContentDialogResult.Primary,
        ContentDialogButton.Secondary => ContentDialogResult.Secondary,
        ContentDialogButton.Close => ContentDialogResult.Close,
        _ => ContentDialogResult.None
    };

    private static void RestoreFocus(TopLevel owner, IInputElement? previousFocus)
    {
        if (owner.GetPresentationSource() is not null &&
            previousFocus is Control control &&
            control.Focusable &&
            control.IsEffectivelyVisible &&
            control.IsEffectivelyEnabled &&
            ReferenceEquals(TopLevel.GetTopLevel(control), owner))
        {
            control.Focus(NavigationMethod.Unspecified);
        }
    }

    private enum DialogState
    {
        Closed,
        Opening,
        Open,
        Closing
    }
}
