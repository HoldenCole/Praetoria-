// Praetoria — further lit sets built like the office (docs/ART_DIRECTION.md §2): the Senate chamber (the political screen)
// and the Academy's parade ground (the cadet screen). Both take plain JSON from Blazor and stand the people in it as
// framed cameo plates; clicking a plate reports back.
import * as THREE from 'three';
import { EffectComposer } from 'three/addons/postprocessing/EffectComposer.js';
import { RenderPass } from 'three/addons/postprocessing/RenderPass.js';
import { UnrealBloomPass } from 'three/addons/postprocessing/UnrealBloomPass.js';
import { RoomEnvironment } from 'three/addons/environments/RoomEnvironment.js';
import { noiseCanvas, texture, sigilCanvas, plaqueCanvas, nebulaCanvas, PLANET } from './office.js';
import { render as cameo } from './portrait.js';

const BRASS = () => new THREE.MeshStandardMaterial({ color: 0x6b5a35, metalness: .85, roughness: .35 });
const BRIGHT_BRASS = () => new THREE.MeshStandardMaterial({ color: 0xc9a24a, metalness: .9, roughness: .3 });

class Set {
  constructor(container, dotnet, cam) {
    this.container = container; this.dotnet = dotnet; this.disposed = false; this.plates = []; this.animated = [];
    const w = container.clientWidth || 960, h = container.clientHeight || 540;
    this.renderer = new THREE.WebGLRenderer({ antialias: true }); this.renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 1.5)); this.renderer.setSize(w, h);
    this.renderer.toneMapping = THREE.ACESFilmicToneMapping; this.renderer.toneMappingExposure = 1.0; this.renderer.shadowMap.enabled = true; this.renderer.shadowMap.type = THREE.PCFSoftShadowMap;
    this.renderer.domElement.className = 'gl'; container.appendChild(this.renderer.domElement);
    this.scene = new THREE.Scene(); this.scene.fog = new THREE.FogExp2(0x070a12, 0.014);
    const pm = new THREE.PMREMGenerator(this.renderer); this.scene.environment = pm.fromScene(new RoomEnvironment(), 0.04).texture;
    this.camera = new THREE.PerspectiveCamera(cam.fov, w / h, 0.1, 400); this.camBase = cam; this.camera.position.set(...cam.pos); this.camera.lookAt(...cam.look);
    this.composer = new EffectComposer(this.renderer); this.composer.addPass(new RenderPass(this.scene, this.camera)); this.composer.addPass(new UnrealBloomPass(new THREE.Vector2(w, h), 0.5, 0.6, 0.74));
    this.raycaster = new THREE.Raycaster(); this.mouse = new THREE.Vector2(-9, -9); this.parallax = new THREE.Vector2();
    this.root = new THREE.Group(); this.scene.add(this.root);
    const el = this.renderer.domElement;
    el.addEventListener('pointermove', e => { const r = el.getBoundingClientRect(); this.mouse.set(((e.clientX - r.left) / r.width) * 2 - 1, -((e.clientY - r.top) / r.height) * 2 + 1); this.parallax.set(this.mouse.x, this.mouse.y); el.style.cursor = this.pick() ? 'pointer' : 'default'; });
    el.addEventListener('pointerleave', () => { this.mouse.set(-9, -9); this.parallax.set(0, 0); });
    el.addEventListener('click', () => { const g = this.pick(); if (g && g.userData.key != null) this.dotnet?.invokeMethodAsync('OnPlate', String(g.userData.key)); });
    this.resizeObserver = new ResizeObserver(() => this.resize()); this.resizeObserver.observe(container);
    this.clock = new THREE.Clock(); this.loop = this.loop.bind(this); requestAnimationFrame(this.loop);
  }
  clear() { this.plates = []; this.animated = []; while (this.root.children.length) { const o = this.root.children[0]; this.root.remove(o); o.traverse?.(c => { c.geometry?.dispose?.(); c.material?.dispose?.(); }); } }
  plate(p, x, y, z, size, key, faceTo) {
    const g = new THREE.Group(); g.position.set(x, y, z);
    const frame = new THREE.Mesh(new THREE.BoxGeometry(size * .82, size * 1.08, .06), new THREE.MeshStandardMaterial({ color: 0x1a1407, metalness: .6, roughness: .4 })); g.add(frame);
    const edge = new THREE.Mesh(new THREE.BoxGeometry(size * .86, size * 1.12, .04), BRIGHT_BRASS()); edge.position.z = -.012; g.add(edge);
    if (p.bust) { const tex = new THREE.TextureLoader().load(cameo({ ...p.bust, size: 256 })); tex.colorSpace = THREE.SRGBColorSpace; const img = new THREE.Mesh(new THREE.PlaneGeometry(size * .78, size * .78), new THREE.MeshStandardMaterial({ map: tex, roughness: .5, metalness: .1 })); img.position.set(0, size * .13, .034); g.add(img); }
    const plaque = new THREE.Mesh(new THREE.PlaneGeometry(size * .7, size * .16), new THREE.MeshBasicMaterial({ map: texture(plaqueCanvas(p.name || '')), transparent: true })); plaque.position.set(0, -size * .4, .034); g.add(plaque);
    const glow = new THREE.PointLight(new THREE.Color(p.accent || '#c9a24a'), 1.4, 3, 2); glow.position.set(0, 0, .8); g.add(glow);
    const stand = new THREE.Mesh(new THREE.CylinderGeometry(.05, .2, Math.max(.2, y - size * .56), 12), new THREE.MeshStandardMaterial({ color: 0x151a24, metalness: .7, roughness: .4 })); stand.position.set(0, -(y - size * .56) / 2 - size * .56, 0); g.add(stand);
    g.userData = { key, baseY: y }; g.lookAt(faceTo ? faceTo.x : this.camera.position.x, y, faceTo ? faceTo.z : this.camera.position.z); this.root.add(g); if (key != null) this.plates.push(g); return g;
  }
  pick() { this.raycaster.setFromCamera(this.mouse, this.camera); const hits = this.raycaster.intersectObjects(this.plates, true); if (!hits.length) return null; let o = hits[0].object; while (o && o.userData.key == null) o = o.parent; return o; }
  resize() { const w = this.container.clientWidth, h = this.container.clientHeight; if (!w || !h) return; this.renderer.setSize(w, h); this.composer.setSize(w, h); this.camera.aspect = w / h; this.camera.updateProjectionMatrix(); }
  loop() {
    if (this.disposed) return; setTimeout(() => requestAnimationFrame(this.loop), 66); if (document.hidden) return;
    const t = this.clock.getElapsedTime(); const c = this.camBase;
    this.camera.position.x += ((c.pos[0] + this.parallax.x * .4) - this.camera.position.x) * .04; this.camera.position.y += ((c.pos[1] + this.parallax.y * .15) - this.camera.position.y) * .04; this.camera.lookAt(...c.look);
    const hovered = this.pick();
    for (const g of this.plates) { const target = g === hovered ? g.userData.baseY + .12 : g.userData.baseY + Math.sin(t * 1.2 + g.position.x) * .015; g.position.y += (target - g.position.y) * .1; }
    for (const a of this.animated) a(t);
    this.composer.render();
  }
  dispose() { this.disposed = true; this.resizeObserver.disconnect(); this.renderer.dispose(); this.renderer.domElement.remove(); }
}

// ---------------- the Senate chamber ----------------
export class Senate extends Set {
  constructor(container, dotnet) { super(container, dotnet, { fov: 38, pos: [0, 4.6, 11.5], look: [0, 2.0, -5] }); }
  build(opts) {
    this.clear(); const R = this.root; const accent = new THREE.Color(opts.accent || '#c9a24a');
    const stone = new THREE.MeshStandardMaterial({ color: 0x2a2f3c, roughness: .55, metalness: .25, map: texture(noiseCanvas(256, 11, 36, 150, 70), 6) });
    const floor = new THREE.Mesh(new THREE.CircleGeometry(16, 64), stone); floor.rotation.x = -Math.PI / 2; floor.receiveShadow = true; R.add(floor);
    for (const r of [3.2, 6.5, 9.8]) { const ring = new THREE.Mesh(new THREE.RingGeometry(r, r + .08, 96), BRIGHT_BRASS()); ring.rotation.x = -Math.PI / 2; ring.position.y = .004; R.add(ring); }
    // dais with the speaker's podium (or the throne under the Imperium)
    const dais = new THREE.Mesh(new THREE.CylinderGeometry(2.6, 2.9, .5, 48), new THREE.MeshStandardMaterial({ color: 0x1b1f2a, metalness: .5, roughness: .4 })); dais.position.set(0, .25, -5); dais.receiveShadow = true; dais.castShadow = true; R.add(dais);
    const dais2 = new THREE.Mesh(new THREE.CylinderGeometry(1.8, 2.1, .4, 48), dais.material); dais2.position.set(0, .7, -5); R.add(dais2);
    if (opts.imperium) { const seat = new THREE.Mesh(new THREE.BoxGeometry(1.2, 2.6, .5), BRIGHT_BRASS()); seat.position.set(0, 2.2, -5.6); R.add(seat); const seatBase = new THREE.Mesh(new THREE.BoxGeometry(1.4, .6, 1.2), new THREE.MeshStandardMaterial({ color: 0x3a1a1e, roughness: .6 })); seatBase.position.set(0, 1.2, -5.3); R.add(seatBase); }
    else { const podium = new THREE.Mesh(new THREE.BoxGeometry(1.1, 1.3, .6), new THREE.MeshStandardMaterial({ color: 0x3a2e1c, roughness: .6 })); podium.position.set(0, 1.55, -4.6); R.add(podium); const top = new THREE.Mesh(new THREE.BoxGeometry(1.3, .08, .8), BRIGHT_BRASS()); top.position.set(0, 2.24, -4.6); R.add(top); }
    // colonnade and dome
    const colMat = new THREE.MeshStandardMaterial({ color: 0x3a4152, roughness: .7, metalness: .1, map: texture(noiseCanvas(128, 4, 20, 150, 50), 2) });
    const N = 16;
    for (let i = 0; i < N; i++) { const a = (i / N) * Math.PI * 2; if (Math.abs(a - Math.PI / 2) < .35) continue; const x = Math.cos(a) * 12.5, z = Math.sin(a) * 12.5 - 2; const col = new THREE.Mesh(new THREE.CylinderGeometry(.42, .5, 9, 16), colMat); col.position.set(x, 4.5, z); col.castShadow = true; R.add(col); const cap = new THREE.Mesh(new THREE.BoxGeometry(1.1, .3, 1.1), BRASS()); cap.position.set(x, 9.1, z); R.add(cap); }
    const ringWall = new THREE.Mesh(new THREE.CylinderGeometry(14, 14, 11, 64, 1, true), new THREE.MeshStandardMaterial({ color: 0x141924, roughness: .8, side: THREE.BackSide })); ringWall.position.set(0, 5.5, -2); R.add(ringWall);
    const dome = new THREE.Mesh(new THREE.SphereGeometry(14, 48, 24, 0, Math.PI * 2, 0, Math.PI / 2), new THREE.MeshStandardMaterial({ color: 0x0e121c, roughness: .9, side: THREE.BackSide })); dome.position.set(0, 9.5, -2); R.add(dome);
    const oculus = new THREE.Mesh(new THREE.CircleGeometry(2.6, 48), new THREE.MeshBasicMaterial({ color: 0xcfe0ff })); oculus.rotation.x = Math.PI / 2; oculus.position.set(0, 23.3, -2); R.add(oculus);
    const shaft = new THREE.Mesh(new THREE.CylinderGeometry(2.6, 3.4, 22, 32, 1, true), new THREE.MeshBasicMaterial({ color: 0x9fb8ff, transparent: true, opacity: .05, side: THREE.DoubleSide, depthWrite: false })); shaft.position.set(0, 12, -2); R.add(shaft);
    const sun = new THREE.DirectionalLight(0xdfe8ff, 1.8); sun.position.set(2, 20, -1); sun.target.position.set(0, 0, -4); sun.castShadow = true; sun.shadow.camera.left = -14; sun.shadow.camera.right = 14; sun.shadow.camera.top = 14; sun.shadow.camera.bottom = -14; sun.shadow.mapSize.set(1024, 1024); R.add(sun); R.add(sun.target);
    R.add(new THREE.HemisphereLight(0x3a4a7a, 0x0a0c10, .5));
    const lamp = new THREE.PointLight(0xffd79a, 12, 14, 2); lamp.position.set(0, 4, -4.5); R.add(lamp);
    // house banners between the columns, ordered by influence; benches in three tiers
    const houses = opts.houses || [];
    const C = new THREE.Vector3(0, 0, -5);   // the chamber turns about the dais
    houses.forEach((h, i) => { const a = Math.PI * 0.12 + (i / Math.max(1, houses.length - 1)) * (Math.PI * 0.76); const x = Math.cos(a) * 13.2, z = C.z - Math.sin(a) * 13.2; const b = new THREE.Mesh(new THREE.PlaneGeometry(1.5, 3.6, 8, 16), new THREE.MeshStandardMaterial({ map: texture(sigilCanvas(h.id, h.accent)), roughness: .85, side: THREE.DoubleSide, emissive: new THREE.Color(h.accent), emissiveIntensity: .12 })); b.position.set(x, 6.8, z); b.lookAt(0, 6.8, C.z); b.userData.banner = true; R.add(b); const rod = new THREE.Mesh(new THREE.CylinderGeometry(.04, .04, 2.0, 8), BRASS()); rod.position.set(x, 8.7, z); rod.lookAt(0, 8.7, C.z); rod.rotateX(Math.PI / 2); R.add(rod); });
    // three tiers of benches around the far half of the dais, built from segments so they read as seats
    const benchMat = new THREE.MeshStandardMaterial({ color: 0x2a2320, roughness: .7 });
    for (let tier = 0; tier < 3; tier++) { const r = 5.2 + tier * 2.3; const segs = 14; for (let k = 0; k < segs; k++) { const a = Math.PI * 0.1 + (k + .5) / segs * (Math.PI * 0.8); const x = Math.cos(a) * r, z = C.z - Math.sin(a) * r; const seat = new THREE.Mesh(new THREE.BoxGeometry(2 * Math.PI * r * 0.8 / segs * .92, .5, 1.1), benchMat); seat.position.set(x, .6 + tier * .55, z); seat.lookAt(0, .6 + tier * .55, C.z); seat.castShadow = true; seat.receiveShadow = true; R.add(seat); const back = new THREE.Mesh(new THREE.BoxGeometry(2 * Math.PI * r * 0.8 / segs * .92, .7, .15), benchMat); back.position.set(x * (r + .5) / r, 1.15 + tier * .55, C.z - (C.z - z) * (r + .5) / r); back.lookAt(0, 1.15 + tier * .55, C.z); R.add(back); } const step = new THREE.Mesh(new THREE.RingGeometry(r - 1.0, r + 1.3, 96, 1, Math.PI * 1.05, Math.PI * 0.9), stone); step.rotation.x = -Math.PI / 2; step.position.set(0, .2 + tier * .55, C.z); R.add(step); }
    // the speaker at the podium, the heads of the great houses on the benches
    if (opts.player) this.plate(opts.player, 0, 2.95, -4.3, 1.05, null);
    (opts.members || []).forEach((m, i) => { const tier = Math.min(2, Math.floor(i / 5)); const slot = i % 5; const a = Math.PI * 0.2 + (slot / 4) * (Math.PI * 0.6); const r = 5.2 + tier * 2.3; const x = Math.cos(a) * r, z = C.z - Math.sin(a) * r + .2; this.plate(m, x, 1.95 + tier * .55, z, .85, m.key, new THREE.Vector3(0, 1.9, C.z)); });
    this.animated.push(t => { R.traverse(o => { if (o.userData.banner) { const pos = o.geometry.attributes.position; for (let i = 0; i < pos.count; i++) { const y = pos.getY(i); pos.setZ(i, Math.sin(t * 1.3 + y * 2.2 + o.position.x) * .04 * (1 - (y + 1.6) / 3.2)); } pos.needsUpdate = true; } }); });
    // haze
    for (let i = 0; i < 4; i++) { const hz = new THREE.Mesh(new THREE.PlaneGeometry(26, 12), new THREE.MeshBasicMaterial({ color: 0x9fb8ff, transparent: true, opacity: .018, depthWrite: false, blending: THREE.AdditiveBlending })); hz.position.set(0, 6, -8 + i * 2.5); R.add(hz); }
  }
}

// ---------------- the Academy's parade ground ----------------
export class Academy extends Set {
  constructor(container, dotnet) { super(container, dotnet, { fov: 40, pos: [0, 3.2, 14], look: [0, 1.9, -3] }); }
  build(opts) {
    this.clear(); const R = this.root;
    const flag = new THREE.MeshStandardMaterial({ color: 0x3b3f48, roughness: .85, metalness: .05, map: texture(noiseCanvas(256, 21, 32, 150, 60), 8) });
    const ground = new THREE.Mesh(new THREE.PlaneGeometry(40, 40), flag); ground.rotation.x = -Math.PI / 2; ground.position.z = -6; ground.receiveShadow = true; R.add(ground);
    const seamMat = new THREE.MeshStandardMaterial({ color: 0x5a5040, roughness: .9 });
    for (let x = -12; x <= 12; x += 6) { const m = new THREE.Mesh(new THREE.BoxGeometry(.04, .004, 40), seamMat); m.position.set(x, .003, -6); R.add(m); }
    for (let z = -24; z <= 12; z += 6) { const m = new THREE.Mesh(new THREE.BoxGeometry(40, .004, .04), seamMat); m.position.set(0, .003, z); R.add(m); }
    // sky: dawn over a world, a moon, the academy's towers in silhouette
    const sky = new THREE.Mesh(new THREE.SphereGeometry(120, 32, 16, 0, Math.PI * 2, 0, Math.PI * .55), new THREE.ShaderMaterial({ side: THREE.BackSide, depthWrite: false, uniforms: {}, vertexShader: `varying vec3 vP; void main(){ vP = position; gl_Position = projectionMatrix * modelViewMatrix * vec4(position,1.0); }`, fragmentShader: `varying vec3 vP; void main(){ float h = normalize(vP).y; vec3 top = vec3(0.02,0.03,0.08); vec3 mid = vec3(0.10,0.14,0.32); vec3 low = vec3(0.85,0.45,0.25); vec3 c = mix(mid, top, smoothstep(0.1, 0.8, h)); c = mix(low, c, smoothstep(0.0, 0.22, h)); gl_FragColor = vec4(c, 1.0); }` })); sky.position.y = -2; R.add(sky);
    const [pc, pg] = PLANET[opts.planetClass || 'terran'] || PLANET.terran;
    const ptex = window.praetoriaFx ? new THREE.TextureLoader().load(window.praetoriaFx.planet(opts.planetClass || 'gas', opts.seed || 'academy', 512)) : null; if (ptex) ptex.colorSpace = THREE.SRGBColorSpace;
    const world = new THREE.Sprite(new THREE.SpriteMaterial({ map: ptex, transparent: true, depthWrite: false })); world.scale.set(26, 26, 1); world.position.set(-30, 24, -100); R.add(world);
    const starsGeo = new THREE.BufferGeometry(); const sp = []; let t0 = 7; const rr = () => { t0 = (t0 * 1664525 + 1013904223) >>> 0; return t0 / 4294967296; }; for (let i = 0; i < 700; i++) sp.push(-100 + rr() * 200, 10 + rr() * 80, -60 - rr() * 60); starsGeo.setAttribute('position', new THREE.Float32BufferAttribute(sp, 3)); R.add(new THREE.Points(starsGeo, new THREE.PointsMaterial({ color: 0xffffff, size: .25, transparent: true, opacity: .6 })));
    const towerMat = new THREE.MeshStandardMaterial({ color: 0x0b0e16, roughness: .9 });
    const towerMat2 = new THREE.MeshStandardMaterial({ color: 0x141a28, roughness: .9, emissive: 0x0a0f1e, emissiveIntensity: .4 });
    for (let i = 0; i < 14; i++) { const h = 14 + (i * 7) % 22, w = 3 + (i * 3) % 5; const tw = new THREE.Mesh(new THREE.BoxGeometry(w, h, w), towerMat2); tw.position.set(-55 + i * 8.5, h / 2 - 1, -95 - (i % 4) * 10); R.add(tw); for (let k = 1; k < h / 3; k++) { const lt = new THREE.Mesh(new THREE.BoxGeometry(w * .7, .2, .1), new THREE.MeshBasicMaterial({ color: (i + k) % 3 ? 0xffd080 : 0x8fb3ff })); lt.position.set(tw.position.x, k * 3 - .5, tw.position.z + w / 2 + .06); R.add(lt); } }
    // colonnade on both flanks, a reviewing dais at the far end with the cohort's banners
    const colMat = new THREE.MeshStandardMaterial({ color: 0x4a4f5c, roughness: .75, map: texture(noiseCanvas(128, 9, 20, 150, 50), 2) });
    for (const sx of [-1, 1]) for (let i = 0; i < 6; i++) { const col = new THREE.Mesh(new THREE.CylinderGeometry(.4, .46, 7, 14), colMat); col.position.set(sx * 11, 3.5, 4 - i * 4.5); col.castShadow = true; R.add(col); const cap = new THREE.Mesh(new THREE.BoxGeometry(1.1, .3, 1.1), BRASS()); cap.position.set(sx * 11, 7.1, 4 - i * 4.5); R.add(cap); }
    for (const sx of [-1, 1]) { const beam = new THREE.Mesh(new THREE.BoxGeometry(.7, .5, 26), colMat); beam.position.set(sx * 11, 7.5, -7.5); R.add(beam); }
    const dais = new THREE.Mesh(new THREE.BoxGeometry(14, .8, 4), new THREE.MeshStandardMaterial({ color: 0x1b1f2a, metalness: .4, roughness: .5 })); dais.position.set(0, .4, -20); dais.receiveShadow = true; R.add(dais);
    const steps = new THREE.Mesh(new THREE.BoxGeometry(16, .4, 1.2), dais.material); steps.position.set(0, .2, -17.5); R.add(steps);
    const wall = new THREE.Mesh(new THREE.BoxGeometry(18, 9, 1), colMat); wall.position.set(0, 4.5, -23); R.add(wall);
    const crest = new THREE.Mesh(new THREE.PlaneGeometry(4, 2.4), new THREE.MeshBasicMaterial({ map: texture(crestCanvas()), transparent: true })); crest.position.set(0, 7.2, -22.45); R.add(crest);
    (opts.houses || []).forEach((h, i) => { const n = Math.max(1, opts.houses.length - 1); const x = -7 + (i / n) * 14; const b = new THREE.Mesh(new THREE.PlaneGeometry(1.2, 3.0, 8, 16), new THREE.MeshStandardMaterial({ map: texture(sigilCanvas(h.id, h.accent)), roughness: .9, side: THREE.DoubleSide })); b.position.set(x, 4.6, -22.4); b.userData.banner = true; R.add(b); });
    // braziers along the flanks
    for (const sx of [-1, 1]) for (let i = 0; i < 3; i++) { const z = 1 - i * 7; const bowl = new THREE.Mesh(new THREE.CylinderGeometry(.5, .3, .5, 12), BRASS()); bowl.position.set(sx * 8.5, 1.3, z); R.add(bowl); const stem = new THREE.Mesh(new THREE.CylinderGeometry(.08, .12, 1.1, 8), BRASS()); stem.position.set(sx * 8.5, .55, z); R.add(stem); const fire = new THREE.Mesh(new THREE.SphereGeometry(.22, 10, 8), new THREE.MeshBasicMaterial({ color: 0xffa040 })); fire.position.set(sx * 8.5, 1.6, z); R.add(fire); const l = new THREE.PointLight(0xff9a4a, 6, 10, 2); l.position.set(sx * 8.5, 1.9, z); R.add(l); this.animated.push(t => { fire.scale.setScalar(1 + .15 * Math.sin(t * 7 + z)); l.intensity = 5 + Math.sin(t * 9 + z) * 1.2; }); }
    const dawn = new THREE.DirectionalLight(0xffc9a0, 1.6); dawn.position.set(-20, 10, -30); dawn.target.position.set(0, 0, -5); dawn.castShadow = true; dawn.shadow.camera.left = -16; dawn.shadow.camera.right = 16; dawn.shadow.camera.top = 16; dawn.shadow.camera.bottom = -16; dawn.shadow.mapSize.set(1024, 1024); R.add(dawn); R.add(dawn.target);
    R.add(new THREE.HemisphereLight(0x6a7aa0, 0x1a1410, .55));
    // the cohort in two ranks; the instructor on the dais; you at the front
    if (opts.player) this.plate(opts.player, -2.6, 1.8, 2.4, 1.05, null);
    (opts.cadets || []).forEach((c, i) => { const rank = Math.floor(i / 5), slot = i % 5; const x = -6 + slot * 3 + (rank % 2) * 1.5; this.plate(c, x, 1.85, -3 - rank * 3.5, 1.05, c.key, new THREE.Vector3(0, 1.7, 8)); });
    if (opts.instructor) this.plate(opts.instructor, 0, 2.9, -19.2, 1.1, opts.instructor.key, new THREE.Vector3(0, 2, 8));
    this.animated.push(t => { R.traverse(o => { if (o.userData.banner) { const pos = o.geometry.attributes.position; for (let i = 0; i < pos.count; i++) { const y = pos.getY(i); pos.setZ(i, Math.sin(t * 1.6 + y * 2.4 + o.position.x) * .05 * (1 - (y + 1.5) / 3.0)); } pos.needsUpdate = true; } }); });
  }
}
function crestCanvas() { const c = document.createElement('canvas'); c.width = 256; c.height = 154; const ctx = c.getContext('2d'); ctx.strokeStyle = '#e6c77a'; ctx.lineWidth = 5; ctx.beginPath(); ctx.moveTo(128, 14); ctx.lineTo(214, 40); ctx.lineTo(214, 100); ctx.quadraticCurveTo(190, 140, 128, 146); ctx.quadraticCurveTo(66, 140, 42, 100); ctx.lineTo(42, 40); ctx.closePath(); ctx.stroke(); ctx.fillStyle = '#e6c77a'; ctx.font = '600 22px Cinzel, serif'; ctx.textAlign = 'center'; ctx.fillText('ACADEMY', 128, 90); ctx.font = '14px Cinzel, serif'; ctx.fillText('OF THE REACH', 128, 112); return c; }

window.praetoriaSets = {
  sets: new Map(),
  create(kind, id, container, dotnet) { this.destroy(id); const s = kind === 'senate' ? new Senate(container, dotnet) : new Academy(container, dotnet); this.sets.set(id, s); return true; },
  build(id, json) { const s = this.sets.get(id); if (!s) return false; s.build(JSON.parse(json)); return true; },
  destroy(id) { const s = this.sets.get(id); if (s) { s.dispose(); this.sets.delete(id); } }
};
