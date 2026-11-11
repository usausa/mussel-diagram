namespace MusselDiagram;

internal static class TestPaths
{
    public static string Root { get; } = FindRoot();

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while ((directory is not null) && !File.Exists(Path.Combine(directory.FullName, "MusselDiagram.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("The solution folder was not found.");
    }
}
