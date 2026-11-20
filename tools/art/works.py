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
    "domed_colony": "a small city of streets, towers and green parks enclosed under one enormous transparent glass dome on a grey airless plain, seen from outside, lit from within",
    "second_world": "a young colony town on a green hilly world, timber and stone houses, fields, a river, a ringed planet in the daytime sky",
    "terrace_farms": "steep green hillsides cut into endless terrace farms, irrigation channels, small farmhouses, two moons in the sky",
    "game_parks": "a wild alien forest and savannah preserve, a stone hunting lodge on a ridge, strange grazing herds far off, golden evening",
    "sea_farms": "floating kelp farms and fish pens on a calm alien ocean, pontoon walkways, harvest barges, low sun",
    "tidal_mills": "a huge sea wall of tidal turbines across a rocky strait, crashing water, industrial piers, stormy sky",
    "deep_harbour": "a great sea harbour where spacecraft land on the water beside stone quays, cranes, lighthouses, evening",
    "sun_fields": "thousands of square mirrored solar panels in long straight rows covering a red desert, bright sun glare on the panels, a control tower",
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
    "prospectors_exchange": "a ramshackle space station bolted onto a floating asteroid in outer space, many small mining spacecraft docked, lit windows, other asteroids drifting",
    "hidden_dock": "a spacecraft slipping into a concealed hangar cut into a dark floating asteroid in outer space, dim red hangar lamps, starfield",
    "rare_earth_diggings": "close view of a steel mining headframe tower and drilling rigs on bright grey asteroid rock with glowing blue crystal veins, floodlights, black starry sky",
    "isotope_pits": "a guarded open pit mine glowing faintly green on a dead world, warning beacons, sealed containers, armed towers",
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
    "minefields": "a cluster of dark metal spheres covered in long spikes, each with a small glowing red lamp, floating weightless against a black starfield, dozens receding into the distance",
    # ---- great works: one to a body ----
    "orbital_ring": "a colossal ring structure encircling an entire planet seen from orbit, city lights along the ring, docks, sunrise over the limb",
    "great_stair": "a space elevator, a single immense cable rising from a city on a planet up through the clouds into orbit, climbing cars, dawn",
    "the_aegis": "a planet seen from space wrapped in a glowing golden hexagonal energy shield, small projector stations around it, beams from enemy starships stopping at the shield",
    "spire_city": "one immense tower city a mile high rising from a plain above the clouds, thousands of lit windows, flying craft, dusk",
    "dyson_lattice": "a gigantic hexagonal metal lattice shell half built around a blazing yellow star in outer space, sunlight streaming through the open hexagons, tiny spacecraft for scale, starfield",
    "sun_foundry": "a dark industrial space station silhouetted against the fiery surface of an enormous sun, pouring a bright stream of molten metal, huge square mirror panels on either side",
    "sky_harbour": "a vast flat floating platform city with landing pads hanging in the sky among orange gas giant cloud towers, large tanker spacecraft landed on it, seen from above",
    "hollowed_world": "the inside of a hollowed-out asteroid city, streets and lights wrapped round the inner cavern, a central sun-lamp",
    "fortress_moon": "an entire moon turned into a fortress, vast gun emplacements and armoured gates across its surface, a planet below",
    "house_mausoleum": "a monumental domed mausoleum on a grey moon, colonnades and statues, eternal flames, a blue planet low in the black sky",
    "gate_anchor": "an immense circular ring gate floating in outer space with a shimmering blue portal of light inside it, fortress towers on the ring, starships passing through, starfield",
    "grand_arsenal": "a colossal space station shipyard in outer space with a dozen grey armoured starships held in skeletal drydock frames, cranes, welding sparks, starfield",
    "long_cylinder": "the interior of a huge rotating cylinder space habitat, fields and towns curving up overhead, a strip of sunlight",
    "lighthouse": "a towering beacon station in deep space sending a great beam of golden light across the stars, ships following it",
    "ring_habitat": "rows of small lit habitat stations strung along the bright icy rings of a gas giant in outer space, the huge banded planet filling the background, starfield",
    "storm_tap": "a space station in orbit lowering a long glowing cable down into a gigantic red swirling storm on a gas giant, lightning in the clouds",
    "breakers_yard": "a huge derelict futuristic space cruiser broken in half in orbit above a planet, glowing cutting lasers slicing its hull, a cloud of metal debris, scaffolding frames, stars",
    "lode_works": "a single enormous metallic asteroid hollowed by mining, glittering tunnels, ore haulers, drilling lights",
    "great_dig": "an archaeological excavation of colossal ancient alien ruins half buried in dust, scaffolds, floodlights, tiny figures",
    "vault_archive": "an endless underground archive of shelves and sealed vaults in a great cavern, warm lamps, catwalks, a deep shaft",
    "undersea_colony": "glowing glass dome cities on a dark sea floor seen underwater, lit windows, submarines with headlights, a ceiling of blue ice high above, shafts of pale light",
    "ice_choir": "a cathedral hall carved into blue glacier ice, light blazing through the walls, a procession of robed pilgrims, mist",
    "garden_estates": "white villas and terraced gardens along a lush green valley, cypress trees, fountains, a lake, warm afternoon light",
    "old_capital_restored": "an ancient ruined palace city being restored, vast domes and colonnades, scaffolding, banners, golden hour",
    "reef_cities": "towns grown into colossal coral reefs rising from a turquoise alien sea, bridges between reef towers, boats",
    "rift_citadel": "a fortress wall and citadel built across the mouth of an immense canyon, towers on both cliffs, a single great gate",
    "mantle_forge": "a colossal foundry sunk into a glowing chasm of magma on a volcanic world, rivers of molten metal, gantries",
    "moon_port": "a spaceport city of landing towers and domes on the grey surface of a moon, spacecraft landing, an enormous blue planet filling the black sky above",
    "hollow_arsenal": "a secret fleet dock inside a hollow moon, warships berthed in a vast cavern, armoured doors opening to stars",
    "comet_watch": "a small gilded chapel riding an icy comet with a brilliant tail, lamps burning, the sun far off, deep stars",
    "flare_harvesters": "huge thin golden collector sails spread in outer space in front of a blazing star throwing out a great arc of solar flare, small harvester spacecraft, starfield",
    "twin_crown": "a binary star system, a big orange sun and a small blue-white sun close together, each enclosed in a thin metallic ring megastructure, black space, stars",
    # ---- infrastructure: the system's own ----
    "civil_works": "aqueducts, power pylons and paved roads crossing a plain to a growing colony city, construction cranes, morning",
    "habitats": "rings of sealed habitat blocks and greenhouse domes under construction, air towers, workers small in the distance",
    "orbital_traffic": "many small spacecraft, tugs and cargo lifters flying in neat lines in orbit above the curve of a blue planet, a traffic control space station with lights",
    "outworks": "a small supply depot space station and survey spacecraft in outer space between two distant planets, cargo pods, a small bright sun, starfield",
    "defence_grid": "a grid of armed picket satellites in outer space linked by thin beams of blue light, a planet and a moon in the distance, a patrol spacecraft, starfield",
    # ---- the buildings of a system's own world ----
    "power_plant": "a fusion power plant with great cooling towers and humming conduits on the edge of a colony city, night, glow",
    "mine": "the headframe and lit shaft mouth of a deep mine in a rocky industrial landscape, ore trains, smoke, dusk",
    "farm_complex": "long glasshouse hydroponic farms glowing green at night on an alien plain, pipes and tanks, a moon above",
    "market": "a bustling open market square in a colony city, awnings, stalls and crowds far off, columns, banners, warm light",
    "helium_skimmers": "skimmer craft rising from a refinery platform toward a gas giant in the sky, fuel tanks, flares",
    "rare_earth_refinery": "a chemical refinery of vats and towers processing glittering ore, coloured smoke, pipes, floodlights",
    "isotope_works": "a heavily shielded industrial works with thick concrete domes and warning lamps, guarded gates, night",
    "sensor_array": "a row of huge white radar dishes on a high mountain plateau pointed at a starry night sky, control bunkers with lit windows, faint aurora",
    "arsenal": "a vast arsenal hall of stacked munitions and artillery, armoured doors, soldiers small in the distance, brazier light",
    "supply_depot": "a military supply base on a plain at night, rows of fuel tanks and stacked crates, landing pads with transport spacecraft loading under floodlights",
    "deep_drill": "a colossal drilling rig boring into the crust of a rocky world, a pillar of dust and fire, gantries, night",
    "orbital_foundry": "an orbital foundry station pouring glowing metal into moulds in zero gravity, a starship frame beside it",
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
        pipe(prompt=WORKS[k] + ", " + STYLE, negative_prompt=NEG + (NEG_SPACE if k in SPACE else ""), num_inference_steps=6, guidance_scale=1.5, width=576, height=352, generator=g).images[0].save(os.path.join(work, k + ".png"))
        print(k, f"{time.time() - t:.0f}s", flush=True)

def pack(work, out, width=432, quality=80):
    from PIL import Image
    os.makedirs(out, exist_ok=True); done = []
    for k in WORKS:
        p = os.path.join(work, k + ".png")
        if not os.path.exists(p): continue
        im = Image.open(p).convert("RGB"); w, h = im.size; trim = int(w * 0.03)   # a painted-in border, where there is one, is at the edge
        im = im.crop((trim, trim, w - trim, h - trim)); im = im.resize((width, round(width * im.size[1] / im.size[0])), Image.LANCZOS)
        im.save(os.path.join(out, k + ".webp"), "WEBP", quality=quality, method=6); done.append(k)
    json.dump({"works": sorted(done)}, open(os.path.join(out, "manifest.json"), "w"), indent=0)
    print(f"{len(done)} of {len(WORKS)} pictures -> {out}")

if __name__ == "__main__":
    if len(sys.argv) >= 3 and sys.argv[1] == "paint": paint(sys.argv[2])
    elif len(sys.argv) >= 4 and sys.argv[1] == "pack": pack(sys.argv[2], sys.argv[3])
    else: print(__doc__)
