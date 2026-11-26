using Praetoria.Core.Data;
using Praetoria.Core.State;
using Praetoria.Core.Systems;

namespace Praetoria.Core.Presentation;

/// <summary>One matter before the courts that touches a house and has not been heard: who, over what, how much, before whom,
/// how the law leans, when it is heard, and who is to plead it.</summary>
/// <param name="Kind"><see cref="Docket.Brought"/>, <see cref="Docket.Against"/>, <see cref="Docket.Charge"/> or <see cref="Docket.Will"/>.</param>
/// <param name="Id">The suit's id (an <see cref="OfficeBusiness"/>), the charge's (an <see cref="Indictment"/>), or the suit of the undutiful will's (a <see cref="WillSuit"/>).</param>
/// <param name="Sum">What turns on it, in the books' hundreds: a suit's stake, a charge's fine.</param>
/// <param name="Good">Whether the law, as it stands, is with the house.</param>
public sealed record DocketEntry(string Kind, string Id, string Title, string Over, int Sum, string Judge, string JudgeIs, string Law, string Heard, string Pleader, string OtherId, bool Good);

/// <summary>The docket of a house (docs/LADDER.md, "The docket"): every matter pending before the courts that touches it, on one
/// list: the suits it brought, the suits against it, and the charges against its people. Read only.</summary>
public static class Docket
{
    public const string Brought = "brought", Against = "against", Charge = "charge", Will = "will";

    public static List<DocketEntry> Of(World w, OfficeCatalog offices, string houseId)
    {
        var list = new List<DocketEntry>(); if (!LadderSystem.Active(w, offices)) return list;
        var suits = LadderSystem.SuitsNaming(w, houseId).Select(b => (b, mine: LadderSystem.PlaintiffOf(w, b) == houseId)).OrderByDescending(x => x.mine).ThenBy(x => x.b.Turn).ThenBy(x => x.b.Id, StringComparer.Ordinal);
        foreach (var (b, mine) in suits)
        {
            string other = w.House(LadderSystem.OtherIn(b, houseId))?.Name ?? "a house"; var judge = w.Char(b.HolderId); var pleader = w.Char(b.AdvocateId) is { Alive: true } a && a.HouseId == houseId ? a : null;
            string title = mine ? $"Your suit against {other}" + (b.Countered ? ", and their counter-suit" : "") : $"{other}'s suit against you" + (b.Countered ? ", and your counter-suit" : "");
            list.Add(new DocketEntry(mine ? Brought : Against, b.Id, title, LadderSystem.MatterOf(b), LadderSystem.StakeOf(b), judge?.Name ?? "a praetor", judge != null ? LadderSystem.NatureOf(judge) : "",
                LadderSystem.LawFor(w, b, houseId), LadderSystem.HeardSays(w, b.Turn), pleader?.Name ?? "", LadderSystem.OtherIn(b, houseId), b.RightId == houseId));
        }
        foreach (var c in LadderSystem.ChargesOn(w, houseId))
        {
            var d = LadderSystem.DeedOf(w, c); var who = w.Char(c.AccusedId); var judge = LadderSystem.JudgeOf(w, c); var pleader = LadderSystem.PleaderOf(w, c); if (d == null || who == null) continue;
            string leans = judge != null ? LadderSystem.Verdict(w, d, c.AccuserId, judge, LadderSystem.DefenceOf(w, pleader)).why : "";
            list.Add(new DocketEntry(Charge, c.Id, $"{w.House(c.AccuserId)?.Name ?? "A house"} charges {who.Name}", "that " + (who.Sex == "female" ? "she " : who.Sex == "male" ? "he " : "they ") + LadderSystem.DeedSays(w, offices, d), LadderSystem.FineCredits,
                judge?.Name ?? "no praetor who sits", leans, LadderSystem.ConvictionSays(w, who), LadderSystem.HeardSays(w, c.Turn), pleader?.Name ?? "", c.AccuserId, false));
        }
        // the suit of the undutiful will, brought within the house by a child the late head struck out (docs/HOUSEHOLD.md)
        foreach (var s in DisownSystem.SuitsOn(w, houseId))
        {
            var child = w.Char(s.PlaintiffId); var judge = DisownSystem.JudgeOf(w, s); var pleader = DisownSystem.AnswersFor(w, s); if (child == null) continue;
            list.Add(new DocketEntry(Will, s.Id, $"{child.Name}'s suit that the will of {s.DeceasedName} was undutiful", DisownSystem.AsksSays(w, s), s.Share,
                judge?.Name ?? "no praetor who sits", judge != null ? LadderSystem.NatureOf(judge) : "", DisownSystem.LawSays(s), LadderSystem.HeardSays(w, s.Turn), pleader?.Name ?? "", child.Id, false));
        }
        return list;
    }
}
