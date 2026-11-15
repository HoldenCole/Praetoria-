// Drives the controls added straight off the gap register (docs/GAPS.md W3, W5, W6, H3, E6, F5, F6): on the wealth panel
// borrows of a relative, lends at interest, entails an heirloom and breaks the entail; reads what each heirloom does, the
// household's keep, the price a relative would take to study, and the old-ties line; then plays some years.
//
//   node register.js ["Start title"] [years]
//
// Needs the build served locally (see README.md here). Screenshots go to PRAETORIA_SHOTS (default ./shots).
const { chromium } = require('playwright');
const title = process.argv[2] || 'The Old Lion'; const years = parseInt(process.argv[3] || '3', 10);
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
  const wealthOn = await find('.panel.wealth'); console.log('wealth panel on: ' + (wealthOn || 'NOT FOUND'));
  let t = await text('.panel.wealth'); console.log('wealth: ' + bad(t));
  console.log('header: ' + (t.match(/your own purse[^.]*?a year[^A-Z]*/) || ['?'])[0].trim());
  console.log('heirloom lines: ' + await page.evaluate(() => [...document.querySelectorAll('.panel.wealth .suit')].map(e => e.innerText.replace(/\s+/g, ' ').slice(0, 170)).join(' || ')));
  console.log('take from the treasury: ' + (await press('.panel.wealth', 'take 20') || await press('.panel.wealth', 'take 5') || 'nothing pressed')); await page.waitForTimeout(300);
  console.log('lend at interest: ' + (await press('.panel.wealth .purse-row', 'Lend 10 at interest') || 'REFUSED OR NONE')); await page.waitForTimeout(300);
  console.log('borrow buttons: ' + await page.evaluate(() => [...document.querySelectorAll('.panel.wealth .purse-row button')].filter(b => /^Borrow/.test(b.innerText.trim())).map(b => b.innerText.trim() + (b.disabled ? ' (' + (b.title || 'cannot') + ')' : '')).slice(0, 6).join(' | ')));
  console.log('borrow: ' + (await press('.panel.wealth .purse-row', 'Borrow') || 'REFUSED OR NONE')); await page.waitForTimeout(300);
  console.log('pills: ' + await page.evaluate(() => [...document.querySelectorAll('.panel.wealth .pill')].map(e => e.innerText).filter(x => /owe/.test(x)).join(' | ')));
  console.log('pay back: ' + (await press('.panel.wealth .purse-row', 'Pay them back') || 'REFUSED OR NONE')); await page.waitForTimeout(300);
  const entailed = await page.evaluate(() => [...document.querySelectorAll('.panel.wealth .suit')].some(e => /entailed: it goes with the house/.test(e.innerText)));
  console.log('an heirloom entailed at the start: ' + entailed);
  if (entailed) {
    console.log('break the entail: ' + (await press('.panel.wealth', 'Break the entail') || 'REFUSED')); await page.waitForTimeout(300);
    console.log('confirm: ' + (await press('.panel.wealth', 'Yes, break the entail') || 'NO CONFIRM')); await page.waitForTimeout(400);
    console.log('entail it again: ' + (await press('.panel.wealth', 'Entail it') || 'REFUSED')); await page.waitForTimeout(400);
  } else { console.log('entail: ' + (await press('.panel.wealth', 'Entail it') || 'NOTHING TO ENTAIL')); await page.waitForTimeout(400); }
  await shot('.panel.wealth', 'register-wealth');

  const eduOn = await find('.panel.education'); console.log('education panel on: ' + (eduOn || 'NOT FOUND'));
  t = await text('.panel.education'); console.log('education: ' + bad(t));
  console.log('priced asks: ' + await page.evaluate(() => [...document.querySelectorAll('.panel.education button')].filter(b => /of your own/.test(b.innerText)).map(b => b.innerText.trim() + (b.disabled ? ' (cannot)' : '')).slice(0, 6).join(' | ') || 'none (all would for the asking, or would not at all)'));
  console.log('pay to study: ' + await page.evaluate(() => { const b = [...document.querySelectorAll('.panel.education button')].find(b => !b.disabled && /of your own/.test(b.innerText)); if (b) { b.click(); return b.innerText.trim(); } return 'none offered'; })); await page.waitForTimeout(400);
  await shot('.panel.education', 'register-education');

  const hhOn = await find('.panel.household'); console.log('household panel on: ' + (hhOn || 'NOT FOUND'));
  t = await text('.panel.household'); console.log('household: ' + bad(t) + ' · ' + (t.match(/Found family and duties[^.]*?handed on[^A-Z]*/) || ['?'])[0].trim());
  const lifeOn = await find('.panel.life'); console.log('life panel on: ' + (lifeOn || 'NOT FOUND'));
  t = await text('.panel.life'); console.log('life: ' + bad(t) + ' · ties: ' + (t.match(/Old (friends|enemies)[^.]*\./) || ['no old ties yet'])[0]);

  for (let y = 0; y < years; y++) await endTurn();
  console.log('played to turn ' + await turnNo());
  await find('.panel.wealth'); t = await text('.panel.wealth'); console.log('wealth after: ' + bad(t));
  console.log('pills after: ' + await page.evaluate(() => [...document.querySelectorAll('.panel.wealth .pill')].map(e => e.innerText).filter(x => /owe/.test(x)).join(' | ')));
  await shot('.panel.wealth', 'register-wealth-after');
  await find('.panel.wealth'); console.log('borrow after: ' + (await press('.panel.wealth .purse-row', 'Borrow') || 'REFUSED OR NONE')); await page.waitForTimeout(300);
  console.log('pills now: ' + await page.evaluate(() => [...document.querySelectorAll('.panel.wealth .pill')].map(e => e.innerText).filter(x => /owe/.test(x)).join(' | ')));
  await shot('.panel.wealth', 'register-wealth-borrowed');
  await find('.panel.life'); t = await text('.panel.life'); console.log('life after: ' + bad(t) + ' · ties: ' + (t.match(/Old (friends|enemies)[^.]*\./) || ['no old ties yet'])[0]);
  await shot('.panel.life', 'register-life');
  console.log('elder asks: ' + await page.evaluate(() => [...document.querySelectorAll('.panel.life .elder-asks button')].map(b => b.innerText.trim() + (b.disabled ? ' (cannot)' : '')).join(' | ') || 'no elder in the house'));
  console.log('console errors: ' + (errors.length ? errors.slice(0, 5).join(' || ') : 'none'));
  await browser.close();
})();
