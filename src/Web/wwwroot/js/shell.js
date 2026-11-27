// Praetoria: the shell's own script (docs/VISUAL_SYSTEM.md 3). It tells the map which rectangles of the screen the interface covers
// (the docked window above all), works the console (the seat, fit, the map modes, the minimap and its caption), zooms on Page Up
// and Page Down, and says when the screen is under 1500px wide. It reads the map through window.praetoriaSpace and never draws it:
// the drawing is space.js's.
//
// The covered rectangles go three ways, so the map can take them however it is built: a `praetoria:covered` event on window (its
// detail: { window, topbar, rail, outliner, console, alerts, toasts }, each { x, y, w, h } in CSS pixels or null), the attribute
// data-covered="x,y,w,h" (the docked window's) on .gs-shell.game, and praetoriaSpace.cover(detail) when the map offers it.
(() => {
  const galaxy = () => { const s = window.praetoriaSpace; if (!s || !s.galaxies) return null; for (const g of s.galaxies.values()) return g; return null; };
  const shown = el => { if (!el) return null; const cs = getComputedStyle(el); if (cs.display === 'none' || cs.visibility === 'hidden') return null; const r = el.getBoundingClientRect(); return r.width > 0 && r.height > 0 ? { x: Math.round(r.left), y: Math.round(r.top), w: Math.round(r.width), h: Math.round(r.height) } : null; };
  const q = s => document.querySelector(s);
  let ref = null, mini = null, last = '', mode = '', narrow = null, timer = 0;

  /** What of the screen the interface covers now. */
  function covered() {
    return {
      window: shown(q('.gs-shell.game .screen.window')) || shown(q('.gs-shell.game .ui-win.docked')) || shown(q('.gs-shell.game .map-side:not(.idle)')),
      topbar: shown(q('.gs-shell.game .topbar')), rail: shown(q('.gs-shell.game > .rail')), outliner: shown(q('.gs-shell.game .outliner')),
      console: shown(q('.gs-shell.game .console')), alerts: shown(q('.gs-shell.game .alerts-row')), toasts: shown(q('.gs-shell.game .toasts')),
    };
  }
  /** The map's mode as its camera makes it: from far off the realms, the houses in the middle distance, the systems close to. */
  function modeOf(g) { if (!g) return 'houses'; if (g.sys) return 'system'; return g.distance > 1700 ? 'realms' : g.distance < 700 ? 'systems' : 'houses'; }
  /** The system nearest the middle of the view (inside a system, that system). */
  function nearest(g) {
    if (!g || !g.model || !g.model.systems) return '';
    if (g.sys && g.sys.name) return g.sys.name;
    let best = null, bd = Infinity; const tx = g.target.x, ty = g.target.y;
    for (const s of g.model.systems) { const d = Math.hypot(s.x - tx, -s.y - ty); if (d < bd) { bd = d; best = s; } }
    return best ? best.name : '';
  }
  function tick() {
    if (!document.body.contains(q('.gs-shell.game'))) return;
    const c = covered(), key = JSON.stringify(c);
    if (key !== last) {
      last = key; const shell = q('.gs-shell.game'), w = c.window;
      if (shell) shell.dataset.covered = w ? `${w.x},${w.y},${w.w},${w.h}` : '';
      window.dispatchEvent(new CustomEvent('praetoria:covered', { detail: c }));
      const s = window.praetoriaSpace; if (s && typeof s.cover === 'function') { try { s.cover(c); } catch (e) { /* the map's own affair */ } }
    }
    const g = galaxy();
    if (g && mini && document.body.contains(mini) && g.mini !== mini && typeof g.setMinimap === 'function') { g.setMinimap(mini); g.dirty = true; }
    const m = modeOf(g); if (m !== mode) { mode = m; ref?.invokeMethodAsync('MapMode', m).catch(() => { }); }
    const cap = q('.gs-shell.game .console [data-near]'); if (cap) { const n = nearest(g); if (cap.textContent !== n) cap.textContent = n; }
    const nw = window.innerWidth < 1500; if (nw !== narrow) { narrow = nw; ref?.invokeMethodAsync('Narrow', nw).catch(() => { }); }
  }
  const zoom = f => { const g = galaxy(); if (g) g.zoom(f); };
  window.addEventListener('keydown', e => {
    if (!q('.gs-shell.game') || (e.target && ['INPUT', 'SELECT', 'TEXTAREA'].includes(e.target.tagName)) || e.ctrlKey || e.metaKey || e.altKey) return;
    if (e.key === 'PageUp') { zoom(0.8); e.preventDefault(); } else if (e.key === 'PageDown') { zoom(1.25); e.preventDefault(); }
  });

  window.praetoriaShell = {
    /** Begin watching: the page's .NET side (for the mode and the width) and the console's minimap canvas. */
    watch(dotnet, canvas) { ref = dotnet; mini = canvas; mode = ''; narrow = null; last = ''; if (!timer) timer = setInterval(tick, 350); tick(); },
    covered,
    /** A tool of the console. */
    map(what) {
      const g = galaxy(); if (!g) return;
      if (what === 'home') g.home();
      else if (what === 'fit') g.fit();
      else if (what === 'realms') { if (!g.sys) { g.distance = Math.max(g.distance, 2300); g.updateCamera(); } }
      else if (what === 'houses') { if (!g.sys) { if (g.distance > 1700 || g.distance < 700) g.distance = 1150; g.updateCamera(); } }
      else if (what === 'regions') { const b = q('.map-tools button[title="The regions of the Reach"]'); if (b) b.click(); }
      else if (what === 'in') zoom(0.8); else if (what === 'out') zoom(1.25);
      setTimeout(tick, 60);
    },
  };
})();
