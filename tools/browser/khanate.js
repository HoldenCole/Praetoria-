// The Vashti Khanates on the map (docs/BEYOND.md): takes a picture of the whole map, where the Vashti Steppe lies beyond The Anvil and
// The Veil with the Khanates' three worlds and two fleets on it, then reads the Khanates' card on the Military screen ("Beyond the
// Reach"): where their ruler sits, what they hold, where their fleets are.
//
//   node khanate.js ["Start title"]
//
// Needs the build served locally (see README.md here). Screenshots go to PRAETORIA_SHOTS (default ./shots).
const { chromium } = require('playwright');
const title = process.argv[2] || 'The Old Lion';
const base = process.env.PRAETORIA_URL || 'http://localhost:8112/'; const shots = process.env.PRAETORIA_SHOTS || './shots';
require('fs').mkdirSync(shots, { recursive: true });
(async () => {
  const browser = await chromium.launch({ args: ['--use-gl=angle', '--use-angle=swiftshader', '--ignore-gpu-blocklist'] });
  const page = await browser.newPage({ viewport: { width: 1600, height: 1100 }, deviceScaleFactor: 2 }); page.setDefaultTimeout(60000);
  const errors = []; page.on('console', m => { if (m.type() === 'error') errors.push(m.text().slice(0, 300)); }); page.on('pageerror', e => errors.push('PAGEERROR ' + e.message.slice(0, 300)));
  await page.goto(base, { waitUntil: 'networkidle' }); await page.waitForSelector('.title-screen', { timeout: 90000 });
  await page.evaluate(() => { try { localStorage.setItem('praetoria.primer.seen', '1'); } catch (e) {} });
  await page.click('.scenario:has-text("' + title + '")'); await page.click('button.begin'); await page.waitForSelector('.gs-shell', { timeout: 90000 });
  for (let i = 0; i < 10; i++) { const had = await page.evaluate(() => { const c = document.querySelector('.win-close') || document.querySelector('.overlay .btn.primary'); if (c) c.click(); return !!c; }); if (!had) break; await page.waitForTimeout(250); }
  await page.evaluate(() => { const el = [...document.querySelectorAll('.rail-btn')].find(e => /^galaxy$/i.test(e.innerText.trim())); if (el) el.click(); }); await page.waitForTimeout(2500);
  await page.evaluate(() => { const b = document.querySelector('button[title="Fit the whole Reach"]'); if (b) b.click(); }); await page.waitForTimeout(3000);
  await page.screenshot({ path: shots + "/khanate-map.png", timeout: 240000 });
  const labels = await page.evaluate(() => document.body.innerText.match(/Khal Orda|Tul Aimag|The Long Grass|Vashti Steppe/g) || []);
  console.log('named on the map screen: ' + [...new Set(labels)].join(', '));
  await page.evaluate(() => { const el = [...document.querySelectorAll('.rail-btn')].find(e => /^military$/i.test(e.innerText.trim())); if (el) el.click(); }); await page.waitForTimeout(500);
  await page.evaluate(() => { const t = [...document.querySelectorAll('.ex-tabs .rung')].find(e => /beyond the reach/i.test(e.innerText)); if (t) t.click(); }); await page.waitForTimeout(600);
  const rows = await page.evaluate(() => [...document.querySelectorAll('.panel.abroad .nation')].map(n => n.innerText.replace(/\s+/g, ' ').slice(0, 520)));
  const khans = rows.find(r => /vashti/i.test(r)); console.log(khans ? '· ' + khans : 'the Khanates are not shown');
  console.log('on the map: ' + /on the map/i.test(khans || '') + ' · sits at Khal Orda: ' + /sits at\s*khal orda/i.test(khans || '') + ' · fleets listed: ' + /its fleets/i.test(khans || ''));
  const el = await page.$('.panel.abroad'); if (el) { await el.scrollIntoViewIfNeeded(); await el.screenshot({ path: shots + '/khanate-card.png' }); }
  console.log('wider than the screen: ' + await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth + 2));
  console.log('errors: ' + errors.length + (errors.length ? ' · ' + errors.slice(0, 3).join(' | ') : '')); await browser.close();
})();
