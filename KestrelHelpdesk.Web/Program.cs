using System.Globalization;
using System.Security.Claims;
using KestrelHelpdesk.Core.Authorization;
using KestrelHelpdesk.Core.Interfaces;
using KestrelHelpdesk.Infrastructure.Data;
using KestrelHelpdesk.Infrastructure.FileStorage;
using KestrelHelpdesk.Infrastructure.Identity;
using KestrelHelpdesk.Infrastructure.ImageProcessing;
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
builder.Services.AddScoped<ITicketQueryService, TicketQueryService>();
builder.Services.AddScoped<ITicketAssignmentService, TicketAssignmentService>();
builder.Services.AddScoped<ITicketCommentService, TicketCommentService>();
builder.Services.AddScoped<ITicketImportService, TicketImportService>();
builder.Services.AddScoped<ITicketRepository, TicketRepository>();
builder.Services.AddScoped<ITicketAttachmentRepository, TicketAttachmentRepository>();
builder.Services.AddScoped<IExcelTicketImportService, ExcelTicketImportService>();
builder.Services.AddScoped<ITicketPdfService, TicketPdfService>();
builder.Services.AddScoped<ITicketAttachmentService, TicketAttachmentService>();
builder.Services.AddScoped<ITicketAttachmentDownloadService, TicketAttachmentDownloadService>();
builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();
builder.Services.AddScoped<IImageAttachmentProcessor, SkiaSharpImageAttachmentProcessor>();
builder.Services.AddScoped<ICurrentUserContext, IdentityCurrentUserContext>();
builder.Services.AddScoped<IUserDirectory, IdentityUserDirectory>();
builder.Services.AddScoped<IRoleManagementService, IdentityRoleManagementService>();
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
    options.SignIn.RequireConfirmedAccount = false;
    options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddSignInManager();

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await IdentitySeeder.SeedAsync(scope.ServiceProvider);

    var userDirectory = scope.ServiceProvider.GetRequiredService<IUserDirectory>();
    var ticketRepository = scope.ServiceProvider.GetRequiredService<ITicketRepository>();
    var agents = await userDirectory.GetAgentsAsync();
    await ticketRepository.UnassignTicketsNotAssignedToAgentsAsync(
        agents.Select(agent => agent.Id));
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

app.MapGet("/tickets/export/pdf", async (
    HttpContext httpContext,
    string? status,
    string? search,
    TicketScope? scope,
    bool notAssignedOnly,
    string? sortBy,
    bool sortDescending,
    string? timeZoneId,
    ITicketRepository ticketRepository,
    IUserDirectory userDirectory,
    ITicketPdfService ticketPdfService,
    CancellationToken cancellationToken) =>
{
    var user = httpContext.User;
    var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrWhiteSpace(userId))
        return Results.Unauthorized();

    var requestedScope = scope ?? TicketScope.EmployeeOwn;
    var effectiveScope = user.IsInRole(AppRoles.Manager)
        ? TicketScope.ManagerAll
        : user.IsInRole(AppRoles.Employee)
            ? TicketScope.EmployeeOwn
            : requestedScope == TicketScope.AgentCreated
                ? TicketScope.AgentCreated
                : TicketScope.AgentAssigned;

    var tickets = await ticketRepository.GetTicketsForExportAsync(
        status,
        search,
        sortBy,
        !sortDescending,
        effectiveScope,
        userId,
        user.IsInRole(AppRoles.Manager) && notAssignedOnly);

    var agents = await userDirectory.GetAgentsAsync();
    var agentNames = agents.ToDictionary(agent => agent.Id, agent => agent.DisplayName, StringComparer.Ordinal);
    var timeZone = TimeZoneInfo.Utc;

    if (!string.IsNullOrWhiteSpace(timeZoneId) && TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var requestedTimeZone))
        timeZone = requestedTimeZone;

    var rows = tickets.Select(ticket => new TicketPdfRow(
        ticket.Id,
        ticket.Title,
        ticket.Status == TicketStatus.InProgress ? "In Progress" : ticket.Status.ToString(),
        ticket.Priority.ToString(),
        string.IsNullOrWhiteSpace(ticket.AssignedToUserId)
            ? "-"
            : agentNames.GetValueOrDefault(ticket.AssignedToUserId) ?? "-",
        TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(ticket.CreatedAt, DateTimeKind.Utc),
                timeZone)
            .ToString("g", CultureInfo.CurrentCulture)))
        .ToList();

    var pdfBytes = ticketPdfService.Generate(rows, "Ticket Board - All Tickets", cancellationToken);
    var fileName = $"tickets-all-{DateTime.Now:yyyyMMdd-HHmmss}.pdf";

    return Results.File(pdfBytes, "application/pdf", fileName);
}).RequireAuthorization();

app.MapGet("/tickets/{ticketId:int}/attachments/{attachmentId:int}", async (
    int ticketId,
    int attachmentId,
    HttpContext httpContext,
    ITicketAttachmentDownloadService attachmentDownloadService,
    CancellationToken cancellationToken) =>
{
    try
    {
        var download = await attachmentDownloadService.OpenDownloadAsync(
            ticketId,
            attachmentId,
            httpContext.User,
            cancellationToken);

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
