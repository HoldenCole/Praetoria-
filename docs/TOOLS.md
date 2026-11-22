# Open tools and repositories worth using

What the owner asked (2026-11-22): *"search for more open source tools and repos. Anything from game design, art,
graphics, content creation."* The last search of this kind found the diffusion pipeline that paints the portraits
(docs/ART_DIRECTION.md, "Portraits").

How this list was made: web searches by area, then each repository's licence, stars and date of last push read from
GitHub on 2026-11-22. A licence marked *(reported)* comes from a model card or an article quoted in a search result and
was not read at the source. Nothing here has been installed or run yet. Nothing here costs money.

The game is a C# core, a Blazor client in the browser, a Three.js map, JSON content, and a Python art pipeline that
runs on a CPU. Each entry says what it would do for that.

## The six I would take first

Ranked by how much closer each brings the game to "it has to look like a game off Steam" for the work it costs.

| | What | Licence | What it gives the game | Cost |
|---|---|---|---|---|
| 1 | **Sound.** Kenney's *UI Audio* and *Interface Sounds* packs for clicks and confirmations; a score either made locally with **ACE-Step 1.5** or taken from **Scott Buckley**'s library | Kenney: CC0. ACE-Step 1.5: MIT (15,000 stars, pushed this month). Scott Buckley: CC BY 4.0, credit required, remixes excluded | The game is silent (ROADMAP "No sound", the one line on the Options window that says so). A click, a seal, a vote called, and four or five pieces of music by mood | Small for clicks (the browser plays them with no library). Music is a choice for the owner: generated, or a credited composer |
| 2 | **Icons and crests.** **game-icons.net** for goods, buildings, offices and alerts; **Armoria** for a coat of arms a house | game-icons: CC BY 3.0, 4,133 icons, credit required. Armoria: code MIT by its README; its simple charges CC0, its complex charges CC BY-NC | Goods and offices are words or a letter today. A house is a colour. Each house gets arms generated from its id, kept for the saga, shown on the map, the Senate floor and the profile | Medium. Armoria must be used with the CC0 charges only: the complex ones are non-commercial |
| 3 | **Floating UI** | MIT (32,800 stars) | Tooltips that can hold a tooltip, placed so they never leave the screen: the pattern CK3 and Victoria 3 lean on. "Tooltips are the browser's own" is an open line in ART_DIRECTION | Small to medium: one script from the CDN the map already uses, and `tips.js` rewritten on it |
| 4 | **Vale** | MIT (6,400 stars) | A linter for prose with rules of our own. Every event text, lecture and document checked in CI for the words and marks the house style forbids, before a person reads it | Small: one binary in CI and a rules folder |
| 5 | **FastSD CPU** (OpenVINO) or **stable-diffusion.cpp** | MIT, MIT (both pushed this week) | The painter runs at half a minute to a minute a picture here. FastSD reports about one to three seconds on a desktop processor *(reported, on a Core i7)*. That turns "paint 400 pictures overnight" into "repaint the library when the style changes" | Medium: a second backend for `tools/art/*.py`, and the pictures compared side by side before anything is replaced |
| 6 | **CsCheck** and **Verify** | Apache-2.0, MIT | CsCheck throws random sequences of commands at a world and, when one breaks it, shrinks the sequence to the shortest that still does. Verify keeps a projection or a chronicle as a file and fails when it changes. Both suit a game tested by soak | Small each |

## By area

### Sound and music

| Tool | Licence | Use | Note |
|---|---|---|---|
| Kenney UI Audio, Interface Sounds | CC0 | Clicks, toggles, confirmations | About fifty files a pack. No credit needed |
| OpenGameArt "Dark Sci-Fi Audio Pack" | CC0 | Five ambient loops | Check each file's page |
| ACE-Step 1.5 | MIT | Whole pieces from a description, made locally | Under 4 GB of graphics memory for the small model *(reported)*. Not tried on a CPU |
| YuE | Apache-2.0 (13,700 stars) | The same, heavier | |
| MOSS-SoundEffect v2 | Apache-2.0 *(reported)* | Effects from a description: a hatch, a crowd, a gavel | Released May 2026. Unverified |
| jsfxr | Unlicense | Synthesised blips, in the browser | Old and small. Good for alerts |
| Tone.js | MIT (14,900 stars) | Music that follows the game: a drone that darkens in war | A real piece of work to compose |
| Howler.js | MIT (25,500 stars) | Playing files | Probably not needed: Web Audio alone will do |
| Scott Buckley | CC BY 4.0 | Finished orchestral pieces by a person | Credit in the game and on the store page |

Do not use: **MusicGen** (its weights are CC BY-NC) and **MMAudio**'s weights (CC BY-NC).

### Painting

| Tool | Licence | Use | Note |
|---|---|---|---|
| FastSD CPU | MIT | Faster painting on a CPU | See the six above |
| stable-diffusion.cpp | MIT (8,100 stars) | Runs newer models (Z-Image, FLUX) on a CPU from one binary | Breadth, where FastSD is speed |
| FLUX.2 klein 4B | Apache-2.0 *(reported from its model card)* | Edits a picture from reference pictures: the same face at five ages from one painted face, not from a seed and luck | About 13 GB of graphics memory. The 9B model is **non-commercial** |
| Z-Image-Turbo | Apache-2.0 *(reported)* | A better base painter than the one in use | |
| ai-toolkit | MIT (12,700 stars) | Train a small style model on our own best pictures, so portraits, scenes and works look like one hand | Needs a graphics card |
| Real-ESRGAN | BSD-3 | Enlarge a picture cleanly | For the title screen and store art |
| rembg | MIT (25,600 stars) | Cut a figure from its background | Portraits over a house's colours |
| IC-Light | Apache-2.0 | Relight a picture | One light across a mixed library |

Do not use: **IP-Adapter FaceID** and **InstantID** for keeping a face. Their code is open but both rest on InsightFace's
face models, which are for research only. **AuraFace** is the encoder made to replace it for commercial work; read its
card before relying on it.

### The map and the close map

| Tool | Licence | Use |
|---|---|---|
| pmndrs/postprocessing | Zlib (3,000 stars) | Better bloom, depth of field and tone mapping than the stock passes |
| troika-three-text | MIT (2,000 stars) | Sharp text drawn in the scene itself. Labels would sort behind planets and fade with distance, which CSS labels cannot do |
| three-geospatial | MIT (1,800 stars) | Atmosphere and clouds for the view of one world |
| wwwtyro/space-3d | Unlicense | Generates a nebula and star backdrop as six images. Dormant since 2024 and still the standard |
| zjoooooo/galaxy-explorer | MIT | A procedural galaxy in Three.js with no build step: to read for its star field |
| EepyBerry/lagrange | unclear | A planet builder in Three.js with rings and biomes: to read. Its licence is not a standard one |
| Quaternius, Kenney Space Kit | CC0 | Low-polygon ships and stations in glTF, for fleets in the close map |
| TRELLIS.2 | MIT (13,200 stars) | A 3D model from one picture | 

TRELLIS.2 needs a 24 GB graphics card. It is not for this machine.

### Words

| Tool | Licence | Use | Note |
|---|---|---|---|
| Vale | MIT | See the six above | |
| ink | MIT (5,000 stars, C# runtime) | Events that run for several steps with memory of what was said | Our events are one screen each. This is the tool for a trial, a negotiation, a seduction |
| Yarn Spinner | MIT core, in C# | The same, lighter | |
| Tracery | Apache-2.0 | A grammar that writes a line a hundred ways | JavaScript, dormant since 2024, small enough to port to C# in a day. For chronicle lines that repeat |
| Kokoro | Apache-2.0 (10,300 stars) | A voice, on a CPU, 54 preset voices | The year in brief read aloud, or the lectures. Recorded once at build, not spoken live |

### Testing

| Tool | Licence | Use |
|---|---|---|
| CsCheck, Verify | Apache-2.0, MIT | See the six above |
| bUnit | MIT (1,300 stars) | Tests a Blazor window without a browser: seconds, not a minute |
| Stryker.NET | Apache-2.0 (2,100 stars) | Changes the code on purpose and reports which changes no test noticed |
| pixelmatch | ISC (7,000 stars) | Compares two screenshots. With the scripts in `tools/browser` it would catch a window that moved |
| MemoryPack | MIT (4,800 stars) | A binary save. A save takes 0.6 seconds in the browser as JSON |

MemoryPack touches every class in the world's state. Large, and only worth it if saving stays slow.

### Games to read

Read for how they solved a problem. Code under the GPL cannot be copied into this game unless this game becomes GPL.

| Game | Licence | What to read it for |
|---|---|---|
| Unciv | MPL-2.0 (11,600 stars) | A whole 4X made by few hands: its AI turn, its rulesets as data |
| FreeOrion | GPL for code, by its own documents; GitHub detects none | A space 4X: governments and policies, supply ranges, AI in Python |
| Project Alice | GPL-3.0 | Victoria 2 rebuilt: an economy with goods, pops and prices that is fast |
| OpenVic | MIT by GitHub's reading; other sources say GPL-3.0 | The same, on Godot. Check before taking anything |
| Pioneer | not detected | A procedural galaxy and its systems |

### Lists to go back to

- **ellisonleao/magictools** (MIT, 17,900 stars): the standing list of game development tools.
- **Roobyx/awesome-game-design** (CC BY 4.0): design reading, by subject.
- **michelpereira/awesome-open-source-games** (CC0): open games by genre.
- **Donchitos/Claude-Code-Game-Studios** (MIT, 40,600 stars): about fifty agent roles and seventy workflows for
  building a game with Claude Code. Worth reading for its checklists (a UX review, a scope check, an asset audit). I
  would take single checklists from it after reading them, and not install it whole: it is somebody else's
  instructions running in our sessions.

## Looked at and left

- **Machinations-style economy modellers** (Monix, Game-Economy-Simulator): small and new. The `simulate`, `roads` and
  `upstart` tools already measure this game's own economy with its own rules.
- **Hunyuan3D**: a community licence with a cap on users and regions excluded.
- **Chatterbox** (voice): reported retired in August 2026.
- **Piper** (voice): the MIT repository is archived; its successor is GPL-3.0.
- **Spacescape**: no licence file, untouched since 2018.

## What needs the owner's answer

- **Music by a model or by a person?** Both are free. Generated music must be declared as AI-made on a store page, as
  the pictures already are. A credited composer needs a line in the credits.
- **Credit lines.** game-icons.net and Scott Buckley both require one. The game has no credits screen yet.
- **A graphics card.** Three of the best finds (FLUX.2 klein for one face at five ages, ai-toolkit for one style,
  TRELLIS.2 for ships) do not run on this machine. If you have a card at home they could be run there.

## Sources

Search results read on 2026-11-22:
[ACE-Step](https://huggingface.co/ACE-Step/ACE-Step-v1-3.5B),
[open music models compared](https://boppy.me/blog/best-open-source-ai-music-models),
[open image models](https://www.bentoml.com/blog/a-guide-to-open-source-image-generation-models),
[FLUX.2 klein 4B](https://huggingface.co/black-forest-labs/FLUX.2-klein-4B),
[image model licences](https://invideo.io/blog/open-source-image-models-licenses/),
[IP-Adapter FaceID licence](https://huggingface.co/h94/IP-Adapter-FaceID/discussions/18),
[InstantID licence](https://huggingface.co/InstantX/InstantID/discussions/2),
[AuraFace](https://huggingface.co/blog/isidentical/auraface),
[FastSD CPU](https://github.com/rupeshs/fastsdcpu),
[painting without a graphics card](https://localaimaster.com/blog/image-generation-without-gpu),
[Armoria](https://github.com/azgaar/armoria),
[Armoria API](https://github.com/azgaar/armoria-api),
[game-icons.net](https://game-icons.net/about.html),
[game-icons on Iconify](https://icon-sets.iconify.design/game-icons/),
[Kenney UI Audio](https://www.kenney.nl/assets/ui-audio),
[Dark Sci-Fi Audio Pack](https://opengameart.org/content/dark-sci-fi-audio-pack),
[Scott Buckley, using this music](https://www.scottbuckley.com.au/library/using-this-music/),
[MOSS-SoundEffect](https://studio.aifilms.ai/blog/moss-soundeffect-v2-open-source-sound-design),
[open voice models](https://www.bentoml.com/blog/exploring-the-world-of-open-source-text-to-speech-models),
[local voice models](https://localaimaster.com/blog/best-local-tts-models),
[Floating UI](https://floating-ui.com),
[Yarn Spinner licence](https://yarnspinner.dev/license/),
[lagrange](https://github.com/EepyBerry/lagrange),
[Quaternius spaceships](https://quaternius.itch.io/lowpoly-spaceships),
[Kenney Space Kit](https://kenney-assets.itch.io/space-kit),
[TRELLIS.2](https://supavoxel.com/models/trellis-2),
[image to 3D compared](https://triposr.org/blog/hunyuan3d-vs-trellis),
[Project Alice](https://schombert.github.io/Project-Alice),
[OpenVic](https://github.com/OpenVicProject/OpenVic),
[Claude Code Game Studios](https://martianlee.github.io/posts/2026-04-18-claude-code-game-studios-architecture).
