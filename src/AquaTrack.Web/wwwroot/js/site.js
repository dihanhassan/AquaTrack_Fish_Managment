/* ===========================================================================
   AquaTrack client runtime
   Everything the HTMX-driven UI needs that HTMX itself does not provide:
   toasts, the shared modal, a progress bar, and Chart.js helpers.
   =========================================================================== */

(function () {
    'use strict';

    const AquaTrack = window.AquaTrack = {};

    /* ------------------------------------------------------------ toasts -- */

    const TOAST_TIMEOUT = 4000;

    AquaTrack.toast = function (message, level) {
        if (!message) return;

        let host = document.getElementById('aq-toasts');
        if (!host) {
            host = document.createElement('div');
            host.id = 'aq-toasts';
            host.className = 'aq-toasts';
            document.body.appendChild(host);
        }

        const el = document.createElement('div');
        el.className = 'aq-toast ' + (level || 'success');

        const text = document.createElement('div');
        text.textContent = message;
        el.appendChild(text);

        const close = document.createElement('button');
        close.type = 'button';
        close.className = 'aq-toast-close';
        close.innerHTML = '&times;';
        close.addEventListener('click', () => el.remove());
        el.appendChild(close);

        host.appendChild(el);
        setTimeout(() => el.remove(), TOAST_TIMEOUT);
    };

    document.body.addEventListener('aquatrack:toast', function (event) {
        const detail = event.detail || {};
        AquaTrack.toast(detail.message, detail.level);
    });

    /* ------------------------------------------------------------- modal -- */

    /*
     * One modal element is reused for every form in the app. Controllers return a
     * form partial into #aq-modal-content and this shows it; a 204 closes it.
     */
    AquaTrack.modal = {
        instance: null,

        element: function () {
            return document.getElementById('aq-modal');
        },

        show: function () {
            const el = this.element();
            if (!el) return;

            if (!this.instance) this.instance = new bootstrap.Modal(el, { backdrop: 'static' });
            this.instance.show();
        },

        hide: function () {
            if (this.instance) this.instance.hide();
        },

        clear: function () {
            const content = document.getElementById('aq-modal-content');
            if (content) content.innerHTML = '';
        }
    };

    // Any element that loads into the modal body opens it once the swap lands.
    document.body.addEventListener('htmx:afterSwap', function (event) {
        if (event.detail.target && event.detail.target.id === 'aq-modal-content') {
            AquaTrack.modal.show();
        }
    });

    document.body.addEventListener('aquatrack:closeModal', function () {
        AquaTrack.modal.hide();
    });

    // A 204 from a form post means "saved, nothing to swap" - so close the modal.
    document.body.addEventListener('htmx:beforeSwap', function (event) {
        if (event.detail.xhr && event.detail.xhr.status === 204) {
            event.detail.shouldSwap = false;
            AquaTrack.modal.hide();
        }

        // 422 carries a re-rendered form with validation errors; swap it in.
        if (event.detail.xhr && event.detail.xhr.status === 422) {
            event.detail.shouldSwap = true;
            event.detail.isError = false;
        }
    });

    // Drop the stale form once the modal has finished animating out.
    document.addEventListener('hidden.bs.modal', function (event) {
        if (event.target.id === 'aq-modal') AquaTrack.modal.clear();
    });

    /* ---------------------------------------------------------- progress -- */

    const progress = document.getElementById('aq-progress');
    let inFlight = 0;

    document.body.addEventListener('htmx:beforeRequest', function () {
        inFlight++;
        if (progress) progress.className = 'active';
    });

    function requestDone() {
        inFlight = Math.max(0, inFlight - 1);
        if (inFlight === 0 && progress) {
            progress.className = 'done';
            setTimeout(() => { if (inFlight === 0) progress.className = ''; }, 220);
        }
    }

    document.body.addEventListener('htmx:afterRequest', requestDone);
    document.body.addEventListener('htmx:responseError', requestDone);
    document.body.addEventListener('htmx:sendError', requestDone);

    document.body.addEventListener('htmx:sendError', function () {
        AquaTrack.toast('Could not reach the server. Check your connection.', 'danger');
    });

    document.body.addEventListener('htmx:responseError', function (event) {
        const status = event.detail.xhr.status;
        // 422 is a handled validation response, not an error the user needs told about.
        if (status === 422 || status === 401 || status === 403) return;

        AquaTrack.toast('Something went wrong (HTTP ' + status + ').', 'danger');
    });

    /* ------------------------------------------------------- confirm hook -- */

    // hx-confirm renders through the browser dialog; this keeps the wording consistent.
    document.body.addEventListener('htmx:confirm', function (event) {
        const question = event.detail.question;
        if (!question) return;

        event.preventDefault();
        if (window.confirm(question)) event.detail.issueRequest(true);
    });

    /* ------------------------------------------------------------ layout -- */

    AquaTrack.toggleSidebar = function () {
        const sidebar = document.querySelector('.aq-sidebar');
        const scrim = document.querySelector('.aq-scrim');
        if (!sidebar) return;

        sidebar.classList.toggle('open');
        if (scrim) scrim.classList.toggle('show', sidebar.classList.contains('open'));
    };

    document.addEventListener('click', function (event) {
        if (event.target.closest('[data-aq-toggle-sidebar]')) {
            event.preventDefault();
            AquaTrack.toggleSidebar();
        }

        if (event.target.classList.contains('aq-scrim')) AquaTrack.toggleSidebar();
    });

    /* ------------------------------------------------------------ charts -- */

    const palette = ['#0f6fa8', '#0fa89a', '#a86a0f', '#16794c', '#b3261e', '#6f42c1', '#0d9488', '#be6b1f'];
    const charts = {};

    const baseOptions = {
        responsive: true,
        maintainAspectRatio: false,
        interaction: { mode: 'index', intersect: false },
        plugins: {
            legend: { labels: { boxWidth: 10, boxHeight: 10, font: { size: 11 }, usePointStyle: true } },
            tooltip: {
                backgroundColor: '#16202b',
                padding: 10,
                titleFont: { size: 12 },
                bodyFont: { size: 12 },
                cornerRadius: 6
            }
        },
        scales: {
            x: { grid: { display: false }, ticks: { font: { size: 11 }, color: '#5c6b7a' } },
            y: {
                beginAtZero: true,
                grid: { color: '#eef1f5' },
                border: { display: false },
                ticks: { font: { size: 11 }, color: '#5c6b7a' }
            }
        }
    };

    /**
     * Renders (or re-renders) a chart into a canvas by id. Safe to call repeatedly:
     * an existing chart on the same canvas is destroyed first so HTMX swaps do not leak.
     */
    AquaTrack.chart = function (canvasId, type, labels, datasets, extraOptions) {
        const canvas = document.getElementById(canvasId);
        if (!canvas || typeof Chart === 'undefined') return;

        if (charts[canvasId]) {
            charts[canvasId].destroy();
            delete charts[canvasId];
        }

        const coloured = datasets.map(function (ds, index) {
            const colour = ds.color || palette[index % palette.length];
            const base = {
                borderColor: colour,
                backgroundColor: type === 'line' ? colour + '22' : colour,
                borderWidth: type === 'line' ? 2 : 0,
                borderRadius: type === 'bar' ? 4 : 0,
                tension: .32,
                fill: type === 'line',
                pointRadius: 0,
                pointHoverRadius: 4
            };

            if (type === 'doughnut' || type === 'pie') {
                base.backgroundColor = labels.map((_, i) => palette[i % palette.length]);
                base.borderColor = '#fff';
                base.borderWidth = 2;
            }

            return Object.assign(base, ds);
        });

        const options = Object.assign({}, baseOptions, extraOptions || {});
        if (type === 'doughnut' || type === 'pie') delete options.scales;

        charts[canvasId] = new Chart(canvas, {
            type: type,
            data: { labels: labels, datasets: coloured },
            options: options
        });
    };

    // Charts inside a swapped-out region must be torn down or Chart.js keeps the canvas alive.
    document.body.addEventListener('htmx:beforeSwap', function (event) {
        const target = event.detail.target;
        if (!target) return;

        Object.keys(charts).forEach(function (id) {
            const canvas = document.getElementById(id);
            if (canvas && target.contains(canvas)) {
                charts[id].destroy();
                delete charts[id];
            }
        });
    });

    /* ------------------------------------------------- line-item helpers -- */

    /*
     * Repeating form rows (purchase lines, sale lines, daily feed rows) are indexed
     * by position so the MVC model binder can bind them as a list. Re-indexing after
     * an add or remove keeps that contract intact.
     */
    AquaTrack.reindexRows = function (containerId) {
        const container = document.getElementById(containerId);
        if (!container) return;

        const rows = container.querySelectorAll('[data-row]');
        rows.forEach(function (row, index) {
            row.setAttribute('data-row-index', index);

            row.querySelectorAll('[name]').forEach(function (input) {
                input.name = input.name.replace(/\[\d+\]/, '[' + index + ']');
            });

            const label = row.querySelector('[data-row-number]');
            if (label) label.textContent = index + 1;
        });

        AquaTrack.recalcTotals(containerId);
    };

    AquaTrack.addRow = function (containerId, templateId) {
        const container = document.getElementById(containerId);
        const template = document.getElementById(templateId);
        if (!container || !template) return;

        const fragment = template.content.cloneNode(true);
        container.appendChild(fragment);
        AquaTrack.reindexRows(containerId);
    };

    AquaTrack.removeRow = function (button, containerId) {
        const row = button.closest('[data-row]');
        if (!row) return;

        const container = document.getElementById(containerId);
        // Always leave one row so the form never becomes unusable.
        if (container && container.querySelectorAll('[data-row]').length <= 1) {
            row.querySelectorAll('input, select').forEach(function (input) {
                if (input.type !== 'hidden') input.value = '';
            });
            AquaTrack.recalcTotals(containerId);
            return;
        }

        row.remove();
        AquaTrack.reindexRows(containerId);
    };

    /** Multiplies quantity by unit price per row and sums the result into the footer. */
    AquaTrack.recalcTotals = function (containerId) {
        const container = document.getElementById(containerId);
        if (!container) return;

        let subTotal = 0;

        container.querySelectorAll('[data-row]').forEach(function (row) {
            const qty = parseFloat(row.querySelector('[data-qty]')?.value) || 0;
            const price = parseFloat(row.querySelector('[data-price]')?.value) || 0;
            const lineTotal = qty * price;

            const output = row.querySelector('[data-line-total]');
            if (output) output.textContent = lineTotal.toFixed(2);

            subTotal += lineTotal;
        });

        const form = container.closest('form') || document;
        const discount = parseFloat(form.querySelector('[data-discount]')?.value) || 0;
        const transport = parseFloat(form.querySelector('[data-transport]')?.value) || 0;
        const grandTotal = subTotal - discount + transport;

        const subEl = form.querySelector('[data-subtotal]');
        if (subEl) subEl.textContent = subTotal.toFixed(2);

        const grandEl = form.querySelector('[data-grandtotal]');
        if (grandEl) grandEl.textContent = grandTotal.toFixed(2);

        // Cash sales default the paid amount to the full total until the user overrides it.
        const paid = form.querySelector('[data-paid]');
        if (paid && paid.dataset.autofill !== 'off') paid.value = grandTotal.toFixed(2);

        const dueEl = form.querySelector('[data-due]');
        if (dueEl) {
            const paidValue = parseFloat(paid?.value) || 0;
            dueEl.textContent = (grandTotal - paidValue).toFixed(2);
        }
    };

    document.addEventListener('input', function (event) {
        const container = event.target.closest('[data-line-container]');
        if (container) AquaTrack.recalcTotals(container.id);

        if (event.target.matches('[data-discount], [data-transport], [data-paid]')) {
            if (event.target.matches('[data-paid]')) event.target.dataset.autofill = 'off';

            const form = event.target.closest('form');
            const lines = form?.querySelector('[data-line-container]');
            if (lines) AquaTrack.recalcTotals(lines.id);
        }
    });

    /* -------------------------------------------------------- form reset -- */

    // After a successful save, reset any form flagged to clear itself (quick-entry screens).
    document.body.addEventListener('aquatrack:refresh', function () {
        document.querySelectorAll('form[data-reset-on-save]').forEach(function (form) {
            form.reset();
        });
    });
})();
