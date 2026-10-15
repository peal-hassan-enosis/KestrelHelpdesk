using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xunit;

namespace KestrelHelpdesk.E2E;

public sealed class AuthenticationAndHealthTests : E2ETestBase
{
    [Fact]
    public async Task HealthEndpoint_ReturnsOk()
    {
        var response = await Page.GotoAsync("/health");

        Assert.NotNull(response);
        Assert.Equal(200, response!.Status);
        await Expect(Page).ToHaveURLAsync(new Regex(@"/health/?$"));
    }

    [Fact]
    public async Task Employee_CanLogInAndReachTicketBoard()
    {
        await LoginAsync(EmployeeEmail, EmployeePassword);

        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Ticket board" })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Create ticket" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task InvalidCredentials_StayOnLoginPage()
    {
        await Page.GotoAsync("/Account/Login");
        await Page.GetByLabel("Email").FillAsync(EmployeeEmail);
        await Page.GetByLabel("Password").FillAsync($"invalid-{Guid.NewGuid():N}");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Log in" }).ClickAsync();

        await Expect(Page).ToHaveURLAsync(new Regex(@"/Account/Login/?$"));
        await Expect(Page.GetByText("Invalid login attempt.")).ToBeVisibleAsync();
    }
}