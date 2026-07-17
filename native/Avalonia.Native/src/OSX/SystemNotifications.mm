#include "common.h"
#import <UserNotifications/UserNotifications.h>

static AvnSystemNotificationPermission GetPermission(UNAuthorizationStatus status) API_AVAILABLE(macos(10.14));
static AvnSystemNotificationPermission GetPermission(UNAuthorizationStatus status)
{
    switch (status)
    {
        case UNAuthorizationStatusNotDetermined:
            return SystemNotificationPermissionNotDetermined;
        case UNAuthorizationStatusDenied:
            return SystemNotificationPermissionDenied;
        case UNAuthorizationStatusAuthorized:
        case UNAuthorizationStatusProvisional:
            return SystemNotificationPermissionGranted;
        default:
            return SystemNotificationPermissionUnsupported;
    }
}

static UNUserNotificationCenter* TryGetNotificationCenter() API_AVAILABLE(macos(10.14));
static UNUserNotificationCenter* TryGetNotificationCenter()
{
    auto bundle = [NSBundle mainBundle];
    auto bundleUrl = [bundle bundleURL];
    if (bundleUrl == nil ||
        [[bundleUrl pathExtension] caseInsensitiveCompare:@"app"] != NSOrderedSame ||
        [bundle bundleIdentifier].length == 0)
    {
        return nil;
    }

    @try
    {
        return [UNUserNotificationCenter currentNotificationCenter];
    }
    @catch (NSException*)
    {
        // UserNotifications rejects command-line hosts that are not packaged as an application bundle.
        return nil;
    }
}

@interface AvnSystemNotificationCenterDelegate : NSObject<UNUserNotificationCenterDelegate>
@end

@implementation AvnSystemNotificationCenterDelegate

- (void)userNotificationCenter:(UNUserNotificationCenter*)center
        willPresentNotification:(UNNotification*)notification
          withCompletionHandler:(void (^)(UNNotificationPresentationOptions options))completionHandler
    API_AVAILABLE(macos(10.14))
{
    if (@available(macOS 11.0, *))
        completionHandler(UNNotificationPresentationOptionBanner | UNNotificationPresentationOptionList);
    else
        completionHandler(UNNotificationPresentationOptionAlert);
}

@end

class AvnSystemNotificationProvider final :
    public ComSingleObject<IAvnSystemNotificationProvider, &IID_IAvnSystemNotificationProvider_V2>
{
public:
    FORWARD_IUNKNOWN()

    AvnSystemNotificationProvider()
    {
        if (@available(macOS 10.14, *))
        {
            auto center = TryGetNotificationCenter();
            if (center != nil && center.delegate == nil)
            {
                _notificationCenterDelegate = [[AvnSystemNotificationCenterDelegate alloc] init];
                center.delegate = _notificationCenterDelegate;
            }
        }
    }

    ~AvnSystemNotificationProvider() override
    {
        if (@available(macOS 10.14, *))
        {
            auto center = TryGetNotificationCenter();
            if (center != nil && center.delegate == _notificationCenterDelegate)
                center.delegate = nil;
        }
    }

    bool IsSupported() override
    {
        if (@available(macOS 10.14, *))
            return TryGetNotificationCenter() != nil;
        return false;
    }

    HRESULT GetPermissionStatus(IAvnSystemNotificationEvents* events) override
    {
        START_COM_CALL;
        if (events == nullptr)
            return E_POINTER;

        if (@available(macOS 10.14, *))
        {
            auto center = TryGetNotificationCenter();
            if (center == nil)
            {
                events->Completed(SystemNotificationPermissionUnsupported, 0);
                return S_OK;
            }

            ComPtr<IAvnSystemNotificationEvents> callback(events);
            [center getNotificationSettingsWithCompletionHandler:^(UNNotificationSettings* settings) {
                callback->Completed(GetPermission(settings.authorizationStatus), 0);
            }];
        }
        else
        {
            events->Completed(SystemNotificationPermissionUnsupported, 0);
        }
        return S_OK;
    }

    HRESULT RequestPermission(IAvnSystemNotificationEvents* events) override
    {
        START_COM_CALL;
        if (events == nullptr)
            return E_POINTER;

        if (@available(macOS 10.14, *))
        {
            auto center = TryGetNotificationCenter();
            if (center == nil)
            {
                events->Completed(SystemNotificationPermissionUnsupported, 0);
                return S_OK;
            }

            ComPtr<IAvnSystemNotificationEvents> callback(events);
            [center requestAuthorizationWithOptions:UNAuthorizationOptionAlert
                                  completionHandler:^(BOOL granted, NSError* error) {
                if (error != nil)
                {
                    if ([error.domain isEqualToString:UNErrorDomain] &&
                        error.code == UNErrorCodeNotificationsNotAllowed)
                    {
                        callback->Completed(SystemNotificationPermissionDenied, 0);
                        return;
                    }

                    callback->Completed(SystemNotificationPermissionDenied, (int)error.code);
                    return;
                }

                [center getNotificationSettingsWithCompletionHandler:^(UNNotificationSettings* settings) {
                    callback->Completed(GetPermission(settings.authorizationStatus), 0);
                }];
            }];
        }
        else
            events->Completed(SystemNotificationPermissionUnsupported, 0);
        return S_OK;
    }

    HRESULT ShowSystemNotification(
        const char* identifier,
        const char* title,
        const char* message,
        IAvnSystemNotificationEvents* events) override
    {
        START_COM_CALL;
        if (identifier == nullptr || message == nullptr || events == nullptr)
            return E_POINTER;
        if (@available(macOS 10.14, *))
        {
            @autoreleasepool
            {
                auto center = TryGetNotificationCenter();
                if (center == nil)
                    return E_NOTIMPL;

                auto nativeIdentifier = [NSString stringWithUTF8String:identifier];
                auto content = [[UNMutableNotificationContent alloc] init];
                content.body = [NSString stringWithUTF8String:message];
                if (title != nullptr && title[0] != '\0')
                    content.title = [NSString stringWithUTF8String:title];

                auto request = [UNNotificationRequest requestWithIdentifier:nativeIdentifier
                                                                     content:content
                                                                     trigger:nil];
                ComPtr<IAvnSystemNotificationEvents> callback(events);
                [center addNotificationRequest:request withCompletionHandler:^(NSError* error) {
                    callback->Completed(error == nil ? 1 : 0, error == nil ? 0 : (int)error.code);
                }];
            }
            return S_OK;
        }
        return E_NOTIMPL;
    }

    HRESULT RemoveSystemNotification(const char* identifier) override
    {
        START_COM_CALL;
        if (identifier == nullptr)
            return E_POINTER;
        if (@available(macOS 10.14, *))
        {
            @autoreleasepool
            {
                auto center = TryGetNotificationCenter();
                if (center == nil)
                    return E_NOTIMPL;

                auto nativeIdentifier = [NSString stringWithUTF8String:identifier];
                [center removePendingNotificationRequestsWithIdentifiers:@[nativeIdentifier]];
                [center removeDeliveredNotificationsWithIdentifiers:@[nativeIdentifier]];
            }
            return S_OK;
        }
        return E_NOTIMPL;
    }

private:
    AvnSystemNotificationCenterDelegate* _notificationCenterDelegate = nil;
};

IAvnSystemNotificationProvider* CreateSystemNotificationProvider()
{
    return new AvnSystemNotificationProvider();
}
