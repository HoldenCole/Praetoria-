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
    "domed_colony": "a town under a huge glass dome on a barren alien plain, lit streets and trees inside, dark sky with stars",
    "second_world": "a young colony town on a green hilly world, timber and stone houses, fields, a river, a ringed planet in the daytime sky",
    "terrace_farms": "steep green hillsides cut into endless terrace farms, irrigation channels, small farmhouses, two moons in the sky",
    "game_parks": "a wild alien forest and savannah preserve, a stone hunting lodge on a ridge, strange grazing herds far off, golden evening",
    "sea_farms": "floating kelp farms and fish pens on a calm alien ocean, pontoon walkways, harvest barges, low sun",
    "tidal_mills": "a huge sea wall of tidal turbines across a rocky strait, crashing water, industrial piers, stormy sky",
    "deep_harbour": "a great sea harbour where spacecraft land on the water beside stone quays, cranes, lighthouses, evening",
    "sun_fields": "endless fields of mirrored solar collectors across a red desert, a cloudless bright sky, a small control tower",
    "caravanserai": "a walled desert caravanserai with a market courtyard, awnings, wells, landed cargo haulers, dunes and red rock",
    "strip_mine": "a vast open-cast strip mine on a dead rocky world, terraces, giant excavators, ore conveyors, dust, black sky",
    "smelter_row": "a mile-long row of blast furnaces glowing orange on a volcanic world, molten metal, smoke stacks, slag heaps",
    "geothermal_tap": "geothermal power towers over a field of lava fissures, steam plumes, heavy pipes, a volcanic night",
    "ice_works": "an ice quarry on a frozen world, great blocks cut by machines, floodlights, a freight lifter, blue ice cliffs",
    "cold_granaries": "rows of frost-covered vault doors cut into a glacier wall, a snowy loading yard, sledges of sacks, cold blue light",
    "penal_colony": "a bleak prison camp on an icy barren plain, watchtowers, low barracks, a fence, searchlights, a mine entrance",
    "proving_ground": "a military proving ground on a cratered dead plain, tanks and troops on manoeuvres far off, shell bursts, bunkers, dust",
    "skimmer_fleet": "scoop ships diving through the golden clouds of a gas giant, trailing vapour, a tanker waiting above, vast cloud bands",
    "cloud_habitat": "a floating city of domes and balloons drifting among the towering clouds of a gas giant, sunset light",
    "aerostat_refinery": "industrial refinery towers hanging under giant balloons in the sky of a gas giant, pipes, flares, orange clouds",
    "belt_foundry": "a factory station built among drifting asteroids, a rock being fed into glowing furnaces, sparks, tug ships",
    "prospectors_exchange": "a ramshackle trading station bolted to an asteroid, docked mining ships, lit windows, signal lamps, drifting rocks",
    "hidden_dock": "a secret dock hidden inside a hollow in a dark asteroid, a smuggler ship slipping in, dim red lamps, shadows",
    "rare_earth_diggings": "narrow mine shafts and headframes on an asteroid surface, glittering ore veins, drilling rigs, floodlights",
    "isotope_pits": "a guarded open pit mine glowing faintly green on a dead world, warning beacons, sealed containers, armed towers",
    "mass_driver": "a miles-long electromagnetic rail launcher on a moon firing an ore canister into space, a planet on the horizon",
    "moon_fort": "gun batteries and bunkers dug into the cratered face of a moon, barrels pointing at space, a blue planet above",
    "hermitage": "a tiny stone chapel and a few cells on an icy comet, a single lamp in a window, a glowing comet tail across the stars",
    "observatory": "a great telescope dome on a dark icy ridge under a blazing field of stars and a nebula, a few lit windows",
    "listening_post": "a field of large radio dishes on a barren moon pointing at the stars, a small bunker, cold light",
    "orbital_batteries": "armoured gun platforms in high orbit above a planet, long cannon barrels, running lights, the curve of the world below",
    "fleet_anchorage": "a naval anchorage in space, warships moored in a row at a long docking spine, fuel tenders, a gas giant behind",
    "waystation": "a wheel-shaped space station turning in open space with merchant ships docked round its rim, distant sun, stars",
    "customs_cordon": "small patrol cutters stopping a line of cargo ships at a checkpoint beacon in open space, searchlights, stars",
    "relay_beacon": "a tall lonely signal relay tower in deep space with a great lit antenna array, pulses of light, vast starfield",
    "patrol_base": "a small armoured military station in deep space with corvettes docked and one leaving, running lights, dark void",
    "minefields": "a field of spiked space mines drifting in the dark with dim red warning lights, a distant sun, a wrecked hull",
    # ---- great works: one to a body ----
    "orbital_ring": "a colossal ring structure encircling an entire planet seen from orbit, city lights along the ring, docks, sunrise over the limb",
    "great_stair": "a space elevator, a single immense cable rising from a city on a planet up through the clouds into orbit, climbing cars, dawn",
    "the_aegis": "a shimmering golden energy shield dome covering a whole planet seen from orbit, projector stations, a fleet held off",
    "spire_city": "one immense tower city a mile high rising from a plain above the clouds, thousands of lit windows, flying craft, dusk",
    "dyson_lattice": "a vast geometric lattice of collectors partly enclosing a blazing sun, tiny ships for scale, golden light through the gaps",
    "sun_foundry": "gigantic mirrors focusing sunlight onto a glowing foundry station close to a huge sun, streams of molten metal",
    "sky_harbour": "an enormous floating port riding the high clouds of a gas giant, tankers and warships docked, storm bands below",
    "hollowed_world": "the inside of a hollowed-out asteroid city, streets and lights wrapped round the inner cavern, a central sun-lamp",
    "fortress_moon": "an entire moon turned into a fortress, vast gun emplacements and armoured gates across its surface, a planet below",
    "house_mausoleum": "a monumental domed mausoleum on a grey moon, colonnades and statues, eternal flames, a blue planet low in the black sky",
    "gate_anchor": "a colossal ring gate hanging in deep space with a glowing lane of light through it, fortress towers round it, ships passing",
    "grand_arsenal": "an immense naval arsenal station in deep space, dozens of warships in drydock cradles, cranes, sparks, magazines",
    "long_cylinder": "the interior of a huge rotating cylinder space habitat, fields and towns curving up overhead, a strip of sunlight",
    "lighthouse": "a towering beacon station in deep space sending a great beam of golden light across the stars, ships following it",
    "ring_habitat": "towns strung like beads along the vast icy rings of a gas giant, the banded planet filling half the sky",
    "storm_tap": "a tether station lowering a long cable into the swirling eye of a giant red storm on a gas giant, lightning",
    "breakers_yard": "a ship-breaking yard among the wrecks of old warships in an asteroid field, cutting torches, hulls being dismantled",
    "lode_works": "a single enormous metallic asteroid hollowed by mining, glittering tunnels, ore haulers, drilling lights",
    "great_dig": "an archaeological excavation of colossal ancient alien ruins half buried in dust, scaffolds, floodlights, tiny figures",
    "vault_archive": "an endless underground archive of shelves and sealed vaults in a great cavern, warm lamps, catwalks, a deep shaft",
    "undersea_colony": "glowing dome cities on the floor of a black ocean under a ceiling of ice, submarines, shafts of pale light",
    "ice_choir": "a cathedral hall carved into blue glacier ice, light blazing through the walls, a procession of robed pilgrims, mist",
    "garden_estates": "white villas and terraced gardens along a lush green valley, cypress trees, fountains, a lake, warm afternoon light",
    "old_capital_restored": "an ancient ruined palace city being restored, vast domes and colonnades, scaffolding, banners, golden hour",
    "reef_cities": "towns grown into colossal coral reefs rising from a turquoise alien sea, bridges between reef towers, boats",
    "rift_citadel": "a fortress wall and citadel built across the mouth of an immense canyon, towers on both cliffs, a single great gate",
    "mantle_forge": "a colossal foundry sunk into a glowing chasm of magma on a volcanic world, rivers of molten metal, gantries",
    "moon_port": "a great harbour city on the near face of a moon, docking towers and customs houses, a huge planet hanging above",
    "hollow_arsenal": "a secret fleet dock inside a hollow moon, warships berthed in a vast cavern, armoured doors opening to stars",
    "comet_watch": "a small gilded chapel riding an icy comet with a brilliant tail, lamps burning, the sun far off, deep stars",
    "flare_harvesters": "vast gossamer collector nets catching a blazing solar flare beside a turbulent sun, harvester ships",
    "twin_crown": "two suns, each ringed by collector stations, joined by a brilliant beam of light across the dark, a distant planet",
    # ---- infrastructure: the system's own ----
    "civil_works": "aqueducts, power pylons and paved roads crossing a plain to a growing colony city, construction cranes, morning",
    "habitats": "rings of sealed habitat blocks and greenhouse domes under construction, air towers, workers small in the distance",
    "orbital_traffic": "busy orbital traffic above a planet, tugs, cargo lifters and pilot boats in lanes, a control tower station",
    "outworks": "supply ships and survey craft crossing between distant worlds of a star system, a supply cache station, small bright sun",
    "defence_grid": "a net of picket satellites and sensor stations spread between planets, faint lines of light linking them, a patrol ship",
    # ---- the buildings of a system's own world ----
    "power_plant": "a fusion power plant with great cooling towers and humming conduits on the edge of a colony city, night, glow",
    "mine": "the headframe and lit shaft mouth of a deep mine in a rocky industrial landscape, ore trains, smoke, dusk",
    "farm_complex": "long glasshouse hydroponic farms glowing green at night on an alien plain, pipes and tanks, a moon above",
    "market": "a bustling open market square in a colony city, awnings, stalls and crowds far off, columns, banners, warm light",
    "helium_skimmers": "skimmer craft rising from a refinery platform toward a gas giant in the sky, fuel tanks, flares",
    "rare_earth_refinery": "a chemical refinery of vats and towers processing glittering ore, coloured smoke, pipes, floodlights",
    "isotope_works": "a heavily shielded industrial works with thick concrete domes and warning lamps, guarded gates, night",
    "sensor_array": "a huge array of sensor dishes on a mountain plateau scanning a starry sky, control bunkers, aurora",
    "arsenal": "a vast arsenal hall of stacked munitions and artillery, armoured doors, soldiers small in the distance, brazier light",
    "supply_depot": "a fleet supply depot of fuel tanks, crates and landing pads with transports loading, floodlights, a warship overhead",
    "deep_drill": "a colossal drilling rig boring into the crust of a rocky world, a pillar of dust and fire, gantries, night",
    "orbital_foundry": "an orbital foundry station pouring glowing metal into moulds in zero gravity, a starship frame beside it",
    "conscription_hall": "a stern marble muster hall with ranks of recruits drawn up before it, banners, officers on the steps, dawn",
    "bastion_guns": "enormous planetary defence cannons on a mountain fortress aimed at the sky, bunkers, searchlights, storm clouds",
    "exotics_lab": "a laboratory of glass vessels holding glowing strange matter, brass instruments, scholars far off, violet light",
    "free_port": "a crowded free port, ships of every kind landed along the quays, warehouses, cranes, flags, lanterns, dusk",
    "patronage_office": "a marble office hall where petitioners wait in a long colonnade before a great desk, clerks, lamplight",
    "granary_relief": "great stone public granaries with grain being handed out to a crowd in a forum, carts, columns, morning light",
}
RETHROWN = {}

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
        pipe(prompt=WORKS[k] + ", " + STYLE, negative_prompt=NEG, num_inference_steps=6, guidance_scale=1.5, width=576, height=352, generator=g).images[0].save(os.path.join(work, k + ".png"))
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
