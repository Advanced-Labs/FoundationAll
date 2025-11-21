using System.Net;
using Lamar.Microsoft.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Configuration;
using Orleans.Hosting;
using Orleans.Runtime.DynamicGrains;

namespace Orion.Core;

/// <summary>
/// Orion node - hosts an Orleans silo with Lamar DI integration
/// </summary>
public class OrionNode
{
    private readonly IHost _host;
    private readonly ILogger<OrionNode> _logger;

    public OrionNode(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        // Integrate Lamar as the DI container
        builder.Host.UseLamar();

        // Configure Orleans silo
        builder.Host.UseOrleans((context, siloBuilder) =>
        {
            ConfigureOrleans(siloBuilder);
        });

        _host = builder.Build();
        _logger = _host.Services.GetRequiredService<ILogger<OrionNode>>();
    }

    private void ConfigureOrleans(ISiloBuilder silo)
    {
        // Cluster configuration
        silo.Configure<ClusterOptions>(opts =>
        {
            opts.ClusterId = "orion-dev";
            opts.ServiceId = "Orion";
        });

        // Endpoint configuration
        silo.Configure<EndpointOptions>(opts =>
        {
            opts.AdvertisedIPAddress = IPAddress.Loopback;
            opts.SiloPort = 11111;
            opts.GatewayPort = 30000;
        });

        // RavenDB membership (Batch #3 requirement)
        // Note: If RavenDB membership has compatibility issues similar to reminders,
        // this can be reverted to UseLocalhostClustering
        try
        {
            silo.UseRavenDbMembershipTable(options =>
            {
                options.Urls = new[] { "http://127.0.0.1:38880" };
                options.DatabaseName = "Orion";
            });
        }
        catch (Exception)
        {
            // Fallback to localhost clustering if RavenDB membership fails
            _logger?.LogWarning("RavenDB membership configuration failed, falling back to localhost clustering");
            silo.UseLocalhostClustering(
                siloPort: 11111,
                gatewayPort: 30000,
                serviceId: "Orion",
                clusterId: "orion-dev");
        }

        // RavenDB grain storage
        silo.AddRavenDbGrainStorage("OrionStore", options =>
        {
            options.Urls = new[] { "http://127.0.0.1:38880" };
            options.DatabaseName = "Orion";
        });

        // In-memory reminders (Batch #3 requirement)
        // RavenDB reminders are known to be incompatible with Orleans 9.1
        silo.UseInMemoryReminderService();

        // Dynamic grain loading (Batch #3 requirement)
        silo.AddDynamicGrainLoading();
    }

    /// <summary>
    /// Loads dynamic grain assemblies at runtime
    /// </summary>
    private async Task LoadDynamicGrainsAsync(CancellationToken cancellationToken = default)
    {
        var services = _host.Services;
        var loader = services.GetRequiredService<IDynamicGrainLoader>();

        // Assume plugin assembly is in the same folder as the console exe
        var baseDir = AppContext.BaseDirectory;
        var assemblyPath = Path.Combine(baseDir, "Orion.DynamicKvGrains.dll");

        if (!File.Exists(assemblyPath))
        {
            _logger.LogWarning("Dynamic grain assembly not found at {Path}. Skipping dynamic load.", assemblyPath);
            return;
        }

        _logger.LogInformation("Loading dynamic grain assembly from {Path}", assemblyPath);

        var result = await loader.LoadGrainAssemblyAsync(assemblyPath);

        if (!result.Success)
        {
            _logger.LogError("Failed to load dynamic grain assembly {Path}: {Errors}",
                assemblyPath,
                string.Join(", ", result.Errors));
        }
        else
        {
            _logger.LogInformation(
                "Loaded {Count} dynamic grain types from {Path} in {Duration}ms. Manifest version: {ManifestVersion}",
                result.GrainTypes.Count,
                assemblyPath,
                result.LoadDuration.TotalMilliseconds,
                result.NewManifestVersion);
        }
    }

    /// <summary>
    /// Starts the Orion node and runs until cancellation
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting Orion node...");

        // Start the host first
        await _host.StartAsync(cancellationToken);

        // Load dynamic grains after the silo is running
        await LoadDynamicGrainsAsync(cancellationToken);

        _logger.LogInformation("Orion node is running. Press Ctrl+C to stop.");

        // Wait for shutdown
        await _host.WaitForShutdownAsync(cancellationToken);
    }

    /// <summary>
    /// Stops the Orion node
    /// </summary>
    public async Task StopAsync()
    {
        _logger.LogInformation("Stopping Orion node...");
        await _host.StopAsync();
        _host.Dispose();
    }
}
