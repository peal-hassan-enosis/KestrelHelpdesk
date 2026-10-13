using KestrelHelpdesk.Core.Authorization;
using KestrelHelpdesk.Core.Models;

namespace KestrelHelpdesk.Tests.Services;

public sealed class TicketAccessPolicyTests
{
    [Fact]
    public void Manager_CanViewAnyTicket()
    {
        var user = User("manager-1", AppRoles.Manager);
        var ticket = new Ticket { CreatedBy = "employee-1", AssignedToUserId = "agent-1" };

        Assert.True(TicketAccessPolicy.CanView(user, ticket));
    }

    [Fact]
    public void Employee_CanViewOwnTicketOnly()
    {
        var user = User("employee-1", AppRoles.Employee);
        var ownTicket = new Ticket { CreatedBy = "employee-1" };
        var otherTicket = new Ticket { CreatedBy = "employee-2" };

        Assert.True(TicketAccessPolicy.CanView(user, ownTicket));
        Assert.False(TicketAccessPolicy.CanView(user, otherTicket));
    }

    [Fact]
    public void Agent_CanViewAssignedTicketOnly()
    {
        var user = User("agent-1", AppRoles.Agent);
        var assignedTicket = new Ticket { CreatedBy = "employee-1", AssignedToUserId = "agent-1" };
        var otherTicket = new Ticket { CreatedBy = "employee-1", AssignedToUserId = "agent-2" };

        Assert.True(TicketAccessPolicy.CanView(user, assignedTicket));
        Assert.False(TicketAccessPolicy.CanView(user, otherTicket));
    }

    private static CurrentUserInfo User(string id, string role) =>
        new(id, $"{id}@example.com", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { role });
}
