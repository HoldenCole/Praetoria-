#!/usr/bin/env python3
"""Praetoria — the Office's rooms (docs/ART_DIRECTION.md, "The office"; the owner: "I like having some actual rooms").

The player sits in an Office, and the Office is a painted room: one for each register of the game (the head of a house's study,
a naval ready room, a counting house, a jurist's study). The client lays the room under the day's visitors, who stand on its open
floor as portrait plates, and the year's letters, which lie on its desk; where those are is written beside each picture.

    ./venv/bin/python tools/art/rooms.py paint <workdir> --cache <dir>     # DRAFTS drafts a room at 640 x 528
    ./venv/bin/python tools/art/rooms.py sheet <workdir> drafts.png       # every draft, to choose from (KEEP)
    ./venv/bin/python tools/art/rooms.py final <workdir> src/Web/wwwroot/img/rooms --cache <dir>
    ./venv/bin/python tools/art/rooms.py sheet <workdir> rooms.png --finals [--layout]
    ./venv/bin/python tools/art/rooms.py lay lay.png                      # the lay-ins alone: lay_<room>.png, its lines and its depth
    ./venv/bin/python tools/art/rooms.py sharp <workdir>                  # how sharp each final is where the Office shows it

Painted with the same openly licensed model as the portraits (tools/art/portraits.py has the set-up), held to a plan by two
ControlNets of the same family and licence (lllyasviel/control_v11p_sd15_mlsd and control_v11f1p_sd15_depth, CreativeML OpenRAIL-M;
`--cache` is where they are kept, about 1.4 GB in fp16). Left to itself the model puts a window onto space where it likes, or
nowhere, and a desk anywhere; so each room is laid in where PLAN says three ways at once (`Lay`): as colour (the back wall, the
floor, the window and what is beyond it, the desk and its lamps, the room's own furniture), as the straight lines of its
architecture (for the MLSD net: wall corners, floor seams, the window's frame, shelves, the desk's edges, the hulls), and as how
near each thing is (for the depth net: the floor rising to the viewer, the desk standing on it, the window far). The model paints
the room over the colour at STRENGTH, nearly from scratch, while the two nets hold the plan (COND). The pictures before this one
were the colour alone repainted at 0.45: the plan held, but the model only smoothed flat colour, and every room came out soft and
flat, or as vector art (the art director's review; Laplacian variance 24 to 51 at the Office's width, where the event paintings,
painted from words alone, score 50 to 1400 at 768). The lay-in still decides more than the seed does: a near-square arch became a
round porthole in every throw, a deep desk front became steps, a box under a sill became the sill, thin bright lines became neon, a
bright even floor became a table, a rounded hull end became a blimp, a whole disc of a gas giant became a lamp or a gilt dome.

The Office shows the picture at about 1100 x 900 (the left rail and the outliner take the rest of a 1600 x 1000 screen), so it is
painted at that shape (1.22 : 1) and none of it is cut away; it is drafted at 640 x 528, near the size the model knows, then
doubled, sharpened, grained and painted again by the same nets at FINISH in four overlapping tiles and a fifth over the window and
the desk: real detail at 1280 x 1056, not stretched pixels. `final` writes <key>.webp and <key>.json: the open floor, the desk or table top and the window as fractions of the
picture, as SEEN by eye on the finished painting. Resumable: a draft or a final already in the work directory is kept. `RETHROWN`
paints a room's drafts again from other seeds; `KEEP` names the draft that goes on. One picture at a time, on half the CPU
(--threads): on a machine busy with builds, all four cores paint four times slower than two. There, about three minutes a draft
and fifteen a final. AI-generated, which a store page must say.
"""
import argparse, json, os, sys, time
STYLE = "scifi interior, eye level wide shot, digital oil painting, visible brushwork, sharp focus, intricate detail, warm lamplight, cold starlight, empty room"
# 'cinematic' (every throw before the seventh study) asked for a shallow depth of field: the floors and the planet were painted out of focus. 'painterly' and
# 'blue' went to make room in the 75 tokens; 'oil painting, visible brushwork' keeps the look.
# The negative prompt is cut at the model's 75 tokens like the prompt, so it is NEG and then what the room itself must not grow (SHUN):
# `paint` and `final` refuse to run if either goes over (the drafts kept from the seventh study on were painted with "watermark" where "signature" now
# stands: the first finals grew a signature). Throws before the fifth study had one longer NEG for all, which also said
# portrait, anime, signature, border, lowres, ocean, boat, mountains, skyscrapers and chandelier.
NEG = "person, face, statue, photo, 3d render, cartoon, flat colors, vector art, cel shading, text, signature, blurry, depth of field, bokeh, daylight, sea, earth, modern"
SHUN = {   # room -> what earlier throws grew there (the art director's reviews of the first and second sets of finals)
    "study":       "galaxy, spiral, porthole, round window, curtains, letters, symbols, chandelier, moon, spear, mountains, landscape, pendant lamp, cord",
    "naval":       "neon, laser, spiral, moon, planets, chairs, stripes, symbols, emblem, airship, blimp, fish, ocean, water, island",
    "stewardship": "towers, spires, skyline, moon, chandelier, eyes, balustrade, crystal ball, globe, jewel, clock, vases, blue planet",
    "law":         "steps, stairs, dais, portico, temple, throne, clock, skyscrapers, moon, snow, rain",   # bokeh is in NEG for all
}
ROOMS = {   # one per register (the client's data-role). A room and the style together must stay within the model's 75 tokens
    "study":       "a senator's study on a space station, a tall arched window onto Saturn with wide tilted rings, a massive dark wood desk with brass lamps, bookshelves, plain crimson banners, bronze eagles, marble floor",
    "naval":       "a starship ready room, a wide viewport onto a line of long angular grey space battleships, round chart table with a glowing star map, blued steel bulkheads, brass trim, crimson banners",
    "stewardship": "a counting house, a long wooden table with brass scales, an abacus, stacks of coins, shelves of ledger books, a tall window onto a huge pale banded gas giant over a city of glass domes",
    "law":         "a jurist's library, marble columns, a heavy carved judge's desk with a gavel, lectern, black robe on a stand, a tall narrow window onto domed rooftops with lit windows at night",
}
# What the earlier throws were told (kept so their drafts can be read). The first: "an empty luxurious study on a space station, a tall arched window overlooking a
# ringed planet in outer space, a heavy wooden desk with brass lamps in the middle, marble floor, red banners, marble busts"; "an empty starship ready room, a wide
# panoramic viewport overlooking a fleet of warships in outer space, a glowing chart table in the middle, steel walls with brass trim, a red banner, open deck"; "an
# empty counting house, a long wooden table with brass scales and an abacus, shelves of ledgers, a tall wide window overlooking a domed city under a huge gas giant
# planet, brass lamps"; "an empty law library, tall bookshelves, marble columns, a high carved wooden bench with a gavel in the middle, a lectern, a tall window
# overlooking a domed city at night". Those finals were turned down whole (a stage set with a neon galaxy in a porthole; tracer fire in a Tron room; a library with
# a lamp for a planet; a temple portico on steps). The throws from the fifth study to the sixth naval, eighth counting house and eighth law room (16:9, 768 x 432,
# the colour lay-in repainted at 0.45 with no net; their script is work/rooms_16x9.py) were told "a senator's study on a space station, a tall narrow arched
# window onto a ringed planet like Saturn, a massive dark wood desk with brass lamps, bookshelves, plain crimson banners, bronze eagles, marble floor"; "a starship
# ready room, a wide viewport onto a line of grey warships, a round brass chart table with a glowing star map, blued steel bulkheads, brass trim, crimson banners,
# brass wall lamps"; "a banker's counting house, a long table with brass scales, an abacus and stacks of gold coins, shelves of ledgers, a tall window onto glass
# domes under a huge banded gas giant like Jupiter, brass lamps"; "a jurist's library, shelves of leather codices, a carved wooden judge's bench with a gavel, a
# lectern, a tall narrow window onto a domed city at night, brass lamps, marble floor", with STYLE "... painterly digital oil painting, cinematic, warm lamplight,
# cold blue starlight, empty room" and NEG "person, people, man, woman, face, statue, photo, 3d render, cartoon, text, watermark, frame, blurry, daylight, blue
# sky, clouds, sea, sailing ship, earth, office chair, computer, modern". Their finals (study 62, naval 62, stewardship 52, law 81) were turned down in the
# second review: all four soft (a shallow depth of field and a gentle finish) and flat; the study's ring a rod skewering the planet, marks on its banners, a blue
# bank and a red slab at its edges; the ready room cel-shaded, its ships fish and blimps, a swirl on the planet's limb, roundels and squiggles on its banners, a
# chair and a step across its deck; the counting house an orrery shop with a jewelled gilt planet, a balustrade for an abacus and the table in a window vitrine;
# the law room a home library with a horn for a gavel, a coffee table for a bench and snow for a city.
DRAFTS = 2                                     # drafts painted a room; the best is kept (three before the seventh study)
RETHROWN = {"study": 7, "naval": 8, "stewardship": 12, "law": 10}   # room -> times its drafts were painted again from new seeds, as faults showed: the planet hung in the
# study as a lamp, then its arched window became a round porthole; a clerk and a judge sat at the desks; the counting house's three windows each grew a planet; the
# library grew statues and a clock face; the bright, evenly lit floors were painted as tables, plank lines running to the window as one long table, the ringed
# planet and the fleet's limb as plain moons, the ships as brass telescopes, then silver airships and saucers; the standard's eagle became a crescent, a star, a
# lantern, its cloth American stripes or a British cross; "glass domes" were painted as cloches on the table and Jupiter's swirls as eyes; the whole disc of the
# giant as a globe lamp on a cord. The sixth study and naval, eighth stewardship and law throws were finished and turned down (above). Hence this throw (study 7,
# naval 7, stewardship 9, law 9; seeds 12200-02, 12210-12, 14220-22, 14230-32): the 1.22 : 1 frame, the nets, and each room laid in again as the review asked.
# Study: the whole ringed planet inside the window at 42% of its width, its ring a thin pale ellipse 1:4 tilted 20 degrees through the centre, the far half behind
# the disc and its shadow across it; flat banners on brass rods with fold shading and a fringe; bookcases and tiles to both edges. Naval: long angular wedge hulls
# in side view, large to small up the glass, warm ports and a blue drive at each stern; no planet's limb, a faint haze; plain banners; no chair; the deck one
# plate with seams toward the viewer only; panelled, lit bulkheads. Counting house: a long table on legs, clear of the wall, under a tall three-paned window; the
# giant cropped by the frame, pale and softly banded; four lit domes in the lower third; a standing abacus with dark beads, a T-shaped balance, coins and
# ledgers; panelled walls of ledgers. Law: a tall carved desk, its gavel on its top; two lit marble columns at the window; a black robe with a crimson stole on a
# T-stand at the left, the lectern at the right; a city of low domes with lit windows under a clear sky. The study came right at once (70; its banners hang as
# crimson pilasters with gilt capitals, but carry no marks). The ready room's haze low in the glass became a sea, its largest hull a battleship off a rocky
# island, the small ones saucers, the chart a grey felt top, the deck planks (70); the counting house's giant an Earth with a blue rim and a glare, its domes a row
# of globes, its ledgers vases, its abacus a clock (90); the law room's lectern a telescope and its city a modern skyline (90). Hence naval 8, stewardship 10 and law
# 10 (seeds 13210-11, 15220-21, 15230-31): no haze, the hulls higher in the glass, "space battleships", a lit chart of orbits, wide steel plates; bands that show
# on the giant, flat domes on lit drums over the roofs of a city, "a city of glass domes", "ledger books", an open abacus with big beads; a solid lectern, broad
# low domes over terraces of roofs, a larger gavel. The ready room came right (80: four angular grey warships in a line, plain banners, riveted steel; 81 drew the
# hulls as comets), and the law room (100: columns, the robe on its stand, a lectern with a book; its city a skyline of spires, not domes). The counting house's
# giant was a dark disc with a bright rim and a sunburst (100) or banded but with a dark moon (101); the domes gold, or onion domes with finials; the balance's
# deep pans lamp shades or bells; the shelves on the back wall trinkets; the abacus, against the books, a rack of papers. Hence stewardship 11 (seeds 16220-21):
# the giant lit from the front, flat shallow pans, plain panelling behind the balance and the abacus, "spires" shunned. The giant was banded at last and the domes
# a city's (110; 111 cratered the giant like a moon), but the pans, lit by the lamp beside them, were still lamp shades, and the abacus a rack. Hence
# stewardship 12 (seeds 17220-21): the lamps moved along the table, the pans low on long chains, big red beads on a light frame. The balance came right (120:
# a T with two pans on chains; 121's was lost among bowls), the giant banded, the domes blue glass; the abacus is still painted as a small cabinet of books in
# both, and is left so. Kept: study 70, naval 80, stewardship 120, law 100.
KEEP = {"study": (7, 0), "naval": (8, 0), "stewardship": (12, 0), "law": (10, 0)}   # room -> the draft that is finished: (throw, number), i.e. <room>_<throw><number>.png
SIZE, FINAL = (640, 528), (1280, 1056)
STRENGTH, FINISH = 0.85, 0.45                  # how much the model repaints the laid-in room, and the doubled draft in the finishing pass. Without the nets, 0.62 kept
# the lay-in's colours and little else (the floor became cabinets, the viewport a screen), so the earlier throws were painted at 0.45 and finished at 0.4 or 0.3:
# soft. With the nets holding the plan, the colour can be nearly repainted. Finished at 0.5, each tile, seeing part of a room told about the whole, added what
# was not there: Saturn hung on a cord from the transom as a lamp (work/study_final_70_cord.png), gold devices on the ready room's banners, a signature in its
# corner, a spiral nebula and ghostly hulls in its glass (work/naval_final_80_marks.png); so 0.45, with the nets holding to the last step
COND = (0.7, 0.5)                              # how hard the MLSD and the depth nets hold; for the first CEASE of the steps, then the model paints freely. Tried on the
# study's first seed (review/cn1): at STRENGTH 1.0 the crimson went and Saturn became a glowing orb on a dark panel; without the depth net a chair and a screen
# stood at the desk; at 0.85 with both, a room
CEASE = 0.85
CONTROLNETS = ("lllyasviel/control_v11p_sd15_mlsd", "lllyasviel/control_v11f1p_sd15_depth")
REFINISHED = {"study": 1, "naval": 2, "law": 1}          # room -> times its finishing pass was painted again from another seed (the study's first hung Saturn on a cord: the
# window lies across the seam of two tiles, and neither saw the whole planet; "pendant lamp, cord" are shunned since)
FINISHED = {"naval": 0.35, "law": 0.35}       # rooms finished at other than FINISH: at 0.45, with the fifth tile, the ready room's banner still grew a starburst, a
# roundel and an emblem, and its glass a great blue arc and two small moons (work/naval_final_80_devices.png); the law room a signature in its corner, and its
# lectern's sloping desk a telescope's tube (work/law_final_100_signature.png). At 0.35 they keep what the draft has and still paint detail; the law room's
# second pass carved little people into the panels over its shelves; a third, from another seed with "relief, figures" shunned, carved them still and made
# the lectern a telescope again (work/law_final_100_telescope.png), so the second is kept (work/law_final_100_reliefs.png is the shipped law.webp)

# ---- the lay-in. Fractions of the picture, x from the left and y from the top; the vanishing point is at (0.5, eye).
PLAN = {
    # the back wall (x0, x1, top), where it meets the floor ("foot"), the eye level; the windows (x0, x1, y0, y1, arched, mullions);
    # the desk or table top (x0, x1 at its front edge, y0, y1) and the depth of its front (0 for a table on legs). An arch is
    # about 1:2 (narrower and it is a slot, squarer and the model rounds it into a porthole).
    "study":       {"back": (0.10, 0.90, 0.035), "foot": 0.64, "eye": 0.40, "windows": [(0.385, 0.615, 0.09, 0.60, 1, 0)], "desk": (0.29, 0.71, 0.615, 0.665, 0.10)},
    "naval":       {"back": (0.08, 0.92, 0.04), "foot": 0.68, "eye": 0.42, "windows": [(0.26, 0.74, 0.11, 0.52, 0, 2)], "desk": (0.31, 0.69, 0.60, 0.69, 0.0)},
    "stewardship": {"back": (0.10, 0.90, 0.03), "foot": 0.63, "eye": 0.40, "windows": [(0.30, 0.70, 0.06, 0.55, 0, 2)], "desk": (0.17, 0.83, 0.60, 0.68, 0.0)},
    "law":         {"back": (0.10, 0.90, 0.03), "foot": 0.62, "eye": 0.40, "windows": [(0.42, 0.58, 0.08, 0.55, 1, 1)], "desk": (0.33, 0.67, 0.56, 0.60, 0.16)},
}
PALETTE = {   # the back wall, the side walls, ceiling, floor (where it meets the wall, and at the bottom of the picture), the desk's front and top, the trim (banners), brass, the lamps
    "study":       {"wall": (60, 50, 46), "side": (40, 32, 28), "ceil": (22, 18, 16), "floor": ((96, 98, 110), (34, 36, 44)), "desk": (34, 20, 14), "top": (104, 66, 40),
                    "trim": (128, 24, 22), "brass": (176, 136, 72), "lamp": (255, 190, 110), "stone": (150, 144, 136)},
    "naval":       {"wall": (40, 50, 64), "side": (28, 35, 46), "ceil": (14, 18, 24), "floor": ((66, 74, 90), (20, 24, 30)), "desk": (38, 46, 56), "top": (40, 96, 140),
                    "trim": (124, 24, 22), "brass": (140, 108, 62), "lamp": (255, 190, 110), "cold": (120, 210, 255)},
    "stewardship": {"wall": (50, 36, 27), "side": (34, 24, 18), "ceil": (16, 13, 12), "floor": ((84, 60, 42), (26, 18, 13)), "desk": (34, 21, 14), "top": (70, 44, 28),
                    "trim": (92, 30, 24), "brass": (190, 150, 80), "lamp": (255, 185, 100)},
    "law":         {"wall": (44, 30, 22), "side": (32, 22, 17), "ceil": (16, 13, 12), "floor": ((84, 84, 96), (28, 28, 36)), "desk": (38, 22, 14), "top": (92, 58, 36),
                    "trim": (120, 22, 24), "brass": (164, 126, 70), "lamp": (255, 195, 120), "stone": (168, 160, 148)},
}
LAMPS = {   # where the warm lamps are; the warm pool on the floor before the desk (it became a glowing ring when strong, in the study); the window's cold light on the floor
    "study":       {"at": [(0.345, 0.545), (0.655, 0.545)], "pool": 0.08, "cold": 0.25},
    "naval":       {"at": [(0.225, 0.29), (0.775, 0.29)], "pool": 0.05, "cold": 0.12},   # wall lamps between the banners and the viewport; the starlight a sheen on the steel
    "stewardship": {"at": [(0.44, 0.555), (0.60, 0.555)], "pool": 0.08, "cold": 0.20},   # clear of the balance: lit, its pans were painted as lamp shades
    "law":         {"at": [(0.35, 0.515), (0.65, 0.515)], "pool": 0.08, "cold": 0.22},
}
# Where things are on the finished paintings, judged by eye (written to <key>.json): the open floor where visitors stand (a band of
# y and the x it runs across), the top of the desk or table where letters lie, and the window.
SEEN = {   # measured on the finals of study 70, naval 80, stewardship 120 and law 100 with a 0.05 grid laid over them (review/grid.py)
    "study":       {"floor": {"y": [0.79, 0.99], "x": [0.06, 0.94]}, "desk": {"x": [0.29, 0.71], "y": [0.615, 0.66]}, "window": {"x": [0.385, 0.615], "y": [0.085, 0.60]}},
    "naval":       {"floor": {"y": [0.82, 0.99], "x": [0.05, 0.95]}, "desk": {"x": [0.31, 0.69], "y": [0.60, 0.68]}, "window": {"x": [0.26, 0.74], "y": [0.11, 0.52]}},
    "stewardship": {"floor": {"y": [0.84, 0.99], "x": [0.04, 0.96]}, "desk": {"x": [0.17, 0.83], "y": [0.60, 0.68]}, "window": {"x": [0.30, 0.70], "y": [0.06, 0.55]}},
    "law":         {"floor": {"y": [0.81, 0.99], "x": [0.05, 0.95]}, "desk": {"x": [0.33, 0.67], "y": [0.56, 0.60]}, "window": {"x": [0.42, 0.58], "y": [0.08, 0.55]}},
}

def throw(k, r=None): return RETHROWN.get(k, 0) if r is None else r
def seed(k, n, r=None): return 5200 + 10 * list(ROOMS).index(k) + n + 1000 * throw(k, r)
def draft(work, k, n, r=None): return os.path.join(work, f"{k}_{throw(k, r)}{n}.png")   # a rethrow paints new files; the old stay, to compare and to keep
def words(k): return ROOMS[k] + ", " + STYLE, NEG + ", " + SHUN[k]

class Lay:
    """A room laid in three ways at once: colour (`a`, for the model to paint over), the straight lines of its architecture (`lines`, white on black, for the
    MLSD net) and how near each thing is (`near`, 0 far to 1 at the bottom of the picture, for the depth net). Coordinates are fractions of the picture."""
    def __init__(s, k, r):
        import numpy as np
        from PIL import Image, ImageDraw
        s.np, s.Image, s.ImageDraw = np, Image, ImageDraw
        s.k, s.r, (s.W, s.H) = k, r, SIZE; P = PLAN[k]; s.C = PALETTE[k]
        (s.x0, s.x1, s.top), s.foot, s.eye = P["back"], P["foot"], P["eye"]
        s.a = np.zeros((s.H, s.W, 3)); s.near = np.zeros((s.H, s.W))
        s.ln = Image.new("L", SIZE, 0); s.l = ImageDraw.Draw(s.ln)
        yy, xx = np.mgrid[0:s.H, 0:s.W]; s.y, s.x = (yy + 0.5) / s.H, (xx + 0.5) / s.W
        s.db = (s.foot - s.eye) / (1 - s.eye)   # how near the back wall is
    def p(s, x, y): return (x * s.W, y * s.H)
    def mask(s, draw):
        m = s.Image.new("L", SIZE, 0); draw(s.ImageDraw.Draw(m), s.p); return s.np.asarray(m) > 127
    def fill(s, m, col, near=None, light=None):
        """Colour a shape, set how near it is, and clear the lines it hides (a floor seam drawn on under a desk would be painted across its front)."""
        c = s.np.array(col, float)
        if light is None: s.a[m] = c
        elif s.np.ndim(light): s.a[m] = c * light[m][..., None]
        else: s.a[m] = c * light
        if near is not None: s.near[m] = near if s.np.isscalar(near) else near[m]
        s.clear(m)
    def clear(s, m):
        ln = s.np.asarray(s.ln).copy(); ln[m] = 0; s.ln = s.Image.fromarray(ln); s.l = s.ImageDraw.Draw(s.ln)
    def poly(s, pts, col, near=None, light=None, line=1):
        m = s.mask(lambda d, p: d.polygon([p(*q) for q in pts], fill=255)); s.fill(m, col, near, light)
        if line: s.l.polygon([s.p(*q) for q in pts], outline=255, width=line)
        return m
    def rect(s, x0, y0, x1, y1, col, near=None, light=None, line=1): return s.poly([(x0, y0), (x1, y0), (x1, y1), (x0, y1)], col, near, light, line)
    def line(s, x0, y0, x1, y1, w=1): s.l.line((*s.p(x0, y0), *s.p(x1, y1)), fill=255, width=w)
    def floor_at(s, y): return (y - s.eye) / (1 - s.eye)   # nearness of the floor at a height in the picture
    def vp(s, x, y, to):   # the point on the line from (x, y) toward the vanishing point... or away from it, at height `to`
        return 0.5 + (x - 0.5) * (to - s.eye) / (y - s.eye), to

    def box(s):
        """The back wall, the side walls, the ceiling and the floor, with how near each is; the room's corners as lines."""
        np = s.np; C = s.C; x, y = s.x, s.y
        tl = np.where(x < s.x0, (0.5 - x) / (0.5 - s.x0), np.where(x > s.x1, (x - 0.5) / (s.x1 - 0.5), 1.0))   # how far toward the viewer, on a side wall
        yf, yc = s.eye + (s.foot - s.eye) * tl, s.eye - (s.eye - s.top) * tl
        floor, ceil = y > yf, y < yc; side = ~floor & ~ceil & (tl > 1)
        s.a[:] = C["wall"]; s.near[:] = s.db
        s.a[side] = C["side"]; s.near[side] = (s.db * tl)[side]
        s.a[ceil] = C["ceil"]; s.near[ceil] = np.clip(s.db * (s.eye - y) / (s.eye - s.top), 0, 1)[ceil]
        t = np.clip((y - s.foot) / (1 - s.foot), 0, 1) ** 0.8; col = np.array(C["floor"][0], float) * (1 - t[..., None]) + np.array(C["floor"][1], float) * t[..., None]
        s.a[floor] = col[floor]; s.near[floor] = np.clip(s.floor_at(y), 0, 1)[floor]
        s.floor_m, s.side_m = floor, side
        for xc in (s.x0, s.x1):
            s.line(xc, s.top, xc, s.foot, 2); s.line(xc, s.foot, *s.vp(xc, s.foot, 1.0), 2); s.line(xc, s.top, *s.vp(xc, s.top, 0.0), 2)
        s.line(s.x0, s.foot, s.x1, s.foot, 2); s.line(s.x0, s.top, s.x1, s.top, 2)

    def tiles(s, kind, size):
        """The floor's pattern in colour and lines: square tiles, planks or deck seams. Seams run toward the viewer; tiles also have rows across."""
        np = s.np; x, y = s.x, s.y; f = s.floor_m; dy = np.maximum(y - s.eye, 1e-3)
        X, Z = (x - 0.5) * s.W / s.H / dy, 1 / dy   # where each pixel lies on the floor, the eye one unit above it
        u = X / size; m = np.ones_like(X)
        if kind == "tiles":
            v = Z / size; m = np.where((np.floor(u) + np.floor(v)) % 2 == 0, 1.07, 0.93)
            m[(np.minimum(u % 1, 1 - u % 1) < 0.025) | (np.minimum(v % 1, 1 - v % 1) < 0.025)] = 0.8
        elif kind == "planks":
            m = 1 + 0.06 * np.sin(np.floor(u) * 2.7) ; m[np.minimum(u % 1, 1 - u % 1) < 0.03] = 0.82
        else:
            m[np.minimum(u % 1, 1 - u % 1) < 0.012] = 0.7
        m = m * (1 + 0.04 * s.r.normal(0, 1, m.shape)); s.a[f] *= m[f][..., None]
        span = (0.5 * s.W / s.H) / (1 - s.eye) / size + 1   # seams that reach the bottom of the picture or a side of it
        for i in range(-int(span) - 6, int(span) + 7):
            Xi = i * size; xb = 0.5 + Xi * (1 - s.eye) * s.H / s.W   # where the seam meets the bottom of the picture (or beyond it)
            xf = 0.5 + Xi * (s.foot - s.eye) * s.H / s.W                # and the foot of the wall
            if not (s.x0 <= xf <= s.x1): continue
            if 0 <= xb <= 1: s.line(xf, s.foot, xb, 1.0)
            else:
                edge = 0.0 if xb < 0 else 1.0; ye = s.eye + (edge - 0.5) * s.W / s.H / Xi; s.line(xf, s.foot, edge, ye)
        if kind == "tiles":
            Zb = 1 / (s.foot - s.eye); j = int(Zb / size)
            while j > 0:
                yr = s.eye + 1 / (j * size)
                if s.foot < yr < 1:
                    xl = max(0.0, 0.5 - (0.5 - s.x0) * (yr - s.eye) / (s.foot - s.eye)); s.line(xl, yr, 1 - xl, yr)
                j -= 1

    def side_shelves(s, rows, wood):
        """Shelves of books down both side walls, in perspective, to the edges of the picture: dark cases, rows of spines, the shelves as lines."""
        np = s.np; x, y = s.x, s.y
        for left in (True, False):
            tl = (0.5 - x) / (0.5 - s.x0) if left else (x - 0.5) / (s.x1 - 0.5)
            yt = s.eye - (s.eye - (s.top + 0.06)) * tl; yb = s.eye + (s.foot - s.eye) * tl
            on = s.side_m & (tl > 1) & (y > yt) & (y < yb)
            s.a[on] = wood; h = (y - yt) / np.maximum(yb - yt, 1e-3)
            band = np.floor(h * rows); within = h * rows - band
            sp = np.floor((tl - 1) * 160); hue = np.stack([(sp * 37 % 60) + 40, (sp * 23 % 30) + 22, (sp * 11 % 20) + 14], -1)
            books = on & (within > 0.12) & (within < 0.9); s.a[books] = hue[books] * (0.8 + 0.4 * ((sp % 3) / 3))[books][..., None]
            for j in range(rows + 1):   # each shelf, from the back corner to the edge of the picture
                fy = j / rows; xa = s.x0 if left else s.x1; ya = (s.top + 0.06) + (s.foot - (s.top + 0.06)) * fy
                t = (0.5 / (0.5 - s.x0)) if left else (0.5 / (s.x1 - 0.5)); s.line(xa, ya, 0.0 if left else 1.0, s.eye + (ya - s.eye) * t)

    def bookcase(s, x0, y0, x1, y1, rows, wood):
        """A bookcase on the back wall: a dark wooden case, rows of leather spines, the shelves and the case as lines."""
        s.rect(x0, y0, x1, y1, wood, s.db + 0.03); h = (y1 - y0) / rows
        hues = [(92, 30, 24), (70, 44, 26), (40, 52, 36), (96, 70, 38), (60, 30, 30), (34, 38, 52), (110, 84, 52)]
        im = s.Image.fromarray(s.a.clip(0, 255).astype("uint8")); d = s.ImageDraw.Draw(im)
        for j in range(rows):
            t0, t1 = y0 + j * h + 0.006, y0 + (j + 1) * h - 0.006; x = x0 + 0.006
            while x < x1 - 0.008:
                w = s.r.integers(3, 7) / s.W; c = hues[int(s.r.integers(0, len(hues)))]; m = s.r.uniform(0.7, 1.15); ty = t0 + s.r.uniform(0, 0.25) * (t1 - t0)
                d.rectangle((*s.p(x, ty), *s.p(min(x + w, x1 - 0.006), t1)), fill=tuple(int(min(255, v * m)) for v in c)); x += w + (1 / s.W if s.r.random() < 0.2 else 0)
            d.rectangle((*s.p(x0, t1), *s.p(x1, t1 + 0.008)), fill=tuple(int(v * 1.7) for v in wood)); s.line(x0, t1 + 0.008, x1, t1 + 0.008)
        box = s.mask(lambda d_, p: d_.rectangle((*p(x0, y0), *p(x1, y1)), fill=255)); s.a[box] = s.np.asarray(im).astype(float)[box]

    def banner(s, cx, top, bot, w, cloth, brass):
        """A plain cloth banner hung flat on the wall from a brass rod: soft vertical folds, a short gold fringe; no device on it (they grew letters and roundels)."""
        np = s.np; x0, x1 = cx - w / 2, cx + w / 2
        u = np.clip((s.x - x0) / w, 0, 1); fold = (0.86 + 0.14 * np.sin(np.pi * 2 * u) ** 2) * (0.8 + 0.2 * np.sin(np.pi * u)) * (1 - 0.15 * np.clip((s.y - top) / (bot - top), 0, 1))   # two soft folds
        s.rect(x0, top + 0.006, x1, bot, cloth, s.db + 0.09, fold, line=0)   # no outline: outlined, the net framed it as an inset panel (study 70, calibration)
        s.rect(x0, bot, x1, bot + 0.02, (200, 158, 72), s.db + 0.09, 0.9 + 0.2 * (np.floor(s.x * s.W) % 3 == 0), line=0); s.line(x0, bot, x1, bot); s.line(x0, bot + 0.02, x1, bot + 0.02)
        s.rect(cx - 0.62 * w, top - 0.004, cx + 0.62 * w, top + 0.006, brass, s.db + 0.1)
        for e in (cx - 0.62 * w, cx + 0.62 * w): s.poly([(e - 0.006, top - 0.01), (e + 0.006, top - 0.01), (e + 0.006, top + 0.012), (e - 0.006, top + 0.012)], brass, s.db + 0.1, line=0)

    def lamp(s, x, base, h, shade, brass):
        """A brass desk lamp standing on the desk top: a stem, a warm shade."""
        s.rect(x - 0.003, base - h, x + 0.003, base, brass, s.near_at(base) + 0.02)
        s.rect(x - 0.02, base - 0.005, x + 0.02, base, brass, s.near_at(base) + 0.02, line=0)
        s.poly([(x - 0.014, base - h - 0.035), (x + 0.014, base - h - 0.035), (x + 0.026, base - h), (x - 0.026, base - h)], shade, s.near_at(base) + 0.02)
    def near_at(s, y): return max(s.db, s.floor_at(y))

    def desk(s, x0, x1, y0, y1, front, panels=True):
        """A desk: its top seen from a little above, and a solid front, panelled (no gaps where legs could grow; a deep front became steps)."""
        np = s.np; C = s.C; inset = 0.02; dn = s.floor_at(y1 + front)
        s.poly([(x0 + inset, y0), (x1 - inset, y0), (x1, y1), (x0, y1)], C["top"], np.clip(s.floor_at(s.y + front), 0, 1), 0.85 + 0.3 * np.clip((s.y - y0) / (y1 - y0), 0, 1), 2)
        s.rect(x0, y1, x1, y1 + front, C["desk"], dn, line=2); s.rect(x0, y1, x1, y1 + 0.008, tuple(v * 1.6 for v in C["desk"]), dn, line=0)
        n = 4 if (x1 - x0) > 0.38 else 3
        for i in range(n if panels else 0):
            px0 = x0 + (x1 - x0) * (i + 0.1) / n; px1 = x0 + (x1 - x0) * (i + 0.9) / n
            s.rect(px0, y1 + 0.2 * front, px1, y1 + 0.82 * front, tuple(v * 1.3 for v in C["desk"]), dn, line=1)
            s.rect(px0 + 0.01, y1 + 0.27 * front, px1 - 0.01, y1 + 0.75 * front, tuple(v * 0.85 for v in C["desk"]), dn, line=1)
        return dn

    def window(s, view, frame, wide=5, vl=()):
        """The windows: what is beyond the glass pasted in (far: nearness 0) and drawn as lines (`vl`, functions of the line drawing, in pixels), the frame,
        a transom where an arch springs, the mullions, the sill."""
        np = s.np
        for wx0, wx1, wy0, wy1, arched, mull in PLAN[s.k]["windows"]:
            r = (wx1 - wx0) * s.W / 2 / s.H; spring = wy0 + r if arched else wy0
            def glass(d, p):
                if arched: d.ellipse((*p(wx0, wy0), *p(wx1, wy0 + 2 * r)), fill=255)
                d.rectangle((*p(wx0, spring), *p(wx1, wy1)), fill=255) if arched else d.rounded_rectangle((*p(wx0, wy0), *p(wx1, wy1)), radius=int(0.025 * s.H), fill=255)
            m = s.mask(glass); s.a[m] = view[m]; s.near[m] = 0.0; s.clear(m)
            if vl:   # drawn apart and kept only on the glass (the giant's limb ran on over the shelves)
                v_ = s.Image.new("L", SIZE, 0); d_ = s.ImageDraw.Draw(v_)
                for f in vl: f(d_)
                ln = s.np.asarray(s.ln).copy(); ln[m] = s.np.maximum(ln[m], s.np.asarray(v_)[m]); s.ln = s.Image.fromarray(ln); s.l = s.ImageDraw.Draw(s.ln)
            fw = wide / s.W
            if arched:
                ring = s.mask(lambda d, p: d.arc((*p(wx0, wy0), *p(wx1, wy0 + 2 * r)), 180, 360, fill=255, width=wide)); s.fill(ring, frame, s.db + 0.02)
                s.l.arc((*s.p(wx0, wy0), *s.p(wx1, wy0 + 2 * r)), 180, 360, fill=255, width=2)
                for xe in (wx0, wx1 - fw): s.rect(xe, spring, xe + fw, wy1, frame, s.db + 0.02)
                s.rect(wx0, spring - 0.004, wx1, spring + 0.004, frame, s.db + 0.02)
                cx, cy = (wx0 + wx1) / 2, spring
                for ang in (45, 90, 135):   # the fanlight's bars
                    a_ = np.radians(ang); ex, ey = cx - np.cos(a_) * (wx1 - wx0) / 2, cy - np.sin(a_) * r
                    bar = s.mask(lambda d, p: d.line((*p(cx, cy), *p(ex, ey)), fill=255, width=3)); s.fill(bar, frame, s.db + 0.02)
                    s.l.line((*s.p(cx, cy), *s.p(ex, ey)), fill=255, width=1)
            else:
                fr = s.mask(lambda d, p: d.rounded_rectangle((*p(wx0, wy0), *p(wx1, wy1)), radius=int(0.025 * s.H), outline=255, width=wide)); s.fill(fr, frame, s.db + 0.02)
                s.l.rounded_rectangle((*s.p(wx0, wy0), *s.p(wx1, wy1)), radius=int(0.025 * s.H), outline=255, width=2)
            for i in range(mull):
                mx = wx0 + (i + 1) / (mull + 1) * (wx1 - wx0); s.rect(mx - 0.004, spring, mx + 0.004, wy1, frame, s.db + 0.02)
            s.rect(wx0 - 0.012, wy1, wx1 + 0.012, wy1 + 0.014, tuple(min(255, v * 1.25) for v in frame), s.db + 0.05, line=2)   # the sill

    def light(s, extra=()):
        """Lamplight and starlight laid over the colour; noise; a slight blur. The nearness map is softened like a depth estimate."""
        np = s.np; from PIL import ImageFilter; C = s.C; L = LAMPS[s.k]; a = s.a; x, y = s.x, s.y
        def fall(gx, gy, sx, sy): return np.exp(-((x - gx) ** 2 / (2 * sx ** 2) + (y - gy) ** 2 / (2 * sy ** 2)))[..., None]
        for lx, ly in L["at"]: a += fall(lx, ly, 0.03, 0.03 * s.W / s.H) * np.array(C["lamp"]) * 0.6 + fall(lx, ly + 0.2, 0.14, 0.16) * np.array(C["lamp"]) * 0.12
        a += fall(0.5, 0.86, 0.22, 0.06) * np.array(C["lamp"]) * L["pool"] * s.floor_m[..., None]
        wx = np.mean([(w[0] + w[1]) / 2 for w in PLAN[s.k]["windows"]]); ww = max(w[1] - w[0] for w in PLAN[s.k]["windows"])
        a += fall(wx, 0.92, ww * 0.6, 0.10) * np.array([90, 120, 170]) * L["cold"] * s.floor_m[..., None]
        for e in extra: a += e
        a += s.r.normal(0, 5, a.shape); a *= (1 + 0.05 * s.r.normal(0, 1, a.shape[:2]))[..., None] * 1.0
        col = s.Image.fromarray(a.clip(0, 255).astype("uint8")).filter(ImageFilter.GaussianBlur(1.0))
        dep = s.Image.fromarray((np.clip(s.near, 0, 1) * 255).astype("uint8")).filter(ImageFilter.GaussianBlur(2.0)).convert("RGB")
        return col, s.ln.convert("RGB"), dep

def sphere(W, H, cx, cy, R, light):
    """A ball lit from `light` (x, y, z toward the viewer): the shade 0..1 on each pixel, and where the ball is (pixels)."""
    import numpy as np
    yy, xx = np.mgrid[0:H, 0:W].astype(float); nx, ny = (xx - cx) / R, (yy - cy) / R; rr = nx ** 2 + ny ** 2; nz = np.sqrt(np.clip(1 - rr, 0, 1))
    L = np.array(light, float); L /= np.linalg.norm(L)
    return np.clip(nx * L[0] + ny * L[1] + nz * L[2], 0, 1), rr < 1, nx, ny

def sky(W, H, r, top, low, stars):
    import numpy as np
    y = np.linspace(0, 1, H)[:, None, None]; a = (np.array(top) * (1 - y) + np.array(low) * y) * np.ones((H, W, 3))
    for _ in range(W * H // stars): sx, sy = r.integers(0, W), r.integers(0, H); a[sy, sx] = np.maximum(a[sy, sx], r.uniform(70, 255))
    return a

def lay_study(L):
    """The head of a house's study: a tall arched window in a stone surround onto Saturn, whole and small in the glass, its ring a thin pale ellipse through the
    disc's centre with the far half behind the disc and its shadow across it (a ring drawn as one line from the side pierced the planet like a spear); banners
    hung flat from rods either side; bookcases on the back wall and down both sides to the edges (bare edges grew a blue bank and a red slab)."""
    np = L.np; C = L.C; W, H = L.W, L.H
    L.box(); L.tiles("tiles", 0.62); L.side_shelves(7, (40, 26, 18))
    L.rect(L.x0, L.top, L.x1, L.top + 0.035, (40, 30, 24), L.db + 0.03, line=1)   # the cornice
    for bx0, bx1 in ((0.115, 0.285), (0.715, 0.885)): L.bookcase(bx0, 0.10, bx1, L.foot, 7, (40, 26, 18))
    for px0, px1 in ((0.285, 0.385), (0.615, 0.715)): L.rect(px0, L.foot - 0.16, px1, L.foot, (46, 34, 28), L.db + 0.01, line=1)   # wainscot below the banners
    wx0, wx1, wy0, wy1, _, _ = PLAN["study"]["windows"][0]; r_ = (wx1 - wx0) * W / 2 / H
    surround = L.mask(lambda d, p: (d.ellipse((*p(wx0 - 0.022, wy0 - 0.027), *p(wx1 + 0.022, wy0 + 2 * r_ + 0.027)), fill=255), d.rectangle((*p(wx0 - 0.022, wy0 + r_), *p(wx1 + 0.022, wy1)), fill=255)))
    L.fill(surround, C["stone"], L.db + 0.02, 0.8 + 0.3 * np.clip(1 - np.abs(L.x - 0.5) / 0.15, 0, 1))
    L.l.arc((*L.p(wx0 - 0.022, wy0 - 0.027), *L.p(wx1 + 0.022, wy0 + 2 * r_ + 0.027)), 180, 360, fill=255, width=1)
    for xe in (wx0 - 0.022, wx1 + 0.022): L.line(xe, wy0 + r_, xe, wy1)
    # beyond: stars, and Saturn whole in the glass, 42% of its width, below the transom
    v = sky(W, H, L.r, [3, 5, 14], [6, 8, 20], 300)   # dark to the sill: a lighter foot grew a rocky horizon
    cx, cy, R = 0.5 * W, 0.395 * H, 0.21 * (wx1 - wx0) * W; shade, disc, nx, ny = sphere(W, H, cx, cy, R, (-0.8, -0.35, 0.5))
    tilt = np.radians(-20); ca, sa = np.cos(tilt), np.sin(tilt); yy, xx = np.mgrid[0:H, 0:W].astype(float)
    u, w_ = (xx - cx) * ca + (yy - cy) * sa, -(xx - cx) * sa + (yy - cy) * ca   # along the ring's long axis, and across it
    lat = w_ / R; band = 0.9 + 0.1 * np.sin(lat * 9 + 0.5 * np.sin(lat * 3))
    body = np.array([230, 206, 156]) * (0.10 + 0.9 * shade ** 0.9 * band)[..., None]
    def ell(a_, b_): return (u / a_) ** 2 + (w_ / b_) ** 2
    A = 2.0 * R; ring = (ell(A, A / 4) < 1) & (ell(1.4 * R, 1.4 * R / 4) > 1); gap = (ell(1.72 * R, 1.72 * R / 4) < 1) & (ell(1.64 * R, 1.64 * R / 4) > 1)
    near_half = w_ > 0   # the ring's near half lies below its long axis (we look down on it a little)
    w2 = w_ + 0.28 * R; shadow = disc & ((u / A) ** 2 + (w2 / (A / 4)) ** 2 < 1) & ((u / (1.4 * R)) ** 2 + (w2 / (1.4 * R / 4)) ** 2 > 1) & ~(ring & near_half)   # the ring's shadow, a little above it
    ringcol = np.array([214, 196, 158]) * np.clip(0.75 - 0.25 * u / A, 0.45, 1.0)[..., None]
    v[ring & ~disc] = ringcol[ring & ~disc]; v[disc] = body[disc]; v[shadow] = v[shadow] * 0.35
    front = ring & near_half; v[front] = ringcol[front]; v[gap] *= 0.55
    L.window(v, (52, 40, 30))
    t = np.linspace(0, 2 * np.pi, 120); circ = [((cx + R * np.cos(q)) / W, (cy + R * np.sin(q)) / H) for q in t]
    L.l.line([L.p(*q) for q in circ], fill=255, width=1)
    for a_ in (A, 1.4 * R):   # the ring's edges: the far half only where it is not behind the disc
        pts = [(cx + a_ * np.cos(q) * ca - a_ / 4 * np.sin(q) * sa, cy + a_ * np.cos(q) * sa + a_ / 4 * np.sin(q) * ca) for q in np.linspace(0, 2 * np.pi, 160)]
        for (x0_, y0_), (x1_, y1_) in zip(pts, pts[1:]):
            mx, my = (x0_ + x1_) / 2, (y0_ + y1_) / 2
            if (mx - cx) ** 2 + (my - cy) ** 2 < R * R and -(mx - cx) * sa + (my - cy) * ca < 0: continue
            L.l.line((x0_, y0_, x1_, y1_), fill=255, width=1)
    for cx_ in (0.335, 0.665): L.banner(cx_, 0.14, 0.44, 0.06, C["trim"], C["brass"])
    dx0, dx1, dy0, dy1, front = PLAN["study"]["desk"]; L.desk(dx0, dx1, dy0, dy1, front)
    for lx, ly in LAMPS["study"]["at"]: L.lamp(lx, dy0 + 0.02, 0.05, (250, 200, 120), C["brass"])
    return L.light()

def lay_naval(L):
    """A starship's ready room: a wide viewport in a brass frame onto a line of long, flat, angular warships in side view, large at the lower left to small at the
    upper right, warm ports and a blue drive at each stern (rounded hull ends became blimps and fish); stars and a faint haze, no planet's limb (it grew a swirl);
    plain crimson banners either side and brass sconces; panelled, lit bulkheads (flat fills were painted as vector art); a round chart table on a drum; the deck one
    plate with seams toward the viewer only (a seam across it became a step under the visitors)."""
    np = L.np; C = L.C; W, H = L.W, L.H
    L.box(); L.tiles("deck", 1.1)   # wide plates: narrow ones were painted as planks (naval 70)
    for px in np.arange(L.x0 + 0.06, L.x1 - 0.01, 0.06): L.line(px, L.top, px, L.foot)   # bulkhead panels
    pan = 0.82 + 0.22 * np.clip(1 - (L.y - L.top) / (L.foot - L.top), 0, 1) + 0.06 * np.sin(np.floor((L.x - L.x0) / 0.06) * 2.1)
    back = ~L.floor_m & ~L.side_m & (L.y > L.top); L.a[back] *= pan[back][..., None]
    for ty, th in ((0.07, 0.016), (0.585, 0.018)): L.rect(L.x0, ty, L.x1, ty + th, C["brass"], L.db + 0.02, 0.8 + 0.3 * (np.abs(L.y - ty - 0.004) < 0.004), line=1)
    t = 0.5 / (0.5 - L.x0)
    for ty, th in ((0.07, 0.016), (0.585, 0.018)):   # and down the side walls to the edges
        for xa, edge in ((L.x0, 0.0), (L.x1, 1.0)):
            L.poly([(xa, ty), (edge, L.eye + (ty - L.eye) * t), (edge, L.eye + (ty + th - L.eye) * t), (xa, ty + th)], tuple(v * 0.8 for v in C["brass"]), None, line=0)
            L.line(xa, ty, edge, L.eye + (ty - L.eye) * t, 2)
    L.rect(L.x0, L.foot - 0.012, L.x1, L.foot, (16, 20, 26), L.db, line=1)   # a kick plate
    v = sky(W, H, L.r, [3, 5, 14], [12, 18, 36], 260)
    # no haze low in the glass: it read as a horizon, and the largest hull sailed on a sea past an island (naval 70)
    from PIL import Image, ImageDraw
    im = Image.fromarray(v.clip(0, 255).astype("uint8")); d = ImageDraw.Draw(im)
    hulls = ((0.40, 0.375, 0.22), (0.54, 0.28, 0.15), (0.63, 0.21, 0.10), (0.69, 0.16, 0.07))   # large lower left to small upper right, clear of the glass's foot
    vl = []
    for sx, sy, Ln in hulls:
        sx, sy, Ln = sx * W, sy * H, Ln * W; st = sx - Ln / 2; bow = (sx + Ln / 2, sy)
        upper = [bow, (sx - 0.05 * Ln, sy - 0.075 * Ln), (sx - 0.05 * Ln, sy - 0.095 * Ln), (sx - 0.28 * Ln, sy - 0.11 * Ln), (sx - 0.28 * Ln, sy - 0.135 * Ln), (st, sy - 0.14 * Ln), (st, sy)]
        lower = [bow, (st, sy), (st, sy + 0.08 * Ln)]
        d.polygon(upper, fill=(104, 110, 124)); d.polygon(lower, fill=(36, 40, 50))
        for x0_, x1_, y_ in ((sx - 0.05 * Ln, sx - 0.28 * Ln, sy - 0.095 * Ln), (sx - 0.28 * Ln, st, sy - 0.135 * Ln)): d.line((x0_, y_, x1_, y_ - 0.01 * Ln), fill=(150, 156, 170), width=1)   # the terraces' lit edges
        d.rectangle((st - 0.012 * Ln, sy - 0.10 * Ln, st, sy + 0.05 * Ln), fill=(170, 220, 255))   # the drives
        for px in np.arange(st + 0.06 * Ln, sx + 0.32 * Ln, max(2.5, Ln / 45)): d.rectangle((px, sy + 0.012 * Ln, px + 1, sy + 0.012 * Ln + 1), fill=(255, 200, 120))   # a row of lit ports
        for poly in (upper, lower): vl.append(lambda l, poly=poly: l.polygon(poly, outline=255, width=1))
    v = np.asarray(im).astype(float)
    for sx, sy, Ln in hulls:   # the drives' glow
        yy, xx = np.mgrid[0:H, 0:W].astype(float); g = np.exp(-(((xx - (sx - Ln / 2) * W) / (0.03 * Ln * W + 2)) ** 2 + ((yy - sy * H) / (0.10 * Ln * W + 2)) ** 2))
        v += g[..., None] * np.array([90, 150, 220])
    L.window(v, C["brass"], 6, vl)
    for cx_ in (0.165, 0.835): L.banner(cx_, 0.13, 0.47, 0.07, C["trim"], C["brass"])
    for lx, ly in LAMPS["naval"]["at"]: L.rect(lx - 0.008, ly - 0.03, lx + 0.008, ly + 0.03, C["brass"], L.db + 0.03); L.rect(lx - 0.005, ly - 0.02, lx + 0.005, ly + 0.01, (255, 214, 150), L.db + 0.03, line=0)
    dx0, dx1, dy0, dy1, _ = PLAN["naval"]["desk"]; dn = L.floor_at(0.79)
    L.rect(0.42, (dy0 + dy1) / 2, 0.58, 0.785, C["desk"], dn, 0.75 + 0.4 * np.clip(1 - np.abs(L.x - 0.47) / 0.08, 0, 1), line=2)   # the drum
    L.rect(0.40, 0.775, 0.60, 0.79, C["brass"], dn, line=1)
    rim = L.mask(lambda d, p: d.ellipse((*p(dx0, dy0), *p(dx1, dy1)), fill=255)); L.fill(rim, C["brass"], dn + 0.02)
    L.l.ellipse((*L.p(dx0, dy0), *L.p(dx1, dy1)), outline=255, width=2)
    face = L.mask(lambda d, p: d.ellipse((*p(dx0 + 0.012, dy0 + 0.008), *p(dx1 - 0.012, dy1 - 0.008)), fill=255)); L.fill(face, C["top"], dn + 0.02, 0.8 + 0.5 * np.exp(-(((L.x - 0.5) / 0.12) ** 2 + ((L.y - (dy0 + dy1) / 2) / 0.03) ** 2)))
    for f_ in (0.3, 0.55, 0.8):   # the chart: rings of orbits, lit, as colour and as lines (plain, the face was painted as grey felt)
        e = (0.5 - f_ * (dx1 - dx0) / 2 * 0.9, (dy0 + dy1) / 2 - f_ * (dy1 - dy0) / 2 * 0.9, 0.5 + f_ * (dx1 - dx0) / 2 * 0.9, (dy0 + dy1) / 2 + f_ * (dy1 - dy0) / 2 * 0.9)
        orbit = L.mask(lambda d, p: d.ellipse((*p(e[0], e[1]), *p(e[2], e[3])), outline=255, width=1)); L.a[orbit] = (150, 220, 255); L.l.ellipse((*L.p(e[0], e[1]), *L.p(e[2], e[3])), outline=255, width=1)
    L.l.ellipse((*L.p(dx0 + 0.012, dy0 + 0.008), *L.p(dx1 - 0.012, dy1 - 0.008)), outline=255, width=1)
    for _ in range(110):
        u, w_ = L.r.uniform(-1, 1), L.r.uniform(-1, 1)
        if u * u + w_ * w_ < 0.8: q = L.p(0.5 + u * (dx1 - dx0) / 2 * 0.9, (dy0 + dy1) / 2 + w_ * (dy1 - dy0) / 2 * 0.9); L.a[int(q[1]), int(q[0])] = (235, 245, 255)
    x, y = L.x, L.y
    glow = [np.exp(-(((x - 0.5) / 0.10) ** 2 + ((y - (dy0 + dy1) / 2) / 0.05) ** 2))[..., None] * np.array(C["cold"]) * 0.4,
            np.exp(-(((x - 0.5) / 0.22) ** 2 + ((y - 0.88) / 0.06) ** 2))[..., None] * np.array(C["cold"]) * 0.12 * L.floor_m[..., None]]
    return L.light(glow)

def lay_stewardship(L):
    """A counting house: a long dark table on legs standing clear of the wall on an open floor (a table in the window's recess read as a shop vitrine); behind
    it a tall three-paned window with panelled wall between its sill and the table; through it a huge pale banded gas giant cropped by the frame, only its lower
    left showing in the upper panes (a whole disc became a lamp, a dome, a jewelled ornament), and four large lit glass domes low in the glass; on the table a
    T-shaped brass balance, stacks of coins, closed ledgers and a standing abacus with dark beads (light beads on rails became a balustrade of brass balls)."""
    np = L.np; C = L.C; W, H = L.W, L.H
    L.box(); L.tiles("planks", 0.22); L.side_shelves(7, (34, 22, 15))
    for bx0, bx1 in ((0.115, 0.20), (0.80, 0.885)): L.bookcase(bx0, 0.07, bx1, L.foot, 8, (36, 23, 15))
    for px0, px1 in ((0.20, 0.29), (0.71, 0.80)):   # plain panelling either side of the window, for the balance and the abacus to stand against (before books, the abacus
        # was painted as a rack of papers, stewardship 100 and 101)
        L.rect(px0, 0.07, px1, L.foot, (44, 30, 21), L.db + 0.01, line=1)
        for py0, py1 in ((0.10, 0.36), (0.39, L.foot - 0.03)): L.rect(px0 + 0.012, py0, px1 - 0.012, py1, (52, 36, 25), L.db + 0.01, line=1)
    wx0, wx1, wy0, wy1, _, _ = PLAN["stewardship"]["windows"][0]
    L.rect(0.29, wy1 + 0.014, 0.71, L.foot, (40, 27, 19), L.db + 0.01, line=1)   # the wainscot under the sill
    for i in range(4): px0 = 0.30 + i * 0.1; L.rect(px0 + 0.01, wy1 + 0.03, px0 + 0.09, L.foot - 0.015, (48, 33, 23), L.db + 0.01, line=1)
    v = sky(W, H, L.r, [3, 5, 14], [10, 14, 30], 700)
    cx, cy, R = 0.74 * W, -0.12 * H, 0.44 * W; shade, disc, nx, ny = sphere(W, H, cx, cy, R, (-0.45, 0.35, 0.85))   # lit from the front: lit from the side it was a dark disc with a bright rim
    lat = ny + 0.04 * nx; b = 0.5 + 0.5 * np.sin(lat * 20 + 0.3 * np.sin(lat * 3))   # soft bands, three or so in sight, no swirls (fine ones and wobbles grew eyes)
    body = (np.array([228, 218, 196]) * b[..., None] + np.array([176, 136, 92]) * (1 - b[..., None])) * (0.12 + 0.88 * shade ** 0.8)[..., None]   # cream and ochre: a gold one was
    # gilded; bands too faint to see, and it was painted as an Earth with a blue rim (stewardship 90)
    v[disc] = body[disc]
    hz = 0.49 * H; v[int(hz):] = v[int(hz):] * 0.2 + np.array([10, 12, 20]) * 0.8   # the dark ground of the city
    from PIL import Image, ImageDraw
    im = Image.fromarray(v.clip(0, 255).astype("uint8")); d = ImageDraw.Draw(im)
    vl = [lambda l: l.arc((cx - R, cy - R, cx + R, cy + R), 0, 180, fill=255, width=1)]   # the giant's limb
    for bx0, bx1, bh in ((0.30, 0.40, 0.012), (0.38, 0.52, 0.02), (0.50, 0.62, 0.014), (0.60, 0.70, 0.018)):   # low roofs of the city, rows of lit windows
        d.rectangle((bx0 * W, hz - bh * H, bx1 * W, H), fill=(22, 22, 30))
        for row in np.arange(hz - bh * H + 3, 0.55 * H, 5):
            for px in np.arange(bx0 * W + 2, bx1 * W - 1, 4):
                if L.r.random() < 0.55: d.rectangle((px, row, px + 1, row + 1), fill=(255, 196, 110))
    for dx, dr in ((0.355, 0.045), (0.47, 0.068), (0.585, 0.042), (0.655, 0.028)):   # flat glass domes on lit drums (whole hemispheres on the horizon were painted as globes)
        dx, rw = dx * W, dr * W; rh = 0.6 * rw; base = hz - 0.022 * H
        d.rectangle((dx - 0.9 * rw, base, dx + 0.9 * rw, hz), fill=(40, 34, 30))
        for px in np.arange(dx - 0.85 * rw, dx + 0.85 * rw, 4): d.rectangle((px, base + 3, px + 1, base + 6), fill=(255, 200, 120))
        for i in range(int(rh)):
            t = i / rh; half = rw * (1 - t * t) ** 0.5; c = np.array([140, 196, 206]) * (0.7 + 0.4 * t) + np.array([90, 60, 20]) * (1 - t) ** 2
            d.line((dx - half, base - i, dx + half, base - i), fill=tuple(int(min(255, q)) for q in c))
        for f in (-0.6, -0.2, 0.2, 0.6): d.line((dx + f * rw, base, dx + f * rw * 0.5, base - rh * (1 - 0.6 * f * f) ** 0.5), fill=(80, 110, 120), width=1)   # the ribs of the glass
        vl.append(lambda l, b_=(dx - rw, base - rh, dx + rw, base + rh): l.arc(b_, 180, 360, fill=255, width=1))
        vl.append(lambda l, b_=(dx - 0.9 * rw, base, dx + 0.9 * rw, hz): l.rectangle(b_, outline=255, width=1))
    vl.append(lambda l: l.line((wx0 * W, hz, wx1 * W, hz), fill=255, width=1))
    L.window(np.asarray(im).astype(float), (44, 30, 20), 5, vl)
    # the table: a thick top, an apron, four turned legs on the floor, its shadow
    dx0, dx1, dy0, dy1, _ = PLAN["stewardship"]["desk"]; yl = 0.81; dn = L.floor_at(yl)
    L.poly([(dx0 + 0.03, dy0), (dx1 - 0.03, dy0), (dx1, dy1), (dx0, dy1)], C["top"], dn, 0.8 + 0.35 * np.clip((L.y - dy0) / (dy1 - dy0), 0, 1), 2)
    L.rect(dx0, dy1, dx1, dy1 + 0.035, C["desk"], dn, line=2)
    for lx in (dx0 + 0.015, dx1 - 0.045, dx0 + 0.07, dx1 - 0.10):
        far = lx in (dx0 + 0.07, dx1 - 0.10); bot = yl - (0.035 if far else 0)
        L.rect(lx, dy1 + 0.035, lx + 0.03, bot, tuple(v * (0.8 if far else 1.0) for v in C["desk"]), L.floor_at(bot), line=1)
    L.a *= 1 - 0.5 * (np.exp(-(((L.x - 0.5) / 0.30) ** 2 + ((L.y - yl) / 0.012) ** 2)) * L.floor_m)[..., None]
    b_ = C["brass"]; bx = 0.235   # the balance: a post, a beam, two pans on chains
    L.rect(bx - 0.004, 0.475, bx + 0.004, dy0 + 0.025, b_, dn + 0.02); L.rect(bx - 0.022, dy0 + 0.018, bx + 0.022, dy0 + 0.028, b_, dn + 0.02)
    L.rect(bx - 0.065, 0.47, bx + 0.065, 0.478, b_, dn + 0.02)
    for px in (bx - 0.058, bx + 0.058):   # the pans: flat shallow dishes on long chains, just above the table (deep ones high up were painted as bells and lamp shades)
        L.line(px, 0.478, px - 0.03, 0.588); L.line(px, 0.478, px + 0.03, 0.588)
        pan = L.mask(lambda d_, p, px=px: d_.chord((*p(px - 0.04, 0.580), *p(px + 0.04, 0.596)), 0, 180, fill=255)); L.fill(pan, (220, 182, 100), dn + 0.02)
        L.l.chord((*L.p(px - 0.04, 0.580), *L.p(px + 0.04, 0.596)), 0, 180, outline=255)
    ax0, ax1, ay0, ay1 = 0.695, 0.815, 0.455, dy0 + 0.01   # the abacus: an open wooden frame on two feet, brass rods, big dark beads; the wall shows through
    # (a dark box behind the beads was painted as a clock, stewardship 90)
    for e in ((ax0, ay0, ax0 + 0.009, ay1 + 0.02), (ax1 - 0.009, ay0, ax1, ay1 + 0.02), (ax0, ay0, ax1, ay0 + 0.011), (ax0, ay1 - 0.011, ax1, ay1)): L.rect(*e, (156, 104, 60), dn + 0.02)
    for j in range(6):   # big red lacquered beads, in rows, pushed to one side (small dark ones in a dark frame were a rack, stewardship 110 and 111)
        by = ay0 + 0.03 + j * 0.022; L.rect(ax0 + 0.009, by - 0.0016, ax1 - 0.009, by + 0.0016, (170, 136, 76), dn + 0.02, line=0); L.line(ax0 + 0.009, by, ax1 - 0.009, by)
        for i in range(8):
            bxx = ax0 + 0.019 + i * 0.0108 + (0.02 if i >= 2 + j % 4 else 0)
            if bxx < ax1 - 0.016: e_ = (bxx - 0.0052, by - 0.009, bxx + 0.0052, by + 0.009); bead = L.mask(lambda d_, p: d_.ellipse((*p(e_[0], e_[1]), *p(e_[2], e_[3])), fill=255)); L.fill(bead, (150, 42, 30), dn + 0.03, 0.8 + 0.5 * np.exp(-(((L.x - bxx + 0.002) / 0.003) ** 2 + ((L.y - by + 0.003) / 0.004) ** 2)))
    for i, n in enumerate((3, 2, 4)):   # closed ledgers, stacked
        for j in range(n): L.rect(0.475 + i * 0.03, dy0 + 0.03 - (j + 1) * 0.016, 0.50 + i * 0.03, dy0 + 0.03 - j * 0.016, ((96, 32, 24), (58, 40, 26), (40, 50, 34))[(i + j) % 3], dn + 0.02, line=1)
    for i, n in enumerate((4, 6, 3, 5)):   # stacks of coins
        cx_ = 0.335 + i * 0.02
        for j in range(n): L.poly([(cx_ - 0.008, dy0 + 0.035 - (j + 1) * 0.0065), (cx_ + 0.008, dy0 + 0.035 - (j + 1) * 0.0065), (cx_ + 0.008, dy0 + 0.035 - j * 0.0065), (cx_ - 0.008, dy0 + 0.035 - j * 0.0065)], (206, 168, 80), dn + 0.02, line=0)
    for lx, ly in LAMPS["stewardship"]["at"]: L.lamp(lx, dy0 + 0.015, 0.045, (250, 196, 110), C["brass"])
    return L.light()

def lay_law(L):
    """A jurist's study: a tall narrow arched window between two lit marble columns, onto a city of low domes with rows of lit windows under a clear starry sky
    (a blurred city became snow); shelves of codices either side under dark panelling; a heavy carved desk with a tall solid front and a gavel on its top (a low
    bench became a coffee table, a gavel on a stand a horn); a black robe with a crimson stole over a T-stand at the left, no knob for a head; a lectern at the
    right."""
    np = L.np; C = L.C; W, H = L.W, L.H
    L.box(); L.tiles("tiles", 0.66); L.side_shelves(7, (40, 26, 18))
    for bx0, bx1 in ((0.115, 0.345), (0.655, 0.885)): L.bookcase(bx0, 0.16, bx1, L.foot, 7, (40, 26, 18))
    for bx0, bx1 in ((0.10, 0.355), (0.645, 0.90)):   # dark panelled wood above the shelves
        L.rect(bx0, L.top, bx1, 0.16, (36, 24, 17), L.db + 0.02, line=1)
        for i in range(3): px0 = bx0 + 0.01 + i * (bx1 - bx0 - 0.02) / 3; L.rect(px0 + 0.006, L.top + 0.02, px0 + (bx1 - bx0 - 0.02) / 3 - 0.006, 0.14, (44, 30, 21), L.db + 0.02, line=1)
    L.rect(0.355, L.top, 0.645, L.foot, (34, 24, 18), L.db, line=0)   # dark wood behind the columns
    v = sky(W, H, L.r, [3, 5, 14], [12, 16, 34], 320)
    from PIL import Image, ImageDraw
    hz = 0.455 * H; im = Image.fromarray(v.clip(0, 255).astype("uint8")); d = ImageDraw.Draw(im); vl = []   # the city: long low roofs, rows of lit windows, three shallow domes
    d.rectangle((0.40 * W, hz + 0.012 * H, 0.60 * W, H), fill=(16, 18, 28))
    for row in np.arange(hz + 0.02 * H, H, 9):   # terraces of low roofs one behind another, each a row of lit windows (tall blocks of windows were a modern skyline, law 90)
        for px in np.arange(0.40 * W, 0.60 * W, 4):
            if L.r.random() < 0.5: d.rectangle((px, row, px + 1, row + 1), fill=(255, 200, 120))
        d.line((0.40 * W, row - 3, 0.60 * W, row - 3), fill=(30, 32, 46), width=1)
    for dx, dw in ((0.452, 0.03), (0.515, 0.045), (0.572, 0.026)):   # three broad low domes on drums
        x0_, x1_, base = (dx - dw) * W, (dx + dw) * W, hz + 0.012 * H; rh = 0.6 * dw * W
        d.rectangle((x0_ + 2, base, x1_ - 2, base + 0.02 * H), fill=(26, 28, 44))
        for px in np.arange(x0_ + 4, x1_ - 3, 4): d.rectangle((px, base + 4, px + 1, base + 7), fill=(255, 200, 120))   # the drum's windows
        d.chord((x0_, base - rh, x1_, base + rh), 180, 360, fill=(40, 44, 64)); d.arc((x0_, base - rh, x1_, base + rh), 200, 300, fill=(110, 116, 150), width=1)
        vl.append(lambda l, b_=(x0_, base - rh, x1_, base + rh): l.arc(b_, 180, 360, fill=255, width=1))
    L.window(np.asarray(im).astype(float), (44, 32, 24), 5, vl)
    for cx_ in (0.375, 0.625):   # the columns: lit shafts, a simple capital and base
        shaft = 0.5 + 0.5 * np.clip(1 - np.abs(L.x - (cx_ - 0.008)) / 0.03, 0, 1) ** 0.8   # a round shaft, lit from the left (a thin bright one became a neon tube)
        L.rect(cx_ - 0.027, 0.12, cx_ + 0.027, L.foot - 0.03, C["stone"], L.db + 0.06, shaft, line=2)
        L.rect(cx_ - 0.036, 0.09, cx_ + 0.036, 0.12, C["stone"], L.db + 0.06, 0.9, line=2); L.rect(cx_ - 0.04, 0.08, cx_ + 0.04, 0.092, C["stone"], L.db + 0.06, 0.75, line=1)
        L.rect(cx_ - 0.036, L.foot - 0.03, cx_ + 0.036, L.foot, C["stone"], L.db + 0.06, 0.7, line=2)
    dx0, dx1, dy0, dy1, front = PLAN["law"]["desk"]; dn = L.desk(dx0, dx1, dy0, dy1, front)
    gx, gy = 0.565, dy0 + 0.02   # the gavel on its round block
    blk = L.mask(lambda d_, p: d_.ellipse((*p(gx - 0.03, gy - 0.004), *p(gx + 0.03, gy + 0.016)), fill=255)); L.fill(blk, (70, 40, 24), dn + 0.02); L.l.ellipse((*L.p(gx - 0.03, gy - 0.004), *L.p(gx + 0.03, gy + 0.016)), outline=255)
    L.rect(gx - 0.02, gy - 0.02, gx + 0.014, gy + 0.002, (52, 30, 17), dn + 0.03); L.poly([(gx + 0.012, gy - 0.012), (gx + 0.07, gy - 0.002), (gx + 0.07, gy + 0.004), (gx + 0.012, gy - 0.005)], (80, 48, 26), dn + 0.03)
    for lx, ly in LAMPS["law"]["at"]: L.lamp(lx, dy0 + 0.02, 0.04, (250, 204, 130), C["brass"])
    sx, top, bot = 0.20, 0.42, 0.78; sn = L.floor_at(bot)   # the robe's stand, and the robe thrown over its bar
    L.rect(sx - 0.004, top, sx + 0.004, bot, (60, 44, 30), sn); L.rect(sx - 0.035, bot - 0.006, sx + 0.035, bot, (60, 44, 30), sn)
    L.rect(sx - 0.042, top - 0.004, sx + 0.042, top + 0.004, C["brass"], sn + 0.01)
    robe = L.poly([(sx - 0.045, top + 0.002), (sx + 0.045, top + 0.002), (sx + 0.052, 0.58), (sx + 0.04, 0.70), (sx + 0.012, 0.66), (sx - 0.012, 0.71), (sx - 0.04, 0.68), (sx - 0.052, 0.57)], (22, 20, 26), sn + 0.01,
                  0.8 + 0.4 * np.cos((L.x - sx) / 0.09 * 2 * np.pi * 2) ** 2, line=1)
    for st in (sx - 0.022, sx + 0.014): L.rect(st, top + 0.004, st + 0.012, 0.62, C["trim"], sn + 0.02, line=1)
    lx = 0.79; ln_ = L.floor_at(0.76)   # the lectern: a solid tapered pedestal, a broad sloping desk on it (a thin post under a narrow slope was painted as a telescope, law 90)
    L.poly([(lx - 0.02, 0.60), (lx + 0.02, 0.60), (lx + 0.03, 0.745), (lx - 0.03, 0.745)], (62, 40, 26), ln_, 0.8 + 0.4 * np.clip(1 - np.abs(L.x - lx + 0.008) / 0.03, 0, 1), line=2)
    L.rect(lx - 0.042, 0.745, lx + 0.042, 0.768, (54, 34, 22), ln_, line=2)
    L.poly([(lx - 0.055, 0.585), (lx + 0.045, 0.548), (lx + 0.055, 0.572), (lx + 0.055, 0.585), (lx - 0.045, 0.622), (lx - 0.055, 0.608)], (92, 60, 36), ln_ + 0.01, line=2)
    L.line(lx - 0.055, 0.585, lx + 0.055, 0.572)
    return L.light()

LAYS = {"study": lay_study, "naval": lay_naval, "stewardship": lay_stewardship, "law": lay_law}

def underpaint(k, n=0, t=None):
    """The room laid in where PLAN says: (colour, lines, depth), each the size of a draft."""
    import numpy as np
    return LAYS[k](Lay(k, np.random.default_rng(seed(k, n, t))))

def model(a):
    import torch
    from diffusers import StableDiffusionControlNetImg2ImgPipeline, ControlNetModel, LCMScheduler
    torch.set_num_threads(a.threads)
    nets = [ControlNetModel.from_pretrained(c, torch_dtype=torch.float32, variant="fp16", cache_dir=a.cache) for c in CONTROLNETS]
    pipe = StableDiffusionControlNetImg2ImgPipeline.from_pretrained(a.model, controlnet=nets, torch_dtype=torch.float32, safety_checker=None)
    if "lcm" in a.model.lower(): pipe.scheduler = LCMScheduler.from_config(pipe.scheduler.config)
    pipe.set_progress_bar_config(disable=True)
    for k in ROOMS:   # the text encoder reads 75 tokens and silently drops the rest: a prompt or negative over that would not say what it seems to
        for text in words(k):
            if len(pipe.tokenizer(text).input_ids) > pipe.tokenizer.model_max_length: sys.exit(f"{k}: over 75 tokens: {text}")
    return torch, pipe

def run(pipe, torch, k, init, lines, depth, strength, steps, cfg, s, cease=CEASE):
    prompt, neg = words(k)
    return pipe(prompt=prompt, negative_prompt=neg, image=init, control_image=[lines, depth], strength=strength, num_inference_steps=round(steps / strength),
                guidance_scale=cfg, controlnet_conditioning_scale=list(COND), control_guidance_end=cease, generator=torch.Generator().manual_seed(s)).images[0]

def paint(a):
    os.makedirs(a.work, exist_ok=True)
    todo = [(k, n, throw(k)) for n in range(DRAFTS) for k in ROOMS] + [(k, n, r) for k, (r, n) in KEEP.items() if r != throw(k)]   # a first draft of every room, then the second...; and a kept one from an earlier throw
    todo = [(k, n, r) for k, n, r in todo if not os.path.exists(draft(a.work, k, n, r)) and (not a.only or k in a.only)]
    if not todo: print("all drafted"); return
    torch, pipe = model(a)
    for k, n, r in todo:
        t = time.time(); col, lines, depth = underpaint(k, n, r); stem = draft(a.work, k, n, r)[:-4]
        col.save(stem + "_lay.png"); lines.save(stem + "_lines.png"); depth.save(stem + "_depth.png")
        run(pipe, torch, k, col, lines, depth, a.strength, a.steps, a.cfg, seed(k, n, r)).save(draft(a.work, k, n, r))
        print(os.path.basename(draft(a.work, k, n, r)), f"{time.time() - t:.0f}s", flush=True)

def doubled(im, r):
    """The draft at FINAL size for the finishing pass: sharpened and lightly grained, so the pass paints detail into it instead of keeping it soft."""
    import numpy as np
    from PIL import Image, ImageFilter
    im = im.convert("RGB").resize(FINAL, Image.LANCZOS).filter(ImageFilter.UnsharpMask(radius=2, percent=150, threshold=0))
    a = np.asarray(im).astype(float) + np.random.default_rng(r).normal(0, 6, (FINAL[1], FINAL[0], 3)); return Image.fromarray(a.clip(0, 255).astype("uint8"))

def tiled(pipe, torch, k, init, lines, depth, strength, steps, cfg, seed0):
    """The finishing pass in four overlapping tiles of about the draft's size, blended across the overlaps, each held by its part of the lines and the depth,
    and then a fifth over the middle, feathered in over them: the window and the desk lie across the seam of the four, and a tile that saw half a planet hung it
    on a cord as a lamp, and painted one pilaster crimson and its twin wood (work/study_final_70_hair.png). Painted whole at the final size the model stays soft;
    at the size it knows it paints edges and texture."""
    import numpy as np
    from PIL import Image
    W, H = init.size; tw, th = 704, 576; xs, ys = (0, W - tw), (0, H - th); acc = np.zeros((H, W, 3)); wsum = np.zeros((H, W, 1))
    def ramp(n, lo, hi):   # 1 inside, falling to 0 across the overlap at the tile's inner edges
        r = np.ones(n); o = 2 * tw - W if n == tw else 2 * th - H
        if lo: r[:o] = np.linspace(0.02, 1, o)
        if hi: r[-o:] = np.linspace(1, 0.02, o)
        return r
    for i, y in enumerate(ys):
        for j, x in enumerate(xs):
            box = (x, y, x + tw, y + th)
            t = run(pipe, torch, k, init.crop(box), lines.crop(box), depth.crop(box), strength, steps, cfg, seed0 + 2 * i + j, 1.0)   # the nets hold to the last step
            w = (ramp(th, i == 1, i == 0)[:, None] * ramp(tw, j == 1, j == 0)[None, :])[..., None]
            acc[y:y + th, x:x + tw] += np.asarray(t).astype(float) * w; wsum[y:y + th, x:x + tw] += w
    out = acc / wsum; x, y = (W - tw) // 2, 64; box = (x, y, x + tw, y + th)   # the middle: the window, the desk and what stands on it, whole in one tile
    t = np.asarray(run(pipe, torch, k, init.crop(box), lines.crop(box), depth.crop(box), strength, steps, cfg, seed0 + 4, 1.0)).astype(float)
    f = 80; ry, rx = np.minimum(1, np.minimum(np.arange(th) + 1, th - np.arange(th)) / f), np.minimum(1, np.minimum(np.arange(tw) + 1, tw - np.arange(tw)) / f)
    m = (ry[:, None] * rx[None, :])[..., None]; out[y:y + th, x:x + tw] = out[y:y + th, x:x + tw] * (1 - m) + t * m
    return Image.fromarray(out.clip(0, 255).astype("uint8"))

def final(a):
    from PIL import Image
    os.makedirs(a.out, exist_ok=True); todo = [k for k in ROOMS if not os.path.exists(os.path.join(a.work, k + "_final.png")) and (not a.only or k in a.only)]
    if todo:
        torch, pipe = model(a)
        for k in todo:
            r, n = KEEP[k]; t = time.time(); stem = draft(a.work, k, n, r)[:-4]
            src = Image.open(draft(a.work, k, n, r)).convert("RGB")
            lines = Image.open(stem + "_lines.png").convert("RGB").resize(FINAL, Image.NEAREST); depth = Image.open(stem + "_depth.png").convert("RGB").resize(FINAL, Image.BILINEAR)
            s0 = seed(k, n, r) + 77 + 13 * REFINISHED.get(k, 0)
            tiled(pipe, torch, k, doubled(src, seed(k, n, r)), lines, depth, FINISHED.get(k, a.strength), a.steps, a.cfg, s0).save(os.path.join(a.work, k + "_final.png"))
            print(k, "final", f"{time.time() - t:.0f}s", flush=True)
    for k in ROOMS:
        p = os.path.join(a.work, k + "_final.png")
        if not os.path.exists(p): continue
        Image.open(p).convert("RGB").save(os.path.join(a.out, k + ".webp"), "WEBP", quality=a.quality, method=6)
        json.dump({"room": k, "image": k + ".webp", "size": list(FINAL), **SEEN[k],
                   "note": "fractions of the picture, x from the left and y from the top: floor is where visitors stand, desk the top where letters lie. Painted at the Office's shape (about 1100 x 900): show it whole. AI-generated: see tools/art/rooms.py"},
                  open(os.path.join(a.out, k + ".json"), "w"), indent=1)
    print("packed", sum(os.path.exists(os.path.join(a.out, k + ".webp")) for k in ROOMS), "->", a.out)

def sharpness(path, width=1100):
    """Laplacian variance of the picture at the Office's width: how much fine edge it has (the art director's measure)."""
    import numpy as np
    from PIL import Image
    im = Image.open(path).convert("L"); g = np.asarray(im.resize((width, round(width * im.height / im.width)), Image.LANCZOS)).astype(float)
    return float((4 * g[1:-1, 1:-1] - g[:-2, 1:-1] - g[2:, 1:-1] - g[1:-1, :-2] - g[1:-1, 2:]).var())

def sheet(a):
    from PIL import Image, ImageDraw, ImageFont
    if a.finals: cells = [(k, os.path.join(a.work, k + "_final.png")) for k in ROOMS]; W, cols = 640, 2
    else: cells = [(f"{k} {RETHROWN.get(k, 0)}{n}" + (" *" if KEEP[k] == (throw(k), n) else ""), draft(a.work, k, n)) for k in ROOMS for n in range(DRAFTS)]; W, cols = 420, DRAFTS
    cells = [(t, p) for t, p in cells if os.path.exists(p)]; H = W * SIZE[1] // SIZE[0]; rows = (len(cells) + cols - 1) // cols
    try: font = ImageFont.truetype("DejaVuSans-Bold.ttf", 20)
    except OSError: font = ImageFont.load_default()
    out = Image.new("RGB", (W * cols, (H + 32) * rows), (12, 14, 20)); d = ImageDraw.Draw(out)
    for i, (t, p) in enumerate(cells):
        x, y = (i % cols) * W, (i // cols) * (H + 32); out.paste(Image.open(p).convert("RGB").resize((W, H), Image.LANCZOS), (x, y))
        if a.finals and a.layout:   # what <key>.json says, over the picture: floor green, desk gold, window blue
            for name, col in (("floor", (90, 200, 120)), ("desk", (230, 190, 90)), ("window", (110, 170, 255))):
                q = SEEN[t][name]; d.rectangle((x + q["x"][0] * W, y + q["y"][0] * H, x + q["x"][1] * W, y + q["y"][1] * H), outline=col, width=2)
        d.text((x + 8, y + H + 5), t + (f"   sharpness {sharpness(p):.0f}" if a.finals else ""), fill=(232, 200, 130), font=font)
    out.save(a.png); print(len(cells), "->", a.png)

if __name__ == "__main__":
    p = argparse.ArgumentParser(); s = p.add_subparsers(dest="cmd", required=True)
    for name, steps, strength in (("paint", 6, STRENGTH), ("final", 4, FINISH)):   # steps: about those actually run, whatever the strength
        q = s.add_parser(name); q.add_argument("work"); q.add_argument("--steps", type=int, default=steps); q.add_argument("--strength", type=float, default=strength)
        q.add_argument("--cfg", type=float, default=2.0); q.add_argument("--model", default="Lykon/dreamshaper-8-lcm"); q.add_argument("--cache", default=None)
        q.add_argument("--only", nargs="*", default=None)
        q.add_argument("--threads", type=int, default=max(1, (os.cpu_count() or 4) // 2))   # half: on a CPU shared with builds, all of it paints four times slower
        q.set_defaults(fn=paint if name == "paint" else final)
    q = s.choices["final"]; q.add_argument("out"); q.add_argument("--quality", type=int, default=84)
    q = s.add_parser("sheet"); q.add_argument("work"); q.add_argument("png"); q.add_argument("--finals", action="store_true"); q.add_argument("--layout", action="store_true"); q.set_defaults(fn=sheet)
    q = s.add_parser("lay"); q.add_argument("png")
    def lay(a):
        for k in ROOMS:
            for im, part in zip(underpaint(k), ("", "_lines", "_depth")): im.save(a.png.replace(".png", f"_{k}{part}.png"))
    q.set_defaults(fn=lay)
    q = s.add_parser("sharp"); q.add_argument("work"); q.set_defaults(fn=lambda a: [print(k, f"{sharpness(os.path.join(a.work, k + '_final.png')):.0f}") for k in ROOMS if os.path.exists(os.path.join(a.work, k + "_final.png"))])
    a = p.parse_args(); a.fn(a)
