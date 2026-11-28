using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;

namespace Praetoria.Core.Presentation;

/// <summary>Somebody in a moment of the year: whose face it is, and what is said under it ("your wife", "head of House Vey").</summary>
public sealed record MomentFace(string Id, string Says);

/// <summary>
/// One moment of the year for the player's line (<see cref="YearMoments"/>): what kind it is and how much it weighs, the words of its
/// card (a kicker, a title, one line of prose, one button and its glyph), who stands in it (the large plate, or the couple, in
/// <see cref="Lead"/>; the family around in <see cref="Around"/>), the house on the other side of it when there is one (its arms
/// stand in where it has no face), and the line the year in brief keeps of it: the subject that line is about, its section, its
/// tone and where it opens. The painting behind it is the kind's (<see cref="SceneArt.ForMoment"/>).
/// </summary>
public sealed record Moment(string Kind, string Key, int Weight, string Kicker, string Title, string Text, string Button, string Icon,
    IReadOnlyList<MomentFace> Lead, IReadOnlyList<MomentFace> Around, string House, string Subject, string Section, string Tone, string Line, string Target)
{
    /// <summary>The painting the card stands on: one of <see cref="SceneArt.Scenes"/>.</summary>
    public string Scene => SceneArt.ForMoment(Kind);
    /// <summary>Ill news: the line of it in the year in brief is grave by the moment's weight, and may stand in the red band.</summary>
    public bool Bad => Tone == "warn";
    /// <summary>The seat passing at a head's death is staged by the succession screen, not by a card of its own.</summary>
    public bool Staged => Kind == YearMoments.Succession;
}

/// <summary>
/// The biggest moments of a year in the life of the player's line, made a moment on the screen instead of a line in a list
/// (docs/VISUAL_SYSTEM.md, "The year's moments"; docs/WEB_CLIENT.md, "After End Year"). Read as a year opens, from what the year
/// that has just ended recorded, the way the year in brief finds it: the deaths of the house (and the seat passing), the newborn,
/// the weddings, the young come of age, the wars declared, the sieges laid, the worlds and the seat lost, the rank, the suits and
/// the charges. Only those of the main line are moments: the head, the spouse, the heir, the children, the parents and the
/// siblings, and the house itself; the rest stay lines. The heaviest first, a death in the main line before anything else; the
/// client opens <see cref="MaxCards"/> of them at most, one at a time, before the year in brief. Read only; no dice.
/// </summary>
public static class YearMoments
{
    public const string Succession = "succession", Death = "death", Birth = "birth", Marriage = "marriage", OfAge = "of_age",
        War = "war", Siege = "siege", WorldLost = "world_lost", SeatLost = "seat_lost", RankLost = "rank_lost", Suit = "suit";
    /// <summary>Every kind, in the order a tie in weight is broken.</summary>
    public static readonly string[] Kinds = { Succession, Death, War, SeatLost, Birth, RankLost, WorldLost, Marriage, Siege, Suit, OfAge };
    /// <summary>At most this many moments of a year are opened as cards; the rest stay lines in the year in brief.</summary>
    public const int MaxCards = 3;

    /// <summary>What each moment weighs: a death in the main line first, then the house's own ill fortune and its births and weddings.</summary>
    public const int HeadDies = 100, SpouseDies = 96, HeirDies = 95, ChildDies = 94, ParentDies = 92, SiblingDies = 88,
        WarDeclared = 84, GreatSeatLost = 82, LastWorldLost = 82, ChildBorn = 80, RankFalls = 78, WorldTaken = 76, HeadWeds = 74,
        GrandchildBorn = 72, ProvinceLost = 68, ChildWeds = 66, Besieged = 64, HeadCharged = 60, Sued = 58, HeadOfAge = 56, HeirOfAge = 54, ChildOfAge = 50;

    /// <summary>The moments of the year that has just ended (the world is at the start of the next), the heaviest first.</summary>
    public static List<Moment> For(World w, ContentDatabase content)
    {
        var list = new List<Moment>();
        var head = w.Protagonist; var house = head != null ? w.House(head.HouseId) : null;
        if (head == null || house == null || w.InAcademy || w.IsDynastyDead || w.Turn <= 1) return list;
        int year = w.Turn - 1;
        Deaths(w, head, house, year, list);
        Births(w, head, house, list);
        Weddings(w, head, house, year, list);
        OfAgeNow(w, content, head, house, list);
        Wars(w, head, house, year, list);
        Sieges(w, head, house, list);
        Losses(w, head, house, list);
        Rank(w, content, head, house, year, list);
        Courts(w, head, house, year, list);
        return list.DistinctBy(m => m.Key).OrderByDescending(m => m.Weight).ThenBy(m => Array.IndexOf(Kinds, m.Kind)).ThenBy(m => m.Key, StringComparer.Ordinal).ToList();
    }

    /// <summary>The moments the client opens as cards: the heaviest <see cref="MaxCards"/>. The seat passing is among them when it
    /// is one, and the succession screen stages it.</summary>
    public static List<Moment> Cards(World w, ContentDatabase content) => For(w, content).Take(MaxCards).ToList();

    // ---------------- the deaths ----------------

    private static void Deaths(World w, Character head, House house, int year, List<Moment> list)
    {
        // the head who died in the year: the succession screen is this moment's card
        foreach (var s in w.Successions.Where(s => s.Turn >= year && s.HeirId == head.Id && s.Cause == "death"))
        {
            if (w.Char(s.DeceasedId) is not { } old) continue;
            list.Add(new Moment(Succession, "succession:" + old.Id, HeadDies, "In mourning", "The seat passes",
                $"{old.Name} died at {s.DeceasedAge}, head of {house.Name}. You are head of the house now.", "Take up the name", "laurels",
                new[] { new MomentFace(old.Id, Join(Tie(w, head, old), "head of the house", $"died at {s.DeceasedAge}")) }, new[] { new MomentFace(head.Id, "you · head now") }, "",
                old.Id, YearInBrief.Family, "warn", $"{old.Name}, head of the house, died at {s.DeceasedAge}. You are head now.", "dynasty:tree"));
        }
        foreach (var d in w.Deaths.Where(d => d.Turn >= year))
        {
            if (w.Char(d.PersonId) is not { Alive: false } c) continue;
            bool spouse = Wed(w, head, c), child = ChildOf(c, head), parent = ChildOf(head, c), sibling = Siblings(head, c), elder = FormerHead(c);
            // kin a start wrote down only as of the head's blood (a brother with no parents on the record): as close as a sibling
            bool blood = !spouse && !child && !parent && !sibling && w.GetRelationship(head.Id, c.Id)?.Bond == BondType.Blood;
            int weight = spouse ? SpouseDies : d.Heir ? HeirDies : child ? ChildDies : parent || elder ? ParentDies : sibling || blood ? SiblingDies : 0;
            if (weight == 0) continue;   // a cousin, a niece, one married into a son's family: a line in the year in brief
            string tie = Tie(w, head, c), how = Gendered(d.How, c);
            string title = spouse ? $"Your {(F(c) ? "wife" : "husband")} is dead"
                : child && d.Heir ? $"Your {(F(c) ? "daughter" : "son")} and heir is dead"
                : d.Heir ? "Your heir is dead"
                : tie.StartsWith("your ", StringComparison.Ordinal) ? $"{Cap(tie)} is dead"
                : elder ? "The old head is dead"
                : $"{c.Name} is dead";
            var next = DynastySystem.FindHeir(w, head);
            // the years of a marriage made in the saga are known; one that stood when it opened (dated 0) is not counted from there
            var marriage = w.Marriages.FirstOrDefault(m => m.Has(c.Id) && m.Has(head.Id) && m.Since > 0);
            string more = d.Heir ? (next != null ? $" {Cap(He(c))} was your heir, and {next.Name} is now." : $" {Cap(He(c))} was your heir, and nobody stands after {Him(c)}.")
                : spouse && marriage != null ? (year - marriage.Since > 0 ? $" You were married {Counted.Years(year - marriage.Since)}." : " You were married this year.")
                : elder ? $" {Cap(He(c))} was head of the house before you."
                : w.Funerals.LastOrDefault(f => f.PersonId == c.Id)?.Lines.LastOrDefault() is { Length: > 0 } last ? " " + last : "";
            // the family around the bier: you, then their husband or wife, their parents and their children, those living
            var around = new List<Character> { head };
            if (DynastySystem.Spouse(w, c) is { } theirs) around.Add(theirs);
            around.AddRange(Parents(c).Select(w.Char).Where(x => x is { Alive: true }).Select(x => x!));
            around.AddRange(w.LivingCharacters().Where(k => ChildOf(k, c)).OrderByDescending(k => k.Age).ThenBy(k => k.Id, StringComparer.Ordinal));
            list.Add(new Moment(Death, "death:" + c.Id, weight, "In mourning", title,
                $"{c.Name} died at {d.Age}{(how.Length > 0 ? ", " + how : "")}.{more}", "Mourn " + Him(c), "candle-light",
                new[] { new MomentFace(c.Id, Join(tie, $"died at {d.Age}")) }, Faces(w, head, around, c.Id), "",
                c.Id, YearInBrief.Family, "warn", $"{c.Name} died at {d.Age}.", "dynasty:dead"));
        }
    }

    // ---------------- the newborn ----------------

    private static void Births(World w, Character head, House house, List<Moment> list)
    {
        var heir = DynastySystem.FindHeir(w, head); var line = DynastySystem.Line(w, head);
        foreach (var c in w.LivingCharacters().Where(c => c.Age == 0 && c.HouseId == house.Id && c.Id != head.Id).OrderBy(c => c.Id, StringComparer.Ordinal))
        {
            var parents = new[] { w.Char(c.FatherId), w.Char(c.MotherId) }.Where(x => x != null).Select(x => x!).ToList();
            bool own = parents.Any(x => x.Id == head.Id), grand = parents.Any(x => ChildOf(x, head)), heirs = heir != null && heir.Id != c.Id && parents.Any(x => x.Id == heir.Id);
            if (!own && !grand && !heirs) continue;   // born to a cousin of the house: a line in the year in brief
            string tie = Tie(w, head, c), noun = tie.StartsWith("your ", StringComparison.Ordinal) && tie.IndexOf(' ', 5) < 0 ? tie[5..] : "child";
            int place = line.FindIndex(x => x.Id == c.Id) + 1;
            string where = place == 1 ? ", and is your heir" : place > 1 ? $", and stands {WarLedger.Nth(place)} in the line" : "";
            var other = parents.FirstOrDefault(x => x.Id != head.Id);
            string to = own ? "you" + (other != null ? " and " + other.Name : "") : string.Join(" and ", parents.Select(x => x.Name));
            list.Add(new Moment(Birth, "birth:" + c.Id, own ? ChildBorn : GrandchildBorn, "A birth", $"A {noun} is born",
                $"{c.Name} is born to {to}{where}.", "Welcome " + Him(c), "sprout",
                new[] { new MomentFace(c.Id, Join(tie, "newborn")) }, Faces(w, head, parents, c.Id), "",
                c.Id, YearInBrief.Family, "good", $"{c.Name} was born to {to}{where}.", "dynasty"));
        }
    }

    // ---------------- the weddings ----------------

    private static void Weddings(World w, Character head, House house, int year, List<Moment> list)
    {
        foreach (var m in w.Marriages.Where(m => m.Since == year).OrderBy(m => m.A, StringComparer.Ordinal))
        {
            if (w.Char(m.A) is not { } a || w.Char(m.B) is not { } b) continue;
            var ours = a.Id == head.Id || b.Id == head.Id ? (a.Id == head.Id ? a : b) : ChildOf(a, head) ? a : ChildOf(b, head) ? b : null;
            if (ours == null) continue;   // a cousin's wedding: a line in the year in brief
            var theirs = ours == a ? b : a; bool you = ours.Id == head.Id;
            var listed = a.HouseId == house.Id ? a : b;   // the one the year in brief's line is about
            string of = theirs.HouseId != house.Id && w.House(theirs.HouseId) is { } th ? " of " + th.Name : "";
            string terms = (m.Kind == CourtshipSystem.Love ? ", for love" : "") + (m.Dowry > 0 ? $", with a dowry of {Money.Credits(m.Dowry)} {(m.DowryFrom == house.Id ? "paid" : "received")}" : "");
            var parents = you ? new List<Character>() : Parents(ours).Select(w.Char).Where(x => x is { Alive: true }).Select(x => x!).ToList();
            list.Add(new Moment(Marriage, "marriage:" + m.A + ":" + m.B, you ? HeadWeds : ChildWeds, "A wedding",
                you ? "You are married" : $"Your {(F(ours) ? "daughter" : "son")} is married",
                you ? $"You wed {theirs.Name}{of}{terms}." : $"{ours.Name} weds {theirs.Name}{of}{terms}.", "Raise a glass", "crowned-heart",
                new[] { new MomentFace(ours.Id, Tie(w, head, ours)), new MomentFace(theirs.Id, Tie(w, head, theirs) is { Length: > 0 } t ? t : of.Trim()) }, Faces(w, head, parents, ""), theirs.HouseId != house.Id ? theirs.HouseId : "",
                listed.Id, YearInBrief.Family, "good", $"{ours.Name} married {theirs.Name}{of}{terms}.", "dynasty:marriages"));
        }
    }

    // ---------------- of age ----------------

    private static void OfAgeNow(World w, ContentDatabase content, Character head, House house, List<Moment> list)
    {
        var heir = DynastySystem.FindHeir(w, head);
        foreach (var c in w.LivingCharacters().Where(c => c.Age == DynastySystem.AgeOfMajority && c.HouseId == house.Id).OrderBy(c => c.Id, StringComparer.Ordinal))
        {
            bool you = c.Id == head.Id, isHeir = heir?.Id == c.Id, child = ChildOf(c, head);
            if (!you && !isHeir && !child) continue;
            string tie = Tie(w, head, c), track = content.Careers.ById(c.CareerTrack)?.NameIn(w.Era) ?? "";
            string goes = track.Length > 0 ? ", and is to go into " + track : "";
            var parents = Parents(c).Select(w.Char).Where(x => x is { Alive: true }).Select(x => x!).ToList();
            list.Add(new Moment(OfAge, "of_age:" + c.Id, you ? HeadOfAge : isHeir ? HeirOfAge : ChildOfAge, "Of age",
                you ? "You come of age" : isHeir ? "Your heir comes of age" : $"Your {(F(c) ? "daughter" : "son")} comes of age",
                you ? "You are sixteen. The regency is over, and the house is yours to rule." : $"{c.Name} is sixteen and of age today{goes}.", "Mark the day", "laurels",
                new[] { new MomentFace(c.Id, Join(tie, "sixteen")) }, Faces(w, head, parents, c.Id), "",
                c.Id, YearInBrief.Family, "good", you ? "You came of age, and the regency is over." : $"{c.Name} came of age{goes}.", "dynasty"));
        }
    }

    // ---------------- war, siege, and what is lost ----------------

    private static void Wars(World w, Character head, House house, int year, List<Moment> list)
    {
        foreach (var war in w.Wars.Concat(w.PastWars).Where(x => x.Since >= year && x.HouseB == house.Id && x.HouseA != house.Id))
        {
            if (w.House(war.HouseA) is not { } foe) continue;
            var foeHead = Economy.HeadOf(w, foe);
            string why = war.Why.Length > 0 ? " " + Cap(war.Why) + "." : "";
            list.Add(new Moment(War, "war:" + foe.Id + ":" + war.Since, WarDeclared, "War declared", $"{foe.Name} declares war",
                $"{(foeHead != null ? foeHead.Name + " of " + foe.Name : foe.Name)} has declared war on your house, with {Counted.Hulls(FleetSystem.StrengthOf(w, foe.Id))} to your {FleetSystem.StrengthOf(w, house.Id)}.{why}", "To arms", "crossed-sabres",
                Lead(foeHead, "head of " + foe.Name), new[] { new MomentFace(head.Id, "you") }, foe.Id,
                foe.Id, YearInBrief.War, "warn", $"{foe.Name} declared war on your house.", "military"));
        }
    }

    private static void Sieges(World w, Character head, House house, List<Moment> list)
    {
        foreach (var s in w.Sieges.Values.Where(s => s.Turns == 1).OrderBy(s => s.HoldingId, StringComparer.Ordinal))
        {
            if (w.Holding(s.HoldingId) is not { } h || h.OwnerId != house.Id) continue;
            var foe = w.House(s.BesiegerId); string foeName = foe?.Name ?? "an enemy";
            var admiral = w.Fleets.Values.Where(f => f.OwnerId == s.BesiegerId && f.SystemId == h.SystemId).OrderByDescending(f => f.Strength).ThenBy(f => f.Id, StringComparer.Ordinal)
                .Select(f => w.Char(f.AdmiralId)).FirstOrDefault(c => c is { Alive: true });
            var lead = admiral ?? (foe != null ? Economy.HeadOf(w, foe) : null);
            var gov = w.Char(h.GovernorId) is { Alive: true } g && g.HouseId == house.Id && g.Id != head.Id ? g : null;
            int left = Math.Max(1, s.Required - s.Turns);
            string falls = left == 1 ? "falls at the end of this year" : "falls in " + Counted.Years(left);
            var around = new List<MomentFace>(); if (gov != null) around.Add(new MomentFace(gov.Id, Join(Tie(w, head, gov), "governor of " + h.Name))); around.Add(new MomentFace(head.Id, "you"));
            list.Add(new Moment(Siege, "siege:" + h.Id, Besieged, "Under siege", $"{h.Name} is under siege",
                $"The fleet of {foeName} lies over {h.Name}{(admiral != null ? ", under " + admiral.Name : "")}. Unless the sky is taken back, the world {falls}.", "To arms", "checked-shield",
                Lead(lead, admiral != null ? "admiral of " + foeName : "head of " + foeName), around, s.BesiegerId,
                h.Id, YearInBrief.War, "warn", $"{Cap(foeName)} laid siege to {h.Name}: unless the sky is taken back, the world {falls}.", "galaxy:" + h.SystemId));
        }
    }

    private static void Losses(World w, Character head, House house, List<Moment> list)
    {
        // what changed hands in the wars' accounts for the year: the account dated with the year that opens is the year just ended's
        var changes = w.Wars.Concat(w.PastWars).SelectMany(x => x.Years.Where(y => y.Turn == w.Turn)).SelectMany(y => y.Changed).Where(c => c.FromId == house.Id).ToList();
        foreach (var c in changes)
        {
            if (w.Holding(c.HoldingId) is not { } h || h.OwnerId == house.Id) continue;
            var foe = w.House(c.ToId); string foeName = foe?.Name ?? "another house"; var foeHead = foe != null ? Economy.HeadOf(w, foe) : null;
            if (c.World)
                list.Add(new Moment(WorldLost, "world_lost:" + h.Id, c.Seat ? LastWorldLost : WorldTaken, "World lost", $"{c.Name} is lost",
                    (c.How == "ceded" ? $"{c.Name} was given up to {foeName} at the peace." : $"{(foeHead != null ? foeHead.Name + " of " + foeName : Cap(foeName))} has taken {c.Name} from your house.") + (c.Seat ? " It was the last world your house held." : ""), "Bear it", "ringed-planet",
                    Lead(foeHead, "head of " + foeName), new[] { new MomentFace(head.Id, "you") }, c.ToId,
                    h.Id, YearInBrief.War, "warn", c.How == "ceded" ? $"{c.Name} was given up to {foeName} at the peace." : $"{c.Name} is lost: {foeName} has taken it.", WarLedger.Target));
            else if (h.IsPlanet && c.Name != h.Name)
                list.Add(new Moment(SeatLost, "seat_lost:" + h.Id + ":" + c.Name, ProvinceLost, "Seat lost", $"{c.Name} is lost",
                    $"Your house had no fleet left to keep {c.Name}, its seat on {h.Name}, and {foeName} has taken it.", "Bear it", "castle",
                    Lead(foeHead, "head of " + foeName), new[] { new MomentFace(head.Id, "you") }, c.ToId,
                    h.Id, YearInBrief.Works, "warn", $"{c.Name} is lost: {foeName} has taken it.", "galaxy:" + h.SystemId));
        }
        foreach (var n in w.NewsNow().Where(n => n.Tone == "warn" && n.SubjectId.Length > 0))
        {
            if (w.Holding(n.SubjectId) is not { } h || h.OwnerId == house.Id) continue;
            var holder = w.House(h.OwnerId); var holderHead = holder != null ? Economy.HeadOf(w, holder) : null;
            // a world taken by a power the wars' accounts do not keep (a nation beyond the Reach): told, and so a moment
            if (n.Target == WarLedger.Target && h.IsPlanet && !changes.Any(c => c.HoldingId == h.Id))
                list.Add(new Moment(WorldLost, "world_lost:" + h.Id, WorldTaken, "World lost", $"{h.Name} is lost",
                    $"{(holder != null ? holder.Name : "Another power")} has taken {h.Name} from your house.", "Bear it", "ringed-planet",
                    Lead(holderHead, holder != null ? "head of " + holder.Name : ""), new[] { new MomentFace(head.Id, "you") }, h.OwnerId,
                    h.Id, YearInBrief.War, "warn", n.Text, WarLedger.Target));
            // the house's own seat, the manor or the great house or the palace, lost with the world it stood on (docs/WEALTH.md)
            else if (n.Target == "house:purse")
                list.Add(new Moment(SeatLost, "seat_lost:" + h.Id, GreatSeatLost, "Seat lost", "The seat is lost",
                    $"{n.Text.TrimEnd('.')}. The house is thought the less of for it.", "Bear it", "castle",
                    Lead(holderHead, holder != null ? "head of " + holder.Name : ""), new[] { new MomentFace(head.Id, "you") }, h.OwnerId,
                    h.Id, YearInBrief.Money, "warn", n.Text, "house:purse"));
        }
    }

    private static void Rank(World w, ContentDatabase content, Character head, House house, int year, List<Moment> list)
    {
        if (!w.WorldCounters.TryGetValue(YearInBrief.LastKey("open", YearInBrief.RankKey), out int was) || !w.WorldCounters.TryGetValue(YearInBrief.OpenKey(YearInBrief.RankKey), out int now) || now >= was) return;
        if (w.Successions.Any(s => s.Turn >= year && s.Cause == "adoption")) return;   // another house took up the banner: its rank was never this one's
        string wasName = content.Titles.NameOf(Progression.Title.OfRank(was), w.Era), nowName = now > 0 ? content.Titles.NameOf(Progression.Title.OfRank(now), w.Era) : "";
        bool last = w.Wars.Concat(w.PastWars).SelectMany(x => x.Years.Where(y => y.Turn == w.Turn)).SelectMany(y => y.Changed).Any(c => c.FromId == house.Id && c.Seat);
        string fell = now > 0 ? $"holds the rank of {nowName} at its end" : "holds no rank at its end";
        list.Add(new Moment(RankLost, "rank_lost:" + was + ":" + now, RankFalls, "Rank lost", now > 0 ? $"The house falls to {nowName}" : "The house is landless",
            $"Your house held the rank of {wasName} as the year opened, and {fell}.{(last ? " It lost its last world, and the rank with it." : "")}", "Bear it", "crown",
            new[] { new MomentFace(head.Id, "you · head of " + house.Name) }, Array.Empty<MomentFace>(), "",
            house.Id, YearInBrief.Senate, "warn", now > 0 ? $"Your house fell from the rank of {wasName} to {nowName}." : $"Your house lost the rank of {wasName}, and holds none now.", "senate"));
    }

    private static void Courts(World w, Character head, House house, int year, List<Moment> list)
    {
        // a suit brought against the house by another (docs/LADDER.md, "The docket"): heard at the end of the next year
        foreach (var b in LadderSystem.SuitsNaming(w, house.Id).Where(b => b.Turn >= year && LadderSystem.PlaintiffOf(w, b) != house.Id))
        {
            var foe = w.House(LadderSystem.OtherIn(b, house.Id)); string foeName = foe?.Name ?? "A house"; var foeHead = foe != null ? Economy.HeadOf(w, foe) : null; var judge = w.Char(b.HolderId);
            string heard = LadderSystem.HeardSays(w, b.Turn), stake = Money.Credits(LadderSystem.StakeOf(b));
            var around = new List<MomentFace>(); if (judge is { Alive: true }) around.Add(new MomentFace(judge.Id, "praetor · to hear it")); around.Add(new MomentFace(head.Id, "you"));
            list.Add(new Moment(Suit, "suit:" + b.Id, Sued, "The courts", $"{foeName} sues your house",
                $"{foeName} has brought a suit against your house over {LadderSystem.MatterOf(b)}: {stake} turn on it, and {(judge != null ? judge.Name + ", praetor, hears it" : "it is heard")} {heard}. {LadderSystem.LawFor(w, b, house.Id)}", "Make ready", "scales",
                Lead(foeHead, "head of " + foeName), around, foe?.Id ?? "",
                foe?.Id ?? b.Id, YearInBrief.Courts, "warn", $"{foeName} brought a suit against your house over {LadderSystem.MatterOf(b)}: {stake} turn on it, heard {heard}.", "senate:ladder"));
        }
        // a charge brought against one of the house
        foreach (var ch in LadderSystem.ChargesOn(w, house.Id).Where(c => c.Turn >= year && c.AccuserId != house.Id))
        {
            if (w.Char(ch.AccusedId) is not { } who) continue;
            var accuser = w.House(ch.AccuserId); string accuserName = accuser?.Name ?? "A house"; var accuserHead = accuser != null ? Economy.HeadOf(w, accuser) : null; var judge = LadderSystem.JudgeOf(w, ch);
            bool you = who.Id == head.Id; string tie = Tie(w, head, who), heard = LadderSystem.HeardSays(w, ch.Turn);
            var around = new List<MomentFace>(); if (accuserHead != null) around.Add(new MomentFace(accuserHead.Id, "brings the charge")); if (judge is { Alive: true }) around.Add(new MomentFace(judge.Id, "praetor · to hear it"));
            list.Add(new Moment(Suit, "charge:" + ch.Id, you ? HeadCharged : Sued, "The courts",
                you ? "You are charged" : tie.StartsWith("your ", StringComparison.Ordinal) ? $"{Cap(tie)} is charged" : $"{who.Name} is charged",
                $"{accuserName} has charged {(you ? "you" : who.Name)}{(judge != null ? " before " + judge.Name + ", praetor" : "")}. It is heard {heard}.", "Make ready", "scales",
                new[] { new MomentFace(who.Id, Join(tie, "charged")) }, around, ch.AccuserId,
                who.Id, YearInBrief.Senate, "warn", $"{accuserName} charged {(you ? "you" : who.Name)}: it is heard {heard}.", "senate:ladder"));
        }
    }

    // ---------------- words and faces ----------------

    /// <summary>How somebody stands to the head, in a card's words: "you", "your wife", "your son"; "of your blood" for kin a start
    /// wrote down only by a bond of blood (a brother with no parents on the record); "" when nothing ties them. A husband or wife who
    /// died this year is still named so, though the marriage's record has gone with them.</summary>
    public static string Tie(World w, Character head, Character c) =>
        c.Id == head.Id ? "you" : Wed(w, head, c) ? (F(c) ? "your wife" : "your husband")
        : Kinship.Of(w, head, c) is { Length: > 0 } kin ? kin
        : w.GetRelationship(head.Id, c.Id)?.Bond == BondType.Blood ? "of your blood" : "";

    private static IReadOnlyList<MomentFace> Lead(Character? c, string says) => c == null ? Array.Empty<MomentFace>() : new[] { new MomentFace(c.Id, says) };
    /// <summary>The family around the one a card is about: up to four, each once, never the one it is about, each with their tie to you.</summary>
    private static IReadOnlyList<MomentFace> Faces(World w, Character head, IEnumerable<Character> people, string but) =>
        people.Where(x => x.Id != but).DistinctBy(x => x.Id).Take(4).Select(x => new MomentFace(x.Id, Tie(w, head, x) is { Length: > 0 } t ? t : w.House(x.HouseId)?.Name ?? "")).ToList();
    private static string Join(params string[] parts) => string.Join(" · ", parts.Where(p => p.Length > 0));
    private static bool F(Character c) => c.Sex == "female";
    private static string He(Character c) => c.Sex == "female" ? "she" : c.Sex == "male" ? "he" : "they";
    private static string Him(Character c) => c.Sex == "female" ? "her" : c.Sex == "male" ? "him" : "them";
    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
    /// <summary>How someone died, said of them: "of what ailed them" of a named girl is "of what ailed her".</summary>
    private static string Gendered(string how, Character c) => c.Sex is "female" or "male"
        ? how.Replace("ailed them", "ailed " + Him(c)).Replace("their own", (F(c) ? "her" : "his") + " own") : how;
    private static IEnumerable<string> Parents(Character c) => new[] { c.FatherId, c.MotherId, c.AdoptedById }.Where(x => x.Length > 0).Distinct();
    private static bool ChildOf(Character child, Character parent) => child.MotherId == parent.Id || child.FatherId == parent.Id || child.AdoptedById == parent.Id;
    private static bool Siblings(Character a, Character b) => a.Id != b.Id && new[] { a.MotherId, a.FatherId }.Where(x => x.Length > 0).Intersect(new[] { b.MotherId, b.FatherId }.Where(x => x.Length > 0)).Any();
    private static bool Wed(World w, Character a, Character b) => w.MarriageOf(a.Id)?.Has(b.Id) == true || w.GetRelationship(a.Id, b.Id)?.Bond == BondType.Marriage;
    /// <summary>A head who laid the house down and lived on as its elder (docs/LIFE.md).</summary>
    private static bool FormerHead(Character c) => c.Flags.Contains(LifeSystem.RetiredChar) && c.Life?.Deeds.Any(d => d.Kind == "head") == true;
}
