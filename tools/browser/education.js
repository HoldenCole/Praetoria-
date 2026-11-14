// Drives education and skills: on the character sheet, chooses a study and studies it hard; on the Dynasty screen, sets a child
// a course against their bent and hires a tutor; then plays some years and reports how skills moved.
//
//   node education.js ["Start title"] [years]
//
// Needs the build served locally (see README.md here). Screenshots go to PRAETORIA_SHOTS (default ./shots).
const { chromium } = require('playwright');
const title = process.argv[2] || 'The Old Lion'; const years = parseInt(process.argv[3] || '5', 10);
const base = process.env.PRAETORIA_URL || 'http://localhost:8112/'; const shots = process.env.PRAETORIA_SHOTS || './shots';
require('fs').mkdirSync(shots, { recursive: true });
(async () => {
  const browser = await chromium.launch({ args: ['--use-gl=angle', '--use-angle=swiftshader', '--ignore-gpu-blocklist'] });
  const page = await browser.newPage({ viewport: { width: 1600, height: 1700 } }); page.setDefaultTimeout(60000);
  const errors = []; page.on('console', m => { if (m.type() === 'error') errors.push(m.text().slice(0, 300)); }); page.on('pageerror', e => errors.push('PAGEERROR ' + e.message.slice(0, 300)));
  await page.goto(base, { waitUntil: 'networkidle' }); await page.waitForSelector('.title-screen', { timeout: 90000 });
  await page.evaluate(() => { try { localStorage.setItem('praetoria.primer.seen', '1'); } catch (e) {} });
  await page.click('.scenario:has-text("' + title + '")'); await page.click('button.begin'); await page.waitForSelector('.gs-shell', { timeout: 90000 });
  const rail = name => page.evaluate(n => { const el = [...document.querySelectorAll('.rail-btn')].find(e => e.innerText.trim().toLowerCase() === n.toLowerCase()); if (el) el.click(); return !!el; }, name);
  const closeWindows = async () => { for (let i = 0; i < 8; i++) { const had = await page.evaluate(() => { const c = document.querySelector('.win-close') || document.querySelector('.overlay .btn.primary'); if (c) c.click(); return !!c; }); if (!had) break; await page.waitForTimeout(150); } };
  const turnNo = () => page.evaluate(() => { const m = document.body.innerText.match(/Turn (\d+)/); return m ? parseInt(m[1], 10) : -1; });
  const endTurn = async () => { await closeWindows(); const before = await turnNo(); await page.evaluate(() => { const b = [...document.querySelectorAll('button')].find(e => e.innerText.includes('End Turn')); if (b) b.click(); }); for (let i = 0; i < 800; i++) { if (await turnNo().catch(() => -1) > before) break; await page.waitForTimeout(50); } await closeWindows(); };
  const skills = () => page.evaluate(() => [...document.querySelectorAll('.learn-row')].map(r => r.innerText.replace(/\s+/g, ' ').trim()).slice(0, 6).join(' | '));
  const press = (sel, text) => page.evaluate(([s, t]) => { const b = [...document.querySelectorAll(s)].find(e => !e.disabled && e.innerText.trim().startsWith(t)); if (b) b.click(); return b ? b.innerText.replace(/\s+/g, ' ').slice(0, 50) : ''; }, [sel, text]);
  await closeWindows(); await rail('You'); await page.waitForTimeout(700);
  let text = await page.evaluate(() => (document.querySelector('.panel.learning') || {}).innerText || 'NO PANEL');
  console.log('learning panel: ' + (/NaN|undefined|Exception|@\(/.test(text) ? 'SUSPICIOUS TEXT' : 'ok') + ' · ' + await skills());
  console.log('chose to study: ' + await page.evaluate(() => { const row = [...document.querySelectorAll('.panel.learning .ac-fact')].find(e => e.innerText.startsWith('You study')); const b = row && row.querySelector('button.opt:not(.here)'); if (b) b.click(); return b ? b.innerText : 'nothing to choose'; }));
  await page.waitForTimeout(300); console.log('studied hard: ' + (await press('.panel.learning button', 'Study') || 'not offered'));
  await page.waitForTimeout(300); await page.screenshot({ path: shots + '/education-you.png' });
  await rail('Dynasty'); await page.waitForTimeout(700);
  text = await page.evaluate(() => (document.querySelector('.panel.education') || {}).innerText || 'NO PANEL');
  console.log('education panel: ' + (/NaN|undefined|Exception|@\(/.test(text) ? 'SUSPICIOUS TEXT' : 'ok') + ' · ' + text.split('\n')[0]);
  console.log('course set against bent: ' + await page.evaluate(() => { const b = [...document.querySelectorAll('.panel.education button.opt')].find(e => e.title === 'against their bent'); if (b) b.click(); return b ? b.innerText : 'no child at lessons'; }));
  await page.waitForTimeout(300); console.log('tutor: ' + (await press('.panel.education button', 'the best') || 'none hired'));
  await page.waitForTimeout(300); await page.screenshot({ path: shots + '/education-dynasty.png' });
  for (let y = 0; y < years; y++) { await rail('House'); await page.waitForTimeout(200); await endTurn(); }
  await rail('You'); await page.waitForTimeout(600); console.log('after ' + years + ' years: ' + await skills());
  await rail('Dynasty'); await page.waitForTimeout(600);
  console.log('children: ' + await page.evaluate(() => [...document.querySelectorAll('.panel.education .suit .suit-who')].map(e => e.innerText.replace(/\s+/g, ' ').slice(0, 170)).join(' || ')));
  await page.screenshot({ path: shots + '/education-end.png' });
  console.log('errors: ' + errors.length); errors.slice(0, 5).forEach(e => console.log('   ' + e));
  await browser.close();
})().catch(e => { console.error('FAILED ' + e.message.split('\n')[0]); process.exit(1); });
