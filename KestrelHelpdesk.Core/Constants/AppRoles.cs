namespace KestrelHelpdesk.Core.Constants;

public static class AppRoles
{
    public const string Employee = "Employee";
    public const string Agent = "Agent";
    public const string Manager = "Manager";

    public const string EmployeeAgentManager = $"{Employee},{Agent},{Manager}";
    public const string EmployeeAgentManagerSpaced = $"{Employee}, {Agent}, {Manager}";
}
