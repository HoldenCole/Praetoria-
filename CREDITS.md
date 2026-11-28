# Credits — third-party art and tools in the browser client

Praetoria's code and writing are the project's own. The browser client uses these open-licensed materials:

- **Icons** (`src/Web/Services/Icons.cs`): from [game-icons.net](https://game-icons.net), licensed
  [CC BY 3.0](https://creativecommons.org/licenses/by/3.0/) — by Lorc, Delapouite, Skoll, sbed, Carl Olsen, Willdabeast,
  Caro Asercion, DarkZaitzev, Faithtoken, Lucas, Felbrigg, John Colburn, Zeromancer, Viscious Speed, Guard13007 and others.
  The interface glyphs of docs/VISUAL_SYSTEM.md (the rail, stocks, skills, traits, alerts, map tools) are drawn from the same
  set's raw SVGs (github.com/game-icons/icons): by **Lorc** and **Delapouite**, CC BY 3.0. The marks for helium-3, rare earths
  and isotopes are drawn for the game.
- **Fonts**: Cinzel (Natanael Gama), Alegreya roman and italic (Huerta Tipográfica) and IBM Plex Sans (IBM), via Google Fonts,
  [SIL Open Font License 1.1](https://openfontlicense.org). `src/Web/wwwroot/fonts/IBMPlexSans-var.woff2` is Plex's latin subset
  from Google Fonts, kept beside the client so the digits in Cinzel titles can be drawn in Plex (`app.css`).
- **Textures** (`src/Web/wwwroot/img/tex/`): `steel-brushed.jpg` from ambientCG Metal009 and `leather-dark.jpg` from ambientCG
  Leather026 ([ambientCG](https://ambientcg.com), CC0 1.0), made seamless at 512 pixels, high-passed and tinted for the game.
- **Frames** (`src/Web/wwwroot/img/ui/`): the window, medium and small brass frames are drawn for the game (SVG); the wax seal,
  the arms, the standard, the laurel sprays (halves of Lorc's laurels, CC BY 3.0), globes and pips are drawn in SVG and CSS.
- **Portraits, event paintings, the pictures of buildings and the Office's rooms** (`src/Web/wwwroot/img/portraits/`,
  `src/Web/wwwroot/img/scenes/`, `src/Web/wwwroot/img/works/`, `src/Web/wwwroot/img/rooms/`): **AI-generated** for this project by
  `tools/art/portraits.py`, `tools/art/scenes.py`, `tools/art/works.py` and `tools/art/rooms.py` with the open-weight model
  [Lykon/dreamshaper-8-lcm](https://huggingface.co/Lykon/dreamshaper-8-lcm) (Stable Diffusion 1.5 family,
  CreativeML OpenRAIL-M); the rooms are held to their plan by two ControlNets of the same family and licence
  (lllyasviel/control_v11p_sd15_mlsd and control_v11f1p_sd15_depth). No artist's name is used in a prompt. A store page for the
  game must disclose this.
- Planet sprites, nebulae and the galaxy are drawn procedurally at runtime (`src/Web/wwwroot/js/`), as is the featureless figure
  shown where no painted portrait exists (the drawn cameo faces are gone); no third-party art. The old 3D office
  (`js/office.js`) is kept in the repository, unused, until the owner has seen the painted rooms.
