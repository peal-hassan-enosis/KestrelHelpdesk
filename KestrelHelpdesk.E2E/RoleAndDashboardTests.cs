using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Xunit;

namespace KestrelHelpdesk.E2E;

public sealed class RoleAndDashboardTests : E2ETestBase
{
    [Fact]
    public async Task Manager_CanSeeEmployeeTicket_WhileAgentCannotSeeUnassignedEmployeeTicket()
    {
        await LoginAsync(EmployeeEmail, EmployeePassword);
        var ticketId = await CreateTicketAsync("E2E-Visibility");

        await using var managerContext = await Browser.NewContextAsync(ContextOptions());
        var managerPage = await managerContext.NewPageAsync();
        await LoginAsync(managerPage, ManagerEmail, ManagerPassword, "/tickets");
        await managerPage.GotoAsync($"/tickets/{ticketId}");
        await Expect(managerPage).ToHaveURLAsync(new Regex($"/tickets/{ticketId}/?$"));
        await Expect(managerPage.GetByRole(AriaRole.Heading, new() { Name = "Issue description" })).ToBeVisibleAsync();

        await using var agentContext = await Browser.NewContextAsync(ContextOptions());
        var agentPage = await agentContext.NewPageAsync();
        await LoginAsync(agentPage, AgentEmail, AgentPassword, "/tickets");
        await agentPage.GotoAsync($"/tickets/{ticketId}");
        await Expect(agentPage.GetByRole(AriaRole.Heading, new() { Name = "Ticket Not Found" })).ToBeVisibleAsync();
        await Expect(agentPage.GetByText("does not exist or you don't have access to it.")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Agent_CanCreateAndAssignTicketToSelf()
    {
        await LoginAsync(AgentEmail, AgentPassword);
        var title = $"E2E-Agent-Assign-{Guid.NewGuid():N}";

        await Page.GotoAsync("/tickets/new");
        await Page.GetByLabel("Ticket title").FillAsync(title);
        await Page.GetByLabel("Category").SelectOptionAsync("Software");
        await Page.GetByLabel("Priority").SelectOptionAsync("High");
        await Page.GetByLabel("Description").FillAsync("Agent assignment end-to-end test.");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Create ticket" }).ClickAsync();

        await Page.GotoAsync("/tickets");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Created by Me" }).ClickAsync();
        await Page.GetByPlaceholder("Title or description").FillAsync(title);
        await Expect(Page.GetByText(title, new() { Exact = true })).ToBeVisibleAsync();

        var row = Page.GetByRole(AriaRole.Row).Filter(new() { HasText = title });
        await row.GetByRole(AriaRole.Button, new() { Name = "Assign to Me" }).ClickAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Assigned" }).ClickAsync();
        await Page.GetByPlaceholder("Title or description").FillAsync(title);
        await Expect(Page.GetByText(title, new() { Exact = true })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task EmployeeDashboard_ShowsOwnTicketScope()
    {
        await LoginAsync(EmployeeEmail, EmployeePassword, "/dashboard");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "My tickets" })).ToBeVisibleAsync();
        await Expect(Page.GetByText("Total tickets", new() { Exact = true })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task AgentDashboard_ProvidesAssignedAndCreatedScopes()
    {
        await LoginAsync(AgentEmail, AgentPassword, "/dashboard");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "My workload" })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Assigned to me" })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Created by me" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task ManagerDashboard_ShowsSupportOverview()
    {
        await LoginAsync(ManagerEmail, ManagerPassword, "/dashboard");
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Support overview" })).ToBeVisibleAsync();
        await Expect(Page.GetByText("Total tickets", new() { Exact = true })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Manager_CanOpenRoleAssignment()
    {
        await LoginAsync(ManagerEmail, ManagerPassword, "/role-assignment");
        await Expect(Page).ToHaveURLAsync(new Regex(@"/role-assignment/?$"));
    }

    [Fact]
    public async Task Employee_CannotOpenManagerRoleAssignment()
    {
        await LoginAsync(EmployeeEmail, EmployeePassword);
        await Page.GotoAsync("/role-assignment");

        await Expect(Page).ToHaveURLAsync(new Regex(@"/Account/AccessDenied\?ReturnUrl=.*role-assignment"));
    }

    private async Task LoginAsync(IPage page, string email, string password, string returnPath)
    {
        await page.GotoAsync($"/Account/Login?ReturnUrl={Uri.EscapeDataString(returnPath)}");
        await page.GetByLabel("Email").FillAsync(email);
        await page.GetByLabel("Password").FillAsync(password);
        await page.GetByRole(AriaRole.Button, new() { Name = "Log in" }).ClickAsync();
        await Expect(page).ToHaveURLAsync(new Regex($"{Regex.Escape(returnPath.TrimEnd('/'))}/?$"));
    }
}