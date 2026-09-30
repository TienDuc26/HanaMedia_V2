// Regression: open Campaign edit from the actual rendered button, then optionally
// round-trip and restore a DEMO-only note. Never changes a real campaign.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const { chromium } = require('playwright');
const demo = JSON.parse(fs.readFileSync('tests/artifacts/demo-v4-access.json', 'utf8'));
const saveDemo = process.argv.includes('--save-demo');
(async () => {
  const browser = await chromium.launch({ headless: true, channel: 'msedge' });
  const errors = [];
  const context = await browser.newContext({ baseURL: 'http://127.0.0.1:5028', viewport: { width: 1440, height: 1000 } });
  const page = await context.newPage();
  page.on('pageerror', e => errors.push(e.message));
  const modal = page.locator('#campaignModal');
  function row(id) { return page.locator('tr').filter({ has: page.locator(`a[href="/Campaigns/Details/${id}"]`) }); }
  async function edit(id) {
    await row(id).locator('button.btn-action-edit').click();
    assert(await modal.evaluate(m => m.classList.contains('active')), 'Edit click must open modal. Browser errors: ' + errors.join('; '));
    await page.waitForFunction(() => getComputedStyle(document.getElementById('campaignModal')).opacity === '1');
    assert.equal(await page.locator('#campaignForm').getAttribute('action'), '/Campaigns/Edit/' + id);
  }
  async function close() {
    await page.locator('#campaignModal .modal-close').click();
    await page.waitForTimeout(220);
  }
  async function save() {
    await Promise.all([page.waitForURL(u => u.pathname === '/Campaigns', { waitUntil: 'load' }), page.locator('#campaignForm button[type=submit]').click()]);
    await page.waitForLoadState('load');
    assert((await page.locator('body').innerText()).includes('Cập nhật chiến dịch thành công.'), 'Save must succeed');
  }
  try {
    await page.goto('/Login');
    await page.locator('[name=username]').fill('demo_v4_qlbooking');
    await page.locator('[name=password]').fill(demo.Password);
    await Promise.all([page.waitForURL(u => !u.pathname.includes('/Login')), page.locator('button[type=submit]').click()]);
    await page.goto('/Campaigns');
    for (const campaign of demo.Campaigns) {
      await edit(campaign.Id);
      assert.equal(await page.locator('#campaignName').inputValue(), campaign.Name);
      assert(await page.locator('#managerId').inputValue());
      assert.match(await page.locator('#startDate').inputValue(), /^\d{4}-\d{2}-\d{2}$/);
      assert(await page.locator('#campaignForm').evaluate(f => f.checkValidity()));
      await close();
    }
    console.log('PASS: all 3 DEMO campaign edit buttons open with populated valid fields');
    const id = demo.Campaigns.find(c => c.Name.includes('Chờ Giám đốc chốt')).Id;
    await edit(id);
    const original = await page.locator('#campaignNotes').inputValue();
    if (saveDemo) {
      const note = '[DEMO V4] Kiểm tra dấu "nháy kép", \'nháy đơn\', <tag> & tiếng Việt.\nDòng thứ hai.';
      let submitted = false;
      try {
        await page.locator('#campaignNotes').fill(note); submitted = true; await save();
        await edit(id); assert.equal(await page.locator('#campaignNotes').inputValue(), note);
        console.log('PASS: edited note persists and special characters round-trip safely');
      } finally {
        if (submitted) {
          await page.goto('/Campaigns'); await edit(id);
          await page.locator('#campaignNotes').fill(original); await save();
          await edit(id); assert.equal(await page.locator('#campaignNotes').inputValue(), original);
          console.log('PASS: original DEMO note restored');
        }
      }
    }
    await close();
    await page.setViewportSize({ width: 390, height: 844 });
    await edit(id);
    assert(await page.locator('#campaignName').isVisible());
    assert(await page.evaluate(() => document.documentElement.scrollWidth) <= 391);
    fs.mkdirSync('tests/artifacts/campaign-edit', { recursive: true });
    await page.screenshot({ path: 'tests/artifacts/campaign-edit/mobile.png', fullPage: true });
    await close();
    await page.setViewportSize({ width: 1440, height: 1000 });
    await page.getByRole('button', { name: 'Tạo chiến dịch mới' }).click();
    assert.equal(await page.locator('#campaignForm').getAttribute('action'), '/Campaigns/Create');
    assert.equal(await page.locator('#campaignName').inputValue(), '');
    assert.equal(await page.locator('#campaignNotes').inputValue(), '');
    await close();
    assert.deepEqual(errors, []);
    console.log('PASS: mobile edit + create reset + no browser JavaScript errors');
  } finally { await browser.close(); }
})().catch(e => { console.error(e); process.exitCode = 1; });
