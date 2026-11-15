// Drives "a venture of one's own" on the Exchange's Ventures tab: settles the house's first venture on one of the house, then
// says what the card and the purses list show (docs/WEALTH.md "A venture of one's own").
//
//   node settle.js ["Start title"]
//
// Needs the build served locally (see README.md here). Screenshots go to PRAETORIA_SHOTS (default ./shots).
const { chromium } = require('playwright');
const title = process.argv[2] || 'The New Man';   // a start whose house owns a venture
const base = process.env.PRAETORIA_URL || 'http://localhost:8112/'; const shots = process.env.PRAETORIA_SHOTS || './shots';
require('fs').mkdirSync(shots, { recursive: true });
(async () => {
  const browser = await chromium.launch({ args: ['--use-gl=angle', '--use-angle=swiftshader', '--ignore-gpu-blocklist'] });
  const page = await browser.newPage({ viewport: { width: 1600, height: 1400 } }); page.setDefaultTimeout(60000);
  const errors = []; page.on('console', m => { if (m.type() === 'error') errors.push(m.text().slice(0, 300)); }); page.on('pageerror', e => errors.push('PAGEERROR ' + e.message.slice(0, 300)));
  await page.goto(base, { waitUntil: 'networkidle' }); await page.waitForSelector('.title-screen', { timeout: 90000 });
  await page.evaluate(() => { try { localStorage.setItem('praetoria.primer.seen', '1'); } catch (e) {} });
  await page.click('.scenario:has-text("' + title + '")'); await page.click('button.begin'); await page.waitForSelector('.gs-shell', { timeout: 90000 });
  const rail = name => page.evaluate(n => { const el = [...document.querySelectorAll('.rail-btn')].find(e => e.innerText.trim().toLowerCase() === n.toLowerCase()); if (el) el.click(); return !!el; }, name);
  const tab = async name => { await page.waitForTimeout(300); await page.evaluate(n => { const t = [...document.querySelectorAll('.ex-tabs .rung')].find(e => e.innerText.includes(n)); if (t) t.click(); }, name); await page.waitForTimeout(500); };
  for (let i = 0; i < 8; i++) { const had = await page.evaluate(() => { const c = document.querySelector('.win-close') || document.querySelector('.overlay .btn.primary'); if (c) c.click(); return !!c; }); if (!had) break; await page.waitForTimeout(250); }
  await rail('Exchange'); await tab('Ventures');
  const card = () => page.evaluate(() => { const c = [...document.querySelectorAll('.vcard')].find(x => x.innerText.includes('Settle it on') || x.innerText.includes('own')) || document.querySelector('.vcard'); return c ? c.innerText.replace(/\s+/g, ' ').slice(0, 420) : 'NO VENTURE'; });
  console.log('before: ' + await card());
  const did = await page.evaluate(() => { const c = [...document.querySelectorAll('.vcard')].find(x => x.innerText.includes('Settle it on')); if (!c) return 'no venture of the house to settle'; const row = [...c.querySelectorAll('.row')].find(r => r.innerText.includes('Settle it on')); if (!row) return 'no settle row'; const bs = [...row.querySelectorAll('button')]; const b = bs.find(x => !x.disabled && x.innerText.trim() !== 'yourself') || bs.find(x => !x.disabled); if (b) b.click(); return b ? 'settled on ' + b.innerText.trim() : 'nobody to settle it on: ' + (bs[0] ? bs[0].title : 'no buttons'); });
  await page.waitForTimeout(500); console.log(did); console.log('after: ' + await card());
  await page.screenshot({ path: shots + '/settle-ventures.png' });
  await rail('House'); await tab('Purses'); await page.waitForTimeout(500);
  console.log('purses: ' + await page.evaluate(() => [...document.querySelectorAll('.panel.wealth .purse-row:not(.own-share)')].map(e => e.innerText.replace(/\s+/g, ' ').slice(0, 150)).join(' || ')));
  console.log('errors: ' + errors.length + (errors.length ? ' · ' + errors.slice(0, 3).join(' | ') : '')); await browser.close();
})();
