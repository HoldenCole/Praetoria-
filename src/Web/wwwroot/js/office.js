// Praetoria — the office as a lit 3D set (docs/ART_DIRECTION.md §2). A room with real materials and lights: a reflective
// floor, pilasters, a tall viewport onto the seat's world, a holographic star table of the Reach, banners in the house
// colour, emissive strips, haze and bloom. Visitors stand as framed portrait plates; clicking one opens the report.
import * as THREE from 'three';
import { EffectComposer } from 'three/addons/postprocessing/EffectComposer.js';
import { RenderPass } from 'three/addons/postprocessing/RenderPass.js';
import { UnrealBloomPass } from 'three/addons/postprocessing/UnrealBloomPass.js';
import { RoomEnvironment } from 'three/addons/environments/RoomEnvironment.js';
import { render as cameo } from './portrait.js';

function hash(s) { let h = 2166136261; for (let i = 0; i < s.length; i++) { h ^= s.charCodeAt(i); h = Math.imul(h, 16777619); } return h >>> 0; }

// ---- procedural textures ----
export function noiseCanvas(size, seed, scale, base, variance) {
  const c = document.createElement('canvas'); c.width = c.height = size; const ctx = c.getContext('2d'); const img = ctx.createImageData(size, size); const d = img.data;
  let t = seed; const r = () => { t = (t * 1664525 + 1013904223) >>> 0; return t / 4294967296; };
  const grid = []; const g = Math.ceil(size / scale) + 2; for (let i = 0; i < g * g; i++) grid.push(r());
  for (let y = 0; y < size; y++) for (let x = 0; x < size; x++) {
    const gx = x / scale, gy = y / scale, ix = Math.floor(gx), iy = Math.floor(gy), fx = gx - ix, fy = gy - iy;
    const a = grid[iy * g + ix], b = grid[iy * g + ix + 1], cc = grid[(iy + 1) * g + ix], dd = grid[(iy + 1) * g + ix + 1];
    const u = fx * fx * (3 - 2 * fx), v = fy * fy * (3 - 2 * fy); const n = (a * (1 - u) + b * u) * (1 - v) + (cc * (1 - u) + dd * u) * v;
    const i = (y * size + x) * 4; const k = base + (n - .5) * variance; d[i] = d[i + 1] = d[i + 2] = Math.max(0, Math.min(255, k)); d[i + 3] = 255;
  }
  ctx.putImageData(img, 0, 0); return c;
}
export function texture(canvas, repeat = 1) { const t = new THREE.CanvasTexture(canvas); t.wrapS = t.wrapT = THREE.RepeatWrapping; t.repeat.set(repeat, repeat); t.colorSpace = THREE.SRGBColorSpace; return t; }
export function sigilCanvas(houseId, accent) {
  const c = document.createElement('canvas'); c.width = 256; c.height = 384; const ctx = c.getContext('2d');
  ctx.fillStyle = accent; ctx.fillRect(0, 0, 256, 384);
  const grad = ctx.createLinearGradient(0, 0, 0, 384); grad.addColorStop(0, 'rgba(255,255,255,.18)'); grad.addColorStop(1, 'rgba(0,0,0,.35)'); ctx.fillStyle = grad; ctx.fillRect(0, 0, 256, 384);
  ctx.strokeStyle = '#e6c77a'; ctx.lineWidth = 6; ctx.strokeRect(10, 10, 236, 364);
  // shield
  ctx.save(); ctx.translate(128, 150); ctx.scale(3.2, 3.2);
  ctx.fillStyle = 'rgba(0,0,0,.35)'; ctx.beginPath(); ctx.moveTo(0, -20); ctx.lineTo(17, -15); ctx.lineTo(17, 0); ctx.bezierCurveTo(17, 10, 9, 17, 0, 20); ctx.bezierCurveTo(-9, 17, -17, 10, -17, 0); ctx.lineTo(-17, -15); ctx.closePath(); ctx.fill();
  ctx.strokeStyle = '#f3e9cf'; ctx.lineWidth = 1.2; ctx.stroke();
  const charge = Math.abs(hashSigned(houseId)) % 8; ctx.fillStyle = '#f3e9cf'; ctx.strokeStyle = '#f3e9cf'; ctx.lineWidth = 3; ctx.beginPath();
  switch (charge) {
    case 0: ctx.moveTo(-12, 6); ctx.lineTo(0, -8); ctx.lineTo(12, 6); ctx.lineTo(8, 6); ctx.lineTo(0, -3); ctx.lineTo(-8, 6); ctx.closePath(); ctx.fill(); break;
    case 1: for (let i = 0; i < 10; i++) { const a = -Math.PI / 2 + i * Math.PI / 5, rr = i % 2 ? 5 : 12; ctx.lineTo(Math.cos(a) * rr, Math.sin(a) * rr); } ctx.closePath(); ctx.fill(); break;
    case 2: ctx.fillRect(-11, -8, 22, 3); ctx.fillRect(-11, -1, 22, 3); ctx.fillRect(-11, 6, 22, 3); break;
    case 3: ctx.arc(0, 0, 8, 0, 6.283); ctx.stroke(); break;
    case 4: ctx.moveTo(0, -12); ctx.lineTo(11, 0); ctx.lineTo(0, 12); ctx.lineTo(-11, 0); ctx.closePath(); ctx.stroke(); break;
    case 5: ctx.fillRect(-2, -12, 4, 24); ctx.fillRect(-12, -2, 24, 4); break;
    case 6: ctx.moveTo(-12, 8); ctx.quadraticCurveTo(0, -16, 12, 8); ctx.closePath(); ctx.fill(); break;
    default: ctx.moveTo(0, -10); ctx.bezierCurveTo(7, -10, 12, -4, 12, 2); ctx.bezierCurveTo(12, 10, 0, 14, 0, 14); ctx.bezierCurveTo(0, 14, -12, 10, -12, 2); ctx.bezierCurveTo(-12, -4, -7, -10, 0, -10); ctx.stroke(); break;
  }
  ctx.restore(); return c;
}
function hashSigned(s) { let h = 17; for (const ch of s) h = (h * 31 + ch.charCodeAt(0)) | 0; return h; }

export const PLANET = { terran: [0x3ddc84, 0xb2ffd6], ocean: [0x2f7bff, 0x8cc8ff], arid: [0xffb03b, 0xffe08a], forge: [0xff4d2e, 0xffa07a], frozen: [0xcfe9ff, 0xffffff], gas: [0xf2c27b, 0xffe6b0], belt: [0xb8afa0, 0xe0d8c8], barren: [0xc9d1dc, 0xf0f4f8] };
const WALLS = { stone: { color: 0x3a4152, rough: .85, metal: .05 }, brass: { color: 0x5a4a28, rough: .35, metal: .8 }, marble: { color: 0x9a9fab, rough: .25, metal: .05 }, wood: { color: 0x4a2e1c, rough: .6, metal: .02 } };
const FLOORS = { slate: { color: 0x2a3142, rough: .3, metal: .7 }, carpet: { color: 0x4a1f24, rough: .95, metal: 0 }, marble: { color: 0x6a6f7a, rough: .15, metal: .2 }, wood: { color: 0x3a2416, rough: .5, metal: .05 } };
const DESKS = { oak: { color: 0x4d3221, rough: .55, metal: .05 }, steel: { color: 0x3a4150, rough: .3, metal: .9 }, marble: { color: 0x7a7f8a, rough: .2, metal: .1 } };

export class Office {
  constructor(container, dotnet) {
    this.container = container; this.dotnet = dotnet; this.disposed = false; this.plates = []; this.opts = null;
    const w = container.clientWidth || 960, h = container.clientHeight || 540;
    this.renderer = new THREE.WebGLRenderer({ antialias: true }); this.renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 1.5)); this.renderer.setSize(w, h);
    this.renderer.toneMapping = THREE.ACESFilmicToneMapping; this.renderer.toneMappingExposure = 1.0; this.renderer.shadowMap.enabled = true; this.renderer.shadowMap.type = THREE.PCFSoftShadowMap;
    this.renderer.domElement.className = 'gl'; container.appendChild(this.renderer.domElement);
    this.scene = new THREE.Scene(); this.scene.fog = new THREE.FogExp2(0x070a12, 0.018);
    const pm = new THREE.PMREMGenerator(this.renderer); this.scene.environment = pm.fromScene(new RoomEnvironment(), 0.04).texture;
    this.camera = new THREE.PerspectiveCamera(36, w / h, 0.1, 400); this.camera.position.set(0, 3.0, 12.8); this.camera.lookAt(0, 2.1, -4);
    this.composer = new EffectComposer(this.renderer); this.composer.addPass(new RenderPass(this.scene, this.camera)); this.composer.addPass(new UnrealBloomPass(new THREE.Vector2(w, h), 0.55, 0.6, 0.72));
    this.raycaster = new THREE.Raycaster(); this.mouse = new THREE.Vector2(-9, -9); this.parallax = new THREE.Vector2();
    this.root = new THREE.Group(); this.scene.add(this.root);
    this.bindInput(); this.resizeObserver = new ResizeObserver(() => this.resize()); this.resizeObserver.observe(container);
    this.clock = new THREE.Clock(); this.loop = this.loop.bind(this); requestAnimationFrame(this.loop);
  }

  build(opts) {
    this.opts = opts; this.plates = []; this.signal = null;
    while (this.root.children.length) { const o = this.root.children[0]; this.root.remove(o); o.traverse?.(c => { c.geometry?.dispose?.(); c.material?.dispose?.(); }); }
    const accent = new THREE.Color(opts.accent || '#c9a24a'); const wall = WALLS[opts.wall] || WALLS.stone, floor = FLOORS[opts.floor] || FLOORS.slate, desk = DESKS[opts.desk] || DESKS.oak;
    const R = this.root;
    const W = 14, D = 16, H = 6.5;   // room: 14 wide, 16 deep, 6.5 high; window wall at z = -8

    // floor with a brushed-metal grain and a reflective sheen
    const floorMat = new THREE.MeshStandardMaterial({ color: floor.color, roughness: floor.rough, metalness: floor.metal, map: texture(noiseCanvas(256, 7, 24, 150, 90), 4), envMapIntensity: floor.metal > .5 ? 1.2 : .4 });
    floorMat.map.repeat.set(6, 6);
    const fl = new THREE.Mesh(new THREE.PlaneGeometry(W, D), floorMat); fl.rotation.x = -Math.PI / 2; fl.position.set(0, 0, -D / 2 + 4); fl.receiveShadow = true; R.add(fl);
    if (opts.floor === 'carpet') { const run = new THREE.Mesh(new THREE.PlaneGeometry(3.2, 10), new THREE.MeshStandardMaterial({ color: 0x5a2228, roughness: 1 })); run.rotation.x = -Math.PI / 2; run.position.set(0, 0.005, -2); R.add(run); }
    // floor inlay: a brass ring under the holo table
    const inlay = new THREE.Mesh(new THREE.RingGeometry(2.3, 2.42, 64), new THREE.MeshStandardMaterial({ color: 0xc9a24a, metalness: .9, roughness: .3 })); inlay.rotation.x = -Math.PI / 2; inlay.position.set(3.7, 0.006, 1.0); R.add(inlay);
    // floor seams: a brass grid so the reflective floor reads as a floor
    const seamMat = new THREE.MeshStandardMaterial({ color: 0x6b5a35, metalness: .8, roughness: .4 });
    for (let x = -6; x <= 6; x += 3) { const m = new THREE.Mesh(new THREE.BoxGeometry(.03, .004, D), seamMat); m.position.set(x, .003, -D / 2 + 4); R.add(m); }
    for (let z = -7; z <= 3; z += 2.5) { const m = new THREE.Mesh(new THREE.BoxGeometry(W, .004, .03), seamMat); m.position.set(0, .003, z); R.add(m); }

    // walls
    const wallMat = new THREE.MeshStandardMaterial({ color: wall.color, roughness: wall.rough, metalness: wall.metal, map: texture(noiseCanvas(256, 3, 40, 160, 60), 3) });
    const back = new THREE.Group(); back.position.z = -8;   // window wall built from pieces around the opening
    const ww = opts.window === 'wide' ? 8.5 : opts.window === 'porthole' ? 5.5 : 6.2, wh = opts.window === 'wide' ? 4.2 : 5.2, wy = opts.window === 'wide' ? 2.9 : 3.3;
    const piece = (x, y, w, h) => { const m = new THREE.Mesh(new THREE.BoxGeometry(w, h, 0.4), wallMat); m.position.set(x, y, 0); m.receiveShadow = true; back.add(m); };
    piece(-(ww / 2 + (W - ww) / 4), H / 2, (W - ww) / 2, H); piece((ww / 2 + (W - ww) / 4), H / 2, (W - ww) / 2, H);
    piece(0, wy + wh / 2 + (H - wy - wh / 2) / 2, ww, H - (wy + wh / 2)); piece(0, (wy - wh / 2) / 2, ww, wy - wh / 2);
    R.add(back);
    const side = (x) => { const m = new THREE.Mesh(new THREE.BoxGeometry(0.4, H, D), wallMat); m.position.set(x, H / 2, -D / 2 + 4); m.receiveShadow = true; R.add(m); }; side(-W / 2); side(W / 2);
    const ceil = new THREE.Mesh(new THREE.PlaneGeometry(W, D), new THREE.MeshStandardMaterial({ color: 0x0c1018, roughness: .9 })); ceil.rotation.x = Math.PI / 2; ceil.position.set(0, H, -D / 2 + 4); R.add(ceil);
    // window frame + mullion + glass sheen
    const frameMat = new THREE.MeshStandardMaterial({ color: 0x6b5a35, metalness: .85, roughness: .35 });
    const frame = (x, y, w, h, d = 0.5) => { const m = new THREE.Mesh(new THREE.BoxGeometry(w, h, d), frameMat); m.position.set(x, y, -8); R.add(m); };
    frame(0, wy + wh / 2, ww + .4, .25); frame(0, wy - wh / 2, ww + .4, .25); frame(-ww / 2, wy, .25, wh + .25); frame(ww / 2, wy, .25, wh + .25);
    if (opts.window !== 'porthole') { frame(0, wy, .12, wh); frame(0, wy - wh / 2 + wh * .62, ww, .12); }
    if (opts.window === 'arched') { const arch = new THREE.Mesh(new THREE.TorusGeometry(ww / 2, .13, 12, 48, Math.PI), frameMat); arch.position.set(0, wy + wh / 2 - ww / 2 + 0.02, -8); R.add(arch); }
    const glass = new THREE.Mesh(new THREE.PlaneGeometry(ww, wh), new THREE.MeshPhysicalMaterial({ color: 0x88aaff, transparent: true, opacity: 0.08, roughness: 0.05, metalness: 0, transmission: 0.2, side: THREE.DoubleSide, depthWrite: false })); glass.position.set(0, wy, -8.05); R.add(glass);

    // the view beyond: space, a world, a distant hull
    const [pc, pg] = PLANET[opts.planetClass] || PLANET.terran;
    const planetUrl = opts.planetUrl || (window.praetoriaFx ? window.praetoriaFx.planet(opts.planetClass || 'terran', opts.seed || 'reach', 512) : null);
    const planetTex = planetUrl ? new THREE.TextureLoader().load(planetUrl) : null; if (planetTex) planetTex.colorSpace = THREE.SRGBColorSpace;
    // a shaded sprite of the world (already lit by the painter) hangs beyond the glass; a sphere behind it catches the rim light
    const pr = 6.2; const planet = new THREE.Mesh(new THREE.SphereGeometry(pr, 64, 48), new THREE.MeshStandardMaterial({ color: pc, roughness: .9, metalness: 0, emissive: new THREE.Color(pc).multiplyScalar(.15) }));
    planet.position.set(7.5, 3.6, -30); R.add(planet); this.planet = planet;
    if (planetTex) { const disc = new THREE.Sprite(new THREE.SpriteMaterial({ map: planetTex, transparent: true, depthWrite: false })); disc.scale.set(pr * 2.12, pr * 2.12, 1); disc.position.set(7.5, 3.6, -29.2); R.add(disc); }
    const atm = new THREE.Mesh(new THREE.SphereGeometry(pr * 1.06, 48, 32), new THREE.MeshBasicMaterial({ color: pg, transparent: true, opacity: .16, side: THREE.BackSide, blending: THREE.AdditiveBlending, depthWrite: false })); atm.position.copy(planet.position); R.add(atm);
    if (opts.planetClass === 'gas') { const ring = new THREE.Mesh(new THREE.RingGeometry(pr * 1.3, pr * 1.9, 64), new THREE.MeshBasicMaterial({ color: 0xf0d0a0, transparent: true, opacity: .35, side: THREE.DoubleSide })); ring.position.copy(planet.position); ring.rotation.x = 1.25; R.add(ring); }
    const moon = new THREE.Mesh(new THREE.SphereGeometry(.9, 24, 16), new THREE.MeshStandardMaterial({ color: 0x8a847c, roughness: .95 })); moon.position.set(-4, 5.5, -26); R.add(moon);
    const starsGeo = new THREE.BufferGeometry(); const sp = []; let t = 99; const rr = () => { t = (t * 1664525 + 1013904223) >>> 0; return t / 4294967296; };
    for (let i = 0; i < 1400; i++) sp.push(-60 + rr() * 120, -20 + rr() * 60, -30 - rr() * 60);
    starsGeo.setAttribute('position', new THREE.Float32BufferAttribute(sp, 3)); R.add(new THREE.Points(starsGeo, new THREE.PointsMaterial({ color: 0xffffff, size: .14, sizeAttenuation: true, transparent: true, opacity: .9 })));
    const nebula = new THREE.Mesh(new THREE.PlaneGeometry(140, 80), new THREE.MeshBasicMaterial({ map: texture(nebulaCanvas(256, hash(opts.seed || 'reach')), 1), transparent: true, opacity: .55, depthWrite: false })); nebula.position.set(0, 4, -70); R.add(nebula);
    const hull = new THREE.Group(); const hm = new THREE.MeshStandardMaterial({ color: 0x2a2f3a, metalness: .7, roughness: .5 });
    const body = new THREE.Mesh(new THREE.CylinderGeometry(.25, .35, 3.2, 8), hm); body.rotation.z = Math.PI / 2; hull.add(body);
    const fin = new THREE.Mesh(new THREE.BoxGeometry(.8, .9, .05), hm); fin.position.set(-1.1, .3, 0); hull.add(fin);
    const eng = new THREE.Mesh(new THREE.SphereGeometry(.14, 12, 8), new THREE.MeshBasicMaterial({ color: accent.clone().lerp(new THREE.Color(0xffe8a3), .5) })); eng.position.set(-1.65, 0, 0); hull.add(eng);
    hull.position.set(-6, 3.8, -18); hull.scale.setScalar(.9); R.add(hull); this.hull = hull;
    // key light from the planet side through the window
    const sun = new THREE.DirectionalLight(0xdfe8ff, 2.2); sun.position.set(6, 9, -14); sun.target.position.set(0, 1, 0); sun.castShadow = true; sun.shadow.mapSize.set(1024, 1024); sun.shadow.camera.left = -10; sun.shadow.camera.right = 10; sun.shadow.camera.top = 10; sun.shadow.camera.bottom = -6; R.add(sun); R.add(sun.target);
    R.add(new THREE.HemisphereLight(0x3a4a7a, 0x0a0c10, 0.55));

    // pilasters, cornice, sconces, emissive strips
    const pil = (x) => { const m = new THREE.Mesh(new THREE.BoxGeometry(.5, H, .5), new THREE.MeshStandardMaterial({ color: wall.color, roughness: wall.rough * .8, metalness: Math.min(1, wall.metal + .1) })); m.position.set(x, H / 2, -7.6); m.castShadow = true; R.add(m); const cap = new THREE.Mesh(new THREE.BoxGeometry(.7, .18, .7), frameMat); cap.position.set(x, H - .1, -7.6); R.add(cap); };
    pil(-ww / 2 - 1.2); pil(ww / 2 + 1.2); pil(-W / 2 + .6); pil(W / 2 - .6);
    const strip = (x, z, len, rot) => { const m = new THREE.Mesh(new THREE.BoxGeometry(len, .05, .08), new THREE.MeshBasicMaterial({ color: accent.clone().lerp(new THREE.Color(0xffffff), .25) })); m.position.set(x, H - .25, z); m.rotation.y = rot; R.add(m); const l = new THREE.PointLight(accent, 5, 10, 2); l.position.set(x, H - .6, z); R.add(l); };
    const fill = new THREE.PointLight(0xaab8ff, 4, 14, 2); fill.position.set(0, 4.5, 6); R.add(fill);
    strip(0, -7.3, W - 2, 0); strip(-W / 2 + .3, -2, 11, Math.PI / 2); strip(W / 2 - .3, -2, 11, Math.PI / 2);
    const sconce = (x) => { const m = new THREE.Mesh(new THREE.SphereGeometry(.07, 12, 8), new THREE.MeshBasicMaterial({ color: 0xffe2b0 })); m.position.set(x, 3.6, -7.5); R.add(m); const cup = new THREE.Mesh(new THREE.ConeGeometry(.16, .22, 12, 1, true), frameMat); cup.position.set(x, 3.48, -7.5); R.add(cup); const l = new THREE.PointLight(0xffd79a, 7, 8, 2); l.position.set(x, 3.6, -7.0); R.add(l); };
    sconce(-ww / 2 - 1.2); sconce(ww / 2 + 1.2);

    // banners on the flanks
    const bannerMat = (id) => new THREE.MeshStandardMaterial({ map: texture(sigilCanvas(id, opts.accent || '#c9a24a')), roughness: .9, metalness: 0, side: THREE.DoubleSide });
    for (const x of [-ww / 2 - 2.6, ww / 2 + 2.6]) { const b = new THREE.Mesh(new THREE.PlaneGeometry(1.4, 3.4, 8, 16), bannerMat(opts.houseId || 'house')); b.position.set(x, 3.6, -7.2); b.castShadow = true; b.userData.banner = true; R.add(b); const rod = new THREE.Mesh(new THREE.CylinderGeometry(.04, .04, 1.8, 8), frameMat); rod.rotation.z = Math.PI / 2; rod.position.set(x, 5.35, -7.2); R.add(rod); }

    // the desk and chair
    const deskMat = new THREE.MeshStandardMaterial({ color: desk.color, roughness: desk.rough, metalness: desk.metal, map: opts.desk === 'oak' ? texture(noiseCanvas(256, 5, 60, 140, 70), 2) : null });
    const dk = new THREE.Mesh(new THREE.BoxGeometry(5.2, .18, 1.8), deskMat); dk.position.set(0, 1.08, -1.2); dk.castShadow = true; dk.receiveShadow = true; R.add(dk);
    const legMat = new THREE.MeshStandardMaterial({ color: 0x151a24, metalness: .6, roughness: .4 });
    for (const [x, z] of [[-2.3, -1.9], [2.3, -1.9], [-2.3, -.55], [2.3, -.55]]) { const leg = new THREE.Mesh(new THREE.BoxGeometry(.18, 1.0, .18), legMat); leg.position.set(x, .5, z); leg.castShadow = true; R.add(leg); }
    const trim = new THREE.Mesh(new THREE.BoxGeometry(5.3, .04, 1.9), frameMat); trim.position.set(0, 1.18, -1.2); R.add(trim);
    const pad = new THREE.Mesh(new THREE.BoxGeometry(1.4, .02, .9), new THREE.MeshStandardMaterial({ color: 0x1a1612, roughness: .95 })); pad.position.set(-.3, 1.19, -1.1); R.add(pad);
    // the role register (GDD Art Direction): the same desk, dressed for the job
    const brassM = new THREE.MeshStandardMaterial({ color: 0xc9a24a, metalness: .9, roughness: .3 });
    if (opts.role === 'military') {
      // a signal lamp that blinks, a model hull on a stand, a chart tube
      const post = new THREE.Mesh(new THREE.CylinderGeometry(.05, .07, .5, 10), brassM); post.position.set(-1.9, 1.44, -1.5); R.add(post);
      const bulb = new THREE.Mesh(new THREE.SphereGeometry(.11, 12, 10), new THREE.MeshBasicMaterial({ color: 0x7fd0ff })); bulb.position.set(-1.9, 1.75, -1.5); R.add(bulb);
      const sig = new THREE.PointLight(0x7fd0ff, 2.5, 3.5, 2); sig.position.set(-1.9, 1.8, -1.5); R.add(sig); this.signal = t => { const on = Math.sin(t * 2.2) > .3; sig.intensity = on ? 3 : .4; bulb.material.color.setHex(on ? 0x9fe0ff : 0x2a4a60); };
      const stand = new THREE.Mesh(new THREE.BoxGeometry(.5, .05, .16), brassM); stand.position.set(1.1, 1.22, -1.6); R.add(stand);
      const hull = new THREE.Mesh(new THREE.CapsuleGeometry(.07, .6, 4, 8), new THREE.MeshStandardMaterial({ color: 0x8a94a6, metalness: .7, roughness: .35 })); hull.rotation.z = Math.PI / 2; hull.position.set(1.1, 1.36, -1.6); R.add(hull);
      const fin = new THREE.Mesh(new THREE.BoxGeometry(.05, .2, .12), hull.material); fin.position.set(1.4, 1.42, -1.6); R.add(fin);
      const tube = new THREE.Mesh(new THREE.CylinderGeometry(.06, .06, .9, 10), new THREE.MeshStandardMaterial({ color: 0x3a2e1c, roughness: .7 })); tube.rotation.z = Math.PI / 2; tube.rotation.y = .4; tube.position.set(.9, 1.25, -.75); R.add(tube);
    } else if (opts.role === 'stewardship') {
      // a stack of ledgers, a brass balance, an abacus of beads
      const inks = [0x5a2a22, 0x2a3a5a, 0x3a4a2a];
      for (let i = 0; i < 3; i++) { const book = new THREE.Mesh(new THREE.BoxGeometry(.62 - i * .04, .09, .42), new THREE.MeshStandardMaterial({ color: inks[i], roughness: .85 })); book.position.set(-1.8 + i * .02, 1.235 + i * .09, -1.45); book.rotation.y = (i - 1) * .12; R.add(book); }
      const base = new THREE.Mesh(new THREE.CylinderGeometry(.14, .18, .04, 12), brassM); base.position.set(1.3, 1.21, -1.5); R.add(base);
      const pillar = new THREE.Mesh(new THREE.CylinderGeometry(.02, .02, .5, 8), brassM); pillar.position.set(1.3, 1.46, -1.5); R.add(pillar);
      const beam = new THREE.Mesh(new THREE.BoxGeometry(.7, .02, .02), brassM); beam.position.set(1.3, 1.7, -1.5); beam.rotation.z = .06; R.add(beam);
      for (const sx of [-1, 1]) { const pan = new THREE.Mesh(new THREE.CylinderGeometry(.11, .09, .02, 12), brassM); pan.position.set(1.3 + sx * .33, 1.5 - sx * .02, -1.5); R.add(pan); const wire = new THREE.Mesh(new THREE.CylinderGeometry(.004, .004, .2, 4), brassM); wire.position.set(1.3 + sx * .33, 1.6 - sx * .02, -1.5); R.add(wire); }
      const frame = new THREE.Mesh(new THREE.BoxGeometry(.5, .3, .03), new THREE.MeshStandardMaterial({ color: 0x3a2e1c, roughness: .7 })); frame.position.set(.6, 1.34, -1.7); frame.rotation.x = -.5; R.add(frame);
      for (let r = 0; r < 4; r++) for (let k = 0; k < 6; k++) { const bead = new THREE.Mesh(new THREE.SphereGeometry(.018, 6, 5), new THREE.MeshStandardMaterial({ color: k % 2 ? 0xc9a24a : 0x2a2622, roughness: .5 })); bead.position.set(.4 + k * .075 + (r % 2) * .02, 1.25 + r * .065, -1.72 + r * .03); R.add(bead); }
    } else if (opts.role === 'law') {
      // a gavel on its block, a codex with a ribbon, a robe stand beside the desk
      const block = new THREE.Mesh(new THREE.CylinderGeometry(.16, .18, .05, 14), new THREE.MeshStandardMaterial({ color: 0x2a1a10, roughness: .6 })); block.position.set(1.4, 1.215, -1.5); R.add(block);
      const head = new THREE.Mesh(new THREE.CylinderGeometry(.07, .07, .26, 12), new THREE.MeshStandardMaterial({ color: 0x3a2416, roughness: .6 })); head.rotation.z = Math.PI / 2; head.position.set(1.1, 1.28, -1.3); R.add(head);
      const handle = new THREE.Mesh(new THREE.CylinderGeometry(.02, .025, .4, 8), head.material); handle.rotation.x = Math.PI / 2; handle.position.set(1.1, 1.28, -1.05); R.add(handle);
      const codex = new THREE.Mesh(new THREE.BoxGeometry(.5, .12, .36), new THREE.MeshStandardMaterial({ color: 0x1e1a2e, roughness: .8 })); codex.position.set(-1.8, 1.25, -1.45); R.add(codex);
      const clasp = new THREE.Mesh(new THREE.BoxGeometry(.06, .13, .1), brassM); clasp.position.set(-1.56, 1.25, -1.45); R.add(clasp);
      const ribbon = new THREE.Mesh(new THREE.BoxGeometry(.04, .005, .5), new THREE.MeshStandardMaterial({ color: accent, roughness: .7 })); ribbon.position.set(-1.75, 1.315, -1.4); R.add(ribbon);
      const pole = new THREE.Mesh(new THREE.CylinderGeometry(.04, .06, 2.2, 10), new THREE.MeshStandardMaterial({ color: 0x3a2e1c, roughness: .7 })); pole.position.set(3.6, 1.1, -2.6); R.add(pole);
      const robe = new THREE.Mesh(new THREE.ConeGeometry(.42, 1.7, 16, 1, true), new THREE.MeshStandardMaterial({ color: 0x1e1a2e, roughness: .9, side: THREE.DoubleSide })); robe.position.set(3.6, 1.3, -2.6); R.add(robe);
      const collar = new THREE.Mesh(new THREE.TorusGeometry(.14, .03, 8, 20), brassM); collar.rotation.x = Math.PI / 2; collar.position.set(3.6, 2.16, -2.6); R.add(collar);
    }
    const lamp = new THREE.Mesh(new THREE.ConeGeometry(.22, .25, 16, 1, true), new THREE.MeshStandardMaterial({ color: 0x6b5a35, metalness: .8, roughness: .3, side: THREE.DoubleSide })); lamp.position.set(2.0, 1.75, -1.4); R.add(lamp);
    const lampL = new THREE.PointLight(0xffd79a, 3, 4, 2); lampL.position.set(2.0, 1.6, -1.4); R.add(lampL);
    const lampB = new THREE.Mesh(new THREE.SphereGeometry(.06, 8, 8), new THREE.MeshBasicMaterial({ color: 0xffe8b0 })); lampB.position.set(2.0, 1.62, -1.4); R.add(lampB);
    const stem = new THREE.Mesh(new THREE.CylinderGeometry(.02, .02, .55, 8), frameMat); stem.position.set(2.0, 1.45, -1.4); R.add(stem);
    const chairMat = new THREE.MeshStandardMaterial({ color: 0x1b1612, roughness: .7, metalness: .1 });
    const seat = new THREE.Mesh(new THREE.BoxGeometry(1.3, .18, 1.1), chairMat); seat.position.set(0, .95, -2.7); R.add(seat);
    const backrest = new THREE.Mesh(new THREE.BoxGeometry(1.3, 1.9, .2), chairMat); backrest.position.set(0, 1.95, -3.2); backrest.castShadow = true; R.add(backrest);
    const crest = new THREE.Mesh(new THREE.BoxGeometry(1.4, .12, .26), frameMat); crest.position.set(0, 2.95, -3.2); R.add(crest);

    // holographic star table
    const hx = 3.7, hz = 1.0;
    const table = new THREE.Mesh(new THREE.CylinderGeometry(1.3, 1.5, .9, 32), new THREE.MeshStandardMaterial({ color: 0x1a1f2c, metalness: .7, roughness: .35 })); table.position.set(hx, .45, hz); table.castShadow = true; R.add(table);
    const rim = new THREE.Mesh(new THREE.TorusGeometry(1.3, .05, 8, 48), frameMat); rim.rotation.x = Math.PI / 2; rim.position.set(hx, .92, hz); R.add(rim);
    const holo = new THREE.Group(); holo.position.set(hx, 1.9, hz); this.holo = holo; R.add(holo);
    const hcol = new THREE.Color(0x7aa2ff);
    const nodes = opts.chart?.nodes || []; const lanes = opts.chart?.lanes || [];
    const sc = 1.9 / Math.max(1, Math.max(...nodes.map(n => Math.abs(n.x)), ...nodes.map(n => Math.abs(n.y)), 1));
    const pts = []; for (const l of lanes) { const a = nodes.find(n => n.id === l.a), b = nodes.find(n => n.id === l.b); if (a && b) pts.push(new THREE.Vector3(a.x * sc, 0, a.y * sc), new THREE.Vector3(b.x * sc, 0, b.y * sc)); }
    if (pts.length) holo.add(new THREE.LineSegments(new THREE.BufferGeometry().setFromPoints(pts), new THREE.LineBasicMaterial({ color: hcol, transparent: true, opacity: .35 })));
    for (const n of nodes) { const m = new THREE.Mesh(new THREE.SphereGeometry(n.mine ? .07 : .045, 8, 8), new THREE.MeshBasicMaterial({ color: n.color || '#8fb3ff' })); m.position.set(n.x * sc, 0, n.y * sc); holo.add(m); }
    const beam = new THREE.Mesh(new THREE.ConeGeometry(1.9, 1.0, 32, 1, true), new THREE.MeshBasicMaterial({ color: hcol, transparent: true, opacity: .06, side: THREE.DoubleSide, depthWrite: false })); beam.position.set(hx, 1.42, hz); R.add(beam);
    const hl = new THREE.PointLight(hcol, 4, 6, 2); hl.position.set(hx, 1.9, hz); R.add(hl);

    // decor on the left: star chart, trophy case or archive
    if (opts.decor === 'trophies') { const caseM = new THREE.Mesh(new THREE.BoxGeometry(2.4, 2.2, .5), new THREE.MeshStandardMaterial({ color: 0x1a1512, roughness: .6 })); caseM.position.set(-5.2, 2.3, -7.4); R.add(caseM); for (let i = 0; i < 4; i++) { const tr = new THREE.Mesh(new THREE.ConeGeometry(.12, .4, 12), new THREE.MeshStandardMaterial({ color: i < (opts.honors || 0) ? 0xe6c77a : 0x2a2318, metalness: .9, roughness: .3 })); tr.position.set(-6.0 + i * .55, 2.5, -7.1); R.add(tr); } const tl = new THREE.PointLight(0xffe0a0, 3, 4, 2); tl.position.set(-5.2, 3.2, -6.8); R.add(tl); }
    else if (opts.decor === 'archive') { for (let r = 0; r < 3; r++) { const shelf = new THREE.Mesh(new THREE.BoxGeometry(2.6, .06, .5), frameMat); shelf.position.set(-5.2, 1.4 + r * .9, -7.4); R.add(shelf); for (let i = 0; i < 9; i++) { const bk = new THREE.Mesh(new THREE.BoxGeometry(.22, .5 + (i % 3) * .1, .4), new THREE.MeshStandardMaterial({ color: [0x4a2f1e, 0x2b3a5a, 0x5a2a2a, 0x3a4a2a, 0x5a4a26][(i + r) % 5], roughness: .9 })); bk.position.set(-6.3 + i * .27, 1.7 + r * .9, -7.4); R.add(bk); } } }
    else { const chart = new THREE.Mesh(new THREE.PlaneGeometry(2.8, 1.8), new THREE.MeshStandardMaterial({ color: 0x0d1119, roughness: .4, emissive: 0x0a1a3a, emissiveIntensity: .6 })); chart.position.set(-5.2, 3.0, -7.75); R.add(chart); const cf = new THREE.Mesh(new THREE.BoxGeometry(3.0, 2.0, .08), frameMat); cf.position.set(-5.2, 3.0, -7.8); R.add(cf); for (const n of nodes) { const m = new THREE.Mesh(new THREE.SphereGeometry(.03, 6, 6), new THREE.MeshBasicMaterial({ color: n.color || '#8fb3ff' })); m.position.set(-5.2 + n.x * sc * .6, 3.0 - n.y * sc * .55, -7.7); R.add(m); } }

    // the player at the desk and the visitors: framed portrait plates
    this.addPlate(opts.player, 0, 2.15, -2.9, 1.25, true);
    const vis = opts.visitors || []; const start = -3.0 - (vis.length - 1) * 0.0;
    vis.forEach((v, i) => { this.addPlate(v, -4.3 + i * 1.35, 1.75, 2.4 - i * 0.8, 1.0, false, i); });

    // haze: a few translucent planes catch the window light
    for (let i = 0; i < 3; i++) { const hz = new THREE.Mesh(new THREE.PlaneGeometry(ww + 2, 7), new THREE.MeshBasicMaterial({ color: 0x9fb8ff, transparent: true, opacity: .025, depthWrite: false, blending: THREE.AdditiveBlending })); hz.position.set(0, 3.3, -6 + i * 1.6); R.add(hz); }
  }

  addPlate(p, x, y, z, size, isPlayer, index) {
    if (!p) return;
    const g = new THREE.Group(); g.position.set(x, y, z);
    const accent = new THREE.Color(p.accent || '#c9a24a');
    const frame = new THREE.Mesh(new THREE.BoxGeometry(size * .82, size * 1.08, .06), new THREE.MeshStandardMaterial({ color: 0x1a1407, metalness: .6, roughness: .4 })); g.add(frame);
    const edge = new THREE.Mesh(new THREE.BoxGeometry(size * .86, size * 1.12, .04), new THREE.MeshStandardMaterial({ color: 0xc9a24a, metalness: .9, roughness: .3 })); edge.position.z = -.012; g.add(edge);
    if (p.bust) { const tex = new THREE.TextureLoader().load(cameo({ ...p.bust, size: 256 })); tex.colorSpace = THREE.SRGBColorSpace; const img = new THREE.Mesh(new THREE.PlaneGeometry(size * .78, size * .78), new THREE.MeshStandardMaterial({ map: tex, roughness: .5, metalness: .1 })); img.position.set(0, size * .13, .034); g.add(img); }
    else { const stripe = new THREE.Mesh(new THREE.PlaneGeometry(size * .78, size * 1.04), new THREE.MeshBasicMaterial({ map: texture(stripeCanvas(p.accent || '#c9a24a')) })); stripe.position.z = .032; g.add(stripe); }
    const plaque = new THREE.Mesh(new THREE.PlaneGeometry(size * .7, size * .16), new THREE.MeshBasicMaterial({ map: texture(plaqueCanvas(p.name || '')), transparent: true })); plaque.position.set(0, -size * .4, .034); g.add(plaque);
    const glow = new THREE.PointLight(accent, isPlayer ? 2.5 : 1.6, 3, 2); glow.position.set(0, 0, .8); g.add(glow);
    if (!isPlayer) { const stand = new THREE.Mesh(new THREE.CylinderGeometry(.05, .18, y - .55, 12), new THREE.MeshStandardMaterial({ color: 0x151a24, metalness: .7, roughness: .4 })); stand.position.set(0, -(y - .55) / 2 - size * .55, 0); g.add(stand); }
    g.userData = { index, isPlayer, baseY: y }; g.lookAt(this.camera.position.x, y, this.camera.position.z); this.root.add(g); if (!isPlayer) this.plates.push(g);
  }

  bindInput() {
    const el = this.renderer.domElement;
    el.addEventListener('pointermove', e => { const r = el.getBoundingClientRect(); this.mouse.set(((e.clientX - r.left) / r.width) * 2 - 1, -((e.clientY - r.top) / r.height) * 2 + 1); this.parallax.set(this.mouse.x, this.mouse.y); el.style.cursor = this.pick() ? 'pointer' : 'default'; });
    el.addEventListener('pointerleave', () => { this.mouse.set(-9, -9); this.parallax.set(0, 0); });
    el.addEventListener('click', () => { const g = this.pick(); if (g && g.userData.index != null) this.dotnet?.invokeMethodAsync('OnVisitor', g.userData.index); });
  }
  pick() { this.raycaster.setFromCamera(this.mouse, this.camera); const hits = this.raycaster.intersectObjects(this.plates, true); if (!hits.length) return null; let o = hits[0].object; while (o && o.userData.index == null) o = o.parent; return o; }
  resize() { const w = this.container.clientWidth, h = this.container.clientHeight; if (!w || !h) return; this.renderer.setSize(w, h); this.composer.setSize(w, h); this.camera.aspect = w / h; this.camera.updateProjectionMatrix(); }
  loop() {
    if (this.disposed) return;
    setTimeout(() => requestAnimationFrame(this.loop), 66);   // ~15 fps is plenty for a room that only breathes
    if (document.hidden) return;
    const t = this.clock.getElapsedTime();
    this.camera.position.x += ((this.parallax.x * .4) - this.camera.position.x) * .04; this.camera.position.y += ((3.1 + this.parallax.y * .15) - this.camera.position.y) * .04; this.camera.lookAt(0, 2.2, -4);
    if (this.holo) this.holo.rotation.y = t * .12;
    if (this.signal) this.signal(t);
    if (this.planet) this.planet.rotation.y = t * .02;
    if (this.hull) { this.hull.position.x = -8 + ((t * .25) % 16); }
    const hovered = this.pick();
    for (const g of this.plates) { const target = g === hovered ? g.userData.baseY + .12 : g.userData.baseY + Math.sin(t * 1.2 + g.position.x) * .02; g.position.y += (target - g.position.y) * .1; }
    this.root.traverse(o => { if (o.userData.banner) { const pos = o.geometry.attributes.position; for (let i = 0; i < pos.count; i++) { const y = pos.getY(i); pos.setZ(i, Math.sin(t * 1.5 + y * 2.2) * .04 * (1 - (y + 1.7) / 3.4)); } pos.needsUpdate = true; } });
    this.composer.render();
  }
  dispose() { this.disposed = true; this.resizeObserver.disconnect(); this.renderer.dispose(); this.renderer.domElement.remove(); }
}

export function plaqueCanvas(name) {
  const c = document.createElement('canvas'); c.width = 256; c.height = 56; const ctx = c.getContext('2d');
  ctx.fillStyle = '#1a1407'; ctx.fillRect(0, 0, 256, 56); ctx.strokeStyle = '#c9a24a'; ctx.lineWidth = 3; ctx.strokeRect(2, 2, 252, 52);
  ctx.fillStyle = '#e6c77a'; ctx.font = '600 24px Cinzel, serif'; ctx.textAlign = 'center'; ctx.textBaseline = 'middle'; ctx.fillText(name.toUpperCase(), 128, 30, 236);
  return c;
}
export function stripeCanvas(accent) {
  const c = document.createElement('canvas'); c.width = 128; c.height = 170; const ctx = c.getContext('2d');
  const bg = ctx.createLinearGradient(0, 0, 0, 170); bg.addColorStop(0, '#1b2233'); bg.addColorStop(1, '#0b0e16'); ctx.fillStyle = bg; ctx.fillRect(0, 0, 128, 170); ctx.fillStyle = accent; ctx.globalAlpha = .32;
  for (let i = -170; i < 300; i += 44) { ctx.beginPath(); ctx.moveTo(i, 170); ctx.lineTo(i + 10, 170); ctx.lineTo(i + 10 + 170, 0); ctx.lineTo(i + 170, 0); ctx.closePath(); ctx.fill(); }
  ctx.globalAlpha = 1; ctx.fillStyle = accent; ctx.fillRect(0, 0, 128, 8);
  ctx.globalAlpha = 1; const g = ctx.createLinearGradient(0, 0, 0, 170); g.addColorStop(0, 'rgba(0,0,0,0)'); g.addColorStop(1, 'rgba(0,0,0,.6)'); ctx.fillStyle = g; ctx.fillRect(0, 0, 128, 170);
  return c;
}
export function nebulaCanvas(size, seed) {
  const c = noiseCanvas(size, seed, 48, 40, 120); const ctx = c.getContext('2d'); ctx.globalCompositeOperation = 'multiply'; const g = ctx.createLinearGradient(0, 0, size, size); g.addColorStop(0, '#3a4a9a'); g.addColorStop(.5, '#6a3a86'); g.addColorStop(1, '#2a7a8a'); ctx.fillStyle = g; ctx.fillRect(0, 0, size, size);
  ctx.globalCompositeOperation = 'destination-in'; const rg = ctx.createRadialGradient(size / 2, size / 2, size * .1, size / 2, size / 2, size * .5); rg.addColorStop(0, 'rgba(0,0,0,1)'); rg.addColorStop(1, 'rgba(0,0,0,0)'); ctx.fillStyle = rg; ctx.fillRect(0, 0, size, size); return c;
}

window.praetoriaOffice = {
  offices: new Map(),
  create(id, container, dotnet) { this.destroy(id); const o = new Office(container, dotnet); this.offices.set(id, o); return true; },
  build(id, json) { const o = this.offices.get(id); if (!o) return false; o.build(JSON.parse(json)); return true; },
  destroy(id) { const o = this.offices.get(id); if (o) { o.dispose(); this.offices.delete(id); } }
};
