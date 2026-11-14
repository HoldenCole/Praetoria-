// Drives "a life of one's own": reads the Life panel on the character sheet, takes a season away, lays the house down if the head
// may, plays some years, and reads what has been written of the new head and what was said of the dead.
//
//   node life.js ["Start title"] [years]
//
// Needs the build served locally (see README.md here). Screenshots go to PRAETORIA_SHOTS (default ./shots).
const { chromium } = require('playwright');
const title = process.argv[2] || 'The Old Lion'; const years = parseInt(process.argv[3] || '8', 10);
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
  const press = text => page.evaluate(t => { const b = [...document.querySelectorAll('.panel.life button')].find(e => !e.disabled && e.innerText.trim().startsWith(t)); if (b) b.click(); return !!b; }, text);
  const life = () => page.evaluate(() => { const el = document.querySelector('.panel.life'); if (!el) return 'NO PANEL'; const g = [...el.querySelectorAll('.gauge')].map(x => x.innerText.replace(/\s+/g, ' ')).join(' | '); const s = (el.querySelector('.muted.small') || {}).innerText || ''; return g + ' · ' + s.replace(/\s+/g, ' ').slice(0, 120); });
  const who = () => page.evaluate(() => (document.querySelector('.dh-title b') || {}).innerText || '');
  await closeWindows(); await rail('You'); await page.waitForTimeout(700);
  const text = await page.evaluate(() => (document.querySelector('.panel.life') || {}).innerText || 'NO PANEL');
  console.log('panel: ' + (/NaN|undefined|Exception|@\(/.test(text) ? 'SUSPICIOUS TEXT' : 'ok') + ' · ' + await who() + ' · ' + await life());
  console.log('a season away: ' + await press('A season away')); await page.waitForTimeout(300); console.log('  ' + await life());
  await page.screenshot({ path: shots + '/life-begin.png' });
  if (await press('Lay the house down')) { await page.waitForTimeout(300); console.log('laid the house down: ' + await press('Yes, lay it down')); await page.waitForTimeout(800); await closeWindows(); await rail('You'); await page.waitForTimeout(500); console.log('  now playing ' + await who() + ' · ' + await life()); }
  else console.log('may not lay the house down yet');
  for (let y = 0; y < years; y++) { await rail('House'); await page.waitForTimeout(200); await endTurn(); }
  await rail('You'); await page.waitForTimeout(600);
  console.log('after ' + years + ' years: ' + await who() + ' · ' + await life());
  console.log('written: ' + await page.evaluate(() => [...document.querySelectorAll('.panel.life .deed')].map(e => e.innerText.replace(/\s+/g, ' ')).join(' | ')));
  console.log('ties: ' + await page.evaluate(() => [...document.querySelectorAll('.panel.life .ac-fact')].map(e => e.innerText.replace(/\s+/g, ' ').slice(0, 110)).join(' || ')));
  const box = await page.evaluate(() => { const r = document.querySelector('.panel.life').getBoundingClientRect(); return { x: Math.max(0, r.x), y: Math.max(0, r.y), width: r.width, height: Math.min(r.height, 900) }; });
  await page.screenshot({ path: shots + '/life-panel.png', clip: box });
  await rail('Dynasty'); await page.waitForTimeout(600);
  console.log('health of the house: ' + await page.evaluate(() => [...document.querySelectorAll('.health-row')].map(e => e.innerText.replace(/\s+/g, ' ')).join(' | ')));
  console.log('said of the dead: ' + await page.evaluate(() => [...document.querySelectorAll('.funeral')].map(e => e.innerText.replace(/\s+/g, ' ')).join(' ## ')));
  const fb = await page.evaluate(() => { const els = [...document.querySelectorAll('.panel.funerals')]; if (!els.length) return null; const a = els[0].getBoundingClientRect(), b = els[els.length - 1].getBoundingClientRect(); return { x: Math.max(0, a.x), y: Math.max(0, a.y), width: a.width, height: Math.min(b.bottom - a.y, 900) }; });
  if (fb) await page.screenshot({ path: shots + '/life-funerals.png', clip: fb });
  console.log('errors: ' + errors.length); errors.slice(0, 5).forEach(e => console.log('   ' + e));
  await browser.close();
})().catch(e => { console.error('FAILED ' + e.message.split('\n')[0]); process.exit(1); });
