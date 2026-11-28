using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;

namespace Praetoria.Core.Presentation;

/// <summary>
/// One thing the player should not have to find by looking (docs/VISUAL_SYSTEM.md 3.5): what kind of thing it is, how much it
/// presses, the sentence, and where a click on it goes ("senate:duties" opens the Senate on its duties tab). Alerts of one kind
/// stack into one badge in the alerts row under the top bar; the severity sets the badge's rim (ember for danger, brass for a
/// decision, steel for information) and its order. The sentence is the badge's tooltip.
/// </summary>
public sealed record Alert(string Kind, string Severity, string Text, string Go);

/// <summary>
/// The alerts of the player's house this year: read only, gathered from what the world already holds (the advisors, the treasury,
/// the battles, the flags the systems set, the news, the courts, the Senate, the family). Moved here from the outliner so each alert
/// carries a kind and a severity the client draws from, and so what is alerted is tested headless.
/// </summary>
public static class Alerts
{
    /// <summary>Something is wrong or about to go wrong: a loss, a threat, an empty chair.</summary>
    public const string Danger = "danger";
    /// <summary>Something waits on the player's decision: a vote undeclared, a suit to answer, a duty to do.</summary>
    public const string Decision = "decision";
    /// <summary>Neutral news.</summary>
    public const string Info = "info";
    /// <summary>Good news.</summary>
    public const string Good = "good";
    /// <summary>The severities, the most pressing first: the order of the alerts row.</summary>
    public static readonly string[] Severities = { Danger, Decision, Info, Good };

    /// <summary>The kinds. Each is one badge; the client gives each its glyph (docs/VISUAL_SYSTEM.md 8.6).</summary>
    public const string Governor = "governor", Health = "health", Vote = "vote", Fleet = "fleet", Office = "office", Duty = "duty",
        War = "war", Beyond = "beyond", Court = "court", Money = "money", Exchange = "exchange", Family = "family", Household = "household",
        Holdings = "holdings", Estates = "estates", Council = "council", Military = "military", Houses = "houses", Crisis = "crisis", News = "news";
    public static readonly string[] Kinds = { Governor, Health, Vote, Fleet, Office, Duty, War, Beyond, Court, Money, Exchange, Family, Household, Holdings, Estates, Council, Military, Houses, Crisis, News };

    /// <summary>How the kind is named in the badge's tooltip head.</summary>
    public static string Title(string kind) => kind switch
    {
        Governor => "Governors", Health => "Health", Vote => "The Senate's votes", Fleet => "Fleets", Office => "Offices and rank", Duty => "Duties this year",
        War => "War", Beyond => "Beyond the Reach", Court => "The courts", Money => "Money", Exchange => "The Exchange", Family => "The family",
        Household => "The household", Holdings => "Worlds and provinces", Estates => "The estates", Council => "The council", Military => "The general",
        Houses => "The houses", Crisis => "Crises", _ => "News",
    };

    /// <summary>The rank of a severity: 0 is the most pressing.</summary>
    public static int Rank(string severity) => Array.IndexOf(Severities, severity) is var i and >= 0 ? i : Severities.Length;

    /// <summary>The most pressing severity among some alerts (a stacked badge takes its rim from it).</summary>
    public static string Worst(IEnumerable<Alert> alerts) => alerts.Select(a => a.Severity).OrderBy(Rank).FirstOrDefault() ?? Info;

    /// <summary>A news line of the year (World.NewsNow) as an alert: its kind by the window it opens, its severity by its tone. A warning
    /// about someone of the house who is ill is a matter of health.</summary>
    public static Alert FromNews(World w, NewsItem n)
    {
        string t = n.Target ?? "", go = n.SubjectId.Length > 0 && t == "dynasty" ? "dynasty:tree:" + n.SubjectId : n.SubjectId.Length > 0 && t == "houses" ? "houses:" + n.SubjectId : t;
        string kind = t.StartsWith("dynasty") || t.StartsWith("you") || t.StartsWith("character") ? Family
            : t.StartsWith("house:household") || t.StartsWith("house:court") ? Household
            : t.StartsWith("military") || t.StartsWith("fleet") || t.StartsWith("galaxy") ? Fleet
            : t.StartsWith("senate:ladder") ? Court
            : t.StartsWith("senate") ? Vote
            : t.StartsWith("laws") ? Estates
            : t.StartsWith("exchange") ? Exchange
            : t.StartsWith("houses") ? Houses
            : t.StartsWith("house") ? Money : News;
        if (n.Tone == "warn" && n.SubjectId.Length > 0 && w.Char(n.SubjectId) is { Alive: true } c && c.Flags.Contains(LifeSystem.IllChar) && c.HouseId == w.Protagonist?.HouseId) kind = Health;
        return new Alert(kind, n.Tone == "warn" ? Danger : n.Tone == "good" ? Good : Info, Plain(n.Text), go);
    }

    /// <summary>Every alert of the player's house now, the most pressing first (a stable order within a severity: as gathered).</summary>
    public static List<Alert> For(World w, ContentDatabase content, CourtModel m, ProgressionSystem? progression)
    {
        var list = new List<Alert>();
        if (w.Protagonist is not { } me || w.House(me.HouseId) is not { } house) return list;
        void Add(string kind, string severity, string text, string? go) => list.Add(new Alert(kind, severity, Plain(text), go ?? "office"));

        /* the council is not on the screen all the time (the owner, 2026-11-22): a seat speaks here only when it warns; all six, with
           who holds each and what they did, are under House, the Household */
        foreach (var seat in m.Advisors.Where(a => a.Urgent)) Add(Council, Danger, seat.Seat + ": " + seat.Line.Split(". ")[0].TrimEnd('.'), "house:council");
        if (m.Treasury.Insolvent) Add(Money, Danger, $"Insolvent: {Praetoria.Core.Money.Held(house.Treasury)} credits" + (me.Purse > 0 ? $". Your own purse holds {Praetoria.Core.Money.Show(me.Purse)}: it can be put into the treasury" : ""), me.Purse > 0 ? "house:purse" : "house:ledger");
        foreach (var bt in w.Battles.Where(b => b.Turn == w.Turn - 1 && b.Involves(me.HouseId)).Take(3))
            Add(War, bt.WinnerId == me.HouseId ? Good : Danger, $"{(bt.WinnerId == me.HouseId ? "Won" : "Lost")}: the battle of {bt.Place} ({Counted.Hulls(bt.AttackerHulls)} against {bt.DefenderHulls})", "military:battles");
        foreach (var ask in w.ServiceAsks.Where(a => a.Turn >= w.Turn - 1).Take(2)) Add(Military, Decision, ServiceSystem.AskSays(w, ask).TrimEnd('.'), "military:service");   /* a command by service, asked of the head (docs/MILITARY.md) */
        // what a player should not have to find by looking (docs/PLAYTHROUGH.md, "New alerts where there are none")
        if (house.Treasury.Manpower <= 0 && m.Fleets.Count + m.Holdings.Count > 0) Add(Money, Danger, "Manpower is at nought: no new hull can be crewed and no levy raised until it recovers", "house:ledger");
        foreach (var ng in w.HoldingsOf(house.Id).Where(x => x.IsPlanet && w.Char(x.GovernorId) is not { Alive: true }).Take(2)) Add(Governor, Danger, ng.Name + " has no governor: name one, or it goes ungoverned", "galaxy:" + ng.SystemId);
        if (m.Standing is { NextTitle: not null } st && st.Paths.FirstOrDefault(pp => pp.Available) is { } open1) Add(Office, Decision, "You could rise to " + st.NextTitle + " now: " + open1.Label.ToLowerInvariant(), "senate:offices");
        foreach (var (wh, wi) in w.Holdings.Values.Where(x => x.OwnerId == house.Id).SelectMany(x => x.Provinces.Select((pv, i) => (x, i))).Where(t => t.x.Provinces[t.i].Withheld && t.x.Provinces[t.i].HolderId != house.Id).Take(2))
            Add(Holdings, Danger, (w.House(wh.Provinces[wi].HolderId)?.Name ?? "A house") + " is withholding the dues of " + wh.Provinces[wi].Name, "galaxy:" + wh.SystemId);
        foreach (var nw in w.NewsNow().Reverse().Take(8)) list.Add(FromNews(w, nw));
        foreach (var su in LadderSystem.SuitsNaming(w, house.Id))
            Add(Court, su.RightId == house.Id ? Info : Danger, (LadderSystem.PlaintiffOf(w, su) == house.Id ? $"Your suit against {w.House(LadderSystem.OtherIn(su, house.Id))?.Name}" : $"{w.House(LadderSystem.OtherIn(su, house.Id))?.Name} sues your house") + $" over {LadderSystem.MatterOf(su)}: {Praetoria.Core.Money.Credits(LadderSystem.StakeOf(su))}, heard {LadderSystem.HeardSays(w, su.Turn)}", "senate:ladder");
        foreach (var ch in LadderSystem.ChargesOn(w, house.Id)) Add(Court, Danger, $"{w.House(ch.AccuserId)?.Name} charges {w.Char(ch.AccusedId)?.Name}: heard {LadderSystem.HeardSays(w, ch.Turn)}" + (LadderSystem.PleaderOf(w, ch) == null ? ", and nobody is to answer it" : ""), "senate:ladder");
        foreach (var jr in w.Judgments.Where(j => j.Turn == w.Turn - 1 && !(j.Kind == "charge" && j.Judge.Length == 0)))
            Add(Court, jr.WinnerId == house.Id ? Good : Danger, jr.WinnerId == house.Id ? $"The court found for you: {w.House(jr.LoserId)?.Name} paid {Praetoria.Core.Money.Credits(jr.Sum)}" : $"The court found against you: you paid {w.House(jr.WinnerId)?.Name} {Praetoria.Core.Money.Credits(jr.Sum)}", "senate:ladder");
        foreach (var war in m.Wars) Add(War, Danger, $"At war with {war.EnemyName}{(war.TheyAreSuing ? ": they sue for peace" : war.TheyOfferTerms ? ": they have sent their terms" : "")}", "military");
        foreach (var sg in m.Sieges.Where(s => s.Ours)) Add(War, Danger, $"{sg.HoldingName} besieged by {sg.BesiegerName} ({sg.Turns}/{sg.Required})", "galaxy:" + (w.Holdings.Values.FirstOrDefault(x => x.Name == sg.HoldingName)?.SystemId ?? ""));
        foreach (var h in m.Household.Where(h => h.Actions.Any(a => a.Id == "defiant"))) Add(Household, Danger, $"{h.Name} is defiant", "house:household");
        if (m.Character.Regency) Add(Family, Info, "A regent rules in your name", "dynasty");
        if (w.HasFlag("pirates_near")) Add(Fleet, Danger, "Raiders within a lane of your worlds", "military:near");
        if (w.HasFlag("raided")) Add(Fleet, Danger, "Your shipping was plundered", "military:near");
        if (w.HasFlag("fuel_short")) Add(Fleet, Danger, "The tanks are dry: the fleet cannot sail until helium-3 is bought or skimmed" + (me.Purse > 0 && house.Treasury.Credits < 6 ? $" (your own purse holds {Praetoria.Core.Money.Show(me.Purse)})" : ""), "exchange:goods");
        string NationName(string prefix) => w.WorldFlags.Where(f => f.StartsWith(prefix)).Select(f => content.Nations.Def(f[prefix.Length..])?.Name ?? f[prefix.Length..]).FirstOrDefault() ?? "a nation beyond the Reach";
        if (w.HasFlag("foreign_war_declared")) Add(Beyond, Danger, $"The Senate has made war on {NationName("foreign_war:")}: fleets may be sent", "military:abroad");
        else if (w.HasFlag("foreign_war") && NationSystem.AtWar(w) is { WarOf.Length: > 0 } own)
            Add(Beyond, own.Defied ? Danger : Info, own.WarOf == me.HouseId ? $"Your house is at war with {NationName("foreign_war:")} on its own account{(own.Defied ? ", against a peace the Senate voted" : ": the Senate voted nothing")}" : $"{w.House(own.WarOf)?.Name ?? own.WarOf} is at war with {NationName("foreign_war:")} on its own account{(own.Defied ? ", against a peace the Senate voted" : "")}: no other house is", "military:abroad");
        else if (w.HasFlag("foreign_war")) Add(Beyond, Info, $"The Reach is at war with {NationName("foreign_war:")}{(w.HasFlag("expedition_away") ? ": your fleet is in their space" : ": send a fleet, or let others win the triumph")}", "military:abroad");
        if (w.HasFlag("peace_awaits_us")) Add(Beyond, Decision, "The Senate has voted peace beyond the Reach, and waits on your answer: come home, or fight on", "military:abroad");
        if (w.HasFlag("expedition_victory")) Add(Beyond, Good, "Your fleet beat them in their own space", "military:abroad");
        if (w.HasFlag("expedition_defeat")) Add(Beyond, Danger, "Your fleet was beaten beyond the Reach", "military:abroad");
        if (w.HasFlag("expedition_lost")) Add(Beyond, Danger, "Your expedition is broken and limping home", "military:abroad");
        if (w.HasFlag("expedition_returned")) Add(Beyond, Good, "Your fleet is home from beyond the Reach", "military:abroad");
        if (w.HasFlag("triumph")) Add(Beyond, Good, $"The triumph is yours: {NationName("nation_subdued:")} pays tribute", "military:abroad");
        else if (w.HasFlag("nation_subdued")) Add(Beyond, Good, $"{NationName("nation_subdued:")} sues for peace and pays tribute", "military:abroad");
        if (w.HasFlag("war_lost_abroad")) Add(Beyond, Danger, "The war abroad is lost: the chair buys peace", "military:abroad");
        if (w.HasFlag("raided_by_nation")) Add(Beyond, Danger, $"Raiders of {NationName("raided_by:")} fell on your world", "military:abroad");
        if (w.HasFlag("nation_freed")) Add(Beyond, Info, "A tributary's treaty is done; its tribute stops", "military:abroad");
        foreach (var f in w.WorldFlags.Where(f => f.StartsWith("price_spike:")).Take(2)) { var gd = content.Market.Good(f["price_spike:".Length..]); var gs = w.Market?.Good(gd?.Id ?? ""); if (gd != null && gs != null) Add(Exchange, Info, $"{gd.Name} dear on the Exchange: {gs.Cause}", "exchange:goods"); }
        if (w.HasFlag("market_crash")) Add(Exchange, Danger, "The Exchange has fallen a tenth in a year", "exchange:shares");
        if (w.HasFlag(VentureSystem.FailingFlag)) Add(Exchange, Danger, "A venture of yours has gone two years without a profit: one more and it is wound up", "exchange:ventures");
        if (w.HasFlag(VentureSystem.FailedFlag)) Add(Exchange, Danger, "A venture of yours has failed and been wound up", "exchange:ventures");
        if (w.HasFlag("takeover_of_mine")) Add(Exchange, Danger, "Another house has taken control of a company you directed", "exchange:shares");
        if (w.HasFlag("stake_in_mine")) Add(Exchange, Info, "A rival holds a tenth or more of a company you direct", "exchange:shares");
        if (w.HasFlag("dividend_cut")) Add(Exchange, Info, "A company you hold has passed its dividend", "exchange:shares");
        if (w.HasFlag("bonds_offered")) Add(Exchange, Info, "The Reach is selling war bonds", "exchange:shares");
        if (w.HasFlag("shares_seized_mine")) Add(Exchange, Danger, "The throne has declared your shares forfeit", "exchange:shares");
        if (w.HasFlag("venture_offered") && (m.Assets?.Offers.Count ?? 1) > 0) Add(Exchange, Decision, "A venture is offered to you: the offer stands for the year", "exchange:ventures");
        if (w.HasFlag("project_stalled")) Add(Exchange, Danger, "A building site has stalled: the year's instalment was not met", "exchange:projects");
        if (w.HasFlag("project_done")) Add(Exchange, Good, "A project of yours is finished", "exchange:projects");
        if (w.HasFlag("first_refusal_taken")) Add(Exchange, Info, "The world's holder matched your price and took the venture first", "exchange:ventures");
        // an alert goes when the thing is done (docs/PLAYTHROUGH.md): these stand only while a motion is open that you have not declared on
        var undeclared = w.Motions.Where(x => x.Open && !x.Votes.ContainsKey(house.Id)).ToList();
        if (undeclared.Any(x => x.TargetId == house.Id && x.ProposerHouseId != house.Id)) Add(Vote, Decision, "A motion against your house is before the Senate, and you have not declared", "senate:chamber");
        else if (undeclared.Any(SenateSystem.IsPetition)) Add(Vote, Decision, "An estate petitions the Senate, and you have not declared: its regard in every house rides on the vote", "senate:chamber");
        else if (undeclared.Any(x => x.Kind == "act")) Add(Vote, Decision, "An Act of the Senate is before the chamber, and you have not declared: the law of the Reach rides on it", "senate:chamber");
        else if (undeclared.Count > 0) Add(Vote, Decision, undeclared.Count == 1 ? "A motion is before the Senate, and you have not declared" : undeclared.Count + " motions are before the Senate that you have not declared on", "senate:chamber");
        if (w.Motions.Any(x => x.Open && x.ProposedTurn < w.Turn && !x.Votes.ContainsKey(me.HouseId))) Add(Vote, Decision, "A motion is decided at the end of this turn and your vote is undeclared", "senate:chamber");
        if (w.HasFlag("election_near")) Add(Vote, Decision, "The consuls are elected at the turn's end: stand, or see who the parties put up", "senate:consuls");
        if (w.HasFlag("consul_elected")) Add(Vote, Good, "You are elected consul", "senate:consuls");
        if (w.HasFlag("consul_lost")) Add(Vote, Info, "Your consulship has ended", "senate:consuls");
        if (w.HasFlag("vetoed_mine")) Add(Vote, Danger, "A consul has vetoed your motion", "senate:chamber");
        if (w.HasFlag("petition_refused")) Add(Estates, Danger, "The Senate turned a petition away: the estate's worlds stir", "laws:estates");
        if (w.HasFlag("petition_carried")) Add(Estates, Good, "The Senate heard a petition: the estate's regard rises", "laws:estates");
        if (w.HasFlag("embargoed")) Add(Exchange, Danger, "The Senate's embargo closes the exchanges to you", "senate:chamber");
        if (w.HasFlag("office_granted")) Add(Office, Good, "The Senate has commissioned you to an office", "senate:offices");
        if (w.HasFlag("office_lost")) Add(Office, Danger, w.HasFlag("treason") ? "Stripped of office for treason" : "Relieved of office by the Senate", "senate:offices");
        foreach (var sd in w.SeatDemands.Where(x => x.AgainstId == me.HouseId))
            Add(Holdings, Decision, (w.House(sd.ById)?.Name ?? "A house") + " asks you to give up " + (w.Holding(sd.HoldingId) is { } sh ? ProvinceSystem.What(sh, sd.Province) : "a seat") + ": step down, or stand. To stand, or to leave it unanswered, is war with them", "galaxy:" + (w.Holding(sd.HoldingId)?.SystemId ?? ""));
        foreach (var (ph, pi) in w.ProvincesOf(me.HouseId).Where(x => x.i < x.h.Provinces.Count && x.h.Provinces[x.i].Dues >= ProvinceSystem.Heavy && !x.h.Provinces[x.i].Withheld).Take(1))
            Add(Holdings, Info, (w.House(ph.OwnerId)?.Name ?? "A house") + " asks heavy dues of " + ph.Provinces[pi].Name, "galaxy:" + ph.SystemId);
        foreach (var f in w.WorldFlags.Where(f => f.StartsWith("office_vacant:")).Take(1)) Add(Office, Info, "An office of the Reach stands vacant", "senate:offices");
        if (w.HasFlag("faction_against_me")) Add(Vote, Danger, "A bench's leader has moved against you: its houses lean the way their leader votes", "senate:chamber");
        if (w.HasFlag("faction_formed")) Add(Vote, Info, "A bench has formed with you on it", "senate:chamber");
        if (w.HasFlag("faction_dissolved")) Add(Vote, Info, "Your bench has broken", "senate:chamber");
        foreach (var f in w.WorldFlags.Where(f => f.StartsWith("estate_hostile:")).Take(2)) Add(Estates, Danger, $"{content.Policies.Estate(f["estate_hostile:".Length..])?.Name ?? f} are hostile to the house", "laws:estates");
        if (w.HasFlag("governor_revolt")) Add(Governor, Danger, "A governor has declared a world for themselves", "worlds");
        if (w.HasFlag("freehold_of_ours")) Add(Holdings, Info, "A world of yours is a free hold: buy it back or take it", "galaxy:" + (m.Reclaims?.FirstOrDefault() is { } fr ? w.Holding(fr.HoldingId)?.SystemId ?? "" : ""));
        if (w.HasFlag("capital_taken")) Add(War, Good, "You hold the capital", "senate");
        // the military depth (docs/MILITARY.md): battles about to be joined, crews and supply, the campaign, the Achilles name
        foreach (var sys in w.Fleets.Values.Where(f => f.OwnerId == house.Id && !f.Moving).Select(f => f.SystemId).Distinct().Where(sid => w.Fleets.Values.Any(e => !e.Moving && e.SystemId == sid && e.OwnerId != house.Id && w.AtWar(house.Id, e.OwnerId))).Take(2))
        {
            var enemy = w.Fleets.Values.First(e => !e.Moving && e.SystemId == sys && e.OwnerId != house.Id && w.AtWar(house.Id, e.OwnerId));
            Add(War, Danger, $"Battle at {w.Galaxy?.System(sys)?.Name ?? sys} at the turn's end: {w.House(enemy.OwnerId)?.Name ?? "the enemy"} in the sky; set your stance", "galaxy:" + sys);
        }
        if (w.HasFlag("fleet_mutinous")) Add(Fleet, Danger, "A fleet has mutinied: hulls are deserting", "military");
        else if (w.HasFlag("fleet_restive")) Add(Fleet, Danger, "A fleet's crews are restive and will not sail", "military");
        if (w.HasFlag("out_of_supply")) Add(Fleet, Danger, "A fleet is out of supply: hulls will desert", "military");
        else if (w.HasFlag("supply_low")) Add(Fleet, Info, "A fleet is running short of supply", "military");
        if (w.HasFlag("campaign_over")) Add(Fleet, Info, "The campaign is over: the fleet is recalled", "military");
        if (w.HasFlag("became_achilles")) Add(Military, Good, "The Reach speaks your name as it speaks of Achilles", "military");
        if (w.HasFlag("hulls_captured")) Add(Fleet, Good, "Prize crews have taken enemy hulls", "military");
        if (w.HasFlag("capital_lost")) Add(War, Danger, "The capital is lost", "senate");
        if (w.HasFlag("rebellion")) Add(Holdings, Danger, "A world of yours is in revolt", "galaxy:" + (w.HoldingsOf(house.Id).FirstOrDefault(x => x.InRebellion)?.SystemId ?? ""));
        // the next rank is within reach: said the year it becomes so, and while it stays so (docs/PLAYTHROUGH.md, Medium, The Senate)
        if (progression is { } prog && prog.NextTitle(w, house.Id) is { } nextRank && prog.OpenRoads(w, w.ProtagonistId) is { Count: > 0 } roads) Add(Office, Decision, RankWithinReach(nextRank.NameIn(w.Era), roads, prog.Voted(nextRank)), "senate");
        if (w.HasFlag("fief_granted")) Add(Holdings, Good, w.IsImperium ? "You have been granted a fief" : "You have been entrusted with a world", "worlds");
        foreach (var f in w.WorldFlags.Where(f => f.StartsWith("house_fell:"))) Add(Houses, Info, $"{f["house_fell:".Length..]} has passed out of the rolls", "houses");
        if (m.GreatCrisis is { Stage: not null, Resolved: false } gc) Add(Crisis, Info, $"{gc.Name}: {gc.Stage}", "houses");
        if (!w.Characters.Values.Any(c => c.Alive && c.HouseId == house.Id && c.Id != w.ProtagonistId)) Add(Family, Danger, "No heir: the line hangs by a thread", "dynasty");
        foreach (var su in w.Courtships.Where(x => x.Incoming)) Add(Family, Decision, $"{w.Char(su.SuitorId)?.Name} asks for {w.Char(su.TargetId)?.Name}'s hand: answer it", "dynasty:marriages");
        foreach (var su in w.Courtships.Where(x => !x.Incoming && !x.Betrothed && x.SuitorId == w.ProtagonistId && x.RivalId.Length > 0 && x.RivalStanding > CourtshipSystem.Standing(w, x))) Add(Family, Danger, $"A rival is ahead in the suit for {w.Char(su.TargetId)?.Name}", "dynasty:marriages");
        if (w.Courtships.Any(x => !x.Incoming && !x.Betrothed && x.SuitorId == w.ProtagonistId && x.ActedTurn < w.Turn)) Add(Family, Decision, "Your suit waits on this year's attention", "dynasty:marriages");
        foreach (var su in w.Courtships.Where(x => !x.Incoming && x.Own && x.SuitorId != w.ProtagonistId && x.AskingSince >= 0 && !x.Forbidden)) Add(Family, Decision, $"{w.Char(su.SuitorId)?.Name} asks your leave to marry {w.Char(su.TargetId)?.Name}", "dynasty:marriages");
        if (w.HasFlag(EducationSystem.ResistsFlag)) Add(Family, Info, "A child of the house is set against their bent", "dynasty:upbringing");
        foreach (var ask in AmbitionSystem.Members(w, house).Where(x => x.Pursuit is { Request.Length: > 0 })) Add(Family, Decision, $"{ask.Name} {AmbitionSystem.RequestSays(w, ask)}", "dynasty:ambitions");
        if (LadderSystem.DutyOf(w, me.Id, LadderSystem.Hearing, LadderSystem.Appeal) is { } benchWork)
            Add(Duty, Decision, benchWork.Kind == LadderSystem.Appeal ? "An appeal waits before your bench" : "A hearing waits before your bench", "senate:duties");
        if (LadderSystem.DutyOf(w, me.Id, LadderSystem.Charge) is { } chargeWork)
            Add(Duty, Decision, "The year's charge of your office waits on you: " + (content.Offices.Def(chargeWork.RungId)?.Name ?? "an office of the Reach"), "senate:duties");
        if (LadderSystem.DutyOf(w, me.Id, LadderSystem.Desk, LadderSystem.Posting, LadderSystem.Brief, LadderSystem.Ledgers, LadderSystem.Watch) is { } rankWork)
            Add(Duty, Info, rankWork.Kind switch { LadderSystem.Desk => "The year's returns are on your desk", LadderSystem.Posting => "You have a year ashore to spend", LadderSystem.Brief => "A brief has come to you", LadderSystem.Ledgers => "The year's ledgers are yours to keep", _ => "Your year as a junior officer is yours to spend" }, "senate:duties");
        if (LadderSystem.DutyOf(w, me.Id, LadderSystem.SeatKind) != null) Add(Duty, Info, "Your year in the Senate is not yet spent", "senate:duties");
        if (LadderSystem.BusinessOf(w, me.Id) is { } officeWork)
            Add(Duty, Decision, officeWork.Kind == LadderSystem.Case ? "A case waits for your ruling as praetor" : officeWork.Kind == LadderSystem.Works ? "The works wait on you as aedile" : "The year's accounts wait on you as quaestor", "senate:ladder");
        if (LadderSystem.Active(w, content.Offices) && w.Emergency == null && LadderSystem.CountIn(w, content.Offices) <= 2 && !w.Candidacies.Any(x => x.CharacterId == me.Id) && LadderSystem.Next(w, content.Offices, me) is { } nextRung)
            Add(Office, Info, $"You could stand for {nextRung.Name.ToLowerInvariant()}: the election is {(LadderSystem.CountIn(w, content.Offices) is var cin && cin == 0 ? "at the end of this year" : "in " + Counted.Years(cin))}", "senate:ladder");
        if (w.HasFlag(PolitySystem.WarFlag)) Add(War, Danger, "Your state is at war for its independence: hold the region", "senate:orders");
        else if (w.HasFlag(PolitySystem.RefuseFlag)) Add(War, Decision, "You fought the one who now holds the throne, and you hold a region: you need not kneel", "senate:orders");
        if (TriumphSystem.Waiting(w, me.Id) != null) Add(Military, Decision, "The Senate has voted you a triumph: come home for it, or decline it and keep the fleet", "military");
        if (w.Recalls.FirstOrDefault(r => r.HouseId == me.HouseId) is { } recall)
            Add(Military, Decision, $"The Senate has ordered {(recall.CharacterId == w.ProtagonistId ? "you" : w.Char(recall.CharacterId)?.Name)} home by year {recall.Deadline}: answer it", "military");
        if (w.CivilWar is { } civil)
        {
            if (civil.HouseId == me.HouseId) Add(War, Danger, "Your house has crossed the line: hold the capital's orbit or break the Senate's fleets", "military");
            else if (!civil.Declared.ContainsKey(me.HouseId)) Add(War, Decision, $"{w.Char(civil.GeneralId)?.Name} marches on the capital: declare for a side, or stand aside", "military");
        }
        else if (w.HasFlag(CaesarSystem.GreatNameFlag) && w.Characters.Values.FirstOrDefault(c => c.Alive && c.Flags.Contains(CaesarSystem.GreatNameChar)) is { } rising)
            Add(Military, Danger, $"{rising.Name} of {w.House(rising.HouseId)?.Name} is a commander of the first rank, and loved: the Reach has few of these", "military");
        else if (w.HasFlag(CaesarSystem.LaurelsFlag)) Add(Military, Info, "The house's general has the victories for a triumph: move it in the Senate", "senate:move");
        if (me.Health < LifeSystem.Sound) Add(Health, Danger, me.Health < LifeSystem.Frail ? "Your health is failing" : "You are ailing", "you");
        if (me.Flags.Contains(LifeSystem.IllChar)) Add(Health, Danger, "You are ill", "you:life");
        if (w.HasFlag(WealthSystem.DebtFlag)) Add(Money, Danger, "One of the house is in debt", "house:purse");
        foreach (var pe in house.Petitions) Add(Household, Decision, $"{w.Char(pe.PersonId)?.Name} asks for a place in your household", "house:household");
        if (house.Duties.Any(du => w.Char(du.HolderId)?.Flags.Contains(HouseholdSystem.SkimmingChar) == true)) Add(Household, Danger, "The steward's accounts do not add up", "house:household");
        if (house.Court.Any(r => r.UnpaidYears > 0)) Add(Household, Danger, "The household's wages are unpaid", "house:court");
        if (w.MarriageOf(w.ProtagonistId) is { Estranged: true }) Add(Family, Danger, "You and your spouse live apart", "dynasty:marriages");
        if (m.Crises != null) foreach (var c in m.Crises.Active) Add(Crisis, Danger, $"Crisis: {c.Name} (severity {c.Severity})", "senate:offices");
        return list.Select((a, i) => (a, i)).OrderBy(x => Rank(x.a.Severity)).ThenBy(x => x.i).Select(x => x.a).ToList();
    }

    /// <summary>"The rank of Legate is within reach: by petition, the house's standing being enough, or by force, the house having the ships"</summary>
    public static string RankWithinReach(string rank, List<string> roads, bool voted = false)
    {
        var says = roads.Select(r => r == "petition" ? (voted ? "by petition to the Senate, which votes on it: the house has the standing, and the land or the seats" : "by petition, the house's standing being enough") : r == "claim" ? "by the claim the house holds" : "by force, the house having the ships");
        return "The rank of " + rank + " is within reach: " + string.Join(", or ", says);
    }

    /// <summary>The owner's style for anything a player reads (docs/VISUAL_SYSTEM.md 1.9): no em dashes.</summary>
    public static string Plain(string s) => s.Replace(" \u2014 ", ": ").Replace("\u2014", ", ");
}
