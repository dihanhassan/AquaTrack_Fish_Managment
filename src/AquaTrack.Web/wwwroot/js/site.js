/* ===========================================================================
   AquaTrack client runtime
   Everything the HTMX-driven UI needs that HTMX itself does not provide:
   toasts, the shared modal, a progress bar, and Chart.js helpers.
   =========================================================================== */

(function () {
    'use strict';

    const AquaTrack = window.AquaTrack = {};

    /* ------------------------------------------------------------ toasts -- */

    // An error stays up longer, because it usually asks the reader to do something.
    const TOAST_TIMEOUT = { success: 3500, info: 4000, warning: 6000, danger: 7000 };

    const TOAST_STYLE = {
        success: { icon: 'bi-check-lg', title: 'Saved' },
        info: { icon: 'bi-info-lg', title: 'Note' },
        warning: { icon: 'bi-exclamation-lg', title: 'Check this' },
        danger: { icon: 'bi-x-lg', title: 'Could not save' }
    };

    const MAX_VISIBLE_TOASTS = 4;

    function toastHost() {
        let host = document.getElementById('aq-toasts');
        if (!host) {
            host = document.createElement('div');
            host.id = 'aq-toasts';
            host.className = 'aq-toasts';
            document.body.appendChild(host);
        }
        return host;
    }

    function dismissToast(el) {
        if (!el || el.classList.contains('leaving')) return;

        el.classList.add('leaving');
        el.addEventListener('animationend', () => el.remove(), { once: true });
        // Belt and braces: if the animation never fires, still clean up.
        setTimeout(() => el.remove(), 400);
    }

    /**
     * Shows a toast. `level` is success | info | warning | danger.
     * A `title` overrides the default heading for the level.
     */
    AquaTrack.toast = function (message, level, title) {
        if (!message) return;

        level = TOAST_STYLE[level] ? level : 'success';

        const host = toastHost();
        const style = TOAST_STYLE[level];
        const timeout = TOAST_TIMEOUT[level];

        // Repeating the same message stacks noise rather than information.
        const duplicate = Array.from(host.children).find(function (child) {
            return child.dataset.message === message && !child.classList.contains('leaving');
        });

        if (duplicate) {
            duplicate.classList.remove('aq-toast-pulse');
            void duplicate.offsetWidth;
            duplicate.classList.add('aq-toast-pulse');
            return;
        }

        const el = document.createElement('div');
        el.className = 'aq-toast ' + level;
        el.dataset.message = message;
        el.setAttribute('role', level === 'danger' ? 'alert' : 'status');

        const icon = document.createElement('div');
        icon.className = 'aq-toast-icon';
        icon.innerHTML = '<i class="bi ' + style.icon + '"></i>';
        el.appendChild(icon);

        const body = document.createElement('div');
        body.className = 'aq-toast-body';

        const heading = document.createElement('div');
        heading.className = 'aq-toast-title';
        heading.textContent = title || style.title;
        body.appendChild(heading);

        const text = document.createElement('div');
        text.className = 'aq-toast-text';
        text.textContent = message;
        body.appendChild(text);

        el.appendChild(body);

        const close = document.createElement('button');
        close.type = 'button';
        close.className = 'aq-toast-close';
        close.setAttribute('aria-label', 'Dismiss');
        close.innerHTML = '&times;';
        close.addEventListener('click', () => dismissToast(el));
        el.appendChild(close);

        const timer = document.createElement('div');
        timer.className = 'aq-toast-timer';
        timer.style.animationDuration = timeout + 'ms';
        el.appendChild(timer);

        host.appendChild(el);

        // Keep the stack short; the oldest has been on screen longest.
        while (host.children.length > MAX_VISIBLE_TOASTS) {
            dismissToast(host.firstElementChild);
        }

        let remaining = setTimeout(() => dismissToast(el), timeout);

        // Hovering pauses the countdown, so a long message can actually be read.
        el.addEventListener('mouseenter', function () {
            clearTimeout(remaining);
            timer.style.animationPlayState = 'paused';
        });

        el.addEventListener('mouseleave', function () {
            timer.style.animationPlayState = 'running';
            remaining = setTimeout(() => dismissToast(el), 1500);
        });
    };

    AquaTrack.dismissAllToasts = function () {
        const host = document.getElementById('aq-toasts');
        if (host) Array.from(host.children).forEach(dismissToast);
    };

    document.body.addEventListener('aquatrack:toast', function (event) {
        const detail = event.detail || {};
        AquaTrack.toast(detail.message, detail.level, detail.title);
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
        },

        // A form partial can opt into a wider dialog with data-modal-size="lg|xl" on its
        // root element (see Sales/Purchases line-item forms). Reset first so a small form
        // loaded after a wide one doesn't inherit the previous size.
        resize: function (content) {
            const dialog = document.getElementById('aq-modal-dialog');
            if (!dialog || !content) return;

            dialog.classList.remove('modal-lg', 'modal-xl');
            const sized = content.querySelector('[data-modal-size]');
            if (sized) dialog.classList.add('modal-' + sized.getAttribute('data-modal-size'));
        }
    };

    // Any element that loads into the modal body opens it once the swap lands.
    document.body.addEventListener('htmx:afterSwap', function (event) {
        if (event.detail.target && event.detail.target.id === 'aq-modal-content') {
            AquaTrack.modal.resize(event.detail.target);
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

    /**
     * Reads the errors out of a freshly swapped form and raises a toast for them.
     *
     * The form already shows errors inline, but a long form can push the offending field off
     * screen, and a modal body scrolls independently. Surfacing the first error as a toast means
     * a rejected save is never silent, whatever the page looks like.
     */
    function announceValidation(root) {
        if (!root || !root.querySelectorAll) return;

        // Field-level errors that did render inline — the specific, actionable ones.
        const fieldMessages = [];
        root.querySelectorAll('.field-validation-error').forEach(function (span) {
            const text = span.textContent.trim();
            if (text) fieldMessages.push(text);
        });

        const messages = [];

        // Form-level errors, which have no inline slot of their own. The generic
        // "check the highlighted fields" stand-in (data-generic) just restates what the field
        // messages already say, so once we have those, skip it and show the real text instead.
        root.querySelectorAll('.alert-danger').forEach(function (alert) {
            if (alert.hasAttribute('data-generic') && fieldMessages.length) return;

            const items = alert.querySelectorAll('li');

            if (items.length) {
                items.forEach(li => messages.push(li.textContent.trim()));
            } else {
                const text = alert.textContent.trim();
                if (text) messages.push(text);
            }
        });

        messages.push.apply(messages, fieldMessages);

        const unique = messages.filter(function (m, i) { return m && messages.indexOf(m) === i; });
        if (!unique.length) return;

        AquaTrack.toast(
            unique.length === 1 ? unique[0] : unique[0] + ' (+' + (unique.length - 1) + ' more)',
            'warning',
            'Check your entries');

        // Put the cursor where the problem is, so the fix is one keystroke away.
        const firstBad = root.querySelector('.input-validation-error, .is-invalid');
        if (firstBad && typeof firstBad.focus === 'function') {
            firstBad.focus({ preventScroll: false });
        }
    }

    document.body.addEventListener('htmx:afterSwap', function (event) {
        if (event.detail.xhr && event.detail.xhr.status === 422) {
            announceValidation(event.detail.target);
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
        AquaTrack.toast(
            'Could not reach the server. Check your connection and try again.',
            'danger',
            'Offline');
    });

    document.body.addEventListener('htmx:timeout', function () {
        AquaTrack.toast('The server took too long to respond. Try again.', 'danger', 'Timed out');
    });

    document.body.addEventListener('htmx:responseError', function (event) {
        const status = event.detail.xhr.status;

        // 422 is a handled validation response; the swap handler announces it instead.
        // 401 and 403 redirect to sign-in or the denied page, which explain themselves.
        if (status === 422 || status === 401 || status === 403) return;

        const message = status === 404
            ? 'That record no longer exists. The list may be out of date.'
            : status === 409
                ? 'Someone else changed this record first. Reload and try again.'
                : status >= 500
                    ? 'The server could not complete that request. Nothing was saved.'
                    : 'That request could not be completed (HTTP ' + status + ').';

        AquaTrack.toast(message, 'danger', 'Request failed');
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
