// The first years (docs/ACADEMY.md, "The first years"): plays a first game as a newcomer would, in a browser that has never played, and
// checks the tutorial on the real screen. For The Old Lion and for The New Man: the primer opens by itself and each of its pages points at
// the part of the screen it is about (dimmed around, ringed); its last page is the list; the rail carries its labels; the list is pinned
// at the top of the outliner with the start's own steps. Then every step is done through the real controls, after its Show me has been
// pressed and has lit the control: a report answered, a window or a tab opened, a building begun, a fleet sent, something bought, a vote
// declared (in year 2: nothing is before the Senate in year 1, and the step says so). Each must tick. The Old Lion also saves, reloads and
// continues (the list remembers), puts the list away and pins it again from the primer; The New Man turns the rail's labels off and on
// in Options. With "academy" it plays The Academy instead: a lecture folded and opened by Read more, a report card each term, the passing
// out with the last card, and the four terms on the real screen.
//
//   node firststeps.js [shots-dir] [academy]
//
// Needs the build served locally (see README.md here). Pictures at 1600 by 1000 and, for the list, Show me and the primer, at 1366 by 768.
// Exits 1 on a failed check or a console error.
const { chromium } = require('playwright');
const shots = process.argv[2] || process.env.PRAETORIA_SHOTS || './shots';
const academy = process.argv.includes('academy');
const base = require('./address')();
require('fs').mkdirSync(shots, { recursive: true });
let failed = 0;
const check = (ok, what) => { console.log((ok ? 'ok    ' : 'FAIL  ') + what); if (!ok) failed++; };

async function open(browser, title, w = 1600, h = 1000) {
  // a new context is a browser that has never played: no primer seen, no first game had
  const ctx = await browser.newContext({ viewport: { width: w, height: h }, deviceScaleFactor: +(process.env.PRAETORIA_SCALE || 1) });
  const page = await ctx.newPage(); page.setDefaultTimeout(120000);
  const errors = []; page.on('console', m => { if (m.type() === 'error') errors.push(m.text().slice(0, 300)); }); page.on('pageerror', e => errors.push('PAGEERROR ' + e.message.slice(0, 300)));
  await page.goto(base, { waitUntil: 'networkidle' }); await page.waitForSelector('.title-screen', { timeout: 90000 });
  await page.click('.scenario:has-text("' + title + '")'); await page.click('button.begin'); await page.waitForSelector('.gs-shell', { timeout: 90000 }); await page.waitForTimeout(1500);
  return { ctx, page, errors };
}

/** The steps as the list stands: id, done, and what the line under it says. */
const steps = page => page.evaluate(() => [...document.querySelectorAll('[data-first-years] [data-step]')].map(e => ({ id: e.dataset.step, done: e.dataset.stepDone === '1', says: (e.querySelector('.nm .k') || {}).innerText || '' })));
const listDone = page => page.evaluate(() => { const l = document.querySelector('[data-first-years]'); return l ? +l.dataset.done : -1; });
const stepDone = (page, id) => page.evaluate(id => { const l = document.querySelector('[data-first-years]'); return l ? (l.dataset.doneSteps || '').split(',').includes(id) : null; }, id);
const turnNo = page => page.evaluate(() => { const d = document.querySelector('[data-turn]'); return d ? parseInt(d.dataset.turn, 10) : -1; });
const lit = page => page.evaluate(() => window.praetoriaGuide ? window.praetoriaGuide.lit() : []);
/** Close what stands in front: an event window, what came of an answer, the year in brief, a box. Not the primer. */
async function closeFront(page) {
  for (let i = 0; i < 10; i++) {
    const had = await page.evaluate(() => {
      const c = document.querySelector('[data-outcome-ok]') || document.querySelector('[data-brief-close]') || document.querySelector('.ev-x') || document.querySelector('.overlay:not([data-handbook]) .btn.primary');
      if (c) c.click(); return !!c;
    });
    if (!had) break; await page.waitForTimeout(250);
  }
}
async function endYear(page) {
  await closeFront(page); const before = await turnNo(page);
  await page.evaluate(() => { const b = document.querySelector('button.endturn'); if (b) b.click(); });
  for (let i = 0; i < 600; i++) {
    if (await turnNo(page) > before) break;
    await page.evaluate(() => { const y = document.querySelector('.confirm-box [data-confirm="yes"]'); if (y) y.click(); });   // a year that would end short asks first
    await page.waitForTimeout(100);
  }
  await page.waitForTimeout(800); await closeFront(page);
}
/** Press a step's Show me and say what it lit: a ring that is on the screen and over the control (the window it opens may take a moment
 *  under software drawing, so it is looked for a few times). */
async function showMe(page, id, want) {
  await page.evaluate(() => window.praetoriaGuide && window.praetoriaGuide.clear());
  await page.click(`[data-show-me="${id}"]`);
  let hit = [];
  for (let i = 0; i < 25; i++) {
    await page.waitForTimeout(200);
    hit = (await lit(page)).filter(x => x.shown && x.over);
    if (hit.length > 0 && (!want || hit.some(x => x.sel.includes(want)))) break;
  }
  check(hit.length > 0 && (!want || hit.some(x => x.sel.includes(want))), `Show me for "${id}" lights ${want || 'its control'} (${hit.map(x => x.sel).join(' | ') || 'nothing'})`);
  return hit;
}
/** A step is ticked: the list says so (a step done is folded out of sight while a window is open, so the list's own record is read). */
async function ticks(page, id) {
  for (let i = 0; i < 30 && !(await stepDone(page, id)); i++) await page.waitForTimeout(150);
  check(await stepDone(page, id) === true, `"${id}" ticks`);
}

// ---- the steps, done through the real controls ----
async function answerReport(page) {
  await showMe(page, 'report', '[data-report]');
  await page.click('.outliner [data-report]:not(.done)'); await page.waitForTimeout(900);
  const chose = await page.evaluate(() => { const c = document.querySelector('.choice[data-choice]:not(.locked)'); if (c) c.click(); return !!c; });
  check(chose, 'a report opens and an answer can be chosen'); await page.waitForTimeout(900); await closeFront(page);
  await ticks(page, 'report');
}
async function openFromRail(page, id, rail) {
  await showMe(page, id, `data-rail="${rail}"`);
  check(await page.evaluate(r => !document.querySelector(`.screen.window[data-screen="${r}"]`), rail), `Show me for "${id}" leaves the window to the player to open`);
  await page.click(`.rail [data-rail="${rail}"]`); await page.waitForTimeout(900);
  await ticks(page, id);
}
async function openTab(page, id, tab) {
  const hit = await showMe(page, id, `data-tab="${tab}"`);
  if (tab === 'ledger') check(hit.some(x => x.sel.startsWith('.topbar')), `Show me for "${id}" lights the treasury in the top bar too`);
  await page.click(`.screen.window [data-tab="${tab}"]`); await page.waitForTimeout(900);
  await ticks(page, id);
}
async function beginBuilding(page) {
  await showMe(page, 'build', '[data-can-begin]');
  await page.click('.screen.window .hs-world [data-can-begin]'); await page.waitForTimeout(700);
  const began = await page.evaluate(() => { const b = [...document.querySelectorAll('.screen.window [data-build-list] .hs-pick .ui-btn')].find(x => !x.disabled); if (b) b.click(); return b ? b.innerText.trim() : ''; });
  check(began.length > 0, 'a building can be begun from the Build list (' + began + ')'); await page.waitForTimeout(900);
  await ticks(page, 'build');
}
async function sendFleet(page) {
  await showMe(page, 'fleet', 'order-bar');
  // a world one lane from the fleet, picked as a click would pick it; the order bar then offers Send
  const to = await page.evaluate(() => {
    const g = [...window.praetoriaSpace.galaxies.values()][0]; const f = g.model.fleets.find(x => x.isPlayer && !x.moving); if (!f) return '';
    const ends = g.model.lanes.filter(l => l.a === f.systemId || l.b === f.systemId).map(l => l.a === f.systemId ? l.b : l.a);
    for (const id of ends) { window.praetoriaSpace.select(id); return id; } return '';
  });
  await page.waitForTimeout(1200);
  const sent = await page.evaluate(() => { const b = document.querySelector('.order-bar [data-send]'); if (b) b.click(); return !!b; });
  check(sent, 'the picked fleet can be sent to ' + to); await page.waitForTimeout(500);
  await page.evaluate(() => { const y = document.querySelector('.confirm-box [data-confirm="yes"]'); if (y) y.click(); }); await page.waitForTimeout(800);
  await ticks(page, 'fleet');
}
async function buy(page, id) {
  await showMe(page, id, 'ui-order');
  const bought = await page.evaluate(() => { const b = [...document.querySelectorAll('.screen.window [data-goods-table] .ui-order .ui-btn')].find(x => /^Buy/.test(x.innerText.trim()) && !x.disabled); if (b) b.click(); return b ? b.innerText.trim().replace(/\s+/g, ' ') : ''; });
  check(bought.length > 0, 'something can be bought on the Exchange (' + bought + ')'); await page.waitForTimeout(900);
  await ticks(page, id);
}
async function vote(page) {
  // year 1: nothing is before the Senate, and the step says so; Show me still lights the Senate's socket
  const s = (await steps(page)).find(x => x.id === 'vote');
  check(s && /Not yet/.test(s.says), 'in year 1 the vote waits, and says why (' + (s ? s.says : 'no step') + ')');
  await endYear(page);
  await showMe(page, 'vote', 'sn-vote');
  const voted = await page.evaluate(() => { const b = [...document.querySelectorAll('.screen.window [data-motion] .sn-vote button')].find(x => /Vote for/.test(x.innerText)); if (b) b.click(); return !!b; });
  check(voted, 'a vote can be declared in year 2'); await page.waitForTimeout(900);
  await ticks(page, 'vote');
}

/** The primer opens by itself in a first game; each of its pages points at its part of the screen; the last is the list. */
async function primer(page, tag) {
  check(await page.$('[data-handbook]') !== null, `${tag}: the primer opens by itself in a first game`);
  let pointed = 0, pages = 0;
  for (let i = 1; i <= 12; i++) {
    await page.waitForTimeout(800); pages++;
    const at = await page.evaluate(() => { const e = document.querySelector('[data-primer-points]'); return e ? e.dataset.primerPoints : ''; });
    const l = (await lit(page)).find(x => x.spot);
    if (at) { pointed++; check(!!l && l.rect.w > 0, `${tag}: primer page ${i} points at ${at}`); }
    if (i === 4) await page.screenshot({ path: `${shots}/firststeps-${tag}-primer-topbar.png` });
    const next = await page.$('[data-handbook] .box-acts .btn.primary');
    if (!next) break;
    if (/desk/i.test(await next.innerText())) {
      check(await page.$('[data-primer-steps] li') !== null, `${tag}: the primer's last page is the list of first steps`);
      await page.screenshot({ path: `${shots}/firststeps-${tag}-primer-steps.png` });
      await next.click(); break;
    }
    await next.click();
  }
  check(pointed >= 7, `${tag}: ${pointed} of ${pages} primer pages point at the screen`);
  await page.waitForTimeout(600);
  check((await lit(page)).length === 0, `${tag}: closing the primer lifts its dim`);
}

async function oldLion(browser) {
  const { ctx, page, errors } = await open(browser, 'The Old Lion');
  await primer(page, 'oldlion');
  check(await page.evaluate(() => document.querySelector('.gs-shell.game').classList.contains('rail-labels')), 'oldlion: the rail carries its labels in a first game');
  const list = await steps(page);
  check(list.map(s => s.id).join(',') === 'report,heir,build,fleet,ledger,vote', 'oldlion: the list holds the start\'s own steps (' + list.map(s => s.id).join(', ') + ')');
  check(list.every(s => !s.done), 'oldlion: nothing is ticked before anything is done');
  await page.mouse.move(800, 980); await page.screenshot({ path: `${shots}/firststeps-oldlion-list.png` });
  await answerReport(page);
  await openFromRail(page, 'heir', 'dynasty');
  await page.evaluate(() => window.praetoriaGuide.clear());
  await page.click('[data-show-me="build"]'); await page.waitForTimeout(1300); await page.mouse.move(800, 980);
  await page.screenshot({ path: `${shots}/firststeps-oldlion-showme-build.png` });
  await beginBuilding(page);
  await sendFleet(page);
  await openTab(page, 'ledger', 'ledger');
  // the list is kept with the saga: saved, the page reloaded, the saga continued
  const before = await listDone(page);
  await page.click('[data-menu]'); await page.click('[data-save]'); await page.waitForTimeout(1200);
  await page.reload({ waitUntil: 'networkidle' }); await page.waitForSelector('.title-screen');
  await page.click('[data-continue]'); await page.waitForSelector('.gs-shell.game', { timeout: 90000 }); await page.waitForTimeout(1500); await closeFront(page);
  check(await listDone(page) === before, `oldlion: a saved and loaded game remembers the list (${before} done before, ${await listDone(page)} after)`);
  // put away, then pinned again from the primer's last page
  await page.click('[data-first-years-close]'); await page.waitForTimeout(600);
  check(await page.$('[data-first-years]') === null, 'oldlion: the list can be put away');
  await page.keyboard.press('?'); await page.waitForSelector('[data-handbook]');
  for (let i = 0; i < 12 && !(await page.$('[data-pin-first-steps]')); i++) { const n = await page.$('[data-handbook] .box-acts .btn.primary'); if (!n) break; await n.click(); await page.waitForTimeout(300); }
  const pin = await page.$('[data-pin-first-steps]'); check(!!pin, 'oldlion: the primer offers to pin the list again');
  if (pin) await pin.click(); await page.waitForTimeout(500);
  await page.keyboard.press('Escape'); await page.waitForTimeout(600);
  check(await listDone(page) === before, 'oldlion: pinned again, the list is as it was left');
  await vote(page);
  check(await page.$('[data-first-years-done]') !== null, 'oldlion: with every step done the list says so');
  await page.screenshot({ path: `${shots}/firststeps-oldlion-done.png` });
  await page.waitForTimeout(8000);
  check(await page.$('[data-first-years]') === null, 'oldlion: then it folds away');
  check(errors.length === 0, 'oldlion: no errors in the page' + (errors.length ? ': ' + errors.slice(0, 2).join(' | ') : ''));
  await ctx.close();
}

async function newMan(browser) {
  const { ctx, page, errors } = await open(browser, 'The New Man');
  await primer(page, 'newman');
  const list = await steps(page);
  check(list.map(s => s.id).join(',') === 'report,you,ledger,coin,ladder,vote', 'newman: steps he can take, with no world and no fleet (' + list.map(s => s.id).join(', ') + ')');
  await page.mouse.move(800, 980); await page.screenshot({ path: `${shots}/firststeps-newman-list.png` });
  await answerReport(page);
  await openFromRail(page, 'you', 'character');
  await openTab(page, 'ledger', 'ledger');
  await page.evaluate(() => window.praetoriaGuide.clear());
  await page.click('[data-show-me="coin"]'); await page.waitForTimeout(1300); await page.mouse.move(800, 980);
  await page.screenshot({ path: `${shots}/firststeps-newman-showme-coin.png` });
  await buy(page, 'coin');
  await openTab(page, 'ladder', 'ladder');
  // at 1366 by 768 the outliner folds: the list keeps only what is left to do, a line a step
  await page.setViewportSize({ width: 1366, height: 768 }); await page.waitForTimeout(1200);
  await page.click('[data-show-me="vote"]'); await page.waitForTimeout(1300); await page.mouse.move(700, 760);
  await page.screenshot({ path: `${shots}/firststeps-1366-showme-vote.png` });
  check((await steps(page)).every(s => !s.done), 'newman at 1366 by 768: with the outliner folded only the steps left to do are shown');
  await page.setViewportSize({ width: 1600, height: 1000 }); await page.waitForTimeout(800);
  await vote(page);
  // Options: the rail's labels are on by themselves in the first years, and the player can turn them off
  await page.click('[data-menu]'); await page.click('[data-settings]'); await page.waitForSelector('[data-settings-box]');
  check(await page.$('[data-rail-labels-auto]') !== null, 'newman: Options says the labels are on for the first years');
  await page.click('[data-set-rail-labels]'); await page.waitForTimeout(700);
  check(await page.evaluate(() => !document.querySelector('.gs-shell.game').classList.contains('rail-labels')), 'newman: turned off in Options, the labels go');
  await page.click('[data-set-rail-labels]'); await page.waitForTimeout(700);
  check(await page.evaluate(() => document.querySelector('.gs-shell.game').classList.contains('rail-labels')), 'newman: and come back when turned on');
  await page.keyboard.press('Escape');
  check(errors.length === 0, 'newman: no errors in the page' + (errors.length ? ': ' + errors.slice(0, 2).join(' | ') : ''));
  await ctx.close();
}

/** At 1366 by 768: the primer pointing and the list, on a smaller screen. */
async function small(browser) {
  const { ctx, page, errors } = await open(browser, 'The Old Lion', 1366, 768);
  for (let i = 0; i < 3; i++) { const n = await page.$('[data-handbook] .box-acts .btn.primary'); if (n) { await n.click(); await page.waitForTimeout(500); } }
  await page.waitForTimeout(800); await page.screenshot({ path: `${shots}/firststeps-1366-primer.png` });
  await page.keyboard.press('Escape'); await page.waitForTimeout(800); await page.mouse.move(700, 760);
  await page.screenshot({ path: `${shots}/firststeps-1366-list.png` });
  check(await page.$('[data-first-years]') !== null, 'at 1366 by 768 the list stands at the top of the outliner');
  check(errors.length === 0, '1366: no errors in the page' + (errors.length ? ': ' + errors.slice(0, 2).join(' | ') : ''));
  await ctx.close();
}

/** The Academy: the lectures folded with Read more, a report card each term, the passing out, and the four terms on the real screen. */
async function theAcademy(browser) {
  const { ctx, page, errors } = await open(browser, 'The Academy');
  await page.waitForSelector('.ac-welcome'); await page.click('.ac-welcome .btn.primary:has-text("teach me everything")'); await page.waitForTimeout(800);
  await page.click('.ac [data-tab="lectures"]'); await page.waitForTimeout(600);
  const folded = await page.evaluate(() => { const l = document.querySelector('.ac-lesson.now'); if (!l) return null; const s = l.querySelector('[data-lecture-summary]').innerText; return { sentences: s.split(/(?<=\.)\s+/).length, full: !!l.querySelector('[data-lecture-full]') }; });
  check(folded && folded.sentences >= 2 && folded.sentences <= 3 && !folded.full, 'academy: this term\'s lecture is folded to ' + (folded ? folded.sentences : '?') + ' sentences');
  await page.mouse.move(1590, 990); await page.screenshot({ path: `${shots}/firststeps-academy-lecture.png` });
  await page.click('.ac-lesson.now [data-read-more]'); await page.waitForTimeout(500);
  check(await page.$('.ac-lesson.now [data-lecture-full]') !== null, 'academy: Read more gives the whole lecture');
  await page.mouse.move(1590, 990); await page.screenshot({ path: `${shots}/firststeps-academy-readmore.png` });
  for (let term = 1; term <= 4; term++) {
    await page.click('.ac [data-tab="term"]'); await page.waitForTimeout(400);
    // answer every report of the term with its first open answer
    for (let r = 0; r < 4; r++) {
      const opened = await page.evaluate(() => { const b = document.querySelector('.ac-report:not(.done)'); if (b) b.click(); return !!b; }); if (!opened) break;
      await page.waitForTimeout(700); await page.evaluate(() => { const c = document.querySelector('.choice[data-choice]:not(.locked)'); if (c) c.click(); }); await page.waitForTimeout(700); await closeFront(page);
    }
    if (term > 1) {
      const card = await page.$('[data-term-report] ~ .ac-card, .ac-card[data-term-card]');
      check(card !== null, `academy: term ${term} opens on the report card of term ${term - 1}`);
      if (term === 2) { await page.mouse.move(1590, 990); await page.waitForTimeout(300); await page.screenshot({ path: `${shots}/firststeps-academy-report.png` }); }
    }
    await page.click('.ac-end'); await page.waitForTimeout(1500);
    await page.evaluate(() => { const y = document.querySelector('.confirm-box [data-confirm="yes"]'); if (y) y.click(); }); await page.waitForTimeout(800);
  }
  await page.waitForSelector('.gs-shell.game', { timeout: 60000 }); await page.waitForTimeout(1200);
  check(await page.$('.box .ac-card.last') !== null, 'academy: the passing-out card carries the last term\'s report');
  check(await page.$('[data-first-years-told]') !== null, 'academy: and says the four terms go on as the list');
  await page.screenshot({ path: `${shots}/firststeps-academy-passing.png` });
  await page.evaluate(() => { const b = document.querySelector('.box .btn.primary'); if (b) b.click(); }); await page.waitForTimeout(1000); await closeFront(page);
  const list = await steps(page);
  const terms = await page.evaluate(() => [...document.querySelectorAll('[data-first-years] .fy-term')].map(e => e.innerText.trim()).join(','));
  check(list.length === 8 && terms === 'I,I,II,II,III,III,IV,IV', 'academy: after passing out the list carries the four terms, two steps each (' + terms + ')');
  await page.mouse.move(800, 980); await page.screenshot({ path: `${shots}/firststeps-academy-list.png` });
  check(errors.length === 0, 'academy: no errors in the page' + (errors.length ? ': ' + errors.slice(0, 2).join(' | ') : ''));
  await ctx.close();
}

(async () => {
  const browser = await chromium.launch({ args: ['--use-gl=angle', '--use-angle=swiftshader', '--ignore-gpu-blocklist', '--disable-gpu-rasterization'] });
  if (academy) await theAcademy(browser);
  else { await oldLion(browser); await newMan(browser); await small(browser); }
  await browser.close();
  console.log(failed === 0 ? 'every check passed' : failed + ' checks failed');
  process.exit(failed === 0 ? 0 : 1);
})();
