using Praetoria.Core.Careers;
using Praetoria.Core.Commands;
using Praetoria.Core.Data;
using Praetoria.Core.Progression;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// The people of the house act on what they want (docs/AMBITIONS.md; docs/PROCESS.md, "One person, not a house"). Every grown
/// member has an ambition, and pursues it without being told: one who wants a command takes one that stands empty, one who wants
/// a fortune founds a venture with their own money, one who wants to restore the house puts their purse into it, one who wants
/// the seat builds a standing to rival the heir's. Now and then they ASK the head for something that would help: a command, a
/// stake, a word spoken for their promotion, to be named heir. The head grants it or refuses; that is the whole of the head's
/// part. They succeed or are thwarted either way, and remember which the head made easier.
/// Only the player's house is run by it. RNG-free.
/// </summary>
public sealed class AmbitionSystem
{
    public const string Command = "command", Stake = "stake", Word = "word", Heir = "heir";
    public const int Adult = DynastySystem.AgeOfMajority, AskEvery = 4, RequestYears = 2, RestYears = 4, FortuneAt = 50, GivenAt = 34, WordInfluence = 2, ThwartedAfter = 10;
    // each year
    public const string AskingFlag = "kin_asks_for_help";
    // deeds
    public const string FulfilledFlag = "kin_fulfilled", ThwartedFlag = "kin_thwarted", FoundedFlag = "kin_founded_a_venture", HeirNamedFlag = "heir_named", RefusedFlag = "kin_refused";
    public const string FulfilledChar = "has_what_they_wanted", ThwartedChar = "thwarted", FounderChar = "founded_a_venture", AskingChar = "asking_the_head";
    private static readonly string[] DeedFlags = { FulfilledFlag, ThwartedFlag, FoundedFlag, HeirNamedFlag, RefusedFlag };

    private readonly BusinessCatalog _ventures; private readonly HoldingCatalog _holdings; private readonly TitleCatalog? _titles; private readonly CareerCatalog _careers;
    public AmbitionSystem(BusinessCatalog ventures, HoldingCatalog holdings, TitleCatalog? titles, CareerCatalog careers) { _ventures = ventures; _holdings = holdings; _titles = titles; _careers = careers; }

    private static void Mark(World w, string flag) { w.WorldFlags.Add(flag); w.WorldCounters["mark:" + flag] = w.Turn; }
    private static void Regard(World w, Character? from, Character? to, int delta, int floor = -100)
    {
        if (from == null || to == null || from.Id == to.Id) return;
        var r = w.Relationship(from.Id, to.Id); if (delta < 0 && r.Disposition <= floor) return;
        r.Disposition = Math.Clamp(r.Disposition + delta, -100, 100);
    }

    public static string Wants(string goal) => goal switch
    {
        "command_a_fleet" or "earn_a_command" => "a command of their own",
        "amass_a_fortune" => "a fortune of their own",
        "restore_house_fortunes" => "to see the house rich again",
        "master_the_senate" => "to rise in their service and be heard",
        "outshine_all_rivals" => "to be the first of the house",
        "seize_the_throne" => "the seat of the house",
        "win_a_great_love" => "a great love",
        _ => "nothing in particular"
    };
    /// <summary>What a person amounts to, for comparing two of the house: what they know and how far they have risen.</summary>
    public static int Standing(Character c) => c.Skills.Values.Sum() + c.CareerRank * 3;
    public static List<Character> Members(World w, House house) =>
        w.LivingCharacters().Where(c => c.HouseId == house.Id && c.Id != w.ProtagonistId && c.Age >= Adult && !c.Flags.Contains(DynastySystem.HiddenFlag)).OrderBy(c => c.Id, StringComparer.Ordinal).ToList();
    public static string RequestSays(World w, Character c)
    {
        var u = c.Pursuit; if (u == null || u.Request.Length == 0) return "";
        return u.Request switch
        {
            Command => $"asks for the command of {(w.Fleets.TryGetValue(u.RequestArg, out var f) ? f.Name : "a fleet")}",
            Stake => $"asks the house to stake them {u.RequestArg} credits to found a venture",
            Word => "asks you to speak for their promotion",
            Heir => "asks to be named your heir",
            _ => ""
        };
    }
    public static string GrantCosts(World w, Character c) => c.Pursuit?.Request switch
    {
        Command => "the admiral who has it now will not thank you",
        Stake => $"{c.Pursuit.RequestArg} credits from the treasury",
        Word => $"{WordInfluence} influence",
        Heir => "the house's standing, a little, and the regard of whoever would have had it",
        _ => ""
    };

    // ---------------- what the head can do ----------------

    public string? AnswerRefusal(World w, Character? head, Character? who, bool grant, ActionPools? pools)
    {
        if (head?.Alive != true || who?.Alive != true || who.HouseId != head.HouseId || who.Pursuit is not { Request.Length: > 0 } u) return "they have asked for nothing";
        if (!grant) return null;
        var house = w.House(head.HouseId)!;
        return u.Request switch
        {
            Command => w.Fleets.TryGetValue(u.RequestArg, out var f) && f.OwnerId == house.Id ? null : "the fleet is gone",
            Stake => house.Treasury.Credits >= int.Parse(u.RequestArg) ? null : "the treasury does not hold it",
            Word => (pools?.Influence ?? 0) >= WordInfluence ? (who.CareerRank < (_careers.ById(who.CareerTrack)?.MaxRank ?? 0) ? null : "they are at the top of their service") : "no influence",
            _ => null
        };
    }

    /// <summary>Grant or refuse what one of the house has asked for.</summary>
    public void Answer(World w, Character head, Character who, bool grant, CommandExecutor executor, CommandContext ctx)
    {
        var u = who.Pursuit!; var house = w.House(head.HouseId)!; string asked = u.Request; who.Flags.Remove(AskingChar);
        if (!grant)
        {
            u.Request = ""; u.RequestArg = ""; u.Refused++; Regard(w, who, head, asked == Heir ? -15 : -8); Mark(w, RefusedFlag);
            w.Log($"{head.Name} tells {who.Name} no."); return;
        }
        switch (asked)
        {
            case Command:
                var fleet = w.Fleets[u.RequestArg]; var old = w.Char(fleet.AdmiralId);
                foreach (var other in w.Fleets.Values.Where(x => x.AdmiralId == who.Id)) other.AdmiralId = "";
                fleet.AdmiralId = who.Id; if (old != null && old.Id != head.Id) Regard(w, old, head, -10);
                w.Log($"{head.Name} gives {who.Name} the command of {fleet.Name}{(old != null ? $", over {old.Name}" : "")}."); break;
            case Stake:
                int sum = int.Parse(u.RequestArg);
                if (!Found(w, house, who, executor, ctx, fromPurse: false)) { w.Log($"{who.Name} could find nothing worth founding with the stake; it stays in the treasury."); u.Request = ""; u.RequestArg = ""; return; }
                _ = sum; break;
            case Word:
                ctx.PoolsOf(head.Id).Spend(new Dictionary<string, int> { [Pool.Influence] = WordInfluence });
                who.CareerRank = Math.Min(who.CareerRank + 1, _careers.ById(who.CareerTrack)?.MaxRank ?? who.CareerRank + 1);
                w.Log($"{head.Name} speaks for {who.Name}, who is {_careers.RankName(who.CareerTrack, who.CareerRank, w.Era) ?? "promoted"} now."); break;
            case Heir:
                NameHeir(w, head, who); break;
        }
        u.Request = ""; u.RequestArg = ""; u.Helped++; Regard(w, who, head, 12);
    }

    public static string? NameHeirRefusal(World w, Character? head, Character? who)
    {
        if (head?.Alive != true || w.House(head.HouseId) is not { } house) return "no house";
        if (who == null) return house.NamedHeirId.Length > 0 ? null : "nobody is named";
        if (!who.Alive || who.HouseId != house.Id || who.Id == head.Id) return "not someone of your house";
        return house.NamedHeirId == who.Id ? "already named" : null;
    }

    /// <summary>Name someone of the house to succeed you, ahead of whoever the law would give; or (null) name nobody and let the
    /// law decide. Passing over the law's heir costs the house a little standing and that person's regard.</summary>
    public static void NameHeir(World w, Character head, Character? who)
    {
        var house = w.House(head.HouseId)!;
        if (who == null) { house.NamedHeirId = ""; w.Log($"{head.Name} names no heir: the succession of {house.Name} is by its law."); return; }
        house.NamedHeirId = ""; var lawful = DynastySystem.FindHeir(w, head);
        house.NamedHeirId = who.Id; Mark(w, HeirNamedFlag);
        if (lawful != null && lawful.Id != who.Id) { house.Legitimacy -= 2; Regard(w, lawful, head, -20); Regard(w, lawful, who, -20); }
        w.Log($"{head.Name} names {who.Name} heir to {house.Name}{(lawful != null && lawful.Id != who.Id ? $", over {lawful.Name}" : "")}.");
    }

    // ---------------- what they do themselves ----------------

    /// <summary>The cheapest venture the house could found now, and where.</summary>
    private (BusinessDef def, string systemId, int cost)? Opening(World w, House house, Character actor, CommandContext ctx, int budget)
    {
        var systems = w.HoldingsOf(house.Id).Where(h => h.SystemId.Length > 0).Select(h => h.SystemId).Distinct().OrderBy(s => s, StringComparer.Ordinal).ToList();
        if (systems.Count == 0 && w.Galaxy != null) systems = w.Galaxy.Systems.Values.Where(s => s.Belt > 0).Select(s => s.Id).OrderBy(s => s, StringComparer.Ordinal).Take(6).ToList();
        foreach (var def in _ventures.Defs.Where(d => !d.Lender && d.Cost.Credits > 0 && d.Cost.Credits <= budget).OrderBy(d => d.Cost.Credits).ThenBy(d => d.Id, StringComparer.Ordinal))
            foreach (var sys in systems)
            {
                int had = house.Treasury.Credits; house.Treasury.Credits = Math.Max(had, def.Cost.Credits + 1000);   // ask whether it could be founded, the money aside
                string? no = new FoundBusinessCommand(actor.Id, def.Id, sys, _ventures, _holdings, _titles).Refusal(ctx);
                house.Treasury.Credits = had;
                if (no == null) return (def, sys, def.Cost.Credits);
            }
        return null;
    }
    public int CheapestVenture() => _ventures.Defs.Where(d => !d.Lender && d.Cost.Credits > 0).Select(d => d.Cost.Credits).DefaultIfEmpty(0).Min();

    /// <summary>They found a venture in the house's name: with their own money, or with a stake the head granted.</summary>
    private bool Found(World w, House house, Character who, CommandExecutor executor, CommandContext ctx, bool fromPurse)
    {
        int budget = fromPurse ? who.Purse : int.TryParse(who.Pursuit?.RequestArg, out int s) ? s : 0;
        var open = Opening(w, house, who, ctx, budget); if (open == null) return false;
        var (def, sys, cost) = open.Value;
        if (fromPurse) { who.Purse -= cost; house.Treasury.Credits += cost; }
        if (!executor.TryExecute(new FoundBusinessCommand(who.Id, def.Id, sys, _ventures, _holdings, _titles), ctx))
        { if (fromPurse) { who.Purse += cost; house.Treasury.Credits -= cost; } return false; }
        w.Founders[FoundBusinessCommand.IdFor(house.Id, def.Id, sys)] = who.Id; who.Pursuit!.Note = $"Founded {def.Name}."; who.Flags.Add(FounderChar); Mark(w, FoundedFlag); LifeSystem.Note(w, who, "venture", $"Founded {def.Name} for the house.");
        w.Log($"{who.Name} founds {def.Name} for {house.Name}{(fromPurse ? " with their own money" : " with the house's stake")}.");
        return true;
    }

    private void Ask(World w, Character c, string request, string arg)
    {
        var u = c.Pursuit!; u.Request = request; u.RequestArg = arg; u.RequestSince = w.Turn; u.LastAsked = w.Turn; c.Flags.Add(AskingChar);
        w.Log($"{c.Name} {RequestSays(w, c)}.");
    }

    public void Tick(World w, CommandExecutor executor, CommandContext ctx)
    {
        w.WorldFlags.Remove(AskingFlag);
        foreach (var f in DeedFlags) if (w.WorldFlags.Contains(f) && w.Counter("mark:" + f) < w.Turn - 1) { w.WorldFlags.Remove(f); w.WorldCounters.Remove("mark:" + f); }
        var p = w.Protagonist; var house = p != null ? w.House(p.HouseId) : null; if (p is not { Alive: true } || house == null) return;
        foreach (var c in w.Characters.Values)
        {
            if (!w.HasFlag(FulfilledFlag)) c.Flags.Remove(FulfilledChar); if (!w.HasFlag(ThwartedFlag)) c.Flags.Remove(ThwartedChar); if (!w.HasFlag(FoundedFlag)) c.Flags.Remove(FounderChar);
        }
        // a named heir who is gone is named no longer
        if (house.NamedHeirId.Length > 0 && (w.Char(house.NamedHeirId) is not { Alive: true } nh || nh.HouseId != house.Id || nh.Id == p.Id)) house.NamedHeirId = "";
        // ventures begun by one of the house, once they stand, carry the founder's name
        foreach (var b in w.BusinessesOf(house.Id).Where(b => b.FounderId.Length == 0).ToList())
            if (w.Founders.TryGetValue(b.Id, out var fid)) { b.FounderId = fid; w.Founders.Remove(b.Id); }
        if (w.FamilyAutonomy <= 0) return;

        var members = Members(w, house); var heir = DynastySystem.FindHeir(w, p);
        foreach (var c in members)
        {
            if (c.Ambition.Length == 0) { c.Pursuit = null; continue; }
            var u = c.Pursuit;
            if (u == null || u.Goal != c.Ambition) c.Pursuit = u = new Pursuit { Goal = c.Ambition, Since = w.Turn };
            if (u.Fulfilled)
            {
                // having had it, in time they want something else
                if (w.Turn - u.FulfilledTurn >= RestYears)
                {
                    string[] next = { "outshine_all_rivals", "master_the_senate", "amass_a_fortune", "restore_house_fortunes", "command_a_fleet" };
                    c.Ambition = next.First(n => n != u.Goal && !(n == "command_a_fleet" && u.Goal == "earn_a_command"));
                    c.Pursuit = new Pursuit { Goal = c.Ambition, Since = w.Turn, Note = "Has set their mind on something new." };
                }
                continue;
            }
            // an unanswered request lapses, and is taken as a no
            if (u.Request.Length > 0 && w.Turn - u.RequestSince >= RequestYears) { u.Request = ""; u.RequestArg = ""; u.Refused++; c.Flags.Remove(AskingChar); Regard(w, c, p, -5); }
            bool mayAsk = u.Request.Length == 0 && w.Turn - u.LastAsked >= AskEvery && w.Turn > u.Since; u.Note = "";
            var career = _careers.ById(c.CareerTrack); bool canRise = career != null && c.CareerRank < career.MaxRank;

            switch (u.Goal)
            {
                case "command_a_fleet": case "earn_a_command":
                {
                    var mine = w.Fleets.Values.FirstOrDefault(f => f.OwnerId == house.Id && f.AdmiralId == c.Id);
                    if (mine != null) { u.Progress = Math.Min(100, u.Progress + 34); u.Note = $"Commands {mine.Name}."; break; }
                    var fleets = w.Fleets.Values.Where(f => f.OwnerId == house.Id).OrderBy(f => f.Id, StringComparer.Ordinal).ToList();
                    u.Progress = Math.Min(60, c.CareerRank * 12 + c.Skill("tactics") * 4);
                    if (fleets.Count == 0) { u.Note = "The house keeps no ships. They look to their own service."; if (mayAsk && canRise) Ask(w, c, Word, ""); break; }
                    // the fleet whose admiral they think they could better
                    var want = fleets.Select(f => (f, adm: w.Char(f.AdmiralId))).Where(x => x.adm == null || (x.adm.Id != p.Id && x.adm.Skill("tactics") < c.Skill("tactics")))
                        .OrderBy(x => x.adm?.Skill("tactics") ?? -1).ThenBy(x => x.f.Id, StringComparer.Ordinal).Select(x => x.f).FirstOrDefault();
                    u.Note = want != null ? $"Has an eye on {want.Name}." : "Waits for a command to fall vacant.";
                    if (mayAsk && want != null) Ask(w, c, Command, want.Id);
                    break;
                }
                case "amass_a_fortune": case "restore_house_fortunes":
                {
                    bool restore = u.Goal == "restore_house_fortunes"; int cheapest = CheapestVenture();
                    if (restore && c.Purse > 5 && house.Treasury.Credits < 60)
                    {
                        int gift = (c.Purse - 5) / 2; if (gift > 0) { c.Purse -= gift; house.Treasury.Credits += gift; u.Given += gift; u.Note = $"Put {gift} credit{(gift == 1 ? "" : "s")} of their own into the house."; }
                    }
                    u.Progress = restore ? Math.Min(100, u.Given * 100 / GivenAt) : Math.Clamp(c.Purse * 100 / FortuneAt, 0, 100);
                    bool hasVenture = w.Founders.Values.Contains(c.Id) || w.Businesses.Values.Any(b => b.FounderId == c.Id);
                    if (!hasVenture && cheapest > 0 && c.Purse >= cheapest + 3 && Found(w, house, c, executor, ctx, fromPurse: true)) break;
                    if (!hasVenture && mayAsk && cheapest > 0 && c.Purse < cheapest + 3 && house.Treasury.Credits >= cheapest + 20 && Opening(w, house, c, ctx, cheapest * 2) is { } open) Ask(w, c, Stake, open.cost.ToString());
                    else if (u.Note.Length == 0) u.Note = hasVenture ? "Minds what they founded." : "Saves what they can.";
                    break;
                }
                case "master_the_senate":
                {
                    bool office = w.Offices.Any(o => o.CharacterId == c.Id);
                    // the ladder counts for most of it: a praetor has been heard (docs/LADDER.md)
                    int climbed = (c.Cursus.Contains(LadderSystem.Quaestor) ? 25 : 0) + (c.Cursus.Contains(LadderSystem.Aedile) ? 15 : 0) + (c.Cursus.Contains(LadderSystem.Praetor) ? 45 : 0);
                    u.Progress = office ? 100 : Math.Min(100, c.CareerRank * 8 + c.Skill("oratory") * 4 + climbed);
                    var sits = LadderSystem.Seat(w, c.Id); var stands = w.Candidacies.FirstOrDefault(cd => cd.CharacterId == c.Id);
                    u.Note = office ? "Holds an office of state." : sits != null ? $"Sits as {sits.OfficeId}." : stands != null ? $"Is standing for {stands.RungId}." : "Works at their service and their speaking.";
                    if (!office && mayAsk && canRise) Ask(w, c, Word, "");
                    break;
                }
                case "outshine_all_rivals":
                {
                    var rival = members.Where(m => m.Id != c.Id).OrderByDescending(Standing).ThenBy(m => m.Id, StringComparer.Ordinal).FirstOrDefault();
                    if (rival == null) { u.Progress = 100; u.Note = "There is nobody of the house to outshine."; break; }
                    int lead = Standing(c) - Standing(rival); u.Progress = Math.Clamp(50 + lead * 5, 0, 100);
                    u.Note = lead > 0 ? $"Ahead of {rival.Name}, and means to stay there." : $"Measures themselves against {rival.Name}.";
                    Regard(w, c, rival, -1, floor: -30); Regard(w, rival, c, -1, floor: -30);
                    if (mayAsk && canRise && lead <= 0) Ask(w, c, Word, "");
                    break;
                }
                case "seize_the_throne":
                {
                    if (heir?.Id == c.Id) { u.Progress = 100; u.Note = "Stands to inherit, and knows it."; break; }
                    int lead = heir != null ? Standing(c) - Standing(heir) : 10; u.Progress = Math.Clamp(45 + lead * 4, 0, 95);
                    u.Note = heir != null ? $"Thinks themselves fitter than {heir.Name}." : "Sees the seat with nobody before it.";
                    if (mayAsk && u.Progress >= 55) Ask(w, c, Heir, "");
                    break;
                }
                case "win_a_great_love":
                {
                    var m = w.MarriageOf(c.Id); var suit = w.Courtships.FirstOrDefault(s => s.SuitorId == c.Id && !s.Incoming);
                    u.Progress = m is { Kind: CourtshipSystem.Love } ? 100 : suit != null ? Math.Min(90, suit.Favour) : 0;
                    u.Note = m is { Kind: CourtshipSystem.Love } ? "Married for love." : m != null ? "Married, and not for love." : suit != null ? $"Is courting {w.Char(suit.TargetId)?.Name}." : "Has not found them yet.";
                    break;
                }
                default: u.Note = ""; break;
            }

            if (u.Progress >= 100 && !u.Fulfilled)
            {
                u.Fulfilled = true; u.FulfilledTurn = w.Turn; u.Request = ""; u.RequestArg = ""; c.Flags.Remove(AskingChar); c.Flags.Add(FulfilledChar); Mark(w, FulfilledFlag);
                Regard(w, c, p, 4 + 6 * Math.Min(2, u.Helped)); LifeSystem.Note(w, c, "fulfilled", $"Had what they had wanted: {Wants(u.Goal)}.");
                w.Log($"{c.Name} has what they wanted: {Wants(u.Goal)}.{(u.Helped > 0 ? $" They know what {p.Name} did toward it." : "")}");
            }
            else if (!u.Fulfilled && w.Turn - u.Since >= ThwartedAfter && u.Progress < 30 && u.Refused > u.Helped)
            {
                // years of wanting it, little to show, and the head has said no more often than yes
                Regard(w, c, p, -1, floor: -50);
                if (c.Flags.Add(ThwartedChar)) { Mark(w, ThwartedFlag); w.Log($"{c.Name} has wanted {Wants(u.Goal)} for {w.Turn - u.Since} years and is no nearer it."); }
            }
            if (u.Request.Length > 0) w.WorldFlags.Add(AskingFlag);
        }
        // a founder who wanted a fortune takes a share of what their venture earns
        foreach (var b in w.BusinessesOf(house.Id).Where(b => b.FounderId.Length > 0))
        {
            var f = w.Char(b.FounderId); if (f is not { Alive: true } || f.HouseId != house.Id || f.Pursuit?.Goal != "amass_a_fortune" && f.Ambition != "amass_a_fortune") continue;
            int share = Math.Max(1, b.Level); if (house.Treasury.Credits >= share) { house.Treasury.Credits -= share; f.Purse += share; }
        }
    }
}
