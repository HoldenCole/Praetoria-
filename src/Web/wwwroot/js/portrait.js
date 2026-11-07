// Praetoria — portraits as cameos (docs/ART_DIRECTION.md §3). An imperial coin profile: a parametric bust silhouette
// (forehead, brow, nose, lips, chin, jaw, neck), hair and beard from sculpted shapes, a uniform collar and brass gorget in
// the house colour, relief shading lit from the upper left, on a plate carrying the house's diagonal stripe. Every
// character gets a stable face from a hash of its id; age greys the hair and recedes the hairline. Canvas 2D, instant.
const SIZE = 256;
const cache = new Map();

function hash(s) { let h = 2166136261; for (let i = 0; i < s.length; i++) { h ^= s.charCodeAt(i); h = Math.imul(h, 16777619); } return h >>> 0; }
function rng(seed) { let t = seed >>> 0; return () => { t += 0x6D2B79F5; let r = Math.imul(t ^ (t >>> 15), 1 | t); r ^= r + Math.imul(r ^ (r >>> 7), 61 | r); return ((r ^ (r >>> 14)) >>> 0) / 4294967296; }; }
function mix(a, b, t) { return a + (b - a) * t; }
function hex(c) { const m = /^#?([0-9a-f]{6})$/i.exec(c || ''); if (!m) return [201, 162, 74]; const n = parseInt(m[1], 16); return [n >> 16 & 255, n >> 8 & 255, n & 255]; }
function rgba(c, a) { const [r, g, b] = Array.isArray(c) ? c : hex(c); return `rgba(${r},${g},${b},${a})`; }
function shade(c, k) { const [r, g, b] = Array.isArray(c) ? c : hex(c); return [Math.min(255, r * k), Math.min(255, g * k), Math.min(255, b * k)]; }

const SKINS = [[241, 213, 191], [230, 194, 163], [212, 168, 131], [192, 140, 103], [160, 105, 74], [122, 74, 50], [90, 53, 36]];
const HAIRS = [[26, 21, 18], [46, 33, 24], [74, 47, 28], [107, 74, 42], [138, 106, 58], [176, 138, 74], [210, 176, 106], [58, 58, 63], [122, 42, 30]];

export function render(spec) {
  const key = JSON.stringify([spec.id, spec.sex, Math.floor((spec.age || 30) / 5), spec.accent, spec.alive !== false, spec.size || SIZE]);
  if (cache.has(key)) return cache.get(key);
  const size = spec.size || SIZE; const c = document.createElement('canvas'); c.width = c.height = size; const ctx = c.getContext('2d');
  const s = size / 256; ctx.scale(s, s);
  const r = rng(hash(spec.id || 'x')); const female = spec.sex === 'female'; const age = spec.age || 30; const accent = hex(spec.accent);

  // ---- plate ----
  const bg = ctx.createRadialGradient(128, 96, 10, 128, 128, 190); bg.addColorStop(0, '#2b3650'); bg.addColorStop(0.7, '#111622'); bg.addColorStop(1, '#070a12'); ctx.fillStyle = bg; ctx.fillRect(0, 0, 256, 256);
  ctx.save(); ctx.globalAlpha = 0.22; ctx.fillStyle = rgba(accent, 1);
  for (let i = -256; i < 512; i += 52) { ctx.beginPath(); ctx.moveTo(i, 256); ctx.lineTo(i + 12, 256); ctx.lineTo(i + 12 + 256, 0); ctx.lineTo(i + 256, 0); ctx.closePath(); ctx.fill(); }
  ctx.restore();
  // a soft back-light halo where the head will be, in the house colour
  const halo = ctx.createRadialGradient(150, 110, 10, 150, 110, 120); halo.addColorStop(0, rgba(accent, 0.28)); halo.addColorStop(1, rgba(accent, 0)); ctx.fillStyle = halo; ctx.fillRect(0, 0, 256, 256);

  // ---- parameters ----
  const skin = SKINS[Math.floor(r() * SKINS.length)];
  let hair = HAIRS[Math.floor(r() * HAIRS.length)];
  const grey = age >= 62 ? 1 : age >= 46 ? Math.min(1, (age - 46) / 18) : 0; if (grey > 0) hair = [mix(hair[0], 214, grey), mix(hair[1], 210, grey), mix(hair[2], 202, grey)];
  const forehead = 0.9 + r() * 0.25, browRidge = female ? 1 + r() * 3 : 3 + r() * 5, noseLen = (female ? 11 : 13) + r() * 7, noseTip = 2 + r() * 3, noseUp = r() * 5 - 2;
  const lipFull = female ? 5 + r() * 3 : 3 + r() * 3, chin = female ? 10 + r() * 6 : 13 + r() * 8, jaw = female ? 0.86 + r() * 0.08 : 0.92 + r() * 0.12, neck = female ? 26 : 32;
  const style = female ? 4 + Math.floor(r() * 2) : (r() < 0.8 ? Math.floor(r() * 4) : 4); const bald = !female && age > 48 && r() < 0.3; const beard = !female && r() < 0.5 && !bald ? Math.floor(1 + r() * 2) : (!female && bald && r() < 0.5 ? 2 : 0);
  const recede = !female && age > 40 ? Math.min(14, (age - 40) * 0.5) : 0;

  // ---- the profile path (facing left; light from upper-left) ----
  // anchor points in a 256 box: crown (150,38) ... neck base
  const X = 150, top = 36;
  const face = () => {
    ctx.beginPath();
    ctx.moveTo(X + 40, top + 6);                                             // back of skull, top
    ctx.bezierCurveTo(X + 10, top - 10, X - 30, top - 4, X - 46 * forehead, top + 30);     // crown to forehead
    ctx.bezierCurveTo(X - 52 * forehead, top + 44, X - 50, top + 56, X - 52, top + 66);    // forehead
    ctx.lineTo(X - 52 - browRidge, top + 74);                                 // brow ridge
    ctx.bezierCurveTo(X - 50, top + 82, X - 54, top + 88, X - 56, top + 96);   // bridge
    ctx.bezierCurveTo(X - 60, top + 104, X - 56 - noseLen, top + 112 + noseUp, X - 56 - noseLen - noseTip, top + 118 + noseUp);   // nose line
    ctx.bezierCurveTo(X - 58 - noseLen, top + 126, X - 58, top + 124, X - 56, top + 128);  // under the nose
    ctx.bezierCurveTo(X - 60, top + 136, X - 60 - lipFull, top + 138, X - 60 - lipFull, top + 142);   // upper lip
    ctx.lineTo(X - 55, top + 146);                                            // mouth line
    ctx.bezierCurveTo(X - 62 - lipFull * 0.8, top + 150, X - 60, top + 156, X - 54, top + 160);        // lower lip
    ctx.bezierCurveTo(X - 62, top + 166, X - 58 - chin * 0.3, top + 170, X - 46 - chin * 0.4, top + 176 + chin * 0.4);   // chin
    ctx.bezierCurveTo(X - 30, top + 184 + chin * 0.3, X - 10 * jaw, top + 180, X + 14 * jaw, top + 172);   // jaw to the neck
    ctx.bezierCurveTo(X + 12 * jaw, top + 184, X + 10 * jaw, top + 194, X + 14 * jaw, top + 200);   // throat
    ctx.quadraticCurveTo(X + 16 * jaw + neck * 0.5, top + 208, X + 14 * jaw + neck, top + 206);    // neck base front
    ctx.quadraticCurveTo(X + 60, top + 200, X + 66, top + 186);               // nape
    ctx.bezierCurveTo(X + 74, top + 150, X + 80, top + 110, X + 74, top + 70); // back of head
    ctx.bezierCurveTo(X + 76, top + 40, X + 64, top + 10, X + 40, top + 6);
    ctx.closePath();
  };
  // ---- shoulders & uniform ----
  ctx.save();
  const cloth = shade(accent, 0.55);
  const coat = ctx.createLinearGradient(40, 200, 220, 256); coat.addColorStop(0, rgba(shade(accent, 0.75), 1)); coat.addColorStop(1, rgba(shade(accent, 0.35), 1));
  ctx.fillStyle = coat; ctx.beginPath(); ctx.moveTo(10, 256); ctx.bezierCurveTo(30, 206, 90, 196, 128, 198); ctx.bezierCurveTo(170, 196, 230, 206, 250, 256); ctx.closePath(); ctx.fill();
  // high collar
  ctx.fillStyle = rgba(cloth, 1); ctx.beginPath(); ctx.moveTo(X + 14 * jaw - 10, top + 196); ctx.lineTo(X + 14 * jaw + neck + 12, top + 206); ctx.lineTo(X + 14 * jaw + neck + 4, top + 226); ctx.lineTo(X + 14 * jaw - 24, top + 222); ctx.closePath(); ctx.fill();
  // brass gorget and braid
  ctx.strokeStyle = '#c9a24a'; ctx.lineWidth = 3; ctx.beginPath(); ctx.moveTo(X + 14 * jaw - 24, top + 224); ctx.lineTo(X + 14 * jaw + neck + 6, top + 228); ctx.stroke();
  ctx.fillStyle = '#e6c77a'; ctx.beginPath(); ctx.arc(X + 14 * jaw + neck - 2, top + 238, 4, 0, 6.283); ctx.fill();
  ctx.strokeStyle = '#8a6c2a'; ctx.lineWidth = 2; ctx.beginPath(); ctx.moveTo(70, 236); ctx.lineTo(118, 230); ctx.stroke(); ctx.beginPath(); ctx.moveTo(72, 244); ctx.lineTo(120, 238); ctx.stroke();
  ctx.restore();

  // ---- hair behind the head (long styles) ----
  if (!bald && (female || style >= 4)) {
    ctx.fillStyle = rgba(shade(hair, 0.8), 1); ctx.beginPath(); ctx.moveTo(X + 10, top + 6); ctx.bezierCurveTo(X + 96, top + 0, X + 112, top + 110, X + 100, top + (female ? 212 : 156)); ctx.quadraticCurveTo(X + 60, top + (female ? 222 : 166), X + 24, top + (female ? 206 : 160)); ctx.lineTo(X + 44, top + 90); ctx.closePath(); ctx.fill();
  }
  // ---- face: relief shading ----
  face(); const skinG = ctx.createLinearGradient(60, 40, 220, 230); skinG.addColorStop(0, rgba(shade(skin, 1.08), 1)); skinG.addColorStop(0.55, rgba(skin, 1)); skinG.addColorStop(1, rgba(shade(skin, 0.6), 1)); ctx.fillStyle = skinG; ctx.fill();
  // ambient occlusion under the jaw and into the neck
  ctx.save(); face(); ctx.clip(); const ao = ctx.createLinearGradient(0, top + 150, 0, top + 215); ao.addColorStop(0, 'rgba(0,0,0,0)'); ao.addColorStop(1, 'rgba(0,0,0,.45)'); ctx.fillStyle = ao; ctx.fillRect(0, top + 140, 256, 90);
  // cheekbone and temple shadow (the far side of the relief)
  const cheek = ctx.createRadialGradient(X + 30, top + 110, 10, X + 30, top + 110, 70); cheek.addColorStop(0, 'rgba(0,0,0,0)'); cheek.addColorStop(1, 'rgba(0,0,0,.28)'); ctx.fillStyle = cheek; ctx.fillRect(0, 0, 256, 256);
  // age: lines
  if (age >= 48) { ctx.strokeStyle = rgba(shade(skin, 0.62), Math.min(0.9, (age - 48) / 30 + 0.3)); ctx.lineWidth = 1.4; ctx.beginPath(); ctx.moveTo(X - 54, top + 130); ctx.quadraticCurveTo(X - 40, top + 150, X - 44, top + 166); ctx.stroke(); ctx.beginPath(); ctx.moveTo(X - 44, top + 98); ctx.lineTo(X - 30, top + 104); ctx.stroke(); ctx.beginPath(); ctx.moveTo(X + 16, top + 186); ctx.lineTo(X + 30, top + 200); ctx.stroke(); }
  ctx.restore();
  // rim light along the lit edge of the profile
  face(); ctx.strokeStyle = 'rgba(255,240,215,.55)'; ctx.lineWidth = 1.6; ctx.save(); ctx.clip(); ctx.lineWidth = 3; ctx.stroke(); ctx.restore();
  // eye: a lidded almond with a dark iris and a highlight
  ctx.save(); ctx.translate(X - 30, top + 84); ctx.fillStyle = rgba(shade(skin, 0.7), 1); ctx.beginPath(); ctx.ellipse(0, 0, 8.5, 3.6, -0.15, 0, 6.283); ctx.fill();
  ctx.fillStyle = '#f0ebe0'; ctx.beginPath(); ctx.ellipse(0, 0.4, 7.2, 2.7, -0.15, 0, 6.283); ctx.fill();
  ctx.fillStyle = ['#3a2a1e', '#2f5a3a', '#2f4a7a', '#6a5a3a', '#4a3a2a'][Math.floor(r() * 5)]; ctx.beginPath(); ctx.arc(-2.6, 0.5, 2.5, 0, 6.283); ctx.fill(); ctx.fillStyle = '#120e0c'; ctx.beginPath(); ctx.arc(-2.6, 0.5, 1.2, 0, 6.283); ctx.fill(); ctx.fillStyle = 'rgba(255,255,255,.8)'; ctx.beginPath(); ctx.arc(-3.4, -0.3, 0.7, 0, 6.283); ctx.fill();
  ctx.strokeStyle = rgba(shade(skin, 0.55), 1); ctx.lineWidth = 1.2; ctx.beginPath(); ctx.ellipse(0, -0.4, 8.6, 3.8, -0.15, Math.PI, Math.PI * 2); ctx.stroke(); ctx.restore();
  // brow
  ctx.strokeStyle = rgba(shade(hair, 0.9), 1); ctx.lineWidth = female ? 2.2 : 3.4; ctx.lineCap = 'round'; ctx.beginPath(); ctx.moveTo(X - 52, top + 72); ctx.quadraticCurveTo(X - 36, top + 66, X - 18, top + 70); ctx.stroke();
  // nostril and mouth line
  ctx.fillStyle = rgba(shade(skin, 0.5), 1); ctx.beginPath(); ctx.ellipse(X - 58 - noseLen * 0.35, top + 124 + noseUp * 0.6, 3, 1.8, 0.3, 0, 6.283); ctx.fill();
  ctx.strokeStyle = rgba(shade(skin, 0.45), 1); ctx.lineWidth = 1.3; ctx.beginPath(); ctx.moveTo(X - 56, top + 146); ctx.quadraticCurveTo(X - 42, top + 148, X - 34, top + 144); ctx.stroke();
  // lips colour
  ctx.fillStyle = rgba(female ? [150, 70, 80] : shade(skin, 0.78), female ? 0.55 : 0.5); ctx.beginPath(); ctx.moveTo(X - 60 - lipFull, top + 141); ctx.quadraticCurveTo(X - 50, top + 136, X - 42, top + 144); ctx.quadraticCurveTo(X - 50, top + 156, X - 58 - lipFull * 0.6, top + 150); ctx.closePath(); ctx.fill();
  // ear
  ctx.fillStyle = rgba(shade(skin, 0.92), 1); ctx.beginPath(); ctx.ellipse(X + 32, top + 104, 9, 15, 0.1, 0, 6.283); ctx.fill(); ctx.strokeStyle = rgba(shade(skin, 0.62), 1); ctx.lineWidth = 1.2; ctx.beginPath(); ctx.ellipse(X + 33, top + 104, 5, 9, 0.1, 0.3, 5.2); ctx.stroke();

  // ---- beard ----
  if (beard) { ctx.fillStyle = rgba(shade(hair, 0.85), 0.95); ctx.beginPath(); ctx.moveTo(X - 58, top + 150); ctx.bezierCurveTo(X - 66, top + 170, X - 52, top + 196 + (beard === 2 ? 14 : 0), X - 20, top + 190 + (beard === 2 ? 12 : 0)); ctx.bezierCurveTo(X + 2, top + 184, X + 12, top + 176, X + 16, top + 164); ctx.lineTo(X + 10, top + 150); ctx.bezierCurveTo(X - 10, top + 158, X - 40, top + 156, X - 50, top + 150); ctx.closePath(); ctx.fill(); }
  // ---- hair on the head ----
  if (!bald) {
    ctx.fillStyle = rgba(hair, 1);
    ctx.beginPath();
    switch (style) {
      case 0: // cropped: follows the skull, hairline straight
        ctx.moveTo(X - 46 * forehead + recede * 0.6, top + 32 + recede * 0.4); ctx.bezierCurveTo(X - 30, top - 2, X + 10, top - 12, X + 40, top + 4); ctx.bezierCurveTo(X + 66, top + 8, X + 78, top + 40, X + 72, top + 72); ctx.lineTo(X + 42, top + 70); ctx.bezierCurveTo(X + 40, top + 40, X + 10, top + 22, X - 30 + recede * 0.5, top + 34 + recede * 0.4); break;
      case 1: // swept back
        ctx.moveTo(X - 48 * forehead + recede * 0.6, top + 30 + recede * 0.4); ctx.bezierCurveTo(X - 20, top - 14, X + 30, top - 18, X + 60, top + 2); ctx.bezierCurveTo(X + 84, top + 22, X + 82, top + 60, X + 72, top + 80); ctx.lineTo(X + 44, top + 72); ctx.bezierCurveTo(X + 44, top + 40, X + 20, top + 26, X - 28 + recede * 0.5, top + 36 + recede * 0.4); break;
      case 2: // side part with a fringe
        ctx.moveTo(X - 54 * forehead, top + 38 + recede * 0.5); ctx.bezierCurveTo(X - 56, top + 18, X - 20, top - 10, X + 30, top - 8); ctx.bezierCurveTo(X + 70, top - 4, X + 80, top + 40, X + 72, top + 76); ctx.lineTo(X + 44, top + 70); ctx.bezierCurveTo(X + 40, top + 36, X + 10, top + 24, X - 36, top + 44 + recede * 0.5); break;
      case 3: // high crest
        ctx.moveTo(X - 46 * forehead + recede * 0.6, top + 30 + recede * 0.4); ctx.bezierCurveTo(X - 30, top - 20, X + 0, top - 30, X + 30, top - 14); ctx.bezierCurveTo(X + 66, top - 4, X + 78, top + 40, X + 72, top + 74); ctx.lineTo(X + 42, top + 70); ctx.bezierCurveTo(X + 40, top + 40, X + 10, top + 22, X - 30 + recede * 0.5, top + 34 + recede * 0.4); break;
      case 4: // long, tied back
        ctx.moveTo(X - 50 * forehead, top + 32); ctx.bezierCurveTo(X - 30, top - 6, X + 20, top - 14, X + 50, top + 2); ctx.bezierCurveTo(X + 80, top + 18, X + 84, top + 60, X + 74, top + 100); ctx.lineTo(X + 48, top + 100); ctx.bezierCurveTo(X + 46, top + 50, X + 14, top + 24, X - 30, top + 36); break;
      default: // long, loose, falls past the jaw
        ctx.moveTo(X - 54 * forehead, top + 40); ctx.bezierCurveTo(X - 40, top - 4, X + 10, top - 16, X + 50, top + 0); ctx.bezierCurveTo(X + 92, top + 14, X + 104, top + 110, X + 96, top + 200); ctx.quadraticCurveTo(X + 70, top + 212, X + 50, top + 200); ctx.lineTo(X + 48, top + 100); ctx.bezierCurveTo(X + 46, top + 50, X + 14, top + 26, X - 34, top + 46); break;
    }
    ctx.closePath(); ctx.fill();
    // hair sheen
    ctx.save(); ctx.clip(); const sheen = ctx.createLinearGradient(X - 40, top, X + 60, top + 90); sheen.addColorStop(0, 'rgba(255,240,215,.35)'); sheen.addColorStop(0.4, 'rgba(255,240,215,0)'); sheen.addColorStop(1, 'rgba(0,0,0,.35)'); ctx.fillStyle = sheen; ctx.fillRect(0, 0, 256, 256); ctx.restore();
  } else {
    // bald: a sheen on the crown
    ctx.save(); face(); ctx.clip(); const sh = ctx.createRadialGradient(X - 20, top + 30, 2, X - 20, top + 30, 40); sh.addColorStop(0, 'rgba(255,245,225,.35)'); sh.addColorStop(1, 'rgba(255,245,225,0)'); ctx.fillStyle = sh; ctx.fillRect(0, 0, 256, 256); ctx.restore();
  }
  // ---- plate frame: brass bevel + corner marks ----
  ctx.strokeStyle = 'rgba(201,162,74,.9)'; ctx.lineWidth = 3; ctx.strokeRect(3, 3, 250, 250);
  ctx.strokeStyle = 'rgba(230,199,122,.35)'; ctx.lineWidth = 1; ctx.strokeRect(8, 8, 240, 240);
  const vig = ctx.createRadialGradient(128, 128, 90, 128, 128, 190); vig.addColorStop(0, 'rgba(0,0,0,0)'); vig.addColorStop(1, 'rgba(0,0,0,.55)'); ctx.fillStyle = vig; ctx.fillRect(0, 0, 256, 256);
  if (spec.alive === false) { ctx.fillStyle = 'rgba(10,12,18,.55)'; ctx.fillRect(0, 0, 256, 256); ctx.strokeStyle = 'rgba(216,112,90,.9)'; ctx.lineWidth = 4; ctx.beginPath(); ctx.moveTo(24, 232); ctx.lineTo(232, 24); ctx.stroke(); }
  const url = c.toDataURL('image/png'); cache.set(key, url); return url;
}

/** Fill every <img data-bust='{"id":..,"sex":..,"age":..,"accent":..}'> lacking a src. */
export function paint() {
  document.querySelectorAll('img[data-bust]').forEach(el => { if (el.getAttribute('src')) return; try { el.setAttribute('src', render(JSON.parse(el.getAttribute('data-bust')))); } catch (e) { console.error('bust', e); } });
}
window.praetoriaBust = { render, paint };
