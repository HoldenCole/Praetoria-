// A long-lived browser on the published game, driven over HTTP so one game can be played across many commands.
//   node driver.js <port> "<scenario name>"        then:  curl -s localhost:<port>/ui
const { chromium } = require('playwright'); const http = require('http'); const url = require('url');
const port = +process.argv[2]; const scenario = process.argv[3] || 'The Old Lion';
const OUTLINE = (max) => {
  const vis = e => { const r = e.getBoundingClientRect(); const s = getComputedStyle(e); return r.width > 1 && r.height > 1 && s.visibility !== 'hidden' && s.display !== 'none' && r.bottom > 0 && r.right > 0; };
  const where = e => { const c = e.closest('.modal, .event, .interstitial, .screen.window, .map-side, .outliner, .topbar, .rail, .title-screen, .creation, .academy'); return c ? (c.className.split(' ').slice(0, 2).join('.')) : 'page'; };
  const els = [...document.querySelectorAll('button, select, a.rung, input[type=range], input[type=checkbox], [role=button], .scenario, .sysp-row')].filter(vis);
  els.forEach((e, i) => e.setAttribute('data-pi', i));
  const controls = els.slice(0, max).map((e, i) => ({ i, w: where(e), t: (e.tagName === 'SELECT' ? 'select[' + [...e.options].map(o => o.text).slice(0, 12).join('|') + ']=' + (e.selectedOptions[0]?.text || '') : (e.innerText || e.value || e.getAttribute('aria-label') || '').replace(/\s+/g, ' ').trim()).slice(0, 90), off: e.disabled ? 1 : undefined, why: (e.title || '').slice(0, 140) || undefined }));
  const top = document.querySelector('.topbar'); const front = [...document.querySelectorAll('.modal, .event, .interstitial')].filter(vis).pop() || [...document.querySelectorAll('.screen.window')].filter(vis).pop() || [...document.querySelectorAll('.map-side')].filter(vis).pop();
  return { top: top ? top.innerText.replace(/\s+/g, ' ').slice(0, 260) : '', front: front ? front.className.split(' ').slice(0, 3).join('.') : 'map', controls, more: els.length > max ? els.length - max : 0 };
};
(async () => {
  const browser = await chromium.launch({ args: ['--use-gl=angle', '--use-angle=swiftshader', '--ignore-gpu-blocklist'] });
  const page = await browser.newPage({ viewport: { width: 1600, height: 1000 }, deviceScaleFactor: 1 }); page.setDefaultTimeout(60000);
  const errors = []; page.on('pageerror', e => errors.push(e.message.slice(0, 300))); page.on('console', m => { if (m.type() === 'error') errors.push(m.text().slice(0, 200)); });
  await page.goto(process.env.PRAETORIA_URL || 'http://localhost:8112/', { waitUntil: 'networkidle' }); await page.waitForSelector('.title-screen', { timeout: 120000 });
  await page.evaluate(() => { try { localStorage.setItem('praetoria.primer.seen', '1'); } catch (e) {} });
  if (scenario !== 'title') { await page.click(`.scenario:has-text("${scenario}")`); await page.click('button.begin'); await page.waitForTimeout(3000); }
  const settle = () => page.waitForTimeout(700);
  http.createServer(async (req, res) => {
    const u = url.parse(req.url, true); let body = ''; req.on('data', d => body += d); await new Promise(r => req.on('end', r));
    const send = o => { res.setHeader('content-type', 'application/json'); res.end(typeof o === 'string' ? o : JSON.stringify(o)); };
    try {
      const q = u.query;
      if (u.pathname === '/ui') return send(await page.evaluate(OUTLINE, +(q.max || 120)));
      if (u.pathname === '/text') return send(await page.evaluate(([sel, n]) => { const vis = e => e.getBoundingClientRect().width > 1; const f = sel ? document.querySelector(sel) : ([...document.querySelectorAll('.modal, .event, .interstitial')].filter(vis).pop() || [...document.querySelectorAll('.screen.window')].filter(vis).pop() || [...document.querySelectorAll('.map-side')].filter(vis).pop() || document.body); return f ? f.innerText.replace(/[ \t]+/g, ' ').replace(/\n{2,}/g, '\n').slice(0, n) : '(nothing matches)'; }, [q.sel || '', +(q.n || 4000)]));
      if (u.pathname === '/click') { const r = await page.evaluate(([i, text, sel]) => { let e = null; if (i !== '') e = document.querySelector('[data-pi="' + i + '"]'); else if (sel) e = document.querySelector(sel); else { const t = text.toLowerCase(); e = [...document.querySelectorAll('button, a.rung, [role=button], .scenario, .sysp-row')].filter(x => x.getBoundingClientRect().width > 1 && !x.disabled).find(x => (x.innerText || '').replace(/\s+/g, ' ').trim().toLowerCase().startsWith(t)); } if (!e) return 'no such control'; if (e.disabled) return 'disabled: ' + (e.title || 'no reason given'); e.click(); return 'clicked: ' + (e.innerText || '').replace(/\s+/g, ' ').trim().slice(0, 80); }, [q.i ?? '', q.text || '', q.sel || '']); await settle(); return send({ r, ui: q.ui === '0' ? undefined : await page.evaluate(OUTLINE, +(q.max || 120)) }); }
      if (u.pathname === '/choose') { const r = await page.evaluate(([i, v]) => { const e = document.querySelector('[data-pi="' + i + '"]'); if (!e || e.tagName !== 'SELECT') return 'not a select'; const o = [...e.options].find(o => o.text.toLowerCase().startsWith(v.toLowerCase()) || o.value === v); if (!o) return 'no such option'; e.value = o.value; e.dispatchEvent(new Event('change', { bubbles: true })); return 'chose ' + o.text; }, [q.i, q.v || '']); await settle(); return send({ r }); }
      if (u.pathname === '/select') { await page.evaluate(n => window.praetoriaSpace && window.praetoriaSpace.select(n), q.name); await page.waitForTimeout(1200); return send(await page.evaluate(OUTLINE, +(q.max || 120))); }
      if (u.pathname === '/endturn') { const t0 = Date.now(); const r = await page.evaluate(() => { const b = document.querySelector('button.endturn'); if (!b) return 'no end turn button here'; if (b.disabled) return 'disabled: ' + b.title; b.click(); return 'ended'; }); await page.waitForTimeout(2500); return send({ r, ms: Date.now() - t0, ui: await page.evaluate(OUTLINE, +(q.max || 60)) }); }
      if (u.pathname === '/eval') return send({ r: await page.evaluate(body || q.js) });
      if (u.pathname === '/shot') { await page.screenshot({ path: q.path }); return send({ saved: q.path }); }
      if (u.pathname === '/scroll') { await page.evaluate(([sel, y]) => { const e = document.querySelector(sel); if (e) e.scrollTo(0, y); }, [q.sel || '.screen.window .win-body', +(q.y || 0)]); return send('ok'); }
      if (u.pathname === '/errors') return send(errors);
      if (u.pathname === '/quit') { send('bye'); await browser.close(); process.exit(0); }
      send('routes: /ui /text?sel=&n= /click?i=|text=|sel= /choose?i=&v= /select?name= /endturn /eval(POST js) /shot?path= /scroll?sel=&y= /errors /quit');
    } catch (e) { send({ error: String(e).slice(0, 300) }); }
  }).listen(port, () => console.log('driver ready on ' + port));
})();
