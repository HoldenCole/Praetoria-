// Drives the three ideas taken from Imperator: Rome (docs/IDEAS_FROM_IMPERATOR.md 1, 2 and 9) as a player would:
//  - foreign gold (docs/BEYOND.md, "Foreign gold in the Senate"): moves a war on the Free Cities of Thessar from the Senate, reads the
//    gold block under the motion, and answers the sum the Cities send;
//  - standing in absence (docs/LADDER.md): looks on the ladder for a refusal for one who commands away from the capital, and moves
//    leave to stand in absence for the house when it can;
//  - a charge against the powerful (docs/LADDER.md): reads the block on the Senate's courts, threatens a general or brings a charge if
//    the courts offer one, and answers a charge brought against the player;
//  - the triumph had at the gate (docs/CAESAR.md): reads the triumph on the Caesar panel, moves one when the Senate would hear it (for
//    the house's own general, or for another house's), and answers it at the gate if one is voted.
//
//   node imperator.js ["Start title"] [years] [take|refuse|expose]
//
// The third argument is the answer given to the Cities' gold (default take: it puts the gold on the record, where a rival may charge it).
// The war is moved once the Cities' purse can pay a mover to withdraw it (from year 5 of a game: PRAETORIA_WAR_YEAR); one motion is
// before the Senate at a time, so leave to stand in absence goes first when the ladder asks for it. PRAETORIA_CAP_MIN caps the run in
// minutes (default 24). Needs the build served locally (see README.md here). Screenshots go to PRAETORIA_SHOTS (default ./shots), imp-*.png.
const { chromium } = require('playwright');
const title = process.argv[2] || 'The Old Lion'; const years = parseInt(process.argv[3] || '16', 10); const goldAnswer = process.argv[4] || 'take';
const base = require('./address')(); const shots = process.env.PRAETORIA_SHOTS || './shots';
const warYear = parseInt(process.env.PRAETORIA_WAR_YEAR || '5', 10); const capMs = parseFloat(process.env.PRAETORIA_CAP_MIN || '24') * 60000;
const LONG = 180000; const t0 = Date.now(); const mins = () => ((Date.now() - t0) / 60000).toFixed(1);
require('fs').mkdirSync(shots, { recursive: true });
const reached = {}; const why = {};   // what each feature came to, for the summary
const mark = (k, said) => { reached[k] = reached[k] || []; if (!reached[k].includes(said)) reached[k].push(said); };
const flat = s => (s || '').replace(/\s+/g, ' ').trim();
// what looks wrong in a screen's text: raw code, an em dash, a figure with a hyphen-minus for its sign
const lint = t => { const out = []; if (/NaN|undefined|Exception|@\(|House House|\(s\)/.test(t)) out.push('raw code'); if (t.includes('\u2014')) out.push('an em dash'); const m = t.match(/(^|[\s(·:,])-\d[\d,.]*/g); if (m) out.push('a hyphen-minus figure (' + m.slice(0, 3).map(x => x.trim()).join(' ') + ')'); return out.length ? 'LOOK: ' + out.join('; ') : 'ok'; };
(async () => {
  const browser = await chromium.launch({ args: ['--use-gl=angle', '--use-angle=swiftshader', '--ignore-gpu-blocklist', '--disable-gpu-rasterization'] });
  const page = await browser.newPage({ viewport: { width: 1600, height: 1200 }, deviceScaleFactor: +(process.env.PRAETORIA_SCALE || 2) }); page.setDefaultTimeout(LONG);
  const errors = []; page.on('console', m => { if (m.type() === 'error') errors.push(m.text().slice(0, 300)); }); page.on('pageerror', e => errors.push('PAGEERROR ' + e.message.slice(0, 300)));
  await page.goto(base, { waitUntil: 'networkidle', timeout: LONG }); await page.waitForSelector('.title-screen', { timeout: LONG });
  await page.evaluate(() => { try { localStorage.setItem('praetoria.primer.seen', '1'); } catch (e) {} });
  await page.click('.scenario:has-text("' + title + '")'); await page.click('button.begin'); await page.waitForSelector('.gs-shell', { timeout: LONG });
  console.log('began "' + title + '" in ' + mins() + ' minutes');

  const rail = name => page.evaluate(n => { const el = [...document.querySelectorAll('.rail-btn')].find(e => e.innerText.trim().toLowerCase() === n.toLowerCase()); if (el) el.click(); return !!el; }, name);
  // a window's tabs are the shared Tabs part (docs/VISUAL_SYSTEM.md 4.4): each a .ui-tab with its key in data-tab, a short label and the
  // whole name in its tooltip. The old names still work: "Move a motion" is the Senate's Motions tab, "Ladder and courts" its Ladder.
  const TABS = { 'move a motion': 'move', 'the chamber': 'chamber', 'ladder and courts': 'ladder', 'the general and the senate': 'general', 'your fleets': 'deck' };
  const tab = async name => { await page.waitForTimeout(300); const ok = await page.evaluate(([n, key]) => { const t = (key && document.querySelector('.screen.window .ui-tab[data-tab="' + key + '"]')) || [...document.querySelectorAll('.screen.window .ui-tab, .screen.window .rung, .cw-tabs .rung')].find(e => [e.innerText, e.title || ''].some(x => x.toLowerCase().startsWith(n.toLowerCase()))); if (t) t.click(); return !!t; }, [name, TABS[name.toLowerCase()] || '']); await page.waitForTimeout(500); return ok; };
  const closeWindows = async () => { for (let i = 0; i < 10; i++) { const had = await page.evaluate(() => { const c = document.querySelector('.win-close') || document.querySelector('.profile-x') || document.querySelector('.overlay .btn.primary'); if (c) c.click(); return !!c; }); if (!had) break; await page.waitForTimeout(150); } };
  const confirm = async () => { await page.waitForTimeout(300); const said = await page.evaluate(() => { const b = document.querySelector('[data-confirm="yes"]'); if (!b) return 'no question was asked'; const q = (b.closest('.confirm-box') || {}).innerText || ''; b.click(); return q.replace(/\s+/g, ' ').slice(0, 400); }); console.log('    asked: ' + said); await page.waitForTimeout(600); const out = await page.evaluate(() => { const o = document.querySelector('[data-outcome-ok]'); const t = o ? (o.closest('.confirm-box') || {}).innerText || '' : ''; if (o) o.click(); return t.replace(/\s+/g, ' ').slice(0, 300); }); if (out) console.log('    outcome: ' + out); await page.waitForTimeout(300); };
  // the top bar: the date plate keeps the year in data-turn (its face is in Roman numerals) and "The Republic · second head" under it;
  // the house's name is .tb-name
  const year = () => page.evaluate(() => { const d = document.querySelector('[data-turn]'); if (d) return parseInt(d.dataset.turn, 10); const m = document.body.innerText.match(/Turn (\d+)/); return m ? parseInt(m[1], 10) : -1; });
  const head = () => page.evaluate(() => { const t = (document.querySelector('.topbar') || {}).innerText || ''; const m = t.match(/\b([a-z]+|\d+(st|nd|rd|th)) head\b/i); return m ? m[0] : ''; });
  const myHouse = () => page.evaluate(() => { const n = document.querySelector('.topbar .tb-name'); const m = ((n || document.querySelector('.topbar') || {}).innerText || '').match(/(House [A-Z][a-z]+)/); return m ? m[1] : ''; });
  const toast = () => page.evaluate(() => [...document.querySelectorAll('.toast, .notice, .flash')].map(e => e.innerText.replace(/\s+/g, ' ')).join(' | ').slice(0, 300));
  // an element's picture, at deviceScaleFactor 2; a block of siblings is pictured from its first element to its last
  const shotOf = async (sel, name, until) => {
    const el = await page.$(sel); if (!el) { console.log('    (no ' + sel + ' to picture)'); return; }
    await el.scrollIntoViewIfNeeded({ timeout: LONG }).catch(() => {});
    if (!until) { await el.screenshot({ path: shots + '/' + name, timeout: LONG }); console.log('    saved ' + name); return; }
    const box = await page.evaluate(([s, u]) => { let a = document.querySelector(s); if (a.getBoundingClientRect().height === 0 && a.previousElementSibling && a.previousElementSibling.matches('.ui-sec')) a = a.previousElementSibling; a.scrollIntoView({ block: 'start' }); const items = [a]; let n = a.nextElementSibling; while (n && !n.matches(u) && !n.querySelector(u)) { items.push(n); n = n.nextElementSibling; } const rs = items.map(e => e.getBoundingClientRect()).filter(r => r.height > 0); const x = Math.min(...rs.map(r => r.left)), y = Math.min(...rs.map(r => r.top)), r2 = Math.max(...rs.map(r => r.right)), b = Math.max(...rs.map(r => r.bottom)); return { x: Math.max(0, x - 4), y: Math.max(0, y), width: r2 - x + 8, height: Math.min(b - y + 2, window.innerHeight - Math.max(0, y)) }; }, [sel, until]);
    await page.screenshot({ path: shots + '/' + name, clip: box, timeout: LONG }); console.log('    saved ' + name);
  };
  const succession = async () => { const had = await page.evaluate(() => { const e = document.querySelector('.epilogue.succession'); if (!e) return ''; const t = e.innerText.replace(/\s+/g, ' ').slice(0, 220); const b = [...e.querySelectorAll('button')].find(x => x.innerText.startsWith('Take up the name')); if (b) b.click(); return t; }); if (had) { console.log('  the seat passed: ' + had); await page.waitForTimeout(600); } return !!had; };
  const KEYS = /factor|Free Cities|\bgold\b|in absence|triumph|the gate|exile|let it be known|charge|trial|finds it proved|lets .{3,40} go|convicted|acquitted|lay the command down/i;
  // what the year's reports and the outliner say of these things, read before they are put away
  const reports = async () => { const lines = await page.evaluate(k => { const re = new RegExp(k, 'i'); const els = [...document.querySelectorAll('.overlay, .event, .modal, .interstitial, .report, .ol-row')]; return [...new Set(els.flatMap(e => e.innerText.split('\n')).map(t => t.replace(/\s+/g, ' ').trim()).filter(t => t.length > 20 && !/ · loyalty \d/.test(t) && re.test(t)))].slice(0, 8); }, KEYS.source); lines.forEach(t => console.log('  report: ' + t.slice(0, 300)));
    const surname = (house || '').replace('House ', ''); lines.filter(t => surname && t.includes(surname) && /praetor, (lets .+ go|finds it proved)/.test(t)).forEach(t => mark('charge', 'the verdict: ' + t.replace(/\s*▸$/, '').slice(0, 200))); };
  const endTurn = async () => { await closeWindows(); const before = await year(); await page.evaluate(() => { const b = document.querySelector('button.endturn'); if (b) b.click(); }); for (let i = 0; i < 3600; i++) { if (await year().catch(() => -1) > before) break; if (await page.$('.epilogue.succession')) break; await page.waitForTimeout(50); } await page.waitForTimeout(300); await succession(); await reports(); await closeWindows(); };
  const senate = async view => { await closeWindows(); await rail('Senate'); await page.waitForTimeout(600); if (view) await tab(view); };
  // the motion's form under "Move a motion": the target chosen, and what the Move it button says
  const form = async (kind, target, here) => {
    if (!here) await senate('Move a motion');
    const named = k => [...document.querySelectorAll('.proposal')].find(p => ((p.querySelector('.ui-row-nm') || p.querySelector('b') || {}).innerText || '').trim() === k);
    const opened = await page.evaluate(k => { const pr = [...document.querySelectorAll('.proposal')].find(p => ((p.querySelector('.ui-row-nm') || p.querySelector('b') || {}).innerText || '').trim() === k); if (!pr) return false; if (!pr.classList.contains('open')) { const row = pr.querySelector('.ui-row'); if (row) row.click(); } return true; }, kind);
    if (opened) await page.waitForTimeout(500);
    const set = await page.evaluate(([k, t]) => { const pr = [...document.querySelectorAll('.proposal')].find(p => ((p.querySelector('.ui-row-nm') || p.querySelector('b') || {}).innerText || '').trim() === k); if (!pr) return 'no "' + k + '" among the motions'; const s = pr.querySelector('select'); if (!s || !t) return ''; const o = [...s.options].find(o => o.text.startsWith(t)); if (!o) return 'no "' + t + '" to choose'; s.value = o.value; s.dispatchEvent(new Event('change', { bubbles: true })); return ''; }, [kind, target]);
    if (set) return { err: set };
    await page.waitForTimeout(500);
    return page.evaluate(k => { const pr = [...document.querySelectorAll('.proposal')].find(p => ((p.querySelector('.ui-row-nm') || p.querySelector('b') || {}).innerText || '').trim() === k); document.querySelectorAll('[data-imp-form="' + k + '"]').forEach(e => e.removeAttribute('data-imp-form')); pr.setAttribute('data-imp-form', k); const b = [...pr.querySelectorAll('button')].find(x => x.innerText.trim().startsWith('Move it')); let ct = pr.innerText; [...pr.querySelectorAll('select option')].forEach(o => { ct = ct.replace(o.text, ' '); }); const c = { innerText: ct }; const ph = [...pr.querySelectorAll('select option')].map(o => o.text).filter(t => t.includes('\u2014')); return { text: c.innerText.replace(/\s+/g, ' ').trim(), off: !b || b.disabled, why: b ? b.title : 'no Move it button', placeholders: ph }; }, kind);
  };
  const moveIt = kind => page.evaluate(k => { const pr = document.querySelector('[data-imp-form="' + k + '"]'); const b = pr && [...pr.querySelectorAll('button')].find(x => x.innerText.trim().startsWith('Move it')); if (!b || b.disabled) return false; b.click(); return true; }, kind);
  const docket = () => page.evaluate(() => [...document.querySelectorAll('.screen.window .motion[data-motion]')].map(m => ({ kind: (m.querySelector('.sn-nm') || {}).innerText || '', text: m.innerText.replace(/\s+/g, ' ').slice(0, 400), mine: m.classList.contains('mine') })));
  const myMotion = async () => { await senate('The chamber'); return (await docket()).find(m => m.mine) || null; };
  const caesar = async () => { await closeWindows(); await rail('Military'); await page.waitForTimeout(700); await tab('The general and the Senate'); return page.evaluate(() => { const el = document.querySelector('.panel.caesar'); return el ? (el.innerText + ' ' + ((el.querySelector('.mc-crews-fig') || { getAttribute: () => '' }).getAttribute('title') || '')).replace(/\s+/g, ' ') : 'NO PANEL'; }); };   // whose the crews are is the tooltip of the crews' figure

  let house = await myHouse(); console.log('you are ' + house + ', ' + await head() + ', year ' + await year());
  let chargeYear = 0, verdictRead = false, lastPowerful = '', threatOnMe = false, gaveCommand = 0, warMoved = 0, goldSeen = false, goldAnswered = false, leaveMoved = 0, leaveHad = false, awaySeen = false, threatMade = false, broughtCharges = 0, chargeAnswered = false, triumphMoved = 0, gateAnswered = false, lastScan = -9;
  const placeholderNote = new Set();

  // ---- the opening screens: what each says before anything is done ----
  {
    const c = await caesar(); console.log('Caesar panel (year ' + await year() + '): ' + lint(c)); console.log('   ' + c.slice(0, 700));
    const tb = await page.evaluate(() => { const b = [...document.querySelectorAll('.panel.caesar button')].find(e => e.innerText.trim().startsWith('Ask the Senate for a triumph')); return b ? (b.disabled ? 'refused: ' + (b.title || '').split('\n').pop() : 'open') : 'no such button'; });   // a locked plate's tooltip ends with what it needs
    console.log('  triumph for the house\'s own general: ' + tb); why.triumph = tb;
    await shotOf(await page.$('[data-caesar-general]') ? '[data-caesar-general]' : '.panel.caesar > .ui-lede', 'imp-caesar-triumph.png', '.ui-sec, .mc-recall');   // the general's section: their face, sway, crews and the triumph's plate
  }

  for (let y = 0; y < years; y++) {
    if (Date.now() - t0 > capMs) { console.log('stopping at ' + mins() + ' minutes, the cap'); break; }
    const yr = await year(); house = await myHouse() || house; console.log('\n== year ' + yr + ' · ' + await head() + ' · ' + mins() + ' min');
    if (chargeAnswered && !verdictRead && yr > chargeYear) {
      verdictRead = true; await closeWindows(); await rail('Chronicle'); await page.waitForTimeout(800);
      const surname = house.replace('House ', '');
      const v = await page.evaluate(n => (document.body.innerText || '').split('\n').map(t => t.trim()).filter(t => /charges /.test(t) && t.includes(n)).slice(0, 4), surname);
      console.log('  the chronicle on the charges: ' + (v.length ? v.map(t => t.slice(0, 300)).join(' || ') : 'nothing'));
    }
    let pending = await myMotion(); if (pending) console.log('  your motion before the Senate: ' + pending.kind);

    // 1. the chamber: the gold block under any war motion, and the triumph's gate
    const gold = await page.evaluate(() => [...document.querySelectorAll('[data-foreign-gold-motion]')].map(g => ({ id: g.getAttribute('data-foreign-gold-motion'), text: g.innerText.replace(/\s+/g, ' '), motion: ((g.closest('.motion') || {}).querySelector ? (g.closest('.motion').querySelector('.sn-tx') || {}).innerText : '') || '', buttons: [...g.querySelectorAll('[data-foreign-gold]')].map(b => ({ a: b.getAttribute('data-foreign-gold'), label: b.innerText.replace(/\s+/g, ' ').trim(), off: b.disabled, why: b.title })) })));
    for (const g of gold) {
      console.log('  gold block (' + lint(g.text) + ') under "' + flat(g.motion).slice(0, 120) + '": ' + g.text.slice(0, 500));
      if (!g.buttons.length) continue;
      goldSeen = true; mark('gold', 'offered in year ' + yr + ': ' + flat(g.text).split('. ')[0]);
      g.buttons.forEach(b => console.log('    [' + b.a + '] ' + b.label + (b.off ? ' (refused: ' + b.why + ')' : ' · ' + b.why.slice(0, 260))));
      await page.evaluate(id => { const m = document.querySelector('[data-foreign-gold-motion="' + id + '"]').closest('.motion'); if (m) m.setAttribute('data-imp-gold', id); }, g.id);
      await shotOf('[data-imp-gold="' + g.id + '"]', 'imp-gold-offer.png');
      const pick = g.buttons.find(b => b.a === goldAnswer && !b.off) || g.buttons.find(b => !b.off);
      if (pick) {
        await page.evaluate(([id, a]) => document.querySelector('[data-foreign-gold-motion="' + id + '"] [data-foreign-gold="' + a + '"]').click(), [g.id, pick.a]); await page.waitForTimeout(800);
        goldAnswered = true; mark('gold', 'answered: ' + pick.label);
        const after = await page.evaluate(id => { const g = document.querySelector('[data-foreign-gold-motion="' + id + '"]'); return g ? g.innerText.replace(/\s+/g, ' ') : 'the block is gone: the motion is off the docket'; }, g.id);
        console.log('    answered "' + pick.label + '": ' + after.slice(0, 300) + (await toast() ? ' · toast: ' + await toast() : ''));
        if (await page.$('[data-imp-gold="' + g.id + '"]')) await shotOf('[data-imp-gold="' + g.id + '"]', 'imp-gold-answered.png');
        const hist = await page.evaluate(() => [...document.querySelectorAll('.screen.window .hist')].map(e => e.innerText.replace(/\s+/g, ' ')).filter(t => /Thessar|withdraw/i.test(t)).slice(0, 2).join(' | '));
        if (hist) console.log('    decided lately: ' + hist);
      }
    }
    const gate = await page.evaluate(() => { const a = document.querySelector('[data-triumph-asked]'); return a ? { text: a.innerText.replace(/\s+/g, ' '), buttons: [...a.querySelectorAll('[data-triumph-choice]')].map(b => ({ c: b.getAttribute('data-triumph-choice'), label: b.innerText.replace(/\s+/g, ' ').trim(), off: b.disabled, why: b.title })) } : null; });
    const homes = await page.evaluate(() => [...document.querySelectorAll('[data-triumph-home]')].map(e => e.innerText.replace(/\s+/g, ' ')));
    if (homes.length) { console.log('  at the gate: ' + homes.join(' | ')); mark('triumph', 'home for a triumph in year ' + yr); }
    if (gate && !gateAnswered) {
      console.log('  THE GATE (' + lint(gate.text) + '): ' + gate.text.slice(0, 500)); gate.buttons.forEach(b => console.log('    [' + b.c + '] ' + b.label + (b.off ? ' (refused: ' + b.why + ')' : ' · ' + b.why)));
      await shotOf('[data-triumph-asked]', 'imp-triumph-gate.png');
      const home = gate.buttons.find(b => b.c === 'home' && !b.off) || gate.buttons.find(b => !b.off);
      if (home) { await page.evaluate(c => document.querySelector('[data-triumph-choice="' + c + '"]').click(), home.c); await page.waitForTimeout(800); gateAnswered = true; mark('triumph', 'voted, and answered at the gate in year ' + yr + ': ' + home.label); console.log('    answered: ' + home.label + ' · now: ' + (await page.evaluate(() => [...document.querySelectorAll('[data-triumph-home]')].map(e => e.innerText.replace(/\s+/g, ' ')).join(' | ')) || 'no line at the gate')); }
    }

    // 2. the ladder: a refusal for one who commands away from the capital, and the leave
    await senate('Ladder and courts');
    const lad = await page.evaluate(() => {
      const p = document.querySelector('.panel.cursus'); if (!p) return null;
      const away = [...p.querySelectorAll('[data-consul-away], [data-stand-away]')].map(e => { (e.closest('.rung-row') || e).setAttribute('data-imp-away', '1'); return e.innerText.replace(/\s+/g, ' '); });
      const plain = b => [...b.childNodes].filter(n => !(n.classList && n.classList.contains('ui-cost'))).map(n => n.textContent).join('').replace(/\s+/g, ' ').trim();   // a plate's words, without its cost
      const mine = [...p.querySelectorAll('button')].filter(b => /^Stand for/.test(b.innerText.trim())).map(b => plain(b) + ': ' + (b.disabled ? (b.title || '').split('\n').pop() : 'open'));
      const leave = p.querySelector('[data-absence-leave]'); const consul = [...p.querySelectorAll('.rung-row')].pop();
      return { away, mine, leave: leave ? leave.innerText.replace(/\s+/g, ' ') : '', next: ((p.querySelector('[data-next-election]') || {}).innerText || '').replace(/\s+/g, ' ').slice(0, 120), consul: consul ? consul.innerText.replace(/\s+/g, ' ') : '', text: p.innerText.replace(/\s+/g, ' ') };
    });
    if (lad) {
      console.log('  ladder (' + lint(lad.text) + '): ' + lad.next);
      console.log('    your own rungs: ' + lad.mine.join('; ')); console.log('    the consulship: ' + lad.consul.slice(0, 300));
      if (lad.mine.some(s => /away from the capital/.test(s))) lad.away.push(lad.mine.find(s => /away from the capital/.test(s)));
      if (lad.away.length) { console.log('    REFUSED FOR COMMANDING AWAY: ' + lad.away.join(' | ')); if (!awaySeen) { awaySeen = true; mark('absence', 'the refusal for commanding away is on the ladder in year ' + yr); await shotOf('[data-imp-away]', 'imp-ladder-away.png'); } }
      if (lad.leave) { console.log('    LEAVE: ' + lad.leave); if (!leaveHad) { leaveHad = true; mark('absence', 'leave to stand in absence carried, shown on the ladder in year ' + yr); await shotOf('.panel.cursus > [data-next-election]', 'imp-absence-leave.png', '.rung-row'); } }
    }

    // the leave is spent at the election it was given for; the ladder then says again to bring the fleet home or give the command to
    // another. Do as it says: give it to another of the house (a threat that waits for the command to be laid down then comes due).
    if (lad && lad.away.length && leaveHad && !lad.leave && !gaveCommand) {
      const fleet = (lad.away.join(' ').match(/(?:You|you) command (.+?) away from the capital/) || [])[1] || '';
      await closeWindows(); await rail('Military'); await page.waitForTimeout(700); await tab('Your fleets');
      // a fleet's admiral is changed from its orders menu (the fleet row's menu, then Admiral): a list of who may take it, each a .mil-pick
      // reading "Give <fleet> to <name> (tactics N, age)", with the refusal in its tooltip
      const menu = await page.evaluate(f => { const row = [...document.querySelectorAll('.screen.window .mil-fleet[data-fleet]')].find(r => f && r.innerText.includes(f)); if (!row) return false; const m = row.querySelector('.mf-menu-btn'); if (m && m.getAttribute('aria-expanded') !== 'true') m.click(); return true; }, fleet); await page.waitForTimeout(500);
      if (menu) { await page.evaluate(f => { const row = [...document.querySelectorAll('.screen.window .mil-fleet[data-fleet]')].find(r => r.innerText.includes(f)); const t = row && [...row.querySelectorAll('.mf-tg')].find(b => b.innerText.trim() === 'Admiral'); if (t && !t.classList.contains('on')) t.click(); }, fleet); await page.waitForTimeout(500); }
      const opts = await page.evaluate(f => [...document.querySelectorAll('.screen.window .mil-pick')].filter(b => !b.hasAttribute('data-make-captain')).map(b => ({ label: (([...b.querySelectorAll(':scope > span')].find(x => !x.className) || b).textContent || '').trim(), off: b.disabled, why: b.title })).filter(o => o.label.startsWith('Give ' + f + ' to')), fleet);
      console.log('  giving up the command of ' + (fleet || '(no fleet named)') + ': ' + (opts.length ? opts.map(o => o.label + (o.off ? ' (refused: ' + o.why + ')' : '')).join('; ').slice(0, 600) : 'no one to give it to'));
      const skill = o => parseInt((o.label.match(/tactics (\d+)/) || [0, 0])[1], 10);   // the most able of those who may take it
      const give = opts.filter(o => !o.off).sort((a, b) => skill(b) - skill(a))[0];
      if (give) {
        await page.evaluate(l => { const b = [...document.querySelectorAll('.screen.window .mil-pick')].find(x => (([...x.querySelectorAll(':scope > span')].find(y => !y.className) || x).textContent || '').trim() === l); if (b) b.click(); }, give.label); await page.waitForTimeout(800);
        gaveCommand = yr; console.log('    gave it: ' + give.label + ' · ' + (await toast() || 'no toast'));
        mark('absence', 'year ' + yr + ': the leave spent, the ladder again said to bring the fleet home or give the command to another; gave it to another');
        await senate('Ladder and courts'); const now = await page.evaluate(() => [...document.querySelectorAll('.panel.cursus [data-consul-away], .panel.cursus [data-stand-away]')].map(e => e.innerText.replace(/\s+/g, ' ')).join(' | '));
        console.log('    the ladder now: ' + (now || 'no refusal for commanding away') + ' · ' + await page.evaluate(() => { const r = [...document.querySelectorAll('.panel.cursus .rung-row')].pop(); return r ? r.innerText.replace(/\s+/g, ' ').slice(0, 200) : ''; }));
        const due = await page.evaluate(() => [...document.querySelectorAll('[data-threat-standing]')].map(e => e.innerText.replace(/\s+/g, ' ')).join(' | ')); if (due) console.log('    the threat now: ' + due);
      }
      await senate('Ladder and courts');
    }

    // 3. the courts: a charge against the powerful
    const pc = await page.evaluate(() => {
      const h = document.querySelector('[data-powerful]'); if (!h) return null; const items = []; let n = h.nextElementSibling; while (n && !n.matches('[data-bring], .ui-sec, [data-no-quarrel]') && !n.querySelector('[data-bring]')) { items.push(n); n = n.nextElementSibling; }
      return { text: [h, ...items].map(e => e.innerText.replace(/\s+/g, ' ')).join(' / '),
        charges: [...document.querySelectorAll('[data-powerful-charge]')].map(e => ({ id: e.getAttribute('data-powerful-charge'), text: e.innerText.replace(/\s+/g, ' '), answers: [...e.querySelectorAll('[data-charge-answer]')].map(b => ({ a: b.getAttribute('data-charge-answer'), label: b.innerText.replace(/\s+/g, ' ').trim(), off: b.disabled, why: b.title })) })),
        threats: [...document.querySelectorAll('[data-threat-standing]')].map(e => e.innerText.replace(/\s+/g, ' ')),
        generals: [...document.querySelectorAll('[data-threat-general]')].map(e => { const b = e.querySelector('button[data-charge-threat]'); return { id: e.getAttribute('data-threat-general'), text: e.innerText.replace(/\s+/g, ' '), off: !b || b.disabled, why: b ? b.title : '' }; }),
        exiles: [...document.querySelectorAll('[data-exile]')].map(e => { const b = e.querySelector('[data-recall-exile]'); return e.innerText.replace(/\s+/g, ' ') + (b ? (b.disabled ? ' (recall refused: ' + b.title + ')' : ' (recall open)') : ''); }),
        bring: [...document.querySelectorAll('.panel.courts .court-charge')].map(e => { const b = e.querySelector('button'); return { text: e.innerText.replace(/\s+/g, ' '), off: !b || b.disabled, why: b ? b.title : '' }; }) };
    });
    if (pc) {
      const busy = pc.charges.length || pc.threats.length || pc.generals.length || pc.exiles.length;
      console.log('  the powerful (' + lint(pc.text) + '): ' + (busy ? '' : pc.text.slice(0, 200)));
      pc.charges.forEach(c => { console.log('    CHARGE: ' + c.text.slice(0, 420)); c.answers.forEach(b => console.log('      [' + b.a + '] ' + b.label + (b.off ? ' (refused: ' + b.why + ')' : ' · ' + b.why.slice(0, 200)))); });
      pc.threats.forEach(t => console.log('    THREAT: ' + t.slice(0, 300)));
      pc.generals.forEach(g => console.log('    general to threaten: ' + g.text.slice(0, 220) + (g.off ? ' (refused: ' + g.why + ')' : '')));
      pc.exiles.forEach(x => console.log('    EXILE: ' + x.slice(0, 300)));
      const sig = pc.charges.map(c => c.text).concat(pc.threats, pc.exiles).join('|');
      if (busy && sig !== lastPowerful) { lastPowerful = sig; await shotOf('[data-powerful]', 'imp-powerful-y' + yr + '.png', '[data-bring], .ui-sec, [data-no-quarrel]'); }
      pc.charges.filter(c => /convicted|acquitted/.test(c.text)).forEach(c => mark('charge', 'year ' + yr + ': ' + c.text.split('. ')[0].slice(0, 120)));
      pc.threats.filter(t => /that you will be charged/.test(t) && !threatOnMe).forEach(t => { threatOnMe = true; mark('charge', 'year ' + yr + ', against you: ' + t.split('. ')[0].slice(0, 140)); });
      // a charge against one of the player's house: stand trial (or lay the command down after a verdict)
      for (const c of pc.charges) {
        const ans = c.answers.find(b => b.a === 'trial' && !b.off) || c.answers.find(b => b.a === 'lay_down' && !b.off);
        if (!ans || chargeAnswered === c.id + ans.a) continue;
        await page.evaluate(([id, a]) => document.querySelector('[data-powerful-charge="' + id + '"] [data-charge-answer="' + a + '"]').click(), [c.id, ans.a]); if (ans.a !== 'trial') await confirm(); await page.waitForTimeout(700);
        chargeAnswered = c.id + ans.a; chargeYear = yr; mark('charge', 'a charge against the house answered in year ' + yr + ': ' + ans.label);
        console.log('    answered "' + ans.label + '": ' + (await page.evaluate(id => { const e = document.querySelector('[data-powerful-charge="' + id + '"]'); return e ? e.innerText.replace(/\s+/g, ' ').slice(0, 300) : 'the charge is gone from the block'; }, c.id)));
        await shotOf('[data-powerful-charge="' + c.id + '"]', 'imp-charge-answered.png');
      }
                  // let it be known that a general will be charged, once
      const g = pc.generals.find(x => !x.off);
      if (g && !threatMade) {
        await page.evaluate(id => document.querySelector('[data-threat-general="' + id + '"] button[data-charge-threat]').click(), g.id); await confirm();
        threatMade = true; mark('charge', 'threatened a charge against ' + flat(g.text).split(' of ')[0] + ' in year ' + yr);
        console.log('    now: ' + (await page.evaluate(() => [...document.querySelectorAll('[data-threat-standing]')].map(e => e.innerText.replace(/\s+/g, ' ')).join(' | ')) || 'no threat line'));
        await shotOf('[data-powerful]', 'imp-threat.png', '[data-bring], .ui-sec, [data-no-quarrel]');
      }
      // bring a charge the record offers, and see whether it is one against the powerful
      const open = pc.bring.find(x => !x.off);
      if (pc.bring.length) console.log('    on the record: ' + pc.bring.length + ' · first: ' + pc.bring[0].text.slice(0, 220) + (pc.bring[0].off ? ' (refused: ' + pc.bring[0].why + ')' : ''));
      if (open && broughtCharges < 2) {
        const before = pc.charges.length;
        await page.evaluate(() => { const b = [...document.querySelectorAll('.panel.courts .court-charge button')].find(e => !e.disabled); if (b) b.click(); }); await page.waitForTimeout(800); broughtCharges++;
        const now = await page.evaluate(() => [...document.querySelectorAll('[data-powerful-charge]')].map(e => e.innerText.replace(/\s+/g, ' ').slice(0, 300)));
        console.log('    brought the charge: ' + open.text.slice(0, 200) + ' · ' + (now.length > before ? 'it is against one of the powerful, held a year: ' + now[now.length - 1] : 'an ordinary charge (not against one of the powerful), heard this year'));
        if (now.length > before) { mark('charge', 'brought a charge against one of the powerful in year ' + yr); await shotOf('[data-powerful]', 'imp-charge-brought.png', '[data-bring], .ui-sec, [data-no-quarrel]'); }
      }
    }

    // 4. the Caesar panel: the triumph block, a triumph asked for the house's own general
    const cz = await caesar();
    const czBtn = await page.evaluate(() => { const b = [...document.querySelectorAll('.panel.caesar button')].find(e => e.innerText.trim().startsWith('Ask the Senate for a triumph')); return b ? { off: b.disabled, why: (b.title || '').split('\n').pop() } : null; });
    const czLine = flat((cz.match(/The crews .*?count is \d+\./) || [''])[0]);
    console.log('  Caesar panel (' + lint(cz) + '): ' + (czLine || cz.slice(0, 200)) + ' · triumph: ' + (czBtn ? (czBtn.off ? 'refused: ' + czBtn.why : 'open') : 'no button (nobody of the house commands)'));
    if (czBtn) why.triumph = czBtn.off ? czBtn.why : 'open';
    pending = await myMotion();
    if (czBtn && !czBtn.off && !pending) {
      await caesar(); await page.evaluate(() => [...document.querySelectorAll('.panel.caesar button')].find(e => e.innerText.trim().startsWith('Ask the Senate for a triumph')).click()); await page.waitForTimeout(700);
      triumphMoved = yr; mark('triumph', 'moved for the house\'s own general in year ' + yr); console.log('  ASKED THE SENATE FOR A TRIUMPH'); await shotOf(await page.$('[data-caesar-general]') ? '[data-caesar-general]' : '.panel.caesar > .ui-lede', 'imp-caesar-asked.png', '.ui-sec, .mc-recall');
      pending = await myMotion();
    }

    // 5. one motion of the player's own a year at most, and one before the Senate at a time
    if (!pending) {
      if (awaySeen && !leaveHad && !leaveMoved) {
        const f = await form('Leave to stand in absence', house);
        if (f.err) console.log('  leave to stand in absence: ' + f.err);
        else {
          console.log('  leave to stand in absence (' + lint(f.text) + '): ' + f.text.slice(0, 400) + ' · Move it: ' + (f.off ? 'refused: ' + f.why : 'open'));
          f.placeholders.forEach(p => placeholderNote.add(p));
          await shotOf('[data-imp-form="Leave to stand in absence"]', 'imp-absence-form.png');
          if (!f.off && await moveIt('Leave to stand in absence')) { leaveMoved = yr; mark('absence', 'the refusal seen; leave to stand in absence moved in year ' + yr); const m = await myMotion(); console.log('    moved: ' + (m ? m.text.slice(0, 300) : 'NOT ON THE DOCKET (the button was open)')); if (m) await shotOf('.screen.window .motion.mine', 'imp-absence-motion.png'); pending = m; }
          else if (f.off) why.absence = f.why;
        }
      } else if (!warMoved && yr >= warYear) {
        const f = await form('War Beyond the Reach', 'The Free Cities of Thessar');
        if (f.err) console.log('  war on the Free Cities: ' + f.err);
        else {
          console.log('  war on the Free Cities (' + lint(f.text) + '): ' + f.text.slice(0, 400) + ' · Move it: ' + (f.off ? 'refused: ' + f.why : 'open'));
          f.placeholders.forEach(p => placeholderNote.add(p));
          await shotOf('[data-imp-form="War Beyond the Reach"]', 'imp-gold-form.png');
          if (!f.off && await moveIt('War Beyond the Reach')) {
            warMoved = yr; const m = await myMotion(); console.log('    moved: ' + (m ? m.text.slice(0, 400) : 'NOT ON THE DOCKET (the button was open)'));
            if (m) { await page.evaluate(() => { const g = document.querySelector('.screen.window .motion.mine [data-foreign-gold-motion]'); if (g) g.closest('.motion').setAttribute('data-imp-war', '1'); }); await shotOf('[data-imp-war]', 'imp-gold-block.png'); }
            pending = m;
          } else if (f.off) why.gold = 'the war could not be moved: ' + f.why;
        }
      }
    }
    // a triumph for another house's general, when the Senate would hear one and nothing of ours is before it
    if (!pending && yr - lastScan >= 4) {
      lastScan = yr; await senate('Move a motion');
      const watched = [...new Set([...(cz.split(/Names the Senate watches/i)[1] || '').matchAll(/of (House [A-Z][a-z]+) \(sway/g)].map(m => m[1]))];
      const houses = await page.evaluate(w => { const pr = [...document.querySelectorAll('.proposal')].find(p => (p.querySelector('b') || {}).innerText === 'Triumph'); return pr ? [...pr.querySelector('select').options].slice(1).map(o => o.text).filter(t => w.includes(t)) : []; }, watched.concat([house]));
      console.log('  the generals the Senate watches: ' + (watched.length ? watched.join(', ') : 'none of a house of the Reach'));
      const seen = {}; let open = '';
      for (const h of houses) { const f = await form('Triumph', h, true); if (f.err) continue; const k = f.off ? f.why.replace(/^[A-Z][a-z]+ [A-Z][a-z]+ (has|is)/, 'the general $1') : 'open'; seen[k] = (seen[k] || 0) + 1; if (!f.off) { open = h; break; } }
      console.log('  a triumph moved for each of those houses: ' + Object.entries(seen).map(([k, n]) => n + ' × ' + k).join('; ').slice(0, 600));
      if (open) {
        const f = await form('Triumph', open, true); console.log('  triumph for ' + open + ' (' + lint(f.text) + '): ' + f.text.slice(0, 300));
        await shotOf('[data-imp-form="Triumph"]', 'imp-triumph-form.png');
        if (await moveIt('Triumph')) { triumphMoved = yr; mark('triumph', 'moved for the general of ' + open + ' in year ' + yr); const m = await myMotion(); console.log('    moved: ' + (m ? m.text.slice(0, 300) : 'NOT ON THE DOCKET (the button was open)')); }
      } else if (houses.length) { const f = await form('Triumph', house, true); if (!f.err) { console.log('  triumph for ' + house + ': ' + (f.off ? 'refused: ' + f.why : 'open')); if (!reached.triumph) await shotOf('[data-imp-form="Triumph"]', 'imp-triumph-form.png'); } }
    }
    await endTurn();
  }

  // ---- after: the chronicle, and the screens once more ----
  const cz = await caesar(); console.log('\nCaesar panel at the end: ' + lint(cz) + ' · ' + cz.slice(0, 400));
  await senate('Ladder and courts'); await shotOf('[data-powerful]', 'imp-powerful-end.png', '[data-bring], .ui-sec, [data-no-quarrel]');
  await closeWindows(); await rail('Chronicle'); await page.waitForTimeout(800);
  const chron = await page.evaluate(() => (document.body.innerText || '').split('\n').map(t => t.trim()).filter(t => t.length > 40 && /factor|Free Cities|gold|triumph|in absence|leave to stand|exile|charges|let it be known|acquitted|convicted|the gate/i.test(t)).slice(0, 14));
  console.log('chronicle:\n   ' + (chron.length ? chron.map(t => t.slice(0, 240)).join('\n   ') : 'nothing of these'));

  console.log('\nWHAT WAS REACHED (year ' + await year() + ', ' + mins() + ' minutes):');
  const said = k => reached[k] ? 'REACHED · ' + reached[k].join(' · ') : null;
  console.log('  foreign gold: ' + (said('gold') || 'NOT REACHED: ' + (why.gold || (warMoved ? 'the war was moved in year ' + warMoved + ' but the Cities sent nothing' : 'the war was not moved'))));
  console.log('  standing in absence: ' + (said('absence') || 'NOT REACHED: ' + (why.absence || 'no refusal for commanding away was on the ladder')));
  console.log('  a charge against the powerful: ' + (said('charge') || 'NOT REACHED: nothing on the record against a general of another house, no charge to bring against one of the powerful, and none brought against the house'));
  console.log('  the triumph at the gate: ' + (said('triumph') || 'NOT REACHED: ' + (why.triumph || 'nobody of the house commands')));
  if (placeholderNote.size) console.log('  the forms\' placeholders carry an em dash: ' + [...placeholderNote].join(' | '));
  console.log('errors: ' + (errors.length ? errors.slice(0, 6).join(' | ') : 'none'));
  await browser.close();
})().catch(e => { console.error('FAILED ' + e.message.split('\n')[0]); process.exit(1); });
