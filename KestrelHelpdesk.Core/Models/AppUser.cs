namespace KestrelHelpdesk.Core.Models;

public class AppUser
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty; // e.g., "Employee", "Agent", "Manager"
}