using Example.Application;

//--------------------------------------------------------------------------------
// Configure builder
//--------------------------------------------------------------------------------
var builder = WebApplication.CreateBuilder(args);

// API
builder.ConfigureApi();

// Blazor
builder.ConfigureBlazor();

// AWS icons
builder.ConfigureAwsIcons();

//--------------------------------------------------------------------------------
// Configure the HTTP request pipeline
//--------------------------------------------------------------------------------
var app = builder.Build();

// Localization
app.UseLocalization();

// Error handler
app.UseErrorHandler();

// Routing
app.UseRouting();

// Antiforgery
app.UseAntiforgery();

// End point
app.MapEndpoints();

// Run
await app.RunAsync();
