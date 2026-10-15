using KestrelHelpdesk.Core.Authorization;
using KestrelHelpdesk.Core.Constants;
using KestrelHelpdesk.Core.Events;
using KestrelHelpdesk.Core.Interfaces;
using KestrelHelpdesk.Core.Models;
using KestrelHelpdesk.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MudBlazor;

namespace KestrelHelpdesk.Web.Components.Pages;

public partial class Tickets : IAsyncDisposable
{
    [Inject] private ITicketQueryService TicketQueryService { get; set; } = default!;
    [Inject] private ITicketAssignmentService TicketAssignmentService { get; set; } = default!;
    [Inject] private IUserDirectory UserDirectory { get; set; } = default!;
    [Inject] private ITicketEventService TicketEvents { get; set; } = default!;
    [Inject] private ITicketPdfService TicketPdfService { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private BrowserTimeZoneService BrowserTimeZone { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ILogger<Tickets> Logger { get; set; } = default!;
    [Inject] private IWebHostEnvironment Environment { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "status")]
    public string? StatusQuery { get; set; }

    private MudDataGrid<Ticket>? grid;
    private IReadOnlyList<AssignableUser> agents = Array.Empty<AssignableUser>();
    private IReadOnlyList<Ticket> currentPageTickets = Array.Empty<Ticket>();
    private TicketScope activeScope = TicketScope.EmployeeOwn;
    private bool isAgent;
    private bool isManager;
    private bool notAssignedOnly;
    private bool exporting;
    private bool exportingCurrentPage;
    private bool exportingAllTickets;
    private string? exportErrorMessage;
    private string? exportSuccessMessage;
    private string? assignmentErrorMessage;
    private string? currentUserId;
    private string searchText = string.Empty;
    private string searchTerm = string.Empty;
    private string? currentSortBy;
    private bool currentSortDescending;
    private CancellationTokenSource? searchDebounceCts;
    private IJSObjectReference? pdfModule;

    protected override async Task OnInitializedAsync()
    {
        TicketEvents.TicketChanged += OnTicketChangedAsync;

        var user = (await AuthenticationStateProvider.GetAuthenticationStateAsync()).User;
        isAgent = user.IsInRole(AppRoles.Agent);
        isManager = user.IsInRole(AppRoles.Manager);
        currentUserId = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        activeScope = isManager ? TicketScope.ManagerAll : isAgent ? TicketScope.AgentAssigned : TicketScope.EmployeeOwn;
        agents = await UserDirectory.GetAgentsAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        await BrowserTimeZone.InitializeAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task<GridData<Ticket>> LoadGridData(GridState<Ticket> state, CancellationToken cancellationToken)
    {
        var sort = state.SortDefinitions.FirstOrDefault();
        currentSortBy = sort?.SortBy;
        currentSortDescending = sort?.Descending ?? false;

        var (items, totalCount) = await TicketQueryService.GetTicketGridDataAsync(
            state.Page,
            state.PageSize,
            StatusQuery,
            searchTerm,
            activeScope,
            notAssignedOnly,
            currentSortBy,
            currentSortDescending);

        currentPageTickets = items;
        return new GridData<Ticket> { Items = items, TotalItems = totalCount };
    }

    private async Task ExportCurrentPageAsync()
    {
        if (exporting)
            return;

        exportErrorMessage = null;
        exportSuccessMessage = null;
        exporting = true;
        exportingCurrentPage = true;

        try
        {
            await GenerateAndDownloadPdfAsync(currentPageTickets, "Current Page");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to export the current ticket page as PDF.");
            exportErrorMessage = GetExportErrorMessage(ex);
        }
        finally
        {
            exporting = false;
            exportingCurrentPage = false;
        }
    }

    private async Task ExportAllTicketsAsync()
    {
        if (exporting)
            return;

        exportErrorMessage = null;
        exportSuccessMessage = null;
        exporting = true;
        exportingAllTickets = true;

        try
        {
            var tickets = await TicketQueryService.GetTicketsForExportAsync(
                StatusQuery,
                searchTerm,
                activeScope,
                notAssignedOnly,
                currentSortBy,
                currentSortDescending);

            await GenerateAndDownloadPdfAsync(tickets, "All Tickets");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to export all tickets as PDF.");
            exportErrorMessage = GetExportErrorMessage(ex);
        }
        finally
        {
            exporting = false;
            exportingAllTickets = false;
        }
    }

    private string GetExportErrorMessage(Exception exception) => Environment.IsDevelopment()
        ? $"PDF export failed: {exception.GetBaseException().Message}"
        : "The PDF could not be exported. Please try again. If the issue continues, check the application logs.";

    private async Task GenerateAndDownloadPdfAsync(IReadOnlyList<Ticket> tickets, string scopeLabel)
    {
        if (tickets.Count == 0)
        {
            exportErrorMessage = scopeLabel == "Current Page"
                ? "There are no tickets on the current page to export."
                : "There are no tickets in the current view to export.";
            return;
        }

        var rows = new List<TicketPdfRow>(tickets.Count);
        foreach (var ticket in tickets)
        {
            rows.Add(new TicketPdfRow(
                ticket.Id,
                ticket.Title,
                GetStatusLabel(ticket.Status),
                ticket.Priority.ToString(),
                GetAssigneeName(ticket.AssignedToUserId),
                BrowserTimeZone.IsInitialized
                    ? BrowserTimeZone.Format(ticket.CreatedAt)
                    : ticket.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm")));
        }

        var pdfBytes = TicketPdfService.Generate(rows, $"Ticket Board - {scopeLabel}");
        var moduleUrl = new Uri(new Uri(NavigationManager.BaseUri), "js/pdf-export.js").ToString();
        pdfModule ??= await JS.InvokeAsync<IJSObjectReference>("import", moduleUrl);

        var base64 = Convert.ToBase64String(pdfBytes);
        var fileName = $"tickets-{(scopeLabel == "Current Page" ? "current-page" : "all")}-{DateTime.Now:yyyyMMdd-HHmmss}.pdf";
        await pdfModule.InvokeVoidAsync("downloadPdf", fileName, base64);
        exportSuccessMessage = $"PDF export started for {tickets.Count} ticket(s).";
    }

    private async Task OnTicketChangedAsync(TicketChangedEventArgs eventArgs) => await InvokeAsync(RefreshGrid);

    private async Task SetScope(TicketScope scope)
    {
        activeScope = scope;
        await RefreshGrid();
    }

    private async Task RefreshGrid()
    {
        if (grid is not null) await grid.ReloadServerData();
    }

    private async Task OnStatusChanged(string? value)
    {
        StatusQuery = value == "All" ? null : value;
        await RefreshGrid();
    }

    private async Task OnSearchInput(string value)
    {
        searchText = value;
        searchDebounceCts?.Cancel();
        searchDebounceCts?.Dispose();
        searchDebounceCts = new CancellationTokenSource();
        var token = searchDebounceCts.Token;

        try
        {
            await Task.Delay(AppLimits.TicketSearchDebounceMilliseconds, token);
            searchTerm = searchText.Trim();
            await RefreshGrid();
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task ManagerAssignmentChanged(int ticketId, ChangeEventArgs e)
    {
        assignmentErrorMessage = null;

        try
        {
            await TicketAssignmentService.AssignTicketAsync(ticketId, e.Value?.ToString());
            await RefreshGrid();
        }
        catch (UnauthorizedAccessException)
        {
            assignmentErrorMessage = "You are not authorized to change this ticket assignment.";
        }
        catch (KeyNotFoundException)
        {
            assignmentErrorMessage = "The ticket could not be found. It may have been removed or is no longer available.";
            await RefreshGrid();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to change assignment for ticket {TicketId}.", ticketId);
            assignmentErrorMessage = "The ticket assignment could not be changed. Please try again.";
        }
    }

    private async Task AssignToMe(int ticketId, string? agentUserId)
    {
        assignmentErrorMessage = null;

        try
        {
            await TicketAssignmentService.AssignTicketAsync(ticketId, agentUserId);
            await RefreshGrid();
        }
        catch (UnauthorizedAccessException)
        {
            assignmentErrorMessage = "You are not authorized to change this ticket assignment.";
        }
        catch (KeyNotFoundException)
        {
            assignmentErrorMessage = "The ticket could not be found. It may have been removed or is no longer available.";
            await RefreshGrid();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to assign ticket {TicketId} for the current agent.", ticketId);
            assignmentErrorMessage = "The ticket assignment could not be changed. Please try again.";
        }
    }

    private string GetAssigneeName(string? userId) =>
        string.IsNullOrWhiteSpace(userId)
            ? "-"
            : agents.FirstOrDefault(a => a.Id == userId)?.DisplayName ?? "-";

    private static Color GetStatusColor(TicketStatus status) => status switch
    {
        TicketStatus.Open => Color.Primary,
        TicketStatus.InProgress => Color.Warning,
        TicketStatus.Resolved => Color.Success,
        _ => Color.Default
    };

    private static string GetStatusLabel(TicketStatus status) => status switch
    {
        TicketStatus.InProgress => "In Progress",
        _ => status.ToString()
    };

    private static Color GetPriorityColor(TicketPriority priority) => priority switch
    {
        TicketPriority.Low => Color.Info,
        TicketPriority.Medium => Color.Default,
        TicketPriority.High => Color.Warning,
        TicketPriority.Critical => Color.Error,
        _ => Color.Default
    };

    public async ValueTask DisposeAsync()
    {
        searchDebounceCts?.Cancel();
        searchDebounceCts?.Dispose();
        TicketEvents.TicketChanged -= OnTicketChangedAsync;

        if (pdfModule is not null)
        {
            try
            {
                await pdfModule.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }
        }
    }
}
