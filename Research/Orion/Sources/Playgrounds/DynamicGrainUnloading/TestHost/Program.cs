using Orleans;
using Orleans.Hosting;
using Orleans.Runtime;
using Orleans.Runtime.DynamicGrains;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

// Read configuration for multi-silo support
var siloPort = builder.Configuration.GetValue<int?>("SiloPort") ?? 11111;
var gatewayPort = builder.Configuration.GetValue<int?>("GatewayPort") ?? 30000;
var isPrimary = builder.Configuration.GetValue<bool?>("IsPrimary") ?? true;
var primarySiloPort = builder.Configuration.GetValue<int?>("PrimarySiloPort") ?? 11111;

// Configure Orleans
builder.Host.UseOrleans((context, siloBuilder) =>
{
    if (isPrimary)
    {
        siloBuilder.UseLocalhostClustering(
            siloPort: siloPort,
            gatewayPort: gatewayPort,
            serviceId: "DynamicGrainsPlayground",
            clusterId: "dynamic-grains-dev");
    }
    else
    {
        siloBuilder.UseLocalhostClustering(
            siloPort: siloPort,
            gatewayPort: gatewayPort,
            primarySiloEndpoint: new IPEndPoint(IPAddress.Loopback, primarySiloPort),
            serviceId: "DynamicGrainsPlayground",
            clusterId: "dynamic-grains-dev");
    }

    siloBuilder
        .ConfigureLogging(logging =>
        {
            logging.AddConsole();
            logging.SetMinimumLevel(LogLevel.Information);
        });

    // CRITICAL: Enable dynamic grain loading
    DynamicGrainLoadingExtensions.AddDynamicGrainLoading(siloBuilder);
});

// Add controllers for testing
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

app.MapControllers();

Console.WriteLine($"Starting TestHost - Port: {siloPort}, Gateway: {gatewayPort}, IsPrimary: {isPrimary}");

app.Run();
