using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Android;
using Avalonia.Android.Platform;
using Avalonia.Android.Platform.Input;
using Avalonia.Android.Platform.Vulkan;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Notifications;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.OpenGL.Egl;
using Avalonia.Platform;
using Avalonia.Rendering;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;
using Avalonia.Vulkan;

namespace Avalonia
{
    public static class AndroidApplicationExtensions
    {
        public static AppBuilder UseAndroid(this AppBuilder builder)
        {
            return builder
                .UseAndroidRuntimePlatformSubsystem()
                .UseWindowingSubsystem(() => AndroidPlatform.Initialize(), "Android")
                .UseHarfBuzz()
                .UseSkia();
        }
    }

    /// <summary>
    /// Represents the rendering mode for platform graphics.
    /// </summary>
    public enum AndroidRenderingMode
    {
        /// <summary>
        /// Avalonia is rendered into a framebuffer.
        /// </summary>
        Software = 1,

        /// <summary>
        /// Enables android EGL rendering.
        /// </summary>
        Egl = 2,

        /// <summary>
        /// Enables Vulkan rendering
        /// </summary>
        Vulkan = 3
    }

    public sealed class AndroidPlatformOptions
    {
        /// <summary>
        /// Gets or sets Avalonia rendering modes with fallbacks.
        /// The first element in the array has the highest priority.
        /// The default value is: <see cref="AndroidRenderingMode.Egl"/>, <see cref="AndroidRenderingMode.Software"/>.
        /// </summary>
        /// <remarks>
        /// If application should work on as wide range of devices as possible, at least add <see cref="AndroidRenderingMode.Software"/> as a fallback value.
        /// </remarks>
        /// <exception cref="System.InvalidOperationException">Thrown if no values were matched.</exception>
        public IReadOnlyList<AndroidRenderingMode> RenderingMode { get; set; } = new[]
        {
            AndroidRenderingMode.Egl, AndroidRenderingMode.Software
        };

        /// <summary>
        /// Gets or sets the Android notification-channel ID used by
        /// <see cref="Controls.Notifications.ISystemNotificationManager"/>.
        /// </summary>
        /// <remarks>
        /// Android permanently associates user settings with a channel ID. Keep
        /// this value stable after an application has shipped.
        /// </remarks>
        public string SystemNotificationChannelId { get; set; } = "avalonia.system-notifications";

        /// <summary>
        /// Gets or sets the user-visible Android notification-channel name. A
        /// null value uses the application label.
        /// </summary>
        public string? SystemNotificationChannelName { get; set; }

        /// <summary>
        /// Gets or sets the optional user-visible Android notification-channel description.
        /// </summary>
        public string? SystemNotificationChannelDescription { get; set; }

        /// <summary>
        /// Gets or sets the Android drawable resource ID used as the notification
        /// status-bar icon. A value of zero falls back to the application icon.
        /// </summary>
        /// <remarks>
        /// Android recommends a dedicated monochrome status-bar drawable rather
        /// than a full-color launcher icon.
        /// </remarks>
        public int SystemNotificationSmallIconResourceId { get; set; }
    }
}

namespace Avalonia.Android
{
    class AndroidPlatform
    {
        public static readonly AndroidPlatform Instance = new AndroidPlatform();
        public static AndroidPlatformOptions? Options { get; private set; }

        internal static Compositor? Compositor { get; private set; }
        internal static ChoreographerTimer? Timer { get; private set; }

        public static void Initialize()
        {
            Options = AvaloniaLocator.Current.GetService<AndroidPlatformOptions>() ?? new AndroidPlatformOptions();

            Dispatcher.InitializeUIThreadDispatcher(new AndroidDispatcherImpl());
            Timer = new ChoreographerTimer();
            AvaloniaLocator.CurrentMutable
                .Bind<ICursorFactory>().ToTransient<CursorFactory>()
                .Bind<IWindowingPlatform>().ToConstant(new WindowingPlatformStub())
                .Bind<IKeyboardDevice>().ToSingleton<AndroidKeyboardDevice>()
                .Bind<IPlatformSettings>().ToSingleton<AndroidPlatformSettings>()
                .Bind<IPlatformIconLoader>().ToSingleton<PlatformIconLoaderStub>()
                .Bind<IRenderLoop>().ToConstant(RenderLoop.FromTimer(Timer))
                .Bind<PlatformHotkeyConfiguration>().ToSingleton<PlatformHotkeyConfiguration>()
                .Bind<KeyGestureFormatInfo>().ToConstant(new KeyGestureFormatInfo(new Dictionary<Key, string>() { }))
                .Bind<IActivatableLifetime>().ToConstant(new AndroidActivatableLifetime());
            AvaloniaLocator.CurrentMutable
                .Bind<ISystemNotificationManager>().ToSingleton<AndroidSystemNotificationManager>();

            var graphics = InitializeGraphics(Options);
            if (graphics is not null)
            {
                AvaloniaLocator.CurrentMutable.Bind<IPlatformGraphics>().ToConstant(graphics);
            }

            Compositor = new Compositor(graphics);
            AvaloniaLocator.CurrentMutable.Bind<Compositor>().ToConstant(Compositor);
        }
        
        private static IPlatformGraphics? InitializeGraphics(AndroidPlatformOptions opts)
        {
            if (opts.RenderingMode is null || !opts.RenderingMode.Any())
            {
                throw new InvalidOperationException($"{nameof(AndroidPlatformOptions)}.{nameof(AndroidPlatformOptions.RenderingMode)} must not be empty or null");
            }

            foreach (var renderingMode in opts.RenderingMode)
            {
                if (renderingMode == AndroidRenderingMode.Software)
                {
                    return null;
                }

                if (renderingMode == AndroidRenderingMode.Egl)
                {
                    if (EglPlatformGraphics.TryCreate() is { } egl)
                    {
                        return egl;
                    }
                }

                if (renderingMode == AndroidRenderingMode.Vulkan)
                {
                    var vulkan = VulkanSupport.TryInitialize(AvaloniaLocator.Current.GetService<VulkanOptions>() ?? new());
                    if (vulkan != null)
                        return vulkan;
                }
            }

            throw new InvalidOperationException($"{nameof(AndroidPlatformOptions)}.{nameof(AndroidPlatformOptions.RenderingMode)} has a value of \"{string.Join(", ", opts.RenderingMode)}\", but no options were applied.");
        }
    }
}
