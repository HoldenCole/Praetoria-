using Godot;
using Praetoria.Core.Creation;
using Praetoria.Core.State;

namespace Praetoria.Game;

/// <summary>
/// Character creation (GDD §13/§18): choose the start (scenario, seed, ironman/continue), then shape the
/// protagonist — name, sex, a background (origin), up to two nature and two aptitude traits, career track and
/// ambition. Every list is read from the content catalogs; the engine validates the result, so the screen
/// only has to present and collect. Emits <see cref="Begin"/> with the choices.
/// </summary>
public partial class CreationScreen : Control
{
    public event Action<string, ulong, CreationOptions, string, double>? Begin;

    private readonly GameState _state;
    private OptionButton _scenario = null!, _sex = null!, _background = null!, _track = null!, _ambition = null!, _mode = null!, _difficulty = null!;
    private LineEdit _name = null!, _seed = null!;
    private Label _backgroundText = null!, _error = null!;
    private readonly List<CheckBox> _nature = new(), _aptitude = new();

    public CreationScreen(GameState state) => _state = state;

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        const string era = "fractured_stars";
        var content = _state.Content;

        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        scroll.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(scroll);
        var center = new CenterContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        scroll.AddChild(center);
        var panel = CourtTheme.MakePanel(CourtTheme.Panel(era), 24, 10);
        panel.CustomMinimumSize = new Vector2(900, 0);
        center.AddChild(panel);
        var col = CourtTheme.Column(10);
        panel.AddChild(col);

        col.AddChild(CourtTheme.Heading("PRAETORIA", CourtTheme.Gilt(era), 34));
        col.AddChild(CourtTheme.Muted("You don't play an empire. You play a family trying to survive one.", era, 14));
        col.AddChild(CourtTheme.Rule());

        // ---- the start ----
        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 16);
        grid.AddThemeConstantOverride("v_separation", 8);
        col.AddChild(grid);

        _scenario = new OptionButton();
        foreach (var id in _state.ScenarioIds()) _scenario.AddItem(id);
        Row(grid, "Scenario", _scenario, era);

        _seed = new LineEdit { Text = "1", PlaceholderText = "any whole number" };
        Row(grid, "Seed", _seed, era);

        _mode = new OptionButton();
        _mode.AddItem("Ironman — bloodline extinct ends the run");
        _mode.AddItem("Continue — a sworn house takes up the banner");
        Row(grid, "Mode", _mode, era);

        _difficulty = new OptionButton();
        foreach (var d in new[] { "Forgiving (0.5) — rivals slow to coalesce", "Standard (1.0)", "Harsh (1.5) — the GDD's default lean", "Brutal (2.0) — the galaxy wants you dead" }) _difficulty.AddItem(d);
        _difficulty.Selected = 1;
        Row(grid, "Difficulty", _difficulty, era);

        col.AddChild(CourtTheme.Rule());
        col.AddChild(CourtTheme.Heading("The character", CourtTheme.Ink(era), 20));
        col.AddChild(CourtTheme.Muted("Leave a field untouched to keep the scenario's own protagonist as written.", era));

        var grid2 = new GridContainer { Columns = 2 };
        grid2.AddThemeConstantOverride("h_separation", 16);
        grid2.AddThemeConstantOverride("v_separation", 8);
        col.AddChild(grid2);

        _name = new LineEdit { PlaceholderText = "scenario default" };
        Row(grid2, "Name", _name, era);

        _sex = new OptionButton();
        _sex.AddItem("scenario default"); _sex.AddItem("male"); _sex.AddItem("female");
        Row(grid2, "Sex", _sex, era);

        _background = new OptionButton();
        _background.AddItem("none — as the scenario wrote you");
        foreach (var b in content.Backgrounds.Defs) _background.AddItem(b.Name);
        _background.ItemSelected += _ => RefreshBackground();
        Row(grid2, "Background", _background, era);

        _track = new OptionButton();
        _track.AddItem("scenario default");
        foreach (var t in content.Careers.Defs) _track.AddItem($"{t.Id} — {t.Name}");
        Row(grid2, "Career track", _track, era);

        _ambition = new OptionButton();
        _ambition.AddItem("scenario default");
        foreach (var a in content.Traits.Ambitions) _ambition.AddItem(a.Name);
        Row(grid2, "Ambition", _ambition, era);

        _backgroundText = CourtTheme.Body("", CourtTheme.Muted(era), 13);
        col.AddChild(_backgroundText);

        // ---- traits ----
        col.AddChild(CourtTheme.Heading($"Nature — who you are (pick up to {CharacterCreation.MaxPickedNature}; acting against it costs stress)", CourtTheme.Ink(era), 15));
        col.AddChild(TraitGrid(content.Traits.Nature, _nature, CharacterCreation.MaxPickedNature, era));
        col.AddChild(CourtTheme.Heading($"Aptitude — what you're good at (pick up to {CharacterCreation.MaxPickedAptitude}; opens options, costs nothing)", CourtTheme.Ink(era), 15));
        col.AddChild(TraitGrid(content.Traits.Aptitude, _aptitude, CharacterCreation.MaxPickedAptitude, era));

        col.AddChild(CourtTheme.Rule());
        _error = CourtTheme.Body("", CourtTheme.Danger, 13);
        col.AddChild(_error);

        var begin = CourtTheme.Action("Take the stage");
        begin.CustomMinimumSize = new Vector2(0, 44);
        begin.Pressed += OnBegin;
        col.AddChild(begin);
    }

    private static void Row(GridContainer grid, string label, Control field, string era)
    {
        grid.AddChild(CourtTheme.Body(label, CourtTheme.Muted(era), 13, wrap: false));
        field.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        field.CustomMinimumSize = new Vector2(520, 0);
        grid.AddChild(field);
    }

    private static GridContainer TraitGrid(IReadOnlyList<TraitDef> traits, List<CheckBox> into, int max, string era)
    {
        var grid = new GridContainer { Columns = 3 };
        grid.AddThemeConstantOverride("h_separation", 12);
        foreach (var t in traits)
        {
            var cb = new CheckBox { Text = t.Id, TooltipText = t.Description };
            cb.Toggled += _ => EnforceMax(into, max);
            into.Add(cb);
            grid.AddChild(cb);
        }
        return grid;
    }

    /// <summary>Once the cap is reached, the unticked boxes lock until one is unticked.</summary>
    private static void EnforceMax(List<CheckBox> boxes, int max)
    {
        int picked = boxes.Count(b => b.ButtonPressed);
        foreach (var b in boxes) b.Disabled = picked >= max && !b.ButtonPressed;
    }

    private void RefreshBackground()
    {
        int i = _background.Selected - 1;
        if (i < 0 || i >= _state.Content.Backgrounds.Defs.Count) { _backgroundText.Text = ""; return; }
        var b = _state.Content.Backgrounds.Defs[i];
        var bits = new List<string>();
        if (b.Legitimacy != 0) bits.Add($"legitimacy {b.Legitimacy:+#;-#;0}");
        foreach (var r in Praetoria.Core.State.Resource.All) if (b.Treasury.Get(r) != 0) bits.Add($"{r} {b.Treasury.Get(r):+#;-#;0}");
        foreach (var t in b.Nature) bits.Add("+" + t);
        foreach (var t in b.Aptitude) bits.Add("+" + t);
        foreach (var kv in b.Skills) bits.Add($"{kv.Key} {kv.Value:+#;-#;0}");
        _backgroundText.Text = $"{b.Description}\n[{string.Join(" · ", bits)}]" +
                               (b.TrackHint != null ? $"  · suggests the {b.TrackHint} track" : "") +
                               (b.AmbitionHint != null ? $"  · ambition: {b.AmbitionHint}" : "");
    }

    private void OnBegin()
    {
        _error.Text = "";
        if (!ulong.TryParse(_seed.Text.Trim(), out var seed)) { _error.Text = "The seed must be a whole number."; return; }
        var content = _state.Content;
        var o = new CreationOptions
        {
            Name = string.IsNullOrWhiteSpace(_name.Text) ? null : _name.Text,
            Sex = _sex.Selected == 0 ? null : _sex.GetItemText(_sex.Selected),
            BackgroundId = _background.Selected == 0 ? null : content.Backgrounds.Defs[_background.Selected - 1].Id,
            CareerTrack = _track.Selected == 0 ? null : content.Careers.Defs[_track.Selected - 1].Id,
            Ambition = _ambition.Selected == 0 ? null : content.Traits.Ambitions[_ambition.Selected - 1].Id,
            Nature = _nature.Where(b => b.ButtonPressed).Select(b => b.Text).ToList(),
            Aptitude = _aptitude.Where(b => b.ButtonPressed).Select(b => b.Text).ToList()
        };
        string mode = _mode.Selected == 1 ? World.ModeContinue : World.ModeIronman;
        double difficulty = _difficulty.Selected switch { 0 => 0.5, 2 => 1.5, 3 => 2.0, _ => 1.0 };
        try
        {
            Begin?.Invoke(_scenario.GetItemText(_scenario.Selected), seed, o, mode, difficulty);
        }
        catch (Praetoria.Core.Data.ContentException ex)
        {
            _error.Text = ex.Message;
        }
    }
}
