using Bunit;
using KestrelHelpdesk.Core.Models;
using KestrelHelpdesk.Web.Components.Pages;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace KestrelHelpdesk.Tests.Components;

public class TicketFormTests : BunitContext
{
    [Fact]
    public void InvalidForm_ShowsValidationMessages_AndDoesNotSubmit()
    {
        // Arrange
        ConfigureJsInterop();
        var submitted = false;
        var model = new Ticket
        {
            Title = string.Empty,
            Description = string.Empty,
            Category = null,
            Priority = TicketPriority.Low,
            Status = TicketStatus.Open
        };

        var cut = Render<TicketForm>(parameters => parameters
            .Add(p => p.TicketModel, model)
            .Add(p => p.IsCreateMode, true)
            .Add(p => p.OnValidSubmit, EventCallback.Factory.Create<Ticket>(
                new object(),
                _ => submitted = true)));

        // Act
        cut.Find("form").Submit();

        // Assert
        var validationSummary = cut.Find("div.alert-danger").TextContent;
        Assert.Contains("Title is required.", validationSummary);
        Assert.Contains("Description is required.", validationSummary);
        Assert.Contains("Category is required.", validationSummary);
        Assert.False(submitted);
    }

    private void ConfigureJsInterop()
    {
        var module = JSInterop.SetupModule("./js/ticket-form.js");
        module.SetupVoid("focusElement");
    }
}
