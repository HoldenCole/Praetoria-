using Godot;
using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.Presentation;
using Praetoria.Core.Systems;

namespace Praetoria.Game;

/// <summary>
/// Court mode (BuildSpec §6, GDD §4/§19): three columns — advisors + pools + domain + fleet on the left, the
/// briefing feed in the centre, the dynasty rail + crisis clock + chronicle on the right. Renders the
/// Godot-free <see cref="CourtProjection"/> model (unit-tested in Core) and turns clicks into commands through
/// <see cref="GameState"/>. Rebuilt wholesale on each change.
/// </summary>
public partial class CourtView : HBoxContainer
{
    private readonly GameState _state;
    private VBoxContainer _left = null!, _center = null!, _right = null!;

    public CourtView(GameState state) => _state = state;

    public override void _Ready()
    {
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        AddThemeConstantOverride("separation", 10);
        _left = Pane(0.24f);
        _center = Pane(0.50f);
        _right = Pane(0.26f);
        Refresh();
    }

    private VBoxContainer Pane(float stretch)
    {
        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsStretchRatio = stretch
        };
        AddChild(scroll);
        var col = CourtTheme.Column(10);
        scroll.AddChild(col);
        return col;
    }

    public void Refresh()
    {
        if (_state.Turn == null) return;
        var m = CourtProjection.Build(_state.Turn, _state.Content);
        CourtTheme.Clear(_left); CourtTheme.Clear(_center); CourtTheme.Clear(_right);
        BuildLeft(m); BuildCenter(m); BuildRight(m);
    }

    private PanelContainer Panel(string era, bool alt, out VBoxContainer col, int margin = 10)
    {
        var p = CourtTheme.MakePanel(alt ? CourtTheme.PanelAlt(era) : CourtTheme.Panel(era), margin);
        col = CourtTheme.Column(4);
        p.AddChild(col);
        return p;
    }

    private Button Cmd(string label, bool enabled, string? tooltip, ICommand command)
    {
        var b = CourtTheme.Action(label, enabled, string.IsNullOrEmpty(tooltip) ? null : tooltip);
        b.Pressed += () => _state.Execute(command);
        return b;
    }

    private Button Opt(Option o, ICommand command) => Cmd(o.Label, o.Refusal == null, o.Refusal, command);

    /// <summary>A collapsible row of secondary actions so the column stays readable.</summary>
    private static VBoxContainer Fold(VBoxContainer into, string title, string era)
    {
        var toggle = new Button { Text = "▸ " + title, ToggleMode = true, Alignment = HorizontalAlignment.Left, Flat = true };
        toggle.AddThemeFontOverride("font", CourtTheme.Serif);
        var body = CourtTheme.Column(3) ;
        body.Visible = false;
        toggle.Toggled += on => { body.Visible = on; toggle.Text = (on ? "▾ " : "▸ ") + title; };
        into.AddChild(toggle); into.AddChild(body);
        return body;
    }

    // ---------------- left ----------------

    private void BuildLeft(CourtModel m)
    {
        string era = m.Era;
        var accent = CourtTheme.Accent(m.Character.Accent, era);
        var ink = CourtTheme.Ink(era);
        var pid = _state.World!.ProtagonistId;

        _left.AddChild(Panel(era, false, out var v));
        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 10);
        head.AddChild(CourtTheme.Portrait(m.Character.Name, accent, ink));
        var titles = CourtTheme.Column(0);
        titles.AddChild(CourtTheme.Heading(m.Character.Name, accent, 22));
        titles.AddChild(CourtTheme.Muted($"{m.Character.HouseName} · {m.Character.TitleName}", era, 13));
        titles.AddChild(CourtTheme.Body($"{m.Character.RankName} of {m.Character.TrackName} · age {m.Character.Age}{(m.Character.Alive ? "" : " · DEAD")}", ink, 13));
        head.AddChild(titles);
        v.AddChild(head);
        v.AddChild(CourtTheme.Body($"Nature: {string.Join(", ", m.Character.Nature)}", ink, 13));
        if (m.Character.Aptitude.Count > 0) v.AddChild(CourtTheme.Body($"Aptitude: {string.Join(", ", m.Character.Aptitude)}", ink, 13));
        v.AddChild(CourtTheme.Body($"Ambition: {m.Character.AmbitionName}", ink, 13));
        if (m.Character.Background != null) v.AddChild(CourtTheme.Muted($"Origin: {m.Character.Background}", era, 12));
        v.AddChild(CourtTheme.Body($"Stress {m.Character.Stress}/100{(m.Character.Broken ? "  — BROKEN" : "")}", m.Character.Stress >= 70 || m.Character.Broken ? CourtTheme.Danger : ink, 13));
        if (m.Character.Regency) v.AddChild(CourtTheme.Body("A regent rules in the heir's name.", CourtTheme.Danger, 13));

        _left.AddChild(Panel(era, true, out var pv));
        pv.AddChild(CourtTheme.Heading("Action pools", CourtTheme.Gilt(era), 15));
        pv.AddChild(CourtTheme.Body($"Influence {m.Pools.Influence} · Treasury {m.Pools.Treasury} · Agents {m.Pools.Agents}", ink, 13));
        pv.AddChild(CourtTheme.Muted("Reading is free; acting costs. Pools refill each turn.", era, 11));

        _left.AddChild(Panel(era, true, out var dv));
        dv.AddChild(CourtTheme.Heading("The Steward — domain", CourtTheme.Gilt(era), 15));
        var t = m.Treasury;
        dv.AddChild(CourtTheme.Body($"Credits {t.Credits} · Materials {t.Materials} · Manpower {t.Manpower}\nInfluence {t.Influence} · Exotics {t.Exotics}", t.Insolvent ? CourtTheme.Danger : ink, 13));
        foreach (var h in m.Holdings)
        {
            dv.AddChild(CourtTheme.Rule());
            dv.AddChild(CourtTheme.Body($"{h.Name}  [{h.SpecName}]{(h.SystemName != null ? $" — {h.SystemName}" : "")}", ink, 13));
            dv.AddChild(CourtTheme.Muted($"pop {h.Population}{(h.MaxPopulation > 0 ? "/" + h.MaxPopulation : "")} · unrest {h.Unrest} · slots {h.SlotsUsed}/{h.Slots}" +
                                         (h.Buildings.Count > 0 ? $" · {string.Join(", ", h.Buildings)}" : ""), era, 11));
            if (h.Besieged) dv.AddChild(CourtTheme.Body("UNDER BLOCKADE — yield halved", CourtTheme.Danger, 12));
            var builds = Fold(dv, "Build", era);
            foreach (var b in h.Builds)
                builds.AddChild(Cmd(b.Label, b.Available, b.Tooltip, new BuildCommand(pid, h.Id, b.BuildingId, _state.Content.Holdings, _state.Content.Titles)));
            if (h.SystemName != null)
            {
                var yard = Fold(dv, "Shipyard", era);
                foreach (var ship in h.Ships)
                    yard.AddChild(Cmd(ship.Label, ship.Refusal == null, ship.Refusal, new RaiseFleetCommand(pid, h.Id, _state.Content.Fleets, _state.Content.ShipClasses, ship.ClassId, _state.Content.Titles)));
            }
        }
        if (m.Standing is { } st && st.TaxOptions.Count > 0)
        {
            var taxes = Fold(dv, $"Tax policy — {st.TaxPolicy}", era);
            foreach (var o in st.TaxOptions) taxes.AddChild(Opt(o, new SetTaxPolicyCommand(pid, o.Id, _state.Content.Holdings)));
            var ex = Fold(dv, "The Exchange", era);
            foreach (var o in st.ExchangeOptions)
            {
                var rate = _state.Content.Holdings.Rates.First(r => $"{r.Give}>{r.Get}" == o.Id);
                ex.AddChild(Opt(o, new ExchangeCommand(pid, rate, _state.Content.Holdings)));
            }
        }

        _left.AddChild(Panel(era, true, out var av));
        av.AddChild(CourtTheme.Heading("The Admiral — fleets", CourtTheme.Gilt(era), 15));
        if (m.Fleets.Count == 0) av.AddChild(CourtTheme.Muted("The house keeps no ships. Lay down a hull at a holding.", era, 12));
        foreach (var f in m.Fleets)
        {
            av.AddChild(CourtTheme.Body($"{f.Name}{(f.Office ? " (office)" : "")} — {f.Composition}; readiness {f.Readiness}{(f.Veterancy > 0 ? $", veterancy {f.Veterancy}" : "")}, " +
                                        (f.Moving ? $"en route to {f.Destination} ({f.Eta})" : $"at {f.Where}") +
                                        (f.AdmiralName != null ? $" — {f.AdmiralName} commanding" : " — unled (fights at 85%)"), ink, 12));
            var cmdFold = Fold(av, $"Command of {f.Name}", era);
            foreach (var o in f.AdmiralOptions) cmdFold.AddChild(Opt(o, new AssignAdmiralCommand(pid, f.Id, o.Id)));
            foreach (var o in f.MergeOptions) cmdFold.AddChild(Opt(o, new MergeFleetsCommand(pid, o.Id, f.Id)));
        }
        foreach (var siege in m.Sieges)
            av.AddChild(CourtTheme.Body(siege.Ours ? $"⚠ {siege.HoldingName} is blockaded by {siege.BesiegerName} — falls in {siege.Required - siege.Turns} turn(s)"
                                                   : $"Siege of {siege.HoldingName}: {siege.Turns}/{siege.Required} turns", CourtTheme.Danger, 12));
        if (m.Wars.Count > 0)
        {
            av.AddChild(CourtTheme.Rule());
            foreach (var war in m.Wars)
            {
                av.AddChild(CourtTheme.Body($"⚔ At war with {war.EnemyName} since turn {war.Since} — they field ~{war.EnemyHulls} hull(s).{(war.TheyAreSuing ? "  Their envoy awaits your answer in the briefing." : "")}", CourtTheme.Danger, 12));
                av.AddChild(Cmd($"Sue for peace with {war.EnemyName}  (1 influence)", war.PeaceAvailable, war.PeaceTooltip, new SueForPeaceCommand(pid, war.EnemyHouseId, _state.Content.Fleets)));
            }
        }
        av.AddChild(CourtTheme.Muted("Give movement orders — and declare war — from the Galaxy map.", era, 11));

        if (m.Standing is { } s)
        {
            _left.AddChild(Panel(era, true, out var cv));
            cv.AddChild(CourtTheme.Heading("The Chancellor — standing", CourtTheme.Gilt(era), 15));
            cv.AddChild(CourtTheme.Body($"Legitimacy {s.Legitimacy}" + (s.Instability > 0 ? $" — the title outstrips it by {s.Instability}; vassals grumble" : " — the title sits securely"), s.Instability > 0 ? CourtTheme.Danger : ink, 13));
            if (s.Claims.Count > 0) cv.AddChild(CourtTheme.Muted($"Claims: {string.Join(", ", s.Claims)}", era));
            if (s.NextTitle != null)
            {
                cv.AddChild(CourtTheme.Muted($"Next rung: {s.NextTitle} (needs legitimacy {s.NextRequirement})", era));
                var prog = _state.Turn!.Progression!;
                foreach (var path in s.Paths)
                {
                    ICommand cmd = path.Id switch
                    {
                        "seize" => new SeizeTitleCommand(pid, prog),
                        "petition" => new PetitionTitleCommand(pid, prog),
                        _ => new ClaimTitleCommand(pid, prog)
                    };
                    cv.AddChild(Cmd(path.Label, path.Available, path.Tooltip, cmd));
                }
            }
            cv.AddChild(CourtTheme.Muted($"Spheres: {s.Spheres}   threat {s.Threat}", era, 11));
            if (s.Honors.Count > 0) cv.AddChild(CourtTheme.Body("Honors: " + string.Join(", ", s.Honors), CourtTheme.Gilt(era), 12));
            var law = Fold(cv, $"Succession — {s.SuccessionLaw}", era);
            foreach (var o in s.LawOptions) law.AddChild(Opt(o, new SetSuccessionLawCommand(pid, o.Id)));

            var throne = Fold(cv, s.Throne.HoldsThrone ? "The Throne — yours" : "The Throne", era);
            if (s.Throne.HoldsThrone) throne.AddChild(CourtTheme.Body("You hold the Imperium. Hold it.", CourtTheme.Gilt(era), 12));
            else
            {
                if (s.Throne.EmperorName != null) throne.AddChild(CourtTheme.Muted($"{s.Throne.EmperorName} holds the throne.", era, 11));
                throne.AddChild(Cmd("Proclaim the Imperium  (3 influence, 2 agents)", s.Throne.ProclaimRefusal == null, s.Throne.ProclaimRefusal, new ProclaimImperiumCommand(pid, _state.Content.Titles)));
                throne.AddChild(Cmd("Seize the throne  (2 treasury, 2 agents)", s.Throne.SeizeRefusal == null, s.Throne.SeizeRefusal, new SeizeThroneCommand(pid, _state.Content.Titles, _state.Content.Friction)));
            }
        }
    }

    // ---------------- centre ----------------

    private void BuildCenter(CourtModel m)
    {
        string era = m.Era;
        _center.AddChild(Panel(era, false, out var hv));
        hv.AddChild(CourtTheme.Heading($"Turn {m.Turn} — the briefing", CourtTheme.Gilt(era), 20));
        hv.AddChild(CourtTheme.Muted(era == "imperium" ? "Under the Imperium." : "The fractured stars; no throne yet claimed.", era));

        if (m.EpilogueText != null)
        {
            _center.AddChild(Panel(era, true, out var ev, 16));
            ev.AddChild(CourtTheme.Heading(m.DynastyDead ? "THE LINE IS ENDED" : "AN ERA TURNS", m.DynastyDead ? CourtTheme.Danger : CourtTheme.Gilt(era), 22));
            ev.AddChild(CourtTheme.Body(m.EpilogueText, CourtTheme.Ink(era), 13));
            if (m.DynastyDead) return;
        }
        if (m.Quiet)
            _center.AddChild(CourtTheme.Muted("A quiet season. The advisors have nothing that needs your hand. End the turn when ready.", era, 13));

        foreach (var card in m.Briefing)
        {
            _center.AddChild(Panel(era, !card.Resolved, out var cv, 14));
            var titleRow = new HBoxContainer();
            titleRow.AddChild(CourtTheme.Heading(card.Title, card.Setpiece ? CourtTheme.Gilt(era) : CourtTheme.Ink(era), card.Setpiece ? 20 : 17));
            var tier = CourtTheme.Muted($"   {card.Tier}", era, 11);
            tier.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
            titleRow.AddChild(tier);
            cv.AddChild(titleRow);
            if (card.Body.Length > 0) cv.AddChild(CourtTheme.Body(card.Body, CourtTheme.Ink(era), 14));
            if (card.Resolved) { cv.AddChild(CourtTheme.Muted("Resolved.", era)); continue; }
            foreach (var c in card.Choices)
            {
                var btn = CourtTheme.Action(c.Label + (c.Cost.Length > 0 ? $"  [{c.Cost}]" : ""), c.Available,
                    c.Available ? null : "Requirements not met, or you cannot afford it this turn.");
                btn.Alignment = HorizontalAlignment.Left;
                btn.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                btn.AddThemeFontOverride("font", CourtTheme.Serif);
                var item = card.Item; var id = c.Id;
                btn.Pressed += () => _state.Resolve(item, id);
                cv.AddChild(btn);
            }
        }
    }

    // ---------------- right ----------------

    private void BuildRight(CourtModel m)
    {
        string era = m.Era;
        var ink = CourtTheme.Ink(era);
        var pid = _state.World!.ProtagonistId;

        _right.AddChild(Panel(era, false, out var dv));
        dv.AddChild(CourtTheme.Heading(m.HouseName, CourtTheme.Accent(m.HouseAccent, era), 18));
        dv.AddChild(CourtTheme.Muted($"Generation {m.Generation} · {m.Mode}", era));
        if (m.Members.Count == 0) dv.AddChild(CourtTheme.Muted("You are the last of the name.", era, 12));
        foreach (var x in m.Members)
            dv.AddChild(CourtTheme.Body($"{x.Name} — {x.Detail}{(x.Bond.Length > 0 ? $" · {x.Bond}" : "")}{(x.Alive ? $" · {x.Disposition:+#;-#;0}" : "")}", x.Alive ? ink : CourtTheme.Muted(era), 12));
        foreach (var h in m.Household.Where(h => h.Actions.Count > 0 || h.Hidden))
        {
            if (h.Hidden) dv.AddChild(CourtTheme.Muted($"{h.Name} ({h.Age}) is fostered away, hidden.", era, 11));
            foreach (var o in h.Actions)
                dv.AddChild(Opt(o, o.Id == "recall" ? new RecallCommand(pid, h.Id) : new FosterCommand(pid, h.Id)));
        }

        if (m.Connections.Count > 0)
        {
            _right.AddChild(Panel(era, true, out var cv));
            cv.AddChild(CourtTheme.Heading("Connections", CourtTheme.Gilt(era), 15));
            foreach (var c in m.Connections)
            {
                cv.AddChild(CourtTheme.Body($"{c.Name} of {c.HouseName} — {c.Detail} · {c.Disposition:+#;-#;0}{(c.Bond.Length > 0 ? $" · {c.Bond}" : "")}{(c.Flags.Count > 0 ? $"  ({string.Join(", ", c.Flags)})" : "")}",
                    c.Hostile ? CourtTheme.Danger : ink, 12));
                var acts = Fold(cv, "Intrigue", era);
                foreach (var o in c.Actions)
                    acts.AddChild(Opt(o, o.Id.StartsWith("marry:") ? new ProposeMarriageCommand(pid, o.Id["marry:".Length..], c.Id, _state.Content.Titles)
                                                                   : new AssassinateCommand(pid, c.Id, _state.Content.Friction)));
            }
        }

        if (m.Crises is { } clock)
        {
            _right.AddChild(Panel(era, true, out var cv));
            cv.AddChild(CourtTheme.Heading("The crisis clock", CourtTheme.Gilt(era), 15));
            cv.AddChild(CourtTheme.Muted(clock.Counters, era, 11));
            if (clock.Active.Count == 0) cv.AddChild(CourtTheme.Body("No crisis is active.", ink, 12));
            var crises = _state.Turn!.Crises!;
            foreach (var a in clock.Active)
            {
                cv.AddChild(CourtTheme.Body($"{a.Name} — severity {a.Severity}, since turn {a.Since}{(a.Author != null ? $", lit by {a.Author}" : "")}", CourtTheme.Danger, 12));
                var def = crises.Def(a.Id);
                if (def == null) continue;
                foreach (var d in a.Dampers)
                {
                    var damper = def.Dampers.First(x => x.Id == d.DamperId);
                    cv.AddChild(Cmd(d.Label, d.Available, d.Tooltip, new ApplyDamperCommand(pid, crises, def, damper)));
                }
            }
            if (clock.Causable.Count > 0)
                cv.AddChild(CourtTheme.Muted("Gates cleared — could erupt: " + string.Join(", ", clock.Causable), era, 11));
        }

        _right.AddChild(Panel(era, false, out var hv));
        hv.AddChild(CourtTheme.Heading("Chronicle", CourtTheme.Gilt(era), 15));
        foreach (var line in m.Chronicle) hv.AddChild(CourtTheme.Body(line, CourtTheme.Muted(era), 11));
    }
}
