using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using Gorm.Playground.Core;

namespace Gorm.Playground.Browser;

[SupportedOSPlatform("browser")]
public static partial class Program
{
    public static void Main() { }

    [JSExport]
    public static string ExplainPreset(string preset) => PlaygroundQueryEngine.Explain(preset);

    [JSExport]
    public static string ExplainIntent(string json) => PlaygroundQueryEngine.ExplainIntentJson(json);

    [JSExport]
    public static string TraversalCatalog() => PlaygroundQueryEngine.GetTraversalCatalogJson();

    [JSExport]
    public static string ExplainTraversal(string json) => PlaygroundQueryEngine.ExplainTraversalJson(json);

    [JSExport]
    public static string PathCatalog() => PlaygroundQueryEngine.GetPathCatalogJson();

    [JSExport]
    public static string ExplainPath(string json) => PlaygroundQueryEngine.ExplainPathJson(json);

    [JSExport]
    public static string PredicateCatalog() => PlaygroundQueryEngine.GetPredicateCatalogJson();

    [JSExport]
    public static string ExplainPredicate(string json) => PlaygroundQueryEngine.ExplainPredicateJson(json);
}
