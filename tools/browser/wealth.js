// Drives the purses, heirlooms and will panel on the House screen: moves money to the head's own purse, sets the allowance, writes
// a will and the custom of the estate, then plays some years and reports what the family did with their own money.
//
//   node wealth.js ["Start title"] [years]
//
// Needs the build served locally (see README.md here). Screenshots go to PRAETORIA_SHOTS (default ./shots).
const { chromium } = require('playwright');
const title = process.argv[2] || 'The Old Lion'; const years = parseInt(process.argv[3] || '4', 10);
const base = require('./address')(); const shots = process.env.PRAETORIA_SHOTS || './shots';
require('fs').mkdirSync(shots, { recursive: true });
(async () => {
  const browser = await chromium.launch({ args: ['--use-gl=angle', '--use-angle=swiftshader', '--ignore-gpu-blocklist'] });
  const page = await browser.newPage({ viewport: { width: 1600, height: 2200 } }); page.setDefaultTimeout(60000);
  const errors = []; page.on('console', m => { if (m.type() === 'error') errors.push(m.text().slice(0, 300)); }); page.on('pageerror', e => errors.push('PAGEERROR ' + e.message.slice(0, 300)));
  await page.goto(base, { waitUntil: 'networkidle' }); await page.waitForSelector('.title-screen', { timeout: 90000 });
  await page.evaluate(() => { try { localStorage.setItem('praetoria.primer.seen', '1'); } catch (e) {} });
  await page.click('.scenario:has-text("' + title + '")'); await page.click('button.begin'); await page.waitForSelector('.gs-shell', { timeout: 90000 });
  const rail = name => page.evaluate(n => { const el = [...document.querySelectorAll('.rail-btn')].find(e => e.innerText.trim().toLowerCase() === n.toLowerCase()); if (el) el.click(); return !!el; }, name);
  // the House window keeps its household and its purses on tabs (the shared Tabs part, docs/VISUAL_SYSTEM.md 4.4)
  const tab = async name => { await page.waitForTimeout(300); await page.evaluate(n => { const t = [...document.querySelectorAll('.ex-tabs .rung, .screen.window .ui-tab')].find(e => e.innerText.toLowerCase().includes(n.toLowerCase())); if (t) t.click(); }, name); };
  const closeWindows = async () => { for (let i = 0; i < 8; i++) { const had = await page.evaluate(() => { const c = document.querySelector('.win-close') || document.querySelector('.overlay .btn.primary'); if (c) c.click(); return !!c; }); if (!had) break; await page.waitForTimeout(150); } };
  const turnNo = () => page.evaluate(() => { const d = document.querySelector('[data-turn]'); if (d) return parseInt(d.dataset.turn, 10); const m = document.body.innerText.match(/Turn (\d+)/); return m ? parseInt(m[1], 10) : -1; });
  // the treasury this script draws on can end the year short, and End Year then asks first (ConfirmBox): it ends the year anyway
  let short = 0;
  const endTurn = async () => { await closeWindows(); const before = await turnNo(); await page.evaluate(() => { const b = document.querySelector('button.endturn'); if (b) b.click(); }); for (let i = 0; i < 800; i++) { if (await turnNo().catch(() => -1) > before) break; if (await page.evaluate(() => { const y = document.querySelector('.confirm-box [data-confirm="yes"]'); if (y) y.click(); return !!y; })) short++; await page.waitForTimeout(50); } await closeWindows(); };
  const press = text => page.evaluate(t => { const b = [...document.querySelectorAll('.panel.wealth button')].find(e => !e.disabled && e.innerText.trim().toLowerCase().startsWith(t.toLowerCase())); if (b) b.click(); return b ? b.innerText.replace(/\s+/g, ' ').slice(0, 50) : ''; }, text);
  const head = () => page.evaluate(() => ((document.querySelector('.panel.wealth [data-purse-head]') || document.querySelector('.panel.wealth h3') || {}).innerText || 'NO PANEL').replace(/\s+/g, ' '));
  await closeWindows(); await rail('House'); await tab('Purse'); await page.waitForTimeout(700);
  const text = await page.evaluate(() => (document.querySelector('.panel.wealth') || {}).innerText || 'NO PANEL');
  console.log('panel: ' + (/NaN|undefined|Exception|@\(/.test(text) ? 'SUSPICIOUS TEXT' : 'ok') + ' · ' + await head());
  console.log('took: ' + (await press('take 5') || 'could not')); await page.waitForTimeout(200);
  console.log('allowance: ' + (await press('generous') || 'could not')); await page.waitForTimeout(200);
  // a thing made to order: enough into the purse for a shrine, then have one made (docs/WEALTH.md "New things")
  for (let i = 0; i < 7; i++) { await press('take 5'); await page.waitForTimeout(300); }
  console.log('made to order: ' + (await press('A shrine') || 'could not: ' + await page.evaluate(() => { const b = [...document.querySelectorAll('.panel.wealth button')].find(e => e.innerText.trim().startsWith('A shrine')); return b ? b.title : 'no button'; }))); await page.waitForTimeout(300);
  console.log('heirlooms: ' + await page.evaluate(() => { const sec = document.querySelector('.panel.wealth [data-purse-sec="heirlooms"]'); return sec ? [...sec.querySelectorAll('.suit .ui-row, .suit-acts, .ui-empty')].map(n => n.innerText.replace(/\s+/g, ' ').slice(0, 80)).join(' || ') : 'NO SECTION'; }));
  // the will is a row a person with a select for their share (docs/VISUAL_SYSTEM.md 4.12): open the first one's and choose half
  console.log('legatee picked: ' + await page.evaluate(() => { const row = document.querySelector('.panel.wealth [data-legacies] .ui-row'); const b = row && row.querySelector('.ui-select-btn'); if (b) b.click(); return row ? row.querySelector('.ui-row-nm').innerText : 'nobody to name'; }));
  await page.waitForTimeout(300); console.log('will: ' + (await press('half') || 'not set')); await page.waitForTimeout(200);
  console.log('estate: ' + (await press('Shared out') || 'could not')); await page.waitForTimeout(300);
  console.log('now: ' + await head()); await page.locator('.screen.window').screenshot({ path: shots + '/wealth-begin.png', timeout: 180000 });
  for (let y = 0; y < years; y++) { await endTurn(); await rail('House'); await tab('Purse'); await page.waitForTimeout(300); }
  console.log('after ' + years + ' years (turn ' + await turnNo() + (short ? ', ' + short + ' of them ended short when asked' : '') + '): ' + await head());
  // the treasury of a poor start does not hold a shrine at once: try again with the years' income
  for (let i = 0; i < 6; i++) { await press('take 5'); await page.waitForTimeout(300); }
  console.log('made to order, later: ' + (await press('A shrine') || 'could not: ' + await page.evaluate(() => { const b = [...document.querySelectorAll('.panel.wealth button')].find(e => e.innerText.trim().startsWith('A shrine')); return b ? b.title : 'no button'; }))); await page.waitForTimeout(400);
  // shares in the head's own name: buy one of whatever is offered, and say what the panel then shows (docs/WEALTH.md)
  for (let i = 0; i < 4; i++) { await press('take 5'); await page.waitForTimeout(300); }
  // shares of your own are one table with one order bar (docs/VISUAL_SYSTEM.md 4.13): pick each offered company in turn and press Buy on the bar
  const offer = await page.evaluate(async () => { const wait = ms => new Promise(r => setTimeout(r, ms)); const box = document.querySelector('.panel.wealth [data-buy-own]'); if (!box) return 'NO TABLE'; let first = ''; for (const tr of [...box.querySelectorAll('tbody tr')]) { tr.click(); await wait(250); const b = [...box.querySelectorAll('.ui-order button')].find(e => /^Buy/.test(e.innerText.trim())); if (!b) continue; if (!first) first = b.title; if (!b.disabled) { b.click(); return (tr.querySelector('td').innerText + ' · ' + b.innerText).replace(/\s+/g, ' '); } } return 'nothing affordable: ' + (first || 'no company listed'); });
  await page.waitForTimeout(400);
  console.log('own shares, bought: ' + offer);
  console.log('own shares, held: ' + await page.evaluate(() => { const sec = document.querySelector('.panel.wealth [data-purse-sec="shares"]'); return sec ? [...sec.querySelectorAll('.purse-row, .ui-empty')].map(n => n.innerText.replace(/\s+/g, ' ').slice(0, 160)).join(' || ') || 'NOTHING HELD' : 'NO SECTION'; }));
  console.log('heirlooms now: ' + await page.evaluate(() => [...document.querySelectorAll('.panel.wealth .suit .ui-row')].map(e => e.innerText.replace(/\s+/g, ' ').slice(0, 200)).join(' || ')));
  // a thing given to the head of another house is a favour asked (docs/WEALTH.md "A gift that asks")
  const gave = await page.evaluate(() => { const b = [...document.querySelectorAll('.panel.wealth .suit .suit-acts button')].find(e => !e.disabled && / of /.test(e.innerText) && !/Sell|Send it home/.test(e.innerText)); if (b) b.click(); return b ? b.innerText.replace(/\s+/g, ' ') : 'nobody to give it to'; });
  await page.waitForTimeout(400);
  console.log('gave the shrine to: ' + gave + ' · ' + await page.evaluate(() => { const el = document.querySelector('.panel.wealth [data-favours]'); return el ? el.innerText.replace(/\s+/g, ' ') : 'no favours line'; }));
  // the favour that gift bought is called in for money (docs/WEALTH.md "A favour called in")
  const called = await page.evaluate(() => { const b = [...document.querySelectorAll('.panel.wealth button')].find(e => e.innerText.includes('Call it in for')); if (b && !b.disabled) b.click(); return b ? (b.disabled ? 'cannot: ' + b.title : b.innerText.replace(/\s+/g, ' ')) : 'no button'; });
  await page.waitForTimeout(400);
  console.log('called the favour in: ' + called + ' · favours line now: ' + await page.evaluate(() => { const el = [...document.querySelectorAll('.panel.wealth [data-favours]')].find(e => /owe you a favour/i.test(e.innerText)); return el ? 'still shown' : 'gone'; }));
  console.log('their purses: ' + await page.evaluate(() => [...document.querySelectorAll('.panel.wealth .purse-row:not(.own-share)')].map(e => e.innerText.replace(/\s+/g, ' ').slice(0, 90)).join(' || ')));
  // the window as it stands, the purse tab at its head (an element shot: the map behind it is slow to draw without a graphics card)
  await page.evaluate(() => { const b = document.querySelector('.screen.window .ui-wbody'); if (b) b.scrollTop = 0; }); await page.waitForTimeout(300);
  await page.locator('.screen.window').screenshot({ path: shots + '/wealth-panel.png', timeout: 180000 });
  console.log('errors: ' + errors.length); errors.slice(0, 5).forEach(e => console.log('   ' + e));
  await browser.close();
})().catch(e => { console.error('FAILED ' + e.message.split('\n')[0]); process.exit(1); });
