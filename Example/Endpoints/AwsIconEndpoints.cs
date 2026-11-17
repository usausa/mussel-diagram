namespace Example.Endpoints;

using Example.Application;

public static class AwsIconEndpoints
{
    public static void MapAwsIconEndpoints(this WebApplication app)
    {
        var group = app.MapGroup(ApiRoutes.AwsIcons);

        group.MapGet(string.Empty, HandleListAsync);
        group.MapGet("/{**path}", HandleFile);
    }

    private static async Task<IResult> HandleListAsync(FileAwsIconPackage package, CancellationToken cancellationToken) =>
        Results.Json(await package.ListAsync(cancellationToken));

    private static IResult HandleFile(string path, FileAwsIconPackage package) =>
        package.FindFile(path) is { } file ? Results.File(file, "image/svg+xml", lastModified: File.GetLastWriteTimeUtc(file)) : Results.NotFound();
}
