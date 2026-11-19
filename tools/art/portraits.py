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
NEG = "photo, 3d render, anime, cartoon, text, watermark, signature, frame, border, deformed, hands, blurry, lowres, hat, helmet, modern clothes, smiling, teeth, nude"

SKIN = ["pale skin", "fair skin", "olive skin", "tan skin", "light brown skin", "brown skin", "dark brown skin", "black skin"]
HAIR = ["black hair", "dark brown hair", "brown hair", "auburn hair", "blond hair", "black hair", "dark brown hair", "red hair"]
FACE = ["sharp cheekbones", "a broad nose and heavy brow", "a narrow face and hooded eyes", "a strong jaw", "a round face", "an aquiline nose", "deep-set eyes", "a long face and thin lips", "a square face", "high forehead and arched brows"]
CUT_M = ["short cropped hair", "hair swept back", "short curly hair", "a shaved head", "hair tied back", "short hair with a side parting"]
CUT_F = ["hair pinned up", "long loose hair", "hair in a braid over one shoulder", "short bobbed hair", "curly hair", "hair in a knot with a gold pin"]
BEARD = ["clean shaven", "clean shaven", "a short beard", "a full beard", "a moustache", "stubble"]
DRESS = ["a dark navy high-collared officer's coat with brass buttons", "a black robe with a crimson stole and a gold chain", "a pale tunic under a deep green mantle pinned with a gold brooch",
         "a charcoal high-collared coat with silver embroidery", "a deep red mantle over a white tunic with a gold clasp", "a dark blue robe with a gold-trimmed collar"]
FOREIGN = ["furs over lamellar armour with a gold torque", "an embroidered silk robe with heavy gold jewellery", "ornate bronze scale armour with a crimson cloak", "a quilted leather coat with bone toggles and a fur collar"]

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

def identities(per_sex, foreign):
    out = []
    for sex in "mf":
        for kind, n in (("r", per_sex), ("x", foreign)):   # r: of the Reach; x: of a nation beyond it
            for i in range(n):
                r = random.Random(int(hashlib.sha1(f"{sex}{kind}{i}".encode()).hexdigest()[:8], 16))
                d = {"skin": SKIN[(i * 3 + (0 if sex == "m" else 5)) % len(SKIN)], "hair": r.choice(HAIR), "face": r.choice(FACE), "cut": r.choice(CUT_M if sex == "m" else CUT_F),
                     "beard": r.choice(BEARD), "dress": (FOREIGN[i % len(FOREIGN)] if kind == "x" else DRESS[i % len(DRESS)])}
                out.append((f"{sex}{kind}{i:02d}", sex, kind, 7000 + i * 13 + (0 if sex == "m" else 500) + (250 if kind == "x" else 0), d))
    return out

def paint(a):
    import torch
    from diffusers import AutoPipelineForText2Image, LCMScheduler
    os.makedirs(a.work, exist_ok=True); torch.set_num_threads(os.cpu_count() or 4)
    pipe = AutoPipelineForText2Image.from_pretrained(a.model, torch_dtype=torch.float32, safety_checker=None)
    if "lcm" in a.model.lower(): pipe.scheduler = LCMScheduler.from_config(pipe.scheduler.config)
    pipe.set_progress_bar_config(disable=True)
    ids = identities(a.per_sex, a.foreign)
    for band in BANDS:
        for name, sex, kind, seed, d in ids:
            if kind == "x" and band in ("youth", "child"): continue   # a court beyond the rim is met through its grown rulers
            path = os.path.join(a.work, f"{name}_{band}.png")
            if os.path.exists(path): continue
            t = time.time(); g = torch.Generator().manual_seed(seed)
            img = pipe(prompt=who(sex, band, d) + ", " + STYLE, negative_prompt=NEG, num_inference_steps=a.steps, guidance_scale=a.cfg, width=512, height=512, generator=g).images[0]
            img.save(path); print(f"{name}_{band} {time.time() - t:.0f}s", flush=True)

def pack(a):
    from PIL import Image
    os.makedirs(a.out, exist_ok=True); faces = {}
    rejected = set(open(os.path.join(a.work, "rejected.txt")).read().split()) if os.path.exists(os.path.join(a.work, "rejected.txt")) else set()
    for f in sorted(os.listdir(a.work)):
        if not f.endswith(".png") or f[:-4] in rejected: continue
        name, band = f[:-4].rsplit("_", 1)
        Image.open(os.path.join(a.work, f)).convert("RGB").resize((a.size, a.size), Image.LANCZOS).save(os.path.join(a.out, f"{name}_{band}.webp"), "WEBP", quality=a.quality, method=6)
        faces.setdefault(name, []).append(band)
    faces = {k: sorted(v, key=BANDS.index) for k, v in faces.items() if "adult" in v or "older" in v}   # an identity needs a grown face
    json.dump({"note": "AI-generated with an openly licensed model; see tools/art/portraits.py", "faces": faces}, open(os.path.join(a.out, "manifest.json"), "w"), indent=0, sort_keys=True)
    print(f"{len(faces)} identities, {sum(len(v) for v in faces.values())} pictures -> {a.out}")

if __name__ == "__main__":
    p = argparse.ArgumentParser(); s = p.add_subparsers(dest="cmd", required=True)
    q = s.add_parser("paint"); q.add_argument("work"); q.add_argument("--per-sex", type=int, default=12); q.add_argument("--foreign", type=int, default=4)
    q.add_argument("--steps", type=int, default=5); q.add_argument("--cfg", type=float, default=1.5); q.add_argument("--model", default="Lykon/dreamshaper-8-lcm"); q.set_defaults(fn=paint)
    q = s.add_parser("pack"); q.add_argument("work"); q.add_argument("out"); q.add_argument("--size", type=int, default=384); q.add_argument("--quality", type=int, default=82); q.set_defaults(fn=pack)
    a = p.parse_args(); a.fn(a)
