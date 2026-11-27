// Whose war it is (docs/BEYOND.md, "Whose war"): the setting for the powers beyond on the new-game screen; then, in a game, the orders on a
// nation's card on the Military screen ("Make war on your own account", raid or take and hold, keep or make a client), the line that says
// whose war it is and what a peace would be now, and the second setting (how often they attack).
// Last, the mark for a conquered world on the Galaxy screen. No world is conquered at the start of a game, so that picture is staged:
// the script hands the map its own model back with two worlds marked, to show how the mark is drawn. It says so in what it prints.
//
//   node whosewar.js ["Start title"]
//
// Needs the build served locally (see README.md here). Screenshots go to PRAETORIA_SHOTS (default ./shots).
const { chromium } = require('playwright');
const title = process.argv[2] || 'The Old Lion';
const base = process.env.PRAETORIA_URL || 'http://localhost:8112/'; const shots = process.env.PRAETORIA_SHOTS || './shots';
require('fs').mkdirSync(shots, { recursive: true });
(async () => {
  const browser = await chromium.launch({ args: ['--use-gl=angle', '--use-angle=swiftshader', '--ignore-gpu-blocklist'] });
  const page = await browser.newPage({ viewport: { width: 1600, height: 1100 }, deviceScaleFactor: 2 }); page.setDefaultTimeout(60000);
  const errors = []; page.on('console', m => { if (m.type() === 'error') errors.push(m.text().slice(0, 300)); }); page.on('pageerror', e => errors.push('PAGEERROR ' + e.message.slice(0, 300)));
  await page.goto(base, { waitUntil: 'networkidle' }); await page.waitForSelector('.title-screen', { timeout: 90000 });
  await page.evaluate(() => { try { localStorage.setItem('praetoria.primer.seen', '1'); } catch (e) {} });
  await page.click('.scenario:has-text("' + title + '")');
  const setting = await page.evaluate(() => { const l = [...document.querySelectorAll('label')].find(x => /the powers beyond/i.test(x.innerText)); return l ? [...l.querySelectorAll('option')].map(o => o.innerText.trim()).join(' | ') : null; });
  console.log('new-game setting, the powers beyond: ' + (setting || 'NOT SHOWN'));
  const often = await page.evaluate(() => { const l = [...document.querySelectorAll('label')].find(x => /how often they attack/i.test(x.innerText)); return l ? [...l.querySelectorAll('option')].map(o => o.innerText.trim()).join(' | ') : null; });
  console.log('new-game setting, how often they attack: ' + (often || 'NOT SHOWN'));
  const lab = await page.evaluateHandle(() => [...document.querySelectorAll('label')].find(x => /the powers beyond/i.test(x.innerText)) || null);
  if (lab.asElement()) { await lab.asElement().scrollIntoViewIfNeeded(); await page.screenshot({ path: shots + '/whosewar-setting.png' }); }
  await page.click('button.begin'); await page.waitForSelector('.gs-shell', { timeout: 90000 });
  for (let i = 0; i < 10; i++) { const had = await page.evaluate(() => { const c = document.querySelector('.win-close') || document.querySelector('.overlay .btn.primary'); if (c) c.click(); return !!c; }); if (!had) break; await page.waitForTimeout(300); }
  // keep the map's model, to stage the conquered mark later
  await page.evaluate(() => { const s = window.praetoriaSpace; const orig = s.setModel.bind(s); window.__setModel = orig; s.setModel = (id, json) => { window.__last = { id, json }; return orig(id, json); }; });
  const rail = async name => { await page.evaluate(n => { const el = [...document.querySelectorAll('.rail-btn')].find(e => new RegExp('^' + n + '$', 'i').test(e.innerText.trim())); if (el) el.click(); }, name); await page.waitForTimeout(700); };
  await rail('military');
  await page.evaluate(() => { const t = (document.querySelector('.screen.window .ui-tab[data-tab="abroad"]') || [...document.querySelectorAll('.ex-tabs .rung')].find(e => /beyond the reach/i.test(e.innerText))); if (t) t.click(); }); await page.waitForTimeout(600);
  const card = async name => page.evaluate(n => { const el = [...document.querySelectorAll('.panel.abroad .nation')].find(x => new RegExp(n, 'i').test(x.innerText)); return el ? el.innerText.replace(/\s+/g, ' ') : null; }, name);
  const press = async (name, label) => page.evaluate(([n, l]) => { const el = [...document.querySelectorAll('.panel.abroad .nation')].find(x => new RegExp(n, 'i').test(x.innerText)); const b = el && [...el.querySelectorAll('button')].find(x => new RegExp(l, 'i').test(x.innerText)); if (b && !b.disabled) { b.click(); return true; } return b ? 'disabled: ' + b.title : false; }, [name, label]);
  let c = await card('hollow crown'); console.log('orders offered on the Hollow Crown: ' + ((c || '').match(/Make war on your own account|Now they take and hold: Order your fleets to raid|Move for peace|Move for war/g) || []).join(' · '));
  console.log('order the fleets to raid: ' + await press('hollow crown', 'Order your fleets to raid')); await page.waitForTimeout(500);
  c = await card('hollow crown'); console.log('  the order now reads: ' + ((c || '').match(/Now they raid: Order your fleets to take and hold/) || ['NOT CHANGED'])[0]);
  console.log('make war on your own account: ' + await press('hollow crown', 'Make war on your own account')); await page.waitForTimeout(700);
  c = await card('hollow crown'); console.log('  whose war: ' + ((c || '').match(/Whose war:? [^.]*/i) || ['NOT SHOWN'])[0].trim());
  console.log('  at war: ' + /at war/i.test(c || '') + ' · the Senate may be moved for peace: ' + /Move for peace/.test(c || ''));
  console.log('  the war score and what it buys: ' + ((c || '').match(/War score [^)]*\)\. Negotiated now: [^.]*\.( At [^.]*\.)?/) || ['NOT SHOWN'])[0]);
  console.log('  your own peace, offered on the card: ' + ((c || '').match(/Make your own peace: [A-Za-z ]+/g) || ['NOT SHOWN']).join(' · '));
  console.log('  asking for tribute at this score: ' + await press('hollow crown', 'They pay tribute'));
  console.log('say you would make it a client: ' + await press('hollow crown', 'say you would make it a client')); await page.waitForTimeout(500);
  c = await card('hollow crown'); console.log('  your word now reads: ' + ((c || '').match(/Beaten decisively, you would make it a client/) || ['NOT CHANGED'])[0]);
  const hint = await card('vashti'); console.log('the Khanates\' card says how often a Great Khan comes: ' + ((hint || '').match(/Under a Great Khan[^.]*\./) || ['NOT SHOWN'])[0]);
  const el = await page.$('.panel.abroad'); if (el) { await el.scrollIntoViewIfNeeded(); await el.screenshot({ path: shots + '/whosewar-card.png' }); }
  // the Galaxy screen, with two worlds marked as conquered (staged: see the note at the top)
  await page.evaluate(() => { const el = document.querySelector('[data-close-window]'); if (el) el.click(); }); await page.waitForTimeout(2500);   // back to the map: the docked window's close (the rail has no Galaxy socket)
  const staged = await page.evaluate(() => {
    if (!window.__last) return 'the map gave no model';
    const m = JSON.parse(window.__last.json); const khan = m.systems.find(s => /khal/i.test(s.id)); const rim = m.systems.find(s => s.id === 'the_veil') || m.systems.find(s => s.ring === 'rim' && s.ownerId);
    const steppe = m.systems.find(s => s.id === 'the_long_grass'); if (!khan || !rim || !steppe) return 'systems not found';
    const was = rim.ownerName; rim.conquered = 'occupied'; rim.accent = khan.accent; rim.ownerId = khan.ownerId; rim.ownerName = khan.ownerName;
    const mine = m.systems.find(s => s.isPlayer); steppe.conquered = 'held'; if (mine) { steppe.accent = mine.accent; steppe.ownerId = mine.ownerId; steppe.ownerName = mine.ownerName; }
    window.__setModel(window.__last.id, JSON.stringify(m)); window.praetoriaSpace.focus(window.__last.id, (rim.x + steppe.x) / 2, (rim.y + steppe.y) / 2);
    return 'STAGED for the picture: ' + rim.name + ' (of ' + was + ') drawn as taken by ' + khan.ownerName + ', and ' + steppe.name + ' drawn as held by ' + (mine ? mine.ownerName : 'a house');
  });
  console.log(staged); await page.waitForTimeout(1500);
  for (let i = 0; i < 1; i++) { await page.evaluate(() => { const b = document.querySelector('.map-zoom button, button[title="Zoom in"]'); if (b) b.click(); }); await page.waitForTimeout(400); }
  await page.waitForTimeout(2000);
  console.log('legend names the mark: ' + await page.evaluate(() => /conquered/i.test(document.querySelector('.legend')?.innerText || '')));
  await page.screenshot({ path: shots + '/whosewar-map.png', timeout: 240000 });
  console.log('wider than the screen: ' + await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth + 2));
  console.log('errors: ' + errors.length + (errors.length ? ' · ' + errors.slice(0, 3).join(' | ') : '')); await browser.close();
})();
