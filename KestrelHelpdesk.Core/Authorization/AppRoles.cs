namespace KestrelHelpdesk.Core.Authorization;

public static class AppRoles
{
    public const string Employee = "Employee";
    public const string Agent = "Agent";
    public const string Manager = "Manager";

    // Use this shared role list for UI visibility attributes and authorization metadata.
    public const string EmployeeAgentManager = Employee + "," + Agent + "," + Manager;
}
