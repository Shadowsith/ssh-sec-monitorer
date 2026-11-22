// Shared Apache ECharts helpers: theme (light / dark like Bootstrap), resizing, formatting, status colors.
class SsmCharts {
    static instances = [];

    /** Creates a chart in the element with the given id; option is a function so it can be rebuilt on theme change. */
    static create(id, buildOption) {
        const el = document.getElementById(id);
        if (!el || typeof echarts === 'undefined') return null;
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
            case 'Warning': return '#ffc107';
            case 'Critical': return '#dc3545';
            case 'Unreachable': case 'Down': return '#842029';
            case 'Disabled': return '#adb5bd';
            default: return '#6c757d';
        }
    }

    static statusText(status) {
        return {
            Ok: 'OK', Up: 'Erreichbar', Warning: 'Warnung', Critical: 'Kritisch', Unreachable: 'Nicht erreichbar',
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
