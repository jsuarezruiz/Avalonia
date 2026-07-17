const activeDialogs = new WeakMap<HTMLElement, HTMLDialogElement>();
let nextDialogId = 0;

export class MessageDialog {
    public static isSupported(): boolean {
        return typeof HTMLDialogElement !== "undefined" &&
            typeof HTMLDialogElement.prototype.showModal === "function";
    }

    public static async show(
        container: HTMLElement,
        title: string | null,
        message: string,
        detail: string | null,
        actions: string[],
        roles: string[]): Promise<number> {
        if (activeDialogs.has(container)) {
            throw new Error("The browser top level already has an active native message dialog.");
        }
        if (!MessageDialog.isSupported()) {
            throw new Error("This browser does not support native modal dialogs.");
        }

        const dialog = document.createElement("dialog");
        const dialogId = ++nextDialogId;
        const headingId = `avalonia-message-dialog-heading-${dialogId}`;
        const descriptionId = `avalonia-message-dialog-description-${dialogId}`;
        dialog.setAttribute("aria-modal", "true");
        dialog.setAttribute("aria-describedby", descriptionId);
        dialog.style.colorScheme = "light dark";
        dialog.style.maxWidth = "min(32rem, calc(100vw - 2rem))";
        dialog.style.minWidth = "min(22rem, calc(100vw - 2rem))";
        dialog.style.padding = "1.5rem";
        dialog.style.border = "1px solid color-mix(in srgb, currentColor 22%, transparent)";
        dialog.style.borderRadius = "0.75rem";
        dialog.style.boxShadow = "0 1rem 3rem rgb(0 0 0 / 30%)";

        if (title) {
            const heading = document.createElement("h2");
            heading.id = headingId;
            heading.textContent = title;
            heading.style.margin = "0 0 1rem";
            heading.style.font = "600 1.25rem system-ui";
            dialog.setAttribute("aria-labelledby", headingId);
            dialog.appendChild(heading);
        } else {
            dialog.setAttribute("aria-label", message);
        }

        const description = document.createElement("div");
        description.id = descriptionId;
        description.style.font = "1rem/1.45 system-ui";

        const primaryText = document.createElement("p");
        primaryText.textContent = message;
        primaryText.style.margin = "0";
        description.appendChild(primaryText);

        if (detail) {
            const detailText = document.createElement("p");
            detailText.textContent = detail;
            detailText.style.margin = "0.75rem 0 0";
            detailText.style.opacity = "0.72";
            description.appendChild(detailText);
        }

        dialog.appendChild(description);

        const form = document.createElement("form");
        form.method = "dialog";
        form.style.display = "flex";
        form.style.justifyContent = "flex-end";
        form.style.flexWrap = "wrap";
        form.style.gap = "0.625rem";
        form.style.marginTop = "1.5rem";

        let cancelIndex = -1;
        actions.forEach((text, index) => {
            const button = document.createElement("button");
            button.type = "submit";
            button.value = index.toString();
            button.textContent = text;
            button.style.minWidth = "5rem";
            button.style.minHeight = "2.5rem";
            button.style.padding = "0.5rem 1rem";
            button.style.font = "600 0.95rem system-ui";

            const role = roles[index];
            if (role.includes("default")) {
                button.autofocus = true;
            }
            if (role.includes("cancel")) {
                cancelIndex = index;
            }
            if (role.includes("destructive")) {
                button.style.color = "#c42b1c";
            }

            form.appendChild(button);
        });

        dialog.appendChild(form);
        container.appendChild(dialog);
        activeDialogs.set(container, dialog);

        return await new Promise<number>((resolve, reject) => {
            dialog.addEventListener("cancel", event => {
                event.preventDefault();
                dialog.close(cancelIndex.toString());
            }, { once: true });

            dialog.addEventListener("close", () => {
                const result = Number.parseInt(dialog.returnValue, 10);
                activeDialogs.delete(container);
                dialog.remove();
                resolve(Number.isFinite(result) ? result : -1);
            }, { once: true });

            try {
                dialog.showModal();
            } catch (error) {
                activeDialogs.delete(container);
                dialog.remove();
                reject(error);
            }
        });
    }

    public static dismiss(container: HTMLElement): void {
        const dialog = activeDialogs.get(container);
        if (dialog?.open) {
            dialog.close("-1");
        }
    }
}
