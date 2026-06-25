using EtoroPortfolioHub.Components;
using EtoroPortfolioHub.Data;
using EtoroPortfolioHub.Models;
using EtoroPortfolioHub.Services;
using EtoroPortfolioHub.State;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<EtoroOptions>(
    builder.Configuration.GetSection(EtoroOptions.SectionName));

builder.Services.AddSingleton<PortfolioState>();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("AppDb")));

builder.Services.AddDefaultIdentity<IdentityUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.User.RequireUniqueEmail = true;

    options.Password.RequiredLength = 8;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<CurrentUserService>();
builder.Services.AddScoped<PortfolioTargetService>();
builder.Services.AddScoped<DividendCalendarService>();

builder.Services.AddHttpClient<EtoroRestClient>();

builder.Services.AddHostedService<PortfolioRefreshService>();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddRazorPages();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}


app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();