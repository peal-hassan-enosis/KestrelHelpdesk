using Bunit;
using KestrelHelpdesk.Core.Models;
using KestrelHelpdesk.Web.Components.Helper;
using Xunit;

namespace KestrelHelpdesk.Tests.Components;

public class PriorityBadgeTests : BunitContext
{
    [Theory]
    [InlineData(TicketPriority.Low, "priority-low")]
    [InlineData(TicketPriority.Medium, "priority-medium")]
    [InlineData(TicketPriority.High, "priority-high")]
    [InlineData(TicketPriority.Critical, "priority-critical")]
    public void RendersExpectedCssClassForPriority(TicketPriority priority, string expectedClass)
    {
        // Act
        var cut = Render<PriorityBadge>(parameters => parameters
            .Add(p => p.Priority, priority));

        // Assert
        var badge = cut.Find("span.ticket-priority-badge");
        Assert.Contains(expectedClass, badge.ClassList);
        Assert.Equal(priority.ToString(), badge.TextContent.Trim());
        Assert.Equal($"Priority: {priority}", badge.GetAttribute("aria-label"));
    }
}
