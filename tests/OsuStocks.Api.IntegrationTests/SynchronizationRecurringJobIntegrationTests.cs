using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using OsuStocks.Application.Common.Models;
using OsuStocks.Application.Features.OsuIntegration.Synchronization.SynchronizeTrackedPlayers;
using OsuStocks.Domain.Common.Enums;
using OsuStocks.Infrastructure.BackgroundJobs;
using Xunit;

namespace OsuStocks.Api.IntegrationTests;

public sealed class SynchronizationRecurringJobIntegrationTests
{
    [Fact]
    public async Task RunTierAsync_SendsTierScopedSynchronizationCommand()
    {
        var sender = new CapturingSender();
        var job = new OsuSynchronizationRecurringJob(sender, NullLogger<OsuSynchronizationRecurringJob>.Instance);

        await job.RunTierAsync(TrackingTier.Tier2);

        Assert.NotNull(sender.LastCommand);
        Assert.Equal(TrackingTier.Tier2, sender.LastCommand!.Tier);
    }

    private sealed class CapturingSender : ISender
    {
        public SynchronizeTrackedPlayersCommand? LastCommand { get; private set; }

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (request is SynchronizeTrackedPlayersCommand command)
            {
                LastCommand = command;
            }

            object response = Result.Success(new SynchronizeTrackedPlayersResponse(0, 0, 0, 0));
            return Task.FromResult((TResponse)response);
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest
        {
            return Task.CompletedTask;
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        {
            if (request is SynchronizeTrackedPlayersCommand command)
            {
                LastCommand = command;
            }

            return Task.FromResult<object?>(Result.Success(new SynchronizeTrackedPlayersResponse(0, 0, 0, 0)));
        }

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            return EmptyAsync<TResponse>();
        }

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
        {
            return EmptyAsync<object?>();
        }

        private static async IAsyncEnumerable<T> EmptyAsync<T>()
        {
            await Task.CompletedTask;
            yield break;
        }
    }
}
