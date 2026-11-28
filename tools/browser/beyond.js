// Reads "Beyond the Reach" on the Military screen (docs/BEYOND.md, stage 0): each nation's ruler, the commander of its fleets, its
// march on the rim and whether a fleet holds it, and how far out an expedition would sail from your nearest fleet.
//
//   node beyond.js ["Start title"]
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
  // a panel inside the docked window scrolls with the window's body, so a tall one is photographed with the screen grown to hold it
  const tallShot = async (sel, path) => { const h = await page.evaluate(s => { const e = document.querySelector(s); return e ? Math.ceil(e.getBoundingClientRect().height) : 0; }, sel); if (!h) return; const vp = page.viewportSize(); const need = Math.min(5000, h + 700); if (need > vp.height) { await page.setViewportSize({ width: vp.width, height: need }); await page.waitForTimeout(800); } const el = await page.$(sel); await el.scrollIntoViewIfNeeded(); await el.screenshot({ path, timeout: 180000 }); if (need > vp.height) { await page.setViewportSize(vp); await page.waitForTimeout(400); } };
  await page.goto(base, { waitUntil: 'networkidle' }); await page.waitForSelector('.title-screen', { timeout: 90000 });
  await page.evaluate(() => { try { localStorage.setItem('praetoria.primer.seen', '1'); } catch (e) {} });
  await page.click('.scenario:has-text("' + title + '")'); await page.click('button.begin'); await page.waitForSelector('.gs-shell', { timeout: 90000 });
  for (let i = 0; i < 10; i++) { const had = await page.evaluate(() => { const c = document.querySelector('.win-close') || document.querySelector('.overlay .btn.primary'); if (c) c.click(); return !!c; }); if (!had) break; await page.waitForTimeout(250); }
  await page.evaluate(() => { const el = [...document.querySelectorAll('.rail-btn')].find(e => /^military$/i.test(e.innerText.trim())); if (el) el.click(); }); await page.waitForTimeout(500);
  await page.evaluate(() => { const t = (document.querySelector('.screen.window .ui-tab[data-tab="abroad"]') || [...document.querySelectorAll('.ex-tabs .rung')].find(e => /beyond the reach/i.test(e.innerText))); if (t) t.click(); }); await page.waitForTimeout(600);
  // each card keeps its ruler, its march, its seat and its fleets under a fold ("Its ruler, march, seat and fleets"): open every fold to read them
  await page.evaluate(() => document.querySelectorAll('.panel.abroad .nation details').forEach(d => { d.open = true; })); await page.waitForTimeout(400);
  const rows = await page.evaluate(() => [...document.querySelectorAll('.panel.abroad .nation')].map(n => n.innerText.replace(/\s+/g, ' ').slice(0, 420)));
  if (rows.length === 0) console.log('no nations shown'); for (const r of rows) console.log('· ' + r);
  await tallShot('.panel.abroad', shots + '/beyond-nations.png');
  console.log('wider than the screen: ' + await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth + 2));
  console.log('errors: ' + errors.length + (errors.length ? ' · ' + errors.slice(0, 3).join(' | ') : '')); await browser.close();
})();
