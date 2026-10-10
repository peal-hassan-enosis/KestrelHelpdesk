using KestrelHelpdesk.Core.Interfaces;
using KestrelHelpdesk.Infrastructure.Data;
using KestrelHelpdesk.Infrastructure.Repositories;
using KestrelHelpdesk.Core.Services;
using KestrelHelpdesk.Web.State;
using Microsoft.EntityFrameworkCore;
using KestrelHelpdesk.Web.Components;
using KestrelHelpdesk.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using KestrelHelpdesk.Web.Components.Account;
using Microsoft.AspNetCore.Components.Authorization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Register Scoped Services (per Circuit / Tab session)
builder.Services.AddScoped<ITicketService, TicketService>();
builder.Services.AddScoped<ITicketRepository, TicketRepository>();
builder.Services.AddScoped<CurrentUserState>();

// Register EF Core DbContextFactory for Blazor Server
builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// Identity authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = IdentityConstants.ApplicationScheme;
    options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
})
    .AddIdentityCookies();

// Identity + roles + EF stores
builder.Services.AddIdentityCore<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = true;
    options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
})
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddAuthorization();

builder.Services.AddCascadingAuthenticationState();

builder.Services.AddScoped<IdentityRedirectManager>();

builder.Services.AddScoped<
    AuthenticationStateProvider,
    IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddSingleton<
    IEmailSender<ApplicationUser>,
    IdentityNoOpEmailSender>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await IdentitySeeder.SeedAsync(scope.ServiceProvider);
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute(
    "/not-found",
    createScopeForStatusCodePages: true);

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapAdditionalIdentityEndpoints();

app.Run();