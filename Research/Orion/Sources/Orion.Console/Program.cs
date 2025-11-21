using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orleans;
using Orleans.Hosting;
using Orion.Core;
using Orion.Console.Logging;
using Orion.DynamicKvGrains;

namespace Orion.Console;

internal class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Orion Console (ocon) - Orleans-based distributed system");

        // ============================================================
        // run command - starts the Orion node (silo)
        // ============================================================
        var runCommand = new Command("run", "Start the Orion node (Orleans silo)");
        runCommand.SetHandler(async () =>
        {
            await RunNodeAsync(args);
        });
        rootCommand.AddCommand(runCommand);

        // ============================================================
        // ping command - calls IPingGrain
        // ============================================================
        var pingCommand = new Command("ping", "Ping a grain to test connectivity");

        var pingKeyOption = new Option<string>(
            name: "--key",
            description: "Grain key to ping",
            getDefaultValue: () => "default");
        pingCommand.AddOption(pingKeyOption);

        var pingMessageOption = new Option<string>(
            name: "--message",
            description: "Message to send",
            getDefaultValue: () => "Hello from ocon!");
        pingCommand.AddOption(pingMessageOption);

        pingCommand.SetHandler(async (string key, string message) =>
        {
            await RunPingAsync(key, message);
        }, pingKeyOption, pingMessageOption);

        rootCommand.AddCommand(pingCommand);

        // ============================================================
        // kv command - dynamic KV grain operations (Batch #3)
        // ============================================================
        var kvCommand = new Command("kv", "Set or get a key/value in the dynamic KV grain store");

        var keyOption = new Option<string>(
            name: "--key",
            description: "Key to address (grain primary key string)")
        {
            IsRequired = true
        };

        var valueOption = new Option<string?>(
            name: "--value",
            description: "Value to set. If omitted, the existing value is read")
        {
            IsRequired = false
        };

        kvCommand.AddOption(keyOption);
        kvCommand.AddOption(valueOption);

        kvCommand.SetHandler(async (string key, string? value) =>
        {
            await RunKvAsync(key, value);
        }, keyOption, valueOption);

        rootCommand.AddCommand(kvCommand);

        return await rootCommand.InvokeAsync(args);
    }

    /// <summary>
    /// Helper to run code with an Orleans client
    /// </summary>
    private static async Task RunWithClientAsync(Func<IClusterClient, Task> work)
    {
        using var host = Host.CreateDefaultBuilder()
            .UseOrleansClient(client =>
            {
                client.UseLocalhostClustering(
                    gatewayPort: 30000,
                    serviceId: "Orion",
                    clusterId: "orion-dev");
            })
            .Build();

        await host.StartAsync();

        try
        {
            var client = host.Services.GetRequiredService<IClusterClient>();
            await work(client);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    /// <summary>
    /// Runs the Orion node (silo)
    /// </summary>
    private static async Task RunNodeAsync(string[] args)
    {
        System.Console.WriteLine("Starting Orion node...");

        try
        {
            var node = new OrionNode(args);
            await node.RunAsync();
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"Error running Orion node: {ex.Message}");
            System.Console.WriteLine(ex.ToString());
        }
    }

    /// <summary>
    /// Runs the ping command - connects as Orleans client and calls IPingGrain
    /// </summary>
    private static async Task RunPingAsync(string key, string message)
    {
        System.Console.WriteLine($"Pinging grain '{key}' with message '{message}'...");

        try
        {
            await RunWithClientAsync(async client =>
            {
                var pingGrain = client.GetGrain<IPingGrain>(key);
                var response = await pingGrain.PingAsync(message);

                System.Console.WriteLine($"Response: {response}");

                var count = await pingGrain.GetPingCountAsync();
                System.Console.WriteLine($"Total ping count: {count}");
            });
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"Error during ping: {ex.Message}");
            System.Console.WriteLine(ex.ToString());
        }
    }

    /// <summary>
    /// Runs the KV command - connects as Orleans client and calls IKvGrain (dynamic grain)
    /// </summary>
    private static async Task RunKvAsync(string key, string? value)
    {
        System.Console.WriteLine(value is null
            ? $"Reading key '{key}'..."
            : $"Setting key '{key}' to '{value}'...");

        try
        {
            await RunWithClientAsync(async client =>
            {
                var kvGrain = client.GetGrain<IKvGrain>(key);

                if (value is not null)
                {
                    await kvGrain.SetAsync(value);
                    System.Console.WriteLine($"Set key '{key}' to '{value}'");
                }
                else
                {
                    var current = await kvGrain.GetAsync();
                    System.Console.WriteLine(
                        current is null
                            ? $"Key '{key}' has no value (null)."
                            : $"Value for key '{key}': {current}");
                }
            });
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"Error during KV operation: {ex.Message}");
            System.Console.WriteLine(ex.ToString());
        }
    }
}
