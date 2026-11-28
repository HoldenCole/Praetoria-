// Guided or sandbox (docs/GOALS.md, "Guided or sandbox"): the aim on the map can be turned off. Checks, in a browser:
// - the new-saga screen offers Guidance, Guided by default, and a saga begun guided shows its aim on the map;
// - the aim's close puts it away, says how to have it back, and keeps the choice for the browser (Options shows Sandbox);
// - Guided in Options brings the aim back, and Sandbox there takes it away again;
// - a newcomer's saga begun as a sandbox has no first steps pinned, and one begun guided has them.
// The warning a sandbox still shows before ruin is checked in tests/GoalTests.cs (a year-one treasury is never short enough here).
//
//   node aim.js [shots]          the published game must be served on :8112 (or PRAETORIA_URL)
// Exits 1 on a failed check or a console error.
const { chromium } = require('playwright');
const shots = process.argv[2] || process.env.PRAETORIA_SHOTS || './shots';
const base = require('./address')();
require('fs').mkdirSync(shots, { recursive: true });
let failed = 0;
const check = (ok, what) => { console.log((ok ? 'ok    ' : 'FAIL  ') + what); if (!ok) failed++; };
const scale = +(process.env.PRAETORIA_SCALE || 1);

/** A browser of its own: a newcomer has never seen the primer, so the saga is a first game. */
async function open(browser, newcomer) {
  const ctx = await browser.newContext({ viewport: { width: 1600, height: 1000 }, deviceScaleFactor: scale });
  const page = await ctx.newPage(); page.setDefaultTimeout(120000);
  const errors = []; page.on('console', m => { if (m.type() === 'error') errors.push(m.text().slice(0, 300)); }); page.on('pageerror', e => errors.push('PAGEERROR ' + e.message.slice(0, 300)));
  await page.goto(base, { waitUntil: 'networkidle' }); await page.waitForSelector('.title-screen', { timeout: 90000 });
  if (!newcomer) await page.evaluate(() => { try { localStorage.setItem('praetoria.primer.seen', '1'); } catch (e) {} });
  return { ctx, page, errors };
}
const guidanceOn = page => page.evaluate(() => { const b = document.querySelector('[data-setting=guidance] button.on'); return b ? b.dataset.v : ''; });
const stored = page => page.evaluate(() => { try { return JSON.parse(localStorage.getItem('praetoria.settings') || '{}').guided; } catch (e) { return 'unreadable'; } });
/** Open a start in the creator, pick the guidance if asked, and begin (saying yes to replacing a running saga). */
async function begin(page, title, guidance) {
  await page.click(`.scenario:has-text("${title}")`);
  await page.waitForSelector('[data-setting=guidance]', { timeout: 30000 });
  if (guidance) { await page.click(`[data-setting=guidance] [data-v=${guidance}]`); await page.waitForTimeout(400); }
  const on = await guidanceOn(page);
  await page.click('button.begin'); await page.waitForTimeout(600);
  const yes = await page.$('.confirm-box [data-confirm="yes"]'); if (yes) await yes.click();
  await page.waitForSelector('.gs-shell', { timeout: 90000 }); await page.waitForTimeout(1800);
  return on;
}
/** Close the cards in front of the first year: the year in brief, a moment, the primer. */
async function clearFront(page) {
  for (let i = 0; i < 10; i++) {
    const had = await page.evaluate(() => {
      const c = document.querySelector('[data-brief-close]') || document.querySelector('[data-moment-go]') || document.querySelector('[data-handbook] .box-x') || document.querySelector('.ev-x');
      if (c) c.click(); return !!c;
    });
    if (!had) break; await page.waitForTimeout(350);
  }
}
const plate = page => page.evaluate(() => { const p = document.querySelector('.aim-plate'); return p ? { kind: p.dataset.aim, close: !!p.querySelector('[data-aim-put-away]'), title: (p.querySelector('.aim-title') || {}).innerText || '' } : null; });
async function options(page) {
  await page.click('[data-menu]'); await page.waitForTimeout(500);
  await page.click('[data-settings]'); await page.waitForSelector('[data-settings-box]', { timeout: 20000 }); await page.waitForTimeout(400);
}
const optionOn = page => page.evaluate(() => { const b = document.querySelector('[data-set="guidance"] button.on'); return b ? b.dataset.guidance : ''; });

(async () => {
  const browser = await chromium.launch({ args: ['--use-gl=angle', '--use-angle=swiftshader', '--ignore-gpu-blocklist', '--disable-gpu-rasterization'] });
  const errors = [];

  // ---- a player who has played before: the aim, its close, Options ----
  {
    const { ctx, page, errors: e } = await open(browser, false);
    await page.click('.scenario:has-text("The New Man")');
    await page.waitForSelector('[data-setting=guidance]', { timeout: 30000 });
    check(await guidanceOn(page) === 'guided', 'the new-saga screen offers Guidance, Guided by default');
    await page.$eval('[data-setting=guidance]', el => el.scrollIntoView({ block: 'center' })); await page.waitForTimeout(400);
    await page.screenshot({ path: `${shots}/aim-1-new-saga-guidance.png` });
    await page.click('.fr-x'); await page.waitForTimeout(400);
    await begin(page, 'The New Man', null); await clearFront(page);
    let p = await plate(page);
    check(p && p.kind === 'start' && p.close, `a guided saga shows its aim with a close (${JSON.stringify(p)})`);
    await page.screenshot({ path: `${shots}/aim-2-guided-map.png` });

    await page.click('[data-aim-put-away]'); await page.waitForTimeout(700);
    p = await plate(page);
    check(p === null || p.kind === 'ruin', `the close puts the aim away (${JSON.stringify(p)})`);
    const note = await page.evaluate(() => [...document.querySelectorAll('.toast.note .notice')].map(x => x.innerText).join(' | '));
    check(/Guidance, in Options, brings it back/.test(note), `a note says how to have it back ("${note}")`);
    check(await stored(page) === false, 'the browser keeps the choice (guided: false)');
    await page.screenshot({ path: `${shots}/aim-3-put-away.png` });

    await options(page);
    check(await optionOn(page) === 'sandbox', 'Options shows Sandbox');
    await page.$eval('[data-set-guidance]', el => el.scrollIntoView({ block: 'center' }));
    await page.screenshot({ path: `${shots}/aim-4-options-sandbox.png` });
    await page.click('[data-guidance="guided"]'); await page.waitForTimeout(500);
    check(await optionOn(page) === 'guided' && await stored(page) === true, 'Guided in Options is kept');
    await page.keyboard.press('Escape'); await page.waitForTimeout(600);
    p = await plate(page);
    check(p && p.kind === 'start', `Guided brings the aim back (${JSON.stringify(p)})`);
    await options(page); await page.click('[data-guidance="sandbox"]'); await page.waitForTimeout(400); await page.keyboard.press('Escape'); await page.waitForTimeout(600);
    p = await plate(page);
    check(p === null || p.kind === 'ruin', `Sandbox in Options takes it away again (${JSON.stringify(p)})`);
    // the choice holds for the next saga: the new-saga screen shows it
    await page.click('[data-menu]'); await page.waitForTimeout(400); await page.click('[data-quit]'); await page.waitForTimeout(600);
    await page.click('.confirm-box [data-confirm="yes"]'); await page.waitForSelector('.title-screen', { timeout: 60000 }); await page.waitForTimeout(800);
    await page.click('.scenario:has-text("The Old Lion")'); await page.waitForSelector('[data-setting=guidance]', { timeout: 30000 });
    check(await guidanceOn(page) === 'sandbox', 'the next new saga opens on the guidance last chosen');
    errors.push(...e); await ctx.close();
  }

  // ---- a newcomer: a sandbox begins without the first steps, a guided first game with them ----
  for (const [g, want] of [['sandbox', false], ['guided', true]]) {
    const { ctx, page, errors: e } = await open(browser, true);
    const on = await begin(page, 'The New Man', g);
    check(on === g, `a newcomer chose ${g} on the new-saga screen`);
    await clearFront(page);
    const list = await page.evaluate(() => !!document.querySelector('[data-first-years]'));
    const p = await plate(page);
    check(list === want, `a newcomer's ${g} saga ${want ? 'has' : 'has no'} first steps pinned`);
    check(want ? (p && p.kind === 'start') : (p === null || p.kind === 'ruin'), `and ${want ? 'shows' : 'does not show'} the aim (${JSON.stringify(p)})`);
    await page.screenshot({ path: `${shots}/aim-5-newcomer-${g}.png` });
    errors.push(...e); await ctx.close();
  }

  check(errors.length === 0, `no errors in the page${errors.length ? ': ' + errors.slice(0, 3).join(' / ') : ''}`);
  await browser.close();
  console.log(failed ? `${failed} checks failed` : 'all checks passed');
  process.exit(failed ? 1 : 0);
})().catch(e => { console.error(e); process.exit(1); });
