// Drives the household panel on the House screen: hands the steward's work to someone, looks for a person to take in, takes
// them in if anyone is willing, and plays some years to see what the steward reports.
//
//   node household.js ["Start title"] [years]
//
// Needs the build served locally (see README.md here). Screenshots go to PRAETORIA_SHOTS (default ./shots).
const { chromium } = require('playwright');
const title = process.argv[2] || 'The Old Lion'; const years = parseInt(process.argv[3] || '4', 10);
const base = process.env.PRAETORIA_URL || 'http://localhost:8112/'; const shots = process.env.PRAETORIA_SHOTS || './shots';
require('fs').mkdirSync(shots, { recursive: true });
(async () => {
  const browser = await chromium.launch({ args: ['--use-gl=angle', '--use-angle=swiftshader', '--ignore-gpu-blocklist'] });
  const page = await browser.newPage({ viewport: { width: 1600, height: 1500 } }); page.setDefaultTimeout(60000);
  const errors = []; page.on('console', m => { if (m.type() === 'error') errors.push(m.text().slice(0, 300)); }); page.on('pageerror', e => errors.push('PAGEERROR ' + e.message.slice(0, 300)));
  await page.goto(base, { waitUntil: 'networkidle' }); await page.waitForSelector('.title-screen', { timeout: 90000 });
  await page.evaluate(() => { try { localStorage.setItem('praetoria.primer.seen', '1'); } catch (e) {} });
  await page.click('.scenario:has-text("' + title + '")'); await page.click('button.begin'); await page.waitForSelector('.gs-shell', { timeout: 90000 });
  const rail = name => page.evaluate(n => { const el = [...document.querySelectorAll('.rail-btn')].find(e => e.innerText.trim().toLowerCase() === n.toLowerCase()) || [...document.querySelectorAll('.rail-btn')].find(e => e.innerText.toLowerCase().includes(n.toLowerCase())); if (el) el.click(); return !!el; }, name);
  const closeWindows = async () => { for (let i = 0; i < 8; i++) { const had = await page.evaluate(() => { const c = document.querySelector('.win-close') || document.querySelector('.overlay .btn.primary'); if (c) c.click(); return !!c; }); if (!had) break; await page.waitForTimeout(150); } };
  const turnNo = () => page.evaluate(() => { const m = document.body.innerText.match(/Turn (\d+)/); return m ? parseInt(m[1], 10) : -1; });
  const endTurn = async () => { await closeWindows(); const before = await turnNo(); await page.evaluate(() => { const b = [...document.querySelectorAll('button')].find(e => e.innerText.includes('End Turn')); if (b) b.click(); }); for (let i = 0; i < 800; i++) { if (await turnNo().catch(() => -1) > before) break; await page.waitForTimeout(50); } await closeWindows(); };
  const panel = () => page.evaluate(() => (document.querySelector('.panel.household') || {}).innerText || 'NO PANEL');
  const press = (sel, text) => page.evaluate(([s, t]) => { const b = [...document.querySelectorAll(s)].find(e => !e.disabled && e.innerText.trim().startsWith(t)); if (b) b.click(); return b ? b.innerText.replace(/\s+/g, ' ').slice(0, 60) : ''; }, [sel, text]);
  await closeWindows(); await rail('House'); await page.waitForTimeout(700);
  let text = await panel(); console.log('panel: ' + (/NaN|undefined|Exception|@\(/.test(text) ? 'SUSPICIOUS TEXT' : 'ok') + ' · ' + text.split('\n')[0]);
  console.log('steward given to: ' + await page.evaluate(() => { const d = [...document.querySelectorAll('.panel.household .duty')][0]; const b = d && [...d.querySelectorAll('button.opt.inline')].find(e => !e.disabled && /skill/.test(e.innerText)); if (b) b.click(); return b ? b.innerText.replace(/\s+/g, ' ') : 'nobody'; }));
  await page.waitForTimeout(300);
  console.log('marshal given to: ' + await page.evaluate(() => { const d = [...document.querySelectorAll('.panel.household .duty')][1]; const b = d && [...d.querySelectorAll('button.opt.inline')].find(e => !e.disabled && /skill/.test(e.innerText)); if (b) b.click(); return b ? b.innerText.replace(/\s+/g, ' ') : 'nobody'; }));
  await page.waitForTimeout(300);
  await press('.panel.household button', 'Look for someone'); await page.waitForTimeout(400);
  console.log('would come: ' + await page.evaluate(() => [...document.querySelectorAll('.panel.household table.prospects tbody tr')].map(r => r.innerText.replace(/\s+/g, ' ').slice(0, 70)).join(' | ')));
  console.log('took in: ' + (await press('.panel.household table.prospects button', 'Take in') || 'nobody willing'));
  await page.waitForTimeout(400); await page.screenshot({ path: shots + '/household-begin.png' });
  for (let y = 0; y < years; y++) {
    await endTurn(); await rail('House'); await page.waitForTimeout(500);
    text = await panel(); if (/NaN|undefined|Exception|@\(/.test(text)) console.log('SUSPICIOUS TEXT in year ' + y);
    const reports = await page.evaluate(() => [...document.querySelectorAll('.panel.household .duty .suit-who .small')].map(e => e.innerText.replace(/\s+/g, ' ')).join(' || '));
    console.log('year ' + (y + 1) + ' (turn ' + await turnNo() + '): ' + reports.slice(0, 260));
  }
  await page.screenshot({ path: shots + '/household-end.png' });
  console.log('taken in now: ' + await page.evaluate(() => [...document.querySelectorAll('.panel.household .hh-cols .suit:not(.incoming) .suit-who b')].map(e => e.innerText).join(', ')));
  console.log('errors: ' + errors.length); errors.slice(0, 5).forEach(e => console.log('   ' + e));
  await browser.close();
})().catch(e => { console.error('FAILED ' + e.message.split('\n')[0]); process.exit(1); });
