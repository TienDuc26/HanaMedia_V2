// Read-only browser checks against persistent demo fixtures. Never submits business forms.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const { chromium } = require('playwright');
const demo = JSON.parse(fs.readFileSync('tests/artifacts/demo-v4-access.json', 'utf8'));
assert(demo.Complete, 'Finish explicit DemoV4 provisioning first.');
const ids = Object.values(demo.Bookings);
const output = 'tests/artifacts/demo-v4-ui';
fs.mkdirSync(output, { recursive: true });
(async () => {
  const browser = await chromium.launch({ headless: true, channel: 'msedge' });
  const errors = [], checks = [];
  const check = (ok, label) => { assert(ok, label); checks.push(label); console.log('PASS: ' + label); };
  try {
    for (const role of ['qlbooking', 'giamdoc', 'phaply', 'ketoan', 'qlytuong']) {
      const context = await browser.newContext({ baseURL: 'http://127.0.0.1:5028', viewport: { width: 1440, height: 1000 } });
      const page = await context.newPage();
      page.on('pageerror', e => errors.push(role + ': ' + e.message));
      await page.goto('/Login');
      await page.locator('[name=username]').fill('demo_v4_' + role);
      await page.locator('[name=password]').fill(demo.Password);
      await Promise.all([page.waitForURL(u => !u.pathname.includes('/Login')), page.locator('button[type=submit]').click()]);
      async function open(path) {
        const response = await page.goto(path);
        check(response.status() === 200 && !page.url().includes('/AccessDenied'), `${role}: ${path} renders`);
      }
      async function form(action, present = true) {
        const locator = page.locator(`form[action="${action}"]`);
        check(present ? await locator.first().isVisible() : await locator.count() === 0, `${role}: ${present ? 'shows' : 'hides'} ${action}`);
      }
      let path;
      if (role === 'qlbooking') {
        await open(`/Bookings/Details/${ids[0]}`);
        await form(`/Bookings/SubmitApproval/${ids[0]}`);
        await form(`/Bookings/Approve/${ids[0]}`, false);
        await open(`/Bookings/Details/${ids[5]}`);
        await form(`/Bookings/UploadContract/${ids[5]}`);
        check((await page.locator('body').innerText()).includes('Bổ sung điều khoản nghiệm thu'), 'Manager sees Legal rejection feedback');
        await open(`/Bookings/Details/${ids[8]}`);
        await form(`/Bookings/UploadContract/${ids[8]}`, false);
        await form(`/Bookings/Acceptance/${ids[8]}`, false);
        path = '/Bookings';
      } else if (role === 'giamdoc') {
        await open(`/Bookings/Details/${ids[1]}`);
        await form(`/Bookings/Approve/${ids[1]}`);
        await form(`/Bookings/Reject/${ids[1]}`);
        await open(`/Bookings/Details/${ids[4]}`);
        await form(`/Bookings/SignContract/${ids[4]}`, false);
        await open(`/Bookings/Details/${ids[6]}`);
        await form(`/Bookings/SignContract/${ids[6]}`);
        check(await page.locator('[name=confirmed]').getAttribute('required') !== null, 'Signing requires explicit checkbox');
        path = '/Director/SignContract';
      } else if (role === 'phaply') {
        path = '/Legal'; await open(path);
        await form(`/Legal/Review/${ids[4]}`);
        await form(`/Legal/Review/${ids[6]}`, false);
      } else if (role === 'ketoan') {
        path = '/Accounting'; await open(path);
        await form(`/Accounting/Payment/${ids[8]}`);
        await form(`/Accounting/Payment/${ids[1]}`, false);
      } else {
        path = '/ManageIdea/Idea'; await open(path);
        const body = await page.locator('body').innerText();
        check(body.includes('Creator nội bộ') && body.includes('Creator đối tác'), 'Both content creator scenarios visible');
        check(!await page.locator('select[name=CampaignId] option').filter({ hasText: '[DEMO V4] Chờ Giám đốc chốt' }).count(), 'Unconfirmed campaign absent from idea form');
      }
      for (const width of [1440, 390]) {
        await page.setViewportSize({ width, height: 1000 }); await open(path);
        check(await page.locator('#appSidebar').count() === 1, `${role} ${width}: sidebar preserved`);
        check(await page.evaluate(() => document.documentElement.scrollWidth) <= width + 1, `${role} ${width}: no page overflow`);
        if (width === 390) {
          await page.locator('#mobileMenuToggle').click();
          check(await page.locator('#mobileMenuToggle').getAttribute('aria-expanded') === 'true', `${role}: mobile menu opens`);
          await page.keyboard.press('Escape');
        }
        await page.screenshot({ path: `${output}/${role}-${width}.png`, fullPage: true });
      }
      await context.close();
    }
    check(errors.length === 0, 'No browser JavaScript errors');
    fs.writeFileSync(`${output}/result.json`, JSON.stringify({ count: checks.length, checks, errors }, null, 2));
    console.log(`PASS: ${checks.length} browser checks, no business mutations.`);
  } finally { await browser.close(); }
})().catch(e => { console.error(e); process.exitCode = 1; });
