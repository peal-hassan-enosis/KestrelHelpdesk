namespace KestrelHelpdesk.Core.Authorization;

public interface ICurrentUserContext
{
    Task<CurrentUserInfo?> GetCurrentUserAsync();
}
