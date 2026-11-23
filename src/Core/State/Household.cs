namespace Praetoria.Core.State;

/// <summary>A standing duty the head has handed to a member of the house (docs/HOUSEHOLD.md): the steward runs the routine of the
/// estates and ventures, the marshal keeps the fleets fuelled and in repair. The head may do any of it personally at any time.</summary>
public sealed class Duty
{
    public string Role { get; set; } = "";
    public string HolderId { get; set; } = "";
    public int Since { get; set; }
    /// <summary>Credits the holder must leave in the treasury. They spend only what is above it.</summary>
    public int Reserve { get; set; } = 20;
    /// <summary>What the head has told a steward to favour (docs/PLAYTHROUGH.md, Medium, Money): "" as they judge; a resource
    /// (buildings that yield it come first); "ventures" (enlarging a venture before building); "hold" (begin nothing new).</summary>
    public string Favour { get; set; } = "";
    /// <summary>What they did last year, in a line.</summary>
    public string LastReport { get; set; } = "";
    /// <summary>What has gone missing under them. Not shown until it is found out.</summary>
    public int Skimmed { get; set; }
}

/// <summary>Someone asking to be taken into the house. The head answers; it lapses in two years.</summary>
public sealed class Petition
{
    public string PersonId { get; set; } = "";
    public string As { get; set; } = "";
    public int Since { get; set; }
}

/// <summary>What a person has said shall be done with what is theirs when they die (docs/WEALTH.md). The house itself goes by the
/// succession law, whatever this says.</summary>
public sealed class Will
{
    /// <summary>Someone other than the heir who is to have part of the purse, or empty.</summary>
    public string LegateeId { get; set; } = "";
    /// <summary>The part of the purse they are to have: 0, 25, 50 or 100.</summary>
    public int SharePct { get; set; }
    /// <summary>Smaller legacies out of the purse, to others than the heir and the one above: person → the part of the purse they are
    /// to have (a tenth or a quarter). With the share above it never comes to more than the whole.</summary>
    public Dictionary<string, int> Also { get; set; } = new();
    /// <summary>Heirloom id → the person it is left to. Anything not named goes to the heir.</summary>
    public Dictionary<string, string> Items { get; set; } = new();
}

/// <summary>A thing worth leaving to someone: a signet, a sword, a library, an estate. It belongs to a person, not to the house.</summary>
public sealed class Heirloom
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>signet, sword, library or estate, handed down; shrine, standard or plate, come by since (docs/WEALTH.md "New things").</summary>
    public string Kind { get; set; } = "";
    public string HolderId { get; set; } = "";
    /// <summary>The house it has always belonged to. Selling it out of that house is felt.</summary>
    public string OfHouseId { get; set; } = "";
    /// <summary>The house it came from, if it came from one: whose standard it was, or who sent the plate.</summary>
    public string FromHouseId { get; set; } = "";
    /// <summary>What it would fetch.</summary>
    public int Value { get; set; }
    /// <summary>Entailed: it goes with the house to whoever has it, and cannot be sold, given or willed away (docs/WEALTH.md).</summary>
    public bool Entailed { get; set; }
}

/// <summary>What one of the house is doing about what they want (docs/AMBITIONS.md). They pursue it themselves; now and then they
/// ask the head for something that would help.</summary>
public sealed class Pursuit
{
    /// <summary>The ambition this pursuit is for.</summary>
    public string Goal { get; set; } = "";
    /// <summary>0..100. At a hundred they have what they wanted.</summary>
    public int Progress { get; set; }
    public int Since { get; set; }
    public bool Fulfilled { get; set; }
    public int FulfilledTurn { get; set; } = -1;
    /// <summary>What they have asked the head for and not been answered: command, stake, word or heir. Empty when nothing is asked.</summary>
    public string Request { get; set; } = "";
    /// <summary>The fleet they want, or the sum.</summary>
    public string RequestArg { get; set; } = "";
    public int RequestSince { get; set; } = -1;
    public int LastAsked { get; set; } = -99;
    /// <summary>Times they have tried for it and failed: an election lost, a battle lost in command.</summary>
    public int Setbacks { get; set; }
    /// <summary>The relative who wants the same prize, while the two quarrel over it.</summary>
    public string RivalId { get; set; } = "";
    /// <summary>What the two of them quarrel over, as it was said: kept so that the loser can hold it against the winner.</summary>
    public string RivalPrize { get; set; } = "";
    /// <summary>The head has answered this request "not yet": it can be said once, and the wait begins again.</summary>
    public bool Deferred { get; set; }
    /// <summary>The prize the head told them to leave to another of the house: they quarrel over it no longer.</summary>
    public string StoodAside { get; set; } = "";
    /// <summary>Times the head helped, and times the head refused or did not answer.</summary>
    public int Helped { get; set; }
    public int Refused { get; set; }
    /// <summary>Credits of their own they have put into the house.</summary>
    public int Given { get; set; }
    /// <summary>What they did about it this year, in a line.</summary>
    public string Note { get; set; } = "";
}

/// <summary>One thing a person did, for the record of their life (docs/LIFE.md).</summary>
public sealed class Deed
{
    public int Turn { get; set; }
    public int Age { get; set; }
    public string Kind { get; set; } = "";
    public string Text { get; set; } = "";
}

/// <summary>The record of a life (docs/LIFE.md): what they did, how often they were wounded, and who has been their friend or their
/// enemy long enough for it to count.</summary>
public sealed class Life
{
    public List<Deed> Deeds { get; set; } = new();
    public int Wounds { get; set; }
    /// <summary>Things already noted once, so they are not noted again (a rank reached, a skill mastered, an office held).</summary>
    public HashSet<string> Marks { get; set; } = new();
    /// <summary>Person id → the turn they first thought well enough of this person to be called a friend.</summary>
    public Dictionary<string, int> Friends { get; set; } = new();
    /// <summary>Person id → the turn they first thought ill enough of this person to be called an enemy.</summary>
    public Dictionary<string, int> Enemies { get; set; } = new();
    public int RestedTurn { get; set; } = -1;
    /// <summary>What they did for the house last year as its elder, in a line (docs/LIFE.md).</summary>
    public string Counsel { get; set; } = "";
    /// <summary>What the head has asked of them as the elder this year: "peace", "ease" or "speak"; empty when nothing.</summary>
    public string Asked { get; set; } = "";
}

/// <summary>What was said when one of the house was buried.</summary>
public sealed class Funeral
{
    public int Turn { get; set; }
    public string PersonId { get; set; } = "";
    public string Name { get; set; } = "";
    public int Age { get; set; }
    public string Epithet { get; set; } = "";
    public List<string> Lines { get; set; } = new();
}

/// <summary>Someone of the player's house who will stand at the next election of the ladder (docs/LADDER.md).</summary>
public sealed class Candidacy
{
    public string RungId { get; set; } = "";
    public string CharacterId { get; set; } = "";
    /// <summary>What the head has put behind them: votes added at the count.</summary>
    public int Backing { get; set; }
}

/// <summary>Something a magistrate did that can be answered for after their term (docs/LADDER.md, "The courts"). Kept for some
/// years; heard once.</summary>
public sealed class OfficeDeed
{
    public string Id { get; set; } = "";
    public string CharacterId { get; set; } = "";
    public string HouseId { get; set; } = "";
    public string RungId { get; set; } = "";
    public int Turn { get; set; }
    /// <summary>"accounts" (money out of the accounts), "gift" (a gift taken to rule), "ruling" (found for a friend against the
    /// law), "bribe" (bought a praetor).</summary>
    public string What { get; set; } = "";
    /// <summary>The house wronged by it, if there was one.</summary>
    public string AgainstId { get; set; } = "";
    /// <summary>Removed for it at the time, or tried for it since.</summary>
    public bool Answered { get; set; }
}

/// <summary>A year's business for one who sits in an elected office (docs/LADDER.md, the work of office): the accounts for a
/// quaestor, the works for an aedile, a case for a praetor. The holder decides it; a holder who does not is thought the less of.</summary>
public sealed class OfficeBusiness
{
    /// <summary>What it is about (docs/LADDER.md, "The facts"): for a case the matter in dispute, for the accounts where the
    /// year's surplus came from.</summary>
    public string Matter { get; set; } = "";
    /// <summary>For a case, how plain the law is: 3 plain, 2 arguable, 1 doubtful. 0 on business made before there were facts.</summary>
    public int Weight { get; set; }
    /// <summary>What turns on it, in credits: a case's damages, the accounts' surplus.</summary>
    public int Stake { get; set; }
    public string Id { get; set; } = "";
    public string RungId { get; set; } = "";
    public string HolderId { get; set; } = "";
    /// <summary>"accounts", "works" or "case".</summary>
    public string Kind { get; set; } = "";
    public int Turn { get; set; }
    /// <summary>For a case: the two houses before the court, and which of them the law favours.</summary>
    public string HouseA { get; set; } = "";
    public string HouseB { get; set; } = "";
    public string RightId { get; set; } = "";
    /// <summary>The region the business concerns, if any.</summary>
    public string RegionId { get; set; } = "";
    /// <summary>What was decided, or "" while it waits.</summary>
    public string Choice { get; set; } = "";
    /// <summary>A case naming the player's house, held a year before it is heard so that the house may answer it (docs/PLAYTHROUGH.md 12).</summary>
    public bool Held { get; set; }
    /// <summary>Who of the player's house is to plead a held case, or "".</summary>
    public string AdvocateId { get; set; } = "";
    /// <summary>For a case: the house that brought it (docs/LADDER.md, "The docket"). The other house is the defendant. "" on a
    /// case made before suits had a plaintiff.</summary>
    public string PlaintiffId { get; set; } = "";
    /// <summary>The defendant has brought a counter-suit on the same matter: the stake was doubled when it did.</summary>
    public bool Countered { get; set; }
    /// <summary>For a case: the house the ruling went for. A ruling against the law may be appealed.</summary>
    public string WinnerId { get; set; } = "";
    /// <summary>For a judge's hearing: the estate on the other side. For an appeal: nothing.</summary>
    public string EstateId { get; set; } = "";
    /// <summary>For an appeal: the praetor whose ruling it is.</summary>
    public string SubjectId { get; set; } = "";
    /// <summary>An audit or an appeal has looked at this already.</summary>
    public bool Seen { get; set; }
}

/// <summary>One of a house's own court (docs/COURT.md): somebody who holds a small position in the household and goes where the house
/// goes, whatever rank it holds. Most are of the commons, sworn to the house and paid by it; a position may be held by one of the
/// family, and then <see cref="KinId"/> says who and the rest of this is theirs.</summary>
public sealed class Retainer
{
    public string Id { get; set; } = "";
    /// <summary>The position held (CourtSystem.Positions).</summary>
    public string Role { get; set; } = "";
    public string Name { get; set; } = "";
    public string Sex { get; set; } = "male";
    public int Age { get; set; } = 30;
    /// <summary>1 to 5. It grows a step every six years in the place.</summary>
    public int Skill { get; set; } = 1;
    public int Since { get; set; }
    /// <summary>Credits a year from the treasury. One of the family is paid nothing for it.</summary>
    public int Wage { get; set; }
    /// <summary>0 to 100: paid and kept, it rises; unpaid, it falls, and low enough they go.</summary>
    public int Loyalty { get; set; } = 40;
    /// <summary>Where they come from, in a line.</summary>
    public string From { get; set; } = "";
    /// <summary>One of the family who holds the position, or "" for one of the commons.</summary>
    public string KinId { get; set; } = "";
    public int UnpaidYears { get; set; }
}

