using Praetoria.Core.Data;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// Coin behind a canvass (docs/WEALTH.md, "Things worth a rich house's money"; docs/PLAYTHROUGH.md, "Hard": "fund a relative's
/// campaign"). When one of the house stands for an office of the ladder its head may spend the treasury on their canvass, in three
/// sizes. It buys votes at the count with falling returns, to a cap, besides what speaking for them buys. A lavish canvass is
/// written on the record as votes bought, and any house may bring it before a praetor at once: the candidate is the one charged.
/// The one whose canvass it was remembers who paid, won or lost.
/// <para>Other houses that can afford it put coin behind their own candidate the year before the count, and it is told, so that
/// a funded rival can be outspent in the election year.</para> RNG-free.
/// </summary>
public static partial class LadderSystem
{
    public const string Modest = "modest", Handsome = "handsome", Lavish = "lavish", BoughtElection = "election";
    public static readonly string[] CampaignSizes = { Modest, Handsome, Lavish };
    public const int CampaignRegard = 4, CampaignWonRegard = 4, CampaignKept = 20, OthersCampaignAfford = 5;
    public const string CampaignsKey = "campaigns_paid_for";

    /// <summary>What a canvass of this size costs in all, in the books' hundreds.</summary>
    public static int CampaignCost(string size) => size switch { Modest => 5, Handsome => 15, Lavish => 40, _ => 0 };
    /// <summary>What it is worth at the count: each size buys fewer votes for its coin than the one below, and the lavish is the most coin can buy.</summary>
    public static int CampaignVotes(string size) => size switch { Modest => 3, Handsome => 6, Lavish => 10, _ => 0 };
    public static string CampaignName(string size) => size switch { Modest => "a modest canvass", Handsome => "a handsome canvass", Lavish => "a lavish canvass", _ => size };
    private static int SizeRank(string size) => Array.IndexOf(CampaignSizes, size);
    /// <summary>A deed that can be brought before a praetor while the one who did it still sits: a praetor bought, an election bought.</summary>
    public static bool ChargedAtOnce(OfficeDeed d) => d.What is Bribed or BoughtElection;

    /// <summary>The coin behind this person for the count that is coming, if any.</summary>
    public static CampaignFund? CampaignOf(World w, string characterId) => w.Campaigns.FirstOrDefault(f => !f.Counted && f.CharacterId == characterId);
    /// <summary>What other houses have put behind their candidates for this rung at the coming count.</summary>
    public static List<CampaignFund> RivalCampaigns(World w, string rungId) =>
        w.Campaigns.Where(f => !f.Counted && f.RungId == rungId && f.HouseId != w.Protagonist?.HouseId).OrderByDescending(f => f.Votes).ThenBy(f => f.HouseId, StringComparer.Ordinal).ToList();
    /// <summary>The votes coin has bought a candidate. The player's house pays for a person; another house for whoever of it stands for the rung.</summary>
    public static int CampaignVotesFor(World w, Character c, string rungId)
    {
        string? mine = w.Protagonist?.HouseId; int best = 0;
        foreach (var f in w.Campaigns) if (!f.Counted && (f.CharacterId == c.Id || (f.HouseId != mine && f.HouseId == c.HouseId && f.RungId == rungId))) best = Math.Max(best, f.Votes);
        return best;
    }

    public static string? CampaignRefusal(World w, OfficeCatalog offices, Character? head, Character? who, string size)
    {
        var house = head != null ? w.House(head.HouseId) : null;
        if (head?.Alive != true || house == null || Economy.HeadOf(w, house)?.Id != head.Id) return "only the head of the house spends its treasury on an election";
        if (who?.Alive != true || who.HouseId != house.Id) return "not of your house";
        if (!Active(w, offices)) return "there are no elections under a throne";
        if (!w.Candidacies.Any(c => c.CharacterId == who.Id)) return who.Id == head.Id ? "you are not standing" : "they are not standing";
        if (SizeRank(size) < 0) return "no such canvass";
        if (SenateActs.CanvassForbidden(w, size)) return $"{SenateActs.LawName(w, SenateActs.CanvassLaw)} forbids {CampaignName(size)}: {CampaignName(Handsome)} is the most coin may do, and that is written down";
        var fund = CampaignOf(w, who.Id);
        if (fund != null && SizeRank(fund.Size) >= SizeRank(size)) return fund.Size == Lavish ? "a lavish canvass is paid for: there is nothing more that coin can buy" : $"{CampaignName(fund.Size)} is paid for already: only a larger one adds to it";
        int cost = CampaignCost(size) - (fund?.Spent ?? 0);
        return house.Treasury.Credits >= cost ? null : $"it costs {Money.Credits(cost)} and the treasury holds {Money.Held(house.Treasury)}";
    }

    /// <summary>The head pays for a canvass of this size out of the treasury, or raises one already paid for to it. The one it is
    /// for thinks the better of the head, a step for each size. A lavish canvass goes on the record.</summary>
    public static CampaignFund Campaign(World w, OfficeCatalog offices, Character head, Character who, string size)
    {
        var house = w.House(head.HouseId)!; var cd = w.Candidacies.First(c => c.CharacterId == who.Id); string office = offices.Rung(cd.RungId)?.Name.ToLowerInvariant() ?? cd.RungId;
        var fund = CampaignOf(w, who.Id); int was = fund != null ? SizeRank(fund.Size) : -1, cost = CampaignCost(size) - (fund?.Spent ?? 0); house.Treasury.Credits -= cost;
        if (fund == null) { fund = new CampaignFund { HouseId = house.Id, CharacterId = who.Id, Turn = w.Turn }; w.Campaigns.Add(fund); w.WorldCounters[CampaignsKey] = w.Counter(CampaignsKey) + 1; }
        fund.RungId = cd.RungId; fund.Size = size; fund.Spent = CampaignCost(size); fund.Votes = CampaignVotes(size);
        if (who.Id != head.Id) { var r = w.Relationship(who.Id, head.Id); r.Disposition = Math.Clamp(r.Disposition + CampaignRegard * (SizeRank(size) - was), -100, 100); }
        bool recorded = SenateActs.CanvassRecorded(w, size); if (recorded) BoughtVotes(w, who, house.Id, cd.RungId);
        w.Log($"{head.Name} puts {Money.Credits(cost)} of the house's behind {(who.Id == head.Id ? "their own canvass" : who.Name + "'s canvass")} for {office}: {CampaignName(size)}{(recorded ? ", and one that will be talked of" : "")}.");
        return fund;
    }
    private static void BoughtVotes(World w, Character who, string houseId, string rungId)
    {
        string id = $"deed_election_{w.Turn}_{who.Id}"; if (w.OfficeRecord.Any(d => d.Id == id)) return;
        w.OfficeRecord.Add(new OfficeDeed { Id = id, CharacterId = who.Id, HouseId = houseId, RungId = rungId, Turn = w.Turn, What = BoughtElection, AgainstId = "", Answered = false });
    }

    /// <summary>The count is over: what was paid for is marked won or lost; one of the player's house who was paid for and won
    /// knows what they owe; other houses' are struck out, and the player's are kept as a record for some years.</summary>
    private static void CampaignsCounted(World w, ISet<string> seated)
    {
        var p = w.Protagonist; string? mine = p?.HouseId;
        foreach (var f in w.Campaigns.Where(f => !f.Counted))
        {
            f.Counted = true; f.Won = f.HouseId == mine ? seated.Contains(f.CharacterId) : w.Magistrates.Any(m => m.HouseId == f.HouseId && m.OfficeId == f.RungId);
            if (f.Won && f.HouseId == mine && p != null && f.CharacterId != p.Id && w.Char(f.CharacterId) is { Alive: true } c) { var r = w.Relationship(c.Id, p.Id); r.Disposition = Math.Clamp(r.Disposition + CampaignWonRegard, -100, 100); }
        }
        w.Campaigns.RemoveAll(f => f.HouseId != mine || w.Turn - f.Turn > CampaignKept);
    }

    /// <summary>Whether a house puts nobody up for a rung at the count of this term: about one house in three does not.</summary>
    private static bool SitsOut(House h, RungDef rung, int term) => Hash(h.Id + ":stand:" + rung.Id + ":" + term) % 3 == 0;
    /// <summary>The term whose count comes next (or is held at the end of this year).</summary>
    private static int ComingTerm(World w, OfficeCatalog offices) => (w.Turn + CountIn(w, offices)) / Math.Max(1, offices.LadderEvery);

    /// <summary>The year before a count, each other house of the Reach that has five times the price of a canvass in its treasury
    /// and owes nothing pays for one, for the highest office it has somebody for: the largest it can afford five times over. An
    /// upright head does not buy a lavish one. It is told, so that it can be answered.</summary>
    private static void OthersCampaign(World w, OfficeCatalog offices)
    {
        if (NextIn(w, offices) != 1) return;
        string? mine = w.Protagonist?.HouseId; int term = (w.Turn + 1) / Math.Max(1, offices.LadderEvery); var said = new List<string>();
        foreach (var h in w.Houses.Values.Where(h => !World.Outsider(h.Id) && h.Id != mine && PolitySystem.InReach(w, h.Id) && h.Debt <= 0 && h.Treasury.Credits >= CampaignCost(Modest) * OthersCampaignAfford).OrderBy(h => h.Id, StringComparer.Ordinal).ToList())
        {
            var head = Economy.HeadOf(w, h); if (head?.Alive != true) continue;
            foreach (var rung in offices.Rungs.Reverse())
            {
                if (SitsOut(h, rung, term)) continue;
                var best = w.LivingCharacters().Where(c => c.HouseId == h.Id && Bar(w, offices, c, rung) == null && Next(w, offices, c)?.Id == rung.Id).OrderByDescending(Merit).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();
                if (best == null) continue;
                bool upright = head.NatureTraits.Contains("Honorable") || head.NatureTraits.Contains("Just");
                string size = Enumerable.Reverse(CampaignSizes).First(s => s == Modest || (h.Treasury.Credits >= CampaignCost(s) * OthersCampaignAfford && !(s == Lavish && upright) && !SenateActs.CanvassForbidden(w, s)));
                h.Treasury.Credits -= CampaignCost(size); w.WorldCounters[CampaignsKey] = w.Counter(CampaignsKey) + 1;
                w.Campaigns.Add(new CampaignFund { HouseId = h.Id, CharacterId = best.Id, RungId = rung.Id, Turn = w.Turn, Size = size, Spent = CampaignCost(size), Votes = CampaignVotes(size) });
                if (SenateActs.CanvassRecorded(w, size)) BoughtVotes(w, best, h.Id, rung.Id);
                said.Add($"{h.Name} puts {Money.Show(CampaignCost(size))} behind {best.Name} for {rung.Name.ToLowerInvariant()}");
                if (w.Candidacies.Any(cd => cd.RungId == rung.Id)) w.Tell("info", $"{h.Name} has put {Money.Credits(CampaignCost(size))} behind {best.Name}, who will stand for {rung.Name.ToLowerInvariant()} against your house", "senate:ladder");
                break;   // one canvass to a house
            }
        }
        if (said.Count > 0) w.Log($"Coin is spent on the coming election: {string.Join("; ", said)}.");
    }

    /// <summary>How the count for a rung would go if it were held today: who stands, and what each would have. What one of the
    /// house who keeps a house at the capital hears (docs/WEALTH.md, "Things worth a rich house's money").</summary>
    public static List<(Character who, int votes)> CountAsItStands(World w, OfficeCatalog offices, RungDef rung, Func<string, int> weight)
    {
        var candidates = Candidates(w, offices, rung); var tally = Tally(w, offices, rung, candidates, weight);
        return candidates.OrderByDescending(c => tally[c.Id]).ThenBy(c => c.Id, StringComparer.Ordinal).Select(c => (c, tally[c.Id])).ToList();
    }
}
