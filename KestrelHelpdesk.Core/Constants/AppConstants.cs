namespace KestrelHelpdesk.Core.Constants;

public static class AppConstants
{
    public const string UnknownAgent = "Unknown agent";
    public const int SearchDebounceMilliseconds = 300;
    public const long ExcelImportMaxFileSizeBytes = 10 * 1024 * 1024;

    public static class TicketBoard
    {
        public const int DefaultPageSize = 15;
        public const int PageSize30 = 30;
        public const int PageSize50 = 50;

        public static int[] PageSizeOptions => [DefaultPageSize, PageSize30, PageSize50];
    }
}
