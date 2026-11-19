// Praetoria — tooltips (docs/ART_DIRECTION.md, "The shell, the windows and the art"). Anything with a title gets the game's own
// tooltip in place of the browser's: at once, in the game's frame, with a heading when the text has one ("Health — what it is")
// and one line per line of a breakdown. The windows are sparse because the detail lives here.
(() => {
  let tip = null, owner = null, timer = 0;
  const text = el => { if (el.hasAttribute('title')) { const t = el.getAttribute('title'); el.removeAttribute('title'); if (t) el.dataset.tip = t; else delete el.dataset.tip; } return el.dataset.tip || ''; };
  const hide = () => { clearTimeout(timer); owner = null; if (tip) tip.classList.remove('on'); };
  const place = (x, y) => { const r = tip.getBoundingClientRect(), W = innerWidth, H = innerHeight; let l = x + 16, t = y + 18; if (l + r.width > W - 8) l = Math.max(8, x - r.width - 12); if (t + r.height > H - 8) t = Math.max(8, y - r.height - 12); tip.style.left = l + 'px'; tip.style.top = t + 'px'; };
  const show = (el, x, y) => {
    const s = text(el); if (!s) return;
    if (!tip) { tip = document.createElement('div'); tip.className = 'gtip'; document.body.appendChild(tip); }
    tip.textContent = ''; const lines = s.split('\n'); const m = /^(.{2,40}?) (?:—|·) (.+)$/.exec(lines[0]);
    if (m && lines.length === 1 && m[2].length > 24) { const h = document.createElement('b'); h.textContent = m[1]; tip.appendChild(h); lines[0] = m[2]; }
    for (const ln of lines) { const d = document.createElement('div'); const k = /^(.+?): (.+)$/.exec(ln); if (k && lines.length > 2) { d.className = 'row'; const a = document.createElement('span'), b = document.createElement('span'); a.textContent = k[1]; b.textContent = k[2]; d.append(a, b); } else d.textContent = ln; tip.appendChild(d); }
    tip.classList.add('on'); place(x, y);
  };
  document.addEventListener('mouseover', e => { const el = e.target.closest && e.target.closest('[title], [data-tip]'); if (el === owner) return; hide(); if (!el || el.closest('svg')) return; owner = el; const x = e.clientX, y = e.clientY; timer = setTimeout(() => { if (owner === el && document.contains(el)) show(el, x, y); }, 180); });
  document.addEventListener('mousemove', e => { if (tip && tip.classList.contains('on')) place(e.clientX, e.clientY); });
  document.addEventListener('mousedown', hide, true); document.addEventListener('scroll', hide, true); document.addEventListener('keydown', hide, true);
})();
