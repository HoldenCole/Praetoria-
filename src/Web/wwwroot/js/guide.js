// Praetoria: the guide (docs/ACADEMY.md, "The first years"; docs/WEB_CLIENT.md, "The first years"). Two things, both drawn in a layer of
// their own over the page, outside Blazor's tree, and neither changes the page:
//
//  - ring(selectors, id): "Show me" lights a control with a pulsing brass ring. Each selector is looked for again on every frame, so a
//    control that appears only once its window has opened is ringed when it does, and one the page draws again is followed. A ring goes
//    out when what it rings is pressed, after twelve seconds, on Escape, or when clear() is called (a step has ticked). While a box or
//    an event window stands over the game the rings wait under it.
//  - spot(selectors): the primer points at a part of the screen: everything else is dimmed and that part is ringed (the hole follows the
//    part as it moves). unspot() takes it away.
//
// lit() says what is ringed now and where, so tools/browser/firststeps.js can check that Show me lights the right control.
(() => {
  const RING_MS = 12000;
  let rings = [], spot = null, raf = 0, until = 0, label = '';
  const shown = el => {
    if (!el || !el.isConnected) return null;
    const r = el.getBoundingClientRect(); if (r.width <= 0 || r.height <= 0) return null;
    const cs = getComputedStyle(el); if (cs.display === 'none' || cs.visibility === 'hidden') return null;
    return r;
  };
  /** The first element a selector finds that is on the screen. */
  const find = sel => { try { for (const el of document.querySelectorAll(sel)) if (shown(el)) return el; } catch (e) { /* a selector the page does not know */ } return null; };
  function layer() {
    let l = document.getElementById('guide-layer');
    if (!l) { l = document.createElement('div'); l.id = 'guide-layer'; l.setAttribute('aria-hidden', 'true'); document.body.appendChild(l); }
    return l;
  }
  const put = (box, r, pad) => { box.style.left = (r.left - pad) + 'px'; box.style.top = (r.top - pad) + 'px'; box.style.width = (r.width + 2 * pad) + 'px'; box.style.height = (r.height + 2 * pad) + 'px'; };
  /** A box, an event window or a profile over the game (not the primer while it points). */
  const covered = () => !!document.querySelector('.box-veil:not(.hb-point), .overlay:not(.box-veil), .confirm-veil, .ev-veil, .profile.pc');

  /** Place every ring and the spotlight where their controls are now. Run each frame while anything is lit, and at once when it is asked
   *  for, so a ring is on the screen the moment Show me is pressed even when the frames come slowly (a busy machine drawing the map). */
  function frame() {
    raf = 0;
    try { place(); }
    finally { if (rings.length || spot) raf = requestAnimationFrame(frame); }
  }
  function place() {
    const now = performance.now();
    if (rings.length && now > until) clearRings();
    const under = rings.length && covered();
    for (const g of rings) {
      const el = find(g.sel); const r = el && shown(el);
      g.el = el;
      if (!r || under) { g.box.style.display = 'none'; continue; }
      g.box.style.display = ''; put(g.box, r, 4);
    }
    if (spot) {
      // the union of what the primer points at, so a part made of two pieces (the ruler's portrait and the house's name) is one hole
      let u = null;
      for (const sel of spot.sels) { const el = find(sel); const r = el && shown(el); if (!r) continue; u = u ? { left: Math.min(u.left, r.left), top: Math.min(u.top, r.top), right: Math.max(u.right, r.right), bottom: Math.max(u.bottom, r.bottom) } : { left: r.left, top: r.top, right: r.right, bottom: r.bottom }; }
      if (u) { spot.box.classList.remove('none'); put(spot.box, { left: u.left, top: u.top, width: u.right - u.left, height: u.bottom - u.top }, 6); }
      else spot.box.classList.add('none');   // nothing to point at on this screen: the whole is dimmed evenly
    }
  }
  const go = () => { try { place(); } catch (e) { /* the next frame tries again */ } if (!raf) raf = requestAnimationFrame(frame); };

  function clearRings() { for (const g of rings) g.box.remove(); rings = []; label = ''; }
  /** A press on what a ring lights puts that ring out: the player has found it. */
  window.addEventListener('pointerdown', e => {
    if (!rings.length) return;
    const t = e.target;
    for (const g of rings.slice()) if (g.el && (g.el === t || g.el.contains(t))) { g.box.remove(); rings.splice(rings.indexOf(g), 1); }
  }, true);
  window.addEventListener('keydown', e => { if (e.key === 'Escape' && rings.length) clearRings(); }, true);

  window.praetoriaGuide = {
    /** Light each of these controls with a pulsing ring (a step's "Show me"). */
    ring(selectors, id) {
      clearRings();
      const l = layer(); label = id || '';
      for (const sel of selectors || []) {
        const box = document.createElement('div'); box.className = 'guide-ring'; box.dataset.sel = sel; box.dataset.step = label; box.style.display = 'none';
        l.appendChild(box); rings.push({ sel, box, el: null });
      }
      until = performance.now() + RING_MS; go();
    },
    /** Put out every ring (a step has ticked, or the list was put away). */
    clear() { clearRings(); },
    /** The primer points at a part of the screen: the rest dimmed, that part ringed. */
    spot(selectors) {
      const l = layer();
      if (!spot) { const box = document.createElement('div'); box.className = 'guide-spot'; l.appendChild(box); spot = { box, sels: [] }; }
      spot.sels = selectors || []; go();
    },
    unspot() { if (spot) { spot.box.remove(); spot = null; } },
    /** What is ringed and lit now: for the browser scripts. */
    lit() {
      const out = [];
      for (const g of rings) { const r = g.box.style.display === 'none' ? null : g.box.getBoundingClientRect(); const el = find(g.sel); const t = el && shown(el); out.push({ sel: g.sel, step: g.box.dataset.step, shown: !!r, over: !!(r && t && r.left <= t.left && r.top <= t.top && r.right >= t.right && r.bottom >= t.bottom) }); }
      // the spotlight glides from one part to the next, so where it points is read from the parts themselves
      if (spot) { const hit = spot.sels.map(s => { const el = find(s); return el && shown(el); }).filter(Boolean); if (hit.length) { const r = hit[0]; out.push({ sel: spot.sels.join(', '), step: 'primer', shown: true, spot: true, rect: { x: r.left, y: r.top, w: r.width, h: r.height } }); } }
      return out;
    }
  };
})();
