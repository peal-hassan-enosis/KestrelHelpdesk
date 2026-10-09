namespace KestrelHelpdesk.Web.State;

using KestrelHelpdesk.Core.Models;

public class CurrentUserState
{
    public AppUser? CurrentUser { get; private set; }

    // Fake user database for the header dropdown selector
    public List<AppUser> AvailableUsers { get; } = new()
    {
        new AppUser { Id = 1, Name = "Alice Employee", Role = "Employee" },
        new AppUser { Id = 2, Name = "Bob Agent", Role = "Agent" },
        new AppUser { Id = 3, Name = "Charlie Manager", Role = "Manager" }
    };

    public CurrentUserState()
    {
        // Default to the first fake user
        CurrentUser = AvailableUsers.First();
    }

    // Event fired whenever the active user changes
    public event Action? OnChange;

    public void SetUser(AppUser user)
    {
        CurrentUser = user;
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}