using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Avalonia.Controls.Notifications;
using Avalonia.Logging;
using Avalonia.Win32.WinRT;
using MicroCom.Runtime;
using Microsoft.Win32;

namespace Avalonia.Win32;

internal sealed class Win32SystemNotificationManager : ISystemNotificationManager
{
    private const string NotificationGroup = "Avalonia";
    private static Exception? s_processIdentityInitializationError;
    private readonly Lazy<IdentityInitialization> _identity = new(
        InitializeIdentity,
        LazyThreadSafetyMode.ExecutionAndPublication);
    private readonly SystemNotificationIdTracker _notificationIds = new();

    public bool IsSupported => OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763) &&
                               HasConfiguredIdentity();

    public ValueTask<SystemNotificationPermissionStatus> GetPermissionStatusAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsSupported)
            return ValueTask.FromResult(SystemNotificationPermissionStatus.Unsupported);
        if (_identity.Value.Identity is null)
            return ValueTask.FromResult(SystemNotificationPermissionStatus.Unsupported);

        using var notifier = CreateNotifier();
        return ValueTask.FromResult(notifier.Setting == NotificationSetting.Enabled
            ? SystemNotificationPermissionStatus.Granted
            : SystemNotificationPermissionStatus.Denied);
    }

    public ValueTask<SystemNotificationPermissionStatus> RequestPermissionAsync(
        CancellationToken cancellationToken = default) =>
        GetPermissionStatusAsync(cancellationToken);

    public ValueTask ShowAsync(
        SystemNotification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureSupported();

        using var notifier = CreateNotifier();
        if (notifier.Setting != NotificationSetting.Enabled)
        {
            throw new UnauthorizedAccessException(
                "Windows notification delivery is disabled for this application or user.");
        }

        var title = notification.Title ?? Application.Current?.Name ?? "Notification";
        var xml = new XDocument(
            new XElement("toast",
                new XElement("visual",
                    new XElement("binding",
                        new XAttribute("template", "ToastGeneric"),
                        new XElement("text", title),
                        new XElement("text", notification.Message)))))
            .ToString(SaveOptions.DisableFormatting);

        using var document = NativeWinRTMethods.CreateInstance<IXmlDocument>(
            "Windows.Data.Xml.Dom.XmlDocument");
        using (var documentIo = document.QueryInterface<IXmlDocumentIO>())
        using (var xmlString = new HStringInterop(xml))
            documentIo.LoadXml(xmlString.Handle);

        using var factory = NativeWinRTMethods.CreateActivationFactory<IToastNotificationFactory>(
            "Windows.UI.Notifications.ToastNotification");
        using var toast = factory.CreateToastNotification(document);
        using (var toast2 = toast.QueryInterface<IToastNotification2>())
        using (var tag = new HStringInterop(GetTag(notification.Id)))
        using (var group = new HStringInterop(NotificationGroup))
        {
            toast2.SetTag(tag.Handle);
            toast2.SetGroup(group.Handle);
        }

        notifier.Show(toast);
        _notificationIds.MarkShown(notification.Id);
        return ValueTask.CompletedTask;
    }

    public ValueTask RemoveAsync(string notificationId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(notificationId);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureSupported();

        using var manager = NativeWinRTMethods.CreateActivationFactory<IToastNotificationManagerStatics>(
            "Windows.UI.Notifications.ToastNotificationManager");
        using var manager2 = manager.QueryInterface<IToastNotificationManagerStatics2>();
        using var history = manager2.History;
        using var tag = new HStringInterop(GetTag(notificationId));
        using var group = new HStringInterop(NotificationGroup);
        var identity = GetIdentity();
        if (identity.RequiresExplicitId)
        {
            using var appId = new HStringInterop(identity.Id);
            history.RemoveGroupedTagWithId(tag.Handle, group.Handle, appId.Handle);
        }
        else
        {
            history.RemoveGroupedTag(tag.Handle, group.Handle);
        }

        _notificationIds.MarkRemoved(notificationId);
        return ValueTask.CompletedTask;
    }

    public ValueTask RemoveAllKnownAsync(CancellationToken cancellationToken = default) =>
        _notificationIds.RemoveAllAsync(RemoveAsync, cancellationToken);

    private IToastNotifier CreateNotifier()
    {
        EnsureSupported();
        using var manager = NativeWinRTMethods.CreateActivationFactory<IToastNotificationManagerStatics>(
            "Windows.UI.Notifications.ToastNotificationManager");
        var identity = GetIdentity();
        if (identity.RequiresExplicitId)
        {
            using var appId = new HStringInterop(identity.Id);
            return manager.CreateToastNotifierWithId(appId.Handle);
        }

        return manager.CreateToastNotifier();
    }

    private void EnsureSupported()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
            throw new NotSupportedException("System notifications require Windows 10 version 1809 or newer.");

        if (_identity.Value.Identity is null)
        {
            throw new NotSupportedException(
                "Windows could not register this application for system notifications.",
                _identity.Value.Error);
        }
    }

    private AppIdentity GetIdentity()
    {
        EnsureSupported();
        return _identity.Value.Identity!.Value;
    }

    private static bool HasConfiguredIdentity()
    {
        if (s_processIdentityInitializationError is not null)
            return false;

        if (TryGetPackagedApplicationId() is not null || TryGetExplicitApplicationId() is not null)
            return true;

        return IsValidApplicationId(Win32Platform.Options.SystemNotificationAppUserModelId);
    }

    internal static void InitializeProcessIdentity(Win32PlatformOptions options)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763) ||
            TryGetPackagedApplicationId() is not null ||
            string.IsNullOrEmpty(options.SystemNotificationAppUserModelId))
        {
            return;
        }

        try
        {
            var configuredId = options.SystemNotificationAppUserModelId;
            if (!IsValidApplicationId(configuredId))
            {
                throw new InvalidOperationException(
                    $"{nameof(Win32PlatformOptions.SystemNotificationAppUserModelId)} must be a " +
                    "non-empty AppUserModelID of at most 128 characters without whitespace.");
            }

            if (TryGetExplicitApplicationId() is { } currentId)
            {
                if (!string.Equals(currentId, configuredId, StringComparison.Ordinal))
                    throw new InvalidOperationException("The process already has a different explicit AppUserModelID.");
                return;
            }

            Marshal.ThrowExceptionForHR(SetCurrentProcessExplicitAppUserModelID(configuredId));
        }
        catch (Exception exception)
        {
            s_processIdentityInitializationError = exception;
            Logger.TryGet(LogEventLevel.Warning, LogArea.Win32Platform)?.Log(
                null,
                "Windows process AppUserModelID initialization failed: {Exception}",
                exception);
        }
    }

    private static IdentityInitialization InitializeIdentity()
    {
        try
        {
            return new IdentityInitialization(CreateIdentity(), null);
        }
        catch (Exception exception)
        {
            Logger.TryGet(LogEventLevel.Warning, LogArea.Win32Platform)?.Log(
                null,
                "Windows system-notification registration failed: {Exception}",
                exception);
            return new IdentityInitialization(null, exception);
        }
    }

    private static AppIdentity CreateIdentity()
    {
        if (TryGetPackagedApplicationId() is { } packagedId)
            return new AppIdentity(packagedId, false);

        var options = Win32Platform.Options;
        var configuredId = options.SystemNotificationAppUserModelId;
        if (!string.IsNullOrEmpty(configuredId))
        {
            if (s_processIdentityInitializationError is { } initializationError)
            {
                throw new InvalidOperationException(
                    "The process AppUserModelID could not be initialized during platform startup.",
                    initializationError);
            }

            if (!IsValidApplicationId(configuredId))
            {
                throw new InvalidOperationException(
                    $"{nameof(Win32PlatformOptions.SystemNotificationAppUserModelId)} must be a " +
                    "non-empty AppUserModelID of at most 128 characters without whitespace.");
            }

            if (TryGetExplicitApplicationId() is not { } currentId ||
                !string.Equals(currentId, configuredId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The configured AppUserModelID was not established during platform startup.");
            }

            if (options.RegisterSystemNotificationsForCurrentUser)
                RegisterCurrentUser(configuredId);

            return new AppIdentity(configuredId, true);
        }

        if (TryGetExplicitApplicationId() is { } explicitId)
            return new AppIdentity(explicitId, true);

        throw new InvalidOperationException(
            "Unpackaged Windows applications must configure a stable system-notification " +
            $"AppUserModelID through {nameof(Win32PlatformOptions)}.{nameof(Win32PlatformOptions.SystemNotificationAppUserModelId)}.");
    }

    private static void RegisterCurrentUser(string applicationId)
    {
        var executable = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ??
            throw new InvalidOperationException("The current executable path is not available.");
        var entryAssembly = GetManagedEntryAssemblyPath(executable);
        var fileName = Path.GetFileNameWithoutExtension(entryAssembly ?? executable);
        var safeName = GetSafeApplicationIdSegment(Application.Current?.Name ?? fileName);
        var suffix = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(applicationId)))[..8];

        InstallStartMenuShortcut(applicationId, safeName, suffix, executable, entryAssembly);
        using var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\AppUserModelId\{applicationId}");
        key?.SetValue("DisplayName", Application.Current?.Name ?? safeName);
        key?.SetValue("IconBackgroundColor", "FFDDDDDD");
    }

    private static void InstallStartMenuShortcut(
        string applicationId,
        string safeName,
        string suffix,
        string executable,
        string? entryAssembly)
    {
        var startMenu = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
        if (string.IsNullOrWhiteSpace(startMenu))
            throw new InvalidOperationException("The current user's Start menu path is not available.");

        var programs = Path.Combine(startMenu, "Programs");
        Directory.CreateDirectory(programs);
        var shortcutPath = Path.Combine(programs, $"{safeName}-{suffix[..8]}.lnk");
        var shellLink = (IShellLinkW)(object)new ShellLink();
        try
        {
            Marshal.ThrowExceptionForHR(shellLink.SetPath(executable));
            Marshal.ThrowExceptionForHR(shellLink.SetWorkingDirectory(
                Path.GetDirectoryName(executable) ?? Environment.CurrentDirectory));
            Marshal.ThrowExceptionForHR(shellLink.SetDescription(
                Application.Current?.Name ?? safeName));
            Marshal.ThrowExceptionForHR(shellLink.SetIconLocation(executable, 0));

            if (IsDotnetHost(executable) && !string.IsNullOrEmpty(entryAssembly))
                Marshal.ThrowExceptionForHR(shellLink.SetArguments($"\"{entryAssembly}\""));

            var propertyStore = (IPropertyStore)shellLink;
            var key = AppUserModelIdPropertyKey;
            var value = PropVariant.FromString(applicationId);
            try
            {
                Marshal.ThrowExceptionForHR(propertyStore.SetValue(ref key, ref value));
                Marshal.ThrowExceptionForHR(propertyStore.Commit());
            }
            finally
            {
                _ = PropVariantClear(ref value);
            }

            Marshal.ThrowExceptionForHR(((IPersistFile)shellLink).Save(shortcutPath, true));
        }
        finally
        {
            Marshal.FinalReleaseComObject(shellLink);
        }
    }

    private static bool IsDotnetHost(string executable) =>
        string.Equals(
            Path.GetFileNameWithoutExtension(executable),
            "dotnet",
            StringComparison.OrdinalIgnoreCase);

    private static string? GetManagedEntryAssemblyPath(string executable)
    {
        if (!IsDotnetHost(executable))
            return null;

        var candidate = Environment.GetCommandLineArgs()[0];
        if (!candidate.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            return null;

        return Path.GetFullPath(candidate);
    }

    private static unsafe string? TryGetPackagedApplicationId()
    {
        uint length = 0;
        _ = GetApplicationUserModelId(Process.GetCurrentProcess().Handle, ref length, null);
        if (length == 0)
            return null;

        var buffer = new char[length];
        fixed (char* pointer = buffer)
        {
            if (GetApplicationUserModelId(Process.GetCurrentProcess().Handle, ref length, pointer) != 0)
                return null;
        }

        return new string(buffer, 0, checked((int)length - 1));
    }

    private static string? TryGetExplicitApplicationId()
    {
        if (GetCurrentProcessExplicitAppUserModelID(out var pointer) < 0 || pointer == IntPtr.Zero)
            return null;

        try
        {
            return Marshal.PtrToStringUni(pointer);
        }
        finally
        {
            Marshal.FreeCoTaskMem(pointer);
        }
    }

    private static string GetTag(string id) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id)))[..16];

    private static string GetSafeApplicationIdSegment(string value)
    {
        var result = new StringBuilder(Math.Min(value.Length, 40));
        foreach (var character in value)
        {
            if (result.Length == 40)
                break;
            if (character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '.' or '-' or '_')
                result.Append(character);
        }

        return result.Length == 0 ? "Application" : result.ToString();
    }

    private static bool IsValidApplicationId(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= 128 &&
        value.IndexOfAny(new[] { ' ', '\t', '\r', '\n' }) < 0;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern unsafe int GetApplicationUserModelId(
        IntPtr process,
        ref uint applicationUserModelIdLength,
        char* applicationUserModelId);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentProcessExplicitAppUserModelID(out IntPtr appId);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PropVariant variant);

    private static PropertyKey AppUserModelIdPropertyKey => new()
    {
        FormatId = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"),
        PropertyId = 5
    };

    private readonly record struct AppIdentity(string Id, bool RequiresExplicitId);
    private readonly record struct IdentityInitialization(AppIdentity? Identity, Exception? Error);

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct PropertyKey
    {
        public Guid FormatId;
        public uint PropertyId;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct PropVariant
    {
        [FieldOffset(0)]
        public ushort VariantType;

        [FieldOffset(8)]
        public IntPtr PointerValue;

        public static PropVariant FromString(string value) => new()
        {
            VariantType = 31, // VT_LPWSTR
            PointerValue = Marshal.StringToCoTaskMemUni(value)
        };
    }

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private sealed class ShellLink
    {
    }

    [ComImport]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLinkW
    {
        [PreserveSig] int GetPath(IntPtr file, int fileLength, IntPtr findData, uint flags);
        [PreserveSig] int GetIdList(out IntPtr itemIdList);
        [PreserveSig] int SetIdList(IntPtr itemIdList);
        [PreserveSig] int GetDescription(IntPtr name, int nameLength);
        [PreserveSig] int SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);
        [PreserveSig] int GetWorkingDirectory(IntPtr directory, int directoryLength);
        [PreserveSig] int SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string directory);
        [PreserveSig] int GetArguments(IntPtr arguments, int argumentsLength);
        [PreserveSig] int SetArguments([MarshalAs(UnmanagedType.LPWStr)] string arguments);
        [PreserveSig] int GetHotkey(out short hotkey);
        [PreserveSig] int SetHotkey(short hotkey);
        [PreserveSig] int GetShowCommand(out int showCommand);
        [PreserveSig] int SetShowCommand(int showCommand);
        [PreserveSig] int GetIconLocation(IntPtr iconPath, int iconPathLength, out int iconIndex);
        [PreserveSig] int SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string iconPath, int iconIndex);
        [PreserveSig] int SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, uint reserved);
        [PreserveSig] int Resolve(IntPtr window, uint flags);
        [PreserveSig] int SetPath([MarshalAs(UnmanagedType.LPWStr)] string path);
    }

    [ComImport]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint propertyCount);
        [PreserveSig] int GetAt(uint propertyIndex, out PropertyKey key);
        [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant value);
        [PreserveSig] int SetValue(ref PropertyKey key, ref PropVariant value);
        [PreserveSig] int Commit();
    }

    [ComImport]
    [Guid("0000010B-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPersistFile
    {
        [PreserveSig] int GetClassId(out Guid classId);
        [PreserveSig] int IsDirty();
        [PreserveSig] int Load([MarshalAs(UnmanagedType.LPWStr)] string fileName, uint mode);
        [PreserveSig] int Save([MarshalAs(UnmanagedType.LPWStr)] string fileName, [MarshalAs(UnmanagedType.Bool)] bool remember);
        [PreserveSig] int SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string fileName);
        [PreserveSig] int GetCurrentFile([MarshalAs(UnmanagedType.LPWStr)] out string fileName);
    }
}
