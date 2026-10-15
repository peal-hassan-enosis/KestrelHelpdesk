namespace KestrelHelpdesk.Core.Constants;

public static class AppLimits
{
    public const int TicketSearchDebounceMilliseconds = 300;
    public const int DefaultPageSize = 15;
    public const int MediumPageSize = 30;
    public const int LargePageSize = 50;
    public const int MaxAttachmentsPerTicket = 3;
    public const long MaxAttachmentSizeBytes = 10 * 1024 * 1024;
    public const string UnknownAgent = "Unknown agent";
}
