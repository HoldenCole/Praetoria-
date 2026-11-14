// Drives "Breaking away" on the Senate screen's standing view: reads what the panel says of each region the house holds
// (what it controls, who would follow, whether the Reach would fight), picks a form of government, declares if it can,
// plays some years and reads the panel and the chronicle again.
//
//   node breakaway.js ["Start title"] [years]
//
// Needs the build served locally (see README.md here). Screenshots go to PRAETORIA_SHOTS (default ./shots).
const { chromium } = require('playwright');
const title = process.argv[2] || 'The Old Lion'; const years = parseInt(process.argv[3] || '4', 10);
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
  const press = text => page.evaluate(t => { const b = [...document.querySelectorAll('.panel.breakaway button')].find(e => !e.disabled && e.innerText.trim().startsWith(t)); if (b) b.click(); return b ? b.innerText.trim() : ''; }, text);
  const panel = () => page.evaluate(() => { const el = document.querySelector('.panel.breakaway'); return el ? el.innerText.replace(/\s+/g, ' ').slice(0, 1400) : 'NO PANEL'; });
  const rows = () => page.evaluate(() => [...document.querySelectorAll('.panel.breakaway .rung-row')].map(e => e.innerText.replace(/\s+/g, ' ').slice(0, 420)).join('\n   '));
  const shot = async name => { const box = await page.evaluate(() => { const el = document.querySelector('.panel.breakaway'); if (!el) return null; el.scrollIntoView({ block: 'start' }); const r = el.getBoundingClientRect(); return { x: Math.max(0, r.x), y: Math.max(0, r.y), width: r.width, height: Math.min(r.height, 1100) }; }); if (box) await page.screenshot({ path: shots + '/' + name + '.png', clip: box }); };

  await closeWindows(); await standing();
  const text = await panel();
  console.log('panel: ' + (/NaN|undefined|Exception|@\(/.test(text) ? 'SUSPICIOUS TEXT' : text === 'NO PANEL' ? text : 'ok'));
  console.log('   ' + await rows());
  console.log('a quiet word: ' + (await press('A quiet word') || 'not open')); await page.waitForTimeout(400);
  console.log('   ' + await rows());
  console.log('as a kingdom: ' + (await press('A kingdom') || 'no such button')); await page.waitForTimeout(300);
  await shot('breakaway-begin');
  const declared = await press('Declare independence');
  if (declared) { await page.waitForTimeout(300); console.log('declared: ' + (await press('Yes: declare') || 'not confirmed')); await page.waitForTimeout(600); await closeWindows(); await standing(); console.log('   ' + await panel()); }
  else console.log('declare: not open to this house (see the reasons above)');
  for (let y = 0; y < years; y++) {
    await endTurn();
    const alerts = await page.evaluate(() => [...document.querySelectorAll('.ol-row')].map(e => e.innerText.replace(/\s+/g, ' ')).filter(t => /independence|kneel|state/i.test(t)).slice(0, 2).join(' | '));
    if (alerts) console.log('  turn ' + await turnNo() + ' alerts: ' + alerts.slice(0, 200));
  }
  await standing();
  console.log('after ' + years + ' years: ' + await panel());
  await shot('breakaway-after');
  await rail('Galaxy'); await page.waitForTimeout(2500);
  console.log('map: ' + await page.evaluate(() => [...document.querySelectorAll('.region-label .rs.state, .region-row.free')].map(e => e.innerText.replace(/\s+/g, ' ').slice(0, 120)).slice(0, 3).join(' | ')));
  await page.screenshot({ path: shots + '/breakaway-map.png' });
  await rail('Chronicle'); await page.waitForTimeout(600);
  console.log('chronicle: ' + await page.evaluate(() => (document.body.innerText || '').split('\n').map(t => t.trim()).filter(t => /INDEPENDENT|does not answer with a fleet|will not have it|makes peace with|is no more|War of the Reach/i.test(t)).slice(0, 6).map(t => t.slice(0, 240)).join('\n   ')));
  console.log('errors: ' + errors.length); errors.slice(0, 5).forEach(e => console.log('   ' + e));
  await browser.close();
})().catch(e => { console.error('FAILED ' + e.message.split('\n')[0]); process.exit(1); });
