using System.Runtime.InteropServices.JavaScript;
using Gorm.Playground.Core;

namespace Gorm.Playground.Browser;

public static partial class Program
{
    public static void Main() { }

    [JSExport]
    public static string ExplainPreset(string preset) => PlaygroundQueryEngine.Explain(preset);
}
