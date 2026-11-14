// Watches what the people of the house are after on the Dynasty screen. It touches nothing for the years given, except that it
// grants the first thing anyone asks for and says no to the second, and reports each year what everyone did about their ambition.
//
//   node ambitions.js ["Start title"] [years]
//
// Needs the build served locally (see README.md here). Screenshots go to PRAETORIA_SHOTS (default ./shots).
const { chromium } = require('playwright');
const title = process.argv[2] || 'The Old Lion'; const years = parseInt(process.argv[3] || '8', 10);
const base = process.env.PRAETORIA_URL || 'http://localhost:8112/'; const shots = process.env.PRAETORIA_SHOTS || './shots';
require('fs').mkdirSync(shots, { recursive: true });
(async () => {
  const browser = await chromium.launch({ args: ['--use-gl=angle', '--use-angle=swiftshader', '--ignore-gpu-blocklist'] });
  const page = await browser.newPage({ viewport: { width: 1600, height: 1900 } }); page.setDefaultTimeout(60000);
  const errors = []; page.on('console', m => { if (m.type() === 'error') errors.push(m.text().slice(0, 300)); }); page.on('pageerror', e => errors.push('PAGEERROR ' + e.message.slice(0, 300)));
  await page.goto(base, { waitUntil: 'networkidle' }); await page.waitForSelector('.title-screen', { timeout: 90000 });
  await page.evaluate(() => { try { localStorage.setItem('praetoria.primer.seen', '1'); } catch (e) {} });
  await page.click('.scenario:has-text("' + title + '")'); await page.click('button.begin'); await page.waitForSelector('.gs-shell', { timeout: 90000 });
  const rail = name => page.evaluate(n => { const el = [...document.querySelectorAll('.rail-btn')].find(e => e.innerText.trim().toLowerCase() === n.toLowerCase()); if (el) el.click(); return !!el; }, name);
  const closeWindows = async () => { for (let i = 0; i < 8; i++) { const had = await page.evaluate(() => { const c = document.querySelector('.win-close') || document.querySelector('.overlay .btn.primary'); if (c) c.click(); return !!c; }); if (!had) break; await page.waitForTimeout(150); } };
  const turnNo = () => page.evaluate(() => { const m = document.body.innerText.match(/Turn (\d+)/); return m ? parseInt(m[1], 10) : -1; });
  const endTurn = async () => { await closeWindows(); const before = await turnNo(); await page.evaluate(() => { const b = [...document.querySelectorAll('button')].find(e => e.innerText.includes('End Turn')); if (b) b.click(); }); for (let i = 0; i < 800; i++) { if (await turnNo().catch(() => -1) > before) break; await page.waitForTimeout(50); } await closeWindows(); };
  const press = text => page.evaluate(t => { const b = [...document.querySelectorAll('.panel.ambitions button')].find(e => !e.disabled && e.innerText.trim().startsWith(t)); if (b) b.click(); return !!b; }, text);
  let answered = 0, shot = false;
  for (let y = 0; y < years; y++) {
    await closeWindows(); await rail('Dynasty'); await page.waitForTimeout(500);
    const text = await page.evaluate(() => (document.querySelector('.panel.ambitions') || {}).innerText || 'NO PANEL'); if (/NaN|undefined|Exception|@\(/.test(text)) console.log('SUSPICIOUS TEXT in year ' + y);
    const rows = await page.evaluate(() => [...document.querySelectorAll('.panel.ambitions .suit')].map(e => e.innerText.replace(/\s+/g, ' ').slice(0, 150)));
    console.log('year ' + y + ': ' + (rows.length ? rows.join(' || ') : 'nobody has set their mind on anything yet'));
    const asking = await page.evaluate(() => { const r = document.querySelector('.panel.ambitions .suit.incoming'); return r ? r.innerText.replace(/\s+/g, ' ').match(/[A-Z][a-z]+ asks[^.]*\./)?.[0] || 'someone asks' : ''; });
    if (asking) {
      if (!shot) { await page.screenshot({ path: shots + '/ambitions-asking.png' }); shot = true; }
      const did = answered % 2 === 0 ? (await press('Grant it') ? 'granted' : (await press('Say no') ? 'could not grant; said no' : 'nothing')) : (await press('Say no') ? 'said no' : 'nothing');
      console.log('   → ' + asking + ' ' + did); answered++; await page.waitForTimeout(300);
    }
    await rail('House'); await page.waitForTimeout(200); await endTurn();
  }
  await rail('Dynasty'); await page.waitForTimeout(500);
  const box = await page.evaluate(() => { const r = document.querySelector('.panel.ambitions').getBoundingClientRect(); return { x: Math.max(0, r.x), y: Math.max(0, r.y), width: r.width, height: Math.min(r.height, 1200) }; });
  await page.screenshot({ path: shots + '/ambitions-panel.png', clip: box });
  console.log('errors: ' + errors.length); errors.slice(0, 5).forEach(e => console.log('   ' + e));
  await browser.close();
})().catch(e => { console.error('FAILED ' + e.message.split('\n')[0]); process.exit(1); });
