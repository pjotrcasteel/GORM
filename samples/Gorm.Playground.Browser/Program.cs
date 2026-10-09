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
}
