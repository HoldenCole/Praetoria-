// Praetoria — tooltips (docs/ART_DIRECTION.md, "Tooltips"; docs/WEB_CLIENT.md). Anything with a title gets the game's own tooltip in
// place of the browser's: in the game's frame, with a heading when the text has one ("Health — what it is") and one line per line
// of a breakdown. The windows are sparse because the detail lives here.
//
// A tooltip may hold a tooltip. A word of the game's own (legitimacy, a bench, a war score: the glossary of content/academy) is
// underlined where it stands in a tooltip, the tooltip stays while the pointer is on it, and resting on the word opens what it
// means beside it, and so on down. A label in a window that is exactly one of those words explains itself the same way.
// Floating UI (js/vendor, MIT) places each one: beside the pointer or the word, turned over or slid along when the edge of the
// screen is in the way, and never taller than the room there is.
(() => {
  const FUI = window.FloatingUIDOM, SHOW = 180, TERM = 220, GRACE = 320, DEPTH = 4, LINKS = 5;
  const stack = [];                       // the open tooltips, outermost first: { el, owner, sticky }
  let showT = 0, closeT = 0, pending = null, watch = 0;

  // ---- the game's own words ----
  let terms = [], byId = {}, rx = null; const byWord = {};
  // words too ordinary to underline wherever they fall; a label that is exactly one of them still explains itself
  const COMMON = new Set(['credits', 'year', 'head', 'world', 'system', 'law', 'rank', 'title', 'weight', 'city', 'front', 'flag', 'company', 'shares', 'agents', 'client', 'nation',
    'generation', 'skill', 'trait', 'health', 'fleet', 'hull', 'loan', 'claim', 'senate', 'reach', 'exchange', 'motion', 'slot', 'treasury actions', 'its clock', 'the senate', 'the reach']);
  const esc = s => s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  const wordsOf = t => { const whole = t.term.toLowerCase(); const parts = whole.replace(/^the /, '').split(/, | and /).map(p => p.replace(/^the /, '').trim()).filter(p => p.length > 2); return [whole, ...parts]; };
  const learn = list => {
    terms = list; byId = {}; const words = [];
    for (const t of list) { byId[t.id] = t; for (const w of wordsOf(t)) { if (!(w in byWord)) byWord[w] = t.id; if (!COMMON.has(w)) words.push(w); } }
    words.sort((a, b) => b.length - a.length);
    rx = words.length ? new RegExp('\\b(' + words.map(w => esc(w) + (w.endsWith('s') ? '?' : 's?')).join('|') + ')\\b', 'gi') : null;
  };
  const idOf = word => { const w = word.toLowerCase(); return byWord[w] || byWord[w.replace(/s$/, '')] || byWord[w + 's'] || null; };
  fetch('content/academy/academy.json').then(r => r.ok ? r.json() : null).then(j => { if (j && Array.isArray(j.glossary)) learn(j.glossary); }).catch(() => { });

  /** Text into a node, with the game's words underlined: each once, a few at most, and never the word the tooltip is about. */
  const write = (node, s, skip, budget) => {
    if (!rx || budget.left <= 0) { node.append(s); return; }
    let last = 0; rx.lastIndex = 0;
    for (let m; (m = rx.exec(s)) && budget.left > 0;) {
      const id = idOf(m[1]); if (!id || skip.has(id)) continue;
      skip.add(id); budget.left--; node.append(s.slice(last, m.index));
      const span = document.createElement('span'); span.className = 'term'; span.dataset.gloss = id; span.textContent = m[1]; node.append(span); last = m.index + m[1].length;
    }
    node.append(s.slice(last));
  };

  // ---- what a thing says ----
  /** A glyph of the sprite in index.html, in one of the six ramps. */
  const glyph = (key, tone) => '<svg class="icon ico ' + (tone || '') + '" viewBox="0 0 512 512" aria-hidden="true"><use href="#i-' + key + '"/></svg>';
  const text = el => { if (el.hasAttribute('title')) { const t = el.getAttribute('title'); el.removeAttribute('title'); if (t) el.dataset.tip = t; else delete el.dataset.tip; } return el.dataset.tip || ''; };
  /** A leaf of a window whose whole text is one of the game's words: it explains itself. */
  const labelTerm = el => {
    if (!terms.length || !el || el.nodeType !== 1 || el.childElementCount > 0 || /^(INPUT|SELECT|OPTION|TEXTAREA)$/.test(el.tagName) || el.closest('.gtip, svg, [data-term], .gl-labels')) return null;
    const s = (el.textContent || '').trim().replace(/[:.]$/, ''); if (s.length < 3 || s.length > 34) return null;
    return byWord[s.toLowerCase()] || null;
  };
  const build = (tipEl, owner, level) => {
    const skip = new Set(stack.slice(0, level).map(t => t.gloss).filter(Boolean)), budget = { left: LINKS };
    const gloss = owner.dataset.gloss && byId[owner.dataset.gloss] ? owner.dataset.gloss : (!text(owner) ? labelTerm(owner) : null);
    tipEl.textContent = '';
    if (gloss) {
      const t = byId[gloss]; skip.add(gloss);
      const h = document.createElement('b'); h.textContent = t.term; const d = document.createElement('div'); write(d, t.means, skip, budget);
      const f = document.createElement('div'); f.className = 'foot'; f.textContent = 'The glossary · ' + t.group; tipEl.append(h, d, f);
      return gloss;
    }
    const s = text(owner); if (!s) return null;
    // a tooltip that opens with one of the game's words is about that word: it is not underlined in its own explanation
    if (rx) { rx.lastIndex = 0; const first = rx.exec(s); if (first && first.index === 0) { const own = idOf(first[1]); if (own) skip.add(own); } }
    const lines = s.split('\n');
    // "[icon] Title — text": a head in brass small capitals with its glyph (docs/VISUAL_SYSTEM.md 4.14)
    const ic = /^\[([\w-]+)\] (.+)$/.exec(lines[0]); let headIcon = '';
    if (ic && !/ = /.test(lines[0])) { headIcon = ic[1]; lines[0] = ic[2]; }
    const m = /^(.{2,40}?) (?:—|·) (.+)$/.exec(lines[0]);
    if (m && (lines.length === 1 && m[2].length > 24 || headIcon)) { const h = document.createElement('b'); if (headIcon) h.innerHTML = glyph(headIcon, 'ivory'); h.append(m[1]); tipEl.appendChild(h); lines[0] = m[2]; const own = idOf(m[1]); if (own) skip.add(own); }
    else if (headIcon) { const h = document.createElement('b'); h.innerHTML = glyph(headIcon, 'ivory'); h.append(lines.shift()); tipEl.appendChild(h); if (!lines.length) return ''; }
    for (const ln of lines) {
      // "[icon] words = value": an effect line, its value red or green by its sign; "key: Press [1] to choose it": the footer of keys
      const fx = /^\[([\w-]+)\] (.+?) = (.+)$/.exec(ln);
      if (fx) { const d = document.createElement('div'); d.className = 'fx'; const w = document.createElement('span'); write(w, fx[2], skip, budget); const v = document.createElement('span'); v.className = 'v' + (/^[−-]/.test(fx[3]) ? ' down' : /^\+/.test(fx[3]) ? ' up' : ''); v.textContent = fx[3]; d.innerHTML = glyph(fx[1], ''); d.append(w, v); tipEl.appendChild(d); continue; }
      if (/^key: /.test(ln)) { const f = document.createElement('div'); f.className = 'foot'; for (const part of ln.slice(5).split(/(\[[^\]]+\])/)) { if (/^\[.+\]$/.test(part)) { const k = document.createElement('kbd'); k.textContent = part.slice(1, -1); f.append(k); } else if (part) f.append(part); } tipEl.appendChild(f); continue; }
      // "* sentence": one item of a list (the alerts of a badge), a whole sentence with a brass diamond before it, never split in two
      if (/^\* /.test(ln)) { const d = document.createElement('div'); d.className = 'li'; write(d, ln.slice(2), skip, budget); tipEl.appendChild(d); continue; }
      const d = document.createElement('div'); const k = /^(.+?): (.+)$/.exec(ln);
      if (k && lines.length > 2) { d.className = 'row'; const a = document.createElement('span'), b = document.createElement('span'); write(a, k[1], skip, budget); b.textContent = k[2]; d.append(a, b); }
      else write(d, ln, skip, budget);
      tipEl.appendChild(d);
    }
    return '';
  };

  // ---- where it goes ----
  const at = (x, y) => ({ getBoundingClientRect: () => ({ x, y, left: x, top: y, right: x, bottom: y, width: 0, height: 0 }) });
  const place = (tipEl, ref, placement, gap) => {
    if (FUI) {
      const arr = tipEl.querySelector(':scope > .arr');
      const mw = [FUI.offset(gap + 14), FUI.flip({ padding: 8 }), FUI.shift({ padding: 8 }),
        FUI.size({ padding: 8, apply({ availableHeight }) { const h = Math.max(140, Math.floor(availableHeight)); tipEl.style.maxHeight = h + 'px'; tipEl.classList.toggle('scrolls', tipEl.scrollHeight > h); } })];  // only a tooltip taller than the screen scrolls (and loses its arrow)
      if (arr && FUI.arrow) mw.push(FUI.arrow({ element: arr, padding: 18 }));
      return FUI.computePosition(ref, tipEl, { strategy: 'fixed', placement, middleware: mw })
        .then(({ x, y, placement: p, middlewareData: md }) => {
          tipEl.style.left = Math.round(x) + 'px'; tipEl.style.top = Math.round(y) + 'px'; const side = p.split('-')[0]; tipEl.dataset.side = side;
          // the arrow sits on the frame's outer edge facing the anchor; its offsets are from the padding box, inside the 16px frame
          if (arr && md && md.arrow) { const b = 16; arr.style.left = side === 'top' || side === 'bottom' ? (md.arrow.x != null ? Math.round(md.arrow.x - b) + 'px' : '') : ''; arr.style.top = side === 'left' || side === 'right' ? (md.arrow.y != null ? Math.round(md.arrow.y - b) + 'px' : '') : ''; }
        });
    }
    // without the library: below and right of the point, turned back at the edges
    const o = ref.getBoundingClientRect(), r = tipEl.getBoundingClientRect(), W = innerWidth, H = innerHeight; let l = o.right + gap, t = o.bottom + gap;
    if (l + r.width > W - 8) l = Math.max(8, o.left - r.width - gap); if (t + r.height > H - 8) t = Math.max(8, o.top - r.height - gap);
    tipEl.style.left = l + 'px'; tipEl.style.top = t + 'px'; return Promise.resolve();
  };

  // ---- opening and closing ----
  const prune = n => { clearTimeout(closeT); while (stack.length > n) stack.pop().el.remove(); if (!stack.length) { clearInterval(watch); watch = 0; } };
  const hideAll = () => { clearTimeout(showT); pending = null; prune(0); };
  /** Keep the outermost n. One that holds a word to rest on, or has a tooltip of its own open, is given a moment: the pointer may be on its way in. */
  const keep = n => {
    clearTimeout(closeT); if (stack.length <= n) return;
    if (stack[n].sticky || stack.length > n + 1) closeT = setTimeout(() => prune(n), GRACE); else prune(n);
  };
  const open = (level, owner, x, y, byKey) => {
    if (!document.contains(owner)) return; prune(level);
    const el = document.createElement('div'); el.className = 'gtip'; el.style.zIndex = 200 + level; el.dataset.level = level;
    const gloss = build(el, owner, level); if (gloss === null) return; const arr = document.createElement('i'); arr.className = 'arr'; el.appendChild(arr);
    const sticky = level + 1 < DEPTH && !!el.querySelector('.term'); if (sticky) el.classList.add('sticky');
    document.body.appendChild(el); stack.push({ el, owner, sticky, gloss });
    const r = owner.getBoundingClientRect(), wide = r.width > 260 || r.height > 90;
    // one inside another opens beside its parent, level with the word, so the parent can still be read
    const beside = () => { const pr = stack[level - 1].el.getBoundingClientRect(), tr = owner.getBoundingClientRect(); return { getBoundingClientRect: () => ({ x: pr.left, y: tr.top, left: pr.left, right: pr.right, top: tr.top, bottom: tr.bottom, width: pr.width, height: tr.height }) }; };
    const done = level > 0 ? place(el, beside(), 'right-start', 6) : byKey || !wide && !isFinite(x) ? place(el, owner, 'bottom-start', 8) : place(el, at(x, y), 'bottom-start', 16);
    done.then(() => el.classList.add('on'));
    if (!watch) watch = setInterval(() => { if (stack.length && !document.contains(stack[0].owner)) hideAll(); }, 700);   // the window was drawn again under it
  };
  const levelOf = tipEl => stack.findIndex(t => t.el === tipEl);

  document.addEventListener('mouseover', e => {
    const target = e.target; if (!target || !target.closest) return;
    const inTip = target.closest('.gtip'); let owner = null, level = 0, held = 0;
    if (inTip) {
      const i = levelOf(inTip); if (i < 0) return; held = i + 1;
      const term = target.closest('.term'); if (term && i + 1 < DEPTH) { owner = term; level = i + 1; if (stack[level] && stack[level].owner === term) held = level + 1; }
    } else {
      owner = target.closest('[title], [data-tip], [data-gloss]'); if (owner && owner.closest('svg')) owner = null;
      if (!owner && labelTerm(target)) owner = target;
      if (owner && stack[0] && stack[0].owner === owner) held = 1;
    }
    keep(held);
    if (owner && stack[level] && stack[level].owner === owner) { clearTimeout(showT); pending = null; return; }
    if (pending === owner) return; clearTimeout(showT); pending = owner; if (!owner) return;
    const x = e.clientX, y = e.clientY;
    showT = setTimeout(() => { if (pending !== owner) return; pending = null; open(level, owner, x, y, false); }, level > 0 ? TERM : SHOW);
  });
  // the keyboard: what has the focus says what it is, anchored to itself
  document.addEventListener('focusin', e => { const el = e.target; if (!el || !el.matches || !el.matches(':focus-visible') || !el.matches('[title], [data-tip], [data-gloss]')) return; hideAll(); open(0, el, NaN, NaN, true); });
  document.addEventListener('focusout', () => { if (stack.length && !stack[0].el.matches(':hover')) hideAll(); });
  document.addEventListener('mousedown', e => { if (!(e.target.closest && e.target.closest('.gtip'))) hideAll(); }, true);
  document.addEventListener('scroll', e => { if (!(e.target.closest && e.target.closest('.gtip'))) hideAll(); }, true);
  document.addEventListener('keydown', e => { if (!['Shift', 'Control', 'Alt', 'Meta', 'Tab'].includes(e.key)) hideAll(); }, true);
  document.documentElement.addEventListener('mouseleave', hideAll);
  window.addEventListener('blur', hideAll);
  window.praetoriaTips = { hide: hideAll, depth: () => stack.length, terms: () => terms.length, placed: () => !!FUI };
})();
