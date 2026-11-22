using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// Asking those above you (docs/PLAYTHROUGH.md, Medium, The Senate, P6, P17 and P54). A house may ask its patron, or the
/// leader of the bench it sits on, for one of three things: coin; its backing for consul (its seats pledged to the house);
/// or to move a motion in its own name that the house wants moved, which is the way a house with no voice gets a motion
/// onto the floor. Each is granted where the head asked thinks well enough of the asker and is able; each leaves the asker
/// owing that house a favour, which is settled by voting for the next motion it moves; and a house that owes one already
/// is not given another. One asking of each house every <see cref="EveryYears"/> years. RNG-free.
/// </summary>
public static class AskAbove
{
    public const string Coin = "coin", Backing = "backing", Motion = "motion";
    /// <summary>The regard at which a patron or a bench's leader will hear a request; the most coin given, and what the giver keeps back.</summary>
    public const int AskAt = 10, CoinMost = 15, CoinKeeps = 30, CoinShare = 6, EveryYears = 2;
    public const string AskedCounter = "asked_above";
    public static string AskedKey(string ofHouseId, string byHouseId) => $"asked_above:{ofHouseId}>{byHouseId}";

    /// <summary>The houses a house may ask: its patron, and the leader of its bench (once, if they are the same house).</summary>
    public static List<(House house, string asWhat)> Above(World w, string houseId)
    {
        var list = new List<(House, string)>(); var mine = w.House(houseId); if (mine == null) return list;
        if (w.House(mine.PatronId) is { } patron) list.Add((patron, "your patron"));
        if (FactionSystem.Of(w, houseId) is { } bench && bench.LeaderHouseId != houseId && w.House(bench.LeaderHouseId) is { } leader)
        {
            if (list.Any(x => x.Item1.Id == leader.Id)) list[0] = (leader, "your patron, and the leader of " + bench.Name); else list.Add((leader, "the leader of " + bench.Name));
        }
        return list;
    }
    public static int RegardOf(World w, House of, House by)
    {
        var a = Economy.HeadOf(w, of); var b = Economy.HeadOf(w, by);
        return a != null && b != null ? w.GetRelationship(a.Id, b.Id)?.Disposition ?? 0 : 0;
    }
    /// <summary>What a house would give if asked for coin: a sixth of its treasury, fifteen at the most.</summary>
    public static int CoinOf(House of) => Math.Clamp(of.Treasury.Credits / CoinShare, 0, CoinMost);

    /// <summary>Why the asking is refused whatever is asked, or null.</summary>
    public static string? Hears(World w, House by, House? of)
    {
        if (of == null || !Above(w, by.Id).Any(x => x.house.Id == of.Id)) return "they are neither your patron nor the leader of your bench";
        if (w.AtWar(by.Id, of.Id)) return "you are at war with them";
        var head = Economy.HeadOf(w, of); if (head == null) return $"{of.Name} has no head to ask";
        int regard = RegardOf(w, of, by);
        if (regard < AskAt) return $"{head.Name} thinks {regard:+0;-0} of you, and hears a request at {AskAt:+0;-0} ({AskAt - regard} short)";
        if (WealthSystem.FavourOwed(w, by.Id, of.Id)) return $"you owe {of.Name} a favour already: vote for the next motion it moves, and ask then";
        int last = w.Counter(AskedKey(of.Id, by.Id));
        return last > 0 && w.Turn - last < EveryYears ? $"you asked {of.Name} in year {last}: not again before year {last + EveryYears}" : null;
    }
    public static string? Refusal(World w, OfficeCatalog offices, Func<string, int> weight, House by, House? of, string what)
    {
        if (Hears(w, by, of) is { } no) return no;
        switch (what)
        {
            case Coin:
                int coin = CoinOf(of!);
                return coin < 3 || of!.Treasury.Credits - coin < CoinKeeps ? $"{of!.Name} has no coin to spare: it holds {of.Treasury.Credits}, and keeps {CoinKeeps} by" : null;
            case Backing:
                if (!w.HasFlag(ElectionSystem.StandFlag(by.Id))) return "you are not standing for consul: declare first";
                if (w.HasFlag(ElectionSystem.StandFlag(of!.Id)) || ElectionSystem.Nominees(w, offices, weight).Any(n => n.houseId == of.Id)) return $"{of.Name} stands itself";
                return ElectionSystem.PledgedTo(w, of.Id) == by.Id ? "its seats are pledged to you already" : null;
            default: return "no such thing to ask";
        }
    }
    /// <summary>What would be given, said before the asking.</summary>
    public static string Says(World w, Func<string, int> weight, House by, House of, string what) =>
        (what == Coin ? $"{of.Name} gives you {CoinOf(of)} credits." : $"{of.Name} pledges its {Counted.Seats(weight(of.Id))} to you for the consulship: they are counted for you, and inside the bench as well.")
        + $" You owe it a favour: vote for the next motion it moves and the account is even; vote otherwise and its head remembers. You may not ask it again for {EveryYears} years.";

    private static void Asked(World w, House by, House of)
    {
        w.WorldCounters[AskedKey(of.Id, by.Id)] = Math.Max(1, w.Turn); w.WorldCounters[AskedCounter] = w.Counter(AskedCounter) + 1; WealthSystem.OweFor(w, by.Id, of.Id);
    }
    public static void Grant(World w, Func<string, int> weight, House by, House of, string what)
    {
        if (what == Coin) { int coin = CoinOf(of); of.Treasury.Credits -= coin; by.Treasury.Credits += coin; w.Log($"{by.Name} asks {of.Name} for coin, and is given {coin} credits. It is a favour owed."); }
        else { w.WorldFlags.Add(ElectionSystem.PledgePrefix + of.Id + ">" + by.Id); w.Log($"{by.Name} asks {of.Name} for its backing, and {of.Name} pledges its seats to it for the consulship. It is a favour owed."); }
        Asked(w, by, of);
    }

    /// <summary>The motion as the house asked would move it: its head proposes it, in its own house's name.</summary>
    public static ProposeMotionCommand? AsTheirs(World w, House of, string kind, string targetId, SenateSystem senate, string officeId, string lawId, string optionId) =>
        Economy.HeadOf(w, of) is { } head ? new ProposeMotionCommand(head.Id, kind, targetId, senate, officeId, lawId, optionId) : null;
    public static string? MoveRefusal(World w, CommandContext ctx, House by, House? of, string kind, string targetId, SenateSystem senate, string officeId, string lawId, string optionId)
    {
        if (Hears(w, by, of) is { } no) return no;
        var cmd = AsTheirs(w, of!, kind, targetId, senate, officeId, lawId, optionId); if (cmd == null) return $"{of!.Name} has no head to ask";
        if (targetId == of!.Id) return $"{of.Name} will not move a motion against itself";
        return cmd.Refusal(ctx) is { } theirs ? $"{of.Name} cannot move it: {theirs}" : null;
    }
    public static void Move(World w, CommandContext ctx, CommandExecutor executor, House by, House of, string kind, string targetId, SenateSystem senate, string officeId, string lawId, string optionId)
    {
        var cmd = AsTheirs(w, of, kind, targetId, senate, officeId, lawId, optionId); if (cmd == null || !executor.TryExecute(cmd, ctx)) return;
        w.Log($"{of.Name} moves it at the asking of {by.Name}. It is a favour owed."); Asked(w, by, of);
    }
}
