# COMPREHENSIVE VISORA PLATFORM ANALYSIS

**Analysis Date:** November 10, 2025
**Framework:** .NET 9.0
**Architecture Style:** Modular Plugin-Based Platform with Reflection-First Discovery

---

## EXECUTIVE SUMMARY

VISORA is an extensible, multi-form platform that enables human developers and AI agents to collaborate on building IDE-like environments. The platform uses a sophisticated module system based on reflection-first discovery, pluggable components, and capability negotiation. It supports multiple execution contexts (CLI, Terminal UI, WPF) while maintaining a unified programming model.

**Key Distinction:** Unlike traditional IDEs with constrained extension points, VISORA treats every capability—shells, GUI panes, services, automations—as loadable modules with runtime negotiation of APIs and lifecycle.

---

## 1. OVERALL ARCHITECTURE & STRUCTURE

### 1.1 Solution Layout

The solution is organized into three tiers:

```
Visora.sln
├── Core Layer (Shared Infrastructure)
│   ├── Visora.Shared
│   ├── Visora.Shared.Windows
│   ├── Visora.Contracts
│   ├── Visora.Core
│   └── Visora.Core.Windows
├── Platform Hosts (Execution Environments)
│   ├── Visora.CLI (Command-line host)
│   ├── Visora.Shell (Shell foundation)
│   └── Visora.Terminal (WPF-based terminal)
├── Module System
│   ├── Visora.Shell.Commands.Core (Sample module)
│   └── Visora.CLI.Module (CLI as a module)
└── Windows Specific
    └── VISORA Windows (Integrated UI shell)
```

**Technology Stack:**
- **Runtime:** .NET 9.0
- **Plugin System:** McMaster.NETCore.Plugins v2.0.0
- **CLI Framework:** System.CommandLine v2.0.0-rc
- **UI Framework:** WPF with Actipro Docking (Terminal)
- **Compilation Target:** x64 primary (with multi-platform support)

### 1.2 Project Dependencies & Layer Separation

**Layer 1: Contracts (Foundation)**
- File: `/src/Visora.Contracts/Visora.Contracts.csproj`
- No external dependencies
- Defines all abstract types and interfaces
- Contains: Modules, Components, Commands, Contexts, Descriptors

**Layer 2: Core Infrastructure**
- File: `/src/Visora.Core/Visora.Core.csproj`
- Dependencies:
  - Visora.Contracts
  - Visora.Shared
  - McMaster.NETCore.Plugins (plugin loading)
- Implements: Module catalog, discovery, lifecycle management
- Contains: ModuleCatalog, ModuleHandle, ModuleLocator, CapabilityProviders

**Layer 3: Application Hosts**
- CLI: `/src/Visora.CLI/Visora.CLI.csproj`
  - Dependencies: Visora.Core, System.CommandLine
- Terminal: `/src/Visora.Terminal/Visora.Terminal.csproj`
  - Dependencies: Visora.Core, WPF framework, Actipro
- Shell: `/src/Visora.Shell/Visora.Shell.csproj`
  - Minimal implementation (scaffolding)

### 1.3 Architectural Patterns

**Pattern 1: Layered Architecture with Clear Separation**
- Contracts layer has NO dependencies on implementation
- Core depends only on Contracts (inversion of dependencies)
- Hosts depend on Core but can exist independently

**Pattern 2: Plugin-Based Extensibility**
- Uses `PluginLoader` for assembly isolation
- Modules are standard .NET assemblies with `.vixm.dll` naming convention
- Unloadable assembly contexts for hot-swapping
- Shared types list prevents duplication (see ModuleCatalogDefaults)

**Pattern 3: Reflection-First Discovery**
- No XML manifests or external configuration files
- Assembly scanning via `Assembly.GetTypes()`
- Convention-based naming (VisoraModule, VisoraComponent implementations)
- Self-describing modules through Descriptor objects

---

## 2. CORE PLATFORM PATTERNS

### 2.1 Module-Component-Command Hierarchy

```
VisoraModule (abstract class)
    ↓ Implements
    Module (Visora.Core.Module)
    ↓ Concrete instances
    ShellCommandsModule, CliModule (examples)
    
    Contains ↓
    
    VisoraComponent[] (discovered via DiscoverComponents)
    ↓ Implements
    Component (Visora.Core.Component)
    ↓ Concrete instances
    CoreUtilitiesComponent
    
    Creates ↓
    
    VisoraCommand[] (via CreateCommands)
    ↓ Implements
    PingCommand, EnvironmentInfoCommand, ModuleProbeCommand
```

**File References:**
- Base abstractions: `/src/Visora.Contracts/Modules/VisoraModule.cs`
- Base abstractions: `/src/Visora.Contracts/Components/VisoraComponent.cs`
- Base abstractions: `/src/Visora.Contracts/Commands/VisoraCommand.cs`
- Concrete implementations: `/src/Visora.Core/Module.cs`, `/src/Visora.Core/Component.cs`

### 2.2 Domain Modeling: Descriptors

VISORA uses **sealed record types** for immutable metadata:

**ModuleDescriptor** (`/src/Visora.Contracts/Modules/ModuleDescriptor.cs`)
```csharp
public sealed record ModuleDescriptor(
    string Id,                                    // Unique identifier
    string Name,                                  // Human-readable name
    Version Version,                              // SemVer version
    string? Description = null,
    IReadOnlyDictionary<string, string>? Tags = null,
    ModuleRuntimeHints? RuntimeHints = null)     // Deployment hints
{
    public static ModuleDescriptor Create(...) => new(...);
}
```

**ComponentDescriptor** (`/src/Visora.Contracts/Components/ComponentDescriptor.cs`)
```csharp
public sealed record ComponentDescriptor(
    string Id,
    string Name,
    string? Description = null,
    IReadOnlyCollection<string>? Tags = null,
    ComponentKind Kind = ComponentKind.Generic)   // Categorization
{
    public enum ComponentKind { Generic, Service, Ui, Console, ShellExtension }
}
```

**CommandDescriptor** (`/src/Visora.Contracts/Commands/CommandDescriptor.cs`)
```csharp
public sealed record CommandDescriptor(
    string Id,
    string Title,
    string? Description = null,
    CommandKind Kind = CommandKind.General,       // Categorization
    IReadOnlyCollection<string>? Aliases = null,
    IReadOnlyCollection<string>? Keywords = null,
    bool IsVisible = true,
    bool IsInstanceScoped = false,
    CommandUiHint? Ui = null)                     // UI hint for menus
{
    public enum CommandKind { General, Navigation, Tool, Shell, Automation }
    public sealed record CommandUiHint(
        string? MenuPath = null,
        string? Icon = null,
        string? DefaultGesture = null);
}
```

**Key Pattern:** All descriptors use factory Create() methods for consistent construction.

### 2.3 Entity Design Patterns

**Context Objects** - Pass rich runtime state to lifecycle methods:

**ModuleContext** (`/src/Visora.Contracts/Modules/ModuleContext.cs`)
```csharp
public sealed class ModuleContext
{
    public ModuleDescriptor Descriptor { get; }      // Self-description
    public IServiceProvider? Services { get; }       // DI container
    public ICapabilityProvider Capabilities { get; } // Runtime APIs
    public IReadOnlyDictionary<string, object?> Properties { get; } // Host metadata
}
```

**ComponentContext** (`/src/Visora.Contracts/Components/ComponentContext.cs`)
```csharp
public sealed class ComponentContext
{
    public VisoraModule Module { get; }             // Parent module
    public ICapabilityProvider Capabilities { get; } // Runtime APIs
}
```

**CommandContext** (`/src/Visora.Contracts/Commands/CommandContext.cs`)
```csharp
public sealed class CommandContext
{
    public VisoraModule Module { get; }
    public VisoraComponent? Component { get; }      // Optional parent component
    public CommandSurface Surface { get; }          // Invocation channel
    public ICapabilityProvider Capabilities { get; }
    public IReadOnlyDictionary<string, object?> Parameters { get; }
    public CancellationToken CancellationToken { get; }
    
    public enum CommandSurface { Programmatic, TextShell, Ui, Automation, Remote }
}
```

### 2.4 Repository/Catalog Pattern

**ModuleCatalog** (`/src/Visora.Core/Modules/ModuleCatalog.cs`)
```csharp
public sealed class ModuleCatalog : IAsyncDisposable
{
    private readonly List<ModuleHandle> _modules = new();
    
    public IReadOnlyList<ModuleHandle> Modules => _modules;
    
    public async Task DiscoverAsync(ModuleCatalogOptions options, 
        CancellationToken cancellationToken = default)
    {
        // Uses ModuleLocator to find candidates
        // Uses ModuleHandle.LoadAsync to materialize each
        // Maintains idempotence (no duplicates)
    }
    
    public ModuleHandle? GetById(string moduleId) => ...
    
    public async ValueTask DisposeAsync() => ...
}
```

**Key Features:**
- Async-first lifecycle (DiscoverAsync, DisposeAsync)
- Respects cancellation tokens throughout
- Maintains module handles for lifecycle management
- Deduplication by path

### 2.5 Service Layer Pattern

**ModuleHandle** (`/src/Visora.Core/Modules/ModuleHandle.cs`) - Wraps a loaded module assembly:

```csharp
public sealed class ModuleHandle : IAsyncDisposable
{
    public string AssemblyPath { get; }
    public Assembly Assembly { get; }
    public VisoraModule Module { get; }
    public ModuleDescriptor Descriptor { get; }
    
    public static Task<ModuleHandle> LoadAsync(string assemblyPath, 
        ModuleCatalogOptions options, CancellationToken cancellationToken)
    {
        // Reflection-based discovery of VisoraModule implementation
        // Singleton instantiation via Activator.CreateInstance
        // Context creation with capabilities & services
    }
    
    public async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        // Idempotent initialization
        // Calls Module.InitializeAsync with ModuleContext
    }
    
    public async Task<ModuleInspection> InspectAsync(CancellationToken cancellationToken = default)
    {
        // Deep inspection: discovers components, instantiates them
        // Collects command metadata without executing
        // Properly cleans up temporary instances
    }
    
    public async Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        // Calls Module.ShutdownAsync
    }
}

// Inspection result records:
public sealed record ModuleInspection(
    string AssemblyPath,
    ModuleDescriptor Descriptor,
    IReadOnlyList<ComponentInspection> Components);

public sealed record ComponentInspection(
    Type ComponentType,
    ComponentDescriptor Descriptor,
    IReadOnlyList<CommandInspection> Commands);

public sealed record CommandInspection(
    CommandDescriptor Descriptor,
    Type CommandType,
    Type DeclaringComponentType);
```

### 2.6 API Design Patterns

**Command Execution Pattern:**

```csharp
public abstract class VisoraCommand
{
    public abstract CommandDescriptor Descriptor { get; }
    
    public virtual ValueTask<CommandResult> ExecuteAsync(
        CommandContext context, 
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult(CommandResult.Success());
}

// Result encapsulation:
public readonly record struct CommandResult(
    CommandOutcome Outcome,
    string? Message = null,
    object? Payload = null)
{
    public static CommandResult Success(string? message = null, object? payload = null) => ...
    public static CommandResult Cancelled(string? message = null) => ...
    public static CommandResult Failed(string? message = null, object? payload = null) => ...
    
    public enum CommandOutcome { Success, Cancelled, Failed }
}
```

**Example Implementation** (`/src/Visora.Shell.Commands.Core/Commands/PingCommand.cs`):
```csharp
public sealed class PingCommand : VisoraCommand
{
    private static readonly CommandDescriptor Info = CommandDescriptor.Create(
        id: "shell.ping",
        title: "Ping",
        description: "Checks connectivity with the Visora host.",
        kind: CommandKind.Automation,
        keywords: new[] { "diagnostics", "ping" });

    public override CommandDescriptor Descriptor => Info;

    public override ValueTask<CommandResult> ExecuteAsync(
        CommandContext context, 
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var surface = context.Surface.ToString();
        var message = $"Pong from '{context.Module.Descriptor.Name}' via {surface} @ {now:O}";
        var payload = new
        {
            Timestamp = now,
            Surface = context.Surface,
            ModuleId = context.Module.Descriptor.Id
        };

        return ValueTask.FromResult(CommandResult.Success(message, payload));
    }
}
```

---

## 3. KEY TECHNICAL PATTERNS

### 3.1 Dependency Injection: Capability Provider Pattern

**Problem:** Modules should not depend on a monolithic service container. Instead, they negotiate capabilities at runtime.

**Solution:** Custom capability provider interface (`/src/Visora.Contracts/Common/ICapabilityProvider.cs`):

```csharp
public interface ICapabilityProvider
{
    bool TryGet<TCapability>(out TCapability? capability) where TCapability : class;
}

public static class CapabilityProviderExtensions
{
    public static TCapability? GetOptional<TCapability>(
        this ICapabilityProvider provider) 
        where TCapability : class
        => provider.TryGet(out TCapability? capability) ? capability : null;

    public static TCapability GetRequired<TCapability>(
        this ICapabilityProvider provider) 
        where TCapability : class
        => provider.TryGet(out TCapability? capability)
            ? capability!
            : throw new InvalidOperationException(
                $"Required capability '{typeof(TCapability).FullName}' not available.");
}
```

**Implementation** (`/src/Visora.Core/Capabilities/CapabilityProviders.cs`):

```csharp
public static class CapabilityProviders
{
    public static ICapabilityProvider Empty { get; } = new NullCapabilityProvider();

    public static CapabilityProviderBuilder CreateBuilder() => new();

    private sealed class NullCapabilityProvider : ICapabilityProvider
    {
        public bool TryGet<TCapability>(out TCapability? capability) 
            where TCapability : class
        {
            capability = null;
            return false;
        }
    }
}

public sealed class CapabilityProviderBuilder
{
    private readonly Dictionary<Type, object> _registrations = new();

    public CapabilityProviderBuilder Add<TCapability>(TCapability capability) 
        where TCapability : class
    {
        if (capability is null) throw new ArgumentNullException(nameof(capability));
        _registrations[typeof(TCapability)] = capability;
        return this;
    }

    public ICapabilityProvider Build()
    {
        if (_registrations.Count == 0)
            return CapabilityProviders.Empty;
        return new DictionaryCapabilityProvider(
            new Dictionary<Type, object>(_registrations));
    }

    private sealed class DictionaryCapabilityProvider : ICapabilityProvider
    {
        private readonly IReadOnlyDictionary<Type, object> _registrations;

        public bool TryGet<TCapability>(out TCapability? capability) 
            where TCapability : class
        {
            if (_registrations.TryGetValue(typeof(TCapability), out var value))
            {
                capability = (TCapability)value;
                return true;
            }
            capability = null;
            return false;
        }
    }
}
```

**Usage Pattern:**
```csharp
// In host setup:
var capabilities = CapabilityProviders.CreateBuilder()
    .Add<IConsoleHost>(consoleHost)
    .Add<IUIManager>(uiManager)
    .Build();

// In module/component code:
var consoleHost = context.Capabilities.GetOptional<IConsoleHost>();
var uiManager = context.Capabilities.GetRequired<IUIManager>();
```

**Key Benefits:**
- Type-safe capability lookup
- No static service locators
- Hosts control what's exposed
- Modules express their requirements explicitly

### 3.2 Configuration Management

**ModuleCatalogOptions** (`/src/Visora.Core/Modules/ModuleCatalogOptions.cs`):

```csharp
public sealed class ModuleCatalogOptions
{
    public IList<string> ProbingPaths { get; } = new List<string>();
    public IList<string> ExplicitModuleFiles { get; } = new List<string>();
    public bool RecurseSubdirectories { get; set; } = true;
    public string SearchPattern { get; set; } = "*.vixm.dll";
    public IServiceProvider? Services { get; set; } = null;
    public ICapabilityProvider Capabilities { get; set; } = 
        CapabilityProviders.Empty;
    public IReadOnlyDictionary<string, object?>? Properties { get; set; } = null;
    public IReadOnlyCollection<Type> SharedTypes { get; set; } = 
        ModuleCatalogDefaults.SharedTypes;

    internal Type[] GetSharedTypesArray() => 
        SharedTypes?.ToArray() ?? ModuleCatalogDefaults.SharedTypes;
}

internal static class ModuleCatalogDefaults
{
    internal static readonly Type[] SharedTypes =
    {
        typeof(VisoraModule),
        typeof(VisoraComponent),
        typeof(VisoraCommand),
        typeof(ModuleDescriptor),
        typeof(ModuleContext),
        typeof(ModuleDiscoveryContext),
        typeof(ComponentDescriptor),
        typeof(ComponentContext),
        typeof(CommandDescriptor),
        typeof(CommandContext),
        typeof(CommandResult),
        typeof(ICapabilityProvider)
    };
}
```

**Usage Pattern** (`/src/Visora.CLI/Program.cs` lines 400-422):
```csharp
private static ModuleCatalogOptions CreateCatalogOptions(ParseResult parseResult)
{
    var extraPaths = parseResult.GetValue(ModulePathOption) ?? Array.Empty<string>();
    var explicitFiles = parseResult.GetValue(ModuleFileOption) ?? Array.Empty<string>();
    var noRecursive = parseResult.GetValue(NoRecursiveOption);

    var options = new ModuleCatalogOptions
    {
        RecurseSubdirectories = !noRecursive
    };

    foreach (var path in GetDefaultProbingPaths())
        options.ProbingPaths.Add(path);

    foreach (var path in extraPaths)
        if (!string.IsNullOrWhiteSpace(path))
            options.ProbingPaths.Add(path);

    foreach (var moduleFile in explicitFiles)
        if (!string.IsNullOrWhiteSpace(moduleFile))
            options.ExplicitModuleFiles.Add(moduleFile);

    return options;
}

private static IEnumerable<string> GetDefaultProbingPaths()
{
    yield return AppContext.BaseDirectory;
    yield return Path.Combine(AppContext.BaseDirectory, "modules");
    yield return Environment.CurrentDirectory;

    var visoraRoot = ResolveVisoraRoot();
    if (!string.IsNullOrWhiteSpace(visoraRoot))
    {
        var globalBin = Path.Combine(visoraRoot, "bin");
        if (Directory.Exists(globalBin))
            yield return globalBin;
    }
}
```

**Key Pattern:**
- Options objects are mutable, allowing builder-style configuration
- Defaults are sensible and overrideable
- Environment variable integration (VISORA_PATH)

### 3.3 Error Handling and Validation

**Pattern 1: Null Guard Arguments**
```csharp
public ModuleContext(
    ModuleDescriptor descriptor, 
    IServiceProvider? services, 
    ICapabilityProvider capabilities, 
    IReadOnlyDictionary<string, object?>? properties = null)
{
    Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
    Services = services;
    Capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
    Properties = properties ?? EmptyProperties;
}
```

**Pattern 2: Operation Results (Not Exceptions)**
Commands use `CommandResult` to communicate outcomes:
```csharp
public static CommandResult Success(string? message = null, object? payload = null)
public static CommandResult Cancelled(string? message = null)
public static CommandResult Failed(string? message = null, object? payload = null)
```

**Pattern 3: Type Loading Error Handling** (`/src/Visora.Core/Modules/ModuleHandle.cs` lines 59-83):
```csharp
try
{
    var assembly = loader.LoadDefaultAssembly();
    var moduleType = assembly
        .GetTypes()
        .FirstOrDefault(t => typeof(VisoraModule).IsAssignableFrom(t) && !t.IsAbstract);

    if (moduleType is null)
        throw new InvalidOperationException(
            $"No VisoraModule implementation found in '{assemblyPath}'.");

    if (Activator.CreateInstance(moduleType) is not VisoraModule module)
        throw new InvalidOperationException(
            $"Unable to create module instance '{moduleType.FullName}'.");

    var descriptor = module.Descriptor 
        ?? throw new InvalidOperationException(
            $"Module '{moduleType.FullName}' returned a null descriptor.");
    
    var capabilities = options.Capabilities 
        ?? throw new InvalidOperationException("Capabilities provider cannot be null.");
    
    var context = new ModuleContext(descriptor, options.Services, 
        capabilities, options.Properties);

    return Task.FromResult(
        new ModuleHandle(assemblyPath, loader, assembly, module, context, options));
}
catch
{
    loader.Dispose();
    throw;
}
```

**Pattern 4: Async Disposal** (`/src/Visora.Core/Modules/ModuleHandle.cs` lines 146-157):
```csharp
public async ValueTask DisposeAsync()
{
    try
    {
        await ShutdownAsync().ConfigureAwait(false);
        await Module.DisposeAsync().ConfigureAwait(false);
    }
    finally
    {
        _loader.Dispose();
    }
}
```

### 3.4 Logging and Monitoring Patterns

**Current Implementation:** Minimal - relies on exceptions and console output.

**Observed Integration Points:**
- CLI output via `Console.WriteLine()` and `Console.Error.WriteLine()`
- Exception messages propagated with context
- Structured inspection API (ModuleHandle.InspectAsync) for diagnostics

**Example** (`/src/Visora.CLI/Program.cs` lines 144-160):
```csharp
await using var catalog = new ModuleCatalog();
try
{
    await catalog.DiscoverAsync(options, ct).ConfigureAwait(false);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Discovery failed: {ex.Message}");
    return 1;
}
```

### 3.5 Data Access & Configuration Patterns

**Plugin Loading** (`/src/Visora.Core/Modules/ModuleHandle.cs` lines 56-57):
```csharp
var sharedTypes = options.GetSharedTypesArray();
var loader = PluginLoader.CreateFromAssemblyFile(
    assemblyPath, 
    sharedTypes: sharedTypes, 
    isUnloadable: true);
```

**Module Discovery** (`/src/Visora.Core/Modules/ModuleLocator.cs`):
```csharp
public static IEnumerable<string> EnumerateCandidateFiles(
    ModuleCatalogOptions options)
{
    if (options is null) throw new ArgumentNullException(nameof(options));

    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    // Explicit files first
    foreach (var explicitFile in options.ExplicitModuleFiles)
    {
        if (string.IsNullOrWhiteSpace(explicitFile))
            continue;

        var path = Path.GetFullPath(explicitFile);
        if (File.Exists(path) && seen.Add(path))
            yield return path;
    }

    // Then probe paths
    var searchOption = options.RecurseSubdirectories 
        ? SearchOption.AllDirectories 
        : SearchOption.TopDirectoryOnly;
    
    foreach (var root in options.ProbingPaths)
    {
        if (string.IsNullOrWhiteSpace(root))
            continue;

        var dir = Path.GetFullPath(root);
        if (!Directory.Exists(dir) || ShouldSkipPath(dir))
            continue;

        foreach (var file in Directory.EnumerateFiles(dir, 
            options.SearchPattern, searchOption))
        {
            var resolved = Path.GetFullPath(file);
            if (ShouldSkipPath(resolved))
                continue;

            if (seen.Add(resolved))
                yield return resolved;
        }
    }

    private static bool ShouldSkipPath(string path)
    {
        var normalized = path.Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);
        return normalized.Contains(
            Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, 
            StringComparison.OrdinalIgnoreCase);
    }
}
```

**Key Patterns:**
- Path deduplication with HashSet
- Order matters: explicit > probed
- Recursive vs flat search is configurable
- Build artifacts (/obj/) are automatically skipped

---

## 4. DOMAIN-SPECIFIC PATTERNS

### 4.1 Business Logic Organization

**Module as Aggregator:**
```csharp
public abstract class VisoraModule : IAsyncDisposable
{
    public abstract ModuleDescriptor Descriptor { get; }
    
    public virtual ValueTask InitializeAsync(
        ModuleContext context, 
        CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
    
    public virtual ValueTask ShutdownAsync(
        ModuleContext context, 
        CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
    
    public virtual IEnumerable<Type> DiscoverComponents(
        ModuleDiscoveryContext context)
        => context.EnumerateComponentCandidates()
            .Where(t => typeof(VisoraComponent).IsAssignableFrom(t));
    
    public virtual ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
```

**Component as Logical Unit:**
```csharp
public abstract class VisoraComponent : IAsyncDisposable
{
    public abstract ComponentDescriptor Descriptor { get; }
    
    public virtual ValueTask InitializeAsync(
        ComponentContext context, 
        CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
    
    public virtual ValueTask ActivateAsync(
        ComponentContext context, 
        CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
    
    public virtual ValueTask DeactivateAsync(
        ComponentContext context, 
        CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
    
    public virtual IEnumerable<VisoraCommand> CreateCommands(
        ComponentContext context)
        => Array.Empty<VisoraCommand>();
    
    public virtual ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
```

**Example: CoreUtilitiesComponent** (`/src/Visora.Shell.Commands.Core/Components/CoreUtilitiesComponent.cs`):
```csharp
public sealed class CoreUtilitiesComponent : Component
{
    private static readonly ComponentDescriptor Info = 
        ComponentDescriptor.Create(
            id: "visora.shell.commands.core.utilities",
            name: "Core Utilities",
            description: "Diagnostics and helper commands for Visora shell experiments.",
            tags: new[] { "core", "shell", "diagnostics" },
            kind: ComponentKind.Console);

    public override ComponentDescriptor Descriptor => Info;

    public override IEnumerable<VisoraCommand> CreateCommands(ComponentContext context)
    {
        yield return new PingCommand();
        yield return new EnvironmentInfoCommand();
        yield return new ModuleProbeCommand();
    }
}
```

### 4.2 Entity and Aggregate Relationships

**Aggregate Root: Module**
- Contains: Component instances
- Lifecycle: Initialize → Active (with components) → Shutdown
- Responsibility: Self-describe, discover components, manage lifecycle

**Value Object: Descriptor (all three flavors)**
- Immutable records
- No identity (equality by value)
- Always serializable (metadata only)

**Entity: Command**
- Has parent component (optional)
- Has execution context
- Returns result (not exception-throwing)

### 4.3 Value Objects

All descriptors are sealed records:
- **ModuleDescriptor** - Id, Name, Version (immutable)
- **ComponentDescriptor** - Id, Name, Kind (immutable)
- **CommandDescriptor** - Id, Title, Kind, UI hints (immutable)
- **CommandResult** - Readonly struct with factory methods
- **ModuleRuntimeHints** - Sealed record for deployment metadata
- **CommandUiHint** - Sealed record for UI menus/icons

### 4.4 Domain Events

**Not currently implemented**, but the architecture supports:
```csharp
// Potential pattern:
public interface IDomainEvent
{
    string EventType { get; }
    DateTimeOffset OccurredAt { get; }
}

public class ModuleInitializedEvent : IDomainEvent
{
    public ModuleDescriptor Module { get; init; }
    public string EventType => "ModuleInitialized";
    public DateTimeOffset OccurredAt { get; init; }
}
```

This would flow through the capability provider to event subscribers.

---

## 5. INFRASTRUCTURE PATTERNS

### 5.1 Plugin Architecture Design

**Unloadable Assembly Isolation:**
```csharp
// From ModuleHandle.LoadAsync
var sharedTypes = options.GetSharedTypesArray();
var loader = PluginLoader.CreateFromAssemblyFile(
    assemblyPath, 
    sharedTypes: sharedTypes, 
    isUnloadable: true);
```

**Benefits:**
- Modules run in isolated contexts
- Shared types prevent version conflicts
- Unloading enables hot-swapping
- Only contract types are shared

### 5.2 Lifecycle Management Strategy

**Three-Phase Lifecycle:**

**Phase 1: Load**
```csharp
var handle = await ModuleHandle.LoadAsync(path, options, ct);
// Assembly loaded, VisoraModule instantiated
```

**Phase 2: Initialize**
```csharp
await handle.EnsureInitializedAsync(ct);
// Module.InitializeAsync(context) called
// Components discovered
// Idempotent—safe to call multiple times
```

**Phase 3: Shutdown**
```csharp
await handle.ShutdownAsync(ct);
// Module.ShutdownAsync(context) called
// Components cleaned up
```

**Cleanup:**
```csharp
await handle.DisposeAsync();
// PluginLoader disposed
// Assembly unloaded
```

### 5.3 Assembly and Namespace Organization

**Namespace Hierarchy:**
```
Visora                          [Root namespace]
├── Contracts                   [Abstract types]
│   ├── Modules
│   ├── Components
│   ├── Commands
│   └── Common
├── Core                        [Implementation]
│   ├── Modules
│   ├── Capabilities
│   └── Windows (planned)
├── CLI                         [Host]
├── CLI.Modules                 [Module]
├── Shell                       [Host]
├── Shell.Commands.Core         [Module]
├── Terminal                    [Host]
└── Shared                      [Utilities]
```

**Assembly Naming Convention:**
- Hosts: `<Name>.exe` or `<Name>.dll`
- Modules: `<Name>.vixm.dll` (vixm = Visora IX Module)
- Examples: `VSCC.vixm.dll`, `Visora.CLI.Module.vixm.dll`

### 5.4 External Service Integration

**System.CommandLine Integration** (`/src/Visora.CLI/Program.cs`):
```csharp
var root = new RootCommand("Visora CLI host and passthrough runner.");
root.Subcommands.Add(BuildRunCommand());
root.Subcommands.Add(BuildModulesCommand());

return await root.Parse(rawArgs).InvokeAsync();
```

**Process Management** (`/src/Visora.CLI/Program.cs` lines 79-98):
```csharp
var psi = new ProcessStartInfo
{
    FileName = exe,
    UseShellExecute = false,
    RedirectStandardInput = false,
    RedirectStandardOutput = false,
    RedirectStandardError = false
};
foreach (var arg in forwarded)
    psi.ArgumentList.Add(arg);

using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };

if (!proc.Start())
{
    Console.Error.WriteLine("Failed to start child process.");
    return 3;
}

await proc.WaitForExitAsync(ct);
return proc.ExitCode;
```

### 5.5 Authentication/Authorization Patterns

**Not implemented** - platform is currently single-user / same-machine.

**Potential Future Pattern:**
```csharp
public interface IAuthenticationProvider
{
    Task<AuthenticationResult> AuthenticateAsync(
        string username, 
        string password, 
        CancellationToken ct);
}

public interface IAuthorizationProvider
{
    Task<bool> IsAuthorizedAsync(
        string userId, 
        string capability, 
        CancellationToken ct);
}

// Exposed via capability provider
var auth = context.Capabilities.GetOptional<IAuthenticationProvider>();
```

---

## 6. API & INTERFACE DESIGN

### 6.1 Controller/Handler Pattern

**CLI Command Pattern** (`/src/Visora.CLI/Program.cs`):
```csharp
var modules = new Command("modules", "Discover, inspect, and deploy Visora modules.");
modules.Subcommands.Add(BuildModulesListCommand());
modules.Subcommands.Add(BuildModulesInspectCommand());
modules.Subcommands.Add(BuildModulesDeployCommand());

// Command action:
list.SetAction(async (ParseResult parseResult, CancellationToken ct) =>
{
    var options = CreateCatalogOptions(parseResult);
    var uniqueMode = parseResult.GetValue(UniqueOption);

    await using var catalog = new ModuleCatalog();
    try
    {
        await catalog.DiscoverAsync(options, ct);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Discovery failed: {ex.Message}");
        return 1;
    }

    var modules = ApplyUniqueness(catalog.Modules, uniqueMode);
    // ... handle results
    return 0;
});
```

**VisoraCommand Pattern:**
```csharp
public override ValueTask<CommandResult> ExecuteAsync(
    CommandContext context, 
    CancellationToken cancellationToken = default)
{
    var payload = new Dictionary<string, object?>
    {
        ["ProcessId"] = Environment.ProcessId,
        ["MachineName"] = Environment.MachineName,
        // ...
    };

    var message = $"Process {Environment.ProcessId} on {Environment.MachineName}...";
    return ValueTask.FromResult(CommandResult.Success(message, payload));
}
```

### 6.2 DTO and Mapping Strategies

**No formal DTO layer** - system uses:
1. **Descriptor records** - metadata DTOs
2. **Inspection records** - read-only data bags
3. **Anonymous objects** - command payloads
4. **Dictionaries** - flexible payloads

**Example: Inspection Result** (from ModuleHandle.InspectAsync):
```csharp
public sealed record ModuleInspection(
    string AssemblyPath,
    ModuleDescriptor Descriptor,
    IReadOnlyList<ComponentInspection> Components);

public sealed record ComponentInspection(
    Type ComponentType,
    ComponentDescriptor Descriptor,
    IReadOnlyList<CommandInspection> Commands);

public sealed record CommandInspection(
    CommandDescriptor Descriptor,
    Type CommandType,
    Type DeclaringComponentType);
```

**Example: Command Payload** (from PingCommand):
```csharp
var payload = new
{
    Timestamp = now,
    Surface = context.Surface,
    ModuleId = context.Module.Descriptor.Id
};

return ValueTask.FromResult(CommandResult.Success(message, payload));
```

### 6.3 Request/Response Patterns

**Command Execution:**
```
Request: CommandContext
├── Module (VisoraModule)
├── Component (VisoraComponent?)
├── Surface (enum: Programmatic | TextShell | Ui | Automation | Remote)
├── Capabilities (ICapabilityProvider)
├── Parameters (Dictionary<string, object?>)
└── CancellationToken

↓ Command.ExecuteAsync(context, ct)

Response: CommandResult
├── Outcome (enum: Success | Cancelled | Failed)
├── Message (string?)
└── Payload (object?)
```

**Module Discovery:**
```
Request: ModuleCatalogOptions
├── ProbingPaths (List<string>)
├── ExplicitModuleFiles (List<string>)
├── RecurseSubdirectories (bool)
├── SearchPattern (string) = "*.vixm.dll"
├── Services (IServiceProvider?)
├── Capabilities (ICapabilityProvider)
├── Properties (Dictionary<string, object?>?)
└── SharedTypes (Collection<Type>)

↓ ModuleCatalog.DiscoverAsync(options, ct)

Response: ModuleHandle[]
└── Each handle contains Module, Descriptor, Assembly
```

### 6.4 Middleware/Interceptor Usage

**Not explicitly implemented**, but the architecture supports:
```csharp
// Potential decorator pattern:
public sealed class LoggingCommandDecorator : VisoraCommand
{
    private readonly VisoraCommand _inner;
    
    public override CommandDescriptor Descriptor => _inner.Descriptor;
    
    public override async ValueTask<CommandResult> ExecuteAsync(
        CommandContext context, 
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Executing: {Descriptor.Title}");
        try
        {
            var result = await _inner.ExecuteAsync(context, cancellationToken);
            Console.WriteLine($"Result: {result.Outcome}");
            return result;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            throw;
        }
    }
}
```

---

## 7. CODE ORGANIZATION PARADIGMS

### 7.1 Naming Conventions

**Classes:**
- Abstract base classes: `Visora<Type>` (VisoraModule, VisoraComponent, VisoraCommand)
- Concrete implementations: Type-specific (PingCommand, CoreUtilitiesComponent)
- Infrastructure: Service pattern (ModuleCatalog, ModuleLocator, CapabilityProviders)

**Methods:**
- Async: `*Async` suffix
- Factories: `Create`, `CreateFrom`, `LoadAsync`
- Queries: `Get`, `Try` prefix
- Checks: `Is`, `Should` prefix

**Identifiers (in descriptors):**
- Module ID: `visora.<subsystem>.<feature>` (e.g., `visora.shell.commands.core`)
- Component ID: `visora.<subsystem>.<feature>.<aspect>` (e.g., `visora.shell.commands.core.utilities`)
- Command ID: `<subsystem>.<verb>.<object>` (e.g., `shell.ping`, `shell.env.info`)

**Example:**
```csharp
// Good: Clear hierarchy
"shell.module.probe"
"shell.ping"
"shell.env.info"

// Modules:
"visora.shell.commands.core"
"visora.cli"

// Components:
"visora.shell.commands.core.utilities"
```

### 7.2 File Organization

**Structure per Project:**
```
Visora.Contracts/
├── Commands/
│   ├── CommandContext.cs
│   ├── CommandDescriptor.cs
│   ├── CommandResult.cs
│   └── VisoraCommand.cs
├── Components/
│   ├── ComponentContext.cs
│   ├── ComponentDescriptor.cs
│   └── VisoraComponent.cs
├── Common/
│   └── ICapabilityProvider.cs
├── Modules/
│   ├── ModuleContext.cs
│   ├── ModuleDescriptor.cs
│   ├── ModuleDiscoveryContext.cs
│   ├── ModuleRuntimeHints.cs
│   └── VisoraModule.cs
└── Visora.Contracts.csproj

Visora.Core/
├── Capabilities/
│   └── CapabilityProviders.cs
├── Component.cs
├── Module.cs
├── Modules/
│   ├── ModuleCatalog.cs
│   ├── ModuleCatalogOptions.cs
│   ├── ModuleHandle.cs
│   └── ModuleLocator.cs
└── Visora.Core.csproj
```

**Principle:** Organize by feature/concern, not by technical layer (Commands, Components, Modules as top-level folders).

### 7.3 Namespace Strategies

**Consistent with folder structure:**
```csharp
namespace Visora.Contracts.Commands;          // ← from Contracts/Commands/
namespace Visora.Contracts.Components;        // ← from Contracts/Components/
namespace Visora.Contracts.Modules;           // ← from Contracts/Modules/
namespace Visora.Core.Modules;                // ← from Core/Modules/
namespace Visora.Core.Capabilities;           // ← from Core/Capabilities/
namespace Visora.Shell.Commands.Core.Commands; // ← from Shell.Commands.Core/Commands/
```

**Root namespace varies by project:**
- Contracts: `Visora.Contracts`
- Core: `Visora.Core`
- CLI: `Visora.CLI`
- Terminal: `Visora.Terminal`
- Modules: `Visora.Shell.Commands.Core` or `Visora.CLI.Modules`

---

## 8. TESTING PATTERNS

### 8.1 Unit Testing Approaches

**Not yet implemented** in the codebase, but the design enables:

```csharp
// Mock capability provider for tests:
[TestClass]
public class PingCommandTests
{
    [TestMethod]
    public async Task ExecuteAsync_ReturnsSuccess()
    {
        // Arrange
        var moduleDescriptor = ModuleDescriptor.Create(
            id: "test.module",
            name: "Test Module",
            version: new Version(1, 0, 0));
        
        var mockModule = new Mock<VisoraModule>();
        mockModule.Setup(m => m.Descriptor).Returns(moduleDescriptor);
        
        var capabilities = CapabilityProviders.Empty;
        
        var context = new CommandContext(
            module: mockModule.Object,
            component: null,
            surface: CommandSurface.Programmatic,
            capabilities: capabilities,
            parameters: null);
        
        var command = new PingCommand();

        // Act
        var result = await command.ExecuteAsync(context, CancellationToken.None);

        // Assert
        Assert.AreEqual(CommandOutcome.Success, result.Outcome);
        Assert.IsNotNull(result.Message);
        Assert.IsNotNull(result.Payload);
    }
}

// Mock module for integration tests:
[TestClass]
public class ModuleCatalogTests
{
    [TestMethod]
    public async Task DiscoverAsync_FindsModulesInPath()
    {
        // Arrange
        var options = new ModuleCatalogOptions
        {
            Capabilities = CapabilityProviders.Empty
        };
        options.ProbingPaths.Add(@"C:\test\modules");
        options.ExplicitModuleFiles.Add(@"C:\test\TestModule.vixm.dll");

        var catalog = new ModuleCatalog();

        // Act
        await catalog.DiscoverAsync(options, CancellationToken.None);

        // Assert
        Assert.IsTrue(catalog.Modules.Count > 0);
        var module = catalog.GetById("test.module");
        Assert.IsNotNull(module);
    }
}
```

### 8.2 Integration Testing Patterns

```csharp
[TestClass]
public class EndToEndModuleTests
{
    [TestMethod]
    public async Task FullModuleLifecycle()
    {
        // Arrange
        var options = new ModuleCatalogOptions
        {
            Capabilities = CapabilityProviders.CreateBuilder()
                .Add<ITestService>(new TestService())
                .Build()
        };
        options.ProbingPaths.Add(GetTestModulesPath());

        var catalog = new ModuleCatalog();

        // Act - Discover
        await catalog.DiscoverAsync(options, CancellationToken.None);
        Assert.IsTrue(catalog.Modules.Count > 0);

        // Act - Initialize
        var handle = catalog.Modules.First();
        await handle.EnsureInitializedAsync(CancellationToken.None);

        // Act - Inspect
        var inspection = await handle.InspectAsync(CancellationToken.None);
        Assert.IsTrue(inspection.Components.Count > 0);

        // Act - Execute Command
        var component = inspection.Components.First();
        var command = component.Commands.First();
        var commandInstance = (VisoraCommand)Activator.CreateInstance(command.CommandType)!;
        
        var context = new CommandContext(
            module: handle.Module,
            component: null,
            surface: CommandSurface.Programmatic,
            capabilities: options.Capabilities);
        
        var result = await commandInstance.ExecuteAsync(context, CancellationToken.None);

        // Assert
        Assert.AreEqual(CommandOutcome.Success, result.Outcome);

        // Act - Shutdown
        await handle.ShutdownAsync(CancellationToken.None);
        await handle.DisposeAsync();
    }
}
```

**Key Design for Testability:**
- Interfaces and abstract classes throughout
- Capability provider for dependency injection
- Async methods with CancellationToken support
- Immutable descriptors for state verification
- Records for value equality testing

---

## 9. ADVANCED ARCHITECTURAL PATTERNS

### 9.1 Reflection-Based Type Discovery

**ModuleDiscoveryContext** (`/src/Visora.Contracts/Modules/ModuleDiscoveryContext.cs`):
```csharp
public sealed class ModuleDiscoveryContext
{
    public Assembly ModuleAssembly { get; }

    public IEnumerable<Type> EnumerateComponentCandidates()
    {
        foreach (var type in ModuleAssembly.GetTypes())
        {
            if (type.IsAbstract) continue;
            if (type.IsInterface) continue;
            if (type.IsNestedPrivate) continue;
            if (typeof(VisoraComponent).IsAssignableFrom(type)) 
                yield return type;
        }
    }
}
```

**Usage in ModuleHandle** (lines 94-135):
```csharp
public async Task<ModuleInspection> InspectAsync(
    CancellationToken cancellationToken = default)
{
    await EnsureInitializedAsync(cancellationToken);

    var discoveryContext = new ModuleDiscoveryContext(Assembly);
    var componentTypes = Module.DiscoverComponents(discoveryContext).ToArray();
    var inspections = new List<ComponentInspection>();

    foreach (var componentType in componentTypes)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Activator.CreateInstance(componentType) is not VisoraComponent component)
            continue;

        var componentContext = new ComponentContext(Module, _context.Capabilities);
        await component.InitializeAsync(componentContext, cancellationToken);

        var descriptor = component.Descriptor;
        var commandInfos = new List<CommandInspection>();

        foreach (var command in component.CreateCommands(componentContext) 
            ?? Array.Empty<VisoraCommand>())
        {
            if (command is null)
                continue;

            commandInfos.Add(new CommandInspection(
                command.Descriptor, 
                command.GetType(), 
                componentType));
            
            // Proper cleanup
            if (command is IAsyncDisposable asyncDisposable)
                await asyncDisposable.DisposeAsync();
            else if (command is IDisposable disposable)
                disposable.Dispose();
        }

        inspections.Add(new ComponentInspection(componentType, descriptor, commandInfos));

        // Proper component cleanup
        if (component is IAsyncDisposable componentAsyncDisposable)
            await componentAsyncDisposable.DisposeAsync();
        else if (component is IDisposable componentDisposable)
            componentDisposable.Dispose();
    }

    return new ModuleInspection(AssemblyPath, Descriptor, inspections);
}
```

**Key Patterns:**
1. Filter conditions are explicit (IsAbstract, IsInterface, IsNestedPrivate)
2. Safe casting with pattern matching
3. Proper resource cleanup with try/finally equivalents
4. Support for both IAsyncDisposable and IDisposable

### 9.2 Extensibility Points

**Module-Level Extension:**
```csharp
public class MyModule : Module
{
    public override ModuleDescriptor Descriptor => ...;
    
    // Customize component discovery:
    public override IEnumerable<Type> DiscoverComponents(
        ModuleDiscoveryContext context)
    {
        // Filter, augment, or replace discovered components
        var base = base.DiscoverComponents(context);
        return base.Where(t => t.Name.Contains("MyFilter"));
    }
    
    // Custom initialization:
    public override async ValueTask InitializeAsync(
        ModuleContext context, 
        CancellationToken cancellationToken = default)
    {
        // Set up module-specific state
        var service = context.Capabilities.GetOptional<IMyService>();
        // ...
    }
}
```

**Component-Level Extension:**
```csharp
public class MyComponent : Component
{
    public override ComponentDescriptor Descriptor => ...;
    
    // Register commands:
    public override IEnumerable<VisoraCommand> CreateCommands(
        ComponentContext context)
    {
        yield return new MyCommand1();
        yield return new MyCommand2();
    }
    
    // Respond to lifecycle:
    public override async ValueTask ActivateAsync(
        ComponentContext context, 
        CancellationToken cancellationToken = default)
    {
        // Subscribe to events, start timers, etc.
    }
    
    public override async ValueTask DeactivateAsync(
        ComponentContext context, 
        CancellationToken cancellationToken = default)
    {
        // Clean up
    }
}
```

**Command-Level Extension:**
```csharp
public class MyCommand : VisoraCommand
{
    public override CommandDescriptor Descriptor => ...;
    
    public override async ValueTask<CommandResult> ExecuteAsync(
        CommandContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var service = context.Capabilities
                .GetRequired<IMyService>();
            var result = await service.DoWorkAsync(
                context.Parameters, 
                cancellationToken);
            
            return CommandResult.Success("Work completed", result);
        }
        catch (Exception ex)
        {
            return CommandResult.Failed($"Error: {ex.Message}");
        }
    }
}
```

### 9.3 Potential Remote/Distributed Patterns

The architecture supports future expansion to:

```csharp
// Potential remote execution:
public sealed record RemoteCommandContext(
    CommandContext Local,
    Uri RemoteHost,
    TimeSpan Timeout) : CommandContext(...);

// Potential serialization:
public interface ICommandSerializer
{
    string SerializeDescriptor(CommandDescriptor descriptor);
    string SerializeContext(CommandContext context);
    T DeserializeResult<T>(string json) where T : class;
}

// Potential message pattern:
public sealed record CommandInvocationMessage(
    string ModuleId,
    string ComponentId,
    string CommandId,
    Dictionary<string, object?> Parameters,
    Guid CorrelationId,
    TimeSpan Timeout);

public sealed record CommandResultMessage(
    Guid CorrelationId,
    CommandOutcome Outcome,
    string? Message,
    object? Payload);
```

---

## 10. KEY DESIGN DECISIONS & RATIONALE

### Decision 1: Reflection-First Discovery (vs. Manifest Files)
**Rationale:** Self-describing modules reduce configuration files and enable AI agents to reason about code structure directly.

### Decision 2: Capability Provider (vs. Service Locator)
**Rationale:** Type-safe, explicit dependencies. Hosts control what's available. No global state.

### Decision 3: Record-Based Descriptors (vs. Classes)
**Rationale:** Immutability, value semantics, and serialization-friendly. Descriptors are data, not behavior.

### Decision 4: Async/Await Everywhere
**Rationale:** Future-proofs for remote execution, I/O operations, and AI-orchestrated async workflows.

### Decision 5: Unloadable Plugin Contexts
**Rationale:** Enables hot-swapping, testing, and resource cleanup without restart.

### Decision 6: Command Result Enum (vs. Exceptions)
**Rationale:** Commands are user-facing; results should be data, not control flow. Cancellation and failure are normal, not exceptional.

### Decision 7: Multi-Surface Command Execution
**Rationale:** Same command runs on CLI, UI, automation, or remote transport without code duplication.

---

## 11. SUMMARY OF PATTERNS BY CATEGORY

### Structural Patterns
- **Layered Architecture** - Contracts → Core → Hosts
- **Plugin Architecture** - Unloadable assemblies with shared type contracts
- **Registry Pattern** - ModuleCatalog maintains module inventory
- **Factory Pattern** - Descriptor.Create(), ModuleHandle.LoadAsync()

### Behavioral Patterns
- **Template Method** - VisoraModule/Component base classes define lifecycle hooks
- **Strategy Pattern** - Different CommandSurface implementations
- **Decorator Pattern** - Capability providers can wrap/augment services
- **Observer Pattern** - Potential via events (not yet implemented)
- **Command Pattern** - VisoraCommand encapsulates execution logic

### Concurrency Patterns
- **Async/Await** - All I/O and lifecycle methods
- **Cancellation Tokens** - Throughout the stack for graceful cancellation
- **Task-Based Asynchrony** - ValueTask for performance-critical paths

### Creational Patterns
- **Singleton** - ModuleCatalog instances
- **Factory** - ModuleHandle.LoadAsync, Descriptor.Create()
- **Builder** - ModuleCatalogOptions, CapabilityProviderBuilder

### Data Patterns
- **Data Transfer Objects** - Descriptor records
- **Value Objects** - CommandResult, immutable descriptors
- **Repository** - ModuleCatalog
- **Active Record** - Modules themselves report their components

### Testing Patterns
- **Test Double** - Mock capabilities, modules, components
- **Fixture** - ModuleCatalogOptions
- **Spy** - Logging decorators (potential)

---

## 12. MATURITY & FUTURE DIRECTIONS

### Current Maturity Level
**Phase M2-M3 (Text shells + hard-wired UI shell)**
- Module loading: ✓ Complete
- CLI integration: ✓ Complete (System.CommandLine)
- Terminal UI: ✓ Prototype (WPF + Actipro)
- Hot-swapping: ✓ Infrastructure ready
- Extensibility: ✓ Working (reflection-based discovery)

### Not Yet Implemented
- [ ] Domain events
- [ ] Remote module execution
- [ ] Distributed capability negotiation
- [ ] Comprehensive logging/observability
- [ ] Module versioning conflicts
- [ ] Performance profiling hooks
- [ ] Test infrastructure/helpers

### Recommended Enhancements
1. **Logging Framework** - Structured logging via capabilities
2. **Middleware** - Command execution pipeline
3. **Caching** - Module inspection results
4. **Monitoring** - Performance metrics, health checks
5. **Documentation** - Automated from descriptors
6. **Type Registration** - DI container integration

---

## 13. FILE REFERENCE GUIDE

| File | Purpose | Key Patterns |
|------|---------|--------------|
| `/src/Visora.Contracts/Modules/VisoraModule.cs` | Base module class | Template method, reflection discovery |
| `/src/Visora.Contracts/Components/VisoraComponent.cs` | Base component class | Lifecycle hooks, command creation |
| `/src/Visora.Contracts/Commands/VisoraCommand.cs` | Base command class | Execute pattern, result object |
| `/src/Visora.Contracts/Commands/CommandContext.cs` | Command execution context | Dependency injection via capabilities |
| `/src/Visora.Contracts/Common/ICapabilityProvider.cs` | Service negotiation | Service locator alternative |
| `/src/Visora.Core/Modules/ModuleCatalog.cs` | Module registry & lifecycle | Registry, factory |
| `/src/Visora.Core/Modules/ModuleHandle.cs` | Loaded module wrapper | Plugin management, lifecycle |
| `/src/Visora.Core/Modules/ModuleLocator.cs` | Module discovery | File system scanning |
| `/src/Visora.Core/Capabilities/CapabilityProviders.cs` | Capability implementation | Builder, type-safe DI |
| `/src/Visora.CLI/Program.cs` | CLI host | System.CommandLine integration |
| `/src/Visora.Shell.Commands.Core/ShellCommandsModule.cs` | Example module | Concrete implementation |
| `/src/Visora.Shell.Commands.Core/Components/CoreUtilitiesComponent.cs` | Example component | Command creation |
| `/src/Visora.Shell.Commands.Core/Commands/PingCommand.cs` | Example command | Execution, result creation |
| `/src/Visora.Terminal/Program.cs` | WPF terminal host | Multi-threaded UI, console management |

---

**Analysis Complete**
