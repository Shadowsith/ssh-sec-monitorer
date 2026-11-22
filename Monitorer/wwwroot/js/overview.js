// Diagrams of the overview page (Apache ECharts). Data comes from the JSON block #overview-data.
class OverviewCharts {
    static init() {
        const el = document.getElementById('overview-data');
        if (!el) return;
        const d = JSON.parse(el.textContent || '{}');
        SsmCharts.create('chart-status', () => OverviewCharts.status(d));
        SsmCharts.create('chart-usage', () => OverviewCharts.usage(d));
        SsmCharts.create('chart-cpu-history', () => OverviewCharts.history(d.cpuHistory, d.cpuWarnPercent, d.cpuCriticalPercent));
        SsmCharts.create('chart-memory-history', () => OverviewCharts.history(d.memoryHistory));
        SsmCharts.create('chart-containers', () => OverviewCharts.containers(d.containers || []));
        SsmCharts.create('chart-live-checks', () => OverviewCharts.liveChecks(d.liveChecks || []));
    }

    /** Two donuts: agents and live checks by status. */
    static status(d) {
        const toData = obj => Object.entries(obj || {}).map(([status, count]) => ({
            name: SsmCharts.statusText(status), value: count, itemStyle: { color: SsmCharts.statusColor(status) }
        }));
        const agents = toData(d.agentStatus);
        const checks = toData(d.liveCheckStatus);
        if (agents.length === 0 && checks.length === 0) return SsmCharts.empty('Noch keine Daten');
        const ring = (name, data, center) => ({
            name, type: 'pie', radius: ['45%', '68%'], center, data, avoidLabelOverlap: true,
            label: { show: true, position: 'center', formatter: () => name, fontSize: 13, fontWeight: 'bold' },
            labelLine: { show: false }
        });
        return {
            tooltip: { trigger: 'item', formatter: p => `${p.seriesName}<br>${p.name}: <b>${p.value}</b>` },
            legend: { bottom: 0, type: 'scroll' },
            series: [ring('Agents', agents, ['27%', '45%']), ring('Live-Checks', checks, ['73%', '45%'])]
        };
    }

    /** Horizontal grouped bars: CPU / RAM / disk per VM, colored by threshold. */
    static usage(d) {
        const rows = (d.usage || []).slice().reverse();     // highest usage on top
        if (rows.length === 0) return SsmCharts.empty('Keine aktiven Agents');
        const bar = (name, key, color) => ({
            name, type: 'bar', barMaxWidth: 14, itemStyle: { color, borderRadius: [0, 3, 3, 0] },
            data: rows.map(r => r[key] === null ? null : Number(r[key]))
        });
        return {
            tooltip: { trigger: 'axis', axisPointer: { type: 'shadow' }, valueFormatter: v => v === null || v === undefined ? '–' : `${v.toLocaleString('de-DE')} %` },
            legend: { top: 0 },
            grid: { left: 8, right: 24, top: 32, bottom: 8, containLabel: true },
            xAxis: { type: 'value', min: 0, max: 100, axisLabel: { formatter: '{value} %' } },
            yAxis: { type: 'category', data: rows.map(r => r.name) },
            series: [bar('CPU', 'cpu', '#5470c6'), bar('RAM', 'memory', '#91cc75'), bar('Disk (max.)', 'disk', '#fac858')]
        };
    }

    /** One line per VM over time, optional warn / critical lines. */
    static history(byAgent, warn, critical) {
        const names = Object.keys(byAgent || {});
        if (names.length === 0) return SsmCharts.empty('Keine Verlaufsdaten');
        const series = names.map(name => ({
            name, type: 'line', showSymbol: false, smooth: true, lineStyle: { width: 1.6 },
            data: SsmCharts.toTimeSeries(byAgent[name])
        }));
        if (warn) {
            series[0].markLine = {
                silent: true, symbol: 'none',
                data: [
                    { yAxis: warn, lineStyle: { color: '#ffc107', type: 'dashed' }, label: { formatter: 'Warnung' } },
                    { yAxis: critical, lineStyle: { color: '#dc3545', type: 'dashed' }, label: { formatter: 'Kritisch' } }
                ]
            };
        }
        return {
            tooltip: { trigger: 'axis', valueFormatter: v => `${Number(v).toLocaleString('de-DE')} %` },
            legend: { type: 'scroll', top: 0 },
            grid: { left: 8, right: 56, top: 36, bottom: 8, containLabel: true },
            xAxis: { type: 'time', axisLabel: { formatter: v => SsmCharts.formatTime(v) } },
            yAxis: { type: 'value', min: 0, max: 100, axisLabel: { formatter: '{value} %' } },
            series
        };
    }

    /** Stacked bars: running / problem / stopped / missing containers per VM. */
    static containers(rows) {
        if (rows.length === 0) return SsmCharts.empty('Keine Container-Daten');
        const part = (name, key, color) => ({
            name, type: 'bar', stack: 'c', barMaxWidth: 28, itemStyle: { color }, data: rows.map(r => r[key]),
            emphasis: { focus: 'series' }
        });
        return {
            tooltip: { trigger: 'axis', axisPointer: { type: 'shadow' } },
            legend: { top: 0 },
            grid: { left: 8, right: 16, top: 32, bottom: 8, containLabel: true },
            xAxis: { type: 'category', data: rows.map(r => r.name), axisLabel: { interval: 0, rotate: rows.length > 5 ? 30 : 0 } },
            yAxis: { type: 'value', minInterval: 1 },
            series: [
                part('läuft', 'running', '#198754'),
                part('Problem', 'problem', '#dc3545'),
                part('gestoppt', 'stopped', '#adb5bd'),
                part('nicht mehr vorhanden', 'missing', '#6c757d')
            ]
        };
    }

    /** Bars with the last response time per live check; failed checks red. */
    static liveChecks(rows) {
        if (rows.length === 0) return SsmCharts.empty('Keine Live-Checks');
        return {
            tooltip: {
                trigger: 'axis', axisPointer: { type: 'shadow' },
                formatter: ps => {
                    const r = rows[ps[0].dataIndex];
                    return `${r.name}<br>${SsmCharts.statusText(r.status)}: <b>${r.responseMs === null ? '–' : r.responseMs + ' ms'}</b>`;
                }
            },
            grid: { left: 8, right: 16, top: 16, bottom: 8, containLabel: true },
            xAxis: { type: 'category', data: rows.map(r => r.name), axisLabel: { interval: 0, rotate: rows.length > 5 ? 30 : 0 } },
            yAxis: { type: 'value', axisLabel: { formatter: '{value} ms' } },
            series: [{
                type: 'bar', barMaxWidth: 36,
                data: rows.map(r => ({
                    value: r.status === 'Down' ? null : r.responseMs,
                    itemStyle: { color: r.status === 'Down' ? '#dc3545' : '#5470c6', borderRadius: [3, 3, 0, 0] }
                })),
                label: { show: true, position: 'top', formatter: p => rows[p.dataIndex].status === 'Down' ? 'down' : `${p.value ?? '–'} ms` }
            }]
        };
    }
}

document.addEventListener('DOMContentLoaded', OverviewCharts.init);
document.addEventListener('ssm:content-updated', OverviewCharts.init);
