// Shared Apache ECharts helpers: theme (light / dark like Bootstrap), resizing, formatting, status colors.
class SsmCharts {
    static instances = [];

    /**
     * Creates a chart in the element with the given id; option is a function so it can be rebuilt on theme change.
     * Called again for the same element (live update, see live.js) it only sets the new data, with a transition.
     */
    static create(id, buildOption) {
        SsmCharts.instances = SsmCharts.instances.filter(e => {
            if (e.el.isConnected) return true;
            if (e.chart) e.chart.dispose();     // element was removed by a live update
            return false;
        });
        const el = document.getElementById(id);
        if (!el || typeof echarts === 'undefined') return null;
        const existing = SsmCharts.instances.find(e => e.el === el);
        if (existing) {
            existing.buildOption = buildOption;
            SsmCharts.update(existing);
            return existing;
        }
        const entry = { el, buildOption, chart: null };
        SsmCharts.instances.push(entry);
        SsmCharts.render(entry);
        return entry;
    }

    static render(entry) {
        if (entry.chart) entry.chart.dispose();
        entry.chart = echarts.init(entry.el, SsmCharts.isDark() ? 'dark' : null, { renderer: 'canvas' });
        const option = entry.buildOption();
        option.backgroundColor = 'transparent';
        entry.chart.setOption(option);
    }

    /** New data into the existing chart; series / axes / title are replaced so they may come and go (e.g. empty state). */
    static update(entry) {
        const option = entry.buildOption();
        option.backgroundColor = 'transparent';
        entry.chart.setOption(option, { replaceMerge: ['series', 'title', 'xAxis', 'yAxis'] });
    }

    static isDark() {
        return document.documentElement.getAttribute('data-bs-theme') === 'dark';
    }

    /** Shows a short text instead of an empty chart. */
    static empty(text) {
        return { title: { text, left: 'center', top: 'middle', textStyle: { fontSize: 13, fontWeight: 'normal', color: '#6c757d' } } };
    }

    static statusColor(status) {
        switch (status) {
            case 'Ok': case 'Up': return '#198754';
            case 'Warning': case 'Degraded': return '#ffc107';
            case 'Critical': return '#dc3545';
            case 'Unreachable': case 'Down': return '#842029';
            case 'Disabled': return '#adb5bd';
            default: return '#6c757d';
        }
    }

    static statusText(status) {
        return {
            Ok: 'OK', Up: 'Erreichbar', Degraded: 'Erreichbar mit Fehlern', Warning: 'Warnung', Critical: 'Kritisch', Unreachable: 'Nicht erreichbar',
            Down: 'Down', Disabled: 'Deaktiviert', Unknown: 'Unbekannt'
        }[status] || status;
    }

    /** Color of a percentage value by warn / critical threshold. */
    static thresholdColor(value, warn, critical) {
        if (value === null || value === undefined) return '#adb5bd';
        return value >= critical ? '#dc3545' : value >= warn ? '#ffc107' : '#198754';
    }

    static formatBytes(value) {
        const units = ['B', 'KB', 'MB', 'GB', 'TB'];
        let v = Number(value) || 0;
        let i = 0;
        while (Math.abs(v) >= 1024 && i < units.length - 1) { v /= 1024; i++; }
        return v.toLocaleString('de-DE', { maximumFractionDigits: i === 0 ? 0 : 1 }) + ' ' + units[i];
    }

    static formatTime(ts) {
        return new Date(ts).toLocaleTimeString('de-DE', { hour: '2-digit', minute: '2-digit' });
    }

    /** [[unix_s, value]] → [[ms, value]] for a time axis. */
    static toTimeSeries(points) {
        return (points || []).map(p => [p[0] * 1000, p[1]]);
    }

    static init() {
        window.addEventListener('resize', () => SsmCharts.instances.forEach(e => e.chart && e.chart.resize()));
        // re-render when the light / dark switch changes the Bootstrap theme
        new MutationObserver(() => SsmCharts.instances.forEach(SsmCharts.render))
            .observe(document.documentElement, { attributes: true, attributeFilter: ['data-bs-theme'] });
    }
}

SsmCharts.init();
