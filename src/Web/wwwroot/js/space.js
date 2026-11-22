// Praetoria — the rendered galaxy (docs/ART_DIRECTION.md §1). Three.js over WebGL: a shader nebula, bloomed stars, suns with
// flares, a hyperlane web, territory as a soft influence field with lit borders, lit planets, fleet glyphs; picking back to
// Blazor. The model is plain JSON handed over by GalaxyMap.razor after every render.
import * as THREE from 'three';
import { EffectComposer } from 'three/addons/postprocessing/EffectComposer.js';
import { RenderPass } from 'three/addons/postprocessing/RenderPass.js';
import { UnrealBloomPass } from 'three/addons/postprocessing/UnrealBloomPass.js';
import { CSS2DRenderer, CSS2DObject } from 'three/addons/renderers/CSS2DRenderer.js';

const MAX_SYS = 64, MAX_HOUSES = 32;

function hash(s) { let h = 2166136261; for (let i = 0; i < s.length; i++) { h ^= s.charCodeAt(i); h = Math.imul(h, 16777619); } return h >>> 0; }
function rnd(seed) { let t = seed >>> 0; return () => { t += 0x6D2B79F5; let r = Math.imul(t ^ (t >>> 15), 1 | t); r ^= r + Math.imul(r ^ (r >>> 7), 61 | r); return ((r ^ (r >>> 14)) >>> 0) / 4294967296; }; }

// ---- textures drawn once ----
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
  TEX.lane = (() => { const c = document.createElement('canvas'); c.width = 4; c.height = 32; const ctx = c.getContext('2d'); const g = ctx.createLinearGradient(0, 0, 0, 32); g.addColorStop(0, 'rgba(255,255,255,0)'); g.addColorStop(.5, 'rgba(255,255,255,1)'); g.addColorStop(1, 'rgba(255,255,255,0)'); ctx.fillStyle = g; ctx.fillRect(0, 0, 4, 32); const t = new THREE.CanvasTexture(c); return t; })();
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
  // a brighter core band and scattered bright knots, like a real emission cloud
  float knots = pow(max(0.0, fbm(p*3.3 + 21.0)), 5.0) * cloud * 2.5; col += vec3(0.9, 0.75, 0.6) * knots;
  // faint dust lanes
  float dust = smoothstep(0.2, 0.5, fbm(p*2.5 + 40.0)) * 0.35;
  col *= (1.0 - dust);
  float vign = smoothstep(1.2, 0.2, length(vUv - 0.5) * 1.6);
  gl_FragColor = vec4(col * vign, 1.0);
}`;

// ---- territory influence field ----
const TERR_FS = `precision highp float; varying vec2 vUv; uniform vec2 uSize; uniform int uCount; uniform vec3 uPos[${MAX_SYS}]; uniform int uOwner[${MAX_SYS}]; uniform vec3 uHouseColor[${MAX_HOUSES}]; uniform float uRadius; uniform int uPlayer;
void main(){
  vec2 p = vUv * uSize;
  float hw[${MAX_HOUSES}]; for (int h = 0; h < ${MAX_HOUSES}; h++) hw[h] = 0.0;
  float total = 0.0;
  for (int i = 0; i < ${MAX_SYS}; i++) { if (i >= uCount) break; int o = uOwner[i]; if (o < 0) continue; float d = distance(p, uPos[i].xy); float w = exp(-(d*d) / (uRadius*uRadius)) * uPos[i].z * 0.55; hw[o] += w; total += w; }
  int best = -1; float bw = 0.0, second = 0.0;
  for (int h = 0; h < ${MAX_HOUSES}; h++) { if (hw[h] > bw) { second = bw; bw = hw[h]; best = h; } else if (hw[h] > second) second = hw[h]; }
  if (best < 0) discard;
  vec3 col = uHouseColor[best];
  float field = bw;
  float inside = smoothstep(0.2, 0.5, field);
  float edge = smoothstep(0.205, 0.222, field) * (1.0 - smoothstep(0.222, 0.245, field));   // a thin lit border
  float seam = 1.0 - smoothstep(0.0, 0.05, bw - second);                                     // where two houses meet
  float alpha = inside * 0.05 + edge * 0.36;
  alpha *= (1.0 - seam * 0.75);
  vec3 c = mix(col * 0.7, col * 1.8, edge);
  if (best == uPlayer) alpha *= 1.4;
  gl_FragColor = vec4(c, alpha);
}`;

export class Galaxy {
  constructor(container, dotnet) {
    this.container = container; this.dotnet = dotnet; this.model = null; this.objects = new THREE.Group(); this.labels = [];
    this.hoverId = null; this.selected = { systemId: null, fleetId: null }; this.dirty = true; this.disposed = false;
    // the close map of one system (docs/SYSTEMS.md, "The close map"): its own group, its own labels and its own things to click
    this.sys = null; this.sysGroup = null; this.sysLabels = []; this.sysHits = []; this.sysBodies = {}; this.galCam = null;
    const w = container.clientWidth || 800, h = container.clientHeight || 500;
    this.renderer = new THREE.WebGLRenderer({ antialias: true, alpha: false, powerPreference: 'high-performance' });
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 1.5)); this.renderer.setSize(w, h); this.renderer.toneMapping = THREE.ACESFilmicToneMapping; this.renderer.toneMappingExposure = 1.1;
    this.renderer.domElement.className = 'gl'; container.appendChild(this.renderer.domElement);
    // A browser may take the graphics context away (a sleeping laptop, a driver restart, too many tabs). The map used to stay black
    // after that. Now: nothing is drawn while it is gone; when the browser gives it back the map is sized and drawn again; and if it
    // is not given back in a few seconds the whole map is built afresh on a new canvas (praetoriaSpace.rebuild).
    this.lost = false; this.lostAt = 0; this.onGiveUp = null;
    this.renderer.domElement.addEventListener('webglcontextlost', e => { e.preventDefault(); this.lost = true; this.lostAt = performance.now(); });
    this.renderer.domElement.addEventListener('webglcontextrestored', () => { this.lost = false; this.resize(); this.dirty = true; });
    const shade = document.createElement('div'); shade.className = 'gl-vignette'; container.appendChild(shade);   // the corners fall away into the dark, so the eye stays on the middle of the chart
    this.labelRenderer = new CSS2DRenderer(); this.labelRenderer.setSize(w, h); this.labelRenderer.domElement.className = 'gl-labels'; container.appendChild(this.labelRenderer.domElement);
    this.scene = new THREE.Scene(); this.scene.background = new THREE.Color(0x02030a);
    this.camera = new THREE.PerspectiveCamera(42, w / h, 1, 20000);
    this.target = new THREE.Vector3(600, -350, 0); this.distance = 1000; this.tilt = 0.42;
    // the composer's own target is multisampled: without it every lane, orbit and border is drawn with stepped edges, because the
    // renderer's antialiasing does not reach what is drawn off-screen for the bloom
    const pr = this.renderer.getPixelRatio(); const smooth = this.renderer.capabilities.isWebGL2 ? new THREE.WebGLRenderTarget(Math.round(w * pr), Math.round(h * pr), { samples: 4, type: THREE.HalfFloatType }) : undefined;
    this.composer = new EffectComposer(this.renderer, smooth); this.composer.addPass(new RenderPass(this.scene, this.camera));
    this.bloom = new UnrealBloomPass(new THREE.Vector2(w, h), 0.9, 0.55, 0.3); this.composer.addPass(this.bloom);
    this.raycaster = new THREE.Raycaster(); this.mouse = new THREE.Vector2(); this.hitTargets = [];
    this.buildBackdrop(); this.scene.add(this.objects);
    this.bindInput(); this.updateCamera();
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

  setModel(model) {
    function convexHull(points) { const p = points.slice().sort((a, b) => a.x - b.x || a.y - b.y); const cross = (o, a, b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x); const lower = []; for (const q of p) { while (lower.length >= 2 && cross(lower[lower.length - 2], lower[lower.length - 1], q) <= 0) lower.pop(); lower.push(q); } const upper = []; for (const q of p.slice().reverse()) { while (upper.length >= 2 && cross(upper[upper.length - 2], upper[upper.length - 1], q) <= 0) upper.pop(); upper.push(q); } return lower.slice(0, -1).concat(upper.slice(0, -1)); }
    function smoothClosed(pts, seg) { const out = []; const n = pts.length; for (let i = 0; i < n; i++) { const p0 = pts[(i - 1 + n) % n], p1 = pts[i], p2 = pts[(i + 1) % n], p3 = pts[(i + 2) % n]; for (let k = 0; k < seg; k++) { const t = k / seg, t2 = t * t, t3 = t2 * t; out.push(new THREE.Vector2(.5 * ((2 * p1.x) + (-p0.x + p2.x) * t + (2 * p0.x - 5 * p1.x + 4 * p2.x - p3.x) * t2 + (-p0.x + 3 * p1.x - 3 * p2.x + p3.x) * t3), .5 * ((2 * p1.y) + (-p0.y + p2.y) * t + (2 * p0.y - 5 * p1.y + 4 * p2.y - p3.y) * t2 + (-p0.y + 3 * p1.y - 3 * p2.y + p3.y) * t3))); } } return out; }
    this.model = model;
    // clear
    for (const l of this.labels) l.element.remove(); this.labels = [];
    while (this.objects.children.length) { const o = this.objects.children[0]; this.objects.remove(o); o.traverse?.(c => { c.geometry?.dispose?.(); if (c.material && !c.material.__shared) c.material.dispose?.(); }); }
    this.hitTargets = [];
    if (this.tip) { this.tip.element.remove(); this.tip = null; }
    if (!model) return;
    const tipEl = document.createElement('div'); tipEl.className = 'map-tip'; tipEl.style.display = 'none'; this.tip = new CSS2DObject(tipEl); this.tip.visible = false; this.tip.position.set(0, 0, 20); this.objects.add(this.tip); this.labels.push(this.tip);
    const T = textures();
    const byId = {}; for (const s of model.systems) byId[s.id] = s;
    // bounds → home camera
    const xs = model.systems.map(s => s.x), ys = model.systems.map(s => -s.y);
    this.fitView = { t: new THREE.Vector3((Math.min(...xs) + Math.max(...xs)) / 2, (Math.min(...ys) + Math.max(...ys)) / 2, 0), d: Math.max((Math.max(...xs) - Math.min(...xs)) * 1.22, (Math.max(...ys) - Math.min(...ys)) * 1.7) + 240 };   // room at the edges for the outermost names
    this.bounds = { minX: Math.min(...xs), maxX: Math.max(...xs), minY: Math.min(...ys), maxY: Math.max(...ys) };
    if (!this.homeSet) {
      // the view opens on your own seat with the Reach running off the edges (ART_DIRECTION: you see a corner, not the galaxy)
      const seat = model.systems.find(s => s.isPlayer) || model.systems[0];
      this.target.set(seat.x, -seat.y, 0); this.distance = Math.min(this.fitView.d, 1050);
      this.seat = { t: this.target.clone(), d: this.distance }; this.homeSet = true; this.updateCamera(true);   // not this.home: that is the method below, and a value of that name hid it, so the seat button did nothing
    }

    // Territory by zoom, as Crusader Kings III does it (the owner: "the ck3 method that shows specific highlighting on certain
    // levels depending on zoom"). Three drawings of the same cells, cross-faded by how far off the camera is:
    //   from far off, the realms: the Reach as one, each nation beyond it as one, a state that has left it as one, and their names;
    //   in the middle distance, the houses: each holder's worlds run together in its colour, a thin line where two holders meet,
    //   a hairline at a region's edge, and the regions' names;
    //   close to, the systems: a faint tint and a fine line round each cell, so the stars and the worlds are what is looked at.
    // Lines are thin and fills are light: the map is the picture and the colour a wash over it. A system nobody holds is a pale
    // ring. Rasterised when the holding changes, not on every model.
    const PADR = 300, rx0 = this.bounds.minX - PADR, ry0 = this.bounds.minY - PADR, rw = (this.bounds.maxX - this.bounds.minX) + 2 * PADR, rh = (this.bounds.maxY - this.bounds.minY) + 2 * PADR;
    // as fine on a large map as on the small one (docs/GALAXY_SIZE.md): about 2.6 map units to a pixel, within what a texture will hold
    const RW = Math.max(1536, Math.min(2560, Math.round(rw / 2.6))), RH = Math.max(64, Math.round(RW * rh / rw));
    const regIndex = {}; (model.regions || []).forEach((r, i) => regIndex[r.id] = i);
    const rgb = h => { const c = new THREE.Color(h || '#8fa8d8'); return [c.r * 255, c.g * 255, c.b * 255]; };
    const myRealm = model.playerRealm || 'reach';
    const sites = model.systems.map(s => ({ x: s.x, y: -s.y, r: regIndex[s.regionId] ?? -1, own: s.ownerId || '', col: rgb(s.accent), mine: !!s.isPlayer, pol: s.realm ?? (s.ownerId ? (s.polity || s.ownerId) : ''), pcol: rgb(s.realmColor || s.polityColor || s.accent) }));
    const sig = [RW, RH, rx0 | 0, ry0 | 0, myRealm].join(',') + '|' + sites.map(t => [t.x | 0, t.y | 0, t.r, t.own, t.col.map(v => v | 0).join('.'), t.mine ? 1 : 0, t.pol, t.pcol.map(v => v | 0).join('.')].join(',')).join(';');
    let layers;
    if (this.terrCache && this.terrCache.sig === sig) { layers = this.terrCache.layers; this.regionCentres = this.terrCache.centres; }
    else {
      const mk = () => { const cv = document.createElement('canvas'); cv.width = RW; cv.height = RH; const ctx = cv.getContext('2d'); const img = ctx.createImageData(RW, RH); return { cv, ctx, img, px: img.data }; };
      const F = mk(), M = mk(), N = mk(); const nReg = (model.regions || []).length; const cx = new Float64Array(nReg), cy = new Float64Array(nReg), cn = new Float64Array(nReg);
      const D0 = 5, D1 = 7, D2 = 12;   // the dark of a line between two holders
      // Each pixel's cell (its nearest system), and how far off the nearest system is that is another system's, another holder's and
      // another realm's: so a border is drawn where the holder or the realm changes, however many cells of the same one lie between.
      // A system reaches only so far, so each is walked over its own patch of the picture and not over all of it.
      const NP = RW * RH, REACH = 244, FAR = 345, upx = rw / RW, upy = rh / RH;
      const near1 = new Int16Array(NP).fill(-1), dist1 = new Float32Array(NP).fill(1e9), dAny = new Float32Array(NP).fill(1e9), dOwn = new Float32Array(NP).fill(1e9), dPol = new Float32Array(NP).fill(1e9), nextReg = new Int16Array(NP).fill(-2);
      const patch = (t, R, fn) => {
        const x0 = Math.max(0, Math.floor((t.x - R - rx0) / upx)), x1 = Math.min(RW - 1, Math.ceil((t.x + R - rx0) / upx));
        const y0 = Math.max(0, Math.floor(RH - (t.y + R - ry0) / upy)), y1 = Math.min(RH - 1, Math.ceil(RH - (t.y - R - ry0) / upy));
        for (let y = y0; y <= y1; y++) { const wy = ry0 + (RH - .5 - y) * upy, dy = t.y - wy; for (let x = x0; x <= x1; x++) { const dx = t.x - (rx0 + (x + .5) * upx); fn(y * RW + x, Math.sqrt(dx * dx + dy * dy)); } }
      };
      sites.forEach((t, i) => patch(t, REACH, (p, d) => { if (d < dist1[p]) { dist1[p] = d; near1[p] = i; } }));
      sites.forEach((t, i) => patch(t, FAR, (p, d) => {
        const b = near1[p]; if (b < 0 || b === i) return; const o = sites[b];
        if (d < dAny[p]) { dAny[p] = d; nextReg[p] = t.r; }
        if (t.own !== o.own && d < dOwn[p]) dOwn[p] = d;
        if (t.pol !== o.pol && d < dPol[p]) dPol[p] = d;
      }));
      const GOLD = [255, 222, 150];
      // a line between two distances in from an edge, soft at both sides by a pixel so it does not stair-step when looked at close to
      const band = (e, a, b) => Math.max(0, Math.min(1, (e - a) / upx + .5)) * Math.max(0, Math.min(1, (b - e) / upx + .5));
      for (let y = 0; y < RH; y++) for (let x = 0; x < RW; x++) {
        const p = y * RW + x, b1 = near1[p]; if (b1 < 0) continue;
        const wx = rx0 + (x + .5) * upx, wy = ry0 + (RH - .5 - y) * upy, dist = dist1[p], t = sites[b1];
        const fade = dist < 236 ? 1 : dist > 244 ? 0 : 1 - (dist - 236) / 8; if (t.r >= 0 && dist < 240) { cx[t.r] += wx; cy[t.r] += wy; cn[t.r]++; }
        if (fade <= 0) continue;
        const o = p * 4, edge = 236 - dist;   // edge: how far inside the open rim of its cell this point lies
        const eAny = Math.min((dAny[p] - dist) / 2, 1e6), eOwn = Math.min((dOwn[p] - dist) / 2, edge), ePol = Math.min((dPol[p] - dist) / 2, edge);   // how far inside its cell, its holder's land and its realm
        const open = dist > 229 ? Math.max(0, 1 - Math.abs(dist - 234) / 4) : 0;   // the open edge of a cell: a fine line
        // the realm's border: a strong line in its colour, a glow inside it, and for a realm that is not the player's a hatching over its land
        const home = t.pol === myRealm, lineCol = home ? [0, 1, 2].map(i => t.pcol[i] + (GOLD[i] - t.pcol[i]) * .55) : t.pcol;
        const hatch = !home && t.pol ? (((wx - wy) % 34 + 34) % 34 < 9 ? 1 : 0) : 0;
        if (t.pol) {   // the realms
          const line = band(ePol, 2.2, 8.2), glow = ePol < 58 ? Math.pow(1 - Math.max(0, ePol - 7.5) / 50.5, 2) : 0, held = t.own ? 1 : .45;
          const k = (home ? .52 : .6) + .12 * hatch + .4 * glow, base = (home ? 54 : 62) * held + 34 * hatch * held + 110 * glow;
          let r = Math.min(255, t.pcol[0] * k), g = Math.min(255, t.pcol[1] * k), bl = Math.min(255, t.pcol[2] * k), a = base;
          if (ePol < 2.2) { const dk = 1 - band(ePol, 2.2, 99); r += (D0 - r) * dk; g += (D1 - g) * dk; bl += (D2 - bl) * dk; a += (150 - a) * dk; }   // a dark hair between two realms' lines
          if (line > 0) { r += (Math.min(255, lineCol[0] * 1.12) - r) * line; g += (Math.min(255, lineCol[1] * 1.12) - g) * line; bl += (Math.min(255, lineCol[2] * 1.12) - bl) * line; a += (238 - a) * line; }
          F.px[o] = r; F.px[o + 1] = g; F.px[o + 2] = bl; F.px[o + 3] = a * fade;
        }
        const realmLine = t.pol ? band(ePol, 1.6, 5.6) : 0, realmGlow = t.pol && ePol < 30 ? Math.pow(1 - Math.max(0, ePol - 5.4) / 24.6, 2) : 0;
        if (t.own) {   // the houses, and the systems
          const glass = eOwn <= 2.2 ? 0 : Math.min(1, (eOwn - 2.2) / 2.2), rim = Math.max(eOwn > 2.2 && eOwn < 6.6 ? 1 - (eOwn - 2.2) / 4.4 : 0, open), k = 0.48 + 0.42 * rim;
          let r = D0 + (Math.min(255, t.col[0] * k) - D0) * glass, g = D1 + (Math.min(255, t.col[1] * k) - D1) * glass, b = D2 + (Math.min(255, t.col[2] * k) - D2) * glass, a = 100 + ((t.mine ? 54 : 40) + 66 * rim + 16 * hatch - 100) * glass;
          if (nextReg[p] >= -1 && nextReg[p] !== t.r && eAny < 2.2) { const w = (1 - eAny / 2.2) * .5; r += (226 - r) * w; g += (214 - g) * w; b += (186 - b) * w; a = Math.max(a, 74 * (1 - eAny / 2.2)); }   // a region's edge: a hairline
          if (realmGlow > 0) { const w = realmGlow * .55; r += (lineCol[0] - r) * w; g += (lineCol[1] - g) * w; b += (lineCol[2] - b) * w; a = Math.max(a, a + 70 * realmGlow); }
          if (realmLine > 0) { r += (Math.min(255, lineCol[0] * 1.08) - r) * realmLine; g += (Math.min(255, lineCol[1] * 1.08) - g) * realmLine; b += (Math.min(255, lineCol[2] * 1.08) - b) * realmLine; a += (226 - a) * realmLine; }
          M.px[o] = r; M.px[o + 1] = g; M.px[o + 2] = b; M.px[o + 3] = Math.min(255, a) * fade;
          const line = Math.max(eAny < 1e5 ? Math.max(0, 1 - eAny / 1.7) : 0, open);
          let nr = Math.min(255, t.col[0] * .95), ng = Math.min(255, t.col[1] * .95), nb = Math.min(255, t.col[2] * .95), na = 11 + 92 * line + 6 * hatch;
          if (realmLine > 0) { nr += (lineCol[0] - nr) * realmLine; ng += (lineCol[1] - ng) * realmLine; nb += (lineCol[2] - nb) * realmLine; na += (190 - na) * realmLine; }   // the realm's border is there at every distance
          N.px[o] = nr; N.px[o + 1] = ng; N.px[o + 2] = nb; N.px[o + 3] = na * fade;
        } else {   // nobody's: a pale ring round the star, and the realm's border where its space ends
          const ring = dist < 54 && dist > 44 ? Math.max(0, 1 - Math.abs(dist - 49) / 3) : 0, disc = dist < 47 ? 12 : 0;
          let r = 150, g = 162, b = 190, a = disc + ring * 76;
          if (realmGlow > 0 && a < 60 * realmGlow) { r = lineCol[0]; g = lineCol[1]; b = lineCol[2]; a = 60 * realmGlow; }
          if (realmLine > 0) { r += (lineCol[0] - r) * realmLine; g += (lineCol[1] - g) * realmLine; b += (lineCol[2] - b) * realmLine; a += (215 - a) * realmLine; }
          if (a > 0) for (const L of [M, N]) { L.px[o] = r; L.px[o + 1] = g; L.px[o + 2] = b; L.px[o + 3] = a * fade; }
        }
      }
      for (const L of [F, M, N]) L.ctx.putImageData(L.img, 0, 0);
      layers = { far: F.cv, mid: M.cv, near: N.cv };
      this.regionCentres = {}; (model.regions || []).forEach((rg, i) => { this.regionCentres[rg.id] = cn[i] > 0 ? { x: cx[i] / cn[i], y: cy[i] / cn[i] } : { x: rg.cx, y: -rg.cy }; });
      this.terrCache = { sig, layers, centres: this.regionCentres };
    }
    this.regionRaster = { canvas: layers.mid, x0: rx0, y0: ry0, w: rw, h: rh };
    this.terrLayers = {};
    for (const [name, z] of [['far', -3], ['mid', -2.9], ['near', -2.8]]) {
      const tex = new THREE.CanvasTexture(layers[name]); tex.colorSpace = THREE.SRGBColorSpace; tex.minFilter = THREE.LinearFilter;
      const mesh = new THREE.Mesh(new THREE.PlaneGeometry(rw, rh), new THREE.MeshBasicMaterial({ map: tex, transparent: true, depthWrite: false, opacity: name === 'mid' ? 1 : 0 })); mesh.position.set(rx0 + rw / 2, ry0 + rh / 2, z); this.objects.add(mesh); this.terrLayers[name] = mesh;
    }
    // the realms' names, for the far view: one over each realm, larger for a larger realm
    this.polities = {}; for (const st of model.systems) { if (!st.ownerId) continue; const key = st.polity || st.ownerId; const pz = this.polities[key] ||= { name: st.polityName || st.ownerName || '', color: st.polityColor || st.accent, x: 0, y: 0, n: 0 }; pz.x += st.x; pz.y += -st.y; pz.n++; }

    // lanes
    const laneGeo = new THREE.BufferGeometry(); const lp = [], lc = [];
    // a lane stops short of the star at each end, so no line is drawn through a sun; one that runs between two systems of the same
    // holder takes some of that holder's colour, so a holding reads as a web and its edge as where the web changes colour
    const GAP = 20, tint = new THREE.Color();
    for (const l of model.lanes) {
      const a = byId[l.a], b = byId[l.b]; if (!a || !b) continue; let c = l.front ? [0.9, 0.3, 0.2] : l.reach ? [0.9, 0.75, 0.4] : [0.55, 0.62, 0.78];
      if (!l.front && !l.reach && a.ownerId && a.ownerId === b.ownerId && a.accent) { tint.set(a.accent); c = [c[0] * .45 + tint.r * .75, c[1] * .45 + tint.g * .75, c[2] * .45 + tint.b * .75]; }
      const dx = b.x - a.x, dy = b.y - a.y, len = Math.hypot(dx, dy) || 1, g2 = Math.min(GAP, len * .3), ux = dx / len * g2, uy = dy / len * g2;
      lp.push(a.x + ux, -(a.y + uy), 0, b.x - ux, -(b.y - uy), 0); lc.push(...c, ...c);
    }
    laneGeo.setAttribute('position', new THREE.Float32BufferAttribute(lp, 3)); laneGeo.setAttribute('color', new THREE.Float32BufferAttribute(lc, 3));
    this.objects.add(new THREE.LineSegments(laneGeo, new THREE.LineBasicMaterial({ vertexColors: true, transparent: true, opacity: 0.36 })));
    // glow: tubes of light under the hairlines
    for (const l of model.lanes) { if (!l.front && !l.reach) continue; const a = byId[l.a], b = byId[l.b]; if (!a || !b) continue; const dx = b.x - a.x, dy = -(b.y - a.y); const len = Math.hypot(dx, dy); const tube = new THREE.Mesh(new THREE.PlaneGeometry(len, l.front ? 7 : 4.5), new THREE.MeshBasicMaterial({ map: T.lane, color: l.front ? 0xff5a3c : l.reach ? 0xe6c77a : 0x6f8ad0, transparent: true, opacity: l.front ? .5 : l.reach ? .6 : .22, blending: THREE.AdditiveBlending, depthWrite: false })); tube.position.set((a.x + b.x) / 2, -(a.y + b.y) / 2, -1); tube.rotation.z = Math.atan2(dy, dx); this.objects.add(tube); }

    // systems
    const sunMat = (color) => new THREE.SpriteMaterial({ map: T.sun, color, transparent: true, blending: THREE.AdditiveBlending, depthWrite: false });
    const ringGeo = new THREE.RingGeometry(1, 1.08, 64); this.suns = []; this.details = []; this.lod = null;
    for (const s of model.systems) {
      const g = new THREE.Group(); g.position.set(s.x, -s.y, 0);
      const h = hash(s.id); const cls = [[0xfff4e0, 1.0], [0xffe2a8, 0.9], [0xffb070, 0.8], [0xbfd4ff, 1.15], [0xffffff, 1.05], [0xffd0a0, 0.85]][h % 6];
      const sun = new THREE.Sprite(sunMat(cls[0])); const sz = (17 + Math.min(9, (s.pop || 0) / 6)) * cls[1]; sun.scale.set(sz, sz, 1); g.add(sun);
      const core = new THREE.Sprite(new THREE.SpriteMaterial({ map: T.glow, color: 0xffffff, transparent: true, blending: THREE.AdditiveBlending, depthWrite: false })); core.scale.set(7 * cls[1], 7 * cls[1], 1); g.add(core);
      const flare = new THREE.Sprite(new THREE.SpriteMaterial({ map: T.flare, color: cls[0], transparent: true, opacity: .3, blending: THREE.AdditiveBlending, depthWrite: false })); flare.scale.set(sz * 2.6, sz * .16, 1); g.add(flare);
      if (s.sunWorks) { const halo = new THREE.Mesh(new THREE.RingGeometry(sz * .46, sz * .5, 48), new THREE.MeshBasicMaterial({ color: 0xffd36a, transparent: true, opacity: .8, side: THREE.DoubleSide, blending: THREE.AdditiveBlending })); halo.userData.spinZ = -0.25; g.add(halo); }   // something is built round the sun itself
      for (let k = 0; k < (s.deepWorks || 0); k++) { const st = new THREE.Sprite(new THREE.SpriteMaterial({ map: T.station, color: 0xffd9a0, transparent: true, depthWrite: false })); st.scale.set(4.4, 4.4, 1); const da = ((h >> 5) % 628) / 100 + k * 1.9; st.position.set(Math.cos(da) * (sz * 1.5 + 30), Math.sin(da) * (sz * 1.5 + 30) * .78, 3); g.add(st); }   // what is built in open space, tied to no world
      if (s.isPlayer) { const home = new THREE.Sprite(new THREE.SpriteMaterial({ map: T.glow, color: 0xffe8a3, transparent: true, opacity: .35, blending: THREE.AdditiveBlending, depthWrite: false })); home.scale.set(sz * 2.2, sz * 2.2, 1); home.userData.pulse = true; g.add(home); }
      // from far off a system is its star and no more: the star is drawn larger as the chart draws back, and what goes round it
      // (orbits, worlds, moons, the lights of what is built) is put away until there is room to see it
      this.suns.push({ parts: [[sun, sz, sz], [core, 7 * cls[1], 7 * cls[1]], [flare, sz * 2.6, sz * .16]] }); const firstBody = g.children.length;
      // the system, not one planet (GDD §19): every body on its own orbit, the primary a lit sphere, the rest sprites
      const bodies = (s.bodies && s.bodies.length) ? s.bodies : [{ name: s.name, cls: s.planetClass, size: 3, primary: true }];
      const orbitMat = new THREE.LineBasicMaterial({ color: 0xc9a24a, transparent: true, opacity: .09 });
      let planet = null; const pr = 5.5 + Math.min(4, (s.pop || 0) / 15);
      // what goes round the sun has an orbit of its own; a moon sits beside its world (docs/SYSTEMS.md). A body with something built on it carries a small gold light.
      const at = {}; let lane = 0; bodies.forEach((b, bi) => { if (b.orbits == null || b.orbits < 0) { const orb = 14 + lane * 10.5, ang = b.ang != null ? b.ang : ((h >> (lane * 3)) % 628) / 100 + lane * 1.7; at[bi] = { orb, x: Math.cos(ang) * orb, y: Math.sin(ang) * orb * .78 }; lane++; } });
      const orbiting = Math.max(1, lane); const moonsOf = {};
      const built = (x, y, z) => { const lamp = new THREE.Sprite(new THREE.SpriteMaterial({ map: T.glow, color: 0xffd36a, transparent: true, opacity: .95, blending: THREE.AdditiveBlending, depthWrite: false })); lamp.scale.set(3.4, 3.4, 1); lamp.position.set(x, y, z); g.add(lamp); };
      bodies.forEach((b, bi) => {
        if (b.orbits != null && b.orbits >= 0 && at[b.orbits]) {   // a moon: close in beside its world, no ring of its own
          const k = moonsOf[b.orbits] = (moonsOf[b.orbits] || 0) + 1, pa = at[b.orbits], ma = ((h >> (bi + 2)) % 628) / 100 + k * 2.3, mr = 5.2 + k * 2.4, mx = pa.x + Math.cos(ma) * mr, my = pa.y + Math.sin(ma) * mr * .78;
          const mtex = window.praetoriaFx ? new THREE.TextureLoader().load(window.praetoriaFx.planet('moon', s.id + b.name, 48)) : null; if (mtex) mtex.colorSpace = THREE.SRGBColorSpace;
          const ms = new THREE.Sprite(new THREE.SpriteMaterial({ map: mtex, color: mtex ? 0xffffff : 0xb0b0ba, transparent: true, depthWrite: false })); ms.scale.set(2.4, 2.4, 1); ms.position.set(mx, my, 4.5); g.add(ms);
          if (b.works) built(mx + 1.6, my + 1.6, 6);
          return;
        }
        const here = at[bi] || { orb: 14, x: 14, y: 0 }; const orb = here.orb, bx = here.x, by = here.y;
        const ring = new THREE.Line(new THREE.BufferGeometry().setFromPoints(Array.from({ length: 65 }, (_, i) => { const a = i / 64 * Math.PI * 2; return new THREE.Vector3(Math.cos(a) * orb, Math.sin(a) * orb * .78, 1); })), orbitMat); g.add(ring);
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
      if (!planet) { planet = new THREE.Mesh(new THREE.SphereGeometry(pr, 16, 12), new THREE.MeshStandardMaterial({ color: planetColor(s.planetClass) })); planet.position.set(15, -10, 5); g.add(planet); }
      if (s.planetClass === 'gas') { const ring = new THREE.Mesh(new THREE.RingGeometry(pr * 1.35, pr * 1.9, 48), new THREE.MeshBasicMaterial({ color: 0xf0d0a0, transparent: true, opacity: 0.5, side: THREE.DoubleSide })); ring.position.copy(planet.position); ring.rotation.x = 1.2; g.add(ring); }
      // atmosphere rim
      const atm = new THREE.Mesh(new THREE.SphereGeometry(pr * 1.12, 24, 18), new THREE.MeshBasicMaterial({ color: planetGlow(s.planetClass), transparent: true, opacity: 0.18, blending: THREE.AdditiveBlending, side: THREE.BackSide, depthWrite: false })); atm.position.copy(planet.position); g.add(atm);
      { const detail = new THREE.Group(); for (const child of g.children.slice(firstBody)) detail.add(child); g.add(detail); this.details.push(detail); }
      if (s.ownerId) { const ring = new THREE.Mesh(ringGeo, new THREE.MeshBasicMaterial({ color: s.accent, transparent: true, opacity: s.isPlayer ? 0.95 : 0.55, blending: THREE.AdditiveBlending, side: THREE.DoubleSide })); ring.scale.set(sz * 0.62, sz * 0.62, 1); g.add(ring); }
      // a world that has changed sides across the rim: a broken ring in the holder's colour, outside the plain one
      if (s.conquered) { for (let k = 0; k < 8; k++) { const seg = new THREE.Mesh(new THREE.RingGeometry(sz * 0.86, sz * 0.94, 10, 1, k * Math.PI / 4, Math.PI / 7), new THREE.MeshBasicMaterial({ color: s.accent, transparent: true, opacity: s.conquered === 'ceded' ? 0.7 : 1, side: THREE.DoubleSide })); g.add(seg); } }
      if (s.bloc) { const bl = new THREE.Mesh(new THREE.RingGeometry(sz * 0.78, sz * 0.8, 64), new THREE.MeshBasicMaterial({ color: s.bloc === 'hegemon' ? 0xe6c77a : 0x7aa2ff, transparent: true, opacity: 0.6, side: THREE.DoubleSide })); g.add(bl); }
      if (s.siege != null) { const arc = new THREE.Mesh(new THREE.RingGeometry(sz * 0.7, sz * 0.76, 48, 1, Math.PI / 2, -Math.PI * 2 * Math.max(0.02, s.siege)), new THREE.MeshBasicMaterial({ color: 0xff5a3c, transparent: true, opacity: 0.95, side: THREE.DoubleSide, blending: THREE.AdditiveBlending })); g.add(arc); }
      if (s.contested) { const c = new THREE.Mesh(new THREE.RingGeometry(sz * 0.5, sz * 0.52, 48), new THREE.MeshBasicMaterial({ color: 0xff6a4a, transparent: true, opacity: 0.8, side: THREE.DoubleSide })); g.add(c); }
      if (s.rebel) { const rb = new THREE.Sprite(new THREE.SpriteMaterial({ map: T.glow, color: 0xff4a2a, transparent: true, opacity: .6, blending: THREE.AdditiveBlending, depthWrite: false })); rb.scale.set(sz * 1.6, sz * 1.6, 1); rb.userData.pulse = true; g.add(rb); }
      if (s.chokepoint) { const ck = new THREE.Mesh(new THREE.RingGeometry(sz * 1.3, sz * 1.34, 4), new THREE.MeshBasicMaterial({ color: 0xe6c77a, transparent: true, opacity: 0.55, side: THREE.DoubleSide })); ck.rotation.z = Math.PI / 4; g.add(ck); }
      if (s.capital) { const cap = new THREE.Mesh(new THREE.RingGeometry(sz * 1.0, sz * 1.04, 64), new THREE.MeshBasicMaterial({ color: 0xffe8a3, transparent: true, opacity: 0.7, side: THREE.DoubleSide, blending: THREE.AdditiveBlending })); g.add(cap); const cap2 = new THREE.Mesh(new THREE.RingGeometry(sz * 1.1, sz * 1.115, 64), new THREE.MeshBasicMaterial({ color: 0xffe8a3, transparent: true, opacity: 0.35, side: THREE.DoubleSide })); g.add(cap2); }
      if (s.target) { const tr = new THREE.Mesh(new THREE.RingGeometry(sz * 0.9, sz * 0.93, 64), new THREE.MeshBasicMaterial({ color: 0xc9a24a, transparent: true, opacity: s.target === 1 ? 0.95 : 0.4, side: THREE.DoubleSide })); tr.userData.pulse = true; g.add(tr); }
      if (s.id === this.selected.systemId) { const sel = new THREE.Mesh(new THREE.RingGeometry(sz * 0.56, sz * 0.6, 64), new THREE.MeshBasicMaterial({ color: 0xffe8a3, transparent: true, opacity: 0.95, side: THREE.DoubleSide, blending: THREE.AdditiveBlending })); sel.userData.spinZ = 0.6; g.add(sel); const sel2 = new THREE.Mesh(new THREE.RingGeometry(sz * 0.66, sz * 0.67, 64), new THREE.MeshBasicMaterial({ color: 0xffe8a3, transparent: true, opacity: 0.5, side: THREE.DoubleSide })); g.add(sel2); }
      for (let k = 0; k < (s.stations || 0); k++) { const st = new THREE.Sprite(new THREE.SpriteMaterial({ map: T.station, color: 0xc9bca8, transparent: true, depthWrite: false })); st.scale.set(5, 5, 1); st.position.set(-12 - k * 6, 10, 3); g.add(st); }
      const hit = new THREE.Mesh(new THREE.SphereGeometry(Math.max(sz * 0.55, 14 + orbiting * 10.5), 8, 8), new THREE.MeshBasicMaterial({ visible: false })); hit.userData = { kind: 'system', id: s.id }; g.add(hit); this.hitTargets.push(hit);
      // label
      const el = document.createElement('div'); el.className = 'sys-label' + (s.isPlayer ? ' mine' : ''); el.innerHTML = `<span class="n">${s.capital ? '♛ ' : ''}${esc(s.name)}</span>${s.ownerName ? `<span class="o" style="color:${s.accent}">${esc(s.ownerName)}</span>` : ''}${s.target ? `<span class="j">${s.target}j</span>` : ''}${s.deposits ? `<span class="d">${esc(s.deposits)}</span>` : ''}`;
      const lab = new CSS2DObject(el); lab.position.set(0, -(14 + orbiting * 10.5) * .78 - 8, 0); g.add(lab); this.labels.push(lab);
      this.objects.add(g);
    }
    for (const key of Object.keys(this.polities)) { const pz = this.polities[key]; if (!pz.name) continue; const el = document.createElement('div'); el.className = 'polity-label'; el.textContent = pz.name; el.style.color = pz.color; el.style.fontSize = Math.round(Math.min(30, 14 + pz.n * 0.4)) + 'px'; const lab = new CSS2DObject(el); lab.position.set(pz.x / pz.n, pz.y / pz.n, 6); this.objects.add(lab); this.labels.push(lab); }
    for (const r of model.regions || []) { const el = document.createElement('div'); el.className = 'region-label ring-' + (r.ring || 'middle') + (r.free || r.state ? ' free' : ''); el.innerHTML = `<span class="rn">${esc(r.name)}</span>${r.state ? `<span class="rs state">${esc(r.state)}${r.stateWar ? ' · at war for it' : ' · no longer of the Reach'}</span>` : ''}<span class="rs">${esc(r.ring || '')}${r.free ? ' · free march' : ''} · ${esc(r.dominant || 'no one')} · ⚜ ${r.allegiance} · ☠ ${r.lawlessness}</span>`; const lab = new CSS2DObject(el); const rc = this.regionCentres[r.id] || { x: r.cx, y: -r.cy }; lab.position.set(rc.x, rc.y, 0); this.objects.add(lab); this.labels.push(lab); }
    // the route a selected fleet would take to the selected system, lane by lane
    if (model.route && model.route.length > 1) { const pts = model.route.map(id => byId[id]).filter(Boolean).map(s => new THREE.Vector3(s.x, -s.y, 6)); const rg = new THREE.BufferGeometry().setFromPoints(pts); const rl = new THREE.Line(rg, new THREE.LineDashedMaterial({ color: 0xffe8a3, dashSize: 10, gapSize: 7, transparent: true, opacity: .9 })); rl.computeLineDistances(); this.objects.add(rl); }
    // fleets
    const groups = {}; for (const f of model.fleets) (groups[f.systemId] ||= []).push(f);
    for (const [sid, list] of Object.entries(groups)) {
      const s = byId[sid]; if (!s) continue; let i = 0; const n = list.length;
      for (const f of list) { const ang = -Math.PI / 2 - 0.6 + i * (Math.PI * 2 / Math.max(4, n)); i++; const fx = s.x + Math.cos(ang) * 30, fy = -s.y - Math.sin(ang) * 30;
        const pirate = f.ownerId === 'pirates'; const spr = new THREE.Sprite(new THREE.SpriteMaterial({ map: pirate ? T.skull : T.ship, color: pirate ? 0xff4a3a : f.accent, transparent: true, opacity: f.isPlayer ? 1 : pirate ? .95 : 0.35 + 0.65 * (f.confidence || 0) / 100, depthWrite: false })); const sc = f.isPlayer ? 13 : pirate ? 12 : 10; spr.scale.set(sc, sc, 1); spr.position.set(fx, fy, 6); this.objects.add(spr);
        const glow = new THREE.Sprite(new THREE.SpriteMaterial({ map: T.glow, color: f.accent, transparent: true, opacity: f.isPlayer ? 0.7 : 0.3, blending: THREE.AdditiveBlending, depthWrite: false })); glow.scale.set(sc * 2.2, sc * 2.2, 1); glow.position.set(fx, fy, 5); glow.userData.pulse = true; this.objects.add(glow);
        if (f.id === this.selected.fleetId) { const sel = new THREE.Mesh(new THREE.RingGeometry(sc * 0.9, sc * 1.0, 48), new THREE.MeshBasicMaterial({ color: 0xffe8a3, transparent: true, opacity: 0.95, side: THREE.DoubleSide })); sel.position.set(fx, fy, 5.5); this.objects.add(sel); }
        if (f.moving) { const trail = new THREE.Sprite(new THREE.SpriteMaterial({ map: T.flare, color: f.accent, transparent: true, opacity: .7, blending: THREE.AdditiveBlending, depthWrite: false })); trail.scale.set(sc * 2.4, sc * .25, 1); trail.position.set(fx - sc * .9, fy - sc * .3, 5.5); this.objects.add(trail); }
        if (f.moving && byId[f.destId]) { const d = byId[f.destId]; const lg = new THREE.BufferGeometry().setFromPoints([new THREE.Vector3(fx, fy, 5), new THREE.Vector3(d.x, -d.y, 5)]); this.objects.add(new THREE.Line(lg, new THREE.LineDashedMaterial({ color: 0xe6c77a, dashSize: 8, gapSize: 6, transparent: true, opacity: 0.8 }))); this.objects.children[this.objects.children.length - 1].computeLineDistances(); }
        const hit = new THREE.Mesh(new THREE.SphereGeometry(sc * 0.8, 8, 8), new THREE.MeshBasicMaterial({ visible: false })); hit.position.set(fx, fy, 6); hit.userData = { kind: 'fleet', id: f.id, systemId: sid }; this.objects.add(hit); this.hitTargets.push(hit);
        const el = document.createElement('div'); el.className = 'fleet-label'; el.style.color = f.accent; el.textContent = `${f.strength}${f.isPlayer ? '' : '?'}`; const lab = new CSS2DObject(el); lab.position.set(fx + sc * 0.9, fy + 2, 6); this.objects.add(lab); this.labels.push(lab);
      }
    }
    this.applyMode(); this.dirty = true;
  }

  setSelection(sel) { this.selected = sel || { systemId: null, fleetId: null }; }

  // ---- camera ----
  updateCamera(immediate) {
    if (this.tip) { this.tip.visible = false; this.tip.element.style.display = 'none'; }   // a note on a star belongs to the view it was asked in: when the chart moves it goes
    if (immediate || !this.cur) { this.cur = { x: this.target.x, y: this.target.y, d: this.distance }; }
    this.applyCamera();
  }
  applyCamera() {
    const c = this.cur || (this.cur = { x: this.target.x, y: this.target.y, d: this.distance });
    c.x += (this.target.x - c.x) * 0.18; c.y += (this.target.y - c.y) * 0.18; c.d += (this.distance - c.d) * 0.18;
    const tilt = this.tilt * (0.5 + 0.5 * Math.min(1, c.d / 1200));   // level out as you close in
    this.camera.position.set(c.x, c.y - c.d * Math.sin(tilt), c.d * Math.cos(tilt)); this.camera.lookAt(c.x, c.y, 0); this.camera.updateProjectionMatrix(); this.dirty = true;
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
      this.distance = Math.max(160, Math.min(6000, this.distance * f)); this.updateCamera();
    }
    if (before) { const after = this.planePoint(px, py); if (after) { this.target.x += before.x - after.x; this.target.y += before.y - after.y; this.updateCamera(); } }
  }
  home() { if (this.sys) { this.fit(); return; } if (this.seat) { this.target.copy(this.seat.t); this.distance = this.seat.d; this.updateCamera(); } }
  // the whole map, whatever the shape of the panel: a narrow panel stands further back so the outermost systems (the nations beyond the rim) are in it
  fit() { if (this.sys) { this.focused = null; this.sysFitD = this.sysFit(); return; } if (!this.fitView) return; const b = this.bounds, a = this.camera.aspect || 1.3; this.target.copy(this.fitView.t); this.distance = b ? Math.max((b.maxX - b.minX) * 1.22 * Math.max(1, 1.3 / a), (b.maxY - b.minY) * 1.7) + 240 : this.fitView.d; this.updateCamera(); }
  // the minimap: the whole Reach in a corner, the viewport drawn on it, click to travel
  setMinimap(canvas) { this.mini = canvas; if (!canvas) return; canvas.addEventListener('pointerdown', e => { if (!this.bounds) return; const r = canvas.getBoundingClientRect(); const m = this.miniMap(); const x = (e.clientX - r.left) / r.width * canvas.width, y = (e.clientY - r.top) / r.height * canvas.height; this.target.set(m.minX + (x - m.ox) / m.k, m.maxY - (y - m.oy) / m.k, 0); this.updateCamera(); }); }
  miniMap() { const b = this.bounds, pad = 70; const w = this.mini.width, h = this.mini.height; const kx = (w - 12) / (b.maxX - b.minX + pad * 2), ky = (h - 12) / (b.maxY - b.minY + pad * 2); const k = Math.min(kx, ky); const ox = (w - (b.maxX - b.minX + pad * 2) * k) / 2 + pad * k, oy = (h - (b.maxY - b.minY + pad * 2) * k) / 2 + pad * k; return { k, ox, oy, minX: b.minX, maxY: b.maxY }; }
  declutter() {
    // Nothing on the map is written over anything else (the owner: "overlapping words and things on the map"). A name that would
    // sit on another name, or under the bar, the window, the outliner, the legend or the minimap, is not shown. Seen from far off
    // the regions' names are placed first and the systems' fit round them; close to, the systems' names come first, your own before
    // others'. A region's name looks for room near its centre and is left out if there is none.
    const els = this.labels.map(l => l.element); const live = e => e.style.display !== 'none' && getComputedStyle(e).display !== 'none';
    const rect = (e, dx = 0, dy = 0) => { const r = e.getBoundingClientRect(); return { x: r.left - dx, y: r.top - dy, w: r.width, h: r.height }; };
    const hits = (a, b, pad) => a.x < b.x + b.w + pad && b.x < a.x + a.w + pad && a.y < b.y + b.h + pad && b.y < a.y + a.h + pad;
    const hud = [...document.querySelectorAll('.topbar, .rail, .outliner, .legend, .minimap, .map-tools, .endturn-dock, .screen.window, .map-side')].map(e => rect(e)).filter(b => b.w > 0 && b.h > 0);
    const view = this.renderer.domElement.getBoundingClientRect(); const far = this.labelRenderer.domElement.classList.contains('far');
    const off = b => b.x < view.left + 4 || b.y < view.top + 4 || b.x + b.w > view.right - 4 || b.y + b.h > view.bottom - 4;
    const placed = [];
    // where the stars themselves are drawn: a region's name keeps off them too, if it can (a name sits just under its star)
    const g = far ? 12 : this.labelRenderer.domElement.classList.contains('near') ? 46 : 28;
    const stars = els.filter(e => e.classList.contains('sys-label') && live(e)).map(e => rect(e)).filter(b => b.w > 0).map(b => ({ x: b.x + b.w / 2 - g, y: b.y - 2 * g - 4, w: 2 * g, h: 2 * g + 4 }));
    const points = () => {   // the names of systems and fleets: shown where they stand, or not at all
      const pts = els.filter(e => e.classList.contains('sys-label') || e.classList.contains('fleet-label'));
      pts.sort((a, b) => (b.classList.contains('mine') ? 2 : 0) + (b.classList.contains('sys-label') ? 1 : 0) - (a.classList.contains('mine') ? 2 : 0) - (a.classList.contains('sys-label') ? 1 : 0));
      for (const e of pts) { if (!live(e)) continue; const b = rect(e); const bad = b.w === 0 || hud.some(h => hits(b, h, 6)) || placed.some(q => hits(b, q, 3)); e.style.visibility = bad ? 'hidden' : ''; if (!bad) placed.push(b); }
    };
    const regions = () => {   // a region's name: the nearest free place to its centre, or not at all
      const step = 18, cands = [[0, 0]]; for (let k = 1; k <= 5; k++) { cands.push([0, -k * step], [0, k * step]); if (k <= 3) cands.push([-k * 70, 0], [k * 70, 0], [-k * 70, -k * step], [k * 70, -k * step], [-k * 70, k * step], [k * 70, k * step]); }
      cands.sort((a, b) => Math.abs(a[0]) + Math.abs(a[1]) * 1.5 - Math.abs(b[0]) - Math.abs(b[1]) * 1.5);
      const regs = els.filter(e => (e.classList.contains('region-label') || e.classList.contains('polity-label')) && live(e)).map(e => ({ e, b: rect(e, parseFloat(e.style.marginLeft) || 0, parseFloat(e.style.marginTop) || 0) })).filter(o => o.b.w > 0).sort((a, b) => a.b.y - b.b.y);
      for (const r of regs) {
        let at = null; const big = r.e.classList.contains('polity-label');   // a realm's name wants air round it, and may lie over stars
        for (const strict of [true, false]) {   // first somewhere clear of the stars as well; failing that, clear of every other name
          for (const [dx, dy] of cands) { const b = { x: r.b.x + dx, y: r.b.y + dy, w: r.b.w, h: r.b.h }; if (off(b) || hud.some(h => hits(b, h, 8)) || placed.some(q => hits(b, q, big ? 26 : 5)) || (strict && !big && stars.some(q => hits(b, q, 0)))) continue; at = { dx, dy, b }; break; }
          if (at) break;
        }
        if (at) { placed.push(at.b); r.e.style.marginLeft = at.dx ? at.dx + 'px' : ''; r.e.style.marginTop = at.dy ? at.dy + 'px' : ''; r.e.style.visibility = ''; } else r.e.style.visibility = 'hidden';
      }
    };
    if (far) { regions(); points(); } else { points(); regions(); }
  }
  drawMinimap() {
    const c = this.mini; if (!c || !this.model || !this.bounds) return; const ctx = c.getContext('2d'); const m = this.miniMap(); const P = (x, y) => [m.ox + (x - m.minX) * m.k, m.oy + (m.maxY - y) * m.k];
    ctx.clearRect(0, 0, c.width, c.height);
    if (this.regionRaster) { const rr = this.regionRaster; const [ax, ay] = P(rr.x0, rr.y0 + rr.h); ctx.globalAlpha = .9; ctx.drawImage(rr.canvas, ax, ay, rr.w * m.k, rr.h * m.k); ctx.globalAlpha = 1; }
    for (const l of this.model.lanes) { const a = this.model.systems.find(s => s.id === l.a), b = this.model.systems.find(s => s.id === l.b); if (!a || !b) continue; const [ax, ay] = P(a.x, -a.y), [bx, by] = P(b.x, -b.y); ctx.strokeStyle = l.front ? 'rgba(255,90,60,.6)' : 'rgba(160,180,220,.16)'; ctx.lineWidth = 1; ctx.beginPath(); ctx.moveTo(ax, ay); ctx.lineTo(bx, by); ctx.stroke(); }
    for (const s of this.model.systems) { const [x, y] = P(s.x, -s.y); ctx.fillStyle = s.isPlayer ? '#ffe8a3' : s.ownerId ? s.accent : '#6a7384'; ctx.beginPath(); ctx.arc(x, y, s.isPlayer ? 2.6 : 1.8, 0, Math.PI * 2); ctx.fill(); if (s.capital) { ctx.strokeStyle = '#ffe8a3'; ctx.beginPath(); ctx.arc(x, y, 4, 0, Math.PI * 2); ctx.stroke(); } }
    // the viewport: sample the map plane on a grid and frame whatever the camera can see (robust to the tilt at any distance)
    const b = this.bounds, pad = 140; let vx0 = Infinity, vy0 = Infinity, vx1 = -Infinity, vy1 = -Infinity; const v = new THREE.Vector3();
    for (let i = 0; i <= 40; i++) for (let j = 0; j <= 24; j++) { const x = b.minX - pad + (b.maxX - b.minX + pad * 2) * i / 40, y = b.minY - pad + (b.maxY - b.minY + pad * 2) * j / 24; v.set(x, y, 0).project(this.camera); if (v.z < 1 && Math.abs(v.x) <= 1 && Math.abs(v.y) <= 1) { vx0 = Math.min(vx0, x); vx1 = Math.max(vx1, x); vy0 = Math.min(vy0, y); vy1 = Math.max(vy1, y); } }
    if (vx0 < vx1) { const [ax, ay] = P(vx0, vy1), [bx, by] = P(vx1, vy0); ctx.strokeStyle = 'rgba(201,162,74,.9)'; ctx.lineWidth = 1; ctx.strokeRect(ax, ay, bx - ax, by - ay); }
  }
  focus(x, y) { this.target.set(x, -y, 0); this.distance = Math.min(this.distance, 900); this.updateCamera(); }
  planePoint(px, py) {
    const rect = this.renderer.domElement.getBoundingClientRect(); this.mouse.set(((px - rect.left) / rect.width) * 2 - 1, -((py - rect.top) / rect.height) * 2 + 1);
    this.raycaster.setFromCamera(this.mouse, this.camera); const pt = new THREE.Vector3(); return this.raycaster.ray.intersectPlane(new THREE.Plane(new THREE.Vector3(0, 0, 1), 0), pt) ? pt : null;
  }
  bindInput() {
    const el = this.renderer.domElement; let down = null, moved = false;
    el.addEventListener('pointerdown', e => { down = { x: e.clientX, y: e.clientY, pt: this.planePoint(e.clientX, e.clientY) }; moved = false; el.setPointerCapture(e.pointerId); });
    el.addEventListener('pointermove', e => {
      if (down) { const pt = this.planePoint(e.clientX, e.clientY); if (Math.abs(e.clientX - down.x) + Math.abs(e.clientY - down.y) > 3) moved = true; if (pt && down.pt && moved) { this.target.x -= pt.x - down.pt.x; this.target.y -= pt.y - down.pt.y; this.updateCamera(); } el.style.cursor = 'grabbing'; return; }
      const hit = this.pick(e.clientX, e.clientY); el.style.cursor = hit ? 'pointer' : 'grab'; this.showTip(hit);
    });
    el.addEventListener('pointerleave', () => { if (!down) this.showTip(null); });   // onto a window, the bar or the list: the note does not stay behind
    el.addEventListener('pointerup', e => { if (down && !moved) { const hit = this.pick(e.clientX, e.clientY); if (hit) this.dotnet?.invokeMethodAsync('OnPick', hit.kind, hit.id); else this.dotnet?.invokeMethodAsync('OnPick', 'none', ''); } down = null; el.style.cursor = 'grab'; });
    el.addEventListener('wheel', e => { e.preventDefault(); this.zoom(e.deltaY > 0 ? 1.15 : 0.87, e.clientX, e.clientY); }, { passive: false });
    // twice on a system goes into it; twice on a world, once inside, goes to the world
    el.addEventListener('dblclick', e => { const hit = this.pick(e.clientX, e.clientY); if (!hit) return; if (!this.sys && hit.kind === 'system') this.dotnet?.invokeMethodAsync('OnPick', 'enter', hit.id); else if (this.sys && hit.kind === 'body') this.dotnet?.invokeMethodAsync('OnPick', 'world', hit.id); });
  }
  showTip(hit) {
    // the label renderer shows whatever is visible on every frame, so a note is put away by its own flag and not by its style
    if (this.sys) { const t = this.sysTip; if (!t) return; if (!hit || !hit.tip) { t.visible = false; t.element.style.display = 'none'; return; } t.visible = true; t.element.innerHTML = hit.tip; t.position.set(hit.at[0], hit.at[1] + (hit.up || 16), 20); return; }
    if (!this.tip || !this.model) return;
    if (!hit) { this.tip.visible = false; this.tip.element.style.display = 'none'; return; }
    const el = this.tip.element; this.tip.visible = true;
    if (hit.kind === 'system') { const s = this.model.systems.find(x => x.id === hit.id); if (!s) return; this.tip.position.set(s.x, -s.y - 30, 20); el.innerHTML = `<b>${esc(s.name)}</b><div>${esc(s.planetClass)} world · ${esc(s.ownerName || 'unclaimed')}${s.governor ? ` · gov. ${esc(s.governor)}` : ''}</div>${s.pop ? `<div>${esc(s.citizens || ('scale ' + s.pop))} citizens · loyalty ${100 - s.unrest}</div>` : ''}${s.industry ? `<div>${esc(s.industry)}</div>` : ''}${s.great ? `<div>★ ${esc(s.great)}</div>` : ''}`; }
    else { const f = this.model.fleets.find(x => x.id === hit.id); const s = f && this.model.systems.find(x => x.id === f.systemId); if (!f || !s) return; this.tip.position.set(s.x, -s.y - 30, 20); el.innerHTML = `<b>${esc(f.name)}</b><div>${esc(f.ownerName || '')} · ${f.strength} hulls${f.isPlayer ? ` · readiness ${f.readiness}` : ` · intel ${f.confidence}%`}</div>`; }
  }
  pick(px, py) {
    const rect = this.renderer.domElement.getBoundingClientRect(); this.mouse.set(((px - rect.left) / rect.width) * 2 - 1, -((py - rect.top) / rect.height) * 2 + 1);
    this.raycaster.setFromCamera(this.mouse, this.camera); const hits = this.raycaster.intersectObjects(this.sys ? this.sysHits : this.hitTargets, false); return hits.length ? hits[0].object.userData : null;
  }
  resize() { const w = this.container.clientWidth, h = this.container.clientHeight; if (!w || !h) return; this.renderer.setSize(w, h); this.labelRenderer.setSize(w, h); this.composer.setSize(w, h); this.camera.aspect = w / h; this.camera.updateProjectionMatrix(); this.dirty = true; }
  loop() {
    if (this.disposed) return;
    // a frame budget: about 20 fps, nothing while the tab is hidden — the map is a chart, not a shooter
    setTimeout(() => requestAnimationFrame(this.loop), 50);
    if (document.hidden) return;
    if (this.lost) { if (performance.now() - this.lostAt > 2500 && this.onGiveUp) { const again = this.onGiveUp; this.onGiveUp = null; again(); } return; }
    const t = this.clock.getElapsedTime(); this.nebula.material.uniforms.uTime.value = t; this.stars.material.uniforms.uTime.value = t; this.applyCamera();
    if (this.sys) { this.tickSystem(t); this.composer.render(); this.labelRenderer.render(this.scene, this.camera); return; }
    this.objects.traverse(o => { if (o.userData.spin) o.rotation.y = t * o.userData.spin; if (o.userData.spinZ) o.rotation.z = t * o.userData.spinZ; });
    this.objects.traverse(o => { if (o.userData.pulse) { const s = 1 + 0.08 * Math.sin(t * 3 + o.position.x * 0.01); o.scale.setScalar ? (o.isSprite ? null : o.scale.set(s, s, 1)) : null; if (o.material?.opacity != null && o.isSprite) o.material.opacity = 0.45 + 0.25 * Math.sin(t * 2.5); } });
    // what a system shows changes with distance: its star alone, larger, from far off; its worlds and orbits from the middle distance in
    { const d = this.cur ? this.cur.d : this.distance, show = d < 2050, grow = Math.round(Math.max(1, Math.min(2.3, d / 1500)) * 20) / 20, key = (show ? 'a' : 'b') + grow;
      if (key !== this.lod && this.suns) { this.lod = key; for (const dg of this.details) dg.visible = show; for (const sn of this.suns) for (const [sp, sx, sy] of sn.parts) sp.scale.set(sx * grow, sy * grow, 1); } }
    // labels fade with distance
    const lr = this.labelRenderer.domElement; lr.classList.toggle('near', this.distance < 700); lr.classList.toggle('far', this.distance > 1700); lr.classList.toggle('veryfar', this.distance > 3200); lr.classList.toggle('realm', this.distance > 2200);
    // which drawing of the territory shows: the systems close to, the houses in the middle distance, the realms from far off
    if (this.terrLayers && this.terrLayers.mid) { const ss = (a, b, v) => { const q = Math.max(0, Math.min(1, (v - a) / (b - a))); return q * q * (3 - 2 * q); }; const far = ss(1900, 2500, this.distance), near = 1 - ss(620, 900, this.distance);
      this.terrLayers.far.material.opacity = far; this.terrLayers.near.material.opacity = near; this.terrLayers.mid.material.opacity = Math.max(0, 1 - far - near); }
    this.composer.render(); this.labelRenderer.render(this.scene, this.camera);
    if ((this.miniTick = (this.miniTick || 0) + 1) % 3 === 0) { this.drawMinimap(); this.declutter(); }
  }
  // ---------------- the close map of one solar system (docs/SYSTEMS.md, "The close map") ----------------
  // The Reach is hidden and the system is drawn where it lies, large: its sun, each thing on its own orbit where it is this year,
  // the belt, the gates where its lanes leave toward the neighbours they lead to, and who is in it. The chart comes from the game
  // (SystemCharts.For); nothing about where a thing is is decided here.
  applyMode() {
    const inside = !!this.sys; this.objects.visible = !inside; for (const l of this.labels) l.visible = !inside; if (inside && this.tip) this.tip.visible = false;
    if (this.sysGroup) this.sysGroup.visible = inside;
    this.labelRenderer.domElement.classList.toggle('in-system', inside);
  }
  // how much of the map lies under the window at the left, the list at the right and the bar on top: what is looked at is put in the middle of the rest
  insets() {
    const host = this.renderer.domElement.getBoundingClientRect(); let L = 0, R = 0, T = 0;
    const shown = e => e && getComputedStyle(e).display !== 'none' && e.getBoundingClientRect().width > 0;
    const side = document.querySelector('.map-side:not(.idle)'), rail = document.querySelector('.rail'), out = document.querySelector('.outliner'), bar = document.querySelector('.topbar');
    if (shown(side)) L = side.getBoundingClientRect().right - host.left; else if (shown(rail)) L = rail.getBoundingClientRect().right - host.left;
    if (shown(out)) R = host.right - out.getBoundingClientRect().left; if (shown(bar)) T = bar.getBoundingClientRect().bottom - host.top;
    return { L: Math.max(0, L), R: Math.max(0, R), T: Math.max(0, T), W: host.width || 800, H: host.height || 500 };
  }
  // put a circle of the map (its centre and its radius) in the middle of what can be seen, as large as will fit
  frame(cx, cy, radius) {
    const i = this.insets(), tanH = Math.tan(THREE.MathUtils.degToRad(this.camera.fov / 2)), visW = Math.max(240, i.W - i.L - i.R), visH = Math.max(240, i.H - i.T);
    const d = Math.max(radius * i.H / (visW * tanH), radius * i.H / (visH * tanH) * 1.16), wpp = 2 * d * tanH / i.H;
    this.frameOff = { x: -(i.L - i.R) / 2 * wpp, y: i.T / 2 * wpp }; this.target.set(cx + this.frameOff.x, cy + this.frameOff.y, 0); this.distance = d; this.updateCamera(); return d;
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

  dispose() { this.disposed = true; this.resizeObserver.disconnect(); try { this.clearSystem(); this.setModel(null); this.renderer.dispose(); } catch { /* a renderer whose context is gone may not tidy up cleanly */ } this.renderer.domElement.remove(); this.labelRenderer.domElement.remove(); }
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
    g.rebuilt = tries; if (tries < 4) g.onGiveUp = () => this.rebuild(id); this.galaxies.set(id, g);
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
  // pick a system as a click on it would, and look at it close to: for the keyboard, and for the scripts in tools/browser (the first map when no id is given)
  select(systemId, id) { const g = id ? this.galaxies.get(id) : [...this.galaxies.values()][0]; if (!g || !g.model) return false; const s = g.model.systems.find(x => x.id === systemId || x.name === systemId); if (!s) return false; g.dotnet?.invokeMethodAsync('OnPick', 'system', s.id); g.focus(s.x, s.y); return s.id; },
  destroy(id) { const g = this.galaxies.get(id); if (g) { g.dispose(); this.galaxies.delete(id); } }
};
