// Drives the Caesar road's panel on the Military screen: reads the general's sway and whose the crews are, pays a donative,
// tries the triumph and the march (and reports why not), plays some years, and reads the panel and the chronicle again.
//
//   node caesar.js ["Start title"] [years]
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
  const press = text => page.evaluate(t => { const b = [...document.querySelectorAll('.panel.caesar button')].find(e => !e.disabled && e.innerText.trim().startsWith(t)); if (b) b.click(); return b ? b.innerText.trim() : ''; }, text);
  const why = text => page.evaluate(t => { const b = [...document.querySelectorAll('.panel.caesar button')].find(e => e.innerText.trim().startsWith(t)); return b ? (b.disabled ? 'refused: ' + (b.getAttribute('title') || '') : 'open') : 'no such button'; }, text);
  const panel = () => page.evaluate(() => { const el = document.querySelector('.panel.caesar'); if (!el) return 'NO PANEL'; return el.innerText.replace(/\s+/g, ' ').slice(0, 900); });
  const crews = () => page.evaluate(() => [...document.querySelectorAll('.panel.caesar .crew-row')].map(e => e.innerText.replace(/\s+/g, ' ')).join(' | '));
  const shot = async name => { const box = await page.evaluate(() => { const el = document.querySelector('.panel.caesar'); if (!el) return null; el.scrollIntoView({ block: 'start' }); const r = el.getBoundingClientRect(); return { x: Math.max(0, r.x), y: Math.max(0, r.y), width: r.width, height: Math.min(r.height, 1200) }; }); if (box) await page.screenshot({ path: shots + '/' + name + '.png', clip: box }); };

  await closeWindows(); await rail('Military'); await page.waitForTimeout(700);
  const text = await panel();
  console.log('panel: ' + (/NaN|undefined|Exception|@\(/.test(text) ? 'SUSPICIOUS TEXT' : text === 'NO PANEL' ? text : 'ok'));
  console.log('   ' + text);
  console.log('crews: ' + await crews());
  console.log('triumph: ' + await why('Ask the Senate for a triumph'));
  console.log('cross the line: ' + await why('Cross the line'));
  console.log('donative: ' + (await press('Donative') || 'refused or none')); await page.waitForTimeout(400);
  console.log('crews: ' + await crews());
  await shot('caesar-begin');
  for (let y = 0; y < years; y++) {
    await endTurn();
    const alerts = await page.evaluate(() => [...document.querySelectorAll('.ol-row')].map(e => e.innerText.replace(/\s+/g, ' ')).filter(t => /triumph|Senate has ordered|crossed the line|marches on/i.test(t)).slice(0, 2).join(' | '));
    if (alerts) console.log('  turn ' + await turnNo() + ' alerts: ' + alerts.slice(0, 220));
    await rail('Military'); await page.waitForTimeout(250);
    const asked = await press('Ask the Senate for a triumph'); if (asked) console.log('  turn ' + await turnNo() + ': asked the Senate for a triumph');
    const home = await press('Come home') || await press('Order them home'); if (home) console.log('  turn ' + await turnNo() + ': ' + home);
  }
  await rail('Military'); await page.waitForTimeout(600);
  console.log('after ' + years + ' years: ' + await panel());
  console.log('crews: ' + await crews());
  await shot('caesar-after');
  await rail('Senate'); await page.waitForTimeout(600);
  console.log('senate: ' + await page.evaluate(() => { const t = document.body.innerText || ''; const shown = t.includes('Censure'); return shown ? ['Triumph', 'Recall'].map(k => k + (t.includes(k) ? ' offered' : ' MISSING')).join(', ') : 'the list of motions is not shown to this house on this view'; }));
  await rail('Chronicle'); await page.waitForTimeout(600);
  console.log('chronicle: ' + await page.evaluate(() => (document.body.innerText || '').split('\n').map(t => t.trim()).filter(t => /triumph|lay down the command|CROSSES|donative|crews of/i.test(t)).slice(0, 8).map(t => t.slice(0, 220)).join('\n   ')));
  console.log('errors: ' + errors.length); errors.slice(0, 5).forEach(e => console.log('   ' + e));
  await browser.close();
})().catch(e => { console.error('FAILED ' + e.message.split('\n')[0]); process.exit(1); });
