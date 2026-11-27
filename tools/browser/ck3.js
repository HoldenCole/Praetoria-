// Drives the three ideas taken from the Crusader Kings list (docs/IDEAS_FROM_MODS.md 1 to 3): a command by service on the Military
// window (offers the player's service where it could be taken), the legates block under the Senate's offices, striking a grown
// relative from the line on the Dynasty window, then laying the house down so the heir takes it up: the succession screen's friends
// and enemies handed down, a profile that itemises them, and the suit of the undutiful will on the Senate's courts if one is brought.
//
//   node ck3.js ["Start title"] [years to play] [lay]
//
// With "lay" the head lays the house down at once; otherwise the game is played on until the head dies, since only a head who dies
// in the seat leaves a will a child struck out can sue over.
//
// Needs the build served locally (see README.md here). Screenshots go to PRAETORIA_SHOTS (default ./shots).
const { chromium } = require('playwright');
const title = process.argv[2] || 'The Old Lion'; const years = parseInt(process.argv[3] || '10', 10); const layDown = process.argv[4] === 'lay';
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
  const tab = async name => { await page.waitForTimeout(300); await page.evaluate(n => { const t = [...document.querySelectorAll('.screen.window .rung, .cw-tabs .rung, .screen.window .ui-tabs .ui-tab')].find(e => e.innerText.toLowerCase().startsWith(n.toLowerCase())); if (t) t.click(); }, name); await page.waitForTimeout(500); };
  const closeWindows = async () => { for (let i = 0; i < 8; i++) { const had = await page.evaluate(() => { const c = document.querySelector('.win-close') || document.querySelector('.profile-x') || document.querySelector('.overlay .btn.primary'); if (c) c.click(); return !!c; }); if (!had) break; await page.waitForTimeout(150); } };
  const confirm = async () => { await page.waitForTimeout(300); const said = await page.evaluate(() => { const b = document.querySelector('[data-confirm="yes"]'); if (!b) return 'no question was asked'; const q = (b.closest('.confirm-box') || {}).innerText || ''; b.click(); return q.replace(/\s+/g, ' ').slice(0, 400); }); console.log('  asked: ' + said); await page.waitForTimeout(600); const out = await page.evaluate(() => { const o = document.querySelector('[data-outcome-ok]'); const t = o ? (o.closest('.confirm-box') || {}).innerText || '' : ''; if (o) o.click(); return t.replace(/\s+/g, ' ').slice(0, 300); }); if (out) console.log('  outcome: ' + out); await page.waitForTimeout(300); };
  const turnNo = () => page.evaluate(() => { const d = document.querySelector('[data-turn]'); if (d) return parseInt(d.dataset.turn, 10); const m = document.body.innerText.match(/Turn (\d+)/); return m ? parseInt(m[1], 10) : -1; });
  let named = [];
  const succession = async label => {   // the succession screen, if one is up: what it hands down, a picture, and on
    const s = await page.evaluate(() => { const e = document.querySelector('.epilogue.succession'); if (!e) return null; const f = e.querySelector('[data-inherited-friends]'), n = e.querySelector('[data-inherited-enemies]'); return { friends: f ? f.innerText.replace(/\s+/g, ' ') : '', enemies: n ? n.innerText.replace(/\s+/g, ' ') : '', all: e.innerText.replace(/\s+/g, ' ').slice(0, 300) }; });
    if (!s) return false;
    named = [...(s.friends + ' ' + s.enemies).matchAll(/([A-Z][a-z]+ [A-Z][a-z]+) of (House [A-Z][a-z]+)/g)].map(m => ({ who: m[1], house: m[2] }));
    console.log('succession (' + label + '): ' + suspicious(s.all) + ' · ' + s.all.slice(0, 160)); console.log('  friends handed down: ' + (s.friends || 'none')); console.log('  enemies handed down: ' + (s.enemies || 'none'));
    const el = await page.$('.epilogue.succession'); if (el) { await el.screenshot({ path: shots + '/ck3-succession.png', timeout: 180000 }); console.log('saved ck3-succession.png'); }
    await page.evaluate(() => { const b = [...document.querySelectorAll('.epilogue.succession button')].find(x => x.innerText.startsWith('Take up the name')); if (b) b.click(); }); await page.waitForTimeout(500);
    return true;
  };
  const endTurn = async () => { await closeWindows(); const before = await turnNo(); await page.evaluate(() => { const b = document.querySelector('button.endturn'); if (b) b.click(); }); for (let i = 0; i < 800; i++) { if (await turnNo().catch(() => -1) > before) break; if (await page.$('.epilogue.succession')) break; await page.waitForTimeout(50); } await page.waitForTimeout(300); const had = await succession('in play'); await closeWindows(); return had; };
  const text = sel => page.evaluate(s => { const e = document.querySelector(s); return e ? e.innerText.replace(/\s+/g, ' ') : 'NOT FOUND: ' + s; }, sel);
  const shotOf = async (sel, name) => { const el = await page.$(sel); if (!el) { console.log('no ' + sel + ' to picture'); return; } await el.scrollIntoViewIfNeeded(); await el.screenshot({ path: shots + '/' + name, timeout: 180000 }); console.log('saved ' + name); };
  const suspicious = t => /NaN|undefined|Exception|@\(|\(s\)|House House|—/.test(t) ? 'SUSPICIOUS TEXT' : 'ok';
  await closeWindows();

  // 1. a command by service, on the Military window
  await rail('Military'); await page.waitForTimeout(800); await tab('A command by service');
  const panel = await text('.screen.window'); console.log('service tab: ' + suspicious(panel) + ' · ' + panel.slice(0, 200));
  const seek = await page.evaluate(() => [...document.querySelectorAll('[data-seek-command]')].map(b => b.innerText.trim() + ' to ' + b.getAttribute('data-seek-command') + (b.disabled ? ' (no: ' + (b.title || '').slice(0, 90) + ')' : ' (yes)')));
  console.log('offers of service: ' + (seek.length ? seek.join('; ') : 'none: ' + await text('[data-service-none="takers"]')));
  await shotOf('.screen.window', 'ck3-service.png');
  const sought = await page.evaluate(() => { const b = [...document.querySelectorAll('[data-seek-command]')].find(e => !e.disabled); if (b) { b.click(); return b.getAttribute('data-seek-command'); } return ''; });
  if (sought) { await page.waitForTimeout(700); await confirm(); console.log('offered service to ' + sought + ': held now: ' + await page.evaluate(() => [...document.querySelectorAll('[data-service-held]')].map(e => e.innerText.replace(/\s+/g, ' ').slice(0, 200)).join(' | ') || 'none')); await shotOf('.screen.window', 'ck3-service-taken.png'); }
  await closeWindows();

  // 2. naming a legate, under the Senate's offices
  await rail('Senate'); await page.waitForTimeout(800); await tab('Offices and parties');
  const legates = await page.evaluate(() => [...document.querySelectorAll('[data-name-legate]')].map(b => b.getAttribute('data-name-legate') + (b.disabled ? ' (no: ' + (b.title || '').slice(0, 80) + ')' : ' (yes)')));
  console.log('legates you could name: ' + (legates.length ? legates.join('; ') : 'none (you hold no office with a fleet under it)'));
  if (legates.length) { const el = await page.evaluateHandle(() => { const b = document.querySelector('[data-name-legate]'); return b ? b.closest('.panel') || b.parentElement : null; }); if (el && el.asElement()) { await el.asElement().screenshot({ path: shots + '/ck3-legates.png', timeout: 180000 }); console.log('saved ck3-legates.png'); } }
  await closeWindows();

  // 3. strike a grown relative from the line, on the Dynasty window
  await rail('Dynasty'); await page.waitForTimeout(900);
  const nodes = await page.$$('.tree .tn:not(.me):not(.dead)'); let struck = ''; const can = [];
  for (let k = 0; k < nodes.length; k++) {
    await nodes[k].click(); await page.waitForTimeout(350);
    const b = await page.evaluate(() => { const s = document.querySelector('[data-strike]'); return s ? { id: s.getAttribute('data-strike'), ok: !s.disabled, tip: s.title || '', label: s.innerText.trim() } : null; });
    if (!b) continue;
    console.log('  ' + b.id + ': ' + b.label + (b.ok ? '' : ' (refused)') + ' · ' + b.tip.replace(/\s+/g, ' ').slice(0, 260));
    if (b.ok) can.push({ k, child: !/not your child/.test(b.tip), id: b.id });
  }
  const pick = can.find(x => x.child) || can[0];   // a child of the head, if one can be struck: only a child may sue over the will
  if (pick) { await nodes[pick.k].click(); await page.waitForTimeout(350); struck = pick.id; console.log('striking ' + struck + (pick.child ? ', a child of the head' : '')); await page.evaluate(() => document.querySelector('[data-strike]').click()); await confirm(); }
  if (struck) {
    await page.waitForTimeout(400); await shotOf('[data-disown="' + struck + '"]', 'ck3-struck-buttons.png');
    console.log('struck list: ' + await text('[data-struck-list]')); console.log('heir now: ' + await page.evaluate(() => { const h = document.querySelector('.person.heir'); return h ? h.innerText.replace(/\s+/g, ' ').slice(0, 120) : 'no heir shown'; }));
    await shotOf('.screen.window', 'ck3-dynasty-struck.png');
  } else console.log('nobody could be struck from the line');
  await closeWindows();

  let profiled = false;
  const profileCheck = async () => {   // a profile that itemises what was handed down, while it is fresh
    if (profiled) return; profiled = true;
    await rail('Houses'); await page.waitForTimeout(900); let found = false;
    for (const n of named) {
      if (found) break;
      await page.evaluate(h => { const x = [...document.querySelectorAll('.standings tbody tr')].find(r => r.innerText.includes(h)); if (x) x.click(); }, n.house); await page.waitForTimeout(600);
      const opened = await page.evaluate(who => { const el = [...document.querySelectorAll('.portrait.opens')].find(e => (e.getAttribute('title') || '').startsWith(who)); if (el) el.click(); return !!el; }, n.who);
      if (!opened) { console.log('  no face of ' + n.who + ' to open on ' + n.house + "'s page"); continue; }
      await page.waitForTimeout(600);
      const why = await page.evaluate(() => [...document.querySelectorAll('.profile [data-inherited-regard]')].map(e => e.getAttribute('data-inherited-regard') + ': ' + e.innerText.replace(/\s+/g, ' ')));
      if (why.length) { found = true; console.log('profile of ' + n.who + ': ' + suspicious(why.join(' ')) + ' · ' + why.join(' | ')); await shotOf('.profile', 'ck3-profile-inherited.png'); }
      else console.log('  the profile of ' + n.who + ' shows no line handed down');
      await page.evaluate(() => { const x = document.querySelector('.profile-x'); if (x) x.click(); }); await page.waitForTimeout(200);
    }
    if (!found) console.log('no profile of the first houses\' people shows a regard handed down');
    await closeWindows();
  };
  // 4. lay the house down, so the heir takes it up: what the old head's friends and enemies now think of them
  await rail('You'); await page.waitForTimeout(500); await tab('A life');
  const laid = layDown && await page.evaluate(() => { const b = [...document.querySelectorAll('.panel.life button')].find(e => !e.disabled && e.innerText.trim().startsWith('Step down')); if (b) b.click(); return !!b; });
  let handed = false;
  if (laid) { await page.waitForTimeout(400); await page.evaluate(() => { const b = [...document.querySelectorAll('button')].find(e => !e.disabled && e.innerText.trim().startsWith('Yes, lay it down')); if (b) b.click(); }); await page.waitForTimeout(1200); handed = await succession('the house laid down'); if (handed) await profileCheck(); }
  else console.log(layDown ? 'the head may not lay the house down yet: playing on until the seat passes' : 'playing on until the seat passes');
  let suitSeen = false;
  for (let y = 0; y < years; y++) {
    if (await endTurn()) { handed = true; await profileCheck(); }
    if (y % 2 === 1 || y === years - 1) {
      await rail('Senate'); await page.waitForTimeout(600); await tab('Ladder and courts');
      const suits = await page.evaluate(() => [...document.querySelectorAll('[data-undutiful-will]')].map(e => e.innerText.replace(/\s+/g, ' ').slice(0, 300)));
      if (suits.length && !suitSeen && handed) { suitSeen = true; console.log('year ' + await turnNo() + ', the suit of the undutiful will: ' + suspicious(suits.join(' ')) + ' · ' + suits.join(' | ')); await shotOf('[data-undutiful-will]', 'ck3-will-suit.png'); }
      await closeWindows();
    }
  }
  if (!suitSeen) console.log('no suit of the undutiful will was brought in ' + years + ' years (one is brought only when the head who struck a child out dies)');

  if (!handed) console.log('the seat did not pass in these years');
  console.log('errors: ' + (errors.length ? errors.slice(0, 6).join(' | ') : 'none'));
  await browser.close();
})().catch(e => { console.error('FAILED ' + e.message.split('\n')[0]); process.exit(1); });
