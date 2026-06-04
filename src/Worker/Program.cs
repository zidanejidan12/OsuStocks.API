using OsuStocks.Application;
using OsuStocks.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, addHangfireServer: true);

var host = builder.Build();
host.Run();
