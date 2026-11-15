namespace Example.Client.Components.Layout;

public sealed partial class RenderModeSwitch
{
#if EXAMPLE_STANDALONE
    private static bool Visible => false;
#else
    private static bool Visible => true;
#endif
}
