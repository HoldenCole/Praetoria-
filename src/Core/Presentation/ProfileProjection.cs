using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;

namespace Praetoria.Core.Presentation;

/// <summary>A trait as the profile shows it: its name and what it means.</summary>
public sealed record ProfileTrait(string Name, string Description);
/// <summary>Someone of a person's blood or bed, as the profile lists them.</summary>
public sealed record ProfileKin(string Id, string Name, string Relation, int Age, bool Alive);
/// <summary>Something they did, with the year and the age they did it at.</summary>
public sealed record ProfileDeed(int Turn, int Age, string Kind, string Text);
/// <summary>What one person thinks of another: the figure, a word for it, the bond between them and why.</summary>
public sealed record ProfileRegard(int Disposition, string Word, string Bond, IReadOnlyList<string> Reasons);

/// <summary>A person, for the profile any portrait opens (docs/ART_DIRECTION.md, "Portraits and profiles"). The same card for the player,
/// a relative, the head of a rival house or a khan beyond the rim: who they are, what they are, what they think of you and you of them,
/// what they are like, what they are good at, whose they are, and what they have done.</summary>
public sealed record PersonProfile(string Id, string Name, string Epithet, string Sex, int Age, bool Alive, string HouseId, string HouseName, string Accent,
    bool IsYou, bool IsHead, bool Foreign, string Station, IReadOnlyList<string> Offices, string Story,
    ProfileRegard? TheirsOfYou, ProfileRegard? YoursOfThem,
    IReadOnlyList<ProfileTrait> Nature, IReadOnlyList<ProfileTrait> Aptitude, IReadOnlyList<string> Marks,
    IReadOnlyList<(string Name, int Value)> Skills, int Health, string HealthName, int Renown, string RenownName, int Stress, string Ambition,
    IReadOnlyList<ProfileKin> Kin, IReadOnlyList<ProfileDeed> Deeds, int Sway, string Ancestry);

/// <summary>What a portrait wears and how its face is set (see <see cref="ProfileProjection.Dress"/>).</summary>
public sealed record BustDress(string Career, int Rank, int Mood, bool Head, bool Laurel, bool Scar, bool Foreign);

public static class ProfileProjection
{
    public const int DeedsShown = 6, SkillsShown = 6;

    /// <summary>A trait as the player reads it: its name in /content/traits ("Honourable"), not its locked id ("Honorable").</summary>
    public static string TraitName(ContentDatabase content, string id) =>
        content.Traits.Nature.Concat(content.Traits.Aptitude).FirstOrDefault(t => t.Id == id)?.Name ?? id;

    /// <summary>A word for a regard, from hatred to devotion.</summary>
    public static string RegardWord(int d) => d >= 60 ? "devoted" : d >= 30 ? "a friend" : d >= 10 ? "well disposed" : d > -10 ? "indifferent" : d > -30 ? "cool" : d > -60 ? "hostile" : "an enemy";

    /// <summary>How a face is set toward the player, from -2 (an enemy's) to +2 (a friend's): the portrait's expression.</summary>
    public static int Mood(World w, string characterId)
    {
        var me = w.Protagonist; if (me == null || me.Id == characterId) return 0;
        int d = w.GetRelationship(characterId, me.Id)?.Disposition ?? 0;
        return d >= 50 ? 2 : d >= 20 ? 1 : d <= -50 ? -2 : d <= -20 ? -1 : 0;
    }

    /// <summary>What a portrait shows of a person beyond their face: the dress of their service and rank, the set of their face toward
    /// the player, the chain of a head of house, the wreath of a triumph, a scar from a wound, the furs of a court beyond the rim.</summary>
    public static BustDress? Dress(World w, string characterId)
    {
        var c = w.Char(characterId); if (c == null) return null;
        var house = w.House(c.HouseId); bool foreign = w.Foreign(c.HouseId);
        return new BustDress(c.CareerTrack.Length > 0 ? c.CareerTrack : "none", c.CareerRank, Mood(w, characterId), house != null && !foreign && Economy.HeadOf(w, house)?.Id == c.Id,
            CaesarSystem.Triumphs(w, c) > 0, c.Life is { Wounds: > 0 }, foreign);
    }

    public static PersonProfile? Build(World w, ContentDatabase content, string characterId)
    {
        var c = w.Char(characterId); if (c == null) return null;
        var me = w.Protagonist; var house = w.House(c.HouseId); bool foreign = w.Foreign(c.HouseId); bool you = me?.Id == c.Id;
        bool head = house != null && Economy.HeadOf(w, house)?.Id == c.Id;
        var nation = foreign ? w.NationOfHouse(c.HouseId) : null; var nd = nation != null ? content.Nations.Def(nation.Id) : null;

        // what they are: a rank in a service, the head of a house, a ruler beyond the rim, a child
        string track = content.Careers.ById(c.CareerTrack)?.NameIn(w.Era) ?? "";
        string? rank = content.Careers.RankName(c.CareerTrack, c.CareerRank, w.Era);
        string station = nd != null && nation!.Ruler?.CharacterId == c.Id ? NationSystem.Styled(nd, nation.Ruler, true).Replace(" " + c.Name, "") + " of " + nd.Name
            : nd != null && nation!.Commander?.CharacterId == c.Id ? nd.CommanderTitle + " of " + nd.Name
            : c.Age < DynastySystem.AgeOfMajority ? "A child of " + (house?.Name ?? "no house")
            : rank != null && track.Length > 0 ? $"{rank} in {track}" : "Of " + (house?.Name ?? "no house") + ", with no service";
        var offices = w.Offices.Where(o => o.CharacterId == c.Id).Select(o => content.Offices.Def(o.OfficeId)?.Name ?? o.OfficeId).OrderBy(x => x, StringComparer.Ordinal).ToList();
        if (head && !foreign) offices.Insert(0, "Head of " + house!.Name);

        int renown = LifeSystem.Renown(c);
        string story = !c.Alive ? $"Dead at {c.Age}. {Cap(LifeSystem.RenownName(renown))}."
            : $"{station}, {c.Age}. {Cap(LifeSystem.HealthName(c.Health))}, and {LifeSystem.RenownName(renown)}."
              + (c.Ambition.Length > 0 && content.Traits.Ambitions.FirstOrDefault(a => a.Id == c.Ambition) is { } amb ? $" Wants to {amb.Name.ToLowerInvariant()}." : "");

        ProfileRegard? theirs = null, yours = null;
        if (me != null && !you)
        {
            var toMe = w.GetRelationship(c.Id, me.Id); var fromMe = w.GetRelationship(me.Id, c.Id);
            theirs = new ProfileRegard(toMe?.Disposition ?? 0, RegardWord(toMe?.Disposition ?? 0), Bond(toMe), Reasons(w, c, me, house));
            yours = new ProfileRegard(fromMe?.Disposition ?? 0, RegardWord(fromMe?.Disposition ?? 0), Bond(fromMe), Array.Empty<string>());
        }

        var nature = c.NatureTraits.OrderBy(t => t, StringComparer.Ordinal).Select(t => new ProfileTrait(TraitName(content, t), content.Traits.Nature.FirstOrDefault(d => d.Id == t || d.Name == t)?.Description ?? "")).ToList();
        var aptitude = c.AptitudeTraits.OrderBy(t => t, StringComparer.Ordinal).Select(t => new ProfileTrait(TraitName(content, t), content.Traits.Aptitude.FirstOrDefault(d => d.Id == t || d.Name == t)?.Description ?? "")).ToList();
        var marks = new List<string>();   // what a life has left on them, and what state they are in
        if (c.Life is { Wounds: > 0 } life) marks.Add(life.Wounds == 1 ? "wounded once" : $"wounded {life.Wounds} times");
        if (c.Stress >= 70) marks.Add("under great strain"); else if (c.Stress >= 40) marks.Add("strained");
        if (CaesarSystem.Triumphs(w, c) > 0) marks.Add(CaesarSystem.Triumphs(w, c) == 1 ? "a triumph" : $"{CaesarSystem.Triumphs(w, c)} triumphs");
        if (c.Flags.Contains(CaesarSystem.RecalledChar)) marks.Add("under recall");
        if (CaesarSystem.Barred(w, c)) marks.Add("barred from command");
        if (c.PatronId.Length > 0 && w.Char(c.PatronId) is { Alive: true } patron) marks.Add("a client of " + patron.Name);
        if (c.BornHouseId.Length > 0 && c.BornHouseId != c.HouseId && w.House(c.BornHouseId) is { } born) marks.Add("born of " + born.Name);

        var skills = c.Skills.Where(s => s.Value > 0).OrderByDescending(s => s.Value).ThenBy(s => s.Key, StringComparer.Ordinal).Take(SkillsShown).Select(s => (s.Key, s.Value)).ToList();

        var kin = new List<ProfileKin>();
        foreach (var (pid, rel) in new[] { (c.FatherId, "father"), (c.MotherId, "mother") }) if (pid.Length > 0 && w.Char(pid) is { } parent) kin.Add(new ProfileKin(parent.Id, parent.Name, rel, parent.Age, parent.Alive));
        if (DynastySystem.Spouse(w, c) is { } spouse) kin.Add(new ProfileKin(spouse.Id, spouse.Name, spouse.Sex == "female" ? "wife" : spouse.Sex == "male" ? "husband" : "spouse", spouse.Age, spouse.Alive));
        foreach (var k in w.Characters.Values.Where(k => k.Id != c.Id && (k.FatherId == c.Id || k.MotherId == c.Id)).OrderByDescending(k => k.Age).ThenBy(k => k.Id, StringComparer.Ordinal))
            kin.Add(new ProfileKin(k.Id, k.Name, k.Sex == "female" ? "daughter" : k.Sex == "male" ? "son" : "child", k.Age, k.Alive));

        var deeds = (c.Life?.Deeds ?? new List<Deed>()).OrderByDescending(d => d.Turn).Take(DeedsShown).Select(d => new ProfileDeed(d.Turn, d.Age, d.Kind, d.Text)).ToList();

        return new PersonProfile(c.Id, c.Name, LifeSystem.Epithet(c), c.Sex, c.Age, c.Alive, c.HouseId, nd?.Name ?? house?.Name ?? c.HouseId, house?.AccentColor ?? "#888888",
            you, head, foreign, station, offices, story, theirs, yours, nature, aptitude, marks, skills, c.Health, LifeSystem.HealthName(c.Health), renown, LifeSystem.RenownName(renown), c.Stress,
            content.Traits.Ambitions.FirstOrDefault(a => a.Id == c.Ambition)?.Name ?? "", kin, deeds, c.CareerTrack == "military" ? CaesarSystem.Sway(w, c) : 0, content.Ancestries.Name(Lineage.Of(w, content.Ancestries, c)));
    }

    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
    private static string Bond(Relationship? r) => r?.Bond is { } b && b != BondType.None ? b.ToString().ToLowerInvariant() : "";

    /// <summary>Why they think of the player as they do, as far as the game knows: blood, an old friendship or enmity, a patron, a war,
    /// a shared bench, a favour owed.</summary>
    private static List<string> Reasons(World w, Character c, Character me, House? theirHouse)
    {
        var why = new List<string>();
        if (c.HouseId == me.HouseId) why.Add("of your own house");
        if (c.FatherId == me.Id || c.MotherId == me.Id) why.Add("your child"); else if (me.FatherId == c.Id || me.MotherId == c.Id) why.Add("your parent");
        if (DynastySystem.Spouse(w, me)?.Id == c.Id) why.Add("your spouse");
        if (c.Life?.Friends.ContainsKey(me.Id) == true) why.Add($"an old friend, since year {c.Life.Friends[me.Id]}");
        if (c.Life?.Enemies.ContainsKey(me.Id) == true) why.Add($"an old enemy, since year {c.Life.Enemies[me.Id]}");
        if (c.PatronId == me.Id) why.Add("your client"); else if (me.PatronId == c.Id) why.Add("your patron");
        if (c.MentorId == me.Id) why.Add("you are their mentor"); else if (me.MentorId == c.Id) why.Add("your mentor");
        if (theirHouse != null && theirHouse.Id != me.HouseId)
        {
            if (w.AtWar(theirHouse.Id, me.HouseId)) why.Add("your houses are at war");
            var mine = FactionSystem.Of(w, me.HouseId); var theirs = FactionSystem.Of(w, theirHouse.Id);
            if (mine != null && theirs != null) why.Add(mine.Id == theirs.Id ? "of your bench in the Senate" : "of another bench in the Senate");
            if (WealthSystem.FavourOwed(w, theirHouse.Id, me.HouseId)) why.Add("their house owes yours a favour"); else if (WealthSystem.FavourOwed(w, me.HouseId, theirHouse.Id)) why.Add("your house owes theirs a favour");
        }
        return why;
    }
}
