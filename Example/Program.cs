using Example;
using Example.Components;

using Microsoft.AspNetCore.Localization;

using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents(options => options.DetailedErrors = builder.Environment.IsDevelopment())
    .AddInteractiveWebAssemblyComponents();
builder.Services.AddMudServices();

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
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(Example.Client.Components.Routes).Assembly);

await app.RunAsync();
