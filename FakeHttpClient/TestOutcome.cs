using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FakeHttpClient;

/// <summary>
/// The result of one test, for the summary table and the machine-readable results file.
/// </summary>
public record TestOutcome(
    string Name,
    string Url,
    string Mode,
    [property: JsonConverter(typeof(JsonStringEnumConverter<TestResult>))] TestResult Result,
    long ElapsedMs,
    long BytesReceived,
    int? Status,
    long? BodyLength,
    string? BodySha256,
    bool? Complete,
    string? Error)
{
    public bool Passed => Result == TestResult.Success;
}

/// <summary>
/// A whole run: what the results file contains.
/// </summary>
public record RunSummary(
    string Version,
    DateTimeOffset Started,
    long ElapsedMs,
    int Total,
    int Passed,
    int Failed,
    IReadOnlyList<TestOutcome> Results);

public static class ResultsWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        // a plain data file, never embedded in HTML: keep "+" and friends readable
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static RunSummary Summarize(DateTimeOffset started, long elapsedMs, IReadOnlyList<TestOutcome> outcomes)
    {
        var passed = outcomes.Count(o => o.Passed);
        return new RunSummary(Version, started, elapsedMs, outcomes.Count, passed, outcomes.Count - passed, outcomes);
    }

    public static string Version =>
        typeof(ResultsWriter).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";

    /// <summary>
    /// Print a fixed-width table of results, one row per test, and a pass count.
    /// </summary>
    public static void WriteTable(TextWriter writer, RunSummary run)
    {
        var rows = run.Results.Select(o => new[]
        {
            o.Name,
            o.Passed ? "pass" : $"FAIL ({o.Result})",
            $"{o.ElapsedMs}ms",
            o.Status?.ToString() ?? "-",
            o.BodyLength?.ToString() ?? "-",
            o.Error ?? "",
        }).ToList();
        var header = new[] { "Test", "Result", "Time", "Status", "Body bytes", "Error" };
        var widths = header.Select((h, i) => Math.Max(h.Length, rows.Select(r => r[i].Length).DefaultIfEmpty(0).Max())).ToArray();

        string Format(string[] cells) =>
            string.Join("  ", cells.Select((c, i) => i is 2 or 3 or 4 ? c.PadLeft(widths[i]) : c.PadRight(widths[i]))).TrimEnd();

        writer.WriteLine();
        writer.WriteLine(Format(header));
        writer.WriteLine(Format(widths.Select(w => new string('-', w)).ToArray()));
        foreach (var row in rows)
        {
            writer.WriteLine(Format(row));
        }
        writer.WriteLine($"{run.Passed}/{run.Total} passed");
    }

    /// <summary>
    /// Write the results as CSV if the path ends in .csv, otherwise as JSON.
    /// </summary>
    public static void WriteFile(string path, RunSummary run)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
        var contents = path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) ? ToCsv(run) : JsonSerializer.Serialize(run, JsonOptions);
        File.WriteAllText(path, contents, new UTF8Encoding(false));
    }

    private static string ToCsv(RunSummary run)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Name,Url,Mode,Result,Passed,ElapsedMs,BytesReceived,Status,BodyLength,BodySha256,Complete,Error");
        foreach (var o in run.Results)
        {
            sb.AppendLine(string.Join(",",
                Csv(o.Name), Csv(o.Url), Csv(o.Mode), o.Result, o.Passed, o.ElapsedMs, o.BytesReceived,
                o.Status, o.BodyLength, Csv(o.BodySha256), o.Complete, Csv(o.Error)));
        }
        return sb.ToString();
    }

    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }
        return value.IndexOfAny(['"', ',', '\n', '\r']) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
    }
}
