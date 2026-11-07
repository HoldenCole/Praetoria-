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
  TEX.ship = glyphTexture(ctx => { ctx.fillStyle = '#fff'; ctx.beginPath(); ctx.moveTo(32, 4); ctx.lineTo(56, 56); ctx.lineTo(32, 44); ctx.lineTo(8, 56); ctx.closePath(); ctx.fill(); });
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
  vec3 col = uA * cloud * 1.35 + uB * wisps * 1.6 + uC * pow(cloud, 3.0) * 1.4;
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
  for (int i = 0; i < ${MAX_SYS}; i++) { if (i >= uCount) break; int o = uOwner[i]; if (o < 0) continue; float d = distance(p, uPos[i].xy); float w = exp(-(d*d) / (uRadius*uRadius)) * uPos[i].z; hw[o] += w; total += w; }
  int best = -1; float bw = 0.0, second = 0.0;
  for (int h = 0; h < ${MAX_HOUSES}; h++) { if (hw[h] > bw) { second = bw; bw = hw[h]; best = h; } else if (hw[h] > second) second = hw[h]; }
  if (best < 0) discard;
  vec3 col = uHouseColor[best];
  float field = bw;
  float inside = smoothstep(0.2, 0.45, field);
  float edge = smoothstep(0.19, 0.225, field) * (1.0 - smoothstep(0.225, 0.275, field));   // the glowing border band
  float seam = 1.0 - smoothstep(0.0, 0.06, bw - second);                                    // where two houses meet
  float alpha = inside * 0.075 + edge * 0.42;
  alpha *= (1.0 - seam * 0.7);
  vec3 c = mix(col * 0.8, col * 1.7, edge);
  if (best == uPlayer) alpha *= 1.3;
  gl_FragColor = vec4(c, alpha);
}`;

export class Galaxy {
  constructor(container, dotnet) {
    this.container = container; this.dotnet = dotnet; this.model = null; this.objects = new THREE.Group(); this.labels = [];
    this.hoverId = null; this.selected = { systemId: null, fleetId: null }; this.dirty = true; this.disposed = false;
    const w = container.clientWidth || 800, h = container.clientHeight || 500;
    this.renderer = new THREE.WebGLRenderer({ antialias: true, alpha: false, powerPreference: 'high-performance' });
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 1.5)); this.renderer.setSize(w, h); this.renderer.toneMapping = THREE.ACESFilmicToneMapping; this.renderer.toneMappingExposure = 1.1;
    this.renderer.domElement.className = 'gl'; container.appendChild(this.renderer.domElement);
    this.labelRenderer = new CSS2DRenderer(); this.labelRenderer.setSize(w, h); this.labelRenderer.domElement.className = 'gl-labels'; container.appendChild(this.labelRenderer.domElement);
    this.scene = new THREE.Scene(); this.scene.background = new THREE.Color(0x02030a);
    this.camera = new THREE.PerspectiveCamera(42, w / h, 1, 20000);
    this.target = new THREE.Vector3(600, -350, 0); this.distance = 1000; this.tilt = 0.42;
    this.composer = new EffectComposer(this.renderer); this.composer.addPass(new RenderPass(this.scene, this.camera));
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
      uniforms: { uTime: { value: 0 }, uA: { value: new THREE.Color(0x24357a) }, uB: { value: new THREE.Color(0x6a3a86) }, uC: { value: new THREE.Color(0x2a7a8a) } } }));
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
    this.model = model;
    // clear
    for (const l of this.labels) l.element.remove(); this.labels = [];
    while (this.objects.children.length) { const o = this.objects.children[0]; this.objects.remove(o); o.traverse?.(c => { c.geometry?.dispose?.(); if (c.material && !c.material.__shared) c.material.dispose?.(); }); }
    this.hitTargets = [];
    if (!model) return;
    const T = textures();
    const byId = {}; for (const s of model.systems) byId[s.id] = s;
    // bounds → home camera
    if (!this.homeSet) { const xs = model.systems.map(s => s.x), ys = model.systems.map(s => -s.y); this.target.set((Math.min(...xs) + Math.max(...xs)) / 2, (Math.min(...ys) + Math.max(...ys)) / 2, 0); this.distance = Math.max(Math.max(...xs) - Math.min(...xs), (Math.max(...ys) - Math.min(...ys)) * 1.6) * 1.05 + 200; this.home = { t: this.target.clone(), d: this.distance }; this.homeSet = true; this.updateCamera(); }

    // territory field
    const houses = []; const houseIdx = {}; const colorOf = {};
    for (const s of model.systems) if (s.ownerId && houseIdx[s.ownerId] == null) { houseIdx[s.ownerId] = houses.length; houses.push(s.ownerId); colorOf[s.ownerId] = new THREE.Color(s.accent); }
    const minX = Math.min(...model.systems.map(s => s.x)) - 160, maxX = Math.max(...model.systems.map(s => s.x)) + 160, minY = Math.min(...model.systems.map(s => s.y)) - 160, maxY = Math.max(...model.systems.map(s => s.y)) + 160;
    const uPos = [], uOwner = [], uCol = [];
    for (let i = 0; i < MAX_SYS; i++) { const s = model.systems[i]; if (s) { uPos.push(new THREE.Vector3(s.x - minX, maxY - s.y, 1.0 + Math.min(1.0, (s.pop || 0) / 60))); uOwner.push(s.ownerId ? houseIdx[s.ownerId] : -1); } else { uPos.push(new THREE.Vector3()); uOwner.push(-1); } }
    for (let h = 0; h < MAX_HOUSES; h++) uCol.push(houses[h] ? colorOf[houses[h]] : new THREE.Color(0));
    const terr = new THREE.Mesh(new THREE.PlaneGeometry(maxX - minX, maxY - minY), new THREE.ShaderMaterial({ transparent: true, depthWrite: false, vertexShader: NEBULA_VS, fragmentShader: TERR_FS,
      uniforms: { uSize: { value: new THREE.Vector2(maxX - minX, maxY - minY) }, uCount: { value: Math.min(MAX_SYS, model.systems.length) }, uPos: { value: uPos }, uOwner: { value: uOwner }, uHouseColor: { value: uCol }, uRadius: { value: 82 }, uPlayer: { value: model.playerHouse ? (houseIdx[model.playerHouse] ?? -1) : -1 } } }));
    terr.position.set((minX + maxX) / 2, -(minY + maxY) / 2, -2); this.objects.add(terr);

    // lanes
    const laneGeo = new THREE.BufferGeometry(); const lp = [], lc = [];
    for (const l of model.lanes) { const a = byId[l.a], b = byId[l.b]; if (!a || !b) continue; const c = l.front ? [0.75, 0.22, 0.16] : l.reach ? [0.85, 0.68, 0.3] : [0.22, 0.3, 0.46]; lp.push(a.x, -a.y, 0, b.x, -b.y, 0); lc.push(...c, ...c); }
    laneGeo.setAttribute('position', new THREE.Float32BufferAttribute(lp, 3)); laneGeo.setAttribute('color', new THREE.Float32BufferAttribute(lc, 3));
    this.objects.add(new THREE.LineSegments(laneGeo, new THREE.LineBasicMaterial({ vertexColors: true, transparent: true, opacity: 0.9 })));

    // systems
    const sunMat = (color) => new THREE.SpriteMaterial({ map: T.sun, color, transparent: true, blending: THREE.AdditiveBlending, depthWrite: false });
    const ringGeo = new THREE.RingGeometry(1, 1.08, 64);
    for (const s of model.systems) {
      const g = new THREE.Group(); g.position.set(s.x, -s.y, 0);
      const h = hash(s.id); const starCol = [0xfff4e0, 0xffe2a8, 0xffc78a, 0xcfe0ff, 0xffffff][h % 5];
      const sun = new THREE.Sprite(sunMat(starCol)); const sz = 26 + Math.min(14, (s.pop || 0) / 4); sun.scale.set(sz, sz, 1); g.add(sun);
      const core = new THREE.Sprite(new THREE.SpriteMaterial({ map: T.glow, color: 0xffffff, transparent: true, blending: THREE.AdditiveBlending, depthWrite: false })); core.scale.set(7, 7, 1); g.add(core);
      // the world: a lit sphere beside its star
      const pr = 5 + Math.min(4, (s.pop || 0) / 15);
      const planet = new THREE.Mesh(new THREE.SphereGeometry(pr, 32, 24), new THREE.MeshStandardMaterial({ color: planetColor(s.planetClass), roughness: 0.75, metalness: 0.05, emissive: new THREE.Color(planetColor(s.planetClass)).multiplyScalar(0.08) }));
      planet.position.set(14, -9, 4); g.add(planet);
      if (s.planetClass === 'gas' || s.planetClass === 'belt') { const ring = new THREE.Mesh(new THREE.RingGeometry(pr * 1.35, pr * 1.9, 48), new THREE.MeshBasicMaterial({ color: s.planetClass === 'gas' ? 0xf0d0a0 : 0xc9bca8, transparent: true, opacity: 0.5, side: THREE.DoubleSide })); ring.position.copy(planet.position); ring.rotation.x = 1.2; g.add(ring); }
      // atmosphere rim
      const atm = new THREE.Mesh(new THREE.SphereGeometry(pr * 1.12, 24, 18), new THREE.MeshBasicMaterial({ color: planetGlow(s.planetClass), transparent: true, opacity: 0.18, blending: THREE.AdditiveBlending, side: THREE.BackSide, depthWrite: false })); atm.position.copy(planet.position); g.add(atm);
      if (s.ownerId) { const ring = new THREE.Mesh(ringGeo, new THREE.MeshBasicMaterial({ color: s.accent, transparent: true, opacity: s.isPlayer ? 0.95 : 0.55, blending: THREE.AdditiveBlending, side: THREE.DoubleSide })); ring.scale.set(sz * 0.62, sz * 0.62, 1); g.add(ring); }
      if (s.bloc) { const bl = new THREE.Mesh(new THREE.RingGeometry(sz * 0.78, sz * 0.8, 64), new THREE.MeshBasicMaterial({ color: s.bloc === 'hegemon' ? 0xe6c77a : 0x7aa2ff, transparent: true, opacity: 0.6, side: THREE.DoubleSide })); g.add(bl); }
      if (s.siege != null) { const arc = new THREE.Mesh(new THREE.RingGeometry(sz * 0.7, sz * 0.76, 48, 1, Math.PI / 2, -Math.PI * 2 * Math.max(0.02, s.siege)), new THREE.MeshBasicMaterial({ color: 0xff5a3c, transparent: true, opacity: 0.95, side: THREE.DoubleSide, blending: THREE.AdditiveBlending })); g.add(arc); }
      if (s.contested) { const c = new THREE.Mesh(new THREE.RingGeometry(sz * 0.5, sz * 0.52, 48), new THREE.MeshBasicMaterial({ color: 0xff6a4a, transparent: true, opacity: 0.8, side: THREE.DoubleSide })); g.add(c); }
      if (s.target) { const tr = new THREE.Mesh(new THREE.RingGeometry(sz * 0.9, sz * 0.93, 64), new THREE.MeshBasicMaterial({ color: 0xc9a24a, transparent: true, opacity: s.target === 1 ? 0.95 : 0.4, side: THREE.DoubleSide })); tr.userData.pulse = true; g.add(tr); }
      if (s.id === this.selected.systemId) { const sel = new THREE.Mesh(new THREE.RingGeometry(sz * 0.56, sz * 0.6, 64), new THREE.MeshBasicMaterial({ color: 0xffe8a3, transparent: true, opacity: 0.95, side: THREE.DoubleSide })); g.add(sel); }
      for (let k = 0; k < (s.stations || 0); k++) { const st = new THREE.Sprite(new THREE.SpriteMaterial({ map: T.station, color: 0xc9bca8, transparent: true, depthWrite: false })); st.scale.set(5, 5, 1); st.position.set(-12 - k * 6, 10, 3); g.add(st); }
      const hit = new THREE.Mesh(new THREE.SphereGeometry(sz * 0.55, 8, 8), new THREE.MeshBasicMaterial({ visible: false })); hit.userData = { kind: 'system', id: s.id }; g.add(hit); this.hitTargets.push(hit);
      // label
      const el = document.createElement('div'); el.className = 'sys-label' + (s.isPlayer ? ' mine' : ''); el.innerHTML = `<span class="n">${esc(s.name)}</span>${s.ownerName ? `<span class="o" style="color:${s.accent}">${esc(s.ownerName)}</span>` : ''}${s.target ? `<span class="j">${s.target}j</span>` : ''}`;
      const lab = new CSS2DObject(el); lab.position.set(0, -sz * 0.62 - 6, 0); g.add(lab); this.labels.push(lab);
      this.objects.add(g);
    }
    // regions
    for (const r of model.regions || []) { const el = document.createElement('div'); el.className = 'region-label'; el.innerHTML = `<span class="rn">${esc(r.name)}</span><span class="rs">${esc(r.culture)} · ${esc(r.dominant || 'no one')} · loyalty ${r.loyalty}</span>`; const lab = new CSS2DObject(el); lab.position.set(r.cx, -(r.top - 70), 0); this.objects.add(lab); this.labels.push(lab); }
    // fleets
    const groups = {}; for (const f of model.fleets) (groups[f.systemId] ||= []).push(f);
    for (const [sid, list] of Object.entries(groups)) {
      const s = byId[sid]; if (!s) continue; let i = 0; const n = list.length;
      for (const f of list) { const ang = -Math.PI / 2 - 0.6 + i * (Math.PI * 2 / Math.max(4, n)); i++; const fx = s.x + Math.cos(ang) * 30, fy = -s.y - Math.sin(ang) * 30;
        const spr = new THREE.Sprite(new THREE.SpriteMaterial({ map: T.ship, color: f.accent, transparent: true, opacity: f.isPlayer ? 1 : 0.35 + 0.65 * (f.confidence || 0) / 100, depthWrite: false })); const sc = f.isPlayer ? 13 : 10; spr.scale.set(sc, sc, 1); spr.position.set(fx, fy, 6); this.objects.add(spr);
        const glow = new THREE.Sprite(new THREE.SpriteMaterial({ map: T.glow, color: f.accent, transparent: true, opacity: f.isPlayer ? 0.7 : 0.3, blending: THREE.AdditiveBlending, depthWrite: false })); glow.scale.set(sc * 2.2, sc * 2.2, 1); glow.position.set(fx, fy, 5); glow.userData.pulse = true; this.objects.add(glow);
        if (f.id === this.selected.fleetId) { const sel = new THREE.Mesh(new THREE.RingGeometry(sc * 0.9, sc * 1.0, 48), new THREE.MeshBasicMaterial({ color: 0xffe8a3, transparent: true, opacity: 0.95, side: THREE.DoubleSide })); sel.position.set(fx, fy, 5.5); this.objects.add(sel); }
        if (f.moving && byId[f.destId]) { const d = byId[f.destId]; const lg = new THREE.BufferGeometry().setFromPoints([new THREE.Vector3(fx, fy, 5), new THREE.Vector3(d.x, -d.y, 5)]); this.objects.add(new THREE.Line(lg, new THREE.LineDashedMaterial({ color: 0xe6c77a, dashSize: 8, gapSize: 6, transparent: true, opacity: 0.8 }))); this.objects.children[this.objects.children.length - 1].computeLineDistances(); }
        const hit = new THREE.Mesh(new THREE.SphereGeometry(sc * 0.8, 8, 8), new THREE.MeshBasicMaterial({ visible: false })); hit.position.set(fx, fy, 6); hit.userData = { kind: 'fleet', id: f.id, systemId: sid }; this.objects.add(hit); this.hitTargets.push(hit);
        const el = document.createElement('div'); el.className = 'fleet-label'; el.style.color = f.accent; el.textContent = `${f.strength}${f.isPlayer ? '' : '?'}`; const lab = new CSS2DObject(el); lab.position.set(fx + sc * 0.9, fy + 2, 6); this.objects.add(lab); this.labels.push(lab);
      }
    }
    this.dirty = true;
  }

  setSelection(sel) { this.selected = sel || { systemId: null, fleetId: null }; }

  // ---- camera ----
  updateCamera() {
    const d = this.distance; this.camera.position.set(this.target.x, this.target.y - d * Math.sin(this.tilt), d * Math.cos(this.tilt)); this.camera.lookAt(this.target); this.camera.updateProjectionMatrix(); this.dirty = true;
  }
  zoom(f, px, py) {
    const before = px != null ? this.planePoint(px, py) : null;
    this.distance = Math.max(220, Math.min(2600, this.distance * f)); this.updateCamera();
    if (before) { const after = this.planePoint(px, py); if (after) { this.target.x += before.x - after.x; this.target.y += before.y - after.y; this.updateCamera(); } }
  }
  home() { if (this.home) { this.target.copy(this.home.t); this.distance = this.home.d; this.updateCamera(); } }
  focus(x, y) { this.target.set(x, -y, 0); this.distance = Math.min(this.distance, 520); this.updateCamera(); }
  planePoint(px, py) {
    const rect = this.renderer.domElement.getBoundingClientRect(); this.mouse.set(((px - rect.left) / rect.width) * 2 - 1, -((py - rect.top) / rect.height) * 2 + 1);
    this.raycaster.setFromCamera(this.mouse, this.camera); const pt = new THREE.Vector3(); return this.raycaster.ray.intersectPlane(new THREE.Plane(new THREE.Vector3(0, 0, 1), 0), pt) ? pt : null;
  }
  bindInput() {
    const el = this.renderer.domElement; let down = null, moved = false;
    el.addEventListener('pointerdown', e => { down = { x: e.clientX, y: e.clientY, pt: this.planePoint(e.clientX, e.clientY) }; moved = false; el.setPointerCapture(e.pointerId); });
    el.addEventListener('pointermove', e => {
      if (down) { const pt = this.planePoint(e.clientX, e.clientY); if (Math.abs(e.clientX - down.x) + Math.abs(e.clientY - down.y) > 3) moved = true; if (pt && down.pt && moved) { this.target.x -= pt.x - down.pt.x; this.target.y -= pt.y - down.pt.y; this.updateCamera(); } el.style.cursor = 'grabbing'; return; }
      const hit = this.pick(e.clientX, e.clientY); el.style.cursor = hit ? 'pointer' : 'grab';
    });
    el.addEventListener('pointerup', e => { if (down && !moved) { const hit = this.pick(e.clientX, e.clientY); if (hit) this.dotnet?.invokeMethodAsync('OnPick', hit.kind, hit.id); else this.dotnet?.invokeMethodAsync('OnPick', 'none', ''); } down = null; el.style.cursor = 'grab'; });
    el.addEventListener('wheel', e => { e.preventDefault(); this.zoom(e.deltaY > 0 ? 1.15 : 0.87, e.clientX, e.clientY); }, { passive: false });
  }
  pick(px, py) {
    const rect = this.renderer.domElement.getBoundingClientRect(); this.mouse.set(((px - rect.left) / rect.width) * 2 - 1, -((py - rect.top) / rect.height) * 2 + 1);
    this.raycaster.setFromCamera(this.mouse, this.camera); const hits = this.raycaster.intersectObjects(this.hitTargets, false); return hits.length ? hits[0].object.userData : null;
  }
  resize() { const w = this.container.clientWidth, h = this.container.clientHeight; if (!w || !h) return; this.renderer.setSize(w, h); this.labelRenderer.setSize(w, h); this.composer.setSize(w, h); this.camera.aspect = w / h; this.camera.updateProjectionMatrix(); this.dirty = true; }
  loop() {
    if (this.disposed) return;
    // a frame budget: about 20 fps, nothing while the tab is hidden — the map is a chart, not a shooter
    setTimeout(() => requestAnimationFrame(this.loop), 50);
    if (document.hidden) return;
    const t = this.clock.getElapsedTime(); this.nebula.material.uniforms.uTime.value = t; this.stars.material.uniforms.uTime.value = t;
    this.objects.traverse(o => { if (o.userData.pulse) { const s = 1 + 0.08 * Math.sin(t * 3 + o.position.x * 0.01); o.scale.setScalar ? (o.isSprite ? null : o.scale.set(s, s, 1)) : null; if (o.material?.opacity != null && o.isSprite) o.material.opacity = 0.45 + 0.25 * Math.sin(t * 2.5); } });
    // labels fade with distance
    const far = this.distance > 1500; for (const l of this.labels) l.element.classList.toggle('far', far);
    this.composer.render(); this.labelRenderer.render(this.scene, this.camera);
  }
  dispose() { this.disposed = true; this.resizeObserver.disconnect(); this.setModel(null); this.renderer.dispose(); this.renderer.domElement.remove(); this.labelRenderer.domElement.remove(); }
}

function esc(s) { return String(s ?? '').replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c])); }
function planetColor(cls) { return ({ terran: 0x3f8f62, ocean: 0x2f6fb5, arid: 0xc98f4a, forge: 0xa5452a, frozen: 0xb9d4ea, gas: 0xc9a06a, belt: 0x7a6f60, barren: 0x6f6a66 })[cls] || 0x3f8f62; }
function planetGlow(cls) { return ({ terran: 0x8ad9ff, ocean: 0x8ac8ff, arid: 0xffd9a0, forge: 0xff9060, frozen: 0xe6f4ff, gas: 0xffe0b0, belt: 0x000000, barren: 0x999999 })[cls] || 0x8ad9ff; }

window.praetoriaSpace = {
  galaxies: new Map(),
  create(id, container, dotnet) { this.destroy(id); const g = new Galaxy(container, dotnet); this.galaxies.set(id, g); return true; },
  setModel(id, json) { const g = this.galaxies.get(id); if (!g) return false; const m = JSON.parse(json); g.setSelection(m.selected); g.setModel(m); return true; },
  zoom(id, f) { this.galaxies.get(id)?.zoom(f); }, home(id) { this.galaxies.get(id)?.home(); }, focus(id, x, y) { this.galaxies.get(id)?.focus(x, y); },
  destroy(id) { const g = this.galaxies.get(id); if (g) { g.dispose(); this.galaxies.delete(id); } }
};
