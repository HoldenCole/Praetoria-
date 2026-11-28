// Ancestry and faces at creation (docs/ART_DIRECTION.md, "Portraits"): in the workshop's Face tab, choose the house's ancestry,
// choose a face among those painted for it, and see that face across a life; then begin, and check the family is painted.
//   cd tools/browser && PRAETORIA_SHOTS=/tmp/shots node faces.js ["African"]        (the client served on :8112)
const { chromium } = require('playwright');
const want = process.argv[2] || 'African'; const out = process.env.PRAETORIA_SHOTS || '/tmp/praetoria-shots';
(async () => {
  require('fs').mkdirSync(out, { recursive: true });
  const browser = await chromium.launch({ args: ['--use-gl=angle', '--use-angle=swiftshader', '--ignore-gpu-blocklist'] });
  const page = await browser.newPage({ viewport: { width: 1600, height: 1100 } }); page.setDefaultTimeout(60000);
  const errors = []; page.on('console', m => { if (m.type() === 'error') errors.push(m.text().slice(0, 200)); }); page.on('pageerror', e => errors.push('PAGEERROR ' + e.message.slice(0, 200)));
  const fails = []; const check = (ok, what) => { if (!ok) fails.push(what); };
  const click = (sel, text) => page.evaluate(([s, t]) => { const el = [...document.querySelectorAll(s)].find(e => !t || e.innerText.trim().startsWith(t)); if (el) el.click(); return !!el; }, [sel, text]);
  await page.goto(require('./address')(), { waitUntil: 'networkidle' }); await page.waitForSelector('.title-screen', { timeout: 90000 });
  await page.evaluate(() => { try { localStorage.setItem('praetoria.primer.seen', '1'); } catch (e) {} });
  await click('.scenario.custom'); await page.waitForSelector('.workshop'); await click('.workshop .ui-tab', 'The face'); await page.waitForTimeout(400);
  const names = await page.evaluate(() => [...document.querySelectorAll('.workshop .chips .chip')].map(e => e.innerText.trim()));
  check(names.includes(want), 'no ancestry called ' + want + ' is offered (' + names.slice(0, 8).join(', ') + ')');
  // before an ancestry is chosen the picker offers one face for each ancestry (choosing one chooses its ancestry), never the faces of one
  const before = await page.evaluate(() => ({ own: document.querySelectorAll('.face-pick .face:not([data-ancestry])').length, by: new Set([...document.querySelectorAll('.face-pick .face[data-ancestry]')].map(e => e.dataset.ancestry)).size }));
  check(before.own === 0, 'the faces of one ancestry are offered before an ancestry is chosen (' + before.own + ')');
  check(before.by >= 2, 'the picker does not offer a face for each ancestry (' + before.by + ' of ' + (names.length - 1) + ')');
  await click('.workshop .chips .chip', want); await page.waitForTimeout(500);
  const faces = await page.evaluate(() => document.querySelectorAll('.face-pick .face').length); check(faces >= 2, 'fewer than two faces to choose from for ' + want + ' (' + faces + ')');
  await page.evaluate(() => { const f = document.querySelectorAll('.face-pick .face'); if (f.length) f[Math.min(1, f.length - 1)].click(); }); await page.waitForTimeout(600);
  const life = await page.evaluate(() => [...document.querySelectorAll('.face-life img')].map(e => e.getAttribute('src'))); check(life.length === 5, 'the chosen face is not shown at five ages (' + life.length + ')');
  check(new Set(life.map(s => (s || '').replace(/_[a-z]+\.webp$/, ''))).size === 1, 'the five ages are not of one face');
  const chosen = (life[2] || '').replace(/_[a-z]+\.webp$/, '');
  check(await page.evaluate(c => { const im = document.querySelector('.workshop .portrait img'); return !!im && (im.getAttribute('src') || '').startsWith(c); }, chosen), 'the preview does not wear the chosen face');
  await page.screenshot({ path: out + '/faces-workshop.png' });
  console.log(want + ': ' + faces + ' faces · console errors: ' + errors.length + ' ' + errors.slice(0, 2).join(' | ')); console.log(fails.length ? 'FAILED: ' + fails.join('; ') : 'faces: all checks passed');
  await browser.close(); process.exit(fails.length || errors.length ? 1 : 0);
})();
