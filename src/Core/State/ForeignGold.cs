namespace Praetoria.Core.State;

/// <summary>Gold a nation beyond the Reach sends a senator before a war is voted on it, or on a nation it favours (docs/BEYOND.md,
/// "Foreign gold in the Senate"). It is the same gift that asks a favour that houses send one another, from outside: a sum to vote
/// against the war, to stay away from the vote, or (to the house that moved it) to withdraw the motion. Kept twenty years, so what
/// was taken can be found and charged before a praetor.</summary>
public sealed class GoldOffer
{
    public string Id { get; set; } = "";
    /// <summary>The nation that pays (the Free Cities), and the nation the war would be on: itself, or one it favours.</summary>
    public string NationId { get; set; } = "";
    public string WarOnId { get; set; } = "";
    /// <summary>Their names as they are written in a sentence ("the Free Cities of Thessar"), kept so the record reads the same whatever the content is later.</summary>
    public string NationName { get; set; } = "";
    public string WarOnName { get; set; } = "";
    /// <summary>The motion of war it was sent about.</summary>
    public string MotionId { get; set; } = "";
    /// <summary>The house it was sent to, and the head who was asked; once it is answered, the head who answered it (the seat may have passed
    /// between the asking and the answer: the gold, the record and the note in a life are theirs).</summary>
    public string HouseId { get; set; } = "";
    public string CharacterId { get; set; } = "";
    /// <summary>What it is for: "against" (vote against the war), "away" (stay away from the vote), "withdraw" (the mover takes the motion back).</summary>
    public string Ask { get; set; } = "";
    /// <summary>The sum, in the books' hundreds. Set aside from the nation's purse while the answer is waited for.</summary>
    public int Sum { get; set; }
    public int Turn { get; set; }
    /// <summary>"" while it waits for an answer; then "taken", "refused", "exposed" or "lapsed".</summary>
    public string Answer { get; set; } = "";
    /// <summary>How the house stood on the war before the gold (+1 for, 0 away, −1 against), and what its seats weighed: so that the count can
    /// be told with and without it.</summary>
    public int Before { get; set; }
    public int Seats { get; set; }
    /// <summary>The motion has been counted (or withdrawn) and what the gold did is settled.</summary>
    public bool Settled { get; set; }
    /// <summary>The war vote went the other way because of it: the motion failed, and would have carried had those who took it voted as they leaned; or the motion was withdrawn.</summary>
    public bool Turned { get; set; }
    /// <summary>One who took it to vote against the war, or stay away, and then voted for it.</summary>
    public bool Broken { get; set; }
}
