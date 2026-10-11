using Microsoft.AspNetCore.Components.Server.Circuits;

namespace KestrelHelpdesk.Web.Services;

public sealed class LoggingCircuitHandler : CircuitHandler
{
    private readonly ILogger<LoggingCircuitHandler> logger;

    public LoggingCircuitHandler(ILogger<LoggingCircuitHandler> logger)
    {
        this.logger = logger;
    }

    public override Task OnCircuitOpenedAsync(
        Circuit circuit,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Blazor circuit {CircuitId} connected.", circuit.Id);
        return Task.CompletedTask;
    }

    public override Task OnCircuitClosedAsync(
        Circuit circuit,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Blazor circuit {CircuitId} disconnected.", circuit.Id);
        return Task.CompletedTask;
    }
}
