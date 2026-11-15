// Drives the courts on the Senate screen (docs/LADDER.md, docs/GAPS.md L2): reads what is on the record against former
// magistrates and who would hear a charge, plays years until there is something to bring, brings it, makes an offer to
// a sitting praetor, and reads what the chronicle says came of both.
//
//   node courts.js ["Start title"] [years]
//
// Needs the build served locally (see README.md here). Screenshots go to PRAETORIA_SHOTS (default ./shots).
const { chromium } = require('playwright');
const title = process.argv[2] || 'The Old Lion'; const years = parseInt(process.argv[3] || '14', 10);
const base = process.env.PRAETORIA_URL || 'http://localhost:8112/'; const shots = process.env.PRAETORIA_SHOTS || './shots';
require('fs').mkdirSync(shots, { recursive: true });
(async () => {
  const browser = await chromium.launch({ args: ['--use-gl=angle', '--use-angle=swiftshader', '--ignore-gpu-blocklist'] });
  const page = await browser.newPage({ viewport: { width: 1600, height: 2400 } }); page.setDefaultTimeout(60000);
  const errors = []; page.on('console', m => { if (m.type() === 'error') errors.push(m.text().slice(0, 300)); }); page.on('pageerror', e => errors.push('PAGEERROR ' + e.message.slice(0, 300)));
  await page.goto(base, { waitUntil: 'networkidle' }); await page.waitForSelector('.title-screen', { timeout: 90000 });
  await page.evaluate(() => { try { localStorage.setItem('praetoria.primer.seen', '1'); } catch (e) {} });
  await page.click('.scenario:has-text("' + title + '")'); await page.click('button.begin'); await page.waitForSelector('.gs-shell', { timeout: 90000 });
  const rail = name => page.evaluate(n => { const el = [...document.querySelectorAll('.rail-btn')].find(e => e.innerText.trim().toLowerCase() === n.toLowerCase()); if (el) el.click(); return !!el; }, name);
  const closeWindows = async () => { for (let i = 0; i < 8; i++) { const had = await page.evaluate(() => { const c = document.querySelector('.win-close') || document.querySelector('.overlay .btn.primary'); if (c) c.click(); return !!c; }); if (!had) break; await page.waitForTimeout(150); } };
  const turnNo = () => page.evaluate(() => { const m = document.body.innerText.match(/Turn (\d+)/); return m ? parseInt(m[1], 10) : -1; });
  const endTurn = async () => { await closeWindows(); const before = await turnNo(); await page.evaluate(() => { const b = [...document.querySelectorAll('button')].find(e => e.innerText.includes('End Turn')); if (b) b.click(); }); for (let i = 0; i < 800; i++) { if (await turnNo().catch(() => -1) > before) break; await page.waitForTimeout(50); } await closeWindows(); };
  // find the screen each panel lives on
  const find = async sel => { for (const s of ['Dynasty', 'You', 'House', 'Houses']) { await rail(s); await page.waitForTimeout(450); if (await page.evaluate(q => !!document.querySelector(q), sel)) return s; } return ''; };
  const press = (sel, text) => page.evaluate(([s, t]) => { const b = [...document.querySelectorAll(s + ' button')].find(e => !e.disabled && e.innerText.trim().startsWith(t)); if (b) b.click(); return b ? b.innerText.trim() : ''; }, [sel, text]);
  const text = sel => page.evaluate(s => { const el = document.querySelector(s); return el ? el.innerText.replace(/\s+/g, ' ') : 'NO PANEL'; }, sel);
  const shot = async (sel, name) => { const box = await page.evaluate(s => { const el = document.querySelector(s); if (!el) return null; el.scrollIntoView({ block: 'start' }); const r = el.getBoundingClientRect(); return { x: Math.max(0, r.x), y: Math.max(0, r.y), width: r.width, height: Math.min(r.height, 1300) }; }, sel); if (box) await page.screenshot({ path: shots + '/' + name + '.png', clip: box }); };

  await closeWindows();
  const bad = t => /NaN|undefined|Exception|@\(/.test(t) ? 'SUSPICIOUS TEXT' : 'ok';
  const senate = async () => { await rail('Senate'); await page.waitForTimeout(400); await page.evaluate(() => { const b = [...document.querySelectorAll('button')].find(e => e.innerText.includes('The ladder')); if (b) b.click(); }); await page.waitForTimeout(500); return page.evaluate(() => !!document.querySelector('.panel.courts')); };
  console.log('courts panel on the Senate screen: ' + await senate());
  let t = await text('.panel.courts'); console.log('courts: ' + bad(t) + ' · ' + t.slice(0, 260));
  let brought = false, offered = false;
  for (let y = 0; y < years && !(brought && offered); y++) {
    await senate();
    if (!offered) {
      const o = await page.evaluate(() => { const b = [...document.querySelectorAll('.panel.courts .court-bench button')].find(e => !e.disabled); if (!b) return ''; const row = b.closest('.court-bench').innerText.replace(/\s+/g, ' '); b.click(); return row; });
      if (o) { offered = true; console.log('year ' + (y + 1) + ' offer made to: ' + o.slice(0, 160)); await page.waitForTimeout(400); await shot('.panel.courts', 'courts-offer'); }
    }
    if (!brought) {
      if (await page.evaluate(() => !![...document.querySelectorAll('.panel.courts .court-charge button')].find(e => !e.disabled))) await shot('.panel.courts', 'courts-charge');
      const c = await page.evaluate(() => { const b = [...document.querySelectorAll('.panel.courts .court-charge button')].find(e => !e.disabled); if (!b) return ''; const row = b.closest('.court-charge').innerText.replace(/\s+/g, ' '); b.click(); return row; });
      if (c) { brought = true; console.log('year ' + (y + 1) + ' charge brought: ' + c.slice(0, 330)); await page.waitForTimeout(400); }
      else if (y % 4 === 0) console.log('year ' + (y + 1) + ' on the record: ' + await page.evaluate(() => document.querySelectorAll('.panel.courts .court-charge').length) + ' · first: ' + await page.evaluate(() => { const r = document.querySelector('.panel.courts .court-charge'); const b = r && r.querySelector('button'); return r ? r.innerText.replace(/\s+/g, ' ').slice(0, 200) + (b && b.disabled ? ' (' + b.title + ')' : '') : 'nothing'; }));
    }
    await endTurn();
  }
  await senate(); t = await text('.panel.courts'); console.log('courts after: ' + bad(t));
  console.log('pocket: ' + await page.evaluate(() => [...document.querySelectorAll('.panel.courts .pill')].map(e => e.innerText).join(' | ') || 'nobody'));
  await shot('.panel.courts', 'courts');
  await rail('Chronicle'); await page.waitForTimeout(600);
  const lines = await page.evaluate(() => document.body.innerText.split('\n').filter(l => /charges |finds it proved|lets .* go|come to an understanding|does not take what|says in open court|whose money/.test(l)).slice(0, 6));
  console.log('chronicle: ' + (lines.length ? lines.map(l => l.slice(0, 260)).join(' || ') : 'nothing of the courts'));
  console.log('played to turn ' + await turnNo());
  console.log('console errors: ' + (errors.length ? errors.slice(0, 5).join(' || ') : 'none'));
  await browser.close();
})();
