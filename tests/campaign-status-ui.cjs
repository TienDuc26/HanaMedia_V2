// Read-only regression for the director-controlled Campaign lifecycle.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const { chromium } = require('playwright');
const demo = JSON.parse(fs.readFileSync('tests/artifacts/demo-v4-access.json', 'utf8'));
(async () => {
  const browser = await chromium.launch({ headless: true, channel: 'msedge' });
  const errors = [];
  try {
    for (const role of ['qlbooking', 'giamdoc']) {
      const context = await browser.newContext({ baseURL: 'http://127.0.0.1:5028', viewport: { width: 1440, height: 1000 } });
      const page = await context.newPage();
      page.on('pageerror', e => errors.push(e.message));
      await page.goto('/Login');
      await page.locator('[name=username]').fill('demo_v4_' + role);
      await page.locator('[name=password]').fill(demo.Password);
      await Promise.all([page.waitForURL(u => !u.pathname.includes('/Login')), page.locator('button[type=submit]').click()]);
      await page.goto('/Campaigns');
      assert.deepEqual(await page.locator('.filter-group select[name=status] option').evaluateAll(opts => opts.map(o => o.value)), ['', 'planning', 'running', 'completed', 'accepted']);
      if (role === 'qlbooking') {
        for (const [index, expected] of [[0, 'Đang chạy'], [1, 'Đang chờ ký']]) {
          const id = demo.Campaigns[index].Id;
          await page.locator('tr').filter({ has: page.locator('a[href="/Campaigns/Details/' + id + '"]') }).locator('button.btn-action-edit').click();
          await page.waitForFunction(() => getComputedStyle(document.getElementById('campaignModal')).opacity === '1');
          assert(await page.locator('#campaignStatus').evaluate(e => e.readOnly));
          assert.equal(await page.locator('#campaignStatus').inputValue(), expected);
          assert.equal(await page.locator('#campaignForm [name=Status]').count(), 0);
          await page.locator('#campaignModal .modal-close').click();
          await page.waitForTimeout(220);
        }
        await page.getByRole('button', { name: 'Tạo chiến dịch mới' }).click();
        assert.equal(await page.locator('#campaignStatus').inputValue(), 'Đang chờ ký');
        await page.setViewportSize({ width: 390, height: 844 });
        assert(await page.evaluate(() => document.documentElement.scrollWidth) <= 391);
        await page.waitForFunction(() => getComputedStyle(document.getElementById('campaignModal')).opacity === '1');
        fs.mkdirSync('tests/artifacts/campaign-status', { recursive: true });
        await page.screenshot({ path: 'tests/artifacts/campaign-status/automatic-status-mobile.png', fullPage: true });
        await page.goto('/Bookings');
        assert.equal(await page.locator('#bookingForm select[name=CampaignId] option[value="' + demo.Campaigns[0].Id + '"]').count(), 1);
        for (const c of demo.Campaigns.slice(1)) assert.equal(await page.locator('#bookingForm select[name=CampaignId] option[value="' + c.Id + '"]').count(), 0);
        console.log('PASS: manager status read-only, create waiting, only confirmed running campaigns eligible for Booking, mobile');
      } else {
        assert.equal(await page.locator('form[action="/Campaigns/Confirm/' + demo.Campaigns[0].Id + '"]').count(), 0);
        for (const c of demo.Campaigns.slice(1)) assert(await page.locator('form[action="/Campaigns/Confirm/' + c.Id + '"]').isVisible());
        console.log('PASS: director can confirm waiting campaigns, no repeated confirm button on running campaign');
      }
      await page.goto('/Campaigns?status=planning');
      assert.equal(await page.locator('a[href="/Campaigns/Details/' + demo.Campaigns[0].Id + '"]').count(), 0);
      for (const c of demo.Campaigns.slice(1)) assert(await page.locator('a[href="/Campaigns/Details/' + c.Id + '"]').count());
      await page.goto('/Campaigns/Details/' + demo.Campaigns[1].Id);
      assert((await page.locator('body').innerText()).includes('Đang chờ ký'));
      console.log('PASS: waiting filter and campaign details match database for ' + role);
      await context.close();
    }
    assert.deepEqual(errors, []);
    console.log('PASS: no browser JavaScript errors; no business data changed');
  } finally { await browser.close(); }
})().catch(e => { console.error(e); process.exitCode = 1; });
