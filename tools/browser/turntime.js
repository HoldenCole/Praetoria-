// Times End Turn in the browser: how long the page takes to reach the next turn, early and late in a game.
//
//   node turntime.js ["Start title"] [turns]
//
// Needs the build served locally (see README.md here).
const { chromium } = require('playwright');
const title = process.argv[2] || 'The Old Lion'; const turns = parseInt(process.argv[3] || '30', 10);
const base = process.env.PRAETORIA_URL || 'http://localhost:8112/';
(async () => {
  const browser = await chromium.launch({ args: ['--use-gl=angle', '--use-angle=swiftshader', '--ignore-gpu-blocklist'] });
  const page = await browser.newPage({ viewport: { width: 1440, height: 900 } }); page.setDefaultTimeout(120000);
  const errors = []; page.on('pageerror', e => errors.push(e.message.slice(0, 200)));
  const t0 = Date.now(); await page.goto(base, { waitUntil: 'networkidle' }); await page.waitForSelector('.title-screen', { timeout: 120000 }); console.log('to the title screen: ' + (Date.now() - t0) + ' ms');
  await page.evaluate(() => { try { localStorage.setItem('praetoria.primer.seen', '1'); } catch (e) {} });
  const t1 = Date.now(); await page.click('.scenario:has-text("' + title + '")'); await page.click('button.begin'); await page.waitForSelector('.gs-shell', { timeout: 120000 }); console.log('to the first turn: ' + (Date.now() - t1) + ' ms');
  const closeWindows = async () => { for (let i = 0; i < 10; i++) { const had = await page.evaluate(() => { const c = document.querySelector('.win-close') || document.querySelector('.overlay .btn.primary') || [...document.querySelectorAll('.overlay button')].find(b => !b.disabled); if (c) c.click(); return !!c; }); if (!had) break; await page.waitForTimeout(150); } };
  const turnNo = () => page.evaluate(() => { const m = document.body.innerText.match(/Turn (\d+)/); return m ? parseInt(m[1], 10) : -1; });
  const times = [];
  for (let i = 0; i < turns; i++) {
    await closeWindows(); const before = await turnNo(); const t = Date.now();
    await page.evaluate(() => { const b = document.querySelector('button.endturn'); if (b) b.click(); });
    let ok = false; for (let k = 0; k < 600; k++) { await page.waitForTimeout(50); await closeWindows(); if (await turnNo() > before) { ok = true; break; } }
    if (!ok) { console.log('stalled at turn ' + before); break; } times.push(Date.now() - t);
  }
  const avg = a => a.length ? Math.round(a.reduce((x, y) => x + y, 0) / a.length) : 0;
  console.log('turns played: ' + times.length + ' · first five ' + avg(times.slice(0, 5)) + ' ms each · last five ' + avg(times.slice(-5)) + ' ms each · slowest ' + Math.max(...times) + ' ms');
  console.log('errors: ' + errors.length + (errors.length ? ' · ' + errors[0] : '')); await browser.close();
})();
