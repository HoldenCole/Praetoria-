// The keys and the seed (docs/WEB_CLIENT.md, "Settings" and "The seed of a new saga"): two new sagas roll different seeds and
// ?seed=N is kept; Enter opens each unanswered report, closes what came of an answer and ends the year only when nothing is
// waiting; the question asked before Quit takes Esc for its no and ignores Enter; a click on End Year and then Enter ends one year.
//
//   node keys.js                 the published game must be served on :8112 (or PRAETORIA_URL)
const { chromium } = require('playwright');
const plain = process.env.PRAETORIA_URL || 'http://localhost:8112/';   // no seed: the title rolls its own
const seeded = require('./address')();
(async () => {
  const browser = await chromium.launch({ args: ['--use-gl=angle', '--use-angle=swiftshader', '--ignore-gpu-blocklist', '--disable-gpu-rasterization'] });
  const out = []; const fails = []; const check = (ok, what) => { const l = (ok ? 'ok   ' : 'FAIL ') + what; console.log(l); out.push(l); if (!ok) fails.push(what); };
  async function seedOf(url) {
    const page = await browser.newPage({ viewport: { width: 1600, height: 1000 } });
    await page.goto(url, { waitUntil: 'networkidle' }); await page.waitForSelector('.title-screen', { timeout: 120000 });
    await page.click('.scenario:has-text("The Old Lion")');
    await page.waitForSelector('[data-setting=seed] input', { timeout: 30000 });
    const v = await page.$eval('[data-setting=seed] input', e => e.value); await page.close(); return v;
  }
  const at = s => { const u = new URL(plain); u.searchParams.set('seed', s); return u.toString(); };
  const s1 = await seedOf(plain), s2 = await seedOf(plain), s3 = await seedOf(at('1')), s4 = await seedOf(at('4242'));
  out.push(`seeds: plain ${s1}, plain ${s2}, ?seed=1 ${s3}, ?seed=4242 ${s4}`);
  check(s1 !== s2, 'two new sagas without a seed in the address roll different seeds');
  check(s3 === '1' && s4 === '4242', 'a seed in the address is the seed');

  const page = await browser.newPage({ viewport: { width: 1600, height: 1000 } });
  const errors = []; page.on('pageerror', e => errors.push(e.message.slice(0, 200))); page.on('console', m => { if (m.type() === 'error') errors.push(m.text().slice(0, 200)); });
  await page.goto(seeded, { waitUntil: 'networkidle' }); await page.waitForSelector('.title-screen', { timeout: 120000 });
  await page.evaluate(() => { try { localStorage.setItem('praetoria.primer.seen', '1'); } catch (e) {} });
  await page.click('.scenario:has-text("The Old Lion")'); await page.click('button.begin'); await page.waitForSelector('.gs-shell', { timeout: 120000 });
  await page.waitForTimeout(2500);
  const turn = () => page.$eval('[data-turn]', e => +e.dataset.turn);
  // the year's moments (a card, or the seat passing) stand before the year in brief and take Enter as it does: read as part of it here
  const state = () => page.evaluate(() => ({ ev: !!document.querySelector('.ev-win'), outcome: !!document.querySelector('[data-outcome]'), ask: !!document.querySelector('[data-confirm]'),
    brief: !!document.querySelector('[data-year-brief], [data-moment], .epilogue.succession'), open: +(document.querySelector('button.endturn .cnt')?.dataset.n || 0), focus: document.activeElement?.className || document.activeElement?.tagName }));
  const press = async k => { await page.keyboard.press(k); await page.waitForTimeout(900); };
  const t0 = await turn(); let st = await state(); out.push(`start: year ${t0} ${JSON.stringify(st)}`);
  if (st.brief) { await press('Enter'); st = await state(); }

  // walk the year by the keyboard: Enter opens each report, a digit answers it, Enter closes what came of it; the year turns only at the end
  let sawOutcomeEnter = false, steps = 0, endedAt = -1;
  while (steps++ < 30) {
    st = await state(); const t = await turn();
    if (t !== t0) { endedAt = steps; break; }
    if (st.outcome) { await press('Enter'); const after = await state(); check(!after.outcome && (await turn()) === t0, `Enter on "What came of it" closes it and the year stands (step ${steps})`); sawOutcomeEnter = true; continue; }
    if (st.ev) { let answered = false; for (const d of ['1', '2', '3', '4']) { await press(d); const a = await state(); if (a.outcome || !a.ev) { answered = true; break; } } if (!answered) { await press('Escape'); } continue; }
    const before = st.open; await press('Enter'); const a = await state();
    if (before > 0) check(a.ev || (await turn()) !== t0, `Enter with ${before} unanswered opens a report (step ${steps}): ${JSON.stringify(a)}`);
  }
  check(sawOutcomeEnter, 'the walk met at least one outcome box');
  check(endedAt > 0, `the year ended by Enter once the reports were done (after ${steps} steps), now year ${await turn()}`);

  // the question box: Enter does nothing, Esc says no (first Enter closes each of the year's cards and the year in brief, one by one)
  const clear = async () => { for (let i = 0; i < 8 && (await state()).brief; i++) await press('Enter'); };
  await clear();
  const t1 = await turn();
  st = await state(); if (st.ev) await press('Escape');
  await page.click('[data-menu]'); await page.waitForTimeout(600);
  await page.click('[data-quit]'); await page.waitForTimeout(800);
  st = await state(); check(st.ask, 'Quit asks "Leave this saga?"');
  await press('Enter'); st = await state(); check(st.ask && (await turn()) === t1, 'Enter on the question leaves it open and the year stands');
  await press('Escape'); st = await state(); check(!st.ask && !!(await page.$('.gs-shell')) && (await turn()) === t1, 'Esc on the question answers no: still in the saga, same year');

  // a mouse click on End Year, then Enter: one year, not two
  await clear();
  const t2 = await turn();
  await page.click('button.endturn'); await page.waitForTimeout(2500);
  st = await state();
  if (st.ask) { out.push('End Year asked first (short year): answered yes by a click'); await page.click('[data-confirm=yes]'); await page.waitForTimeout(2500); st = await state(); }
  out.push(`after a click on End Year: year ${await turn()}, focus on ${st.focus}, ${JSON.stringify(st)}`);
  await press('Enter'); await page.waitForTimeout(1500);
  const t3 = await turn(); check(t3 === t2 + 1, `a click on End Year then Enter ends one year (was ${t2}, now ${t3})`);
  check(errors.length === 0, 'no errors in the page' + (errors.length ? ': ' + errors.slice(0, 3).join(' | ') : ''));
  console.log(fails.length ? `${fails.length} FAILED` : 'all passed');
  await browser.close();
  process.exitCode = fails.length ? 1 : 0;
})().catch(e => { console.error(e); process.exit(1); });
