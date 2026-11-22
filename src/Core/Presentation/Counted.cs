namespace Praetoria.Core;

/// <summary>A number and what it counts, said as a person would say it: "1 hull", "4 hulls", and never the plural in brackets. Everything the game
/// writes for the player that counts something goes through here (docs/WEB_CLIENT.md, "Words"). A method for each thing counted, so
/// that a line built inside a quoted attribute of a page needs no quotation marks of its own.</summary>
public static class Counted
{
    public static string Of(long n, string one) => n == 1 ? $"1 {one}" : $"{n} {one}s";
    /// <summary>With a word before the thing: "3 more years", "1 office hull".</summary>
    public static string Of(long n, string before, string one) => n == 1 ? $"1 {before} {one}" : $"{n} {before} {one}s";
    public static string Agents(long n) => Of(n, "agent");
    public static string Buildings(long n) => Of(n, "building");
    public static string Claims(long n) => Of(n, "claim");
    public static string Clients(long n) => Of(n, "client");
    public static string Errors(long n) => Of(n, "error");
    public static string Failures(long n) => Of(n, "failure");
    public static string Fleets(long n) => Of(n, "fleet");
    public static string Holdings(long n) => Of(n, "holding");
    public static string Houses(long n) => Of(n, "house");
    public static string Hulls(long n) => Of(n, "hull");
    public static string Jumps(long n) => Of(n, "jump");
    public static string Lanes(long n) => Of(n, "lane");
    public static string Laws(long n) => Of(n, "law");
    public static string Matters(long n) => Of(n, "matter");
    public static string Motions(long n) => Of(n, "motion");
    public static string Names(long n) => Of(n, "name");
    public static string Regions(long n) => Of(n, "region");
    public static string Reports(long n) => Of(n, "report");
    public static string Scenarios(long n) => Of(n, "scenario");
    public static string Seats(long n) => Of(n, "seat");
    public static string Seeds(long n) => Of(n, "seed");
    public static string Sites(long n) => Of(n, "site");
    public static string Squadrons(long n) => Of(n, "squadron");
    public static string Starts(long n) => Of(n, "start");
    public static string Stations(long n) => Of(n, "station");
    public static string Successions(long n) => Of(n, "succession");
    public static string Systems(long n) => Of(n, "system");
    public static string Takeovers(long n) => Of(n, "takeover");
    public static string Terms(long n) => Of(n, "term");
    public static string Things(long n) => Of(n, "thing");
    public static string Times(long n) => Of(n, "time");
    public static string Turns(long n) => Of(n, "turn");
    public static string Triumphs(long n) => Of(n, "triumph");
    public static string Ventures(long n) => Of(n, "venture");
    public static string Wars(long n) => Of(n, "war");
    public static string Warnings(long n) => Of(n, "warning");
    public static string Worlds(long n) => Of(n, "world");
    public static string Years(long n) => Of(n, "year");
    // the verb that follows a count agrees with it: "1 seat is counted", "4 seats are counted"
    public static string Are(long n) => n == 1 ? "is" : "are";
    public static string Were(long n) => n == 1 ? "was" : "were";
    public static string Come(long n) => n == 1 ? "comes" : "come";
    public static string Go(long n) => n == 1 ? "goes" : "go";
    public static string Sail(long n) => n == 1 ? "sails" : "sail";
    public static string Remain(long n) => n == 1 ? "remains" : "remain";
    public static string MoreYears(long n) => Of(n, "more", "year");
    public static string OfficeHulls(long n) => Of(n, "office", "hull");
    public static string OtherWars(long n) => Of(n, "other", "war");
    public static string LesserThings(long n) => Of(n, "lesser", "thing");
    public static string TreasuryActions(long n) => Of(n, "treasury", "action");
    public static string TokenIssues(long n) => Of(n, "token", "issue");
}
