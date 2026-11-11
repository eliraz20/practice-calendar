using System.Globalization;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
if (args.Length > 0 && args[0] == "recall")
{
    try { RecallReport.Run(args); return 0; }
    catch (Exception e) when (e is FormatException or IOException or ArgumentException or OverflowException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException)
    { Console.Error.WriteLine(e.Message); return 1; }
}

if (args.Length != 1 || args[0] == "--help")
{
    Console.WriteLine("Usage: dotnet run -- recall recall.db 2026-05-05 study.tsv  |  original format: <input-file|->");
    return args.Length == 1 ? 0 : 2;
}

try
{
    string[] lines = args[0] == "-" ? Console.In.ReadToEnd().ReplaceLineEndings("\n").TrimEnd('\n').Split('\n') : File.ReadAllLines(args[0]);
    Check(lines.Length >= 1 && lines.Any(line => !string.IsNullOrWhiteSpace(line)), "Input is empty or missing records.");
    var sessions = lines.Select(s => Fields(s, 3)).Select(f => (Date: Day(f[0]), Skill: f[1], Minutes: Whole(f[2], 1)));
    foreach (var group in sessions.GroupBy(s => s.Skill).OrderBy(g => g.Key))
    {
        int last = -2, run = 0, longest = 0;
        foreach (var day in group.Select(s => s.Date.DayNumber).Distinct().Order())
        {
            run = day == last + 1 ? run + 1 : 1;
            longest = Math.Max(longest, run);
            last = day;
        }

        Console.WriteLine($"{group.Key}: {group.Sum(s => s.Minutes)} minutes, longest {longest}-day streak");
    }

    return 0;
}
catch (Exception e)when (e is IOException or FormatException or OverflowException or ArgumentException or UnauthorizedAccessException or DivideByZeroException)
{
    Console.Error.WriteLine(e.Message);
    return 1;
}

static void Check(bool valid, string message)
{
    if (!valid)
        throw new FormatException(message);
}

static string[] Fields(string line, int count)
{
    var fields = line.Split('\t', StringSplitOptions.TrimEntries);
    Check(fields.Length == count && fields.All(s => s.Length > 0), $"Expected {count} tab-separated values in '{line}'.");
    return fields;
}

static long Whole(string value, long minimum)
{
    var number = long.Parse(value, CultureInfo.InvariantCulture);
    Check(number >= minimum, $"Expected an integer of at least {minimum}.");
    return number;
}

static DateOnly Day(string value) => DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture);
