// Praetoria — portraits (docs/ART_DIRECTION.md, "Portraits and profiles"). A bust in profile, the way a face is struck on a coin or cut
// in a cameo: one line from the brow down the nose to the chin carries the likeness, and that line is thrown from a hash of the
// person's id, so the same face appears wherever they do. Built in layers: a backdrop for their station, dress for their career and
// rank in the house's colour, the head, its features, the hair, the marks of their age, and an expression that follows what they think
// of you. Flat shapes with hard edges, lit from the front, with the house's colour on the back of the head. Five ages of a face
// (child, youth, adult, older, elder). Canvas 2D, drawn once and cached.
//
// The layers (backdrop, dress, head, features, hair) are also the slots painted art would drop into, part for part.
const SIZE = 384, U = SIZE / 256;
const cache = new Map();
let grain = null;

function hash(s) { let h = 2166136261; for (let i = 0; i < s.length; i++) { h ^= s.charCodeAt(i); h = Math.imul(h, 16777619); } return h >>> 0; }
function rng(seed) { let t = seed >>> 0; return () => { t += 0x6D2B79F5; let r = Math.imul(t ^ (t >>> 15), 1 | t); r ^= r + Math.imul(r ^ (r >>> 7), 61 | r); return ((r ^ (r >>> 14)) >>> 0) / 4294967296; }; }
function mix(a, b, t) { return a + (b - a) * t; }
function mixc(a, b, t) { return [mix(a[0], b[0], t), mix(a[1], b[1], t), mix(a[2], b[2], t)]; }
function hex(c) { const m = /^#?([0-9a-f]{6})$/i.exec(c || ''); if (!m) return [201, 162, 74]; const n = parseInt(m[1], 16); return [n >> 16 & 255, n >> 8 & 255, n & 255]; }
function rgba(c, a) { const [r, g, b] = Array.isArray(c) ? c : hex(c); return `rgba(${r | 0},${g | 0},${b | 0},${a == null ? 1 : a})`; }
function shade(c, k) { return [Math.min(255, c[0] * k), Math.min(255, c[1] * k), Math.min(255, c[2] * k)]; }
function soft(ctx, x, y, rx, ry, col, a, rot) {
  ctx.save(); ctx.translate(x, y); if (rot) ctx.rotate(rot); ctx.scale(1, ry / rx);
  const g = ctx.createRadialGradient(0, 0, 0, 0, 0, rx); g.addColorStop(0, rgba(col, a)); g.addColorStop(1, rgba(col, 0)); ctx.fillStyle = g; ctx.beginPath(); ctx.arc(0, 0, rx, 0, 6.3); ctx.fill(); ctx.restore();
}
function quadAt(a, c, b, t) { const u = 1 - t; return [u * u * a[0] + 2 * u * t * c[0] + t * t * b[0], u * u * a[1] + 2 * u * t * c[1] + t * t * b[1]]; }

const SKINS = [[241, 208, 184], [229, 188, 156], [210, 164, 126], [188, 136, 100], [158, 106, 76], [122, 80, 56], [94, 60, 46]];
const HAIRS = [[30, 24, 22], [52, 36, 27], [84, 54, 31], [116, 80, 45], [150, 112, 62], [186, 148, 84], [214, 184, 120], [64, 62, 70], [134, 50, 34]];
const EYES = [[78, 54, 36], [100, 72, 42], [62, 100, 80], [78, 112, 150], [104, 116, 126], [46, 38, 32]];
const BRASS = [196, 156, 70], BRASS2 = [242, 214, 140], DEEP = [30, 18, 30], WARM = [255, 246, 228];

export function render(spec) {
  const look = spec.look || {};   // a face by choice (docs/CHARACTER.md): any feature given here overrides the dice of the id
  const key = JSON.stringify([spec.id, spec.sex, spec.age, spec.accent, spec.alive !== false, spec.career, spec.rank, spec.mood, spec.close, spec.head, spec.laurel, spec.foreign, spec.scar, look.skin, look.hair, look.style, look.beard, look.scar]);
  if (cache.has(key)) return cache.get(key);
  const c = document.createElement('canvas'); c.width = c.height = SIZE; const ctx = c.getContext('2d'); ctx.scale(U, U); ctx.lineCap = 'round'; ctx.lineJoin = 'round';
  const id = spec.id || 'x', r = rng(hash(id)); const female = spec.sex === 'female'; const age = spec.age == null ? 30 : spec.age; const accent = hex(spec.accent);
  const child = age < 13, youth = age >= 13 && age < 20, older = age >= 46, elder = age >= 62;
  const k = child ? 1 : youth ? 0.35 : 0;

  // ---- the dice: always thrown in the same order, so one chosen feature does not reshuffle the rest ----
  const skinRoll = Math.floor(r() * SKINS.length), hairRoll = Math.floor(r() * HAIRS.length), eyeRoll = Math.floor(r() * EYES.length);
  const d = {}; for (const n of ['slope', 'brow', 'nose', 'drop', 'bridge', 'lip', 'chin', 'jaw', 'eye', 'browH', 'style', 'beard', 'bald', 'freckle', 'neck', 'skull', 'long', 'arch']) d[n] = r();
  const skin = SKINS[look.skin >= 0 ? look.skin % SKINS.length : skinRoll];
  let hair = HAIRS[look.hair >= 0 ? look.hair % HAIRS.length : hairRoll];
  const grey = age >= 66 ? 1 : age >= 44 ? Math.min(1, (age - 44) / 22) : 0; if (grey > 0) hair = mixc(hair, [222, 218, 212], grey);
  const eye = EYES[eyeRoll];
  const skinSh = mixc(shade(skin, 0.6), [96, 34, 56], 0.24), skinHi = mixc(skin, WARM, 0.5), ink = mixc(shade(skin, 0.3), DEEP, 0.5), rim = mixc(accent, [255, 255, 255], 0.45);
  const hairSh = mixc(shade(hair, 0.55), DEEP, 0.3), hairHi = mixc(hair, WARM, 0.4);
  const career = spec.foreign ? 'foreign' : child ? 'child' : (spec.career || 'none');
  const mood = spec.mood || 0;   // -2 an enemy's face .. +2 a friend's

  backdrop(ctx, career, accent, r);

  // ---- the line of the face, looking right: brow, nose, lips, chin ----
  const top = mix(32, 46, k), ny = top + mix(58, 63, k);                 // the crown, and the dip between the eyes
  const fx = mix(152, 147, k) - (female ? 1.5 : 0);                      // how far forward that dip sits
  const fh = mix(1, 0.72, k) * (0.96 + d.long * 0.1);                     // the length of the lower face
  const slope = d.slope * 5 * (1 - k) - k * 3.5, sk = d.skull * 4;
  const brow = (female ? 0.8 : 2.8) * (1 - k) + d.brow * 1.6;
  const noseL = mix(female ? 13 : 15.5, 6.5, k) + d.nose * 5.5 * (1 - k * 0.6), noseD = (19 + d.drop * 4 + (elder ? 1.5 : 0)) * fh;
  const bridge = k > 0.5 ? -1 : d.bridge < (female ? 0.18 : 0.36) ? 1 : d.bridge < 0.74 ? 0 : -1;   // hooked, straight, or turned up
  const lip = ((female ? 2.3 : 1.1) + d.lip * 2.2 + k) * (elder ? 0.7 : 1);
  const chin = mix(female ? -0.5 : 2, -2.5, k) + d.chin * 4.5 * (1 - k * 0.6), jowl = elder ? 3 : older ? 1.2 : 0;
  const tipY = ny + noseD, subY = tipY + 6.5 * fh, ulY = subY + 8 * fh, mY = ulY + 4.5 * fh, llY = mY + 5.5 * fh, dipY = llY + 6 * fh, chinY = dipY + 10 * fh, botY = chinY + 8.5 * fh + jowl;
  const jx = mix(104, 108, k) + (female ? 2 : 0), jy = botY - mix(female ? 15 : 13, 13, k) - d.jaw * 5;   // the corner of the jaw
  const tx = fx + noseL, ex = fx - 13.5, ey = ny + 3.5 + k, ew = 7.4 + d.eye * 1.6 + k * 2, eh = (3.9 + d.eye * 1.1 + k * 2) * (mood < 0 ? 1 + mood * 0.09 : 1);
  const nb = botY + mix(female ? 27 : 25, 21, k);                                      // where the neck meets the shoulders
  const nt = female || k > 0 ? 0 : (d.neck - 0.5) * 5;                   // a bull neck or a thin one
  const bt = { x: mix(74, 82, k) + (female ? 4 : 0) - nt, y: ny + 30 }, ft = { x: fx - mix(21, 27, k) - (female ? 4 : 0) + nt, y: botY - 4 };
  const bb = { x: bt.x - 6, y: nb + 4 }, fb = { x: ft.x - 3, y: nb + 6 };

  const front = () => {
    ctx.bezierCurveTo(128, top - 1, fx - 4 - slope, top + 12, fx + 1 - slope * 0.5, ny - 30);                          // the forehead
    ctx.quadraticCurveTo(fx + 3, ny - 16, fx + 2 + brow, ny - 8); ctx.quadraticCurveTo(fx + 2.5 + brow, ny - 2.5, fx, ny);   // the brow, and in under it
    ctx.quadraticCurveTo((fx + tx) / 2 + bridge * 2.2, (ny + tipY) / 2 - bridge * 1.4 - 2, tx, tipY - 2);                 // the bridge
    ctx.bezierCurveTo(tx + 2.4, tipY + 1, tx + 0.5, tipY + 5, tx - 4, tipY + 5); ctx.quadraticCurveTo(fx + 7, tipY + 4.5, fx + 3.5, subY + 0.5);   // the tip, and back under it
    ctx.quadraticCurveTo(fx + 2.6, (subY + ulY) / 2 + 1, fx + 5 + lip, ulY); ctx.quadraticCurveTo(fx + 6.5 + lip, mY - 1.2, fx + 2.6, mY);      // the upper lip
    ctx.quadraticCurveTo(fx + 6.5 + lip * 0.9, mY + 0.8, fx + 5 + lip * 0.85, llY); ctx.quadraticCurveTo(fx + 4 + lip * 0.5, dipY - 0.5, fx + 0.8, dipY);   // the lower
    ctx.quadraticCurveTo(fx, dipY + 4.5, fx + 3 + chin, chinY - 1); ctx.bezierCurveTo(fx + 5.5 + chin, chinY + 4.5, fx + chin, botY, fx - 9, botY);         // the chin
  };
  const head = () => {
    ctx.beginPath(); ctx.moveTo(104, top); front();
    ctx.quadraticCurveTo(fx - 30, botY - 0.5, jx, jy); ctx.quadraticCurveTo(jx - 4, jy - 12, jx - 4, ny + 20); ctx.lineTo(bt.x, ny + 34);
    ctx.bezierCurveTo(54, ny + 12, 54 - sk, top + 6, 104, top); ctx.closePath();
  };

  // ---- hair, beard and baldness ----
  let style; const maleStyles = [0, 1, 2, 3, 7, 4, 0, 1], femaleStyles = [4, 5, 6, 7, 8, 5, 6, 4], childStyles = [0, 2, 8, 5];
  if (look.style >= 0) style = (female ? femaleStyles : maleStyles)[look.style % 8];
  else style = child ? childStyles[Math.floor(d.style * 4)] : (female ? femaleStyles : maleStyles)[Math.floor(d.style * 8)];
  const bald = !female && !child && age > 48 && d.bald < 0.26 && !(look.style >= 0);
  const recede = !female && age > 36 ? Math.min(9, (age - 36) * 0.3) : 0;
  let beard = female || age < 19 ? 0 : (d.beard < 0.42 ? 0 : d.beard < 0.56 ? 1 : d.beard < 0.76 ? 2 : d.beard < 0.9 ? 3 : 4);
  if (look.beard === 0) beard = 0; else if (look.beard === 1 && !female && beard === 0 && age >= 19) beard = 2;

  // ---- the shoulders and what is worn on them ----
  const bs = mix(female ? 0.93 : 1, 0.78, k), inv = x => 100 + (x - 100) / bs;
  ctx.save(); ctx.translate(100, nb); ctx.scale(bs, bs); ctx.translate(-100, -nb);
  const D = { ctx, career, rank: spec.rank || 0, accent, nb, B: inv(bb.x), F: inv(fb.x), r: rng(hash(id + 'd')), isHead: !!spec.head, rim, female };
  dress(D);
  ctx.restore();

  // ---- the neck, in the shadow of the jaw ----
  const neck = () => { ctx.beginPath(); ctx.moveTo(bt.x, bt.y - 16); ctx.quadraticCurveTo(bt.x - 2, (bt.y + nb) / 2, bb.x, bb.y); ctx.lineTo(fb.x, fb.y); ctx.quadraticCurveTo(ft.x + (female || k > 0 ? 0.5 : 3.5), (ft.y + nb) / 2 + 3, ft.x, ft.y - 16); ctx.lineTo(jx + 2, ny + 10); ctx.closePath(); };
  neck(); ctx.fillStyle = rgba(mixc(skin, skinSh, 0.3)); ctx.fill(); ctx.save(); neck(); ctx.clip();
  ctx.fillStyle = rgba(skinSh, 0.85); ctx.beginPath(); ctx.moveTo(fx + 4, botY - 6); ctx.quadraticCurveTo(fx - 30, botY, jx - 2, jy); ctx.lineTo(jx - 12, jy + 20); ctx.quadraticCurveTo(fx - 30, botY + 15, fx - 4, botY + 11); ctx.closePath(); ctx.fill();   // what the jaw throws
  ctx.fillStyle = rgba(skinSh, 0.42); ctx.beginPath(); ctx.moveTo(bt.x - 4, bt.y - 20); ctx.lineTo(bt.x + 13, bt.y - 10); ctx.quadraticCurveTo(bt.x + 6, (bt.y + nb) / 2, bb.x + 16, nb + 10); ctx.lineTo(bb.x - 6, nb + 10); ctx.closePath(); ctx.fill();   // the back of it, away from the light
  ctx.strokeStyle = rgba(skinHi, 0.5); ctx.lineWidth = 3; ctx.beginPath(); ctx.moveTo(ft.x - 1.5, botY + 13); ctx.quadraticCurveTo(ft.x + 1, (ft.y + nb) / 2 + 3, fb.x - 1.5, fb.y); ctx.stroke();
  if (older) { ctx.strokeStyle = rgba(skinSh, elder ? 0.6 : 0.34); ctx.lineWidth = 1.1; for (let i = 0; i < 2; i++) { ctx.beginPath(); ctx.moveTo(ft.x - 1, botY + 16 + i * 8); ctx.quadraticCurveTo(ft.x - 12, botY + 20 + i * 8, ft.x - 20, botY + 16 + i * 8); ctx.stroke(); } }
  ctx.restore();
  ctx.strokeStyle = rgba(rim, 0.55); ctx.lineWidth = 2.2; ctx.beginPath(); ctx.moveTo(bt.x, bt.y); ctx.quadraticCurveTo(bt.x - 2, (bt.y + nb) / 2, bb.x, bb.y - 4); ctx.stroke();
  ctx.strokeStyle = rgba(ink, 0.6); ctx.lineWidth = 1.4; ctx.beginPath(); ctx.moveTo(ft.x, botY + 4); ctx.quadraticCurveTo(ft.x + (female || k > 0 ? 0.5 : 3.5), (ft.y + nb) / 2 + 3, fb.x, fb.y); ctx.stroke();
  ctx.save(); ctx.translate(100, nb); ctx.scale(bs, bs); ctx.translate(-100, -nb); collar(D); ctx.restore();

  // ---- the head: flat skin, then light and shadow in shapes ----
  head(); ctx.fillStyle = rgba(skin); ctx.fill();
  ctx.save(); head(); ctx.clip();
  let g = ctx.createLinearGradient(60, 0, fx + 16, 0); g.addColorStop(0, rgba(skinSh, 0.62)); g.addColorStop(0.5, rgba(skinSh, 0.14)); g.addColorStop(0.8, rgba(skin, 0)); g.addColorStop(1, rgba(skinHi, 0.3)); ctx.fillStyle = g; ctx.fillRect(0, 0, 256, 256);
  ctx.strokeStyle = rgba(skinHi, 0.55); ctx.lineWidth = 9; ctx.beginPath(); ctx.moveTo(104, top); front(); ctx.stroke();                                              // light along the line of the face
  ctx.fillStyle = rgba(skinSh, 0.2 + (elder ? 0.16 : older ? 0.08 : 0) + (female || k > 0 ? 0 : 0.06)); ctx.beginPath(); ctx.moveTo(105, ny + 17); ctx.quadraticCurveTo(124, ny + 20, fx - 17, mY + 2); ctx.quadraticCurveTo(fx - 22, botY - 8, fx - 30, botY + 2); ctx.lineTo(jx - 10, jy + 8); ctx.closePath(); ctx.fill();   // the hollow under the cheekbone
  ctx.fillStyle = rgba(skinSh, 0.34); ctx.beginPath(); ctx.ellipse(ex + 1.5, ey - 1.5, ew + 4.5, eh + 3.6, -0.12, 0, 6.3); ctx.fill();                                // the socket
  ctx.fillStyle = rgba(skinHi, 0.38); ctx.beginPath(); ctx.ellipse(ex - 5, ey + 13 + k, 10, 4.6, 0.32, 0, 6.3); ctx.fill();                                           // the cheekbone
  ctx.fillStyle = rgba(skinSh, 0.34); ctx.beginPath(); ctx.ellipse(fx + 1, subY + 2, 5, 2.6, 0.3, 0, 6.3); ctx.fill(); ctx.beginPath(); ctx.ellipse(fx, dipY + 0.5, 5.5, 2.6, 0, 0, 6.3); ctx.fill();   // under the nose, under the lip
  ctx.fillStyle = rgba(skinSh, 0.3); ctx.beginPath(); ctx.moveTo(fx - 8, botY + 1); ctx.quadraticCurveTo(fx - 30, botY - 0.5, jx, jy); ctx.lineTo(jx + 2, jy - 5); ctx.quadraticCurveTo(fx - 30, botY - 6.5, fx - 6, botY - 5); ctx.closePath(); ctx.fill();   // the edge of the jaw turning under
  if (k > 0.5) { ctx.fillStyle = rgba(skinHi, 0.3); ctx.beginPath(); ctx.ellipse(fx - 16, mY - 4, 12, 10, 0, 0, 6.3); ctx.fill(); }
  const flush = (mix(0.2, 0.34, k) + (female ? 0.06 : 0)) * Math.min(1, (skin[0] + skin[1] + skin[2]) / 560);
  soft(ctx, ex - 3, mY - 13 * fh, 13, 9, [214, 86, 76], flush);
  if (beard === 1) { ctx.fillStyle = rgba(hairSh, 0.3); ctx.beginPath(); ctx.moveTo(110, ny + 12); ctx.quadraticCurveTo(fx - 20, subY - 4, fx + 12, subY + 1); ctx.lineTo(fx + 14, botY + 4); ctx.lineTo(jx - 8, jy + 8); ctx.closePath(); ctx.fill(); }   // stubble
  if (d.freckle > 0.82 && !older) { ctx.fillStyle = rgba(shade(skin, 0.7), 0.5); const fr = rng(hash(id + 'f')); for (let i = 0; i < 22; i++) { ctx.beginPath(); ctx.arc(ex - 12 + fr() * 24, ey + 8 + fr() * 14, 0.8, 0, 6.3); ctx.fill(); } }

  // the lips, cut by the edge of the face
  const lipCol = female ? mixc(skin, [184, 62, 74], 0.56) : mixc(skin, [150, 76, 72], 0.36), lipSh = mixc(shade(lipCol, 0.66), DEEP, 0.2), cnx = fx - 10.5 - d.lip * 2, cny = mY + 0.6 - mood * 1.9;
  if (beard !== 2) {
    ctx.fillStyle = rgba(lipSh, female ? 0.95 : 0.6); ctx.beginPath(); ctx.moveTo(cnx, cny); ctx.quadraticCurveTo(fx - 4, ulY - 1.5 - lip * 0.4, fx + 14, ulY - 3); ctx.lineTo(fx + 14, mY); ctx.quadraticCurveTo(fx - 3, mY + 0.6, cnx, cny); ctx.fill();
    ctx.fillStyle = rgba(lipCol, female ? 1 : 0.8); ctx.beginPath(); ctx.moveTo(cnx, cny); ctx.quadraticCurveTo(fx - 3, mY + 0.6, fx + 14, mY); ctx.lineTo(fx + 14, llY + 3); ctx.quadraticCurveTo(fx - 3, llY + 2.2 + lip * 0.3, cnx, cny); ctx.fill();
    ctx.fillStyle = rgba(WARM, female ? 0.4 : 0.2); ctx.beginPath(); ctx.ellipse(fx + 2.5 + lip * 0.5, (mY + llY) / 2 + 0.6, 2.6, 1.3, 0.2, 0, 6.3); ctx.fill();
  }
  ctx.strokeStyle = rgba(mixc(lipSh, DEEP, 0.6), 0.95); ctx.lineWidth = 1.5; ctx.beginPath(); ctx.moveTo(fx + 3, mY); ctx.quadraticCurveTo(fx - 4, mY + 0.9 + mood * 0.5, cnx, cny); ctx.stroke();
  soft(ctx, cnx - 1, cny, 2.4, 2.4, skinSh, 0.28);

  // ---- the marks of age ----
  if (older) {
    ctx.strokeStyle = rgba(skinSh, elder ? 0.62 : 0.36); ctx.lineWidth = 1.15;
    ctx.beginPath(); ctx.moveTo(tx - 11, tipY + 3); ctx.quadraticCurveTo(fx - 9, mY - 6, cnx - 2.5, cny + 5); ctx.stroke();                                                    // from the nose down past the mouth
    for (let i = -1; i <= 1; i++) { ctx.beginPath(); ctx.moveTo(ex - ew - 1.5, ey + 1 + i * 1.5); ctx.lineTo(ex - ew - 7.5, ey + 1 + i * 4.2); ctx.stroke(); }             // at the corner of the eye
    ctx.beginPath(); ctx.moveTo(ex - ew + 1, ey + eh + 3.5); ctx.quadraticCurveTo(ex, ey + eh + 6.5, ex + ew - 1, ey + eh + 2.5); ctx.stroke();                              // under it
    if (elder) { for (let i = 0; i < 3; i++) { ctx.beginPath(); ctx.moveTo(fx - 4 - slope, ny - 30 + i * 5.5); ctx.quadraticCurveTo(fx - 14, ny - 33 + i * 5.5, fx - 25, ny - 30 + i * 5.5); ctx.stroke(); }
      ctx.beginPath(); ctx.moveTo(fx - 12, botY - 2); ctx.quadraticCurveTo(fx - 20, botY - 9, fx - 17, botY - 17); ctx.stroke(); }                                           // the brow, the jowl
  }
  if (look.scar || spec.scar) { ctx.strokeStyle = 'rgba(132,44,40,.85)'; ctx.lineWidth = 2; ctx.beginPath(); ctx.moveTo(ex - 9, ey + 7); ctx.quadraticCurveTo(ex - 5, ey + 19, ex - 13, ey + 32); ctx.stroke(); ctx.strokeStyle = 'rgba(255,226,214,.5)'; ctx.lineWidth = 0.8; ctx.beginPath(); ctx.moveTo(ex - 8, ey + 7); ctx.quadraticCurveTo(ex - 4, ey + 19, ex - 12, ey + 32); ctx.stroke(); }
  ctx.restore();

  // ---- the eye, and the brow that carries the mood ----
  const eyeShape = () => { ctx.beginPath(); ctx.moveTo(ex - ew, ey + 1); ctx.quadraticCurveTo(ex, ey - eh * 1.45, ex + ew - 1, ey - eh * 0.72); ctx.quadraticCurveTo(ex + ew + 1.2, ey, ex + ew - 1.5, ey + eh * 0.62); ctx.quadraticCurveTo(ex, ey + eh * 0.82, ex - ew, ey + 1); ctx.closePath(); };
  ctx.save(); eyeShape(); ctx.fillStyle = '#ece5da'; ctx.fill(); ctx.clip();
  ctx.fillStyle = 'rgba(150,118,118,.5)'; ctx.fillRect(ex - ew - 1, ey - eh * 2, ew * 0.9, eh * 4);                                                                    // the white is never white at its corner
  ctx.fillStyle = rgba(shade(eye, 0.5)); ctx.beginPath(); ctx.ellipse(ex + ew - 3.6, ey - 0.2, 3.3, eh * 1.12, 0, 0, 6.3); ctx.fill();
  ctx.fillStyle = rgba(mixc(eye, [255, 232, 180], 0.15)); ctx.beginPath(); ctx.ellipse(ex + ew - 3.4, ey, 2.6, eh * 0.92, 0, 0, 6.3); ctx.fill();
  ctx.fillStyle = 'rgba(12,8,10,.96)'; ctx.beginPath(); ctx.ellipse(ex + ew - 2.6, ey - 0.1, 1.35, eh * 0.5, 0, 0, 6.3); ctx.fill();
  ctx.fillStyle = 'rgba(30,14,16,.34)'; ctx.fillRect(ex - ew, ey - eh * 2, ew * 2 + 2, eh * 1.35);                                                                      // the lid's shadow
  ctx.fillStyle = 'rgba(255,255,255,.95)'; ctx.beginPath(); ctx.arc(ex + ew - 3.8, ey - eh * 0.34, 1, 0, 6.3); ctx.fill();
  ctx.restore();
  ctx.strokeStyle = rgba(ink, 0.95); ctx.lineWidth = female ? 2.3 : 1.9; ctx.beginPath(); ctx.moveTo(ex - ew - (female ? 1.5 : 0.5), ey + 1.4); ctx.quadraticCurveTo(ex, ey - eh * 1.5, ex + ew - 1, ey - eh * 0.72); if (female) ctx.lineTo(ex + ew + 2.6, ey - eh * 0.72 - 2.2); ctx.stroke();   // the upper lid, and its lashes
  ctx.strokeStyle = rgba(skinSh, 0.55); ctx.lineWidth = 1; ctx.beginPath(); ctx.moveTo(ex - ew + 1, ey - eh * 1.2 - 1.6); ctx.quadraticCurveTo(ex + 1, ey - eh * 2.1 - 1.2, ex + ew, ey - eh * 1.35 - 1); ctx.stroke();                 // the crease above it
  ctx.strokeStyle = rgba(skinSh, 0.5); ctx.beginPath(); ctx.moveTo(ex - ew + 2, ey + eh * 0.75 + 0.4); ctx.quadraticCurveTo(ex + 1, ey + eh * 1.05, ex + ew - 1.5, ey + eh * 0.62); ctx.stroke();
  const browCol = grey > 0.6 ? mixc(hairSh, [150, 146, 140], 0.5) : mixc(hairSh, ink, 0.3), by = ey - eh * 1.5 - 5.2 - d.browH * 1.8 - k, th = (female ? 2.1 : 3.6) + d.brow * 1.2 - k * 0.8, arch = 2 + d.arch * 2.4 + (female ? 0.8 : 0), dropB = -mood * 2.2;
  const b0 = [ex + ew + 4.5 + brow * 0.5, by + 2 + dropB], b1 = [ex - ew - 4, by + 2.4 - dropB * 0.25];
  ctx.fillStyle = rgba(browCol, 0.95); ctx.beginPath(); ctx.moveTo(b0[0], b0[1] - th * 0.55); ctx.quadraticCurveTo((b0[0] + b1[0]) / 2, by - arch - th * 0.5, b1[0], b1[1]); ctx.quadraticCurveTo((b0[0] + b1[0]) / 2, by - arch + th * 0.6, b0[0], b0[1] + th * 0.55); ctx.closePath(); ctx.fill();
  if (mood <= -2) { ctx.strokeStyle = rgba(skinSh, 0.7); ctx.lineWidth = 1.2; ctx.beginPath(); ctx.moveTo(fx - 3, ny - 12); ctx.lineTo(fx - 4.5, ny - 5); ctx.stroke(); }   // a furrow

  // the wing of the nose, and the nostril
  ctx.strokeStyle = rgba(skinSh, 0.75); ctx.lineWidth = 1.4; ctx.beginPath(); ctx.moveTo(tx - 10.5, tipY - 3.5); ctx.bezierCurveTo(tx - 14, tipY - 1, tx - 13.5, tipY + 5, tx - 9, tipY + 4.6); ctx.stroke();
  ctx.fillStyle = rgba(ink, 0.8); ctx.beginPath(); ctx.ellipse(tx - 6.2, tipY + 3.6, 2.5, 1.05, -0.12, 0, 6.3); ctx.fill();

  // the line itself
  ctx.strokeStyle = rgba(ink, 0.6); ctx.lineWidth = 1.4; ctx.beginPath(); ctx.moveTo(fx - 4 - slope, top + 12); ctx.bezierCurveTo(fx - 2 - slope, top + 17, fx + 0.5 - slope * 0.5, ny - 36, fx + 1 - slope * 0.5, ny - 30); ctx.quadraticCurveTo(fx + 3, ny - 16, fx + 2 + brow, ny - 8); ctx.quadraticCurveTo(fx + 2.5 + brow, ny - 2.5, fx, ny);
  ctx.quadraticCurveTo((fx + tx) / 2 + bridge * 2.2, (ny + tipY) / 2 - bridge * 1.4 - 2, tx, tipY - 2); ctx.bezierCurveTo(tx + 2.4, tipY + 1, tx + 0.5, tipY + 5, tx - 4, tipY + 5); ctx.quadraticCurveTo(fx + 7, tipY + 4.5, fx + 3.5, subY + 0.5);
  ctx.quadraticCurveTo(fx + 2.6, (subY + ulY) / 2 + 1, fx + 5 + lip, ulY); ctx.quadraticCurveTo(fx + 6.5 + lip, mY - 1.2, fx + 2.6, mY); ctx.quadraticCurveTo(fx + 6.5 + lip * 0.9, mY + 0.8, fx + 5 + lip * 0.85, llY); ctx.quadraticCurveTo(fx + 4 + lip * 0.5, dipY - 0.5, fx + 0.8, dipY);
  ctx.quadraticCurveTo(fx, dipY + 4.5, fx + 3 + chin, chinY - 1); ctx.bezierCurveTo(fx + 5.5 + chin, chinY + 4.5, fx + chin, botY, fx - 9, botY); ctx.quadraticCurveTo(fx - 30, botY - 0.5, jx, jy); ctx.stroke();

  // ---- hair, ear, beard ----
  const H = { ctx, top, ny, fx, botY, nb, jx, jy, hair, hairSh, hairHi, rim, accent, female, k, r: rng(hash(id + 'h')), hfx: fx - 7 - recede * 1.2 - slope * 0.6, hfy: top + 17 - recede * 0.9, sb: female || k > 0.5 ? 0 : 5 + d.beard * 6 };
  const ear = () => {
    const ecx = 101, ecy = ny + 13.5; ctx.save(); ctx.translate(ecx, ecy); ctx.rotate(-0.14);
    ctx.fillStyle = rgba(mixc(skin, skinSh, 0.26)); ctx.beginPath(); ctx.ellipse(0, -1, 7.4, 11.5, 0, 0, 6.3); ctx.fill(); ctx.strokeStyle = rgba(ink, 0.5); ctx.lineWidth = 1.2; ctx.stroke();
    ctx.fillStyle = rgba(skinSh, 0.6); ctx.beginPath(); ctx.ellipse(1.2, -1.5, 3.6, 6.8, 0, 0, 6.3); ctx.fill();
    ctx.strokeStyle = rgba(skinHi, 0.6); ctx.lineWidth = 1.5; ctx.beginPath(); ctx.arc(0, -1.5, 6, 3.5, 5.6); ctx.stroke(); ctx.restore();
  };
  const hidesEar = !bald && (style === 5 || style === 8);
  if (hidesEar) ear();
  if (bald) baldFringe(H); else hairDo(H, style);
  if (!hidesEar) ear();
  if (beard >= 2) {
    const len = beard === 2 ? 3 + d.beard * 14 : 4, B = rng(hash(id + 'b'));
    const whisk = (x0, x1, y0, y1, n) => { ctx.save(); ctx.clip(); ctx.fillStyle = rgba(hairSh, 0.5); ctx.fillRect(0, (y0 + y1) / 2 + 2, 256, 256); for (let i = 0; i < n; i++) { const x = mix(x0, x1, B()), y = mix(y0, y1, B()); ctx.strokeStyle = rgba(B() < 0.6 ? hairSh : hairHi, 0.45); ctx.lineWidth = 1.2; ctx.beginPath(); ctx.moveTo(x, y); ctx.quadraticCurveTo(x + 2, y + 4, x + 3 + B() * 2, y + 8); ctx.stroke(); } ctx.restore(); };
    ctx.fillStyle = rgba(hair);
    if (beard === 2) {
      ctx.beginPath(); ctx.moveTo(109, ny + 10); ctx.lineTo(116, ny + 13); ctx.quadraticCurveTo(fx - 23, subY - 3, fx - 9, subY + 2); ctx.lineTo(fx + 3.2, subY + 1.5); ctx.quadraticCurveTo(fx + 3.4, (subY + ulY) / 2 + 1, fx + 6.2 + lip, ulY - 0.4);
      ctx.lineTo(cnx + 1, mY - 1); ctx.lineTo(cnx, mY + 3.5); ctx.lineTo(fx + 2, dipY - 2.5); ctx.quadraticCurveTo(fx + 2 + len * 0.3, dipY + 5, fx + 5.5 + chin + len * 0.5, chinY);
      ctx.bezierCurveTo(fx + 6.5 + chin + len * 0.4, chinY + 8 + len, fx - 4, botY + 3 + len, fx - 15, botY + 2 + len * 0.8); ctx.quadraticCurveTo(fx - 34, botY + 2 + len * 0.3, jx - 3, jy + 3); ctx.lineTo(104, ny + 25); ctx.closePath(); ctx.fill();
      whisk(jx - 6, fx + 8, subY, botY + len, 14);
      ctx.fillStyle = rgba(lipCol, 0.85); ctx.beginPath(); ctx.moveTo(cnx + 2, mY + 0.6); ctx.quadraticCurveTo(fx - 2, mY + 0.4, fx + 5 + lip * 0.85, mY + 0.8); ctx.quadraticCurveTo(fx + 6 + lip * 0.85, llY - 1, fx + 3, llY + 0.5); ctx.quadraticCurveTo(fx - 4, llY - 0.5, cnx + 2, mY + 0.6); ctx.fill();
    } else {
      ctx.beginPath(); ctx.moveTo(fx - 12, subY + 3.5); ctx.lineTo(fx + 3.2, subY + 1.5); ctx.quadraticCurveTo(fx + 3.4, (subY + ulY) / 2 + 1, fx + 6 + lip, ulY - 0.6); ctx.lineTo(cnx - (beard === 4 ? 4 : 0), mY + (beard === 4 ? 2.5 : -0.6)); ctx.closePath(); ctx.fill(); whisk(cnx - 4, fx + 6, subY, mY - 2, 8);   // a moustache
      if (beard === 3) { ctx.fillStyle = rgba(hair); ctx.beginPath(); ctx.moveTo(cnx + 2, mY + 4); ctx.lineTo(fx + 2, dipY - 2); ctx.quadraticCurveTo(fx + 2, dipY + 5, fx + 5.5 + chin + len * 0.5, chinY); ctx.bezierCurveTo(fx + 6 + chin, chinY + 8 + len, fx - 4, botY + 3 + len, fx - 14, botY + 1 + len * 0.6); ctx.quadraticCurveTo(fx - 20, botY - 6, cnx + 2, mY + 4); ctx.closePath(); ctx.fill(); whisk(fx - 18, fx + 8, dipY, botY + len, 16); }   // and a pointed beard
    }
  }
  if (spec.laurel) {   // the wreath of a triumph, tied at the back
    const lc = [102, top + 50], R = 45;
    for (const off of [0, 1]) for (let i = 0; i < 9; i++) { const a = 3.25 + i * 0.235 + off * 0.1, rr = R - off * 6.5; ctx.save(); ctx.translate(lc[0] + Math.cos(a) * rr, lc[1] + Math.sin(a) * rr * 0.98); ctx.rotate(a + Math.PI / 2 + (off ? 0.5 : -0.5));
      const lg = ctx.createLinearGradient(-8, -3, 8, 3); lg.addColorStop(0, rgba(shade(BRASS, 0.8))); lg.addColorStop(1, rgba(BRASS2)); ctx.fillStyle = lg; ctx.beginPath(); ctx.moveTo(-8, 0); ctx.quadraticCurveTo(0, -5.4, 9, 0); ctx.quadraticCurveTo(0, 5.4, -8, 0); ctx.fill(); ctx.strokeStyle = rgba(shade(BRASS, 0.55), 0.7); ctx.lineWidth = 0.7; ctx.beginPath(); ctx.moveTo(-6, 0); ctx.lineTo(7, 0); ctx.stroke(); ctx.restore(); }
    ctx.strokeStyle = rgba(shade(accent, 0.9)); ctx.lineWidth = 3; ctx.beginPath(); ctx.moveTo(59, top + 46); ctx.quadraticCurveTo(48, top + 56, 46, top + 72); ctx.moveTo(59, top + 46); ctx.quadraticCurveTo(56, top + 60, 58, top + 74); ctx.stroke();
  }

  // ---- finish: grain, a vignette, and the pall of the dead ----
  if (!grain) { grain = document.createElement('canvas'); grain.width = grain.height = 256; const gx = grain.getContext('2d'); const im = gx.createImageData(256, 256); const gr = rng(7); for (let i = 0; i < im.data.length; i += 4) { const v = gr() * 255; im.data[i] = im.data[i + 1] = im.data[i + 2] = v; im.data[i + 3] = 13; } gx.putImageData(im, 0, 0); }
  ctx.drawImage(grain, 0, 0, 256, 256);
  const vig = ctx.createRadialGradient(128, 112, 84, 128, 128, 190); vig.addColorStop(0, 'rgba(0,0,0,0)'); vig.addColorStop(1, 'rgba(6,4,12,.6)'); ctx.fillStyle = vig; ctx.fillRect(0, 0, 256, 256);
  if (spec.alive === false) { ctx.globalCompositeOperation = 'saturation'; ctx.fillStyle = 'rgba(0,0,0,.8)'; ctx.fillRect(0, 0, 256, 256); ctx.globalCompositeOperation = 'source-over'; ctx.fillStyle = 'rgba(16,18,28,.42)'; ctx.fillRect(0, 0, 256, 256); }

  let out = c;
  if (spec.close) {   // a thumbnail is the head, not the bust: the same picture, closer
    out = document.createElement('canvas'); out.width = out.height = 192; const o = out.getContext('2d'); const side = mix(168, 150, k) * U, mx = 114 * U, my = (top + mix(66, 60, k)) * U;
    o.imageSmoothingQuality = 'high'; o.drawImage(c, Math.max(0, mx - side / 2), Math.max(0, my - side / 2), side, side, 0, 0, 192, 192);
  }
  const url = out.toDataURL('image/png'); cache.set(key, url); return url;
}

// Where such a person is met: the bridge of a ship, a colonnade, a court, a nursery, a hall beyond the rim. Out of focus and dark
// where the face will cross it, lit from the side the face looks toward, with the house's pennant hanging behind the head.
function backdrop(ctx, career, accent, r) {
  const base = { military: [[34, 50, 82], [8, 12, 24]], stewardship: [[92, 70, 46], [22, 15, 10]], law: [[74, 40, 46], [17, 9, 12]], child: [[78, 92, 110], [16, 20, 28]], foreign: [[96, 62, 36], [18, 12, 8]], none: [[58, 64, 86], [12, 14, 22]] }[career] || [[58, 64, 86], [12, 14, 22]];
  const bg = ctx.createRadialGradient(200, 70, 10, 128, 128, 215); bg.addColorStop(0, rgba(base[0])); bg.addColorStop(1, rgba(base[1])); ctx.fillStyle = bg; ctx.fillRect(0, 0, 256, 256);
  if (career === 'military') {   // a viewport and its stars, to look out of
    ctx.fillStyle = 'rgba(4,7,18,.9)'; ctx.beginPath(); ctx.ellipse(214, 92, 92, 108, 0, 0, 6.3); ctx.fill(); ctx.strokeStyle = 'rgba(130,160,210,.4)'; ctx.lineWidth = 5; ctx.stroke(); ctx.strokeStyle = 'rgba(20,30,52,.9)'; ctx.lineWidth = 2; ctx.beginPath(); ctx.ellipse(214, 92, 97, 113, 0, 0, 6.3); ctx.stroke();
    for (let i = 0; i < 46; i++) { const a = r() * 6.3, dd = Math.sqrt(r()) * 88; const s = r(); ctx.fillStyle = `rgba(226,236,255,${0.4 + s * 0.6})`; ctx.beginPath(); ctx.arc(214 + Math.cos(a) * dd, 92 + Math.sin(a) * dd * 1.15, 0.5 + s * 1.1, 0, 6.3); ctx.fill(); }
    soft(ctx, 236, 50, 60, 44, [90, 120, 200], 0.24); soft(ctx, 200, 160, 46, 26, [150, 90, 160], 0.14);
  } else if (career === 'stewardship') {   // columns in lamplight
    for (const [x, w] of [[58, 26], [190, 30]]) { const cg = ctx.createLinearGradient(x, 0, x + w, 0); cg.addColorStop(0, 'rgba(120,90,60,.14)'); cg.addColorStop(1, 'rgba(255,232,190,.26)'); ctx.fillStyle = cg; ctx.fillRect(x, -4, w, 264); ctx.fillStyle = 'rgba(20,12,8,.25)'; for (let i = 1; i < 4; i++) ctx.fillRect(x + i * w / 4, -4, 1, 264); }
    soft(ctx, 236, 44, 120, 100, [255, 206, 130], 0.36);
  } else if (career === 'law') {   // panelled wood, and a drape
    ctx.strokeStyle = 'rgba(206,160,134,.16)'; ctx.lineWidth = 2.5; for (const x of [60, 128, 196]) ctx.strokeRect(x, 14, 54, 120);
    const dg = ctx.createLinearGradient(210, 0, 260, 0); dg.addColorStop(0, 'rgba(84,22,32,.6)'); dg.addColorStop(1, 'rgba(160,44,58,.75)'); ctx.fillStyle = dg; ctx.beginPath(); ctx.moveTo(260, -4); ctx.lineTo(214, -4); ctx.quadraticCurveTo(236, 110, 208, 260); ctx.lineTo(260, 260); ctx.closePath(); ctx.fill();
    soft(ctx, 190, 30, 100, 70, [255, 220, 180], 0.16);
  } else if (career === 'foreign') {   // firelight in a hall beyond the rim
    soft(ctx, 236, 214, 190, 170, [255, 146, 60], 0.5); ctx.strokeStyle = 'rgba(26,16,10,.55)'; ctx.lineWidth = 8; for (const [a, b] of [[70, 20], [150, 190], [226, 250]]) { ctx.beginPath(); ctx.moveTo(a, -6); ctx.lineTo(b, 262); ctx.stroke(); }
  } else if (career === 'child') {   // a tall window with morning in it
    const wg = ctx.createLinearGradient(0, 0, 0, 160); wg.addColorStop(0, 'rgba(255,238,200,.4)'); wg.addColorStop(1, 'rgba(255,238,200,.12)'); ctx.fillStyle = wg; ctx.beginPath(); ctx.moveTo(176, 160); ctx.lineTo(176, 36); ctx.quadraticCurveTo(214, -14, 252, 36); ctx.lineTo(252, 160); ctx.closePath(); ctx.fill();
    ctx.strokeStyle = 'rgba(20,24,34,.55)'; ctx.lineWidth = 3; ctx.beginPath(); ctx.moveTo(214, 10); ctx.lineTo(214, 160); ctx.moveTo(176, 92); ctx.lineTo(252, 92); ctx.stroke(); soft(ctx, 214, 76, 110, 90, [255, 236, 190], 0.2);
  } else soft(ctx, 220, 50, 130, 110, [150, 170, 220], 0.18);
  // the house's pennant behind the head, and its colour on the wall
  let pg = ctx.createLinearGradient(10, 0, 48, 0); pg.addColorStop(0, rgba(shade(accent, 0.5))); pg.addColorStop(1, rgba(shade(accent, 0.95))); ctx.fillStyle = pg; ctx.beginPath(); ctx.moveTo(10, -4); ctx.lineTo(48, -4); ctx.lineTo(48, 168); ctx.lineTo(29, 150); ctx.lineTo(10, 168); ctx.closePath(); ctx.fill();
  pg = ctx.createLinearGradient(0, 0, 0, 170); pg.addColorStop(0, 'rgba(0,0,0,0)'); pg.addColorStop(1, 'rgba(0,0,0,.45)'); ctx.fillStyle = pg; ctx.fill(); ctx.fillStyle = rgba(BRASS2, 0.9); ctx.fillRect(10, 8, 38, 2.5);
  soft(ctx, 70, 120, 120, 130, accent, 0.14);
}

// The cap of hair over the skull: from the sideburn up the temple to the hairline, over the crown, down the back to the nape.
function cap(H, o) {
  const { ctx, top, ny, hfx, hfy } = H;
  ctx.beginPath(); ctx.moveTo(111, ny + 6 + (o.sb || 0)); ctx.lineTo(115, ny - 1); ctx.quadraticCurveTo(126, ny - 6, 129, ny - 15); ctx.quadraticCurveTo(131, hfy + 10, hfx, hfy);
  ctx.bezierCurveTo(hfx + 2, top - 1 - o.t - o.lift, 128, top - 3 - o.t - o.lift * 0.7, 102, top - 3 - o.t);
  ctx.bezierCurveTo(54 - o.tb, top - 2 - o.t, 44 - o.tb, ny + 6, 70 - o.tb * 0.5, ny + 33 + o.nape);
  ctx.quadraticCurveTo(86, ny + 37 + o.nape * 0.5, 95, ny + 27); ctx.lineTo(97, ny + 5); ctx.lineTo(110, ny + 5); ctx.closePath();
}
// A mass of hair: flat colour, a shadow shape where it turns from the light, a band of light, a few lines of its flow, the house's
// colour down its back edge.
function mass(H, draw, flows, o) {
  const { ctx, hair, hairSh, hairHi, rim } = H; o = o || {};
  draw(); ctx.fillStyle = rgba(hair); ctx.fill(); ctx.save(); draw(); ctx.clip();
  if (o.shadow) { ctx.fillStyle = rgba(hairSh, 0.6); ctx.beginPath(); o.shadow(); ctx.fill(); }
  if (o.light) { ctx.strokeStyle = rgba(hairHi, 0.55); ctx.lineWidth = o.lightW || 5; ctx.beginPath(); o.light(); ctx.stroke(); }
  ctx.strokeStyle = rgba(hairSh, 0.6); ctx.lineWidth = 1.2; for (const f of flows || []) { ctx.beginPath(); ctx.moveTo(f[0], f[1]); ctx.quadraticCurveTo(f[2], f[3], f[4], f[5]); ctx.stroke(); }
  if (o.rim) { ctx.strokeStyle = rgba(rim, 0.6); ctx.lineWidth = 4.5; ctx.beginPath(); o.rim(); ctx.stroke(); }
  ctx.restore();
}

function hairDo(H, style) {
  const { ctx, top, ny, botY, nb, hfx, hfy, hair, hairSh, hairHi, accent, female, r, sb } = H;
  const o = [{ t: 2.5, tb: 1.5, lift: 0, nape: 0 }, { t: 6, tb: 5, lift: 6, nape: 5 }, { t: 5, tb: 3, lift: 1, nape: 2 }, { t: 9, tb: 1, lift: 3, nape: -2 }, { t: 4, tb: 3, lift: 1, nape: 0 }, { t: 5, tb: 6, lift: 2, nape: 0 }, { t: 4, tb: 2, lift: 1, nape: 0 }, { t: 7, tb: 7, lift: 4, nape: 4 }, { t: 5, tb: 6, lift: 2, nape: 0 }][style];
  o.sb = sb;
  const swept = [], n = 5; for (let j = 1; j <= n; j++) swept.push([hfx - 3 + j, hfy + j * 6 - 2, 100, top + j * 8 - 6 - o.t, 54 + j * 4 - o.tb, ny - 16 + j * 9]);   // combed back from the brow
  const crown = [], W = [80, top + 12]; for (const [x, y] of [[hfx - 4, hfy + 3], [126, ny - 14], [112, ny], [92, ny + 4], [78, ny + 26], [58, ny + 6], [52, top + 30]]) crown.push([W[0], W[1], (W[0] + x) / 2 + (y - W[1]) * 0.12, (W[1] + y) / 2 - (x - W[0]) * 0.12, x, y]);   // falling from the crown
  const shadow = () => { ctx.moveTo(131, ny - 20); ctx.quadraticCurveTo(96, ny - 30, 40, ny - 22); ctx.lineTo(30, ny + 60); ctx.lineTo(131, ny + 60); ctx.closePath(); };
  const light = () => { ctx.moveTo(hfx - 9, hfy - 2 - o.lift * 0.5); ctx.quadraticCurveTo(104, top + 3 - o.t * 0.4 - o.lift * 0.3, 72, top + 15); };
  const rimBack = () => { ctx.moveTo(84, top - 3 - o.t); ctx.bezierCurveTo(54 - o.tb, top - 2 - o.t, 44 - o.tb, ny + 6, 70 - o.tb * 0.5, ny + 33 + o.nape); };

  if (style === 6) {   // wound into a knot at the back
    mass(H, () => { ctx.beginPath(); ctx.arc(46, ny - 4, 15, 0, 6.3); }, [[36, ny - 10, 46, ny - 16, 57, ny - 9], [35, ny - 2, 46, ny - 9, 58, ny - 1], [38, ny + 5, 47, ny - 1, 56, ny + 6]],
      { shadow: () => { ctx.arc(46, ny + 6, 15, 0, 6.3); }, rim: () => { ctx.arc(46, ny - 4, 15, 1.6, 4.4); } });
    ctx.strokeStyle = rgba(accent); ctx.lineWidth = 3; ctx.beginPath(); ctx.moveTo(60, ny - 16); ctx.quadraticCurveTo(57, ny - 4, 62, ny + 8); ctx.stroke();
  }
  if (style === 4) {   // tied, the tail down the back
    mass(H, () => { ctx.beginPath(); ctx.moveTo(60, ny - 8); ctx.bezierCurveTo(32, ny - 2, 30, ny + 40, 42, ny + (female ? 84 : 58)); ctx.lineTo(51, ny + (female ? 78 : 52)); ctx.bezierCurveTo(46, ny + 40, 52, ny + 16, 68, ny + 8); ctx.closePath(); },
      [[50, ny + 4, 40, ny + 24, 44, ny + 50], [58, ny + 6, 47, ny + 26, 49, ny + 48]], { rim: () => { ctx.moveTo(60, ny - 8); ctx.bezierCurveTo(32, ny - 2, 30, ny + 40, 42, ny + 84); } });
    ctx.strokeStyle = rgba(accent); ctx.lineWidth = 3.4; ctx.beginPath(); ctx.moveTo(57, ny - 9); ctx.lineTo(64, ny + 7); ctx.stroke();
  }
  if (style === 7) {   // curls
    const pts = []; for (let i = 0; i < 12; i++) { const a = 2.5 + i * 0.36; pts.push([98 + Math.cos(a) * 47, top + 44 + Math.sin(a) * 45, 10.5 + r() * 2.5]); } pts.push([hfx - 3, hfy + 2, 9], [124, ny - 12, 9], [72, ny + 30, 10], [86, top + 14, 16], [112, top + 12, 16], [70, top + 36, 16], [66, ny + 8, 14]);
    cap(H, o); ctx.fillStyle = rgba(hair); ctx.fill();
    ctx.beginPath(); for (const [x, y, rr] of pts) { ctx.moveTo(x + rr, y); ctx.arc(x, y, rr, 0, 6.3); } ctx.fill(); ctx.save(); ctx.clip();
    ctx.fillStyle = rgba(hairSh, 0.55); ctx.beginPath(); shadow(); ctx.fill();
    for (const [x, y, rr] of pts) { ctx.strokeStyle = rgba(hairSh, 0.6); ctx.lineWidth = 1.3; ctx.beginPath(); ctx.arc(x + 1, y + 1, rr * 0.62, 0.4, 3.6); ctx.stroke(); ctx.strokeStyle = rgba(hairHi, 0.5); ctx.lineWidth = 1.6; ctx.beginPath(); ctx.arc(x, y, rr * 0.7, 3.9, 5.5); ctx.stroke(); }
    ctx.restore(); return;
  }
  mass(H, () => cap(H, o), style === 0 || style === 3 ? swept.slice(0, 3) : swept, { shadow, light, rim: rimBack, lightW: style === 0 ? 3.5 : 5.5 });
  if (style === 3) { ctx.save(); cap(H, o); ctx.clip(); ctx.fillStyle = rgba(hairSh, 0.35); ctx.fillRect(0, ny - 22, 256, 100); ctx.restore(); }   // cut close at the sides
  if (style === 2) mass(H, () => { ctx.beginPath(); ctx.moveTo(hfx - 22, hfy - 12); ctx.quadraticCurveTo(hfx + 4, hfy - 8, hfx + 9, ny - 24); ctx.quadraticCurveTo(hfx + 3, ny - 15, 129, ny - 13); ctx.quadraticCurveTo(128, hfy + 6, hfx - 22, hfy - 12); ctx.closePath(); },
    [[hfx - 14, hfy - 6, hfx + 2, hfy - 2, hfx + 5, ny - 22], [hfx - 12, hfy + 1, hfx - 2, hfy + 6, hfx - 1, ny - 17]], { light: () => { ctx.moveTo(hfx - 12, hfy - 8); ctx.quadraticCurveTo(hfx + 2, hfy - 5, hfx + 6, ny - 26); }, lightW: 2.5 });   // a fringe over the brow
  if (style === 5 || style === 8) {   // falling past the ear, to the shoulder or to the jaw
    const end = style === 5 ? nb + 14 : botY + 1, fl = [];
    for (let j = 0; j < 5; j++) { const x0 = 92 - j * 11; fl.push([x0, ny - 2, x0 + 9 - j * 2.5, (ny + end) / 2, x0 + 3 - j, end - 1]); }
    const fall = () => { ctx.beginPath(); ctx.moveTo(98, ny - 12); ctx.bezierCurveTo(108, ny + 14, 98, end - 38, style === 5 ? 101 : 104, end - 8); ctx.quadraticCurveTo(92, end + 10, 72, end + 8); ctx.quadraticCurveTo(44, end + 3, 38, end - 24); ctx.bezierCurveTo(30, end - 58, 34, ny + 26, 44, ny - 16); ctx.closePath(); };
    mass(H, fall, fl, { shadow: () => { ctx.moveTo(28, ny + 10); ctx.lineTo(74, ny + 2); ctx.quadraticCurveTo(62, (ny + end) / 2, 78, end + 14); ctx.lineTo(28, end + 14); ctx.closePath(); },
      light: () => { ctx.moveTo(97, ny + 6); ctx.quadraticCurveTo(102, (ny + end) / 2, 96, end - 12); }, lightW: 3, rim: () => { ctx.moveTo(44, ny - 16); ctx.bezierCurveTo(34, ny + 26, 30, end - 58, 38, end - 24); ctx.quadraticCurveTo(44, end + 3, 72, end + 8); } });
  }
}

// What is left to a bald man: a band from the temple round to the nape, and the light on his crown.
function baldFringe(H) {
  const { ctx, top, ny } = H;
  mass(H, () => { ctx.beginPath(); ctx.moveTo(111, ny + 8); ctx.lineTo(113, ny - 4); ctx.quadraticCurveTo(84, ny - 16, 58, ny - 12); ctx.quadraticCurveTo(50, ny + 8, 68, ny + 33); ctx.quadraticCurveTo(86, ny + 36, 95, ny + 27); ctx.lineTo(97, ny + 5); ctx.closePath(); },
    [[106, ny - 4, 84, ny - 8, 62, ny - 2], [100, ny + 2, 80, ny + 2, 64, ny + 10]], { shadow: () => { ctx.rect(0, ny + 10, 256, 60); }, rim: () => { ctx.moveTo(58, ny - 12); ctx.quadraticCurveTo(50, ny + 8, 68, ny + 33); } });
  soft(ctx, 104, top + 12, 26, 9, WARM, 0.5, 0.1);
}

// What is worn on the shoulders: a uniform with its rank, a steward's mantle, an advocate's robe, a child's tunic, the furs of a hall
// beyond the rim. One mass turning from the light, the near shoulder rounding toward us, folds, and brass that catches the light.
function dress(o) {
  const { ctx, career, rank, accent, nb, B, F, r, isHead, rim } = o; const mid = (B + F) / 2;
  const body = () => { ctx.beginPath(); ctx.moveTo(B - 6, nb - 7); ctx.bezierCurveTo(B - 32, nb + 1, 6, nb + 14, -12, nb + 62); ctx.lineTo(-12, 340); ctx.lineTo(240, 340); ctx.bezierCurveTo(232, nb + 54, 198, nb + 14, F + 6, nb - 3); ctx.quadraticCurveTo(mid, nb + 11, B - 6, nb - 7); ctx.closePath(); };
  const paint = cloth => {
    body(); const g = ctx.createLinearGradient(0, 0, 236, 0); g.addColorStop(0, rgba(mixc(shade(cloth, 0.5), DEEP, 0.35))); g.addColorStop(0.55, rgba(cloth)); g.addColorStop(1, rgba(mixc(cloth, WARM, 0.14))); ctx.fillStyle = g; ctx.fill(); ctx.save(); body(); ctx.clip();
    ctx.fillStyle = rgba(mixc(cloth, WARM, 0.1), 0.55); ctx.beginPath(); ctx.ellipse(74, nb + 84, 58, 74, 0, 0, 6.3); ctx.fill();                                                    // the near shoulder
    ctx.strokeStyle = rgba(DEEP, 0.3); ctx.lineWidth = 2.2; ctx.beginPath(); ctx.ellipse(74, nb + 84, 58, 74, 0, -1.25, 0.2); ctx.stroke();
    ctx.strokeStyle = rgba(mixc(cloth, WARM, 0.4), 0.5); ctx.lineWidth = 6; ctx.beginPath(); ctx.moveTo(F + 6, nb - 3); ctx.bezierCurveTo(198, nb + 14, 232, nb + 54, 240, 340); ctx.stroke();   // light down the chest
    ctx.strokeStyle = rgba(rim, 0.5); ctx.lineWidth = 5; ctx.beginPath(); ctx.moveTo(B - 6, nb - 7); ctx.bezierCurveTo(B - 32, nb + 1, 6, nb + 14, -12, nb + 62); ctx.stroke();               // the house's colour on the back
    soft(ctx, mid + 8, nb + 8, 44, 13, DEEP, 0.4);
  };
  const fold = (x0, y0, x1, y1, x2, y2, a) => { ctx.strokeStyle = rgba(DEEP, a || 0.3); ctx.lineWidth = 2.2; ctx.beginPath(); ctx.moveTo(x0, y0); ctx.quadraticCurveTo(x1, y1, x2, y2); ctx.stroke(); ctx.strokeStyle = 'rgba(255,250,236,.1)'; ctx.lineWidth = 1.3; ctx.beginPath(); ctx.moveTo(x0 + 2.4, y0); ctx.quadraticCurveTo(x1 + 2.4, y1, x2 + 2.4, y2); ctx.stroke(); };
  const brass = (x, y, rad) => { const g = ctx.createRadialGradient(x + rad * 0.4, y - rad * 0.4, 0.3, x, y, rad); g.addColorStop(0, rgba(BRASS2)); g.addColorStop(1, rgba(shade(BRASS, 0.66))); ctx.fillStyle = g; ctx.beginPath(); ctx.arc(x, y, rad, 0, 6.3); ctx.fill(); };
  if (career === 'military') {
    paint(mixc([30, 38, 62], accent, 0.14));
    const a = [F + 2, nb + 3], cc = [F + 38, nb + 28], b = [182, 340];
    ctx.strokeStyle = rgba(shade(accent, 0.55)); ctx.lineWidth = 6; ctx.beginPath(); ctx.moveTo(a[0], a[1]); ctx.quadraticCurveTo(cc[0], cc[1], b[0], b[1]); ctx.stroke(); ctx.strokeStyle = rgba(mixc(accent, WARM, 0.15)); ctx.lineWidth = 3; ctx.stroke();   // the piping down the front
    for (const t of [0.1, 0.2, 0.3]) { const p = quadAt(a, cc, b, t); brass(p[0] - 9, p[1] + 3, 2.8); }
    if (rank >= 6) { const g = ctx.createLinearGradient(40, 0, 210, 0); g.addColorStop(0, rgba(shade(accent, 0.6))); g.addColorStop(1, rgba(mixc(accent, WARM, 0.18))); ctx.fillStyle = g; ctx.beginPath(); ctx.moveTo(40, nb + 10); ctx.lineTo(84, nb + 10); ctx.lineTo(236, nb + 110); ctx.lineTo(190, nb + 122); ctx.closePath(); ctx.fill();
      ctx.strokeStyle = rgba(BRASS2, 0.85); ctx.lineWidth = 1.4; ctx.beginPath(); ctx.moveTo(46, nb + 13); ctx.lineTo(194, nb + 118); ctx.moveTo(80, nb + 12); ctx.lineTo(230, nb + 110); ctx.stroke(); }   // an admiral's sash
    if (rank >= 3) { ctx.save(); ctx.translate(70, nb + 22); ctx.rotate(-0.06); const g = ctx.createLinearGradient(-30, 0, 30, 0); g.addColorStop(0, rgba(shade(BRASS, 0.62))); g.addColorStop(1, rgba(BRASS2)); ctx.fillStyle = g; ctx.beginPath(); ctx.ellipse(0, 0, 31, 9, 0, 0, 6.3); ctx.fill();
      ctx.lineWidth = 1.7; for (let i = -4; i <= 4; i++) { ctx.strokeStyle = rgba(i > 0 ? BRASS2 : BRASS, 0.95); ctx.beginPath(); ctx.moveTo(i * 6.4, 6.5 - Math.abs(i) * 0.5); ctx.lineTo(i * 6.9, 20 - Math.abs(i)); ctx.stroke(); }
      ctx.strokeStyle = rgba(shade(BRASS, 0.5), 0.6); ctx.lineWidth = 1; ctx.beginPath(); ctx.ellipse(0, 0, 24, 5.4, 0, 0, 6.3); ctx.stroke(); ctx.restore(); }   // an epaulette
    ctx.restore();
  } else if (career === 'law') {
    paint([34, 30, 40]); fold(150, nb + 30, 160, nb + 70, 150, 340); fold(30, nb + 40, 22, nb + 70, 30, 340);
    const g = ctx.createLinearGradient(B, 0, 130, 0); g.addColorStop(0, rgba(shade(accent, 0.55))); g.addColorStop(1, rgba(mixc(accent, WARM, 0.1))); ctx.fillStyle = g; ctx.beginPath(); ctx.moveTo(B + 2, nb - 2); ctx.lineTo(B + 30, nb + 6); ctx.quadraticCurveTo(B + 46, nb + 40, 128, 340); ctx.lineTo(92, 340); ctx.quadraticCurveTo(B + 14, nb + 40, B + 2, nb - 2); ctx.closePath(); ctx.fill();   // the stole
    ctx.strokeStyle = rgba(BRASS2, 0.8); ctx.lineWidth = 1.5; ctx.beginPath(); ctx.moveTo(B + 30, nb + 6); ctx.quadraticCurveTo(B + 46, nb + 40, 128, 340); ctx.stroke();
    if (rank >= 4) { ctx.strokeStyle = rgba(BRASS, 0.95); ctx.lineWidth = 3.4; ctx.beginPath(); ctx.moveTo(46, nb + 12); ctx.quadraticCurveTo(128, nb + 66, 196, nb + 30); ctx.stroke(); brass(126, nb + 43, 7); }   // a justice's chain
    ctx.restore();
  } else if (career === 'stewardship') {
    paint([228, 218, 200]);
    const edge = () => { ctx.moveTo(mid + 6, nb + 6); ctx.quadraticCurveTo(132, nb + 40, 146, 340); };
    const g = ctx.createLinearGradient(0, 0, 150, 0); g.addColorStop(0, rgba(mixc(shade(accent, 0.45), DEEP, 0.25))); g.addColorStop(1, rgba(mixc(accent, WARM, 0.08))); ctx.fillStyle = g; ctx.beginPath(); edge(); ctx.lineTo(-20, 340); ctx.lineTo(-20, nb - 40); ctx.lineTo(B, nb - 14); ctx.closePath(); ctx.fill();   // the mantle over the near shoulder
    ctx.strokeStyle = rgba(BRASS2, 0.9); ctx.lineWidth = 3; ctx.beginPath(); edge(); ctx.stroke();
    fold(mid, nb + 14, 96, nb + 44, 108, 340); fold(B + 6, nb + 12, 60, nb + 50, 66, 340); fold(B - 6, nb + 10, 30, nb + 44, 26, 340, 0.24); fold(170, nb + 30, 180, nb + 70, 172, 340, 0.16);
    ctx.strokeStyle = rgba(rim, 0.5); ctx.lineWidth = 5; ctx.beginPath(); ctx.moveTo(B - 6, nb - 7); ctx.bezierCurveTo(B - 32, nb + 1, 6, nb + 14, -12, nb + 62); ctx.stroke();
    ctx.restore(); brass(mid + 8, nb + 9, 6 + Math.min(3, rank * 0.5)); ctx.fillStyle = rgba(accent); ctx.beginPath(); ctx.arc(mid + 8, nb + 9, 2.6, 0, 6.3); ctx.fill();   // the brooch that holds it
  } else if (career === 'foreign') {
    paint(mixc([84, 56, 38], accent, 0.3));
    const fur = () => { ctx.beginPath(); ctx.moveTo(-14, nb + 46); ctx.bezierCurveTo(30, nb + 50, 70, nb + 28, 110, nb + 36); ctx.bezierCurveTo(150, nb + 42, 196, nb + 34, 240, nb + 78); ctx.lineTo(240, nb - 40); ctx.lineTo(-14, nb - 40); ctx.closePath(); };
    fur(); const g = ctx.createLinearGradient(0, 0, 236, 0); g.addColorStop(0, '#4c3c30'); g.addColorStop(1, '#a8927a'); ctx.fillStyle = g; ctx.fill(); ctx.save(); fur(); ctx.clip();
    for (let i = 0; i < 170; i++) { const x = r() * 240, y = nb - 10 + r() * 80; ctx.strokeStyle = r() < 0.5 ? 'rgba(40,26,18,.5)' : 'rgba(232,210,176,.42)'; ctx.lineWidth = 1.1; ctx.beginPath(); ctx.moveTo(x, y); ctx.lineTo(x + 2 + r() * 2, y + 6 + r() * 5); ctx.stroke(); } ctx.restore();
    ctx.strokeStyle = rgba(DEEP, 0.4); ctx.lineWidth = 3; ctx.beginPath(); ctx.moveTo(-14, nb + 47); ctx.bezierCurveTo(30, nb + 51, 70, nb + 29, 110, nb + 37); ctx.bezierCurveTo(150, nb + 43, 196, nb + 35, 240, nb + 79); ctx.stroke();
    ctx.restore();
  } else {
    paint(career === 'child' ? mixc([216, 206, 190], accent, 0.24) : mixc([60, 62, 78], accent, 0.36));
    fold(mid + 14, nb + 22, 134, nb + 60, 128, 340, 0.16);
    ctx.restore();
  }
  if (isHead && career !== 'child') {   // the head of a house wears its signet on a chain
    const a = [44, nb + 12], cc = [118, nb + 78], b = [196, nb + 30], p = quadAt(a, cc, b, 0.5);
    ctx.strokeStyle = rgba(shade(BRASS, 0.6)); ctx.lineWidth = 4; ctx.beginPath(); ctx.moveTo(a[0], a[1]); ctx.quadraticCurveTo(cc[0], cc[1], b[0], b[1]); ctx.stroke(); ctx.strokeStyle = rgba(BRASS2); ctx.lineWidth = 2; ctx.setLineDash([3, 2.4]); ctx.stroke(); ctx.setLineDash([]);
    const g = ctx.createLinearGradient(p[0] - 9, 0, p[0] + 9, 0); g.addColorStop(0, rgba(shade(accent, 0.6))); g.addColorStop(1, rgba(mixc(accent, [255, 255, 255], 0.3))); ctx.fillStyle = g; ctx.beginPath(); ctx.moveTo(p[0], p[1] - 3); ctx.lineTo(p[0] + 9, p[1] + 9); ctx.lineTo(p[0], p[1] + 22); ctx.lineTo(p[0] - 9, p[1] + 9); ctx.closePath(); ctx.fill(); ctx.strokeStyle = rgba(BRASS2); ctx.lineWidth = 1.8; ctx.stroke();
  }
}

// The collar, drawn over the neck: it is what sits in front of the throat.
function collar(o) {
  const { ctx, career, rank, accent, nb, B, F } = o; const mid = (B + F) / 2, a = [B - 6, nb - 7], cc = [mid, nb + 11], b = [F + 6, nb - 3];
  const band = (h, col, edge) => { ctx.fillStyle = rgba(col); ctx.beginPath(); ctx.moveTo(a[0], a[1]); ctx.quadraticCurveTo(cc[0], cc[1], b[0], b[1]); ctx.lineTo(b[0] - 3, b[1] - h); ctx.quadraticCurveTo(cc[0], cc[1] - h - 1, a[0] + 2, a[1] - h); ctx.closePath(); ctx.fill();
    if (edge) { ctx.strokeStyle = rgba(edge); ctx.lineWidth = 1.6; ctx.beginPath(); ctx.moveTo(b[0] - 3, b[1] - h); ctx.quadraticCurveTo(cc[0], cc[1] - h - 1, a[0] + 2, a[1] - h); ctx.stroke(); } };
  if (career === 'military') {
    const cloth = mixc([30, 38, 62], accent, 0.14); band(13, shade(cloth, 1.35), mixc(accent, WARM, 0.2));
    const pips = Math.min(4, Math.max(1, Math.ceil(rank / 2))); for (let i = 0; i < pips; i++) { const p = quadAt(a, cc, b, 0.9 - i * 0.11); ctx.fillStyle = rgba(BRASS2); ctx.beginPath(); ctx.arc(p[0] - 1, p[1] - 6.5, 1.9, 0, 6.3); ctx.fill(); }   // rank on the collar
  } else if (career === 'law') {
    band(9, [34, 30, 40]); for (const s of [0, 1]) { ctx.fillStyle = s ? '#f4efe6' : '#cfc7bd'; ctx.beginPath(); ctx.moveTo(b[0] - 12 + s * 7, b[1] - 3); ctx.lineTo(b[0] - 4 + s * 7, b[1] - 1); ctx.lineTo(b[0] - 5 + s * 9, b[1] + 26); ctx.lineTo(b[0] - 15 + s * 9, b[1] + 24); ctx.closePath(); ctx.fill(); }   // the bands at the throat
  } else if (career === 'foreign') {
    ctx.strokeStyle = rgba(shade(BRASS, 0.7)); ctx.lineWidth = 5.4; ctx.beginPath(); ctx.moveTo(a[0] + 6, a[1] - 9); ctx.quadraticCurveTo(cc[0], cc[1] - 8, b[0] - 5, b[1] - 9); ctx.stroke(); ctx.strokeStyle = rgba(BRASS2); ctx.lineWidth = 2; ctx.beginPath(); ctx.moveTo(a[0] + 20, a[1] - 5); ctx.quadraticCurveTo(cc[0] + 6, cc[1] - 9, b[0] - 5, b[1] - 10); ctx.stroke();   // a torque
  } else if (career !== 'stewardship') { band(6, shade(accent, career === 'child' ? 0.8 : 0.7), mixc(accent, WARM, 0.25)); }   // the neckband, in the house's colour
  else band(4, [206, 196, 178]);
}

/** Fill every <img data-bust='{"id":..,"sex":..,"age":..,"accent":..}'> lacking a src, and every SVG <image data-bust> lacking an href. */
export function paint() {
  document.querySelectorAll('img[data-bust], image[data-bust]').forEach(el => { const at = el.tagName.toLowerCase() === 'image' ? 'href' : 'src'; if (el.getAttribute(at)) return; try { el.setAttribute(at, render(JSON.parse(el.getAttribute('data-bust')))); } catch (e) { console.error('portrait', e); } });
}
window.praetoriaBust = { render, paint };
