// The dashboard audit (docs/DASHBOARDS.md): opens every screen on the rail at an ordinary desktop size, mid-game, and for each
// takes a screenshot of exactly what a player sees without scrolling and measures it: how much of the screen is empty, how
// many numbers, charts and buttons are on it, and how far the page runs below the fold.
//
//   node audit.js ["Start title"] [years]
//
// Needs the build served locally (see README.md here). Screenshots go to PRAETORIA_SHOTS (default ./shots).
const { chromium } = require('playwright');
const title = process.argv[2] || 'The Old Lion'; const years = parseInt(process.argv[3] || '3', 10);
const W = parseInt(process.env.PRAETORIA_W || '1440', 10), H = parseInt(process.env.PRAETORIA_H || '900', 10);
const base = process.env.PRAETORIA_URL || 'http://localhost:8112/'; const shots = process.env.PRAETORIA_SHOTS || './shots';
require('fs').mkdirSync(shots, { recursive: true });
(async () => {
  const browser = await chromium.launch({ args: ['--use-gl=angle', '--use-angle=swiftshader', '--ignore-gpu-blocklist'] });
  const page = await browser.newPage({ viewport: { width: W, height: H } }); page.setDefaultTimeout(60000);
  const errors = []; page.on('console', m => { if (m.type() === 'error') errors.push(m.text().slice(0, 300)); }); page.on('pageerror', e => errors.push('PAGEERROR ' + e.message.slice(0, 300)));
  await page.goto(base, { waitUntil: 'networkidle' }); await page.waitForSelector('.title-screen', { timeout: 90000 });
  await page.evaluate(() => { try { localStorage.setItem('praetoria.primer.seen', '1'); } catch (e) {} });
  await page.click('.scenario:has-text("' + title + '")'); await page.click('button.begin'); await page.waitForSelector('.gs-shell', { timeout: 90000 });
  const rail = name => page.evaluate(n => { const el = [...document.querySelectorAll('.rail-btn')].find(e => e.innerText.trim().toLowerCase() === n.toLowerCase()); if (el) el.click(); return !!el; }, name);
  const closeWindows = async () => { for (let i = 0; i < 8; i++) { const had = await page.evaluate(() => { const c = document.querySelector('.win-close') || document.querySelector('.overlay .btn.primary'); if (c) c.click(); return !!c; }); if (!had) break; await page.waitForTimeout(150); } };
  const turnNo = () => page.evaluate(() => { const m = document.body.innerText.match(/Turn (\d+)/); return m ? parseInt(m[1], 10) : -1; });
  const endTurn = async () => { await closeWindows(); const before = await turnNo(); await page.evaluate(() => { const b = document.querySelector('button.endturn'); if (b) b.click(); }); for (let i = 0; i < 800; i++) { if (await turnNo().catch(() => -1) > before) break; await page.waitForTimeout(50); } await closeWindows(); };
  // find the screen each panel lives on
  const find = async sel => { for (const s of ['Dynasty', 'You', 'House', 'Houses']) { await rail(s); await page.waitForTimeout(450); if (await page.evaluate(q => !!document.querySelector(q), sel)) return s; } return ''; };
  const press = (sel, text) => page.evaluate(([s, t]) => { const b = [...document.querySelectorAll(s + ' button')].find(e => !e.disabled && e.innerText.trim().startsWith(t)); if (b) b.click(); return b ? b.innerText.trim() : ''; }, [sel, text]);
  const text = sel => page.evaluate(s => { const el = document.querySelector(s); return el ? el.innerText.replace(/\s+/g, ' ') : 'NO PANEL'; }, sel);
  const shot = async (sel, name) => { const box = await page.evaluate(s => { const el = document.querySelector(s); if (!el) return null; el.scrollIntoView({ block: 'start' }); const r = el.getBoundingClientRect(); return { x: Math.max(0, r.x), y: Math.max(0, r.y), width: r.width, height: Math.min(r.height, 1300) }; }, sel); if (box) await page.screenshot({ path: shots + '/' + name + '.png', clip: box }); };

  await closeWindows();
  for (let y = 0; y < years; y++) await endTurn();
  await closeWindows();
  const only = (process.env.PRAETORIA_ONLY || '').toUpperCase().split(',').filter(Boolean);   // PRAETORIA_ONLY=MILITARY,HOUSE to look at some screens only
  const names = (await page.evaluate(() => [...document.querySelectorAll('.rail-btn')].map(e => e.innerText.trim()).filter(Boolean))).filter(n => only.length === 0 || only.includes(n.toUpperCase()));
  console.log('screens on the rail: ' + names.join(', ') + ' · turn ' + await turnNo() + ' · ' + W + 'x' + H);
  // what a player sees without scrolling: sample the viewport on a grid and ask what is under each point
  const measure = () => page.evaluate(() => {
    const W = innerWidth, H = innerHeight, cols = 48, rows = 30; let empty = 0, total = 0;
    const big = el => { const r = el.getBoundingClientRect(); return r.width * r.height > W * H * 0.12; };
    const hasOwnText = el => [...el.childNodes].some(n => n.nodeType === 3 && n.textContent.trim().length > 0);
    for (let i = 0; i < cols; i++) for (let j = 0; j < rows; j++) {
      const x = (i + 0.5) * W / cols, y = (j + 0.5) * H / rows; const el = document.elementFromPoint(x, y); total++;
      if (!el) { empty++; continue; }
      const tag = el.tagName.toLowerCase(); const drawn = ['svg', 'path', 'circle', 'rect', 'line', 'canvas', 'img', 'button', 'input', 'polygon', 'text', 'g'].includes(tag);
      if (!drawn && big(el) && !hasOwnText(el)) empty++;          // a large container with nothing of its own under the point
      else if (!drawn && !hasOwnText(el) && el.children.length === 0 && !getComputedStyle(el).backgroundImage.includes('gradient') && (el.innerText || '').trim() === '') { const bg = getComputedStyle(el).backgroundColor; if (bg === 'rgba(0, 0, 0, 0)') empty++; }
    }
    const main = document.querySelector('.gs-main') || document.querySelector('main') || document.body;
    const text = main.innerText || ''; const words = text.split(/\s+/).filter(Boolean).length; const numbers = (text.match(/\b\d[\d,.]*%?/g) || []).length;
    const vis = el => { const r = el.getBoundingClientRect(); return r.bottom > 0 && r.top < H && r.width > 0 && r.height > 0; };
    const inView = sel => [...main.querySelectorAll(sel)].filter(vis).length;
    const scroller = [...document.querySelectorAll('*')].filter(e => e.scrollHeight > e.clientHeight + 40 && ['auto', 'scroll'].includes(getComputedStyle(e).overflowY)).filter(e => e.clientHeight > innerHeight * 0.5).sort((a, b) => b.clientHeight - a.clientHeight)[0];
    return { emptyPct: Math.round(100 * empty / total), words, numbers, buttons: inView('button'), charts: inView('svg, canvas'), bars: inView('.bar, .meter, progress, [class*="bar-"], [class*="gauge"]'), panels: inView('.panel, section'),
      pages: scroller ? +(scroller.scrollHeight / scroller.clientHeight).toFixed(1) : 1 };
  });
  const rows = [];
  for (const n of names) {
    await rail(n); await page.waitForTimeout(900); await closeWindows();
    const m = await measure(); const file = shots + '/audit-' + n.toLowerCase().replace(/[^a-z]+/g, '-') + '.png';
    await page.screenshot({ path: file }); rows.push({ n, ...m });
    console.log(n.padEnd(12) + ' empty ' + String(m.emptyPct).padStart(3) + '% · words ' + String(m.words).padStart(4) + ' · numbers ' + String(m.numbers).padStart(3) + ' · buttons ' + String(m.buttons).padStart(3) + ' · charts ' + String(m.charts).padStart(2) + ' · panels ' + String(m.panels).padStart(2) + ' · runs to ' + m.pages + ' screens');
    // the tabs inside a screen are screens too
    const tabs = await page.evaluate(() => [...document.querySelectorAll('.ex-tabs button, .tabs button')].map(e => e.innerText.trim()).filter(Boolean));
    for (let t = 1; t < tabs.length && t < 5; t++) {
      await page.evaluate(i => { const b = [...document.querySelectorAll('.ex-tabs button, .tabs button')][i]; if (b) b.click(); }, t); await page.waitForTimeout(700);
      const mt = await measure(); const f2 = shots + '/audit-' + n.toLowerCase().replace(/[^a-z]+/g, '-') + '-tab' + t + '.png'; await page.screenshot({ path: f2 });
      console.log(('  ' + tabs[t].slice(0, 26)).padEnd(30) + ' empty ' + String(mt.emptyPct).padStart(3) + '% · words ' + String(mt.words).padStart(4) + ' · numbers ' + String(mt.numbers).padStart(3) + ' · buttons ' + String(mt.buttons).padStart(3) + ' · charts ' + String(mt.charts).padStart(2) + ' · runs to ' + mt.pages + ' screens');
    }
  }
  // an alert should land on the thing it is about: press each and say which screen and which tab it opened
  if (!only.length) {
    const alerts = await page.evaluate(() => [...document.querySelectorAll('.ol-alert')].map(e => e.innerText.trim()));
    for (let i = 0; i < alerts.length; i++) {
      await page.evaluate(k => { const b = document.querySelectorAll('.ol-alert')[k]; if (b) b.click(); }, i); await page.waitForTimeout(500); await closeWindows();
      const at = await page.evaluate(() => ({ rail: (document.querySelector('.rail-btn.active, .rail-btn.here, .rail-btn.on') || {}).innerText || '?', tab: (document.querySelector('.ex-tabs .rung.here') || {}).innerText || '' }));
      console.log('alert: ' + alerts[i].slice(0, 60).padEnd(62) + ' -> ' + at.rail.trim() + (at.tab ? ' · ' + at.tab.trim() : ''));
    }
  }
  console.log('console errors: ' + (errors.length ? errors.slice(0, 5).join(' || ') : 'none'));
  await browser.close();
})();
