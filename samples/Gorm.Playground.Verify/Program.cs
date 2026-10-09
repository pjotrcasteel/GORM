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


// A deterministic matrix of >100 distinct editable intents is consumed by Chromium for byte-for-byte SQL parity.
for (var index = 0; index < 120; index++)
{
    var intent = new PlaygroundQueryIntent(1, "ServiceNode", index % 2 == 0 ? "Active" : "Inactive",
        "Name", index * 53 % 10001, index % 100 + 1);
    var json = System.Text.Json.JsonSerializer.Serialize(intent, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
    var explained = PlaygroundQueryEngine.ExplainIntentJson(json);
    var encoded = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(explained));
    Console.WriteLine($"PASS INTENT {index}: {encoded}");
}

string[] invalidIntents =
[
    "{}", "[]", "{\"version\":2,\"root\":\"ServiceNode\",\"state\":\"Active\",\"orderBy\":\"Name\",\"skip\":0,\"take\":25}",
    "{\"version\":1,\"root\":\"FakeNode\",\"state\":\"Active\",\"orderBy\":\"Name\",\"skip\":0,\"take\":25}",
    "{\"version\":1,\"root\":\"ServiceNode\",\"state\":\"Bogus\",\"orderBy\":\"Name\",\"skip\":0,\"take\":25}",
    "{\"version\":1,\"root\":\"ServiceNode\",\"state\":\"Active\",\"orderBy\":\"State\",\"skip\":0,\"take\":25}",
    "{\"version\":1,\"root\":\"ServiceNode\",\"state\":\"Active\",\"orderBy\":\"Name\",\"skip\":-1,\"take\":25}",
    "{\"version\":1,\"root\":\"ServiceNode\",\"state\":\"Active\",\"orderBy\":\"Name\",\"skip\":0,\"take\":0}",
    "{\"version\":1,\"root\":\"ServiceNode\",\"state\":\"Active\",\"orderBy\":\"Name\",\"skip\":0,\"take\":101}",
    "{\"version\":1,\"root\":\"ServiceNode\",\"state\":\"Active\",\"orderBy\":\"Name\",\"skip\":0,\"take\":25,\"hack\":true}",
    "{\"version\":1,\"root\":\"ServiceNode\",\"state\":\"Active\",\"orderBy\":\"Name\",\"skip\":0,\"skip\":1,\"take\":25}",
    "{\"version\":\"1\",\"root\":\"ServiceNode\",\"state\":\"Active\",\"orderBy\":\"Name\",\"skip\":0,\"take\":25}"
];

foreach (var invalid in invalidIntents)
{
    try
    {
        _ = PlaygroundQueryEngine.ExplainIntentJson(invalid);
        throw new InvalidOperationException($"Invalid Playground query intent was unexpectedly accepted: {invalid}");
    }
    catch (NotSupportedException)
    {
        Console.WriteLine("PASS invalid intent rejected");
    }
}
