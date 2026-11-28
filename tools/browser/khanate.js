// The nations on the map (docs/BEYOND.md): takes a picture of the whole Galaxy screen, with the five nations' space beyond the rim (the
// Vashti Steppe, the Thessaran Shore, the Ardent Concord, the Outer Compact, the Hollow Crown), then reads the card of each on the Military screen ("Beyond the
// Reach"): where its ruler sits, what it holds, where its fleets are, and what kind of neighbour it is.
//
//   node khanate.js ["Start title"]
//
// Needs the build served locally (see README.md here). Screenshots go to PRAETORIA_SHOTS (default ./shots).
const { chromium } = require('playwright');
const title = process.argv[2] || 'The Old Lion';
const base = require('./address')(); const shots = process.env.PRAETORIA_SHOTS || './shots';
require('fs').mkdirSync(shots, { recursive: true });
(async () => {
  const browser = await chromium.launch({ args: ['--use-gl=angle', '--use-angle=swiftshader', '--ignore-gpu-blocklist', '--disable-gpu-rasterization'] });
  const page = await browser.newPage({ viewport: { width: 1600, height: 1100 }, deviceScaleFactor: +(process.env.PRAETORIA_SCALE || 2) }); page.setDefaultTimeout(180000);
  const errors = []; page.on('console', m => { if (m.type() === 'error') errors.push(m.text().slice(0, 300)); }); page.on('pageerror', e => errors.push('PAGEERROR ' + e.message.slice(0, 300)));
  // a panel inside the docked window scrolls with the window's body, so a tall one is photographed with the screen grown to hold it
  const tallShot = async (sel, path) => { const h = await page.evaluate(s => { const e = document.querySelector(s); return e ? Math.ceil(e.getBoundingClientRect().height) : 0; }, sel); if (!h) return; const vp = page.viewportSize(); const need = Math.min(5000, h + 700); if (need > vp.height) { await page.setViewportSize({ width: vp.width, height: need }); await page.waitForTimeout(800); } const el = await page.$(sel); await el.scrollIntoViewIfNeeded(); await el.screenshot({ path, timeout: 180000 }); if (need > vp.height) { await page.setViewportSize(vp); await page.waitForTimeout(400); } };
  await page.goto(base, { waitUntil: 'networkidle' }); await page.waitForSelector('.title-screen', { timeout: 90000 });
  await page.evaluate(() => { try { localStorage.setItem('praetoria.primer.seen', '1'); } catch (e) {} });
  await page.click('.scenario:has-text("' + title + '")'); await page.click('button.begin'); await page.waitForSelector('.gs-shell', { timeout: 90000 });
  for (let i = 0; i < 10; i++) { const had = await page.evaluate(() => { const c = document.querySelector('.win-close') || document.querySelector('.overlay .btn.primary'); if (c) c.click(); return !!c; }); if (!had) break; await page.waitForTimeout(250); }
  await page.evaluate(() => { const el = document.querySelector('[data-close-window]') || [...document.querySelectorAll('.rail-btn')].find(e => /^galaxy$/i.test(e.innerText.trim())); if (el) el.click(); }); await page.waitForTimeout(2500);   // back to the map: the docked window's close (the rail has no Galaxy socket)
  await page.evaluate(() => { const b = (document.querySelector('[data-map="fit"]') || document.querySelector('button[title="Fit the whole Reach"]')); if (b) b.click(); }); await page.waitForTimeout(3000);
  await page.screenshot({ path: shots + "/khanate-map.png", timeout: 240000 });
  const labels = await page.evaluate(() => (document.body.innerText.match(/Vashti Steppe|Thessaran Shore|Ardent Concord|Outer Compact|Hollow Crown/gi) || []).map(x => x.toLowerCase().replace(/\b\w/g, c => c.toUpperCase())));   // the map writes region names in capitals
  console.log('named on the map screen: ' + [...new Set(labels)].join(', '));
  await page.evaluate(() => { const el = [...document.querySelectorAll('.rail-btn')].find(e => /^military$/i.test(e.innerText.trim())); if (el) el.click(); }); await page.waitForTimeout(500);
  await page.evaluate(() => { const t = (document.querySelector('.screen.window .ui-tab[data-tab="abroad"]') || [...document.querySelectorAll('.ex-tabs .rung')].find(e => /beyond the reach/i.test(e.innerText))); if (t) t.click(); }); await page.waitForTimeout(600);
  // each card keeps its ruler, its march, its seat and its fleets under a fold ("Its ruler, march, seat and fleets"): open every fold to read them
  await page.evaluate(() => document.querySelectorAll('.panel.abroad .nation details').forEach(d => { d.open = true; })); await page.waitForTimeout(400);
  const rows = await page.evaluate(() => [...document.querySelectorAll('.panel.abroad .nation')].map(n => n.innerText.replace(/\s+/g, ' ').slice(0, 1100)));
  for (const [name, seat] of [['vashti', 'khal orda'], ['thessar', 'thessar'], ['ardent concord', 'concordia'], ['outer compact', 'the wardenry'], ['hollow crown', 'porphyra']]) {
    const row = rows.find(r => new RegExp(name, 'i').test(r)); console.log(row ? '· ' + row : name + ' is not shown');
    console.log('  on the map: ' + /on the map/i.test(row || '') + ' · sits at ' + seat + ': ' + new RegExp('sits at\\s*' + seat, 'i').test(row || '') + ' · fleets listed: ' + /its fleets/i.test(row || ''));
  }
  await tallShot('.panel.abroad', shots + '/khanate-card.png');
  console.log('wider than the screen: ' + await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth + 2));
  console.log('errors: ' + errors.length + (errors.length ? ' · ' + errors.slice(0, 3).join(' | ') : '')); await browser.close();
})();
