using System;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Input;
using Avalonia.Native.Interop;
using Avalonia.Platform;
using MicroCom.Runtime;

namespace Avalonia.Native
{
    internal abstract class WindowBaseImpl : TopLevelImpl, IWindowBaseImpl
    {
        private const int NoInterface = unchecked((int)0x80004002);
        private AvaloniaNativeMessageDialogProvider? _messageDialogProvider;
        private bool _messageDialogProviderUnavailable;

        internal WindowBaseImpl(IAvaloniaNativeFactory factory) : base(factory)
        {

        }

        public new IAvnWindowBase? Native => _handle?.Native as IAvnWindowBase;

        public PixelPoint Position
        {
            get => Native?.Position.ToAvaloniaPixelPoint() ?? default;
            set => Native?.SetPosition(value.ToAvnPoint());
        }

        public Action? Deactivated { get; set; }
        public Action? Activated { get; set; }

        public Action<PixelPoint>? PositionChanged { get; set; }

        public Size? FrameSize
        {
            get
            {
                if (Native != null)
                {
                    unsafe
                    {
                        var s = new AvnSize { Width = -1, Height = -1 };
                        Native.GetFrameSize(&s);
                        return s.Width < 0  && s.Height < 0 ? null : new Size(s.Width, s.Height);
                    }
                }

                return default;
            }
        }
        
        internal override void Init(MacOSTopLevelHandle handle)
        {
            _handle = handle;

            base.Init(handle);

            var monitor = this.TryGetFeature<IScreenImpl>()!.AllScreens
                .OrderBy(x => x.Scaling)
                .First(m => m.Bounds.Contains(Position));

            Resize(new Size(monitor.WorkingArea.Width * 0.75d, monitor.WorkingArea.Height * 0.7d), WindowResizeReason.Layout);
        }

        public void Activate()
        {
            Native?.Activate();
        }

        public void Resize(Size clientSize, WindowResizeReason reason)
        {
            Native?.Resize(clientSize.Width, clientSize.Height, (AvnPlatformResizeReason)reason);
        }
        
        public override void SetFrameThemeVariant(PlatformThemeVariant themeVariant)
        {
            Native?.SetFrameThemeVariant((AvnPlatformThemeVariant)themeVariant);
        }

        public override void Dispose()
        {
            _messageDialogProvider?.Dispose();
            _messageDialogProvider = null;
            Native?.Close();
            base.Dispose();
        }

        public override object? TryGetFeature(Type featureType)
        {
            if (featureType == typeof(INativeMessageDialogProvider) &&
                !_messageDialogProviderUnavailable &&
                Native is { } native)
            {
                if (_messageDialogProvider is not null)
                    return _messageDialogProvider;

                try
                {
                    return _messageDialogProvider = new AvaloniaNativeMessageDialogProvider(
                        native.QueryInterface<IAvnMessageDialogProvider>());
                }
                catch (COMException exception) when (exception.HResult == NoInterface)
                {
                    // Keep the feature optional when a managed assembly is paired with
                    // an older Avalonia.Native binary that does not expose the interface.
                    _messageDialogProviderUnavailable = true;
                }
            }

            return base.TryGetFeature(featureType);
        }

        public virtual void Show(bool activate, bool isDialog)
        {
            Native?.Show(activate.AsComBool(), isDialog.AsComBool());
        }

        public void Hide()
        {
            Native?.Hide();
        }

        public void BeginMoveDrag(PointerPressedEventArgs e)
        {
            Native?.BeginMoveDrag();
        }

        public Size MaxAutoSizeHint => this.TryGetFeature<IScreenImpl>()!.AllScreens
            .Select(s => s.Bounds.Size.ToSize(1))
            .OrderByDescending(x => x.Width + x.Height).FirstOrDefault();

        public void SetTopmost(bool value)
        {
            Native?.SetTopMost(value.AsComBool());
        }

        // TODO
        public void BeginResizeDrag(WindowEdge edge, PointerPressedEventArgs e)
        {

        }

        public void SetMinMaxSize(Size minSize, Size maxSize)
        {
            Native?.SetMinMaxSize(minSize.ToAvnSize(), maxSize.ToAvnSize());
        }

        protected class WindowBaseEvents : TopLevelEvents, IAvnWindowBaseEvents
        {
            private readonly WindowBaseImpl _parent;

            public WindowBaseEvents(WindowBaseImpl parent) : base(parent)
            {
                _parent = parent;
            }

            void IAvnWindowBaseEvents.PositionChanged(AvnPoint position)
            {
                _parent.PositionChanged?.Invoke(position.ToAvaloniaPixelPoint());
            }

            void IAvnWindowBaseEvents.Activated() => _parent.Activated?.Invoke();

            void IAvnWindowBaseEvents.Deactivated() => _parent.Deactivated?.Invoke();
        }
    }
}
