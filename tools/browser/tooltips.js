// Tooltips and the map, checked in a real browser (docs/WEB_CLIENT.md, "Tooltips"; docs/GALAXY.md, "The chart at three distances").
//   node tooltips.js [shots-dir]        the published game must be served on :8112 (or PRAETORIA_URL)
// Fails (exit 1) if a tooltip does not open inside another, if one leaves the screen, if the map's own note stays up after the pointer
// has left its star, or if the page logs an error.
const { chromium } = require('playwright'); const path = require('path');
(async () => {
  const out = process.argv[2] || path.join(__dirname, 'shots'); const fails = []; const need = (ok, what) => { if (!ok) fails.push(what); console.log((ok ? 'ok   ' : 'FAIL ') + what); };
  const browser = await chromium.launch({ args: ['--use-gl=angle', '--use-angle=swiftshader', '--ignore-gpu-blocklist'] });
  const page = await browser.newPage({ viewport: { width: 1600, height: 1000 }, deviceScaleFactor: 1 }); page.setDefaultTimeout(60000);
  const errors = []; page.on('pageerror', e => errors.push(e.message.slice(0, 200))); page.on('console', m => { if (m.type() === 'error') errors.push(m.text().slice(0, 200)); });
  await page.goto(process.env.PRAETORIA_URL || 'http://localhost:8112/', { waitUntil: 'networkidle' }); await page.waitForSelector('.title-screen', { timeout: 120000 });
  await page.evaluate(() => { try { localStorage.setItem('praetoria.primer.seen', '1'); } catch (e) { } });
  await page.click('.scenario:has-text("The Old Lion")'); await page.click('button.begin'); await page.waitForSelector('.topbar', { timeout: 120000 }); await page.waitForTimeout(4000);
  const tips = () => page.evaluate(() => [...document.querySelectorAll('.gtip')].map(t => { const r = t.getBoundingClientRect(); return { level: +t.dataset.level, head: t.innerText.slice(0, 40), terms: t.querySelectorAll('.term').length, in: r.left >= 0 && r.top >= 0 && r.right <= innerWidth && r.bottom <= innerHeight }; }));
  need(await page.evaluate(() => window.praetoriaTips && praetoriaTips.terms() > 50 && praetoriaTips.placed()), 'the glossary is read and Floating UI is loaded');

  // a tooltip, a tooltip inside it, and one inside that
  const clout = page.locator('.topbar .res').filter({ hasText: /^\s*\d+/ }).nth(5); await page.evaluate(() => { const e = [...document.querySelectorAll('.topbar .res')].find(e => /clout/i.test(e.getAttribute('title') || e.dataset.tip || '')); if (e) e.setAttribute('data-probe', 'clout'); });
  await page.hover('[data-probe=clout]'); await page.waitForTimeout(600); let t = await tips();
  need(t.length === 1 && t[0].terms > 0, 'a tooltip opens and underlines the game\'s own words (' + (t[0] ? t[0].terms : 0) + ')');
  await page.locator('.gtip .term').last().hover(); await page.waitForTimeout(700); t = await tips();
  need(t.length === 2 && t[1].level === 1, 'resting on a word opens what it means beside the first');
  if (await page.locator('.gtip[data-level="1"] .term').count()) { await page.locator('.gtip[data-level="1"] .term').first().hover(); await page.waitForTimeout(700); t = await tips(); need(t.length === 3, 'and a word inside that opens a third'); }
  need(t.every(x => x.in), 'every one of them is wholly on the screen'); await page.screenshot({ path: path.join(out, 'tooltip-nested.png') });
  await page.mouse.move(800, 700, { steps: 6 }); await page.waitForTimeout(700); need((await tips()).length === 0, 'they close when the pointer leaves them');
  await page.hover('.endturn-dock button, button.end-turn, .endturn button'); await page.waitForTimeout(600); t = await tips(); need(t.length === 1 && t[0].in, 'one at the foot of the screen is turned upward and stays on it');
  await page.mouse.move(800, 600, { steps: 4 }); await page.waitForTimeout(400);

  // the map: its own note goes when the pointer leaves the star, and the chart from far off
  const note = () => page.evaluate(() => [...document.querySelectorAll('.map-tip')].filter(e => e.style.display !== 'none' && e.innerText.trim().length > 0).length);
  const star = await page.evaluate(() => { const l = document.querySelector('.sys-label.mine'); if (!l) return null; const r = l.getBoundingClientRect(); return [r.left + r.width / 2, r.top - 40]; });
  if (star) { await page.mouse.move(star[0], star[1], { steps: 5 }); await page.waitForTimeout(500); const shown = await note(); await page.mouse.move(1450, 420, { steps: 6 }); await page.waitForTimeout(500); need(shown >= 0 && (await note()) === 0, 'the map\'s note on a star goes when the pointer moves onto the list'); }
  await page.screenshot({ path: path.join(out, 'map-near.png') });
  await page.click('.map-tools button[title*="whole"], .map-tools button:has-text("▣")'); await page.waitForTimeout(3500); await page.mouse.move(820, 130); await page.waitForTimeout(300);
  await page.screenshot({ path: path.join(out, 'map-far.png') });
  await page.evaluate(() => praetoriaSpace.select('anvils_rest')); await page.waitForTimeout(1200); await page.click('button:has-text("Go into the system")'); await page.waitForTimeout(3500); await page.mouse.move(900, 900);
  await page.screenshot({ path: path.join(out, 'map-system.png') });
  need(errors.length === 0, 'the page logged no errors' + (errors.length ? ': ' + errors.slice(0, 3).join(' | ') : ''));
  await browser.close(); console.log(fails.length ? `\n${fails.length} failed` : '\nall passed'); process.exit(fails.length ? 1 : 0);
})().catch(e => { console.error(e); process.exit(1); });
