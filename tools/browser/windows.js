// The windows, one by one (docs/ART_DIRECTION.md): opens every window from the rail and every tab in it, photographs each, and
// reports two things the redesign's rule forbids: a first screen that does not fit (how far the window scrolls), and a section that is
// a heading over nothing but a muted line ("No war.") — an empty section should fold away, not keep its heading.
//
//   node windows.js ["Start title"] [years to play first] [quick]      quick: report only, no photographs
//
// Needs the build served locally (see README.md here). Screenshots go to PRAETORIA_SHOTS (default ./shots). PRAETORIA_ONLY=house,laws
// looks at some windows only; PRAETORIA_W and PRAETORIA_H set the size (1600 by 1000), PRAETORIA_SCALE the device scale (1).
// Tabs are found as the old .cw-tabs/.ex-tabs rungs or the shared Tabs part (.ui-tabs .ui-tab, docs/VISUAL_SYSTEM.md 4.4).
const { chromium } = require('playwright');
const title = process.argv[2] || 'The Old Lion'; const years = parseInt(process.argv[3] || '0', 10);
const quick = process.argv[4] === 'quick';
const base = process.env.PRAETORIA_URL || 'http://localhost:8112/'; const shots = process.env.PRAETORIA_SHOTS || './shots';
require('fs').mkdirSync(shots, { recursive: true });
(async () => {
  const browser = await chromium.launch({ args: ['--use-gl=angle', '--use-angle=swiftshader', '--ignore-gpu-blocklist'] });
  const only = (process.env.PRAETORIA_ONLY || '').toLowerCase().split(',').map(x => x.trim()).filter(Boolean);
  const page = await browser.newPage({ viewport: { width: +(process.env.PRAETORIA_W || 1600), height: +(process.env.PRAETORIA_H || 1000) }, deviceScaleFactor: +(process.env.PRAETORIA_SCALE || 1) }); page.setDefaultTimeout(60000);
  const errors = []; page.on('console', m => { if (m.type() === 'error') errors.push(m.text().slice(0, 300)); }); page.on('pageerror', e => errors.push('PAGEERROR ' + e.message.slice(0, 300)));
  await page.goto(base, { waitUntil: 'networkidle' }); await page.waitForSelector('.title-screen', { timeout: 90000 });
  await page.evaluate(() => { try { localStorage.setItem('praetoria.primer.seen', '1'); } catch (e) {} });
  await page.click('.scenario:has-text("' + title + '")'); await page.click('button.begin'); await page.waitForSelector('.gs-shell', { timeout: 90000 }); await page.waitForTimeout(1200);
  const closeWindows = async () => { for (let i = 0; i < 8; i++) { const had = await page.evaluate(() => { const c = document.querySelector('.win-close') || document.querySelector('.overlay .btn.primary'); if (c) c.click(); return !!c; }); if (!had) break; await page.waitForTimeout(150); } };
  const turnNo = () => page.evaluate(() => { const m = document.body.innerText.match(/Turn (\d+)/i); return m ? parseInt(m[1], 10) : -1; });
  const endTurn = async () => { await closeWindows(); const before = await turnNo(); await page.evaluate(() => { const b = document.querySelector('button.endturn'); if (b) b.click(); }); for (let i = 0; i < 800; i++) { if (await turnNo().catch(() => -1) > before) break; await page.waitForTimeout(50); } await closeWindows(); };
  for (let y = 0; y < years; y++) await endTurn();
  console.log('turn ' + await turnNo());
  const rails = await page.evaluate(() => [...document.querySelectorAll('.rail-btn')].map(e => e.innerText.trim().toLowerCase()).filter(n => n && n !== 'galaxy'));
  // what a window says of itself: how far it scrolls, and its sections that are a heading over one muted line
  const look = () => page.evaluate(() => {
    const body = document.querySelector('.screen.window .win-body') || document.querySelector('.ui-win .ui-wbody'); if (!body) return null;
    const hollow = [...body.querySelectorAll('section.panel')].filter(s => {
      const h = s.querySelector('h3'); if (!h || s.getBoundingClientRect().height === 0) return false;
      const rest = [...s.children].filter(c => c !== h && c.getBoundingClientRect().height > 0);
      return rest.length === 0 || (rest.length === 1 && rest[0].matches('.muted, .empty-line') && !rest[0].querySelector('button, select, input'));
    }).map(s => s.querySelector('h3').innerText.replace(/\s+/g, ' ').trim().slice(0, 40));
    return { over: body.scrollHeight - body.clientHeight, hollow };
  });
  let long = 0, hollowAll = 0;
  for (const r of rails) {
    if (only.length && !only.includes(r)) continue;
    await page.evaluate(n => { const el = [...document.querySelectorAll('.rail-btn')].find(e => e.innerText.trim().toLowerCase() === n); if (el) el.click(); }, r);
    await page.waitForTimeout(1500);
    const tabs = await page.evaluate(() => [...document.querySelectorAll('.screen.window .cw-tabs .rung, .screen.window .ex-tabs .rung, .ui-win .ui-tabs .ui-tab')].map(e => e.innerText.trim()));
    for (let i = 0; i < Math.max(1, tabs.length); i++) {
      if (tabs.length > 0) { await page.evaluate(i => { const b = [...document.querySelectorAll('.screen.window .cw-tabs .rung, .screen.window .ex-tabs .rung, .ui-win .ui-tabs .ui-tab')][i]; if (b) b.click(); }, i); await page.waitForTimeout(700); }
      const l = await look(); if (!l) { console.log(r + ': no window opened'); continue; }
      const name = r + (tabs.length > 0 ? '_' + tabs[i].toLowerCase().replace(/[^a-z]+/g, '-').replace(/-+$/, '') : '');
      if (!quick) await page.screenshot({ path: shots + '/win_' + name + '.png' });
      if (l.over > 40) long++; hollowAll += l.hollow.length;
      console.log(name.padEnd(40) + (l.over > 40 ? 'scrolls ' + l.over + 'px' : 'fits').padEnd(16) + (l.hollow.length ? 'heading over nothing: ' + l.hollow.join(' | ') : ''));
    }
  }
  console.log('windows that scroll: ' + long + ' · headings over nothing: ' + hollowAll);
  console.log('errors: ' + errors.length + (errors.length ? ' ' + errors.slice(0, 3).join(' | ') : ''));
  await browser.close();
})();
