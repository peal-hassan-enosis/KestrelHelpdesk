using System.Globalization;
using Microsoft.JSInterop;

namespace KestrelHelpdesk.Web.Services;

public sealed class BrowserTimeZoneService : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private IJSObjectReference? _module;

    public BrowserTimeZoneService(IJSRuntime js)
    {
        _js = js;
    }

    public string TimeZoneId { get; private set; } = "UTC";

    public bool IsInitialized { get; private set; }

    public async Task InitializeAsync()
    {
        if (IsInitialized)
        {
            return;
        }

        _module = await _js.InvokeAsync<IJSObjectReference>("import", "./js/timezone.js");
        TimeZoneId = await _module.InvokeAsync<string>("getBrowserTimeZone") ?? "UTC";
        IsInitialized = true;
    }

    public DateTime ToLocalTime(DateTime utcDateTime)
    {
        var utc = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);

        try
        {
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
            return TimeZoneInfo.ConvertTimeFromUtc(utc, timeZone);
        }
        catch (TimeZoneNotFoundException)
        {
            return utc;
        }
        catch (InvalidTimeZoneException)
        {
            return utc;
        }
    }

    public string Format(DateTime utcDateTime)
    {
        return ToLocalTime(utcDateTime).ToString("g", CultureInfo.CurrentCulture);
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try
            {
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // The Blazor circuit may already be disconnected.
            }
        }
    }
}
