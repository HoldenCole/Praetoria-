# Credits — third-party art and tools in the browser client

Praetoria's code and writing are the project's own. The browser client uses these open-licensed materials:

- **Icons** (`src/Web/Services/Icons.cs`): from [game-icons.net](https://game-icons.net), licensed
  [CC BY 3.0](https://creativecommons.org/licenses/by/3.0/) — by Lorc, Delapouite, Skoll, sbed, Carl Olsen, Willdabeast,
  Caro Asercion, DarkZaitzev, Faithtoken, Lucas, Felbrigg, John Colburn, Zeromancer, Viscious Speed, Guard13007 and others.
- **Fonts**: Cinzel (Natanael Gama), Cormorant Garamond (Christian Thalmann) and IBM Plex Sans (IBM), via Google Fonts, SIL OFL.
- **Portraits, event paintings and the pictures of buildings** (`src/Web/wwwroot/img/portraits/`, `src/Web/wwwroot/img/scenes/`,
  `src/Web/wwwroot/img/works/`): **AI-generated** for this project by `tools/art/portraits.py`, `tools/art/scenes.py` and
  `tools/art/works.py` with the open-weight model
  [Lykon/dreamshaper-8-lcm](https://huggingface.co/Lykon/dreamshaper-8-lcm) (Stable Diffusion 1.5 family,
  CreativeML OpenRAIL-M). No artist's name is used in a prompt. A store page for the game must disclose this.
- Planet sprites, nebulae, the galaxy and the office are drawn procedurally at runtime (`src/Web/wwwroot/js/`), as is the
  fallback portrait shown where no painted one exists; no third-party art.
