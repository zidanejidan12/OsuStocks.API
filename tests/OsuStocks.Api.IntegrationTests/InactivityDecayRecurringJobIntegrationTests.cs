using Hangfire;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using OsuStocks.Application.Common.Models;
using OsuStocks.Application.Features.OsuIntegration.InactivityDecay;
using OsuStocks.Infrastructure.BackgroundJobs;
using Xunit;

namespace OsuStocks.Api.IntegrationTests;

public sealed class InactivityDecayRecurringJobIntegrationTests
{
    [Fact]
    public async Task RunAsync_SendsEvaluateInactivityDecayCommand()
    {
        var sender = new CapturingSender();
        var job = new InactivityDecayRecurringJob(sender, NullLogger<InactivityDecayRecurringJob>.Instance);

        await job.RunAsync();

        Assert.True(sender.CommandSent);
    }

    [Fact]
    public void RunAsync_IsProtectedWithDisableConcurrentExecution()
    {
        var method = typeof(InactivityDecayRecurringJob).GetMethod(nameof(InactivityDecayRecurringJob.RunAsync));

        Assert.NotNull(method);
        var attributes = method!.GetCustomAttributes(typeof(DisableConcurrentExecutionAttribute), inherit: true);
        Assert.NotEmpty(attributes);
    }

    [Fact]
    public void InactivityDecayJob_IsRegisteredInRecurringJobs()
    {
        var capturingManager = new CapturingRecurringJobManager();
        var registrar = new OsuSynchronizationRecurringJobRegistrar(capturingManager);

        registrar.Register();

        Assert.Contains("inactivity-decay", capturingManager.RegisteredJobIds);
    }

    private sealed class CapturingSender : ISender
    {
        public bool CommandSent { get; private set; }

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (request is EvaluateInactivityDecayCommand)
            {
                CommandSent = true;
            }

            object response = Result.Success(new EvaluateInactivityDecayResponse(0, 0));
            return Task.FromResult((TResponse)response);
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest
        {
            return Task.CompletedTask;
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<object?>(Result.Success(new EvaluateInactivityDecayResponse(0, 0)));
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

    private sealed class CapturingRecurringJobManager : IRecurringJobManager
    {
        public List<string> RegisteredJobIds { get; } = [];

        public void AddOrUpdate(string recurringJobId, Hangfire.Common.Job job, string cronExpression, RecurringJobOptions options)
        {
            RegisteredJobIds.Add(recurringJobId);
        }

        public void RemoveIfExists(string recurringJobId)
        {
        }

        public void Trigger(string recurringJobId)
        {
        }
    }
}
