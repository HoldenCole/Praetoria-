// Saves a game in the browser, reloads the page, and continues it: the turn, the treasury and the chronicle must come back.
//
//   node saveload.js ["Start title"]
//
// Needs the build served locally (see README.md here).
const { chromium } = require('playwright');
const title = process.argv[2] || 'The Old Lion';
const base = process.env.PRAETORIA_URL || 'http://localhost:8112/';
(async () => {
  const browser = await chromium.launch({ args: ['--use-gl=angle', '--use-angle=swiftshader', '--ignore-gpu-blocklist'] });
  const page = await browser.newPage({ viewport: { width: 1440, height: 900 } }); page.setDefaultTimeout(120000);
  const errors = []; page.on('pageerror', e => errors.push(e.message.slice(0, 200))); page.on('console', m => { if (m.type() === 'error') errors.push(m.text().slice(0, 200)); });
  await page.goto(base, { waitUntil: 'networkidle' }); await page.waitForSelector('.title-screen');
  await page.evaluate(() => { try { localStorage.setItem('praetoria.primer.seen', '1'); } catch (e) {} });
  await page.click('.scenario:has-text("' + title + '")'); await page.click('button.begin'); await page.waitForSelector('.gs-shell');
  const closeWindows = async () => { for (let i = 0; i < 10; i++) { const had = await page.evaluate(() => { const c = document.querySelector('.win-close') || document.querySelector('.overlay .btn.primary') || [...document.querySelectorAll('.overlay button')].find(b => !b.disabled); if (c) c.click(); return !!c; }); if (!had) break; await page.waitForTimeout(150); } };
  const turnNo = () => page.evaluate(() => { const m = document.body.innerText.match(/Turn (\d+)/); return m ? parseInt(m[1], 10) : -1; });
  const top = () => page.evaluate(() => { const el = document.querySelector('.topbar') || document.querySelector('header'); return el ? el.innerText.replace(/\s+/g, ' ').slice(0, 160) : 'NO TOP BAR'; });
  for (let i = 0; i < 4; i++) { await closeWindows(); const before = await turnNo(); await page.evaluate(() => { const b = document.querySelector('button.endturn'); if (b) b.click(); }); for (let k = 0; k < 400; k++) { await page.waitForTimeout(50); await closeWindows(); if (await turnNo() > before) break; } }
  await closeWindows(); const was = { turn: await turnNo(), top: await top() };
  const saved = await page.evaluate(() => { const b = [...document.querySelectorAll('button')].find(e => e.innerText.trim() === 'Save'); if (b) b.click(); return !!b; }); await page.waitForTimeout(800);
  console.log('before: turn ' + was.turn + ' · ' + was.top); console.log('save pressed: ' + saved);
  await page.reload({ waitUntil: 'networkidle' }); await page.waitForSelector('.title-screen');
  const offer = await page.evaluate(() => [...document.querySelectorAll('.title-screen button')].map(b => b.innerText.trim().replace(/\s+/g, ' ')).filter(t => /continue|resume|load/i.test(t)).slice(0, 3).join(' | '));
  console.log('the title screen offers: ' + (offer || 'NOTHING TO CONTINUE'));
  const went = await page.evaluate(() => { const b = [...document.querySelectorAll('.title-screen button')].find(e => /continue|resume|load/i.test(e.innerText)); if (b) b.click(); return !!b; });
  if (went) { await page.waitForSelector('.gs-shell', { timeout: 60000 }).catch(() => {}); await page.waitForTimeout(600); await closeWindows(); const now = { turn: await turnNo(), top: await top() };
    console.log('after: turn ' + now.turn + ' · ' + now.top); console.log(now.turn === was.turn && now.top === was.top ? 'SAME: the game came back as it was left' : 'DIFFERENT: what came back is not what was saved'); }
  console.log('errors: ' + errors.length + (errors.length ? ' · ' + errors[0] : '')); await browser.close();
})();
