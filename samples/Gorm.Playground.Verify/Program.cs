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


// Real model-derived route coverage. Every incoming/outgoing shape is exercised with varied IDs.
var routes = PlaygroundQueryEngine.GetTraversalCatalog();
if (routes.Count != 6)
{
    throw new InvalidOperationException($"Expected six mapped Playground routes, got {routes.Count}.");
}

Console.WriteLine($"PASS TRAVERSAL CATALOG: {Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(PlaygroundQueryEngine.GetTraversalCatalogJson()))}");
for (var index = 0; index < 120; index++)
{
    var route = routes[index % routes.Count];
    var id = Guid.Parse($"00000000-0000-0000-0000-{index + 1:x12}");
    var intent = new PlaygroundTraversalIntent(2, route.Root, route.Direction, route.Edge, route.Target, id);
    var json = System.Text.Json.JsonSerializer.Serialize(intent, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
    var explained = PlaygroundQueryEngine.ExplainTraversalJson(json);
    if (!explained.Contains("MATCH", StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"Mapped traversal {index} failed to generate real SQL MATCH.");
    }

    Console.WriteLine($"PASS TRAVERSAL {index}: {Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(explained))}");
}

string[] invalidTraversals =
[
    "{}",
    "[]",
    """{"version":1,"root":"PersonNode","direction":"outgoing","edge":"WorksOnEdge","target":"ProjectNode","nodeId":"00000000-0000-0000-0000-000000000001"}""",
    """{"version":2,"root":"PersonNode","direction":"incoming","edge":"WorksOnEdge","target":"ProjectNode","nodeId":"00000000-0000-0000-0000-000000000001"}""",
    """{"version":2,"root":"PersonNode","direction":"outgoing","edge":"DependsOnEdge","target":"ProjectNode","nodeId":"00000000-0000-0000-0000-000000000001"}""",
    """{"version":2,"root":"FakeNode","direction":"outgoing","edge":"WorksOnEdge","target":"ProjectNode","nodeId":"00000000-0000-0000-0000-000000000001"}""",
    """{"version":2,"root":"PersonNode","direction":"outgoing","edge":"WorksOnEdge","target":"FakeNode","nodeId":"00000000-0000-0000-0000-000000000001"}""",
    """{"version":2,"root":"PersonNode","direction":"outgoing","edge":"WorksOnEdge","target":"ProjectNode","nodeId":"invalid"}""",
    """{"version":2,"root":"PersonNode","direction":"reverse","edge":"WorksOnEdge","target":"ProjectNode","nodeId":"00000000-0000-0000-0000-000000000001"}""",
    """{"version":2,"root":"PersonNode","direction":"outgoing","edge":"WorksOnEdge","target":"ProjectNode","nodeId":"00000000-0000-0000-0000-000000000001","eval":true}""",
    """{"version":2,"root":"PersonNode","direction":"outgoing","edge":"WorksOnEdge","edge":"DependsOnEdge","target":"ProjectNode","nodeId":"00000000-0000-0000-0000-000000000001"}""",
    """{"version":"2","root":"PersonNode","direction":"outgoing","edge":"WorksOnEdge","target":"ProjectNode","nodeId":"00000000-0000-0000-0000-000000000001"}"""
];

foreach (var invalid in invalidTraversals)
{
    try
    {
        _ = PlaygroundQueryEngine.ExplainTraversalJson(invalid);
        throw new InvalidOperationException($"An invalid traversal was unexpectedly accepted: {invalid}");
    }
    catch (NotSupportedException)
    {
        Console.WriteLine("PASS invalid traversal rejected");
    }
}


// v3: every bounded two-hop path must emit the same real SQL in native .NET and browser WASM.
var paths = PlaygroundQueryEngine.GetPathCatalog();
if (paths.Count != 4)
{
    throw new InvalidOperationException($"Expected four model-validated two-hop paths, found {paths.Count}.");
}

Console.WriteLine($"PASS PATH CATALOG: {Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(PlaygroundQueryEngine.GetPathCatalogJson()))}");

for (var index = 0; index < 120; index++)
{
    var route = paths[index % paths.Count];
    var id = Guid.Parse($"00000000-0000-0000-0000-{index + 31:x12}");
    var intent = new PlaygroundPathIntent(3, route.Root, id, route.Hops);
    var json = System.Text.Json.JsonSerializer.Serialize(intent,
        new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
    var result = PlaygroundQueryEngine.ExplainPathJson(json);
    using var parsed = System.Text.Json.JsonDocument.Parse(result);
    var sql = parsed.RootElement.GetProperty("sql").GetString() ?? "";
    if (!sql.Contains("MATCH", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException($"No graph MATCH in two-hop GORM path {index}.");
    }

    Console.WriteLine($"PASS PATH {index}: {Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(result))}");
}

string[] invalidPaths =
[
    "{}",
    "[]",
    """{"version":3,"root":"ServiceNode","nodeId":"00000000-0000-0000-0000-000000000001","hops":[]}""",
    """{"version":3,"root":"ServiceNode","nodeId":"00000000-0000-0000-0000-000000000001","hops":[{"direction":"outgoing","edge":"RoutesToEdge","target":"ServiceNode"}]}""",
    """{"version":3,"root":"ServiceNode","nodeId":"00000000-0000-0000-0000-000000000001","hops":[{"direction":"outgoing","edge":"RoutesToEdge","target":"ServiceNode"},{"direction":"incoming","edge":"DependsOnEdge","target":"DatabaseNode"}]}""",
    """{"version":3,"root":"ServiceNode","nodeId":"00000000-0000-0000-0000-000000000001","hops":[{"direction":"outgoing","edge":"DependsOnEdge","target":"DatabaseNode"},{"direction":"outgoing","edge":"RoutesToEdge","target":"ServiceNode"}]}""",
    """{"version":3,"root":"FakeNode","nodeId":"00000000-0000-0000-0000-000000000001","hops":[{"direction":"outgoing","edge":"RoutesToEdge","target":"ServiceNode"},{"direction":"outgoing","edge":"DependsOnEdge","target":"DatabaseNode"}]}""",
    """{"version":3,"root":"ServiceNode","nodeId":"bad","hops":[{"direction":"outgoing","edge":"RoutesToEdge","target":"ServiceNode"},{"direction":"outgoing","edge":"DependsOnEdge","target":"DatabaseNode"}]}""",
    """{"version":3,"root":"ServiceNode","nodeId":"00000000-0000-0000-0000-000000000001","hops":[{"direction":"outgoing","edge":"RoutesToEdge","target":"ServiceNode","eval":true},{"direction":"outgoing","edge":"DependsOnEdge","target":"DatabaseNode"}]}""",
    """{"version":3,"root":"ServiceNode","nodeId":"00000000-0000-0000-0000-000000000001","hops":[{"direction":"outgoing","edge":"RoutesToEdge","target":"ServiceNode"},{"direction":"outgoing","edge":"DependsOnEdge","edge":"RoutesToEdge","target":"DatabaseNode"}]}""",
    """{"version":"3","root":"ServiceNode","nodeId":"00000000-0000-0000-0000-000000000001","hops":[{"direction":"outgoing","edge":"RoutesToEdge","target":"ServiceNode"},{"direction":"outgoing","edge":"DependsOnEdge","target":"DatabaseNode"}]}""",
    """{"version":3,"root":"ServiceNode","nodeId":"00000000-0000-0000-0000-000000000001","hops":[{"direction":"reverse","edge":"RoutesToEdge","target":"ServiceNode"},{"direction":"outgoing","edge":"DependsOnEdge","target":"DatabaseNode"}]}"""
];

foreach (var invalid in invalidPaths)
{
    try
    {
        _ = PlaygroundQueryEngine.ExplainPathJson(invalid);
        throw new InvalidOperationException($"Unsupported two-hop GORM path was accepted: {invalid}");
    }
    catch (NotSupportedException)
    {
        Console.WriteLine("PASS invalid path rejected");
    }
}
