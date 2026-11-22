// Global UI behaviour. No inline scripts are used (Content-Security-Policy: script-src 'self').
class SiteUi {
    static init() {
        SiteUi.applyWidths();
        SiteUi.bindConfirm();
        SiteUi.bindAutoSubmit();
        SiteUi.bindCopy();
        SiteUi.bindThemeToggle();
        SiteUi.bindToggleGroups();
        SiteUi.rememberCollapse();
        SiteUi.bindAccordionAll();
        SiteUi.bindContainerFilter();
        SiteUi.autoRefresh();
    }

    /** Progress bars: CSP forbids inline style attributes, so widths come from data-width. */
    static applyWidths() {
        document.querySelectorAll('[data-width]').forEach(el => {
            const value = parseFloat(el.dataset.width);
            el.style.width = (isNaN(value) ? 0 : Math.min(100, Math.max(0, value))) + '%';
        });
    }

    /** Buttons / links with data-confirm ask before submitting. */
    static bindConfirm() {
        document.addEventListener('click', event => {
            const el = event.target.closest('[data-confirm]');
            if (el && !window.confirm(el.dataset.confirm)) {
                event.preventDefault();
                event.stopPropagation();
            }
        });
    }

    /** Selects with data-auto-submit submit their form on change (e.g. role per user). */
    static bindAutoSubmit() {
        document.querySelectorAll('select[data-auto-submit]').forEach(select => {
            select.addEventListener('change', () => select.form && select.form.requestSubmit());
        });
    }

    /** Buttons with data-copy-target="#id" copy the element's text to the clipboard. */
    static bindCopy() {
        document.querySelectorAll('[data-copy-target]').forEach(btn => btn.addEventListener('click', async () => {
            const source = document.querySelector(btn.dataset.copyTarget);
            if (!source || !navigator.clipboard) return;
            await navigator.clipboard.writeText(source.textContent);
            const label = btn.innerHTML;
            btn.textContent = 'Kopiert';
            setTimeout(() => { btn.innerHTML = label; }, 1500);
        }));
    }

    static bindThemeToggle() {
        document.querySelectorAll('[data-theme-toggle]').forEach(btn => btn.addEventListener('click', () => {
            const next = document.documentElement.getAttribute('data-bs-theme') === 'dark' ? 'light' : 'dark';
            document.documentElement.setAttribute('data-bs-theme', next);
            try { localStorage.setItem('ssm-theme', next); } catch (e) { /* ignore */ }
        }));
    }

    /**
     * Shows / hides form sections: inputs with data-toggle-group="x" control
     * elements with data-show-for="x" data-show-value="value" (several values: comma-separated).
     */
    static bindToggleGroups() {
        const groups = new Set([...document.querySelectorAll('[data-toggle-group]')].map(el => el.dataset.toggleGroup));
        groups.forEach(group => {
            const inputs = document.querySelectorAll(`[data-toggle-group="${group}"]`);
            const update = () => {
                let value = null;
                inputs.forEach(input => {
                    if (input.type === 'radio') { if (input.checked) value = input.value; }
                    else value = input.value;
                });
                document.querySelectorAll(`[data-show-for="${group}"]`).forEach(el => {
                    el.classList.toggle('d-none', !el.dataset.showValue.split(',').includes(value));
                });
            };
            inputs.forEach(input => input.addEventListener('change', update));
            update();
        });
    }

    /** Accordions with data-remember-collapse="key" keep their open items across reloads (session storage). */
    static rememberCollapse() {
        document.querySelectorAll('[data-remember-collapse]').forEach(container => {
            const key = container.dataset.rememberCollapse;
            const items = [...container.querySelectorAll('.accordion-collapse')];
            let stored = null;
            try { stored = JSON.parse(sessionStorage.getItem(key) || 'null'); } catch (e) { /* ignore */ }
            if (Array.isArray(stored)) {
                items.forEach(item => {
                    const open = stored.includes(item.id);
                    item.classList.toggle('show', open);
                    const button = container.querySelector(`[data-bs-target="#${item.id}"]`);
                    if (button) {
                        button.classList.toggle('collapsed', !open);
                        button.setAttribute('aria-expanded', open ? 'true' : 'false');
                    }
                });
            }
            const save = () => {
                const open = items.filter(i => i.classList.contains('show')).map(i => i.id);
                try { sessionStorage.setItem(key, JSON.stringify(open)); } catch (e) { /* ignore */ }
            };
            container.addEventListener('shown.bs.collapse', save);
            container.addEventListener('hidden.bs.collapse', save);
        });
    }

    /** Buttons with data-accordion-all="show|hide" data-target="#accordion" open / close every item. */
    static bindAccordionAll() {
        document.querySelectorAll('[data-accordion-all]').forEach(btn => btn.addEventListener('click', () => {
            const container = document.querySelector(btn.dataset.target);
            if (!container || typeof bootstrap === 'undefined') return;
            container.querySelectorAll('.accordion-collapse').forEach(item => {
                const instance = bootstrap.Collapse.getOrCreateInstance(item, { toggle: false });
                btn.dataset.accordionAll === 'show' ? instance.show() : instance.hide();
            });
        }));
    }

    /** Switch with data-container-filter hides cards with data-running="false"; the choice is remembered. */
    static bindContainerFilter() {
        document.querySelectorAll('[data-container-filter]').forEach(input => {
            const key = input.dataset.remember;
            try { input.checked = key ? localStorage.getItem(key) === 'true' : input.checked; } catch (e) { /* ignore */ }
            const apply = () => {
                document.querySelectorAll('[data-container-card]').forEach(card => {
                    card.classList.toggle('d-none', input.checked && card.dataset.running !== 'true');
                });
                document.querySelectorAll('[data-container-empty]').forEach(hint => {
                    const body = hint.parentElement;
                    const visible = body.querySelectorAll('[data-container-card]:not(.d-none)').length;
                    hint.classList.toggle('d-none', visible > 0);
                });
                try { if (key) localStorage.setItem(key, input.checked ? 'true' : 'false'); } catch (e) { /* ignore */ }
            };
            input.addEventListener('change', apply);
            apply();
        });
    }

    /** Pages with data-auto-refresh="seconds" reload themselves, unless the user is typing or a panel is open. */
    static autoRefresh() {
        const el = document.querySelector('[data-auto-refresh]');
        if (!el) return;
        const seconds = parseInt(el.dataset.autoRefresh, 10);
        if (!seconds) return;
        window.setInterval(() => {
            // open accordion items are remembered (rememberCollapse) and do not block the refresh
            const busy = document.querySelector('.collapse.show:not(.accordion-collapse), .modal.show, .offcanvas.show') ||
                (document.activeElement && ['INPUT', 'TEXTAREA', 'SELECT'].includes(document.activeElement.tagName));
            if (!busy && document.visibilityState === 'visible') window.location.reload();
        }, seconds * 1000);
    }
}

document.addEventListener('DOMContentLoaded', SiteUi.init);
