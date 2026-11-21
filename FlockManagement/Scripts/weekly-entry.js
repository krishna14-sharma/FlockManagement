/* Weekly Flock Entry - live calculations. Mirrors the server formulas in sp_Flock_RecalculatePopulation,
   sp_CalculateUniformity and fn_VarianceStatus so the user sees the result before saving.
   Needs window.weeklyData (rendered by Weekly/Entry.cshtml) and fwm.js. */
(function ($) {
    'use strict';
    var D = window.weeklyData;
    if (!D) return;

    var std = {};               // parameter code -> standard row
    var formulaValues = {};     // nutrient code -> declared value of the selected formula
    var calc = {};              // latest actual values by parameter code

    var SUMMARY_ORDER = ['FEED_INTAKE', 'WEIGHT_GAIN', 'BODY_WEIGHT', 'ENERGY', 'PROTEIN', 'METHIONINE', 'LYSINE', 'LINOLEIC_ACID',
        'CALCIUM', 'PHOSPHORUS', 'FAT', 'FIBRE', 'SALT', 'HD_PRODUCTION', 'HATCHING_EGG_PCT', 'EGG_WEIGHT', 'MORTALITY', 'UNIFORMITY',
        'MALE_BODY_WEIGHT', 'MALE_UNIFORMITY', 'LIGHT_HOURS', 'LIGHT_INTENSITY', 'TEMP_DAY', 'TEMP_NIGHT'];

    function setStandards(list) {
        std = {};
        (list || []).forEach(function (s) { std[s.code] = s; });
    }

    function int(sel) { var v = fwm.parseNum($(sel).val()); return v === null ? 0 : Math.round(v); }

    function statusOf(code, actual) {
        var s = std[code];
        if (!s || s.std === null || actual === null || actual === undefined) return 'NODATA';
        return fwm.status(s.std, actual, s);
    }

    function badgeFor(code, actual) {
        var st = statusOf(code, actual);
        return st === 'NODATA' ? '' : fwm.badge(st);
    }

    // ------------------------------------------------------------------ population
    var pop = {};
    function recalcPopulation() {
        var mF = int('#FemaleMortality'), mM = int('#MaleMortality'), cF = int('#FemaleCulling'), cM = int('#MaleCulling');
        var closeF = D.open.F + D.received.F - mF - cF - D.outBirds.F;
        var closeM = D.open.M + D.received.M - mM - cM - D.outBirds.M;
        var openT = D.open.F + D.open.M;
        pop = { closeF: closeF, closeM: closeM, closeT: closeF + closeM, openT: openT, mort: mF + mM, cull: cF + cM };

        $('#totMort').text(fwm.num(mF + mM));
        $('#totCull').text(fwm.num(cF + cM));
        $('#closeF').text(fwm.num(closeF));
        $('#closeM').text(fwm.num(closeM));
        $('#closeT').text(fwm.num(closeF + closeM));
        $('#mortPctF').text(D.open.F > 0 ? fwm.num(mF * 100 / D.open.F, 2) + '%' : '-');
        $('#mortPctM').text(D.open.M > 0 ? fwm.num(mM * 100 / D.open.M, 2) + '%' : '-');

        calc.MORTALITY = openT > 0 ? (mF + mM) * 100 / openT : null;
        calc.MALE_MORTALITY = D.open.M > 0 ? mM * 100 / D.open.M : null;
        $('#mortPct').text(calc.MORTALITY !== null ? fwm.num(calc.MORTALITY, 2) + '%' : '-');
        $('#mortStatus').html(badgeFor('MORTALITY', calc.MORTALITY));

        var errors = [];
        if (closeF < 0) errors.push('Female mortality + culling is more than the female birds available.');
        if (closeM < 0) errors.push('Male mortality + culling is more than the male birds available.');
        $('#popError').toggleClass('d-none', errors.length === 0).html(errors.join('<br>'));
        checkReasons();
        recalcFeed();
        recalcProduction();
    }

    function checkReasons() {
        var total = 0, rows = 0;
        $('#reasonTable .js-reason-count').each(function () { var v = fwm.parseNum($(this).val()); if (v) { total += v; rows++; } });
        var expected = (pop.mort || 0) + (pop.cull || 0);
        if (!rows) { $('#reasonCheck').text(''); return; }
        $('#reasonCheck').html(total === expected
            ? '<span class="text-success"><i class="bi bi-check-circle"></i> Reasons add up to ' + fwm.num(total) + '</span>'
            : '<span class="text-warning-emphasis"><i class="bi bi-exclamation-triangle"></i> Reasons total ' + fwm.num(total) + ', mortality + culling is ' + fwm.num(expected) + '</span>');
    }

    // ------------------------------------------------------------------ body weight
    function histogram(canvasId, weights, stats) {
        if (!stats || !window.Chart) { if (fwm.charts[canvasId]) { fwm.charts[canvasId].destroy(); delete fwm.charts[canvasId]; } return; }
        var bins = 12, lo = Math.floor(stats.min), hi = Math.ceil(stats.max);
        if (hi === lo) hi = lo + 1;
        var width = (hi - lo) / bins, counts = [], labels = [], colors = [];
        for (var i = 0; i < bins; i++) {
            var from = lo + i * width, to = from + width, mid = (from + to) / 2;
            counts.push(weights.filter(function (w) { return w >= from && (i === bins - 1 ? w <= to : w < to); }).length);
            labels.push(Math.round(from));
            colors.push(mid >= stats.lower && mid <= stats.upper ? fwm.palette.actual : '#adb5bd');
        }
        fwm.barChart(canvasId, labels, [{ label: 'Birds', data: counts, backgroundColor: colors }], { yTitle: 'Birds' });
    }

    function recalcWeight() {
        var avg = { F: null, M: null };
        $('.js-samples').each(function () {
            var sex = $(this).data('sex'), weights = fwm.parseWeights($(this).val()), s = fwm.sampleStats(weights, D.rangePct);
            var $card = $(this).closest('.card-body');
            var set = function (k, v) { $card.find('[data-stat="' + k + '"]').html(v); };
            var bwCode = sex === 'M' ? 'MALE_BODY_WEIGHT' : 'BODY_WEIGHT', uniCode = sex === 'M' ? 'MALE_UNIFORMITY' : 'UNIFORMITY';
            if (!s) {
                ['n', 'avg', 'min', 'max', 'cv', 'uni'].forEach(function (k) { set(k, '-'); });
                set('status', '');
                calc[uniCode] = null;
            } else {
                avg[sex] = s.avg;
                calc[uniCode] = s.uniformity;
                set('n', fwm.num(s.n));
                set('avg', fwm.num(s.avg, 0));
                set('min', fwm.num(s.min, 0));
                set('max', fwm.num(s.max, 0));
                set('cv', fwm.num(s.cv, 1));
                set('uni', fwm.num(s.uniformity, 1) + '%');
                var bwStd = std[bwCode], msg = [];
                msg.push('Body weight ' + badgeFor(bwCode, s.avg) + (bwStd && bwStd.std !== null ? ' <span class="text-muted">std ' + fwm.num(bwStd.std, 0) + ' g</span>' : ''));
                msg.push('Uniformity ' + badgeFor(uniCode, s.uniformity) + (std[uniCode] && std[uniCode].std !== null ? ' <span class="text-muted">std ' + fwm.num(std[uniCode].std, 1) + '%</span>' : ''));
                msg.push('<span class="text-muted">range ' + fwm.num(s.lower, 0) + ' &ndash; ' + fwm.num(s.upper, 0) + ' g, ' + s.within + ' birds inside</span>');
                if (s.n < 50) msg.push('<span class="text-warning-emphasis"><i class="bi bi-exclamation-triangle"></i> fewer than 50 birds weighed</span>');
                set('status', msg.join(' &middot; '));
            }
            histogram('hist' + sex, weights, s);
        });

        var main = D.mainSex;
        var bw = avg[main] !== null ? avg[main] : fwm.parseNum($('#ManualBodyWeight').val());
        calc.BODY_WEIGHT = bw;
        calc.MALE_BODY_WEIGHT = main === 'F' ? avg.M : null;
        calc.WEIGHT_GAIN = bw !== null && D.prev.BODY_WEIGHT !== null ? bw - D.prev.BODY_WEIGHT : null;
        $('#gainVal').text(calc.WEIGHT_GAIN !== null ? fwm.signed(calc.WEIGHT_GAIN, 0, ' g') : '-');
        $('#gainStatus').html(badgeFor('WEIGHT_GAIN', calc.WEIGHT_GAIN));
        recalcSummary();
    }

    // ------------------------------------------------------------------ feed
    function recalcFeed() {
        var qty = 0, waste = 0, any = false;
        $('#feedTable .feed-row').each(function () {
            var q = fwm.parseNum($(this).find('.js-qty').val()), w = fwm.parseNum($(this).find('.js-waste').val());
            if (q !== null) { qty += q; any = true; }
            if (w !== null) waste += w;
        });
        var avgBirds = ((D.open.F + D.open.M) + (pop.closeT || 0)) / 2;
        var birdDays = avgBirds * 7;
        calc.FEED_INTAKE = any && birdDays > 0 ? (qty - waste) * 1000 / birdDays : null;
        $('#feedTotal').text(any ? fwm.num(qty, 2) + ' kg (net ' + fwm.num(qty - waste, 2) + ' kg)' : '-');
        $('#birdDays').text(birdDays > 0 ? fwm.num(birdDays) : '-');
        $('#intake').text(calc.FEED_INTAKE !== null ? fwm.num(calc.FEED_INTAKE, 1) : '-');
        $('#intakeStatus').html(badgeFor('FEED_INTAKE', calc.FEED_INTAKE));
        var s = std.FEED_INTAKE;
        $('#intakeStd').text(s && s.std !== null ? 'standard ' + fwm.num(s.std, 1) + (s.min !== null ? ' (' + fwm.num(s.min, 1) + ' – ' + fwm.num(s.max, 1) + ')' : '') : 'no standard');
        recalcSummary();
    }

    function reloadStandards() {
        var feedTypeId = $('#FeedTypeID').val();
        $.getJSON(D.urls.standards, { flockId: D.flockId, weekNo: D.weekNo, feedTypeId: feedTypeId || null })
            .done(function (list) { if ($.isArray(list)) { setStandards(list); recalcAll(); } })
            .fail(window.fwmAjaxError);
    }

    function reloadFormulaList($select, feedTypeId, keep) {
        return $.getJSON(D.urls.formulas, { feedTypeId: feedTypeId || null }).done(function (items) {
            var current = keep ? $select.val() : null;
            $select.empty().append($('<option>', { value: '', text: '-- No formula --' }));
            items.forEach(function (i) { $select.append($('<option>', { value: i.id, text: i.text })); });
            if (current && $select.find('option[value="' + current + '"]').length) $select.val(current);
            else if (items.length) $select.val(String(items[0].id));
        });
    }

    function loadFormulaValues() {
        var id = $('#FeedFormulaID').val();
        if (!id) { formulaValues = {}; recalcNutrition(); return; }
        $.getJSON(D.urls.formulaValues, { id: id }).done(function (list) {
            formulaValues = {};
            (list || []).forEach(function (v) { formulaValues[v.code] = v.value; });
            recalcNutrition();
        }).fail(window.fwmAjaxError);
    }

    // ------------------------------------------------------------------ nutrition
    function recalcNutrition() {
        var lab = $('input[name="NutritionSource"]:checked').val() === 'L';
        $('.js-lab').toggle(lab);
        $('#nutrientTable .js-lab-val').prop('disabled', !lab);
        $('#nutrientTable .nutrient-row').each(function () {
            var code = $(this).data('code'), s = std[code], dec = s ? s.dec : 2;
            var fv = formulaValues[code] !== undefined ? formulaValues[code] : null;
            var lv = fwm.parseNum($(this).find('.js-lab-val').val());
            var used = lab ? lv : fv;
            calc[code] = used;
            $(this).find('.js-formula-val').text(fwm.num(fv, dec));
            $(this).find('.js-used').text(fwm.num(used, dec));
            if (used !== null && s && s.std !== null) {
                var v = used - s.std, pct = s.std !== 0 ? v / s.std * 100 : null;
                $(this).find('.js-var').text(fwm.signed(v, dec) + (pct !== null ? ' (' + fwm.signed(pct, 1, '%') + ')' : ''));
            } else {
                $(this).find('.js-var').text('-');
            }
            $(this).find('.js-status').html(badgeFor(code, used));
        });
        recalcSummary();
    }

    // ------------------------------------------------------------------ production & environment
    function stdText(code, unitSuffix) {
        var s = std[code];
        if (!s || s.std === null) return 'no standard';
        return 'standard ' + fwm.num(s.std, s.dec) + (unitSuffix || '');
    }

    function rangeText(s) {
        if (!s || (s.min === null && s.max === null)) return '-';
        return (s.min !== null ? fwm.num(s.min, s.dec) : '') + ' \u2013 ' + (s.max !== null ? fwm.num(s.max, s.dec) : '');
    }

    function recalcProduction() {
        var val = function (id) { return fwm.parseNum($('#' + id).val()); };
        var total = val('Production_TotalEggs'), he = val('Production_HatchingEggs'), ew = val('Production_AvgEggWeight');
        var henDays = ((D.open.F || 0) + (pop.closeF || 0)) / 2 * 7;
        calc.HD_PRODUCTION = total !== null && henDays > 0 ? total * 100 / henDays : null;
        calc.HATCHING_EGG_PCT = total !== null && total > 0 && he !== null ? he * 100 / total : null;
        calc.EGG_WEIGHT = ew;
        $('#henDays').text(henDays > 0 ? fwm.num(henDays) : '-');
        $('#hdPct').text(calc.HD_PRODUCTION !== null ? fwm.num(calc.HD_PRODUCTION, 2) + '%' : '-');
        $('#hdStatus').html(badgeFor('HD_PRODUCTION', calc.HD_PRODUCTION));
        $('#hdStd').text(stdText('HD_PRODUCTION', '%'));
        $('#hePct').text(calc.HATCHING_EGG_PCT !== null ? fwm.num(calc.HATCHING_EGG_PCT, 1) + '%' : '-');
        $('#heStatus').html(badgeFor('HATCHING_EGG_PCT', calc.HATCHING_EGG_PCT));
        $('#heStd').text(stdText('HATCHING_EGG_PCT', '%'));
        $('#ewVal').text(ew !== null ? fwm.num(ew, 1) + ' g' : '-');
        $('#ewStatus').html(badgeFor('EGG_WEIGHT', ew));
        $('#ewStd').text(stdText('EGG_WEIGHT', ' g'));

        var errors = [];
        if (total !== null && he !== null && he > total) errors.push('Hatching eggs cannot exceed the total eggs collected.');
        if (total !== null && henDays > 0 && total > henDays * 1.05) errors.push('Total eggs are more than one egg per hen per day. Check the egg count.');
        $('#prodError').toggleClass('d-none', errors.length === 0).html(errors.join('<br>'));

        $('[data-env]').each(function () {
            var code = $(this).data('env'), s = std[code], v = fwm.parseNum($(this).find('.js-env').val());
            calc[code] = v;
            $(this).find('.js-std').text(s && s.std !== null ? fwm.num(s.std, s.dec) : '-');
            $(this).find('.js-rng').text(rangeText(s));
            $(this).find('.js-st').html(badgeFor(code, v));
        });
        recalcSummary();
    }

    // ------------------------------------------------------------------ summary
    function recalcSummary() {
        var $body = $('#summaryTable tbody').empty();
        SUMMARY_ORDER.forEach(function (code) {
            var s = std[code], a = calc[code];
            if (!s && (a === null || a === undefined)) return;
            if (code.indexOf('MALE_') === 0 && (a === null || a === undefined)) return;
            var dec = s ? s.dec : 2, sv = s ? s.std : null;
            var v = a !== null && a !== undefined && sv !== null ? a - sv : null;
            var pct = v !== null && sv ? v / sv * 100 : null;
            var prev = D.prev[code];
            $body.append('<tr>' +
                '<td data-label="Parameter" class="fw-semibold">' + (s ? s.name : code) + '</td>' +
                '<td data-label="Unit" class="text-muted">' + (s ? s.unit || '' : '') + '</td>' +
                '<td data-label="Standard" class="text-end">' + fwm.num(sv, dec) + '</td>' +
                '<td data-label="Actual" class="text-end fw-semibold">' + fwm.num(a, dec) + '</td>' +
                '<td data-label="Variance" class="text-end">' + (v !== null ? fwm.signed(v, dec) : '-') + '</td>' +
                '<td data-label="Variance %" class="text-end">' + (pct !== null ? fwm.signed(pct, 1, '%') : '-') + '</td>' +
                '<td data-label="Previous Week" class="text-end text-muted">' + (prev !== undefined && prev !== null ? fwm.num(prev, dec) : '-') + '</td>' +
                '<td data-label="Status">' + fwm.badge(statusOf(code, a)) + '</td></tr>');
        });
        if (!$body.children().length) $body.append('<tr><td colspan="8" class="text-center text-muted py-3">Nothing entered yet.</td></tr>');
    }

    function recalcAll() { recalcPopulation(); recalcWeight(); recalcNutrition(); recalcProduction(); }

    // ------------------------------------------------------------------ rows
    function reindex(selector, prefix) {
        $(selector).each(function (i) {
            $(this).find('[name^="' + prefix + '["]').each(function () {
                this.name = this.name.replace(new RegExp('^' + prefix + '\\[\\d+\\]'), prefix + '[' + i + ']');
            });
        });
    }

    function addRow(templateId, tableSel) {
        var html = document.getElementById(templateId).innerHTML;
        var $row = $(html);
        $(tableSel + ' tbody').append($row);
        return $row;
    }

    // ------------------------------------------------------------------ wiring
    $(function () {
        setStandards(D.standards);

        $(document).on('input change', '.js-pop', recalcPopulation);
        $(document).on('input change', '.js-reason-count', checkReasons);
        $(document).on('input', '.js-samples, #ManualBodyWeight', recalcWeight);
        $(document).on('input change', '#feedTable .js-qty, #feedTable .js-waste', recalcFeed);
        $(document).on('input change', '.js-lab-val', recalcNutrition);
        $(document).on('change', 'input[name="NutritionSource"]', recalcNutrition);
        $(document).on('input change', '.js-prod, .js-env', recalcProduction);

        $(document).on('click', '.js-remove-row', function () {
            $(this).closest('tr').remove();
            checkReasons();
            recalcFeed();
        });
        $('#addReason').on('click', function () { addRow('reasonTemplate', '#reasonTable'); });
        $('#addFeed').on('click', function () {
            var $row = addRow('feedTemplate', '#feedTable');
            var mainType = $('#FeedTypeID').val();
            if (mainType) {
                $row.find('.js-feedtype').val(mainType);
                reloadFormulaList($row.find('.js-formula'), mainType, false);
            }
        });
        $(document).on('change', '#feedTable .js-feedtype', function () {
            reloadFormulaList($(this).closest('tr').find('.js-formula'), $(this).val(), false);
        });

        $('#FeedTypeID').on('change', function () {
            var ft = $(this).val();
            reloadStandards();
            reloadFormulaList($('#FeedFormulaID'), ft, false).done(loadFormulaValues);
            // keep a single feed line in step with the main feed type
            var $rows = $('#feedTable .feed-row');
            if ($rows.length === 1 && ft) {
                $rows.find('.js-feedtype').val(ft);
                reloadFormulaList($rows.find('.js-formula'), ft, false);
            }
        });
        $('#FeedFormulaID').on('change', loadFormulaValues);

        // tabs
        var order = ['population', 'weight', 'feed', 'nutrition', 'production', 'summary'];
        $('[data-tab]').on('shown.bs.tab', function () {
            var t = $(this).data('tab');
            $('#Tab').val(t);
            if (t === 'weight') recalcWeight();   // charts need a visible canvas to size
            if (t === 'summary') recalcSummary();
        });
        function go(step) {
            var i = order.indexOf($('#Tab').val()) + step;
            if (i < 0 || i >= order.length) return;
            bootstrap.Tab.getOrCreateInstance(document.querySelector('[data-tab="' + order[i] + '"]')).show();
        }
        $('.js-prev-tab').on('click', function () { go(-1); });
        $('.js-next-tab').on('click', function () { go(1); });

        $('#weeklyForm').on('submit', function (e) {
            reindex('#reasonTable .reason-row', 'MortalityReasons');
            reindex('#feedTable .feed-row', 'FeedLines');
            recalcPopulation();
            if (pop.closeF < 0 || pop.closeM < 0) {
                e.preventDefault();
                go(-order.indexOf($('#Tab').val()));
                $('#popError')[0].scrollIntoView({ behavior: 'smooth', block: 'center' });
                return false;
            }
            $(window).off('beforeunload');
        });

        var dirty = false;
        $('#weeklyForm').on('input change', ':input', function () { dirty = true; });
        $(window).on('beforeunload', function () { if (dirty) return 'You have unsaved changes.'; });

        loadFormulaValues();
        recalcAll();
    });
})(jQuery);
