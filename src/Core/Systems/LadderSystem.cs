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
public static class LadderSystem
{
    public const string Quaestor = "quaestor", Aedile = "aedile", Praetor = "praetor";
    public const int StandInfluence = 2, BackInfluence = 2, BackVotes = 6, Adult = DynastySystem.AgeOfMajority;
    // deeds
    public const string HeldFlag = "ladder_election_held", ElectedFlag = "ladder_elected", LostFlag = "ladder_lost", KinElectedFlag = "kin_elected", GamesFlag = "games_given";
    // each year
    public const string StandingFlag = "standing_for_office", MagistrateFlag = "is_magistrate", AedileFlag = "is_aedile";
    public const string ElectedChar = "newly_elected", StandingChar = "standing_for_office";
    private static readonly string[] DeedFlags = { HeldFlag, ElectedFlag, LostFlag, KinElectedFlag, GamesFlag };

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
        var tally = candidates.ToDictionary(c => c.Id, c => Merit(c) + (w.Candidacies.FirstOrDefault(cd => cd.CharacterId == c.Id && cd.RungId == rung.Id)?.Backing ?? 0) + w.Counter(OrderSystem.CanvassKey(c.HouseId)));
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
            foreach (var c in candidates.Where(c => c.HouseId == mine && !winners.Contains(c)))
            {
                if (c.Id == p!.Id) { Mark(w, LostFlag); c.Stress += 5; }
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

    // ---------------- the year ----------------

    public static void Tick(World w, OfficeCatalog offices)
    {
        w.WorldFlags.Remove(StandingFlag); w.WorldFlags.Remove(MagistrateFlag); w.WorldFlags.Remove(AedileFlag);
        foreach (var f in DeedFlags) if (w.WorldFlags.Contains(f) && w.Counter("mark:" + f) < w.Turn - 1) { w.WorldFlags.Remove(f); w.WorldCounters.Remove("mark:" + f); }
        foreach (var c in w.Characters.Values) { if (!w.HasFlag(ElectedFlag) && !w.HasFlag(KinElectedFlag)) c.Flags.Remove(ElectedChar); c.Flags.Remove(StandingChar); }
        if (!Active(w, offices)) { w.Magistrates.Clear(); w.Candidacies.Clear(); return; }
        var p = w.Protagonist; var house = p != null ? w.House(p.HouseId) : null;

        // a seat lapses with its holder; a holder is paid
        foreach (var m in w.Magistrates.ToList())
        {
            var c = w.Char(m.CharacterId);
            if (c is not { Alive: true } || c.Flags.Contains(LifeSystem.FrailChar)) { w.Magistrates.Remove(m); continue; }
            m.HouseId = c.HouseId;
            if (offices.Rung(m.OfficeId) is { Stipend: > 0 } rung && house != null && c.HouseId == house.Id) c.Purse += rung.Stipend;
        }
        if (p is not { Alive: true } || house == null) return;
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
