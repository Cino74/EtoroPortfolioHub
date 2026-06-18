using EtoroPortfolioHub.Components;
using EtoroPortfolioHub.Data;
using EtoroPortfolioHub.Models;
using EtoroPortfolioHub.Services;
using EtoroPortfolioHub.State;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<EtoroOptions>(
    builder.Configuration.GetSection(EtoroOptions.SectionName));

// Stato applicativo
builder.Services.AddSingleton<PortfolioState>();

// Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("AppDb")));

// Servizi applicativi
builder.Services.AddScoped<PortfolioTargetService>();

// Client eToro
builder.Services.AddHttpClient<EtoroRestClient>();

// Background services
builder.Services.AddHostedService<PortfolioRefreshService>();

builder.Services.AddScoped<DividendCalendarService>();

var runLegacyTargetMigration =
    builder.Configuration.GetValue<bool>("DataMigration:RunLegacyPortfolioTargetMigrationOnStartup");

if (runLegacyTargetMigration)
{
    builder.Services.AddHostedService<LegacyPortfolioTargetsMigrationHostedService>();
}

// Razor Components / Interactive Server
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
