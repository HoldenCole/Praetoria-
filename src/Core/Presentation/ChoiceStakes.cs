using Praetoria.Core.Events;
using Praetoria.Core.State;

namespace Praetoria.Core.Presentation;

/// <summary>
/// What a choice on a report will cost and give, and what a greyed one lacks, in words (docs/PLAYTHROUGH.md 8: every one of five
/// players said a report's choices were blind: no cost before, "requirements not met" with no requirement, no outcome after).
/// Read only. It describes what touches the player and the player's house; what a choice does to others stays in the story.
/// </summary>
public static class ChoiceStakes
{
    private static string Signed(int n) => (n > 0 ? "+" : "−") + Math.Abs(n);
    private const string EstatePrefix = "estate_offset:";
    private static string Op(CompareOp op) => op.ToString().ToLowerInvariant() switch { "gte" or "ge" => "at least", "gt" => "more than", "lte" or "le" => "at most", "lt" => "less than", "eq" => "exactly", "neq" or "ne" => "not", _ => "at least" };

    /// <summary>What the choice does to the player's house and person, as far as its effects say: "−5 materials · −200 credits · legitimacy +2".</summary>
    public static string Of(Choice choice, EvalContext ctx, Character player)
    {
        var parts = new List<string>(); var sums = new Dictionary<string, int>(); int legit = 0, stress = 0;
        bool Mine(string role) => ctx.Actor(role) is { } a && a.HouseId == player.HouseId;
        bool Me(string role) => ctx.Actor(role)?.Id == player.Id;
        foreach (var e in choice.Effects)
            switch (e)
            {
                case AdjustResourceEffect r when Mine(r.Role): sums[r.ResourceKey] = sums.GetValueOrDefault(r.ResourceKey) + r.Delta; break;
                case SellAtMarketEffect sm when Mine(sm.Role): { int got = sm.Proceeds(ctx); sums[sm.ResourceKey] = sums.GetValueOrDefault(sm.ResourceKey) - sm.Units; sums[Resource.Credits] = sums.GetValueOrDefault(Resource.Credits) + got; break; }
                case AdjustLegitimacyEffect l when Mine(l.Role): legit += l.Delta; break;
                case AdjustStressEffect s when Me(s.Role): stress += s.Delta; break;
                case AdjustSkillEffect k when Me(k.Role) && k.Delta != 0: parts.Add(k.Skill.Replace('_', ' ') + " " + Signed(k.Delta)); break;
                case AdjustRelationshipEffect a when ctx.Binding.TryGet(a.To, out var to) && to == player.Id && ctx.Binding.TryGet(a.From, out var from) && from != player.Id && ctx.World.Char(from) is { } who && a.Delta != 0:
                    parts.Add(who.Name + " thinks " + (a.Delta > 0 ? "better" : "worse") + " of you (" + Signed(a.Delta) + ")"); break;
                case AdvanceCareerEffect c when Me(c.Role): parts.Add("a step up in the service"); break;
                case DeclareWarEffect: parts.Add("war"); break;
                case MakePeaceEffect: parts.Add("peace"); break;
                case PeaceTermsEffect pt when ctx.Actor(pt.Role) is { } foe && ctx.World.Wars.FirstOrDefault(x => x.Between(player.HouseId, foe.HouseId)) is { } war:
                {
                    int lead = war.Lead(player.HouseId); var rules = ctx.Content?.Fleets ?? Systems.FleetRules.Default; string them = ctx.World.House(foe.HouseId)?.Name ?? foe.HouseId;
                    if (pt.Kind == Systems.HousePeace.Tribute && lead != 0)
                    {
                        var p = Systems.HousePeace.Payable(ctx.World, lead > 0 ? foe.HouseId : player.HouseId, Math.Abs(lead) * rules.TributePerScore);
                        sums[Resource.Credits] = sums.GetValueOrDefault(Resource.Credits) + (lead > 0 ? p.credits : -p.credits);
                        foreach (var (g, n) in p.goods) sums[g] = sums.GetValueOrDefault(g) + (lead > 0 ? n : -n);
                        parts.Add(p.unpaid > 0 ? $"peace ({Money.Show(Math.Abs(lead) * rules.TributePerScore)} is owed by the score; {Money.Show(p.unpaid)} of it {(lead > 0 ? them + " does not have" : "you do not have")})" : "peace");
                    }
                    else if (pt.Kind == Systems.HousePeace.Cede && pt.World(ctx) is { } h) parts.Add(lead >= 0 ? $"peace, and {h.Name} ({Census.Say(Census.Citizens(ctx.World, h))} citizens) is yours" : $"peace, and {h.Name} is theirs");
                    else parts.Add(lead > 0 && pt.Kind == Systems.HousePeace.White ? "peace, nothing asked: " + them + " will remember it" : "peace, each side keeping what it holds");
                    break;
                }
                case GrantClaimEffect g when Mine(g.Role): parts.Add("a claim to a higher rank"); break;
                // what an estate thinks of the player's house, and the Reach's trust in its magistrates: counters, but ones the player
                // watches on the Laws screen and the ladder's panel, so a choice that moves them says so (docs/events/war.md and votes.md)
                case AdjustCounterEffect cnt when cnt.Delta != 0 && cnt.Key.StartsWith(EstatePrefix, StringComparison.Ordinal):
                {
                    string id = cnt.Key[EstatePrefix.Length..]; string name = ctx.Content?.Policies.Estates.FirstOrDefault(x => x.Id == id)?.Name ?? id.Replace('_', ' ');
                    parts.Add((name.StartsWith("The ", StringComparison.Ordinal) ? "the " + name[4..] : name) + " " + Signed(cnt.Delta)); break;
                }
                case AdjustCounterEffect trust when trust.Delta != 0 && trust.Key == Systems.LadderSystem.TrustKey: parts.Add("trust in the magistrates " + Signed(trust.Delta)); break;
                case KillEffect kill when ctx.Actor(kill.Role) is { } dead && dead.Id != player.Id: parts.Add(dead.Name + " is killed"); break;
            }
        var money = Resource.All.Where(k => sums.GetValueOrDefault(k) != 0).Select(k => (sums[k] > 0 ? "+" : "−") + Money.Of(k, Math.Abs(sums[k])) + " " + Resource.Label(k)).ToList();
        if (sums.GetValueOrDefault(Resource.Coin) is var coin && coin != 0) money.Add((coin > 0 ? "+" : "−") + Money.Coin(Math.Abs(coin)));   // single credits (docs/MONEY.md): not one of Resource.All
        if (legit != 0) money.Add("legitimacy " + Signed(legit));
        if (stress != 0) money.Add("strain " + Signed(stress));
        return string.Join(" · ", money.Concat(parts));
    }

    /// <summary>What the choice does to the player's house and person as items, one to a chip on the event window and a line in its
    /// tooltip (docs/VISUAL_SYSTEM.md 6, "What this needs from Core"): the same effects <see cref="Of"/> puts in words, each with its kind,
    /// its icon, its signed amount and the figure a chip shows, whether it is good for you, what it stands at now and after, and the
    /// person it is about. The client draws from these and never parses the words.</summary>
    public static IReadOnlyList<StakeItem> Items(Choice choice, EvalContext ctx, Character player)
    {
        var items = new List<StakeItem>(); var sums = new Dictionary<string, int>(); int legit = 0, stress = 0;
        var house = ctx.World.House(player.HouseId);
        bool Mine(string role) => ctx.Actor(role) is { } a && a.HouseId == player.HouseId;
        bool Me(string role) => ctx.Actor(role)?.Id == player.Id;
        var skills = new List<StakeItem>(); var regard = new List<StakeItem>(); var other = new List<StakeItem>();
        foreach (var e in choice.Effects)
            switch (e)
            {
                case AdjustResourceEffect r when Mine(r.Role): sums[r.ResourceKey] = sums.GetValueOrDefault(r.ResourceKey) + r.Delta; break;
                case SellAtMarketEffect sm when Mine(sm.Role): { int got = sm.Proceeds(ctx); sums[sm.ResourceKey] = sums.GetValueOrDefault(sm.ResourceKey) - sm.Units; sums[Resource.Credits] = sums.GetValueOrDefault(Resource.Credits) + got; break; }
                case AdjustLegitimacyEffect l when Mine(l.Role): legit += l.Delta; break;
                case AdjustStressEffect s when Me(s.Role): stress += s.Delta; break;
                case AdjustSkillEffect k when Me(k.Role) && k.Delta != 0:
                {
                    int now = player.Skill(k.Skill); string name = Cap(k.Skill.Replace('_', ' '));
                    skills.Add(new StakeItem("skill:" + k.Skill, SkillIcon(k.Skill), k.Delta, Signed(k.Delta), name + " " + Signed(k.Delta), k.Delta > 0, now.ToString(), (now + k.Delta).ToString()));
                    break;
                }
                case AdjustRelationshipEffect a when ctx.Binding.TryGet(a.To, out var to) && to == player.Id && ctx.Binding.TryGet(a.From, out var from) && from != player.Id && ctx.World.Char(from) is { } who && a.Delta != 0:
                {
                    int now = ctx.World.GetRelationship(who.Id, player.Id)?.Disposition ?? 0;
                    regard.Add(new StakeItem("regard", "shaking-hands", a.Delta, Signed(a.Delta), who.Name + " thinks " + (a.Delta > 0 ? "better" : "worse") + " of you", a.Delta > 0,
                        Counted.Figure(now), Counted.Figure(Math.Clamp(now + a.Delta, -100, 100)), who.Id));
                    break;
                }
                case AdvanceCareerEffect c when Me(c.Role):
                {
                    string era = ctx.World.Era; int rank = player.CareerRank; int? max = ctx.Content?.Careers.MaxRank(player.CareerTrack);
                    string now = ctx.Content?.Careers.RankName(player.CareerTrack, rank, era) ?? "", next = max != null && rank >= max ? now : ctx.Content?.Careers.RankName(player.CareerTrack, rank + 1, era) ?? "";
                    other.Add(new StakeItem("career", "upgrade", 1, "Step up", "A step up in the service", true, now, next));
                    break;
                }
                case DeclareWarEffect: other.Add(new StakeItem("war", "war", 0, "War", "War", false)); break;
                case MakePeaceEffect: other.Add(new StakeItem("peace", "peace", 0, "Peace", "Peace", true)); break;
                case PeaceTermsEffect pt when ctx.Actor(pt.Role) is { } foe && ctx.World.Wars.FirstOrDefault(x => x.Between(player.HouseId, foe.HouseId)) is { } war:
                {
                    int lead = war.Lead(player.HouseId); var rules = ctx.Content?.Fleets ?? Systems.FleetRules.Default; string them = ctx.World.House(foe.HouseId)?.Name ?? foe.HouseId; string words;
                    if (pt.Kind == Systems.HousePeace.Tribute && lead != 0)
                    {
                        var p = Systems.HousePeace.Payable(ctx.World, lead > 0 ? foe.HouseId : player.HouseId, Math.Abs(lead) * rules.TributePerScore);
                        sums[Resource.Credits] = sums.GetValueOrDefault(Resource.Credits) + (lead > 0 ? p.credits : -p.credits);
                        foreach (var (g, n) in p.goods) sums[g] = sums.GetValueOrDefault(g) + (lead > 0 ? n : -n);
                        words = p.unpaid > 0 ? $"Peace: {Money.Show(Math.Abs(lead) * rules.TributePerScore)} is owed by the score; {Money.Show(p.unpaid)} of it {(lead > 0 ? them + " does not have" : "you do not have")}" : "Peace";
                    }
                    else if (pt.Kind == Systems.HousePeace.Cede && pt.World(ctx) is { } h) words = lead >= 0 ? $"Peace, and {h.Name} ({Census.Say(Census.Citizens(ctx.World, h))} citizens) is yours" : $"Peace, and {h.Name} is theirs";
                    else words = lead > 0 && pt.Kind == Systems.HousePeace.White ? "Peace, nothing asked: " + them + " will remember it" : "Peace, each side keeping what it holds";
                    other.Add(new StakeItem("peace", "peace", 0, "Peace", words, true));
                    break;
                }
                case GrantClaimEffect g when Mine(g.Role): other.Add(new StakeItem("claim", "claim", 0, "Claim", "A claim to a higher rank", true)); break;
                // the same three Of says in words: an estate's regard for the house, the Reach's trust in its magistrates, a death
                case AdjustCounterEffect cnt when cnt.Delta != 0 && cnt.Key.StartsWith(EstatePrefix, StringComparison.Ordinal):
                {
                    string id = cnt.Key[EstatePrefix.Length..]; string name = ctx.Content?.Policies.Estates.FirstOrDefault(x => x.Id == id)?.Name ?? Cap(id.Replace('_', ' '));
                    int now = Systems.PolicySystem.Approval(ctx.World, player.HouseId, id);
                    other.Add(new StakeItem("estate:" + id, EstateIcon(id), cnt.Delta, Signed(cnt.Delta), name + " " + Signed(cnt.Delta), cnt.Delta > 0,
                        Counted.Figure(now), Counted.Figure(now + cnt.Delta)));
                    break;
                }
                case AdjustCounterEffect trust when trust.Delta != 0 && trust.Key == Systems.LadderSystem.TrustKey:
                    other.Add(new StakeItem("trust", "tied-scroll", trust.Delta, Signed(trust.Delta), "Trust in the magistrates " + Signed(trust.Delta), trust.Delta > 0)); break;
                case KillEffect kill when ctx.Actor(kill.Role) is { } dead && dead.Id != player.Id:
                    other.Add(new StakeItem("death", "plain-dagger", 0, "Dies", dead.Name + " is killed", false, PersonId: dead.Id)); break;
            }
        foreach (var k in Resource.All.Where(k => sums.GetValueOrDefault(k) != 0))
        {
            int d = sums[k]; long held = k == Resource.Credits ? house?.Treasury.Total ?? 0 : house?.Treasury.Get(k) ?? 0;
            string before = k == Resource.Credits ? Money.Exact(held) : held.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
            string after = k == Resource.Credits ? (held + (long)d * Money.PerUnit is var a && a < 0 ? "−" + Money.Exact(-a) : Money.Exact(a)) : Math.Max(0, held + d).ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
            string fig = (d > 0 ? "+" : "−") + Money.Of(k, Math.Abs(d));
            items.Add(new StakeItem("resource:" + k, ResourceIcon(k), d, fig, fig + " " + Resource.Label(k), d > 0, before, after));
        }
        if (sums.GetValueOrDefault(Resource.Coin) is var coin && coin != 0)
        {
            long held = house?.Treasury.Total ?? 0;
            items.Add(new StakeItem("resource:" + Resource.Coin, "credits", coin, (coin > 0 ? "+" : "−") + Money.Exact(Math.Abs(coin)), (coin > 0 ? "+" : "−") + Money.Coin(Math.Abs(coin)), coin > 0,
                Money.Exact(held), held + coin is var a && a < 0 ? "−" + Money.Exact(-a) : Money.Exact(held + coin)));
        }
        if (legit != 0) { int now = house?.Legitimacy ?? 0; items.Add(new StakeItem("legitimacy", "legitimacy", legit, Signed(legit), "Legitimacy " + Signed(legit), legit > 0, now.ToString(), Math.Clamp(now + legit, 0, 100).ToString())); }
        if (stress != 0) items.Add(new StakeItem("stress", "brain", stress, Signed(stress), "Strain " + Signed(stress), stress < 0, player.Stress.ToString(), Math.Clamp(player.Stress + stress, 0, 100).ToString()));
        items.AddRange(skills); items.AddRange(other); items.AddRange(regard);
        return items;
    }

    /// <summary>Every requirement of a choice that can be said, and whether it holds now: a greyed choice's lacks, and for one that is open
    /// the stock it is measured against ("needs at least 1,000 credits in the treasury (it holds 2,500)"), for the choice's tooltip.</summary>
    public static IReadOnlyList<StakeNeed> Needs(Choice choice, EvalContext ctx) =>
        choice.Requirements.Select(c => { bool met = c.Evaluate(ctx); return new StakeNeed(met ? Say(c, ctx, true, true) : Say(c, ctx, true), met); })
            .Where(n => n.Words.Length > 0).GroupBy(n => n.Words).Select(g => g.First()).ToList();

    /// <summary>The glyph of a good in the house's store (docs/VISUAL_SYSTEM.md 8.3).</summary>
    public static string ResourceIcon(string k) => k switch
    {
        Resource.Credits or Resource.Coin => "credits", Resource.Materials => "materials", Resource.Manpower => "manpower", Resource.Influence => "laurels",
        Resource.Exotics => "exotics", Resource.Helium3 => "helium3", Resource.RareEarths => "rare_earths", Resource.Isotopes => "isotopes", _ => "credits"
    };
    /// <summary>The glyph of a skill (docs/VISUAL_SYSTEM.md 8.4).</summary>
    public static string SkillIcon(string skill) => skill switch
    {
        "tactics" => "target-arrows", "gunnery" => "cannon", "leadership" => "rally-the-troops", "logistics" => "cargo-crate", "discipline" => "checked-shield",
        "law" => "scales", "oratory" => "public-speaker", "diplomacy" => "shaking-hands", "charisma" => "sun", "intrigue" => "cowled",
        "administration" => "tied-scroll", "economics" => "abacus", "engineering" => "gears", _ => "upgrade"
    };
    /// <summary>The glyph of a pool of the year's actions (docs/VISUAL_SYSTEM.md 8.3).</summary>
    public static string PoolIcon(string pool) => pool switch { Pool.Influence => "public-speaker", Pool.Treasury => "scroll-quill", Pool.Agents => "cowled", _ => "hourglass" };
    /// <summary>The estate's glyph, as the Laws window draws it (LawsScreen.EstateIcon).</summary>
    public static string EstateIcon(string estate) => estate switch
    {
        "commons" => "three-friends", "merchants" => "coins-pile", "magistrates" => "tied-scroll", "old_blood" => "crown", "officers" => "crossed-sabres", "frontier" => "flag-objective", _ => "public-speaker"
    };
    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    /// <summary>What the player's treasury would be short of if this choice were taken, or null: a choice that takes more than the
    /// house has is said to, before it is chosen.</summary>
    public static string? Overdraws(Choice choice, EvalContext ctx, Character player)
    {
        var house = ctx.World.House(player.HouseId); if (house == null) return null; int credits = 0;
        foreach (var e in choice.Effects) if (e is AdjustResourceEffect r && r.ResourceKey == Resource.Credits && ctx.Actor(r.Role) is { } a && a.HouseId == player.HouseId) credits += r.Delta;
        return credits < 0 && house.Treasury.Credits + credits < 0 ? $"this takes {Money.Credits(-credits)} and the treasury holds {Money.Held(house.Treasury)}: it would leave the house {Money.Coin(-(house.Treasury.Total + (long)credits * Money.PerUnit))} below nought" : null;
    }

    /// <summary>What a greyed choice lacks, in words: every requirement that does not hold, said plainly.</summary>
    public static string Unmet(Choice choice, EvalContext ctx)
    {
        var lacks = new List<string>();
        foreach (var c in choice.Requirements) if (!c.Evaluate(ctx)) lacks.Add(Say(c, ctx, true));
        lacks = lacks.Where(x => x.Length > 0).Distinct().ToList();
        return lacks.Count == 0 ? "not open to you as things stand" : string.Join("; ", lacks);
    }
    /// <summary>A trait as said after "one who is": an aptitude is a calling and takes its article ("a Schemer"), a nature does not ("Honorable").</summary>
    private static string TraitWord(EvalContext ctx, string id)
    {
        var apt = ctx.Content?.Traits.Aptitude.FirstOrDefault(x => x.Id == id); var nat = ctx.Content?.Traits.Nature.FirstOrDefault(x => x.Id == id);
        string name = apt?.Name ?? nat?.Name ?? id.Replace('_', ' ');
        return apt != null && !Adjective(name) ? ("AEIOU".Contains(char.ToUpperInvariant(name[0])) ? "an " : "a ") + name : name;
    }
    /// <summary>An aptitude named by an adjective ("Greedy", "Paranoid", "Brilliant") takes no article; one named by a noun ("a Schemer") does.</summary>
    private static bool Adjective(string name) =>
        new[] { "y", "id", "ant", "ent", "ous", "ful", "ive", "ic", "ble", "al" }.Any(e => name.EndsWith(e, StringComparison.OrdinalIgnoreCase));
    /// <summary>What the player is, for a refusal said to them: "Ambitious, Proud and a Tactician".</summary>
    private static string Traits(EvalContext ctx, Character c)
    {
        var all = c.NatureTraits.Concat(c.AptitudeTraits).Distinct().Select(t => TraitWord(ctx, t)).ToList();
        return all.Count == 0 ? "" : all.Count == 1 ? all[0] : string.Join(", ", all.Take(all.Count - 1)) + " and " + all[^1];
    }
    /// <summary>Why no bargain is to be had with the role's house (VoteMarks.BargainBarredChar), each cause that holds: a war (in the words
    /// the atWar requirement uses, so the two are said once), blood, a favour standing either way, a word broken to it lately.</summary>
    private static string BarredWhy(EvalContext ctx, string role)
    {
        var w = ctx.World; string? mine = w.Protagonist?.HouseId; var theirs = ctx.Actor(role) is { } bound ? w.House(bound.HouseId) : null;
        if (mine == null || theirs == null) return "not while a bargain stands between your house and theirs";
        var why = new List<string>();
        if (w.AtWar(mine, theirs.Id)) why.Add($"not while your house is at war with {theirs.Name}");
        if (Systems.FeudSystem.Between(w, mine, theirs.Id)) why.Add($"not while there is blood between your house and {theirs.Name}: it owes you nothing in the Senate");
        if (Systems.WealthSystem.FavourOwed(w, mine, theirs.Id) || Systems.WealthSystem.FavourOwed(w, theirs.Id, mine)) why.Add($"not while a favour stands between your house and {theirs.Name}, either way (a house owes another one favour at a time)");
        if (Systems.DealingsSystem.Remembered(w, Systems.DealingsSystem.PledgeBrokenKind, mine, theirs.Id).OrderByDescending(d => d.Turn).FirstOrDefault() is { } broke && w.Turn - broke.Turn < Systems.DealingsSystem.PledgeMemoryYears)
            why.Add($"not before year {broke.Turn + Systems.DealingsSystem.PledgeMemoryYears}: you broke your word on a vote to {theirs.Name} in year {broke.Turn}");
        return why.Count > 0 ? string.Join("; ", why) : $"not while a bargain stands between your house and {theirs.Name} this year";
    }
    private static string Who(EvalContext ctx, string role) => role is "self" or "" ? "you" : ctx.Actor(role)?.Name ?? "they";
    private static string Say(ICondition c, EvalContext ctx, bool want, bool met = false)
    {
        switch (c)
        {
            case AllCondition all: return string.Join("; ", all.Of.Where(x => met || x.Evaluate(ctx) != want).Select(x => Say(x, ctx, want, met)).Where(x => x.Length > 0));
            case AnyCondition any: return want ? "one of: " + string.Join(", or ", any.Of.Select(x => Say(x, ctx, true)).Where(x => x.Length > 0)) : string.Join("; ", any.Of.Where(x => x.Evaluate(ctx)).Select(x => Say(x, ctx, false)).Where(x => x.Length > 0));
            case NotCondition not: return Say(not.Of, ctx, !want, met);
            case SkillCondition s: { var a = ctx.Actor(s.Role); return want ? $"needs {s.Skill.Replace('_', ' ')} {Op(s.Op)} {s.Value} ({Who(ctx, s.Role)} {(a == null ? "have none" : "have " + a.Skill(s.Skill))})" : $"not for one with {s.Skill.Replace('_', ' ')} {Op(s.Op)} {s.Value}"; }
            case ResourceCondition r: { var a = ctx.Actor(r.Role); var h = a != null ? ctx.World.House(a.HouseId) : null; return want ? $"needs {Op(r.Op)} {Money.Of(r.ResourceKey, r.Value)} {Resource.Label(r.ResourceKey)} in the treasury (it holds {Money.Of(r.ResourceKey, h?.Treasury.Get(r.ResourceKey) ?? 0)})" : $"only with fewer than {Money.Of(r.ResourceKey, r.Value)} {Resource.Label(r.ResourceKey)}"; }
            case TraitCondition t:
            {
                string trait = TraitWord(ctx, t.Trait); var who = ctx.Actor(t.Role);
                string yours = !met && who != null && who.Id == ctx.World.ProtagonistId ? Traits(ctx, who) : "";
                return (want == t.Present ? "needs one who is " : "not for one who is ") + trait + (yours.Length > 0 ? $" (you are {yours})" : "");
            }
            case RankCondition k: return want ? (k.VsRole != null ? "needs a higher rank in the service than " + Who(ctx, k.VsRole) : $"needs rank {Op(k.Op)} {k.Value} in the service") : "not at your rank in the service";
            case RelationshipCondition rel: { ctx.Binding.TryGet(rel.From, out var f); ctx.Binding.TryGet(rel.To, out var to); int now = f != null && to != null ? ctx.World.GetRelationship(f, to)?.Disposition ?? 0 : 0; string who = ctx.World.Char(f ?? "")?.Name ?? "they"; return want ? $"needs {who} to think {Op(rel.Op)} {rel.Value} of {(to == ctx.World.ProtagonistId ? "you" : ctx.World.Char(to ?? "")?.Name ?? "them")} (now {now})" : $"not while {who} thinks so of you"; }
            case WealthCondition wl: return want ? $"needs a house {Op(wl.Op)} {wl.Tier.Replace('_', ' ')} in wealth" : "not for a house so rich";
            case TitleCondition tt: return (want == tt.Present ? "needs the rank of " : "not for a ") + Progression.Title.StyleOf(tt.TitleId, ctx.World.Era);
            case ClaimCondition cl: return want == cl.Present ? "needs a claim to the rank of " + Progression.Title.StyleOf(cl.TitleId, ctx.World.Era) : "not while the house holds that claim";
            case AgeCondition ag: return want ? $"needs an age {Op(ag.Op)} {ag.Value}" : "not at that age";
            case FleetStrengthCondition fl: { var a = ctx.Actor(fl.Role); int have = a != null ? Systems.FleetSystem.StrengthOf(ctx.World, a.HouseId) : 0; return want ? $"needs {Op(fl.Op)} {Counted.Hulls(fl.Value)} (the house has {have})" : "not with a fleet of that size"; }
            case AtWarCondition aw: return want == aw.Present ? "only in a war" : aw.Role is "self" or "" ? "not while at war" : $"not while your house is at war with {(ctx.Actor(aw.Role) is { } foe && ctx.World.House(foe.HouseId) is { } foeHouse ? foeHouse.Name : "theirs")}";
            case PeaceTermCondition pc: { var t = pc.Terms(ctx).FirstOrDefault(); return !want ? "" : t == null ? (pc.Kind == Systems.HousePeace.Tribute ? "tribute is asked by the side ahead in the war, and you are not ahead (a battle won is 2, a world taken 3)" : "there is no such term to ask in this war") : t.Why.Length > 0 ? t.Why : "they will not take that term"; }
            case BondCondition: return want ? "needs a tie between the two that is not there" : "not between two so tied";
            case TrackCondition tr: return want == tr.Present ? "needs a career in " + tr.Track.Replace('_', ' ') : "not for one in " + tr.Track.Replace('_', ' ');
            case EraCondition er: return want == er.Present ? "only under the " + er.Era : "not under the " + er.Era;
            case DepositCondition dp: return want == dp.Present ? "needs " + Resource.A(dp.ResourceKey) + " deposit on a world of the house" : "";
            case SphereCondition sp: return want ? $"needs standing {Op(sp.Op)} {sp.Value} with {sp.SphereKey.Replace('_', ' ')}" : "";
            case BlocCondition bl: return want == bl.Present ? "needs a seat with " + bl.Bloc.Replace('_', ' ') : "not for one of " + bl.Bloc.Replace('_', ' ');
            case CharFlagCondition cf when cf.Flag == Systems.VoteMarks.BargainBarredChar: return BarredWhy(ctx, cf.Role);
            case CharFlagCondition cf when cf.Flag == Systems.VoteMarks.VoteWantedChar && want == cf.Value: return WantedWhy(ctx, cf.Role);
            case WorldFlagCondition wf when wf.Flag == Systems.VoteMarks.FavoursToCallFlag: return want == wf.Value ? "needs a house that owes yours a favour and can vote against the motion: not the house that moved it, nor one at war with yours" : "";
            case WorldFlagCondition wf when wf.Flag == Systems.VoteMarks.ClientFlag: return want == wf.Value ? "needs a client house with no favour standing between it and yours, either way" : "";
            case WorldFlagCondition wf when wf.Flag == Systems.VoteMarks.SplittableFlag: return want == wf.Value ? "needs a house of that bench, not its leader, that would vote for the motion and has not declared" : "";
            case WorldFlagCondition wf when wf.Flag == Systems.ElectionSystem.CourtFlag: return want == wf.Value ? "needs another consul, of a third house, with a veto still to use this term" : "";
            default: return "";   // a flag, a counter, a turn: something that happened or did not, which the story has told
        }
    }
    /// <summary>Why that house may not have the player's vote on its motion now: given elsewhere already, or no such motion before the Senate.</summary>
    private static string WantedWhy(EvalContext ctx, string role)
    {
        var w = ctx.World; string? mine = w.Protagonist?.HouseId; var bound = ctx.Actor(role); var theirs = bound != null ? w.House(bound.HouseId) : null;
        if (mine == null || theirs == null) return "needs a motion of theirs before the Senate that your vote may be had on";
        var m = Systems.SenateSystem.Docket(w).FirstOrDefault(x => x.ProposerHouseId == theirs.Id);
        if (m == null) return $"{theirs.Name} has no motion before the Senate now";
        if (Systems.VoteMarks.Given(w, mine, m) is { } given) return $"your vote on the motion of {theirs.Name} is {given} already: giving it again would break that";
        return Systems.WealthSystem.StrikesAt(m, mine) ? $"the motion of {theirs.Name} strikes at your house" : "needs a motion of theirs before the Senate that your vote may be had on";
    }
}

/// <summary>One thing a choice does to the player or the house (docs/VISUAL_SYSTEM.md 6): <paramref name="Kind"/> ("resource:credits",
/// "legitimacy", "stress", "skill:tactics", "regard", "career", "war", "peace", "claim", "pool:influence"), the icon key, the signed amount (0
/// where it is not a number), the figure a chip shows ("+10", "−900", "Step up"), the line a tooltip says, whether it is good for you, what
/// it stands at before and after where that is known ("2,500" and "1,900"; "Commander" and "Captain"), and the person it is about.</summary>
public sealed record StakeItem(string Kind, string Icon, int Amount, string Figure, string Words, bool Good, string Before = "", string After = "", string PersonId = "");
/// <summary>A requirement of a choice in words, and whether it holds now.</summary>
public sealed record StakeNeed(string Words, bool Met);
