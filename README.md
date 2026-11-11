# Practice Calendar

Build the next practice session from recall history, carrying overdue prompts forward.

## Try it

From this folder with the .NET 10 SDK installed:

```sh
dotnet run -- recall recall.db 2026-05-05 study.tsv
```

Rows: card / ID / prompt / answer; review / ID / date / pass or fail. One review per card per date. Recall DB stores the deck and outcomes. A failure resets the delay to one day; consecutive successes use 2, 4, 8, … days up to 64. Omit the TSV to print due prompts. Append --study in its place to reveal answers and record a session interactively.

The included study.tsv uses tab-separated fields. Comment lines start with `#`.

The database is created on first use. Use a different database filename for another class or team.

The original exercise remains available with `dotnet run -- example.tsv`.

## When to use it

A learner returning after missed practice needs a session built from their own prompts and recall history.

Example result:

```text
2 prompts due on 2026-05-05
A [2026-05-04]: Say good morning in Italian
B [2026-05-05]: Say good night
```

Related course or field workflow: [University of Minnesota on spaced recall](https://cei.umn.edu/teaching-resources/leveraging-learning-sciences/spaced-and-interleaved-practice-improves-recall).
