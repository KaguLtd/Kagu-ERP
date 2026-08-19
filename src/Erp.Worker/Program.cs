using KaguERP.Bootstrap;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddKaguErpBootstrap();

using var host = builder.Build();
await host.RunAsync();

