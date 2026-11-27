// Drives the new law screens: the edicts panel and the local-law summary on the Laws window, the law of the Reach by category,
// the local-law block on a world's card, and the Senate's Act picker grouped by category. Issues an edict the head may issue and
// sets a local law on a world, then reports what the screens say and saves a picture of each. Last, at the "larger" text size
// (an 18px root) at 1600 by 1000 and 1366 by 768, and the "large" one at 1366 by 768, it opens every tab of the Laws window and
// reports anything cut short: a word ellipsised, a ladder of plates wider than its row, a plate squeezed, marks run under a
// plate's cost, a fact of the header under the frame, an edict's chips hidden. A whole-screen picture is kept only once the top bar, the rail and the minimap have
// painted (a 2x capture can come back with tiles missing; it is taken again).
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
  const tab = async name => { await page.waitForTimeout(300); await page.evaluate(n => { const t = [...document.querySelectorAll('.screen.window .rung, .screen.window .ui-tab')].find(e => e.innerText.toLowerCase().includes(n.toLowerCase())); if (t) t.click(); }, name); await page.waitForTimeout(400); };
  const closeWindows = async () => { for (let i = 0; i < 8; i++) { const had = await page.evaluate(() => { const c = document.querySelector('.win-close') || document.querySelector('.overlay .btn.primary'); if (c) c.click(); return !!c; }); if (!had) break; await page.waitForTimeout(150); } };
  const confirm = async () => { await page.waitForTimeout(300); const said = await page.evaluate(() => { const b = document.querySelector('[data-confirm="yes"]'); if (!b) return 'no question was asked'; const q = (b.closest('.confirm-box') || {}).innerText || ''; b.click(); return q.replace(/\s+/g, ' ').slice(0, 300); }); console.log('  asked: ' + said); await page.waitForTimeout(600); const out = await page.evaluate(() => { const o = document.querySelector('[data-outcome-ok]'); const t = o ? (o.closest('.confirm-box') || {}).innerText || '' : ''; if (o) o.click(); return t.replace(/\s+/g, ' ').slice(0, 300); }); if (out) console.log('  outcome: ' + out); await page.waitForTimeout(300); };
  const text = sel => page.evaluate(s => { const e = document.querySelector(s); return e ? e.innerText.replace(/\s+/g, ' ') : 'NOT FOUND: ' + s; }, sel);
  const shot = async (sel, name) => { const el = await page.$(sel); if (!el) { console.log('no ' + sel + ' to picture'); return; } await el.scrollIntoViewIfNeeded(); await el.screenshot({ path: shots + '/' + name }); console.log('saved ' + name); };
  const suspicious = t => /NaN|undefined|Exception|@\(|\(s\)/.test(t) ? 'SUSPICIOUS TEXT' : 'ok';
  // the shell has painted: steel on the bar and the rail, the minimap drawn; then two frames
  const settle = async () => {
    for (let i = 0; i < 100; i++) {
      const ok = await page.evaluate(() => {
        const bg = s => { const e = document.querySelector(s); return !!e && getComputedStyle(e).backgroundImage !== 'none'; };
        const c = document.querySelector('.console .mm canvas'); let drawn = !c;
        if (c) { try { const d = c.getContext('2d').getImageData(0, 0, c.width, c.height).data; let lit = 0; for (let k = 0; k < d.length; k += 16) if (d[k] + d[k + 1] + d[k + 2] > 90) lit++; drawn = lit > 40; } catch (e) { drawn = true; } }
        return bg('.topbar') && bg('.rail') && drawn;
      });
      if (ok) break; await page.waitForTimeout(250);
    }
    await page.evaluate(() => new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r))));
  };
  // a picture of the whole screen, checked: the bar's brass trim across the width and the rail's steel down its length are in it
  const fullShot = async name => {
    for (let attempt = 1; attempt <= 4; attempt++) {
      await settle(); const buf = await page.screenshot();
      const bad = await page.evaluate(async b64 => {
        const img = new Image(); img.src = 'data:image/png;base64,' + b64; await img.decode();
        const s = img.width / window.innerWidth, c = document.createElement('canvas'); c.width = img.width; c.height = img.height; const g = c.getContext('2d'); g.drawImage(img, 0, 0);
        const mean = (x, y, w, h) => { const d = g.getImageData(Math.round(x * s), Math.round(y * s), Math.round(w * s), Math.round(h * s)).data; const m = [0, 0, 0]; for (let k = 0; k < d.length; k += 4) { m[0] += d[k]; m[1] += d[k + 1]; m[2] += d[k + 2]; } return m.map(v => v / (d.length / 4)); };
        const bar = document.querySelector('.topbar').getBoundingClientRect(), rail = document.querySelector('.rail').getBoundingClientRect(), out = [];
        for (const f of [.3, .45, .6, .75, .9]) { const m = mean(bar.width * f, bar.bottom + 1.5, 24, 2.5); if (m[0] < 100) out.push('the bar\'s trim at x ' + Math.round(bar.width * f)); }
        for (const f of [.3, .55, .8]) { const m = mean(rail.left + 2, rail.top + rail.height * f, 6, 40); if (m[0] < 20 || (m[0] + m[1] + m[2]) / 3 < 24) out.push('the rail at y ' + Math.round(rail.top + rail.height * f)); }
        return out;
      }, buf.toString('base64'));
      if (bad.length === 0) { require('fs').writeFileSync(shots + '/' + name, buf); console.log('saved ' + name + (attempt > 1 ? ' (taken ' + attempt + ' times)' : '')); return; }
      console.log('  ' + name + ': not painted (' + bad.join(', ') + '), taken again'); await page.waitForTimeout(1500);
    }
    console.log('NOT SAVED ' + name + ': the shell never painted whole');
  };
  // what is cut short in the Laws window as it stands
  const cuts = () => page.evaluate(() => {
    const bad = [], say = e => (e.innerText || '').replace(/\s+/g, ' ').trim().slice(0, 40), win = document.querySelector('.laws-win'); if (!win) return ['no Laws window'];
    const wr = win.getBoundingClientRect(), shown = e => getComputedStyle(e).display !== 'none' && e.getClientRects().length > 0;
    win.querySelectorAll('*').forEach(e => { const cs = getComputedStyle(e); if (cs.textOverflow === 'ellipsis' && shown(e) && e.scrollWidth > e.clientWidth + 1 && !e.classList.contains('es2-note')) bad.push('ellipsised: ' + say(e)); });
    win.querySelectorAll('.lw-lad').forEach(l => { if (l.scrollWidth > l.clientWidth + 1) bad.push('ladder wider than its row: ' + say(l.closest('.lw-row').querySelector('.lw-nm'))); if (l.getBoundingClientRect().right > wr.right - 20) bad.push('ladder under the frame: ' + say(l)); });
    win.querySelectorAll('.lw-pl').forEach(p => { if (p.scrollWidth > p.clientWidth + 1) bad.push('plate squeezed: ' + say(p)); const mk = p.querySelector('.lw-mks'), pc = p.querySelector('.lw-pc, .lw-in'); if (mk && pc && shown(pc) && mk.getBoundingClientRect().right > pc.getBoundingClientRect().left + 1) bad.push('marks under the cost: ' + say(p)); });
    win.querySelectorAll('.ui-facts > span').forEach(f => { if (shown(f) && f.getBoundingClientRect().right > wr.right - 24) bad.push('fact under the frame: ' + say(f)); });
    win.querySelectorAll('.lw-fxline').forEach(l => { if (l.scrollHeight > l.clientHeight + 1 && getComputedStyle(l).overflow === 'hidden') bad.push('chips hidden: ' + say(l)); });
    return bad;
  });
  await closeWindows();

  // the Laws window: the law of the Reach by category, and the edicts (each on its own tab)
  await rail('Laws'); await page.waitForTimeout(700); await tab('The Reach');
  const cats = await page.evaluate(() => [...document.querySelectorAll('[data-reach-category]')].map(e => e.getAttribute('data-reach-category')));
  console.log('law of the Reach, by category: ' + cats.join(', '));
  await fullShot('laws-reach.png');
  await tab('Edicts');
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
  await rail('Laws'); await page.waitForTimeout(700); await tab('Your laws');
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

  // the larger text size (an 18px root) at 1600 by 1000 and 1366 by 768, and the large one (16.5px) at 1366 by 768: every tab of the
  // Laws window, and what is cut short
  for (const [size, w, h] of [['larger', 1600, 1000], ['larger', 1366, 768], ['large', 1366, 768]]) {
    await page.evaluate(s => { try { localStorage.setItem('praetoria.settings', JSON.stringify({ text: s })); } catch (e) {} }, size);
    await page.setViewportSize({ width: w, height: h });
    await page.goto(base, { waitUntil: 'networkidle' }); await page.waitForSelector('.title-screen', { timeout: 90000 });
    await page.click('.scenario:has-text("' + title + '")'); await page.click('button.begin'); await page.waitForSelector('.gs-shell', { timeout: 90000 }); await closeWindows();
    console.log(size + ' text at ' + w + ' by ' + h + ' (root ' + await page.evaluate(() => getComputedStyle(document.documentElement).fontSize) + ')');
    await rail('Laws'); await page.waitForTimeout(900);
    for (const t of await page.evaluate(() => [...document.querySelectorAll('.laws-win .ui-tab')].map(e => e.innerText.replace(/\d+/g, '').trim()))) {
      await tab(t); await settle(); const bad = await cuts();
      console.log('  ' + t + ': ' + (bad.length ? bad.length + ' cut short\n    ' + bad.join('\n    ') : 'nothing cut short'));
    }
    await closeWindows();
  }
  await page.evaluate(() => { try { localStorage.removeItem('praetoria.settings'); } catch (e) {} });
  console.log('errors: ' + (errors.length ? errors.join(' | ') : 'none'));
  await browser.close();
})();
