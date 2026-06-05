using Microsoft.Extensions.DependencyInjection;
using OsuStocks.Application;
using OsuStocks.Infrastructure;
using OsuStocks.Infrastructure.BackgroundJobs;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, addHangfireServer: true);

var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    var registrar = scope.ServiceProvider.GetRequiredService<IOsuSynchronizationRecurringJobRegistrar>();
    registrar.Register();
}

host.Run();
