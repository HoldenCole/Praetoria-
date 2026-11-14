// Drives the ladder of offices: opens the Senate's standing view, reads the ladder, puts the head's name down for the first rung
// they may stand for, calls in what is owed, plays past the next election, and reads who sits and what was written.
//
//   node ladder.js ["Start title"] [years]
//
// Needs the build served locally (see README.md here). Screenshots go to PRAETORIA_SHOTS (default ./shots).
const { chromium } = require('playwright');
const title = process.argv[2] || 'The Circling Heir'; const years = parseInt(process.argv[3] || '7', 10);
const base = process.env.PRAETORIA_URL || 'http://localhost:8112/'; const shots = process.env.PRAETORIA_SHOTS || './shots';
require('fs').mkdirSync(shots, { recursive: true });
(async () => {
  const browser = await chromium.launch({ args: ['--use-gl=angle', '--use-angle=swiftshader', '--ignore-gpu-blocklist'] });
  const page = await browser.newPage({ viewport: { width: 1600, height: 1900 } }); page.setDefaultTimeout(60000);
  const errors = []; page.on('console', m => { if (m.type() === 'error') errors.push(m.text().slice(0, 300)); }); page.on('pageerror', e => errors.push('PAGEERROR ' + e.message.slice(0, 300)));
  await page.goto(base, { waitUntil: 'networkidle' }); await page.waitForSelector('.title-screen', { timeout: 90000 });
  await page.evaluate(() => { try { localStorage.setItem('praetoria.primer.seen', '1'); } catch (e) {} });
  await page.click('.scenario:has-text("' + title + '")'); await page.click('button.begin'); await page.waitForSelector('.gs-shell', { timeout: 90000 });
  const rail = name => page.evaluate(n => { const el = [...document.querySelectorAll('.rail-btn')].find(e => e.innerText.trim().toLowerCase() === n.toLowerCase()); if (el) el.click(); return !!el; }, name);
  const closeWindows = async () => { for (let i = 0; i < 8; i++) { const had = await page.evaluate(() => { const c = document.querySelector('.win-close') || document.querySelector('.overlay .btn.primary'); if (c) c.click(); return !!c; }); if (!had) break; await page.waitForTimeout(150); } };
  const turnNo = () => page.evaluate(() => { const m = document.body.innerText.match(/Turn (\d+)/); return m ? parseInt(m[1], 10) : -1; });
  const endTurn = async () => { await closeWindows(); const before = await turnNo(); await page.evaluate(() => { const b = [...document.querySelectorAll('button')].find(e => e.innerText.includes('End Turn')); if (b) b.click(); }); for (let i = 0; i < 800; i++) { if (await turnNo().catch(() => -1) > before) break; await page.waitForTimeout(50); } await closeWindows(); };
  const standing = async () => { await rail('Senate'); await page.waitForTimeout(400); await page.evaluate(() => { const b = [...document.querySelectorAll('button')].find(e => e.innerText.includes('Your standing')); if (b) b.click(); }); await page.waitForTimeout(500); };
  const press = text => page.evaluate(t => { const b = [...document.querySelectorAll('.panel.cursus button')].find(e => !e.disabled && e.innerText.trim().startsWith(t)); if (b) b.click(); return b ? b.innerText.trim() : ''; }, text);
  const rungs = () => page.evaluate(() => [...document.querySelectorAll('.panel.cursus .rung-row')].map(e => e.innerText.replace(/\s+/g, ' ').slice(0, 330)).join('\n   '));
  const head = () => page.evaluate(() => { const el = document.querySelector('.panel.cursus'); return el ? el.querySelector('h3').innerText.replace(/\s+/g, ' ') + ' · ' + ((el.querySelector('.muted.small') || {}).innerText || '').replace(/\s+/g, ' ').slice(-70) : 'NO PANEL'; });
  const shot = async name => { const box = await page.evaluate(() => { const el = document.querySelector('.panel.cursus'); if (!el) return null; el.scrollIntoView({ block: 'start' }); const r = el.getBoundingClientRect(); return { x: Math.max(0, r.x), y: Math.max(0, r.y), width: r.width, height: Math.min(r.height, 1100) }; }); if (box) await page.screenshot({ path: shots + '/' + name + '.png', clip: box }); };

  await closeWindows(); await standing();
  console.log('playing: ' + await page.evaluate(() => (document.querySelector('.topbar') || document.body).innerText.replace(/\s+/g, ' ').slice(0, 90)));
  const text = await page.evaluate(() => (document.querySelector('.panel.cursus') || {}).innerText || 'NO PANEL');
  console.log('panel: ' + (/NaN|undefined|Exception|@\(/.test(text) ? 'SUSPICIOUS TEXT' : text === 'NO PANEL' ? text : 'ok') + ' · ' + await head());
  console.log('   ' + await rungs());
  const stood = await press('Stand for'); console.log('stand: ' + (stood || 'NO RUNG OPEN TO THE HEAD')); await page.waitForTimeout(400);
  if (stood) { console.log('call in what is owed: ' + (await press('Call in what is owed') || 'refused')); await page.waitForTimeout(400); }
  await shot('ladder-standing');
  for (let y = 0; y < years; y++) {
    await endTurn();
    const alerts = await page.evaluate(() => [...document.querySelectorAll('.ol-row')].map(e => e.innerText.replace(/\s+/g, ' ')).filter(t => /stand|election|games|magistr/i.test(t)).slice(0, 2).join(' | '));
    if (alerts) console.log('  turn ' + await turnNo() + ' alerts: ' + alerts.slice(0, 200));
  }
  await standing();
  console.log('after ' + years + ' years: ' + await head());
  console.log('   ' + await rungs());
  const games = await press('Give games'); if (games) { console.log('games: ' + games); await page.waitForTimeout(400); }
  await shot('ladder-after');
  await rail('Chronicle'); await page.waitForTimeout(600);
  console.log('chronicle: ' + await page.evaluate(() => (document.body.innerText || document.body.textContent || '').split('\n').map(t => t.trim()).filter(t => /quaestor|aedile|praetor|magistrate/i.test(t)).slice(0, 6).map(t => t.slice(0, 260)).join('\n   ')));
  await rail('You'); await page.waitForTimeout(600);
  console.log('written of you: ' + await page.evaluate(() => [...document.querySelectorAll('.panel.life .deed')].map(e => e.innerText.replace(/\s+/g, ' ')).filter(t => /quaestor|aedile|praetor|games|stood/i.test(t)).join(' | ')));
  console.log('errors: ' + errors.length); errors.slice(0, 5).forEach(e => console.log('   ' + e));
  await browser.close();
})().catch(e => { console.error('FAILED ' + e.message.split('\n')[0]); process.exit(1); });
