// Solar systems (docs/SYSTEMS.md, docs/BUILDINGS.md): picks the player's seat on the map and opens its System tab. Prints every place
// in it (the sun, each world and moon, open space) with its kind, its feature, its building slots and whether its site for a great
// work is free; opens places until one offers something that can be built now, and builds it; lays a level of infrastructure on the
// Infrastructure tab; then picks a system nobody holds and reads what settling it would take.
//
//   node systems.js ["Start title"] [size: small|medium|large|extra_large]
//
// Needs the build served locally (see README.md here). Screenshots go to PRAETORIA_SHOTS (default ./shots).
const { chromium } = require('playwright');
const title = process.argv[2] || 'The Old Lion'; const size = process.argv[3] || 'small';
const base = process.env.PRAETORIA_URL || 'http://localhost:8112/'; const shots = process.env.PRAETORIA_SHOTS || './shots';
require('fs').mkdirSync(shots, { recursive: true });
(async () => {
  const browser = await chromium.launch({ args: ['--use-gl=angle', '--use-angle=swiftshader', '--ignore-gpu-blocklist'] });
  const page = await browser.newPage({ viewport: { width: 1600, height: 1000 } }); page.setDefaultTimeout(60000);
  const errors = []; page.on('console', m => { if (m.type() === 'error') errors.push(m.text().slice(0, 300)); }); page.on('pageerror', e => errors.push('PAGEERROR ' + e.message.slice(0, 300)));
  await page.goto(base, { waitUntil: 'networkidle' }); await page.waitForSelector('.title-screen', { timeout: 90000 });
  await page.evaluate(() => { try { localStorage.setItem('praetoria.primer.seen', '1'); } catch (e) {} });
  await page.click('.scenario:has-text("' + title + '")');
  await page.evaluate(v => { const s = document.querySelector('[data-setting=size]'); if (s) { s.value = v; s.dispatchEvent(new Event('change', { bubbles: true })); } }, size);
  await page.waitForTimeout(300); await page.click('button.begin'); await page.waitForSelector('.gs-shell', { timeout: 90000 }); await page.waitForTimeout(2500);
  let failed = 0; const check = (ok, what) => { console.log((ok ? 'ok   ' : 'FAIL ') + what); if (!ok) failed++; };
  const pick = async name => { const id = await page.evaluate(n => window.praetoriaSpace.select(n), name); await page.waitForTimeout(1800); return id; };
  // the seat: the first of the player's holdings in the outliner
  const seat = await page.evaluate(() => { const g = [...window.praetoriaSpace.galaxies.values()][0]; const s = g.model.systems.find(x => x.isPlayer); return s ? s.name : null; });
  check(!!seat, 'the player holds a system: ' + seat);
  await pick(seat);
  const bodies = await page.evaluate(() => [...document.querySelectorAll('.sysp-body')].map(b => b.querySelector('.sysp-name b').innerText + ' [' + b.querySelector('.sysp-name .muted').innerText + ']' + [...b.querySelectorAll('.pill.feat')].map(p => ' *' + p.innerText).join('') + ' slots ' + b.querySelectorAll('.sysp-slots i').length + (b.querySelector('.sysp-site') ? ' +site' : '')));
  check(bodies.length >= 5, bodies.length + ' bodies listed, the sun among them'); bodies.forEach(b => console.log('       ' + b));
  check(bodies.some(b => b.includes('the sun') && b.includes('slots 0 +site')), 'the sun has no building slot, and a site for a great work');
  check(bodies.some(b => b.includes('Open space')), 'open space is a place to build, tied to no world'); check(bodies.some(b => b.includes('*')), 'a body has a feature no other system need have');
  check(bodies.some(b => b.includes('the home world') && !b.includes('slots 0')), "the system's own world (the home world) shows its holding's slots");
  await page.screenshot({ path: shots + '/system-seat.png' });
  // open each body until one offers something that can be built now, and build it
  const n = bodies.length; let builtName = null;
  for (let i = 0; i < n && !builtName; i++) {
    await page.evaluate(i => { const rows = [...document.querySelectorAll('.sysp-row')]; rows[i].click(); }, i); await page.waitForTimeout(500);
    const offer = await page.evaluate(() => { const w = [...document.querySelectorAll('.sysp-open .sysp-work')].find(x => { const b = x.querySelector('button.opt'); return b && !b.disabled; }); return w ? w.querySelector('.sw-head b').innerText : null; });
    if (i === 2) await page.screenshot({ path: shots + '/system-body-open.png' });
    const pics = await page.evaluate(() => [...document.querySelectorAll('.sysp-open .sw-art')].filter(x => x.complete && x.naturalWidth > 0).length); if (pics > 0 && !global.sawArt) { global.sawArt = pics; }
    if (offer) { await page.evaluate(() => { const w = [...document.querySelectorAll('.sysp-open .sysp-work')].find(x => { const b = x.querySelector('button.opt'); return b && !b.disabled; }); w.querySelector('button.opt').click(); }); await page.waitForTimeout(1200); builtName = offer; }
    else await page.evaluate(i => { const rows = [...document.querySelectorAll('.sysp-row')]; rows[i].click(); }, i);
  }
  check(!!builtName, 'something could be built on a body of the seat: ' + builtName);
  const standing = await page.evaluate(() => [...document.querySelectorAll('.sysp-built .built, .sysp-work.stands .sw-head b')].map(x => x.innerText));
  check(standing.some(x => builtName && x.startsWith(builtName)), 'it shows on its body: ' + standing.join(', '));
  await page.screenshot({ path: shots + '/system-built.png' });
  // infrastructure: the system's own, a level at a time
  await page.evaluate(() => { const t = [...document.querySelectorAll('.sys-tabs .rung, .sys-tabs .ui-tab')].find(x => x.innerText.trim().toLowerCase().startsWith('infrastructure')); if (t) t.click(); }); await page.waitForTimeout(700);
  const tracks = await page.evaluate(() => [...document.querySelectorAll('.sysp-work.infra')].map(x => x.querySelector('.sw-head b').innerText + ' ' + x.querySelectorAll('.lv i.on').length + '/' + x.querySelectorAll('.lv i').length));
  check(tracks.length >= 4, 'infrastructure tracks: ' + tracks.join(', '));
  const laidOk = await page.evaluate(() => { const b = [...document.querySelectorAll('.sysp-work.infra button.opt')].find(x => !x.disabled); if (b) { b.click(); return true; } return false; }); await page.waitForTimeout(1000);
  const after = await page.evaluate(() => [...document.querySelectorAll('.sysp-work.infra')].map(x => x.querySelectorAll('.lv i.on').length + x.querySelectorAll('.lv i.up').length).reduce((a, b) => a + b, 0));
  check(laidOk && after >= 1, 'a level of infrastructure was laid or begun');
  await page.screenshot({ path: shots + '/system-infrastructure.png' });
  console.log('       pictures shown beside what can be built: ' + (global.sawArt || 0) + ' (none until tools/art/works.py has been run and packed)');
  // a system nobody holds
  const empty = await page.evaluate(() => { const g = [...window.praetoriaSpace.galaxies.values()][0]; const s = g.model.systems.find(x => !x.ownerId && x.ring !== 'beyond'); return s ? s.name : null; });
  if (empty) { await pick(empty); const settle = await page.evaluate(() => { const b = document.querySelector('.sysp-settle'); return b ? b.innerText.replace(/\s+/g, ' ') : null; }); check(!!settle && settle.includes('colony'), 'an empty system (' + empty + ') says what settling it takes: ' + (settle || '').slice(0, 160)); await page.screenshot({ path: shots + '/system-empty.png' }); }
  else console.log('       (no system is empty on this map)');
  console.log('errors: ' + errors.length + (errors.length ? ' ' + errors.slice(0, 3).join(' | ') : ''));
  console.log(failed === 0 && errors.length === 0 ? 'systems: all checks passed' : 'systems: ' + failed + ' check(s) failed');
  await browser.close();
})();
