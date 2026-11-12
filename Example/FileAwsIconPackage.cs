namespace Example;

using Example.Client.Models;

public sealed class FileAwsIconPackage(IConfiguration configuration) : IAwsIconPackage
{
    private readonly string folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configuration["AwsIconFolder"] ?? "AwsIcons")) + Path.DirectorySeparatorChar;

    public Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>>(Directory.Exists(folder)
            ? [.. Directory.EnumerateFiles(folder, "*.svg", SearchOption.AllDirectories).Select(x => Path.GetRelativePath(folder, x).Replace('\\', '/'))]
            : []);

    public async Task<string?> ReadAsync(string path, CancellationToken cancellationToken) =>
        FindFile(path) is { } file ? await File.ReadAllTextAsync(file, cancellationToken) : null;

    public string? FindFile(string path)
    {
        var file = Path.GetFullPath(Path.Combine(folder, path));
        return file.StartsWith(folder, StringComparison.OrdinalIgnoreCase) && file.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) && File.Exists(file) ? file : null;
    }
}
