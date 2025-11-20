# Building VISORA Hosts - Step-by-Step Guide

**Last Updated:** 2025-11-10
**VISORA Version:** .NET 9.0
**Skill Level:** Advanced
**Estimated Time:** 60-120 minutes

---

## Table of Contents

1. [Overview](#overview)
2. [Prerequisites](#prerequisites)
3. [Host Fundamentals](#host-fundamentals)
4. [Step-by-Step Implementation](#step-by-step-implementation)
5. [ModuleCatalog Setup](#modulecatalog-setup)
6. [Capability Provider Construction](#capability-provider-construction)
7. [Discovery and Initialization Flow](#discovery-and-initialization-flow)
8. [Command Execution](#command-execution)
9. [CLI vs UI Host Patterns](#cli-vs-ui-host-patterns)
10. [Testing Hosts](#testing-hosts)
11. [Common Mistakes](#common-mistakes)
12. [Advanced Topics](#advanced-topics)
13. [Best Practices](#best-practices)
14. [Reference Examples](#reference-examples)
15. [Cross-References](#cross-references)

---

## Overview

### What is a VISORA Host?

A **host** is an application that loads and manages VISORA modules. Hosts:

- Discover modules from configured locations
- Load modules into isolated contexts
- Provide capabilities (APIs) to modules
- Initialize and manage module lifecycles
- Execute commands from modules
- Handle module unloading and cleanup

**Key Characteristics:**
- **Module orchestrator**: Manages module discovery, loading, initialization
- **Capability provider**: Provides APIs and services to modules
- **Execution environment**: Provides CLI, Terminal UI, or WPF interface
- **Lifecycle manager**: Handles startup, shutdown, and hot-swapping

### Host Architecture

```
┌─────────────────────────────────────────────────┐
│                  Host Application                │
├─────────────────────────────────────────────────┤
│  1. ModuleCatalog                                │
│     ├─ Discovery configuration                  │
│     ├─ Module loading                           │
│     └─ Module registry                          │
│                                                  │
│  2. Capability Provider                          │
│     ├─ ILogger                                   │
│     ├─ IFileSystem                               │
│     ├─ IConsole                                  │
│     └─ Custom capabilities                       │
│                                                  │
│  3. Module Initialization                        │
│     ├─ Call InitializeAsync on modules          │
│     ├─ Discover components                      │
│     ├─ Collect commands                         │
│     └─ Build command registry                   │
│                                                  │
│  4. Execution Engine                             │
│     ├─ CLI command parser                       │
│     ├─ REPL loop (Terminal UI)                  │
│     ├─ Menu handlers (WPF)                      │
│     └─ Command dispatcher                       │
│                                                  │
│  5. Cleanup                                      │
│     ├─ Call ShutdownAsync on modules            │
│     ├─ Dispose modules                          │
│     └─ Unload contexts                          │
└─────────────────────────────────────────────────┘
```

### Host Types

| Host Type | Surface | Use Case |
|-----------|---------|----------|
| **CLI** | `Surface.Cli` | Command-line tools, automation |
| **Terminal** | `Surface.Terminal` | Interactive terminal UI (TUI) |
| **WPF** | `Surface.Wpf` | Full GUI application |
| **Custom** | `Surface.Custom` | Specialized environments |

---

## Prerequisites

### Required Knowledge

- **VISORA architecture**: Understanding of modules, components, commands
- **Async programming**: Task, ValueTask, CancellationToken
- **Dependency injection**: Service providers, capability patterns
- **.NET hosting**: Application lifecycle, configuration

### Required References

```xml
<ItemGroup>
  <ProjectReference Include="..\Visora.Contracts\Visora.Contracts.csproj" />
  <ProjectReference Include="..\Visora.Core\Visora.Core.csproj" />
</ItemGroup>
```

Additional packages (depending on host type):
- **CLI**: `System.CommandLine`
- **Terminal UI**: Terminal UI framework
- **WPF**: WPF framework packages

---

## Host Fundamentals

### Host Responsibilities

A VISORA host must:

1. **Configure discovery**: Specify where to find modules (`.vixm.dll` files)
2. **Create catalog**: Instantiate and configure `ModuleCatalog`
3. **Build capabilities**: Create `ICapabilityProvider` with host-provided APIs
4. **Discover modules**: Scan configured locations for modules
5. **Initialize modules**: Call `InitializeAsync` on each module
6. **Discover components**: Collect components from each module
7. **Register commands**: Build command registry from all components
8. **Execute commands**: Dispatch user input to appropriate commands
9. **Handle shutdown**: Gracefully shutdown and dispose modules

### Core Types

**ModuleCatalog**: Manages module discovery and loading

**File**: `/src/Visora.Core/Modules/ModuleCatalog.cs`

```csharp
public sealed class ModuleCatalog : IAsyncDisposable
{
    public IReadOnlyList<ModuleHandle> Modules { get; }

    public async Task DiscoverAsync(
        ModuleCatalogOptions options,
        CancellationToken cancellationToken = default);

    public ModuleHandle? GetById(string moduleId);

    public async ValueTask DisposeAsync();
}
```

**ModuleHandle**: Represents a loaded module

```csharp
public sealed class ModuleHandle : IAsyncDisposable
{
    public string AssemblyPath { get; }
    public ModuleDescriptor Descriptor { get; }
    public VisoraModule Module { get; }

    public async Task<ModuleInspection> InspectAsync(
        CancellationToken cancellationToken = default);

    public async ValueTask DisposeAsync();
}
```

**ICapabilityProvider**: Provides capabilities to modules

**File**: `/src/Visora.Contracts/Common/ICapabilityProvider.cs`

```csharp
public interface ICapabilityProvider
{
    bool TryGet<TCapability>(out TCapability? capability)
        where TCapability : class;
}
```

---

## Step-by-Step Implementation

### Step 1: Create Host Project

```bash
# Create console application
dotnet new console -n MyHost -f net9.0
cd MyHost

# Add references
dotnet add reference ../Visora.Contracts/Visora.Contracts.csproj
dotnet add reference ../Visora.Core/Visora.Core.csproj
```

### Step 2: Create Basic Host Structure

**File**: `Program.cs`

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using Visora.Core.Modules;

namespace MyHost;

internal static class Program
{
    static async Task<int> Main(string[] args)
    {
        Console.WriteLine("MyHost starting...");

        try
        {
            await using var host = new MyHostApplication();
            await host.InitializeAsync(CancellationToken.None);
            await host.RunAsync(CancellationToken.None);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fatal error: {ex.Message}");
            return 1;
        }
    }
}
```

### Step 3: Implement Host Application Class

**File**: `MyHostApplication.cs`

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using Visora.Contracts;
using Visora.Core.Modules;

namespace MyHost;

internal sealed class MyHostApplication : IAsyncDisposable
{
    private ModuleCatalog? _catalog;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("Initializing host...");

        // Step 1: Create capability provider
        var capabilities = CreateCapabilityProvider();

        // Step 2: Configure catalog options
        var options = new ModuleCatalogOptions
        {
            Capabilities = capabilities,
            Services = null, // No DI container in this simple example
            RecurseSubdirectories = true
        };

        // Add probing paths
        options.ProbingPaths.Add(AppContext.BaseDirectory);
        options.ProbingPaths.Add(Path.Combine(AppContext.BaseDirectory, "modules"));

        // Step 3: Create and discover modules
        _catalog = new ModuleCatalog();
        await _catalog.DiscoverAsync(options, cancellationToken);

        Console.WriteLine($"Discovered {_catalog.Modules.Count} modules");

        // Step 4: Initialize each module
        foreach (var handle in _catalog.Modules)
        {
            var context = new ModuleContext(
                descriptor: handle.Descriptor,
                services: null,
                capabilities: capabilities,
                properties: new Dictionary<string, object?>
                {
                    ["HostName"] = "MyHost",
                    ["HostVersion"] = new Version(1, 0, 0),
                    ["ModulePath"] = Path.GetDirectoryName(handle.AssemblyPath)
                });

            await handle.Module.InitializeAsync(context, cancellationToken);
            Console.WriteLine($"  Initialized: {handle.Descriptor.Name}");
        }
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("Host running. Press Ctrl+C to exit.");

        // Simple wait loop (replace with actual execution logic)
        var tcs = new TaskCompletionSource();
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            tcs.TrySetResult();
        };

        await tcs.Task;
    }

    private ICapabilityProvider CreateCapabilityProvider()
    {
        // Create basic capability provider
        // We'll expand this in the next section
        return new BasicCapabilityProvider();
    }

    public async ValueTask DisposeAsync()
    {
        Console.WriteLine("Shutting down host...");

        if (_catalog != null)
        {
            await _catalog.DisposeAsync();
        }
    }
}
```

### Step 4: Implement Basic Capability Provider

**File**: `BasicCapabilityProvider.cs`

```csharp
using System;
using System.Collections.Generic;
using Visora.Contracts;

namespace MyHost;

internal sealed class BasicCapabilityProvider : ICapabilityProvider
{
    private readonly Dictionary<Type, object> _capabilities = new();

    public BasicCapabilityProvider()
    {
        // Register basic capabilities
        _capabilities[typeof(IConsoleOutput)] = new ConsoleOutput();
        _capabilities[typeof(IFileSystemAccess)] = new FileSystemAccess();
    }

    public bool TryGet<TCapability>(out TCapability? capability)
        where TCapability : class
    {
        if (_capabilities.TryGetValue(typeof(TCapability), out var instance))
        {
            capability = instance as TCapability;
            return capability != null;
        }

        capability = null;
        return false;
    }

    public void Register<TCapability>(TCapability instance)
        where TCapability : class
    {
        _capabilities[typeof(TCapability)] = instance;
    }
}

// Example capability interfaces
internal interface IConsoleOutput
{
    void WriteLine(string message);
}

internal sealed class ConsoleOutput : IConsoleOutput
{
    public void WriteLine(string message) => Console.WriteLine(message);
}

internal interface IFileSystemAccess
{
    bool FileExists(string path);
    string ReadAllText(string path);
}

internal sealed class FileSystemAccess : IFileSystemAccess
{
    public bool FileExists(string path) => File.Exists(path);
    public string ReadAllText(string path) => File.ReadAllText(path);
}
```

### Step 5: Build and Test

```bash
dotnet build
dotnet run
```

Expected output:
```
MyHost starting...
Initializing host...
Discovered 2 modules
  Initialized: My Feature Module
  Initialized: Visora Shell Commands
Host running. Press Ctrl+C to exit.
```

---

## ModuleCatalog Setup

### ModuleCatalogOptions Configuration

**File**: `/src/Visora.Core/Modules/ModuleCatalogOptions.cs`

```csharp
public sealed class ModuleCatalogOptions
{
    // Capability provider (required)
    public ICapabilityProvider Capabilities { get; set; }

    // Optional DI container
    public IServiceProvider? Services { get; set; }

    // Host properties passed to modules
    public IReadOnlyDictionary<string, object?>? Properties { get; set; }

    // Search paths for modules
    public List<string> ProbingPaths { get; } = new();

    // Explicit module files
    public List<string> ExplicitModuleFiles { get; } = new();

    // Recurse subdirectories?
    public bool RecurseSubdirectories { get; set; } = true;
}
```

### Configuring Search Paths

```csharp
var options = new ModuleCatalogOptions
{
    Capabilities = capabilities,
    RecurseSubdirectories = true
};

// Add default probing paths
options.ProbingPaths.Add(AppContext.BaseDirectory);
options.ProbingPaths.Add(Path.Combine(AppContext.BaseDirectory, "modules"));

// Add user-specified paths
options.ProbingPaths.Add(Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    ".myhost", "modules"));

// Add environment variable path
var customPath = Environment.GetEnvironmentVariable("MYHOST_MODULES");
if (!string.IsNullOrEmpty(customPath))
{
    options.ProbingPaths.Add(customPath);
}
```

### Explicit Module Files

```csharp
// Load specific modules
options.ExplicitModuleFiles.Add("/path/to/MyModule.vixm.dll");
options.ExplicitModuleFiles.Add("/path/to/OtherModule.vixm.dll");
```

### Discovery Pattern

```csharp
await using var catalog = new ModuleCatalog();

try
{
    await catalog.DiscoverAsync(options, cancellationToken);
    Console.WriteLine($"Discovered {catalog.Modules.Count} modules:");

    foreach (var handle in catalog.Modules)
    {
        Console.WriteLine($"  - {handle.Descriptor.Name} v{handle.Descriptor.Version}");
        Console.WriteLine($"    Path: {handle.AssemblyPath}");
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Module discovery failed: {ex.Message}");
    throw;
}
```

---

## Capability Provider Construction

### Capability Provider Pattern

Capabilities are host-provided APIs that modules can optionally use:

```csharp
// In module code
var logger = context.Capabilities.GetOptional<ILogger>();
logger?.LogInformation("Module starting");

var fileSystem = context.Capabilities.GetRequired<IFileSystem>();
```

### Building a Comprehensive Capability Provider

```csharp
public sealed class HostCapabilityProvider : ICapabilityProvider
{
    private readonly Dictionary<Type, object> _capabilities = new();

    public HostCapabilityProvider()
    {
        RegisterCoreCapabilities();
        RegisterExtendedCapabilities();
    }

    private void RegisterCoreCapabilities()
    {
        // Logging
        _capabilities[typeof(ILogger)] = CreateLogger();

        // File system
        _capabilities[typeof(IFileSystem)] = new FileSystemCapability();

        // Console I/O
        _capabilities[typeof(IConsole)] = new ConsoleCapability();

        // HTTP client
        _capabilities[typeof(HttpClient)] = new HttpClient();
    }

    private void RegisterExtendedCapabilities()
    {
        // Progress reporting
        _capabilities[typeof(IProgress<double>)] = new Progress<double>(OnProgress);

        // Configuration
        _capabilities[typeof(IConfiguration)] = LoadConfiguration();

        // Host information
        _capabilities[typeof(IHostInfo)] = new HostInfo
        {
            Name = "MyHost",
            Version = new Version(1, 0, 0),
            Surface = Surface.Cli
        };
    }

    public bool TryGet<TCapability>(out TCapability? capability)
        where TCapability : class
    {
        if (_capabilities.TryGetValue(typeof(TCapability), out var instance))
        {
            capability = instance as TCapability;
            return capability != null;
        }

        capability = null;
        return false;
    }

    public void Register<TCapability>(TCapability instance)
        where TCapability : class
    {
        _capabilities[typeof(TCapability)] = instance;
    }

    public void Unregister<TCapability>() where TCapability : class
    {
        _capabilities.Remove(typeof(TCapability));
    }

    private ILogger CreateLogger()
    {
        // Return actual logger implementation
        return new ConsoleLogger();
    }

    private IConfiguration LoadConfiguration()
    {
        // Load configuration from file or environment
        return new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
    }

    private void OnProgress(double value)
    {
        Console.WriteLine($"Progress: {value:P0}");
    }
}
```

### Common Capabilities to Provide

| Capability | Purpose | Example |
|------------|---------|---------|
| **ILogger** | Logging | `Microsoft.Extensions.Logging.ILogger` |
| **IFileSystem** | File I/O | Custom abstraction over `System.IO` |
| **IConsole** | Console I/O | Reading/writing to console |
| **HttpClient** | HTTP requests | Standard `HttpClient` |
| **IConfiguration** | Configuration | `Microsoft.Extensions.Configuration` |
| **IProgress<T>** | Progress reporting | Standard `IProgress<T>` |
| **IHostInfo** | Host metadata | Custom interface with host details |

---

## Discovery and Initialization Flow

### Complete Initialization Sequence

```csharp
public async Task InitializeHostAsync(CancellationToken cancellationToken)
{
    // 1. Create capability provider
    var capabilities = new HostCapabilityProvider();
    var logger = capabilities.GetRequired<ILogger>();

    logger.LogInformation("Starting module discovery...");

    // 2. Configure catalog
    var options = new ModuleCatalogOptions
    {
        Capabilities = capabilities,
        Services = null,
        Properties = new Dictionary<string, object?>
        {
            ["HostName"] = "MyHost",
            ["HostVersion"] = new Version(1, 0, 0),
            ["StartTime"] = DateTimeOffset.UtcNow
        }
    };

    ConfigureSearchPaths(options);

    // 3. Discover modules
    _catalog = new ModuleCatalog();
    await _catalog.DiscoverAsync(options, cancellationToken);

    logger.LogInformation("Discovered {Count} modules", _catalog.Modules.Count);

    // 4. Initialize modules
    foreach (var handle in _catalog.Modules)
    {
        logger.LogInformation("Initializing module: {Name}", handle.Descriptor.Name);

        var context = CreateModuleContext(handle, capabilities, options.Properties);

        try
        {
            await handle.Module.InitializeAsync(context, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize module: {Name}", handle.Descriptor.Name);
            // Decide: throw or continue with other modules?
            throw;
        }
    }

    // 5. Discover and register commands
    await DiscoverAndRegisterCommandsAsync(cancellationToken);

    logger.LogInformation("Host initialization complete");
}

private ModuleContext CreateModuleContext(
    ModuleHandle handle,
    ICapabilityProvider capabilities,
    IReadOnlyDictionary<string, object?>? properties)
{
    var moduleProperties = new Dictionary<string, object?>(properties ?? new Dictionary<string, object?>())
    {
        ["ModulePath"] = Path.GetDirectoryName(handle.AssemblyPath),
        ["ModuleFileName"] = Path.GetFileName(handle.AssemblyPath)
    };

    return new ModuleContext(
        descriptor: handle.Descriptor,
        services: null,
        capabilities: capabilities,
        properties: moduleProperties);
}

private async Task DiscoverAndRegisterCommandsAsync(CancellationToken cancellationToken)
{
    _commandRegistry = new CommandRegistry();

    foreach (var handle in _catalog!.Modules)
    {
        // Discover components
        var discoveryContext = new ModuleDiscoveryContext(
            moduleAssembly: handle.GetType().Assembly,
            capabilities: _capabilities);

        var componentTypes = handle.Module.DiscoverComponents(discoveryContext);

        // Instantiate and initialize components
        foreach (var componentType in componentTypes)
        {
            var component = (VisoraComponent)Activator.CreateInstance(componentType)!;

            var componentContext = new ComponentContext(
                module: handle.Module,
                descriptor: component.Descriptor,
                capabilities: _capabilities,
                surface: Surface.Cli,
                properties: new Dictionary<string, object?>());

            await component.InitializeAsync(componentContext, cancellationToken);

            // Get commands
            var commands = component.CreateCommands(componentContext);
            foreach (var command in commands)
            {
                _commandRegistry.Register(command);
            }
        }
    }
}
```

---

## Command Execution

### Command Registry

```csharp
public sealed class CommandRegistry
{
    private readonly Dictionary<string, VisoraCommand> _commands = new();
    private readonly Dictionary<string, string> _aliases = new();

    public void Register(VisoraCommand command)
    {
        var id = command.Descriptor.Id;
        _commands[id] = command;

        // Register aliases
        if (command.Descriptor.Aliases != null)
        {
            foreach (var alias in command.Descriptor.Aliases)
            {
                _aliases[alias] = id;
            }
        }
    }

    public VisoraCommand? Resolve(string commandIdOrAlias)
    {
        // Try direct lookup
        if (_commands.TryGetValue(commandIdOrAlias, out var command))
        {
            return command;
        }

        // Try alias lookup
        if (_aliases.TryGetValue(commandIdOrAlias, out var actualId))
        {
            return _commands.GetValueOrDefault(actualId);
        }

        return null;
    }

    public IReadOnlyList<VisoraCommand> GetAllCommands()
    {
        return _commands.Values.ToList();
    }
}
```

### Executing Commands

```csharp
public async Task<CommandResult> ExecuteCommandAsync(
    string commandId,
    Dictionary<string, object?> parameters,
    CancellationToken cancellationToken)
{
    // Resolve command
    var command = _commandRegistry.Resolve(commandId);
    if (command == null)
    {
        return CommandResult.Failure($"Command not found: {commandId}");
    }

    // Find owning component and module
    var (module, component) = FindOwners(command);
    if (module == null || component == null)
    {
        return CommandResult.Failure("Cannot determine command ownership");
    }

    // Create execution context
    var context = new CommandContext(
        module: module,
        component: component,
        descriptor: command.Descriptor,
        surface: Surface.Cli,
        capabilities: _capabilities,
        parameters: parameters,
        properties: new Dictionary<string, object?>());

    // Execute
    try
    {
        var result = await command.ExecuteAsync(context, cancellationToken);
        return result;
    }
    catch (OperationCanceledException)
    {
        return CommandResult.Cancelled("Command was cancelled");
    }
    catch (Exception ex)
    {
        _logger?.LogError(ex, "Command execution failed: {CommandId}", commandId);
        return CommandResult.Failure($"Execution error: {ex.Message}");
    }
}
```

---

## CLI vs UI Host Patterns

### CLI Host Pattern

CLI hosts execute single commands and exit:

```csharp
static async Task<int> Main(string[] args)
{
    // Parse command line
    if (args.Length == 0)
    {
        Console.WriteLine("Usage: myhost <command> [args...]");
        return 1;
    }

    var commandId = args[0];
    var commandArgs = args.Skip(1).ToArray();

    // Initialize host
    await using var host = new MyHostApplication();
    await host.InitializeAsync(CancellationToken.None);

    // Execute command
    var parameters = ParseArguments(commandArgs);
    var result = await host.ExecuteCommandAsync(commandId, parameters, CancellationToken.None);

    // Display result
    if (result.IsSuccess)
    {
        Console.WriteLine(result.Message);
        return 0;
    }
    else
    {
        Console.Error.WriteLine($"Error: {result.Message}");
        return 1;
    }
}
```

### Terminal UI Host Pattern (REPL)

Terminal UI hosts run a Read-Eval-Print Loop:

```csharp
public async Task RunReplAsync(CancellationToken cancellationToken)
{
    Console.WriteLine("MyHost Interactive Shell");
    Console.WriteLine("Type 'help' for commands, 'exit' to quit");

    while (!cancellationToken.IsCancellationRequested)
    {
        Console.Write("> ");
        var input = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(input))
            continue;

        if (input.Equals("exit", StringComparison.OrdinalIgnoreCase))
            break;

        if (input.Equals("help", StringComparison.OrdinalIgnoreCase))
        {
            DisplayHelp();
            continue;
        }

        // Parse input
        var parts = input.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var commandId = parts[0];
        var args = parts.Length > 1 ? ParseArguments(parts[1]) : new Dictionary<string, object?>();

        // Execute
        var result = await ExecuteCommandAsync(commandId, args, cancellationToken);

        // Display result
        Console.WriteLine(result.IsSuccess ? result.Message : $"Error: {result.Message}");
    }
}
```

### WPF Host Pattern

WPF hosts integrate with UI events:

```csharp
public class MainWindow : Window
{
    private readonly MyHostApplication _host;

    public MainWindow()
    {
        InitializeComponent();
        _host = new MyHostApplication();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await _host.InitializeAsync(CancellationToken.None);
        PopulateCommandMenu();
    }

    private async void MenuItem_Click(object sender, RoutedEventArgs e)
    {
        var menuItem = (MenuItem)sender;
        var commandId = menuItem.Tag as string;

        if (commandId == null) return;

        // Execute command
        var result = await _host.ExecuteCommandAsync(
            commandId,
            new Dictionary<string, object?>(),
            CancellationToken.None);

        // Display result
        MessageBox.Show(result.Message, "Command Result");
    }

    private void PopulateCommandMenu()
    {
        var commands = _host.GetAllCommands();
        foreach (var command in commands)
        {
            var menuItem = new MenuItem
            {
                Header = command.Descriptor.Title,
                Tag = command.Descriptor.Id
            };
            menuItem.Click += MenuItem_Click;
            CommandMenu.Items.Add(menuItem);
        }
    }
}
```

---

## Testing Hosts

### Integration Testing

```csharp
public class HostIntegrationTests
{
    [Fact]
    public async Task Host_InitializesSuccessfully()
    {
        // Arrange
        await using var host = new MyHostApplication();

        // Act
        await host.InitializeAsync(CancellationToken.None);

        // Assert
        var modules = host.GetLoadedModules();
        Assert.NotEmpty(modules);
    }

    [Fact]
    public async Task Host_ExecutesCommand_Successfully()
    {
        // Arrange
        await using var host = new MyHostApplication();
        await host.InitializeAsync(CancellationToken.None);

        // Act
        var result = await host.ExecuteCommandAsync(
            "shell.ping",
            new Dictionary<string, object?>(),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Contains("Pong", result.Message);
    }
}
```

---

## Common Mistakes

### Mistake 1: Not Disposing Catalog

**Problem:**
```csharp
var catalog = new ModuleCatalog();
await catalog.DiscoverAsync(options);
// Forgot to dispose  ❌
```

**Solution:**
```csharp
await using var catalog = new ModuleCatalog();  ✅
await catalog.DiscoverAsync(options);
```

### Mistake 2: Empty Capability Provider

**Problem:**
```csharp
var capabilities = CapabilityProviders.Empty;  ❌
// Modules can't access any host APIs
```

**Solution:**
```csharp
var capabilities = new HostCapabilityProvider();  ✅
// Provide useful capabilities
```

### Mistake 3: Not Initializing Modules

**Problem:**
```csharp
await catalog.DiscoverAsync(options);
// Forgot to call InitializeAsync on modules  ❌
```

**Solution:**
```csharp
foreach (var handle in catalog.Modules)
{
    await handle.Module.InitializeAsync(context, cancellationToken);  ✅
}
```

---

## Best Practices

### 1. Graceful Shutdown

```csharp
public async ValueTask DisposeAsync()
{
    if (_catalog != null)
    {
        foreach (var handle in _catalog.Modules)
        {
            try
            {
                var context = CreateModuleContext(handle);
                await handle.Module.ShutdownAsync(context, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error shutting down module: {Name}",
                    handle.Descriptor.Name);
            }
        }

        await _catalog.DisposeAsync();
    }
}
```

### 2. Error Handling

```csharp
foreach (var handle in catalog.Modules)
{
    try
    {
        await handle.Module.InitializeAsync(context, cancellationToken);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Module initialization failed: {Name}", handle.Descriptor.Name);
        // Decide: continue with other modules or fail fast?
    }
}
```

### 3. Capability Versioning

```csharp
// Provide versioned capabilities for backward compatibility
capabilities.Register<IFileSystemV1>(new FileSystemV1());
capabilities.Register<IFileSystemV2>(new FileSystemV2());
```

---

## Reference Examples

### Complete CLI Host (Simplified)

**Reference**: `/src/Visora.CLI/Program.cs`

The VISORA CLI host demonstrates:
- Module discovery from multiple paths
- Command-line parsing with System.CommandLine
- Module inspection
- Module deployment

---

## Cross-References

### Related Documentation

- **[Creating Modules](./creating-modules.md)**: Module fundamentals
- **[Testing Strategies](./testing-strategies.md)**: Testing guide
- **[Plugin Architecture Pattern](../patterns/plugin-architecture/visora-analysis.md)**: Deep dive
- **[Module Lifecycle Pattern](../patterns/module-lifecycle/visora-analysis.md)**: Lifecycle details
- **[Capability Negotiation Pattern](../patterns/capability-negotiation/visora-analysis.md)**: Capability design

### Related Decisions

- **[ADR-002: Capability vs Service Locator](../decisions/capability-vs-service-locator.md)**: Why capabilities
- **[ADR-004: Unloadable Plugins](../decisions/unloadable-plugins.md)**: Module unloading

---

**End of Document**
