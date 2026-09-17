/* Shared presentation only: keep the current role's navigation and form actions. */
(function () {
    'use strict';
    if (window.hanaMobileLayoutReady) return;
    window.hanaMobileLayoutReady = true;

    const mobile = window.matchMedia('(max-width: 1024px)');
    const sidebar = document.getElementById('appSidebar');
    const toggle = document.getElementById('mobileMenuToggle');
    const close = document.getElementById('mobileMenuClose');
    const backdrop = document.getElementById('mobileNavBackdrop');
    const header = document.querySelector('.mobile-topbar');
    const main = document.querySelector('main');
    if (!sidebar || !toggle || !close || !backdrop || !main) return;

    const label = document.getElementById('mobilePageLabel');
    const currentLink = sidebar.querySelector('.nav-item.active');
    if (label && currentLink) label.textContent = currentLink.textContent.trim();
    let opened = false;
    let returnFocus = toggle;
    const tableRegions = new Map();
    const userTrigger = sidebar.querySelector('.user-menu-trigger');
    if (userTrigger) {
        userTrigger.setAttribute('role', 'button');
        userTrigger.setAttribute('tabindex', '0');
        userTrigger.setAttribute('aria-label', 'Mở menu tài khoản');
        userTrigger.addEventListener('keydown', event => {
            if (event.key === 'Enter' || event.key === ' ') {
                event.preventDefault();
                userTrigger.click();
            }
        });
    }

    function setOpen(value, restoreFocus = true) {
        opened = value && mobile.matches;
        document.body.classList.toggle('mobile-menu-open', opened);
        toggle.setAttribute('aria-expanded', String(opened));
        toggle.setAttribute('aria-label', opened ? 'Đóng menu điều hướng' : 'Mở menu điều hướng');
        backdrop.hidden = !opened;
        sidebar.inert = mobile.matches && !opened;
        main.inert = opened;
        header.inert = opened;
        if (opened) {
            returnFocus = document.activeElement;
            sidebar.setAttribute('role', 'dialog');
            sidebar.setAttribute('aria-modal', 'true');
            close.focus();
        } else {
            sidebar.removeAttribute('role');
            sidebar.removeAttribute('aria-modal');
            if (restoreFocus && mobile.matches && returnFocus) returnFocus.focus();
        }
    }

    toggle.addEventListener('click', () => setOpen(!opened));
    close.addEventListener('click', () => setOpen(false));
    backdrop.addEventListener('click', () => setOpen(false));
    sidebar.addEventListener('click', event => {
        // Password dialog is already portalled to body by _UserMenuPartial.
        if (mobile.matches && event.target.closest('a.nav-item, .user-menu-item')) setOpen(false, false);
    });
    document.addEventListener('keydown', event => {
        if (!opened) return;
        if (event.key === 'Escape') {
            event.preventDefault();
            setOpen(false);
        }
        if (event.key !== 'Tab') return;
        const focusable = Array.from(sidebar.querySelectorAll('a[href], button:not(:disabled), [tabindex="0"]'))
            .filter(element => element.getClientRects().length && getComputedStyle(element).visibility !== 'hidden');
        const first = focusable[0];
        const last = focusable[focusable.length - 1];
        if (event.shiftKey && document.activeElement === first) {
            event.preventDefault();
            last.focus();
        } else if (!event.shiftKey && document.activeElement === last) {
            event.preventDefault();
            first.focus();
        }
    });

    function fitTables() {
        if (!mobile.matches) {
            tableRegions.forEach((attributes, region) => {
                attributes.forEach(([name, value]) => value === null ? region.removeAttribute(name) : region.setAttribute(name, value));
                region.classList.remove('mobile-table-scroll');
            });
            tableRegions.clear();
            document.querySelectorAll('[data-mobile-table-wrapper]').forEach(wrapper => wrapper.replaceWith(...wrapper.childNodes));
            return;
        }
        document.querySelectorAll('main table, .modal-card table').forEach(table => {
            let scroller = table.parentElement;
            // Reuse existing scroll regions instead of nesting horizontal scrollbars.
            while (scroller && scroller !== main && !/auto|scroll/.test(getComputedStyle(scroller).overflowX)) scroller = scroller.parentElement;
            if (!scroller || scroller === main) {
                scroller = document.createElement('div');
                scroller.dataset.mobileTableWrapper = '';
                table.before(scroller);
                scroller.appendChild(table);
            }
            if (!tableRegions.has(scroller)) {
                tableRegions.set(scroller, ['tabindex', 'role', 'aria-label'].map(name => [name, scroller.getAttribute(name)]));
                scroller.classList.add('mobile-table-scroll');
                if (!scroller.hasAttribute('tabindex')) scroller.setAttribute('tabindex', '0');
                if (!scroller.hasAttribute('role')) scroller.setAttribute('role', 'region');
                if (!scroller.hasAttribute('aria-label')) scroller.setAttribute('aria-label', 'Bảng dữ liệu, vuốt ngang để xem các cột');
            }
            const columnCount = table.tHead?.rows[0]?.cells.length || table.rows[0]?.cells.length || 0;
            table.classList.toggle('mobile-wide-table', columnCount > 4);
        });
    }

    mobile.addEventListener('change', () => {
        setOpen(false, false);
        fitTables();
    });
    setOpen(false, false);
    fitTables();
    // Tables inserted by existing detail dialogs get the same bounded scroll area.
    const observer = new MutationObserver(records => {
        if (records.some(record => Array.from(record.addedNodes).some(node => node.nodeType === 1 &&
            (node.matches('table') || node.querySelector('table'))))) fitTables();
    });
    observer.observe(document.body, { childList: true, subtree: true });
})();
