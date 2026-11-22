using Praetoria.Core.State;
using Praetoria.Core.Systems;

namespace Praetoria.Core.Presentation;

/// <summary>How the player's own ambition stands (docs/PLAYTHROUGH.md, Medium, A first game: "Aims: the ambition's conditions
/// and progress ... a next step"). <see cref="Progress"/> is 0 to 100; <see cref="Stands"/> is where it stands in figures;
/// <see cref="Next"/> is one thing that would bring it nearer, and <see cref="Target"/> the window where that is done.</summary>
public sealed record Aim(string Wants, int Progress, bool Had, string Stands, string Next, string Target);

/// <summary>
/// The player's ambition was a word on the character sheet. This reckons it the way the ambitions of the house's other people are
/// reckoned (<see cref="AmbitionSystem"/>), with the differences a head of a house brings: the house's wealth where a relative
/// would count what they gave it, the Reach's offices and the house's rank where a relative would count their own climb. It reads
/// the world and changes nothing; nothing in the rules waits on it. RNG-free.
/// </summary>
public static class OwnAim
{
    public static Aim? For(World w, Character p)
    {
        var house = w.House(p.HouseId); if (house == null || p.Ambition.Length == 0) return null;
        string wants = AmbitionSystem.Wants(p.Ambition);
        switch (p.Ambition)
        {
            case "command_a_fleet":
            case "earn_a_command":
            {
                var mine = w.Fleets.Values.Where(f => f.AdmiralId == p.Id).OrderBy(f => f.Id, StringComparer.Ordinal).FirstOrDefault();
                if (mine != null) return new Aim(wants, 100, true, $"You command {mine.Name}.", "", "military");
                int near = Math.Min(60, p.CareerRank * 12 + p.Skill("tactics") * 4); bool any = w.Fleets.Values.Any(f => f.OwnerId == house.Id && f.LevyOf.Length == 0 && f.HiredFrom.Length == 0);
                return new Aim(wants, near, false, $"You command no fleet. Rank and skill at tactics bring it to {near} of 100 without one; a command is the rest.",
                    any ? "Name yourself admiral of one of the house's fleets." : "The house has no fleet: raise hulls at a world with a shipyard, and take the command.", "military");
            }
            case "amass_a_fortune":
            {
                int near = Math.Clamp(p.Purse * 100 / AmbitionSystem.FortuneAt, 0, 100);
                return new Aim(wants, near, near >= 100, $"Your own purse holds {Money.Credits(p.Purse)}; {Money.Show(AmbitionSystem.FortuneAt)} is a fortune. The house's treasury is not counted: it is the house's.",
                    near >= 100 ? "" : "Pay yourself an allowance from the treasury, or have a venture of the house settled on you.", "house:purse");
            }
            case "restore_house_fortunes":
            {
                int near = Math.Clamp(house.Wealth * 100 / AmbitionSystem.RestoredAt, 0, 100);
                return new Aim(wants, near, near >= 100, $"The house is worth {Money.Show(house.Wealth)}; at {Money.Show(AmbitionSystem.RestoredAt)} it is rich again. Its worth is its treasury, its stores, its worlds and its ventures, less what it owes.",
                    near >= 100 ? "" : house.Debt > 0 ? "Clear the house's debt: what is owed is taken straight off its worth." : "Found or enlarge a venture, or build on a world: what yields is counted in the house's worth.", "house:ledger");
            }
            case "master_the_senate":
            {
                var office = w.Offices.FirstOrDefault(o => o.CharacterId == p.Id); bool magistrate = w.Magistrates.Any(m => m.CharacterId == p.Id);
                if (office != null || magistrate) return new Aim(wants, 100, true, "You hold an office of the Reach.", "", "senate");
                int near = Math.Min(90, p.CareerRank * 8 + p.Skill("oratory") * 4);
                return new Aim(wants, near, false, $"You hold no office of the Reach. Rank and skill at oratory bring it to {near} of 100 without one; an office is the rest.",
                    "Stand for an office when one is open: the ladder, and the consulship, are under Senate.", "senate");
            }
            case "outshine_all_rivals":
            {
                var rival = AmbitionSystem.Members(w, house).OrderByDescending(AmbitionSystem.Standing).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();
                if (rival == null) return new Aim(wants, 100, true, "There is nobody grown of the house to outshine.", "", "dynasty");
                int lead = AmbitionSystem.Standing(p) - AmbitionSystem.Standing(rival), near = Math.Clamp(50 + lead * 5, 0, 100);
                return new Aim(wants, near, near >= 100, $"What you know and how far you have risen come to {AmbitionSystem.Standing(p)}; {rival.Name}, the best of the house after you, comes to {AmbitionSystem.Standing(rival)}. Ten clear is beyond question.",
                    near >= 100 ? "" : "Study, a tutor, or a rise in your service: each point of skill counts one, each rank three.", "dynasty:upbringing");
            }
            case "seize_the_throne":
            {
                int rank = Progression.Title.RankOf(house.Title), top = Math.Max(1, Progression.Title.RankOf(Progression.Title.Emperor));
                if (rank >= top) return new Aim(wants, 100, true, "The house holds the highest seat there is.", "", "senate");
                int near = Math.Clamp(rank * 100 / top, 0, 99);
                return new Aim(wants, near, false, $"The house stands on rung {rank} of {top}. {(w.IsImperium ? "The throne is the last of them." : "The Republic stands, and has no throne: the last rung is taken only by ending it.")}",
                    "Rise a rung: by petition, by a claim or by force, under Senate. The outliner says when the next is within reach.", "senate");
            }
            case "win_a_great_love":
            {
                var m = w.MarriageOf(p.Id);
                if (m is { Kind: CourtshipSystem.Love }) return new Aim(wants, 100, true, "You married for love.", "", "dynasty:marriages");
                if (m != null) return new Aim(wants, Math.Clamp(m.Warmth * 60 / 100, 0, 60), false, $"You are married, and it was not for love. The marriage's warmth is {m.Warmth} of 100.", "Time together warms a marriage: Dynasty, Marriages.", "dynasty:marriages");
                var suit = w.Courtships.FirstOrDefault(c => c.SuitorId == p.Id && !c.Incoming); var t = suit != null ? w.Char(suit.TargetId) : null;
                if (suit != null && t != null) return new Aim(wants, Math.Min(90, suit.Favour), false, $"You are paying court to {t.Name}, whose favour is {suit.Favour} of 100. At {CourtshipSystem.LoveAt} it is love.", "Spend time with them each year, and ask when the family would agree.", "dynasty:marriages");
                return new Aim(wants, 0, false, "You are unmarried and are courting nobody.", "Begin to pay court to someone: Dynasty, Marriages.", "dynasty:marriages");
            }
            default: return null;
        }
    }
}
