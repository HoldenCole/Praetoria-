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
}
