// A locked answer, clicked, says why (docs/EVENTS.md, "Two answers open"): plays a start until a report shows an answer that is not open,
// clicks it, and checks that the line under it that says what it needs flashes (again on a second click), that a reader is told the same,
// and that nothing was chosen; and counts the reports that were opened with fewer than two answers open. Pictures before and after.
//   node locked.js ["The Fallen House"] [years]
const { chromium } = require('playwright');
const scenario = process.argv[2] || 'The Fallen House', years = +(process.argv[3] || 12);
const shots = process.env.PRAETORIA_SHOTS || __dirname + '/shots';
(async () => {
  const browser = await chromium.launch({ args: ['--use-gl=angle', '--use-angle=swiftshader', '--ignore-gpu-blocklist', '--disable-gpu-rasterization'] });
  const page = await browser.newPage({ viewport: { width: 1440, height: 900 }, deviceScaleFactor: 1 }); page.setDefaultTimeout(60000);
  const errors = []; page.on('pageerror', e => errors.push(e.message.slice(0, 300))); page.on('console', m => { if (m.type() === 'error') errors.push(m.text().slice(0, 200)); });
  await page.goto(require('./address')(), { waitUntil: 'networkidle' }); await page.waitForSelector('.title-screen', { timeout: 120000 });
  await page.evaluate(() => { try { localStorage.setItem('praetoria.primer.seen', '1'); } catch (e) {} });
  await page.click(`.scenario:has-text("${scenario}")`); await page.click('button.begin'); await page.waitForTimeout(3000);
  const has = async sel => (await page.locator(sel).count()) > 0;
  // close whatever stands over the game but a report: the year in brief, what came of an answer, the primer
  const clear = async () => {
    for (let k = 0; k < 6; k++) {
      if (await has('[data-brief-close]')) { await page.click('[data-brief-close]'); await page.waitForTimeout(300); continue; }
      if (await has('[data-outcome-ok]')) { await page.click('[data-outcome-ok]'); await page.waitForTimeout(300); continue; }
      break;
    }
  };
  const fails = []; let found = false, reports = 0, fewer = 0;
  for (let y = 0; y < years && !found; y++) {
    await clear();
    for (let i = 0; i < 8 && !found; i++) {
      await clear();
      if (!await has('.ev-win')) {
        // the next unanswered report from the outliner
        const next = page.locator('[data-report]:not(.done)');
        if (!await next.count()) break;
        await next.first().click(); await page.waitForTimeout(500);
        if (!await has('.ev-win')) break;
      }
      reports++;
      const open = await page.locator('.ev-ch.choice:not(.locked)').count(), locked = await page.locator('.ev-ch.choice.locked').count();
      if (open < 2) fewer++;
      if (locked > 0) {
        const choice = page.locator('.ev-ch.choice.locked').first(); const id = await choice.getAttribute('data-choice');
        await page.screenshot({ path: `${shots}/locked-before.png` });
        await choice.click({ force: true }); await page.waitForTimeout(250);   // (forced: Playwright waits on aria-disabled; a person's click on it lands)
        const flashed = await page.locator(`.ev-ch.locked [data-why="${id}"].flash`).count();
        const said = (await page.locator('[data-locked-said]').innerText()).trim();
        const why = (await page.locator(`[data-why="${id}"]`).innerText()).trim();
        const answered = await page.locator('.ev-win .ev-done').count();
        await page.screenshot({ path: `${shots}/locked-after.png` });
        console.log(`year ${y + 1}: clicked the locked answer "${id}": its line flashed ${flashed ? 'yes' : 'NO'}; the line says "${why}"; a reader hears "${said}"; answered by it: ${answered ? 'YES (wrong)' : 'no'}`);
        if (!flashed) fails.push('the requirement line did not flash');
        if (said.length < 3 || !said.toLowerCase().startsWith(why.toLowerCase().replace(/\.$/, '').slice(0, 12))) fails.push('what a reader hears is not what the line says');
        if (answered) fails.push('clicking a locked answer answered the report');
        await page.waitForTimeout(1300);
        await choice.click({ force: true }); await page.waitForTimeout(150);   // a second click flashes the line again: it is drawn afresh
        if (!await page.locator(`.ev-ch.locked [data-why="${id}"].flash`).count()) fails.push('a second click did not flash the line');
        found = true;
      } else {
        // answered with the first open answer, as a player would
        await page.locator('.ev-ch.choice:not(.locked)').first().click().catch(() => {});
        await page.waitForTimeout(500);
        if (await has('.ev-win') && await has('.ev-x')) { await page.click('.ev-x').catch(() => {}); await page.waitForTimeout(300); }
      }
    }
    if (!found) {
      await clear(); if (await has('.ev-win')) await page.click('.ev-x').catch(() => {});
      await page.evaluate(() => { const b = document.querySelector('button.endturn'); if (b && !b.disabled) b.click(); }); await page.waitForTimeout(3000);
      if (await has('[data-confirm-ok], .confirm-box button.primary')) { await page.locator('.confirm-box button.primary').first().click().catch(() => {}); await page.waitForTimeout(2500); }
    }
  }
  console.log(`${reports} reports opened; ${fewer} of them with fewer than two answers open as the browser came to them`);
  if (!found) fails.push(`no report with a locked answer in ${years} years`);
  if (errors.length) fails.push('console errors: ' + errors.slice(0, 3).join(' | '));
  console.log(fails.length ? 'FAILED: ' + fails.join('; ') : 'PASSED');
  await browser.close(); process.exit(fails.length ? 1 : 0);
})();
