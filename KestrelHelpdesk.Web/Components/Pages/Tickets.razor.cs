using KestrelHelpdesk.Core.Authorization;
using KestrelHelpdesk.Core.Events;
using KestrelHelpdesk.Core.Interfaces;
using KestrelHelpdesk.Core.Models;
using KestrelHelpdesk.Web.Services;
using Microsoft.AspNetCore.Components;
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

    private async Task ExportCurrentPageAsync() => await ExportTicketsAsync(currentPageTickets, "Current Page");

    private async Task ExportAllTicketsAsync()
    {
        exporting = true;
        try
        {
            var tickets = await TicketQueryService.GetTicketsForExportAsync(
                StatusQuery,
                searchTerm,
                activeScope,
                notAssignedOnly,
                currentSortBy,
                currentSortDescending);

            await ExportTicketsAsync(tickets, "All Tickets");
        }
        finally
        {
            exporting = false;
        }
    }

    private async Task ExportTicketsAsync(IReadOnlyList<Ticket> tickets, string scopeLabel)
    {
        if (tickets.Count == 0) return;

        exporting = true;
        try
        {
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
            pdfModule ??= await JS.InvokeAsync<IJSObjectReference>("import", "./js/pdf-export.js");
            var base64 = Convert.ToBase64String(pdfBytes);
            var fileName = $"tickets-{(scopeLabel == "Current Page" ? "current-page" : "all")}-{DateTime.Now:yyyyMMdd-HHmmss}.pdf";
            await pdfModule.InvokeVoidAsync("downloadPdf", fileName, base64);
        }
        finally
        {
            exporting = false;
        }
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
            await Task.Delay(300, token);
            searchTerm = searchText.Trim();
            await RefreshGrid();
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task ManagerAssignmentChanged(int ticketId, ChangeEventArgs e)
    {
        await TicketAssignmentService.AssignTicketAsync(ticketId, e.Value?.ToString());
        await RefreshGrid();
    }

    private async Task AssignToMe(int ticketId, string? agentUserId)
    {
        await TicketAssignmentService.AssignTicketAsync(ticketId, agentUserId);
        await RefreshGrid();
    }

    private string GetAssigneeName(string? userId) =>
        string.IsNullOrWhiteSpace(userId)
            ? "-"
            : agents.FirstOrDefault(a => a.Id == userId)?.DisplayName ?? "Unknown agent";

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
