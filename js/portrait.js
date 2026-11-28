// Praetoria — the portrait a person has when the painted library holds none for them (docs/VISUAL_SYSTEM.md 4.15 and 9: "the
// code-drawn faces are not shown anywhere again"). The cameo faces this file used to draw are gone. Every portrait is a painting
// (img/portraits, PortraitLibrary); where none can be found (the library failed to load, or the person has no sex recorded) the plate
// holds this instead: the house colour as a backdrop and the dark shape of head and shoulders against it, with no features, the way
// an empty frame waits for its picture. Canvas 2D, drawn once a colour and cached.
//
// It keeps its old surface, render(spec) and window.praetoriaBust.paint(), because Portrait.razor fills an <img data-bust> with it and
// the old 3D room (js/office.js, kept unused until the owner has seen the painted Office) imports render.
const SIZE = 256;
const cache = new Map();

function hex(c) { const m = /^#?([0-9a-f]{6})$/i.exec(c || ''); if (!m) return [201, 162, 74]; const n = parseInt(m[1], 16); return [n >> 16 & 255, n >> 8 & 255, n & 255]; }
function rgba(c, a) { return `rgba(${c[0] | 0},${c[1] | 0},${c[2] | 0},${a == null ? 1 : a})`; }
function mix(a, b, t) { return [a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t]; }

/** A data URL of the waiting plate for this person: spec is { id, sex, age, accent, alive, ... }; only the colour, the age band and
 *  whether they live change it. */
export function render(spec) {
  const accent = hex(spec && spec.accent), age = spec && spec.age != null ? spec.age : 30, alive = !spec || spec.alive !== false;
  const child = age < 13, key = accent.join(',') + (child ? 'c' : 'a') + (alive ? '' : 'd');
  if (cache.has(key)) return cache.get(key);
  const c = document.createElement('canvas'); c.width = c.height = SIZE; const ctx = c.getContext('2d');
  // the backdrop: the house colour, darkened, lit from above
  const base = alive ? accent : mix(accent, [128, 128, 128], 1);
  const g = ctx.createRadialGradient(SIZE * .5, SIZE * .32, SIZE * .05, SIZE * .5, SIZE * .45, SIZE * .8);
  g.addColorStop(0, rgba(mix(base, [40, 36, 30], .45))); g.addColorStop(1, rgba(mix(base, [6, 8, 12], .86)));
  ctx.fillStyle = g; ctx.fillRect(0, 0, SIZE, SIZE);
  // head and shoulders as one dark shape, no features
  const s = child ? .82 : 1, cx = SIZE * .5, hy = SIZE * (child ? .44 : .4), hr = SIZE * .15 * s;
  ctx.fillStyle = 'rgba(8,10,14,.82)';
  ctx.beginPath(); ctx.ellipse(cx, hy, hr * .86, hr, 0, 0, Math.PI * 2); ctx.fill();
  ctx.beginPath(); ctx.moveTo(cx - hr * .42, hy + hr * .8); ctx.lineTo(cx + hr * .42, hy + hr * .8); ctx.lineTo(cx + hr * .5, hy + hr * 1.35);
  ctx.bezierCurveTo(cx + SIZE * .3 * s, hy + hr * 1.5, cx + SIZE * .4 * s, hy + hr * 2.2, cx + SIZE * .44 * s, SIZE);
  ctx.lineTo(cx - SIZE * .44 * s, SIZE); ctx.bezierCurveTo(cx - SIZE * .4 * s, hy + hr * 2.2, cx - SIZE * .3 * s, hy + hr * 1.5, cx - hr * .5, hy + hr * 1.35); ctx.closePath(); ctx.fill();
  // a rim of the house colour along the shape's edge, so it reads as a figure and not a hole
  ctx.strokeStyle = rgba(mix(base, [255, 240, 210], .25), .35); ctx.lineWidth = 2; ctx.beginPath(); ctx.ellipse(cx, hy, hr * .86, hr, 0, Math.PI * 1.1, Math.PI * 1.9); ctx.stroke();
  const url = c.toDataURL('image/png'); cache.set(key, url); return url;
}

/** Fill every <img data-bust='{"id":..,"accent":..}'> lacking a src, and every SVG <image data-bust> lacking an href. */
export function paint() {
  document.querySelectorAll('img[data-bust], image[data-bust]').forEach(el => { const at = el.tagName.toLowerCase() === 'image' ? 'href' : 'src'; if (el.getAttribute(at)) return; try { el.setAttribute(at, render(JSON.parse(el.getAttribute('data-bust')))); } catch (e) { console.error('portrait', e); } });
}
window.praetoriaBust = { render, paint };
