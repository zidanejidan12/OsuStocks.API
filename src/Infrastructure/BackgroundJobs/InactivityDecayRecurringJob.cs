using Hangfire;
using MediatR;
using Microsoft.Extensions.Logging;
using OsuStocks.Application.Features.OsuIntegration.InactivityDecay;

namespace OsuStocks.Infrastructure.BackgroundJobs;

public sealed class InactivityDecayRecurringJob(
    ISender sender,
    ILogger<InactivityDecayRecurringJob> logger)
{
    [AutomaticRetry(Attempts = 3)]
    [DisableConcurrentExecution(timeoutInSeconds: 60)]
    public async Task RunAsync()
    {
        var result = await sender.Send(new EvaluateInactivityDecayCommand());

        if (!result.IsSuccess)
        {
            logger.LogWarning(
                "Inactivity decay evaluation failed: {Code} - {Message}",
                result.Error?.Code,
                result.Error?.Message);
            return;
        }

        logger.LogInformation(
            "Inactivity decay evaluation completed. PlayersEvaluated={PlayersEvaluated}, DecayEventsPublished={DecayEventsPublished}",
            result.Value?.PlayersEvaluated,
            result.Value?.DecayEventsPublished);
    }
}
