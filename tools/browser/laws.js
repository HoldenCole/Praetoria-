// Drives the new law screens: the edicts panel and the local-law summary on the Laws window, the law of the Reach by category,
// the local-law block on a world's card, and the Senate's Act picker grouped by category. Issues an edict the head may issue and
// sets a local law on a world, then reports what the screens say and saves a picture of each.
//
//   node laws.js ["Start title"]
//
// Needs the build served locally (see README.md here). Screenshots go to PRAETORIA_SHOTS (default ./shots).
const { chromium } = require('playwright');
const title = process.argv[2] || 'The Old Lion';
const base = process.env.PRAETORIA_URL || 'http://localhost:8112/'; const shots = process.env.PRAETORIA_SHOTS || './shots';
require('fs').mkdirSync(shots, { recursive: true });
(async () => {
  const browser = await chromium.launch({ args: ['--use-gl=angle', '--use-angle=swiftshader', '--ignore-gpu-blocklist'] });
  const page = await browser.newPage({ viewport: { width: 1600, height: 1000 }, deviceScaleFactor: 2 }); page.setDefaultTimeout(60000);
  const errors = []; page.on('console', m => { if (m.type() === 'error') errors.push(m.text().slice(0, 300)); }); page.on('pageerror', e => errors.push('PAGEERROR ' + e.message.slice(0, 300)));
  await page.goto(base, { waitUntil: 'networkidle' }); await page.waitForSelector('.title-screen', { timeout: 90000 });
  await page.evaluate(() => { try { localStorage.setItem('praetoria.primer.seen', '1'); } catch (e) {} });
  await page.click('.scenario:has-text("' + title + '")'); await page.click('button.begin'); await page.waitForSelector('.gs-shell', { timeout: 90000 });
  const rail = name => page.evaluate(n => { const el = [...document.querySelectorAll('.rail-btn')].find(e => e.innerText.trim().toLowerCase() === n.toLowerCase()); if (el) el.click(); return !!el; }, name);
  const tab = async name => { await page.waitForTimeout(300); await page.evaluate(n => { const t = [...document.querySelectorAll('.screen.window .rung')].find(e => e.innerText.toLowerCase().includes(n.toLowerCase())); if (t) t.click(); }, name); await page.waitForTimeout(400); };
  const closeWindows = async () => { for (let i = 0; i < 8; i++) { const had = await page.evaluate(() => { const c = document.querySelector('.win-close') || document.querySelector('.overlay .btn.primary'); if (c) c.click(); return !!c; }); if (!had) break; await page.waitForTimeout(150); } };
  const confirm = async () => { await page.waitForTimeout(300); const said = await page.evaluate(() => { const b = document.querySelector('[data-confirm="yes"]'); if (!b) return 'no question was asked'; const q = (b.closest('.confirm-box') || {}).innerText || ''; b.click(); return q.replace(/\s+/g, ' ').slice(0, 300); }); console.log('  asked: ' + said); await page.waitForTimeout(600); const out = await page.evaluate(() => { const o = document.querySelector('[data-outcome-ok]'); const t = o ? (o.closest('.confirm-box') || {}).innerText || '' : ''; if (o) o.click(); return t.replace(/\s+/g, ' ').slice(0, 300); }); if (out) console.log('  outcome: ' + out); await page.waitForTimeout(300); };
  const text = sel => page.evaluate(s => { const e = document.querySelector(s); return e ? e.innerText.replace(/\s+/g, ' ') : 'NOT FOUND: ' + s; }, sel);
  const shot = async (sel, name) => { const el = await page.$(sel); if (!el) { console.log('no ' + sel + ' to picture'); return; } await el.scrollIntoViewIfNeeded(); await el.screenshot({ path: shots + '/' + name }); console.log('saved ' + name); };
  const suspicious = t => /NaN|undefined|Exception|@\(|\(s\)/.test(t) ? 'SUSPICIOUS TEXT' : 'ok';
  await closeWindows();

  // the Laws window: the law of the Reach by category, and the edicts
  await rail('Laws'); await page.waitForTimeout(700); await tab('The law of the Reach');
  const cats = await page.evaluate(() => [...document.querySelectorAll('[data-reach-category]')].map(e => e.getAttribute('data-reach-category')));
  console.log('law of the Reach, by category: ' + cats.join(', '));
  await page.screenshot({ path: shots + '/laws-reach.png' }); console.log('saved laws-reach.png');
  const edicts = await text('[data-edicts]'); console.log('edicts panel: ' + suspicious(edicts) + ' · ' + edicts.slice(0, 400));
  const can = await page.evaluate(() => [...document.querySelectorAll('[data-edict]')].map(b => b.getAttribute('data-edict') + (b.disabled ? ' (no: ' + (b.title || '').slice(0, 60) + ')' : ' (yes)')));
  console.log('edicts offered: ' + can.join('; '));
  await shot('[data-edicts]', 'edicts-panel.png');
  const pick = await page.evaluate(() => { const b = [...document.querySelectorAll('[data-edict]')].find(e => !e.disabled); if (b) { b.click(); return b.getAttribute('data-edict'); } return ''; });
  console.log('issued: ' + (pick || 'none could be issued')); if (pick) { await confirm(); await shot('[data-edicts]', 'edicts-in-force.png'); console.log('in force now: ' + await page.evaluate(() => [...document.querySelectorAll('[data-edict-in-force]')].map(e => e.getAttribute('data-edict-in-force')).join(', '))); }
  await closeWindows();

  // a world's card on the Worlds window: the local-law block, and a law set
  await rail('Worlds'); await page.waitForTimeout(900);
  console.log('world picked: ' + await page.evaluate(() => { const r = document.querySelector('.screen.window tr.mine'); if (r) r.click(); return r ? r.innerText.replace(/\s+/g, ' ').slice(0, 60) : 'no world of yours in the list'; })); await page.waitForTimeout(700);
  const block = await text('[data-local-laws]'); console.log('local-law block: ' + suspicious(block) + ' · ' + block.slice(0, 500));
  await shot('[data-local-laws]', 'local-laws-block.png');
  const set = await page.evaluate(() => { const want = ['dole:corn_dole', 'local_tithe:remitted', 'charter:chartered']; for (const k of want) { const b = document.querySelector('[data-local-option="' + k + '"]'); if (b && !b.disabled) { b.click(); return k; } } const b = [...document.querySelectorAll('[data-local-option]')].find(e => !e.disabled); if (b) { b.click(); return b.getAttribute('data-local-option'); } return ''; });
  console.log('set: ' + (set || 'none could be set')); if (set) { await confirm(); await shot('[data-local-laws]', 'local-laws-set.png'); console.log('block now: ' + (await text('[data-local-laws]')).slice(0, 300)); }
  await closeWindows();
  await rail('Laws'); await page.waitForTimeout(700); await tab('The laws in your gift');
  console.log('summary on the Laws window: ' + (await text('[data-local-summary]')).slice(0, 300));
  await shot('[data-local-summary]', 'local-laws-summary.png');
  await closeWindows();

  // the Senate: the Act picker grouped by category
  await rail('Senate'); await page.waitForTimeout(900); await tab('Move a motion');
  await page.evaluate(() => { const b = [...document.querySelectorAll('button')].find(e => e.innerText.startsWith('Show what could be moved')); if (b) b.click(); });
  await page.waitForTimeout(400);
  const groups = await page.evaluate(() => [...document.querySelectorAll('optgroup[data-act-category]')].map(g => g.getAttribute('label') + ' (' + g.children.length + ')'));
  console.log('Act picker groups: ' + groups.join(', '));
  const chosen = await page.evaluate(() => { const sel = [...document.querySelectorAll('select.sel')].find(s => s.querySelector('optgroup[data-act-category]')); if (!sel) return ''; const o = [...sel.querySelectorAll('option')].find(x => x.value === 'usury') || sel.querySelectorAll('option')[1]; sel.value = o.value; sel.dispatchEvent(new Event('change', { bubbles: true })); sel.closest('.proposal').scrollIntoView(); return o.value; });
  await page.waitForTimeout(600); console.log('Act chosen in the picker: ' + chosen);
  console.log('option chosen: ' + await page.evaluate(() => { const s = [...document.querySelectorAll('select.sel')].find(x => x.querySelector('optgroup[data-act-category]')); const p = s && s.closest('.proposal'); const o = p && [...p.querySelectorAll('select.sel')].find(x => x !== s); if (!o) return 'no option picker'; const v = [...o.options].find(x => x.value === 'capped') || o.options[1]; if (!v) return 'none'; o.value = v.value; o.dispatchEvent(new Event('change', { bubbles: true })); return v.value; }));
  await page.waitForTimeout(600);
  const prop = await page.evaluate(() => { const s = [...document.querySelectorAll('select.sel')].find(x => x.querySelector('optgroup[data-act-category]')); return s ? s.closest('.proposal').innerText.replace(/\s+/g, ' ').slice(0, 700) : 'NOT FOUND'; });
  console.log('the Act proposal: ' + suspicious(prop) + ' · ' + prop);
  const el = await page.evaluateHandle(() => { const s = [...document.querySelectorAll('select.sel')].find(x => x.querySelector('optgroup[data-act-category]')); return s ? s.closest('.proposal') : null; });
  if (el && el.asElement()) { await el.asElement().screenshot({ path: shots + '/senate-act-picker.png' }); console.log('saved senate-act-picker.png'); }
  // the ladder of offices shows the age the law of the Reach sets for each rung
  await tab('Ladder and courts'); console.log('ladder ages: ' + await page.evaluate(() => { const t = (document.querySelector('.screen.window') || document.body).innerText; return (t.match(/from \d+/g) || []).slice(0, 6).join(', ') || 'none shown'; }));
  console.log('errors: ' + (errors.length ? errors.join(' | ') : 'none'));
  await browser.close();
})();
