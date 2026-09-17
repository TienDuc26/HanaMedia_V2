// Read-only browser smoke test: no login attempts, form submissions or DB writes.
// Requires Playwright and an existing authenticated storage state OUTSIDE the repo.
// Set MOBILE_BASE_URL, MOBILE_STORAGE_STATE and optionally MOBILE_BROWSER_EXECUTABLE.
// Run: node tests/mobile-layout.cjs
const assert = require('node:assert/strict');
const { chromium } = require('playwright');

(async () => {
    assert(process.env.MOBILE_BASE_URL, 'Set MOBILE_BASE_URL to the running test app.');
    assert(process.env.MOBILE_STORAGE_STATE, 'Set MOBILE_STORAGE_STATE to a short-lived authenticated Playwright state file.');
    const baseURL = new URL(process.env.MOBILE_BASE_URL).origin;
    const browser = await chromium.launch({ headless: true, executablePath: process.env.MOBILE_BROWSER_EXECUTABLE || undefined });
    try {
        const context = await browser.newContext({ baseURL, storageState: process.env.MOBILE_STORAGE_STATE, viewport: { width: 390, height: 844 } });
        const page = await context.newPage();
        const errors = [];
        page.on('pageerror', error => errors.push(`${new URL(page.url()).pathname}: ${error.message}`));
        await page.goto('/');
        assert(await page.locator('#appSidebar').count(), 'An authenticated business page is required.');
        const paths = new Set(await page.locator('#appSidebar .nav-item[href]').evaluateAll(links => links.map(link => link.getAttribute('href'))));
        for (const path of Array.from(paths)) {
            await page.goto(path);
            const details = await page.locator('main a[href*="/Details/"], main a[href*="/Workspace/"]').evaluateAll(links => links.map(link => link.getAttribute('href')));
            for (const detail of details) paths.add(detail);
        }
        let checks = 0;
        for (const viewport of [
            { width: 360, height: 800 }, { width: 390, height: 844 },
            { width: 768, height: 1024 }, { width: 844, height: 390 },
            { width: 1024, height: 768 }, { width: 1440, height: 900 }
        ]) {
            await page.setViewportSize(viewport);
            for (const path of paths) {
                const response = await page.goto(path);
                assert.equal(response.status(), 200, path);
                assert(!/\/Login|\/AccessDenied/i.test(page.url()), `Unexpected redirect: ${path}`);
                assert(await page.locator('#appSidebar').count(), `Missing role sidebar: ${path}`);
                const width = await page.evaluate(() => document.documentElement.scrollWidth);
                assert(width <= viewport.width + 1, `${path}: page overflow ${width}px at ${viewport.width}px`);
                if (viewport.width <= 1024) {
                    const toggle = page.locator('#mobileMenuToggle');
                    assert(await toggle.isVisible(), `Missing mobile menu: ${path}`);
                    await toggle.click();
                    assert.equal(await toggle.getAttribute('aria-expanded'), 'true');
                    assert(await page.locator('main').evaluate(main => main.inert));
                    await page.keyboard.press('Escape');
                    assert.equal(await toggle.getAttribute('aria-expanded'), 'false');
                    assert.equal(await page.evaluate(() => document.activeElement.id), 'mobileMenuToggle');
                    assert(!await page.locator('main').evaluate(main => main.inert));
                } else {
                    assert(!await page.locator('#mobileMenuToggle').isVisible());
                    assert(await page.locator('#appSidebar').isVisible());
                    assert(!await page.locator('#appSidebar').evaluate(sidebar => sidebar.inert));
                    assert.equal(await page.locator('[data-mobile-table-wrapper]').count(), 0);
                }
                checks++;
            }
        }
        // The same page must recover when rotating/resizing with the drawer open.
        await page.setViewportSize({ width: 390, height: 844 });
        await page.locator('#mobileMenuToggle').click();
        await page.setViewportSize({ width: 1440, height: 900 });
        await page.waitForFunction(() => !document.body.classList.contains('mobile-menu-open'));
        assert(!await page.locator('main').evaluate(main => main.inert));
        assert(!await page.locator('#appSidebar').evaluate(sidebar => sidebar.inert));
        assert.deepEqual(errors, [], 'Browser JavaScript errors');
        console.log(`PASS: ${checks} page/viewport checks; mobile drawer, keyboard, desktop recovery; no JS errors.`);
    } finally {
        await browser.close();
    }
})().catch(error => { console.error(error.message); process.exitCode = 1; });
