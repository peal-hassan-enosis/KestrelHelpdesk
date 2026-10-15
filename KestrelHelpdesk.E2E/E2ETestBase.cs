using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit.v3;

namespace KestrelHelpdesk.E2E;

public abstract class E2ETestBase : PageTest
{
    private bool traceEnabled;
    private string? tracePath;

    protected static string BaseUrl =>
        Environment.GetEnvironmentVariable("KESTREL_E2E_BASE_URL")
        ?? "http://localhost:5062";

    protected static string RequiredSetting(string name) =>
        Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException($"Required E2E setting '{name}' is not configured.");

    protected static string EmployeeEmail => RequiredSetting("KESTREL_E2E_EMPLOYEE_EMAIL");
    protected static string EmployeePassword => RequiredSetting("KESTREL_E2E_EMPLOYEE_PASSWORD");
    protected static string AgentEmail => RequiredSetting("KESTREL_E2E_AGENT_EMAIL");
    protected static string AgentPassword => RequiredSetting("KESTREL_E2E_AGENT_PASSWORD");
    protected static string ManagerEmail => RequiredSetting("KESTREL_E2E_MANAGER_EMAIL");
    protected static string ManagerPassword => RequiredSetting("KESTREL_E2E_MANAGER_PASSWORD");

    public override BrowserNewContextOptions ContextOptions() => new()
    {
        BaseURL = BaseUrl,
        IgnoreHTTPSErrors = true,
        ViewportSize = new() { Width = 1440, Height = 1000 }
    };

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        Page.SetDefaultTimeout(10_000);
        SetDefaultExpectTimeout(10_000);

        traceEnabled = string.Equals(
            Environment.GetEnvironmentVariable("KESTREL_E2E_TRACE"),
            "true",
            StringComparison.OrdinalIgnoreCase);

        if (traceEnabled)
        {
            await Context.Tracing.StartAsync(new()
            {
                Screenshots = true,
                Snapshots = true,
                Sources = true
            });
        }
    }

    public override async ValueTask DisposeAsync()
    {
        if (traceEnabled)
        {
            Directory.CreateDirectory(Path.Combine("TestResults", "Traces"));
            tracePath = Path.Combine(
                "TestResults",
                "Traces",
                $"trace-{Guid.NewGuid():N}.zip");

            await Context.Tracing.StopAsync(new() { Path = tracePath });
        }

        await base.DisposeAsync();
    }

    protected async Task LoginAsync(string email, string password, string returnPath = "/tickets")
    {
        await Page.GotoAsync($"/Account/Login?ReturnUrl={Uri.EscapeDataString(returnPath)}");
        await Page.GetByLabel("Email").FillAsync(email);
        await Page.GetByLabel("Password").FillAsync(password);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Log in" }).ClickAsync();
        await Expect(Page).ToHaveURLAsync(new Regex($"{Regex.Escape(returnPath.TrimEnd('/'))}/?$"));
    }

    protected async Task<int> CreateTicketAsync(
        string prefix,
        string? description = null,
        string category = "Network",
        string priority = "High")
    {
        var title = $"{prefix}-{Guid.NewGuid():N}";

        await Page.GotoAsync("/tickets/new");
        await Page.GetByLabel("Ticket title").FillAsync(title);
        await Page.GetByLabel("Category").SelectOptionAsync(category);
        await Page.GetByLabel("Priority").SelectOptionAsync(priority);
        await Page.GetByLabel("Description").FillAsync(
            description ?? "Created by the Playwright end-to-end test suite.");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Create ticket" }).ClickAsync();

        await Expect(Page).ToHaveURLAsync(new Regex(@"/tickets/\d+/?$"));
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = title })).ToBeVisibleAsync();

        var match = Regex.Match(Page.Url, @"/tickets/(\d+)/?$");
        if (!match.Success)
            throw new InvalidOperationException($"Could not extract ticket ID from URL: {Page.Url}");

        return int.Parse(match.Groups[1].Value);
    }
}
