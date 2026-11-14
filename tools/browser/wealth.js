// Drives the purses, heirlooms and will panel on the House screen: moves money to the head's own purse, sets the allowance, writes
// a will and the custom of the estate, then plays some years and reports what the family did with their own money.
//
//   node wealth.js ["Start title"] [years]
//
// Needs the build served locally (see README.md here). Screenshots go to PRAETORIA_SHOTS (default ./shots).
const { chromium } = require('playwright');
const title = process.argv[2] || 'The Old Lion'; const years = parseInt(process.argv[3] || '4', 10);
const base = process.env.PRAETORIA_URL || 'http://localhost:8112/'; const shots = process.env.PRAETORIA_SHOTS || './shots';
require('fs').mkdirSync(shots, { recursive: true });
(async () => {
  const browser = await chromium.launch({ args: ['--use-gl=angle', '--use-angle=swiftshader', '--ignore-gpu-blocklist'] });
  const page = await browser.newPage({ viewport: { width: 1600, height: 2200 } }); page.setDefaultTimeout(60000);
  const errors = []; page.on('console', m => { if (m.type() === 'error') errors.push(m.text().slice(0, 300)); }); page.on('pageerror', e => errors.push('PAGEERROR ' + e.message.slice(0, 300)));
  await page.goto(base, { waitUntil: 'networkidle' }); await page.waitForSelector('.title-screen', { timeout: 90000 });
  await page.evaluate(() => { try { localStorage.setItem('praetoria.primer.seen', '1'); } catch (e) {} });
  await page.click('.scenario:has-text("' + title + '")'); await page.click('button.begin'); await page.waitForSelector('.gs-shell', { timeout: 90000 });
  const rail = name => page.evaluate(n => { const el = [...document.querySelectorAll('.rail-btn')].find(e => e.innerText.trim().toLowerCase() === n.toLowerCase()); if (el) el.click(); return !!el; }, name);
  const closeWindows = async () => { for (let i = 0; i < 8; i++) { const had = await page.evaluate(() => { const c = document.querySelector('.win-close') || document.querySelector('.overlay .btn.primary'); if (c) c.click(); return !!c; }); if (!had) break; await page.waitForTimeout(150); } };
  const turnNo = () => page.evaluate(() => { const m = document.body.innerText.match(/Turn (\d+)/); return m ? parseInt(m[1], 10) : -1; });
  const endTurn = async () => { await closeWindows(); const before = await turnNo(); await page.evaluate(() => { const b = [...document.querySelectorAll('button')].find(e => e.innerText.includes('End Turn')); if (b) b.click(); }); for (let i = 0; i < 800; i++) { if (await turnNo().catch(() => -1) > before) break; await page.waitForTimeout(50); } await closeWindows(); };
  const press = text => page.evaluate(t => { const b = [...document.querySelectorAll('.panel.wealth button')].find(e => !e.disabled && e.innerText.trim().startsWith(t)); if (b) b.click(); return b ? b.innerText.replace(/\s+/g, ' ').slice(0, 50) : ''; }, text);
  const head = () => page.evaluate(() => ((document.querySelector('.panel.wealth h3') || {}).innerText || 'NO PANEL').replace(/\s+/g, ' '));
  await closeWindows(); await rail('House'); await page.waitForTimeout(700);
  const text = await page.evaluate(() => (document.querySelector('.panel.wealth') || {}).innerText || 'NO PANEL');
  console.log('panel: ' + (/NaN|undefined|Exception|@\(/.test(text) ? 'SUSPICIOUS TEXT' : 'ok') + ' · ' + await head());
  console.log('took: ' + (await press('take 5') || 'could not')); await page.waitForTimeout(200);
  console.log('allowance: ' + (await press('generous') || 'could not')); await page.waitForTimeout(200);
  console.log('heirlooms: ' + await page.evaluate(() => { const col = [...document.querySelectorAll('.panel.wealth .hh-head')].find(e => e.innerText.startsWith('Heirlooms')); let n = col ? col.nextElementSibling : null; const out = []; while (n && !n.classList.contains('hh-head')) { out.push(n.innerText.replace(/\s+/g, ' ').slice(0, 80)); n = n.nextElementSibling; } return out.join(' || '); }));
  console.log('legatee picked: ' + await page.evaluate(() => { const row = [...document.querySelectorAll('.panel.wealth .suit-acts')].find(e => e.innerText.includes('Of your purse')); const bs = row ? [...row.querySelectorAll('button')] : []; const i = bs.findIndex(b => b.innerText.trim() === 'all'); const b = bs[i + 1]; if (b) b.click(); return b ? b.innerText : 'nobody to name'; }));
  await page.waitForTimeout(200); console.log('will: ' + (await press('half') || 'not set')); await page.waitForTimeout(200);
  console.log('estate: ' + (await press('Shared out') || 'could not')); await page.waitForTimeout(300);
  console.log('now: ' + await head()); await page.screenshot({ path: shots + '/wealth-begin.png' });
  for (let y = 0; y < years; y++) { await endTurn(); await rail('House'); await page.waitForTimeout(300); }
  console.log('after ' + years + ' years: ' + await head());
  console.log('their purses: ' + await page.evaluate(() => [...document.querySelectorAll('.panel.wealth .purse-row')].map(e => e.innerText.replace(/\s+/g, ' ').slice(0, 90)).join(' || ')));
  await page.evaluate(() => { const el = document.querySelector('.panel.wealth'); if (el) el.scrollIntoView(); }); await page.waitForTimeout(300);
  const box = await page.evaluate(() => { const r = document.querySelector('.panel.wealth').getBoundingClientRect(); return { x: Math.max(0, r.x), y: Math.max(0, r.y), width: r.width, height: Math.min(r.height, 1400) }; });
  await page.screenshot({ path: shots + '/wealth-panel.png', clip: box });
  console.log('errors: ' + errors.length); errors.slice(0, 5).forEach(e => console.log('   ' + e));
  await browser.close();
})().catch(e => { console.error('FAILED ' + e.message.split('\n')[0]); process.exit(1); });
