// Portraits and profiles (docs/ART_DIRECTION.md): photographs the You screen, the family tree, the houses, and the profile card a
// portrait opens (yourself, a relative, the head of another house), and checks that a portrait opens a profile and kin open in turn.
//   cd tools/browser && PRAETORIA_SHOTS=/tmp/shots node profiles.js        (the client served on :8112)
const { chromium } = require('playwright');
const out = process.env.PRAETORIA_SHOTS || '/tmp/praetoria-shots'; const title = process.argv[2] || 'The Old Lion';
(async () => {
  require('fs').mkdirSync(out, { recursive: true });
  const browser = await chromium.launch({ args: ['--use-gl=angle', '--use-angle=swiftshader', '--ignore-gpu-blocklist'] });
  const page = await browser.newPage({ viewport: { width: 1600, height: 1100 }, deviceScaleFactor: 1 }); page.setDefaultTimeout(180000);   // a whole-page picture over the map is slow without a graphics card
  const errors = []; page.on('console', m => { if (m.type() === 'error') errors.push(m.text().slice(0, 200)); }); page.on('pageerror', e => errors.push('PAGEERROR ' + e.message.slice(0, 200)));
  const fails = []; const check = (ok, what) => { if (!ok) fails.push(what); };
  await page.goto(require('./address')(), { waitUntil: 'networkidle' }); await page.waitForSelector('.title-screen', { timeout: 90000 });
  await page.evaluate(() => { try { localStorage.setItem('praetoria.primer.seen', '1'); } catch (e) {} });
  await page.click('.scenario:has-text("' + title + '")'); await page.click('button.begin'); await page.waitForSelector('.gs-shell', { timeout: 90000 });
  for (let i = 0; i < 10; i++) { const had = await page.evaluate(() => { const c = document.querySelector('.win-close') || document.querySelector('.overlay .btn.primary'); if (c) c.click(); return !!c; }); if (!had) break; await page.waitForTimeout(300); }
  const rail = async name => { await page.evaluate(n => { const el = [...document.querySelectorAll('.rail-btn')].find(e => new RegExp('^' + n + '$', 'i').test(e.innerText.trim())); if (el) el.click(); }, name); await page.waitForTimeout(1500); };
  const painted = () => page.evaluate(() => [...document.querySelectorAll('.portrait img')].every(e => (e.getAttribute('src') || '').length > 10));
  await rail('you'); check(await painted(), 'a portrait on the You screen was not painted'); await page.screenshot({ path: out + '/you.png' });
  // your own portrait opens your own profile
  await page.click('[data-you-portrait] .portrait'); await page.waitForSelector('.profile', { timeout: 10000 }); await page.waitForTimeout(600);
  check(await page.evaluate(() => !!document.querySelector('.profile h2')?.innerText.trim()), 'the profile has no name'); await page.screenshot({ path: out + '/profile_you.png', animations: 'disabled' });   // the card rises in: finish that before the picture
  // a relative opens from the kin list, in the same card
  const kin = await page.$('.profile .pk'); if (kin) { const before = await page.evaluate(() => document.querySelector('.profile h2').innerText); await kin.click(); await page.waitForTimeout(700);
    check(await page.evaluate(b => document.querySelector('.profile h2').innerText !== b, before), 'a relative did not open from the kin list'); await page.screenshot({ path: out + '/profile_kin.png', animations: 'disabled' }); }
  await page.click('.profile-x'); await page.waitForTimeout(300); check(!(await page.$('.profile')), 'the profile did not close');
  await rail('dynasty'); check(await page.evaluate(() => { const im = [...document.querySelectorAll('.tree image, .tree img')]; return im.length > 0 && im.every(e => (e.getAttribute('href') || e.getAttribute('src') || '').length > 10); }), 'the family tree has no faces'); await page.screenshot({ path: out + '/dynasty.png' });
  await rail('houses'); await page.screenshot({ path: out + '/houses.png' });
  // someone of another house
  await page.evaluate(() => { const r = document.querySelectorAll('.standings tbody tr'); if (r.length > 1) r[1].click(); }); await page.waitForTimeout(700);   // a house's page opens from its row
  const other = await page.evaluate(() => { const all = [...document.querySelectorAll('.portrait.opens')]; const el = all[Math.min(all.length - 1, 3)]; if (el) el.click(); return all.length; });
  if (other > 0) { await page.waitForSelector('.profile', { timeout: 10000 }); await page.waitForTimeout(600); await page.screenshot({ path: out + '/profile_other.png', animations: 'disabled' }); await page.click('.profile-x'); } else fails.push('no portrait on the Houses screen opens a profile');
  await rail('office'); await page.screenshot({ path: out + '/office.png' });
  await page.setViewportSize({ width: 420, height: 900 }); await rail('you'); await page.click('[data-you-portrait] .portrait').catch(() => {}); await page.waitForTimeout(700); await page.screenshot({ path: out + '/profile_phone.png', animations: 'disabled' });
  console.log('console errors: ' + errors.length + ' ' + errors.slice(0, 2).join(' | ')); console.log(fails.length ? 'FAILED: ' + fails.join('; ') : 'profiles: all checks passed'); await browser.close(); process.exit(fails.length || errors.length ? 1 : 0);
})();
