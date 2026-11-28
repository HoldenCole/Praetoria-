// Drives the family controls: on the wealth panel gives a relative a sum, lends another, sets whose money a dowry is; on the
// education panel asks a relative to study and to teach; then plays some years and reads both panels and the chronicle.
//
//   node family.js ["Start title"] [years]
//
// Needs the build served locally (see README.md here). Screenshots go to PRAETORIA_SHOTS (default ./shots).
const { chromium } = require('playwright');
const title = process.argv[2] || 'The Old Lion'; const years = parseInt(process.argv[3] || '3', 10);
const base = require('./address')(); const shots = process.env.PRAETORIA_SHOTS || './shots';
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
  const turnNo = () => page.evaluate(() => { const d = document.querySelector('[data-turn]'); if (d) return parseInt(d.dataset.turn, 10); const m = document.body.innerText.match(/Turn (\d+)/); return m ? parseInt(m[1], 10) : -1; });
  // what this script takes from the treasury can end the year short, and End Year then asks first (ConfirmBox): end it anyway
  const endTurn = async () => { await closeWindows(); const before = await turnNo(); await page.evaluate(() => { const b = document.querySelector('button.endturn'); if (b) b.click(); }); for (let i = 0; i < 800; i++) { if (await turnNo().catch(() => -1) > before) break; await page.evaluate(() => { const y = document.querySelector('.confirm-box [data-confirm="yes"]'); if (y) y.click(); }); await page.waitForTimeout(50); } await closeWindows(); };
  // find the screen each panel lives on
  const find = async sel => { for (const s of ['Dynasty', 'You', 'House', 'Houses']) { await rail(s); await page.waitForTimeout(450); if (await page.evaluate(q => !!document.querySelector(q), sel)) return s; const tabs = await page.evaluate(() => document.querySelectorAll('.screen.window .ex-tabs .rung, .screen.window .cw-tabs .rung, .screen.window .ui-tab').length); for (let i = 1; i < tabs; i++) { await page.evaluate(k => document.querySelectorAll('.screen.window .ex-tabs .rung, .screen.window .cw-tabs .rung, .screen.window .ui-tab')[k].click(), i); await page.waitForTimeout(300); if (await page.evaluate(q => !!document.querySelector(q), sel)) return s; } } return ''; };
  const press = (sel, text) => page.evaluate(([s, t]) => { const b = [...document.querySelectorAll(s + ' button')].find(e => !e.disabled && e.innerText.trim().toLowerCase().startsWith(t.toLowerCase())); if (b) b.click(); return b ? b.innerText.replace(/\s+/g, ' ').trim() : ''; }, [sel, text]);
  const text = sel => page.evaluate(s => { const el = document.querySelector(s); return el ? el.innerText.replace(/\s+/g, ' ') : 'NO PANEL'; }, sel);
  const shot = async (sel, name) => { const box = await page.evaluate(s => { const el = document.querySelector(s); if (!el) return null; el.scrollIntoView({ block: 'start' }); const r = el.getBoundingClientRect(); return { x: Math.max(0, r.x), y: Math.max(0, r.y), width: r.width, height: Math.min(r.height, 1300) }; }, sel); if (box) await page.screenshot({ path: shots + '/' + name + '.png', clip: box, timeout: 180000 }); };

  await closeWindows();
  const wealthOn = await find('.panel.wealth'); console.log('wealth panel on: ' + (wealthOn || 'NOT FOUND'));
  let t = await text('.panel.wealth'); console.log('wealth: ' + (/NaN|undefined|Exception|@\(/.test(t) ? 'SUSPICIOUS TEXT' : 'ok') + ' · purse rows ' + await page.evaluate(() => document.querySelectorAll('.panel.wealth .purse-row').length));
  // the sum to move is on a joined stepper (docs/VISUAL_SYSTEM.md 4.12): raise it to 2,000 so there is enough to give and then lend
  for (let i = 0; i < 3; i++) { await page.evaluate(() => { const b = document.querySelector('.panel.wealth .ui-step-wrap button[aria-label="More"]'); if (b && !b.disabled) b.click(); }); await page.waitForTimeout(150); }
  console.log('take from the treasury: ' + (await press('.panel.wealth', 'take 2') || await press('.panel.wealth', 'take 5') || 'nothing pressed')); await page.waitForTimeout(300);
  console.log('give: ' + (await press('.panel.wealth .purse-row', 'Give 5') || 'REFUSED OR NONE')); await page.waitForTimeout(300);
  console.log('lend: ' + (await press('.panel.wealth .purse-row', 'Lend') || 'REFUSED OR NONE')); await page.waitForTimeout(300);
  console.log('dowries: ' + (await press('.panel.wealth', 'From my own purse first') || 'NO SUCH BUTTON')); await page.waitForTimeout(300);
  console.log('owed: ' + await page.evaluate(() => [...document.querySelectorAll('.panel.wealth .pill')].map(e => e.innerText).filter(x => /owes you/i.test(x)).join(' | ')));
  await shot('.panel.wealth', 'family-wealth');
  const eduOn = await find('.panel.education'); console.log('education panel on: ' + (eduOn || 'NOT FOUND'));
  t = await text('.panel.education'); console.log('education: ' + (/NaN|undefined|Exception|@\(/.test(t) ? 'SUSPICIOUS TEXT' : 'ok'));
  // a grown relative is a row with a Study select (docs/VISUAL_SYSTEM.md 4.12): open it and take the first thing they may be asked
  const openStudy = () => page.evaluate(() => { const b = document.querySelector('.panel.education .dy-study .ui-select:not(.dy-tutor) .ui-select-btn'); if (b) b.click(); return !!b; });
  await openStudy(); await page.waitForTimeout(300);
  const asked = await page.evaluate(() => { const m = document.querySelector('.panel.education .dy-study .ui-menu'); if (!m) return 'NO ASK ROW'; const b = [...m.querySelectorAll('button')].find(e => !/cannot/.test(e.innerText) && !/^Have /.test(e.innerText.trim())); if (b) b.click(); return b ? b.innerText.replace(/\s+/g, ' ').trim() : 'nothing could be asked'; });
  console.log('asked to study: ' + asked); await page.waitForTimeout(400);
  await openStudy(); await page.waitForTimeout(300);
  console.log('teach: ' + (await press('.panel.education', 'Have ') || 'nobody has anything to teach another')); await page.waitForTimeout(300);
  await shot('.panel.education', 'family-education');
  for (let y = 0; y < years; y++) await endTurn();
  await find('.panel.wealth'); await page.waitForTimeout(300);
  console.log('after ' + years + ' years, owed: ' + await page.evaluate(() => [...document.querySelectorAll('.panel.wealth .pill')].map(e => e.innerText).filter(x => /owes you/i.test(x)).join(' | ') || 'nothing'));
  await rail('Chronicle'); await page.waitForTimeout(600);
  console.log('chronicle: ' + await page.evaluate(() => (document.body.innerText || '').split('\n').map(x => x.trim()).filter(x => /credits of their own|lends |paid back the last|turns to |their own affair|takes .* in hand|under their wing|has learned something/i.test(x)).slice(0, 8).map(x => x.slice(0, 200)).join('\n   ')));
  console.log('errors: ' + errors.length); errors.slice(0, 5).forEach(e => console.log('   ' + e));
  await browser.close();
})().catch(e => { console.error('FAILED ' + e.message.split('\n')[0]); process.exit(1); });
