/**
 * hemaui.js - tiny shared UI helpers used across HemaFlow pages.
 *   HemaUI.toast({ message, type, duration })   small corner notification
 *   HemaUI.alert({ title, message, okText, variant }) -> Promise (resolves when closed)
 * No dependencies (no jQuery, no Bootstrap JS). type/variant: success | warning | danger | info
 */
(function () {
    'use strict';
    if (window.HemaUI) return;

    var COLORS = { success: '#198754', warning: '#e0a800', danger: '#dc3545', error: '#dc3545', info: '#0d6efd' };
    var ICONS = {
        success: 'bi-check-circle-fill', warning: 'bi-exclamation-triangle-fill',
        danger: 'bi-x-circle-fill', error: 'bi-x-circle-fill', info: 'bi-info-circle-fill'
    };

    function colorFor(t) { return COLORS[t] || COLORS.info; }
    function iconFor(t) { return ICONS[t] || ICONS.info; }

    function injectCss() {
        if (document.getElementById('hemaui-css')) return;
        var css = [
            '#hemaui-toasts{position:fixed;top:76px;right:20px;z-index:2000;display:flex;flex-direction:column;gap:8px;width:360px;max-width:calc(100% - 40px);pointer-events:none;margin:0;padding:0;list-style:none}',
            '.hemaui-toast{pointer-events:auto;position:relative;overflow:hidden;display:flex;justify-content:space-between;align-items:center;gap:12px;padding:12px 14px;border-radius:6px;box-shadow:rgba(111,111,111,.25) 0 8px 24px;opacity:0;transform:translateX(24px);transition:opacity .25s ease,transform .25s ease,background-color .25s;font-family:inherit;color:var(--c)}',
            '.hemaui-toast.show{opacity:1;transform:none}',
            '.hemaui-toast:hover{transform:scale(1.01)}',
            '.hemaui-toast.success{--c:#047857;background-color:#7dffbc;--g:rgba(16,185,129,.25);background-image:linear-gradient(0deg,transparent 23%,var(--g) 24%,var(--g) 25%,transparent 26%,transparent 73%,var(--g) 74%,var(--g) 75%,transparent 76%),linear-gradient(90deg,transparent 23%,var(--g) 24%,var(--g) 25%,transparent 26%,transparent 73%,var(--g) 74%,var(--g) 75%,transparent 76%);background-size:55px 55px}',
            '.hemaui-toast.info{--c:#1e3a8a;background-color:#7eb8ff;--g:rgba(59,131,246,.25);background-image:linear-gradient(0deg,transparent 23%,var(--g) 24%,var(--g) 25%,transparent 26%,transparent 73%,var(--g) 74%,var(--g) 75%,transparent 76%),linear-gradient(90deg,transparent 23%,var(--g) 24%,var(--g) 25%,transparent 26%,transparent 73%,var(--g) 74%,var(--g) 75%,transparent 76%);background-size:55px 55px}',
            '.hemaui-toast.warning{--c:#78350f;background-color:#ffe57e;--g:rgba(245,159,11,.25);background-image:linear-gradient(0deg,transparent 23%,var(--g) 24%,var(--g) 25%,transparent 26%,transparent 73%,var(--g) 74%,var(--g) 75%,transparent 76%),linear-gradient(90deg,transparent 23%,var(--g) 24%,var(--g) 25%,transparent 26%,transparent 73%,var(--g) 74%,var(--g) 75%,transparent 76%);background-size:55px 55px}',
            '.hemaui-toast.danger{--c:#7f1d1d;background-color:#ff7e7e;--g:rgba(239,68,68,.25);background-image:linear-gradient(0deg,transparent 23%,var(--g) 24%,var(--g) 25%,transparent 26%,transparent 73%,var(--g) 74%,var(--g) 75%,transparent 76%),linear-gradient(90deg,transparent 23%,var(--g) 24%,var(--g) 25%,transparent 26%,transparent 73%,var(--g) 74%,var(--g) 75%,transparent 76%);background-size:55px 55px}',
            '.hemaui-toast .hu-body{display:flex;align-items:center;gap:10px;min-width:0}',
            '.hemaui-toast .hu-text{font-size:.875rem;line-height:1.35;font-weight:500;overflow-wrap:anywhere}',
            '.hemaui-toast svg{width:18px;height:18px;flex:none;color:var(--c)}',
            '.hemaui-toast .hu-close{flex:none;display:flex;align-items:center;padding:2px;border:0;background:none;border-radius:5px;cursor:pointer;color:var(--c);transition:background-color .25s}',
            '.hemaui-toast .hu-close:hover{background-color:rgba(255,255,255,.45)}',
            '.hemaui-toast .hu-bar{position:absolute;left:0;bottom:0;height:3px;width:100%;background:var(--c);transform-origin:left;animation:hu-bar var(--d,5s) linear forwards}',
            '.hemaui-toast:hover .hu-bar{animation-play-state:paused}',
            '@keyframes hu-bar{from{transform:scaleX(1)}to{transform:scaleX(0)}}',
            '[data-toast]{display:none!important}',
            '.hemaui-overlay{position:fixed;inset:0;background:rgba(0,0,0,.45);z-index:2100;display:flex;align-items:center;justify-content:center;padding:20px;opacity:0;transition:opacity .15s}',
            '.hemaui-overlay.show{opacity:1}',
            '.hemaui-dialog{background:#fff;border:1px solid #e1e1e1;border-top:4px solid var(--hu,#0d6efd);max-width:420px;width:100%;padding:26px 24px 20px;text-align:center;box-shadow:0 12px 40px rgba(0,0,0,.25)}',
            '.hemaui-dialog .hemaui-ico{font-size:2rem;color:var(--hu,#0d6efd);margin-bottom:6px}',
            '.hemaui-dialog h5{font-weight:700;margin:0 0 8px;font-size:1.1rem}',
            '.hemaui-dialog p{margin:0 0 18px;color:#555;font-size:.92rem}',
            '.hemaui-dialog button{background:#212529;color:#fff;border:0;padding:9px 26px;font-weight:600;font-size:.9rem;cursor:pointer}',
            '.hemaui-dialog button:focus{outline:2px solid #0d6efd;outline-offset:2px}'
        ].join('\n');
        var s = document.createElement('style');
        s.id = 'hemaui-css';
        s.textContent = css;
        document.head.appendChild(s);
    }

    function container() {
        injectCss();
        var c = document.getElementById('hemaui-toasts');
        if (!c) {
            c = document.createElement('div');
            c.id = 'hemaui-toasts';
            c.setAttribute('aria-live', 'polite');
            document.body.appendChild(c);
        }
        return c;
    }

    var SVG = {
        success: 'M8.5 11.5 11 14l4-4m6 2a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z',
        info: 'M10 11h2v5m-2 0h4m-2.592-8.5h.01M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z',
        warning: 'M12 13V8m0 8h.01M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z',
        danger: 'm15 9-6 6m0-6 6 6m6-3a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z'
    };
    function svg(d) {
        return '<svg aria-hidden="true" fill="none" viewBox="0 0 24 24"><path stroke="currentColor" stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="' + d + '"></path></svg>';
    }

    // Top-right notification that clears itself after 5 seconds (progress bar shows the time left;
    // hovering pauses it). type: success | info | warning | danger (error = danger).
    function toast(opts) {
        opts = opts || {};
        var type = opts.type === 'error' ? 'danger' : (SVG[opts.type] ? opts.type : 'info');
        var duration = opts.duration === undefined ? 5000 : opts.duration;

        var t = document.createElement('div');
        t.className = 'hemaui-toast ' + type;
        t.setAttribute('role', type === 'danger' ? 'alert' : 'status');
        t.innerHTML = '<div class="hu-body">' + svg(SVG[type]) + '<div class="hu-text"></div></div>' +
            '<button type="button" class="hu-close" aria-label="Dismiss">' + svg('M6 18 17.94 6M18 18 6.06 6') + '</button>';
        t.querySelector('.hu-text').textContent = opts.message || '';
        var bar = null;
        if (duration > 0) {
            bar = document.createElement('div');
            bar.className = 'hu-bar';
            bar.style.setProperty('--d', duration + 'ms');
            t.appendChild(bar);
        }
        container().appendChild(t);
        requestAnimationFrame(function () { t.classList.add('show'); });

        var gone = false;
        function remove() {
            if (gone) return; gone = true;
            t.classList.remove('show');
            setTimeout(function () { if (t.parentNode) t.parentNode.removeChild(t); }, 260);
        }
        t.querySelector('.hu-close').addEventListener('click', remove);
        if (bar) bar.addEventListener('animationend', remove);
        return { close: remove };
    }

    // Any server message rendered as <div data-toast="success|danger|info|warning">text</div> becomes a toast.
    function convertServerMessages() {
        var nodes = document.querySelectorAll('[data-toast]');
        for (var i = 0; i < nodes.length; i++) {
            var n = nodes[i], msg = (n.textContent || '').replace(/\s+/g, ' ').trim();
            var type = n.getAttribute('data-toast');
            if (n.parentNode) n.parentNode.removeChild(n);
            if (msg) toast({ message: msg, type: type });
        }
    }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', convertServerMessages);
    else convertServerMessages();

    function alertDialog(opts) {
        opts = opts || {};
        injectCss();
        var variant = opts.variant || opts.type || 'info';

        return new Promise(function (resolve) {
            var overlay = document.createElement('div');
            overlay.className = 'hemaui-overlay';
            var box = document.createElement('div');
            box.className = 'hemaui-dialog';
            box.setAttribute('role', 'alertdialog');
            box.setAttribute('aria-modal', 'true');
            box.style.setProperty('--hu', colorFor(variant));

            var ico = document.createElement('div');
            ico.className = 'hemaui-ico';
            var i = document.createElement('i');
            i.className = 'bi ' + iconFor(variant);
            ico.appendChild(i);

            var h = document.createElement('h5');
            h.textContent = opts.title || '';
            var p = document.createElement('p');
            p.textContent = opts.message || '';
            var ok = document.createElement('button');
            ok.type = 'button';
            ok.textContent = opts.okText || 'OK';

            box.appendChild(ico);
            if (opts.title) box.appendChild(h);
            box.appendChild(p);
            box.appendChild(ok);
            overlay.appendChild(box);
            document.body.appendChild(overlay);
            requestAnimationFrame(function () { overlay.classList.add('show'); });
            ok.focus();

            var done = false;
            function close() {
                if (done) return;
                done = true;
                document.removeEventListener('keydown', onKey);
                overlay.classList.remove('show');
                setTimeout(function () { if (overlay.parentNode) overlay.parentNode.removeChild(overlay); }, 160);
                resolve();
            }
            function onKey(e) { if (e.key === 'Escape' || e.key === 'Enter') close(); }
            ok.addEventListener('click', close);
            document.addEventListener('keydown', onKey);
        });
    }


    // ---- Confirm dialog for [data-confirm] links/buttons (Admin, Patient, Doctor) ----
    // Lab Technician pages ship their own handler (#confirmModal), so skip there.
    function confirmDialog(opts) {
        return new Promise(function (resolve) {
            var o = document.createElement('div');
            o.style.cssText = 'position:fixed;inset:0;background:rgba(15,23,42,.45);z-index:2100;display:flex;align-items:center;justify-content:center;padding:16px';
            var box = document.createElement('div');
            box.style.cssText = 'background:#fff;width:100%;max-width:420px;padding:1.5rem;font-family:inherit;border-radius:0;box-shadow:0 12px 40px rgba(15,23,42,.25)';
            var h = document.createElement('div');
            h.style.cssText = 'font-weight:700;font-size:1rem;color:#0f172a;margin-bottom:.5rem';
            h.textContent = opts.title || 'Are you sure?';
            var m = document.createElement('div');
            m.style.cssText = 'font-size:.85rem;color:#475569;margin-bottom:1.25rem';
            m.textContent = opts.message || '';
            var row = document.createElement('div');
            row.style.cssText = 'display:flex;justify-content:flex-end;gap:.5rem';
            function btn(label, bg, fg, border) {
                var b = document.createElement('button');
                b.type = 'button'; b.textContent = label;
                b.style.cssText = 'border-radius:0;font-size:.8rem;font-weight:600;padding:.55rem 1.25rem;cursor:pointer;background:' + bg + ';color:' + fg + ';border:1px solid ' + border;
                return b;
            }
            var cancel = btn('Cancel', '#fff', '#334155', '#e2e8f0');
            var ok = btn(opts.okText || 'Confirm', opts.danger ? '#dc3545' : '#0f172a', '#fff', opts.danger ? '#dc3545' : '#0f172a');
            row.appendChild(cancel); row.appendChild(ok);
            box.appendChild(h); box.appendChild(m); box.appendChild(row); o.appendChild(box);
            function close(result) {
                document.removeEventListener('keydown', onKey);
                if (o.parentNode) o.parentNode.removeChild(o);
                resolve(result);
            }
            function onKey(e) { if (e.key === 'Escape') close(false); }
            cancel.addEventListener('click', function () { close(false); });
            ok.addEventListener('click', function () { close(true); });
            o.addEventListener('click', function (e) { if (e.target === o) close(false); });
            document.addEventListener('keydown', onKey);
            document.body.appendChild(o);
            ok.focus();
        });
    }

    document.addEventListener('click', function (e) {
        var el = e.target && e.target.closest ? e.target.closest('[data-confirm]') : null;
        if (!el || el.tagName === 'FORM' || document.getElementById('confirmModal')) return;
        if (el.dataset.hemaConfirmed) { delete el.dataset.hemaConfirmed; return; }
        e.preventDefault();
        e.stopPropagation();
        confirmDialog({
            title: el.getAttribute('data-confirm-title'),
            message: el.getAttribute('data-confirm'),
            okText: el.getAttribute('data-confirm-text'),
            danger: el.getAttribute('data-confirm-danger') === 'true'
        }).then(function (yes) {
            if (!yes) return;
            el.dataset.hemaConfirmed = '1';
            el.click();
        });
    }, true);

    document.addEventListener('submit', function (e) {
        var f = e.target;
        if (!f || !f.matches || !f.matches('form[data-confirm]') || document.getElementById('confirmModal')) return;
        if (f.dataset.hemaConfirmed) { delete f.dataset.hemaConfirmed; return; }
        e.preventDefault();
        var submitter = e.submitter || null;
        confirmDialog({
            title: f.getAttribute('data-confirm-title'),
            message: f.getAttribute('data-confirm'),
            okText: f.getAttribute('data-confirm-text'),
            danger: f.getAttribute('data-confirm-danger') === 'true'
        }).then(function (yes) {
            if (!yes) return;
            f.dataset.hemaConfirmed = '1';
            if (f.requestSubmit) { submitter ? f.requestSubmit(submitter) : f.requestSubmit(); } else { f.submit(); }
        });
    }, true);

    // ---- Live search: [data-live-search] on a form field submits its (GET) form as you type ----
    // Text boxes wait 400ms after the last key; selects and dates submit straight away.
    // After the page reloads, focus goes back to the box with the cursor at the end.
    var liveTimer = null;
    document.addEventListener('input', function (e) {
        var el = e.target;
        if (!el || !el.matches || !el.matches('input[data-live-search]:not([type=date])') || !el.form) return;
        clearTimeout(liveTimer);
        liveTimer = setTimeout(function () {
            try { sessionStorage.setItem('hemaLiveFocus', el.name); } catch (x) { }
            if (el.form.requestSubmit) el.form.requestSubmit(); else el.form.submit();
        }, 400);
    });
    document.addEventListener('change', function (e) {
        var el = e.target;
        if (!el || !el.matches || !el.matches('select[data-live-search], input[type=date][data-live-search]') || !el.form) return;
        if (el.form.requestSubmit) el.form.requestSubmit(); else el.form.submit();
    });
    document.addEventListener('DOMContentLoaded', function () {
        var name = null;
        try { name = sessionStorage.getItem('hemaLiveFocus'); sessionStorage.removeItem('hemaLiveFocus'); } catch (x) { }
        if (!name) return;
        var box = document.querySelector('input[data-live-search][name="' + name + '"]');
        if (box) { box.focus(); var n = box.value.length; try { box.setSelectionRange(n, n); } catch (x) { } }
    });

    if (document.head) injectCss();
    window.HemaUI = { toast: toast, alert: alertDialog };
})();
