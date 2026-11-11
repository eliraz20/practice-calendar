using Microsoft.Data.Sqlite;

static class RecallReport
{
    public static void Run(string[] args)
    {
        if (args.Length != 3 && args.Length != 4)
            throw new FormatException("Usage: recall recall.db 2026-05-05 study.tsv");
        using var db = Open(args[1]);
        var today = Day(args[2]);
        using var tx = Begin(db);
        Sql(db, "CREATE TABLE IF NOT EXISTS card(id TEXT PRIMARY KEY,prompt TEXT NOT NULL,answer TEXT NOT NULL); CREATE TABLE IF NOT EXISTS review(card TEXT REFERENCES card(id),day TEXT,outcome TEXT,PRIMARY KEY(card,day))");
        void Record(string id, string date, string result)
        {
            Require(Day(date) <= today && result is "pass" or "fail", "Invalid date or outcome.");
            var existing = Query(db, "SELECT outcome FROM review WHERE card=$a AND day=$b", id, date);
            Require(existing.Count == 0 || existing[0][0] == result, "Conflicting result for card and date.");
            Sql(db, "INSERT INTO review VALUES($a,$b,$c) ON CONFLICT DO NOTHING", id, date, result);
        }

        bool study = args.Length == 4 && args[3] == "--study";
        if (args.Length == 4 && !study)
            foreach (var r in Rows(args[3]))
            {
                if (r is ["card", var id, var prompt, var answer])
                    InsertOnce(db, "card", ["id", "prompt", "answer"], [id, prompt, answer]);
                else if (r is ["review", var card, var date, var result])
                    Record(card, date, result);
                else
                    throw new FormatException("Expected card or review.");
            }

        tx.Commit();
        var due = new List<(string[] Card, DateOnly Due)>();
        foreach (var card in Query(db, "SELECT id,prompt,answer FROM card"))
        {
            var history = Query(db, "SELECT day,outcome FROM review WHERE card=$a AND day<=$b ORDER BY day", card[0], today.ToString("yyyy-MM-dd"));
            var streak = history.AsEnumerable().Reverse().TakeWhile(r => r[1] == "pass").Count();
            var next = history.Count == 0 ? today : Day(history[^1][0]).AddDays(1 << Math.Min(6, streak));
            if (next <= today)
                due.Add((card, next));
        }

        Console.WriteLine($"{due.Count} prompts due on {today:yyyy-MM-dd}");
        foreach (var item in due.OrderBy(x => x.Due).ThenBy(x => x.Card[0]))
        {
            Console.WriteLine($"{item.Card[0]} [{item.Due:yyyy-MM-dd}]: {item.Card[1]}");
            if (study)
            {
                Console.WriteLine("Press Enter to reveal, or q to finish.");
                var key = Console.ReadLine();
                if (key == null || key == "q")
                    break;
                Console.WriteLine(item.Card[2] + " — enter pass or fail");
                var result = Console.ReadLine();
                if (result == null)
                    break;
                Record(item.Card[0], today.ToString("yyyy-MM-dd"), result);
            }
        }
    }

    static List<string[]> Rows(string file, int width = 0, bool allowEmpty = false)
    {
        using var reader = file == "-" ? Console.In : File.OpenText(file);
        var rows = new List<string[]>();
        int line = 0;
        while (reader.ReadLine()is string s)
        {
            line++;
            if (string.IsNullOrWhiteSpace(s) || s.TrimStart().StartsWith("#"))
                continue;
            var cells = s.Split('\t', StringSplitOptions.TrimEntries);
            if ((width > 0 && cells.Length != width) || (!allowEmpty && cells.Any(string.IsNullOrEmpty)))
                throw new FormatException($"Invalid columns on line {line}.");
            rows.Add(cells);
        }

        if (rows.Count == 0)
            throw new FormatException("Empty input.");
        return rows;
    }

    static void Require(bool condition, string message)
    {
        if (!condition)
            throw new FormatException(message);
    }

    static DateOnly Day(string s) => DateOnly.ParseExact(s, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
    static SqliteTransaction? transaction;
    static SqliteConnection Open(string file)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = file, ForeignKeys = true }.ToString());
        connection.Open();
        return connection;
    }

    static SqliteTransaction Begin(SqliteConnection db) => transaction = db.BeginTransaction();
    static SqliteCommand Command(SqliteConnection db, string sql, string[] values)
    {
        var cmd = db.CreateCommand();
        if (transaction?.Connection != null)
            cmd.Transaction = transaction;
        cmd.CommandText = sql;
        for (int i = 0; i < values.Length; i++)
            cmd.Parameters.AddWithValue("$" + (char)(97 + i), values[i]);
        return cmd;
    }

    static void Sql(SqliteConnection db, string sql, params string[] values)
    {
        using var cmd = Command(db, sql, values);
        cmd.ExecuteNonQuery();
    }

    static List<string[]> Query(SqliteConnection db, string sql, params string[] values)
    {
        using var cmd = Command(db, sql, values);
        using var reader = cmd.ExecuteReader();
        var records = new List<string[]>();
        while (reader.Read())
            records.Add(Enumerable.Range(0, reader.FieldCount).Select(i => Convert.ToString(reader.GetValue(i), System.Globalization.CultureInfo.InvariantCulture) ?? "").ToArray());
        return records;
    }

    static void InsertOnce(SqliteConnection db, string table, string[] columns, string[] values)
    {
        var old = Query(db, $"SELECT {string.Join(",", columns)} FROM {table} WHERE {columns[0]}=$a", values[0]);
        if (old.Count != 0)
        {
            Require(old[0].SequenceEqual(values), "Conflicting record: " + values[0]);
            return;
        }

        var slots = Enumerable.Range(0, values.Length).Select(i => "$" + (char)(97 + i));
        Sql(db, $"INSERT INTO {table} ({string.Join(",", columns)}) VALUES ({string.Join(",", slots)})", values);
    }
}
