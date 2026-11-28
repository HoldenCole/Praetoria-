// The year's moments (docs/VISUAL_SYSTEM.md, "The year's moments"; docs/WEB_CLIENT.md, "After End Year"): plays a start year by year
// until a death in the main line and a birth have come, and checks that each year's cards open one at a time before the year in brief,
// at most three, with the right names and faces (the dead in grey with the ribbon, the newborn with the parents); that Enter and Esc
// close a card; that the year in brief then has each card's line and its worst news first in the red band; and that the funeral
// report names whom it buries and shows their face. Photographs a death card, a birth card, the red band and the funeral at 1600 by
// 1000 and 1366 by 768.
//
//   node moments.js ["Start title"] [years]          the published game served (PRAETORIA_URL, see README.md); seed 1 by address.js
//
// Exits 1 on a failed check or an error in the page. Pictures go to PRAETORIA_SHOTS (default ./shots), named moments-*.png.
const { chromium } = require('playwright');
const title = process.argv[2] || 'The Old Lion'; const years = parseInt(process.argv[3] || '30', 10);
const base = require('./address')(); const shots = process.env.PRAETORIA_SHOTS || './shots';
require('fs').mkdirSync(shots, { recursive: true });
const BIG = { width: 1600, height: 1000 }, SMALL = { width: 1366, height: 768 };
(async () => {
  const browser = await chromium.launch({ args: ['--use-gl=angle', '--use-angle=swiftshader', '--ignore-gpu-blocklist', '--disable-gpu-rasterization'] });
  const page = await browser.newPage({ viewport: BIG }); page.setDefaultTimeout(180000);
  const errors = []; page.on('console', m => { if (m.type() === 'error') errors.push(m.text().slice(0, 300)); }); page.on('pageerror', e => errors.push('PAGEERROR ' + e.message.slice(0, 300)));
  const fails = []; const check = (ok, what) => { console.log((ok ? 'ok   ' : 'FAIL ') + what); if (!ok) fails.push(what); };
  const wait = ms => page.waitForTimeout(ms);
  await page.goto(base, { waitUntil: 'networkidle' }); await page.waitForSelector('.title-screen', { timeout: 120000 });
  await page.evaluate(() => { try { localStorage.setItem('praetoria.primer.seen', '1'); localStorage.removeItem('praetoria.brief.every'); } catch (e) {} });
  await page.click('.scenario:has-text("' + title + '")'); await page.click('button.begin'); await page.waitForSelector('.gs-shell', { timeout: 120000 });
  await wait(2000);
  const turn = () => page.evaluate(() => { const d = document.querySelector('[data-turn]'); return d ? +d.dataset.turn : -1; });
  const shot = async (name, el) => {
    for (const [vp, tag] of [[BIG, ''], [SMALL, '-1366']]) {
      await page.setViewportSize(vp); await wait(700);
      await page.evaluate(() => window.praetoriaBust && praetoriaBust.paint()); await wait(400);
      // the card rises as it opens: the picture is of it standing
      await page.waitForFunction(() => [...document.querySelectorAll('.mo-stage, .ev-win, .yb')].every(e => e.getAnimations().every(a => a.playState !== 'running') && getComputedStyle(e).opacity === '1'), null, { timeout: 15000 }).catch(() => {});
      await page.screenshot({ path: `${shots}/${name}${tag}.png` });
      if (tag && el) { const fits = await page.evaluate(sel => { const b = document.querySelector(sel); if (!b) return null; const r = b.getBoundingClientRect(); return r.bottom <= innerHeight + 1 && r.top >= -1; }, el); check(fits !== false, `${name} at 1366 by 768: ${el} is on the screen without scrolling`); }
    }
    await page.setViewportSize(BIG); await wait(400); console.log('  saved ' + name + '.png and ' + name + '-1366.png');
  };
  // what stands in front of the desk now: the succession screen, a moment card (and which of how many), the year in brief, a report
  const front = () => page.evaluate(() => {
    const card = document.querySelector('.mo-veil[data-moment]');
    const c = card ? {
      kind: card.dataset.moment, key: card.dataset.momentKey, subject: card.dataset.momentSubject || '', at: +card.dataset.momentAt, of: +card.dataset.momentOf,
      title: (card.querySelector('.mo-title') || {}).innerText || '', kicker: (card.querySelector('.mo-kick') || {}).innerText || '', prose: (card.querySelector('.mo-prose') || {}).innerText || '',
      button: (card.querySelector('[data-moment-go]') || {}).innerText || '', scene: (card.querySelector('.mo-art') || {}).style?.backgroundImage || '',
      lead: [...card.querySelectorAll('.mo-who.lead')].map(e => ({ id: e.dataset.face || '', name: (e.querySelector('.mo-name') || {}).innerText || '', says: (e.querySelector('.mo-says') || {}).innerText || '',
        alive: e.dataset.alive === '1', grey: !!e.querySelector('.portrait.dead'), ribbon: !!e.querySelector('.pf-ribbon'), bust: (e.querySelector('[data-bust]') || {}).dataset?.bust || '' })),
      around: [...card.querySelectorAll('.mo-who.side')].map(e => ({ id: e.dataset.face, name: (e.querySelector('.mo-name') || {}).innerText || '', says: (e.querySelector('.mo-says') || {}).innerText || '' })),
    } : null;
    return { succession: !!document.querySelector('.epilogue.succession'), card: c, brief: !!document.querySelector('[data-year-brief]'),
      report: !!document.querySelector('.ev-win'), outcome: !!document.querySelector('[data-outcome]'), ask: !!document.querySelector('[data-confirm]') };
  });
  const suspicious = t => /NaN|undefined|Exception|@\(|\(s\)|—|House House|\(-\d/.test(t);
  let deathSeen = null, birthSeen = null, bandSeen = false, funeralSeen = false, cardsSeen = 0, years0 = await turn();
  const seenKinds = {}; const buried = [];   // the dead of the house so far, whose funeral may come

  for (let y = 0; y < years && !(deathSeen && birthSeen && funeralSeen); y++) {
    // end the year: close what is open over the desk, press End Year, and say yes if it asks about a short year
    for (let i = 0; i < 6; i++) { const had = await page.evaluate(() => { const c = document.querySelector('.win-close') || document.querySelector('[data-close-window]'); if (c) c.click(); return !!c; }); if (!had) break; await wait(150); }
    const before = await turn();
    await page.evaluate(() => document.querySelector('button.endturn')?.click());
    for (let i = 0; i < 40 && (await turn()) === before; i++) { await wait(500); const f = await front(); if (f.ask) { await page.evaluate(() => document.querySelector('[data-confirm=yes]')?.click()); } }
    await wait(1500);
    const now = await turn(); if (now === before) { console.log('the year did not end'); break; }
    let f = await front();
    if (f.succession) {
      const who = await page.evaluate(() => (document.querySelector('.epilogue.succession .succ-who.old b') || {}).innerText || '');
      console.log(`year ${now - 1}: the seat passes (${who || 'a sworn house'}): the succession screen stands first`);
      if (who) buried.push(who);
      check(!f.card && !f.brief, `year ${now - 1}: the succession screen stands alone in front, before the moments and the year in brief`);
      await page.keyboard.press('Enter'); await wait(900); f = await front();
      check(!f.succession, `year ${now - 1}: Enter takes up the name and closes the succession screen`);
    }
    // the cards, one at a time, before the year in brief
    const cards = []; let n = 0; let closedBy = ['Enter', 'Escape', 'button'];
    while (f.card && n < 6) {
      const c = f.card; cards.push(c); n++; cardsSeen++;
      check(!f.brief && !f.report, `year ${now - 1}: card ${c.at} of ${c.of} (${c.kind}) stands before the year in brief and the reports`);
      check(c.of <= 3 && c.at === n, `year ${now - 1}: card ${c.at} of ${c.of}, three at most and in order`);
      check(c.lead.length >= 1 || c.kind === 'war' || c.kind === 'siege' || c.kind === 'world_lost' || c.kind === 'seat_lost' || c.kind === 'suit', `year ${now - 1}: the ${c.kind} card has a face in its large plate`);
      for (const l of c.lead) check(l.bust.includes('"id":"' + l.id + '"'), `year ${now - 1}: the large plate of ${l.name} is painted from ${l.name}'s own record`);
      check(!suspicious(c.title + c.prose + c.kicker + c.button + c.lead.map(l => l.says).join(' ') + c.around.map(a => a.says).join(' ')), `year ${now - 1}: the ${c.kind} card's words are plain (${c.title})`);
      check(c.scene.includes('img/scenes/'), `year ${now - 1}: the ${c.kind} card stands on a painting (${c.scene.replace(/.*scenes\//, '').replace(/\..*/, '')})`);
      console.log(`  card ${c.at}/${c.of} ${c.kind}: "${c.kicker.replace(/\s+/g, ' ')}" · ${c.title} · ${c.prose}`);
      console.log(`     lead: ${c.lead.map(l => `${l.name} (${l.says}${l.alive ? '' : ', dead'})`).join(' and ')} · around: ${c.around.map(a => `${a.name} (${a.says})`).join(', ') || 'nobody'}`);
      seenKinds[c.kind] = (seenKinds[c.kind] || 0) + 1;
      if (c.kind === 'death') {
        const l = c.lead[0];
        check(l && !l.alive && l.grey && l.ribbon, `year ${now - 1}: ${l?.name} is dead and drawn in grey with the mourning ribbon`);
        check(/ is dead$/.test(c.title) && c.prose.startsWith(l.name + ' died at'), `year ${now - 1}: the death card names ${l?.name} in its title and its line`);
        check(/^Mourn (him|her|them)$/.test(c.button.trim()), `year ${now - 1}: one button, "${c.button.trim()}"`);
        if (l) buried.push(l.name);
        if (!deathSeen) { deathSeen = { year: now - 1, name: l.name, id: l.id }; await shot('moments-death', '[data-moment-go]'); }
      }
      if (c.kind === 'birth') {
        const l = c.lead[0];
        check(l && l.alive && !l.grey, `year ${now - 1}: the newborn ${l?.name} is drawn living`);
        check(c.around.length >= 1 && c.around.every(a => c.prose.includes(a.name) || a.says === 'you'), `year ${now - 1}: the newborn stands with the parents, and the line names them (${c.around.map(a => a.name).join(', ')})`);
        check(/^A .+ is born$/.test(c.title) && c.prose.startsWith(l.name + ' is born to'), `year ${now - 1}: the birth card names ${l?.name}`);
        if (!birthSeen) { birthSeen = { year: now - 1, name: l.name, id: l.id }; await shot('moments-birth', '[data-moment-go]'); }
      }
      if (!['death', 'birth'].includes(c.kind) && seenKinds[c.kind] === 1) await shot('moments-' + c.kind.replace('_', '-'), '[data-moment-go]');
      // close it: by Enter, by Esc, or by its one button, in turn
      const how = closedBy[(n - 1) % 3];
      if (how === 'button') await page.evaluate(() => document.querySelector('[data-moment-go]')?.click()); else await page.keyboard.press(how);
      await wait(800); const g = await front();
      check(!g.card || g.card.key !== c.key, `year ${now - 1}: ${how === 'button' ? 'its button' : how} closes the ${c.kind} card`);
      check((await turn()) === now, `year ${now - 1}: closing a card ends no year`);
      f = g;
    }
    // then the year in brief: each card's line is in it, and its worst news stands first in the red band
    if (f.brief) {
      const brief = await page.evaluate(() => ({ text: (document.querySelector('[data-year-brief]') || {}).innerText || '',
        band: [...document.querySelectorAll('[data-year-brief] .yb-line.grave')].map(e => e.innerText.replace(/\s+/g, ' ').trim()),
        first: (document.querySelector('[data-year-brief] .ui-sec') || {}).dataset?.worst || '' }));
      for (const c of cards) check(await page.evaluate(s => !!document.querySelector(`[data-year-brief] [data-subject="${s}"]`), c.subject), `year ${now - 1}: the year in brief keeps a line of the ${c.kind} card (${c.title})`);
      if (brief.band.length) {
        check(brief.first !== '', `year ${now - 1}: the red band is the first section of the year in brief`);
        console.log(`  the red band: ${brief.band.join(' | ')}`);
        if (!bandSeen && cards.some(c => c.kind === 'death')) { bandSeen = true; check(cards.filter(c => c.kind === 'death').some(c => brief.band[0].includes(c.lead[0].name)), `year ${now - 1}: the death in the main line is the first of the worst news`); await shot('moments-brief-band', null); }
      }
      check(!/is ill\./.test(brief.text) || !cards.filter(c => c.kind === 'death').some(c => brief.text.includes(c.lead[0].name + ' is ill')), `year ${now - 1}: no line says the dead are ill`);
      await page.evaluate(() => document.querySelector('[data-brief-close]')?.click()); await wait(600);
    } else if (cards.length) check(false, `year ${now - 1}: the year in brief opens once the cards are read`);
    if (!cards.length) console.log(`year ${now - 1}: no moments`); else console.log(`year ${now - 1}: ${cards.length} card${cards.length === 1 ? '' : 's'}`);
    // the funeral, when its report comes: it names whom it buries and shows the face
    if (buried.length && !funeralSeen) {
      const opened = await page.evaluate(() => { const b = [...document.querySelectorAll('[data-report]')].find(e => /A Death in the House/i.test(e.innerText)); if (b) b.click(); return !!b; });
      if (opened) {
        await wait(900);
        const ev = await page.evaluate(() => { const e = document.querySelector('.ev-win'); if (!e) return null; const lead = e.querySelector('.ev-cast.l');
          return { title: (e.querySelector('.ev-title') || {}).innerText || '', prose: [...e.querySelectorAll('.ev-prose')].map(p => p.innerText).join(' '), facts: (e.querySelector('[data-facts]') || {}).innerText || '',
            lead: lead ? { name: (lead.querySelector('.ev-plate b') || {}).innerText || '', dead: lead.classList.contains('dead') && !!lead.querySelector('.portrait.dead'), says: (lead.querySelector('.ev-plate span') || {}).innerText || '' } : null }; });
        if (ev) {
          console.log(`  the funeral: ${ev.title} · ${ev.prose.slice(0, 160)} · lead ${ev.lead?.name} (${ev.lead?.says})`);
          check(/^A Death in the House$/i.test(ev.title.trim()), 'the funeral report is open');
          check(ev.lead && ev.lead.dead && ev.prose.startsWith(ev.lead.name + ' is dead'), `the funeral names ${ev.lead?.name}, whom it buries, and shows ${ev.lead?.name} in grey`);
          check(!!ev.lead && buried.includes(ev.lead.name), `the funeral buries one of the house's dead (${buried.join(', ')})`);
          check(/What was said at the graveside/.test(ev.facts), 'what was said at the graveside is under the prose');
          check(!suspicious(ev.prose + ev.facts + (ev.lead?.says || '')), "the funeral's words are plain");
          funeralSeen = true; await shot('moments-funeral', '.ev-choices');
          await page.keyboard.press('2'); await wait(900);
          if ((await front()).outcome) { await page.keyboard.press('Enter'); await wait(600); }
        }
      }
    }
  }
  console.log(`played ${(await turn()) - years0} years of "${title}": ${cardsSeen} cards (${Object.entries(seenKinds).map(([k, v]) => k + ' ' + v).join(', ')})`);
  check(!!deathSeen, 'a death in the main line came and had its card' + (deathSeen ? ` (${deathSeen.name}, year ${deathSeen.year})` : ''));
  check(!!birthSeen, 'a birth came and had its card' + (birthSeen ? ` (${birthSeen.name}, year ${birthSeen.year})` : ''));
  check(funeralSeen, 'the funeral report came and named the dead');
  check(errors.length === 0, 'no errors in the page' + (errors.length ? ': ' + errors.slice(0, 3).join(' | ') : ''));
  console.log(fails.length ? `${fails.length} FAILED` : 'all passed');
  await browser.close();
  process.exitCode = fails.length ? 1 : 0;
})().catch(e => { console.error('FAILED ' + e.message.split('\n')[0]); process.exit(1); });
