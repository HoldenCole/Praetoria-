using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// Struck from the line, cast out, and the undutiful will (docs/HOUSEHOLD.md; docs/IDEAS_FROM_MODS.md, entry 3; docs/GAPS.md R18).
/// <para>The head of a house may STRIKE a grown relative of its blood out of the line of succession (they stay of the house), or CAST
/// them OUT of the house altogether. Either costs the house legitimacy and the kin's regard, unless the record holds a cause against
/// the one struck: they defied the head, married without the head's leave, left the house for a rival, refused the field when called
/// out over a death, or were put out of office for what they did in it. Either can be undone at the price it cost. A named heir
/// cannot be struck out.</para>
/// <para>The one struck keeps a grievance, through rules that exist: unless they are loyal, what they want becomes their place in the
/// line (the seat of the house), which makes them wilful, so that they grow defiant, warm to the house's rivals, may marry against the
/// head's wish and may leave for a rival house; one cast out goes to their husband's or wife's house, their patron's, the house of a
/// parent born elsewhere, or alone as the first of a new name, takes their purse, and means to outshine the house that cast them out.</para>
/// <para>At the head's death a child they struck or cast out with no cause on the record, and whom neither the will nor the house's
/// custom leaves anything, may bring the SUIT OF THE UNDUTIFUL WILL before a praetor of another house, on the house's docket: heard at
/// the end of the year after, it gives a child who wins a quarter of the dead head's purse and a part of their heirlooms, and puts one
/// struck out back in the line. One cast out stays out, and may still be taken back.</para>
/// <para>Other houses' heads strike out or cast out a relative for the plain causes the record holds for any house. RNG-free.</para>
/// </summary>
public static class DisownSystem
{
    public const string Struck = "struck", CastOut = "cast_out";
    public const string StruckChar = "struck_from_the_line", CastOutChar = "cast_out";
    public const int Adult = DynastySystem.AgeOfMajority;
    // what it costs: the house's legitimacy, the regard of each of the kin, and the regard of the one struck (less when there was cause)
    public const int StrikeLegitimacy = 3, StrikeKin = 5, StrikeSelf = 25, StrikeSelfWithCause = 15;
    public const int CastLegitimacy = 5, CastKin = 10, CastSelf = 40, CastSelfWithCause = 25;
    /// <summary>What the one restored or taken back thinks the better of the head; and how ill one cast out may think of the head and still come back.</summary>
    public const int RestoreSelf = 15, TakeBackSelf = 20, ComeBackAt = -60;
    /// <summary>How long defiance, a marriage without leave, or a removal from office stays a cause on the record.</summary>
    public const int CauseYears = 10;
    /// <summary>The legitimacy of a new name founded by one cast out alone.</summary>
    public const int NewNameLegitimacy = 5;
    // the undutiful will
    public const int SharePct = 25, ItemsOneIn = 4, UndutifulLegitimacy = 2, WonRegard = 10, LostRegard = 10, SettledRegard = 10, SueUnlessRegard = 30, FriendAt = 30, KeptYears = 2;
    public const int PleadBase = 55, PleadFor = 6, PleadAgainst = 4;
    // other houses
    public const int OthersOneIn = 2, FreshYears = 2;
    // deeds, for content: raised the year it happens, cleared the year after
    public const string StruckFlag = "kin_struck_from_the_line", CastFlag = "kin_cast_out", SuitFlag = "undutiful_will_brought", UndutifulFlag = "will_found_undutiful", UpheldFlag = "will_upheld";
    private static readonly string[] DeedFlags = { StruckFlag, CastFlag, SuitFlag, UndutifulFlag, UpheldFlag };
    // counters
    public const string StruckKey = "struck_from_lines", CastKey = "cast_out_of_houses", StruckByOthersKey = "struck_by_other_houses", CastByOthersKey = "cast_out_by_other_houses",
        RestoredKey = "restored_to_lines", TakenBackKey = "taken_back_into_houses", NewNamesKey = "new_names_founded", SuitsKey = "undutiful_will_suits", SuitsWonKey = "undutiful_will_won", SuitsSettledKey = "undutiful_will_settled";
    public const string Upheld = "upheld", Undutiful = "undutiful", Settled = "settled", Lapsed = "lapsed";
    public const string BySpouse = "spouse", ByPatron = "patron", ByParent = "parent", Alone = "alone";

    public static string DefiedKey(string id) => "defied_the_head:" + id;
    public static string WedKey(string id) => "wed_without_leave:" + id;
    public static string ShamedKey(string id) => "refused_field_seen:" + id;
    public static string DefectedKey(string id) => "defected_seen:" + id;

    private static uint Hash(string s) { uint h = 2166136261; foreach (char ch in s) h = (h ^ ch) * 16777619; return h; }
    private static void Mark(World w, string flag) { w.WorldFlags.Add(flag); w.WorldCounters["mark:" + flag] = w.Turn; }
    private static void Count(World w, string key) => w.WorldCounters[key] = w.Counter(key) + 1;
    private static void Regard(World w, Character? from, Character? to, int delta)
    {
        if (from == null || to == null || from.Id == to.Id || delta == 0) return;
        var r = w.Relationship(from.Id, to.Id); r.Disposition = Math.Clamp(r.Disposition + delta, -100, 100);
    }
    private static bool Mine(World w, string houseId) => w.Protagonist?.HouseId == houseId;

    // ---------------- who is out, and why ----------------

    public static bool IsStruck(Character c) => c.Flags.Contains(StruckChar);
    /// <summary>The standing record of this kind against someone, or null.</summary>
    public static Disowning? Standing(World w, Character c, string kind) => w.Disownings.LastOrDefault(d => d.Stands && d.PersonId == c.Id && d.Kind == kind);
    /// <summary>Those a house has struck out or cast out, whose record stands and who live.</summary>
    public static List<Disowning> Of(World w, string houseId, string kind) =>
        w.Disownings.Where(d => d.Stands && d.HouseId == houseId && d.Kind == kind && w.Char(d.PersonId) is { Alive: true }).OrderBy(d => d.Turn).ThenBy(d => d.Id, StringComparer.Ordinal).ToList();

    /// <summary>Of the blood of their house: not someone taken in or married in, nor a head who has laid the house down.</summary>
    public static bool OfTheBlood(Character c) => !HouseholdSystem.IsFound(c) && !c.Flags.Contains(LifeSystem.RetiredChar);

    /// <summary>The cause the record holds against someone, in words that follow their name ("defied the head in year 12"), or null.</summary>
    public static string? Cause(World w, Character c)
    {
        int? At(string key) => w.WorldCounters.TryGetValue(key, out var t) ? t : null;
        bool Lately(int? t) => t is { } y && w.Turn - y <= CauseYears;
        if (c.Flags.Contains(FamilySystem.DefiantFlag)) return "have grown defiant of the head";
        if (Lately(At(DefiedKey(c.Id)))) return $"defied the head in year {At(DefiedKey(c.Id))}";
        if (c.Flags.Contains(CourtshipSystem.ElopedChar) || Lately(At(WedKey(c.Id)))) return "married without the head's leave" + (At(WedKey(c.Id)) is { } y ? $" in year {y}" : "");
        if (c.Flags.Contains(FamilySystem.DefectedFlag)) return "once left the house for a rival";
        if (c.Flags.Contains(FeudSystem.RefusedFieldChar)) return "were called out over a death and did not come, to the house's shame";
        if (w.WorldCounters.TryGetValue(LadderSystem.BarredKey(c.Id), out var until) && w.Turn - (until - LadderSystem.BarYears) <= CauseYears) return $"were put out of office for what they did in it, in year {until - LadderSystem.BarYears}";
        return null;
    }

    /// <summary>A cause plain enough for another house's head to act on, and the year it arose: put out of office, the field refused, or come over
    /// from another house by defection. Only a fresh one: a head acts within <see cref="FreshYears"/> of it, or lets it lie.</summary>
    public static (string says, int since, bool grave)? PlainCause(World w, Character c)
    {
        if (w.WorldCounters.TryGetValue(LadderSystem.BarredKey(c.Id), out var until) && until - LadderSystem.BarYears >= w.Turn - FreshYears) return ("were put out of office for what they did in it", until - LadderSystem.BarYears, true);
        if (c.Flags.Contains(FeudSystem.RefusedFieldChar) && w.WorldCounters.TryGetValue(ShamedKey(c.Id), out var shamed) && shamed >= w.Turn - FreshYears) return ("were called out over a death and did not come", shamed, true);
        if (c.Flags.Contains(FamilySystem.DefectedFlag) && w.WorldCounters.TryGetValue(DefectedKey(c.Id), out var came) && came >= w.Turn - FreshYears) return ("came over from another house, which they had left", came, false);
        return null;
    }

    public sealed record Price(int Legitimacy, int Kin, int Self, string? Cause);
    /// <summary>What striking out (or casting out) this person would cost now.</summary>
    public static Price PriceOf(World w, Character who, string kind)
    {
        var cause = Cause(w, who); bool strike = kind == Struck;
        return cause != null ? new Price(0, 0, strike ? StrikeSelfWithCause : CastSelfWithCause, cause)
            : new Price(strike ? StrikeLegitimacy : CastLegitimacy, strike ? StrikeKin : CastKin, strike ? StrikeSelf : CastSelf, null);
    }
    /// <summary>The kin whose regard it costs: everyone grown of the house's blood but the head and the one struck.</summary>
    public static List<Character> Kin(World w, House house, Character head, Character who) =>
        w.LivingCharacters().Where(c => c.HouseId == house.Id && c.Id != head.Id && c.Id != who.Id && c.Age >= Adult && OfTheBlood(c)).OrderBy(c => c.Id, StringComparer.Ordinal).ToList();

    // ---------------- the refusals ----------------

    private static string? HeadRefusal(World w, Character? head, out House house)
    {
        house = head != null ? w.House(head.HouseId)! : null!;
        return head?.Alive != true || house == null || Economy.HeadOf(w, house)?.Id != head.Id ? "only the head of a house decides who stands in its line" : null;
    }
    /// <summary>What a grown relative of the blood must be for either: of the house, not the head, not a child, not the named heir.</summary>
    private static string? RelativeRefusal(World w, Character head, House house, Character? who, string act)
    {
        if (who is not { Alive: true } || who.HouseId != house.Id) return "they are not of your house";
        if (who.Id == head.Id) return act.StartsWith("strike") ? "you cannot strike yourself from your own line" : "you cannot cast yourself out of your own house";
        if (who.Age < Adult) return $"{who.Name} is a child: only someone grown can be {(act.StartsWith("strike") ? "struck from the line" : "cast out")}";
        if (!OfTheBlood(who)) return $"{who.Name} is not of the blood of the house: they stand in the line only after everyone who is, and can be sent home instead";
        if (house.NamedHeirId == who.Id) return $"{who.Name} is the heir you named: name another, or leave it to the law, first";
        return null;
    }
    private static string? Afford(House house, int legitimacy, string what) =>
        house.Legitimacy >= legitimacy ? null : $"{what} would cost {legitimacy} legitimacy and the house has {house.Legitimacy}";

    public static string? StrikeRefusal(World w, Character? head, Character? who)
    {
        if (HeadRefusal(w, head, out var house) is { } no) return no;
        if (RelativeRefusal(w, head!, house, who, "strike out") is { } not) return not;
        if (IsStruck(who!)) return $"{who!.Name} is struck from the line already";
        if (!DynastySystem.Line(w, head!).Any(c => c.Id != who!.Id && !IsStruck(c))) return $"there is nobody else to stand in the line: struck out, {who!.Name} would still be your heir";
        return Afford(house, PriceOf(w, who!, Struck).Legitimacy, "striking them out");
    }
    public static string? RestoreRefusal(World w, Character? head, Character? who)
    {
        if (HeadRefusal(w, head, out var house) is { } no) return no;
        if (who is not { Alive: true } || who.HouseId != house.Id) return "they are not of your house";
        var d = Standing(w, who, Struck); if (d == null || d.HouseId != house.Id) return $"{who.Name} stands in the line";
        return Afford(house, d.Legitimacy, "restoring them, at the price striking them out cost,");
    }
    public static string? CastOutRefusal(World w, Character? head, Character? who)
    {
        if (HeadRefusal(w, head, out var house) is { } no) return no;
        if (RelativeRefusal(w, head!, house, who, "cast out") is { } not) return not;
        if (!DynastySystem.Line(w, head!).Any(c => c.Id != who!.Id)) return $"{who!.Name} is the only one who could follow you: cast out, the house would have nobody";
        return Afford(house, PriceOf(w, who!, CastOut).Legitimacy, "casting them out");
    }
    public static string? TakeBackRefusal(World w, Character? head, Character? who)
    {
        if (HeadRefusal(w, head, out var house) is { } no) return no;
        if (who is not { Alive: true }) return "no such person";
        var d = Standing(w, who, CastOut); if (d == null || d.HouseId != house.Id) return $"{who.Name} was not cast out of your house";
        if (who.HouseId == house.Id) return $"{who.Name} is of your house";
        var theirs = w.House(who.HouseId);
        if (theirs != null && Economy.HeadOf(w, theirs)?.Id == who.Id && w.LivingCharacters().Any(c => c.HouseId == theirs.Id && c.Id != who.Id)) return $"{who.Name} leads {theirs.Name} now, and will not leave its people";
        int regard = w.GetRelationship(who.Id, head!.Id)?.Disposition ?? 0;
        if (regard < ComeBackAt) return $"{who.Name} will not come back: they think {Counted.Figure(regard)} of you, and it would take {Counted.Figure(ComeBackAt)} or better";
        return Afford(house, d.Legitimacy, "taking them back, at the price casting them out cost,");
    }

    // ---------------- what the screen says before it is done ----------------

    /// <summary>Whether they could sue over the head's will, said before it is done: only a child of the head can, and only if the will
    /// leaves them nothing; one struck out under the custom of portions takes a portion all the same.</summary>
    private static string SueSays(World w, Character who, Price p, string kind)
    {
        if (p.Cause != null) return "";
        var house = w.House(who.HouseId); var head = house != null ? Economy.HeadOf(w, house) : null;
        if (head == null || !IsChildOf(who, head)) return " They are not your child, so they cannot sue over your will.";
        if (kind == Struck && house!.Inheritance == WealthSystem.Portions) return " By the custom of the house they still take a portion of the treasury at your death, and one who does cannot sue over your will.";
        return " Should they outlive you, they may sue that your will was undutiful, unless it leaves them something.";
    }
    private static string PriceSays(Price p, string kind) => p.Cause != null
        ? $"The record holds cause: they {p.Cause}. It costs no legitimacy and the kin do not hold it against you."
        : $"There is no cause on the record against them: it costs {p.Legitimacy} legitimacy, and each of the kin thinks {p.Kin} the less of you.";
    public static string StrikeSays(World w, Character who)
    {
        var p = PriceOf(w, who, Struck);
        return $"Strike {who.Name} out of the line of succession. They stay of the house. {PriceSays(p, Struck)} {who.Name} thinks {p.Self} the less of you" + (Grieves(w, who) ? ", wants their place back, and may strike back." : ", and takes it quietly.")
            + SueSays(w, who, p, Struck) + " It can be undone at the same price.";
    }
    public static string CastOutSays(World w, Character who)
    {
        var p = PriceOf(w, who, CastOut); var house = w.House(who.HouseId); var (to, how) = house != null ? WhereTo(w, house, who) : (null, Alone);
        string goes = how switch { BySpouse => $"They go to {to!.Name}, their {(who.Sex == "female" ? "husband" : who.Sex == "male" ? "wife" : "spouse")}'s house", ByPatron => $"They go to {to!.Name}, their patron's house", ByParent => $"They go to {to!.Name}, the house of a parent born to it", _ => "They have nowhere to go, and found a name of their own" };
        // a command by service they hold ends with it, unless they go into the house whose fleet it is
        string service = ServiceSystem.Held(w, who) is { } s
            ? (to != null && s.OwnerId == to.Id ? $" They keep the command of {s.FleetName}, of {to.Name}, by right and not by service." : $" Their command of {s.FleetName} of {w.House(s.OwnerId)?.Name ?? s.OwnerId} by service ends.") : "";
        return $"Cast {who.Name} out of the house. {goes}, and take their purse ({Money.Credits(Math.Max(0, who.Purse))}).{service} {PriceSays(p, CastOut)} {who.Name} thinks {p.Self} the less of you."
            + SueSays(w, who, p, CastOut) + " They can be taken back at the same price, if they will come.";
    }
    public static string RestoreSays(World w, Character who)
    {
        var d = Standing(w, who, Struck); if (d == null) return "";
        return $"Restore {who.Name} to the line. It costs what striking them out cost: " + (d.Legitimacy > 0 ? $"{d.Legitimacy} legitimacy, and each of the kin thinks {d.KinRegard} the less of you for changing your mind." : "nothing, for there was cause.") + $" {who.Name} thinks {RestoreSelf} the better of you.";
    }
    public static string TakeBackSays(World w, Character who)
    {
        var d = Standing(w, who, CastOut); if (d == null) return "";
        return $"Take {who.Name} back into the house, from {w.House(who.HouseId)?.Name}. It costs what casting them out cost: " + (d.Legitimacy > 0 ? $"{d.Legitimacy} legitimacy, and each of the kin thinks {d.KinRegard} the less of you for changing your mind." : "nothing, for there was cause.") + $" {who.Name} thinks {TakeBackSelf} the better of you.";
    }

    // ---------------- doing it ----------------

    /// <summary>Whether this person keeps a grievance that moves them: anyone not loyal, while the family has wills of its own.</summary>
    private static bool Grieves(World w, Character who) => !who.NatureTraits.Contains("Loyal") && (!Mine(w, who.HouseId) || w.FamilyAutonomy > 0);
    private static Disowning Record(World w, string kind, Character head, House house, Character who, Price p) => new()
    {
        Id = $"{kind}_{w.Turn}_{who.Id}", Kind = kind, PersonId = who.Id, PersonName = who.Name, HouseId = house.Id, HeadId = head.Id, Turn = w.Turn,
        Cause = p.Cause ?? "", Legitimacy = p.Legitimacy, KinRegard = p.Kin, WasAmbition = who.Ambition
    };
    /// <summary>The grievance, through what exists: what they want becomes the thing taken from them.</summary>
    private static void Aggrieve(World w, Character who, Disowning d, string want)
    {
        if (!Grieves(w, who)) return;
        who.Ambition = want; d.Ambition = want;
    }
    /// <summary>The grievance is answered: what they wanted before, they want again, if what they want is still the grievance.</summary>
    private static void Appease(Character who, Disowning d) { if (d.Ambition.Length > 0 && who.Ambition == d.Ambition) who.Ambition = d.WasAmbition; }
    private static void End(World w, Disowning d, string how) { d.UndoneTurn = w.Turn; d.Ended = how; }
    private static void Pay(World w, House house, Character head, Disowning d, int legitimacy, int kin, string line)
    {
        if (legitimacy > 0) LegitimacyBook.Move(w, house, -legitimacy, LegitimacyBook.Family, line);
        if (kin > 0) foreach (var k in Kin(w, house, head, w.Char(d.PersonId)!)) Regard(w, k, head, -kin);
    }

    /// <summary>Strike them out of the line: they stay of the house.</summary>
    public static Disowning Strike(World w, Character head, Character who)
    {
        var house = w.House(head.HouseId)!; var p = PriceOf(w, who, Struck); var d = Record(w, Struck, head, house, who, p); w.Disownings.Add(d);
        who.Flags.Add(StruckChar);
        Pay(w, house, head, d, p.Legitimacy, p.Kin, $"{who.Name} struck from the line with no cause on the record");
        Regard(w, who, head, -p.Self); Aggrieve(w, who, d, "seize_the_throne");
        LifeSystem.Note(w, who, "failure", $"Was struck from the line of {house.Name} by {head.Name}" + (p.Cause != null ? $", who said they {p.Cause}." : ", with no cause given."));
        LifeSystem.Note(w, head, "struck", $"Struck {who.Name} from the line.");
        Count(w, StruckKey); if (!Mine(w, house.Id)) Count(w, StruckByOthersKey); else if (who.Id != w.ProtagonistId) Mark(w, StruckFlag);
        w.Log($"{head.Name} strikes {who.Name} out of the line of {house.Name}" + (p.Cause != null ? $": they {p.Cause}." : ", and gives no cause."));
        return d;
    }

    public static void Restore(World w, Character head, Character who)
    {
        var house = w.House(head.HouseId)!; var d = Standing(w, who, Struck)!;
        who.Flags.Remove(StruckChar); End(w, d, "restored");
        Pay(w, house, head, d, d.Legitimacy, d.KinRegard, $"{who.Name} restored to the line");
        Regard(w, who, head, RestoreSelf); Appease(who, d); Count(w, RestoredKey);
        LifeSystem.Note(w, who, "restored", $"Was restored to the line of {house.Name}.");
        w.Log($"{head.Name} restores {who.Name} to the line of {house.Name}.");
    }

    /// <summary>Where one cast out goes: to their husband's or wife's house, to their patron's, to the house of a parent born to another,
    /// and otherwise nowhere, so that they found a name of their own. One cast out by another house does not come to the player's
    /// unless it is to a husband or wife there.</summary>
    public static (House? to, string how) WhereTo(World w, House from, Character who)
    {
        string? mine = w.Protagonist?.HouseId; bool ours = from.Id == mine;
        bool Fit(House? h, bool spouse) => h != null && h.Id != from.Id && !World.Outsider(h.Id) && (ours || spouse || h.Id != mine);
        if (DynastySystem.Spouse(w, who) is { } sp && w.House(sp.HouseId) is var sh && Fit(sh, true)) return (sh, BySpouse);
        if (w.Char(who.PatronId) is { Alive: true } pt && w.House(pt.HouseId) is var ph && Fit(ph, false)) return (ph, ByPatron);
        foreach (var pid in new[] { who.MotherId, who.FatherId })
        {
            if (w.Char(pid) is not { } parent) continue;
            if (w.House(parent.HouseId) is var h1 && Fit(h1, false)) return (h1, ByParent);
            if (parent.BornHouseId.Length > 0 && w.House(parent.BornHouseId) is var h2 && Fit(h2, false)) return (h2, ByParent);
        }
        return (null, Alone);
    }

    /// <summary>They leave the house's rolls: its posts, its courtships, its debts with them, and anything entailed on the house.</summary>
    private static void Leave(World w, House house, Character head, Character who)
    {
        house.Members.Remove(who.Id);
        LayDown(w, house, who);
        foreach (var item in w.Heirlooms.Where(i => i.HolderId == who.Id && i.Entailed)) item.HolderId = head.Id;   // what goes with the house stays with it
        foreach (var b in w.Businesses.Values.Where(b => b.HeldById == who.Id && b.OwnerId == house.Id)) b.HeldById = "";   // a venture of their own was the house's to give
        if (who.LentHead > 0) { int pay = Math.Clamp(head.Purse, 0, who.LentHead); head.Purse -= pay; who.Purse += pay; }
        who.LentHead = 0; who.LentSince = -1; who.Flags.Remove(WealthSystem.HeadOwesChar); who.OwesHead = 0; who.OwesSince = -1; who.OwesKin = 0; who.OwesKinId = "";
        if (house.Id == w.Protagonist?.HouseId) w.Courtships.RemoveAll(c => c.SuitorId == who.Id || c.TargetId == who.Id);   // their matches are no longer the head's to bless
    }

    /// <summary>They lay down what they held of a house they leave: its posts and its court, its fleets' commands and captaincies, and the
    /// governing of its worlds.</summary>
    private static void LayDown(World w, House house, Character who)
    {
        house.Duties.RemoveAll(x => x.HolderId == who.Id); house.Court.RemoveAll(r => r.KinId == who.Id);
        foreach (var f in w.Fleets.Values.Where(f => f.OwnerId == house.Id)) { if (f.AdmiralId == who.Id) f.AdmiralId = ""; if (f.CaptainId == who.Id) f.CaptainId = ""; }
        foreach (var h in w.Holdings.Values.Where(h => h.OwnerId == house.Id && h.GovernorId == who.Id)) h.GovernorId = "";
    }

    /// <summary>Cast them out of the house. They go where <see cref="WhereTo"/> says, with their purse.</summary>
    public static Disowning CastOutOf(World w, Character head, Character who)
    {
        var house = w.House(head.HouseId)!; var p = PriceOf(w, who, CastOut);
        if (Standing(w, who, Struck) is { } was) { who.Flags.Remove(StruckChar); End(w, was, "cast_out"); }
        var d = Record(w, CastOut, head, house, who, p); w.Disownings.Add(d);
        Pay(w, house, head, d, p.Legitimacy, p.Kin, $"{who.Name} cast out of the house with no cause on the record");
        Regard(w, who, head, -p.Self);
        LifeSystem.Note(w, who, "failure", $"Was cast out of {house.Name} by {head.Name}" + (p.Cause != null ? $", who said they {p.Cause}." : ", with no cause given."));
        LifeSystem.Note(w, head, "struck", $"Cast {who.Name} out of the house.");
        var (to, how) = WhereTo(w, house, who); d.WasBornHouseId = who.BornHouseId; d.WentHow = how;
        Leave(w, house, head, who);
        if (to == null) to = Found(w, house, who);
        else who.BornHouseId = house.Id;   // in another's house they live as one taken in, under their own name
        who.HouseId = to.Id; if (!to.Members.Contains(who.Id)) to.Members.Add(who.Id); d.WentTo = to.Id;
        ServiceSystem.HouseChanged(w, who, $"{who.Name} was cast out of {house.Name}");   // that day: they command no fleet for a house their new one may be at war with
        if (!w.Pools.ContainsKey(who.Id)) w.Pools[who.Id] = ActionPools.ForNpc();
        who.Flags.Add(CastOutChar); who.Flags.Remove(FamilySystem.DefiantFlag); Aggrieve(w, who, d, "outshine_all_rivals");
        Count(w, CastKey); if (!Mine(w, house.Id)) Count(w, CastByOthersKey); else Mark(w, CastFlag);
        string went = how switch { BySpouse => $"to {to.Name}, where their husband or wife is", ByPatron => $"to {to.Name}, their patron's house", ByParent => $"to {to.Name}, a parent's people", _ => $"alone, and are the first of a new name, {to.Name}" };
        w.Log($"{head.Name} casts {who.Name} out of {house.Name}" + (p.Cause != null ? $": they {p.Cause}." : ", and gives no cause.") + $" They go {went}, and take their purse with them.");
        if (!Mine(w, house.Id) && to.Id == w.Protagonist?.HouseId) w.Tell("info", $"{who.Name}, cast out of {house.Name}, comes to live in your house with their husband or wife.", "dynasty", who.Id);
        return d;
    }

    /// <summary>One cast out with nowhere to go founds a name of their own: a house without land, named for them, whose treasury is their purse.</summary>
    private static House Found(World w, House from, Character who)
    {
        string given = who.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? who.Name;
        string id = "house_of_" + who.Id; if (w.Houses.ContainsKey(id)) id += "_" + w.Turn;
        string name = "House " + given; if (w.Houses.Values.Any(h => h.Name == name)) name = $"The House of {who.Name}";
        var h = new House
        {
            Id = id, Name = name, AccentColor = Presentation.Palette.NextHouseColor(w, id), Title = "landless", Legitimacy = NewNameLegitimacy, Culture = from.Culture, Ancestry = from.Ancestry, Origin = "new",
            HeadId = who.Id, Treasury = new Resources { Credits = Math.Max(0, who.Purse) }
        };
        if (who.Purse > 0) who.Purse = 0;   // what they had is what the new name has
        w.Houses[id] = h; LegitimacyBook.Settle(w, h); Count(w, NewNamesKey);
        who.BornHouseId = "";   // the first of the name: its blood begins with them
        return h;
    }

    public static void TakeBack(World w, Character head, Character who)
    {
        var house = w.House(head.HouseId)!; var d = Standing(w, who, CastOut)!; var from = w.House(who.HouseId);
        if (from != null)
        {
            from.Members.Remove(who.Id); LayDown(w, from, who);   // what they held of the house they leave stays with it
            bool empty = !w.LivingCharacters().Any(c => c.HouseId == from.Id && c.Id != who.Id);
            if (empty && d.WentHow == Alone && from.Treasury.Credits > 0) { who.Purse += from.Treasury.Credits; from.Treasury.Credits = 0; }   // the name they founded was only them
        }
        who.HouseId = house.Id; if (!house.Members.Contains(who.Id)) house.Members.Add(who.Id); who.BornHouseId = d.WasBornHouseId;
        ServiceSystem.HouseChanged(w, who, $"{who.Name} was taken back into {house.Name}");
        who.Flags.Remove(CastOutChar); End(w, d, "taken_back");
        Pay(w, house, head, d, d.Legitimacy, d.KinRegard, $"{who.Name} taken back into the house");
        Regard(w, who, head, TakeBackSelf); Appease(who, d); Count(w, TakenBackKey);
        LifeSystem.Note(w, who, "restored", $"Was taken back into {house.Name}.");
        w.Log($"{head.Name} takes {who.Name} back into {house.Name}" + (from != null ? $", from {from.Name}." : "."));
    }

    // ---------------- the undutiful will ----------------

    public static bool IsChildOf(Character c, Character parent) => c.MotherId == parent.Id || c.FatherId == parent.Id || c.AdoptedById == parent.Id;
    /// <summary>Would this child bring the suit? Anyone but a loyal child who thinks well of whoever has the house now.</summary>
    public static bool WouldSue(World w, Character child, Character heir) =>
        !(child.NatureTraits.Contains("Loyal") && (w.GetRelationship(child.Id, heir.Id)?.Disposition ?? 0) >= SueUnlessRegard) && (!Mine(w, heir.HouseId) || w.FamilyAutonomy > 0);

    /// <summary>Whether the dead head's will, or the custom of the house, leaves this child something: the larger share or a smaller
    /// legacy out of a purse that holds anything, an heirloom the dead head held that is not entailed, or (while they are of the house)
    /// a portion of the treasury by the custom of portions. A child left anything was not forgotten, and has no suit.</summary>
    public static bool Remembered(World w, House house, Character dead, Character child)
    {
        if (dead.Will is { } will)
        {
            int purse = Math.Max(0, dead.Purse);
            if (will.LegateeId == child.Id && purse * will.SharePct / 100 > 0) return true;
            if (will.Also.TryGetValue(child.Id, out var pct) && purse * pct / 100 > 0) return true;
            if (will.Items.Any(kv => kv.Value == child.Id && w.Heirlooms.Any(i => i.Id == kv.Key && i.HolderId == dead.Id && !i.Entailed))) return true;
        }
        return house.Inheritance == WealthSystem.Portions && child.HouseId == house.Id && house.Treasury.Credits * WealthSystem.PortionPct / 100 > 0;
    }

    /// <summary>The seat has passed (called once, from <see cref="DynastySystem.HandOver"/>). When the head who held it died, each grown
    /// child of theirs they struck or cast out with no cause on the record, and whom the will and the house's custom leave nothing
    /// (<see cref="Remembered"/>), may bring the suit of the undutiful will, while a praetor of another house sits to hear it. What it
    /// asks is reckoned now, before the will is read: a quarter of the purse, and one heirloom in four of those not entailed, the most
    /// valuable first, and at least one.</summary>
    public static void SeatPassed(World w, Character? previous, Character heir)
    {
        if (previous == null || previous.Alive || previous.HouseId != heir.HouseId || w.House(heir.HouseId) is not { } house) return;
        foreach (var d in w.Disownings.Where(d => d.Stands && d.HouseId == house.Id && d.Cause.Length == 0).OrderBy(d => d.Id, StringComparer.Ordinal).ToList())
        {
            var child = w.Char(d.PersonId);
            if (child is not { Alive: true } || child.Id == heir.Id || child.Age < Adult || !IsChildOf(child, previous) || !WouldSue(w, child, heir)) continue;
            if (Remembered(w, house, previous, child)) continue;   // the will, or the custom of the house, did not forget them
            if (ChargeSystem.Abroad(w, child)) { w.Log($"{child.Name} would sue that {previous.Name}'s will was undutiful, but is in exile, and an exile brings no suit before the courts of the Reach."); continue; }
            if (w.WillSuits.Any(s => s.PlaintiffId == child.Id && s.DeceasedId == previous.Id)) continue;
            var judge = LadderSystem.Bench(w, house.Id, child.HouseId); if (judge == null) { w.Log($"{child.Name} would sue that {previous.Name}'s will was undutiful, but no praetor of another house sits to hear it."); continue; }
            var things = w.Heirlooms.Where(i => i.HolderId == previous.Id && !i.Entailed).OrderByDescending(i => i.Value).ThenBy(i => i.Id, StringComparer.Ordinal).ToList();
            var s = new WillSuit
            {
                Id = $"will_{w.Turn}_{child.Id}", HouseId = house.Id, PlaintiffId = child.Id, DeceasedId = previous.Id, DeceasedName = previous.Name, Turn = w.Turn, JudgeId = judge.Id,
                Share = Math.Max(0, previous.Purse) * SharePct / 100, Items = things.Take(things.Count == 0 ? 0 : Math.Max(1, things.Count / ItemsOneIn)).Select(i => i.Id).ToList()
            };
            w.WillSuits.Add(s); Count(w, SuitsKey); if (Mine(w, house.Id)) Mark(w, SuitFlag);
            w.Log($"{child.Name} brings a suit before {judge.Name}, praetor, that the will of {previous.Name} was undutiful: they were {(d.Kind == Struck ? "struck from the line" : "cast out")} with no cause on the record. {AsksSays(w, s)}");
            // the year named outright: a head who dies at the year's end is read of in the next, and the suit is heard at the end of that one
            if (Mine(w, house.Id)) w.Tell("warn", $"{child.Name} sues that {previous.Name}'s will was undutiful: they were {(d.Kind == Struck ? "struck from the line" : "cast out")} with no cause on the record. {AsksSays(w, s)} {judge.Name}, praetor, who is {Verdict(w, s, judge).why}, hears it at the end of year {s.Turn + 1}. Until then you may come to terms or send someone to answer it.", "senate:ladder", child.Id);
        }
    }

    /// <summary>The suits of the undutiful will waiting to be heard against a house.</summary>
    public static List<WillSuit> SuitsOn(World w, string houseId) => w.WillSuits.Where(s => s.Outcome.Length == 0 && s.HouseId == houseId).OrderBy(s => s.Id, StringComparer.Ordinal).ToList();
    /// <summary>Those decided lately, for the screen.</summary>
    public static List<WillSuit> DecidedOn(World w, string houseId) => w.WillSuits.Where(s => s.Outcome.Length > 0 && s.HouseId == houseId).OrderByDescending(s => s.DecidedTurn).ThenBy(s => s.Id, StringComparer.Ordinal).ToList();
    public static string ItemsSays(World w, WillSuit s)
    {
        var names = s.Items.Select(id => w.Heirlooms.FirstOrDefault(i => i.Id == id)?.Name).Where(n => n != null).ToList();
        return names.Count == 0 ? "" : string.Join(", ", names);
    }
    public static string AsksSays(World w, WillSuit s) =>
        $"They ask a quarter of the purse, {Money.Credits(s.Share)}" + (ItemsSays(w, s) is { Length: > 0 } items ? $", and {items}" : "") + ".";
    /// <summary>What a win would do, for the docket: one struck out stands in the line again; one cast out stays out, and may be taken back.</summary>
    public static string WinSays(World w, WillSuit s)
    {
        string child = w.Char(s.PlaintiffId)?.Name ?? "the child";
        return Over(w, s)?.Kind == CastOut ? $"If the praetor finds the will undutiful, {child} takes what the suit asks. The casting out stands: they may still be taken back, at the price it cost."
            : $"If the praetor finds the will undutiful, {child} takes what the suit asks and stands in the line again.";
    }
    /// <summary>What coming to terms would do, for the docket.</summary>
    public static string TermsSays(World w, WillSuit s) =>
        $"Pay {w.Char(s.PlaintiffId)?.Name ?? "the child"} what the suit asks now, and it is withdrawn. {AsksSays(w, s)} " + (Over(w, s)?.Kind == CastOut ? "The casting out stands." : "The striking stands.");
    public static string LawSays(WillSuit s) => "The record shows no cause for putting them out: the law, read plainly, is with them.";
    /// <summary>The standing record of how the plaintiff was put out of the house the suit is against, or null.</summary>
    public static Disowning? Over(World w, WillSuit s) => w.Disownings.LastOrDefault(d => d.Stands && d.PersonId == s.PlaintiffId && d.HouseId == s.HouseId);

    /// <summary>The praetor who is to hear it: the one named while they sit and are of neither house, or another of neither house.</summary>
    public static Character? JudgeOf(World w, WillSuit s)
    {
        var child = w.Char(s.PlaintiffId); string other = child?.HouseId ?? s.HouseId;
        if (w.Char(s.JudgeId) is { Alive: true } j && LadderSystem.Seat(w, j.Id)?.OfficeId == LadderSystem.Praetor && j.HouseId != s.HouseId && j.HouseId != other) return j;
        return LadderSystem.Bench(w, s.HouseId, other);
    }
    /// <summary>Who answers it for the house: whoever the head sent, else the house's advocate on the council, else the head; and if the
    /// head is abroad, the best pleader of the house at home. Nobody in exile is heard (docs/LADDER.md, "Exile").</summary>
    public static Character? AnswersFor(World w, WillSuit s)
    {
        var house = w.House(s.HouseId); if (house == null) return null;
        if (w.Char(s.PleaderId) is { Alive: true } p && p.HouseId == house.Id && p.Id != s.PlaintiffId && !ChargeSystem.Abroad(w, p)) return p;
        if (house.Duty(HouseholdSystem.Advocate)?.HolderId is { Length: > 0 } adv && w.Char(adv) is { Alive: true } a && a.HouseId == house.Id && a.Id != s.PlaintiffId && !ChargeSystem.Abroad(w, a)) return a;
        if (Economy.HeadOf(w, house) is { } head && !ChargeSystem.Abroad(w, head)) return head;
        return w.LivingCharacters().Where(c => c.HouseId == house.Id && c.Id != s.PlaintiffId && c.Age >= Adult && !ChargeSystem.Abroad(w, c))
            .OrderByDescending(LadderSystem.Craft).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();
    }
    public static int DefenceOf(World w, WillSuit s) => AnswersFor(w, s) is { } a ? LadderSystem.Craft(a) + (HouseholdSystem.IsAdvocateOf(w, s.HouseId, a.Id) ? w.Counter(HouseholdSystem.CounselEdgeKey(s.HouseId)) : 0) : 0;
    /// <summary>In a hundred, how likely the child wins before a praetor who goes by the pleading.</summary>
    public static int Chance(World w, WillSuit s, int defence) => Math.Clamp(PleadBase + LadderSystem.Craft(w.Char(s.PlaintiffId)) * PleadFor - defence * PleadAgainst, 5, 95);

    /// <summary>The grounds a praetor finds on.</summary>
    public const string ByRecord = "record", ByPurchase = "bought", ByFriendship = "friend", ForSale = "for_sale", ByPleading = "pleading";
    /// <summary>How the praetor would find, and on what ground. The upright go by the record, which shows no cause; one bought by the house
    /// finds for it; a friend of the house finds for it; one for sale finds for whichever has more, the child's purse or the house's
    /// treasury; the rest go by who pleads better.</summary>
    public static (bool undutiful, string ground) Grounds(World w, WillSuit s, Character judge, int? defence = null)
    {
        var house = w.House(s.HouseId); var child = w.Char(s.PlaintiffId);
        if (judge.NatureTraits.Contains("Honorable") || judge.NatureTraits.Contains("Just")) return (true, ByRecord);
        if (LadderSystem.BoughtBy(w, judge) == s.HouseId) return (false, ByPurchase);
        var bench = w.House(judge.HouseId); var benchHead = bench != null ? Economy.HeadOf(w, bench) : null; var ourHead = house != null ? Economy.HeadOf(w, house) : null;
        if (benchHead != null && ourHead != null && (w.GetRelationship(benchHead.Id, ourHead.Id)?.Disposition ?? 0) >= FriendAt) return (false, ByFriendship);
        if (judge.NatureTraits.Contains("Ruthless") || judge.NatureTraits.Contains("Cruel") || judge.Ambition == "amass_a_fortune") return ((child?.Purse ?? 0) > (house?.Treasury.Credits ?? 0), ForSale);
        return ((int)(Hash(s.Id + ":verdict") % 100) < Chance(w, s, defence ?? DefenceOf(w, s)), ByPleading);
    }
    /// <summary>How the praetor would find, and why, in words that follow "who is".</summary>
    public static (bool undutiful, string why) Verdict(World w, WillSuit s, Character judge, int? defence = null)
    {
        var (undutiful, ground) = Grounds(w, s, judge, defence);
        return (undutiful, ground switch
        {
            ByRecord => "upright, and will go by the record, which shows no cause",
            ByPurchase => "bought by the house, and will find for it",
            ByFriendship => "a friend of the house, and will find for it",
            ForSale => "known to be for sale, and will find for whichever has more to give",
            _ => "neither upright nor a friend of the house, and will go by who pleads better"
        });
    }
    /// <summary>How the praetor found, once it is heard: the words between "{name}, praetor, " and "found the will undutiful" or "let the will stand".</summary>
    private static string HowFound(string ground) => ground switch
    {
        ByRecord => "went by the record, which showed no cause, and ",
        ByPurchase => "who had been bought by the house, ",
        ByFriendship => "a friend of the house, ",
        ForSale => "known to be for sale, went with the side that had more to give and ",
        _ => "went by who pleaded better and "
    };

    /// <summary>The share is paid: from the head's purse, then the treasury; and the heirlooms asked for, where the house still has them.</summary>
    private static (int paid, List<string> things) PayShare(World w, WillSuit s, House house, Character head, Character child)
    {
        int fromPurse = Math.Clamp(head.Purse, 0, s.Share); head.Purse -= fromPurse;
        int fromHouse = Math.Clamp(house.Treasury.Credits, 0, s.Share - fromPurse); house.Treasury.Credits -= fromHouse; child.Purse += fromPurse + fromHouse;
        var things = new List<string>();
        foreach (var id in s.Items)
            if (w.Heirlooms.FirstOrDefault(i => i.Id == id) is { Entailed: false } item && w.Char(item.HolderId) is { } holder && holder.HouseId == house.Id && holder.Id != child.Id)
            { holder.Will?.Items.Remove(item.Id); item.HolderId = child.Id; things.Add(item.Name); }
        return (fromPurse + fromHouse, things);
    }
    private static string Got(int paid, List<string> things) => Money.Credits(paid) + (things.Count > 0 ? " and " + string.Join(", ", things) : "");

    public static string? SettleRefusal(World w, Character? head, WillSuit? s)
    {
        if (HeadRefusal(w, head, out var house) is { } no) return "only the head of a house comes to terms for it";
        if (s == null || s.Outcome.Length > 0 || s.HouseId != house.Id) return "no such suit is waiting";
        int has = Math.Max(0, head!.Purse) + Math.Max(0, house.Treasury.Credits);
        return has >= s.Share ? null : $"terms are the {Money.Credits(s.Share)} the suit asks, and your purse and the treasury hold {Money.Credits(has)}";
    }
    /// <summary>Come to terms before it is heard: the share is paid now and the suit is withdrawn. The striking stands.</summary>
    public static void Settle(World w, Character head, WillSuit s)
    {
        var house = w.House(head.HouseId)!; var child = w.Char(s.PlaintiffId)!;
        var (paid, things) = PayShare(w, s, house, head, child);
        s.Outcome = Settled; s.DecidedTurn = w.Turn; s.How = $"settled before it was heard: {Got(paid, things)} paid"; Regard(w, child, head, SettledRegard); Count(w, SuitsSettledKey);
        w.Log($"{head.Name} comes to terms with {child.Name} over the will of {s.DeceasedName}: {Got(paid, things)}, and the suit is withdrawn.");
    }
    public static string? PleadRefusal(World w, Character? head, WillSuit? s, Character? pleader)
    {
        if (HeadRefusal(w, head, out var house) is { } no) return "only the head of a house sends its pleader";
        if (s == null || s.Outcome.Length > 0 || s.HouseId != house.Id) return "no such suit is waiting";
        if (pleader is not { Alive: true } || pleader.HouseId != house.Id || pleader.Age < Adult) return "there is no one of age in the house to send";
        if (pleader.Id == s.PlaintiffId) return "they are the one who brings it";
        if (ChargeSystem.AbroadSays(w, pleader, pleader.Id == w.ProtagonistId) is { } away) return away;   // an exile pleads in no court
        return s.PleaderId == pleader.Id ? "they are already to answer it" : null;
    }
    public static void SendPleader(World w, Character head, WillSuit s, Character pleader)
    {
        s.PleaderId = pleader.Id;
        w.Log($"{pleader.Name} is to answer {w.Char(s.PlaintiffId)?.Name}'s suit over the will of {s.DeceasedName} (law and oratory {LadderSystem.Craft(pleader)}; theirs is {LadderSystem.Craft(w.Char(s.PlaintiffId))}).");
    }
    /// <summary>What sending this pleader would do, in words, for the docket.</summary>
    public static string PleadSays(World w, WillSuit s, Character pleader)
    {
        var judge = JudgeOf(w, s); int ours = LadderSystem.Craft(pleader); if (judge == null) return $"law and oratory {ours}";
        string ground = Grounds(w, s, judge, ours).ground;
        return $"law and oratory {ours}: " + (ground == ByRecord ? "the praetor is upright and will go by the record, and no pleading will change the verdict"
            : ground == ByPleading ? $"against their {LadderSystem.Craft(w.Char(s.PlaintiffId))}, about {Chance(w, s, ours)} in a hundred that the will is found undutiful"
            : "the praetor will not go by the pleading");
    }

    private static void Hear(World w)
    {
        foreach (var s in w.WillSuits.Where(s => s.Outcome.Length == 0 && s.Turn < w.Turn).OrderBy(s => s.Id, StringComparer.Ordinal).ToList())
        {
            var house = w.House(s.HouseId); var child = w.Char(s.PlaintiffId); var head = house != null ? Economy.HeadOf(w, house) : null; var judge = JudgeOf(w, s);
            if (house == null || head == null || child is not { Alive: true } || child.Id == head.Id || judge == null || ChargeSystem.Abroad(w, child))
            {
                s.Outcome = Lapsed; s.DecidedTurn = w.Turn; s.How = judge == null ? "lapsed: no praetor of another house sat to hear it" : child != null && ChargeSystem.Abroad(w, child) ? "lapsed: the one who brought it went into exile, and an exile brings no suit"
                    : "lapsed: there was nobody left to bring it, or to answer it";
                w.Log($"The suit over the will of {s.DeceasedName} lapses.");
                if (house != null && Mine(w, house.Id)) w.Tell("info", $"{(child != null ? child.Name + "'s" : "The")} suit over the will of {s.DeceasedName} has lapsed: {s.How["lapsed: ".Length..]}.", "senate:ladder");
                continue;
            }
            var (undutiful, ground) = Grounds(w, s, judge); var answers = AnswersFor(w, s);
            s.DecidedTurn = w.Turn;
            if (undutiful)
            {
                var (paid, things) = PayShare(w, s, house, head, child);
                s.Outcome = Undutiful; s.How = $"{judge.Name}, praetor, {HowFound(ground)}found the will undutiful: {Got(paid, things)} to {child.Name}";
                // one struck out stands in the line again; one cast out is not brought back by it, and may still be taken back
                var d = Over(w, s); bool castOut = d?.Kind == CastOut;
                if (d is { Kind: Struck }) { End(w, d, "found_undutiful"); Appease(child, d); child.Flags.Remove(StruckChar); }
                LegitimacyBook.Move(w, house, -UndutifulLegitimacy, LegitimacyBook.Courts, "A late head's will found undutiful in the courts");
                Regard(w, child, head, WonRegard); Count(w, SuitsWonKey);
                LifeSystem.Note(w, child, "office", $"Sued that the will of {s.DeceasedName} was undutiful, and won.");
                if (Mine(w, house.Id)) { Mark(w, UndutifulFlag); w.Tell("warn", $"{judge.Name}, praetor, finds the will of {s.DeceasedName} undutiful: {child.Name} takes {Got(paid, things)}" + (castOut ? ". The casting out stands: they may still be taken back, at the price it cost." : child.HouseId == house.Id ? ", and stands in the line again." : "."), "senate:ladder", child.Id); }
                w.Log($"{judge.Name}, praetor, finds that the will of {s.DeceasedName} did not do its duty by {child.Name}, who takes {Got(paid, things)}.");
            }
            else
            {
                s.Outcome = Upheld; s.How = $"{judge.Name}, praetor, {HowFound(ground)}let the will stand" + (answers != null ? $", {answers.Name} answering for the house" : "");
                Regard(w, child, head, -LostRegard);
                LifeSystem.Note(w, child, "failure", $"Sued that the will of {s.DeceasedName} was undutiful, and lost.");
                if (Mine(w, house.Id)) { Mark(w, UpheldFlag); w.Tell("good", $"{judge.Name}, praetor, lets the will of {s.DeceasedName} stand: {child.Name}'s suit fails.", "senate:ladder", child.Id); }
                w.Log($"{judge.Name}, praetor, lets the will of {s.DeceasedName} stand, and {child.Name}'s suit fails.");
            }
        }
        w.WillSuits.RemoveAll(s => s.Outcome.Length > 0 && s.DecidedTurn < w.Turn - KeptYears);
    }

    // ---------------- the year ----------------

    /// <summary>Once a year, after the family's own wills (FamilySystem): what is to be remembered is written down, records follow the people
    /// in them, suits of the undutiful will that waited a year are heard, and other houses' heads act on plain causes.</summary>
    public static void Tick(World w)
    {
        foreach (var f in DeedFlags) if (w.WorldFlags.Contains(f) && w.Counter("mark:" + f) < w.Turn - 1) { w.WorldFlags.Remove(f); w.WorldCounters.Remove("mark:" + f); }
        Remember(w); Follow(w); Hear(w); Others(w); Forget(w);
    }

    /// <summary>The causes written down for someone who has died are let go: nothing reads them once they are gone.</summary>
    private static void Forget(World w)
    {
        foreach (var k in w.WorldCounters.Keys.Where(k => CausePrefixes.Any(p => k.StartsWith(p, StringComparison.Ordinal)) && w.Char(k[(k.IndexOf(':') + 1)..]) is not { Alive: true }).ToList())
            w.WorldCounters.Remove(k);
    }
    private static readonly string[] CausePrefixes = { DefiedKey(""), WedKey(""), ShamedKey(""), DefectedKey("") };

    /// <summary>Marks that pass with the year are written down where they last: defiance (the latest year), a marriage without leave, the field
    /// refused and a defection (the year they were first seen).</summary>
    private static void Remember(World w)
    {
        foreach (var c in w.LivingCharacters())
        {
            if (c.Flags.Contains(FamilySystem.DefiantFlag)) w.WorldCounters[DefiedKey(c.Id)] = w.Turn;
            if (c.Flags.Contains(CourtshipSystem.ElopedChar) && !w.WorldCounters.ContainsKey(WedKey(c.Id))) w.WorldCounters[WedKey(c.Id)] = w.Turn;
            if (c.Flags.Contains(FeudSystem.RefusedFieldChar) && !w.WorldCounters.ContainsKey(ShamedKey(c.Id))) w.WorldCounters[ShamedKey(c.Id)] = w.Turn;
            if (c.Flags.Contains(FamilySystem.DefectedFlag) && !w.WorldCounters.ContainsKey(DefectedKey(c.Id))) w.WorldCounters[DefectedKey(c.Id)] = w.Turn;
        }
    }

    /// <summary>A record follows its person: one struck out who has left the house, or come to lead it, is struck from it no longer; the dead
    /// are out of every line.</summary>
    private static void Follow(World w)
    {
        foreach (var d in w.Disownings.Where(d => d.Stands).ToList())
        {
            var c = w.Char(d.PersonId); var house = w.House(d.HouseId);
            if (c is not { Alive: true }) { End(w, d, "died"); continue; }
            // the grievance is answered when the thing is undone, as at a restoring: they want what they wanted before
            if (d.Kind == Struck && (c.HouseId != d.HouseId || (house != null && Economy.HeadOf(w, house)?.Id == c.Id))) { End(w, d, c.HouseId != d.HouseId ? "left" : "succeeded"); c.Flags.Remove(StruckChar); Appease(c, d); continue; }
            if (d.Kind == CastOut && (house == null || c.HouseId == d.HouseId)) { End(w, d, "left"); c.Flags.Remove(CastOutChar); Appease(c, d); }
        }
    }

    /// <summary>Other houses' heads act on a plain, fresh cause against one of their own blood, one in <see cref="OthersOneIn"/> by a hash of the
    /// house, the person and the year it arose, and one a year in a house: one put out of office, or who refused the field, is cast out by a
    /// proud, honourable or just head and struck from the line by any other; one who came over from another house is struck from the line.</summary>
    private static void Others(World w)
    {
        string? mine = w.Protagonist?.HouseId;
        foreach (var h in w.Houses.Values.Where(h => h.Id != mine && !World.Outsider(h.Id)).OrderBy(h => h.Id, StringComparer.Ordinal).ToList())
        {
            var head = Economy.HeadOf(w, h); if (head is not { Alive: true }) continue;
            foreach (var c in w.LivingCharacters().Where(c => c.HouseId == h.Id && c.Id != head.Id && c.Age >= Adult && OfTheBlood(c) && !IsStruck(c)).OrderBy(c => c.Id, StringComparer.Ordinal).ToList())
            {
                if (PlainCause(w, c) is not { } cause) continue;
                if (w.Disownings.Any(d => d.PersonId == c.Id && d.HouseId == h.Id && d.Turn >= cause.since)) continue;   // done once for it already
                if (Hash($"{h.Id}:{c.Id}:{cause.since}") / 7 % OthersOneIn != 0) continue;   // this head lets it lie
                bool stern = head.NatureTraits.Contains("Proud") || head.NatureTraits.Contains("Honorable") || head.NatureTraits.Contains("Just");
                Disowning? done = null;
                if (cause.grave && stern && CastOutRefusal(w, head, c) == null) done = CastOutOf(w, head, c);
                else if (StrikeRefusal(w, head, c) == null) done = Strike(w, head, c);
                if (done == null) continue;
                var p = w.Protagonist;
                bool known = p != null && (w.GetRelationship(head.Id, p.Id) != null || w.Dealings.Any(x => x.FromHouseId == h.Id || x.ToHouseId == h.Id));
                if (known) w.Tell("info", $"{head.Name} of {h.Name} has {(done.Kind == Struck ? "struck" : "cast")} {c.Name} {(done.Kind == Struck ? "out of the line" : "out of the house")}: they {cause.says}.", "houses", h.Id);
                break;
            }
        }
    }
}
