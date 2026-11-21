/* Flock Weight Management - shared analysis helpers (status logic, formatting, Chart.js builders).
   Status labels / CSS come from tblStatusConfig via window.fwmStatuses (rendered by the layout). */
(function (window, $) {
    'use strict';

    var fwm = window.fwm = window.fwm || {};

    fwm.statuses = window.fwmStatuses || {};

    fwm.statusDef = function (code) {
        return fwm.statuses[code] || { name: code || 'No Data', css: 'status-nodata', color: '#6c757d' };
    };

    fwm.badge = function (code, text) {
        var d = fwm.statusDef(code);
        return '<span class="badge status-badge ' + d.css + '">' + (text || d.name) + '</span>';
    };

    fwm.color = function (code) { return fwm.statusDef(code).color; };

    /* Mirrors dbo.fn_VarianceStatus / VarianceCalculator.Evaluate */
    fwm.status = function (std, act, s) {
        if (std === null || std === undefined || act === null || act === undefined || isNaN(act)) return 'NODATA';
        s = s || {};
        var pct = std !== 0 ? (act - std) / std * 100 : 0, abs = Math.abs(pct), low = act < std;
        var fav = (s.dir === 'H' && !low) || (s.dir === 'L' && low);
        var warn = s.warn !== null && s.warn !== undefined ? s.warn : 5, crit = s.crit !== null && s.crit !== undefined ? s.crit : 10;
        if (!fav && ((s.cmin !== null && s.cmin !== undefined && act < s.cmin) || (s.cmax !== null && s.cmax !== undefined && act > s.cmax))) return 'CRITICAL';
        var hasMin = s.min !== null && s.min !== undefined, hasMax = s.max !== null && s.max !== undefined;
        if (hasMin || hasMax) {
            if ((!hasMin || act >= s.min) && (!hasMax || act <= s.max)) return 'WITHIN';
            var hasCrit = (s.cmin !== null && s.cmin !== undefined) || (s.cmax !== null && s.cmax !== undefined);
            if (!fav && !hasCrit && abs > crit) return 'CRITICAL';
            return hasMin && act < s.min ? 'BELOW' : 'ABOVE';
        }
        if (abs <= warn) return 'WITHIN';
        if (!fav && abs > crit) return 'CRITICAL';
        return low ? 'BELOW' : 'ABOVE';
    };

    fwm.num = function (v, dec) {
        if (v === null || v === undefined || v === '' || isNaN(v)) return '-';
        return Number(v).toLocaleString('en-IN', { minimumFractionDigits: dec || 0, maximumFractionDigits: dec || 0 });
    };

    fwm.signed = function (v, dec, suffix) {
        if (v === null || v === undefined || isNaN(v)) return '-';
        return (v > 0 ? '+' : '') + fwm.num(v, dec) + (suffix || '');
    };

    fwm.parseNum = function (v) {
        if (v === null || v === undefined) return null;
        var s = String(v).replace(/,/g, '').trim();
        if (s === '') return null;
        var n = Number(s);
        return isNaN(n) ? null : n;
    };

    /* Weight samples: avg, min, max, sd (sample), cv, uniformity within +/- range % */
    fwm.sampleStats = function (weights, rangePct) {
        var n = weights.length;
        if (!n) return null;
        var sum = 0, min = Infinity, max = -Infinity;
        weights.forEach(function (w) { sum += w; if (w < min) min = w; if (w > max) max = w; });
        var avg = sum / n, ss = 0;
        weights.forEach(function (w) { ss += (w - avg) * (w - avg); });
        var sd = n > 1 ? Math.sqrt(ss / (n - 1)) : null;
        var lower = avg * (1 - rangePct / 100), upper = avg * (1 + rangePct / 100);
        var within = weights.filter(function (w) { return w >= lower && w <= upper; }).length;
        return { n: n, avg: avg, min: min, max: max, sd: sd, cv: sd !== null && avg ? sd / avg * 100 : null, lower: lower, upper: upper, within: within, uniformity: within * 100 / n };
    };

    fwm.parseWeights = function (text) {
        return String(text || '').split(/[\s,;]+/).map(function (x) { return fwm.parseNum(x); }).filter(function (x) { return x !== null && x > 0; });
    };

    // ------------------------------------------------------------------ charts
    var palette = { actual: '#1f5f4a', standard: '#0d6efd', band: 'rgba(13,110,253,0.10)', grid: 'rgba(0,0,0,0.06)' };
    fwm.palette = palette;
    fwm.charts = {};

    function destroy(id) { if (fwm.charts[id]) { fwm.charts[id].destroy(); delete fwm.charts[id]; } }

    fwm.findSeries = function (trend, code) {
        if (!trend || !trend.series) return null;
        for (var i = 0; i < trend.series.length; i++) if (trend.series[i].code === code) return trend.series[i];
        return null;
    };

    /* Standard vs actual line chart with min-max band and status-coloured points. */
    fwm.stdActualChart = function (canvasId, series, opts) {
        destroy(canvasId);
        var el = document.getElementById(canvasId);
        if (!el || !series || !window.Chart) return null;
        opts = opts || {};
        var labels = series.points.map(function (p) { return 'Wk ' + p.w; });
        var datasets = [];
        var hasBand = series.points.some(function (p) { return p.mn !== null || p.mx !== null; });
        if (hasBand && !opts.noBand) {
            datasets.push({ label: 'Max', data: series.points.map(function (p) { return p.mx; }), borderWidth: 0, pointRadius: 0, fill: '+1', backgroundColor: palette.band, spanGaps: true });
            datasets.push({ label: 'Min', data: series.points.map(function (p) { return p.mn; }), borderWidth: 0, pointRadius: 0, fill: false, spanGaps: true });
        }
        datasets.push({ label: opts.stdLabel || 'Standard', data: series.points.map(function (p) { return p.s; }), borderColor: palette.standard, borderDash: [6, 4], borderWidth: 2, pointRadius: 0, fill: false, spanGaps: true });
        datasets.push({
            label: opts.actLabel || 'Actual', data: series.points.map(function (p) { return p.a; }), borderColor: palette.actual, backgroundColor: palette.actual,
            borderWidth: 2.5, pointRadius: 3.5, pointBackgroundColor: series.points.map(function (p) { return fwm.color(p.st); }), fill: false, spanGaps: true,
            type: opts.bar ? 'bar' : 'line'
        });
        fwm.charts[canvasId] = new Chart(el, {
            type: 'line',
            data: { labels: labels, datasets: datasets },
            options: {
                responsive: true, maintainAspectRatio: false, interaction: { mode: 'index', intersect: false },
                plugins: {
                    legend: { labels: { filter: function (item) { return item.text !== 'Max' && item.text !== 'Min'; }, boxWidth: 12 } },
                    tooltip: {
                        filter: function (item) { return item.dataset.label !== 'Max' && item.dataset.label !== 'Min'; },
                        callbacks: {
                            label: function (ctx) { return ctx.dataset.label + ': ' + fwm.num(ctx.parsed.y, series.dec) + ' ' + (series.unit || ''); },
                            afterBody: function (items) {
                                var p = series.points[items[0].dataIndex];
                                return p && p.v !== null ? 'Variance: ' + fwm.signed(p.v, 2, '%') + '  (' + fwm.statusDef(p.st).name + ')' : '';
                            }
                        }
                    }
                },
                scales: {
                    y: { title: { display: !!series.unit, text: series.unit }, grid: { color: palette.grid }, beginAtZero: !!opts.zero },
                    x: { grid: { display: false } }
                },
                onClick: opts.onClick
            }
        });
        return fwm.charts[canvasId];
    };

    fwm.barChart = function (canvasId, labels, datasets, opts) {
        destroy(canvasId);
        var el = document.getElementById(canvasId);
        if (!el || !window.Chart) return null;
        opts = opts || {};
        fwm.charts[canvasId] = new Chart(el, {
            type: 'bar',
            data: { labels: labels, datasets: datasets },
            options: {
                responsive: true, maintainAspectRatio: false, indexAxis: opts.horizontal ? 'y' : 'x',
                plugins: { legend: { display: datasets.length > 1, labels: { boxWidth: 12 } } },
                scales: { y: { grid: { color: palette.grid }, title: { display: !!opts.yTitle, text: opts.yTitle }, suggestedMin: opts.min, suggestedMax: opts.max }, x: { grid: { display: false } } }
            }
        });
        return fwm.charts[canvasId];
    };

    fwm.loadTrend = function (url, flockId) {
        return $.getJSON(url, { flockId: flockId }).fail(window.fwmAjaxError);
    };

})(window, jQuery);
