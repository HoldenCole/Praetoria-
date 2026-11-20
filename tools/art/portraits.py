#!/usr/bin/env python3
"""Praetoria — the portrait library (docs/ART_DIRECTION.md, "Portraits").

Paints the game's portraits with an openly licensed diffusion model and writes them where the web client reads them. A person in
the game is one *identity* (a face, a colouring, a way of dressing) seen at five ages: child, youth, adult, older, elder. The game
gives each character an identity by a hash of their id and shows the picture for their age, so a face grows old with its owner.

    python3 -m venv venv && ./venv/bin/pip install torch --index-url https://download.pytorch.org/whl/cpu
    ./venv/bin/pip install diffusers transformers accelerate safetensors pillow
    ./venv/bin/python tools/art/portraits.py paint  <workdir> [--per-sex 12] [--foreign 4] [--steps 5]
    ./venv/bin/python tools/art/portraits.py pack   <workdir> src/Web/wwwroot/img/portraits

`paint` is resumable: it skips pictures already in the work directory, and paints the adult faces first so a part-finished library
is usable. `pack` shrinks what is there to WebP and writes manifest.json. Raising --per-sex and running both again adds faces
without changing the ones already assigned... except that a hash over a longer list lands elsewhere, so do it between releases.

The model is Lykon/dreamshaper-8-lcm (Stable Diffusion 1.5 family, CreativeML OpenRAIL-M: output may be used commercially). It runs
on a CPU at about a minute a picture; on a GPU, seconds. Any model diffusers can load may be named with --model. The pictures are
AI-generated, which a store page must say (Steam's content survey asks).
"""
import argparse, hashlib, json, os, random, sys, time

BANDS = ["adult", "older", "elder", "youth", "child"]   # painted in this order: the commonest first
STYLE = "head and shoulders portrait, facing the viewer, painterly digital oil painting, dramatic warm key light, cool rim light, dark plain background, detailed face"
NEG = "photo, 3d render, anime, cartoon, text, watermark, signature, frame, border, deformed, hands, blurry, lowres, hat, helmet, hood, veil, modern clothes, smiling, teeth, nude"

SKIN = ["pale skin", "fair skin", "olive skin", "tan skin", "light brown skin", "brown skin", "dark brown skin", "black skin"]
HAIR = ["black hair", "dark brown hair", "brown hair", "auburn hair", "blond hair", "black hair", "dark brown hair", "red hair"]
FACE = ["sharp cheekbones", "a broad nose and heavy brow", "a narrow face and hooded eyes", "a strong jaw", "a round face", "an aquiline nose", "deep-set eyes", "a long face and thin lips", "a square face", "high forehead and arched brows"]
CUT_M = ["short cropped hair", "hair swept back", "short curly hair", "a shaved head", "hair tied back", "short hair with a side parting"]
CUT_F = ["hair pinned up", "long loose hair", "hair in a braid over one shoulder", "short bobbed hair", "curly hair", "hair in a knot with a gold pin"]
BEARD = ["clean shaven", "clean shaven", "a short beard", "a full beard", "a moustache", "stubble"]
DRESS = ["a dark navy high-collared officer's coat with brass buttons", "a black robe with a crimson stole and a gold chain", "a pale tunic under a deep green mantle pinned with a gold brooch",
         "a charcoal high-collared coat with silver embroidery", "a deep red mantle over a white tunic with a gold clasp", "a dark blue robe with a gold-trimmed collar"]
RESEED = {"fr00": 1, "fr03": 1, "mr05": 1, "faf02": 1}   # identities whose first throw came out wrong (a picture frame painted into the picture): thrown again
REDO = {"fx01_older": 2, "faf02_youth": 1}                      # one picture of an identity thrown again, the rest of that identity kept
FOREIGN = ["furs over lamellar armour with a gold torque", "an embroidered silk robe with heavy gold jewellery", "ornate bronze scale armour with a crimson cloak", "a quilted leather coat with bone toggles and a fur collar"]

# ---- ancestry (docs/ART_DIRECTION.md, "Portraits"; the game's names for these are in content/ancestries/ancestries.json) ----
# What the painter is told for each, and the hair that goes with it. A house has one ancestry and its people are painted from it.
ANCESTRY = {
    "northern":      (["Northern European, pale skin", "Scandinavian, fair skin", "Slavic, pale skin", "Celtic, pale freckled skin"], ["blond hair", "light brown hair", "red hair", "brown hair", "ash blond hair"]),
    "mediterranean": (["Italian, olive skin", "Spanish, olive skin", "Greek, olive skin"], ["black hair", "dark brown hair"]),
    "levantine":     (["Middle Eastern, Arab, tan skin", "Persian, olive tan skin", "Levantine, tan skin"], ["black hair", "dark brown hair"]),
    "south_asian":   (["South Asian, Indian, brown skin", "South Asian, Tamil, dark brown skin", "South Asian, Punjabi, light brown skin"], ["black hair"]),
    "african":       (["Black, West African, dark brown skin", "Black, East African, brown skin", "Black African, very dark skin"], ["black hair"]),
    "east_asian":    (["East Asian, Chinese", "East Asian, Japanese", "East Asian, Korean"], ["black hair"]),
    "steppe":        (["Mongolian, weathered tan skin", "Central Asian, Kazakh, weathered skin"], ["black hair"]),
}
CODE = {"northern": "no", "mediterranean": "me", "levantine": "le", "south_asian": "sa", "african": "af", "east_asian": "ea", "steppe": "st"}
CUT_AF_M = ["short cropped hair", "a shaved head", "short natural hair", "short dreadlocks tied back"]
CUT_AF_F = ["long braided hair", "natural curly hair", "short natural hair", "hair in a braided crown"]
# The first library was painted before houses had an ancestry; its faces are kept, each given the one it shows (by eye).
LEGACY = {"fr00": "south_asian", "fr01": "mediterranean", "fr02": "mediterranean", "fr03": "south_asian", "fr04": "northern", "fr05": "levantine", "fr06": "african", "fr07": "mediterranean",
          "fr08": "northern", "fr09": "northern", "fr10": "levantine", "fr11": "african", "mr00": "northern", "mr01": "northern", "mr02": "levantine", "mr03": "mediterranean", "mr04": "mediterranean",
          "mr05": "african", "mr06": "northern", "mr07": "south_asian", "mr08": "northern", "mr09": "levantine", "mr10": "south_asian", "mr11": "mediterranean"}
# How many more of each to paint, to bring every ancestry to six faces a sex (the steppe, met only through its rulers, to four).
NEW = {"east_asian": (6, 6), "african": (5, 4), "south_asian": (4, 4), "levantine": (4, 4), "steppe": (4, 4), "mediterranean": (3, 3), "northern": (2, 3)}   # (men, women)

def who(sex, band, d):
    """The subject, in few enough words for the model's 77 tokens."""
    m = sex == "m"
    if band == "child":
        return f"a 9 year old {'boy' if m else 'girl'}, child, {d['skin']}, {d['hair']}, {d['face']}, serious, wearing a simple cream tunic with a blue trim"
    if band == "youth":
        return f"a 17 year old {'young man' if m else 'young woman'}, teenager, {d['skin']}, {d['hair']}, {d['cut']}, {d['face']}, wearing {d['dress']}"
    age = {"adult": "35 year old", "older": "55 year old middle-aged", "elder": "78 year old elderly"}[band]
    hair = d["hair"] if band == "adult" else ("greying " + d["hair"] if band == "older" else "white hair")
    lines = "" if band == "adult" else (", lined face, crow's feet" if band == "older" else ", deeply wrinkled old face, sunken cheeks")
    beard = (", " + d["beard"]) if m else ""
    return f"a {age} {'man' if m else 'woman'}, {d['skin']}, {hair}, {d['cut']}{beard}, {d['face']}{lines}, stern, wearing {d['dress']}"

def identities(per_sex=12, foreign=0):
    """Every identity: (name, sex, ancestry, seed, description, the ages it is painted at). The first library's, then the new ones in
    rounds, one of each ancestry and sex at a time, so that a part-finished run has widened every group a little."""
    out = []
    for sex in "mf":
        for i in range(per_sex):
            name = f"{sex}r{i:02d}"; r = random.Random(int(hashlib.sha1(f"{sex}r{i}".encode()).hexdigest()[:8], 16))
            d = {"skin": SKIN[(i * 3 + (0 if sex == "m" else 5)) % len(SKIN)], "hair": r.choice(HAIR), "face": r.choice(FACE), "cut": r.choice(CUT_M if sex == "m" else CUT_F), "beard": r.choice(BEARD), "dress": DRESS[i % len(DRESS)]}
            if name in LEGACY: out.append((name, sex, LEGACY[name], 7000 + i * 13 + (0 if sex == "m" else 500) + 100003 * RESEED.get(name, 0), d, BANDS))
    new = []; had = {}
    for n, anc in LEGACY.items(): had[(anc, n[0])] = had.get((anc, n[0]), 0) + 1
    for rnd in range(max(max(v) for v in NEW.values())):
        for anc, (men, women) in NEW.items():
            for sex, n in (("m", men), ("f", women)):
                if rnd >= n: continue
                name = f"{sex}{CODE[anc]}{rnd:02d}"; r = random.Random(int(hashlib.sha1(name.encode()).hexdigest()[:8], 16)); looks, hairs = ANCESTRY[anc]
                cuts = (CUT_AF_M if sex == "m" else CUT_AF_F) if anc == "african" else (CUT_M if sex == "m" else CUT_F)
                d = {"skin": looks[rnd % len(looks)], "hair": r.choice(hairs), "face": r.choice(FACE), "cut": cuts[(rnd + r.randrange(2)) % len(cuts)], "beard": r.choice(BEARD),
                     "dress": FOREIGN[(0, 3)[rnd % 2]] if anc == "steppe" else DRESS[(rnd * 5 + (0 if sex == "m" else 3) + len(anc)) % len(DRESS)]}
                new.append((had.get((anc, sex), 0) + rnd, len(new), (name, sex, anc, 9000 + int(hashlib.sha1(name.encode()).hexdigest()[:6], 16) % 90000 + 7919 * RESEED.get(name, 0), d, [b for b in BANDS if not (anc == "steppe" and b == "child")])))
    return out + [x[2] for x in sorted(new)]   # the thinnest group first: each new face goes where there are fewest

def paint(a):
    import torch
    from diffusers import AutoPipelineForText2Image, LCMScheduler
    os.makedirs(a.work, exist_ok=True); torch.set_num_threads(os.cpu_count() or 4)
    pipe = AutoPipelineForText2Image.from_pretrained(a.model, torch_dtype=torch.float32, safety_checker=None)
    if "lcm" in a.model.lower(): pipe.scheduler = LCMScheduler.from_config(pipe.scheduler.config)
    pipe.set_progress_bar_config(disable=True)
    for name, sex, anc, seed, d, bands in identities():
        for band in bands:   # a whole identity at a time: a face is of use once all its ages are painted
            path = os.path.join(a.work, f"{name}_{band}.png")
            if os.path.exists(path): continue
            t = time.time(); g = torch.Generator().manual_seed(seed + 7 * REDO.get(f"{name}_{band}", 0))
            img = pipe(prompt=who(sex, band, d) + ", " + STYLE, negative_prompt=NEG, num_inference_steps=a.steps, guidance_scale=a.cfg, width=512, height=512, generator=g).images[0]
            img.save(path); print(f"{name}_{band} {time.time() - t:.0f}s", flush=True)

def pack(a):
    from PIL import Image
    os.makedirs(a.out, exist_ok=True); faces = {}
    rejected = set(open(os.path.join(a.work, "rejected.txt")).read().split()) if os.path.exists(os.path.join(a.work, "rejected.txt")) else set()
    for name, sex, anc, seed, d, bands in identities():
        if name in rejected or not all(os.path.exists(os.path.join(a.work, f"{name}_{b}.png")) for b in bands): continue   # only a face painted at all its ages
        for b in bands:
            im = Image.open(os.path.join(a.work, f"{name}_{b}.png")).convert("RGB"); w, h = im.size; m = int(w * a.trim)   # trim the edges: a stray frame or a cut-off shoulder lives there
            im.crop((m, m // 2, w - m, h - m - m // 2)).resize((a.size, a.size), Image.LANCZOS).save(os.path.join(a.out, f"{name}_{b}.webp"), "WEBP", quality=a.quality, method=6)
        faces[name] = {"sex": "male" if sex == "m" else "female", "ancestry": anc, "ages": sorted(bands, key=BANDS.index)}
    json.dump({"note": "AI-generated with an openly licensed model; see tools/art/portraits.py", "faces": faces}, open(os.path.join(a.out, "manifest.json"), "w"), indent=0, sort_keys=True)
    by = {}
    for f in faces.values(): by[(f["ancestry"], f["sex"])] = by.get((f["ancestry"], f["sex"]), 0) + 1
    print(f"{len(faces)} identities, {sum(len(v['ages']) for v in faces.values())} pictures -> {a.out}"); print("  " + ", ".join(f"{k[0]} {k[1]} {v}" for k, v in sorted(by.items())))

if __name__ == "__main__":
    p = argparse.ArgumentParser(); s = p.add_subparsers(dest="cmd", required=True)
    q = s.add_parser("paint"); q.add_argument("work"); q.add_argument("--per-sex", type=int, default=12); q.add_argument("--foreign", type=int, default=4)
    q.add_argument("--steps", type=int, default=5); q.add_argument("--cfg", type=float, default=1.5); q.add_argument("--model", default="Lykon/dreamshaper-8-lcm"); q.set_defaults(fn=paint)
    q = s.add_parser("pack"); q.add_argument("work"); q.add_argument("out"); q.add_argument("--size", type=int, default=448); q.add_argument("--quality", type=int, default=84); q.add_argument("--trim", type=float, default=0.07); q.set_defaults(fn=pack)
    a = p.parse_args(); a.fn(a)
