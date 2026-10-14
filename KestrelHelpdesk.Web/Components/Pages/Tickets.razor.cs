using KestrelHelpdesk.Core.Authorization;
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
    private IReadOnlyList<AssignableUser> formerAssignees = Array.Empty<AssignableUser>();
    private string? assignmentErrorMessage;
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

        var activeAgentIds = agents.Select(agent => agent.Id).ToHashSet(StringComparer.Ordinal);
        var formerAssigneeIds = items
            .Select(ticket => ticket.AssignedToUserId)
            .Where(id => !string.IsNullOrWhiteSpace(id) && !activeAgentIds.Contains(id))
            .Select(id => id!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        formerAssignees = await UserDirectory.GetUsersByIdsAsync(formerAssigneeIds);
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

        var rows = tickets.Select(ticket => new TicketPdfRow(
            ticket.Id,
            ticket.Title,
            GetStatusLabel(ticket.Status),
            ticket.Priority.ToString(),
            GetAssigneeName(ticket.AssignedToUserId),
            BrowserTimeZone.IsInitialized
                ? BrowserTimeZone.Format(ticket.CreatedAt)
                : ticket.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm"))).ToList();

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
        StatusQuery = value;
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
            await Task.Delay(AppConstants.SearchDebounceMilliseconds, token);
            searchTerm = searchText.Trim();
            await RefreshGrid();
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task ManagerAssignmentChanged(int ticketId, ChangeEventArgs e)
    {
        await UpdateAssignmentAsync(
            () => TicketAssignmentService.AssignTicketAsync(ticketId, e.Value?.ToString()));
    }

    private async Task AssignToMe(int ticketId, string? agentUserId)
    {
        await UpdateAssignmentAsync(() => TicketAssignmentService.AssignTicketAsync(ticketId, agentUserId));
    }

    private async Task UpdateAssignmentAsync(Func<Task> update)
    {
        assignmentErrorMessage = null;

        try
        {
            await update();
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogWarning(ex, "The current user was not authorized to change a ticket assignment.");
            assignmentErrorMessage = "You are not allowed to make that assignment change.";
            return;
        }
        catch (KeyNotFoundException ex)
        {
            Logger.LogWarning(ex, "Assignment update referenced a missing ticket or user.");
            assignmentErrorMessage = "The ticket or selected agent no longer exists. Refresh the board and try again.";
            return;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to change a ticket assignment.");
            assignmentErrorMessage = Environment.IsDevelopment()
                ? $"The assignment could not be updated: {ex.GetBaseException().Message}"
                : "The assignment could not be updated. Please refresh the board and try again.";
            return;
        }

        try
        {
            await RefreshGrid();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Assignment was saved, but the ticket grid could not refresh.");
            assignmentErrorMessage = "The assignment was saved, but the board could not refresh. Reload the page to see the latest state.";
        }
    }

    private string GetAssigneeName(string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return "-";

        var activeAgent = agents.FirstOrDefault(agent => agent.Id == userId);
        if (activeAgent is not null)
            return activeAgent.DisplayName;

        var formerAssignee = formerAssignees.FirstOrDefault(user => user.Id == userId);
        return formerAssignee is null
            ? $"Former assignee ({userId})"
            : $"{formerAssignee.DisplayName} (not an active agent)";
    }

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
