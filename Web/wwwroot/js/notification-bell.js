(function (global) {
    const notificationsContainer = document.getElementById("bellNotificationsContainer");
    const bellUnreadDot = document.getElementById("bellUnreadDot");
    const offcanvasElement = document.getElementById("notificationBellPanel");

    if (!notificationsContainer || !offcanvasElement) {
        return;
    }

    const readStateKey = "notification-bell-read-v2";
    const refreshIntervalMs = 10000;
    let widgetPayload = null;
    let panelOpened = false;

    function safeParseObject(raw) {
        try {
            const parsed = raw ? JSON.parse(raw) : {};
            return parsed && typeof parsed === "object" ? parsed : {};
        } catch {
            return {};
        }
    }

    function loadReadState() {
        try {
            return safeParseObject(global.localStorage.getItem(readStateKey));
        } catch {
            return {};
        }
    }

    function saveReadState(state) {
        try {
            global.localStorage.setItem(readStateKey, JSON.stringify(state));
        } catch {
            // Ошибки локального хранилища не должны ломать панель уведомлений.
        }
    }

    function escapeHtml(value) {
        return String(value ?? "")
            .replaceAll("&", "&amp;")
            .replaceAll("<", "&lt;")
            .replaceAll(">", "&gt;")
            .replaceAll("\"", "&quot;")
            .replaceAll("'", "&#39;");
    }

    function formatDate(value) {
        const date = value ? new Date(value) : null;
        if (!date || Number.isNaN(date.getTime())) {
            return "Дата неизвестна";
        }

        return date.toLocaleString("ru-RU", {
            day: "2-digit",
            month: "2-digit",
            year: "numeric",
            hour: "2-digit",
            minute: "2-digit"
        });
    }

    function normalizeNotifications() {
        const source = Array.isArray(widgetPayload?.notifications)
            ? widgetPayload.notifications
            : [];

        const readState = loadReadState();
        return source
            .map((item) => {
                const id = String(item?.id ?? "");
                const unreadCountRaw = Number(item?.unreadCount ?? 0);
                const unreadCount = Number.isFinite(unreadCountRaw) ? Math.max(0, unreadCountRaw) : 0;
                const sourceName = String(item?.source ?? "system");
                const localRead = Boolean(readState[id]);
                const isUnread = sourceName === "chat"
                    ? unreadCount > 0
                    : !localRead;
                const occurredAtValue = item?.occurredAtUtc ? new Date(item.occurredAtUtc) : null;

                return {
                    id,
                    source: sourceName,
                    title: String(item?.title ?? "Уведомление"),
                    message: String(item?.message ?? ""),
                    url: String(item?.url ?? ""),
                    unreadCount,
                    isUnread,
                    occurredAtText: formatDate(item?.occurredAtUtc),
                    occurredAtMs: occurredAtValue && !Number.isNaN(occurredAtValue.getTime())
                        ? occurredAtValue.getTime()
                        : 0
                };
            })
            .filter((item) => item.id.length > 0)
            .sort((left, right) => right.occurredAtMs - left.occurredAtMs);
    }

    function setBellUnreadState(hasUnread) {
        if (!bellUnreadDot) {
            return;
        }

        bellUnreadDot.classList.toggle("d-none", !hasUnread);
    }

    function renderNotifications() {
        const notifications = normalizeNotifications();
        const hasUnread = notifications.some((item) => item.isUnread);
        setBellUnreadState(hasUnread);

        if (notifications.length === 0) {
            notificationsContainer.innerHTML = '<div class="small text-muted">Пока уведомлений нет.</div>';
            return;
        }

        notificationsContainer.innerHTML = notifications
            .map((item) => {
                const unreadClass = item.isUnread ? " notification-item-unread" : "";
                const unreadDot = item.isUnread ? '<span class="notification-item-dot" aria-hidden="true"></span>' : "";
                const unreadCounter = item.source === "chat" && item.unreadCount > 1
                    ? `<span class="badge rounded-pill text-bg-primary ms-1">${item.unreadCount}</span>`
                    : "";
                const content = `
                    <div class="d-flex justify-content-between align-items-start gap-2">
                        <div class="notification-title fw-semibold text-break">${escapeHtml(item.title)}${unreadCounter}</div>
                        ${unreadDot}
                    </div>
                    <div class="notification-message mt-1 text-break">${escapeHtml(item.message)}</div>
                    <div class="notification-meta mt-2">${escapeHtml(item.occurredAtText)}</div>
                `;

                if (item.url.length > 0) {
                    return `<a class="history-item notification-item${unreadClass}" href="${escapeHtml(item.url)}">${content}</a>`;
                }

                return `<div class="history-item notification-item${unreadClass}">${content}</div>`;
            })
            .join("");
    }

    async function loadWidget() {
        try {
            const response = await fetch("/Notifications/Widget", { credentials: "same-origin" });
            if (!response.ok) {
                throw new Error("Панель уведомлений недоступна.");
            }

            widgetPayload = await response.json();
        } catch {
            widgetPayload = null;
        }

        renderNotifications();
    }

    async function markAllAsRead() {
        const notifications = normalizeNotifications();
        const readState = loadReadState();

        notifications
            .filter((item) => item.source !== "chat")
            .forEach((item) => {
                readState[item.id] = true;
            });
        saveReadState(readState);

        try {
            await fetch("/Notifications/MarkRead", {
                method: "GET",
                credentials: "same-origin",
                headers: { "X-Requested-With": "XMLHttpRequest" },
                keepalive: true
            });
        } catch {
            // Временная ошибка отметки не должна мешать переходу пользователя.
        }
    }

    offcanvasElement.addEventListener("shown.bs.offcanvas", () => {
        panelOpened = true;
    });

    offcanvasElement.addEventListener("hidden.bs.offcanvas", async () => {
        if (!panelOpened) {
            return;
        }

        panelOpened = false;
        await markAllAsRead();
        await loadWidget();
    });

    global.addEventListener("pagehide", () => {
        if (!panelOpened) {
            return;
        }

        const readState = loadReadState();
        normalizeNotifications()
            .filter((item) => item.source !== "chat")
            .forEach((item) => {
                readState[item.id] = true;
            });
        saveReadState(readState);

        try {
            fetch("/Notifications/MarkRead", {
                method: "GET",
                credentials: "same-origin",
                keepalive: true
            });
        } catch {
            // Временная ошибка отметки не должна мешать закрытию страницы.
        }
    });

    void loadWidget();
    global.setInterval(() => {
        void loadWidget();
    }, refreshIntervalMs);
})(window);
