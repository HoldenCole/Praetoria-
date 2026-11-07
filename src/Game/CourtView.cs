using Godot;
using Praetoria.Core.Commands;
using Praetoria.Core.Events;
using Praetoria.Core.Rng;
using Praetoria.Core.State;
using Praetoria.Core.Systems;

namespace Praetoria.Game;

/// <summary>
/// Court mode (BuildSpec §6, GDD §4/§19): three columns — advisors + pools + domain on the left, the briefing
/// feed in the centre, the dynasty rail + crisis clock on the right. Everything shown is read from the
/// World; every click is a command through the bus. Rebuilt wholesale on each change (simple, correct;
/// optimise when it hurts).
/// </summary>
public partial class CourtView : HBoxContainer
{
    private static readonly IRng NoRng = new SplitMix64Rng(0);
    private readonly GameState _state;
    private VBoxContainer _left = null!, _center = null!, _right = null!;

    public CourtView(GameState state) => _state = state;

    public override void _Ready()
    {
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        AddThemeConstantOverride("separation", 10);

        _left = Pane(0.22f);
        _center = Pane(0.52f);
        _right = Pane(0.26f);
        Refresh();
    }

    private VBoxContainer Pane(float stretch)
    {
        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsStretchRatio = stretch
        };
        AddChild(scroll);
        var col = CourtTheme.Column(10);
        scroll.AddChild(col);
        return col;
    }

    public void Refresh()
    {
        var w = _state.World;
        if (w == null || _state.Turn == null) return;
        string era = w.Era;
        CourtTheme.Clear(_left); CourtTheme.Clear(_center); CourtTheme.Clear(_right);
        BuildAdvisors(w, era);
        BuildBriefing(w, era);
        BuildDynasty(w, era);
    }

    // ---------------- left: advisors, pools, domain, progression ----------------

    private void BuildAdvisors(World w, string era)
    {
        var p = w.Protagonist!;
        var house = w.House(p.HouseId)!;
        var accent = CourtTheme.Accent(house.AccentColor, era);

        var card = CourtTheme.MakePanel(CourtTheme.Panel(era));
        var v = CourtTheme.Column(4); card.AddChild(v);
        v.AddChild(CourtTheme.Heading(p.Name, accent, 22));
        v.AddChild(CourtTheme.Muted($"{house.Name} · {_state.Content.Titles.ById(house.Title)?.Name ?? house.Title}", era, 13));
        v.AddChild(CourtTheme.Body($"{_state.RankName(p)} of {_state.Content.Careers.ById(p.CareerTrack)?.Name ?? p.CareerTrack} · age {p.Age} · {(p.Alive ? "" : "DEAD")}", CourtTheme.Ink(era), 13));
        v.AddChild(CourtTheme.Body($"Nature: {string.Join(", ", p.NatureTraits.OrderBy(t => t, StringComparer.Ordinal))}", CourtTheme.Ink(era), 13));
        if (p.AptitudeTraits.Count > 0) v.AddChild(CourtTheme.Body($"Aptitude: {string.Join(", ", p.AptitudeTraits.OrderBy(t => t, StringComparer.Ordinal))}", CourtTheme.Ink(era), 13));
        v.AddChild(CourtTheme.Body($"Ambition: {_state.Content.Traits.Ambitions.FirstOrDefault(a => a.Id == p.Ambition)?.Name ?? p.Ambition}", CourtTheme.Ink(era), 13));
        v.AddChild(CourtTheme.Body($"Stress {p.Stress}/100" + (p.Flags.Contains(DynastySystem.BrokenFlag) ? "  — BROKEN" : ""), p.Stress >= 70 ? CourtTheme.Danger : CourtTheme.Ink(era), 13));
        if (w.HasFlag(DynastySystem.RegencyFlag)) v.AddChild(CourtTheme.Body("A regent rules in the heir's name.", CourtTheme.Danger, 13));
        _left.AddChild(card);

        // pools (GDD §9) — this turn's bandwidth
        var pools = _state.Turn!.PlayerPools;
        var poolPanel = CourtTheme.MakePanel(CourtTheme.PanelAlt(era));
        var pv = CourtTheme.Column(2); poolPanel.AddChild(pv);
        pv.AddChild(CourtTheme.Heading("Action pools", CourtTheme.Gilt(era), 15));
        pv.AddChild(CourtTheme.Body($"Influence {pools.Influence} · Treasury {pools.Treasury} · Agents {pools.Agents}", CourtTheme.Ink(era), 13));
        pv.AddChild(CourtTheme.Muted("Reading is free; acting costs. Pools refill each turn.", era, 11));
        _left.AddChild(poolPanel);

        // the Steward: treasury + holdings + builds (GDD §17)
        var t = house.Treasury;
        var dom = CourtTheme.MakePanel(CourtTheme.PanelAlt(era));
        var dv = CourtTheme.Column(4); dom.AddChild(dv);
        dv.AddChild(CourtTheme.Heading("The Steward — domain", CourtTheme.Gilt(era), 15));
        dv.AddChild(CourtTheme.Body($"Credits {t.Credits} · Materials {t.Materials} · Manpower {t.Manpower}\nInfluence {t.Influence} · Exotics {t.Exotics}", t.Credits < 0 ? CourtTheme.Danger : CourtTheme.Ink(era), 13));
        foreach (var h in w.HoldingsOf(house.Id).OrderBy(h => h.Id, StringComparer.Ordinal))
        {
            var spec = _state.Content.Holdings.Spec(h.Specialization);
            dv.AddChild(CourtTheme.Rule());
            dv.AddChild(CourtTheme.Body($"{h.Name}  [{spec?.Name ?? h.Specialization}]" + (w.Galaxy?.System(h.SystemId) is { } sys ? $" — {sys.Name}" : ""), CourtTheme.Ink(era), 13));
            dv.AddChild(CourtTheme.Muted($"pop {h.Population}{(spec?.MaxPopulation > 0 ? "/" + spec.MaxPopulation : "")} · unrest {h.Unrest} · slots {h.Buildings.Count}/{spec?.Slots ?? 0}" +
                                         (h.Buildings.Count > 0 ? $" · {string.Join(", ", h.Buildings.Select(b => _state.Content.Holdings.Building(b)?.Name ?? b))}" : ""), era, 11));
            foreach (var b in _state.Content.Holdings.Buildings.OrderBy(b => b.Id, StringComparer.Ordinal))
            {
                var cmd = new BuildCommand(p.Id, h.Id, b.Id, _state.Content.Holdings);
                if (!b.FitsSpecialization(h.Specialization) || h.Buildings.Contains(b.Id)) continue;
                bool can = cmd.CanExecute(new CommandContext(w, NoRng));
                var btn = CourtTheme.Action($"Build {b.Name}  ({Cost(b.Cost)})", can,
                    $"yield {Cost(b.Yield)}" + (b.Upkeep.IsEmpty() ? "" : $" · upkeep {Cost(b.Upkeep)}"));
                btn.Pressed += () => _state.Execute(cmd);
                dv.AddChild(btn);
            }
        }
        _left.AddChild(dom);

        // the Chancellor: title, legitimacy, the three rise paths (GDD §13)
        if (_state.Turn.Progression is { } prog)
        {
            var ch = CourtTheme.MakePanel(CourtTheme.PanelAlt(era));
            var cv = CourtTheme.Column(4); ch.AddChild(cv);
            cv.AddChild(CourtTheme.Heading("The Chancellor — standing", CourtTheme.Gilt(era), 15));
            int inst = w.Counter("title_instability");
            cv.AddChild(CourtTheme.Body($"Legitimacy {house.Legitimacy}" + (inst > 0 ? $" — the title outstrips it by {inst}; vassals grumble" : " — the title sits securely"), inst > 0 ? CourtTheme.Danger : CourtTheme.Ink(era), 13));
            if (house.Claims.Count > 0) cv.AddChild(CourtTheme.Muted($"Claims: {string.Join(", ", house.Claims.OrderBy(c => c, StringComparer.Ordinal))}", era));
            var next = prog.NextTitle(w, house.Id);
            if (next != null)
            {
                cv.AddChild(CourtTheme.Muted($"Next rung: {next.Name} (needs legitimacy {next.LegitimacyRequirement})", era));
                Path(cv, new SeizeTitleCommand(p.Id, prog), w, "Seize it by force", "Military — ignores legitimacy; spikes the galaxy's fear");
                Path(cv, new PetitionTitleCommand(p.Id, prog), w, "Petition the throne", "Merit — granted only what you're legitimate for; arrives clean");
                Path(cv, new ClaimTitleCommand(p.Id, prog), w, "Press a claim", "Intrigue — needs a claim; launders legitimacy, leaves a trail");
            }
            cv.AddChild(CourtTheme.Muted($"Spheres: {string.Join(" · ", house.SphereInfluence.OrderBy(k => k.Key, StringComparer.Ordinal).Select(k => $"{k.Key} {k.Value}"))}   threat {w.Counter("threat")}", era, 11));
            _left.AddChild(ch);
        }
    }

    private void Path(VBoxContainer into, ICommand cmd, World w, string label, string tooltip)
    {
        var btn = CourtTheme.Action(label, cmd.CanExecute(new CommandContext(w, NoRng)), tooltip);
        btn.Pressed += () => _state.Execute(cmd);
        into.AddChild(btn);
    }

    private static string Cost(Resources r) =>
        string.Join(", ", Praetoria.Core.State.Resource.All.Where(k => r.Get(k) != 0).Select(k => $"{r.Get(k)} {k}")) is { Length: > 0 } s ? s : "—";

    // ---------------- centre: the briefing feed (GDD §9, §15) ----------------

    private void BuildBriefing(World w, string era)
    {
        var header = CourtTheme.MakePanel(CourtTheme.Panel(era));
        var hv = CourtTheme.Column(2); header.AddChild(hv);
        hv.AddChild(CourtTheme.Heading($"Turn {w.Turn} — the briefing", CourtTheme.Gilt(era), 20));
        hv.AddChild(CourtTheme.Muted(w.Era == "imperium" ? "Under the Imperium." : "The fractured stars; no throne yet claimed.", era));
        _center.AddChild(header);

        if (w.IsDynastyDead)
        {
            var end = CourtTheme.MakePanel(CourtTheme.PanelAlt(era), 16);
            var ev = CourtTheme.Column(4); end.AddChild(ev);
            ev.AddChild(CourtTheme.Heading("THE LINE IS ENDED", CourtTheme.Danger, 22));
            ev.AddChild(CourtTheme.Body("No name-bearer remains. The dynasty is extinguished. The chronicle on the right is its saga.", CourtTheme.Ink(era)));
            _center.AddChild(end);
            return;
        }

        var items = _state.Turn!.Briefing;
        if (items.Count == 0)
            _center.AddChild(CourtTheme.Muted("A quiet season. The advisors have nothing that needs your hand. End the turn when ready.", era, 13));

        foreach (var item in items)
        {
            var fired = item.Fired;
            var text = _state.Content.Text(fired.Def.Id);
            var ctx = new EvalContext(w, fired.Binding, NoRng, _state.Content);
            var card = CourtTheme.MakePanel(item.Resolved ? CourtTheme.Panel(era) : CourtTheme.PanelAlt(era), 14);
            var cv = CourtTheme.Column(6); card.AddChild(cv);

            string title = TextRenderer.Substitute(text?.Title ?? fired.Def.Id, ctx);
            var titleRow = new HBoxContainer();
            titleRow.AddChild(CourtTheme.Heading(title, fired.Def.Tier == EventTier.Setpiece ? CourtTheme.Gilt(era) : CourtTheme.Ink(era), fired.Def.Tier == EventTier.Setpiece ? 20 : 17));
            var tier = CourtTheme.Muted($"   {fired.Def.Tier.ToString().ToUpperInvariant()}", era, 11);
            tier.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
            titleRow.AddChild(tier);
            cv.AddChild(titleRow);

            if (!string.IsNullOrWhiteSpace(text?.Body))
                cv.AddChild(CourtTheme.Body(TextRenderer.Substitute(text!.Body, ctx), CourtTheme.Ink(era), 14));

            if (item.Resolved)
            {
                cv.AddChild(CourtTheme.Muted("Resolved.", era));
            }
            else
            {
                foreach (var oc in _state.Turn.Offer(item))
                {
                    string label = text != null && text.Choices.TryGetValue(oc.Choice.Id, out var ct) ? TextRenderer.Substitute(ct, ctx) : oc.Choice.Id;
                    string cost = oc.Choice.Cost.Count == 0 ? "" : $"  [{string.Join(", ", oc.Choice.Cost.Select(c => $"{c.Value} {c.Key}"))}]";
                    var btn = CourtTheme.Action(label + cost, oc.Available, oc.Available ? null : "Requirements not met, or you cannot afford it this turn.");
                    btn.Alignment = HorizontalAlignment.Left;
                    btn.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                    var captured = item; var choiceId = oc.Choice.Id;
                    btn.Pressed += () => _state.Resolve(captured, choiceId);
                    cv.AddChild(btn);
                }
            }
            _center.AddChild(card);
        }
    }

    // ---------------- right: dynasty rail + crisis clock + chronicle ----------------

    private void BuildDynasty(World w, string era)
    {
        var p = w.Protagonist!;
        var house = w.House(p.HouseId)!;

        var dyn = CourtTheme.MakePanel(CourtTheme.Panel(era));
        var dv = CourtTheme.Column(4); dyn.AddChild(dv);
        dv.AddChild(CourtTheme.Heading(house.Name, CourtTheme.Accent(house.AccentColor, era), 18));
        dv.AddChild(CourtTheme.Muted($"Generation {w.Counter("generation") + 1} · {w.Mode}", era));
        foreach (var id in house.Members)
        {
            var c = w.Char(id);
            if (c == null || c.Id == p.Id) continue;
            var rel = w.GetRelationship(c.Id, p.Id);
            string bond = rel?.Bond is { } b && b != BondType.None ? $" · {b.ToString().ToLowerInvariant()}" : "";
            string disp = rel != null ? $" · {rel.Disposition:+#;-#;0}" : "";
            dv.AddChild(CourtTheme.Body($"{c.Name} — {(c.Alive ? $"{_state.RankName(c)}, {c.Age}" : "dead")}{bond}{disp}", c.Alive ? CourtTheme.Ink(era) : CourtTheme.Muted(era), 12));
        }
        _right.AddChild(dyn);

        // connections outside the house (BuildSpec §7 "connections of X")
        var others = w.LivingCharacters().Where(c => c.HouseId != house.Id).OrderBy(c => c.Id, StringComparer.Ordinal).ToList();
        if (others.Count > 0)
        {
            var con = CourtTheme.MakePanel(CourtTheme.PanelAlt(era));
            var cv = CourtTheme.Column(3); con.AddChild(cv);
            cv.AddChild(CourtTheme.Heading("Connections", CourtTheme.Gilt(era), 15));
            foreach (var c in others)
            {
                var rel = w.GetRelationship(c.Id, p.Id);
                var h = w.House(c.HouseId);
                string flags = c.Flags.Count == 0 ? "" : $"  ({string.Join(", ", c.Flags.Where(f => !f.StartsWith("background:")).OrderBy(f => f, StringComparer.Ordinal))})";
                var row = CourtTheme.Body($"{c.Name} of {h?.Name ?? c.HouseId} — {_state.RankName(c)} · {(rel?.Disposition ?? 0):+#;-#;0}{(rel?.Bond is { } b && b != BondType.None ? $" · {b.ToString().ToLowerInvariant()}" : "")}{flags}",
                    (rel?.Disposition ?? 0) < -20 ? CourtTheme.Danger : CourtTheme.Ink(era), 12);
                cv.AddChild(row);
            }
            _right.AddChild(con);
        }

        // the crisis clock (GDD §16)
        if (_state.Turn!.Crises is { } crises)
        {
            var cc = CourtTheme.MakePanel(CourtTheme.PanelAlt(era));
            var cv = CourtTheme.Column(4); cc.AddChild(cv);
            cv.AddChild(CourtTheme.Heading("The crisis clock", CourtTheme.Gilt(era), 15));
            cv.AddChild(CourtTheme.Muted($"unrest {w.Counter("unrest")} · corruption {w.Counter("corruption")} · goodwill {w.Counter("goodwill")} · coalition pressure {w.Counter("coalition_pressure")}", era, 11));
            if (w.Crises.Count == 0) cv.AddChild(CourtTheme.Body("No crisis is active.", CourtTheme.Ink(era), 12));
            foreach (var active in w.Crises.Values.OrderBy(c => c.Id, StringComparer.Ordinal))
            {
                var def = crises.Def(active.Id);
                cv.AddChild(CourtTheme.Body($"{def?.Name ?? active.Id} — severity {active.Severity}, since turn {active.TurnStarted}{(active.Organic ? "" : $", lit by {w.Char(active.AuthorId)?.Name ?? active.AuthorId}")}", CourtTheme.Danger, 12));
                if (def == null) continue;
                foreach (var d in def.Dampers)
                {
                    var cmd = new ApplyDamperCommand(p.Id, crises, def, d);
                    bool can = cmd.CanExecute(new CommandContext(w, NoRng));
                    var btn = CourtTheme.Action($"{d.Name} (−{d.Relief})" + (d.Cost.Count > 0 ? $"  [{string.Join(", ", d.Cost.Select(c => $"{c.Value} {c.Key}"))}]" : ""), can,
                        can ? null : "Not available — earned by prior play, or unaffordable.");
                    btn.Pressed += () => _state.Execute(cmd);
                    cv.AddChild(btn);
                }
            }
            var causable = crises.Causable(w, NoRng);
            if (causable.Count > 0)
                cv.AddChild(CourtTheme.Muted("Gates cleared — could erupt: " + string.Join(", ", causable.Select(c => c.Name)), era, 11));
            _right.AddChild(cc);
        }

        // the chronicle (GDD §14 — the seed of the saga epilogue)
        var chron = CourtTheme.MakePanel(CourtTheme.Panel(era));
        var hv = CourtTheme.Column(3); chron.AddChild(hv);
        hv.AddChild(CourtTheme.Heading("Chronicle", CourtTheme.Gilt(era), 15));
        foreach (var h in w.History.Skip(Math.Max(0, w.History.Count - 10)).Reverse())
            hv.AddChild(CourtTheme.Body($"T{h.Turn}  {h.Text}", CourtTheme.Muted(era), 11));
        _right.AddChild(chron);
    }
}
