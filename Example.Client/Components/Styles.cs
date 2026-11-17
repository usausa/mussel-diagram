namespace Example.Client.Components;

using MudBlazor;

public static class Styles
{
    public static MudTheme Theme { get; } = new()
    {
        PaletteDark = new PaletteDark
        {
            Background = "#1F1F1D",
            Surface = "#262624"
        },

        Typography = new Typography
        {
            Body1 = new Body1Typography { FontSize = ".875rem" },
            Subtitle1 = new Subtitle1Typography { FontSize = ".875rem" },

            Button = new ButtonTypography { TextTransform = "none" }
        }
    };
}
