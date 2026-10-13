using System.ComponentModel.DataAnnotations;
using KestrelHelpdesk.Core.Authorization;
using KestrelHelpdesk.Core.Interfaces;
using KestrelHelpdesk.Core.Models;

namespace KestrelHelpdesk.Core.Services;

public sealed class TicketImportService : ITicketImportService
{
    private readonly ITicketRepository _repository;
    private readonly ICurrentUserContext _currentUser;
    private readonly IUserDirectory _userDirectory;

    public TicketImportService(
        ITicketRepository repository,
        ICurrentUserContext currentUser,
        IUserDirectory userDirectory)
    {
        _repository = repository;
        _currentUser = currentUser;
        _userDirectory = userDirectory;
    }

    public async Task<IReadOnlyList<string>> ValidateImportRowsAsync(
        IReadOnlyList<TicketImportRow> rows,
        bool ignoreAssignments,
        CancellationToken cancellationToken = default)
    {
        var user = await RequireUserAsync();
        var errors = new List<string>();
        var agents = await _userDirectory.GetAgentsAsync();
        var agentByEmail = agents.ToDictionary(a => a.Email, StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var ticket = new Ticket
            {
                Title = row.Title,
                Description = row.Description,
                Priority = row.Priority,
                Category = row.Category,
                Status = TicketStatus.Open,
                CreatedBy = user.UserId,
                CreatedAt = DateTime.UtcNow
            };

            foreach (var validationError in ValidateTicket(ticket))
                errors.Add($"Row {row.ExcelRowNumber}: {validationError}");
        }

        if (!user.IsInRole(AppRoles.Manager)
            && !ignoreAssignments
            && rows.Any(r => !string.IsNullOrWhiteSpace(r.AssignedToEmail)))
        {
            errors.Add("The Excel file contains ticket assignments, but your role is not authorized to assign tickets.");
        }

        if (user.IsInRole(AppRoles.Manager))
        {
            foreach (var row in rows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(row.AssignedToEmail))
                    continue;

                if (!agentByEmail.ContainsKey(row.AssignedToEmail.Trim()))
                    errors.Add($"Row {row.ExcelRowNumber}: Assigned To '{row.AssignedToEmail}' is not a valid agent email.");
            }
        }

        return errors;
    }

    public async Task<int> ImportTicketsAsync(
        IReadOnlyList<TicketImportRow> rows,
        bool ignoreAssignments,
        CancellationToken cancellationToken = default)
    {
        if (rows.Count == 0)
            throw new InvalidOperationException("There are no tickets to import.");

        var user = await RequireUserAsync();
        var errors = await ValidateImportRowsAsync(rows, ignoreAssignments, cancellationToken);
        if (errors.Count > 0)
            throw new InvalidOperationException(string.Join(" ", errors));

        var agents = await _userDirectory.GetAgentsAsync();
        var agentByEmail = agents.ToDictionary(a => a.Email, StringComparer.OrdinalIgnoreCase);
        var tickets = new List<Ticket>(rows.Count);

        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string? assignedToUserId = null;
            if (!ignoreAssignments && !string.IsNullOrWhiteSpace(row.AssignedToEmail))
                assignedToUserId = agentByEmail[row.AssignedToEmail.Trim()].Id;

            tickets.Add(new Ticket
            {
                Title = row.Title,
                Description = row.Description,
                Priority = row.Priority,
                Category = row.Category,
                Status = TicketStatus.Open,
                CreatedBy = user.UserId,
                AssignedToUserId = assignedToUserId,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _repository.CreateTicketsAsync(tickets, cancellationToken);
        return tickets.Count;
    }

    private async Task<CurrentUserInfo> RequireUserAsync() =>
        await _currentUser.GetCurrentUserAsync()
        ?? throw new UnauthorizedAccessException("You must be signed in.");

    private static IReadOnlyList<string> ValidateTicket(Ticket ticket)
    {
        var validationContext = new ValidationContext(ticket);
        var validationResults = new List<ValidationResult>();
        Validator.TryValidateObject(ticket, validationContext, validationResults, validateAllProperties: true);

        return validationResults
            .Select(result => result.ErrorMessage)
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .Cast<string>()
            .ToList();
    }
}
