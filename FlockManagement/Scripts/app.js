/* Flock Weight Management - shared client behaviour (jQuery + Bootstrap 5) */
(function ($) {
    'use strict';

    // ----- Unobtrusive validation: Bootstrap 5 styling -----
    if ($.validator) {
        $.validator.setDefaults({
            highlight: function (el) { $(el).addClass('is-invalid').removeClass('is-valid'); },
            unhighlight: function (el) { $(el).removeClass('is-invalid'); },
            ignore: ':hidden, [readonly], :disabled'
        });
        // Accept yyyy-MM-dd from <input type="date"> regardless of browser culture
        $.validator.methods.date = function (value, element) {
            return this.optional(element) || /^\d{4}-\d{2}-\d{2}$/.test(value) || !/Invalid|NaN/.test(new Date(value).toString());
        };
    }
    // Mark server-side errors too
    $('.input-validation-error').addClass('is-invalid');

    // ----- Confirmation for destructive actions: <button data-confirm="..."> -----
    $(document).on('click', '[data-confirm]', function (e) {
        if (!window.confirm($(this).data('confirm'))) { e.preventDefault(); e.stopImmediatePropagation(); }
    });

    // ----- Prevent double submission -----
    $(document).on('submit', 'form', function () {
        var $form = $(this);
        if ($form.attr('method') && $form.attr('method').toLowerCase() === 'get') return;
        if ($.fn.valid && $form.data('validator') && !$form.valid()) return;
        $form.find('button[type=submit]').prop('disabled', true).each(function () {
            var $b = $(this);
            $b.data('label', $b.html()).html('<span class="spinner-border spinner-border-sm me-1"></span>Saving...');
        });
    });

    // ----- Auto-hide success alerts -----
    $('[data-autohide]').each(function () {
        var el = this, ms = parseInt($(el).data('autohide'), 10) || 5000;
        setTimeout(function () { if (window.bootstrap) { bootstrap.Alert.getOrCreateInstance(el).close(); } }, ms);
    });

    // ----- Show / hide password -----
    $(document).on('click', '[data-toggle-password]', function () {
        var $input = $($(this).data('toggle-password'));
        var show = $input.attr('type') === 'password';
        $input.attr('type', show ? 'text' : 'password');
        $(this).find('i').toggleClass('bi-eye', !show).toggleClass('bi-eye-slash', show);
    });

    // ----- Cascading dropdowns -----
    // <select data-cascade-target="#child" data-cascade-url="..." data-cascade-empty="..."
    //         data-cascade-param="farmId" data-cascade-include="farmId:#FarmID">
    $(document).on('change', 'select[data-cascade-target]', function () {
        var $parent = $(this);
        var $child = $($parent.data('cascade-target'));
        var empty = $parent.data('cascade-empty') || '-- Select --';
        var param = $parent.data('cascade-param') || 'farmId';
        var value = $parent.val();
        var query = {};
        query[param] = value;
        var include = ($parent.data('cascade-include') || '').toString();
        if (include) {
            include.split(',').forEach(function (pair) {
                var bits = pair.split(':');
                if (bits.length === 2) query[bits[0]] = $(bits[1]).val();
            });
        }
        $child.empty().append($('<option/>').val('').text(empty));
        var always = $parent.data('cascade-always') === true || $parent.data('cascade-always') === 'true';
        if (!value && !always) { $child.trigger('change'); return; }
        $child.prop('disabled', true);
        $.getJSON($parent.data('cascade-url'), query)
            .done(function (items) {
                $.each(items, function (_, item) { $child.append($('<option/>').val(item.id).text(item.text)); });
            })
            .fail(handleAjaxError)
            .always(function () { $child.prop('disabled', false).trigger('change'); });
    });

    // ----- Auto-submit filter forms: <select data-autosubmit> -----
    $(document).on('change', '[data-autosubmit]', function () { $(this).closest('form').trigger('submit'); });

    // ----- Export any table to CSV: <button data-export-csv="#tableId" data-export-name="Farms"> -----
    $(document).on('click', '[data-export-csv]', function () {
        var $table = $($(this).data('export-csv'));
        var name = ($(this).data('export-name') || 'export').toString().replace(/[^a-z0-9]+/gi, '_');
        var rows = [];
        $table.find('tr').each(function () {
            var cells = [];
            $(this).find('th, td').not('.no-export').each(function () {
                if ($(this).attr('colspan')) return;
                var text = $(this).text().replace(/\s+/g, ' ').trim();
                cells.push('"' + text.replace(/"/g, '""') + '"');
            });
            if (cells.length) rows.push(cells.join(','));
        });
        var blob = new Blob(['﻿' + rows.join('\r\n')], { type: 'text/csv;charset=utf-8;' });
        var link = document.createElement('a');
        link.href = URL.createObjectURL(blob);
        link.download = name + '_' + new Date().toISOString().slice(0, 10) + '.csv';
        document.body.appendChild(link);
        link.click();
        setTimeout(function () { URL.revokeObjectURL(link.href); link.remove(); }, 100);
    });

    // ----- AJAX session handling -----
    function handleAjaxError(xhr) {
        if (xhr.status === 401) { window.location.reload(); return; }
        if (xhr.status === 403) { window.alert('You do not have permission for this action.'); return; }
        window.alert('The server could not complete the request. Please try again.');
    }
    window.fwmAjaxError = handleAjaxError;

})(jQuery);
