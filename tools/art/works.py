#!/usr/bin/env python3
"""Praetoria — a picture for everything that can be built in a solar system (docs/BUILDINGS.md).

One small wide painting for each building, each great work, each track of infrastructure and each building of a system's own world,
painted with the same openly licensed model as the portraits (tools/art/portraits.py has the set-up) and written where the web
client reads them, with a list of what was painted:

    ./venv/bin/python tools/art/works.py paint <workdir>
    ./venv/bin/python tools/art/works.py pack  <workdir> src/Web/wwwroot/img/works

The key of a picture is the id of the thing in content (content/holdings/works.json, buildings.json, holdings_ext.json), so a new
building needs a line here and nothing else. Resumable: a picture already in the work directory is kept. `RETHROWN` paints one again
from another seed. AI-generated, which a store page must say.
"""
import os, sys, time, json
STYLE = "science fiction concept art, painterly digital matte painting, wide shot, cinematic lighting, muted colours with warm gold highlights, highly detailed"
NEG = "photo, anime, cartoon, text, letters, watermark, signature, frame, border, blurry, lowres, people in foreground, close-up face, logo, ui"
WORKS = {
    # ---- buildings: in a body's slots ----
    "outpost": "a few small pressurised domes and a landing pad on a barren grey moon, antenna mast, a lander, black starry sky",
    "domed_colony": "a huge transparent geodesic glass dome covering a small city of towers and parks, standing alone on a barren grey rocky plain at night, stars",
    "second_world": "a young colony town on a green hilly world, timber and stone houses, fields, a river, a ringed planet in the daytime sky",
    "terrace_farms": "steep green hillsides cut into endless terrace farms, irrigation channels, small farmhouses, two moons in the sky",
    "game_parks": "a wild alien forest and savannah preserve, a stone hunting lodge on a ridge, strange grazing herds far off, golden evening",
    "sea_farms": "floating kelp farms and fish pens on a calm alien ocean, pontoon walkways, harvest barges, low sun",
    "tidal_mills": "a huge sea wall of tidal turbines across a rocky strait, crashing water, industrial piers, stormy sky",
    "deep_harbour": "a great sea harbour where spacecraft land on the water beside stone quays, cranes, lighthouses, evening",
    "sun_fields": "a huge solar power plant, endless rows of solar panels in a desert, bright sun in a clear sky",
    "caravanserai": "a walled desert caravanserai with a market courtyard, awnings, wells, landed cargo haulers, dunes and red rock",
    "strip_mine": "a vast open-cast strip mine on a dead rocky world, terraces, giant excavators, ore conveyors, dust, black sky",
    "smelter_row": "a mile-long row of blast furnaces glowing orange on a volcanic world, molten metal, smoke stacks, slag heaps",
    "geothermal_tap": "geothermal power towers over a field of lava fissures, steam plumes, heavy pipes, a volcanic night",
    "ice_works": "an ice quarry on a frozen world, great blocks cut by machines, floodlights, a freight lifter, blue ice cliffs",
    "cold_granaries": "rows of frost-covered vault doors cut into a glacier wall, a snowy loading yard, sledges of sacks, cold blue light",
    "penal_colony": "a bleak prison camp on an icy barren plain, watchtowers, low barracks, a fence, searchlights, a mine entrance",
    "proving_ground": "a military proving ground on a cratered dead plain, tanks and troops on manoeuvres far off, shell bursts, bunkers, dust",
    "skimmer_fleet": "several large scoop spacecraft flying low through golden gas giant clouds with long intake funnels, vapour trails, a big tanker ship above",
    "cloud_habitat": "a floating city of domes and balloons drifting among the towering clouds of a gas giant, sunset light",
    "aerostat_refinery": "industrial refinery towers hanging under giant balloons in the sky of a gas giant, pipes, flares, orange clouds",
    "belt_foundry": "close view of a huge industrial space station with glowing orange furnace openings built onto a grey asteroid, floating in outer space, sparks, small tug spacecraft, starfield",
    "prospectors_exchange": "interior of a crowded frontier trading hall, miners and brokers at tables with ore samples and brass scales, lanterns, a round window onto asteroids",
    "hidden_dock": "a spacecraft slipping into a concealed hangar cut into a dark floating asteroid in outer space, dim red hangar lamps, starfield",
    "rare_earth_diggings": "close view of a steel mining headframe tower and drilling rigs on bright grey asteroid rock with glowing blue crystal veins, floodlights, black starry sky",
    "isotope_pits": "a deep open pit mine at night with an eerie green glow at the bottom, excavators on the terraces, warning lights",
    "mass_driver": "a miles-long electromagnetic rail launcher on a moon firing an ore canister into space, a planet on the horizon",
    "moon_fort": "close view of a massive concrete fortress with three huge gun turrets on a bright grey cratered moon, sunlit, long cannon barrels raised, a blue planet behind, black sky",
    "hermitage": "a tiny stone chapel and a few cells on an icy comet, a single lamp in a window, a glowing comet tail across the stars",
    "observatory": "a great telescope dome on a dark icy ridge under a blazing field of stars and a nebula, a few lit windows",
    "listening_post": "close view of three giant white satellite dishes on a bright sunlit grey moon surface, steel lattice supports, a small bunker, black sky with stars",
    "orbital_batteries": "armoured space weapon platforms with long cannons floating in orbit high above the curved horizon of a planet, running lights, starfield",
    "fleet_anchorage": "three long angular futuristic space cruisers with glowing blue engines lined up side by side at a metal space station, in orbit above a ringed planet, black space, stars",
    "waystation": "a wheel-shaped space station turning in open space with merchant ships docked round its rim, distant sun, stars",
    "customs_cordon": "two small patrol spacecraft shining searchlights on a large cargo starship stopped beside a glowing checkpoint buoy in open space, starfield",
    "relay_beacon": "a tall lonely signal relay tower in deep space with a great lit antenna array, pulses of light, vast starfield",
    "patrol_base": "a small armoured military station in deep space with corvettes docked and one leaving, running lights, dark void",
    "minefields": "a black spiked metal mine with a small red light floating in outer space, many more identical spiked mines behind it receding into the dark, starfield",
    # ---- the home world's own ----
    "governors_palace": "a grand governor's palace of white marble with a great dome, colonnades and banners above a colony city, guards on the steps small in the distance, golden hour",
    "forum": "a wide stone forum square ringed with colonnades in a colony city, market stalls and a crowd far off, a rostrum, statues, morning light",
    "academy": "an academy campus of domed halls, libraries and courtyards on a hill, students in robes small in the distance, cypress trees, a ringed planet in the evening sky",
    "great_temple": "a vast temple with a towering portico and a huge bronze door on a rocky hill, braziers burning, pilgrims climbing the long stair, dusk",
    "barracks_quarter": "a walled military barracks quarter with drill squares, armouries and watchtowers, ranks of soldiers on parade small in the distance, banners, dawn",
    "spaceport": "a busy spaceport on the ground, landing pads with cargo spacecraft, control tower, warehouses, cranes, floodlights, a city behind, dusk",
    "hospital": "interior of a large hospital ward, two long rows of white beds, tall arched windows, doctors and nurses in white, clean bright light",
    "mint": "a fortified mint building of heavy stone and bronze doors, sparks from coin presses seen through tall windows, guards, stacked strongboxes, lamplight",
    # ---- great works: one to a body ----
    "orbital_ring": "a colossal ring structure encircling an entire planet seen from orbit, city lights along the ring, docks, sunrise over the limb",
    "great_stair": "a space elevator, one thin straight tower rising from a city vertically through the clouds into space, seen from far away",
    "the_aegis": "a planet surrounded by a glowing honeycomb energy shield, a force field sphere of blue hexagons around the whole world, seen from space",
    "spire_city": "one immense tower city a mile high rising from a plain above the clouds, thousands of lit windows, flying craft, dusk",
    "dyson_lattice": "a gigantic hexagonal metal lattice shell half built around a blazing yellow star in outer space, sunlight streaming through the open hexagons, tiny spacecraft for scale, starfield",
    "sun_foundry": "a huge dark industrial station silhouetted in front of an enormous blazing sun, rivers of glowing molten metal pouring from it",
    "sky_harbour": "a vast flat floating platform city with landing pads hanging in the sky among orange gas giant cloud towers, large tanker spacecraft landed on it, seen from above",
    "hollowed_world": "the inside of a hollowed-out asteroid city, streets and lights wrapped round the inner cavern, a central sun-lamp",
    "fortress_moon": "an entire moon turned into a fortress, vast gun emplacements and armoured gates across its surface, a planet below",
    "house_mausoleum": "a monumental domed mausoleum on a grey moon, colonnades and statues, eternal flames, a blue planet low in the black sky",
    "gate_anchor": "an immense circular ring gate floating in outer space with a shimmering blue portal of light inside it, fortress towers on the ring, starships passing through, starfield",
    "grand_arsenal": "a colossal space station shipyard in outer space with a dozen grey armoured starships held in skeletal drydock frames, cranes, welding sparks, starfield",
    "long_cylinder": "the interior of a huge rotating cylinder space habitat, fields and towns curving up overhead, a strip of sunlight",
    "lighthouse": "a towering beacon station in deep space sending a great beam of golden light across the stars, ships following it",
    "ring_habitat": "a wheel-shaped space station floating among the icy rings of Saturn, the huge ringed planet behind it, starfield",
    "storm_tap": "a space station in orbit lowering a long glowing cable down into a gigantic red swirling storm on a gas giant, lightning in the clouds",
    "breakers_yard": "the stripped skeleton frame of a huge wrecked starship in a vast dark hangar, workers cutting it apart with welding torches, showers of sparks, scaffolding and cranes",
    "lode_works": "a single enormous metallic asteroid hollowed by mining, glittering tunnels, ore haulers, drilling lights",
    "great_dig": "an archaeological excavation of colossal ancient alien ruins half buried in dust, scaffolds, floodlights, tiny figures",
    "vault_archive": "an endless underground archive of shelves and sealed vaults in a great cavern, warm lamps, catwalks, a deep shaft",
    "undersea_colony": "glowing glass dome cities on a dark sea floor seen underwater, lit windows, submarines with headlights, a ceiling of blue ice high above, shafts of pale light",
    "ice_choir": "a cathedral hall carved into blue glacier ice, light blazing through the walls, a procession of robed pilgrims, mist",
    "garden_estates": "white villas and terraced gardens along a lush green valley, cypress trees, fountains, a lake, warm afternoon light",
    "old_capital_restored": "an ancient ruined palace city being restored, vast domes and colonnades, scaffolding, banners, golden hour",
    "reef_cities": "a town of white houses and towers built on top of giant coral reef rocks rising out of a turquoise sea, stone bridges between them, close view",
    "rift_citadel": "a fortress wall and citadel built across the mouth of an immense canyon, towers on both cliffs, a single great gate",
    "mantle_forge": "a colossal foundry sunk into a glowing chasm of magma on a volcanic world, rivers of molten metal, gantries",
    "moon_port": "rockets and spacecraft parked on landing pads beside domes on the grey surface of the moon, a control tower, the Earth in the black sky",
    "hollow_arsenal": "a secret fleet dock inside a hollow moon, warships berthed in a vast cavern, armoured doors opening to stars",
    "comet_watch": "a small gilded chapel riding an icy comet with a brilliant tail, lamps burning, the sun far off, deep stars",
    "flare_harvesters": "huge thin golden collector sails spread in outer space in front of a blazing star throwing out a great arc of solar flare, small harvester spacecraft, starfield",
    "twin_crown": "two suns setting side by side over a desert horizon, a bright beam of light joining the two suns, double sunset",
    # ---- infrastructure: the system's own ----
    "civil_works": "aqueducts, power pylons and paved roads crossing a plain to a growing colony city, construction cranes, morning",
    "habitats": "rings of sealed habitat blocks and greenhouse domes under construction, air towers, workers small in the distance",
    "orbital_traffic": "many small spacecraft, tugs and cargo lifters flying in neat lines in orbit above the curve of a blue planet, a traffic control space station with lights",
    "outworks": "a small supply depot space station and survey spacecraft in outer space between two distant planets, cargo pods, a small bright sun, starfield",
    "defence_grid": "a military satellite with gun turrets and radar dishes in orbit above a planet, more satellites in the distance, starfield",
    # ---- the buildings of a system's own world ----
    "power_plant": "a fusion power plant with great cooling towers and humming conduits on the edge of a colony city, night, glow",
    "mine": "the headframe and lit shaft mouth of a deep mine in a rocky industrial landscape, ore trains, smoke, dusk",
    "farm_complex": "rows of large glass greenhouses full of green crops, daylight, irrigation pipes, farm fields beyond",
    "market": "a bustling open market square in a colony city, awnings, stalls and crowds far off, columns, banners, warm light",
    "helium_skimmers": "a floating industrial refinery platform high in the sky among towering orange clouds, big spherical gas tanks, pipes, aerostat balloons holding it up",
    "rare_earth_refinery": "a chemical refinery of vats and towers processing glittering ore, coloured smoke, pipes, floodlights",
    "isotope_works": "a heavily shielded industrial works with thick concrete domes and warning lamps, guarded gates, night",
    "sensor_array": "five huge white radio telescope dishes in a row on a desert plateau, dusk, first stars",
    "arsenal": "a vast arsenal hall of stacked munitions and artillery, armoured doors, soldiers small in the distance, brazier light",
    "supply_depot": "a military supply base on a plain at night, rows of fuel tanks and stacked crates, landing pads with transport spacecraft loading under floodlights",
    "deep_drill": "a colossal drilling rig boring into the crust of a rocky world, a pillar of dust and fire, gantries, night",
    "orbital_foundry": "the interior of a huge space foundry, giant crucibles pouring glowing molten metal, a starship hull under construction, a big window onto a planet",
    "conscription_hall": "a stern marble muster hall with ranks of recruits drawn up before it, banners, officers on the steps, dawn",
    "bastion_guns": "enormous planetary defence cannons on a mountain fortress aimed at the sky, bunkers, searchlights, storm clouds",
    "exotics_lab": "a laboratory of glass vessels holding glowing strange matter, brass instruments, scholars far off, violet light",
    "free_port": "a crowded free port, ships of every kind landed along the quays, warehouses, cranes, flags, lanterns, dusk",
    "patronage_office": "a marble office hall where petitioners wait in a long colonnade before a great desk, clerks, lamplight",
    "granary_relief": "great stone public granaries with grain being handed out to a crowd in a forum, carts, columns, morning light",
}
# things that are in space: the model reaches for a sea and a shore unless it is told not to
SPACE = {"skimmer_fleet", "orbital_batteries", "fleet_anchorage", "waystation", "customs_cordon", "relay_beacon", "patrol_base", "minefields", "belt_foundry", "prospectors_exchange",
         "hidden_dock", "rare_earth_diggings", "orbital_ring", "dyson_lattice", "sun_foundry", "gate_anchor", "grand_arsenal", "lighthouse", "breakers_yard", "lode_works", "flare_harvesters",
         "twin_crown", "orbital_traffic", "outworks", "defence_grid", "orbital_foundry", "ring_habitat", "comet_watch", "hermitage", "moon_port", "moon_fort", "listening_post", "the_aegis", "storm_tap"}
NEG_SPACE = ", sea, ocean, water, waves, lake, shore, boat, sailing ship, battleship, warship at sea, clouds, mountains, horizon"
RETHROWN = {k: 1 for k in ("domed_colony", "sun_fields", "skimmer_fleet", "moon_fort", "listening_post", "orbital_batteries", "fleet_anchorage", "customs_cordon", "minefields",
                           "belt_foundry", "prospectors_exchange", "hidden_dock", "rare_earth_diggings", "dyson_lattice", "sun_foundry", "sky_harbour", "grand_arsenal", "ring_habitat",
                           "breakers_yard", "undersea_colony", "moon_port", "flare_harvesters", "twin_crown", "orbital_traffic", "outworks", "defence_grid", "sensor_array", "supply_depot",
                           "the_aegis", "storm_tap", "gate_anchor")}
RETHROWN.update({k: 2 for k in ("moon_fort", "listening_post", "belt_foundry", "rare_earth_diggings")})
RETHROWN.update({k: 3 for k in ("fleet_anchorage", "breakers_yard", "minefields", "twin_crown", "sun_foundry")})   # (the sensor array and the sun fields were tried twice more and their second throw kept)

# the audit of 2026-11-20 (docs/ART_DIRECTION.md, "the rule for pictures of things"): nineteen that did not show their subject, painted again from simpler prompts
RETHROWN.update({k: 5 for k in ("breakers_yard", "farm_complex", "hospital", "isotope_pits", "prospectors_exchange", "great_stair", "sun_fields", "defence_grid")})
RETHROWN.update({k: 8 for k in ("domed_colony", "sun_foundry")})
RETHROWN.update({k: 9 for k in ("minefields", "the_aegis", "sensor_array")})
RETHROWN.update({k: 10 for k in ("helium_skimmers", "twin_crown", "ring_habitat")})
RETHROWN.update({k: 12 for k in ("reef_cities", "moon_port", "orbital_foundry")})
NEG_EXTRA = {"helium_skimmers": ", sea, ocean, water, ship, boat"}

def paint(work):
    os.makedirs(work, exist_ok=True)
    todo = [k for k in WORKS if not os.path.exists(os.path.join(work, k + ".png"))]
    if not todo: print("nothing to paint"); return
    import torch
    from diffusers import AutoPipelineForText2Image, LCMScheduler
    torch.set_num_threads(os.cpu_count() or 4)
    pipe = AutoPipelineForText2Image.from_pretrained("Lykon/dreamshaper-8-lcm", torch_dtype=torch.float32, safety_checker=None)
    pipe.scheduler = LCMScheduler.from_config(pipe.scheduler.config); pipe.set_progress_bar_config(disable=True)
    for k in todo:
        t = time.time(); g = torch.Generator().manual_seed(7300 + list(WORKS).index(k) + 1000 * RETHROWN.get(k, 0))
        pipe(prompt=WORKS[k] + ", " + STYLE, negative_prompt=NEG + (NEG_SPACE if k in SPACE else "") + NEG_EXTRA.get(k, ""), num_inference_steps=6, guidance_scale=1.5, width=576, height=352, generator=g).images[0].save(os.path.join(work, k + ".png"))
        print(k, f"{time.time() - t:.0f}s", flush=True)

SIGNED = {"spaceport": 20}

def pack(work, out, width=432, quality=80):
    from PIL import Image
    os.makedirs(out, exist_ok=True); done = []
    for k in WORKS:
        p = os.path.join(work, k + ".png")
        if not os.path.exists(p): continue
        im = Image.open(p).convert("RGB"); w, h = im.size; trim = int(w * 0.03)   # a painted-in border, where there is one, is at the edge
        cut = SIGNED.get(k, 0)   # the model signs a picture now and then, in a bottom corner: that much of the bottom is cut, and the same share of the right
        im = im.crop((trim, trim, w - trim - round(cut * w / h), h - trim - cut)); im = im.resize((width, round(width * im.size[1] / im.size[0])), Image.LANCZOS)
        im.save(os.path.join(out, k + ".webp"), "WEBP", quality=quality, method=6); done.append(k)
    json.dump({"works": sorted(done)}, open(os.path.join(out, "manifest.json"), "w"), indent=0)
    print(f"{len(done)} of {len(WORKS)} pictures -> {out}")

if __name__ == "__main__":
    if len(sys.argv) >= 3 and sys.argv[1] == "paint": paint(sys.argv[2])
    elif len(sys.argv) >= 4 and sys.argv[1] == "pack": pack(sys.argv[2], sys.argv[3])
    else: print(__doc__)
