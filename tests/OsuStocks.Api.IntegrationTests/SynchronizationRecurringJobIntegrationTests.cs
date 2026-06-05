using Hangfire;
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
    public async Task RunTier1Async_SendsTier1SynchronizationCommand()
    {
        var sender = new CapturingSender();
        var job = new OsuSynchronizationRecurringJob(sender, NullLogger<OsuSynchronizationRecurringJob>.Instance);

        await job.RunTier1Async();

        Assert.NotNull(sender.LastCommand);
        Assert.Equal(TrackingTier.Tier1, sender.LastCommand!.Tier);
    }

    [Fact]
    public async Task RunTier2Async_SendsTier2SynchronizationCommand()
    {
        var sender = new CapturingSender();
        var job = new OsuSynchronizationRecurringJob(sender, NullLogger<OsuSynchronizationRecurringJob>.Instance);

        await job.RunTier2Async();

        Assert.NotNull(sender.LastCommand);
        Assert.Equal(TrackingTier.Tier2, sender.LastCommand!.Tier);
    }

    [Fact]
    public async Task RunTier3Async_SendsTier3SynchronizationCommand()
    {
        var sender = new CapturingSender();
        var job = new OsuSynchronizationRecurringJob(sender, NullLogger<OsuSynchronizationRecurringJob>.Instance);

        await job.RunTier3Async();

        Assert.NotNull(sender.LastCommand);
        Assert.Equal(TrackingTier.Tier3, sender.LastCommand!.Tier);
    }

    [Theory]
    [InlineData(nameof(OsuSynchronizationRecurringJob.RunTier1Async))]
    [InlineData(nameof(OsuSynchronizationRecurringJob.RunTier2Async))]
    [InlineData(nameof(OsuSynchronizationRecurringJob.RunTier3Async))]
    public void SyncRecurringJobs_AreProtectedWithDisableConcurrentExecution(string methodName)
    {
        var method = typeof(OsuSynchronizationRecurringJob).GetMethod(methodName);

        Assert.NotNull(method);
        var attributes = method!.GetCustomAttributes(typeof(DisableConcurrentExecutionAttribute), inherit: true);
        Assert.NotEmpty(attributes);
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
