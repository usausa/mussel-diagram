namespace MusselDiagram;

using System.Reflection;

using Microsoft.JSInterop;

public sealed class ScriptBridgeTests
{
    [Fact]
    public void ScriptEntryPointsStayOutOfThePublicApi()
    {
        var entryPoints = typeof(MusselDiagramView).Assembly.GetExportedTypes()
            .SelectMany(static x => x.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(static x => x.IsDefined(typeof(JSInvokableAttribute), false))
            .Select(static x => $"{x.DeclaringType!.Name}.{x.Name}");

        Assert.Empty(entryPoints);
    }
}
