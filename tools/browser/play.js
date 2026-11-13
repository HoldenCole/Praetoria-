// Drives the published web build in a headless browser. One script, four checks:
//
//   node play.js screens  "<Start title>" <turns> [Screen,Screen,...]   every named screen renders, with a screenshot of each
//   node play.js turns    "<Start title>" <turns> [perf]                seconds each End Turn takes; with perf, where they went
//   node play.js exchange "<Start title>" <turns>                       walks the Exchange tabs and places an order on each
//   node play.js phone    "<Start title>" <turns>                       the main screens at phone width, with what overflows
//
// It needs the build served locally (see README.md here) and Playwright with Chromium.
// The Office is a 3D room, which a machine with no graphics card draws at about a frame a second; every check therefore
// moves to a flat screen before it ends a turn, and waits for the page rather than for a fixed time.
const { chromium } = require('playwright');
const [mode = 'screens', title = 'The Circling Heir', turnsArg = '3', extra = ''] = process.argv.slice(2);
const turns = parseInt(turnsArg, 10) || 0;
const base = process.env.PRAETORIA_URL || 'http://localhost:8112/';
const shots = process.env.PRAETORIA_SHOTS || './shots';
const fs = require('fs'); fs.mkdirSync(shots, { recursive: true });

async function open(viewport, query = '') {
  const browser = await chromium.launch({ args: ['--use-gl=angle', '--use-angle=swiftshader', '--ignore-gpu-blocklist'] });
  const page = await browser.newPage({ viewport }); page.setDefaultTimeout(60000);
  const errors = [];
  page.on('console', m => { if (m.type() === 'error') errors.push(m.text().slice(0, 300)); if (m.text().startsWith('PERF')) console.log('   ' + m.text().slice(0, 700)); });
  page.on('pageerror', e => errors.push('PAGEERROR ' + e.message.slice(0, 300)));
  await page.goto(base + query, { waitUntil: 'networkidle' }); await page.waitForSelector('.title-screen', { timeout: 90000 });
  await page.evaluate(() => { try { localStorage.setItem('praetoria.primer.seen', '1'); } catch (e) {} });
  await page.click('.scenario:has-text("' + title + '")'); await page.click('button.begin'); await page.waitForSelector('.gs-shell', { timeout: 90000 });
  return { browser, page, errors };
}
// clicks through the page's own code, so a slow frame cannot make the click miss
const rail = (page, name) => page.evaluate(n => { const el = [...document.querySelectorAll('.rail-btn')].find(e => e.innerText.toLowerCase().includes(n.toLowerCase())); if (el) el.click(); return !!el; }, name);
const closeWindows = async page => { for (let i = 0; i < 8; i++) { const had = await page.evaluate(() => { const c = document.querySelector('.win-close') || document.querySelector('.overlay .btn.primary'); if (c) c.click(); return !!c; }); if (!had) break; await page.waitForTimeout(150); } };
const turnNo = page => page.evaluate(() => { const m = document.body.innerText.match(/Turn (\d+)/); return m ? parseInt(m[1], 10) : -1; });
async function endTurn(page) {
  await closeWindows(page); const before = await turnNo(page); const started = Date.now();
  await page.evaluate(() => { const b = [...document.querySelectorAll('button')].find(e => e.innerText.includes('End Turn')); if (b) b.click(); });
  for (let i = 0; i < 1200; i++) { if (await turnNo(page).catch(() => -1) > before) break; await page.waitForTimeout(50); }
  await closeWindows(page); return (Date.now() - started) / 1000;
}
async function play(page, n) { await rail(page, 'House'); await page.waitForTimeout(400); const t = []; for (let i = 0; i < n; i++) t.push(await endTurn(page)); return t; }
const suspicious = text => /NaN|undefined|Exception|Infinity|at Praetoria|@\(/.test(text);

(async () => {
  if (mode === 'turns') {
    const { browser, page, errors } = await open({ width: 1600, height: 900 }, extra === 'perf' ? '?perf' : '');
    const t = await play(page, turns); console.log(title + ': turns ' + t.map(x => x.toFixed(1)).join(', ') + ' s · errors ' + errors.length); await browser.close(); return;
  }
  if (mode === 'phone') {
    const { browser, page, errors } = await open({ width: 390, height: 844 }); await play(page, turns);
    for (const s of ['House', 'Senate', 'Exchange', 'Houses', 'Military', 'You']) {
      if (!await rail(page, s)) { console.log(s + ': no rail button at this width'); continue; } await page.waitForTimeout(700);
      const over = await page.evaluate(() => { const w = document.documentElement.clientWidth; return { page: document.documentElement.scrollWidth - w, worst: [...document.querySelectorAll('.dash *')].map(e => ({ r: e.getBoundingClientRect(), c: e.className })).filter(x => x.r.right > w + 4 && x.r.width > 0).slice(0, 3).map(x => String(x.c).slice(0, 30) + ' +' + Math.round(x.r.right - w)) }; });
      console.log(s + ': page scrolls sideways by ' + over.page + ' px' + (over.worst.length ? ' · past the edge: ' + over.worst.join(', ') : ''));
      await page.screenshot({ path: shots + '/phone-' + s.toLowerCase() + '.png' });
    }
    console.log('errors: ' + errors.length); await browser.close(); return;
  }
  const { browser, page, errors } = await open({ width: 1600, height: 950 }); await play(page, turns);
  if (mode === 'exchange') {
    await rail(page, 'Exchange'); await page.waitForTimeout(600);
    for (const name of ['Goods', 'Ventures', 'Projects', 'Shares']) {
      const ok = await page.evaluate(n => { const t = [...document.querySelectorAll('.ex-tabs .rung')].find(e => e.innerText.includes(n)); if (t) t.click(); return !!t; }, name); if (!ok) { console.log(name + ': NO TAB'); continue; }
      await page.waitForTimeout(500);
      const did = await page.evaluate(n => { const pick = sel => [...document.querySelectorAll(sel)].find(b => !b.disabled); const b = n === 'Ventures' ? pick('.standings.ventures button') : n === 'Shares' ? [...document.querySelectorAll('.standings.shares button')].find(x => !x.disabled && x.innerText.startsWith('Buy')) : n === 'Goods' ? [...document.querySelectorAll('.standings.goods button')].find(x => !x.disabled && x.innerText.startsWith('Buy')) : null; if (b) { const t = b.innerText; b.click(); return t.replace(/\n/g, ' ').slice(0, 40); } return ''; }, name);
      await page.waitForTimeout(400); const text = await page.evaluate(() => document.querySelector('.dash.exchange').innerText);
      console.log(name + ': ' + (suspicious(text) ? 'SUSPICIOUS TEXT' : 'ok') + (did ? ' · clicked "' + did + '"' : '')); await page.screenshot({ path: shots + '/exchange-' + name.toLowerCase() + '.png' });
    }
  } else {
    for (const s of (extra || 'House,Senate,Laws,You,Worlds,Exchange,Military,Dynasty,Houses,Chronicle').split(',')) {
      if (!await rail(page, s)) { console.log(s + ': NO SCREEN'); continue; } await page.waitForTimeout(700);
      const text = await page.evaluate(() => (document.querySelector('.dash') || document.body).innerText);
      const stats = await page.evaluate(() => [...document.querySelectorAll('.dh-stats .stat')].slice(0, 4).map(e => e.innerText.replace(/\n/g, ' ')).join(' | '));
      console.log(s + ': ' + (suspicious(text) ? 'SUSPICIOUS TEXT' : 'ok') + ' · ' + stats.slice(0, 150)); await page.screenshot({ path: shots + '/screen-' + s.toLowerCase() + '.png' });
    }
  }
  console.log('errors: ' + errors.length); errors.slice(0, 5).forEach(e => console.log('   ' + e));
  await browser.close();
})().catch(e => { console.error('FAILED ' + e.message.split('\n')[0]); process.exit(1); });
