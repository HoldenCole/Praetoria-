using Godot;
using Praetoria.Core.Commands;
using Praetoria.Core.Galaxy;
using Praetoria.Core.State;
using Praetoria.Core.Systems;

namespace Praetoria.Game;

/// <summary>
/// Galaxy mode (BuildSpec §6, GDD §19): the command-table star map — a 2D node-and-lane chart at curated
/// scale, territory shaded by house accent, the player's systems ringed, contested systems marked. Click a
/// system for its panel: owner, holdings, lanes, distance from home. Draws from <see cref="GalaxyProjection"/>
/// (Godot-free, tested); fleets and fogged intel arrive with the military layer and will draw on top.
/// </summary>
public partial class GalaxyView : HBoxContainer
{
    private readonly GameState _state;
    private GalaxyMap _map = null!;
    private VBoxContainer _panel = null!;

    public GalaxyView(GameState state) => _state = state;

    public override void _Ready()
    {
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        AddThemeConstantOverride("separation", 10);

        _map = new GalaxyMap(_state) { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsStretchRatio = 0.72f };
        _map.Selected += _ => RefreshPanel();
        AddChild(_map);

        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsStretchRatio = 0.28f };
        AddChild(scroll);
        _panel = CourtTheme.Column(8);
        scroll.AddChild(_panel);
        Refresh();
    }

    public void Refresh()
    {
        _map.Rebuild();
        RefreshPanel();
    }

    private void RefreshPanel()
    {
        CourtTheme.Clear(_panel);
        var w = _state.World;
        if (w == null) return;
        string era = w.Era;

        if (w.Galaxy == null)
        {
            _panel.AddChild(CourtTheme.Body("This scenario has no star map.", CourtTheme.Ink(era)));
            return;
        }

        // legend
        var legend = CourtTheme.MakePanel(CourtTheme.Panel(era));
        var lv = CourtTheme.Column(3); legend.AddChild(lv);
        lv.AddChild(CourtTheme.Heading(w.Galaxy.Name, CourtTheme.Gilt(era), 18));
        lv.AddChild(CourtTheme.Muted("Territory is public knowledge. Fleets: yours exact (filled), rivals' as reported intel (hollow, faded by confidence). Chevron = house fleet, diamond = office fleet.", era, 11));
        foreach (var h in w.Houses.Values.OrderBy(h => h.Id, StringComparer.Ordinal))
        {
            var systems = GalaxyProjection.TerritoryOf(w, h.Id);
            var row = new HBoxContainer();
            var swatch = new ColorRect { Color = CourtTheme.Accent(h.AccentColor, era), CustomMinimumSize = new Vector2(14, 14) };
            row.AddChild(swatch);
            int hulls = FleetSystem.StrengthOf(w, h.Id);
            bool mine = h.Id == w.Protagonist?.HouseId;
            string fleet = mine ? $" · {hulls} hull(s)" : (GalaxyProjection.Fleets(w).Where(f => f.OwnerHouseId == h.Id).Sum(f => f.Strength) is var seen && seen > 0 ? $" · ~{seen} hull(s) reported" : "");
            row.AddChild(CourtTheme.Body($"  {h.Name} — {systems.Count} system(s){fleet}{(mine ? "  (you)" : "")}", CourtTheme.Ink(era), 12, wrap: false, serif: false));
            lv.AddChild(row);
        }
        _panel.AddChild(legend);

        // the selected system
        var node = _map.SelectedNode;
        var sel = CourtTheme.MakePanel(CourtTheme.PanelAlt(era));
        var sv = CourtTheme.Column(4); sel.AddChild(sv);
        if (node == null)
        {
            sv.AddChild(CourtTheme.Body("Select a system.", CourtTheme.Muted(era)));
        }
        else
        {
            sv.AddChild(CourtTheme.Heading(node.Name, CourtTheme.Accent(node.OwnerHouseId != null ? node.AccentColor : null, era), 20));
            bool atWar = node.OwnerHouseId != null && w.Protagonist != null && w.AtWar(node.OwnerHouseId, w.Protagonist.HouseId);
            sv.AddChild(CourtTheme.Body(node.OwnerHouseName != null ? $"Held by {node.OwnerHouseName}{(node.Contested ? " — CONTESTED" : "")}{(atWar ? "  ⚔ AT WAR" : "")}" : "Unclaimed — the frontier.", atWar ? CourtTheme.Danger : CourtTheme.Ink(era), 13));
            if (node.OwnerHouseId != null && w.Protagonist != null && node.OwnerHouseId != w.Protagonist.HouseId)
            {
                var rules = _state.Content.Fleets;
                var noRng = new Praetoria.Core.Rng.SplitMix64Rng(0);
                if (!atWar)
                {
                    var cmd = new DeclareWarCommand(w.ProtagonistId, node.OwnerHouseId, rules);
                    var btn = CourtTheme.Action($"Declare war on {node.OwnerHouseName}  ({rules.DeclareWarCost} influence)", cmd.CanExecute(new CommandContext(w, noRng)),
                        "Your fleets will fight theirs wherever they meet; an unopposed fleet in their space takes their holdings — and theirs yours.");
                    btn.Pressed += () => _state.Execute(cmd);
                    sv.AddChild(btn);
                }
                else
                {
                    var cmd = new SueForPeaceCommand(w.ProtagonistId, node.OwnerHouseId, rules);
                    bool can = cmd.CanExecute(new CommandContext(w, noRng));
                    var btn = CourtTheme.Action($"Sue for peace with {node.OwnerHouseName}  ({rules.PeaceCost} influence)", can,
                        can ? null : "Refused while they outgun you, or no Influence left this turn.");
                    btn.Pressed += () => _state.Execute(cmd);
                    sv.AddChild(btn);
                }
            }
            foreach (var hid in node.HoldingIds)
            {
                var h = w.Holding(hid)!;
                var spec = _state.Content.Holdings.Spec(h.Specialization);
                sv.AddChild(CourtTheme.Rule());
                sv.AddChild(CourtTheme.Body($"{h.Name} — {spec?.Name ?? h.Specialization} ({w.House(h.OwnerId)?.Name ?? h.OwnerId})", CourtTheme.Ink(era), 13));
                bool mine = h.OwnerId == w.Protagonist?.HouseId;
                // mixed visibility (GDD §19): your own domain in depth, a rival's only as the public record
                sv.AddChild(CourtTheme.Muted(mine
                    ? $"pop {h.Population} · unrest {h.Unrest} · buildings: {(h.Buildings.Count == 0 ? "—" : string.Join(", ", h.Buildings.Select(b => _state.Content.Holdings.Building(b)?.Name ?? b)))}"
                    : $"pop ~{Rough(h.Population)} · {h.Buildings.Count} building(s) reported", era, 11));
            }
            var here = GalaxyProjection.Fleets(w).Where(f => f.SystemId == node.SystemId).ToList();
            if (here.Count > 0)
            {
                sv.AddChild(CourtTheme.Rule());
                foreach (var f in here)
                    sv.AddChild(CourtTheme.Body(f.IsPlayer
                            ? $"⚓ {f.Name}{(f.Office ? " (office)" : "")} — {f.Strength} hull(s), readiness {f.Readiness}{(f.Moving ? $", en route to {w.Galaxy.System(f.DestinationId)?.Name}" : "")}"
                            : $"◦ {f.Name} ({f.OwnerHouseName}) — ~{f.Strength} hull(s), reported {f.Confidence}% as of turn {f.TurnObserved}",
                        f.IsPlayer ? CourtTheme.Ink(era) : CourtTheme.Muted(era), 12, serif: false));
            }
            // orders: send one of your idle fleets here (GDD §9 fleet orders spend Treasury)
            var own = w.Fleets.Values.Where(f => f.OwnerId == w.Protagonist?.HouseId && !f.Moving && f.SystemId != node.SystemId)
                                      .OrderBy(f => f.Id, StringComparer.Ordinal).ToList();
            if (own.Count > 0)
            {
                sv.AddChild(CourtTheme.Rule());
                var pid = w.ProtagonistId;
                foreach (var f in own)
                {
                    var cmd = new MoveFleetCommand(pid, f.Id, node.SystemId, _state.Content.Fleets);
                    int hops = w.Galaxy.Hops(f.SystemId, node.SystemId);
                    bool can = cmd.CanExecute(new CommandContext(w, new Praetoria.Core.Rng.SplitMix64Rng(0)));
                    var btn = CourtTheme.Action($"Order {f.Name} here  ({(hops < 0 ? "unreachable" : hops + " jump(s)")}, 1 treasury)", can,
                        can ? null : "Unreachable, already underway, or no Treasury left this turn.");
                    btn.Pressed += () => _state.Execute(cmd);
                    sv.AddChild(btn);
                }
            }
            sv.AddChild(CourtTheme.Rule());
            var lanes = w.Galaxy.Neighbors(node.SystemId).Select(s => w.Galaxy.System(s)!.Name);
            sv.AddChild(CourtTheme.Muted("Lanes → " + string.Join(", ", lanes), era, 12));
            var home = w.Holdings.Values.FirstOrDefault(h => h.OwnerId == w.Protagonist?.HouseId && h.SystemId != "");
            if (home != null && home.SystemId != node.SystemId)
            {
                int hops = w.Galaxy.Hops(home.SystemId, node.SystemId);
                sv.AddChild(CourtTheme.Muted(hops < 0 ? "Unreachable from your seat." : $"{hops} jump(s) from {w.Galaxy.System(home.SystemId)!.Name}.", era, 12));
            }
        }
        _panel.AddChild(sel);
    }

    private static string Rough(int pop) => pop < 10 ? "few" : pop < 30 ? "tens" : "many";
}

/// <summary>The drawn map itself. Pure rendering of the projection; input only selects.</summary>
public partial class GalaxyMap : Control
{
    public event Action<MapNode?>? Selected;
    public MapNode? SelectedNode { get; private set; }

    private readonly GameState _state;
    private IReadOnlyList<MapNode> _nodes = Array.Empty<MapNode>();
    private IReadOnlyList<MapLane> _lanes = Array.Empty<MapLane>();
    private IReadOnlyList<MapFleet> _fleets = Array.Empty<MapFleet>();
    private readonly Dictionary<string, Vector2> _screen = new();

    private const float Pad = 48f;
    private const float Radius = 11f;

    public GalaxyMap(GameState state)
    {
        _state = state;
        MouseFilter = MouseFilterEnum.Stop;
    }

    public override void _Ready()
    {
        Resized += QueueRedraw;
        Rebuild();
    }

    public void Rebuild()
    {
        var w = _state.World;
        _nodes = w == null ? Array.Empty<MapNode>() : GalaxyProjection.Nodes(w);
        _lanes = w == null ? Array.Empty<MapLane>() : GalaxyProjection.Lanes(w);
        _fleets = w == null ? Array.Empty<MapFleet>() : GalaxyProjection.Fleets(w);
        if (SelectedNode != null) SelectedNode = _nodes.FirstOrDefault(n => n.SystemId == SelectedNode.SystemId);
        QueueRedraw();
    }

    /// <summary>Fit map units into the control, preserving aspect, with padding.</summary>
    private void Layout()
    {
        _screen.Clear();
        if (_nodes.Count == 0) return;
        double minX = _nodes.Min(n => n.X), maxX = _nodes.Max(n => n.X);
        double minY = _nodes.Min(n => n.Y), maxY = _nodes.Max(n => n.Y);
        double spanX = Math.Max(1, maxX - minX), spanY = Math.Max(1, maxY - minY);
        float availW = Math.Max(1, Size.X - 2 * Pad), availH = Math.Max(1, Size.Y - 2 * Pad);
        double scale = Math.Min(availW / spanX, availH / spanY);
        float offX = Pad + (float)((availW - spanX * scale) / 2), offY = Pad + (float)((availH - spanY * scale) / 2);
        foreach (var n in _nodes)
            _screen[n.SystemId] = new Vector2(offX + (float)((n.X - minX) * scale), offY + (float)((n.Y - minY) * scale));
    }

    public override void _Draw()
    {
        var w = _state.World;
        string era = w?.Era ?? "fractured_stars";
        DrawRect(new Rect2(Vector2.Zero, Size), CourtTheme.Background(era));
        Layout();
        if (_nodes.Count == 0)
        {
            DrawString(ThemeDB.FallbackFont, new Vector2(Pad, Pad), "No star map for this scenario.", HorizontalAlignment.Left, -1, 16, CourtTheme.Muted(era));
            return;
        }

        foreach (var l in _lanes)
            if (_screen.TryGetValue(l.A, out var a) && _screen.TryGetValue(l.B, out var b))
                DrawLine(a, b, CourtTheme.Lane, 2f, true);

        var font = CourtTheme.Sans;   // Galaxy mode speaks in a technical sans (BuildSpec §6)
        foreach (var n in _nodes)
        {
            var p = _screen[n.SystemId];
            var fill = n.OwnerHouseId != null ? CourtTheme.Accent(n.AccentColor, era) : CourtTheme.Unclaimed;
            // territory glow
            if (n.OwnerHouseId != null) DrawCircle(p, Radius * 2.2f, new Color(fill, 0.18f));
            DrawCircle(p, Radius, fill);
            if (n.IsPlayerTerritory) DrawArc(p, Radius + 5f, 0, Mathf.Tau, 48, CourtTheme.Gilt(era), 2.5f, true);
            if (n.Contested) DrawArc(p, Radius + 9f, 0, Mathf.Tau, 48, CourtTheme.Danger, 1.5f, true);
            var w0 = _state.World;
            if (w0 != null && w0.Sieges.Values.Any(s => w0.Holding(s.HoldingId)?.SystemId == n.SystemId))
            {
                var siege = w0.Sieges.Values.First(s => w0.Holding(s.HoldingId)?.SystemId == n.SystemId);
                float frac = Mathf.Clamp(siege.Turns / (float)Mathf.Max(1, siege.Required), 0f, 1f);
                DrawArc(p, Radius + 17f, -Mathf.Pi / 2, -Mathf.Pi / 2 + Mathf.Tau * frac, 48, CourtTheme.Danger, 3f, true);   // the siege clock
            }
            if (SelectedNode?.SystemId == n.SystemId) DrawArc(p, Radius + 13f, 0, Mathf.Tau, 64, CourtTheme.Ink(era), 1.5f, true);

            string label = n.Name;
            float width = font.GetStringSize(label, HorizontalAlignment.Left, -1, 13).X;
            DrawString(font, new Vector2(p.X - width / 2, p.Y + Radius + 18), label, HorizontalAlignment.Left, -1, 13, CourtTheme.Ink(era));
            if (n.OwnerHouseName != null)
            {
                float w2 = font.GetStringSize(n.OwnerHouseName, HorizontalAlignment.Left, -1, 11).X;
                DrawString(font, new Vector2(p.X - w2 / 2, p.Y + Radius + 32), n.OwnerHouseName, HorizontalAlignment.Left, -1, 11, CourtTheme.Muted(era));
            }
        }

        // fleet markers (GDD §19): house fleets as chevrons, office fleets as diamonds; own exact, rivals faded by confidence
        var bySystem = _fleets.GroupBy(f => f.SystemId);
        foreach (var group in bySystem)
        {
            if (!_screen.TryGetValue(group.Key, out var p)) continue;
            int i = 0;
            foreach (var f in group.OrderByDescending(f => f.IsPlayer).ThenBy(f => f.FleetId, StringComparer.Ordinal))
            {
                var at = p + new Vector2(Radius + 10f + i * 16f, -Radius - 6f);
                var col = CourtTheme.Accent(f.AccentColor, era);
                float alpha = f.IsPlayer ? 1f : Mathf.Clamp(f.Confidence / 100f, 0.25f, 0.9f);
                DrawFleetGlyph(at, f.Office, new Color(col, alpha), !f.IsPlayer);
                string s = f.Strength.ToString();
                DrawString(font, at + new Vector2(-3, 14 + 10), s, HorizontalAlignment.Left, -1, 10, new Color(CourtTheme.Ink(era), alpha));
                if (f.IsPlayer && f.Moving && _screen.TryGetValue(f.DestinationId, out var dest))
                    DrawDashedLine(p, dest, new Color(col, 0.7f), 1.5f, 6f);
                i++;
            }
        }
    }

    private void DrawFleetGlyph(Vector2 at, bool office, Color col, bool hollow)
    {
        Vector2[] pts = office
            ? new[] { at + new Vector2(0, -7), at + new Vector2(6, 0), at + new Vector2(0, 7), at + new Vector2(-6, 0) }
            : new[] { at + new Vector2(-6, 5), at + new Vector2(0, -7), at + new Vector2(6, 5), at + new Vector2(0, 1) };
        if (hollow)
        {
            var closed = pts.Append(pts[0]).ToArray();
            DrawPolyline(closed, col, 1.5f, true);
        }
        else
        {
            DrawColoredPolygon(pts, col);
        }
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton mb || !mb.Pressed || mb.ButtonIndex != MouseButton.Left) return;
        MapNode? hit = null; float best = 24f;
        foreach (var n in _nodes)
        {
            if (!_screen.TryGetValue(n.SystemId, out var p)) continue;
            float d = p.DistanceTo(mb.Position);
            if (d < best) { best = d; hit = n; }
        }
        SelectedNode = hit;
        Selected?.Invoke(hit);
        QueueRedraw();
        AcceptEvent();
    }
}
