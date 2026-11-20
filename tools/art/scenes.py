#!/usr/bin/env python3
"""Praetoria — the paintings an event opens on (docs/ART_DIRECTION.md, "The event window").

A dozen or so wide scenes, one per kind of occasion, painted with the same openly licensed model as the portraits
(tools/art/portraits.py has the set-up) and written where the web client reads them:

    ./venv/bin/python tools/art/scenes.py <workdir> src/Web/wwwroot/img/scenes

Low in contrast and dark toward the bottom on purpose: a title and a face sit over them. Resumable. AI-generated, which a store
page must say.
"""
import os, sys, time
STYLE = "wide establishing shot, painterly digital oil painting, cinematic, moody low-key lighting, muted colours, Roman imperial science fiction, no people in foreground"
NEG = "photo, 3d render, anime, cartoon, text, watermark, signature, frame, border, blurry, lowres, bright, garish, close-up face, ocean, sea, sailing ship, aeroplane, car"
SCENES = {
    "audience": "a grand marble study with tall windows onto a starry sky and a ringed planet, a heavy desk, banners, lamplight",
    "senate":   "a vast semicircular senate chamber of marble benches and columns, robed figures small in the distance, shafts of light",
    "bridge":   "the dark command deck of a starship interior, officers seated at glowing consoles in silhouette, a huge curved window onto a planet and stars",
    "battle":   "a fleet of spacecraft and huge capital starships exchanging laser fire in deep space above a planet, explosions, burning starship hulls, stars, debris",
    "market":   "a crowded trading exchange hall under a glass dome, merchants and brokers, hanging lamps, cargo and ledgers",
    "family":   "a noble family's great hall at night, long table, hearth fire, ancestral busts along the wall, a starry window",
    "funeral":  "a funeral procession by torchlight in a marble necropolis, mourners in dark cloaks, a bier, falling ash",
    "court":    "a solemn court of law, a high bench with magistrates, a lone accused before it, columns and braziers",
    "unrest":   "an angry crowd with torches filling a marble forum at dusk, soldiers with shields holding a line, banners, smoke, columns",
    "steppe":   "a warlord's great tent hall on a windswept alien steppe, fur and gold, fire pits, warriors, moons in the sky",
    "dispatch": "a cloaked courier handing a sealed scroll to an officer in a torch-lit marble corridor at night, rain beyond the arches",
    "feast":    "a wedding feast in a columned hall hung with garlands and banners, long tables, guests, warm lamplight",
    "intrigue": "a shadowed colonnade at night, two cloaked figures whispering, a single lamp, long shadows, a city below",
    "world":    "a colony city on an alien world seen from a hill at dawn, towers and domes, fields, a huge moon and ships in the sky",
    "shipyard": "a space station drydock in orbit, the skeleton of a huge starship under construction inside scaffolding, sparks, a planet below, stars",
    "triumph":  "a Roman triumph, a general in a chariot under a great marble triumphal arch, legionaries, cheering crowd, laurel, red banners, golden light",
    "title":    "a vast marble capital city of domes and colonnades at dusk under a huge ringed planet, a fleet of great starships crossing the sky, red banners, epic key art",
}
RETHROWN = {"bridge": 1, "battle": 2, "unrest": 1, "dispatch": 1, "shipyard": 1, "triumph": 1}   # painted again with a better description
if __name__ == "__main__":
    work, out = sys.argv[1], sys.argv[2]; os.makedirs(work, exist_ok=True); os.makedirs(out, exist_ok=True)
    todo = [k for k in SCENES if not os.path.exists(os.path.join(work, k + ".png"))]
    if todo:
        import torch
        from diffusers import AutoPipelineForText2Image, LCMScheduler
        torch.set_num_threads(os.cpu_count() or 4)
        pipe = AutoPipelineForText2Image.from_pretrained("Lykon/dreamshaper-8-lcm", torch_dtype=torch.float32, safety_checker=None)
        pipe.scheduler = LCMScheduler.from_config(pipe.scheduler.config); pipe.set_progress_bar_config(disable=True)
        for i, k in enumerate(todo):
            t = time.time(); g = torch.Generator().manual_seed(4200 + list(SCENES).index(k) + 1000 * RETHROWN.get(k, 0))
            pipe(prompt=SCENES[k] + ", " + STYLE, negative_prompt=NEG, num_inference_steps=6, guidance_scale=1.5, width=768, height=352, generator=g).images[0].save(os.path.join(work, k + ".png"))
            print(k, f"{time.time() - t:.0f}s", flush=True)
    from PIL import Image
    for k in SCENES:
        p = os.path.join(work, k + ".png")
        if os.path.exists(p): Image.open(p).convert("RGB").save(os.path.join(out, k + ".webp"), "WEBP", quality=80, method=6)
    print("packed", len(os.listdir(out)))
