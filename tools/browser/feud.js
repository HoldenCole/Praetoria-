// Drives "blood between houses" (docs/FEUD.md): arranges accidents for people of other houses from the Houses screen, year after
// year, until one is laid at your door, then reads the panel on the You screen, offers the price, and says what happened.
//
//   node feud.js ["Start title"]
//
// Needs the build served locally (see README.md here). Screenshots go to PRAETORIA_SHOTS (default ./shots).
const { chromium } = require('playwright');
const title = process.argv[2] || 'The Old Lion';
const base = require('./address')(); const shots = process.env.PRAETORIA_SHOTS || './shots';
require('fs').mkdirSync(shots, { recursive: true });
(async () => {
  const browser = await chromium.launch({ args: ['--use-gl=angle', '--use-angle=swiftshader', '--ignore-gpu-blocklist', '--disable-gpu-rasterization'] });
  const page = await browser.newPage({ viewport: { width: 1600, height: 1400 }, deviceScaleFactor: +(process.env.PRAETORIA_SCALE || 2) }); page.setDefaultTimeout(180000);
  const errors = []; page.on('console', m => { if (m.type() === 'error') errors.push(m.text().slice(0, 300)); }); page.on('pageerror', e => errors.push('PAGEERROR ' + e.message.slice(0, 300)));
  await page.goto(base, { waitUntil: 'networkidle' }); await page.waitForSelector('.title-screen', { timeout: 90000 });
  await page.evaluate(() => { try { localStorage.setItem('praetoria.primer.seen', '1'); } catch (e) {} });
  await page.click('.scenario:has-text("' + title + '")'); await page.click('button.begin'); await page.waitForSelector('.gs-shell', { timeout: 90000 });
  const rail = name => page.evaluate(n => { const el = [...document.querySelectorAll('.rail-btn')].find(e => e.innerText.trim().toLowerCase() === n.toLowerCase()); if (el) el.click(); return !!el; }, name);
  const dismiss = async () => { for (let i = 0; i < 10; i++) { const had = await page.evaluate(() => { const c = document.querySelector('.win-close') || document.querySelector('.overlay .btn.primary'); if (c) c.click(); return !!c; }); if (!had) break; await page.waitForTimeout(250); } };
  const panel = () => page.evaluate(() => { const p = document.querySelector('.panel.feud'); return p ? p.innerText.replace(/\s+/g, ' ').slice(0, 900) : ''; });
  await dismiss();
  let found = '', lastSeen = '';
  for (let year = 1; year <= 14 && !found; year++) {
    await rail('Houses'); await page.waitForTimeout(500);
    // open each house in turn until one offers an accident that can be arranged
    const tried = await page.evaluate(async () => {
      const wait = ms => new Promise(r => setTimeout(r, ms));
      // a house's page replaces the standings (its own tab), so each house is opened from the standings afresh
      // the accident's plate carries its order in data-act ("assassinate")
      // under load a page can take more than a moment to draw: wait for it (the standings' rows, or a house's people) rather than a fixed time
      const until = async (test, ms) => { for (let t = 0; t < ms; t += 100) { if (test()) return true; await wait(100); } return false; };
      for (let i = 0; i < 40; i++) {
        const back = [...document.querySelectorAll('.ui-tabs .ui-tab')].find(t => t.innerText.trim().startsWith('The standings')); if (back && !back.classList.contains('on')) { back.click(); await until(() => document.querySelector('.standings tbody tr'), 8000); }
        const rows = [...document.querySelectorAll('.houses tbody tr, .house-row, .standings tbody tr')]; if (i >= rows.length) break;
        if (rows[i].classList.contains('mine')) continue;   // your own house
        rows[i].click(); await until(() => document.querySelector('.hu-person') || !document.querySelector('.standings'), 8000); await wait(150);
        const b = [...document.querySelectorAll('.hu-person button')].find(x => (x.dataset.act === 'assassinate' || /^Arrange (an )?accident/.test(x.innerText.trim())) && !x.disabled); if (b) { const t = b.innerText.replace(/\s+/g, ' ').trim(); b.click(); return t; }
      }
      const any = [...document.querySelectorAll('.hu-person button')].find(x => (x.dataset.act === 'assassinate' || /^Arrange (an )?accident/.test(x.innerText.trim())) && !x.disabled); if (any) { const t = any.innerText.trim(); any.click(); return t; }
      return '';
    });
    // the accident is asked first ("Before it is done"): say yes, and put away what the outcome box says
    let asked = '', outcome = '';
    if (tried) {
      await page.waitForTimeout(600);
      asked = await page.evaluate(() => { const y = document.querySelector('[data-confirm="yes"]'); if (!y) return ''; const q = ((y.closest('.confirm-box') || {}).innerText || '').replace(/\s+/g, ' ').slice(0, 160); y.click(); return q; });
      await page.waitForTimeout(800);
      outcome = await page.evaluate(() => { const o = document.querySelector('[data-outcome-ok]'); if (!o) return ''; const t = ((o.closest('.confirm-box') || {}).innerText || '').replace(/\s+/g, ' ').slice(0, 200); o.click(); return t; });
      if (year === 1) console.log('  asked: ' + (asked || 'NO QUESTION WAS ASKED'));
      if (outcome) console.log('  outcome: ' + outcome);
    }
    await page.waitForTimeout(400); await rail('You'); await page.waitForTimeout(500); const shown = await panel();
    // the panel also shows blood between other houses ("Elsewhere"): go on until a feud is yours, one with something to answer
    found = shown && await page.evaluate(() => document.querySelectorAll('.panel.feud button').length > 0) ? shown : ''; if (shown && !found) lastSeen = shown;
    console.log(`year ${year}: ${tried || 'no accident could be arranged'}${found ? ' · laid at your door: the panel has your feud' : shown ? ' · the panel shows the blood of others only' : ''}`);
    if (!found) { const before = await page.evaluate(() => { const d = document.querySelector('[data-turn]'); return d ? +d.dataset.turn : -1; }); await dismiss(); await page.evaluate(() => { const b = document.querySelector('button.endturn'); if (b) b.click(); }); for (let i = 0; i < 1200; i++) { await page.waitForTimeout(50); if (await page.evaluate(() => { const d = document.querySelector('[data-turn]'); return d ? +d.dataset.turn : -1; }) > before) break; } await page.waitForTimeout(300); await dismiss(); }
  }
  if (!found) { console.log('no death was laid at your door in fourteen years' + (lastSeen ? '; the panel shows only the blood of others: ' + lastSeen : '')); console.log('errors: ' + errors.length); await browser.close(); return; }
  console.log('panel: ' + found);
  // the You window's tall header folds into a strip as its body scrolls, which can keep the panel moving: photograph the page if the panel will not hold still
  // so the picture is a clip of where the panel stands once scrolled to, which does not wait for it to hold still
  await page.evaluate(() => { const p = document.querySelector('.panel.feud'); if (p) p.scrollIntoView({ block: 'center' }); }); await page.waitForTimeout(1000);
  const box = await page.evaluate(() => { const p = document.querySelector('.panel.feud'); if (!p) return null; const r = p.getBoundingClientRect(); return { x: Math.max(0, r.x), y: Math.max(0, r.y), width: r.width, height: Math.min(r.height, window.innerHeight - Math.max(0, r.y)) }; });
  if (box) { try { await page.screenshot({ path: shots + '/feud-panel.png', clip: box, timeout: 180000 }); } catch (e) { console.log('no photograph: ' + e.message.split('\n')[0]); } }
  console.log('buttons: ' + await page.evaluate(() => [...document.querySelectorAll('.panel.feud button')].map(b => b.innerText.replace(/\s+/g, ' ').trim() + (b.disabled ? ' (off: ' + (b.title || '').split('\n').pop().slice(0, 120) + ')' : ''))   /* a locked plate's tooltip ends with what it needs */.join(' | ')));
  const did = await page.evaluate(() => { const b = [...document.querySelectorAll('.panel.feud button')].find(x => /Offer the price|Pay it|Take the field|Ask the price/.test(x.innerText) && !x.disabled); if (!b) return ''; const t = b.innerText.trim(); b.click(); return t; });
  await page.waitForTimeout(500); console.log('clicked: ' + (did || 'nothing could be clicked')); console.log('after: ' + await panel());
  await rail('Chronicle'); await page.waitForTimeout(500);
  console.log('chronicle: ' + await page.evaluate(() => [...document.querySelectorAll('.chron-line, .timeline li, .chronicle li, .tl-row')].map(e => e.innerText.replace(/\s+/g, ' ')).filter(t => /blood|dies suddenly|credits for|will not take money|calls/.test(t)).slice(0, 6).join(' || ')));
  console.log('errors: ' + errors.length + (errors.length ? ' · ' + errors.slice(0, 3).join(' | ') : '')); await browser.close();
})();
