using KestrelHelpdesk.Core.Authorization;
using KestrelHelpdesk.Core.Interfaces;
using KestrelHelpdesk.Infrastructure.Data;
using KestrelHelpdesk.Infrastructure.Identity;
using KestrelHelpdesk.Infrastructure.Repositories;
using KestrelHelpdesk.Core.Services;
using KestrelHelpdesk.Web.Components;
using KestrelHelpdesk.Web.Components.Account;
using KestrelHelpdesk.Web.Security;
using KestrelHelpdesk.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options =>
    {
        options.DetailedErrors = builder.Environment.IsDevelopment();
    });

builder.Services.AddMudServices();

builder.Services.AddScoped<ITicketService, TicketService>();
builder.Services.AddScoped<ITicketRepository, TicketRepository>();
builder.Services.AddScoped<ITicketAttachmentRepository, TicketAttachmentRepository>();
builder.Services.AddScoped<IExcelTicketImportService, ExcelTicketImportService>();
builder.Services.AddScoped<ITicketPdfService, TicketPdfService>();
builder.Services.AddScoped<ITicketAttachmentService, TicketAttachmentService>();
builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();
builder.Services.AddScoped<ICurrentUserContext, IdentityCurrentUserContext>();
builder.Services.AddScoped<IUserDirectory, IdentityUserDirectory>();
builder.Services.AddScoped<BrowserTimeZoneService>();
builder.Services.AddScoped<CircuitHandler, LoggingCircuitHandler>();
builder.Services.AddSingleton<ITicketEventService, TicketEventService>();

builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>();

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = IdentityConstants.ApplicationScheme;
    options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
})
.AddIdentityCookies();

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
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();
builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await IdentitySeeder.SeedAsync(scope.ServiceProvider);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapHealthChecks("/health");
app.MapGet("/tickets/{ticketId:int}/attachments/{attachmentId:int}", async (
    int ticketId,
    int attachmentId,
    ITicketAttachmentService attachmentService,
    CancellationToken cancellationToken) =>
{
    try
    {
        var download = await attachmentService.OpenDownloadAsync(ticketId, attachmentId, cancellationToken);
        return (IResult)Results.File(download.Content, download.ContentType, download.FileName);
    }
    catch (UnauthorizedAccessException)
    {
        return Results.Forbid();
    }
    catch (KeyNotFoundException)
    {
        return Results.NotFound();
    }
    catch (FileNotFoundException)
    {
        return Results.NotFound();
    }
}).RequireAuthorization();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.MapAdditionalIdentityEndpoints();

app.Run();
