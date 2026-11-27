"""The title's key art and the plate of "A character of your own" on the title screen (docs/ART_DIRECTION.md, "Key art").

Painted with the same open model as the portraits and the event paintings (Lykon/dreamshaper-8-lcm, CreativeML OpenRAIL-M) on a
CPU, six LCM steps each. The key art is told what the Reach is not: no dome, no obelisk, no river road of a present-day Earth city.

    python tools/art/keyart.py <out dir>      # writes title.png and own.png; scale and save them as webp in img/scenes
"""
import os, sys
os.environ.setdefault("HF_HUB_OFFLINE", "1")
import torch
from diffusers import AutoPipelineForText2Image, LCMScheduler

NEG_TITLE = ("photo, 3d render, anime, cartoon, text, watermark, frame, blurry, lowres, earth, dome, capitol, basilica, obelisk, "
             "washington, eiffel, river, car, aeroplane, mountains only")
NEG_OWN = ("photo, 3d render, anime, cartoon, text, watermark, signature, frame, border, blurry, lowres, garish, earth, capitol building, "
           "st peter's basilica, obelisk, washington, rome, eiffel tower, river, car, aeroplane")
STYLE = "painterly digital oil painting, cinematic, moody low-key lighting, muted colours, Roman imperial science fiction, epic key art"
JOBS = [
    ("title", "matte painting of a science fiction imperial city of bronze towers and spires, a giant ringed gas giant rising over the "
              "horizon, a glowing orbital ring arc across the sky, starships, dusk, red banners, painterly, cinematic, muted", NEG_TITLE, 768, 352, 7505),
    ("own", "a hooded cloaked figure seen from behind, standing alone in a dark marble hall before a tall arched window full of stars "
            "and a ringed planet, silhouette, mystery, " + STYLE, NEG_OWN, 512, 640, 7404),
]

def main(out: str) -> None:
    os.makedirs(out, exist_ok=True)
    pipe = AutoPipelineForText2Image.from_pretrained("Lykon/dreamshaper-8-lcm", torch_dtype=torch.float32, safety_checker=None)
    pipe.scheduler = LCMScheduler.from_config(pipe.scheduler.config)
    for name, prompt, neg, w, h, seed in JOBS:
        g = torch.Generator().manual_seed(seed)
        pipe(prompt=prompt, negative_prompt=neg, num_inference_steps=6, guidance_scale=1.5, width=w, height=h, generator=g).images[0].save(os.path.join(out, name + ".png"))

if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "keyart")
