using Praetoria.Core;
using Praetoria.Core.Data;
using Praetoria.Core.Events;
using Praetoria.Tools;

// Praetoria headless console harness (BuildSpec §2 /src/Tools, §M1 acceptance).
// Subcommands:
//   play      [--seed N] [--turns N] [--auto] [--scenario id] [--save f] [--load f]   interactive (or auto) playthrough
//   demo      [--seed N]                                         scripted run that proves the cascade
//   validate                                                     lint the content set (CI gate)
// The whole simulation runs here without Godot — the Milestone-1 rule.

var (command, opts) = ParseArgs(args);

try
{
    return command switch
    {
        "validate" => Commands.Validate(),
        "demo"     => Commands.Demo(opts),
        "turn"     => Commands.Turn(opts),
        "economy"  => Commands.Economy(opts),
        "crisis"   => Commands.Crisis(opts),
        "spheres"  => Commands.Spheres(opts),
        "progression" => Commands.Progression(opts),
        "dynasty"  => Commands.Dynasty(opts),
        "play"     => Commands.Play(opts),
        "help" or "--help" or "-h" => Help(),
        "version" or "--version" => Version(),
        "create"   => Commands.Create(),
        "map"      => Commands.Map(opts),
        "epilogue" => Commands.EpilogueOf(opts),
        "simulate" => Commands.Simulate(opts),
        _ => Unknown(command)
    };
}
catch (ContentException ex)
{
    Console.Error.WriteLine($"Content error: {ex.Message}");
    return 2;
}

static (string, Options) ParseArgs(string[] args)
{
    string command = args.Length > 0 && !args[0].StartsWith('-') ? args[0] : "play";
    var o = new Options();
    for (int i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--seed" when i + 1 < args.Length: o.Seed = ulong.Parse(args[++i]); break;
            case "--turns" when i + 1 < args.Length: o.Turns = int.Parse(args[++i]); break;
            case "--scenario" when i + 1 < args.Length: o.Scenario = args[++i]; break;
            case "--auto": o.Auto = true; break;
            case "--continue": o.Continue = true; break;
            case "--intensity" when i + 1 < args.Length: o.Intensity = args[++i]; break;
            case "--autonomy" when i + 1 < args.Length: o.Autonomy = double.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
            case "--seeds" when i + 1 < args.Length: o.Seeds = int.Parse(args[++i]); break;
            case "--difficulty" when i + 1 < args.Length: o.Difficulty = double.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
            case "--name" when i + 1 < args.Length: o.Creation.Name = args[++i]; break;
            case "--sex" when i + 1 < args.Length: o.Creation.Sex = args[++i]; break;
            case "--background" when i + 1 < args.Length: o.Creation.BackgroundId = args[++i]; break;
            case "--nature" when i + 1 < args.Length: o.Creation.Nature = Split(args[++i]); break;
            case "--aptitude" when i + 1 < args.Length: o.Creation.Aptitude = Split(args[++i]); break;
            case "--track" when i + 1 < args.Length: o.Creation.CareerTrack = args[++i]; break;
            case "--ambition" when i + 1 < args.Length: o.Creation.Ambition = args[++i]; break;
            case "--save" when i + 1 < args.Length: o.Save = args[++i]; break;
            case "--load" when i + 1 < args.Length: o.Load = args[++i]; break;
        }
    }
    return (command, o);
}

static int Help()
{
    Console.WriteLine($"praetoria {Praetoria.Core.PraetoriaVersion.Current} — <command> [options]");
    Console.WriteLine("  play [--seed N] [--turns N] [--auto] [--scenario id] [--save file] [--load file] [--continue]");
    Console.WriteLine("       [--name N] [--sex male|female] [--background id] [--nature A,B] [--aptitude A,B] [--track t] [--ambition a] [--difficulty 0.5..2]");
    Console.WriteLine("  create                         list character-creation choices (backgrounds, traits, tracks, ambitions)");
    Console.WriteLine("  map [--scenario id]            the galaxy: systems, lanes, and who holds what");
    Console.WriteLine("  epilogue --load file           the saga epilogue of a saved game");
    Console.WriteLine("  simulate [--scenario id|all] [--seeds N] [--turns N]   balance soak: aggregate stats over many seeds");
    Console.WriteLine("  turn [--seed N] [--turns N]   full Briefing→Action→Resolve cycle with pools + NPCs");
    Console.WriteLine("  economy [--seed N] [--turns N] domain economy: holdings accrue, invest in buildings");
    Console.WriteLine("  crisis [--seed N]              crisis gate/cascade/damper demonstration");
    Console.WriteLine("  spheres [--seed N]             power-balance: career feeds spheres feeds coalitions");
    Console.WriteLine("  progression [--seed N]         title ladder + legitimacy soft-lock (merit vs seize)");
    Console.WriteLine("  dynasty [--seed N]             aging, death, birth, succession (the dynasty outlives the man)");
    Console.WriteLine("  demo [--seed N]               cascade demonstration");
    Console.WriteLine("  validate");
    Console.WriteLine("  version");
    return 0;
}

static List<string> Split(string csv) =>
    csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

static int Version()
{
    Console.WriteLine($"Praetoria {Praetoria.Core.PraetoriaVersion.Current} — \"{Praetoria.Core.PraetoriaVersion.Codename}\" (headless core)");
    return 0;
}

static int Unknown(string c)
{
    Console.Error.WriteLine($"Unknown command '{c}'. Try 'help'.");
    return 1;
}

sealed class Options
{
    public ulong Seed = 1;
    public int Turns = 6;
    public bool Auto;
    public string Scenario = "academy_crucible";
    public string? Save;    // write the World here when the run ends (between turns)
    public string? Load;    // resume from a save instead of a fresh scenario
    public bool Continue;   // GDD §14 continue mode: a sworn house takes up the banner on dynasty death
    public double Difficulty = 1.0;   // GDD §14 one knob
    public string Intensity = "default";   // GDD §20 crisis intensity: off | low | default | dominant
    public double Autonomy = 1.0;          // GDD §20 family autonomy
    public int Seeds = 10;                 // simulate: how many seeds
    public Praetoria.Core.Creation.CreationOptions Creation = new();   // character customisation at start
}
