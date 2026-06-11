using EtoroPortfolioHub.Components;
using EtoroPortfolioHub.Models;
using EtoroPortfolioHub.Services;
using EtoroPortfolioHub.State;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<EtoroOptions>(
    builder.Configuration.GetSection(EtoroOptions.SectionName));

builder.Services.AddSingleton<PortfolioState>();

builder.Services.AddHttpClient<EtoroRestClient>();

builder.Services.AddHostedService<PortfolioRefreshService>();

builder.Services.AddSingleton<PortfolioTargetService>();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();