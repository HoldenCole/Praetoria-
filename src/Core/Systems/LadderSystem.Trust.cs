using Praetoria.Core.Data;
using Praetoria.Core.State;

namespace Praetoria.Core.Systems;

/// <summary>
/// Trust told apart (docs/LADDER.md, "Whose name it is"). Public trust was one number for the whole Reach. It still is, and it
/// is what the Reach as a whole thinks of its magistrates. But what moves it is now also set against the OFFICE whose business it
/// was and the REGION where it was felt: a quaestor who takes money lowers the quaestorship's name further than the Reach's, and
/// the name of the magistrates in his own region further still. An office's name is what a scandal costs at the count for THAT
/// office. A region that does not believe the magistrates holds a little less closely to the Reach each year, and one that
/// does, a little more. An office's name runs back toward the Reach's own figure by a point a year; a region's, which remembers
/// longer, by a point every second year. RNG-free.
/// </summary>
public static partial class LadderSystem
{
    public const int TrustOffsetMax = 30, RegionTrustAllegiance = 1, RegionFeels = 2, RegionForgetsEvery = 2;
    public static readonly string[] TrustOffices = { "quaestor", "aedile", "praetor", "consul", "treasurer", "speaker", "bench", "administration" };
    // whose business is being decided: set for the length of a decision, so that every point of trust it moves is set against it
    [ThreadStatic] private static string? _trustOffice;
    [ThreadStatic] private static string? _trustRegion;
    public static string OfficeTrustKey(string office) => "trust_office:" + office;
    public static string RegionTrustKey(string regionId) => "trust_region:" + regionId;

    /// <summary>Which office's name a piece of business bears on, or "" if none's.</summary>
    public static string OfficeOf(OfficeBusiness b) => b.Kind switch
    {
        Accounts or Works or Case => b.RungId,
        Charge => b.RungId.StartsWith("consul") ? "consul" : b.RungId,
        Hearing or Appeal or Brief => "bench",
        Desk or Ledgers => "administration",
        _ => ""
    };
    public static string OfficeTrustName(string office) => office switch
    {
        "quaestor" => "the quaestors", "aedile" => "the aediles", "praetor" => "the praetors", "consul" => "the consuls", "treasurer" => "the Treasurer", "speaker" => "the Speaker",
        "bench" => "the judges", "administration" => "the administration", _ => "the magistrates"
    };
    /// <summary>What the Reach thinks of one office: its own figure for the magistrates, moved by what that office's holders have done.</summary>
    public static int OfficeTrust(World w, string office) => office.Length == 0 ? Trust(w) : Math.Clamp(Trust(w) + w.Counter(OfficeTrustKey(office)), 0, 100);
    /// <summary>What one region thinks of the magistrates: the Reach's figure, moved by what has been done to it and in it.</summary>
    public static int RegionTrust(World w, string regionId) => regionId.Length == 0 ? Trust(w) : Math.Clamp(Trust(w) + w.Counter(RegionTrustKey(regionId)), 0, 100);

    private static void TrustOf(World w, OfficeBusiness b, Character who)
    {
        _trustOffice = OfficeOf(b); _trustRegion = b.RegionId.Length > 0 ? b.RegionId : HomeRegion(w, who.HouseId) ?? "";
    }
    private static void TrustDone() { _trustOffice = null; _trustRegion = null; }
    /// <summary>Ordinary honest dealing (a single point) mends a name that is under the Reach's and does no more: nobody is admired
    /// for doing the job. Something notable (two or more) can lift a name above the Reach's. A wrong is felt twice over in the
    /// region it was done to: it was their money, or their case.</summary>
    private static void SetAgainst(World w, int by)
    {
        void Move(string key, int n)
        {
            int was = w.Counter(key); if (n == 1 && was >= 0) return;
            int v = Math.Clamp(was + n, -TrustOffsetMax, TrustOffsetMax); if (v == 0) w.WorldCounters.Remove(key); else w.WorldCounters[key] = v;
        }
        if (!string.IsNullOrEmpty(_trustOffice)) Move(OfficeTrustKey(_trustOffice), by);
        if (!string.IsNullOrEmpty(_trustRegion)) Move(RegionTrustKey(_trustRegion), by < 0 ? by * RegionFeels : by);
    }

    /// <summary>The year: a region that does not believe the magistrates loosens, one that does holds closer; and every office's
    /// and every region's own figure runs a point back toward the Reach's.</summary>
    public static void TrustApart(World w)
    {
        if (w.Galaxy != null)
            foreach (var r in w.Galaxy.Regions.OrderBy(r => r.Id, StringComparer.Ordinal))
            {
                if (!w.WorldCounters.ContainsKey(RegionTrustKey(r.Id))) continue;   // a region with no figure of its own goes as the Reach goes, which is reckoned elsewhere
                int t = RegionTrust(w, r.Id); var state = RegionSystem.StateOf(w, r.Id);
                if (t < TrustLow) state.Allegiance = Math.Max(0, state.Allegiance - RegionTrustAllegiance);
                else if (t >= TrustHigh) state.Allegiance = Math.Min(100, state.Allegiance + RegionTrustAllegiance);
            }
        // an office's name runs back a point a year; a region remembers longer, and runs back a point every second year
        foreach (var key in w.WorldCounters.Keys.Where(k => k.StartsWith("trust_office:", StringComparison.Ordinal) || (w.Turn % RegionForgetsEvery == 0 && k.StartsWith("trust_region:", StringComparison.Ordinal))).OrderBy(k => k, StringComparer.Ordinal).ToList())
        {
            int v = w.WorldCounters[key]; v -= Math.Sign(v); if (v == 0) w.WorldCounters.Remove(key); else w.WorldCounters[key] = v;
        }
    }

    /// <summary>The offices and regions whose name stands apart from the Reach's by five or more, said plainly; "" if none does.</summary>
    public static string TrustApartSays(World w)
    {
        int t = Trust(w); var parts = new List<string>();
        foreach (var o in TrustOffices) { int d = w.Counter(OfficeTrustKey(o)); if (Math.Abs(d) >= 5) parts.Add($"{OfficeTrustName(o)} are trusted {(d < 0 ? "less" : "more")} than the rest ({OfficeTrust(w, o)} against {t})".Replace("the Treasurer are", "the Treasurer is").Replace("the Speaker are", "the Speaker is").Replace("the administration are", "the administration is")); }
        if (w.Galaxy != null)
            foreach (var r in w.Galaxy.Regions.OrderBy(r => r.Id, StringComparer.Ordinal)) { int d = w.Counter(RegionTrustKey(r.Id)); if (Math.Abs(d) >= 5) parts.Add($"{r.Name} believes the magistrates {(d < 0 ? "less" : "more")} than the Reach does ({RegionTrust(w, r.Id)})"); }
        return parts.Count == 0 ? "" : char.ToUpperInvariant(parts[0][0]) + string.Join("; ", parts)[1..] + ".";
    }
}
