namespace Example.Endpoints;

using Example.Application;

public static class SettingEndpoints
{
    public static void MapSettingEndpoints(this WebApplication app)
    {
        app.MapGet("/render-mode/{mode}", HandleRenderMode);
    }

    private static IResult HandleRenderMode(string mode, string? redirect, HttpContext context)
    {
        SaveCookie(context, RenderModes.CookieName, mode == RenderModes.WebAssembly ? RenderModes.WebAssembly : RenderModes.Server);
        return RedirectBack(redirect);
    }

    private static void SaveCookie(HttpContext context, string name, string value) =>
        context.Response.Cookies.Append(name, value, new CookieOptions { Path = "/", MaxAge = TimeSpan.FromDays(30), IsEssential = true });

    private static IResult RedirectBack(string? redirect) =>
        Results.LocalRedirect(string.IsNullOrEmpty(redirect) || !redirect.StartsWith('/', StringComparison.Ordinal) || redirect.StartsWith("//", StringComparison.Ordinal) ? "/" : redirect);
}
