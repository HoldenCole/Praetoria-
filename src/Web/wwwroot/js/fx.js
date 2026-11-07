// Praetoria visual effects: procedural nebula backgrounds and planet sprites drawn on canvas, cached as data URLs.
(function () {
  function mulberry(seed) { let t = seed >>> 0; return function () { t += 0x6D2B79F5; let r = Math.imul(t ^ (t >>> 15), 1 | t); r ^= r + Math.imul(r ^ (r >>> 7), 61 | r); return ((r ^ (r >>> 14)) >>> 0) / 4294967296; }; }
  function hash(s) { let h = 2166136261; for (let i = 0; i < s.length; i++) { h ^= s.charCodeAt(i); h = Math.imul(h, 16777619); } return h >>> 0; }
  // value noise with fbm
  function noiseMaker(seed) {
    const rnd = mulberry(seed), perm = new Uint8Array(512), grad = new Float32Array(256);
    for (let i = 0; i < 256; i++) { perm[i] = i; grad[i] = rnd(); }
    for (let i = 255; i > 0; i--) { const j = Math.floor(rnd() * (i + 1)); [perm[i], perm[j]] = [perm[j], perm[i]]; }
    for (let i = 0; i < 256; i++) perm[i + 256] = perm[i];
    const fade = t => t * t * t * (t * (t * 6 - 15) + 10);
    function n2(x, y) {
      const X = Math.floor(x) & 255, Y = Math.floor(y) & 255, xf = x - Math.floor(x), yf = y - Math.floor(y);
      const a = grad[perm[X + perm[Y]]], b = grad[perm[X + 1 + perm[Y]]], c = grad[perm[X + perm[Y + 1]]], d = grad[perm[X + 1 + perm[Y + 1]]];
      const u = fade(xf), v = fade(yf);
      return (a * (1 - u) + b * u) * (1 - v) + (c * (1 - u) + d * u) * v;
    }
    return function fbm(x, y, oct) { let s = 0, amp = .5, f = 1, norm = 0; for (let i = 0; i < oct; i++) { s += amp * n2(x * f, y * f); norm += amp; amp *= .5; f *= 2.03; } return s / norm; };
  }
  const palettes = {
    terran: { sea: [20, 70, 140], land: [60, 130, 70], hi: [150, 170, 110], cloud: .55, atm: [120, 190, 255] },
    ocean: { sea: [10, 60, 150], land: [40, 110, 160], hi: [200, 220, 240], cloud: .5, atm: [140, 200, 255] },
    arid: { sea: [150, 100, 50], land: [200, 150, 80], hi: [230, 200, 140], cloud: .15, atm: [230, 190, 140] },
    forge: { sea: [60, 20, 15], land: [140, 50, 30], hi: [255, 120, 60], cloud: .3, atm: [255, 140, 80], glow: true },
    frozen: { sea: [150, 180, 210], land: [220, 235, 245], hi: [255, 255, 255], cloud: .4, atm: [200, 230, 255] },
    gas: { sea: [160, 110, 60], land: [220, 180, 120], hi: [245, 225, 190], cloud: 0, atm: [240, 210, 160], bands: true },
    belt: { sea: [70, 65, 60], land: [120, 110, 100], hi: [170, 160, 150], cloud: 0, atm: [0, 0, 0], belt: true },
    barren: { sea: [80, 75, 70], land: [120, 110, 100], hi: [160, 150, 140], cloud: 0, atm: [120, 110, 100] }
  };
  const cache = {};
  function planet(cls, seedStr, size) {
    const key = cls + '|' + seedStr + '|' + size; if (cache[key]) return cache[key];
    const p = palettes[cls] || palettes.terran, seed = hash(seedStr), fbm = noiseMaker(seed), cfbm = noiseMaker(seed ^ 0x9e37);
    const c = document.createElement('canvas'); c.width = c.height = size; const ctx = c.getContext('2d');
    const img = ctx.createImageData(size, size), d = img.data, r = size / 2 - 1, cx = size / 2, cy = size / 2;
    const lx = -0.55, ly = -0.4, lz = 0.73;
    for (let y = 0; y < size; y++) for (let x = 0; x < size; x++) {
      const dx = (x - cx) / r, dy = (y - cy) / r, dd = dx * dx + dy * dy, i = (y * size + x) * 4;
      if (dd > 1) { // atmosphere rim
        const t = Math.max(0, 1 - (Math.sqrt(dd) - 1) * 9); if (t > 0 && !p.belt) { d[i] = p.atm[0]; d[i + 1] = p.atm[1]; d[i + 2] = p.atm[2]; d[i + 3] = 255 * t * t * .8; }
        continue;
      }
      const dz = Math.sqrt(1 - dd);
      // sphere mapping -> texture coords
      const lon = Math.atan2(dx, dz), lat = Math.asin(dy);
      let u = lon * 1.6 + 10, v = lat * 1.6 + 10;
      let h = p.bands ? fbm(u * 0.6, v * 4 + fbm(u * 2, v * 2, 3) * 1.5, 4) : fbm(u, v, 6);
      let col;
      if (p.bands) { col = h > .5 ? p.land : p.sea; const k = (h - .5) * 3; col = mix(p.sea, p.land, clamp(.5 + k)); if (h > .62) col = mix(col, p.hi, clamp((h - .62) * 6)); }
      else if (h < 0.47) col = mix(p.sea, darken(p.sea, .6), (0.47 - h) * 3);
      else if (h < 0.52) col = mix(p.sea, p.land, (h - 0.47) * 20);
      else col = mix(p.land, p.hi, clamp((h - 0.52) * 3.2));
      if (p.glow && h > .6) col = mix(col, [255, 170, 60], clamp((h - .6) * 4));
      // clouds
      if (p.cloud > 0) { const cl = cfbm(u * 1.3 + 3, v * 1.3, 4); if (cl > 1 - p.cloud * .9) { const a = clamp((cl - (1 - p.cloud * .9)) * 4); col = mix(col, [240, 245, 250], a * .8); } }
      // lighting
      const nl = Math.max(0, dx * lx + dy * ly + dz * lz); const light = 0.18 + 0.95 * Math.pow(nl, 0.9);
      const spec = Math.pow(Math.max(0, nl), 24) * (cls === 'ocean' || cls === 'frozen' ? .45 : .15);
      d[i] = clamp8(col[0] * light + 255 * spec); d[i + 1] = clamp8(col[1] * light + 255 * spec); d[i + 2] = clamp8(col[2] * light + 255 * spec); d[i + 3] = 255;
      // limb atmosphere tint
      const rim = Math.pow(dd, 6); if (!p.belt) { d[i] = clamp8(d[i] * (1 - rim * .6) + p.atm[0] * rim * .6); d[i + 1] = clamp8(d[i + 1] * (1 - rim * .6) + p.atm[1] * rim * .6); d[i + 2] = clamp8(d[i + 2] * (1 - rim * .6) + p.atm[2] * rim * .6); }
    }
    ctx.putImageData(img, 0, 0);
    if (p.belt) { // a scatter of rocks instead of a smooth disc
      ctx.globalCompositeOperation = 'destination-in'; ctx.fillStyle = '#000'; ctx.beginPath();
      const rnd = mulberry(seed); for (let k = 0; k < 9; k++) { const a = rnd() * 6.283, rr = r * (0.25 + rnd() * .55); ctx.moveTo(cx + Math.cos(a) * rr * .9, cy + Math.sin(a) * rr * .9); ctx.arc(cx + Math.cos(a) * rr * .55, cy + Math.sin(a) * rr * .55, r * (0.18 + rnd() * .22), 0, 6.283); }
      ctx.fill(); ctx.globalCompositeOperation = 'source-over';
    }
    const url = c.toDataURL('image/png'); cache[key] = url; return url;
  }
  function mix(a, b, t) { t = clamp(t); return [a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t]; }
  function darken(a, k) { return [a[0] * k, a[1] * k, a[2] * k]; }
  function clamp(t) { return t < 0 ? 0 : t > 1 ? 1 : t; }
  function clamp8(v) { return v < 0 ? 0 : v > 255 ? 255 : v | 0; }

  let nebulaUrl = null;
  function nebula(seedStr, w, h) {
    if (nebulaUrl) return nebulaUrl;
    const seed = hash(seedStr), fbm = noiseMaker(seed), fbm2 = noiseMaker(seed ^ 77), rnd = mulberry(seed);
    const c = document.createElement('canvas'); c.width = w; c.height = h; const ctx = c.getContext('2d');
    const img = ctx.createImageData(w, h), d = img.data;
    const tints = [[40, 60, 120], [90, 40, 90], [30, 90, 110]];
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) {
      const u = x / w * 3, v = y / h * 2, n = fbm(u, v, 5), m = fbm2(u * 1.7 + 5, v * 1.7, 4);
      const a = Math.pow(Math.max(0, n - .42), 1.6) * 2.2, i = (y * w + x) * 4; const t = mix(tints[0], tints[1], m); const t2 = mix(t, tints[2], Math.pow(m, 3));
      d[i] = 5 + t2[0] * a; d[i + 1] = 7 + t2[1] * a; d[i + 2] = 12 + t2[2] * a; d[i + 3] = 255;
    }
    ctx.putImageData(img, 0, 0);
    for (let k = 0; k < 700; k++) { const x = rnd() * w, y = rnd() * h, s = rnd(); ctx.fillStyle = `rgba(${200 + s * 55},${210 + s * 40},255,${0.25 + s * .7})`; ctx.beginPath(); ctx.arc(x, y, s * s * 1.6 + .3, 0, 6.283); ctx.fill(); }
    nebulaUrl = c.toDataURL('image/jpeg', 0.85); return nebulaUrl;
  }
  // Fill every <image data-planet="class|seed"> whose href is empty, and every [data-nebula] background.
  function paint() {
    document.querySelectorAll('image[data-planet]').forEach(el => { if (el.getAttribute('href')) return; const [cls, seed] = el.getAttribute('data-planet').split('|'); el.setAttribute('href', planet(cls, seed, 96)); });
    document.querySelectorAll('img[data-planet]').forEach(el => { if (el.getAttribute('src')) return; const [cls, seed, size] = el.getAttribute('data-planet').split('|'); el.setAttribute('src', planet(cls, seed, parseInt(size || '96'))); });
    document.querySelectorAll('[data-nebula]').forEach(el => { if (el.style.backgroundImage) return; el.style.backgroundImage = `url(${nebula('the-reach', 1024, 640)})`; });
  }
  window.praetoriaFx = { planet, nebula, paint };
})();
