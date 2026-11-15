using Praetoria.Core.Data;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// The ladder of offices (docs/LADDER.md): the magistracies below the consulship. A rung is held by a PERSON, not a house, for a
/// term; it is filled by election every fifth year; it asks an age and, above the first, a rung already held. Nobody stands for
/// consul who has not been praetor. Every house puts forward whoever of its people is qualified and best; in the player's house a
/// person stands for themselves if it is what they want, the head may stand, and the head may put weight behind any of them.
/// <para>The houses vote with their weight in the chamber, for their own, then their patron's, then their party's candidate,
/// then whoever their head likes best; and a candidate carries something of their own besides: how they speak, how they are
/// liked, how far they have risen.</para>
/// Holding a rung pays a little, teaches the work, is written into the life, and adds to the house's standing in the Senate for
/// good. There is no ladder under a throne. RNG-free.
/// </summary>
public static partial class LadderSystem
{
    public const string Quaestor = "quaestor", Aedile = "aedile", Praetor = "praetor";
    public const int StandInfluence = 2, BackInfluence = 2, BackVotes = 6, Adult = DynastySystem.AgeOfMajority;
    // deeds
    public const string HeldFlag = "ladder_election_held", ElectedFlag = "ladder_elected", LostFlag = "ladder_lost", KinElectedFlag = "kin_elected", GamesFlag = "games_given";
    // each year
    public const string StandingFlag = "standing_for_office", MagistrateFlag = "is_magistrate", AedileFlag = "is_aedile";
    public const string ElectedChar = "newly_elected", StandingChar = "standing_for_office";
    public const string BlamedFlag = "kin_blames_the_head", BlamesChar = "blames_the_head"; public const int BlameRegard = 10;
    private static readonly string[] DeedFlags = { HeldFlag, ElectedFlag, LostFlag, KinElectedFlag, GamesFlag, ExposedFlag, KinExposedFlag, BlamedFlag, AuditFoundFlag, OverturnedForUsFlag, AbsentFlag, RemittedFlag, SilencedFlag, DeskExposedFlag, TrustFellFlag, TrustRestoredFlag, JuniorCaughtFlag, SpokenForFlag, ProsecutionWonFlag, ProsecutionLostFlag, BribeRefusedFlag, BribeOutFlag, KinAcquittedFlag };

    private static void Mark(World w, string flag) { w.WorldFlags.Add(flag); w.WorldCounters["mark:" + flag] = w.Turn; }
    private static uint Hash(string s) { uint h = 2166136261; foreach (char ch in s) h = (h ^ ch) * 16777619; return h; }

    public static bool Active(World w, OfficeCatalog offices) => !w.IsImperium && offices.Rungs.Count > 0;
    public static bool Due(World w, OfficeCatalog offices) => Active(w, offices) && w.Turn > 0 && w.Turn % offices.LadderEvery == 0;
    public static int NextIn(World w, OfficeCatalog offices) => w.Turn % offices.LadderEvery == 0 ? offices.LadderEvery : offices.LadderEvery - w.Turn % offices.LadderEvery;
    public static OfficeHolding? Seat(World w, string characterId) => w.Magistrates.FirstOrDefault(m => m.CharacterId == characterId);
    public static List<OfficeHolding> Holders(World w, string rungId) => w.Magistrates.Where(m => m.OfficeId == rungId).OrderBy(m => m.CharacterId, StringComparer.Ordinal).ToList();

    /// <summary>Someone who opens a saga already of an age and a rank to have climbed some of the ladder has climbed it. Once.</summary>
    public static void Seed(World w)
    {
        if (w.Counter("cursus_seeded") != 0) return; w.WorldCounters["cursus_seeded"] = 1;
        foreach (var c in w.Characters.Values.Where(c => c.Cursus.Count == 0 && c.CareerRank >= 1))
        {
            if (c.Age >= 28) c.Cursus.Add(Quaestor);
            if (c.Age >= 36 && c.CareerRank >= 2) c.Cursus.Add(Praetor);
        }
    }

    /// <summary>A saga does not open on an empty bench: the seats are filled from those who have already climbed, one of a house to
    /// a rung, the best first. The player's house is left to win its own. Once; they sit until the first election.</summary>
    public static void Open(World w, OfficeCatalog offices)
    {
        if (w.Counter("magistrates_seated") != 0) return; w.WorldCounters["magistrates_seated"] = 1;
        if (!Active(w, offices) || w.Magistrates.Count > 0) return;
        var mine = w.Protagonist?.HouseId; var taken = new HashSet<string>();
        foreach (var rung in offices.Rungs.Reverse())
        {
            var houses = new HashSet<string>();
            var fit = w.LivingCharacters().Where(c => c.HouseId != mine && c.HouseId != PirateSystem.HouseId && !taken.Contains(c.Id) && c.Age >= rung.MinAge && c.CareerRank >= rung.MinRank && c.CareerTrack.Length > 0
                    && (rung.Requires.Length == 0 || c.Cursus.Contains(rung.Requires)) && (rung.Id == Praetor ? c.Cursus.Contains(Praetor) : !c.Cursus.Contains(Praetor))
                    && !c.Flags.Contains(LifeSystem.RetiredChar) && !c.Flags.Contains(DynastySystem.HiddenFlag))
                .OrderByDescending(Merit).ThenBy(c => c.Id, StringComparer.Ordinal);
            foreach (var c in fit)
            {
                if (houses.Count >= rung.Seats) break; if (!houses.Add(c.HouseId)) continue;
                taken.Add(c.Id); if (!c.Cursus.Contains(rung.Id)) c.Cursus.Add(rung.Id);
                w.Magistrates.Add(new OfficeHolding { OfficeId = rung.Id, HouseId = c.HouseId, CharacterId = c.Id, SinceTurn = w.Turn });
            }
        }
    }

    /// <summary>Why this person may not stand for this rung, or null.</summary>
    public static string? Bar(World w, OfficeCatalog offices, Character? c, RungDef? rung)
    {
        if (c?.Alive != true || rung == null) return "no such office";
        if (!Active(w, offices)) return "there are no elections under a throne";
        if (!PolitySystem.InReach(w, c.HouseId)) return "the house has left the Reach";
        if (w.Counter(BarredKey(c.Id)) > w.Turn) return $"they were removed from office for taking money, and may not stand until year {w.Counter(BarredKey(c.Id))}";
        if (c.Age < rung.MinAge) return $"not before {rung.MinAge}";
        if (c.CareerRank < rung.MinRank || c.CareerTrack.Length == 0) return "they must first have a place in a service";
        if (rung.Requires.Length > 0 && !c.Cursus.Contains(rung.Requires)) return $"they must first have been {offices.Rung(rung.Requires)?.Name.ToLowerInvariant() ?? rung.Requires}";
        if (c.Cursus.Contains(rung.Id)) return "they have held it";
        int at = IndexOf(offices, rung.Id); if (c.Cursus.Any(id => IndexOf(offices, id) > at)) return "they have already gone higher";
        if (c.Flags.Contains(LifeSystem.RetiredChar) || c.Flags.Contains(DynastySystem.HiddenFlag)) return "they have laid their work down";
        return null;
    }

    private static int IndexOf(OfficeCatalog offices, string rungId) { for (int i = 0; i < offices.Rungs.Count; i++) if (offices.Rungs[i].Id == rungId) return i; return -1; }

    /// <summary>The highest rung this person could stand for now.</summary>
    public static RungDef? Next(World w, OfficeCatalog offices, Character c) => offices.Rungs.Reverse().FirstOrDefault(r => Bar(w, offices, c, r) == null);

    /// <summary>What a candidate brings of their own: how they speak, how they are liked, how far they have risen, what they have done.</summary>
    public static int Merit(Character c) => c.Skill("oratory") * 2 + c.Skill("charisma") * 2 + c.CareerRank * 2 + LifeSystem.Renown(c) / 6;

    /// <summary>Everyone standing for a rung: from each other house its best qualified person, about two houses in three; from the
    /// player's house those who have said they will stand.</summary>
    public static List<Character> Candidates(World w, OfficeCatalog offices, RungDef rung, ISet<string>? seated = null)
    {
        var mine = w.Protagonist?.HouseId; var list = new List<Character>();
        foreach (var h in w.Houses.Values.Where(h => h.Id != PirateSystem.HouseId && h.Id != mine).OrderBy(h => h.Id, StringComparer.Ordinal))
        {
            if (Hash(h.Id + ":stand:" + rung.Id + ":" + w.Turn / offices.LadderEvery) % 3 == 0) continue;
            // a person stands for the highest rung open to them, so a house's best is not wasted on a lower one; one who stood
            // higher this year and was not chosen may still be put up for this, if the house has nobody whose rung it is
            var best = w.LivingCharacters().Where(c => c.HouseId == h.Id && Bar(w, offices, c, rung) == null && (seated == null ? Next(w, offices, c)?.Id == rung.Id : !seated.Contains(c.Id)))
                .OrderByDescending(c => Next(w, offices, c)?.Id == rung.Id).ThenByDescending(Merit).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();
            if (best != null) list.Add(best);
        }
        foreach (var cd in w.Candidacies.Where(cd => cd.RungId == rung.Id))
            if (w.Char(cd.CharacterId) is { } c && c.HouseId == mine && Bar(w, offices, c, rung) == null) list.Add(c);
        return list;
    }

    /// <summary>The count: each house's weight to a candidate, and each candidate's own merit, backing and canvass.</summary>
    public static Dictionary<string, int> Tally(World w, OfficeCatalog offices, RungDef rung, List<Character> candidates, Func<string, int> weight)
    {
        var tally = candidates.ToDictionary(c => c.Id, c => Merit(c) + TrustVotes(w, c) + LifeSystem.TieVotes(w, c.HouseId) + (w.Candidacies.FirstOrDefault(cd => cd.CharacterId == c.Id && cd.RungId == rung.Id)?.Backing ?? 0) + w.Counter(OrderSystem.CanvassKey(c.HouseId)));
        if (candidates.Count == 0) return tally;
        foreach (var h in w.Houses.Values.Where(h => h.Id != PirateSystem.HouseId).OrderBy(h => h.Id, StringComparer.Ordinal))
        {
            var party = FactionSystem.Of(w, h.Id); var head = Economy.HeadOf(w, h);
            int Liking(Character c) => head != null && w.House(c.HouseId) is { } ch && Economy.HeadOf(w, ch) is { } chead ? w.GetRelationship(head.Id, chead.Id)?.Disposition ?? 0 : 0;
            var vote = candidates.Where(c => c.HouseId == h.Id).OrderByDescending(Merit).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault()
                ?? (h.PatronId.Length > 0 ? candidates.Where(c => c.HouseId == h.PatronId).OrderByDescending(Merit).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault() : null)
                ?? (party != null ? candidates.Where(c => party.Members.Contains(c.HouseId)).OrderByDescending(Merit).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault() : null)
                ?? candidates.OrderByDescending(Liking).ThenByDescending(Merit).ThenBy(c => c.Id, StringComparer.Ordinal).First();
            tally[vote.Id] += weight(h.Id);
        }
        return tally;
    }

    /// <summary>The election: every rung's term ends, and every rung is filled from those standing.</summary>
    public static void Hold(World w, OfficeCatalog offices, Func<string, int> weight)
    {
        if (!Active(w, offices)) return;
        var p = w.Protagonist; var mine = p?.HouseId; var taken = new HashSet<string>(); var lines = new List<string>();
        w.Magistrates.Clear();
        foreach (var rung in offices.Rungs.Reverse())   // the highest first: someone who could have a greater rung is not also given a lesser
        {
            var candidates = Candidates(w, offices, rung, taken).Where(c => !taken.Contains(c.Id)).ToList();
            var tally = Tally(w, offices, rung, candidates, weight);
            var winners = candidates.OrderByDescending(c => tally[c.Id]).ThenBy(c => c.Id, StringComparer.Ordinal).Take(rung.Seats).ToList();
            foreach (var c in winners)
            {
                taken.Add(c.Id); w.Magistrates.Add(new OfficeHolding { OfficeId = rung.Id, HouseId = c.HouseId, CharacterId = c.Id, SinceTurn = w.Turn });
                if (!c.Cursus.Contains(rung.Id)) c.Cursus.Add(rung.Id);
                LifeSystem.Note(w, c, "office", $"Elected {rung.Name.ToLowerInvariant()}.");
                if (c.HouseId == mine) { c.Flags.Add(ElectedChar); if (c.Id == p!.Id) Mark(w, ElectedFlag); else Mark(w, KinElectedFlag); }
            }
            foreach (var c in winners.Where(c => c.HouseId == mine && c.Id != p!.Id && (w.Candidacies.FirstOrDefault(cd => cd.CharacterId == c.Id)?.Backing ?? 0) > 0))
            { var owed = w.Relationship(c.Id, p!.Id); owed.Disposition = Math.Clamp(owed.Disposition + 6, -100, 100); }   // one who was spoken for and won knows what they owe
            foreach (var c in candidates.Where(c => c.HouseId == mine && !winners.Contains(c)))
            {
                AmbitionSystem.Setback(w, c, $"Stood for {rung.Name.ToLowerInvariant()} and was not chosen.");   // written into their life; it costs them, and counts against what they want
                if (c.Id == p!.Id) Mark(w, LostFlag);
                else if ((w.Candidacies.FirstOrDefault(cd => cd.CharacterId == c.Id)?.Backing ?? 0) == 0 && w.Candidacies.Any(cd => cd.CharacterId == c.Id))
                {
                    // they stood, the head said nothing for them, and they lost: it is the head they blame
                    var r = w.Relationship(c.Id, p.Id); r.Disposition = Math.Clamp(r.Disposition - BlameRegard, -100, 100);
                    c.Flags.Add(BlamesChar); Mark(w, BlamedFlag);
                    w.Log($"{c.Name} stood alone and lost, and says so: {p.Name} did not speak one word for them.");
                }
                w.Log($"{c.Name} stood for {rung.Name.ToLowerInvariant()} and was not chosen ({tally[c.Id]} against {(winners.Count > 0 ? tally[winners[^1].Id] : 0)}).");
            }
            if (winners.Count > 0) lines.Add($"{rung.Name.ToLowerInvariant()}s {string.Join(", ", winners.Select(c => $"{c.Name} ({tally[c.Id]})"))}");
        }
        w.Candidacies.Clear(); Mark(w, HeldFlag);
        if (lines.Count > 0) w.Log($"The Reach elects its magistrates: {string.Join("; ", Enumerable.Reverse(lines))}.");
    }

    // ---------------- what the head and their own can do ----------------

    public static string? StandRefusal(World w, OfficeCatalog offices, Character? head, Character? who, string rungId)
    {
        if (head?.Alive != true || who?.Alive != true || who.HouseId != head.HouseId) return "not of your house";
        if (who.Id != head.Id) return "whether they stand is their own affair";
        if (w.Candidacies.Any(c => c.CharacterId == who.Id)) return "you are standing already";
        return Bar(w, offices, who, offices.Rung(rungId));
    }
    public static void Stand(World w, OfficeCatalog offices, Character who, string rungId)
    {
        w.Candidacies.Add(new Candidacy { RungId = rungId, CharacterId = who.Id }); who.Flags.Add(StandingChar);
        w.Log($"{who.Name} will stand for {offices.Rung(rungId)?.Name.ToLowerInvariant()} at the next election.");
    }
    public static string? BackRefusal(World w, Character? head, Character? who)
    {
        if (head?.Alive != true || who?.Alive != true || who.HouseId != head.HouseId) return "not of your house";
        var cd = w.Candidacies.FirstOrDefault(c => c.CharacterId == who.Id); if (cd == null) return "they are not standing";
        return cd.Backing >= BackVotes * 2 ? "you have done what can be done for them" : null;
    }
    /// <summary>The head puts their weight behind someone of the house who is standing.</summary>
    public static void Back(World w, Character head, Character who)
    {
        var cd = w.Candidacies.First(c => c.CharacterId == who.Id); cd.Backing += BackVotes;
        if (who.Id != head.Id) { var r = w.Relationship(who.Id, head.Id); r.Disposition = Math.Clamp(r.Disposition + 6, -100, 100); }
        w.Log(who.Id == head.Id ? $"{head.Name} calls in what is owed for the election." : $"{head.Name} speaks for {who.Name} among the houses.");
    }

    private static string AskedKey(string id) => "asked_down:" + id;
    private static int Term(World w, OfficeCatalog offices) => w.Turn / Math.Max(1, offices.LadderEvery) + 1;
    public static string? DissuadeRefusal(World w, OfficeCatalog offices, Character? head, Character? who)
    {
        if (head?.Alive != true || who?.Alive != true || who.HouseId != head.HouseId || who.Id == head.Id) return "not one of your house";
        if (!w.Candidacies.Any(c => c.CharacterId == who.Id)) return "they are not standing";
        return w.Counter(AskedKey(who.Id)) == Term(w, offices) ? "you have asked them once" : null;
    }
    /// <summary>The head asks one of the house not to stand. It is their name and their choice: the loyal withdraw, the wilful
    /// stay in and remember being asked. True if they stood down.</summary>
    public static bool Dissuade(World w, OfficeCatalog offices, Character head, Character who)
    {
        w.WorldCounters[AskedKey(who.Id)] = Term(w, offices); var r = w.Relationship(who.Id, head.Id);
        if (CourtshipSystem.Wilful(who) && w.FamilyAutonomy > 0)
        {
            r.Disposition = Math.Clamp(r.Disposition - 5, -100, 100);
            w.Log($"{head.Name} asks {who.Name} not to stand. {who.Name} hears it out, and leaves their name where it is."); return false;
        }
        w.Candidacies.RemoveAll(c => c.CharacterId == who.Id); who.Flags.Remove(StandingChar); r.Disposition = Math.Clamp(r.Disposition - 8, -100, 100);
        w.Log($"{head.Name} asks {who.Name} not to stand. {who.Name} takes their name down, and does not pretend to be glad of it."); return true;
    }

    public static string? GamesRefusal(World w, OfficeCatalog offices, Character? who)
    {
        var seat = who != null ? Seat(w, who.Id) : null; var rung = seat != null ? offices.Rung(seat.OfficeId) : null;
        if (who?.Alive != true || rung == null || rung.Games <= 0) return "only an aedile gives games";
        if (w.Counter("games:" + who.Id) == seat!.SinceTurn + 1) return "you have given your games";
        return who.Purse >= rung.Games ? null : $"it takes {rung.Games} credits of your own";
    }
    /// <summary>An aedile gives games out of their own purse: the house is talked of, and it is written into their life.</summary>
    public static void Games(World w, OfficeCatalog offices, Character who)
    {
        var seat = Seat(w, who.Id)!; var rung = offices.Rung(seat.OfficeId)!; who.Purse -= rung.Games; w.WorldCounters["games:" + who.Id] = seat.SinceTurn + 1;
        if (w.House(who.HouseId) is { } h) h.Legitimacy += 2;
        LifeSystem.Note(w, who, "office", "Gave games as aedile that the city remembered."); if (who.HouseId == w.Protagonist?.HouseId) Mark(w, GamesFlag);
        w.Log($"{who.Name}, aedile, gives games at their own cost. The city talks of nothing else for a month.");
    }

    /// <summary>What the rungs its people have climbed add to a house's standing in the Senate.</summary>
    public static int SenateStanding(World w, OfficeCatalog offices, House h)
    {
        // the highest rung anyone living of the house has reached, and one more for each other who has climbed at all
        var climbed = w.LivingCharacters().Where(c => c.HouseId == h.Id && c.Cursus.Count > 0).Select(c => c.Cursus.Max(id => offices.Rung(id)?.Standing ?? 0)).ToList();
        return climbed.Count == 0 ? 0 : Math.Min(14, climbed.Max() + climbed.Count - 1);
    }

    // ---------------- the work of office ----------------
    // The owner's rule (docs/PROCESS.md): every position has work. A quaestor keeps the Reach's accounts, an aedile sees to its works,
    // a praetor hears a case. Each year, each of them. The player decides the business of an office the player holds; everyone else
    // decides their own by their nature. What they decide moves the public's trust in the magistrates, and that is one of the things
    // that can be wrong with the Reach.

    public const string Accounts = "accounts", Works = "works", Case = "case";
    public const string Straight = "straight", Favour = "favour", Relief = "relief", Lanes = "lanes", Grain = "grain", Nothing = "nothing", Law = "law", Friend = "friend", Gift = "gift";
    public const string TrustKey = "public_trust", ExposedFlag = "office_exposed", KinExposedFlag = "kin_office_exposed", BusinessFlag = "office_business", ExposedChar = "removed_from_office";
    public const int TrustStart = 60, TrustLow = 35, FavourCredits = 8, WorksCredits = 6, GiftCredits = 10, Damages = 5, BarYears = 5, ReliefBy = 2, ReliefCredits = 4, TrustCeiling = 85;
    public static string BarredKey(string characterId) => "barred_office:" + characterId;

    /// <summary>What the Reach thinks of its magistrates, 0 to 100. Low, and they are known to be for sale.</summary>
    public static int Trust(World w) => w.WorldCounters.TryGetValue(TrustKey, out int v) ? Math.Clamp(v, 0, 100) : TrustStart;
    /// <summary>Straight dealing is what is expected: it mends trust, slowly, and never past the point where people stop looking.</summary>
    /// Ordinary honest dealing (a single point) only mends trust back to where it starts: nobody is admired for doing the job. Something
    /// notable (two points or more: a thief caught, a bent ruling overturned, a region relieved) can lift it past that.
    /// And it mends by one point a year at the most, however many of them deal straight: a year of honest magistrates is a year.
    private static void MoveTrust(World w, int by)
    {
        int t = Trust(w);
        if (by <= 0) { w.WorldCounters[TrustKey] = Math.Max(0, t + by); return; }
        if (by == 1) { if (t >= TrustStart || w.Counter(MendedKey) == w.Turn + 1) return; w.WorldCounters[MendedKey] = w.Turn + 1; w.WorldCounters[TrustKey] = t + 1; return; }
        w.WorldCounters[TrustKey] = Math.Max(t, Math.Min(TrustCeiling, t + by));
    }
    private const string MendedKey = "trust_mended_in";
    public static string KindOf(RungDef rung) => rung.Id == Praetor ? Case : rung.Games > 0 ? Works : Accounts;
    public static string[] ChoicesFor(string kind) => kind == Case ? new[] { Law, Settle, Friend, Gift } : kind == Works ? new[] { Lanes, Grain, Both, Nothing } : new[] { Straight, Relief, Favour };

    // ---------------- the facts of the year's business (docs/LADDER.md, "The facts") ----------------

    public const string Settle = "settle", Both = "both"; public const int Plain = 3, Arguable = 2, Doubtful = 1;
    private static readonly (string id, string says)[] Matters =
    {
        ("boundary", "a boundary between two estates"), ("debt", "a debt of long standing"), ("contract", "a contract that was not honoured"), ("inheritance", "a contested inheritance"), ("salvage", "the salvage of a wreck")
    };
    private static readonly (string id, string says)[] Surpluses =
    {
        ("dues", "harbour dues that came in over the estimate"), ("fines", "fines the courts collected"), ("levy", "a levy that was overcollected"), ("escheat", "an estate that fell to the Reach for want of an heir")
    };
    /// <summary>What turns on it: a case's damages, the accounts' surplus. Five where the business was made before there were facts.</summary>
    public static int StakeOf(OfficeBusiness b) => b.Stake > 0 ? b.Stake : Damages;
    /// <summary>How plain the law is in a case. Plain, where it was made before there were facts.</summary>
    public static int WeightOf(OfficeBusiness b) => b.Weight > 0 ? b.Weight : Plain;
    public static string WeightSays(OfficeBusiness b) => WeightOf(b) == Plain ? "The law, read plainly, is with" : WeightOf(b) == Arguable ? "The law is arguable, and leans to" : "The law is doubtful. If anything it leans to";
    private static string MatterSays(OfficeBusiness b) => Matters.Concat(Surpluses).FirstOrDefault(m => m.id == b.Matter).says ?? "";
    /// <summary>What a ruling against the law costs the Reach's trust: the plainer the law, the more.</summary>
    public static int BentCost(OfficeBusiness b) => WeightOf(b) == Plain ? 4 : WeightOf(b) == Arguable ? 3 : 1;
    private static void Facts(World w, OfficeBusiness b)
    {
        if (b.Kind == Case && b.HouseA.Length > 0)
        {
            b.Matter = Matters[(int)(Hash(b.Id + ":matter") % (uint)Matters.Length)].id;
            uint x = Hash(b.Id + ":weight") % 10; b.Weight = x < 4 ? Plain : x < 8 ? Arguable : Doubtful;
            b.Stake = Damages * (1 + (int)(Hash(b.Id + ":stake") % 3));
        }
        else if (b.Kind == Accounts)
        {
            b.Matter = Surpluses[(int)(Hash(b.Id + ":matter") % (uint)Surpluses.Length)].id;
            b.Stake = Math.Min(4 + (int)(Hash(b.Id + ":stake") % 9), Math.Max(0, RegionSystem.CapitalHolder(w)?.Treasury.Credits ?? 0));
        }
    }
    public static OfficeBusiness? BusinessOf(World w, string characterId) => w.OfficeWork.FirstOrDefault(b => b.HolderId == characterId && b.Choice.Length == 0 && b.Kind is Accounts or Works or Case);

    private static int Regard(World w, House? of, House? toward)
    {
        var a = of != null ? Economy.HeadOf(w, of) : null; var b = toward != null ? Economy.HeadOf(w, toward) : null;
        return a != null && b != null && a.Id != b.Id ? w.GetRelationship(a.Id, b.Id)?.Disposition ?? 0 : 0;
    }
    private static void Shift(World w, House? of, House? toward, int by)
    {
        var a = of != null ? Economy.HeadOf(w, of) : null; var b = toward != null ? Economy.HeadOf(w, toward) : null;
        if (a == null || b == null || a.Id == b.Id) return; var r = w.Relationship(a.Id, b.Id); r.Disposition = Math.Clamp(r.Disposition + by, -100, 100);
    }
    private static string? WorstRegion(World w, HashSet<string> taken) => w.Regions.Where(kv => kv.Value.Lawlessness >= 30 && !taken.Contains("region:" + kv.Key)).OrderByDescending(kv => kv.Value.Lawlessness).ThenBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => kv.Key).FirstOrDefault();
    private static string? HomeRegion(World w, string houseId) => w.Galaxy == null ? null : w.HoldingsOf(houseId).Where(h => h.IsPlanet).Select(h => w.Galaxy.System(h.SystemId)?.RegionId ?? "").Where(r => r.Length > 0)
        .GroupBy(r => r).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).Select(g => g.Key).FirstOrDefault();

    /// <summary>This year's business for a sitting magistrate.</summary>
    private static OfficeBusiness Make(World w, OfficeHolding seat, RungDef rung, HashSet<string> taken)
    {
        var b = new OfficeBusiness { Id = $"biz_{w.Turn}_{seat.CharacterId}", RungId = rung.Id, HolderId = seat.CharacterId, Kind = KindOf(rung), Turn = w.Turn };
        if (b.Kind == Accounts) { b.RegionId = (RegionSystem.CapitalHolder(w)?.Treasury.Credits ?? 0) >= ReliefCredits ? WorstRegion(w, taken) ?? "" : ""; if (b.RegionId.Length > 0) taken.Add("region:" + b.RegionId); }   // one quaestor to a region, and only if the purse has it
        else if (b.Kind == Works) b.RegionId = HomeRegion(w, seat.HouseId) ?? "";
        else
        {
            // two houses with a quarrel, neither the judge's own: those at war first, then those whose heads think worst of each other
            var houses = w.Houses.Values.Where(h => h.Id != PirateSystem.HouseId && h.Id != seat.HouseId && PolitySystem.InReach(w, h.Id) && Economy.HeadOf(w, h) != null && !taken.Contains(h.Id)).OrderBy(h => h.Id, StringComparer.Ordinal).ToList();
            var last = w.OfficeWork.Where(x => x.Kind == Case && x.Turn == w.Turn - 1 && x.HouseA.Length > 0).Select(x => string.CompareOrdinal(x.HouseA, x.HouseB) < 0 ? x.HouseA + "|" + x.HouseB : x.HouseB + "|" + x.HouseA).ToHashSet();
            var pairs = new List<(House a, House b, int heat)>();
            for (int i = 0; i < houses.Count; i++) for (int j = i + 1; j < houses.Count; j++)
            {
                int heat = (w.AtWar(houses[i].Id, houses[j].Id) ? 100 : 0) - Regard(w, houses[i], houses[j]) - Regard(w, houses[j], houses[i]);
                if (heat >= 30 && !last.Contains(houses[i].Id + "|" + houses[j].Id)) pairs.Add((houses[i], houses[j], heat));   // a quarrel heard last year is not heard again this year
            }
            var pick = pairs.OrderByDescending(x => x.heat).ThenBy(x => x.a.Id, StringComparer.Ordinal).ThenBy(x => x.b.Id, StringComparer.Ordinal).Take(6).ToList();
            if (pick.Count > 0) { var x = pick[(int)(Hash(b.Id) % (uint)pick.Count)]; b.HouseA = x.a.Id; b.HouseB = x.b.Id; }
            else if (houses.Count >= 2) { int i = (int)(Hash(b.Id) % (uint)houses.Count); b.HouseA = houses[i].Id; b.HouseB = houses[(i + 1) % houses.Count].Id; }
            if (b.HouseA.Length > 0) { b.RightId = Hash(b.Id + ":right") % 2 == 0 ? b.HouseA : b.HouseB; taken.Add(b.HouseA); taken.Add(b.HouseB); }
        }
        Facts(w, b);
        return b;
    }

    // ---------------- what the Reach's trust in its magistrates does (docs/LADDER.md) ----------------

    public const int TrustHigh = 75, ScandalYears = 10, TaintVotes = 6, TaintVotesLow = 12, RemovedVotes = 8;
    public const string TrustFellFlag = "trust_collapsed", TrustRestoredFlag = "trust_restored", TrustLowKey = "trust_is_low";
    public static string ScandalKey(string houseId) => "office_scandal:" + houseId;
    /// <summary>A house one of whose people was removed from office for taking money, inside the last ten years.</summary>
    public static bool Tainted(World w, string houseId) => w.WorldCounters.TryGetValue(ScandalKey(houseId), out int t) && w.Turn - t < ScandalYears;
    /// <summary>What a house's scandal costs it at a count: more when the magistrates are little trusted and the Reach wants clean hands.</summary>
    public static int TaintCost(World w, string houseId) => !Tainted(w, houseId) ? 0 : Trust(w) < 50 ? TaintVotesLow : TaintVotes;
    /// <summary>The votes a candidate loses for their house's scandal, and for having been removed from office themselves.</summary>
    public static int TrustVotes(World w, Character c) => -TaintCost(w, c.HouseId) - (w.WorldCounters.ContainsKey(BarredKey(c.Id)) ? RemovedVotes : 0);

    /// <summary>What the state of the magistrates' name is doing this year, said plainly.</summary>
    public static string TrustSays(World w)
    {
        int t = Trust(w);
        return t < TrustLow ? "The squares blame whoever is in office: the commons think worse of every house that holds one, every year, and the house that holds the capital loses standing with them. A house with a scandal to its name loses twice the votes at a count."
            : t < 50 ? "A house with a scandal to its name loses twice the votes at a count, because the Reach wants clean hands."
            : t >= TrustHigh ? "Office is honoured: every house that holds one gains standing, and the commons think the better of it."
            : "It does nothing either way while it stands here.";
    }

    /// <summary>The year's consequences of that trust: for the houses in office, for the commons, and for the house that holds the capital.</summary>
    public const int TrustEstates = 2;
    private static void TrustYear(World w)
    {
        int t = Trust(w); var chair = RegionSystem.CapitalHolder(w); bool was = w.Counter(TrustLowKey) > 0;
        var inOffice = w.Magistrates.Select(m => m.HouseId).Concat(w.Offices.Select(o => o.HouseId)).Distinct().OrderBy(id => id, StringComparer.Ordinal).ToList();
        if (t < TrustLow)
        {
            if (chair is { Legitimacy: > 20 }) chair.Legitimacy -= 1;             // it is the chair that is blamed for what sits under it
            // the commons most; the merchants, who must go to those courts and deal with those desks; and the magistrates' own
            // order, whose name it is
            foreach (var id in inOffice) { PolicySystem.Sway(w, id, "commons", -3); PolicySystem.Sway(w, id, "merchants", -TrustEstates); PolicySystem.Sway(w, id, "magistrates", -TrustEstates); }
            if (!was) { w.WorldCounters[TrustLowKey] = 1; Mark(w, TrustFellFlag); w.Log("The Reach no longer believes its magistrates can be had for less than money. It is said openly in the squares, and the houses in office are the ones it is said about."); }
            return;
        }
        if (t >= TrustHigh) foreach (var id in inOffice) { PolicySystem.Sway(w, id, "commons", 1); PolicySystem.Sway(w, id, "merchants", 1); PolicySystem.Sway(w, id, "magistrates", 1); if (w.House(id) is { Legitimacy: < 70 } h) h.Legitimacy += 1; }
        if (was && t >= 50) { w.WorldCounters.Remove(TrustLowKey); Mark(w, TrustRestoredFlag); w.Log("The magistrates of the Reach are believed again, more or less. It took years, and it was done by people who could have taken the money."); }
    }

    /// <summary>What it is, said to the one who must decide it.</summary>
    public static string Says(World w, OfficeCatalog offices, OfficeBusiness b)
    {
        string region = w.Galaxy?.Region(b.RegionId)?.Name ?? "";
        return b.Kind switch
        {
            Accounts => "The year's accounts of the Reach are yours to close." + (b.Stake > 0 ? $" They show {b.Stake} credits over, from {MatterSays(b)}." : " They balance to the credit: there is nothing over.")
                + (region.Length > 0 ? $" {region} is lawless ({RegionSystem.StateOf(w, b.RegionId).Lawlessness} in a hundred), and what is over could go to it." : ""),
            Works => "The city's works and the lanes are yours to see to this year, out of your own purse." + (region.Length > 0 ? $" The lanes of {region}, where your house's worlds are, want repair ({RegionSystem.StateOf(w, b.RegionId).Lawlessness} in a hundred lawless)." : ""),
            _ => b.HouseA.Length == 0 ? "No case of weight has come before you this year."
                : $"{w.House(b.HouseA)?.Name} and {w.House(b.HouseB)?.Name} are before your court{(MatterSays(b).Length > 0 ? " over " + MatterSays(b) : "")}. {StakeOf(b)} credits turn on it. {WeightSays(b)} {w.House(b.RightId)?.Name}."
        };
    }
    public static string ChoiceSays(World w, OfficeBusiness b, string choice)
    {
        var holder = w.Char(b.HolderId); var mine = holder != null ? w.House(holder.HouseId) : null;
        string friend = b.Kind == Case && b.HouseA.Length > 0 ? (w.House(FriendOf(w, b, mine))?.Name ?? "") : "";
        string richer = b.Kind == Case && b.HouseA.Length > 0 ? (w.House(RicherOf(w, b))?.Name ?? "") : "";
        return choice switch
        {
            Straight => "Close them as they stand. The magistrates are trusted a little more, and so is your house.",
            Relief => b.RegionId.Length > 0 ? $"Send the {ReliefSum(b)} credits to {w.Galaxy?.Region(b.RegionId)?.Name}: its lanes are safer by {ReliefFor(b)}, and its houses remember who did it." : "There is nowhere that needs it this year: close them as they stand.",
            Favour => $"Let {FavourSum(b)} credits find their way to your house. The magistrates are trusted less, and one time in {(FavourSum(b) >= BigSum ? "three" : "four")} it comes out{(FavourSum(b) >= BigSum ? ": it is a large sum to lose" : "")}.",
            Both => $"Lanes and grain both, for {WorksCredits * 2} credits of your own: the lanes are safer, your house is thought the better of, and it is remembered as a generous year.",
            Settle => b.HouseA.Length == 0 ? "Sit, and rise." : WeightOf(b) == Doubtful ? $"Bring them to terms: {w.House(b.RightId)?.Name} takes half of what it asked. Where the law is doubtful this is what an honest court does, and both houses think the better of yours."
                : $"Bring them to terms: {w.House(b.RightId)?.Name} takes half of what the law gives it. The other house thinks the better of yours; {w.House(b.RightId)?.Name} had the law, and knows it.",
            Lanes => b.RegionId.Length > 0 ? $"Repair the lanes of {w.Galaxy?.Region(b.RegionId)?.Name} for {WorksCredits} credits of your own." : $"There is no region of your house's to repair: the {WorksCredits} credits go on the capital's streets.",
            Grain => $"Grain for the squares, for {WorksCredits} credits of your own: your house is thought the better of.",
            Nothing => "Do nothing. Nothing is spent, and if you could have paid it is noticed.",
            Law => b.HouseA.Length == 0 ? "Sit, and rise." : $"Rule on the law, for {w.House(b.RightId)?.Name}. The loser pays {StakeOf(b)} credits and thinks the less of you; the winner the better.",
            Friend => b.HouseA.Length == 0 ? "Sit, and rise." : $"Rule for {friend}, which thinks the better of your house." + (FriendOf(w, b, mine) == b.RightId ? " As it happens the law is with them."
                : WeightOf(b) == Doubtful ? " The law leans the other way, but it is doubtful enough that the ruling can be defended: it will not be appealed or held against you." : $" The law is not with them, and it will be seen: it may be appealed, and it stays on the record."),
            Gift => b.HouseA.Length == 0 ? "Sit, and rise." : $"Take {GiftCredits} credits from {richer}, and rule for them. One time in three it comes out.",
            _ => ""
        };
    }
    /// <summary>Old ties in a court (docs/LIFE.md): a magistrate who has been the player's friend for years finds for the player's
    /// house when it is before them, and an old enemy for the other side. The upright rule on the law, whoever is before them.</summary>
    public static string? TieWinner(World w, OfficeBusiness b, Character judge)
    {
        var mine = w.Protagonist?.HouseId; if (mine == null || judge.HouseId == mine || (b.HouseA != mine && b.HouseB != mine)) return null;
        return judge.Flags.Contains(LifeSystem.OldFriendChar) ? mine : judge.Flags.Contains(LifeSystem.OldEnemyChar) ? (b.HouseA == mine ? b.HouseB : b.HouseA) : null;
    }
    private static string FriendOf(World w, OfficeBusiness b, House? judge) => Regard(w, w.House(b.HouseA), judge) >= Regard(w, w.House(b.HouseB), judge) ? b.HouseA : b.HouseB;
    private static string RicherOf(World w, OfficeBusiness b) => (w.House(b.HouseA)?.Treasury.Credits ?? 0) >= (w.House(b.HouseB)?.Treasury.Credits ?? 0) ? b.HouseA : b.HouseB;

    public static string? DecideRefusal(World w, Character? who, string choice)
    {
        var b = who != null ? BusinessOf(w, who.Id) : null; if (who?.Alive != true || b == null) return "your office has no business waiting";
        if (!ChoicesFor(b.Kind).Contains(choice)) return "not a thing this office can do";
        if (choice == Relief && b.RegionId.Length == 0) return "no region is lawless enough to need it this year, or the capital's purse is empty";
        if (choice == Settle && b.HouseA.Length == 0) return "there is nobody before you to bring to terms";
        if (choice == Both && Funds(w, who) < WorksCredits * 2) return $"it takes {WorksCredits * 2} credits of your own";
        return choice is Lanes or Grain && Funds(w, who) < WorksCredits ? $"it takes {WorksCredits} credits of your own" : null;
    }
    public const int BigSum = 9;
    /// <summary>What the quaestor can send to a region: the year's surplus, or the old fixed sum where there are no figures.</summary>
    public static int ReliefSum(OfficeBusiness b) => b.Stake > 0 ? b.Stake : ReliefCredits;
    public static int ReliefFor(OfficeBusiness b) => Math.Max(1, ReliefSum(b) / 2);
    public static int FavourSum(OfficeBusiness b) => b.Stake > 0 ? b.Stake : FavourCredits;
    private static int Funds(World w, Character who) => who.HouseId == w.Protagonist?.HouseId ? who.Purse : w.House(who.HouseId)?.Treasury.Credits ?? 0;
    private static void Pay(World w, Character who, int credits)
    {
        if (who.HouseId == w.Protagonist?.HouseId) who.Purse += credits; else if (w.House(who.HouseId) is { } h) h.Treasury.Credits += credits;
    }

    /// <summary>The holder decides the year's business.</summary>
    public static void Decide(World w, OfficeCatalog offices, Character who, string choice)
    {
        var b = BusinessOf(w, who.Id)!; b.Choice = choice; var house = w.House(who.HouseId); var mine = w.Protagonist?.HouseId; var chair = RegionSystem.CapitalHolder(w);
        string office = offices.Rung(b.RungId)?.Name.ToLowerInvariant() ?? b.RungId; bool ours = who.HouseId == mine; bool exposed = false;
        void Region(int by) { if (b.RegionId.Length > 0) { var rs = RegionSystem.StateOf(w, b.RegionId); rs.Lawlessness = Math.Clamp(rs.Lawlessness + by, 0, 100); } }
        void Legit(int by) { if (house != null) house.Legitimacy = Math.Clamp(house.Legitimacy + by, 0, 100); }
        switch (choice)
        {
            case Straight: MoveTrust(w, 1); if (house is { Legitimacy: < 80 }) Legit(1); break;
            case Relief:
                if (b.RegionId.Length == 0) { MoveTrust(w, 1); break; }
                Region(-ReliefFor(b)); MoveTrust(w, 1); if (chair != null) chair.Treasury.Credits -= Math.Min(ReliefSum(b), Math.Max(0, chair.Treasury.Credits));
                foreach (var h in w.Houses.Values.Where(h => h.Id != who.HouseId && HomeRegion(w, h.Id) == b.RegionId).OrderBy(h => h.Id, StringComparer.Ordinal)) Shift(w, h, house, 6);
                if (ours) w.Log($"{who.Name}, {office}, finds the money for {w.Galaxy?.Region(b.RegionId)?.Name}. Its lanes are the safer for it.");
                break;
            case Favour:
                if (house != null) house.Treasury.Credits += FavourSum(b); if (b.Stake > 0 && chair != null && chair.Id != who.HouseId) chair.Treasury.Credits -= Math.Min(b.Stake, Math.Max(0, chair.Treasury.Credits));
                MoveTrust(w, -3); exposed = Hash(b.Id + ":exposed") % (FavourSum(b) >= BigSum ? 3 : 4) == 0;
                break;
            case Both: Pay(w, who, -WorksCredits * 2); Region(-ReliefBy); Legit(3); MoveTrust(w, 1); break;   // good work, as either alone is: it mends a poor name, and does not gild a good one
            case Lanes: Pay(w, who, -WorksCredits); Region(-ReliefBy); MoveTrust(w, 1); break;
            case Grain: Pay(w, who, -WorksCredits); Legit(2); MoveTrust(w, 1); break;
            case Nothing: if (Funds(w, who) >= WorksCredits) MoveTrust(w, -1); break;   // one who could have paid and did not is noticed; one who could not is merely poor
            case Settle:
            {
                if (b.HouseA.Length == 0) break;
                var right = w.House(b.RightId); var other = w.House(b.RightId == b.HouseA ? b.HouseB : b.HouseA); b.WinnerId = b.RightId;
                if (right != null && other != null) { int half = Math.Min(StakeOf(b) / 2, Math.Max(0, other.Treasury.Credits)); other.Treasury.Credits -= half; right.Treasury.Credits += half; }
                Shift(w, other, house, 4); Shift(w, right, house, WeightOf(b) == Doubtful ? 4 : -3);
                if (WeightOf(b) == Doubtful) { MoveTrust(w, 1); if (house is { Legitimacy: < 80 }) Legit(1); }
                if (ours || b.HouseA == mine || b.HouseB == mine) w.Log($"{who.Name}, praetor, brings {right?.Name} and {other?.Name} to terms. {other?.Name} pays {StakeOf(b) / 2} credits.");
                if (b.RightId == mine) w.WorldFlags.Add("case_won"); else if (b.HouseA == mine || b.HouseB == mine) w.WorldFlags.Add("case_lost");
                break;
            }
            case Law: case Friend: case Gift:
                if (b.HouseA.Length == 0) break;
                string winner = choice == Law ? b.RightId : choice == Friend ? TieWinner(w, b, who) ?? FriendOf(w, b, house) : BoughtWinner(w, b, who) ?? RicherOf(w, b), loser = winner == b.HouseA ? b.HouseB : b.HouseA;
                var win = w.House(winner); var lose = w.House(loser); bool just = winner == b.RightId; b.WinnerId = winner;
                if (win != null && lose != null) { int paid = Math.Min(StakeOf(b), Math.Max(0, lose.Treasury.Credits)); lose.Treasury.Credits -= paid; win.Treasury.Credits += paid; }
                Shift(w, win, house, choice == Law ? 6 : choice == Friend ? 12 : 4); Shift(w, lose, house, choice == Law ? -6 : -12);
                if (choice == Law) { MoveTrust(w, 1); if (house is { Legitimacy: < 80 }) Legit(1); }
                else if (choice == Friend) { if (!just) { MoveTrust(w, -BentCost(b)); if (WeightOf(b) != Doubtful) Legit(-1); } }
                else
                {
                    string? buyer = BoughtWinner(w, b, who);   // one already paid is not paid again
                    if (buyer == null) { Pay(w, who, GiftCredits); if (win != null) win.Treasury.Credits -= Math.Min(GiftCredits, Math.Max(0, win.Treasury.Credits)); }
                    MoveTrust(w, -4); exposed = Hash(b.Id + ":exposed") % 3 == 0;
                    if (buyer != null && exposed) BribeOut(w, buyer, who);
                }
                if (ours || winner == mine || loser == mine)
                    w.Log($"{who.Name}, praetor, finds for {win?.Name} against {lose?.Name}{(just ? "" : WeightOf(b) == Doubtful ? ", where the law was doubtful" : ", against the plain reading of the law")}. {lose?.Name} pays {StakeOf(b)} credits.");
                if (winner == mine || loser == mine) w.WorldFlags.Add(winner == mine ? "case_won" : "case_lost");
                break;
        }
        string what = b.Kind == Case ? (choice == Law ? "Sat as praetor and ruled on the law." : choice == Settle ? "Sat as praetor and brought two houses to terms." : choice == Friend ? "Sat as praetor and found for a friend." : "Sat as praetor and took a gift.")
            : b.Kind == Works ? (choice == Nothing ? "" : choice == Both ? "Gave the Reach lanes and grain in one year as aedile." : "Saw to the Reach's works as aedile.") : choice == Favour ? "" : "Kept the Reach's accounts as quaestor.";
        if (what.Length > 0 && who.Life?.Marks.Add("business:" + b.RungId + ":" + (choice is Friend or Gift ? choice : "done")) != false) LifeSystem.Note(w, who, "office", what);
        if (choice == Favour || choice == Gift || (choice == Friend && b.WinnerId.Length > 0 && b.WinnerId != b.RightId && WeightOf(b) != Doubtful)) Record(w, who, b, choice, exposed);
        if (exposed) Expose(w, offices, who, b);
    }

    /// <summary>It comes out. The magistrate is removed, may not stand again for five years, and the house is thought the less of.</summary>
    private static void Expose(World w, OfficeCatalog offices, Character who, OfficeBusiness b)
    {
        var mine = w.Protagonist; var house = w.House(who.HouseId);
        w.Magistrates.RemoveAll(m => m.CharacterId == who.Id); w.WorldCounters[BarredKey(who.Id)] = w.Turn + BarYears; w.WorldCounters[ScandalKey(who.HouseId)] = w.Turn; MoveTrust(w, -3);
        if (house != null) house.Legitimacy = Math.Clamp(house.Legitimacy - 5, 0, 100);
        who.Flags.Add(ExposedChar); LifeSystem.Note(w, who, "office", "Was removed from office for taking money.");
        if (mine != null && who.HouseId == mine.HouseId) Mark(w, who.Id == mine.Id ? ExposedFlag : KinExposedFlag);
        w.Log($"It comes out that {who.Name} of {house?.Name}, {offices.Rung(b.RungId)?.Name.ToLowerInvariant()}, took money in office. {who.Name} is removed and may not stand again for {BarYears} years.");
    }

    /// <summary>What someone other than the player does with their year's business: by their nature.</summary>
    public static string Inclination(World w, Character c, OfficeBusiness b)
    {
        bool honest = c.NatureTraits.Contains("Honorable") || c.NatureTraits.Contains("Just");
        bool grasping = !honest && (c.NatureTraits.Contains("Ruthless") || c.NatureTraits.Contains("Cruel") || c.Ambition == "amass_a_fortune");
        bool partial = !honest && (c.NatureTraits.Contains("Loyal") || c.NatureTraits.Contains("Vengeful") || Hash(c.Id + ":partial") % 3 == 0);
        if (b.Kind == Case && BoughtWinner(w, b, c) != null) return Gift;             // bought, and the buyer is before them
        if (b.Kind == Case && !honest && TieWinner(w, b, c) != null) return Friend;   // an old friend, or an old enemy, of one of the parties
        if (b.Kind == Case) return grasping && Hash(b.Id + ":take") % 2 == 0 ? Gift : partial && !grasping ? Friend : b.HouseA.Length > 0 && WeightOf(b) == Doubtful ? Settle : Law;
        if (b.Kind == Works) return Funds(w, c) < WorksCredits ? Nothing
            : Funds(w, c) >= WorksCredits * 6 && (c.NatureTraits.Contains("Proud") || c.Ambition is "master_the_senate" or "outshine_all_rivals") ? Both
            : b.RegionId.Length > 0 && Hash(b.Id + ":works") % 2 == 0 ? Lanes : Grain;
        return grasping && Hash(b.Id + ":take") % 2 == 0 ? Favour : b.RegionId.Length > 0 && (honest || Hash(b.Id + ":relief") % 3 == 0) ? Relief : Straight;
    }

    /// <summary>The year's business: last year's that the player left undone is closed by the clerks; every sitting magistrate has new
    /// business; all but the player decide their own at once.</summary>
    private static void Business(World w, OfficeCatalog offices)
    {
        var p = w.Protagonist; w.WorldCounters.TryAdd(TrustKey, TrustStart);   // written down, so that what content does to it starts from where it stands
        CloseDuties(w);
        foreach (var b in w.OfficeWork.Where(b => b.Choice.Length == 0).ToList())
        {
            b.Choice = "left"; MoveTrust(w, -1);
            if (p != null && b.HolderId == p.Id) w.Log($"The business of {p.Name}'s office went undone last year, and the clerks closed it. It was noticed.");
        }
        w.OfficeWork.RemoveAll(b => b.Turn < w.Turn - 1);
        var taken = new HashSet<string>();
        foreach (var seat in w.Magistrates.OrderBy(m => m.OfficeId, StringComparer.Ordinal).ThenBy(m => m.CharacterId, StringComparer.Ordinal).ToList())
        {
            var rung = offices.Rung(seat.OfficeId); var who = w.Char(seat.CharacterId); if (rung == null || who is not { Alive: true }) continue;
            var b = Make(w, seat, rung, taken); w.OfficeWork.Add(b);
            if (p == null || who.Id != p.Id) Decide(w, offices, who, Inclination(w, who, b));
        }
        // above where it starts, a good name fades a point a year; below it, nothing mends it but honest dealing (a point a year, above)
        int t = Trust(w); if (t > TrustStart) w.WorldCounters[TrustKey] = t - 1;
        TrustYear(w);
    }

    // ---------------- the year ----------------

    public static void Tick(World w, OfficeCatalog offices, PolicyCatalog? policy = null)
    {
        w.WorldFlags.Remove(BenchFlag); w.WorldFlags.Remove(SeatFlag); w.WorldFlags.Remove(ChargeFlag); w.WorldFlags.Remove(DeskFlag);
        w.WorldFlags.Remove(StandingFlag); w.WorldFlags.Remove(MagistrateFlag); w.WorldFlags.Remove(AedileFlag); w.WorldFlags.Remove(BusinessFlag); w.WorldFlags.Remove("case_won"); w.WorldFlags.Remove("case_lost");
        foreach (var f in DeedFlags) if (w.WorldFlags.Contains(f) && w.Counter("mark:" + f) < w.Turn - 1) { w.WorldFlags.Remove(f); w.WorldCounters.Remove("mark:" + f); }
        foreach (var c in w.Characters.Values) { if (!w.HasFlag(ElectedFlag) && !w.HasFlag(KinElectedFlag)) c.Flags.Remove(ElectedChar); c.Flags.Remove(StandingChar); }
        if (!w.HasFlag(ExposedFlag) && !w.HasFlag(KinExposedFlag)) foreach (var c in w.Characters.Values) c.Flags.Remove(ExposedChar);
        if (!w.HasFlag(SpokenForFlag)) foreach (var c in w.Characters.Values) c.Flags.Remove(SpokenForChar);
        if (!w.HasFlag(BlamedFlag)) foreach (var c in w.Characters.Values) c.Flags.Remove(BlamesChar);
        if (!Active(w, offices)) { w.Magistrates.Clear(); w.Candidacies.Clear(); w.OfficeWork.Clear(); w.OfficeRecord.Clear(); return; }
        var p = w.Protagonist; var house = p != null ? w.House(p.HouseId) : null;

        // a seat lapses with its holder; a holder is paid
        foreach (var m in w.Magistrates.ToList())
        {
            var c = w.Char(m.CharacterId);
            if (c is not { Alive: true } || c.Flags.Contains(LifeSystem.FrailChar)) { w.Magistrates.Remove(m); continue; }
            m.HouseId = c.HouseId;
            if (offices.Rung(m.OfficeId) is { Stipend: > 0 } rung && house != null && c.HouseId == house.Id) c.Purse += rung.Stipend;
        }
        Business(w, offices); Duties(w, offices, policy); Courts(w, offices);
        if (p is not { Alive: true } || house == null) return;
        if (BusinessOf(w, p.Id) != null) w.WorldFlags.Add(BusinessFlag);
        if (DutyOf(w, p.Id, Hearing, Appeal) != null) w.WorldFlags.Add(BenchFlag);
        if (DutyOf(w, p.Id, SeatKind) != null) w.WorldFlags.Add(SeatFlag);
        if (DutyOf(w, p.Id, Charge) != null) w.WorldFlags.Add(ChargeFlag);
        if (DutyOf(w, p.Id, Desk, Posting, Brief, Ledgers, Watch) != null) w.WorldFlags.Add(DeskFlag);
        w.Candidacies.RemoveAll(cd => w.Char(cd.CharacterId) is not { Alive: true } c || c.HouseId != house.Id || Bar(w, offices, c, offices.Rung(cd.RungId)) != null);

        // The people of the house stand for themselves (docs/PROCESS.md): one who wants to rise and be heard, or to be first, puts
        // their name down two years before the count for the highest rung open to them.
        if (w.FamilyAutonomy > 0 && NextIn(w, offices) <= 2)
            foreach (var c in w.LivingCharacters().Where(c => c.HouseId == house.Id && c.Id != p.Id && c.Age >= Adult && c.Ambition is "master_the_senate" or "outshine_all_rivals" or "seize_the_throne").OrderBy(c => c.Id, StringComparer.Ordinal).ToList())
            {
                if (c.HouseId != house.Id || c.Id == p.Id || w.Candidacies.Any(cd => cd.CharacterId == c.Id) || w.Counter(AskedKey(c.Id)) == Term(w, offices)) continue;
                var rung = Next(w, offices, c); if (rung == null) continue;
                w.Candidacies.Add(new Candidacy { RungId = rung.Id, CharacterId = c.Id });
                w.Log($"{c.Name} has put their name down for {rung.Name.ToLowerInvariant()}.");
            }
        // an aedile of the house with the money gives their games without being told
        foreach (var m in w.Magistrates.Where(m => m.HouseId == house.Id && m.CharacterId != p.Id).ToList())
            if (w.Char(m.CharacterId) is { } kin && GamesRefusal(w, offices, kin) == null && kin.Purse >= (offices.Rung(m.OfficeId)?.Games ?? 0) + 5) Games(w, offices, kin);

        foreach (var cd in w.Candidacies) w.Char(cd.CharacterId)?.Flags.Add(StandingChar);
        if (w.Candidacies.Any(cd => cd.CharacterId == p.Id)) w.WorldFlags.Add(StandingFlag);
        if (Seat(w, p.Id) is { } mine) { w.WorldFlags.Add(MagistrateFlag); if ((offices.Rung(mine.OfficeId)?.Games ?? 0) > 0) w.WorldFlags.Add(AedileFlag); }
    }
}
