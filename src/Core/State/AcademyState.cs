namespace Praetoria.Core.State;

/// <summary>
/// The Academy year (docs/ACADEMY.md; the owner's direction): a prologue apart from the world. A cadet enters at sixteen, spends a year
/// of four terms among the cohort, and graduates into the Reach at seventeen. While <see cref="Active"/> the world does not turn: no
/// Senate, no fleets, no economy, no world commands — only the Academy's own events, studies and lessons. The register is fixed at
/// entry: the Republic's Academy (academics, friendly competition, war games, nobody hurt) or the Imperium's Institute (the Standards
/// at war, and cadets who do not come back). Serialised with the World.
/// </summary>
public sealed class AcademyState
{
    public bool Active { get; set; }
    /// <summary>"republic" or "imperium", fixed when the cadet enters.</summary>
    public string Register { get; set; } = "republic";
    /// <summary>0 before the first term opens; 1..<see cref="Terms"/> in the year.</summary>
    public int Term { get; set; }
    public int Terms { get; set; } = 4;
    /// <summary>The subject the cadet is reading this term (a subject id), or "" for the house's habit.</summary>
    public string Focus { get; set; } = "";
    /// <summary>Marks earned by subject across the year.</summary>
    public Dictionary<string, int> Marks { get; set; } = new();
    /// <summary>The cadets of the year: real people of the Reach's houses, fixed at muster.</summary>
    public List<string> Cohort { get; set; } = new();
    /// <summary>Each classmate's running score for the year's order of merit.</summary>
    public Dictionary<string, int> Scores { get; set; } = new();
    public string InstructorId { get; set; } = "";
    /// <summary>"full" (every lesson, for a first run), "brief" (the state of the Reach only) or "" (not yet asked).</summary>
    public string Tutorial { get; set; } = "";
    /// <summary>The service the cadet has asked to be commissioned into (a career track id), or "" to let the board decide.</summary>
    public string Service { get; set; } = "";

    // ---- the Institute (imperium register) ----
    /// <summary>The cadet's Standard (a standard id), or "" in the Republic.</summary>
    public string Standard { get; set; } = "";
    /// <summary>Cadet id → standard id.</summary>
    public Dictionary<string, string> StandardOf { get; set; } = new();
    /// <summary>Standard id → strength in the war of the Standards.</summary>
    public Dictionary<string, int> StandardStrength { get; set; } = new();
    /// <summary>Standards whose banners have been taken, in order.</summary>
    public List<string> Fallen { get; set; } = new();

    // ---- the outcome ----
    public bool Graduated { get; set; }
    public int Place { get; set; }
    public int Of { get; set; }
    public bool IsImperium => Register == "imperium";
}
