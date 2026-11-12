using Example;
using Example.Client.Models;
using Example.Components;

using Microsoft.AspNetCore.Localization;

using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents(options => options.DetailedErrors = builder.Environment.IsDevelopment())
    .AddInteractiveWebAssemblyComponents();
builder.Services.AddMudServices();
builder.Services.AddSingleton<FileAwsIconPackage>();
builder.Services.AddSingleton<IAwsIconPackage>(static services => services.GetRequiredService<FileAwsIconPackage>());

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}

app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("ja-JP"),
    SupportedCultures = [CultureInfo.GetCultureInfo("ja-JP")],
    SupportedUICultures = [CultureInfo.GetCultureInfo("ja-JP")]
});
app.UseAntiforgery();

app.MapStaticAssets();
app.MapGet("/render-mode/{mode}", static (string mode, string? redirect, HttpContext context) =>
{
    context.Response.Cookies.Append(
        RenderModes.CookieName,
        mode is RenderModes.WebAssembly or RenderModes.Auto ? mode : RenderModes.Server,
        new CookieOptions { Path = "/", IsEssential = true, SameSite = SameSiteMode.Lax, MaxAge = TimeSpan.FromDays(365) });
    return Results.LocalRedirect(String.IsNullOrEmpty(redirect) || !redirect.StartsWith('/') || redirect.StartsWith("//", StringComparison.Ordinal) ? "/" : redirect);
});
app.MapGet("/aws-icons", static async (FileAwsIconPackage package, CancellationToken cancellationToken) => Results.Json(await package.ListAsync(cancellationToken)));
app.MapGet("/aws-icons/{**path}", static (string path, FileAwsIconPackage package) =>
    package.FindFile(path) is { } file ? Results.File(file, "image/svg+xml", lastModified: File.GetLastWriteTimeUtc(file)) : Results.NotFound());
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(Example.Client.Components.Routes).Assembly);

await app.RunAsync();
