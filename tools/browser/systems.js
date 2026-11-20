// Solar systems (docs/SYSTEMS.md): picks the player's seat on the map, opens its System tab, opens a body and builds the first work
// that can be built there; then picks a system nobody holds and reads what settling it would take. Prints every body of the seat with
// its kind, its feature and its room, and says what was built.
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
  const bodies = await page.evaluate(() => [...document.querySelectorAll('.sysp-body')].map(b => b.querySelector('.sysp-name b').innerText + ' [' + b.querySelector('.sysp-name .muted').innerText + ']' + [...b.querySelectorAll('.pill.feat')].map(p => ' *' + p.innerText).join('') + ' room ' + b.querySelectorAll('.sysp-slots i').length));
  check(bodies.length >= 5, bodies.length + ' bodies listed, the sun among them'); bodies.forEach(b => console.log('       ' + b));
  check(bodies.some(b => b.includes('the sun')), 'the sun is a place of its own'); check(bodies.some(b => b.includes('*')), 'a body has a feature no other system need have');
  await page.screenshot({ path: shots + '/system-seat.png' });
  // open each body until one offers something that can be built now, and build it
  const n = bodies.length; let builtName = null;
  for (let i = 0; i < n && !builtName; i++) {
    await page.evaluate(i => { const rows = [...document.querySelectorAll('.sysp-row')]; rows[i].click(); }, i); await page.waitForTimeout(500);
    const offer = await page.evaluate(() => { const w = [...document.querySelectorAll('.sysp-open .sysp-work')].find(x => { const b = x.querySelector('button.opt'); return b && !b.disabled; }); return w ? w.querySelector('.sw-head b').innerText : null; });
    if (i === 1) await page.screenshot({ path: shots + '/system-body-open.png' });
    if (offer) { await page.evaluate(() => { const w = [...document.querySelectorAll('.sysp-open .sysp-work')].find(x => { const b = x.querySelector('button.opt'); return b && !b.disabled; }); w.querySelector('button.opt').click(); }); await page.waitForTimeout(1200); builtName = offer; }
    else await page.evaluate(i => { const rows = [...document.querySelectorAll('.sysp-row')]; rows[i].click(); }, i);
  }
  check(!!builtName, 'something could be built on a body of the seat: ' + builtName);
  const standing = await page.evaluate(() => [...document.querySelectorAll('.sysp-built .built')].map(x => x.innerText));
  check(standing.some(x => builtName && x.startsWith(builtName)), 'it shows on its body: ' + standing.join(', '));
  await page.screenshot({ path: shots + '/system-built.png' });
  // a system nobody holds
  const empty = await page.evaluate(() => { const g = [...window.praetoriaSpace.galaxies.values()][0]; const s = g.model.systems.find(x => !x.ownerId && x.ring !== 'beyond'); return s ? s.name : null; });
  if (empty) { await pick(empty); const settle = await page.evaluate(() => { const b = document.querySelector('.sysp-settle'); return b ? b.innerText.replace(/\s+/g, ' ') : null; }); check(!!settle && settle.includes('colony'), 'an empty system (' + empty + ') says what settling it takes: ' + (settle || '').slice(0, 160)); await page.screenshot({ path: shots + '/system-empty.png' }); }
  else console.log('       (no system is empty on this map)');
  console.log('errors: ' + errors.length + (errors.length ? ' ' + errors.slice(0, 3).join(' | ') : ''));
  console.log(failed === 0 && errors.length === 0 ? 'systems: all checks passed' : 'systems: ' + failed + ' check(s) failed');
  await browser.close();
})();
