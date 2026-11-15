namespace Example.Application;

using Example.Client.Components;
using Example.Client.Models;
using Example.Components;
using Example.Endpoints;

using Microsoft.AspNetCore.Localization;

using MudBlazor.Services;

public static class ApplicationExtensions
{
    //--------------------------------------------------------------------------------
    // API
    //--------------------------------------------------------------------------------

    public static IHostApplicationBuilder ConfigureApi(this IHostApplicationBuilder builder)
    {
        builder.Services.AddProblemDetails();

        return builder;
    }

    //--------------------------------------------------------------------------------
    // Blazor
    //--------------------------------------------------------------------------------

    public static IHostApplicationBuilder ConfigureBlazor(this IHostApplicationBuilder builder)
    {
        // Razor components
        builder.Services
            .AddRazorComponents()
            .AddInteractiveServerComponents(options =>
            {
                options.DetailedErrors = builder.Environment.IsDevelopment();
            })
            .AddInteractiveWebAssemblyComponents();

        // MudBlazor
        builder.Services.AddMudServices();

        return builder;
    }

    //--------------------------------------------------------------------------------
    // AWS icons
    //--------------------------------------------------------------------------------

    public static IHostApplicationBuilder ConfigureAwsIcons(this IHostApplicationBuilder builder)
    {
        builder.Services.AddSingleton<FileAwsIconPackage>();
        builder.Services.AddSingleton<IAwsIconPackage>(static services => services.GetRequiredService<FileAwsIconPackage>());

        return builder;
    }

    //--------------------------------------------------------------------------------
    // Localization
    //--------------------------------------------------------------------------------

    public static WebApplication UseLocalization(this WebApplication app)
    {
        app.UseRequestLocalization(new RequestLocalizationOptions
        {
            DefaultRequestCulture = new RequestCulture("ja-JP"),
            SupportedCultures = [CultureInfo.GetCultureInfo("ja-JP")],
            SupportedUICultures = [CultureInfo.GetCultureInfo("ja-JP")]
        });

        return app;
    }

    //--------------------------------------------------------------------------------
    // Error handler
    //--------------------------------------------------------------------------------

    public static WebApplication UseErrorHandler(this WebApplication app)
    {
        // API: ProblemDetails
        app.UseWhen(
            static context => context.Request.Path.StartsWithSegments(ApiRoutes.Prefix, StringComparison.OrdinalIgnoreCase),
            static b =>
            {
                b.UseExceptionHandler();
                b.UseStatusCodePages();
            });

        // Page: error page and not found page
        app.UseWhen(
            static context => !context.Request.Path.StartsWithSegments(ApiRoutes.Prefix, StringComparison.OrdinalIgnoreCase),
            static b =>
            {
                b.UseExceptionHandler("/error", createScopeForErrors: true);
                b.UseWhen(
                    static context => HttpMethods.IsGet(context.Request.Method),
                    static g => g.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true));
            });

        return app;
    }

    //--------------------------------------------------------------------------------
    // End point
    //--------------------------------------------------------------------------------

    public static WebApplication MapEndpoints(this WebApplication app)
    {
        // Static assets
        app.MapStaticAssets();

        // Blazor
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode()
            .AddInteractiveWebAssemblyRenderMode()
            .AddAdditionalAssemblies(typeof(Routes).Assembly);

        // Settings
        app.MapSettingEndpoints();

        // API
        app.MapAwsIconEndpoints();

        return app;
    }
}
