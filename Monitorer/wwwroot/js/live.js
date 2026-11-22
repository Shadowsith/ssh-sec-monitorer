// Live updates via SignalR (hub /hubs/monitor). The server only announces which topics changed; a page whose
// [data-live="topic ..."] container shows one of them re-fetches itself and swaps that container in place.
// No full reload: scroll position, open accordions and the chart instances stay (charts animate to the new data).
// After a swap 'ssm:content-updated' is dispatched (detail.root) so the page scripts re-bind / re-draw.
class LiveUpdates {
    static MIN_GAP_MS = 3000;   // parallel polls send bursts - at most one re-fetch every 3 s
    static timer = null;
    static pending = false;
    static lastRun = 0;
    static connected = false;

    static root() {
        return document.querySelector('[data-live]');
    }

    static init() {
        const root = LiveUpdates.root();
        if (!root || typeof signalR === 'undefined') return;
        const topics = new Set(root.dataset.live.split(/\s+/).filter(Boolean));

        const connection = new signalR.HubConnectionBuilder()
            .withUrl('/hubs/monitor')
            .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
            .configureLogging(signalR.LogLevel.Warning)
            .build();
        connection.on('changed', changed => {
            if (Array.isArray(changed) && changed.some(t => topics.has(t))) LiveUpdates.schedule();
        });
        connection.onreconnecting(() => LiveUpdates.setStatus(false));
        // updates sent while disconnected are lost - fetch once to catch up
        connection.onreconnected(() => { LiveUpdates.setStatus(true); LiveUpdates.schedule(); });
        // automatic reconnect gave up (or the server closed it, e.g. session expired): start() handles both
        connection.onclose(() => { LiveUpdates.setStatus(false); setTimeout(() => LiveUpdates.start(connection, true), 5000); });

        document.addEventListener('visibilitychange', () => {
            if (document.visibilityState === 'visible' && LiveUpdates.pending) LiveUpdates.schedule();
        });
        LiveUpdates.start(connection, false);
    }

    static async start(connection, catchUp) {
        try {
            await connection.start();
            LiveUpdates.setStatus(true);
            if (catchUp) LiveUpdates.schedule();
        } catch (e) {
            // 401: session expired or user deactivated - the reload lands on the login page
            if (e && e.statusCode === 401) { window.location.reload(); return; }
            LiveUpdates.setStatus(false);
            setTimeout(() => LiveUpdates.start(connection, true), 10000);
        }
    }

    static schedule() {
        LiveUpdates.pending = true;
        if (LiveUpdates.timer) return;
        const wait = Math.max(300, LiveUpdates.lastRun + LiveUpdates.MIN_GAP_MS - Date.now());
        LiveUpdates.timer = setTimeout(LiveUpdates.run, wait);
    }

    /** The user is in the middle of something that a swap would destroy. */
    static isBusy(root) {
        if (root.querySelector('.collapse.show:not(.accordion-collapse), .collapsing, .modal.show, .dropdown-menu.show')) return true;
        const active = document.activeElement;
        return !!active && root.contains(active) &&
            active.matches('textarea, select, input:not([type=checkbox]):not([type=radio]):not([type=button]):not([type=submit])');
    }

    static async run() {
        LiveUpdates.timer = null;
        const root = LiveUpdates.root();
        if (!root || document.visibilityState !== 'visible') return;   // hidden: caught up on visibilitychange
        if (LiveUpdates.isBusy(root)) {
            LiveUpdates.timer = setTimeout(LiveUpdates.run, 2000);
            return;
        }
        LiveUpdates.pending = false;
        LiveUpdates.lastRun = Date.now();
        try {
            const response = await fetch(window.location.href, { credentials: 'same-origin', headers: { Accept: 'text/html' } });
            if (response.redirected || response.status === 401) { window.location.reload(); return; }
            if (!response.ok) return;   // e.g. agent deleted (404) - keep the last state
            const fresh = new DOMParser().parseFromString(await response.text(), 'text/html').querySelector('[data-live]');
            const current = LiveUpdates.root();
            if (!fresh || !current || LiveUpdates.isBusy(current)) {
                if (fresh && current) LiveUpdates.schedule();
                return;
            }
            LiveUpdates.swap(current, document.importNode(fresh, true));
        } catch (e) {
            console.warn('Live update failed', e);
        }
    }

    static swap(current, fresh) {
        // keep the chart elements: their ECharts instances are updated instead of re-created
        current.querySelectorAll('.chart-box[id]').forEach(chart => {
            const target = fresh.querySelector('#' + CSS.escape(chart.id));
            if (target) target.replaceWith(chart);
        });
        const focusId = document.activeElement && current.contains(document.activeElement) ? document.activeElement.id : null;
        current.replaceWith(fresh);
        if (focusId) {
            const el = document.getElementById(focusId);
            if (el) el.focus({ preventScroll: true });
        }
        document.dispatchEvent(new CustomEvent('ssm:content-updated', { detail: { root: fresh } }));
    }

    /** Indicator in the header (layout): live or reconnecting. */
    static setStatus(connected) {
        LiveUpdates.connected = connected;
        const el = document.querySelector('[data-live-status]');
        if (!el) return;
        el.classList.remove('d-none');
        el.title = I18n.t(connected ? 'js.live.connectedHint' : 'js.live.disconnectedHint');
        const icon = el.querySelector('i');
        icon.className = connected ? 'bi bi-broadcast text-success me-1' : 'bi bi-wifi-off text-warning me-1';
        el.querySelector('span').textContent = I18n.t(connected ? 'js.live.connected' : 'js.live.disconnected');
    }
}

document.addEventListener('DOMContentLoaded', LiveUpdates.init);
