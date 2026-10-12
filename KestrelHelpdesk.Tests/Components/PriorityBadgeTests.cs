using Bunit;
using KestrelHelpdesk.Core.Models;
using KestrelHelpdesk.Web.Components.UI;
using Xunit;

namespace KestrelHelpdesk.Tests.Components;

public class PriorityBadgeTests : BunitContext
{
    [Theory]
    [InlineData(TicketPriority.Low, "bg-info")]
    [InlineData(TicketPriority.Medium, "bg-secondary")]
    [InlineData(TicketPriority.High, "bg-warning")]
    [InlineData(TicketPriority.Critical, "bg-danger")]
    public void RendersExpectedCssClassForPriority(TicketPriority priority, string expectedClass)
    {
        // Act
        var cut = Render<PriorityBadge>(parameters => parameters
            .Add(p => p.Priority, priority));

        // Assert
        var badge = cut.Find("span.badge");
        Assert.Contains(expectedClass, badge.ClassList);
        Assert.Equal(priority.ToString(), badge.TextContent.Trim());
    }
}
