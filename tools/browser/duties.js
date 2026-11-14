// Drives the duties panel on the Senate's standing view: reads the year a senator has to spend and any matter before the
// head's bench or the charge of an office of the Reach the head holds, decides each, plays some years doing so, and reads the
// panel and the chronicle again.
//
//   node duties.js ["Start title"] [years]
//
// Needs the build served locally (see README.md here). Screenshots go to PRAETORIA_SHOTS (default ./shots).
const { chromium } = require('playwright');
const title = process.argv[2] || 'The Circling Heir'; const years = parseInt(process.argv[3] || '4', 10);
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
  const block = sel => page.evaluate(s => { const el = document.querySelector('.panel.duties ' + s); return el ? el.innerText.replace(/\s+/g, ' ').slice(0, 800) : ''; }, sel);
  const press = (sel, n) => page.evaluate(([s, i]) => { const bs = [...document.querySelectorAll('.panel.duties ' + s + ' button')].filter(e => !e.disabled); const b = bs[Math.min(i, bs.length - 1)]; if (b) b.click(); return b ? b.innerText.trim() : ''; }, [sel, n]);
  const shot = async name => { const box = await page.evaluate(() => { const el = document.querySelector('.panel.duties'); if (!el) return null; el.scrollIntoView({ block: 'start' }); const r = el.getBoundingClientRect(); return { x: Math.max(0, r.x), y: Math.max(0, r.y), width: r.width, height: Math.min(r.height, 1100) }; }); if (box) await page.screenshot({ path: shots + '/' + name + '.png', clip: box }); };

  await closeWindows(); await standing();
  console.log('playing: ' + await page.evaluate(() => (document.querySelector('.topbar') || document.body).innerText.replace(/\s+/g, ' ').slice(0, 60)));
  let text = await page.evaluate(() => (document.querySelector('.panel.duties') || {}).innerText || 'NO PANEL');
  console.log('panel at the start: ' + (/NaN|undefined|Exception|@\(/.test(text) ? 'SUSPICIOUS TEXT' : text === 'NO PANEL' ? text : 'ok') + ' · ' + text.replace(/\s+/g, ' ').slice(0, 260));
  for (let y = 0; y < years; y++) {
    await endTurn(); await standing();
    if (!(await page.evaluate(() => !!document.querySelector('.panel.duties')))) { await closeWindows(); await page.waitForTimeout(400); await standing(); }   // something was still on top of the screen
    if (y === 0) console.log('panel at turn ' + await turnNo() + ': ' + (await page.evaluate(() => (document.querySelector('.panel.duties') || {}).innerText || 'NO PANEL')).replace(/\s+/g, ' ').slice(0, 500));
    const seat = await block('.duty-seat'); const bench = await block('.duty-bench'); const charge = await block('.duty-charge');
    if (charge) { console.log('charge: ' + charge); await shot('duties-charge'); console.log('  charge → ' + (await press('.duty-charge', y) || 'NOTHING COULD BE PRESSED')); await page.waitForTimeout(300); }
    if (y === 0) { console.log('seat: ' + (seat || (charge ? 'none: the charge is the year' : 'NOTHING WAITING'))); console.log('bench: ' + (bench || 'nothing before the bench')); await shot('duties-waiting'); }
    const s = seat ? await press('.duty-seat', y) : ''; const b = bench ? await press('.duty-bench', y) : ''; await page.waitForTimeout(350);
    console.log('  turn ' + await turnNo() + ': seat → ' + (s || 'none') + (bench ? ' · bench → ' + (b || 'NOTHING COULD BE PRESSED') : '') + ' · left waiting: ' + ((await block('.duty-seat')) ? 'seat ' : '') + ((await block('.duty-bench')) ? 'bench' : '') );
    const alerts = await page.evaluate(() => [...document.querySelectorAll('.ol-row')].map(e => e.innerText.replace(/\s+/g, ' ')).filter(t => /bench|year in the Senate|charge of your office/i.test(t)).join(' | '));
    if (alerts) console.log('    STILL ALERTING: ' + alerts.slice(0, 160));
  }
  await shot('duties-after');
  text = await page.evaluate(() => (document.querySelector('.panel.duties') || {}).innerText || 'NO PANEL');
  console.log('panel after: ' + text.replace(/\s+/g, ' ').slice(0, 420));
  await rail('Chronicle'); await page.waitForTimeout(600);
  console.log('chronicle: ' + await page.evaluate(() => (document.body.innerText || '').split('\n').map(t => t.trim()).filter(t => /reads the year's accounts|speaks for|at the service of|holding .* together|finds for|overturns|follows the money/i.test(t)).slice(0, 8).map(t => t.slice(0, 220)).join('\n   ')));
  console.log('errors: ' + errors.length); errors.slice(0, 5).forEach(e => console.log('   ' + e));
  await browser.close();
})().catch(e => { console.error('FAILED ' + e.message.split('\n')[0]); process.exit(1); });
