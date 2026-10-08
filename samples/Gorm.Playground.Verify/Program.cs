using Gorm.Playground.Core;

foreach (var key in PlaygroundQueryEngine.SupportedExamples)
{
    var sql = PlaygroundQueryEngine.Explain(key);
    if (string.IsNullOrWhiteSpace(sql) || !sql.Contains("SELECT", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException($"Real GORM Explain() returned no SQL SELECT for '{key}'.");
    }

    if (key is "outgoing" or "incoming" or "chained" && !sql.Contains("MATCH", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException($"Real GORM Explain() returned no graph MATCH for '{key}'.");
    }

    Console.WriteLine($"PASS {key}: {sql.ReplaceLineEndings(" ")}");
}

try
{
    _ = PlaygroundQueryEngine.Explain("temporal");
    throw new InvalidOperationException("Temporal queries must not be marked authoritative before GORM supports their mapped SQL path.");
}
catch (NotSupportedException)
{
    Console.WriteLine("PASS unsupported temporal example fails closed");
}
