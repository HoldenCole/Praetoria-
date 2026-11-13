// Drives the character workshop on the title screen: every tab, every preset, both modes, then begins a saga with one and
// plays a few turns.
//
//   node workshop.js ["Preset name"] [turns] [academy]     academy: begin at the Academy and stop once its screen is up
//
// Needs the build served locally (see README.md here). Screenshots go to PRAETORIA_SHOTS (default ./shots).
const { chromium } = require('playwright');
const preset = process.argv[2] || 'The Heir'; const turns = parseInt(process.argv[3] || '3', 10);
const base = process.env.PRAETORIA_URL || 'http://localhost:8112/'; const shots = process.env.PRAETORIA_SHOTS || './shots';
require('fs').mkdirSync(shots, { recursive: true });
(async () => {
  const browser = await chromium.launch({ args: ['--use-gl=angle', '--use-angle=swiftshader', '--ignore-gpu-blocklist'] });
  const page = await browser.newPage({ viewport: { width: 1600, height: 1100 } }); page.setDefaultTimeout(60000);
  const errors = []; page.on('console', m => { if (m.type() === 'error') errors.push(m.text().slice(0, 300)); }); page.on('pageerror', e => errors.push('PAGEERROR ' + e.message.slice(0, 300)));
  await page.goto(base, { waitUntil: 'networkidle' }); await page.waitForSelector('.title-screen', { timeout: 90000 });
  await page.evaluate(() => { try { localStorage.setItem('praetoria.primer.seen', '1'); } catch (e) {} });
  const click = (sel, text) => page.evaluate(([s, t]) => { const el = [...document.querySelectorAll(s)].find(e => !t || e.innerText.trim().startsWith(t)); if (el) el.click(); return !!el; }, [sel, text]);
  const power = () => page.evaluate(() => (document.querySelector('.ws-power') || {}).innerText || '');
  const problems = () => page.evaluate(() => [...document.querySelectorAll('.ws-problems div')].map(e => e.innerText));
  await click('.scenario.custom'); await page.waitForSelector('.workshop');
  console.log('opened · ' + (await power()).replace(/\n/g, ' '));
  for (const tab of ['The person', 'The face', 'The house', 'What it holds', 'The family', 'The world']) {
    await click('.workshop .ex-tabs .rung', tab); await page.waitForTimeout(250);
    const text = await page.evaluate(() => document.querySelector('.workshop').innerText);
    console.log(tab + ': ' + (/NaN|undefined|Exception|@\(/.test(text) ? 'SUSPICIOUS TEXT' : 'ok') + ' · ' + text.length + ' chars');
    await page.screenshot({ path: shots + '/workshop-' + tab.toLowerCase().replace(/ /g, '-') + '.png' });
  }
  for (const name of ['The Heir', 'The Officer', 'The New Man', 'The Colossus']) {
    await click('.workshop .ws-row .chip', name); await page.waitForTimeout(300);
    console.log(name + ': ' + (await power()).replace(/\n/g, ' ').slice(0, 90) + ' · problems ' + (await problems()).length);
  }
  await page.screenshot({ path: shots + '/workshop-colossus.png' });
  // standard must object to the Colossus; free must not
  await click('.workshop .ws-modes .chip', 'Standard'); await page.waitForTimeout(300); const std = await problems(); console.log('the Colossus under Standard: ' + std.length + ' objection(s), e.g. "' + (std[0] || '') + '"');
  const blocked = await page.evaluate(() => document.querySelector('button.begin').disabled); console.log('Begin disabled while there are objections: ' + blocked);
  await click('.workshop .ws-row .chip', 'Throw the dice'); await page.waitForTimeout(300); console.log('dice (standard): ' + (await power()).replace(/\n/g, ' ').slice(0, 70) + ' · problems ' + (await problems()).length);
  await click('.workshop .ws-row .chip', preset); await page.waitForTimeout(300);
  await click('.workshop .ex-tabs .rung', 'The face'); await click('.workshop .chip', 'a scar'); await click('.workshop .swatch'); await page.waitForTimeout(400);
  await page.screenshot({ path: shots + '/workshop-face.png' });
  if (process.argv[4] === 'academy') {
    await click('.workshop .ex-tabs .rung', 'The world'); await click('.workshop .chip', 'At the'); await page.waitForTimeout(300); console.log('academy start: problems ' + (await problems()).length);
    await click('button.begin'); await page.waitForSelector('.gs-shell', { timeout: 90000 }); await page.waitForTimeout(800);
    const text = await page.evaluate(() => document.body.innerText.replace(/\n/g, ' ').slice(0, 160)); console.log('academy screen: ' + (await page.$('.academy-shell') ? 'up' : 'MISSING') + ' · ' + text);
    await page.screenshot({ path: shots + '/custom-academy.png' }); console.log('errors: ' + errors.length); errors.slice(0, 5).forEach(e => console.log('   ' + e)); await browser.close(); return;
  }
  await click('button.begin'); await page.waitForSelector('.gs-shell', { timeout: 90000 }); await page.waitForTimeout(800);
  const rail = name => page.evaluate(n => { const el = [...document.querySelectorAll('.rail-btn')].find(e => e.innerText.toLowerCase().includes(n.toLowerCase())); if (el) el.click(); return !!el; }, name);
  const closeWindows = async () => { for (let i = 0; i < 8; i++) { const had = await page.evaluate(() => { const c = document.querySelector('.win-close') || document.querySelector('.overlay .btn.primary'); if (c) c.click(); return !!c; }); if (!had) break; await page.waitForTimeout(150); } };
  const turnNo = () => page.evaluate(() => { const m = document.body.innerText.match(/Turn (\d+)/); return m ? parseInt(m[1], 10) : -1; });
  await closeWindows(); await rail('House'); await page.waitForTimeout(500);
  for (let t = 0; t < turns; t++) { await closeWindows(); const before = await turnNo(); await page.evaluate(() => { const b = [...document.querySelectorAll('button')].find(e => e.innerText.includes('End Turn')); if (b) b.click(); }); for (let i = 0; i < 600; i++) { if (await turnNo().catch(() => -1) > before) break; await page.waitForTimeout(50); } }
  await closeWindows();
  for (const s of ['House', 'You', 'Dynasty', 'Senate']) { await rail(s); await page.waitForTimeout(600); const stats = await page.evaluate(() => [...document.querySelectorAll('.dh-stats .stat')].slice(0, 4).map(e => e.innerText.replace(/\n/g, ' ')).join(' | ')); const text = await page.evaluate(() => (document.querySelector('.dash') || document.body).innerText); console.log(s + ': ' + (/NaN|undefined|Exception|@\(/.test(text) ? 'SUSPICIOUS TEXT' : 'ok') + ' · ' + stats.slice(0, 140)); await page.screenshot({ path: shots + '/custom-' + s.toLowerCase() + '.png' }); }
  console.log('errors: ' + errors.length); errors.slice(0, 5).forEach(e => console.log('   ' + e));
  await browser.close();
})().catch(e => { console.error('FAILED ' + e.message.split('\n')[0]); process.exit(1); });
