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
    const stand = new THREE.Mesh(new THREE.CylinderGeometry(.05, .2, Math.max(.2, y - size * .56), 12), new THREE.MeshStandardMaterial({ color: 0x4a4f5c, metalness: .5, roughness: .45 })); stand.position.set(0, -(y - size * .56) / 2 - size * .56, 0); g.add(stand);
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


// ---------------- the ancestral hall (Dynasty) ----------------
export class Hall extends Set {
  constructor(container, dotnet) { super(container, dotnet, { fov: 40, pos: [0, 3.6, 15], look: [0, 2.4, -6] }); }
  build(opts) {
    this.clear(); const R = this.root; const accent = new THREE.Color(opts.accent || '#c9a24a');
    const W = 16, D = 34, H = 9;
    const stone = new THREE.MeshStandardMaterial({ color: 0x5a5f6c, roughness: .6, metalness: .15, map: texture(noiseCanvas(256, 13, 30, 150, 60), 6) });
    const floor = new THREE.Mesh(new THREE.PlaneGeometry(W, D), stone); floor.rotation.x = -Math.PI / 2; floor.position.set(0, 0, -D / 2 + 6); floor.receiveShadow = true; R.add(floor);
    const runner = new THREE.Mesh(new THREE.PlaneGeometry(3.4, D - 2), new THREE.MeshStandardMaterial({ color: accent.clone().multiplyScalar(.35), roughness: 1 })); runner.rotation.x = -Math.PI / 2; runner.position.set(0, .004, -D / 2 + 6); R.add(runner);
    const wallMat = new THREE.MeshStandardMaterial({ color: 0x4e5567, roughness: .85, metalness: .05, map: texture(noiseCanvas(256, 2, 40, 150, 50), 4) });
    for (const sx of [-1, 1]) { const wall = new THREE.Mesh(new THREE.BoxGeometry(.5, H, D), wallMat); wall.position.set(sx * W / 2, H / 2, -D / 2 + 6); wall.receiveShadow = true; R.add(wall); }
    const far = new THREE.Mesh(new THREE.BoxGeometry(W, H + 8, .5), wallMat); far.position.set(0, (H + 8) / 2, -D + 6); R.add(far);
    // vaulted ceiling: a half cylinder
    // vaulted ceiling: the inside of a barrel laid along the hall, ribbed at every bay
    const vault = new THREE.Mesh(new THREE.CylinderGeometry(W / 2, W / 2, D + 14, 40, 1, true), new THREE.MeshStandardMaterial({ color: 0x5a6278, roughness: .9, side: THREE.BackSide, emissive: 0x0e1218, map: texture(noiseCanvas(256, 9, 30, 150, 40), 3) })); vault.rotation.x = Math.PI / 2; vault.position.set(0, H - 3, -D / 2 + 13); R.add(vault);
    for (let i = 0; i < 7; i++) { const rib = new THREE.Mesh(new THREE.TorusGeometry(W / 2 - .05, .14, 8, 40, Math.PI), new THREE.MeshStandardMaterial({ color: 0x3a3226, roughness: .7 })); rib.position.set(0, H - 3, 3 - i * 5.5); R.add(rib); }
    for (let i = 0; i < 6; i++) { const z = 2 - i * 5.5; for (const sx of [-1, 1]) { const col = new THREE.Mesh(new THREE.CylinderGeometry(.45, .55, H, 16), stone); col.position.set(sx * (W / 2 - 1.2), H / 2, z); col.castShadow = true; R.add(col); } const lampY = H - 1.2; const lamp = new THREE.Mesh(new THREE.SphereGeometry(.22, 12, 8), new THREE.MeshBasicMaterial({ color: 0xffe2b0 })); lamp.position.set(0, lampY, z); R.add(lamp); const chain = new THREE.Mesh(new THREE.CylinderGeometry(.03, .03, 2.4, 6), BRASS()); chain.position.set(0, lampY + 1.2, z); R.add(chain); const l = new THREE.PointLight(0xffd79a, 28, 16, 2); l.position.set(0, lampY - .3, z); R.add(l);
      for (const sx of [-1, 1]) { const sconce = new THREE.Mesh(new THREE.BoxGeometry(.3, .5, .2), new THREE.MeshBasicMaterial({ color: 0xffd8a0 })); sconce.position.set(sx * (W / 2 - .4), 4.2, z + 2.75); R.add(sconce); const sl = new THREE.PointLight(0xffc27a, 12, 10, 2); sl.position.set(sx * (W / 2 - 1.2), 4.2, z + 2.75); R.add(sl); } }
    R.add(new THREE.HemisphereLight(0x5a6a9a, 0x4a4038, 1.4));
    const key = new THREE.DirectionalLight(0xffe0c0, 1.1); key.position.set(4, 10, 8); key.target.position.set(0, 1, -8); key.castShadow = true; key.shadow.camera.left = -12; key.shadow.camera.right = 12; key.shadow.camera.top = 14; key.shadow.camera.bottom = -14; key.shadow.mapSize.set(1024, 1024); R.add(key); R.add(key.target);
    // the hearth at the far end and the house's great banner above it
    const hearth = new THREE.Mesh(new THREE.BoxGeometry(5, 3.4, 1.2), new THREE.MeshStandardMaterial({ color: 0x1b1612, roughness: .9 })); hearth.position.set(0, 1.7, -D + 6.9); R.add(hearth);
    const fireGlow = new THREE.PointLight(0xff8a3a, 14, 14, 2); fireGlow.position.set(0, 1.2, -D + 8); R.add(fireGlow);
    const fire = new THREE.Mesh(new THREE.SphereGeometry(.5, 12, 8), new THREE.MeshBasicMaterial({ color: 0xffa040 })); fire.position.set(0, 1.0, -D + 7.6); R.add(fire); this.animated.push(t => { fire.scale.set(1 + .15 * Math.sin(t * 7), 1 + .25 * Math.sin(t * 9 + 1), 1); fireGlow.intensity = 12 + Math.sin(t * 11) * 2.5; });
    const great = new THREE.Mesh(new THREE.PlaneGeometry(3.2, 7, 8, 16), new THREE.MeshStandardMaterial({ map: texture(sigilCanvas(opts.houseId || 'house', opts.accent || '#c9a24a')), roughness: .9, side: THREE.DoubleSide })); great.position.set(0, 5.6, -D + 6.4); great.userData.banner = true; R.add(great);
    // the tree: generations as rows down the hall; parents behind children; the dead hang on the walls as dimmed portraits
    const nodes = opts.nodes || []; const gens = Math.max(1, ...nodes.map(n => n.gen + 1)); const pos = {};
    for (let g = 0; g < gens; g++) { const row = nodes.filter(n => n.gen === g); const z = -2 - (gens - 1 - g) * 5.5; const spread = Math.min(10, row.length * 2.2); row.forEach((n, i) => { const x = row.length === 1 ? 0 : -spread / 2 + (i / (row.length - 1)) * spread; pos[n.key] = new THREE.Vector3(x, 0, z); }); }
    const lineMat = new THREE.LineBasicMaterial({ color: 0xc9a24a, transparent: true, opacity: .5 });
    for (const n of nodes) { const p = pos[n.key]; if (!p) continue; this.plate(n, p.x, n.me ? 2.1 : 1.85, p.z, n.me ? 1.15 : .95, n.key, new THREE.Vector3(p.x, 1.8, 20));
      for (const pk of (n.parents || [])) { const q = pos[pk]; if (!q) continue; const pts = [new THREE.Vector3(q.x, .02, q.z + .6), new THREE.Vector3(q.x, .02, (q.z + p.z) / 2), new THREE.Vector3(p.x, .02, (q.z + p.z) / 2), new THREE.Vector3(p.x, .02, p.z - .6)]; R.add(new THREE.Line(new THREE.BufferGeometry().setFromPoints(pts), lineMat)); }
      if (n.spouse && pos[n.spouse]) { const q = pos[n.spouse]; R.add(new THREE.Line(new THREE.BufferGeometry().setFromPoints([new THREE.Vector3(p.x, 1.2, p.z), new THREE.Vector3(q.x, 1.2, q.z)]), new THREE.LineBasicMaterial({ color: 0xe6c77a, transparent: true, opacity: .35 }))); }
    }
    this.animated.push(t => { R.traverse(o => { if (o.userData.banner) { const pos = o.geometry.attributes.position; for (let i = 0; i < pos.count; i++) { const y = pos.getY(i); pos.setZ(i, Math.sin(t * 1.1 + y * 1.2) * .06 * (1 - (y + 3.5) / 7)); } pos.needsUpdate = true; } }); });
    for (let i = 0; i < 4; i++) { const hz = new THREE.Mesh(new THREE.PlaneGeometry(W, H), new THREE.MeshBasicMaterial({ color: 0xffb070, transparent: true, opacity: .02, depthWrite: false, blending: THREE.AdditiveBlending })); hz.position.set(0, H / 2, -D + 8 + i * 4); R.add(hz); }
  }
}

// ---------------- the command deck (Military) ----------------
export class WarRoom extends Set {
  constructor(container, dotnet) { super(container, dotnet, { fov: 42, pos: [0, 3.4, 11], look: [0, 2.2, -6] }); }
  build(opts) {
    this.clear(); const R = this.root; const accent = new THREE.Color(opts.accent || '#c9a24a');
    const W = 18, D = 22, H = 7;
    const deck = new THREE.MeshStandardMaterial({ color: 0x1a1f2c, roughness: .35, metalness: .7, map: texture(noiseCanvas(256, 17, 18, 150, 70), 6) });
    const floor = new THREE.Mesh(new THREE.PlaneGeometry(W, D), deck); floor.rotation.x = -Math.PI / 2; floor.position.set(0, 0, -D / 2 + 5); floor.receiveShadow = true; R.add(floor);
    const seamMat = new THREE.MeshBasicMaterial({ color: 0x3a5a9a }); for (let x = -8; x <= 8; x += 4) { const m = new THREE.Mesh(new THREE.BoxGeometry(.04, .004, D), seamMat); m.position.set(x, .003, -D / 2 + 5); R.add(m); }
    const wallMat = new THREE.MeshStandardMaterial({ color: 0x141a26, roughness: .6, metalness: .5 });
    for (const sx of [-1, 1]) { const wall = new THREE.Mesh(new THREE.BoxGeometry(.5, H, D), wallMat); wall.position.set(sx * W / 2, H / 2, -D / 2 + 5); R.add(wall); }
    const ceil = new THREE.Mesh(new THREE.PlaneGeometry(W, D), new THREE.MeshStandardMaterial({ color: 0x2a3344, roughness: .8, metalness: .2, emissive: 0x0a0e16 })); ceil.rotation.x = Math.PI / 2; ceil.position.set(0, H, -D / 2 + 5); R.add(ceil);
    for (let i = 0; i < 4; i++) { for (const sx of [-1, 1]) { const strip = new THREE.Mesh(new THREE.PlaneGeometry(.25, 5), new THREE.MeshBasicMaterial({ color: 0xbfd4ff })); strip.rotation.x = Math.PI / 2; strip.position.set(sx * 4.5, H - .02, 2 - i * 5.5); R.add(strip); const sl = new THREE.PointLight(0x9fb8ff, 9, 11, 2); sl.position.set(sx * 4.5, H - .7, 2 - i * 5.5); R.add(sl); } }
    // the great viewport: the far wall is glass from waist height up, the fleet parked outside above the world
    const sill = new THREE.Mesh(new THREE.BoxGeometry(W, 1.6, .6), wallMat); sill.position.set(0, .8, -D + 5); R.add(sill);
    const lintel = new THREE.Mesh(new THREE.BoxGeometry(W, .6, .6), wallMat); lintel.position.set(0, H - .3, -D + 5); R.add(lintel);
    for (let i = -2; i <= 2; i++) { const mull = new THREE.Mesh(new THREE.BoxGeometry(.18, H - 2, .3), BRASS()); mull.position.set(i * W / 5, H / 2 + .4, -D + 5); R.add(mull); }
    const glass = new THREE.Mesh(new THREE.PlaneGeometry(W, H - 2.2), new THREE.MeshPhysicalMaterial({ color: 0x88aaff, transparent: true, opacity: .06, roughness: .05, depthWrite: false })); glass.position.set(0, H / 2 + .4, -D + 5.05); R.add(glass);
    const [pc, pg] = PLANET[opts.planetClass || 'terran'] || PLANET.terran;
    const ptex = window.praetoriaFx ? new THREE.TextureLoader().load(window.praetoriaFx.planet(opts.planetClass || 'terran', opts.seed || 'reach', 512)) : null; if (ptex) ptex.colorSpace = THREE.SRGBColorSpace;
    const world = new THREE.Sprite(new THREE.SpriteMaterial({ map: ptex, transparent: true, depthWrite: false })); world.scale.set(44, 44, 1); world.position.set(6, -12, -60); R.add(world);
    const starsGeo = new THREE.BufferGeometry(); const sp = []; let t0 = 3; const rr = () => { t0 = (t0 * 1664525 + 1013904223) >>> 0; return t0 / 4294967296; }; for (let i = 0; i < 1200; i++) sp.push(-90 + rr() * 180, -30 + rr() * 80, -40 - rr() * 60); starsGeo.setAttribute('position', new THREE.Float32BufferAttribute(sp, 3)); R.add(new THREE.Points(starsGeo, new THREE.PointsMaterial({ color: 0xffffff, size: .16, transparent: true, opacity: .85 })));
    const neb = new THREE.Mesh(new THREE.PlaneGeometry(160, 90), new THREE.MeshBasicMaterial({ map: texture(nebulaCanvas(256, 77), 1), transparent: true, opacity: .5, depthWrite: false })); neb.position.set(-10, 10, -95); R.add(neb);
    // the fleet outside: one hull silhouette per ship, grouped by fleet, with engine lights
    const hullMat = new THREE.MeshStandardMaterial({ color: 0x7a8396, metalness: .6, roughness: .45 }); const fleets = opts.fleets || [];
    fleets.forEach((f, fi) => { const cx = -7 + (fi % 3) * 7, cy = 4.5 + Math.floor(fi / 3) * 3, cz = -38 - fi * 3; const n = Math.min(12, f.ships || 1);
      for (let k = 0; k < n; k++) { const hull = new THREE.Group(); const L = f.classes?.[k] === 'cruiser' ? 3.4 : f.classes?.[k] === 'battleship' ? 4.6 : 2.2; const body = new THREE.Mesh(new THREE.CylinderGeometry(.22, .34, L, 8), hullMat); body.rotation.z = Math.PI / 2; hull.add(body); const fin = new THREE.Mesh(new THREE.BoxGeometry(L * .3, .6, .06), hullMat); fin.position.set(-L * .2, .3, 0); hull.add(fin); const eng = new THREE.Mesh(new THREE.SphereGeometry(.12, 8, 6), new THREE.MeshBasicMaterial({ color: accent.clone().lerp(new THREE.Color(0xffe8a3), .4) })); eng.position.set(-L / 2 - .05, 0, 0); hull.add(eng); hull.position.set(cx + (k % 4) * 2.4, cy + Math.floor(k / 4) * 1.3, cz - (k % 3) * 1.5); hull.rotation.y = .3; R.add(hull); this.animated.push(t => { hull.position.y += Math.sin(t * .7 + k) * .0015; }); }
      const glow = new THREE.PointLight(accent, 3, 12, 2); glow.position.set(cx + 3, cy, cz + 3); R.add(glow);
    });
    const sun = new THREE.DirectionalLight(0xdfe8ff, 1.8); sun.position.set(-8, 10, -20); sun.target.position.set(0, 1, -5); sun.castShadow = true; sun.shadow.camera.left = -12; sun.shadow.camera.right = 12; sun.shadow.camera.top = 12; sun.shadow.camera.bottom = -8; sun.shadow.mapSize.set(1024, 1024); R.add(sun); R.add(sun.target);
    R.add(new THREE.HemisphereLight(0x3a4a7a, 0x2a3040, 1.2));
    // the strategic table: the Reach as a hologram, war fronts in red
    const tx = 0, tz = -5; const table = new THREE.Mesh(new THREE.CylinderGeometry(2.6, 2.8, .9, 48), new THREE.MeshStandardMaterial({ color: 0x1a1f2c, metalness: .7, roughness: .35 })); table.position.set(tx, .45, tz); table.castShadow = true; R.add(table);
    const rim = new THREE.Mesh(new THREE.TorusGeometry(2.6, .06, 8, 64), BRIGHT_BRASS()); rim.rotation.x = Math.PI / 2; rim.position.set(tx, .92, tz); R.add(rim);
    const holo = new THREE.Group(); holo.position.set(tx, 1.5, tz); R.add(holo); const hcol = new THREE.Color(0x7aa2ff);
    const chart = opts.chart || { nodes: [], lanes: [] }; const xs = chart.nodes.map(n => n.x), ys = chart.nodes.map(n => n.y);
    const cx0 = xs.length ? (Math.min(...xs) + Math.max(...xs)) / 2 : 0, cy0 = ys.length ? (Math.min(...ys) + Math.max(...ys)) / 2 : 0; const sc = 2.1 / Math.max(1, ...chart.nodes.map(n => Math.max(Math.abs(n.x - cx0), Math.abs(n.y - cy0))));
    const P = n => new THREE.Vector3((n.x - cx0) * sc, 0, (n.y - cy0) * sc);
    const pts = []; const fronts = []; for (const l of chart.lanes) { const a = chart.nodes.find(n => n.id === l.a), b = chart.nodes.find(n => n.id === l.b); if (!a || !b) continue; (l.front ? fronts : pts).push(P(a), P(b)); }
    if (pts.length) holo.add(new THREE.LineSegments(new THREE.BufferGeometry().setFromPoints(pts), new THREE.LineBasicMaterial({ color: hcol, transparent: true, opacity: .35 })));
    if (fronts.length) holo.add(new THREE.LineSegments(new THREE.BufferGeometry().setFromPoints(fronts), new THREE.LineBasicMaterial({ color: 0xff5a3c, transparent: true, opacity: .9 })));
    for (const n of chart.nodes) { const q = P(n); const m = new THREE.Mesh(new THREE.SphereGeometry(n.mine ? .11 : .06, 8, 8), new THREE.MeshBasicMaterial({ color: n.color || '#8fb3ff' })); m.position.copy(q); holo.add(m); const halo = new THREE.Mesh(new THREE.SphereGeometry(n.mine ? .22 : .13, 8, 8), new THREE.MeshBasicMaterial({ color: n.color || '#8fb3ff', transparent: true, opacity: .18, blending: THREE.AdditiveBlending, depthWrite: false })); halo.position.copy(q); holo.add(halo); if (n.fleet) { const fm = new THREE.Mesh(new THREE.ConeGeometry(.09, .24, 6), new THREE.MeshBasicMaterial({ color: n.fleetColor || '#e6c77a' })); fm.position.set(q.x, .3, q.z); holo.add(fm); } }
    const disc = new THREE.Mesh(new THREE.CircleGeometry(2.4, 48), new THREE.MeshBasicMaterial({ color: hcol, transparent: true, opacity: .07, side: THREE.DoubleSide, depthWrite: false })); disc.rotation.x = -Math.PI / 2; holo.add(disc);
    const ring1 = new THREE.Mesh(new THREE.RingGeometry(1.2, 1.23, 64), new THREE.MeshBasicMaterial({ color: hcol, transparent: true, opacity: .25, side: THREE.DoubleSide, depthWrite: false })); ring1.rotation.x = -Math.PI / 2; holo.add(ring1);
    const beam = new THREE.Mesh(new THREE.ConeGeometry(2.6, 1.2, 48, 1, true), new THREE.MeshBasicMaterial({ color: hcol, transparent: true, opacity: .05, side: THREE.DoubleSide, depthWrite: false })); beam.position.set(tx, 1.5, tz); R.add(beam);
    const hl = new THREE.PointLight(hcol, 5, 8, 2); hl.position.set(tx, 2.2, tz); R.add(hl); this.animated.push(t => { holo.rotation.y = t * .08; });
    // wall displays: emissive panels with bars
    for (const sx of [-1, 1]) for (let i = 0; i < 3; i++) { const scr = new THREE.Mesh(new THREE.PlaneGeometry(2.6, 1.5), new THREE.MeshBasicMaterial({ map: texture(screenCanvas(i, opts.readouts || [])), transparent: false })); scr.position.set(sx * (W / 2 - .3), 3.2 + (i % 2) * 1.8, -1 - i * 5); scr.rotation.y = -sx * Math.PI / 2; R.add(scr); const l = new THREE.PointLight(0x5a8aff, 2, 5, 2); l.position.set(sx * (W / 2 - 1), 3.2, -1 - i * 5); R.add(l); }
    // admirals at the table, the player at its head
    if (opts.player) this.plate(opts.player, 0, 2.05, tz - 4.2, 1.1, null);
    (opts.admirals || []).forEach((a, i) => { const n = opts.admirals.length; const ang = n === 1 ? Math.PI * .2 : Math.PI * .15 + i * (Math.PI * .7 / (n - 1)); this.plate(a, tx + Math.cos(ang) * 4.6, 1.85, tz - Math.sin(ang) * 3.4 + .4, .9, a.key, new THREE.Vector3(tx, 1.8, tz)); });
    for (let i = 0; i < 3; i++) { const hz = new THREE.Mesh(new THREE.PlaneGeometry(W, H), new THREE.MeshBasicMaterial({ color: 0x9fb8ff, transparent: true, opacity: .02, depthWrite: false, blending: THREE.AdditiveBlending })); hz.position.set(0, H / 2, -D + 7 + i * 3); R.add(hz); }
  }
}
function screenCanvas(i, readouts) {
  const c = document.createElement('canvas'); c.width = 256; c.height = 148; const ctx = c.getContext('2d');
  ctx.fillStyle = '#06101e'; ctx.fillRect(0, 0, 256, 148); ctx.strokeStyle = '#2a4a8a'; ctx.lineWidth = 2; ctx.strokeRect(3, 3, 250, 142);
  ctx.fillStyle = '#8fb3ff'; ctx.font = '600 13px Cinzel, serif'; ctx.fillText(['READINESS', 'HULLS', 'FRONTS'][i % 3], 12, 22);
  const vals = readouts[i % 3] || [0.3, 0.6, 0.45, 0.8]; vals.slice(0, 6).forEach((v, k) => { ctx.fillStyle = '#163a6a'; ctx.fillRect(12, 34 + k * 18, 232, 10); ctx.fillStyle = k % 2 ? '#5a8aff' : '#8fb3ff'; ctx.fillRect(12, 34 + k * 18, 232 * Math.max(0.02, Math.min(1, v)), 10); });
  return c;
}

// ---------------- the keep's great hall (House) ----------------
export class Keep extends Set {
  constructor(container, dotnet) { super(container, dotnet, { fov: 40, pos: [0, 3.6, 14], look: [0, 2.2, -6] }); }
  build(opts) {
    this.clear(); const R = this.root; const accent = new THREE.Color(opts.accent || '#c9a24a');
    const W = 16, D = 30, H = 9;
    const stone = new THREE.MeshStandardMaterial({ color: 0x5a5e68, roughness: .7, metalness: .1, map: texture(noiseCanvas(256, 23, 34, 150, 60), 6) });
    const floor = new THREE.Mesh(new THREE.PlaneGeometry(W, D), stone); floor.rotation.x = -Math.PI / 2; floor.position.set(0, 0, -D / 2 + 6); floor.receiveShadow = true; R.add(floor);
    const wallMat = new THREE.MeshStandardMaterial({ color: 0x4a4e5c, roughness: .9, map: texture(noiseCanvas(256, 5, 40, 150, 50), 4) });
    for (const sx of [-1, 1]) { const wall = new THREE.Mesh(new THREE.BoxGeometry(.5, H, D), wallMat); wall.position.set(sx * W / 2, H / 2, -D / 2 + 6); R.add(wall); for (let i = 0; i < 4; i++) { const win = new THREE.Mesh(new THREE.PlaneGeometry(1.4, 3.6), new THREE.MeshBasicMaterial({ color: 0x9fb8ff })); win.position.set(sx * (W / 2 - .26), 5.2, 1 - i * 6.5); win.rotation.y = -sx * Math.PI / 2; R.add(win); const l = new THREE.PointLight(0x9fb8ff, 4, 9, 2); l.position.set(sx * (W / 2 - 1.5), 5, 1 - i * 6.5); R.add(l); } }
    const far = new THREE.Mesh(new THREE.BoxGeometry(W, H, .5), wallMat); far.position.set(0, H / 2, -D + 6); R.add(far);
    const ceil = new THREE.Mesh(new THREE.PlaneGeometry(W, D), new THREE.MeshStandardMaterial({ color: 0x3e3224, roughness: .9, emissive: 0x0c0906 })); ceil.rotation.x = Math.PI / 2; ceil.position.set(0, H, -D / 2 + 6); R.add(ceil);
    for (let i = 0; i < 5; i++) { const beam = new THREE.Mesh(new THREE.BoxGeometry(W, .5, .5), new THREE.MeshStandardMaterial({ color: 0x4a3020, roughness: .8 })); beam.position.set(0, H - .25, 3 - i * 6); R.add(beam); for (const sx of [-1, 1]) { const up = new THREE.PointLight(0xffd0a0, 7, 9, 2); up.position.set(sx * 4.5, H - 1.4, 0 - i * 6); R.add(up); } }
    // tapestries: the house banner over the dais and the holdings' worlds as globes on pedestals along the hall
    const dais = new THREE.Mesh(new THREE.BoxGeometry(8, .6, 3), new THREE.MeshStandardMaterial({ color: 0x1b1f2a, metalness: .3, roughness: .6 })); dais.position.set(0, .3, -D + 8); R.add(dais);
    const seat = new THREE.Mesh(new THREE.BoxGeometry(1.4, 2.4, .5), new THREE.MeshStandardMaterial({ color: 0x3a2416, roughness: .7 })); seat.position.set(0, 1.8, -D + 7.2); R.add(seat);
    const crest = new THREE.Mesh(new THREE.BoxGeometry(1.6, .2, .3), BRIGHT_BRASS()); crest.position.set(0, 3.1, -D + 7.2); R.add(crest);
    const great = new THREE.Mesh(new THREE.PlaneGeometry(3.6, 7, 8, 16), new THREE.MeshStandardMaterial({ map: texture(sigilCanvas(opts.houseId || 'house', opts.accent || '#c9a24a')), roughness: .9, side: THREE.DoubleSide })); great.position.set(0, 5.4, -D + 6.4); great.userData.banner = true; R.add(great);
    const hearthL = new THREE.PointLight(0xffb070, 10, 14, 2); hearthL.position.set(0, 3, -D + 9); R.add(hearthL);
    const holdings = opts.holdings || [];
    holdings.forEach((h, i) => { const sx = i % 2 ? 1 : -1; const z = -5.5 - Math.floor(i / 2) * 5; const x = sx * 5.2; const ped = new THREE.Mesh(new THREE.CylinderGeometry(.5, .7, 1.4, 12), stone); ped.position.set(x, .7, z); ped.castShadow = true; R.add(ped);
      const ptex = window.praetoriaFx ? new THREE.TextureLoader().load(window.praetoriaFx.planet(h.planetClass || 'terran', h.id, 128)) : null; if (ptex) ptex.colorSpace = THREE.SRGBColorSpace;
      if (h.kind === 'station') { const ring = new THREE.Mesh(new THREE.TorusGeometry(.6, .08, 8, 32), BRIGHT_BRASS()); ring.position.set(x, 2.2, z); ring.rotation.x = 1.2; R.add(ring); this.animated.push(t => { ring.rotation.z = t * .4; }); }
      else { const globe = new THREE.Mesh(new THREE.SphereGeometry(.75, 32, 24), new THREE.MeshStandardMaterial({ color: ptex ? 0xffffff : 0x3f8f62, map: ptex, roughness: .8 })); globe.position.set(x, 2.2, z); globe.castShadow = true; R.add(globe); this.animated.push(t => { globe.rotation.y = t * .2; }); const atm = new THREE.Mesh(new THREE.SphereGeometry(.8, 24, 16), new THREE.MeshBasicMaterial({ color: 0x8ad9ff, transparent: true, opacity: .12, side: THREE.BackSide, blending: THREE.AdditiveBlending, depthWrite: false })); atm.position.copy(globe.position); R.add(atm); }
      const plaque = new THREE.Mesh(new THREE.PlaneGeometry(1.6, .36), new THREE.MeshBasicMaterial({ map: texture(plaqueCanvas(h.name || '')), transparent: true })); plaque.position.set(x, 1.45, z + .72); R.add(plaque);
      const l = new THREE.PointLight(accent, 2.5, 5, 2); l.position.set(x, 3.2, z); R.add(l); const pl = new THREE.PointLight(0xffd8a8, 2, 4, 2); pl.position.set(x - sx * .9, 1.2, z + .9); R.add(pl);
      if (h.unrest >= 50) { const warn = new THREE.PointLight(0xff5a3c, 3, 4, 2); warn.position.set(x, 1.8, z + .8); R.add(warn); this.animated.push(t => { warn.intensity = 2 + Math.sin(t * 5) * 1.5; }); }
    });
    // a long table with the household seated
    const table = new THREE.Mesh(new THREE.BoxGeometry(2.2, .16, 12), new THREE.MeshStandardMaterial({ color: 0x4d3221, roughness: .55, map: texture(noiseCanvas(256, 5, 60, 140, 70), 2) })); table.position.set(0, 1.1, -7); table.castShadow = true; R.add(table);
    for (const [x, z] of [[-.9, -2], [.9, -2], [-.9, -12], [.9, -12]]) { const leg = new THREE.Mesh(new THREE.BoxGeometry(.18, 1.0, .18), new THREE.MeshStandardMaterial({ color: 0x2a1a10 })); leg.position.set(x, .5, z); R.add(leg); }
    for (let i = 0; i < 4; i++) { const cl = new THREE.Mesh(new THREE.SphereGeometry(.08, 8, 6), new THREE.MeshBasicMaterial({ color: 0xffe2b0 })); cl.position.set(0, 1.45, -3 - i * 3); R.add(cl); const l = new THREE.PointLight(0xffd79a, 5, 7, 2); l.position.set(0, 1.9, -3 - i * 3); R.add(l); }
    if (opts.player) this.plate(opts.player, 0, 2.2, -D + 8.2, 1.15, null);
    (opts.household || []).forEach((m, i) => { const sx = i % 2 ? 1 : -1; const z = -2.6 - Math.floor(i / 2) * 2.4; this.plate(m, sx * 2.2, 1.8, z, .85, m.key, new THREE.Vector3(sx * -1.5, 1.7, z + 9)); });
    const sun = new THREE.DirectionalLight(0xbfd0ff, 1.4); sun.position.set(-10, 12, -4); sun.castShadow = true; sun.shadow.camera.left = -12; sun.shadow.camera.right = 12; sun.shadow.camera.top = 12; sun.shadow.camera.bottom = -12; sun.shadow.mapSize.set(1024, 1024); R.add(sun);
    R.add(new THREE.HemisphereLight(0x3a4a7a, 0x3a3028, 1.1));
    this.animated.push(t => { R.traverse(o => { if (o.userData.banner) { const pos = o.geometry.attributes.position; for (let i = 0; i < pos.count; i++) { const y = pos.getY(i); pos.setZ(i, Math.sin(t * 1.1 + y * 1.2) * .06 * (1 - (y + 3.5) / 7)); } pos.needsUpdate = true; } }); });
    for (let i = 0; i < 3; i++) { const hz = new THREE.Mesh(new THREE.PlaneGeometry(W, H), new THREE.MeshBasicMaterial({ color: 0xffd0a0, transparent: true, opacity: .018, depthWrite: false, blending: THREE.AdditiveBlending })); hz.position.set(0, H / 2, -D + 9 + i * 5); R.add(hz); }
  }
}

window.praetoriaSets = {
  sets: new Map(),
  create(kind, id, container, dotnet) { this.destroy(id); const K = { senate: Senate, academy: Academy, hall: Hall, war: WarRoom, keep: Keep }[kind] || Academy; const s = new K(container, dotnet); this.sets.set(id, s); return true; },
  build(id, json) { const s = this.sets.get(id); if (!s) return false; s.build(JSON.parse(json)); return true; },
  destroy(id) { const s = this.sets.get(id); if (s) { s.dispose(); this.sets.delete(id); } }
};
