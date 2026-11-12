namespace MusselDiagram;

using System.Runtime.CompilerServices;

internal static class TestCulture
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        var english = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.DefaultThreadCurrentCulture = english;
        CultureInfo.DefaultThreadCurrentUICulture = english;
        CultureInfo.CurrentCulture = english;
        CultureInfo.CurrentUICulture = english;
    }
}
