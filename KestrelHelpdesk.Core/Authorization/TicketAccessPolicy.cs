using KestrelHelpdesk.Core.Models;

namespace KestrelHelpdesk.Core.Authorization;

public static class TicketAccessPolicy
{
    public static bool CanView(CurrentUserInfo user, Ticket ticket) =>
        user.IsInRole(AppRoles.Manager)
        || ticket.CreatedBy == user.UserId
        || (user.IsInRole(AppRoles.Agent) && ticket.AssignedToUserId == user.UserId);

    public static bool CanEdit(CurrentUserInfo user, Ticket ticket) =>
        CanView(user, ticket);
}
