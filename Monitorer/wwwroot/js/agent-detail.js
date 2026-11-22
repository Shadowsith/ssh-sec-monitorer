// History charts of the agent detail page (Apache ECharts). Data comes from the JSON block #history-data.
class AgentDetailCharts {
    static init() {
        const el = document.getElementById('history-data');
        if (!el) return;
        const host = (JSON.parse(el.textContent || '{}').host) || {};

        SsmCharts.create('chart-cpu-mem', () => AgentDetailCharts.lines([
            { name: 'CPU', points: host.cpu_percent },
            { name: 'RAM', points: host.memory_percent }
        ], v => `${I18n.number(v)} %`, { min: 0, max: 100, axisLabel: { formatter: '{value} %' } }));

        SsmCharts.create('chart-net', () => AgentDetailCharts.lines([
            { name: 'RX', points: host.net_rx_bps, area: true },
            { name: 'TX', points: host.net_tx_bps, area: true }
        ], v => `${SsmCharts.formatBytes(v)}/s`, { min: 0, axisLabel: { formatter: v => SsmCharts.formatBytes(v) + '/s' } }));
    }

    static lines(series, valueFormatter, yAxis) {
        const available = series.filter(s => Array.isArray(s.points) && s.points.length > 0);
        if (available.length === 0) return SsmCharts.empty(I18n.t('js.chart.noHistory'));
        return {
            tooltip: { trigger: 'axis', valueFormatter },
            legend: { top: 0 },
            grid: { left: 8, right: 16, top: 32, bottom: 8, containLabel: true },
            xAxis: { type: 'time', axisLabel: { formatter: v => SsmCharts.formatTime(v) } },
            yAxis: Object.assign({ type: 'value' }, yAxis),
            series: available.map(s => ({
                name: s.name, type: 'line', showSymbol: false, smooth: true,
                areaStyle: s.area ? { opacity: 0.15 } : undefined,
                data: SsmCharts.toTimeSeries(s.points)
            }))
        };
    }
}

document.addEventListener('DOMContentLoaded', AgentDetailCharts.init);
document.addEventListener('ssm:content-updated', AgentDetailCharts.init);
