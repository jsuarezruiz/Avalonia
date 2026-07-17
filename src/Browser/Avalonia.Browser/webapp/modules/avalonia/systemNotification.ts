import { Caniuse } from "./caniuse";

const activeNotifications = new Map<string, Notification>();

export class SystemNotificationBridge {
    public static isSupported(): boolean {
        if (typeof globalThis.Notification === "undefined" || !globalThis.isSecureContext) {
            return false;
        }

        // Mobile browsers commonly expose Notification but reject its constructor.
        // An active service-worker registration is the standards-based delivery
        // path there; it does not need to control the current page.
        return SystemNotificationBridge.hasServiceWorkerSupport() ||
            !SystemNotificationBridge.requiresPersistentNotifications();
    }

    public static getPermissionStatus(): number {
        if (!SystemNotificationBridge.isSupported()) {
            return 0;
        }

        const permission = globalThis.Notification.permission;
        if (permission === "default") return 1;
        if (permission === "denied") return 2;
        if (permission === "granted") return 3;
        return 0;
    }

    public static async requestPermission(): Promise<number> {
        if (!SystemNotificationBridge.isSupported()) {
            return 0;
        }

        await globalThis.Notification.requestPermission();
        return SystemNotificationBridge.getPermissionStatus();
    }

    public static async show(id: string, title: string | null, message: string): Promise<void> {
        if (!SystemNotificationBridge.isSupported()) {
            throw new Error("This browser does not support system notifications.");
        }
        if (globalThis.Notification.permission !== "granted") {
            throw new DOMException("System-notification permission has not been granted.", "NotAllowedError");
        }

        const registration = await SystemNotificationBridge.getActiveServiceWorkerRegistration();
        if (registration) {
            await registration.showNotification(title ?? "", {
                body: message,
                tag: id
            });
            return;
        }

        if (SystemNotificationBridge.requiresPersistentNotifications()) {
            throw new Error(
                "Mobile browser notifications require an active service-worker registration.");
        }

        const notification = new globalThis.Notification(title ?? "", {
            body: message,
            tag: id
        });
        activeNotifications.set(id, notification);
        notification.addEventListener("close", () => {
            if (activeNotifications.get(id) === notification) {
                activeNotifications.delete(id);
            }
        }, { once: true });
    }

    public static async remove(id: string): Promise<void> {
        const notification = activeNotifications.get(id);
        if (notification) {
            activeNotifications.delete(id);
            notification.close();
        }

        const registration = await SystemNotificationBridge.getActiveServiceWorkerRegistration();
        if (registration) {
            const notifications = await registration.getNotifications({ tag: id });
            notifications.forEach(item => item.close());
        }
    }

    private static hasServiceWorkerSupport(): boolean {
        return "serviceWorker" in globalThis.navigator;
    }

    private static async getActiveServiceWorkerRegistration(): Promise<ServiceWorkerRegistration | null> {
        if (!SystemNotificationBridge.hasServiceWorkerSupport()) {
            return null;
        }

        const registration = await globalThis.navigator.serviceWorker.getRegistration();
        return registration?.active ? registration : null;
    }

    private static requiresPersistentNotifications(): boolean {
        const navigator = globalThis.navigator as Navigator & {
            maxTouchPoints?: number;
            platform?: string;
        };

        // iPadOS can identify itself as macOS when desktop-class browsing is enabled.
        return Caniuse.isMobile() ||
            (navigator.platform === "MacIntel" && (navigator.maxTouchPoints ?? 0) > 1);
    }
}
