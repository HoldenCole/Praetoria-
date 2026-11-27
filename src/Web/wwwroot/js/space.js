// Praetoria: the rendered galaxy (docs/VISUAL_SYSTEM.md 7, docs/ART_DIRECTION.md §1). Three.js over WebGL: a shader nebula, the
// territory of the Reach drawn by one shader from data rasterised only when the holdings change, stars by class in their own glow
// pass, lanes as screen-space hairlines, and the names, fleets and marks as crisp screen-space elements over it; picking back to
// Blazor. The model is plain JSON handed over by GalaxyMap.razor after every render.
//
// The territory (7.2), as the mock's tools/terr.py draws it: the outer edge of the land is the threshold of a smooth field (a sum of
// Gaussians over every system), so it is one curve with no scallops; inside, every point belongs to its nearest system. Per texel the
// CPU keeps the four nearest systems and the field's signed distance; the fragment shader finds the exact nearest system among them
// and the exact distance to the edge of its holder, its realm and its region, and measures each in screen pixels along the edge's own
// normal, so every line is 1 to 2px at every zoom and does not alias under the tilted camera. Three drawings of the same cells are
// cross-faded by distance: the realms from far off, the houses in the middle distance, the systems close to. The territory, the
// lanes and the nebula are drawn in one pass; only the stars go through the bloom, and are added over it.
import * as THREE from 'three';
import { EffectComposer } from 'three/addons/postprocessing/EffectComposer.js';
import { RenderPass } from 'three/addons/postprocessing/RenderPass.js';
import { ShaderPass } from 'three/addons/postprocessing/ShaderPass.js';
import { UnrealBloomPass } from 'three/addons/postprocessing/UnrealBloomPass.js';
import { CSS2DRenderer, CSS2DObject } from 'three/addons/renderers/CSS2DRenderer.js';

function hash(s) { let h = 2166136261; for (let i = 0; i < s.length; i++) { h ^= s.charCodeAt(i); h = Math.imul(h, 16777619); } return h >>> 0; }
function rnd(seed) { let t = seed >>> 0; return () => { t += 0x6D2B79F5; let r = Math.imul(t ^ (t >>> 15), 1 | t); r ^= r + Math.imul(r ^ (r >>> 7), 61 | r); return ((r ^ (r >>> 14)) >>> 0) / 4294967296; }; }
const rgb01 = h => { let c = String(h || '#8fa8d8').replace('#', ''); if (c.length === 3) c = c.split('').map(x => x + x).join(''); return [parseInt(c.slice(0, 2), 16) / 255, parseInt(c.slice(2, 4), 16) / 255, parseInt(c.slice(4, 6), 16) / 255]; };
/** A colour toned as the spec tones a house colour for anything larger than a mark: saturation and value scaled (2.2). */
function tone(c, sat, val) {
  const [r, g, b] = c, mx = Math.max(r, g, b), mn = Math.min(r, g, b), d = mx - mn; let h = 0;
  if (d) { if (mx === r) h = ((g - b) / d) % 6; else if (mx === g) h = (b - r) / d + 2; else h = (r - g) / d + 4; h /= 6; if (h < 0) h += 1; }
  const s = (mx ? d / mx : 0) * sat, v = Math.min(1, mx * val), i = Math.floor(h * 6), f = h * 6 - i, p = v * (1 - s), q = v * (1 - f * s), t = v * (1 - (1 - f) * s);
  return [[v, t, p], [q, v, p], [p, v, t], [p, q, v], [t, p, v], [v, p, q]][i % 6];
}
const css = c => `rgb(${Math.round(c[0] * 255)},${Math.round(c[1] * 255)},${Math.round(c[2] * 255)})`;
const ico = (name, toneName, cls = '') => `<svg class="icon ico ${toneName} ${cls}" viewBox="0 0 512 512" aria-hidden="true"><use href="#i-${name}"/></svg>`;
// stars by class (7.3): B, F, G, K, M
const STAR = [[169, 196, 255], [244, 240, 255], [255, 225, 160], [255, 185, 120], [255, 148, 120]].map(c => c.map(v => v / 255));
const starClass = id => [0, 1, 1, 2, 2, 2, 3, 3, 4][hash(id) % 9];
const DEPOSIT = { helium3: ['helium3', 'steel'], rare_earths: ['rare_earths', 'ivory'], isotopes: ['isotopes', 'ember'] };
// lit globes for the tooltip, the ramps of Components/Ui/Globe.razor
const GLOBE = { forge: ['#ffd8a8', '#d86a33', '#8a2f1c', '#3d140d', 'rgba(255,120,70,.45)'], arid: ['#fbe3b4', '#d9a35c', '#8a5a26', '#3f2510', 'rgba(255,200,130,.35)'], barren: ['#fbe3b4', '#d9a35c', '#8a5a26', '#3f2510', 'rgba(255,200,130,.35)'],
  ocean: ['#d6ecff', '#4f8fd6', '#1d4f8f', '#0b2346', 'rgba(120,180,255,.45)'], frozen: ['#ffffff', '#cfe3f2', '#7fa2c0', '#2a3d52', 'rgba(200,230,255,.4)'], gas: ['#fff0cf', '#e2b87a', '#a0703a', '#3d2a14', 'rgba(255,220,160,.35)'],
  belt: ['#e6e0d4', '#a89f90', '#6a6256', '#26231f', 'rgba(220,210,190,.25)'], terran: ['#d6efcf', '#6aa36a', '#2f6b56', '#1d4a7a', 'rgba(110,190,255,.45)'] };
const globe = (cls, px) => { const g = GLOBE[cls] || GLOBE.terran; return `<span class="ui-globe sm" style="width:${px / 15}rem;height:${px / 15}rem;--g0:${g[0]};--g1:${g[1]};--g2:${g[2]};--g3:${g[3]};--atm:${g[4]}"></span>`; };
const sentence = s => { s = String(s || ''); return s ? s[0].toUpperCase() + s.slice(1).toLowerCase() : s; };

// ---- textures drawn once (the close map of a system uses them) ----
function radialTexture(size, stops) {
  const c = document.createElement('canvas'); c.width = c.height = size; const ctx = c.getContext('2d');
  const g = ctx.createRadialGradient(size / 2, size / 2, 0, size / 2, size / 2, size / 2);
  for (const [o, col] of stops) g.addColorStop(o, col);
  ctx.fillStyle = g; ctx.fillRect(0, 0, size, size);
  const t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace; return t;
}
function glyphTexture(draw) {
  const c = document.createElement('canvas'); c.width = c.height = 64; const ctx = c.getContext('2d'); draw(ctx); const t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace; return t;
}
const TEX = {};
function textures() {
  if (TEX.sun) return TEX;
  TEX.sun = radialTexture(128, [[0, 'rgba(255,255,255,1)'], [0.18, 'rgba(255,245,220,1)'], [0.35, 'rgba(255,220,150,.55)'], [0.6, 'rgba(255,180,90,.12)'], [1, 'rgba(0,0,0,0)']]);
  TEX.glow = radialTexture(128, [[0, 'rgba(255,255,255,.9)'], [0.4, 'rgba(255,255,255,.25)'], [1, 'rgba(255,255,255,0)']]);
  TEX.star = radialTexture(32, [[0, 'rgba(255,255,255,1)'], [0.3, 'rgba(255,255,255,.6)'], [1, 'rgba(255,255,255,0)']]);
  // a hull seen from above: a long bow, two engine pods and a notch at the stern
  TEX.hull = glyphTexture(ctx => { ctx.fillStyle = '#fff'; ctx.beginPath(); ctx.moveTo(32, 2); ctx.lineTo(41, 26); ctx.lineTo(54, 40); ctx.lineTo(54, 58); ctx.lineTo(44, 58); ctx.lineTo(41, 48); ctx.lineTo(36, 54); ctx.lineTo(28, 54); ctx.lineTo(23, 48); ctx.lineTo(20, 58); ctx.lineTo(10, 58); ctx.lineTo(10, 40); ctx.lineTo(23, 26); ctx.closePath(); ctx.fill(); });
  TEX.ship = glyphTexture(ctx => { ctx.fillStyle = '#fff'; ctx.beginPath(); ctx.moveTo(32, 4); ctx.lineTo(56, 56); ctx.lineTo(32, 44); ctx.lineTo(8, 56); ctx.closePath(); ctx.fill(); });
  TEX.flare = (() => { const c = document.createElement('canvas'); c.width = 256; c.height = 16; const ctx = c.getContext('2d'); const g = ctx.createLinearGradient(0, 0, 256, 0); g.addColorStop(0, 'rgba(255,255,255,0)'); g.addColorStop(.5, 'rgba(255,255,255,.9)'); g.addColorStop(1, 'rgba(255,255,255,0)'); ctx.fillStyle = g; ctx.fillRect(0, 0, 256, 16); const t = new THREE.CanvasTexture(c); return t; })();
  TEX.skull = glyphTexture(ctx => { ctx.fillStyle = '#fff'; ctx.beginPath(); ctx.arc(32, 26, 16, 0, 6.283); ctx.fill(); ctx.fillRect(22, 36, 20, 12); ctx.fillStyle = '#000'; ctx.beginPath(); ctx.arc(25, 26, 4.5, 0, 6.283); ctx.arc(39, 26, 4.5, 0, 6.283); ctx.fill(); ctx.fillRect(28, 40, 2, 8); ctx.fillRect(34, 40, 2, 8); ctx.fillStyle = '#fff'; ctx.save(); ctx.translate(32, 50); ctx.rotate(.6); ctx.fillRect(-18, -2, 36, 4); ctx.rotate(-1.2); ctx.fillRect(-18, -2, 36, 4); ctx.restore(); });
  TEX.station = glyphTexture(ctx => { ctx.strokeStyle = '#fff'; ctx.lineWidth = 6; ctx.beginPath(); ctx.rect(14, 14, 36, 36); ctx.stroke(); ctx.beginPath(); ctx.arc(32, 32, 8, 0, 6.283); ctx.fill(); });
  return TEX;
}

// ---- nebula shader ----
const NEBULA_VS = `varying vec2 vUv; void main(){ vUv = uv; gl_Position = projectionMatrix * modelViewMatrix * vec4(position,1.0); }`;
const NOISE = `
vec3 mod289(vec3 x){return x-floor(x*(1.0/289.0))*289.0;} vec2 mod289(vec2 x){return x-floor(x*(1.0/289.0))*289.0;} vec3 permute(vec3 x){return mod289(((x*34.0)+1.0)*x);}
float snoise(vec2 v){ const vec4 C=vec4(0.211324865405187,0.366025403784439,-0.577350269189626,0.024390243902439); vec2 i=floor(v+dot(v,C.yy)); vec2 x0=v-i+dot(i,C.xx); vec2 i1=(x0.x>x0.y)?vec2(1.0,0.0):vec2(0.0,1.0); vec4 x12=x0.xyxy+C.xxzz; x12.xy-=i1; i=mod289(i); vec3 p=permute(permute(i.y+vec3(0.0,i1.y,1.0))+i.x+vec3(0.0,i1.x,1.0)); vec3 m=max(0.5-vec3(dot(x0,x0),dot(x12.xy,x12.xy),dot(x12.zw,x12.zw)),0.0); m=m*m; m=m*m; vec3 x=2.0*fract(p*C.www)-1.0; vec3 h=abs(x)-0.5; vec3 ox=floor(x+0.5); vec3 a0=x-ox; m*=1.79284291400159-0.85373472095314*(a0*a0+h*h); vec3 g; g.x=a0.x*x0.x+h.x*x0.y; g.yz=a0.yz*x12.xz+h.yz*x12.yw; return 130.0*dot(m,g); }
float fbm(vec2 p){ float v=0.0, a=0.5; for(int i=0;i<6;i++){ v+=a*snoise(p); p=p*2.03+vec2(17.1,9.3); a*=0.5; } return v; }`;
const NEBULA_FS = `precision highp float; varying vec2 vUv; uniform float uTime; uniform vec3 uA; uniform vec3 uB; uniform vec3 uC; ${NOISE}
void main(){
  vec2 p = vUv * 3.0;
  float n1 = fbm(p + uTime*0.004);
  float n2 = fbm(p*1.7 + vec2(5.2, 1.3) - uTime*0.003);
  float n3 = fbm(p*0.6 + vec2(-3.0, 8.0));
  float cloud = smoothstep(-0.15, 0.75, n1) * smoothstep(-0.3, 0.6, n3);
  float wisps = pow(max(0.0, n2), 2.2) * 0.9;
  vec3 col = uA * cloud * 0.95 + uB * wisps * 1.1 + uC * pow(cloud, 3.0) * 1.2;
  float knots = pow(max(0.0, fbm(p*3.3 + 21.0)), 5.0) * cloud * 2.5; col += vec3(0.9, 0.75, 0.6) * knots;
  float dust = smoothstep(0.2, 0.5, fbm(p*2.5 + 40.0)) * 0.35;
  col *= (1.0 - dust);
  float vign = smoothstep(1.2, 0.2, length(vUv - 0.5) * 1.6);
  gl_FragColor = vec4(col * vign * 2.4, 1.0);   // no bloom lifts it now (it is drawn in the main pass): a little brighter
}`;

// ---- the territory (7.2) ----
const TERR_VS = `varying vec2 vW; void main(){ vec4 w = modelMatrix * vec4(position, 1.0); vW = w.xy; gl_Position = projectionMatrix * viewMatrix * w; }`;
const TERR_FS = `
uniform sampler2D uCand; uniform sampler2D uField; uniform sampler2D uSites; uniform sampler2D uPal;
uniform vec4 uBox; uniform vec3 uTier; uniform float uDpr;
varying vec2 vW;
const vec3 IVORY = vec3(0.890, 0.827, 0.643);   // the Reach's outer edge, #E3D3A4
const vec3 OWN = vec3(0.914, 0.725, 0.286);     // your land, #E9B949
const vec3 OWNLINE = vec3(0.941, 0.776, 0.380); // its border, #F0C661
const vec3 WASH = vec3(0.93, 0.85, 0.66);
const vec3 SLATE = vec3(0.62, 0.67, 0.78);
void over(inout vec4 d, vec3 c, float a){ a = clamp(a, 0.0, 1.0); d.rgb = c * a + d.rgb * (1.0 - a); d.a = a + d.a * (1.0 - a); }
float band(float e, float a, float b){ return clamp(min(e - a, b - e) + 0.5, 0.0, 1.0); }
float candOf(vec4 c, int k){ return k == 0 ? c.r : k == 1 ? c.g : k == 2 ? c.b : c.a; }
// a realm's edge: the Reach's in ivory with a short glow inward (the only line that glows), a nation's or a state's in its own toned
// colour, the edge of nobody's space beyond the rim a faint slate
void realmEdge(inout vec4 d, bool reach, bool outer, vec3 lineCol, float er, bool inside){
  if (!inside) return;
  if (reach) { over(d, IVORY, exp(-max(er, 0.0) / 5.0) * 0.10); over(d, vec3(0.0), clamp(1.0 - abs(er), 0.0, 1.0) * 0.5); over(d, IVORY, band(er, 0.6, 2.1) * 0.82); }
  else if (outer) over(d, SLATE, band(er, 0.4, 1.4) * 0.22);
  else { over(d, vec3(0.0), clamp(1.0 - abs(er), 0.0, 1.0) * 0.4); over(d, lineCol, band(er, 0.4, 1.9) * 0.7); }
}
// your own land: a 2px gold border (over the Reach's edge where the two meet) and an ivory hairline 4px inside it
void ownEdge(inout vec4 d, bool mine, float pOwn, float er, bool inside, float k){
  if (!mine || !inside) return;
  if (er <= pOwn + 0.01) over(d, OWNLINE, band(er, 0.6, 2.6) * 0.92 * k);
  over(d, vec3(1.0, 0.96, 0.86), band(min(pOwn, er), 4.2, 5.2) * 0.38 * k);
}
void main(){
  vec2 uv = (vW - uBox.xy) / uBox.zw;
  // world units per CSS pixel along x and y of the screen
  vec2 jx = dFdx(vW) * uDpr, jy = dFdy(vW) * uDpr;
  vec4 cand = texture(uCand, uv), fld = texture(uField, uv);
  // the nearest system, exactly, among the four the texel keeps
  float ida = -1.0, best = 1e20; vec4 a0 = vec4(0.0), a1 = vec4(0.0);
  for (int k = 0; k < 4; k++) {
    float id = candOf(cand, k); if (id < 0.0) continue;
    vec4 s0 = texelFetch(uSites, ivec2(int(id + 0.5), 0), 0); vec2 q = vW - s0.xy; float dd = dot(q, q);
    if (dd < best) { best = dd; ida = id; a0 = s0; a1 = texelFetch(uSites, ivec2(int(id + 0.5), 1), 0); }
  }
  // how far to the nearest edge of a cell, of a holder, of a realm and of a region: the bisector with the nearest system across it
  float eAny = 1e5, eOwn = 1e5, eRl = 1e5, eReg = 1e5; vec2 nAny = vec2(1.0, 0.0), nOwn = nAny, nRl = nAny, nReg = nAny;
  for (int k = 0; k < 4; k++) {
    float id = candOf(cand, k); if (id < 0.0 || abs(id - ida) < 0.5) continue;
    vec4 s0 = texelFetch(uSites, ivec2(int(id + 0.5), 0), 0), s1 = texelFetch(uSites, ivec2(int(id + 0.5), 1), 0);
    vec2 ab = s0.xy - a0.xy; float L = length(ab); if (L < 1e-3) continue;
    vec2 q = vW - s0.xy; float bis = (dot(q, q) - best) / (2.0 * L); vec2 n = ab / L;
    if (bis < eAny) { eAny = bis; nAny = n; }
    if (abs(s0.z - a0.z) > 0.5 && bis < eOwn) { eOwn = bis; nOwn = n; }
    if (abs(s0.w - a0.w) > 0.5 && bis < eRl) { eRl = bis; nRl = n; }
    if (abs(s1.x - a1.x) > 0.5 && bis < eReg) { eReg = bis; nReg = n; }
  }
  vec2 nf = dot(fld.gb, fld.gb) > 1e-8 ? normalize(fld.gb) : vec2(1.0, 0.0);
  #define PX(e, n) ((e) / max(1e-6, length(vec2(dot(n, jx), dot(n, jy)))))
  float pAny = PX(eAny, nAny), pOwn = PX(eOwn, nOwn), pRl = PX(eRl, nRl), pReg = PX(eReg, nReg), pOut = PX(fld.r, nf);
  if (ida < 0.0) discard;
  vec4 hf = texelFetch(uPal, ivec2(int(a0.z + 0.5), 0), 0), hl = texelFetch(uPal, ivec2(int(a0.z + 0.5), 1), 0);
  vec4 rl = texelFetch(uPal, ivec2(int(a0.w + 0.5), 2), 0), rf = texelFetch(uPal, ivec2(int(a0.w + 0.5), 3), 0);
  bool none = hf.a < 0.5, mine = abs(hf.a - 2.0) < 0.5, nation = abs(hf.a - 3.0) < 0.5;
  bool reach = rl.a < 0.5, foreign = abs(rl.a - 1.0) < 0.5, outer = rl.a > 2.5;
  float cover = clamp(pOut + 0.5, 0.0, 1.0); bool inside = pOut > -1.0;
  float hatch = mod(floor(gl_FragCoord.x) + floor(gl_FragCoord.y), floor(10.0 * uDpr + 0.5)) < uDpr ? 1.0 : 0.0;
  float ein = min(min(pOwn, pRl), pOut), bnd = exp(-max(ein, 0.0) / 16.0), er = min(pRl, pOut);
  bool seam = pOwn < pRl - 0.01 && pOwn < pOut - 0.01;
  vec4 outc = vec4(0.0);
  if (uTier.x > 0.001) {   // far: the realms only
    vec4 d = vec4(0.0); float bandR = exp(-max(er, 0.0) / 22.0);
    if (reach) over(d, WASH, (0.05 + 0.06 * bandR) * cover);
    else if (!outer) over(d, rf.rgb, (0.07 + 0.15 * bandR + (foreign ? 0.09 * hatch : 0.0)) * cover);
    if (mine) over(d, OWN, 0.16 * cover);
    if (pRl < pOut - 0.01) over(d, vec3(0.0), clamp(1.0 - pRl, 0.0, 1.0) * 0.45);
    realmEdge(d, reach, outer, rl.rgb, er, inside);
    outc += d * uTier.x;
  }
  if (uTier.y > 0.001) {   // the middle distance: the houses
    vec4 d = vec4(0.0);
    if (reach) over(d, WASH, 0.045 * cover);
    if (!none) over(d, hf.rgb, (nation ? 0.05 + 0.14 * bnd + 0.09 * hatch : mine ? 0.15 + 0.25 * bnd : 0.06 + 0.16 * bnd) * cover);
    if (reach && inside && pReg < pOwn - 0.01) over(d, vec3(0.95, 0.9, 0.78), clamp(1.0 - abs(pReg - 0.5), 0.0, 1.0) * 0.13);
    if (seam) { over(d, vec3(0.0), clamp(1.0 - pOwn, 0.0, 1.0) * 0.5); if (!none) over(d, hl.rgb, band(pOwn, 0.6, mine ? 2.6 : 1.9) * (mine ? 0.92 : 0.56)); }
    realmEdge(d, reach, outer, rl.rgb, er, inside);
    ownEdge(d, mine, pOwn, er, inside, 1.0);
    outc += d * uTier.y;
  }
  if (uTier.z > 0.001) {   // close to: the systems, a faint tint and a fine line round each cell
    vec4 d = vec4(0.0);
    if (reach) over(d, WASH, 0.03 * cover);
    if (!none) over(d, hf.rgb, (mine ? 0.10 + 0.12 * bnd : 0.035 + 0.06 * bnd) * cover);
    if (inside) { over(d, vec3(0.0), clamp(1.0 - pAny, 0.0, 1.0) * 0.3); over(d, none ? SLATE : hl.rgb, band(pAny, 0.5, 1.5) * (none ? 0.2 : 0.4)); }
    if (seam && !none) over(d, hl.rgb, band(pOwn, 0.5, mine ? 2.2 : 1.5) * (mine ? 0.8 : 0.5));
    realmEdge(d, reach, outer, rl.rgb, er, inside);
    ownEdge(d, mine, pOwn, er, inside, 0.9);
    outc += d * uTier.z;
  }
  gl_FragColor = outc;
}`;

// ---- the lanes (7.3): screen-space hairlines that stop short of each star ----
const LANE_VS = `
attribute vec3 pb; attribute vec2 corner; attribute vec4 col; attribute float wpx;
uniform vec2 uRes; uniform float uDpr;
varying vec4 vCol; varying float vSide; varying float vAlong; varying float vLen; varying float vWd;
void main(){
  vec4 ca = projectionMatrix * modelViewMatrix * vec4(position, 1.0), cb = projectionMatrix * modelViewMatrix * vec4(pb, 1.0);
  vec2 sa = (ca.xy / ca.w * 0.5 + 0.5) * uRes, sb = (cb.xy / cb.w * 0.5 + 0.5) * uRes;
  vec2 dv = sb - sa; float L = max(length(dv), 1e-3); vec2 dir = dv / L, nrm = vec2(-dir.y, dir.x);
  float hw = (wpx * 0.5 + 1.0) * uDpr;
  vec2 sp = mix(sa, sb, corner.x) + nrm * corner.y * hw; vec4 c = corner.x < 0.5 ? ca : cb;
  gl_Position = vec4((sp / uRes * 2.0 - 1.0) * c.w, c.z, c.w);
  vCol = col; vSide = corner.y * hw; vAlong = corner.x * L; vLen = L; vWd = wpx * uDpr;
}`;
const LANE_FS = `
uniform float uDpr; uniform float uGap;
varying vec4 vCol; varying float vSide; varying float vAlong; varying float vLen; varying float vWd;
void main(){
  float gap = uGap * uDpr; float cap = clamp(min(vAlong - gap, vLen - gap - vAlong) / uDpr + 0.5, 0.0, 1.0);
  float a = clamp((max(vWd, uDpr) * 0.5 - abs(vSide)) / uDpr + 0.5, 0.0, 1.0) * min(1.0, vWd / uDpr);
  float al = vCol.a * a * cap; if (al <= 0.003) discard;
  gl_FragColor = vec4(vCol.rgb * al, al);
}`;

// ---- the stars (7.3): a core and a soft halo by class, constant in pixels; drawn into the glow pass ----
const STAR_VS = `
attribute vec3 color; attribute vec2 sz; uniform float uDpr; uniform float uGrow;
varying vec3 vC; varying float vK;
void main(){ vC = color; vec4 mv = modelViewMatrix * vec4(position, 1.0); gl_Position = projectionMatrix * mv; gl_PointSize = sz.y * uGrow * uDpr; vK = sz.x / sz.y; }`;
const STAR_FS = `
varying vec3 vC; varying float vK;
void main(){
  vec2 q = gl_PointCoord * 2.0 - 1.0; float r = length(q); if (r > 1.0) discard;
  float halo = pow(1.0 - r, 2.4) * 0.62 + exp(-r * r / (vK * vK * 3.0)) * 0.35;
  float core = 1.0 - smoothstep(vK * 0.5, vK, r);
  gl_FragColor = vec4(vC * halo + mix(vC, vec3(1.0), 0.72) * core * 1.15, 1.0);
}`;
// the last pass: the scene, and the stars' light added over it
const COMPOSITE = {
  uniforms: { tDiffuse: { value: null }, tGlow: { value: null }, uGlow: { value: 1 } },
  vertexShader: `varying vec2 vUv; void main(){ vUv = uv; gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0); }`,
  fragmentShader: `uniform sampler2D tDiffuse; uniform sampler2D tGlow; uniform float uGlow; varying vec2 vUv; void main(){ vec4 b = texture2D(tDiffuse, vUv); vec3 g = texture2D(tGlow, vUv).rgb * uGlow; gl_FragColor = vec4(b.rgb + g, 1.0); }`
};
const ss = (a, b, v) => { const q = Math.max(0, Math.min(1, (v - a) / (b - a))); return q * q * (3 - 2 * q); };
const HOME_D = 1650;   // the opening distance (7.1): 15 to 22 systems on a 1600 by 1000 screen
const MAX_EMPTY = 0.2; // the empty space past the Reach's edge stays under a fifth of the free map (7.1)
let lastCover = null;  // the interface's rectangles, as js/shell.js last told them

export class Galaxy {
  constructor(container, dotnet) {
    this.container = container; this.dotnet = dotnet; this.model = null; this.objects = new THREE.Group(); this.glowObjects = new THREE.Group(); this.labels = [];
    this.selected = { systemId: null, fleetId: null }; this.dirty = true; this.disposed = false; this.cover = lastCover;
    // the close map of one system (docs/SYSTEMS.md, "The close map"): its own group, its own labels and its own things to click
    this.sys = null; this.sysGroup = null; this.sysLabels = []; this.sysHits = []; this.sysBodies = {}; this.galCam = null;
    const w = container.clientWidth || 800, h = container.clientHeight || 500; this.vw = w; this.vh = h;
    this.renderer = new THREE.WebGLRenderer({ antialias: true, alpha: false, powerPreference: 'high-performance' });
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 1.5)); this.renderer.setSize(w, h); this.renderer.toneMapping = THREE.ACESFilmicToneMapping; this.renderer.toneMappingExposure = 1.1;
    this.renderer.domElement.className = 'gl'; container.appendChild(this.renderer.domElement);
    // A browser may take the graphics context away (a sleeping laptop, a driver restart, too many tabs). Nothing is drawn while it is
    // gone; when it is given back the map is sized and drawn again; if not, the map is built afresh on a new canvas (rebuild below).
    this.lost = false; this.lostAt = 0; this.onGiveUp = null;
    this.renderer.domElement.addEventListener('webglcontextlost', e => { e.preventDefault(); this.lost = true; this.lostAt = performance.now(); });
    this.renderer.domElement.addEventListener('webglcontextrestored', () => { this.lost = false; this.resize(); this.dirty = true; this.terrKey = null; if (this.model) this.setModel(this.model); });
    const shade = document.createElement('div'); shade.className = 'gl-vignette'; container.appendChild(shade);
    this.labelRenderer = new CSS2DRenderer(); this.labelRenderer.setSize(w, h); this.labelRenderer.domElement.className = 'gl-labels tier-mid'; container.appendChild(this.labelRenderer.domElement);
    this.tipEl = document.createElement('div'); this.tipEl.className = 'gtip map-gtip'; this.tipEl.style.display = 'none'; document.body.appendChild(this.tipEl);
    this.scene = new THREE.Scene(); this.scene.background = new THREE.Color(0x02030a);
    this.glowScene = new THREE.Scene();
    this.camera = new THREE.PerspectiveCamera(42, w / h, 1, 20000); this.probe = new THREE.PerspectiveCamera(42, w / h, 1, 20000);
    this.target = new THREE.Vector3(600, -350, 0); this.distance = HOME_D; this.tilt = 0.42; this.vo = null;
    // the main pass draws into a multisampled target, so the hairlines are smooth; the stars are drawn and bloomed on their own and
    // added over it, so no border, lane or name is ever caught in the bloom (7.2)
    const pr = this.renderer.getPixelRatio(), webgl2 = this.renderer.capabilities.isWebGL2;
    const main = webgl2 ? new THREE.WebGLRenderTarget(Math.round(w * pr), Math.round(h * pr), { samples: 4, type: THREE.HalfFloatType }) : undefined;
    this.composer = new EffectComposer(this.renderer, main); this.composer.addPass(new RenderPass(this.scene, this.camera));
    this.bloom = new UnrealBloomPass(new THREE.Vector2(w, h), 0.9, 0.55, 0.3); this.bloom.enabled = false; this.composer.addPass(this.bloom);   // inside a system: the old bloom over everything
    const glowRT = new THREE.WebGLRenderTarget(Math.round(w * pr), Math.round(h * pr), { type: THREE.HalfFloatType });
    this.glowComposer = new EffectComposer(this.renderer, glowRT); this.glowComposer.renderToScreen = false; this.glowComposer.addPass(new RenderPass(this.glowScene, this.camera));
    this.glowBloom = new UnrealBloomPass(new THREE.Vector2(w, h), 0.75, 0.42, 0.12); this.glowComposer.addPass(this.glowBloom);
    this.final = new ShaderPass(COMPOSITE); this.composer.addPass(this.final);
    this.raycaster = new THREE.Raycaster(); this.mouse = new THREE.Vector2();
    this.buildBackdrop(); this.scene.add(this.objects); this.glowScene.add(this.glowObjects);
    this.bindInput(); this.updateCamera(true);
    this.resizeObserver = new ResizeObserver(() => this.resize()); this.resizeObserver.observe(container);
    this.clock = new THREE.Clock(); this.loop = this.loop.bind(this); requestAnimationFrame(this.loop);
  }

  buildBackdrop() {
    textures();
    const geo = new THREE.PlaneGeometry(6000, 4000);
    this.nebula = new THREE.Mesh(geo, new THREE.ShaderMaterial({ vertexShader: NEBULA_VS, fragmentShader: NEBULA_FS, depthWrite: false,
      uniforms: { uTime: { value: 0 }, uA: { value: new THREE.Color(0x1d2c66) }, uB: { value: new THREE.Color(0x4a2f6e) }, uC: { value: new THREE.Color(0x1f6a7a) } } }));
    this.nebula.position.set(600, -350, -900); this.scene.add(this.nebula);
    // stars in depth for parallax
    const r = rnd(1234); const n = 2600; const pos = new Float32Array(n * 3); const col = new Float32Array(n * 3); const size = new Float32Array(n);
    for (let i = 0; i < n; i++) { pos[i * 3] = -1500 + r() * 4200; pos[i * 3 + 1] = -2200 + r() * 3700; pos[i * 3 + 2] = -800 + r() * 700; const t = r(); const c = t < .15 ? [.7, .8, 1] : t < .8 ? [1, 1, 1] : [1, .85, .6]; col[i * 3] = c[0]; col[i * 3 + 1] = c[1]; col[i * 3 + 2] = c[2]; size[i] = 1 + r() * r() * 4; }
    const g = new THREE.BufferGeometry(); g.setAttribute('position', new THREE.BufferAttribute(pos, 3)); g.setAttribute('color', new THREE.BufferAttribute(col, 3)); g.setAttribute('size', new THREE.BufferAttribute(size, 1));
    const mat = new THREE.ShaderMaterial({ transparent: true, depthWrite: false, blending: THREE.AdditiveBlending, uniforms: { uTex: { value: TEX.star }, uTime: { value: 0 } },
      vertexShader: `attribute float size; attribute vec3 color; varying vec3 vC; varying float vS; uniform float uTime; void main(){ vC = color; vS = size; vec4 mv = modelViewMatrix * vec4(position,1.0); float tw = 0.75 + 0.25*sin(uTime*1.3 + position.x*0.37 + position.y*0.11); gl_PointSize = size * tw * (900.0 / -mv.z); gl_Position = projectionMatrix * mv; }`,
      fragmentShader: `uniform sampler2D uTex; varying vec3 vC; void main(){ vec4 t = texture2D(uTex, gl_PointCoord); gl_FragColor = vec4(vC, 1.0) * t; }` });
    this.stars = new THREE.Points(g, mat); this.scene.add(this.stars);
    this.scene.add(new THREE.AmbientLight(0x404a66, 0.9));
    this.sun = new THREE.DirectionalLight(0xfff0d8, 1.6); this.sun.position.set(-400, 300, 700); this.scene.add(this.sun);
  }

  // ---------------- the model ----------------
  setModel(model) {
    this.model = model;
    for (const l of this.labels) l.element.remove(); this.labels = []; this.pins = []; this.tokens = []; this.regionPins = []; this.realmPins = [];
    for (const grp of [this.objects, this.glowObjects]) while (grp.children.length) { const o = grp.children[0]; grp.remove(o); o.traverse?.(c => { if (c === this.terrMesh) return; c.geometry?.dispose?.(); if (c.material && !c.material.__shared) c.material.dispose?.(); }); }
    this.details = []; this.hideTip();
    if (!model) return;
    const byId = {}; for (const s of model.systems) byId[s.id] = s; this.byId = byId;
    const xs = model.systems.map(s => s.x), ys = model.systems.map(s => -s.y);
    this.bounds = { minX: Math.min(...xs), maxX: Math.max(...xs), minY: Math.min(...ys), maxY: Math.max(...ys) };
    // the backdrop is laid under the whole map, however large
    const bw = this.bounds.maxX - this.bounds.minX, bh = this.bounds.maxY - this.bounds.minY, bcx = (this.bounds.minX + this.bounds.maxX) / 2, bcy = (this.bounds.minY + this.bounds.maxY) / 2;
    this.nebula.position.set(bcx, bcy, -900); this.nebula.scale.set(Math.max(1, (bw + 3000) / 6000), Math.max(1, (bh + 3000) / 4000), 1);
    this.stars.position.set(bcx - 1200, bcy + 600, 0); this.stars.scale.set(Math.max(1, (bw + 2400) / 4200), Math.max(1, (bh + 2400) / 3700), 1);
    this.buildTerritory(model);
    this.buildLanes(model, byId);
    this.buildStars(model);
    this.buildPins(model, byId);
    this.buildFleets(model, byId);
    this.buildNames(model);
    // the route a selected fleet would take to the selected system, lane by lane; and the voyages under way
    if (model.route && model.route.length > 1) { const pts = model.route.map(id => byId[id]).filter(Boolean).map(s => new THREE.Vector3(s.x, -s.y, 1)); const rl = new THREE.Line(new THREE.BufferGeometry().setFromPoints(pts), new THREE.LineDashedMaterial({ color: 0xf6e2a0, dashSize: 10, gapSize: 7, transparent: true, opacity: .9, depthTest: false })); rl.computeLineDistances(); rl.renderOrder = 3; this.objects.add(rl); }
    for (const f of model.fleets) if (f.moving && byId[f.destId] && byId[f.systemId]) { const a = byId[f.systemId], d = byId[f.destId]; const lg = new THREE.Line(new THREE.BufferGeometry().setFromPoints([new THREE.Vector3(a.x, -a.y, 1), new THREE.Vector3(d.x, -d.y, 1)]), new THREE.LineDashedMaterial({ color: f.isPlayer ? 0xe6c77a : 0xa9b6d0, dashSize: 8, gapSize: 6, transparent: true, opacity: f.isPlayer ? .8 : .45, depthTest: false })); lg.computeLineDistances(); lg.renderOrder = 3; this.objects.add(lg); }
    this.buildDetails(model);
    if (!this.homeSet) {
      // the view opens on your own seat, at the opening distance, with the empty space past the Reach's edge kept small (7.1)
      const seat = byId[model.seat] || model.systems.find(s => s.isPlayer) || model.systems[0];
      this.seatAt = { x: seat.x, y: -seat.y }; let d = HOME_D;
      try { const kept = parseFloat(localStorage.getItem('praetoria.map.zoom.' + (model.key || ''))); if (kept > 150 && kept < 7000) d = kept; } catch { /* no storage: the opening distance */ }
      this.distance = Math.min(d, this.fitDistance()); this.vo = null;
      const t = this.clampTarget(this.seatAt.x, this.seatAt.y, this.distance); this.target.set(t.x, t.y, 0); this.homeSet = true; this.updateCamera(true);
    }
    this.applyMode(); this.dirty = true; this.relabel = true;
  }
  setSelection(sel) { this.selected = sel || { systemId: null, fleetId: null }; }

  /** The territory's data, rasterised when the holdings change and never per frame. */
  buildTerritory(model) {
    const S = model.systems, N = S.length; if (!N) return;
    const holderOf = s => (s.realm || '').startsWith('nation:') ? s.realm : (s.ownerId || '');
    const realmOf = s => s.realm || 'none';
    const key = N + '|' + model.playerHouse + '|' + S.map(s => [s.x | 0, s.y | 0, holderOf(s), realmOf(s), s.regionId, s.accent, s.realmColor].join(',')).join(';');
    if (this.terrKey === key && this.terr) { this.objects.add(this.terrMesh); return; }
    this.terrKey = key;
    // labels: holders (0 is nobody), realms (0 is the Reach), regions
    const holders = { '': 0 }, hk = ['']; const realms = { reach: 0 }, rk = ['reach']; const regions = {}; let nreg = 0;
    for (const s of S) { const h = holderOf(s); if (!(h in holders)) { holders[h] = hk.length; hk.push(h); } const r = realmOf(s); if (!(r in realms)) { realms[r] = rk.length; rk.push(r); } if (!(s.regionId in regions)) regions[s.regionId] = nreg++; }
    const sx = new Float64Array(N), sy = new Float64Array(N), sh = new Int32Array(N), sr = new Int32Array(N), sg = new Int32Array(N);
    S.forEach((s, i) => { sx[i] = s.x; sy[i] = -s.y; sh[i] = holders[holderOf(s)]; sr[i] = realms[realmOf(s)]; sg[i] = regions[s.regionId]; });
    // the field's reach follows how far apart the systems stand, so the small and the extra-large maps are drawn alike (R 205 at 205)
    let nn = 0; for (let i = 0; i < N; i++) { let b = 1e12; for (let j = 0; j < N; j++) if (j !== i) b = Math.min(b, (sx[i] - sx[j]) ** 2 + (sy[i] - sy[j]) ** 2); nn += Math.sqrt(b); }
    nn = N > 1 ? nn / N : 205; const R = Math.max(140, Math.min(340, nn)), T = 0.33, R2 = R * R;
    const pad = R * 2.2, x0 = this.bounds.minX - pad, y0 = this.bounds.minY - pad, spanX = this.bounds.maxX - this.bounds.minX + 2 * pad, spanY = this.bounds.maxY - this.bounds.minY + 2 * pad;
    const cell = Math.max(6, Math.max(spanX, spanY) / 1400), GW = Math.ceil(spanX / cell), GH = Math.ceil(spanY / cell), NP = GW * GH;
    const F = new Float32Array(NP), GX = new Float32Array(NP), GY = new Float32Array(NP);
    const cd = new Float32Array(NP * 4).fill(1e30), ci = new Float32Array(NP * 4).fill(-1);
    const patch = (i, rad, fn) => {
      const i0 = Math.max(0, Math.floor((sx[i] - rad - x0) / cell)), i1 = Math.min(GW - 1, Math.ceil((sx[i] + rad - x0) / cell));
      const j0 = Math.max(0, Math.floor((sy[i] - rad - y0) / cell)), j1 = Math.min(GH - 1, Math.ceil((sy[i] + rad - y0) / cell));
      for (let j = j0; j <= j1; j++) { const wy = y0 + (j + .5) * cell, dy = wy - sy[i]; for (let x = i0; x <= i1; x++) { const dx = x0 + (x + .5) * cell - sx[i]; fn(j * GW + x, dx, dy, dx * dx + dy * dy); } }
    };
    for (let i = 0; i < N; i++) patch(i, R * 3, (p, dx, dy, dd) => { const e = Math.exp(-dd / R2); F[p] += e; GX[p] += e * dx; GY[p] += e * dy; });
    const RC = Math.max(R * 2.6, nn * 2.6);
    for (let i = 0; i < N; i++) patch(i, RC, (p, dx, dy, dd) => {
      const o = p * 4; if (dd >= cd[o + 3]) return; let k = 3; while (k > 0 && cd[o + k - 1] > dd) { cd[o + k] = cd[o + k - 1]; ci[o + k] = ci[o + k - 1]; k--; } cd[o + k] = dd; ci[o + k] = i;
    });
    // the field's signed distance to its threshold, in world units, and its inward normal
    const E = new Float32Array(NP), fieldData = new Uint16Array(NP * 4), h16 = THREE.DataUtils.toHalfFloat;
    const regSum = new Float64Array(nreg * 3), realmSum = new Float64Array(rk.length * 3), landSum = [0, 0, 0];
    for (let p = 0; p < NP; p++) {
      const gx = -2 / R2 * GX[p], gy = -2 / R2 * GY[p], gl = Math.hypot(gx, gy); let e = (F[p] - T) / Math.max(gl, 1e-9); e = Math.max(-400, Math.min(400, e)); E[p] = e;
      fieldData[p * 4] = h16(e); fieldData[p * 4 + 1] = h16(gl > 1e-12 ? gx / gl : 0); fieldData[p * 4 + 2] = h16(gl > 1e-12 ? gy / gl : 0); fieldData[p * 4 + 3] = h16(1);
      if (e > 0 && ci[p * 4] >= 0) { const s = ci[p * 4], wx = x0 + (p % GW + .5) * cell, wy = y0 + (Math.floor(p / GW) + .5) * cell; const g = sg[s] * 3, r = sr[s] * 3; regSum[g] += wx; regSum[g + 1] += wy; regSum[g + 2]++; realmSum[r] += wx; realmSum[r + 1] += wy; realmSum[r + 2]++; landSum[0] += wx; landSum[1] += wy; landSum[2]++; }
    }
    const mkTex = (data, type, filter) => { const t = new THREE.DataTexture(data, GW, GH, THREE.RGBAFormat, type); t.minFilter = t.magFilter = filter; t.wrapS = t.wrapT = THREE.ClampToEdgeWrapping; t.generateMipmaps = false; t.needsUpdate = true; return t; };
    const candTex = mkTex(ci, THREE.FloatType, THREE.NearestFilter), fieldTex = mkTex(fieldData, THREE.HalfFloatType, THREE.LinearFilter);
    const sites = new Float32Array(N * 2 * 4); S.forEach((s, i) => { sites.set([sx[i], sy[i], sh[i], sr[i]], i * 4); sites.set([sg[i], s.isPlayer ? 1 : 0, 0, 0], (N + i) * 4); });
    const siteTex = new THREE.DataTexture(sites, N, 2, THREE.RGBAFormat, THREE.FloatType); siteTex.minFilter = siteTex.magFilter = THREE.NearestFilter; siteTex.needsUpdate = true;
    // the palette: a holder's fill and line by kind (0 nobody, 1 a house, 2 yours, 3 a nation), a realm's line and fill by kind (0 the Reach, 1 a nation, 2 a state, 3 nobody's)
    const PW = Math.max(hk.length, rk.length), pal = new Float32Array(PW * 4 * 4); this.holderCol = {};
    const any = k => S.find(s => holderOf(s) === k), anyR = k => S.find(s => realmOf(s) === k);
    hk.forEach((k, i) => {
      let kind = 0, fill = [0, 0, 0], line = [0, 0, 0];
      if (k) { const s = any(k); const col = rgb01(k.startsWith('nation:') ? s.realmColor : s.accent);
        if (k.startsWith('nation:')) { kind = 3; fill = tone(col, .55, .62); line = tone(col, .6, .85); }
        else if (k === model.playerHouse) { kind = 2; fill = rgb01('#E9B949'); line = rgb01('#F0C661'); }
        else { kind = 1; fill = tone(col, .8, .88); line = tone(col, .8, .9); } }
      this.holderCol[k] = { fill, line, kind };
      pal.set([...fill, kind], (0 * PW + i) * 4); pal.set([...line, 1], (1 * PW + i) * 4);
    });
    rk.forEach((k, i) => {
      let kind = k === 'reach' ? 0 : k.startsWith('nation:') ? 1 : k.startsWith('state:') ? 2 : 3; const s = anyR(k); const col = rgb01(s ? s.realmColor : '#c3cde2');
      const line = kind === 0 ? rgb01('#E3D3A4') : kind === 1 ? tone(col, .6, .85) : tone(col, .8, .9), fill = kind === 1 ? tone(col, .55, .62) : tone(col, .8, .88);
      pal.set([...line, kind], (2 * PW + i) * 4); pal.set([...fill, 1], (3 * PW + i) * 4);
    });
    const palTex = new THREE.DataTexture(pal, PW, 4, THREE.RGBAFormat, THREE.FloatType); palTex.minFilter = palTex.magFilter = THREE.NearestFilter; palTex.needsUpdate = true;
    if (this.terrMesh) { this.terrMesh.geometry.dispose(); for (const u of ['uCand', 'uField', 'uSites', 'uPal']) this.terrMesh.material.uniforms[u].value?.dispose?.(); this.terrMesh.material.dispose(); }
    const mat = new THREE.ShaderMaterial({ vertexShader: TERR_VS, fragmentShader: TERR_FS, transparent: true, premultipliedAlpha: true, depthWrite: false, depthTest: false,
      uniforms: { uCand: { value: candTex }, uField: { value: fieldTex }, uSites: { value: siteTex }, uPal: { value: palTex }, uBox: { value: new THREE.Vector4(x0, y0, GW * cell, GH * cell) }, uTier: { value: new THREE.Vector3(0, 1, 0) }, uDpr: { value: this.renderer.getPixelRatio() } } });
    mat.__shared = true;
    this.terrMesh = new THREE.Mesh(new THREE.PlaneGeometry(GW * cell, GH * cell), mat); this.terrMesh.position.set(x0 + GW * cell / 2, y0 + GH * cell / 2, 0); this.terrMesh.renderOrder = 1; this.terrMesh.frustumCulled = false;
    this.objects.add(this.terrMesh);
    const centre = (sum, i) => sum[i * 3 + 2] > 0 ? { x: sum[i * 3] / sum[i * 3 + 2], y: sum[i * 3 + 1] / sum[i * 3 + 2] } : null;
    this.terr = { x0, y0, cell, GW, GH, E, ci, sx, sy, sh, sr, sg, hk, rk, R, nn,
      regionAt: Object.fromEntries(Object.keys(regions).map(id => [id, centre(regSum, regions[id])])), realmAt: Object.fromEntries(rk.map((k, i) => [k, centre(realmSum, i)])),
      land: landSum[2] > 0 ? { x: landSum[0] / landSum[2], y: landSum[1] / landSum[2] } : { x: (this.bounds.minX + this.bounds.maxX) / 2, y: (this.bounds.minY + this.bounds.maxY) / 2 } };
    this.miniBase = null;
  }
  /** The field's signed distance at a point of the map (inside the land when positive). */
  eoutAt(x, y) {
    const t = this.terr; if (!t) return 1; const fx = (x - t.x0) / t.cell - .5, fy = (y - t.y0) / t.cell - .5; if (fx < 0 || fy < 0 || fx >= t.GW - 1 || fy >= t.GH - 1) return -100;
    const i = Math.floor(fx), j = Math.floor(fy), u = fx - i, v = fy - j, p = j * t.GW + i;
    return (t.E[p] * (1 - u) + t.E[p + 1] * u) * (1 - v) + (t.E[p + t.GW] * (1 - u) + t.E[p + t.GW + 1] * u) * v;
  }
  /** The nearest system to a point of the map, from the texel's candidates. */
  siteAt(x, y) {
    const t = this.terr; if (!t) return -1; const i = Math.max(0, Math.min(t.GW - 1, Math.floor((x - t.x0) / t.cell))), j = Math.max(0, Math.min(t.GH - 1, Math.floor((y - t.y0) / t.cell))), o = (j * t.GW + i) * 4;
    let best = -1, bd = Infinity; for (let k = 0; k < 4; k++) { const s = t.ci[o + k]; if (s < 0) continue; const d = (t.sx[s] - x) ** 2 + (t.sy[s] - y) ** 2; if (d < bd) { bd = d; best = s; } } return best;
  }

  buildLanes(model, byId) {
    const pa = [], pb = [], corner = [], col = [], wpx = [], idx = []; let v = 0;
    for (const l of model.lanes) {
      const a = byId[l.a], b = byId[l.b]; if (!a || !b) continue;
      // hairlines (7.3); a lane that touches your systems in gold, a front in ember, the lanes out of a picked fleet's system in brass
      const mine = a.isPlayer || b.isPlayer; let c = [168 / 255, 188 / 255, 222 / 255, .28], wd = 1;
      if (l.front) { c = [224 / 255, 85 / 255, 63 / 255, .72]; wd = 1.5; } else if (l.reach) { c = [246 / 255, 226 / 255, 160 / 255, .62]; wd = 1.3; } else if (mine) { c = [233 / 255, 185 / 255, 73 / 255, .6]; wd = 1.3; }
      for (const [cx, cy] of [[0, -1], [0, 1], [1, -1], [1, 1]]) { pa.push(a.x, -a.y, 0); pb.push(b.x, -b.y, 0); corner.push(cx, cy); col.push(...c); wpx.push(wd); }
      idx.push(v, v + 1, v + 2, v + 2, v + 1, v + 3); v += 4;
    }
    const g = new THREE.BufferGeometry(); g.setAttribute('position', new THREE.Float32BufferAttribute(pa, 3)); g.setAttribute('pb', new THREE.Float32BufferAttribute(pb, 3)); g.setAttribute('corner', new THREE.Float32BufferAttribute(corner, 2)); g.setAttribute('col', new THREE.Float32BufferAttribute(col, 4)); g.setAttribute('wpx', new THREE.Float32BufferAttribute(wpx, 1)); g.setIndex(idx);
    const pr = this.renderer.getPixelRatio();
    this.laneMat = new THREE.ShaderMaterial({ vertexShader: LANE_VS, fragmentShader: LANE_FS, transparent: true, premultipliedAlpha: true, depthWrite: false, depthTest: false, side: THREE.DoubleSide,
      uniforms: { uRes: { value: new THREE.Vector2(this.vw * pr, this.vh * pr) }, uDpr: { value: pr }, uGap: { value: 8 } } });
    const m = new THREE.Mesh(g, this.laneMat); m.frustumCulled = false; m.renderOrder = 2; this.objects.add(m);
  }

  buildStars(model) {
    const n = model.systems.length, pos = new Float32Array(n * 3), col = new Float32Array(n * 3), sz = new Float32Array(n * 2);
    model.systems.forEach((s, i) => {
      const h = hash(s.id), c = STAR[starClass(s.id)], big = Math.min(1, (s.pop || 0) / 40);
      pos.set([s.x, -s.y, 0], i * 3); col.set(c, i * 3);
      sz.set([8 + (h >> 4) % 3 + big * 2 + (s.isPlayer ? 1 : 0), 24 + (h >> 7) % 9 + big * 6], i * 2);   // an 8 to 12px core, a 24 to 38px halo
    });
    const g = new THREE.BufferGeometry(); g.setAttribute('position', new THREE.BufferAttribute(pos, 3)); g.setAttribute('color', new THREE.BufferAttribute(col, 3)); g.setAttribute('sz', new THREE.BufferAttribute(sz, 2));
    this.starMat = new THREE.ShaderMaterial({ vertexShader: STAR_VS, fragmentShader: STAR_FS, transparent: true, depthWrite: false, depthTest: false, blending: THREE.AdditiveBlending, uniforms: { uDpr: { value: this.renderer.getPixelRatio() }, uGrow: { value: 1 } } });
    const pts = new THREE.Points(g, this.starMat); pts.frustumCulled = false; this.glowObjects.add(pts);
  }

  /** What stands at each star, drawn in screen space so it is crisp at every zoom: the name, the marks, the crown, your seat. */
  buildPins(model, byId) {
    for (const s of model.systems) {
      const el = document.createElement('div'); const hc = this.holderCol?.[(s.realm || '').startsWith('nation:') ? s.realm : (s.ownerId || '')];
      el.className = 'sys-pin' + (s.isPlayer ? ' mine' : '') + (s.capital ? ' capital' : '') + ((s.realm || '').startsWith('nation:') ? ' foreign' : '');
      const under = !s.ownerId && !(s.realm || '').startsWith('nation:') ? '' : s.isPlayer ? '#e9b949' : hc ? css((s.realm || '').startsWith('nation:') ? hc.line : rgb01(s.accent)) : s.accent;
      const deps = (s.dep || []).map(d => DEPOSIT[d] ? ico(DEPOSIT[d][0], DEPOSIT[d][1], 'dep') : '').join('');
      let marks = '';
      if (s.id === model.seat) marks += '<i class="mk seat"></i><i class="mk streak"></i>';
      if (s.sunWorks) marks += '<i class="mk works"></i>';
      if (s.bloc) marks += `<i class="mk bloc ${s.bloc}"></i>`;
      if (s.chokepoint) marks += '<i class="mk ck"></i>';
      if (s.contested) marks += '<i class="mk cont"></i>';
      if (s.rebel) marks += '<i class="mk rebel"></i>';
      if (s.siege != null) marks += `<i class="mk siege" style="--p:${Math.max(.03, s.siege) * 360}deg"></i>`;
      if (s.conquered) marks += `<i class="mk conq ${s.conquered}" style="--hc:${esc(s.accent)}"></i>`;
      if (s.target) marks += `<i class="mk tgt ${s.target === 1 ? 'next' : ''}"></i><b class="jumps" title="">${s.target}</b>`;
      if (s.id === this.selected.systemId) marks += '<i class="mk sel"></i>';
      const stn = (s.stations || 0) + (s.deepWorks || 0); if (stn) marks += `<span class="stn">${'<i></i>'.repeat(Math.min(4, s.stations || 0))}${'<i class="w"></i>'.repeat(Math.min(3, s.deepWorks || 0))}</span>`;
      el.innerHTML = marks + (s.capital ? `<span class="cap">${ico('crown', 'brass')}</span>` : '')
        + `<div class="sys-label${s.isPlayer ? ' mine' : ''}"><span class="nm" style="--hc:${under || 'transparent'}">${esc(s.name)}</span>${deps}</div>`;
      const lab = new CSS2DObject(el); lab.position.set(s.x, -s.y, 0); this.objects.add(lab); this.labels.push(lab);
      this.pins.push({ s, el, lab, name: el.querySelector('.sys-label') });
    }
  }

  /** Fleets (7.3): a chevron in the owner's colour with a dark outline and a light facet, and a number plate; a fleet only reported is hollow, with "?". */
  buildFleets(model, byId) {
    const at = {}; for (const f of model.fleets) (at[f.systemId] ||= []).push(f);
    const spots = [[13, -33], [-45, -33], [13, -58], [-45, -58], [22, 6], [-54, 6]];
    for (const [sid, list] of Object.entries(at)) {
      const s = byId[sid]; if (!s) continue;
      list.sort((a, b) => (b.isPlayer ? 1 : 0) - (a.isPlayer ? 1 : 0) || b.strength - a.strength);
      const el = document.createElement('div'); el.className = 'fleet-pin';
      list.slice(0, spots.length).forEach((f, k) => {
        const pirate = f.ownerId === 'pirates', known = f.isPlayer || (f.confidence || 0) >= 100;
        const t = document.createElement('div'); t.className = 'fl-tok' + (f.isPlayer ? ' mine' : '') + (known ? '' : ' reported') + (pirate ? ' raid' : '') + (f.moving ? ' moving' : '') + (f.id === this.selected.fleetId ? ' sel' : '');
        t.style.left = spots[k][0] + 'px'; t.style.top = spots[k][1] + 'px'; t.style.setProperty('--fc', f.isPlayer ? '#e9b949' : pirate ? '#a3324a' : f.accent);
        t.innerHTML = `<svg viewBox="0 0 22 22" aria-hidden="true"><path class="b" d="M11 2.2 L20 19 L11 14.6 L2 19 Z"/><path class="f" d="M11 2.2 L11 14.6 L2 19 Z"/></svg><b>${f.strength}${known ? '' : '?'}</b>`;
        el.appendChild(t); this.tokens.push({ f, el: t, sx: s.x, sy: -s.y, dx: spots[k][0], dy: spots[k][1] });
      });
      const lab = new CSS2DObject(el); lab.position.set(s.x, -s.y, 0); this.objects.add(lab); this.labels.push(lab);
    }
  }

  /** The names of the regions (the middle distance) and of the realms (from far off), placed later by a search round their centres. */
  buildNames(model) {
    const t = this.terr; if (!t) return;
    for (const r of model.regions || []) {
      const c = t.regionAt[r.id] || { x: r.cx, y: -r.cy };
      const el = document.createElement('div'); el.className = 'region-pin'; el.innerHTML = `<div class="region-label${r.ring === 'beyond' ? ' beyond' : ''}${r.free || r.state ? ' free' : ''}">${esc(r.name)}${r.state ? `<span class="rs state">${esc(r.state)}${r.stateWar ? ', at war for it' : ', no longer of the Reach'}</span>` : ''}</div>`;
      const lab = new CSS2DObject(el); lab.position.set(c.x, c.y, 0); this.objects.add(lab); this.labels.push(lab); this.regionPins.push(this.keepPlace({ el, inner: el.firstChild, lab, x: c.x, y: c.y, key: 'r:' + r.id }));
    }
    const realms = {}; for (const s of model.systems) { const k = s.realm || ''; if (!k || k === 'none') continue; const z = realms[k] ||= { name: k === 'reach' ? (model.galaxyName || 'The Reach') : (s.polityName || s.ownerName || ''), color: s.realmColor, lands: new Set(), n: 0 }; z.n++; const rg = (model.regions || []).find(r => r.id === s.regionId); if (rg) z.lands.add(rg.name); }
    for (const [k, z] of Object.entries(realms)) {
      const c = t.realmAt[k]; if (!c || !z.name) continue;
      const lands = [...z.lands].filter(n => n.toLowerCase() !== z.name.toLowerCase()); const sub = k === 'reach' ? '' : lands.slice(0, 2).join(' · ');
      const words = z.name.split(/\s+/), shown = words.length > 4 && /^the$/i.test(words[0]) ? words.slice(1).join(' ') : z.name;   // never more than four words in spaced capitals
      const el = document.createElement('div'); el.className = 'realm-pin'; el.innerHTML = `<div class="realm-label${k === 'reach' ? ' reach' : ''}" style="--rc:${esc(z.color || '#c3cde2')}" title="${esc(z.name)}"><span class="rn">${esc(shown)}</span>${sub ? `<span class="rs">${esc(sub)}</span>` : ''}</div>`;
      const lab = new CSS2DObject(el); lab.position.set(c.x, c.y, 0); this.objects.add(lab); this.labels.push(lab); this.realmPins.push(this.keepPlace({ el, inner: el.firstChild, lab, x: c.x, y: c.y, big: z.n, key: 'm:' + k }));
    }
  }

  /** A region's or a realm's name is hidden until the search has placed it; across a new model it keeps where it was. */
  keepPlace(pin) { const k = (this.places ||= {})[pin.key]; if (k) { pin.inner.style.setProperty('--dx', k.dx + 'px'); pin.inner.style.setProperty('--dy', k.dy + 'px'); } else pin.inner.style.visibility = 'hidden'; return pin; }
  /** Close to, a system is its worlds on their orbits: put away until the systems are what is looked at. */
  buildDetails(model) {
    this.details = [];
    for (const s of model.systems) {
      const g = new THREE.Group(); g.position.set(s.x, -s.y, 0); g.visible = false; const h = hash(s.id);
      const bodies = (s.bodies && s.bodies.length) ? s.bodies : [{ name: s.name, cls: s.planetClass, size: 3, primary: true }];
      const orbitMat = new THREE.LineBasicMaterial({ color: 0xc9a24a, transparent: true, opacity: .1 });
      let planet = null; const pr = 5.5 + Math.min(4, (s.pop || 0) / 15);
      const at = {}; let lane = 0; bodies.forEach((b, bi) => { if (b.orbits == null || b.orbits < 0) { const orb = 16 + lane * 10.5, ang = b.ang != null ? b.ang : ((h >> (lane * 3)) % 628) / 100 + lane * 1.7; at[bi] = { orb, x: Math.cos(ang) * orb, y: Math.sin(ang) * orb * .78 }; lane++; } });
      const moonsOf = {};
      const built = (x, y, z) => { const lamp = new THREE.Sprite(new THREE.SpriteMaterial({ map: TEX.glow, color: 0xffd36a, transparent: true, opacity: .95, blending: THREE.AdditiveBlending, depthWrite: false })); lamp.scale.set(3.4, 3.4, 1); lamp.position.set(x, y, z); g.add(lamp); };
      bodies.forEach((b, bi) => {
        if (b.orbits != null && b.orbits >= 0 && at[b.orbits]) {
          const k = moonsOf[b.orbits] = (moonsOf[b.orbits] || 0) + 1, pa = at[b.orbits], ma = ((h >> (bi + 2)) % 628) / 100 + k * 2.3, mr = 5.2 + k * 2.4, mx = pa.x + Math.cos(ma) * mr, my = pa.y + Math.sin(ma) * mr * .78;
          const mtex = window.praetoriaFx ? new THREE.TextureLoader().load(window.praetoriaFx.planet('moon', s.id + b.name, 48)) : null; if (mtex) mtex.colorSpace = THREE.SRGBColorSpace;
          const ms = new THREE.Sprite(new THREE.SpriteMaterial({ map: mtex, color: mtex ? 0xffffff : 0xb0b0ba, transparent: true, depthWrite: false })); ms.scale.set(2.4, 2.4, 1); ms.position.set(mx, my, 4.5); g.add(ms);
          if (b.works) built(mx + 1.6, my + 1.6, 6); return;
        }
        const here = at[bi] || { orb: 16, x: 16, y: 0 }; const orb = here.orb, bx = here.x, by = here.y;
        g.add(new THREE.Line(new THREE.BufferGeometry().setFromPoints(Array.from({ length: 65 }, (_, i) => { const a = i / 64 * Math.PI * 2; return new THREE.Vector3(Math.cos(a) * orb, Math.sin(a) * orb * .78, 1); })), orbitMat));
        if (b.works) built(bx + (b.primary ? pr * .8 : 3), by + (b.primary ? pr * .8 : 3), 7);
        if (b.primary) {
          const ptex = window.praetoriaFx ? new THREE.TextureLoader().load(window.praetoriaFx.planet(b.cls, s.id, 128)) : null; if (ptex) ptex.colorSpace = THREE.SRGBColorSpace;
          planet = new THREE.Mesh(new THREE.SphereGeometry(pr, 32, 24), new THREE.MeshStandardMaterial({ color: ptex ? 0xffffff : planetColor(b.cls), map: ptex, roughness: 0.8, metalness: 0.0, emissive: new THREE.Color(planetColor(b.cls)).multiplyScalar(0.12) }));
          planet.position.set(bx, by, 5); planet.rotation.x = 0.4; g.add(planet); planet.userData.spin = 0.15 + (h % 7) * 0.02;
        } else if (b.cls === 'belt') {
          const pts = []; let rs = h + bi; const rr = () => { rs = (rs * 1664525 + 1013904223) >>> 0; return rs / 4294967296; }; for (let q = 0; q < 70; q++) { const a = rr() * Math.PI * 2, r2 = orb + (rr() - .5) * 5; pts.push(Math.cos(a) * r2, Math.sin(a) * r2 * .78, 1 + rr()); }
          const geo = new THREE.BufferGeometry(); geo.setAttribute('position', new THREE.Float32BufferAttribute(pts, 3)); g.add(new THREE.Points(geo, new THREE.PointsMaterial({ color: 0xc9bca8, size: 1.3, transparent: true, opacity: .85 })));
        } else {
          const tex = window.praetoriaFx ? new THREE.TextureLoader().load(window.praetoriaFx.planet(b.cls, s.id + b.name, 64)) : null; if (tex) tex.colorSpace = THREE.SRGBColorSpace;
          const sp = new THREE.Sprite(new THREE.SpriteMaterial({ map: tex, color: tex ? 0xffffff : planetColor(b.cls), transparent: true, depthWrite: false })); const bs = b.cls === 'comet' ? 2.6 : b.size === 3 ? 7.5 : b.size === 2 ? 5 : 3.2; sp.scale.set(bs, bs, 1); sp.position.set(bx, by, 4); g.add(sp);
        }
      });
      if (planet && s.planetClass === 'gas') { const ring = new THREE.Mesh(new THREE.RingGeometry(pr * 1.35, pr * 1.9, 48), new THREE.MeshBasicMaterial({ color: 0xf0d0a0, transparent: true, opacity: 0.5, side: THREE.DoubleSide })); ring.position.copy(planet.position); ring.rotation.x = 1.2; g.add(ring); }
      if (planet) { const atm = new THREE.Mesh(new THREE.SphereGeometry(pr * 1.12, 24, 18), new THREE.MeshBasicMaterial({ color: planetGlow(s.planetClass), transparent: true, opacity: 0.18, blending: THREE.AdditiveBlending, side: THREE.BackSide, depthWrite: false })); atm.position.copy(planet.position); g.add(atm); }
      g.renderOrder = 4; this.objects.add(g); this.details.push(g);
    }
  }

  // ---------------- the camera (7.1) ----------------
  /** The free map, in the canvas's pixels: between the rail (or the open window) and the outliner, under the top bar. */
  freeRect() {
    const host = this.renderer.domElement.getBoundingClientRect(), W = host.width || this.vw || 800, H = host.height || this.vh || 500;
    let c = this.cover; if (!c) { try { c = window.praetoriaShell?.covered?.(); } catch { c = null; } }
    let x0 = 0, x1 = W, y0 = 0; const y1 = H; const ok = r => r && r.w > 0 && r.h > 0;
    if (c) {
      if (ok(c.rail)) x0 = Math.max(x0, c.rail.x + c.rail.w - host.left);
      if (ok(c.window)) x0 = Math.max(x0, c.window.x + c.window.w - host.left);
      if (ok(c.outliner)) x1 = Math.min(x1, c.outliner.x - host.left - 8); else if (ok(c.console)) x1 = Math.min(x1, c.console.x - host.left - 8);
      if (ok(c.topbar)) y0 = Math.max(y0, c.topbar.y + c.topbar.h - host.top);
    }
    if (x1 - x0 < 160) { x0 = Math.max(0, x1 - 160); }
    return { x0, x1, y0, y1, W, H };
  }
  placeCam(cam, x, y, d, vo) {
    const tilt = this.tilt * (0.5 + 0.5 * Math.min(1, d / 1200));
    cam.aspect = this.vw / this.vh; cam.position.set(x, y - d * Math.sin(tilt), d * Math.cos(tilt)); cam.lookAt(x, y, 0);
    cam.setViewOffset(this.vw, this.vh, this.vw / 2 - vo.x, this.vh / 2 - vo.y, this.vw, this.vh); cam.updateMatrixWorld();
  }
  /** Where the look-at point stands on the screen: the middle of the free map. */
  principal() { const f = this.freeRect(); return { x: (f.x0 + f.x1) / 2, y: (f.y0 + f.y1) / 2 }; }
  updateCamera(immediate) {
    this.hideTip();
    if (immediate || !this.cur) { this.cur = { x: this.target.x, y: this.target.y, d: this.distance }; this.vo = this.principal(); }
    this.applyCamera(0);
  }
  /** The camera a step of k toward where it is going (the loop steps it by the time that has passed, so a slow machine is not slow to settle). */
  applyCamera(k = 0) {
    const c = this.cur || (this.cur = { x: this.target.x, y: this.target.y, d: this.distance }), p = this.principal(); if (!this.vo) this.vo = { ...p };
    const ease = (a, b, snap) => { const v = a + (b - a) * k; return Math.abs(b - v) < snap ? b : v; };
    const was = c.x + ',' + c.y + ',' + c.d + ',' + this.vo.x + ',' + this.vo.y;
    c.x = ease(c.x, this.target.x, .4); c.y = ease(c.y, this.target.y, .4); c.d = ease(c.d, this.distance, .4); this.vo.x = ease(this.vo.x, p.x, .3); this.vo.y = ease(this.vo.y, p.y, .3);
    this.placeCam(this.camera, c.x, c.y, c.d, this.vo); if (k === 0 || was !== c.x + ',' + c.y + ',' + c.d + ',' + this.vo.x + ',' + this.vo.y) this.dirty = true;
  }
  settled() { const c = this.cur, p = this.principal(); return c && c.x === this.target.x && c.y === this.target.y && c.d === this.distance && this.vo && this.vo.x === p.x && this.vo.y === p.y; }
  /** How much of the free map would be empty space past the edge of the land, with the camera at a target and distance. */
  emptyAt(x, y, d) {
    if (!this.terr) return 0;
    const f = this.freeRect(); this.placeCam(this.probe, x, y, d, { x: (f.x0 + f.x1) / 2, y: (f.y0 + f.y1) / 2 });
    const plane = new THREE.Plane(new THREE.Vector3(0, 0, 1), 0), pt = new THREE.Vector3(); let n = 0, empty = 0;
    const top = f.y0 + 56;   // the strip under the bar holds the alert badges (7.1): it is meant to be open space
    for (let i = 0; i < 24; i++) for (let j = 0; j < 16; j++) {
      const sx = f.x0 + (i + .5) / 24 * (f.x1 - f.x0), sy = top + (j + .5) / 16 * (f.y1 - top);
      this.mouse.set(sx / f.W * 2 - 1, -(sy / f.H) * 2 + 1); this.raycaster.setFromCamera(this.mouse, this.probe);
      if (!this.raycaster.ray.intersectPlane(plane, pt)) { empty++; n++; continue; }
      n++; if (this.eoutAt(pt.x, pt.y) <= 0) empty++;
    }
    return n ? empty / n : 0;
  }
  /** A target moved, if it must be, the least way toward the land so the empty space stays under a fifth of the free map; from far off, only kept over the map. */
  clampTarget(x, y, d, limit = MAX_EMPTY) {
    const b = this.bounds; if (!b || !this.terr || this.sys) return { x, y };
    const inBounds = (px, py) => ({ x: Math.max(b.minX, Math.min(b.maxX, px)), y: Math.max(b.minY, Math.min(b.maxY, py)) });
    if (d > 1750) return inBounds(x, y);
    if (this.emptyAt(x, y, d) <= limit) return { x, y };
    const c = this.terr.land, best = { x, y, e: this.emptyAt(x, y, d), m: Infinity };
    for (const [tx, ty] of [[x, c.y], [c.x, y], [c.x, c.y]]) {
      if (this.emptyAt(tx, ty, d) > limit) { for (let k = 1; k <= 6; k++) { const m = k / 6, px = x + (tx - x) * m, py = y + (ty - y) * m, e = this.emptyAt(px, py, d); if (e < best.e - 1e-6 && best.m === Infinity) { best.x = px; best.y = py; best.e = e; } } continue; }
      let lo = 0, hi = 1; for (let i = 0; i < 9; i++) { const m = (lo + hi) / 2; if (this.emptyAt(x + (tx - x) * m, y + (ty - y) * m, d) <= limit) hi = m; else lo = m; }
      const px = x + (tx - x) * hi, py = y + (ty - y) * hi, m = Math.hypot(px - x, py - y); if (m < best.m) { best.x = px; best.y = py; best.m = m; best.e = limit; }
    }
    return inBounds(best.x, best.y);
  }
  /** A pan, held back where it would open more empty space than is allowed. */
  panTo(nx, ny) {
    if (this.sys || !this.terr) { this.target.x = nx; this.target.y = ny; this.updateCamera(true); return; }
    const d = this.distance, b = this.bounds;
    if (d > 1750) { const t = this.clampTarget(nx, ny, d); this.target.x = t.x; this.target.y = t.y; this.updateCamera(true); return; }
    const limit = Math.max(MAX_EMPTY, this.emptyAt(this.target.x, this.target.y, d)) + 0.003;
    for (const [px, py] of [[nx, ny], [nx, this.target.y], [this.target.x, ny]]) { if (px < b.minX - 400 || px > b.maxX + 400 || py < b.minY - 400 || py > b.maxY + 400) continue; if (this.emptyAt(px, py, d) <= limit) { this.target.x = px; this.target.y = py; break; } }
    this.updateCamera(true);
  }
  fitDistance() {
    const b = this.bounds; if (!b) return 4000; const f = this.freeRect(), tanH = Math.tan(THREE.MathUtils.degToRad(this.camera.fov / 2));
    const w = Math.max(240, f.x1 - f.x0), h = Math.max(240, f.y1 - f.y0), H = f.H;
    return Math.max((b.maxX - b.minX + 360) * H / (w * 2 * tanH), (b.maxY - b.minY + 360) * 1.25 * H / (h * 2 * tanH));
  }
  zoom(f, px, py) {
    const before = px != null ? this.planePoint(px, py) : null;
    if (this.sys) {
      // close to: no nearer than a world fills the view, and pulling back past the lanes leaves the system for the Reach
      const far = (this.sysFitD || this.sys.edge * 3) * 1.45; if (f > 1 && this.distance * f > far) { this.dotnet?.invokeMethodAsync('OnPick', 'leave', ''); return; }
      this.distance = Math.max(60, Math.min(far, this.distance * f)); this.updateCamera();
    } else {
      // pushing in past the nearest the Reach is looked at from goes into the system in front of the eye
      if (f < 1 && this.distance <= 161 && this.model) { const near = this.model.systems.map(s => ({ s, d: Math.hypot(s.x - this.target.x, -s.y - this.target.y) })).sort((a, b) => a.d - b.d)[0]; if (near && near.d < 110) { this.dotnet?.invokeMethodAsync('OnPick', 'enter', near.s.id); return; } }
      this.distance = Math.max(160, Math.min(Math.max(2400, this.fitDistance() * 1.3), this.distance * f)); this.updateCamera();
    }
    if (before) { const after = this.planePoint(px, py); if (after) { this.target.x += before.x - after.x; this.target.y += before.y - after.y; } }
    if (!this.sys) { const t = this.clampTarget(this.target.x, this.target.y, this.distance); this.target.x = t.x; this.target.y = t.y; }
    this.updateCamera(); this.keepZoom = performance.now();
  }
  /** Your seat, at the opening distance. */
  home() { if (this.sys) { this.fit(); return; } if (!this.seatAt) return; this.distance = Math.min(HOME_D, this.fitDistance()); const t = this.clampTarget(this.seatAt.x, this.seatAt.y, this.distance); this.target.set(t.x, t.y, 0); this.updateCamera(); this.keepZoom = performance.now(); }
  /** The whole map in the free part of the screen. */
  fit() { if (this.sys) { this.focused = null; this.sysFitD = this.sysFit(); return; } const b = this.bounds; if (!b) return; this.target.set((b.minX + b.maxX) / 2, (b.minY + b.maxY) / 2, 0); this.distance = this.fitDistance(); this.updateCamera(); }
  focus(x, y) { this.distance = Math.min(this.distance, 1200); const t = this.clampTarget(x, -y, this.distance, 0.32); this.target.set(t.x, t.y, 0); this.updateCamera(); }
  /** The interface's rectangles (js/shell.js): the map re-offsets to the free part of the screen and keeps its names out from under them. */
  setCover(c) { const was = JSON.stringify([this.cover?.window, this.cover?.outliner, this.cover?.rail, this.cover?.topbar]); this.cover = c; this.relabel = true; this.hideTip(); if (this.model && !this.sys && was !== JSON.stringify([c?.window, c?.outliner, c?.rail, c?.topbar])) { const t = this.clampTarget(this.target.x, this.target.y, this.distance); this.target.x = t.x; this.target.y = t.y; } }
  planePoint(px, py) {
    const rect = this.renderer.domElement.getBoundingClientRect(); this.mouse.set(((px - rect.left) / rect.width) * 2 - 1, -((py - rect.top) / rect.height) * 2 + 1);
    this.raycaster.setFromCamera(this.mouse, this.camera); const pt = new THREE.Vector3(); return this.raycaster.ray.intersectPlane(new THREE.Plane(new THREE.Vector3(0, 0, 1), 0), pt) ? pt : null;
  }
  /** A point of the map on the screen, in client pixels. */
  screenOf(x, y) { const r = this.renderer.domElement.getBoundingClientRect(), v = new THREE.Vector3(x, y, 0).project(this.camera); return { x: r.left + (v.x + 1) / 2 * r.width, y: r.top + (1 - v.y) / 2 * r.height, z: v.z }; }

  // ---------------- input ----------------
  bindInput() {
    const el = this.renderer.domElement; let down = null, moved = false;
    el.addEventListener('pointerdown', e => { down = { x: e.clientX, y: e.clientY, pt: this.planePoint(e.clientX, e.clientY) }; moved = false; el.setPointerCapture(e.pointerId); });
    el.addEventListener('pointermove', e => {
      if (down) { if (Math.abs(e.clientX - down.x) + Math.abs(e.clientY - down.y) > 3) moved = true; const pt = this.planePoint(e.clientX, e.clientY); if (pt && down.pt && moved) { this.panTo(this.target.x - (pt.x - down.pt.x), this.target.y - (pt.y - down.pt.y)); } el.style.cursor = 'grabbing'; return; }
      const hit = this.pick(e.clientX, e.clientY); el.style.cursor = hit ? 'pointer' : 'grab'; this.showTip(hit);
    });
    el.addEventListener('pointerleave', () => { if (!down) this.showTip(null); });   // onto a window, the bar or the list: the note does not stay behind
    el.addEventListener('pointerup', e => { if (down && !moved) { const hit = this.pick(e.clientX, e.clientY); if (hit) this.dotnet?.invokeMethodAsync('OnPick', hit.kind, hit.id); else this.dotnet?.invokeMethodAsync('OnPick', 'none', ''); } down = null; el.style.cursor = 'grab'; });
    el.addEventListener('wheel', e => { e.preventDefault(); this.zoom(e.deltaY > 0 ? 1.15 : 0.87, e.clientX, e.clientY); }, { passive: false });
    // twice on a system goes into it; twice on a world, once inside, goes to the world
    el.addEventListener('dblclick', e => { const hit = this.pick(e.clientX, e.clientY); if (!hit) return; if (!this.sys && hit.kind === 'system') this.dotnet?.invokeMethodAsync('OnPick', 'enter', hit.id); else if (this.sys && hit.kind === 'body') this.dotnet?.invokeMethodAsync('OnPick', 'world', hit.id); });
  }
  /** What is under the pointer: inside a system by its shapes; on the map of the Reach in screen space, a fleet's token first, then the nearest star within reach. */
  pick(px, py) {
    if (this.sys) { const rect = this.renderer.domElement.getBoundingClientRect(); this.mouse.set(((px - rect.left) / rect.width) * 2 - 1, -((py - rect.top) / rect.height) * 2 + 1); this.raycaster.setFromCamera(this.mouse, this.camera); const hits = this.raycaster.intersectObjects(this.sysHits, false); return hits.length ? hits[0].object.userData : null; }
    if (!this.model) return null; const far = this.tier === 'far';
    for (const t of this.tokens || []) { if (far && !t.f.isPlayer) continue; const p = this.screenOf(t.sx, t.sy); const x = p.x + t.dx, y = p.y + t.dy; if (px >= x - 2 && px <= x + 40 && py >= y - 2 && py <= y + 24) return { kind: 'fleet', id: t.f.id, systemId: t.f.systemId }; }
    let best = null, bd = 18 * 18;
    for (const s of this.model.systems) { const p = this.screenOf(s.x, -s.y); const d = (p.x - px) ** 2 + (p.y - py) ** 2; if (d < bd) { bd = d; best = s; } }
    return best ? { kind: 'system', id: best.id } : null;
  }
  hideTip() { if (this.tipEl) this.tipEl.style.display = 'none'; if (this.sysTip) { this.sysTip.visible = false; this.sysTip.element.style.display = 'none'; } }
  /** The note on what is under the pointer (7.4): a system's globe, name, kind and region, who holds it and what is in it; a fleet's strength. */
  showTip(hit) {
    // inside a system, the close map's own note
    if (this.sys) { const t = this.sysTip; if (!t) return; if (!hit || !hit.tip) { t.visible = false; t.element.style.display = 'none'; return; } t.visible = true; t.element.innerHTML = hit.tip; t.position.set(hit.at[0], hit.at[1] + (hit.up || 16), 20); return; }
    const el = this.tipEl; if (!el || !this.model) return;
    if (!hit) { el.style.display = 'none'; this.tipFor = null; return; }
    const key = hit.kind + ':' + hit.id; let ax, ay;
    if (hit.kind === 'system') {
      const s = this.byId[hit.id]; if (!s) return; const p = this.screenOf(s.x, -s.y); ax = p.x; ay = p.y;
      if (this.tipFor !== key) {
        const hc = this.holderCol?.[(s.realm || '').startsWith('nation:') ? s.realm : (s.ownerId || '')]; const sw = s.isPlayer ? '#e9b949' : hc && hc.kind ? css(hc.line) : 'transparent';
        const kind = [sentence(s.industry || (s.planetClass + ' world')), s.regionName].filter(Boolean).join(' · ') + (s.capital ? ' · the capital' : '');
        const row = (icon, t, k, v) => `${ico(icon, t)}<span class="k">${k}</span><span class="v">${v}</span>`;
        let kv = row('flying-flag', 'ivory', 'Held by', s.ownerName ? `<i class="own" style="background:${sw}"></i><b>${esc(s.ownerName)}</b>` : 'No one');
        if (s.governor) kv += row('office-chair', 'ivory', 'Governor', esc(s.governor));
        if (s.citizens) kv += row('population', 'ivory', 'Citizens', `<b>${esc(s.citizens)}</b>`);
        if (s.pop) kv += row('loyalty', 'good', 'Loyalty', `<b>${100 - (s.unrest || 0)}</b>`);
        if (s.depNames && s.depNames.length) kv += row(DEPOSIT[s.dep[0]]?.[0] || 'rare_earths', DEPOSIT[s.dep[0]]?.[1] || 'steel', 'Deposits', esc(s.depNames.join(', ')));
        const nb = s.bodyCount || (s.bodies || []).length; if (nb) kv += row('ringed-planet', 'ivory', 'Bodies', `${nb}${s.greatCity ? `, with the great city of ${esc(s.greatCity)}` : ''}`);
        else if (s.greatCity) kv += row('crown', 'brass', 'Great city', esc(s.greatCity));
        if (s.target) kv += row('fleet', 'brass', 'From your fleet', `${s.target} ${s.target === 1 ? 'jump' : 'jumps'}`);
        el.innerHTML = `<div class="hd">${globe(s.planetClass, 30)}<div><div class="nm">${esc(s.name)}</div><div class="sb">${esc(kind)}</div></div></div><div class="kv">${kv}</div><div class="foot">Click to open the system · twice to go into it</div><i class="arr"></i>`;
      }
    } else {
      const f = this.model.fleets.find(x => x.id === hit.id), s = f && this.byId[f.systemId]; if (!f || !s) return; const tk = this.tokens.find(t => t.f.id === f.id); const p = this.screenOf(s.x, -s.y); ax = p.x + (tk ? tk.dx + 11 : 0); ay = p.y + (tk ? tk.dy + 11 : 0);
      if (this.tipFor !== key) {
        const row = (icon, t, k, v) => `${ico(icon, t)}<span class="k">${k}</span><span class="v">${v}</span>`; const dest = f.moving && this.byId[f.destId] ? this.byId[f.destId].name : '';
        let kv = row('flying-flag', 'ivory', 'Of', `<i class="own" style="background:${f.isPlayer ? '#e9b949' : esc(f.accent)}"></i><b>${esc(f.ownerName || '')}</b>`) + row('fleet', 'ivory', 'Hulls', `<b>${f.strength}</b>`);
        kv += f.isPlayer ? row('rally-the-troops', 'ivory', 'Readiness', `<b>${f.readiness}</b>`) : row('spyglass', 'steel', 'Intelligence', `${f.confidence}% sure`);
        if (dest) kv += row('compass', 'ivory', 'Bound for', `${esc(dest)}${f.eta ? `, ${f.eta} ${f.eta === 1 ? 'year' : 'years'} off` : ''}`);
        el.innerHTML = `<div class="hd"><span class="chev" style="--fc:${f.isPlayer ? '#e9b949' : esc(f.accent)}"><svg viewBox="0 0 22 22"><path d="M11 2.2 L20 19 L11 14.6 L2 19 Z"/></svg></span><div><div class="nm">${esc(f.name)}</div><div class="sb">${f.isPlayer ? 'Your fleet' : 'Reported'} · at ${esc(s.name)}</div></div></div><div class="kv">${kv}</div><div class="foot">${f.isPlayer ? 'Click to pick it, then a world to see the way there' : 'Click to see the system it is in'}</div><i class="arr"></i>`;
      }
    }
    this.tipFor = key; el.style.display = 'block'; el.classList.add('on');
    // beside the star, on the side with room, within the free map
    const host = this.renderer.domElement.getBoundingClientRect(), f = this.freeRect(), L = host.left + f.x0 + 8, R = host.left + f.x1 - 8, T = host.top + f.y0 + 8, B = host.top + f.y1 - 8;
    const w = el.offsetWidth, h = el.offsetHeight; let side = 'right', x = ax + 26; if (x + w > R) { side = 'left'; x = ax - 26 - w; } if (x < L) x = Math.max(L, Math.min(R - w, x));
    const y = Math.max(T, Math.min(B - h, ay - h * 0.32)); el.dataset.side = side; el.style.left = x + 'px'; el.style.top = y + 'px';
    const arr = el.querySelector('.arr'); if (arr) arr.style.top = Math.max(10, Math.min(h - 22, ay - y - 6)) + 'px';
  }
  resize() {
    const w = this.container.clientWidth, h = this.container.clientHeight; if (!w || !h) return; this.vw = w; this.vh = h;
    this.renderer.setSize(w, h); this.labelRenderer.setSize(w, h); this.composer.setSize(w, h); this.glowComposer.setSize(w, h); this.camera.aspect = w / h;
    const pr = this.renderer.getPixelRatio(); if (this.laneMat) this.laneMat.uniforms.uRes.value.set(w * pr, h * pr);
    this.applyCamera(); this.dirty = true; this.relabel = true; this.miniBase = null;
  }

  // ---------------- names that are never written over anything (rule 5, 7.3) ----------------
  /** The rectangles no name may sit under: the interface (js/shell.js tells them) and the map's own furniture. */
  obstacles() {
    const out = []; const add = r => { if (r && r.w > 0 && r.h > 0) out.push({ x: r.x, y: r.y, w: r.w, h: r.h }); };
    const c = this.cover || window.praetoriaShell?.covered?.(); if (c) for (const k of Object.keys(c)) add(c[k]);
    for (const e of document.querySelectorAll('.gs-shell.game .endturn-dock, .galaxy .order-bar, .galaxy .crumbs, .galaxy .world-strip, .galaxy .legend, .galaxy .map-side:not(.idle)')) { if (getComputedStyle(e).display === 'none') continue; const r = e.getBoundingClientRect(); add({ x: r.left, y: r.top, w: r.width, h: r.height }); }
    return out;
  }
  declutter() {
    if (!this.model || this.sys) return;
    const hits = (a, b, pad) => a.x < b.x + b.w + pad && b.x < a.x + a.w + pad && a.y < b.y + b.h + pad && b.y < a.y + a.h + pad;
    const host = this.renderer.domElement.getBoundingClientRect(), f = this.freeRect(), view = { x: host.left + f.x0, y: host.top + f.y0, w: f.x1 - f.x0, h: f.y1 - f.y0 };
    const inView = (b, m = 2) => b.x >= view.x + m && b.y >= view.y + m && b.x + b.w <= view.x + view.w - m && b.y + b.h <= view.y + view.h - m;
    const ui = this.obstacles(), tier = this.tier;
    const rect = e => { const r = e.getBoundingClientRect(); return { x: r.left, y: r.top, w: r.width, h: r.height }; };
    // where the stars are on the screen
    const stars = []; for (const p of this.pins) { const q = this.screenOf(p.s.x, -p.s.y); p.at = q; if (q.z < 1 && q.x > host.left - 20 && q.x < host.right + 20 && q.y > host.top - 20 && q.y < host.bottom + 20) { stars.push({ x: q.x - 12, y: q.y - 12, w: 24, h: 24, s: p.s }); if (p.s.capital) stars.push({ x: q.x - 9, y: q.y - 32, w: 18, h: 20, s: p.s }); } }
    const placed = [];
    // fleets first: they stand beside their stars
    for (const t of this.tokens) { const b = rect(t.el); const show = b.w > 0 && inView(b) && !ui.some(u => hits(b, u, 4)); t.el.style.visibility = show ? '' : 'hidden'; if (show) placed.push(b); }
    // the names of systems: yours first, the capital, then the most peopled
    const order = this.pins.slice().sort((a, b) => (b.s.isPlayer ? 4 : 0) + (b.s.capital ? 2 : 0) - (a.s.isPlayer ? 4 : 0) - (a.s.capital ? 2 : 0) || (b.s.pop || 0) - (a.s.pop || 0));
    for (const p of order) {
      const e = p.name; let why = 'none', b = null;
      // under its star first; failing that, over it (clear of its crown and its fleets)
      for (const up of [false, true]) {
        p.el.classList.toggle('up', up); const b0 = rect(e); if (!b0.w) { why = 'none'; break; }
        b = { x: b0.x + 5, y: b0.y + 1, w: b0.w - 10, h: b0.h - 3 };   // the name itself, without the soft backing round it
        why = !inView(b) ? 'off' : ui.find(u => hits(b, u, 4)) ? 'ui' : placed.find(q => hits(b, q, 2)) ? 'name' : stars.find(st => (st.s !== p.s || up) && hits(b, { x: st.x + 4, y: st.y + 4, w: st.w - 8, h: st.h - 8 }, 0))?.s.name || '';
        if (!why) break;
      }
      if (why) p.el.classList.remove('up'); const bad = !!why;
      if (window.__mapWhy) window.__mapWhy[p.s.name] = why ? why + ' ' + JSON.stringify(b) : 'shown';
      e.style.visibility = bad ? 'hidden' : ''; if (!bad) placed.push(b);
      for (const m of p.el.querySelectorAll('.cap, .mk, .jumps, .stn')) { const mb = rect(m); m.style.visibility = mb.w && (!inView(mb, -20) || ui.some(u => hits(mb, u, 0))) ? 'hidden' : ''; }
    }
    // the lanes as segments on the screen, which a name may cross at a cost
    const segs = []; for (const l of this.model.lanes) { const a = this.byId[l.a], b = this.byId[l.b]; if (!a || !b) continue; const pa = this.screenOf(a.x, -a.y), pb = this.screenOf(b.x, -b.y); segs.push([pa.x, pa.y, pb.x, pb.y]); }
    const crosses = r => { let n = 0; for (const [x1, y1, x2, y2] of segs) { if (Math.max(x1, x2) < r.x || Math.min(x1, x2) > r.x + r.w || Math.max(y1, y2) < r.y || Math.min(y1, y2) > r.y + r.h) continue; const dx = x2 - x1, dy = y2 - y1; let t0 = 0, t1 = 1; for (const [p, q] of [[-dx, x1 - r.x], [dx, r.x + r.w - x1], [-dy, y1 - r.y], [dy, r.y + r.h - y1]]) { if (p === 0) { if (q < 0) { t0 = 2; break; } } else { const t = q / p; if (p < 0) t0 = Math.max(t0, t); else t1 = Math.min(t1, t); } } if (t0 <= t1) n++; } return n; };
    // a region's or a realm's name: the clearest place near its centre, or not at all
    const place = (list, big) => {
      for (const r of list) {
        const e = r.inner; e.style.setProperty('--dx', '0px'); e.style.setProperty('--dy', '0px'); const b0 = rect(e); if (!b0.w || this.screenOf(r.x, r.y).z >= 1) { e.style.visibility = 'hidden'; continue; }
        let best = null;
        for (let dy = -100; dy <= 100; dy += 10) for (let dx = -180; dx <= 180; dx += 15) {
          const b = { x: b0.x + dx, y: b0.y + dy, w: b0.w, h: b0.h };
          // a region's name keeps off the stars and crosses a lane only at a cost; a realm's, from far off, may lie over the stars
          if (!inView(b, 6) || ui.some(u => hits(b, u, 8)) || placed.some(q => hits(b, q, big ? 14 : 6)) || (!big && stars.some(st => hits(b, st, 2)))) continue;
          const pen = Math.hypot(dx, dy * 1.6) * 0.05 + (big ? stars.filter(st => hits(b, st, 0)).length * 3 : crosses(b) * 6); if (!best || pen < best.pen) best = { pen, dx, dy, b };
        }
        if (!best) { e.style.visibility = 'hidden'; this.places[r.key] = null; continue; }
        e.style.visibility = ''; e.style.setProperty('--dx', best.dx + 'px'); e.style.setProperty('--dy', best.dy + 'px'); placed.push(best.b); this.places[r.key] = { dx: best.dx, dy: best.dy };
      }
    };
    this.places ||= {};
    if (tier === 'far') { place(this.realmPins.slice().sort((a, b) => b.big - a.big), true); for (const r of this.regionPins) { r.inner.style.visibility = 'hidden'; this.places[r.key] = null; } }
    else { place(this.regionPins, false); for (const r of this.realmPins) { r.inner.style.visibility = 'hidden'; this.places[r.key] = null; } }
  }

  /** Inside a system: its names never over one another either. The world looked at and the system's own world first, then the gates,
      the worlds, the belt and the fleets; a comet's or a moon's name gives way. */
  declutterSystem() {
    const rank = e => e.classList.contains('looked') ? 0 : e.classList.contains('primary') ? 1 : e.classList.contains('sysv-prov') ? 1 : e.classList.contains('sysv-exit') ? 2 : e.classList.contains('moon') ? 6 : e.classList.contains('comet') ? 5 : e.classList.contains('sysv-fleet') ? 4 : 3;
    const els = this.sysLabels.filter(l => l !== this.sysTip && l.element.style.display !== 'none').map(l => l.element).filter(e => e.firstChild).sort((a, b) => rank(a) - rank(b));
    const ui = this.obstacles(), placed = [], hits = (a, b, p) => a.x < b.x + b.w + p && b.x < a.x + a.w + p && a.y < b.y + b.h + p && b.y < a.y + a.h + p;
    for (const e of els) { const r = e.firstChild.getBoundingClientRect(); if (!r.width) continue; const b = { x: r.left, y: r.top, w: r.width, h: r.height }; const bad = ui.some(u => hits(b, u, 4)) || placed.some(q => hits(b, q, 2)); e.firstChild.style.visibility = bad ? 'hidden' : ''; if (!bad) placed.push(b); }
  }

  // ---------------- the minimap (7.5) ----------------
  setMinimap(canvas) {
    this.mini = canvas; this.miniBase = null; if (!canvas || canvas.__map) return; canvas.__map = true;
    canvas.addEventListener('pointerdown', e => {
      const g = [...(window.praetoriaSpace?.galaxies?.values() || [])].find(x => x.mini === canvas); if (!g || !g.bounds || g.sys) return;
      const r = canvas.getBoundingClientRect(), m = g.miniMap(); const x = (e.clientX - r.left) / r.width * m.W, y = (e.clientY - r.top) / r.height * m.H;
      const t = g.clampTarget(m.minX + (x - m.ox) / m.k, m.maxY - (y - m.oy) / m.k, g.distance); g.target.set(t.x, t.y, 0); g.updateCamera();
    });
  }
  miniMap() {
    const c = this.mini, b = this.bounds, pad = 150, W = c.width, H = c.height;
    const k = Math.min((W - 8) / (b.maxX - b.minX + pad * 2), (H - 8) / (b.maxY - b.minY + pad * 2));
    return { k, W, H, ox: (W - (b.maxX - b.minX + pad * 2) * k) / 2 + pad * k, oy: (H - (b.maxY - b.minY + pad * 2) * k) / 2 + pad * k, minX: b.minX, maxY: b.maxY };
  }
  /** The same territory toned to 38%, dark lines between owners, the Reach's edge in ivory: drawn once for the holdings. */
  drawMiniBase() {
    const c = this.mini, t = this.terr; if (!c || !t) return;
    const dpr = Math.min(2, window.devicePixelRatio || 1), cw = Math.round((c.clientWidth || 278) * dpr), ch = Math.round((c.clientHeight || 146) * dpr); if (cw > 0 && ch > 0 && (c.width !== cw || c.height !== ch)) { c.width = cw; c.height = ch; }
    const m = this.miniMap(), W = c.width, H = c.height, base = document.createElement('canvas'); base.width = W; base.height = H; const ctx = base.getContext('2d'), img = ctx.createImageData(W, H), px = img.data;
    const own = new Int32Array(W * H).fill(-2), realm = new Int32Array(W * H).fill(-2);
    for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) { const wx = m.minX + (x + .5 - m.ox) / m.k, wy = m.maxY - (y + .5 - m.oy) / m.k; if (this.eoutAt(wx, wy) <= 0) continue; const s = this.siteAt(wx, wy); if (s < 0) continue; own[y * W + x] = t.sh[s]; realm[y * W + x] = t.sr[s]; }
    const ivory = rgb01('#E3D3A4');
    for (let y = 0; y < H; y++) for (let x = 0; x < W; x++) {
      const i = y * W + x, o = i * 4, h = own[i]; if (h === -2) continue;
      const hc = this.holderCol[t.hk[h]] || { kind: 0 }; let c3 = hc.kind ? hc.fill : [.55, .6, .7], a = hc.kind === 2 ? .62 : hc.kind ? .38 : .12;
      const nb = [x > 0 ? i - 1 : -1, x < W - 1 ? i + 1 : -1, y > 0 ? i - W : -1, y < H - 1 ? i + W : -1];
      const edge = nb.some(j => j < 0 || own[j] === -2), seam = nb.some(j => j >= 0 && own[j] !== -2 && own[j] !== h), rseam = nb.some(j => j >= 0 && realm[j] !== -2 && realm[j] !== realm[i]);
      if (edge && t.rk[realm[i]] === 'reach') { c3 = ivory; a = .9; } else if (edge) { c3 = hc.kind ? hc.line : [.6, .65, .75]; a = .6; } else if (rseam) { c3 = [0, 0, 0]; a = .85; } else if (seam) { c3 = c3.map(v => v * .35); a = Math.max(a, .55); }
      px[o] = c3[0] * 255; px[o + 1] = c3[1] * 255; px[o + 2] = c3[2] * 255; px[o + 3] = a * 255;
    }
    ctx.putImageData(img, 0, 0); this.miniBase = base;
  }
  drawMinimap() {
    const c = this.mini; if (!c || !this.model || !this.bounds) return; if (!this.miniBase) this.drawMiniBase(); const ctx = c.getContext('2d'), m = this.miniMap();
    ctx.clearRect(0, 0, c.width, c.height); if (this.miniBase) ctx.drawImage(this.miniBase, 0, 0);
    if (this.sys) return;
    // the viewport: the free map's four corners on the plane, in brass, with a dot at each corner
    const host = this.renderer.domElement.getBoundingClientRect(), f = this.freeRect(), P = (x, y) => [m.ox + (x - m.minX) * m.k, m.oy + (m.maxY - y) * m.k];
    const corners = [[f.x0, f.y0], [f.x1, f.y0], [f.x1, f.y1], [f.x0, f.y1]].map(([x, y]) => this.planePoint(host.left + x, host.top + y)).filter(Boolean).map(p => P(p.x, p.y));
    if (corners.length === 4) {
      const dpr = c.width / (c.clientWidth || c.width); ctx.save(); ctx.strokeStyle = 'rgba(232,200,120,.95)'; ctx.lineWidth = dpr; ctx.beginPath(); corners.forEach(([x, y], i) => i ? ctx.lineTo(x, y) : ctx.moveTo(x, y)); ctx.closePath(); ctx.stroke();
      ctx.fillStyle = '#f6e2a0'; for (const [x, y] of corners) { ctx.beginPath(); ctx.arc(x, y, 1.6 * dpr, 0, 7); ctx.fill(); } ctx.restore();
    }
  }

  // ---------------- the frame ----------------
  loop() {
    if (this.disposed) return;
    // a frame budget: about 20 fps, nothing while the tab is hidden: the map is a chart, not a shooter
    setTimeout(() => requestAnimationFrame(this.loop), 50);
    if (document.hidden) return;
    if (this.lost) { if (performance.now() - this.lostAt > 2500 && this.onGiveUp) { const again = this.onGiveUp; this.onGiveUp = null; again(); } return; }
    const t = this.clock.getElapsedTime(); this.nebula.material.uniforms.uTime.value = t; this.stars.material.uniforms.uTime.value = t; const wasSettled = this.settled();
    const now = performance.now(), dt = Math.min(0.3, (now - (this.lastT || now)) / 1000); this.lastT = now; this.applyCamera(1 - Math.exp(-dt / 0.11));
    const pr = this.renderer.getPixelRatio();
    if (this.sys) { this.tickSystem(t); this.final.uniforms.uGlow.value = 0; this.composer.render(); this.labelRenderer.render(this.scene, this.camera); if ((this.miniTick = (this.miniTick || 0) + 1) % 6 === 0) { this.drawMinimap(); this.declutterSystem(); } return; }
    // which drawing of the territory shows (7.2), and what the names and the systems show: far, the realms; the middle, the houses; close, the systems
    const d = this.cur ? this.cur.d : this.distance, far = ss(1750, 2300, d), near = 1 - ss(560, 780, d), mid = Math.max(0, 1 - far - near);
    if (this.terrMesh) { this.terrMesh.material.uniforms.uTier.value.set(far, mid, near); this.terrMesh.material.uniforms.uDpr.value = pr; }
    if (this.starMat) { this.starMat.uniforms.uDpr.value = pr; this.starMat.uniforms.uGrow.value = d < 700 ? 1.25 : d > 2600 ? 0.8 : 1; }
    if (this.laneMat) this.laneMat.uniforms.uDpr.value = pr;
    const tier = d > 2000 ? 'far' : d < 680 ? 'near' : 'mid'; if (tier !== this.tier) { this.tier = tier; const lr = this.labelRenderer.domElement; lr.classList.toggle('tier-far', tier === 'far'); lr.classList.toggle('tier-mid', tier === 'mid'); lr.classList.toggle('tier-near', tier === 'near'); this.relabel = true; this.tierChanged = true; }
    const show = d < 650; if (show !== this.detailsOn) { this.detailsOn = show; for (const g of this.details) g.visible = show; }
    if (show) { this.objects.traverse(o => { if (o.userData.spin) o.rotation.y = t * o.userData.spin; }); this.dirty = true; }
    // a map that is still is drawn a few times a second (the nebula drifts slowly), not twenty: the rest of the page keeps the machine
    if (!this.dirty && !this.relabel && !this.tierChanged && wasSettled && now - (this.lastRender || 0) < 250) return;
    this.dirty = false; this.lastRender = now;
    this.glowComposer.render(); this.final.uniforms.tGlow.value = this.glowComposer.readBuffer.texture; this.final.uniforms.uGlow.value = 1;
    this.composer.render(); this.labelRenderer.render(this.scene, this.camera);
    // the names are placed when the camera stops, and again when the interface moves
    const settled = this.settled(); if (!settled) this.moving = true;
    if ((settled && (this.moving || this.relabel)) || this.tierChanged || (!settled && now - (this.lastDeclutter || 0) > 700)) { if (settled) { this.moving = false; this.relabel = false; } this.tierChanged = false; this.lastDeclutter = now; this.declutter(); }
    if (settled && this.keepZoom && performance.now() - this.keepZoom > 800) { this.keepZoom = 0; try { localStorage.setItem('praetoria.map.zoom.' + (this.model?.key || ''), String(Math.round(this.distance))); } catch { /* the zoom is not kept */ } }
    if ((this.miniTick = (this.miniTick || 0) + 1) % 3 === 0 || !wasSettled) this.drawMinimap();
  }
  // ---------------- the close map of one solar system (docs/SYSTEMS.md, "The close map") ----------------
  // The Reach is hidden and the system is drawn where it lies, large: its sun, each thing on its own orbit where it is this year,
  // the belt, the gates where its lanes leave toward the neighbours they lead to, and who is in it. The chart comes from the game
  // (SystemCharts.For); nothing about where a thing is is decided here.
  applyMode() {
    const inside = !!this.sys; this.objects.visible = !inside; for (const l of this.labels) l.visible = !inside; if (inside) this.hideTip();
    if (this.sysGroup) this.sysGroup.visible = inside; this.bloom.enabled = inside;
    this.labelRenderer.domElement.classList.toggle('in-system', inside); this.miniBase = null;
  }
  // how much of the map lies under the window at the left, the list at the right and the bar on top (the free map, as js/shell.js tells it)
  insets() { const f = this.freeRect(); return { L: f.x0, R: f.W - f.x1, T: f.y0, W: f.W, H: f.H }; }
  // put a circle of the map (its centre and its radius) in the middle of what can be seen, as large as will fit
  frame(cx, cy, radius) {
    const i = this.insets(), tanH = Math.tan(THREE.MathUtils.degToRad(this.camera.fov / 2)), visW = Math.max(240, i.W - i.L - i.R), visH = Math.max(240, i.H - i.T);
    const d = Math.max(radius * i.H / (visW * tanH), radius * i.H / (visH * tanH) * 1.16);
    // the camera's view offset already puts the look-at point in the middle of the free map, so the circle's centre is the target
    this.frameOff = { x: 0, y: 0 }; this.target.set(cx, cy, 0); this.distance = d; this.updateCamera(); return d;
  }
  sysFit() { return this.frame(this.sysAt.x, this.sysAt.y, this.sys.edge * 1.34); }   // the gates' names stand outside the edge, and are to be read
  clearSystem() {
    for (const l of this.sysLabels) l.element.remove(); this.sysLabels = []; this.sysHits = []; this.sysBodies = {}; this.sysTip = null;
    if (this.sysGroup) { this.scene.remove(this.sysGroup); this.sysGroup.traverse(c => { c.geometry?.dispose?.(); if (c.material && !c.material.__shared) c.material.dispose?.(); }); this.sysGroup = null; }
  }
  setSystem(chart) {
    const was = this.sys;
    if (!chart) {   // back to the Reach, looking where it was looked at from
      if (!was) return; this.clearSystem(); this.sys = null; this.focused = null; this.bloom.strength = 0.9; this.applyMode();
      if (this.galCam) { this.target.copy(this.galCam.t); this.distance = this.galCam.d; this.galCam = null; this.updateCamera(); }
      return;
    }
    const s = this.model && this.model.systems.find(x => x.id === chart.id); const at = s ? { x: s.x, y: -s.y } : { x: this.target.x, y: this.target.y };
    const same = was && was.id === chart.id;
    // the worlds are seen to move when a year passes: each from where it was to where it is
    const from = {}; if (same && was.year !== chart.year) for (const b of was.bodies) from[b.index] = b.angle;
    if (!was) this.galCam = { t: this.target.clone(), d: this.distance };
    this.sys = chart; this.sysAt = at; this.clearSystem(); this.buildSystem(chart, at, from); this.applyMode();
    if (!same) { this.focused = null; this.sysFitD = this.sysFit(); }
    if (chart.focusBody != null && chart.focusBody >= 0) { if (this.focused !== chart.focusBody || !same) this.focusBody(chart.focusBody); }
    else if (this.focused != null) { this.focused = null; this.sysFitD = this.sysFit(); }
  }
  // look at one world close to: the view goes to it and follows it
  focusBody(index) { const b = this.sysBodies[index]; if (!b) return; this.focused = index; this.frame(this.sysAt.x + b.group.position.x, this.sysAt.y + b.group.position.y, b.reach || Math.max(40, b.r * 4.2)); }
  buildSystem(chart, at, from) {
    const T = textures(); const g = new THREE.Group(); g.position.set(at.x, at.y, 0); this.sysGroup = g; this.scene.add(g);
    const label = (html, cls, x, y, z, parent) => { const el = document.createElement('div'); el.className = cls; el.innerHTML = `<div class="in">${html}</div>`; const lab = new CSS2DObject(el); lab.position.set(x, y, z || 0); (parent || g).add(lab); this.sysLabels.push(lab); return lab; };
    const hitAt = (x, y, r, data, parent) => { const m = new THREE.Mesh(new THREE.SphereGeometry(r, 10, 8), new THREE.MeshBasicMaterial({ visible: false })); m.position.set(x, y, 2); m.userData = data; (parent || g).add(m); this.sysHits.push(m); return m; };
    const circle = (r, color, opacity, dashed) => {
      const pts = Array.from({ length: 129 }, (_, i) => { const a = i / 128 * Math.PI * 2; return new THREE.Vector3(Math.cos(a) * r, Math.sin(a) * r, 0); });
      const line = new THREE.Line(new THREE.BufferGeometry().setFromPoints(pts), dashed ? new THREE.LineDashedMaterial({ color, transparent: true, opacity, dashSize: 6, gapSize: 9 }) : new THREE.LineBasicMaterial({ color, transparent: true, opacity }));
      if (dashed) line.computeLineDistances(); return line;
    };
    const tipEl = document.createElement('div'); tipEl.className = 'map-tip'; tipEl.style.display = 'none'; this.sysTip = new CSS2DObject(tipEl); this.sysTip.visible = false; g.add(this.sysTip); this.sysLabels.push(this.sysTip);

    // the sun
    const tint = [[0xfff4e0, 1.0], [0xffe2a8, 0.92], [0xffb070, 0.84], [0xbfd4ff, 1.12], [0xffffff, 1.05], [0xffd0a0, 0.88]][chart.sunTint % 6];
    const sunR = 30 * tint[1], close = chart.focusBody != null && chart.focusBody >= 0;   // looking at one world, the sun is turned down so the world is what is seen
    const corona = new THREE.Sprite(new THREE.SpriteMaterial({ map: T.sun, color: tint[0], transparent: true, opacity: close ? .5 : 1, blending: THREE.AdditiveBlending, depthWrite: false })); corona.scale.set(sunR * (close ? 3.4 : 5.2), sunR * (close ? 3.4 : 5.2), 1); g.add(corona);
    const core = new THREE.Sprite(new THREE.SpriteMaterial({ map: T.glow, color: 0xffffff, transparent: true, blending: THREE.AdditiveBlending, depthWrite: false })); core.scale.set(sunR * 1.5, sunR * 1.5, 1); g.add(core);
    const flare = new THREE.Sprite(new THREE.SpriteMaterial({ map: T.flare, color: tint[0], transparent: true, opacity: close ? 0 : .16, blending: THREE.AdditiveBlending, depthWrite: false })); flare.scale.set(sunR * 6.5, sunR * .3, 1); g.add(flare);
    if (chart.sunWorks) { const halo = new THREE.Mesh(new THREE.RingGeometry(sunR * 1.55, sunR * 1.66, 96), new THREE.MeshBasicMaterial({ color: 0xffd36a, transparent: true, opacity: .85, side: THREE.DoubleSide, blending: THREE.AdditiveBlending })); halo.userData.spinZ = -0.2; g.add(halo); }
    if (chart.sun != null) hitAt(0, 0, sunR * 0.9, { kind: 'body', id: String(chart.sun), tip: `<b>${esc(chart.sunName || 'The sun')}</b><div>the sun of ${esc(chart.name)}${chart.sunWorks ? ' · built on' : ''}</div>`, at: [0, 0], up: sunR + 10 });

    // the edge of the system, and the gates where its lanes leave
    const rim = circle(chart.edge, 0x8fa8d8, .16, true); g.add(rim);
    for (const e of chart.exits) {
      const col = new THREE.Color(e.accent || '#8fa8d8'); const ux = Math.cos(e.angle), uy = Math.sin(e.angle);
      const beam = new THREE.Line(new THREE.BufferGeometry().setFromPoints([new THREE.Vector3(e.x, e.y, 0), new THREE.Vector3(e.x + ux * chart.edge * 0.9, e.y + uy * chart.edge * 0.9, 0)]), new THREE.LineBasicMaterial({ vertexColors: true, transparent: true, opacity: .85 }));
      beam.geometry.setAttribute('color', new THREE.Float32BufferAttribute([col.r, col.g, col.b, col.r * .12, col.g * .12, col.b * .12], 3)); g.add(beam);
      const gate = new THREE.Mesh(new THREE.RingGeometry(9, 11, 40), new THREE.MeshBasicMaterial({ color: col, transparent: true, opacity: .95, side: THREE.DoubleSide, blending: THREE.AdditiveBlending })); gate.position.set(e.x, e.y, 1); gate.userData.spinZ = 0.35; g.add(gate);
      const glow = new THREE.Sprite(new THREE.SpriteMaterial({ map: T.glow, color: col, transparent: true, opacity: .55, blending: THREE.AdditiveBlending, depthWrite: false })); glow.scale.set(46, 46, 1); glow.position.set(e.x, e.y, 0.5); glow.userData.pulse = true; g.add(glow);
      const arrow = new THREE.Sprite(new THREE.SpriteMaterial({ map: T.ship, color: col, transparent: true, opacity: .9, depthWrite: false, rotation: e.angle - Math.PI / 2 })); arrow.scale.set(11, 11, 1); arrow.position.set(e.x + ux * 22, e.y + uy * 22, 1); g.add(arrow);
      const who = e.ring === 'beyond' ? "a nation's space" : e.holderName || 'unclaimed';
      label(`<span class="n">${e.chokepoint ? '◆ ' : ''}${esc(e.name)}</span><span class="o" style="color:${esc(e.accent)}">${esc(who)}</span>`, 'sysv-exit', e.x + ux * 46, e.y + uy * 46, 2);
      hitAt(e.x, e.y, 20, { kind: 'exit', id: e.id, tip: `<b>The lane to ${esc(e.name)}</b><div>${esc(who)}${e.chokepoint ? ' · a key of the lanes' : ''}</div><div>click to go there</div>`, at: [e.x, e.y], up: 22 });
    }

    // what goes round the sun, and what goes round that
    const lit = 2.513;   // the side of a painted world the light falls on, as fx.js paints it
    const radius = b => b.cls === 'comet' ? 4.2 : b.parent >= 0 ? 3.6 + (b.size - 1) * 1.2 : b.cls === 'gas' ? 15 : b.primary ? 12.5 : b.size >= 3 ? 11 : b.size === 2 ? 8.2 : 5.6;
    const byIndex = {}; for (const b of chart.bodies) byIndex[b.index] = b;
    for (const b of chart.bodies) {
      if (b.cls === 'belt') {
        // the belt: a ring of rock, thicker where the system is richer in it
        g.add(circle(b.orbit, 0xc9bca8, .08));
        const n = 520 + 260 * Math.min(3, chart.belt || 1), pts = [], cols = []; let rs = hash(chart.id + '#' + b.index); const rr = () => { rs = (rs * 1664525 + 1013904223) >>> 0; return rs / 4294967296; };
        for (let q = 0; q < n; q++) { const a = rr() * Math.PI * 2, w = (rr() + rr() + rr()) / 3 - .5, r2 = b.orbit + w * 34; pts.push(Math.cos(a) * r2, Math.sin(a) * r2, (rr() - .5) * 5); const c = .5 + rr() * .45; cols.push(c, c * .94, c * .86); }
        const geo = new THREE.BufferGeometry(); geo.setAttribute('position', new THREE.Float32BufferAttribute(pts, 3)); geo.setAttribute('color', new THREE.Float32BufferAttribute(cols, 3));
        const rocks = new THREE.Points(geo, new THREE.PointsMaterial({ size: 2.1, vertexColors: true, transparent: true, opacity: .9, sizeAttenuation: true })); rocks.userData.spinZ = 0.012; g.add(rocks);
        for (let q = 0; q < 9; q++) { const a = rr() * Math.PI * 2, r2 = b.orbit + (rr() - .5) * 20; const rock = new THREE.Sprite(new THREE.SpriteMaterial({ map: window.praetoriaFx ? new THREE.TextureLoader().load(window.praetoriaFx.planet('moon', chart.id + 'rock' + q, 48)) : T.glow, color: 0xb8ab98, transparent: true, depthWrite: false })); const sc = 3 + rr() * 3.5; rock.scale.set(sc, sc * (.7 + rr() * .3), 1); rock.position.set(Math.cos(a) * r2, Math.sin(a) * r2, 1); g.add(rock); }
        const lx = Math.cos(b.angle) * b.orbit, ly = Math.sin(b.angle) * b.orbit;
        label(`<span class="n">${esc(b.name)}</span><span class="o">${b.deposit ? esc(b.deposit.replace('_', ' ')) + ' · ' : ''}${chart.belt || 0} claim${chart.belt === 1 ? '' : 's'}${b.works ? ' · ' + b.works + ' built' : ''}</span>`, 'sysv-label belt', lx, ly - 22, 2);
        hitAt(lx, ly, 24, { kind: 'body', id: String(b.index), tip: `<b>${esc(b.name)}</b><div>an asteroid belt · room for ${chart.belt || 0} claim${chart.belt === 1 ? '' : 's'}</div>`, at: [lx, ly], up: 20 });
        this.sysBodies[b.index] = { group: { position: new THREE.Vector3(lx, ly, 0) }, r: 20, fixed: true };
        continue;
      }
      const grp = new THREE.Group(); const r = radius(b); g.add(grp);
      // its path: a circle round the sun, a long loop for a comet; a moon's is small and goes with its world
      if (b.parent < 0 && b.cls !== 'comet') g.add(circle(b.orbit, b.primary ? 0xe6c77a : 0xc9a24a, b.primary ? .3 : .13));
      else if (b.cls === 'comet') {
        const pts = [];   // the loop is drawn through where it is, about the same sun
        const here = Math.atan2(b.y, b.x), rNow = Math.hypot(b.x, b.y), p = b.orbit * (1 - b.ecc * b.ecc); let m = Math.acos(Math.max(-1, Math.min(1, (p / Math.max(1, rNow) - 1) / b.ecc))); const axis = here - m;
        for (let i = 0; i <= 160; i++) { const v = i / 160 * Math.PI * 2, rr2 = p / (1 + b.ecc * Math.cos(v)); pts.push(new THREE.Vector3(Math.cos(v + axis) * rr2, Math.sin(v + axis) * rr2, 0)); }
        const path = new THREE.Line(new THREE.BufferGeometry().setFromPoints(pts), new THREE.LineDashedMaterial({ color: 0x9fd8ff, transparent: true, opacity: .22, dashSize: 5, gapSize: 7 })); path.computeLineDistances(); g.add(path);
        const tail = new THREE.Sprite(new THREE.SpriteMaterial({ map: T.flare, color: 0xbfe6ff, transparent: true, opacity: .75, blending: THREE.AdditiveBlending, depthWrite: false, rotation: here })); tail.scale.set(44, 5, 1); tail.position.set(Math.cos(here) * 20, Math.sin(here) * 20, 0); grp.add(tail);
      } else { const ring = circle(b.orbit, 0xaeb7c9, .14); grp.userData.moonRing = ring; }
      const looked = chart.focusBody === b.index;
      const tex = window.praetoriaFx ? new THREE.TextureLoader().load(window.praetoriaFx.planet(b.parent >= 0 ? 'moon' : b.cls, chart.id + (b.primary ? '' : b.name), looked ? 384 : b.primary || b.cls === 'gas' ? 192 : b.parent >= 0 ? 64 : 128)) : null; if (tex) tex.colorSpace = THREE.SRGBColorSpace;
      const disc = new THREE.Sprite(new THREE.SpriteMaterial({ map: tex, color: tex ? 0xffffff : planetColor(b.cls), transparent: true, depthWrite: false })); disc.scale.set(r * 2.1, r * 2.1, 1); disc.position.z = 3; grp.add(disc); grp.userData.disc = disc;
      if (b.cls === 'gas') {
        // a giant's rings: thin bands of light that do not hide the giant, the nearer half drawn over it and the further half under
        for (const [ri, ro, op] of [[1.42, 1.62, .34], [1.68, 1.86, .22], [1.92, 2.02, .14]]) for (const half of [0, 1]) {
          const band = new THREE.Mesh(new THREE.RingGeometry(r * ri, r * ro, 72, 1, half ? Math.PI : 0, Math.PI), new THREE.MeshBasicMaterial({ color: 0xf0d8b0, transparent: true, opacity: op, side: THREE.DoubleSide, blending: THREE.AdditiveBlending, depthWrite: false }));
          band.rotation.x = 1.12; band.position.z = half ? 3.6 : 2.4; band.renderOrder = half ? 3 : 1; grp.add(band);
        }
        disc.renderOrder = 2;
      }
      if (b.primary) {
        const own = new THREE.Mesh(new THREE.RingGeometry(r * (looked ? 1.2 : 1.42), r * (looked ? 1.225 : 1.52), 96), new THREE.MeshBasicMaterial({ color: chart.holder ? chart.accent : 0x8fa8d8, transparent: true, opacity: chart.holder ? .95 : .4, side: THREE.DoubleSide, blending: THREE.AdditiveBlending })); own.position.z = 2; grp.add(own);
        if (chart.isPlayer) { const home = new THREE.Sprite(new THREE.SpriteMaterial({ map: T.glow, color: 0xffe8a3, transparent: true, opacity: .3, blending: THREE.AdditiveBlending, depthWrite: false })); home.scale.set(r * 5, r * 5, 1); home.userData.pulse = true; grp.add(home); }
      }
      // what is built on it: a lamp for each building, a turning ring for a great work, a dashed one for what is rising
      for (let k = 0; k < Math.min(6, b.works || 0); k++) { const a = -0.6 + k * 0.62; const lamp = new THREE.Sprite(new THREE.SpriteMaterial({ map: T.glow, color: 0xffd36a, transparent: true, opacity: .95, blending: THREE.AdditiveBlending, depthWrite: false })); lamp.scale.set(4.6, 4.6, 1); lamp.position.set(Math.cos(a) * (r + 3.4), Math.sin(a) * (r + 3.4), 4); grp.add(lamp); }
      if (b.great) { const gw = new THREE.Mesh(new THREE.RingGeometry(r * 1.72, r * 1.8, 6), new THREE.MeshBasicMaterial({ color: 0xffd36a, transparent: true, opacity: .9, side: THREE.DoubleSide, blending: THREE.AdditiveBlending })); gw.userData.spinZ = 0.3; gw.position.z = 2; grp.add(gw); }
      if (b.rising) { const rz = circle(r * 1.95, 0xffd36a, .7, true); rz.userData.spinZ = -0.4; grp.add(rz); }
      if (String(b.index) === String(chart.selectedBody) && !looked) { const sel = new THREE.Mesh(new THREE.RingGeometry(r * 2.15, r * 2.3, 64), new THREE.MeshBasicMaterial({ color: 0xffe8a3, transparent: true, opacity: .95, side: THREE.DoubleSide, blending: THREE.AdditiveBlending })); sel.userData.spinZ = 0.5; sel.position.z = 2; grp.add(sel); }
      const kind = b.parent >= 0 ? 'moon' : b.cls === 'gas' ? 'gas giant' : b.cls === 'comet' ? 'comet' : b.cls + ' world';
      const marks = [b.featureName ? '✦ ' + esc(b.featureName) : '', b.deposit ? esc(b.deposit.replace('_', ' ')) : '', b.works ? b.works + ' built' : '', b.rising ? b.rising + ' rising' : ''].filter(Boolean).join(' · ');
      label(`<span class="n">${b.primary && chart.capital ? '♛ ' : ''}${esc(b.name)}</span>${b.primary && chart.holderName ? `<span class="o" style="color:${esc(chart.accent)}">${esc(chart.holderName)}</span>` : ''}${marks ? `<span class="m">${marks}</span>` : ''}`, 'sysv-label' + (b.primary ? ' primary' : '') + (b.parent >= 0 ? ' moon' : '') + (looked ? ' looked' : '') + (b.parent >= 0 && b.parent === chart.focusBody ? ' of-looked' : '') + (b.cls === 'comet' ? ' comet' : ''), 0, -(r + 5), 4, grp);
      const hit = hitAt(0, 0, Math.max(7, r * 1.15), { kind: 'body', id: String(b.index), tip: `<b>${esc(b.name)}</b><div>${esc(kind)}${b.parent < 0 && b.cls !== 'comet' ? ` · ${b.period} years to go round` : ''}</div>${marks ? `<div>${marks}</div>` : ''}<div>click to pick · twice to go to it</div>`, at: [0, 0], up: r + 8 }, grp);
      // the provinces of the world looked at: a pin on its face for each, and beside it whose it is and what city it has
      if (looked && b.primary && chart.provinces && chart.provinces.length) {
        const n = chart.provinces.length; chart.provinces.forEach((pv, k) => {
          const a = Math.PI / 2 + (k - (n - 1) / 2) * 1.5, px = Math.cos(a) * r * .5, py = Math.sin(a) * r * .46 - r * .08, ox = Math.cos(a) * r * 2.3, oy = Math.sin(a) * r * 1.6 + r * .2;
          const pin = new THREE.Sprite(new THREE.SpriteMaterial({ map: T.glow, color: pv.accent || '#ffe8a3', transparent: true, opacity: 1, depthWrite: false })); pin.scale.set(3.2, 3.2, 1); pin.position.set(px, py, 5); grp.add(pin);
          const lead = new THREE.Line(new THREE.BufferGeometry().setFromPoints([new THREE.Vector3(px, py, 5), new THREE.Vector3(ox, oy, 5)]), new THREE.LineBasicMaterial({ color: pv.accent || '#ffe8a3', transparent: true, opacity: .7 })); grp.add(lead);
          label(`<span class="n">${esc(pv.name)}</span><span class="c">${pv.great ? '★ ' : ''}${esc(pv.city)}${pv.kind ? ' · ' + esc(pv.kind) : ''}</span><span class="o" style="color:${esc(pv.accent)}">${esc(pv.holderName || 'held by the house')}</span><span class="m">${esc(pv.citizens || '')}${pv.level > 1 ? ' · level ' + pv.level : ''}</span>`, 'sysv-prov ' + (ox < -1 ? 'left' : ox > 1 ? 'right' : 'mid'), ox, oy, 6, grp);
          hitAt(ox, oy, 7, { kind: 'province', id: String(k), tip: `<b>${esc(pv.name)}</b><div>a province of ${esc(b.name)} · ${esc(pv.holderName || '')}</div><div>click for the world's provinces</div>`, at: [b.x + ox, b.y + oy], up: 8 }, grp);
        });
      }
      const mine = chart.bodies.filter(o => o.parent === b.index), reach = (mine.length ? Math.max(...mine.map(o => o.orbit)) : r * 2) + 34;
      this.sysBodies[b.index] = { group: grp, r, hit, body: b, angle: b.angle, from: from[b.index], reach: Math.max(reach, r * 4.4) };
    }
    // set each where it is (or where it was, to be moved on): a moon about its world
    this.sysMoveFrom = Object.keys(from).length ? performance.now() : 0;
    this.placeBodies(this.sysMoveFrom ? 0 : 1);
    for (const key of Object.keys(this.sysBodies)) { const sb = this.sysBodies[key]; if (sb.group.userData && sb.group.userData.moonRing) { const par = this.sysBodies[sb.body.parent]; if (par && !par.fixed) par.group.add(sb.group.userData.moonRing); } }

    // who is here: fleets, stations, claims
    const worldOf = i => (this.sysBodies[i] && !this.sysBodies[i].fixed) ? this.sysBodies[i] : null;
    for (const f of chart.fleets) {
      const home = f.atBody >= 0 ? worldOf(f.atBody) : null; const hb = home ? byIndex[f.atBody] : null; const px = home ? f.x - hb.x : f.x, py = home ? f.y - hb.y : f.y; const parent = home ? home.group : g;
      const pirate = f.ownerId === 'pirates'; const sc = (f.isPlayer ? 13 : 11) * (close && home ? .7 : 1);   // close to a world its fleets are drawn to its scale
      const seen = f.laidUp ? .45 : f.isPlayer ? 1 : .4 + .6 * (f.confidence || 0) / 100;
      const spr = new THREE.Sprite(new THREE.SpriteMaterial({ map: pirate ? T.skull : T.hull, color: pirate ? 0xff4a3a : f.accent, transparent: true, opacity: seen, depthWrite: false })); spr.scale.set(sc, sc, 1); spr.position.set(px, py, 6); parent.add(spr);
      // the rest of the fleet behind its flagship, in echelon on either quarter: a hull drawn for each, six at most
      if (!pirate) for (let k = 1, n = Math.min(6, Math.max(0, (f.strength | 0) - 1)); k <= n; k++) { const row = Math.ceil(k / 2), side = k % 2 ? -1 : 1, ws = sc * .62;
        const wing = new THREE.Sprite(new THREE.SpriteMaterial({ map: T.hull, color: f.accent, transparent: true, opacity: seen * .8, depthWrite: false })); wing.scale.set(ws, ws, 1); wing.position.set(px + side * row * sc * .62, py - row * sc * .58, 5.8); parent.add(wing); }
      const glow = new THREE.Sprite(new THREE.SpriteMaterial({ map: T.glow, color: f.accent, transparent: true, opacity: f.isPlayer ? .6 : .28, blending: THREE.AdditiveBlending, depthWrite: false })); glow.scale.set(sc * 2.2, sc * 2.2, 1); glow.position.set(px, py, 5); if (!f.laidUp) glow.userData.pulse = true; parent.add(glow);
      if (f.id === chart.selectedFleet) { const sel = new THREE.Mesh(new THREE.RingGeometry(sc * .9, sc * 1.02, 48), new THREE.MeshBasicMaterial({ color: 0xffe8a3, transparent: true, opacity: .95, side: THREE.DoubleSide })); sel.position.set(px, py, 5.5); sel.userData.spinZ = 0.8; parent.add(sel); }
      label(`${esc(f.name)} · ${f.strength}${f.isPlayer ? '' : '?'}${f.laidUp ? ' · laid up' : f.moving ? ' · under way' : ''}`, 'sysv-fleet', px + sc * 1.1, py + 3, 6, parent).element.style.color = f.accent;
      hitAt(px, py, sc * .9, { kind: 'fleet', id: f.id, tip: `<b>${esc(f.name)}</b><div>${esc(f.ownerName || '')} · ${f.strength} hull${f.strength === 1 ? '' : 's'}${f.isPlayer ? '' : ` · reported, ${f.confidence}% sure`}</div>${f.laidUp ? '<div>laid up: no crews aboard</div>' : f.moving ? '<div>making for the lane</div>' : ''}`, at: home ? [hb.x + px, hb.y + py] : [px, py], up: 14 }, parent);
    }
    for (const p of chart.posts) {
      const home = p.atBody >= 0 ? worldOf(p.atBody) : null; const hb = home ? byIndex[p.atBody] : null; const px = home ? p.x - hb.x : p.x, py = home ? p.y - hb.y : p.y; const parent = home ? home.group : g;
      const st = new THREE.Sprite(new THREE.SpriteMaterial({ map: T.station, color: p.accent, transparent: true, depthWrite: false })); const sc = (p.kind === 'claim' ? 6 : 7.5) * (close && home ? .6 : 1); st.scale.set(sc, sc, 1); st.position.set(px, py, 5); parent.add(st);
      hitAt(px, py, sc * .8, { kind: 'post', id: p.id, tip: `<b>${esc(p.name)}</b><div>${p.kind === 'claim' ? 'a claim in the belt' : 'a station'} · ${esc(p.ownerName)}</div>`, at: home ? [hb.x + px, hb.y + py] : [px, py], up: 12 }, parent);
    }
  }
  // where each thing is, a part k of the way from where it was last year to where it is this year (k = 1: where it is)
  placeBodies(k) {
    const ease = k < 1 ? k * k * (3 - 2 * k) : 1; const pos = {};
    const at = sb => { const b = sb.body; if (b.cls === 'comet') return [b.x, b.y]; let a = b.angle; if (sb.from != null && ease < 1) { let d = (b.angle - sb.from) % (Math.PI * 2); if (d < 0) d += Math.PI * 2; a = b.angle - d * (1 - ease); } return [Math.cos(a) * b.orbit, Math.sin(a) * b.orbit]; };
    for (const key of Object.keys(this.sysBodies)) { const sb = this.sysBodies[key]; if (sb.fixed || sb.body.parent >= 0) continue; const [x, y] = at(sb); sb.group.position.set(x, y, 0); pos[key] = [x, y]; }
    for (const key of Object.keys(this.sysBodies)) { const sb = this.sysBodies[key]; if (sb.fixed || sb.body.parent < 0) continue; const par = pos[sb.body.parent] || [0, 0]; const [x, y] = at(sb); sb.group.position.set(par[0] + x, par[1] + y, 0); pos[key] = [par[0] + x, par[1] + y]; }
    // the lit side of each world faces the sun
    for (const key of Object.keys(this.sysBodies)) { const sb = this.sysBodies[key]; if (sb.fixed) continue; const d = sb.group.userData.disc; if (d) d.material.rotation = Math.atan2(-sb.group.position.y, -sb.group.position.x) - 2.513; if (sb.hit && sb.hit.userData) sb.hit.userData.at = [sb.group.position.x, sb.group.position.y]; }
  }
  tickSystem(t) {
    if (this.sysMoveFrom) { const k = (performance.now() - this.sysMoveFrom) / 1800; this.placeBodies(Math.min(1, k)); if (k >= 1) this.sysMoveFrom = 0; }
    if (this.focused != null && this.sysBodies[this.focused] && this.sysMoveFrom) { const b = this.sysBodies[this.focused], o = this.frameOff || { x: 0, y: 0 }; this.target.set(this.sysAt.x + b.group.position.x + o.x, this.sysAt.y + b.group.position.y + o.y, 0); }
    this.sysGroup.traverse(o => { if (o.userData.spinZ) o.rotation.z = t * o.userData.spinZ; if (o.userData.pulse && o.isSprite) o.material.opacity = (o.userData.base ?? (o.userData.base = o.material.opacity)) * (0.75 + 0.25 * Math.sin(t * 2.4 + o.position.x * 0.05)); });
    this.bloom.strength = this.focused != null ? 0.28 : 0.7;   // close to a world the glow is turned down, or the world is all glare
    const lr = this.labelRenderer.domElement, fitD = this.sysFitD || this.sys.edge * 3; lr.classList.toggle('sys-far', this.distance > fitD * 1.15); lr.classList.toggle('sys-near', this.distance < fitD * 0.55); lr.classList.toggle('sys-world', this.focused != null);
  }

  dispose() { this.disposed = true; this.resizeObserver.disconnect(); try { this.clearSystem(); this.setModel(null); this.renderer.dispose(); } catch { /* a renderer whose context is gone may not tidy up cleanly */ } this.renderer.domElement.remove(); this.labelRenderer.domElement.remove(); this.tipEl?.remove(); }
}

function esc(s) { return String(s ?? '').replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c])); }
function planetColor(cls) { return ({ terran: 0x3f8f62, ocean: 0x2f6fb5, arid: 0xc98f4a, forge: 0xa5452a, frozen: 0xb9d4ea, gas: 0xc9a06a, belt: 0x7a6f60, barren: 0x6f6a66 })[cls] || 0x3f8f62; }
function planetGlow(cls) { return ({ terran: 0x8ad9ff, ocean: 0x8ac8ff, arid: 0xffd9a0, forge: 0xff9060, frozen: 0xe6f4ff, gas: 0xffe0b0, belt: 0x000000, barren: 0x999999 })[cls] || 0x8ad9ff; }

window.praetoriaSpace = {
  galaxies: new Map(),
  create(id, container, dotnet) { this.destroy(id); const g = new Galaxy(container, dotnet); g.onGiveUp = () => this.rebuild(id); this.galaxies.set(id, g); return true; },
  // the graphics context was lost and not given back: the same map on a new canvas, looking where it looked, with what was picked still picked
  rebuild(id) {
    const old = this.galaxies.get(id); if (!old) return false;
    const { container, dotnet, model, mini, selected } = old; const target = old.target.clone(), distance = old.distance, tilt = old.tilt, tries = (old.rebuilt || 0) + 1;
    const keep = { sys: old.sys, galCam: old.galCam }; old.dispose(); old.sys = keep.sys; old.galCam = keep.galCam; this.galaxies.delete(id);
    let g; try { g = new Galaxy(container, dotnet); } catch { return false; }   // the browser will give no context at all: the screens still work without the map
    g.rebuilt = tries; if (tries < 4) g.onGiveUp = () => this.rebuild(id); this.galaxies.set(id, g); g.homeSet = true;
    if (model) { g.setSelection(selected); g.setModel(model); }
    if (old.sys) { g.setSystem(old.sys); g.galCam = old.galCam; }
    g.target.copy(target); g.distance = distance; g.tilt = tilt; g.updateCamera(true); if (mini) g.setMinimap(mini);
    return true;
  },
  setModel(id, json) { const g = this.galaxies.get(id); if (!g) return false; const m = JSON.parse(json); g.setSelection(m.selected); g.setModel(m); return true; },
  // the close map of one system: a chart to go into it (or to draw it again as it now stands), null to come back out to the Reach
  setSystem(id, json) { const g = this.galaxies.get(id); if (!g) return false; g.setSystem(json ? JSON.parse(json) : null); return true; },
  zoom(id, f) { this.galaxies.get(id)?.zoom(f); }, home(id) { this.galaxies.get(id)?.home(); }, fit(id) { this.galaxies.get(id)?.fit(); }, focus(id, x, y) { this.galaxies.get(id)?.focus(x, y); },
  minimap(id, canvas) { this.galaxies.get(id)?.setMinimap(canvas); },
  /** js/shell.js: the rectangles of the screen the interface covers (the docked window, the bar, the rail, the outliner, the console, the alerts, the toasts). */
  cover(c) { lastCover = c; for (const g of this.galaxies.values()) g.setCover(c); },
  // pick a system as a click on it would, and look at it close to: for the keyboard, and for the scripts in tools/browser (the first map when no id is given)
  select(systemId, id) { const g = id ? this.galaxies.get(id) : [...this.galaxies.values()][0]; if (!g || !g.model) return false; const s = g.model.systems.find(x => x.id === systemId || x.name === systemId); if (!s) return false; g.dotnet?.invokeMethodAsync('OnPick', 'system', s.id); g.focus(s.x, s.y); return s.id; },
  destroy(id) { const g = this.galaxies.get(id); if (g) { g.dispose(); this.galaxies.delete(id); } }
};
