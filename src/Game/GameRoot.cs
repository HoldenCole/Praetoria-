using Godot;
using Praetoria.Core;
using Praetoria.Core.Creation;
using Praetoria.Core.Data;

namespace Praetoria.Game;

/// <summary>
/// The Godot entry point (BuildSpec §6, Milestone 3). Loads content, shows character creation, then hosts
/// the two co-equal modes — Court and Galaxy — behind a persistent top bar with the mode toggle, End Turn,
/// Save and Load (GDD §4). It owns no rules: every button is a call on <see cref="GameState"/>.
/// </summary>
public partial class GameRoot : Control
{
    private const string SavePath = "user://praetoria.save.json";

    private GameState _state = null!;
    private VBoxContainer _shell = null!;
    private HBoxContainer _topBar = null!;
    private Control _stage = null!;
    private Label _status = null!;
    private Button _courtBtn = null!, _galaxyBtn = null!, _endTurnBtn = null!, _saveBtn = null!, _loadBtn = null!, _treeBtn = null!;
    private CourtView? _court;
    private GalaxyView? _galaxy;
    private bool _showGalaxy;

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        DisplayServer.WindowSetTitle($"Praetoria {PraetoriaVersion.Current}");

        string contentRoot = ProjectSettings.GlobalizePath("res://content");
        if (!Directory.Exists(contentRoot)) contentRoot = ContentLocator.FindContentDir();
        _state = new GameState(contentRoot);
        _state.Changed += OnChanged;

        _shell = new VBoxContainer();
        _shell.SetAnchorsPreset(LayoutPreset.FullRect);
        _shell.AddThemeConstantOverride("separation", 8);
        AddChild(_shell);

        var bg = new ColorRect { Color = CourtTheme.Background("fractured_stars") };
        bg.SetAnchorsPreset(LayoutPreset.FullRect);
        bg.ShowBehindParent = true;
        AddChild(bg);
        MoveChild(bg, 0);

        BuildTopBar();
        _stage = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        foreach (var side in new[] { "margin_left", "margin_right", "margin_bottom" }) _stage.AddThemeConstantOverride(side, 10);
        _shell.AddChild(_stage);

        ShowCreation();
    }

    private void BuildTopBar()
    {
        _topBar = new HBoxContainer();
        _topBar.AddThemeConstantOverride("separation", 8);
        var panel = CourtTheme.MakePanel(CourtTheme.Panel("fractured_stars"), 8, 0);
        panel.AddChild(_topBar);
        _shell.AddChild(panel);

        _status = CourtTheme.Heading("Praetoria", CourtTheme.Gilt("fractured_stars"), 16);
        _status.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _topBar.AddChild(_status);

        _courtBtn = new Button { Text = "Court", ToggleMode = true, ButtonPressed = true };
        _galaxyBtn = new Button { Text = "Galaxy", ToggleMode = true };
        _courtBtn.Pressed += () => SetMode(false);
        _galaxyBtn.Pressed += () => SetMode(true);
        _endTurnBtn = new Button { Text = "End turn  ▶" };
        _endTurnBtn.Pressed += () => _state.EndTurn();
        _saveBtn = new Button { Text = "Save" };
        _saveBtn.Pressed += () => { _state.Save(ProjectSettings.GlobalizePath(SavePath)); Flash("Saved."); };
        _loadBtn = new Button { Text = "Load" };
        _loadBtn.Pressed += OnLoad;
        _treeBtn = new Button { Text = "Dynasty" };
        _treeBtn.Pressed += ShowDynastyTree;
        var quit = new Button { Text = "Quit" };
        quit.Pressed += () => GetTree().Quit();
        foreach (var b in new[] { _courtBtn, _galaxyBtn, _treeBtn, _endTurnBtn, _saveBtn, _loadBtn, quit }) _topBar.AddChild(b);
        SetInGame(false);
    }

    /// <summary>The summonable full-tree overlay (GDD §19): the house by generation, parents to children, with the
    /// living, the dead, the hidden and the married marked. A popup, not a mode.</summary>
    private void ShowDynastyTree()
    {
        var w = _state.World;
        if (w?.Protagonist == null) return;
        var popup = new PopupPanel();
        popup.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = CourtTheme.Panel(w.Era), ContentMarginLeft = 18, ContentMarginRight = 18, ContentMarginTop = 14, ContentMarginBottom = 14 });
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(620, 480) };
        var col = CourtTheme.Column(4);
        scroll.AddChild(col); popup.AddChild(scroll);
        var house = w.House(w.Protagonist.HouseId)!;
        col.AddChild(CourtTheme.Heading($"{house.Name} — the tree", CourtTheme.Accent(house.AccentColor, w.Era), 20));
        col.AddChild(CourtTheme.Muted($"Generation {w.Counter("generation") + 1} · law of {house.SuccessionLaw}", w.Era));
        var members = w.Characters.Values.Where(c => c.HouseId == house.Id).ToList();
        var roots = members.Where(c => !members.Any(m => m.Id == c.FatherId || m.Id == c.MotherId)).OrderBy(c => c.Age).ThenBy(c => c.Id, StringComparer.Ordinal).Reverse().ToList();
        void Line(Praetoria.Core.State.Character c, int depth)
        {
            string spouse = Praetoria.Core.Systems.DynastySystem.Spouse(w, c)?.Name is { } s ? $" ⚭ {s}" : "";
            string mark = !c.Alive ? "† " : c.Id == w.ProtagonistId ? "★ " : c.Flags.Contains(Praetoria.Core.Systems.DynastySystem.HiddenFlag) ? "⌂ " : "";
            col.AddChild(CourtTheme.Body(new string(' ', depth * 4) + $"{mark}{c.Name}{spouse} — {(c.Alive ? $"{c.Age}, {_state.RankName(c)}" : "dead")}{(c.NatureTraits.Count > 0 ? $" · {string.Join("/", c.NatureTraits)}" : "")}", c.Alive ? CourtTheme.Ink(w.Era) : CourtTheme.Muted(w.Era), 12, wrap: false));
            foreach (var child in members.Where(m => m.FatherId == c.Id || m.MotherId == c.Id).OrderByDescending(m => m.Age).ThenBy(m => m.Id, StringComparer.Ordinal))
                if (depth < 8) Line(child, depth + 1);
        }
        foreach (var r in roots) Line(r, 0);
        col.AddChild(CourtTheme.Muted("★ you · † dead · ⌂ fostered away · ⚭ married", w.Era, 11));
        AddChild(popup);
        popup.PopupCentered();
        popup.PopupHide += () => popup.QueueFree();
    }

    private void SetInGame(bool inGame)
    {
        _courtBtn.Visible = _galaxyBtn.Visible = _endTurnBtn.Visible = _saveBtn.Visible = _treeBtn.Visible = inGame;
        _loadBtn.Visible = true;
    }

    private void ShowCreation()
    {
        CourtTheme.Clear(_stage);
        _court = null; _galaxy = null;
        var screen = new CreationScreen(_state);
        screen.Begin += (scenario, seed, options, mode, difficulty, intensity, autonomy) =>
        {
            _state.Start(scenario, seed, options, mode, difficulty, intensity, autonomy);   // throws ContentException on a bad pick; the screen shows it
            EnterGame();
        };
        _stage.AddChild(screen);
        _status.Text = $"Praetoria {PraetoriaVersion.Current} — a new dynasty";
    }

    private void EnterGame()
    {
        CourtTheme.Clear(_stage);
        _court = new CourtView(_state);
        _galaxy = new GalaxyView(_state) { Visible = false };
        _stage.AddChild(_court);
        _stage.AddChild(_galaxy);
        SetInGame(true);
        SetMode(false);
        OnChanged();
    }

    private void SetMode(bool galaxy)
    {
        _showGalaxy = galaxy;
        _courtBtn.ButtonPressed = !galaxy;
        _galaxyBtn.ButtonPressed = galaxy;
        if (_court != null) _court.Visible = !galaxy;
        if (_galaxy != null) { _galaxy.Visible = galaxy; if (galaxy) _galaxy.Refresh(); }
    }

    private void OnChanged()
    {
        var w = _state.World;
        if (w == null) return;
        var p = w.Protagonist;
        string era = w.Era == "imperium" ? "the Imperium" : "the Fractured Stars";
        _status.Text = p == null ? "Praetoria" :
            $"Turn {w.Turn} · {p.Name} of {w.House(p.HouseId)?.Name ?? p.HouseId} · {_state.RankName(p)} · {era}" +
            (w.IsDynastyDead ? "  —  THE LINE IS ENDED" : "");
        _status.AddThemeColorOverride("font_color", CourtTheme.Gilt(w.Era));
        _endTurnBtn.Disabled = w.IsDynastyDead;
        _court?.Refresh();
        if (_showGalaxy) _galaxy?.Refresh();
    }

    private void OnLoad()
    {
        string path = ProjectSettings.GlobalizePath(SavePath);
        if (!File.Exists(path)) { Flash("No save found."); return; }
        try
        {
            _state.Load(path);
            if (_court == null) EnterGame();
            Flash("Loaded.");
        }
        catch (ContentException ex) { Flash(ex.Message); }
    }

    private void Flash(string text)
    {
        _status.Text = text;
        var timer = GetTree().CreateTimer(1.6);
        timer.Timeout += OnChanged;
    }
}
