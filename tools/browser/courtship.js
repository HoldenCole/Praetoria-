// Drives courtship and marriage on the Dynasty screen: begins a suit, presses it for some years, proposes when the family would
// agree, and reports what the panel shows at each step.
//
//   node courtship.js ["Start title"] [years]
//
// Needs the build served locally (see README.md here). Screenshots go to PRAETORIA_SHOTS (default ./shots).
const { chromium } = require('playwright');
const title = process.argv[2] || 'The New Man'; const years = parseInt(process.argv[3] || '6', 10);
const base = process.env.PRAETORIA_URL || 'http://localhost:8112/'; const shots = process.env.PRAETORIA_SHOTS || './shots';
require('fs').mkdirSync(shots, { recursive: true });
(async () => {
  const browser = await chromium.launch({ args: ['--use-gl=angle', '--use-angle=swiftshader', '--ignore-gpu-blocklist'] });
  const page = await browser.newPage({ viewport: { width: 1600, height: 1300 } }); page.setDefaultTimeout(60000);
  const errors = []; page.on('console', m => { if (m.type() === 'error') errors.push(m.text().slice(0, 300)); }); page.on('pageerror', e => errors.push('PAGEERROR ' + e.message.slice(0, 300)));
  await page.goto(base, { waitUntil: 'networkidle' }); await page.waitForSelector('.title-screen', { timeout: 90000 });
  await page.evaluate(() => { try { localStorage.setItem('praetoria.primer.seen', '1'); } catch (e) {} });
  await page.click('.scenario:has-text("' + title + '")'); await page.click('button.begin'); await page.waitForSelector('.gs-shell', { timeout: 90000 });
  const rail = name => page.evaluate(n => { const el = [...document.querySelectorAll('.rail-btn')].find(e => e.innerText.toLowerCase().includes(n.toLowerCase())); if (el) el.click(); return !!el; }, name);
  const closeWindows = async () => { for (let i = 0; i < 8; i++) { const had = await page.evaluate(() => { const c = document.querySelector('.win-close') || document.querySelector('.overlay .btn.primary'); if (c) c.click(); return !!c; }); if (!had) break; await page.waitForTimeout(150); } };
  const turnNo = () => page.evaluate(() => { const m = document.body.innerText.match(/Turn (\d+)/); return m ? parseInt(m[1], 10) : -1; });
  const endTurn = async () => { await closeWindows(); const before = await turnNo(); await page.evaluate(() => { const b = [...document.querySelectorAll('button')].find(e => e.innerText.includes('End Turn')); if (b) b.click(); }); for (let i = 0; i < 800; i++) { if (await turnNo().catch(() => -1) > before) break; await page.waitForTimeout(50); } await closeWindows(); };
  const press = text => page.evaluate(t => { const b = [...document.querySelectorAll('.panel.courtship button')].find(e => !e.disabled && e.innerText.trim().startsWith(t)); if (b) b.click(); return !!b; }, text);
  const panel = () => page.evaluate(() => (document.querySelector('.panel.courtship') || {}).innerText || 'NO PANEL');
  const gauges = () => page.evaluate(() => [...document.querySelectorAll('.panel.courtship .suit .gauge')].map(g => g.innerText.replace(/\s+/g, ' ').trim()).join(' | '));
  await closeWindows(); await rail('Dynasty'); await page.waitForTimeout(700);
  let text = await panel(); console.log('panel: ' + (/NaN|undefined|Exception|@\(/.test(text) ? 'SUSPICIOUS TEXT' : 'ok') + ' · ' + text.split('\n')[0]);
  console.log('prospects listed: ' + await page.evaluate(() => document.querySelectorAll('table.prospects tbody tr').length));
  await page.screenshot({ path: shots + '/courtship-begin.png' });
  console.log('pay court: ' + await press('Pay court')); await page.waitForTimeout(400); console.log('  ' + await gauges());
  for (let y = 0; y < years; y++) {
    await rail('Dynasty'); await page.waitForTimeout(400);
    text = await panel(); if (/NaN|undefined|Exception|@\(/.test(text)) console.log('SUSPICIOUS TEXT in year ' + y);
    if (!/is courting|may be promised/.test(text)) { console.log('year ' + y + ': no suit in hand · ' + (text.match(/and .* of House.*|Nobody of the house is married\./) || [''])[0].slice(0, 90)); await rail('House'); await page.waitForTimeout(300); await endTurn(); continue; }
    const verdict = (text.match(/Asked now, on these terms: (.*)/) || [, ''])[1].slice(0, 80);
    if (/they will agree/.test(verdict)) { console.log('year ' + y + ': proposing · ' + await press('Propose the match')); await page.waitForTimeout(500); await page.screenshot({ path: shots + '/courtship-wed.png' }); console.log('  ' + (await panel()).split('\n').slice(0, 6).join(' / ').slice(0, 260)); await rail('House'); await page.waitForTimeout(300); await endTurn(); continue; }
    const did = (await press('Time together')) ? 'time' : (await press('Put the case')) ? 'family' : (await press('A gift')) ? 'gift' : 'nothing';
    await page.waitForTimeout(300); console.log('year ' + y + ': ' + did + ' · ' + await gauges() + ' · ' + verdict);
    if (y === 1) await page.screenshot({ path: shots + '/courtship-suit.png' });
    await rail('House'); await page.waitForTimeout(300); await endTurn();
  }
  await rail('Dynasty'); await page.waitForTimeout(600); await page.screenshot({ path: shots + '/courtship-end.png' });
  console.log('at the end: ' + (await panel()).split('\n').slice(0, 5).join(' / ').slice(0, 300));
  console.log('errors: ' + errors.length); errors.slice(0, 5).forEach(e => console.log('   ' + e));
  await browser.close();
})().catch(e => { console.error('FAILED ' + e.message.split('\n')[0]); process.exit(1); });
