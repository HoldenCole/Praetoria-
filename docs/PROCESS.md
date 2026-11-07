# Praetoria — Development Cycle

The owner's standing instruction for how this project advances. Every session reads this first and works the
current phase without asking.

```
40 things  →  QA  →  implement graphics  →  improve graphics  →  QA  →  10 small QoL fixes  →  check for bugs  →  reassess  →  repeat
```

- **40 things**: systems, content and depth. "If you see a gap, something that would make the game better or
  deeper, or closer to the design — just do it." Each thing lands with tests and a line in `CHANGELOG.md`.
- **QA**: full test suite, both validators, a 50-turn auto-play soak of every scenario on several seeds, the
  balance simulation (`praetoria simulate`), and a read of the chronicle for absurdities. Fix what it finds.
- **Implement graphics / improve graphics**: the Godot layer (`/src/Game`). Compile-checked in every session; run
  and polished in a session with Godot 4.3 and a display (see `MILESTONE_3_NOTES.md`).
- **10 small QoL fixes**: harness and UI ergonomics, labels, tooltips, defaults, error messages.
- **Check for bugs**: targeted soak with hostile seeds, the determinism and save/load round-trips, a review of
  engine-written flags that content never clears.
- **Reassess**: update `GAP_ANALYSIS.md` and the README's gap table; pick the next 40.

**The bar for V1:** at minimum everything Star Dynasties does; since the simulation is already deeper, V1 should
make Star Dynasties look unfinished. Nothing in V1 may feel like it is building toward an absent finale (GDD §16).

**Phase log**
- 2026-11-07 — "40 things" phase: 31 done (v0.7.0-alpha), 20 more in progress (v0.8.0-alpha). Next: QA.
