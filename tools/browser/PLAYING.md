# Playing Praetoria through the driver

The game is already published and served at http://localhost:8112/ (publish and serve it first: see README.md, "Set up").
`tools/browser/driver.js` keeps one browser open on one game so you can play it across many commands.

Start your own driver on YOUR port (run from /home/user/Praetoria-/tools/browser so `playwright` resolves), in the background:

    (nohup node tools/browser/driver.js <PORT> "<Scenario name>" > /tmp/praetoria-driver-<PORT>.log 2>&1 &)

Wait until the log says `driver ready` (up to ~90 s; poll with a loop, a bare `sleep` is blocked:
`T=$(( $(date +%s) + 90 )); until grep -q "driver ready" /tmp/praetoria-driver-<PORT>.log || [ $(date +%s) -ge $T ]; do sleep 3; done`).
Scenario names on the title screen: "The Old Lion", "The New Man", "The Circling Heir", "The Fallen House", "The Risen Officer",
"The Imperial Court". Pass `title` instead of a name to be left on the title screen (to try the new-game options yourself).

Then drive it with curl (all GET unless noted; responses are JSON):

- `curl -s "localhost:PORT/ui"` : the top bar text, which window is in front, and every visible control as
  `{i, w (where), t (text), off (1 if disabled), why (its tooltip: for a disabled button this is usually WHY it is refused)}`.
  `?max=200` for more. Indices are valid until the screen changes; call /ui again after anything changes.
- `curl -s "localhost:PORT/text?n=5000"` : the text of the front window (or `?sel=.map-side` / any CSS selector). This is how you READ a screen. Cheap; use it a lot.
- `curl -s "localhost:PORT/click?i=12"` or `/click?text=senate` (first enabled control whose text starts with that, case-insensitive)
  or `/click?sel=<css>`. Returns what was clicked and the new /ui (`&ui=0` to skip it).
- `curl -s "localhost:PORT/choose?i=7&v=House%20Oriel"` : set a <select>.
- `curl -s "localhost:PORT/select?name=Cinder%20Gate"` : click a star system on the map by its name (opens its panel in `.map-side`).
- `curl -s "localhost:PORT/endturn"` : end the year. Reports/events then appear as the front window; read and answer them.
- `curl -s "localhost:PORT/scroll?sel=.screen.window%20.win-body&y=600"`, `curl -s "localhost:PORT/shot?path=/tmp/shot.png"` (a screenshot; look at it with the Read tool only when the text is not enough, a picture costs far more than text), `curl -s localhost:PORT/errors` (browser console errors), `curl -s -X POST localhost:PORT/eval -d '<js expression>'`.
- `curl -s localhost:PORT/quit` when you are finished. Always quit your driver at the end.

Notes: the left rail opens the windows (OFFICE, MILITARY, GALAXY, YOU, WORLDS, SENATE, HOUSE, EXCHANGE, LAWS, DYNASTY, HOUSES, CHRONICLE);
many windows have tabs (controls with short names near the top). The map is behind the windows; a window has a close button (×).
The right-hand column (outliner) lists reports, alerts, fleets and holdings; clicking one opens it. URL-encode spaces as %20.
