// Only invoked by V4Flow --ui while its isolated SQL clone is running.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const { chromium } = require('playwright');

(async () => {
  assert(process.env.V4_PASSWORD, 'Run through V4Flow --ui.');
  const browser = await chromium.launch({ headless: true, channel: 'msedge' });
  const errors = [];
  let checks = 0;
  fs.mkdirSync('tests/artifacts/v4', { recursive: true });
  try {
    const routes = {
      manager: ['/Bookings', `/Bookings/Details/${process.env.V4_DRAFT_ID}`, '/Campaigns'],
      director: ['/Director/Dashboard', '/Director/BookingCampaign', '/Director/SignContract', '/Director/Config', '/Campaigns', '/Accounting'],
      legal: ['/Legal?status=all'], accountant: ['/Accounting'], idea: ['/ManageIdea/Idea']
    };
    for (const [role, paths] of Object.entries(routes)) {
      const context = await browser.newContext({ baseURL: 'http://127.0.0.1:15028', viewport: { width: 1440, height: 1000 } });
      const page = await context.newPage();
      page.on('pageerror', e => errors.push(`${role}: ${e.message}`));
      await page.goto('/Login');
      await page.locator('[name=username]').fill(`v4_${role}`);
      await page.locator('[name=password]').fill(process.env.V4_PASSWORD);
      await Promise.all([page.waitForURL(url => !url.pathname.includes('/Login')), page.locator('button[type=submit]').click()]);
      for (const width of [1440, 390]) {
        await page.setViewportSize({ width, height: 1000 });
        for (const path of paths) {
          const response = await page.goto(path);
          assert.equal(response.status(), 200, `${role} ${path}`);
          assert(!page.url().includes('/AccessDenied'));
          await page.waitForTimeout(150);
          const scrollWidth = await page.evaluate(() => document.documentElement.scrollWidth);
          assert(scrollWidth <= width + 1, `${role} ${path}: width ${scrollWidth} exceeds ${width}`);
          assert(await page.locator('#appSidebar').count(), `${role}: preserve sidebar`);
          if (width === 390) {
            await page.locator('#mobileMenuToggle').click();
            assert.equal(await page.locator('#mobileMenuToggle').getAttribute('aria-expanded'), 'true');
            await page.keyboard.press('Escape');
          }
          const shot = `${role}-${path.split('?')[0].split('/').filter(Boolean).join('-')}-${width}.png`;
          await page.screenshot({ path: `tests/artifacts/v4/${shot}`, fullPage: true, animations: "disabled" });
          checks++;
        }
      }
      if (role === 'manager') {
        await page.setViewportSize({ width: 1440, height: 1000 });
        await page.goto('/Bookings');
        const draftRow = page.locator('tr').filter({ has: page.locator(`a[href="/Bookings/Details/${process.env.V4_DRAFT_ID}"]`) });
        await draftRow.locator('button.btn-action-edit').click();
        assert.equal(await page.locator('#bookingActualCost').inputValue(), '40000000.00', 'Edit uses snapshot 60/10/30, not current 50/10/40');
        await page.locator('#bookingPrice').fill('200000000');
        assert.equal(await page.locator('#bookingActualCost').inputValue(), '80000000.00');
        assert.equal(await page.locator('#bookingForm [name=participantIds]').count(), 0);
        assert.equal(await page.locator('#bookingKolId option:checked').count(), 3);
        assert.equal(await page.locator('#bookingForm [name=acceptanceFile]').count(), 0);
        await page.waitForTimeout(250);
        assert(await page.locator('#bookingForm').evaluate(f => f.checkValidity()), 'Valid editable Booking form');
        await page.screenshot({ path: 'tests/artifacts/v4/booking-edit.png', fullPage: true });
        checks += 6;
        // Create with manually entered wages; filters must never discard selection.
        await page.goto('/Bookings');
        await page.evaluate(() => openCreateModal());
        await page.locator('#bookingCampaignId').selectOption(process.env.V4_CAMPAIGN_ID);
        await page.locator('#bookingKolId').selectOption(process.env.V4_KOL_IDS.split(','));
        await page.locator('#bookingDeadline').fill('2026-12-31');
        await page.locator('#bookingPrice').fill('100000000');
        const editor=page.locator('#createWageAllocation .wage-editor');
        const wage=editor.locator(`#wage-${process.env.V4_STAFF_ID}`);
        const person=editor.locator('.wage-person').filter({has:page.locator(`#wage-${process.env.V4_STAFF_ID}`)});
        await editor.locator('[data-wage-search]').fill('v4 staff');
        assert.equal(await editor.locator('.wage-person:visible').count(),1);
        await person.locator('[data-wage-choice]').check();
        await wage.fill('3000000.25');
        assert((await editor.locator('[data-wage-remaining]').innerText()).includes('6.999.999,75'));
        await editor.locator('[data-wage-department]').selectOption('Booking');
        await editor.locator('[data-wage-search]').fill('no such employee');
        assert(await editor.locator('[data-wage-empty]').isVisible());
        assert(await person.locator('[data-wage-choice]').isChecked());
        assert.equal(await wage.isDisabled(),false);
        await editor.locator('[data-wage-search]').fill('');
        await editor.locator('[data-wage-selected]').check();
        assert.equal(await editor.locator('.wage-person:visible').count(),1);
        await editor.locator('[data-wage-selected]').uncheck();
        await editor.locator('[data-wage-department]').selectOption('HCNS');
        await editor.locator('[data-wage-search]').fill('dang anh');
        assert.equal(await editor.locator('.wage-person:visible').count(),1, 'Unaccented name plus department filtering');
        const extraPerson=editor.locator('.wage-person:visible');
        await extraPerson.locator('[data-wage-choice]').check();
        await extraPerson.locator('[data-wage-amount]').fill('1000000');
        await editor.locator('[data-wage-department]').selectOption('Booking');
        assert.equal(await editor.locator('.wage-person:visible').count(),0,'Department filter excludes name match in another department');
        await editor.locator('[data-wage-department]').selectOption('');
        await editor.locator('[data-wage-search]').fill('');
        await editor.locator('[data-wage-selected]').check();
        assert.equal(await editor.locator('.wage-person:visible').count(),2);
        await wage.fill('10000001');
        await page.locator('#bookingForm button[type=submit]').click();
        assert(await editor.locator('.wage-error').isVisible());
        assert(page.url().endsWith('/Bookings'));
        await wage.fill('3000000.25');
        await page.setViewportSize({width:390,height:1000});
        await editor.scrollIntoViewIfNeeded();
        assert(await page.evaluate(()=>document.documentElement.scrollWidth)<=391);
        await page.screenshot({path:'tests/artifacts/v4/booking-create-wages-mobile.png',fullPage:true});
        await page.setViewportSize({width:1440,height:1000});
        await editor.scrollIntoViewIfNeeded();
        await page.screenshot({path:'tests/artifacts/v4/booking-create-wages-desktop.png',fullPage:true});
        await Promise.all([page.waitForURL(/Bookings?\/Details\/\d+/),page.locator('#bookingForm button[type=submit]').click()]);
        assert.equal(await page.locator(`#wage-${process.env.V4_STAFF_ID}`).inputValue(),'3000000.25');
        assert.equal(Number(await page.locator(`#wage-${process.env.V4_FILTER_STAFF_ID}`).inputValue()),1000000);
        const detailsEditor=page.locator('.wage-editor');
        const wageForm=page.locator('form[action*="UpdateWages"]');
        await detailsEditor.locator('[data-wage-search]').fill('v4 staff');
        await page.locator(`#wage-${process.env.V4_STAFF_ID}`).fill('2000000');
        await Promise.all([page.waitForNavigation(),wageForm.locator('button').click()]);
        assert.equal(Number(await page.locator(`#wage-${process.env.V4_STAFF_ID}`).inputValue()),2000000);
        await page.locator('.wage-person').filter({has:page.locator(`#wage-${process.env.V4_STAFF_ID}`)}).locator('[data-wage-choice]').uncheck();
        await Promise.all([page.waitForNavigation(),page.locator('form[action*="UpdateWages"] button').click()]);
        assert.equal(await page.locator(`#wage-${process.env.V4_STAFF_ID}`).isDisabled(),true);
        checks += 18;
        const campaignId = process.env.V4_CAMPAIGN_FLOW_ID;
        await page.goto('/Campaigns/Details/' + campaignId);
        const complete = page.locator('form[action="/Campaigns/Complete/' + campaignId + '"]');
        assert(await complete.isVisible());
        page.once('dialog', dialog => dialog.accept());
        await Promise.all([page.waitForNavigation(), complete.locator('button').click()]);
        const receipt = page.locator('form[action="/Campaigns/Accept/' + campaignId + '"]');
        assert(await receipt.isVisible());
        assert.equal(await receipt.evaluate(f => f.checkValidity()), false, 'Cannot accept without received-payment checkbox');
        await page.setViewportSize({ width: 390, height: 1000 });
        assert(await receipt.locator('button').isVisible());
        await receipt.locator('[name=receivedPayment]').check();
        await Promise.all([page.waitForNavigation(), receipt.locator('button').click()]);
        assert((await page.locator('body').innerText()).includes('Đã nghiệm thu'));
        assert.equal(await page.locator('form[action="/Campaigns/Accept/' + campaignId + '"]').count(), 0);
        assert(await page.evaluate(() => document.documentElement.scrollWidth) <= 391);
        await page.screenshot({ path: 'tests/artifacts/v4/campaign-accepted-mobile.png', fullPage: true });
        await page.goto('/Campaigns?status=accepted');
        assert(await page.locator('a[href="/Campaigns/Details/' + campaignId + '"]').count());
        checks += 8;
      }
      if (role === 'director') {
        await page.goto('/Director/Dashboard');
        assert.equal(await page.locator('[data-metric]').count(), 3);
        const debt = await page.locator('[data-metric=outstanding-campaigns]').getAttribute('data-value');
        await page.locator('.period-tab').filter({ hasText: 'Ngày' }).click();
        assert.equal(await page.locator('[data-metric=outstanding-campaigns]').getAttribute('data-value'), debt);
        await page.getByRole('link', { name: 'Xem danh sách đầy đủ' }).click();
        assert(page.url().includes('pendingAcceptance=true'));
        assert.equal(await page.locator('a[href="/Campaigns/Details/' + process.env.V4_CAMPAIGN_FLOW_ID + '"]').count(), 0);
        checks += 4;
        await page.goto('/Director/BookingCampaign');
        await page.locator('.tab-header').filter({ hasText: 'Tổng quan thù lao' }).click();
        const salaryButtons = page.locator('button[data-salary]');
        const salaryIndex = await salaryButtons.evaluateAll(buttons => buttons.findIndex(button => {
          const data = JSON.parse(button.dataset.salary);
          return !data.legacy && data.pool === 10000000;
        }));
        assert(salaryIndex >= 0, 'Find the v4 commission snapshot instead of a legacy fixture');
        await salaryButtons.nth(salaryIndex).click();
        assert(await page.locator('#salaryModal').isVisible());
        assert.equal((await page.locator('#salBookingValue').innerText()).replace(/\D/g, ''), '10000000');
        checks += 2;
      }
      await context.close();
    }
    assert.deepEqual(errors, [], 'Browser JavaScript errors');
    console.log(`PASS: ${checks} browser viewport/interaction checks, no page JavaScript errors.`);
  } finally { await browser.close(); }
})().catch(e => { console.error(e.message); process.exitCode = 1; });
