using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// A life of one's own (docs/LIFE.md). HEALTH: everyone has it; age lowers what it can be, wounds, illness and strain lower it,
/// rest restores it, and it bears on how long they live. A RECORD: what a person of the house has done is written down as they do
/// it, and their renown and the name they are known by come from that record, not from the house's. TIES THAT LAST: someone who
/// has thought well of you for eight years is an old friend, someone who has thought ill of you that long an old enemy, and their
/// deaths are felt. RETIREMENT: a head may lay the house down in life and stay on as its elder. And a FUNERAL, at which what is
/// said is made from the record.
/// The record, ties and funerals are kept for the player's house. RNG-free; death itself is still the dynasty's dice.
/// </summary>
public static class LifeSystem
{
    public const int Adult = DynastySystem.AgeOfMajority, OldTieYears = 8, FriendAt = 45, EnemyAt = -40, Sound = 55, Frail = 35, RestInfluence = 2, PhysicianCredits = 6, RetireAge = 50, KinRetireAge = 70, MaxDeeds = 40;
    public const string IllChar = "ill", FrailChar = "frail", RetiredChar = "retired", OldFriendChar = "old_friend", OldEnemyChar = "old_enemy", WoundedChar = "wounded";
    // each year
    public const string AilingFlag = "head_ailing", FrailFlag = "head_frail", IllFlag = "head_ill", OldFriendFlag = "has_old_friend", OldEnemyFlag = "has_old_enemy", ElderFlag = "elder_in_the_house";
    // deeds
    public const string WoundedNowFlag = "wounded_now", FriendDiedFlag = "old_friend_died", EnemyDiedFlag = "old_enemy_died", FuneralFlag = "funeral_held", RetiredNowFlag = "head_retired";
    private static readonly string[] TickFlags = { AilingFlag, FrailFlag, IllFlag, OldFriendFlag, OldEnemyFlag, ElderFlag };
    private static readonly string[] DeedFlags = { WoundedNowFlag, FriendDiedFlag, EnemyDiedFlag, FuneralFlag, RetiredNowFlag };

    private static void Mark(World w, string flag) { w.WorldFlags.Add(flag); w.WorldCounters["mark:" + flag] = w.Turn; }
    private static uint Hash(string s) { uint h = 2166136261; foreach (char ch in s) h = (h ^ ch) * 16777619; return h; }
    /// <summary>Is this one of the people whose lives are written down: the player's house, the player's own character included?</summary>
    public static bool Followed(World w, Character c) => w.Protagonist is { } p && c.HouseId == p.HouseId;

    // ---------------- health ----------------

    /// <summary>As well as someone of this age can be.</summary>
    public static int Cap(int age) => age <= 40 ? 100 : Math.Max(30, 100 - (age - 40) * 3 / 2);
    public static string HealthName(int health) => health >= 80 ? "hale" : health >= Sound ? "sound" : health >= Frail ? "ailing" : "frail";
    /// <summary>How health bears on the year's chance of death for someone old enough to be at risk.</summary>
    public static double DeathFactor(Character c) => c.Health >= 80 ? 0.75 : c.Health >= Sound ? 1.0 : c.Health >= Frail ? 1.5 : 2.4;
    /// <summary>The chance that someone not yet old dies of what ails them.</summary>
    public static double EarlyDeath(Character c) => c.Health < 25 ? 0.12 : 0.0;

    /// <summary>A wound: in battle, in a duel, by a knife that missed its mark.</summary>
    public static void Wound(World w, Character c, string how)
    {
        c.Health = Math.Max(5, c.Health - 20); c.Flags.Add(WoundedChar);
        if (!Followed(w, c)) return;
        c.Life ??= new Life(); c.Life.Wounds++;
        Note(w, c, "wound", $"Wounded {how}.");
        // it shows
        string key = "look:" + c.Id; string look = w.Cosmetics.TryGetValue(key, out var have) ? have : "";
        if (!look.Contains("scar=1")) w.Cosmetics[key] = look.Contains("scar=") ? look.Replace("scar=0", "scar=1").Replace("scar=-1", "scar=1") : look.Length > 0 ? look + ",scar=1" : "scar=1";
        if (c.Id == w.ProtagonistId) Mark(w, WoundedNowFlag);
    }

    public static string? RestRefusal(World w, Character? who)
    {
        if (who?.Alive != true) return "no such person";
        if ((who.Life?.RestedTurn ?? -1) >= w.Turn) return "once a year";
        return who.Health >= Cap(who.Age) && who.Stress == 0 ? "you are as well as you can be" : null;
    }
    /// <summary>A season away from it all.</summary>
    public static void Rest(World w, Character who)
    {
        who.Life ??= new Life(); who.Life.RestedTurn = w.Turn;
        who.Health = Math.Min(Cap(who.Age), who.Health + 8); who.Stress = Math.Max(0, who.Stress - 10);
        w.Log($"{who.Name} takes a season away from it all.");
    }
    public static string? PhysicianRefusal(World w, Character? who) =>
        who?.Alive != true ? "no such person" : who.Purse < PhysicianCredits ? "your purse does not hold it" : who.Health >= Cap(who.Age) && !who.Flags.Contains(IllChar) ? "there is nothing for them to treat" : null;
    public static void Physicians(World w, Character who)
    {
        who.Purse -= PhysicianCredits; who.Health = Math.Min(Cap(who.Age), who.Health + 6);
        if (who.Flags.Remove(IllChar) && Followed(w, who)) w.Log($"{who.Name} is well again, under the physicians.");
    }

    // ---------------- the record ----------------

    private static int Weight(string kind) => kind switch
    {
        "head" => 10, "victory" => 8, "office" => 8, "fulfilled" => 6, "mastery" => 5, "adopted" => 5, "love" => 5, "wound" => 4, "venture" => 4, "retired" => 4,
        "rank" => 3, "wed" => 3, "took_in" => 3, "child" => 2, "friend" => 2, _ => 1
    };

    /// <summary>Write something into the record of a life, if it is one of the lives that are kept.</summary>
    public static void Note(World w, Character? c, string kind, string text)
    {
        if (c == null || !Followed(w, c)) return;
        c.Life ??= new Life(); c.Life.Deeds.Add(new Deed { Turn = w.Turn, Age = c.Age, Kind = kind, Text = text });
        if (c.Life.Deeds.Count > MaxDeeds) c.Life.Deeds.Remove(c.Life.Deeds.OrderBy(d => Weight(d.Kind)).ThenBy(d => d.Turn).First());
    }
    private static bool Once(Character c, string mark) { c.Life ??= new Life(); return c.Life.Marks.Add(mark); }

    /// <summary>What a person is, apart from their house: how far they rose and what they did. 0 to 100.</summary>
    public static int Renown(Character c) => Math.Clamp(c.CareerRank * 5 + (c.Life?.Deeds.Sum(d => Weight(d.Kind)) ?? 0), 0, 100);
    public static string RenownName(int renown) => renown >= 70 ? "known across the Reach" : renown >= 45 ? "a name people know" : renown >= 25 ? "known in their own circle" : renown >= 10 ? "little known" : "unknown";

    /// <summary>The name they are known by, if their life has earned one.</summary>
    public static string Epithet(Character c)
    {
        var l = c.Life; if (l == null) return "";
        int victories = l.Deeds.Count(d => d.Kind == "victory"), ventures = l.Deeds.Count(d => d.Kind == "venture");
        if (victories >= 3) return "the Victorious";
        if (l.Wounds >= 2) return "the Scarred";
        if (c.NatureTraits.Contains("Just") && l.Deeds.Any(d => d.Kind == "office")) return "the Just";
        if (ventures >= 2) return "the Builder";
        if (l.Deeds.Count(d => d.Kind is "took_in" or "adopted") >= 2) return "the Open-Handed";
        if (Renown(c) >= 70) return "the Great";
        if (c.NatureTraits.Contains("Cruel") && Renown(c) >= 30) return "the Hard";
        if (c.Age >= 78) return "the Old";
        return "";
    }

    /// <summary>What is said over them, made from what they did.</summary>
    public static Funeral Eulogy(World w, Character c)
    {
        var house = w.House(c.HouseId); string epithet = Epithet(c); var lines = new List<string>(); var l = c.Life;
        int headed = l?.Deeds.Where(d => d.Kind == "head").Select(d => w.Turn - d.Turn).DefaultIfEmpty(-1).Max() ?? -1;
        lines.Add($"{c.Name}{(epithet.Length > 0 ? $", called {epithet}," : "")} is dead at {c.Age}{(headed > 0 ? $", head of {house?.Name} for {headed} year{(headed == 1 ? "" : "s")}" : "")}.");
        if (l != null)
            foreach (var d in l.Deeds.Where(d => d.Kind != "head").OrderByDescending(d => Weight(d.Kind)).ThenBy(d => d.Turn).Take(3).OrderBy(d => d.Turn))
                lines.Add($"At {d.Age}: {char.ToLowerInvariant(d.Text[0]) + d.Text[1..]}");
        var spouse = DynastySystem.Spouse(w, c); var m = w.MarriageOf(c.Id);
        int children = w.Characters.Values.Count(k => k.Alive && (k.MotherId == c.Id || k.FatherId == c.Id || k.AdoptedById == c.Id));
        if (spouse != null && m != null) lines.Add(m.Warmth >= CourtshipSystem.WarmAt ? $"{spouse.Name} does not leave the grave until dark." : m.Estranged ? $"{spouse.Name} attends, and stands apart." : $"{spouse.Name} stands with the family.");
        if (children > 0) lines.Add(children == 1 ? "One child follows the bier." : $"{children} children follow the bier.");
        int renown = Renown(c);
        lines.Add(c.NatureTraits.Contains("Honorable") ? "Nobody present can remember a promise of theirs broken."
            : c.NatureTraits.Contains("Cruel") ? "Few weep, and those few carefully."
            : c.NatureTraits.Contains("Loyal") ? "They never once left the house's side, and the house knows it."
            : c.NatureTraits.Contains("Just") ? "People they ruled against have come to stand in the rain."
            : renown >= 45 ? "The street outside is full." : renown >= 15 ? "The family is there, and some who served with them." : "It is a small funeral.");
        return new Funeral { Turn = w.Turn, PersonId = c.Id, Name = c.Name, Age = c.Age, Epithet = epithet, Lines = lines };
    }

    /// <summary>Someone has died. If they were of the house they are buried and spoken over; if they were an old friend or an old
    /// enemy of the head, that is felt. Called before the seat passes, while the house still knows whose it is.</summary>
    public static void Died(World w, Character c)
    {
        var p = w.Protagonist; if (p == null) return;
        if (c.HouseId == p.HouseId)
        {
            var f = Eulogy(w, c); w.Funerals.Add(f); while (w.Funerals.Count > 12) w.Funerals.RemoveAt(0);
            Mark(w, FuneralFlag); w.Log($"At the funeral of {c.Name}: {string.Join(" ", f.Lines.Skip(1).Take(2))}");
        }
        if (c.Id == p.Id || p.Life == null) return;
        if (p.Life.Friends.TryGetValue(c.Id, out int since) && w.Turn - since >= OldTieYears)
        { Mark(w, FriendDiedFlag); p.Stress += 8; Note(w, p, "friend", $"Lost {c.Name}, a friend of {w.Turn - since} years."); }
        if (p.Life.Enemies.TryGetValue(c.Id, out int hated) && w.Turn - hated >= OldTieYears) { Mark(w, EnemyDiedFlag); Note(w, p, "friend", $"Outlived {c.Name}, an enemy of {w.Turn - hated} years."); }
        p.Life.Friends.Remove(c.Id); p.Life.Enemies.Remove(c.Id);
    }

    // ---------------- retirement ----------------

    public static string? RetireRefusal(World w, Character? head)
    {
        if (head?.Alive != true || head.Id != w.ProtagonistId) return "not yours to lay down";
        if (head.Age < RetireAge && head.Health >= Frail) return $"not before {RetireAge}, unless your health fails";
        var heir = DynastySystem.FindHeir(w, head);
        return heir == null ? "there is nobody to hand it to" : heir.Age < Adult ? $"{heir.Name} is not of age" : null;
    }

    /// <summary>Lay the house down in life. The heir has it; the old head stays on as its elder, and teaches the new one.</summary>
    public static void Retire(World w, Character head)
    {
        var heir = DynastySystem.FindHeir(w, head)!; var house = w.House(head.HouseId);
        Note(w, head, "retired", $"Laid down the headship of {house?.Name} in life, to {heir.Name}.");
        DynastySystem.HandOver(w, heir);
        head.Flags.Add(RetiredChar); heir.MentorId = head.Id; heir.JoinedTurn = w.Turn; if (house != null) { house.Legitimacy += 2; if (house.NamedHeirId == heir.Id) house.NamedHeirId = ""; }
        foreach (var (a, b) in new[] { (heir.Id, head.Id), (head.Id, heir.Id) }) { var r = w.Relationship(a, b); r.Disposition = Math.Clamp(r.Disposition + 10, -100, 100); }
        Mark(w, RetiredNowFlag);
    }

    // ---------------- the year ----------------

    public static void Tick(World w, Data.ContentDatabase? content = null)
    {
        foreach (var f in TickFlags) w.WorldFlags.Remove(f);
        foreach (var f in DeedFlags) if (w.WorldFlags.Contains(f) && w.Counter("mark:" + f) < w.Turn - 1) { w.WorldFlags.Remove(f); w.WorldCounters.Remove("mark:" + f); }
        var p = w.Protagonist; var house = p != null ? w.House(p.HouseId) : null;
        var officers = w.Offices.Select(o => o.CharacterId).ToHashSet();

        foreach (var c in w.LivingCharacters().Where(c => c.Age >= Adult).OrderBy(c => c.Id, StringComparer.Ordinal).ToList())
        {
            bool ours = house != null && c.HouseId == house.Id; int cap = Cap(c.Age); bool wasIll = c.Flags.Contains(IllChar);
            if (c.Health > cap) c.Health = cap;
            if (c.Stress >= 70) c.Health -= 3;
            // illness comes more often with age and to those already low
            int chance = Math.Max(1, (c.Age - 30) / 4) + (c.Health < 50 ? 6 : 0);
            if (!wasIll && Hash(c.Id + ":ill:" + w.Turn) % 100 < chance)
            {
                c.Flags.Add(IllChar); c.Health -= 12; if (ours) w.Log($"{c.Name} is ill.");
            }
            else if (wasIll)
            {
                // one of the house with money of their own sends for the physicians without being told
                if (ours && c.Id != p!.Id && c.Purse >= PhysicianCredits) Physicians(w, c);
                else if (c.Health >= 45 || Hash(c.Id + ":mend:" + w.Turn) % 3 == 0) { c.Flags.Remove(IllChar); if (ours) w.Log($"{c.Name} is well again."); }
                else c.Health -= 4;
            }
            else if (c.Stress < 50) c.Health = Math.Min(cap, c.Health + 3);
            c.Health = Math.Clamp(c.Health, 1, 100);
            if (c.Health >= Math.Min(cap, 70)) c.Flags.Remove(WoundedChar);

            // the frail, and the old of the house, lay their work down
            bool frail = c.Health < Frail; if (frail) c.Flags.Add(FrailChar); else c.Flags.Remove(FrailChar);
            if (frail || (ours && c.Id != p!.Id && c.Age >= KinRetireAge))
            {
                bool laidDown = false;
                foreach (var fl in w.Fleets.Values.Where(x => x.AdmiralId == c.Id)) { fl.AdmiralId = ""; laidDown = true; }
                if (ours && c.Id != p!.Id)
                {
                    foreach (var d in house!.Duties.Where(d => d.HolderId == c.Id).ToList()) { house.Duties.Remove(d); laidDown = true; }
                    foreach (var h in w.Holdings.Values.Where(h => h.OwnerId == house.Id && h.GovernorId == c.Id)) { h.GovernorId = ""; laidDown = true; }
                    if (laidDown || !frail) { if (c.Flags.Add(RetiredChar)) { Note(w, c, "retired", frail ? "Laid down their work, their health gone." : "Laid down their work, full of years."); w.Log($"{c.Name} lays down their work."); } }
                }
            }

            if (!ours) continue;
            // the record: what can be seen to have changed since last year
            if (c.CareerTrack.Length > 0 && c.CareerRank > 0 && Once(c, "rank:" + c.CareerRank) && c.Life!.Marks.Count > 1)
                Note(w, c, "rank", $"Rose to {content?.Careers.RankName(c.CareerTrack, c.CareerRank, w.Era) ?? $"rank {c.CareerRank}"}.");
            if (officers.Contains(c.Id) && Once(c, "office:" + w.Offices.First(o => o.CharacterId == c.Id).OfficeId)) Note(w, c, "office", "Took an office of state.");
            foreach (int at in new[] { 5, 7, 9 })
                foreach (var kv in c.Skills.Where(kv => kv.Value >= at).OrderBy(kv => kv.Key, StringComparer.Ordinal))
                    if (Once(c, $"skill:{kv.Key}:{at}")) Note(w, c, "mastery", $"Became {EducationSystem.LevelName(at)} in {kv.Key}.");
        }

        if (p is not { Alive: true } || house == null) return;
        p.Life ??= new Life();
        // ties that last: who has thought well, or ill, of the head for years
        foreach (var o in w.LivingCharacters().Where(o => o.Id != p.Id && o.Age >= Adult).OrderBy(o => o.Id, StringComparer.Ordinal))
        {
            int d = w.GetRelationship(o.Id, p.Id)?.Disposition ?? 0; bool kin = o.HouseId == p.HouseId;
            if (d >= FriendAt && !kin) { p.Life.Friends.TryAdd(o.Id, w.Turn); p.Life.Enemies.Remove(o.Id); }
            else if (d <= EnemyAt) { p.Life.Enemies.TryAdd(o.Id, w.Turn); p.Life.Friends.Remove(o.Id); }
            else { if (d < FriendAt - 15) p.Life.Friends.Remove(o.Id); if (d > EnemyAt + 15) p.Life.Enemies.Remove(o.Id); }
            o.Flags.Remove(OldFriendChar); o.Flags.Remove(OldEnemyChar);
        }
        foreach (var id in p.Life.Friends.Keys.Concat(p.Life.Enemies.Keys).Where(id => w.Char(id) is not { Alive: true }).ToList()) { p.Life.Friends.Remove(id); p.Life.Enemies.Remove(id); }
        foreach (var (id, since) in p.Life.Friends.Where(kv => w.Turn - kv.Value >= OldTieYears).OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            var o = w.Char(id)!; o.Flags.Add(OldFriendChar); w.WorldFlags.Add(OldFriendFlag);
            var r = w.Relationship(id, p.Id); if (r.Disposition < 80) r.Disposition++;   // it deepens
            if (Once(p, "friend:" + id)) Note(w, p, "friend", $"{o.Name} of {w.House(o.HouseId)?.Name} had been a friend for {OldTieYears} years.");
            _ = since;
        }
        foreach (var (id, _) in p.Life.Enemies.Where(kv => w.Turn - kv.Value >= OldTieYears).OrderBy(kv => kv.Key, StringComparer.Ordinal)) { w.Char(id)!.Flags.Add(OldEnemyChar); w.WorldFlags.Add(OldEnemyFlag); }

        if (p.Flags.Contains(IllChar)) w.WorldFlags.Add(IllFlag);
        if (p.Health < Frail) w.WorldFlags.Add(FrailFlag); else if (p.Health < Sound) w.WorldFlags.Add(AilingFlag);
        if (w.LivingCharacters().Any(c => c.HouseId == house.Id && c.Id != p.Id && c.Flags.Contains(RetiredChar) && p.MentorId == c.Id)) w.WorldFlags.Add(ElderFlag);
    }
}
