// Watches for the Senate's emergency powers (docs/EMERGENCY.md): plays The Old Lion year by year, reading the consulship panel on the
// Senate screen each year, until the Reach is in danger and then until a consul is voted the powers; says what the panel shows.
//
//   node emergency.js ["Start title"] [years]
//
// Needs the build served locally (see README.md here). Screenshots go to PRAETORIA_SHOTS (default ./shots).
const { chromium } = require('playwright');
const title = process.argv[2] || 'The Old Lion'; const years = parseInt(process.argv[3] || '45', 10);
const base = process.env.PRAETORIA_URL || 'http://localhost:8112/'; const shots = process.env.PRAETORIA_SHOTS || './shots';
require('fs').mkdirSync(shots, { recursive: true });
(async () => {
  const browser = await chromium.launch({ args: ['--use-gl=angle', '--use-angle=swiftshader', '--ignore-gpu-blocklist', '--disable-gpu-rasterization'] });
  const page = await browser.newPage({ viewport: { width: 1600, height: 1400 }, deviceScaleFactor: +(process.env.PRAETORIA_SCALE || 2) }); page.setDefaultTimeout(180000);
  const errors = []; page.on('console', m => { if (m.type() === 'error') errors.push(m.text().slice(0, 300)); }); page.on('pageerror', e => errors.push('PAGEERROR ' + e.message.slice(0, 300)));
  await page.goto(base, { waitUntil: 'networkidle' }); await page.waitForSelector('.title-screen', { timeout: 90000 });
  await page.evaluate(() => { try { localStorage.setItem('praetoria.primer.seen', '1'); } catch (e) {} });
  await page.click('.scenario:has-text("' + title + '")'); await page.click('button.begin'); await page.waitForSelector('.gs-shell', { timeout: 90000 });
  const rail = name => page.evaluate(n => { const el = [...document.querySelectorAll('.rail-btn')].find(e => e.innerText.trim().toLowerCase() === n.toLowerCase()); if (el) el.click(); return !!el; }, name);
  const dismiss = async () => { for (let i = 0; i < 10; i++) { const had = await page.evaluate(() => { const c = document.querySelector('.win-close') || document.querySelector('.overlay .btn.primary'); if (c) c.click(); return !!c; }); if (!had) break; await page.waitForTimeout(250); } };
  const consuls = () => page.evaluate(() => { const t = document.querySelector('.ui-tab[data-tab="offices"]'); if (t) t.click(); });   // the consulship is under the Senate's Offices
  await dismiss(); await rail('Senate'); await page.waitForTimeout(600); await consuls(); await page.waitForTimeout(400);
  let sawDanger = false, sawPowers = false;
  for (let year = 1; year <= years && !sawPowers; year++) {
    const read = await page.evaluate(() => {
      const p = document.querySelector('.panel.consulship'); if (!p) return { none: true };
      const card = p.querySelector('.off-card.emergency'); const line = [...p.querySelectorAll('.muted.small')].find(e => e.innerText.includes('in danger'));
      return { card: card ? card.innerText.replace(/\s+/g, ' ') : '', danger: line ? line.innerText.replace(/\s+/g, ' ') : '', motion: [...document.querySelectorAll('.docket, .motion, .mo-row')].some(e => /emergency powers/i.test(e.innerText)) };
    });
    if (read.danger && !sawDanger) { sawDanger = true; console.log(`year ${year}: ${read.danger}`); const el = await page.$('.panel.consulship'); if (el) await el.screenshot({ path: shots + '/emergency-danger.png' }); }
    if (read.card) { sawPowers = true; console.log(`year ${year}: ${read.card}`); const el = await page.$('.panel.consulship'); if (el) { await el.scrollIntoViewIfNeeded(); await el.screenshot({ path: shots + '/emergency-powers.png' }); } break; }
    { const before = await page.evaluate(() => { const d = document.querySelector('[data-turn]'); return d ? +d.dataset.turn : -1; }); await page.evaluate(() => { const b = document.querySelector('button.endturn'); if (b) b.click(); }); for (let i = 0; i < 1200; i++) { await page.waitForTimeout(50); if (await page.evaluate(() => { const d = document.querySelector('[data-turn]'); return d ? +d.dataset.turn : -1; }) > before) break; } await page.waitForTimeout(300); } await dismiss(); await rail('Senate'); await page.waitForTimeout(350); await consuls(); await page.waitForTimeout(350);
  }
  if (!sawDanger) console.log(`the Reach was in no danger in ${years} years`); else if (!sawPowers) console.log(`no consul was voted the powers in ${years} years`);
  await rail('Chronicle'); await page.waitForTimeout(500);
  console.log('chronicle: ' + await page.evaluate(() => [...document.querySelectorAll('.chron-line, .timeline li, .chronicle li, .tl-row, .hist')].map(e => e.innerText.replace(/\s+/g, ' ')).filter(t => /EMERGENCY POWERS|emergency powers/.test(t)).slice(0, 4).join(' || ')));
  console.log('errors: ' + errors.length + (errors.length ? ' · ' + errors.slice(0, 3).join(' | ') : '')); await browser.close();
})();
