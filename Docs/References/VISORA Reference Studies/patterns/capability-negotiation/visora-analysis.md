# Capability Negotiation Pattern - VISORA Deep Dive

**Last Updated:** November 10, 2025
**VISORA Version:** .NET 9.0
**Pattern Tier:** Tier 1 (Foundational)
**Related Patterns:** Context Objects, Plugin Architecture, Testable Design

---

## Table of Contents

1. [Pattern Overview](#pattern-overview)
2. [Why Not Traditional DI?](#why-not-traditional-di)
3. [VISORA Implementation](#visora-implementation)
4. [Code Examples](#code-examples)
5. [File References](#file-references)
6. [Type Safety](#type-safety)
7. [Comparison to Dependency Injection](#comparison-to-dependency-injection)
8. [Capability Propagation](#capability-propagation)
9. [Scoping Strategies](#scoping-strategies)
10. [Testing Patterns](#testing-patterns)
11. [Best Practices](#best-practices)
12. [Advanced Topics](#advanced-topics)

---

## Pattern Overview

### What is Capability Negotiation?

**Definition:** A pattern where modules explicitly request capabilities (services, APIs, resources) from the host at runtime through a type-safe provider interface, rather than receiving a monolithic dependency injection container.

**Key Characteristics:**
- **Explicit Dependencies:** Modules state what they need
- **Optional vs Required:** Modules handle missing capabilities gracefully
- **Type-Safe:** No string-based lookups or reflection
- **Host-Controlled:** Host decides what capabilities to expose
- **Testable:** Easy to mock capabilities for testing

### Core Components

```
┌─────────────────────────────────────────────────────────┐
│                    HOST                                 │
│                                                         │
│  ┌───────────────────────────────────────────────────┐ │
│  │       CapabilityProviderBuilder                   │ │
│  │  .Add<IConsoleHost>(consoleHost)                  │ │
│  │  .Add<IUIManager>(uiManager)                      │ │
│  │  .Add<ILogger>(logger)                            │ │
│  │  .Build() → ICapabilityProvider                   │ │
│  └───────────────────────────────────────────────────┘ │
│                        ↓                                │
│  ┌───────────────────────────────────────────────────┐ │
│  │       ModuleContext / CommandContext              │ │
│  │  Capabilities: ICapabilityProvider                │ │
│  └───────────────────────────────────────────────────┘ │
│                        ↓                                │
└─────────────────────────────────────────────────────────┘
                         ↓
┌─────────────────────────────────────────────────────────┐
│                    MODULE                               │
│                                                         │
│  public override async ValueTask InitializeAsync(      │
│      ModuleContext context, ...)                       │
│  {                                                      │
│      // Optional capability                            │
│      var console = context.Capabilities                │
│          .GetOptional<IConsoleHost>();                 │
│                                                         │
│      // Required capability                            │
│      var logger = context.Capabilities                 │
│          .GetRequired<ILogger>();                      │
│  }                                                      │
└─────────────────────────────────────────────────────────┘
```

---

## Why Not Traditional DI?

### Problems with Traditional Dependency Injection

**1. Monolithic Service Container**
```csharp
// ❌ PROBLEM: Module receives entire IServiceProvider
public class MyModule : VisoraModule
{
    public override ValueTask InitializeAsync(
        ModuleContext context, ...)
    {
        // What services are available? No idea!
        var service = context.Services.GetService<IMyService>();

        // Is it null because:
        // - Service not registered?
        // - Wrong type name?
        // - Host forgot to add it?
        if (service == null)
        {
            // Now what?
        }
    }
}
```

**Problems:**
- No visibility into available services
- Discovery is trial-and-error
- Unclear which services are required vs optional
- Hard to diagnose missing dependencies

---

**2. Service Locator Anti-Pattern**
```csharp
// ❌ ANTI-PATTERN: Global service access
public static class GlobalServices
{
    public static IServiceProvider Provider { get; set; }
}

public class MyCommand : VisoraCommand
{
    public override async ValueTask<CommandResult> ExecuteAsync(...)
    {
        // Hidden dependency!
        var service = GlobalServices.Provider.GetService<IMyService>();

        // Impossible to test without global state
        // Breaks isolation
    }
}
```

**Problems:**
- Hidden dependencies
- Global mutable state
- Breaks testability
- Violates encapsulation

---

**3. Constructor Injection Limitations**
```csharp
// ❌ PROBLEM: Modules are instantiated by reflection
public class MyModule : VisoraModule
{
    // Can't use constructor injection!
    // Activator.CreateInstance() doesn't pass parameters
    public MyModule(ILogger logger, IConsoleHost console)
    {
        // This constructor is NEVER called
    }
}
```

**Problems:**
- Modules are created via `Activator.CreateInstance()`
- No control over constructor parameters
- Can't use standard DI constructor injection

---

### Why Capability Negotiation Solves These

✅ **Explicit Dependencies:**
```csharp
// Module clearly states what it needs
var console = context.Capabilities.GetOptional<IConsoleHost>();
var logger = context.Capabilities.GetRequired<ILogger>();
```

✅ **Host Control:**
```csharp
// Host decides exactly what to expose
var capabilities = CapabilityProviders.CreateBuilder()
    .Add<ILogger>(logger)
    .Add<IConsoleHost>(console)
    // NOT exposing IInternalApi - modules can't access it
    .Build();
```

✅ **Testability:**
```csharp
// Easy to mock for tests
var mockLogger = new Mock<ILogger>();
var capabilities = CapabilityProviders.CreateBuilder()
    .Add<ILogger>(mockLogger.Object)
    .Build();
```

---

## VISORA Implementation

### Interface Definition

**File:** `/src/Visora.Contracts/Common/ICapabilityProvider.cs`

```csharp
/// <summary>
/// Provides negotiated capabilities from the host to modules, components, and commands.
/// </summary>
public interface ICapabilityProvider
{
    /// <summary>
    /// Attempts to retrieve the specified capability.
    /// </summary>
    /// <typeparam name="TCapability">Capability type.</typeparam>
    /// <param name="capability">Capability instance if available.</param>
    /// <returns><c>true</c> when the capability is available.</returns>
    bool TryGet<TCapability>(out TCapability? capability) where TCapability : class;
}
```

**Key Design Decisions:**
- **Generic Method:** Type-safe lookup via `TCapability`
- **Try Pattern:** Returns bool + out parameter (like `Dictionary.TryGetValue`)
- **Class Constraint:** Only reference types (interfaces, classes)
- **Null Reference:** Capability is null if unavailable

---

### Extension Methods

**File:** `/src/Visora.Contracts/Common/ICapabilityProvider.cs:22-31`

```csharp
/// <summary>
/// Convenience helpers for <see cref="ICapabilityProvider"/>.
/// </summary>
public static class CapabilityProviderExtensions
{
    public static TCapability? GetOptional<TCapability>(this ICapabilityProvider provider)
        where TCapability : class
        => provider.TryGet(out TCapability? capability) ? capability : null;

    public static TCapability GetRequired<TCapability>(this ICapabilityProvider provider)
        where TCapability : class
        => provider.TryGet(out TCapability? capability)
            ? capability!
            : throw new InvalidOperationException(
                $"Required capability '{typeof(TCapability).FullName}' not available.");
}
```

**Usage Patterns:**

**Optional Capability:**
```csharp
var console = context.Capabilities.GetOptional<IConsoleHost>();
if (console != null)
{
    await console.WriteLineAsync("Hello!");
}
```

**Required Capability:**
```csharp
// Throws if not available
var logger = context.Capabilities.GetRequired<ILogger>();
logger.Log("Always works or throws");
```

---

### Builder Implementation

**File:** `/src/Visora.Core/Capabilities/CapabilityProviders.cs`

```csharp
/// <summary>
/// Helpers for working with capability providers.
/// </summary>
public static class CapabilityProviders
{
    /// <summary>
    /// An empty provider that never resolves capabilities.
    /// </summary>
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
```

**Empty Provider:**
- Used when no capabilities are available
- Always returns `false` from `TryGet`
- Singleton instance (no allocations)

---

### Builder Class

**File:** `/src/Visora.Core/Capabilities/CapabilityProviders.cs:29-70`

```csharp
/// <summary>
/// Builds an <see cref="ICapabilityProvider"/> backed by a dictionary.
/// </summary>
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

        public DictionaryCapabilityProvider(IReadOnlyDictionary<Type, object> registrations)
            => _registrations = registrations;

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

**Implementation Details:**
- **Dictionary-Based:** Simple, fast lookup by `Type`
- **Fluent API:** `Add()` returns `this` for chaining
- **Immutable Result:** `Build()` creates read-only dictionary
- **Defensive Copy:** Dictionary is copied during `Build()`
- **Type Safety:** Cast is safe because `Add<T>` enforces type

---

## Code Examples

### Example 1: Host Setup

```csharp
// Host creates capabilities based on environment

public class CliHost
{
    public static ICapabilityProvider CreateCapabilities()
    {
        var builder = CapabilityProviders.CreateBuilder();

        // Always available
        builder.Add<ILogger>(new ConsoleLogger());
        builder.Add<IFileSystem>(new FileSystemService());

        // Only in CLI
        builder.Add<IConsoleHost>(new CliConsoleHost());

        // Conditionally available
        if (HasNetworkAccess())
        {
            builder.Add<IHttpClient>(new HttpClientService());
        }

        return builder.Build();
    }
}

public class WpfHost
{
    public static ICapabilityProvider CreateCapabilities()
    {
        var builder = CapabilityProviders.CreateBuilder();

        // Always available
        builder.Add<ILogger>(new FileLogger());
        builder.Add<IFileSystem>(new FileSystemService());

        // Only in WPF
        builder.Add<IUIManager>(new WpfUIManager());
        builder.Add<IDialogService>(new WpfDialogService());

        // NOT providing IConsoleHost - no console in GUI

        return builder.Build();
    }
}
```

**Key Insight:** Different hosts expose different capabilities. Modules adapt based on what's available.

---

### Example 2: Module Consumption

**File:** `/src/Visora.Shell.Commands.Core/ShellCommandsModule.cs`

```csharp
public sealed class ShellCommandsModule : Module
{
    private ILogger? _logger;

    private static readonly ModuleDescriptor Info = ModuleDescriptor.Create(
        id: "visora.shell.commands.core",
        name: "Shell Commands (Core)",
        version: new Version(0, 1, 0),
        description: "Provides basic shell commands for diagnostics and inspection.",
        tags: new Dictionary<string, string>
        {
            ["category"] = "shell",
            ["platform"] = "cross-platform"
        });

    public override ModuleDescriptor Descriptor => Info;

    public override async ValueTask InitializeAsync(
        ModuleContext context,
        CancellationToken cancellationToken = default)
    {
        // Optional: Get logger if available
        _logger = context.Capabilities.GetOptional<ILogger>();
        _logger?.Log($"Initializing {Info.Name}...");

        // Required: Validate we have minimal capabilities
        // (In this simple module, we don't require any specific capabilities)

        await Task.CompletedTask;
    }

    public override async ValueTask ShutdownAsync(
        ModuleContext context,
        CancellationToken cancellationToken = default)
    {
        _logger?.Log($"Shutting down {Info.Name}...");
        await Task.CompletedTask;
    }
}
```

**Pattern:**
- Store optional capabilities as fields
- Use in lifecycle methods
- Gracefully handle absence

---

### Example 3: Command Execution

**File:** `/src/Visora.Shell.Commands.Core/Commands/EnvironmentInfoCommand.cs`

```csharp
public sealed class EnvironmentInfoCommand : VisoraCommand
{
    private static readonly CommandDescriptor Info = CommandDescriptor.Create(
        id: "shell.env.info",
        title: "Environment Info",
        description: "Displays detailed environment and runtime information.",
        kind: CommandKind.Automation,
        keywords: new[] { "environment", "diagnostics", "system" });

    public override CommandDescriptor Descriptor => Info;

    public override ValueTask<CommandResult> ExecuteAsync(
        CommandContext context,
        CancellationToken cancellationToken = default)
    {
        // Optional: Use console host if available for rich output
        var console = context.Capabilities.GetOptional<IConsoleHost>();

        var payload = new Dictionary<string, object?>
        {
            ["ProcessId"] = Environment.ProcessId,
            ["MachineName"] = Environment.MachineName,
            ["OSVersion"] = Environment.OSVersion.VersionString,
            ["RuntimeVersion"] = Environment.Version.ToString(),
            ["WorkingDirectory"] = Environment.CurrentDirectory,
            ["UserName"] = Environment.UserName,
            ["Is64BitProcess"] = Environment.Is64BitProcess,
            ["ProcessorCount"] = Environment.ProcessorCount
        };

        var message = FormatEnvironmentInfo(payload, console != null);

        // Use console if available
        if (console != null)
        {
            console.WriteLineAsync(message).GetAwaiter().GetResult();
        }

        return ValueTask.FromResult(CommandResult.Success(message, payload));
    }

    private static string FormatEnvironmentInfo(
        Dictionary<string, object?> info,
        bool richOutput)
    {
        if (richOutput)
        {
            // Rich formatting for console
            return string.Join("\n",
                "=== Environment Information ===",
                $"Process ID:      {info["ProcessId"]}",
                $"Machine Name:    {info["MachineName"]}",
                $"OS Version:      {info["OSVersion"]}",
                $"Runtime Version: {info["RuntimeVersion"]}",
                $"Working Dir:     {info["WorkingDirectory"]}",
                $"User Name:       {info["UserName"]}",
                $"64-bit Process:  {info["Is64BitProcess"]}",
                $"Processor Count: {info["ProcessorCount"]}");
        }
        else
        {
            // Simple formatting
            return $"Process {info["ProcessId"]} on {info["MachineName"]} " +
                   $"running {info["RuntimeVersion"]}";
        }
    }
}
```

**Pattern:**
- Check for optional capabilities
- Adapt behavior based on availability
- Provide graceful degradation

---

### Example 4: Testing with Mock Capabilities

```csharp
[TestClass]
public class ModuleInitializationTests
{
    [TestMethod]
    public async Task Module_InitializesWithLogger_LogsMessage()
    {
        // Arrange
        var mockLogger = new Mock<ILogger>();

        var capabilities = CapabilityProviders.CreateBuilder()
            .Add<ILogger>(mockLogger.Object)
            .Build();

        var descriptor = ModuleDescriptor.Create(
            id: "test.module",
            name: "Test Module",
            version: new Version(1, 0, 0));

        var context = new ModuleContext(
            descriptor,
            services: null,
            capabilities,
            properties: null);

        var module = new MyModule();

        // Act
        await module.InitializeAsync(context, CancellationToken.None);

        // Assert
        mockLogger.Verify(
            l => l.Log(It.Is<string>(s => s.Contains("Initializing"))),
            Times.Once);
    }

    [TestMethod]
    public async Task Module_InitializesWithoutLogger_DoesNotThrow()
    {
        // Arrange - No logger capability
        var capabilities = CapabilityProviders.Empty;

        var descriptor = ModuleDescriptor.Create(
            id: "test.module",
            name: "Test Module",
            version: new Version(1, 0, 0));

        var context = new ModuleContext(
            descriptor,
            services: null,
            capabilities,
            properties: null);

        var module = new MyModule();

        // Act & Assert - Should not throw
        await module.InitializeAsync(context, CancellationToken.None);
    }

    [TestMethod]
    public async Task Command_RequiresDatabase_ThrowsIfNotAvailable()
    {
        // Arrange
        var capabilities = CapabilityProviders.Empty; // No database

        var context = new CommandContext(
            module: Mock.Of<VisoraModule>(),
            component: null,
            surface: CommandSurface.Programmatic,
            capabilities: capabilities,
            parameters: null);

        var command = new DatabaseCommand();

        // Act & Assert
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            async () => await command.ExecuteAsync(context, CancellationToken.None));
    }
}
```

---

## File References

### Core Implementation

| File | Lines | Purpose |
|------|-------|---------|
| `/src/Visora.Contracts/Common/ICapabilityProvider.cs` | 8-16 | Interface definition |
| `/src/Visora.Contracts/Common/ICapabilityProvider.cs` | 22-31 | Extension methods (GetOptional, GetRequired) |
| `/src/Visora.Core/Capabilities/CapabilityProviders.cs` | 10-27 | Empty provider and builder factory |
| `/src/Visora.Core/Capabilities/CapabilityProviders.cs` | 29-70 | Builder and dictionary-based provider |

### Usage in Context Objects

| File | Purpose |
|------|---------|
| `/src/Visora.Contracts/Modules/ModuleContext.cs` | ModuleContext contains ICapabilityProvider |
| `/src/Visora.Contracts/Components/ComponentContext.cs` | ComponentContext contains ICapabilityProvider |
| `/src/Visora.Contracts/Commands/CommandContext.cs` | CommandContext contains ICapabilityProvider |

### Example Usage

| File | Purpose |
|------|---------|
| `/src/Visora.Shell.Commands.Core/ShellCommandsModule.cs` | Module using optional logger |
| `/src/Visora.Shell.Commands.Core/Commands/EnvironmentInfoCommand.cs` | Command adapting to console availability |

---

## Type Safety

### How Type Safety is Maintained

**1. Generic Type Parameter:**
```csharp
public bool TryGet<TCapability>(out TCapability? capability)
    where TCapability : class
```

- Compiler enforces `TCapability` is a class
- No runtime type name lookups
- IntelliSense provides type information

**2. Dictionary Keyed by Type:**
```csharp
private readonly Dictionary<Type, object> _registrations = new();

public CapabilityProviderBuilder Add<TCapability>(TCapability capability)
{
    _registrations[typeof(TCapability)] = capability;
    //              ^^^^^^^^^^^^^^^
    //              Type token (compile-time)
    return this;
}
```

**3. Safe Casting:**
```csharp
public bool TryGet<TCapability>(out TCapability? capability)
{
    if (_registrations.TryGetValue(typeof(TCapability), out var value))
    {
        capability = (TCapability)value;
        //           ^^^^^^^^^^^^^^^^^^
        //           Safe because Add<T> enforced type
        return true;
    }
    // ...
}
```

**Why This Is Safe:**
- `Add<TLogger>(logger)` stores `typeof(TLogger) -> logger`
- `TryGet<TLogger>()` looks up `typeof(TLogger)`
- Cast is guaranteed to succeed (or dictionary miss)

---

### Type Safety vs String-Based Lookup

**❌ String-Based (Unsafe):**
```csharp
// Anti-pattern
var logger = (ILogger)services.GetService("ILogger");
//           ^^^^^^^
//           Runtime cast - can throw

// Typo = silent failure
var logger = services.GetService("ILoger"); // Oops!
```

**✅ Type-Based (Safe):**
```csharp
// Type-safe
var logger = capabilities.GetOptional<ILogger>();
//                                     ^^^^^^^
//                                     Compile-time check

// Typo = compile error
var logger = capabilities.GetOptional<ILoger>(); // ❌ Compile error
```

---

## Comparison to Dependency Injection

### VISORA Capability Negotiation vs ASP.NET Core DI

| Aspect | VISORA Capability Negotiation | ASP.NET Core DI |
|--------|-------------------------------|-----------------|
| **Registration** | `builder.Add<T>(instance)` | `services.AddSingleton<T>(instance)` |
| **Retrieval** | `capabilities.GetOptional<T>()` | `services.GetService<T>()` |
| **Required** | `capabilities.GetRequired<T>()` | `services.GetRequiredService<T>()` |
| **Null Handling** | Explicit (returns null) | Explicit (returns null) |
| **Constructor Injection** | ❌ Not supported | ✅ Supported |
| **Scopes** | ❌ Single scope | ✅ Singleton, Scoped, Transient |
| **Lifetime Management** | Manual | Automatic |
| **Factory Registration** | ❌ Not supported | ✅ Supported |
| **Interface Binding** | Manual (one type per registration) | Automatic (multiple implementations) |

---

### When to Use Which

**Use Capability Negotiation When:**
- ✅ Modules are loaded dynamically (plugins)
- ✅ Host controls what's exposed
- ✅ Optional capabilities are common
- ✅ Testing requires simple mocks
- ✅ Minimal API surface

**Use ASP.NET Core DI When:**
- ✅ Static application structure
- ✅ Constructor injection is needed
- ✅ Lifetime scopes (request-scoped services)
- ✅ Complex dependency graphs
- ✅ Factory patterns

**VISORA Hybrid Approach:**
```csharp
// Host uses ASP.NET Core DI internally
var services = new ServiceCollection();
services.AddSingleton<ILogger, ConsoleLogger>();
services.AddScoped<IDatabaseService, DatabaseService>();
var provider = services.BuildServiceProvider();

// But exposes capabilities to modules
var capabilities = CapabilityProviders.CreateBuilder()
    .Add<ILogger>(provider.GetRequiredService<ILogger>())
    .Add<IDatabaseService>(provider.GetRequiredService<IDatabaseService>())
    .Build();

// Pass to modules
var context = new ModuleContext(descriptor, provider, capabilities, null);
```

---

### Capability Negotiation vs Service Locator

**Service Locator Anti-Pattern:**
```csharp
// ❌ ANTI-PATTERN
public static class ServiceLocator
{
    public static IServiceProvider Services { get; set; }
}

public class MyCommand : VisoraCommand
{
    public override async ValueTask<CommandResult> ExecuteAsync(...)
    {
        var service = ServiceLocator.Services.GetService<IMyService>();
        // Hidden dependency, global state, breaks tests
    }
}
```

**Capability Negotiation (NOT a Service Locator):**
```csharp
// ✅ GOOD PATTERN
public class MyCommand : VisoraCommand
{
    public override async ValueTask<CommandResult> ExecuteAsync(
        CommandContext context, ...)
    {
        var service = context.Capabilities.GetOptional<IMyService>();
        //            ^^^^^^^^^^^^^^^^^^
        //            Passed explicitly via context (no global state)
    }
}
```

**Why Not Service Locator:**
1. **No Global State:** `ICapabilityProvider` is passed explicitly
2. **Visible Dependencies:** Capabilities are in method signature (via context)
3. **Testable:** Easy to pass mock provider
4. **Scoped:** Each context has its own provider

---

## Capability Propagation

### How Capabilities Flow Through the System

```
Host Creates Capabilities
         ↓
ModuleCatalogOptions.Capabilities
         ↓
ModuleContext.Capabilities (created during LoadAsync)
         ↓
Module.InitializeAsync(context) ← Receives capabilities
         ↓
ComponentContext.Capabilities (derived from ModuleContext)
         ↓
Component.InitializeAsync(context) ← Receives same capabilities
         ↓
CommandContext.Capabilities (derived from ComponentContext)
         ↓
Command.ExecuteAsync(context) ← Receives same capabilities
```

**Implementation:**

**Step 1: Host Setup:**
```csharp
var capabilities = CapabilityProviders.CreateBuilder()
    .Add<ILogger>(logger)
    .Build();

var options = new ModuleCatalogOptions
{
    Capabilities = capabilities
};
```

**Step 2: Module Load:**
```csharp
// In ModuleHandle.LoadAsync
var context = new ModuleContext(
    descriptor,
    options.Services,
    options.Capabilities, // ← Propagated from options
    options.Properties);
```

**Step 3: Component Initialization:**
```csharp
// In ModuleHandle.InspectAsync
var componentContext = new ComponentContext(
    Module,
    _context.Capabilities); // ← Same capabilities as module
```

**Step 4: Command Execution:**
```csharp
// When executing a command
var commandContext = new CommandContext(
    module: module,
    component: component,
    surface: surface,
    capabilities: moduleContext.Capabilities, // ← Same capabilities
    parameters: parameters);

await command.ExecuteAsync(commandContext, ct);
```

**Key Insight:** Same `ICapabilityProvider` instance flows through entire hierarchy.

---

## Scoping Strategies

### Global Scope (Current VISORA)

**Pattern:** All modules share same capabilities.

```csharp
var capabilities = CapabilityProviders.CreateBuilder()
    .Add<ILogger>(globalLogger)
    .Build();

var options = new ModuleCatalogOptions
{
    Capabilities = capabilities // ← All modules get this
};

await catalog.DiscoverAsync(options, ct);
```

**Pros:**
- Simple
- Consistent API surface

**Cons:**
- Can't provide module-specific capabilities
- All modules see same services

---

### Per-Module Scope (Advanced)

**Pattern:** Different capabilities per module.

```csharp
// Custom loader that creates per-module capabilities
public async Task<ModuleHandle> LoadModuleWithScopedCapabilitiesAsync(
    string modulePath,
    ICapabilityProvider globalCapabilities)
{
    // Load module to get descriptor
    var handle = await ModuleHandle.LoadAsync(modulePath, options, ct);

    // Create scoped capabilities based on module ID
    var scopedCapabilities = CreateScopedCapabilities(
        handle.Descriptor.Id,
        globalCapabilities);

    // Replace context with scoped capabilities
    // (Requires internal API changes - illustrative only)
    handle.ReplaceContext(new ModuleContext(
        handle.Descriptor,
        options.Services,
        scopedCapabilities,
        options.Properties));

    return handle;
}

private ICapabilityProvider CreateScopedCapabilities(
    string moduleId,
    ICapabilityProvider global)
{
    var builder = CapabilityProviders.CreateBuilder();

    // Copy global capabilities
    // (Requires capability enumeration - not currently supported)
    // ...

    // Add module-specific capabilities
    if (moduleId == "trusted.module")
    {
        builder.Add<IAdminApi>(new AdminApi());
    }

    return builder.Build();
}
```

**Note:** Current VISORA implementation doesn't support per-module scoping. This is a future extension.

---

### Per-Command Scope (Advanced)

**Pattern:** Different capabilities per command invocation.

```csharp
public async Task<CommandResult> ExecuteCommandWithScopedCapabilitiesAsync(
    VisoraCommand command,
    CommandContext baseContext,
    CancellationToken ct)
{
    // Create scoped capabilities for this command execution
    var scopedCapabilities = CapabilityProviders.CreateBuilder()
        .Add<ILogger>(new CommandLogger(command.Descriptor.Id))
        .Add<ICancellationToken>(new CancellationTokenCapability(ct))
        .Build();

    // Create scoped context
    var scopedContext = new CommandContext(
        baseContext.Module,
        baseContext.Component,
        baseContext.Surface,
        scopedCapabilities, // ← Scoped
        baseContext.Parameters,
        ct);

    return await command.ExecuteAsync(scopedContext, ct);
}
```

---

## Testing Patterns

### Unit Testing: Mocking Capabilities

```csharp
[TestClass]
public class CommandTests
{
    [TestMethod]
    public async Task ExecuteAsync_WithLogger_LogsExecution()
    {
        // Arrange
        var mockLogger = new Mock<ILogger>();
        mockLogger.Setup(l => l.Log(It.IsAny<string>()));

        var capabilities = CapabilityProviders.CreateBuilder()
            .Add<ILogger>(mockLogger.Object)
            .Build();

        var context = CreateTestContext(capabilities);
        var command = new MyCommand();

        // Act
        await command.ExecuteAsync(context, CancellationToken.None);

        // Assert
        mockLogger.Verify(l => l.Log(It.Is<string>(s => s.Contains("Executing"))), Times.Once);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithoutLogger_DoesNotThrow()
    {
        // Arrange
        var capabilities = CapabilityProviders.Empty;
        var context = CreateTestContext(capabilities);
        var command = new MyCommand();

        // Act & Assert
        await command.ExecuteAsync(context, CancellationToken.None);
        // Should not throw
    }

    private CommandContext CreateTestContext(ICapabilityProvider capabilities)
    {
        var mockModule = new Mock<VisoraModule>();
        return new CommandContext(
            mockModule.Object,
            component: null,
            CommandSurface.Programmatic,
            capabilities,
            parameters: null,
            CancellationToken.None);
    }
}
```

---

### Integration Testing: Real Capabilities

```csharp
[TestClass]
public class IntegrationTests
{
    [TestMethod]
    public async Task FullModuleLifecycle_WithRealCapabilities()
    {
        // Arrange
        var logger = new TestLogger();
        var fileSystem = new InMemoryFileSystem();

        var capabilities = CapabilityProviders.CreateBuilder()
            .Add<ILogger>(logger)
            .Add<IFileSystem>(fileSystem)
            .Build();

        var options = new ModuleCatalogOptions
        {
            Capabilities = capabilities
        };
        options.ExplicitModuleFiles.Add("TestModule.vixm.dll");

        var catalog = new ModuleCatalog();

        // Act - Discover and initialize
        await catalog.DiscoverAsync(options, CancellationToken.None);
        var module = catalog.Modules.First();
        await module.EnsureInitializedAsync(CancellationToken.None);

        // Assert - Module used capabilities
        Assert.IsTrue(logger.Entries.Any(e => e.Contains("Initializing")));
        Assert.IsTrue(fileSystem.OperationCount > 0);
    }
}
```

---

### Test Helpers

```csharp
/// <summary>
/// Test helper for creating capability providers.
/// </summary>
public static class TestCapabilities
{
    public static ICapabilityProvider WithLogger(ILogger logger)
        => CapabilityProviders.CreateBuilder()
            .Add<ILogger>(logger)
            .Build();

    public static ICapabilityProvider WithDefaults()
        => CapabilityProviders.CreateBuilder()
            .Add<ILogger>(new NullLogger())
            .Add<IFileSystem>(new InMemoryFileSystem())
            .Build();

    public static ICapabilityProvider Empty
        => CapabilityProviders.Empty;
}

// Usage
var context = CreateContext(TestCapabilities.WithLogger(mockLogger.Object));
```

---

## Best Practices

### Designing Capabilities

**1. Use Interfaces, Not Concrete Classes:**
```csharp
// ✅ GOOD
public interface ILogger
{
    void Log(string message);
}

builder.Add<ILogger>(new ConsoleLogger());

// ❌ BAD
builder.Add<ConsoleLogger>(new ConsoleLogger());
// Now modules depend on concrete class
```

---

**2. Keep Interfaces Small:**
```csharp
// ✅ GOOD: Focused interfaces
public interface ILogger
{
    void Log(string message);
}

public interface IFileReader
{
    Task<string> ReadAllTextAsync(string path);
}

// ❌ BAD: God interface
public interface IEverything
{
    void Log(string message);
    Task<string> ReadFileAsync(string path);
    void SendEmail(string to, string body);
    // 50 more methods...
}
```

---

**3. Optional vs Required:**
```csharp
// ✅ GOOD: Clear intent
var logger = context.Capabilities.GetOptional<ILogger>();
logger?.Log("Optional logging");

var database = context.Capabilities.GetRequired<IDatabaseService>();
await database.ConnectAsync(); // Always works or threw earlier

// ❌ BAD: Unclear
var logger = context.Capabilities.GetOptional<ILogger>();
logger.Log("Required but using optional"); // NullReferenceException!
```

---

**4. Defensive Programming:**
```csharp
// ✅ GOOD: Check before use
public override async ValueTask InitializeAsync(ModuleContext context, ...)
{
    var console = context.Capabilities.GetOptional<IConsoleHost>();
    if (console != null)
    {
        await console.WriteLineAsync("Module starting...");
    }

    var db = context.Capabilities.GetRequired<IDatabaseService>();
    await db.ConnectAsync(); // Throws early if missing
}

// ❌ BAD: Assume it exists
var console = context.Capabilities.GetOptional<IConsoleHost>();
await console.WriteLineAsync("Crash if null!");
```

---

### When to Use Required vs Optional

**Use GetRequired When:**
- Module cannot function without capability
- Fail-fast is desired
- Capability is part of module contract

**Use GetOptional When:**
- Module can degrade gracefully
- Capability enhances but isn't essential
- Behavior adapts to environment

**Example:**
```csharp
// Required: Module can't work without database
var db = context.Capabilities.GetRequired<IDatabaseService>();

// Optional: UI enhancements, but works without
var ui = context.Capabilities.GetOptional<IUIManager>();

// Optional: Logging is helpful but not critical
var logger = context.Capabilities.GetOptional<ILogger>();
```

---

## Advanced Topics

### Capability Composition

**Pattern:** Build capabilities from other capabilities.

```csharp
public class LoggingFileSystemDecorator : IFileSystem
{
    private readonly IFileSystem _inner;
    private readonly ILogger _logger;

    public LoggingFileSystemDecorator(IFileSystem inner, ILogger logger)
    {
        _inner = inner;
        _logger = logger;
    }

    public async Task<string> ReadAllTextAsync(string path)
    {
        _logger.Log($"Reading file: {path}");
        var result = await _inner.ReadAllTextAsync(path);
        _logger.Log($"Read {result.Length} characters");
        return result;
    }
}

// Host composition
var logger = new ConsoleLogger();
var fileSystem = new FileSystem();
var loggingFileSystem = new LoggingFileSystemDecorator(fileSystem, logger);

var capabilities = CapabilityProviders.CreateBuilder()
    .Add<ILogger>(logger)
    .Add<IFileSystem>(loggingFileSystem) // ← Decorated
    .Build();
```

---

### Lazy Capabilities

**Pattern:** Defer capability creation until first access.

```csharp
public class LazyCapabilityProvider : ICapabilityProvider
{
    private readonly Dictionary<Type, Lazy<object>> _factories = new();

    public void Register<T>(Func<T> factory) where T : class
    {
        _factories[typeof(T)] = new Lazy<object>(() => factory());
    }

    public bool TryGet<TCapability>(out TCapability? capability)
        where TCapability : class
    {
        if (_factories.TryGetValue(typeof(TCapability), out var lazy))
        {
            capability = (TCapability)lazy.Value;
            return true;
        }

        capability = null;
        return false;
    }
}

// Usage
var provider = new LazyCapabilityProvider();
provider.Register<IExpensiveService>(() => new ExpensiveService());
// Service only created when first accessed
```

---

### Capability Validation

**Pattern:** Validate required capabilities at startup.

```csharp
public static class CapabilityValidator
{
    public static void ValidateRequirements(
        ICapabilityProvider capabilities,
        params Type[] requiredTypes)
    {
        var missing = new List<Type>();

        foreach (var type in requiredTypes)
        {
            var method = typeof(ICapabilityProvider).GetMethod("TryGet")
                .MakeGenericMethod(type);

            var parameters = new object[] { null };
            var result = (bool)method.Invoke(capabilities, parameters);

            if (!result)
            {
                missing.Add(type);
            }
        }

        if (missing.Any())
        {
            throw new InvalidOperationException(
                $"Missing required capabilities: {string.Join(", ", missing.Select(t => t.Name))}");
        }
    }
}

// Usage in module
public override ValueTask InitializeAsync(ModuleContext context, ...)
{
    CapabilityValidator.ValidateRequirements(
        context.Capabilities,
        typeof(ILogger),
        typeof(IDatabaseService));

    // If we get here, all requirements are met
}
```

---

## Related Patterns

- **[Context Objects](../context-objects/overview.md)** - Contexts carry capability providers
- **[Plugin Architecture](../plugin-architecture/visora-analysis.md)** - Modules access host via capabilities
- **[Testable Design](../testable-design/overview.md)** - Easy mocking via capability providers

---

## Further Reading

### Internal Documentation
- [Context Objects Pattern](../context-objects/overview.md)
- [Testable Design](../testable-design/overview.md)
- [Module Lifecycle](../module-lifecycle/overview.md)

### External Resources
- [Service Locator Anti-Pattern](https://blog.ploeh.dk/2010/02/03/ServiceLocatorisanAnti-Pattern/)
- [Dependency Injection Principles](https://martinfowler.com/articles/injection.html)

---

**Next:** [Meta-Platform Illustrations](./meta-platform-illustrations.md) - Cross-runtime capability access
