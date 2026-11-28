// Drives "blood between houses" (docs/FEUD.md): arranges accidents for people of other houses from the Houses screen, year after
// year, until one is laid at your door, then reads the panel on the You screen, offers the price, and says what happened.
//
//   node feud.js ["Start title"]
//
// Needs the build served locally (see README.md here). Screenshots go to PRAETORIA_SHOTS (default ./shots).
const { chromium } = require('playwright');
const title = process.argv[2] || 'The Old Lion';
const base = process.env.PRAETORIA_URL || 'http://localhost:8112/'; const shots = process.env.PRAETORIA_SHOTS || './shots';
require('fs').mkdirSync(shots, { recursive: true });
(async () => {
  const browser = await chromium.launch({ args: ['--use-gl=angle', '--use-angle=swiftshader', '--ignore-gpu-blocklist'] });
  const page = await browser.newPage({ viewport: { width: 1600, height: 1400 }, deviceScaleFactor: 2 }); page.setDefaultTimeout(60000);
  const errors = []; page.on('console', m => { if (m.type() === 'error') errors.push(m.text().slice(0, 300)); }); page.on('pageerror', e => errors.push('PAGEERROR ' + e.message.slice(0, 300)));
  await page.goto(base, { waitUntil: 'networkidle' }); await page.waitForSelector('.title-screen', { timeout: 90000 });
  await page.evaluate(() => { try { localStorage.setItem('praetoria.primer.seen', '1'); } catch (e) {} });
  await page.click('.scenario:has-text("' + title + '")'); await page.click('button.begin'); await page.waitForSelector('.gs-shell', { timeout: 90000 });
  const rail = name => page.evaluate(n => { const el = [...document.querySelectorAll('.rail-btn')].find(e => e.innerText.trim().toLowerCase() === n.toLowerCase()); if (el) el.click(); return !!el; }, name);
  const dismiss = async () => { for (let i = 0; i < 10; i++) { const had = await page.evaluate(() => { const c = document.querySelector('.win-close') || document.querySelector('.overlay .btn.primary'); if (c) c.click(); return !!c; }); if (!had) break; await page.waitForTimeout(250); } };
  const panel = () => page.evaluate(() => { const p = document.querySelector('.panel.feud'); return p ? p.innerText.replace(/\s+/g, ' ').slice(0, 900) : ''; });
  await dismiss();
  let found = '';
  for (let year = 1; year <= 14 && !found; year++) {
    await rail('Houses'); await page.waitForTimeout(500);
    // open each house in turn until one offers an accident that can be arranged
    const tried = await page.evaluate(async () => {
      const wait = ms => new Promise(r => setTimeout(r, ms));
      // a house's page replaces the standings (its own tab), so each house is opened from the standings afresh
      for (let i = 0; i < 40; i++) {
        const back = [...document.querySelectorAll('.ui-tabs .ui-tab')].find(t => t.innerText.trim().startsWith('The standings')); if (back) { back.click(); await wait(200); }
        const rows = [...document.querySelectorAll('.houses tbody tr, .house-row, .standings tbody tr')]; if (i >= rows.length) break;
        rows[i].click(); await wait(250);
        const b = [...document.querySelectorAll('.hu-person button')].find(x => /^Arrange (an )?accident/.test(x.innerText.trim()) && !x.disabled); if (b) { const t = b.innerText.replace(/\s+/g, ' ').trim(); b.click(); return t; }
      }
      const any = [...document.querySelectorAll('.hu-person button')].find(x => /^Arrange (an )?accident/.test(x.innerText.trim()) && !x.disabled); if (any) { const t = any.innerText.trim(); any.click(); return t; }
      return '';
    });
    await page.waitForTimeout(400); await rail('You'); await page.waitForTimeout(500); found = await panel();
    console.log(`year ${year}: ${tried || 'no accident could be arranged'}${found ? ' · the panel has appeared' : ''}`);
    if (!found) { await page.evaluate(() => { const b = document.querySelector('button.endturn'); if (b) b.click(); }); await page.waitForTimeout(1800); await dismiss(); }
  }
  if (!found) { console.log('no death was laid at your door in fourteen years'); console.log('errors: ' + errors.length); await browser.close(); return; }
  console.log('panel: ' + found);
  // the You window's tall header folds into a strip as its body scrolls, which can keep the panel moving: photograph the page if the panel will not hold still
  const el = await page.$('.panel.feud'); if (el) { await el.scrollIntoViewIfNeeded(); try { await el.screenshot({ path: shots + '/feud-panel.png', timeout: 15000 }); } catch (e) { try { await page.screenshot({ path: shots + '/feud-panel.png', timeout: 30000 }); } catch (e2) { console.log('no photograph: ' + e2.message.split('\n')[0]); } } }
  console.log('buttons: ' + await page.evaluate(() => [...document.querySelectorAll('.panel.feud button')].map(b => b.innerText.trim() + (b.disabled ? ' (off: ' + (b.title || '').slice(0, 70) + ')' : '')).join(' | ')));
  const did = await page.evaluate(() => { const b = [...document.querySelectorAll('.panel.feud button')].find(x => /Offer the price|Pay it|Take the field|Ask the price/.test(x.innerText) && !x.disabled); if (!b) return ''; const t = b.innerText.trim(); b.click(); return t; });
  await page.waitForTimeout(500); console.log('clicked: ' + (did || 'nothing could be clicked')); console.log('after: ' + await panel());
  await rail('Chronicle'); await page.waitForTimeout(500);
  console.log('chronicle: ' + await page.evaluate(() => [...document.querySelectorAll('.chron-line, .timeline li, .chronicle li, .tl-row')].map(e => e.innerText.replace(/\s+/g, ' ')).filter(t => /blood|dies suddenly|credits for|will not take money|calls/.test(t)).slice(0, 6).join(' || ')));
  console.log('errors: ' + errors.length + (errors.length ? ' · ' + errors.slice(0, 3).join(' | ') : '')); await browser.close();
})();
