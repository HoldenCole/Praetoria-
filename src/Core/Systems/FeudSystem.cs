using Praetoria.Core.Commands;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>Blood between houses (docs/FEUD.md; the owner's direction of 2026-11-17, docs/GAPS.md R4). A killing binds the dead person's
/// house to answer it. The debt is on the house, so it outlives the people; it is ended by blood (the one to blame, or the wrong blood,
/// which starts a debt the other way), by a duel, by a price, or by letting it lie at a cost in standing. Other houses carry and end
/// theirs by the same rules, each head by their nature. RNG-free outside a duel fought by command.</summary>
public static class FeudSystem
{
    public const string BegunFlag = "feud_begun", OwedToUsFlag = "blood_owed_to_us", WeOweFlag = "we_owe_blood", AnsweredFlag = "blood_answered", UnansweredFlag = "the_dead_unanswered";
    public const string PriceAskedFlag = "blood_price_asked", PriceOfferedFlag = "blood_price_offered", CalledOutFlag = "called_out", DuelFoughtFlag = "duel_fought", LetLieFlag = "blood_let_lie";
    public const string PayFlag = "feud_answer_pay", RefuseFlag = "feud_answer_refuse", TakeFlag = "feud_answer_take", SpurnFlag = "feud_answer_spurn", FightFlag = "feud_answer_fight", DeclineFlag = "feud_answer_decline";
    public const string AsksChar = "feud_asks_", BlamedChar = "blood_on_their_hands", RefusedFieldChar = "refused_the_field", WonDuelChar = "won_a_duel";
    public const string BegunKey = "feuds_begun", BloodKey = "feuds_ended_blood", WrongBloodKey = "feuds_wrong_blood", DuelKey = "feuds_ended_duel", PriceKey = "feuds_ended_price", LetLieKey = "feuds_let_lie", DuelsKey = "duels_fought", DuelDeathsKey = "duel_deaths", FieldRefusedKey = "duels_refused";
    private static readonly string[] YearFlags = { BegunFlag, AnsweredFlag, UnansweredFlag, PriceAskedFlag, PriceOfferedFlag, CalledOutFlag, DuelFoughtFlag, LetLieFlag };
    private static readonly string[] AnswerFlags = { PayFlag, RefuseFlag, TakeFlag, SpurnFlag, FightFlag, DeclineFlag };

    public const int PriceBase = 20, PricePerRank = 10, PriceForAHead = 30, KinRegard = -30, HeadRegard = -15, LeanBy = 3, TracedOneIn = 3, SuspectAt = -40;
    public const int AskLapses = 2, GraceYears = 2, ShameCap = 4, LetLieYears = 8, ActOneIn = 3, LetLieStanding = 3, LetLieKin = -10, LetLieThanks = 15, PriceAdmits = 1, FieldRefusedStanding = 3;
    public const int DuelDeathPct = 40, DuelWinnerWoundPct = 25, DuelKinRegard = -15, DuelFloor = 15, DuelCeiling = 85, DuelPerPoint = 6;

    private static uint Hash(string key) { uint h = 2166136261; foreach (var ch in key) h = (h ^ ch) * 16777619; return h; }
    private static void Mark(World w, string flag) { w.WorldFlags.Add(flag); w.WorldCounters["mark:" + flag] = w.Turn; }
    private static void Bump(World w, string key) => w.WorldCounters[key] = w.Counter(key) + 1;
    private static Character? Head(World w, string houseId) => w.House(houseId) is { } h ? Economy.HeadOf(w, h) : null;
    private static void Regard(World w, Character? from, Character? to, int d)
    { if (from == null || to == null || from.Id == to.Id) return; var r = w.Relationship(from.Id, to.Id); r.Disposition = Math.Clamp(r.Disposition + d, -100, 100); }
    private static bool Mine(World w, string houseId) => w.Protagonist?.HouseId == houseId;

    // ---- reading ----

    public static IEnumerable<BloodFeud> Open(World w) => w.Feuds.Where(f => f.Open);
    /// <summary>Is there blood between these two houses, either way?</summary>
    public static bool Between(World w, string a, string b) => a != b && w.Feuds.Any(f => f.Open && (f.WrongedId == a && f.OwingId == b || f.WrongedId == b && f.OwingId == a));
    /// <summary>How a house leans on a motion moved by a house it has blood with.</summary>
    public static int Lean(World w, string voterHouseId, string proposerHouseId) => Between(w, voterHouseId, proposerHouseId) ? -LeanBy : 0;
    /// <summary>The one a debt is held against now: whoever did it while they live, then the head of their house.</summary>
    public static Character? Blamed(World w, BloodFeud f) => w.Char(f.BlamedId) is { Alive: true } b && b.HouseId == f.OwingId ? b : Head(w, f.OwingId);
    public static string Describe(World w, BloodFeud f) =>
        $"{f.VictimName}, {(f.How == "battle" ? "killed in battle" : f.How == "knife" ? "killed by a hired hand" : "killed")} in year {f.Since}";

    private static bool CloseKin(Character a, Character b) =>
        a.Id == b.Id || a.FatherId == b.Id || a.MotherId == b.Id || b.FatherId == a.Id || b.MotherId == a.Id
        || a.FatherId.Length > 0 && a.FatherId == b.FatherId || a.MotherId.Length > 0 && a.MotherId == b.MotherId;

    /// <summary>How a person fights, one against one: what they know of fighting, their service, their health and their years.</summary>
    public static int Prowess(Character c) =>
        c.Skill("tactics") + c.Skill("gunnery") + (c.CareerTrack == "military" ? c.CareerRank : 0) + (c.Health >= 80 ? 1 : c.Health < LifeSystem.Sound ? -2 : 0) - Math.Max(0, c.Age - 45) / 5;
    /// <summary>The chance in a hundred that the challenger wins.</summary>
    public static int DuelOdds(Character challenger, Character other) => Math.Clamp(50 + (Prowess(challenger) - Prowess(other)) * DuelPerPoint, DuelFloor, DuelCeiling);

    // ---- a death ----

    /// <summary>Whether a death by a hired hand is laid at the right door. One time in three the trail is found; and a house that already has
    /// blood or a war with the killer's, or whose head already thinks very ill of them, needs no proof.</summary>
    public static bool Traced(World w, Character victim, Character killer)
    {
        if (Between(w, victim.HouseId, killer.HouseId) || w.AtWar(victim.HouseId, killer.HouseId)) return true;
        if (Head(w, victim.HouseId) is { } head && (w.GetRelationship(head.Id, killer.Id)?.Disposition ?? 0) <= SuspectAt) return true;
        return Hash($"trace:{victim.Id}:{killer.Id}:{w.Turn}") / 7 % TracedOneIn == 0;
    }

    /// <summary>A commander dead in battle is war, and forgotten at the peace, unless they were the head of their house or the head's near
    /// blood: that is remembered.</summary>
    public static bool BattleDeathBinds(World w, Character victim) => Head(w, victim.HouseId) is { } head && CloseKin(victim, head);

    /// <summary>Someone is dead and a house is known to have done it. Call before the death is carried out. If the dead person's house itself
    /// owed blood to the killers, this answers that debt: fully if it was the one to blame who died, and otherwise with the wrong blood,
    /// which the dead person's house now holds against the killers in its turn. If no blood was owed, it is owed now.</summary>
    public static BloodFeud? Blood(World w, Character victim, string killerHouseId, string? killerId, string how)
    {
        if (victim.HouseId == killerHouseId || w.House(victim.HouseId) == null || w.House(killerHouseId) == null) return null;
        if (World.Outsider(victim.HouseId) || World.Outsider(killerHouseId)) return null;
        var answered = w.Feuds.Where(f => f.Open && f.WrongedId == killerHouseId && f.OwingId == victim.HouseId).OrderBy(f => f.Since).ThenBy(f => f.Id, StringComparer.Ordinal).ToList();
        var due = answered.FirstOrDefault(f => Blamed(w, f)?.Id == victim.Id) ?? answered.FirstOrDefault();
        if (due != null)
        {
            bool right = Blamed(w, due)?.Id == victim.Id;
            End(w, due, "blood"); Bump(w, BloodKey);
            w.Log($"{w.House(killerHouseId)!.Name} has its blood for {due.VictimName}: {victim.Name} of {w.House(victim.HouseId)!.Name} is dead{(right ? ", and it was the one they held to blame. The debt is paid." : ". It was not the one they held to blame.")}");
            if (Mine(w, killerHouseId)) Mark(w, AnsweredFlag);
            if (right) return null;
            Bump(w, WrongBloodKey);
        }
        var wronged = w.House(victim.HouseId)!; var owing = w.House(killerHouseId)!; var head = Economy.HeadOf(w, wronged); var blamed = (killerId != null ? w.Char(killerId) : null) ?? Economy.HeadOf(w, owing);
        bool wasHead = head?.Id == victim.Id;
        var feud = new BloodFeud
        {
            Id = $"feud_{w.Turn}_{victim.Id}", WrongedId = wronged.Id, OwingId = owing.Id, BlamedId = blamed?.Id ?? "", VictimId = victim.Id, VictimName = victim.Name, How = how, Since = w.Turn,
            Price = PriceBase + victim.CareerRank * PricePerRank + (wasHead ? PriceForAHead : 0),
        };
        if (w.Feuds.Any(f => f.Id == feud.Id)) return null;
        w.Feuds.Add(feud); Bump(w, BegunKey);
        blamed?.Flags.Add(BlamedChar);
        foreach (var kin in w.LivingCharacters().Where(c => c.HouseId == wronged.Id && c.Id != victim.Id).ToList())
        { Regard(w, kin, blamed, KinRegard); if (Economy.HeadOf(w, owing) is { } theirs && theirs.Id != blamed?.Id) Regard(w, kin, theirs, HeadRegard); }
        w.Log($"There is blood between {wronged.Name} and {owing.Name}: {victim.Name} is dead{(blamed != null ? $", and {wronged.Name} holds {blamed.Name} to blame" : "")}.");
        if (Mine(w, wronged.Id)) { Mark(w, BegunFlag); w.WorldFlags.Add(OwedToUsFlag); }
        if (Mine(w, owing.Id)) { Mark(w, BegunFlag); w.WorldFlags.Add(WeOweFlag); }
        return feud;
    }

    private static void End(World w, BloodFeud f, string how)
    {
        f.Ended = how; f.EndedTurn = w.Turn; if (w.Char(f.AskerId) is { } asker) asker.Flags.Remove(AsksChar + f.Asked); f.Asked = ""; f.AskerId = "";
        if (w.Char(f.BlamedId) is { } b && !w.Feuds.Any(o => o.Open && o.BlamedId == b.Id)) b.Flags.Remove(BlamedChar);
        Standing(w);
    }

    /// <summary>Keep the two standing flags true to what the player's house is owed and owes.</summary>
    private static void Standing(World w)
    {
        string? mine = w.Protagonist?.HouseId; if (mine == null) return;
        if (w.Feuds.Any(f => f.Open && f.WrongedId == mine)) w.WorldFlags.Add(OwedToUsFlag); else w.WorldFlags.Remove(OwedToUsFlag);
        if (w.Feuds.Any(f => f.Open && f.OwingId == mine)) w.WorldFlags.Add(WeOweFlag); else w.WorldFlags.Remove(WeOweFlag);
    }

    // ---- ending it ----

    /// <summary>Would this person take the field if called out? Those bred to it, and those too proud to be seen refusing.</summary>
    public static bool TakesTheField(Character c) =>
        c.Health >= LifeSystem.Frail && (c.CareerTrack == "military" || c.NatureTraits.Contains("Honorable") || c.NatureTraits.Contains("Proud") || c.NatureTraits.Contains("Arrogant") || c.NatureTraits.Contains("Cruel"));

    /// <summary>Would this head take money for their dead? Not the vengeful, the cruel or the proud; and nobody whose house was refused once.</summary>
    public static bool TakesAPrice(World w, BloodFeud f) =>
        Head(w, f.WrongedId) is { } head && !head.NatureTraits.Contains("Vengeful") && !head.NatureTraits.Contains("Cruel") && !head.NatureTraits.Contains("Proud") && !f.Refused;

    /// <summary>Would the owing house pay if asked? If it has the money half as much again, or the house asking is no weaker, or its head
    /// is a just or honourable one. An arrogant head never pays.</summary>
    public static bool WouldPay(World w, BloodFeud f) =>
        w.House(f.OwingId) is { } owing && owing.Treasury.Credits >= f.Price && Head(w, f.OwingId) is { } head && !head.NatureTraits.Contains("Arrogant")
        && (owing.Treasury.Credits >= f.Price * 3 / 2 || Hulls(w, f.WrongedId) >= Hulls(w, f.OwingId) || head.NatureTraits.Contains("Just") || head.NatureTraits.Contains("Honorable"));
    private static int Hulls(World w, string houseId) => w.Fleets.Values.Where(fl => fl.OwnerId == houseId).Sum(fl => fl.Strength);

    /// <summary>The price is paid and taken: the money moves, the owing house has admitted it, and the dead person's near kin think the less
    /// of a head who took coin for them.</summary>
    public static void PayPrice(World w, BloodFeud f)
    {
        var owing = w.House(f.OwingId)!; var wronged = w.House(f.WrongedId)!; int price = Math.Min(f.Price, Math.Max(0, owing.Treasury.Credits));
        owing.Treasury.Credits -= price; wronged.Treasury.Credits += price; owing.Legitimacy = Math.Max(0, owing.Legitimacy - PriceAdmits);
        var head = Economy.HeadOf(w, wronged); var victim = w.Char(f.VictimId);
        if (head != null && victim != null) foreach (var kin in w.LivingCharacters().Where(c => c.HouseId == wronged.Id && c.Id != head.Id && CloseKin(c, victim)).ToList()) Regard(w, kin, head, LetLieKin / 2);
        w.Log($"{owing.Name} pays {wronged.Name} {Money.Credits(price)} for {f.VictimName}. The blood between them is bought off.");
        End(w, f, "price"); Bump(w, PriceKey);
    }

    /// <summary>The wronged house lets it lie: it costs standing, the dead person's kin do not thank the head for it, and the other house does.</summary>
    public static void LetLie(World w, BloodFeud f, bool chosen)
    {
        var wronged = w.House(f.WrongedId)!; var head = Economy.HeadOf(w, wronged); var victim = w.Char(f.VictimId);
        if (chosen)
        {
            int cost = Math.Max(0, LetLieStanding - f.Shame); wronged.Legitimacy = Math.Max(0, wronged.Legitimacy - cost);
            if (head != null) foreach (var kin in w.LivingCharacters().Where(c => c.HouseId == wronged.Id && c.Id != head.Id && (victim == null || CloseKin(c, victim))).ToList()) Regard(w, kin, head, LetLieKin);
            Regard(w, Head(w, f.OwingId), head, LetLieThanks);
            w.Log($"{wronged.Name} lets the death of {f.VictimName} lie, and says so. {w.House(f.OwingId)?.Name} had not expected it.");
        }
        else w.Log($"Nobody speaks of {f.VictimName} now. The blood between {wronged.Name} and {w.House(f.OwingId)?.Name} has been let lie.");
        if (Mine(w, f.WrongedId)) Mark(w, LetLieFlag);
        End(w, f, "let_lie"); Bump(w, LetLieKey);
    }

    /// <summary>The one called out will not come: it costs their house standing, they are known for it, and what the wronged house does next
    /// is on them.</summary>
    public static void RefuseField(World w, BloodFeud f, Character blamed, Character challenger)
    {
        f.Refused = true; f.FieldRefused = true; blamed.Flags.Add(RefusedFieldChar); Bump(w, FieldRefusedKey);
        if (w.House(f.OwingId) is { } owing) owing.Legitimacy = Math.Max(0, owing.Legitimacy - FieldRefusedStanding);
        LifeSystem.Note(w, blamed, "failure", $"Was called out by {challenger.Name} for the death of {f.VictimName}, and did not come.");
        w.Log($"{challenger.Name} of {w.House(f.WrongedId)?.Name} calls {blamed.Name} out for the death of {f.VictimName}. {blamed.Name} does not come.");
    }

    /// <summary>The two meet. <paramref name="roll"/> and <paramref name="fate"/> are 0 to 99. The loser dies four times in ten and is otherwise
    /// wounded; the winner is sometimes wounded too. However it falls the debt is ended: it was answered in the open, and a death in it is
    /// nobody's to avenge.</summary>
    public static Character Duel(World w, BloodFeud f, Character challenger, Character blamed, int roll, int fate)
    {
        bool won = roll < DuelOdds(challenger, blamed); var winner = won ? challenger : blamed; var loser = won ? blamed : challenger;
        Bump(w, DuelsKey); winner.Flags.Add(WonDuelChar);
        LifeSystem.Note(w, winner, "victory", $"Met {loser.Name} over the death of {f.VictimName}, and won.");
        if (challenger.Id == w.ProtagonistId || blamed.Id == w.ProtagonistId || Mine(w, challenger.HouseId) || Mine(w, blamed.HouseId)) Mark(w, DuelFoughtFlag);
        End(w, f, "duel"); Bump(w, DuelKey);
        if (fate < DuelDeathPct)
        {
            w.Log($"{challenger.Name} of {w.House(challenger.HouseId)?.Name} and {blamed.Name} of {w.House(blamed.HouseId)?.Name} meet over the death of {f.VictimName}. {loser.Name} is killed. It was done in the open, and it ends there.");
            foreach (var kin in w.LivingCharacters().Where(c => c.HouseId == loser.HouseId && c.Id != loser.Id).ToList()) Regard(w, kin, winner, DuelKinRegard);
            Bump(w, DuelDeathsKey); DynastySystem.Die(w, loser.Id, "in a duel");
        }
        else
        {
            w.Log($"{challenger.Name} of {w.House(challenger.HouseId)?.Name} and {blamed.Name} of {w.House(blamed.HouseId)?.Name} meet over the death of {f.VictimName}. {loser.Name} is cut down and yields. It ends there.");
            LifeSystem.Wound(w, loser, $"in a duel with {winner.Name}");
        }
        if (winner.Alive && (fate * 7 + roll) % 100 < DuelWinnerWoundPct) LifeSystem.Wound(w, winner, $"in a duel with {loser.Name}");
        return winner;
    }

    /// <summary>The best of a house to send to the field: an adult, sound, who would go.</summary>
    public static Character? Champion(World w, string houseId) =>
        w.LivingCharacters().Where(c => c.HouseId == houseId && c.Age >= DynastySystem.AgeOfMajority && c.Age <= 60 && TakesTheField(c))
            .OrderByDescending(Prowess).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();

    // ---- the year ----

    /// <summary>Once a year: debts between houses that are gone are struck out; what the player answered is carried out and what they did not
    /// answer is taken as a refusal; each wronged house other than the player's does what its head would; a house that owes the player
    /// may offer the price; and a debt nobody answers costs the house that is owed, until at last it is let lie.</summary>
    public static void Tick(World w)
    {
        foreach (var flag in YearFlags) if (w.WorldFlags.Contains(flag) && w.Counter("mark:" + flag) < w.Turn - 1) { w.WorldFlags.Remove(flag); w.WorldCounters.Remove("mark:" + flag); }
        string? mine = w.Protagonist?.HouseId;
        foreach (var f in Open(w).OrderBy(f => f.Id, StringComparer.Ordinal).ToList())
            if (!w.LivingCharacters().Any(c => c.HouseId == f.WrongedId) || !w.LivingCharacters().Any(c => c.HouseId == f.OwingId)) End(w, f, "extinct");
        Answers(w);
        foreach (var f in Open(w).OrderBy(f => f.Id, StringComparer.Ordinal).ToList())
        {
            if (!f.Open) continue;
            if (f.WrongedId == mine) { Unanswered(w, f); if (f.Open && f.OwingId != mine) TheyOffer(w, f); continue; }
            f.Unanswered++;
            if (f.Asked.Length > 0) continue;   // waiting on the player
            if (Hash($"{f.Id}:{w.Turn}:act") / 7 % ActOneIn == 0) Act(w, f);
            if (f.Open && f.Unanswered >= LetLieYears) LetLie(w, f, chosen: false);
        }
        Standing(w);
    }

    /// <summary>The player's own dead: two years' grace, then a point of standing a year (four at most) and the near kin's regard, and after
    /// eight years it is let lie whether the head says so or not.</summary>
    private static void Unanswered(World w, BloodFeud f)
    {
        f.Unanswered++; var house = w.House(f.WrongedId)!; var head = Economy.HeadOf(w, house); var victim = w.Char(f.VictimId);
        if (f.Unanswered > GraceYears)
        {
            if (f.Shame < ShameCap) { f.Shame++; house.Legitimacy = Math.Max(0, house.Legitimacy - 1); }
            if (head != null && victim != null) foreach (var kin in w.LivingCharacters().Where(c => c.HouseId == house.Id && c.Id != head.Id && CloseKin(c, victim)).ToList()) Regard(w, kin, head, -3);
            Mark(w, UnansweredFlag);
        }
        if (f.Unanswered >= LetLieYears) LetLie(w, f, chosen: false);
    }

    /// <summary>A house that owes the player blood offers the price once, some year, if its head is a just or honourable one or the player's
    /// house is the stronger.</summary>
    private static void TheyOffer(World w, BloodFeud f)
    {
        if (f.Offered || f.Asked.Length > 0 || w.HasFlag(PriceOfferedFlag) || Hash($"{f.Id}:{w.Turn}:offer") / 7 % ActOneIn != 0) return;
        var head = Head(w, f.OwingId); var owing = w.House(f.OwingId); if (head == null || owing == null || owing.Treasury.Credits < f.Price) return;
        if (!head.NatureTraits.Contains("Just") && !head.NatureTraits.Contains("Honorable") && Hulls(w, f.WrongedId) <= Hulls(w, f.OwingId)) return;
        f.Offered = true; Ask(w, f, "offer", head, PriceOfferedFlag);
        w.Log($"{head.Name} of {owing.Name} offers {Money.Credits(f.Price)} for the death of {f.VictimName}, to have an end of it.");
    }

    private static void Ask(World w, BloodFeud f, string what, Character asker, string flag)
    { f.Asked = what; f.AskerId = asker.Id; f.AskedTurn = w.Turn; asker.Flags.Add(AsksChar + what); Mark(w, flag); }

    /// <summary>What a wronged house does, by its head's nature. The honourable and the proud send someone to the field if they have someone
    /// to send. The cruel, the ruthless and the vengeful, and anyone who was refused, send a knife. The rest ask the price.</summary>
    private static void Act(World w, BloodFeud f)
    {
        var head = Head(w, f.WrongedId); var blamed = Blamed(w, f); if (head == null || blamed == null) return;
        bool player = blamed.Id == w.ProtagonistId, toPlayer = Mine(w, f.OwingId);
        bool hard = head.NatureTraits.Contains("Cruel") || head.NatureTraits.Contains("Ruthless") || head.NatureTraits.Contains("Vengeful");
        bool open = head.NatureTraits.Contains("Honorable") || head.NatureTraits.Contains("Proud");
        var champion = Champion(w, f.WrongedId);
        if (champion != null && !f.FieldRefused && (open || f.Refused && !hard))
        {
            if (player) { if (!w.HasFlag(CalledOutFlag)) { Ask(w, f, "duel", champion, CalledOutFlag); w.Log($"{champion.Name} of {w.House(f.WrongedId)?.Name} calls {blamed.Name} out for the death of {f.VictimName}."); } return; }
            if (!TakesTheField(blamed)) { RefuseField(w, f, blamed, champion); return; }
            Duel(w, f, champion, blamed, (int)(Hash($"{f.Id}:{w.Turn}:duel") / 7 % 100), (int)(Hash($"fate:{w.Turn}:{f.Id}") / 7 % 100));
            return;
        }
        if (hard || f.Refused)
        {
            // never the player's own person: a knife for the one to blame, or for one of the player's adult kin
            var target = !player ? blamed : w.LivingCharacters().Where(c => c.HouseId == f.OwingId && c.Id != blamed.Id && c.Age >= DynastySystem.AgeOfMajority && !c.Flags.Contains(DynastySystem.HiddenFlag))
                .OrderByDescending(c => c.CareerRank).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();
            if (target == null || target.Flags.Contains(DynastySystem.HiddenFlag) || target.Age < DynastySystem.AgeOfMajority) return;
            int odds = (int)(AssassinateCommand.Odds(w, head, target) * 100);
            if ((int)(Hash($"{f.Id}:{w.Turn}:knife") / 7 % 100) < odds)
            {
                w.Log($"{target.Name} of {w.House(target.HouseId)?.Name} dies suddenly. Nobody doubts whose hand it was.");
                Blood(w, target, f.WrongedId, head.Id, "knife"); DynastySystem.Die(w, target.Id, "by a knife, for the blood between the houses");
            }
            else
            {
                w.Log($"An attempt on {target.Name} fails. It was {w.House(f.WrongedId)?.Name}'s, for {f.VictimName}.");
                if (target.HouseId == w.Protagonist?.HouseId) w.Tell("warn", $"An attempt was made on the life of {target.Name}. It failed. It was {w.House(f.WrongedId)?.Name}'s, for {f.VictimName}: there is blood between the houses", "houses", f.WrongedId);
                if (toPlayer) { w.WorldFlags.Add("knife_turned"); CourtSystem.Held(w, target); }
            }
            return;
        }
        if (toPlayer) { if (!w.HasFlag(PriceAskedFlag)) { Ask(w, f, "price", head, PriceAskedFlag); w.Log($"{head.Name} of {w.House(f.WrongedId)?.Name} asks {Money.Credits(f.Price)} for the death of {f.VictimName}."); } return; }
        if (WouldPay(w, f)) PayPrice(w, f); else { f.Refused = true; w.Log($"{w.House(f.WrongedId)?.Name} asks a price for {f.VictimName}, and {w.House(f.OwingId)?.Name} will not pay it."); }
    }

    /// <summary>What the player answered to what was asked of them, through an event; and what was asked last year and never answered is a no.</summary>
    private static void Answers(World w)
    {
        var said = AnswerFlags.Where(w.WorldFlags.Contains).ToList(); foreach (var flag in said) w.WorldFlags.Remove(flag);
        foreach (var f in Open(w).Where(f => f.Asked.Length > 0).OrderBy(f => f.AskedTurn).ThenBy(f => f.Id, StringComparer.Ordinal).ToList())
        {
            string yes = f.Asked == "price" ? PayFlag : f.Asked == "offer" ? TakeFlag : FightFlag, no = f.Asked == "price" ? RefuseFlag : f.Asked == "offer" ? SpurnFlag : DeclineFlag;
            if (said.Remove(yes)) Answer(w, f, true, null);
            else if (said.Remove(no) || w.Turn - f.AskedTurn >= AskLapses) Answer(w, f, false, null);
        }
    }

    /// <summary>Carry out the player's answer to what a house asked of them. A duel accepted by command is fought on the command's dice.</summary>
    public static void Answer(World w, BloodFeud f, bool yes, Rng.IRng? rng)
    {
        string asked = f.Asked; var asker = w.Char(f.AskerId); f.Asked = ""; asker?.Flags.Remove(AsksChar + asked); f.AskerId = "";
        var me = w.Protagonist; if (me == null || !f.Open) return;
        if (asked == "price")
        {
            if (yes && w.House(f.OwingId) is { } mine && mine.Treasury.Credits >= f.Price) PayPrice(w, f);
            else { f.Refused = true; Regard(w, Head(w, f.WrongedId), me, -10); w.Log($"{w.House(f.OwingId)?.Name} will not pay for {f.VictimName}."); }
        }
        else if (asked == "offer")
        {
            if (yes) PayPrice(w, f); else w.Log($"{w.House(f.WrongedId)?.Name} will not take money for {f.VictimName}.");
        }
        else if (asked == "duel" && asker is { Alive: true })
        {
            if (!yes) { RefuseField(w, f, me, asker); return; }
            int roll = rng != null ? rng.NextInt(0, 100) : (int)(Hash($"{f.Id}:{w.Turn}:duel") / 7 % 100), fate = rng != null ? rng.NextInt(0, 100) : (int)(Hash($"fate:{w.Turn}:{f.Id}") / 7 % 100);
            Duel(w, f, asker, me, roll, fate);
        }
    }

    // ---- what the player may do about a debt ----

    public static string? ActRefusal(World w, Character? me, BloodFeud? f, string what, Character? champion = null)
    {
        if (me?.Alive != true || f is not { Open: true }) return "there is no such debt";
        bool owed = f.WrongedId == me.HouseId, owe = f.OwingId == me.HouseId; if (!owed && !owe) return "it is not your house's affair";
        if (me.Id != w.ProtagonistId) return "the head of the house answers for its dead";
        var blamed = Blamed(w, f);
        switch (what)
        {
            case FeudCommand.Pay: case FeudCommand.Refuse: return owe && f.Asked == "price" ? what == FeudCommand.Pay && w.House(me.HouseId)!.Treasury.Credits < f.Price ? $"the house has not the {Money.Credits(f.Price)}" : null : "nobody has asked you for a price";
            case FeudCommand.Take: case FeudCommand.Spurn: return owed && f.Asked == "offer" ? null : "nobody has offered you a price";
            case FeudCommand.Fight: case FeudCommand.Decline: return owe && f.Asked == "duel" ? what == FeudCommand.Fight && me.Health < LifeSystem.Frail ? "you are in no state to take the field" : null : "nobody has called you out";
        }
        if (f.ActedTurn == w.Turn) return "one thing a year is done about a death";
        if (f.Asked.Length > 0) return "they are waiting on your answer";
        switch (what)
        {
            case FeudCommand.Challenge:
                if (!owed) return "it is for the house that is owed to call someone out";
                if (blamed == null) return "there is nobody left to call out";
                if (f.FieldRefused) return "they would not come the first time, and will not now";
                var who = champion ?? me; if (who.HouseId != me.HouseId || !who.Alive) return "not one of your house";
                if (who.Age < DynastySystem.AgeOfMajority) return "they are a child";
                if (who.Health < LifeSystem.Frail) return who.Id == me.Id ? "you are in no state to take the field" : $"{who.Name} is in no state to take the field";
                if (who.Id != me.Id && !TakesTheField(who)) return $"{who.Name} will not fight for it";
                return blamed.Age < DynastySystem.AgeOfMajority ? "the one to blame is a child" : null;
            case FeudCommand.Demand:
                return !owed ? "it is for the house that is owed to ask a price" : f.Refused ? "they have refused once already" : null;
            case FeudCommand.Offer:
                return !owe ? "it is for the house that owes to offer a price" : f.Offered ? "you have offered once already" : w.House(me.HouseId)!.Treasury.Credits < f.Price ? $"the house has not the {Money.Credits(f.Price)}" : null;
            case FeudCommand.LetLie:
                return owed ? null : "it is not yours to forgive";
        }
        return "no such thing";
    }

    public static void Act(World w, Character me, BloodFeud f, string what, Character? champion, Rng.IRng rng)
    {
        switch (what)
        {
            case FeudCommand.Pay: Answer(w, f, true, rng); return;
            case FeudCommand.Take: Answer(w, f, true, rng); return;
            case FeudCommand.Fight: Answer(w, f, true, rng); return;
            case FeudCommand.Refuse: case FeudCommand.Spurn: case FeudCommand.Decline: Answer(w, f, false, rng); return;
        }
        f.ActedTurn = w.Turn; var blamed = Blamed(w, f);
        switch (what)
        {
            case FeudCommand.Challenge:
                var who = champion ?? me;
                if (!TakesTheField(blamed!)) { RefuseField(w, f, blamed!, who); return; }
                Duel(w, f, who, blamed!, rng.NextInt(0, 100), rng.NextInt(0, 100)); return;
            case FeudCommand.Demand:
                if (WouldPay(w, f)) PayPrice(w, f);
                else { f.Refused = true; Regard(w, me, Head(w, f.OwingId), -10); w.Log($"{w.House(f.WrongedId)?.Name} asks {Money.Credits(f.Price)} for {f.VictimName}, and {w.House(f.OwingId)?.Name} will not pay it."); }
                return;
            case FeudCommand.Offer:
                f.Offered = true;
                if (TakesAPrice(w, f)) PayPrice(w, f);
                else w.Log($"{w.House(f.OwingId)?.Name} offers {Money.Credits(f.Price)} for {f.VictimName}. {w.House(f.WrongedId)?.Name} will not take money for their dead.");
                return;
            case FeudCommand.LetLie: LetLie(w, f, chosen: true); return;
        }
    }
}
