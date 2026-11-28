// Reads the hold on a throne (docs/THRONE.md) from the Senate screen in a start that opens under one: what it is made of, whether a
// standard has been raised, and what the buttons say. Plays on year by year until a pretender rises or the years run out.
//
//   node throne.js ["Start title"] [years]
//
// Needs the build served locally (see README.md here). Screenshots go to PRAETORIA_SHOTS (default ./shots).
const { chromium } = require('playwright');
const title = process.argv[2] || 'The Imperial Court'; const years = parseInt(process.argv[3] || '30', 10);
const base = require('./address')(); const shots = process.env.PRAETORIA_SHOTS || './shots';
require('fs').mkdirSync(shots, { recursive: true });
(async () => {
  const browser = await chromium.launch({ args: ['--use-gl=angle', '--use-angle=swiftshader', '--ignore-gpu-blocklist', '--disable-gpu-rasterization'] });
  const page = await browser.newPage({ viewport: { width: 1600, height: 1400 }, deviceScaleFactor: +(process.env.PRAETORIA_SCALE || 2) }); page.setDefaultTimeout(180000);
  const errors = []; page.on('console', m => { if (m.type() === 'error') errors.push(m.text().slice(0, 300)); }); page.on('pageerror', e => errors.push('PAGEERROR ' + e.message.slice(0, 300)));
  await page.goto(base, { waitUntil: 'networkidle' }); await page.waitForSelector('.title-screen', { timeout: 90000 });
  await page.evaluate(() => { try { localStorage.setItem('praetoria.primer.seen', '1'); } catch (e) {} });
  await page.click('.scenario:has-text("' + title + '")'); await page.click('button.begin'); await page.waitForSelector('.gs-shell', { timeout: 90000 });
  const rail = () => page.evaluate(() => { const el = [...document.querySelectorAll('.rail-btn')].find(e => /^(senate|court)$/i.test(e.innerText.trim())); if (el) el.click(); return el ? el.innerText.trim() : ''; });
  const dismiss = async () => { for (let i = 0; i < 10; i++) { const had = await page.evaluate(() => { const c = document.querySelector('.win-close') || document.querySelector('.overlay .btn.primary'); if (c) c.click(); return !!c; }); if (!had) break; await page.waitForTimeout(250); } };
  const read = () => page.evaluate(() => {
    const d = [...document.querySelectorAll('details')].find(x => /The throne/.test(x.querySelector('summary')?.innerText || '')); if (!d) return null; d.open = true;
    const kv = d.querySelector('.kv'); return { text: d.innerText.replace(/\s+/g, ' ').slice(0, 700), made: kv ? kv.getAttribute('title') : '', buttons: [...d.querySelectorAll('button')].map(b => b.innerText.replace(/\s+/g, ' ').trim() + (b.disabled ? ' (off: ' + (b.title || '').slice(0, 80) + ')' : '')).join(' | ') };
  });
  const tab = () => page.evaluate(() => { const t = document.querySelector('.screen.window .ui-tab[data-tab="offices"]') || [...document.querySelectorAll('.ex-tabs .rung')].find(e => e.innerText.includes('titles')); if (t) t.click(); return !!t; });   // the throne is on the Senate's Offices tab
  await dismiss(); console.log('screen: ' + await rail()); await page.waitForTimeout(400); await tab(); await page.waitForTimeout(500);
  console.log('headline tile: ' + await page.evaluate(() => { const t = [...document.querySelectorAll('.tile, .ui-fig, .ui-facts > span')].find(e => /the throne/i.test(e.innerText + ' ' + (e.title || ''))); return t ? t.innerText.replace(/\s+/g, ' ') : 'none'; }));
  let first = await read(); if (!first) { console.log('no throne panel found'); await browser.close(); return; }
  console.log('year 1: ' + first.text); console.log('made of: ' + first.made); console.log('buttons: ' + first.buttons);
  const shot = async name => { const el = await page.evaluateHandle(() => { const d = [...document.querySelectorAll('details')].find(x => /The throne/.test(x.querySelector('summary')?.innerText || '')); return d ? (d.closest('section') || d) : null; });   /* the throne is a fold inside the Senate's rank section: picture the fold */ if (el.asElement()) { await el.asElement().scrollIntoViewIfNeeded(); await el.asElement().screenshot({ path: shots + '/' + name }); } };
  await shot('throne-hold.png');
  let risen = false;
  for (let year = 2; year <= years && !risen; year++) {
    { const before = await page.evaluate(() => { const d = document.querySelector('[data-turn]'); return d ? +d.dataset.turn : -1; }); await page.evaluate(() => { const b = document.querySelector('button.endturn'); if (b) b.click(); }); for (let i = 0; i < 1200; i++) { await page.waitForTimeout(50); if (await page.evaluate(() => { const d = document.querySelector('[data-turn]'); return d ? +d.dataset.turn : -1; }) > before) break; } await page.waitForTimeout(300); } await dismiss(); await rail(); await page.waitForTimeout(250); await tab(); await page.waitForTimeout(300);
    const r = await read(); if (r && /has raised a standard/.test(r.text)) { risen = true; console.log(`year ${year}: ${r.text}`); console.log('buttons: ' + r.buttons); await shot('throne-rising.png'); }
  }
  if (!risen) console.log(`nobody raised a standard in ${years} years`);
  console.log('errors: ' + errors.length + (errors.length ? ' · ' + errors.slice(0, 3).join(' | ') : '')); await browser.close();
})();
