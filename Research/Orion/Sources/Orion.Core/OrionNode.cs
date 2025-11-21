using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Configuration;
using Orleans.Hosting;
using Orleans.Runtime;
using Orleans.Runtime.DynamicGrains;
using Orleans.Providers.RavenDb.StorageProviders;

namespace Orion.Core;

/// <summary>
/// Orion node - hosts an Orleans silo with default Microsoft DI
/// </summary>
public class OrionNode
{
    private readonly IHost _host;
    private readonly ILogger<OrionNode> _logger;

    public OrionNode(string[] args)
    {
        var builder = Host.CreateDefaultBuilder(args);

        // Use default Microsoft.Extensions.DependencyInjection container
        // Configure Orleans silo
        builder.UseOrleans(ConfigureOrleans);

        _host = builder.Build();
        _logger = _host.Services.GetRequiredService<ILogger<OrionNode>>();
    }

    private void ConfigureOrleans(ISiloBuilder silo)
    {
        // Localhost clustering for development
        silo.UseLocalhostClustering(
            siloPort: 11111,
            gatewayPort: 30000,
            primarySiloEndpoint: null,
            serviceId: "Orion",
            clusterId: "orion-dev");

        // RavenDB grain storage
        silo.AddRavenDbGrainStorage("OrionStore", options =>
        {
            options.Urls = new[] { "http://127.0.0.1:38880" };
            options.DatabaseName = "Orion";
        });

        // NOTE: Reminders are intentionally disabled for now.
        // The current Orleans fork has incompatible GrainService constructor
        // changes that break both RavenDB and in-memory reminder implementations.
        // We'll revisit reminders in a dedicated batch once the fork is aligned.

        // Dynamic grain loading (Batch #3 requirement)
        DynamicGrainLoadingExtensions.AddDynamicGrainLoading(silo);
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
