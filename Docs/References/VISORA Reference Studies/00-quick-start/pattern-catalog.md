# VISORA Platform Patterns - Complete Catalog

**Last Updated:** November 10, 2025
**VISORA Version:** .NET 9.0
**Document Type:** Quick Reference

---

## Overview

This catalog provides a comprehensive overview of all 16 architectural patterns used in the VISORA platform. Each pattern is presented with:
- **Name** - Official pattern name
- **One-sentence description** - Core concept
- **VISORA use case** - How VISORA implements it
- **Meta-platform relevance** - How it could adapt to polyglot scenarios
- **When to use** - Decision criteria

Patterns are organized by tier, from foundational (Tier 1) to advanced (Tier 5).

---

## Table of Contents

### [Tier 1: Foundational Patterns](#tier-1-foundational-patterns)
1. [Plugin Architecture](#1-plugin-architecture-tier-1)
2. [Layered Architecture](#2-layered-architecture-tier-1)
3. [Capability Negotiation](#3-capability-negotiation-tier-1)

### [Tier 2: Structural Patterns](#tier-2-structural-patterns)
4. [Module-Component-Command Hierarchy](#4-module-component-command-hierarchy-tier-2)
5. [Context Objects](#5-context-objects-tier-2)
6. [Immutable Metadata](#6-immutable-metadata-tier-2)

### [Tier 3: Behavioral Patterns](#tier-3-behavioral-patterns)
7. [Command Execution](#7-command-execution-tier-3)
8. [Template Method](#8-template-method-tier-3)
9. [Registry Pattern](#9-registry-pattern-tier-3)
10. [Result Objects](#10-result-objects-tier-3)

### [Tier 4: Implementation Patterns](#tier-4-implementation-patterns)
11. [Reflection-First Discovery](#11-reflection-first-discovery-tier-4)
12. [Factory Patterns](#12-factory-patterns-tier-4)
13. [Builder Pattern](#13-builder-pattern-tier-4)
14. [Module Lifecycle](#14-module-lifecycle-tier-4)

### [Tier 5: Advanced Patterns](#tier-5-advanced-patterns)
15. [Async Patterns](#15-async-patterns-tier-5)
16. [Multi-Surface Execution](#16-multi-surface-execution-tier-5)
17. [Testable Design](#bonus-testable-design-cross-cutting)

---

## Tier 1: Foundational Patterns

These patterns form the architectural foundation of VISORA and would be critical to any meta-platform implementation.

### 1. Plugin Architecture (Tier 1)

| Aspect | Details |
|--------|---------|
| **Description** | Isolate modules in unloadable assembly contexts with shared type contracts |
| **VISORA Use Case** | McMaster.NETCore.Plugins loads `.vixm.dll` assemblies with shared base types, enabling hot-swapping and isolation |
| **Meta-Platform Relevance** | Foundation for polyglot runtime hosting (Python, Node.js, etc.) with isolated execution contexts |
| **When to Use** | When you need: dynamic extensibility, hot-swapping, assembly isolation, version independence |
| **Related Patterns** | Registry Pattern (#9), Module Lifecycle (#14) |
| **Documentation** | [`patterns/plugin-architecture/visora-analysis.md`](../patterns/plugin-architecture/visora-analysis.md) |
| **Illustrations** | [`patterns/plugin-architecture/meta-platform-illustrations.md`](../patterns/plugin-architecture/meta-platform-illustrations.md) |

**Key Files:**
- `/src/Visora.Core/Modules/ModuleHandle.cs:56-78` - PluginLoader setup
- `/src/Visora.Core/Modules/ModuleCatalogOptions.cs:500-517` - Shared types configuration

**Quick Example:**
```csharp
var sharedTypes = options.GetSharedTypesArray();
var loader = PluginLoader.CreateFromAssemblyFile(
    assemblyPath,
    sharedTypes: sharedTypes,
    isUnloadable: true);
```

---

### 2. Layered Architecture (Tier 1)

| Aspect | Details |
|--------|---------|
| **Description** | Organize system into distinct layers with clear dependency flow (Contracts → Core → Hosts) |
| **VISORA Use Case** | Three layers: `Visora.Contracts` (interfaces), `Visora.Core` (implementation), `Visora.CLI/Terminal` (hosts) |
| **Meta-Platform Relevance** | Contract layer enables language-agnostic API definitions via IDL or shared protocols |
| **When to Use** | When you need: clear separation of concerns, testability, independent deployment of layers |
| **Related Patterns** | Plugin Architecture (#1), Capability Negotiation (#3) |
| **Documentation** | [`patterns/layered-architecture/overview.md`](../patterns/layered-architecture/overview.md) |

**Key Principles:**
- Contracts layer has **zero dependencies**
- Core layer depends only on Contracts
- Hosts depend on Core but don't know about each other
- Dependency inversion via interfaces

**Quick Example:**
```
Visora.Contracts (interfaces, abstractions)
     ↑ depends on
Visora.Core (implementations, infrastructure)
     ↑ depends on
Visora.CLI / Visora.Terminal (application hosts)
```

---

### 3. Capability Negotiation (Tier 1)

| Aspect | Details |
|--------|---------|
| **Description** | Type-safe runtime dependency negotiation without monolithic service containers |
| **VISORA Use Case** | `ICapabilityProvider` interface with `GetRequired<T>()` / `GetOptional<T>()` methods |
| **Meta-Platform Relevance** | Critical for cross-runtime service sharing (e.g., .NET calling Python services) |
| **When to Use** | When you need: explicit dependencies, optional services, host-controlled APIs, testability |
| **Related Patterns** | Context Objects (#5), Testable Design (#17) |
| **Documentation** | [`patterns/capability-negotiation/visora-analysis.md`](../patterns/capability-negotiation/visora-analysis.md) |
| **Illustrations** | [`patterns/capability-negotiation/meta-platform-illustrations.md`](../patterns/capability-negotiation/meta-platform-illustrations.md) |

**Key Files:**
- `/src/Visora.Contracts/Common/ICapabilityProvider.cs` - Interface definition
- `/src/Visora.Core/Capabilities/CapabilityProviders.cs` - Builder implementation

**Quick Example:**
```csharp
// Host provides capabilities
var capabilities = CapabilityProviders.CreateBuilder()
    .Add<IConsoleHost>(consoleHost)
    .Add<IUIManager>(uiManager)
    .Build();

// Module consumes capabilities
var console = context.Capabilities.GetOptional<IConsoleHost>();
var ui = context.Capabilities.GetRequired<IUIManager>();
```

---

## Tier 2: Structural Patterns

These patterns define the shape and organization of VISORA's domain model.

### 4. Module-Component-Command Hierarchy (Tier 2)

| Aspect | Details |
|--------|---------|
| **Description** | Three-level containment hierarchy for organizing functionality |
| **VISORA Use Case** | `VisoraModule` contains `VisoraComponent[]` which create `VisoraCommand[]` |
| **Meta-Platform Relevance** | Universal structure for organizing polyglot modules (Python packages, Node.js modules, etc.) |
| **When to Use** | When you need: logical grouping, discovery, lazy instantiation, clear ownership |
| **Related Patterns** | Reflection-First Discovery (#11), Template Method (#8) |
| **Documentation** | [`patterns/module-component-command/overview.md`](../patterns/module-component-command/overview.md) |

**Structure:**
```
VisoraModule (e.g., ShellCommandsModule)
    ├─ Descriptor: ModuleDescriptor
    ├─ DiscoverComponents() → VisoraComponent[]
    │
    ├─ VisoraComponent (e.g., CoreUtilitiesComponent)
    │   ├─ Descriptor: ComponentDescriptor
    │   └─ CreateCommands() → VisoraCommand[]
    │       │
    │       ├─ VisoraCommand (e.g., PingCommand)
    │       │   ├─ Descriptor: CommandDescriptor
    │       │   └─ ExecuteAsync() → CommandResult
```

**Key Files:**
- `/src/Visora.Contracts/Modules/VisoraModule.cs` - Module abstraction
- `/src/Visora.Contracts/Components/VisoraComponent.cs` - Component abstraction
- `/src/Visora.Contracts/Commands/VisoraCommand.cs` - Command abstraction

---

### 5. Context Objects (Tier 2)

| Aspect | Details |
|--------|---------|
| **Description** | Rich objects carrying runtime state, capabilities, and metadata to lifecycle methods |
| **VISORA Use Case** | `ModuleContext`, `ComponentContext`, `CommandContext` passed to Initialize/Execute methods |
| **Meta-Platform Relevance** | Standard way to pass runtime environment to polyglot code |
| **When to Use** | When you need: rich parameter passing, capability access, extensible metadata, testability |
| **Related Patterns** | Capability Negotiation (#3), Module Lifecycle (#14) |
| **Documentation** | [`patterns/context-objects/overview.md`](../patterns/context-objects/overview.md) |

**Context Types:**

| Context | Contains | Used By |
|---------|----------|---------|
| **ModuleContext** | Descriptor, Services, Capabilities, Properties | Module.InitializeAsync() |
| **ComponentContext** | Parent Module, Capabilities | Component.InitializeAsync() |
| **CommandContext** | Module, Component?, Surface, Capabilities, Parameters, CancellationToken | Command.ExecuteAsync() |

**Key Files:**
- `/src/Visora.Contracts/Modules/ModuleContext.cs` - Module context
- `/src/Visora.Contracts/Components/ComponentContext.cs` - Component context
- `/src/Visora.Contracts/Commands/CommandContext.cs` - Command context

**Quick Example:**
```csharp
public override async ValueTask<CommandResult> ExecuteAsync(
    CommandContext context,
    CancellationToken cancellationToken = default)
{
    var service = context.Capabilities.GetRequired<IMyService>();
    var moduleId = context.Module.Descriptor.Id;
    var surface = context.Surface; // Programmatic, TextShell, Ui, etc.

    // Use context to access everything needed
    return CommandResult.Success($"Executed on {surface}");
}
```

---

### 6. Immutable Metadata (Tier 2)

| Aspect | Details |
|--------|---------|
| **Description** | Use sealed record types for all metadata and descriptors |
| **VISORA Use Case** | `ModuleDescriptor`, `ComponentDescriptor`, `CommandDescriptor` are sealed records |
| **Meta-Platform Relevance** | Serialization-friendly metadata for cross-runtime communication |
| **When to Use** | When you need: immutability, value semantics, serialization, thread-safety |
| **Related Patterns** | Factory Patterns (#12), Result Objects (#10) |
| **Documentation** | [`patterns/immutable-metadata/overview.md`](../patterns/immutable-metadata/overview.md) |

**Descriptor Types:**

| Descriptor | Properties | Factory Method |
|------------|------------|----------------|
| **ModuleDescriptor** | Id, Name, Version, Description, Tags, RuntimeHints | `ModuleDescriptor.Create(...)` |
| **ComponentDescriptor** | Id, Name, Description, Tags, Kind | `ComponentDescriptor.Create(...)` |
| **CommandDescriptor** | Id, Title, Description, Kind, Aliases, Keywords, IsVisible, Ui | `CommandDescriptor.Create(...)` |

**Key Files:**
- `/src/Visora.Contracts/Modules/ModuleDescriptor.cs` - Module metadata
- `/src/Visora.Contracts/Components/ComponentDescriptor.cs` - Component metadata
- `/src/Visora.Contracts/Commands/CommandDescriptor.cs` - Command metadata

**Quick Example:**
```csharp
private static readonly CommandDescriptor Info = CommandDescriptor.Create(
    id: "shell.ping",
    title: "Ping",
    description: "Checks connectivity with the Visora host.",
    kind: CommandKind.Automation,
    keywords: new[] { "diagnostics", "ping" });
```

---

## Tier 3: Behavioral Patterns

These patterns define how VISORA components interact and behave at runtime.

### 7. Command Execution (Tier 3)

| Aspect | Details |
|--------|---------|
| **Description** | Encapsulate user-facing operations as command objects with async execution |
| **VISORA Use Case** | `VisoraCommand.ExecuteAsync(CommandContext)` returns `CommandResult` |
| **Meta-Platform Relevance** | Universal command pattern for cross-runtime operations |
| **When to Use** | When you need: user-facing operations, undo/redo, logging, async execution |
| **Related Patterns** | Result Objects (#10), Context Objects (#5) |
| **Documentation** | [`patterns/command-execution/overview.md`](../patterns/command-execution/overview.md) |

**Command Structure:**
```csharp
public abstract class VisoraCommand
{
    public abstract CommandDescriptor Descriptor { get; }

    public virtual ValueTask<CommandResult> ExecuteAsync(
        CommandContext context,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult(CommandResult.Success());
}
```

**Key Files:**
- `/src/Visora.Contracts/Commands/VisoraCommand.cs` - Base class
- `/src/Visora.Shell.Commands.Core/Commands/PingCommand.cs` - Example implementation

---

### 8. Template Method (Tier 3)

| Aspect | Details |
|--------|---------|
| **Description** | Define algorithm skeleton in base class with overridable steps |
| **VISORA Use Case** | `VisoraModule`, `VisoraComponent` base classes define lifecycle hooks |
| **Meta-Platform Relevance** | Standard lifecycle pattern for any runtime |
| **When to Use** | When you need: shared infrastructure, customizable steps, lifecycle management |
| **Related Patterns** | Module Lifecycle (#14), Async Patterns (#15) |
| **Documentation** | [`patterns/template-method/overview.md`](../patterns/template-method/overview.md) |

**Lifecycle Hooks:**

**Module Lifecycle:**
```csharp
public abstract class VisoraModule
{
    public abstract ModuleDescriptor Descriptor { get; }

    public virtual ValueTask InitializeAsync(ModuleContext context, CT ct)
        => ValueTask.CompletedTask;

    public virtual IEnumerable<Type> DiscoverComponents(ModuleDiscoveryContext context)
        => context.EnumerateComponentCandidates()...;

    public virtual ValueTask ShutdownAsync(ModuleContext context, CT ct)
        => ValueTask.CompletedTask;

    public virtual ValueTask DisposeAsync()
        => ValueTask.CompletedTask;
}
```

**Component Lifecycle:**
```csharp
public abstract class VisoraComponent
{
    public abstract ComponentDescriptor Descriptor { get; }

    public virtual ValueTask InitializeAsync(ComponentContext context, CT ct)
        => ValueTask.CompletedTask;

    public virtual ValueTask ActivateAsync(ComponentContext context, CT ct)
        => ValueTask.CompletedTask;

    public virtual ValueTask DeactivateAsync(ComponentContext context, CT ct)
        => ValueTask.CompletedTask;

    public virtual IEnumerable<VisoraCommand> CreateCommands(ComponentContext context)
        => Array.Empty<VisoraCommand>();

    public virtual ValueTask DisposeAsync()
        => ValueTask.CompletedTask;
}
```

---

### 9. Registry Pattern (Tier 3)

| Aspect | Details |
|--------|---------|
| **Description** | Central repository for tracking and looking up loaded modules |
| **VISORA Use Case** | `ModuleCatalog` maintains `ModuleHandle[]` with lookup by ID |
| **Meta-Platform Relevance** | Essential for tracking polyglot runtimes and modules |
| **When to Use** | When you need: centralized tracking, lookup by ID, lifecycle management |
| **Related Patterns** | Plugin Architecture (#1), Module Lifecycle (#14) |
| **Documentation** | [`patterns/registry-pattern/overview.md`](../patterns/registry-pattern/overview.md) |

**Key Operations:**
```csharp
public sealed class ModuleCatalog : IAsyncDisposable
{
    public IReadOnlyList<ModuleHandle> Modules { get; }

    // Discover, load, and register modules
    public async Task DiscoverAsync(ModuleCatalogOptions options, CT ct);

    // Lookup by ID
    public ModuleHandle? GetById(string moduleId);

    // Cleanup all modules
    public async ValueTask DisposeAsync();
}
```

**Key Files:**
- `/src/Visora.Core/Modules/ModuleCatalog.cs` - Registry implementation
- `/src/Visora.Core/Modules/ModuleLocator.cs` - Discovery logic

---

### 10. Result Objects (Tier 3)

| Aspect | Details |
|--------|---------|
| **Description** | Use data objects instead of exceptions for expected operation outcomes |
| **VISORA Use Case** | `CommandResult` with `Outcome` enum (Success/Cancelled/Failed) plus optional message/payload |
| **Meta-Platform Relevance** | Language-agnostic result pattern (no exception semantics) |
| **When to Use** | When you need: user-facing operations, expected failures, cancellation, serializable results |
| **Related Patterns** | Command Execution (#7), Immutable Metadata (#6) |
| **Documentation** | [`patterns/result-objects/overview.md`](../patterns/result-objects/overview.md) |

**CommandResult Structure:**
```csharp
public readonly record struct CommandResult(
    CommandOutcome Outcome,
    string? Message = null,
    object? Payload = null)
{
    public static CommandResult Success(string? message = null, object? payload = null);
    public static CommandResult Cancelled(string? message = null);
    public static CommandResult Failed(string? message = null, object? payload = null);

    public enum CommandOutcome { Success, Cancelled, Failed }
}
```

**Key Files:**
- `/src/Visora.Contracts/Commands/CommandResult.cs` - Result definition

**Quick Example:**
```csharp
public override async ValueTask<CommandResult> ExecuteAsync(
    CommandContext context,
    CancellationToken cancellationToken = default)
{
    try
    {
        var result = await DoWorkAsync(cancellationToken);
        return CommandResult.Success("Work completed", result);
    }
    catch (OperationCanceledException)
    {
        return CommandResult.Cancelled("User cancelled");
    }
    catch (Exception ex)
    {
        return CommandResult.Failed($"Error: {ex.Message}");
    }
}
```

---

## Tier 4: Implementation Patterns

These patterns guide the implementation details of VISORA's architecture.

### 11. Reflection-First Discovery (Tier 4)

| Aspect | Details |
|--------|---------|
| **Description** | Use assembly scanning and reflection instead of manifest files for discovery |
| **VISORA Use Case** | `Assembly.GetTypes()` finds `VisoraModule`/`VisoraComponent` implementations automatically |
| **Meta-Platform Relevance** | Conceptual equivalent in Python (inspect module), Node.js (require.cache), etc. |
| **When to Use** | When you need: self-describing modules, no config files, AI-friendly code structure |
| **Related Patterns** | Plugin Architecture (#1), Module-Component-Command Hierarchy (#4) |
| **Documentation** | [`patterns/reflection-discovery/overview.md`](../patterns/reflection-discovery/overview.md) |

**Discovery Pattern:**
```csharp
// In ModuleHandle.LoadAsync
var assembly = loader.LoadDefaultAssembly();
var moduleType = assembly
    .GetTypes()
    .FirstOrDefault(t => typeof(VisoraModule).IsAssignableFrom(t) && !t.IsAbstract);

// In ModuleDiscoveryContext.EnumerateComponentCandidates
foreach (var type in ModuleAssembly.GetTypes())
{
    if (type.IsAbstract) continue;
    if (type.IsInterface) continue;
    if (type.IsNestedPrivate) continue;
    if (typeof(VisoraComponent).IsAssignableFrom(type))
        yield return type;
}
```

**Key Files:**
- `/src/Visora.Core/Modules/ModuleHandle.cs:59-83` - Module discovery
- `/src/Visora.Contracts/Modules/ModuleDiscoveryContext.cs` - Component discovery

---

### 12. Factory Patterns (Tier 4)

| Aspect | Details |
|--------|---------|
| **Description** | Use static factory methods for object creation instead of constructors |
| **VISORA Use Case** | `Descriptor.Create()`, `ModuleHandle.LoadAsync()` |
| **Meta-Platform Relevance** | Standard pattern across all languages |
| **When to Use** | When you need: complex initialization, async creation, validation, named constructors |
| **Related Patterns** | Immutable Metadata (#6), Module Lifecycle (#14) |
| **Documentation** | [`patterns/factory-patterns/overview.md`](../patterns/factory-patterns/overview.md) |

**Factory Examples:**

**Descriptor Factory:**
```csharp
// Static factory methods on records
var descriptor = ModuleDescriptor.Create(
    id: "my.module",
    name: "My Module",
    version: new Version(1, 0, 0),
    description: "Optional description");
```

**Async Factory:**
```csharp
// Async factory for complex initialization
var handle = await ModuleHandle.LoadAsync(assemblyPath, options, ct);
```

**Builder Factory:**
```csharp
// Builder pattern for complex objects
var capabilities = CapabilityProviders.CreateBuilder()
    .Add<IService1>(service1)
    .Add<IService2>(service2)
    .Build();
```

---

### 13. Builder Pattern (Tier 4)

| Aspect | Details |
|--------|---------|
| **Description** | Construct complex objects step-by-step with fluent API |
| **VISORA Use Case** | `CapabilityProviderBuilder`, `ModuleCatalogOptions` |
| **Meta-Platform Relevance** | Universal pattern for configuration objects |
| **When to Use** | When you need: many optional parameters, fluent API, immutable result |
| **Related Patterns** | Capability Negotiation (#3), Factory Patterns (#12) |
| **Documentation** | [`patterns/builder-pattern/overview.md`](../patterns/builder-pattern/overview.md) |

**Builder Examples:**

**Capability Builder:**
```csharp
public sealed class CapabilityProviderBuilder
{
    private readonly Dictionary<Type, object> _registrations = new();

    public CapabilityProviderBuilder Add<TCapability>(TCapability capability)
    {
        _registrations[typeof(TCapability)] = capability;
        return this; // Fluent API
    }

    public ICapabilityProvider Build()
    {
        if (_registrations.Count == 0)
            return CapabilityProviders.Empty;
        return new DictionaryCapabilityProvider(_registrations);
    }
}
```

**Options Builder:**
```csharp
var options = new ModuleCatalogOptions
{
    RecurseSubdirectories = true,
    SearchPattern = "*.vixm.dll"
};
options.ProbingPaths.Add(@"C:\modules");
options.ProbingPaths.Add(@"./local");
```

**Key Files:**
- `/src/Visora.Core/Capabilities/CapabilityProviders.cs:32-49` - Builder implementation

---

### 14. Module Lifecycle (Tier 4)

| Aspect | Details |
|--------|---------|
| **Description** | Multi-phase lifecycle: Load → Initialize → Active → Shutdown → Dispose |
| **VISORA Use Case** | `ModuleHandle` orchestrates module lifecycle with idempotent operations |
| **Meta-Platform Relevance** | Standard lifecycle for any runtime (init, start, stop, cleanup) |
| **When to Use** | When you need: resource management, hot-swapping, graceful shutdown |
| **Related Patterns** | Plugin Architecture (#1), Template Method (#8) |
| **Documentation** | [`patterns/module-lifecycle/overview.md`](../patterns/module-lifecycle/overview.md) |

**Lifecycle Phases:**

```
1. LOAD
   ModuleHandle.LoadAsync(path, options, ct)
   → Assembly loaded into isolated context
   → VisoraModule instantiated
   → ModuleContext created

2. INITIALIZE (Idempotent)
   ModuleHandle.EnsureInitializedAsync(ct)
   → Module.InitializeAsync(context, ct)
   → _initialized flag set

3. ACTIVE
   ModuleHandle.InspectAsync(ct)
   → Deep component discovery
   → Command enumeration
   → Temporary instances cleaned up

4. SHUTDOWN (Idempotent)
   ModuleHandle.ShutdownAsync(ct)
   → Module.ShutdownAsync(context, ct)
   → _initialized flag cleared

5. DISPOSE
   ModuleHandle.DisposeAsync()
   → Module.DisposeAsync()
   → PluginLoader.Dispose()
   → Assembly unloaded
```

**Key Files:**
- `/src/Visora.Core/Modules/ModuleHandle.cs:51-157` - Lifecycle implementation

---

## Tier 5: Advanced Patterns

These patterns address performance, scalability, and advanced runtime scenarios.

### 15. Async Patterns (Tier 5)

| Aspect | Details |
|--------|---------|
| **Description** | Async/await everywhere with CancellationToken support and ValueTask optimization |
| **VISORA Use Case** | All lifecycle methods are async, use `ValueTask` for hot paths |
| **Meta-Platform Relevance** | Essential for cross-runtime async coordination (async/await in Python, promises in Node.js) |
| **When to Use** | Always for: I/O operations, lifecycle methods, long-running operations |
| **Related Patterns** | Module Lifecycle (#14), Command Execution (#7) |
| **Documentation** | [`patterns/async-patterns/overview.md`](../patterns/async-patterns/overview.md) |

**Async Best Practices:**

**1. ValueTask for Performance:**
```csharp
// Use ValueTask for potentially synchronous paths
public virtual ValueTask InitializeAsync(
    ModuleContext context,
    CancellationToken cancellationToken = default)
    => ValueTask.CompletedTask; // No allocation if synchronous
```

**2. CancellationToken Everywhere:**
```csharp
public async Task<ModuleInspection> InspectAsync(
    CancellationToken cancellationToken = default)
{
    foreach (var componentType in componentTypes)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // ... work
    }
}
```

**3. ConfigureAwait(false) in Libraries:**
```csharp
await Module.InitializeAsync(_context, cancellationToken)
    .ConfigureAwait(false);
```

**4. Proper Async Disposal:**
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
        _loader.Dispose(); // Sync disposal last
    }
}
```

---

### 16. Multi-Surface Execution (Tier 5)

| Aspect | Details |
|--------|---------|
| **Description** | Single command implementation runs on multiple execution surfaces (CLI, UI, automation, remote) |
| **VISORA Use Case** | `CommandContext.Surface` enum identifies invocation channel |
| **Meta-Platform Relevance** | Critical for executing same logic across different runtimes/UIs |
| **When to Use** | When you need: unified command model, surface-specific behavior, testability |
| **Related Patterns** | Command Execution (#7), Context Objects (#5) |
| **Documentation** | [`patterns/multi-surface-execution/overview.md`](../patterns/multi-surface-execution/overview.md) |

**Execution Surfaces:**
```csharp
public enum CommandSurface
{
    Programmatic,  // Called from code
    TextShell,     // CLI/terminal
    Ui,            // GUI (WPF, etc.)
    Automation,    // Scripts, agents
    Remote         // RPC, HTTP, etc.
}
```

**Surface-Aware Command:**
```csharp
public override async ValueTask<CommandResult> ExecuteAsync(
    CommandContext context,
    CancellationToken cancellationToken = default)
{
    var result = await DoWorkAsync(context.Parameters);

    // Surface-specific formatting
    var message = context.Surface switch
    {
        CommandSurface.TextShell => FormatForTerminal(result),
        CommandSurface.Ui => FormatForGui(result),
        CommandSurface.Automation => FormatForScript(result),
        _ => result.ToString()
    };

    return CommandResult.Success(message, result);
}
```

**Key Files:**
- `/src/Visora.Contracts/Commands/CommandContext.cs` - Surface enum

---

## Bonus: Testable Design (Cross-Cutting)

### 17. Testable Design (Cross-Cutting)

| Aspect | Details |
|--------|---------|
| **Description** | Architecture optimized for unit and integration testing |
| **VISORA Use Case** | Abstract base classes, capability mocking, record equality, no static dependencies |
| **Meta-Platform Relevance** | Essential for testing cross-runtime integrations |
| **When to Use** | Always - design for testability from the start |
| **Related Patterns** | Capability Negotiation (#3), Immutable Metadata (#6) |
| **Documentation** | [`patterns/testable-design/overview.md`](../patterns/testable-design/overview.md) |

**Testability Features:**

**1. Mock Capabilities:**
```csharp
var mockService = new Mock<IMyService>();
var capabilities = CapabilityProviders.CreateBuilder()
    .Add<IMyService>(mockService.Object)
    .Build();
```

**2. Mock Modules:**
```csharp
var mockModule = new Mock<VisoraModule>();
mockModule.Setup(m => m.Descriptor).Returns(descriptor);

var context = new CommandContext(
    module: mockModule.Object,
    component: null,
    surface: CommandSurface.Programmatic,
    capabilities: CapabilityProviders.Empty);
```

**3. Record Equality:**
```csharp
var expected = CommandDescriptor.Create(
    id: "shell.ping",
    title: "Ping");

var actual = command.Descriptor;

Assert.AreEqual(expected, actual); // Value equality
```

**4. No Static State:**
```csharp
// ❌ BAD - Global state
public static IServiceProvider GlobalServices;

// ✅ GOOD - Passed through context
public override async ValueTask<CommandResult> ExecuteAsync(
    CommandContext context, ...)
{
    var service = context.Capabilities.GetRequired<IService>();
}
```

---

## Pattern Relationships

### Dependency Graph

```
Tier 1: Foundational
├─ Plugin Architecture (#1)
│  ├─ Registry Pattern (#9)
│  └─ Module Lifecycle (#14)
│
├─ Layered Architecture (#2)
│  └─ Plugin Architecture (#1)
│
└─ Capability Negotiation (#3)
   ├─ Context Objects (#5)
   └─ Builder Pattern (#13)

Tier 2: Structural
├─ Module-Component-Command Hierarchy (#4)
│  ├─ Template Method (#8)
│  └─ Reflection-First Discovery (#11)
│
├─ Context Objects (#5)
│  └─ Capability Negotiation (#3)
│
└─ Immutable Metadata (#6)
   └─ Factory Patterns (#12)

Tier 3: Behavioral
├─ Command Execution (#7)
│  ├─ Result Objects (#10)
│  └─ Multi-Surface Execution (#16)
│
├─ Template Method (#8)
│  └─ Module Lifecycle (#14)
│
├─ Registry Pattern (#9)
│  └─ Plugin Architecture (#1)
│
└─ Result Objects (#10)
   └─ Immutable Metadata (#6)

Tier 4: Implementation
├─ Reflection-First Discovery (#11)
│  └─ Plugin Architecture (#1)
│
├─ Factory Patterns (#12)
│  └─ Builder Pattern (#13)
│
├─ Builder Pattern (#13)
│  └─ Capability Negotiation (#3)
│
└─ Module Lifecycle (#14)
   ├─ Async Patterns (#15)
   └─ Template Method (#8)

Tier 5: Advanced
├─ Async Patterns (#15)
│  └─ All async operations
│
└─ Multi-Surface Execution (#16)
   └─ Command Execution (#7)

Cross-Cutting
└─ Testable Design (#17)
   └─ All patterns
```

---

## Pattern Selection Guide

### By Use Case

| Use Case | Recommended Patterns |
|----------|---------------------|
| **Building a new module** | Plugin Architecture (#1), Module Lifecycle (#14), Template Method (#8) |
| **Adding commands** | Command Execution (#7), Result Objects (#10), Context Objects (#5) |
| **Accessing host services** | Capability Negotiation (#3), Context Objects (#5) |
| **Creating metadata** | Immutable Metadata (#6), Factory Patterns (#12) |
| **Testing modules** | Testable Design (#17), Capability Negotiation (#3) |
| **Dynamic discovery** | Reflection-First Discovery (#11), Registry Pattern (#9) |
| **Cross-runtime integration** | Plugin Architecture (#1), Capability Negotiation (#3), Multi-Surface Execution (#16) |
| **Async operations** | Async Patterns (#15), Module Lifecycle (#14) |

### By Problem Domain

| Problem | Solution Patterns |
|---------|------------------|
| **"How do I load modules dynamically?"** | Plugin Architecture (#1), Registry Pattern (#9) |
| **"How do modules access host APIs?"** | Capability Negotiation (#3), Context Objects (#5) |
| **"How do I organize functionality?"** | Module-Component-Command Hierarchy (#4), Layered Architecture (#2) |
| **"How do I handle user operations?"** | Command Execution (#7), Result Objects (#10) |
| **"How do I make code testable?"** | Testable Design (#17), Capability Negotiation (#3) |
| **"How do I discover components?"** | Reflection-First Discovery (#11), Template Method (#8) |
| **"How do I manage lifecycle?"** | Module Lifecycle (#14), Async Patterns (#15) |
| **"How do I create objects?"** | Factory Patterns (#12), Builder Pattern (#13) |

### By Meta-Platform Goal

| Goal | Critical Patterns |
|------|------------------|
| **Hosting Python runtime** | Plugin Architecture (#1), Capability Negotiation (#3), Module Lifecycle (#14) |
| **Hosting Node.js runtime** | Plugin Architecture (#1), Capability Negotiation (#3), Async Patterns (#15) |
| **Cross-runtime commands** | Command Execution (#7), Multi-Surface Execution (#16), Result Objects (#10) |
| **Unified service layer** | Capability Negotiation (#3), Layered Architecture (#2), Registry Pattern (#9) |
| **Polyglot discovery** | Reflection-First Discovery (#11), Immutable Metadata (#6) |

---

## Quick Reference Tables

### Pattern Summary Matrix

| # | Pattern | Tier | Complexity | VISORA | Meta-Platform | Documentation |
|---|---------|------|------------|--------|---------------|---------------|
| 1 | Plugin Architecture | 1 | High | ✓ Complete | Critical | [Deep Dive](../patterns/plugin-architecture/visora-analysis.md) |
| 2 | Layered Architecture | 1 | Medium | ✓ Complete | Critical | [Overview](../patterns/layered-architecture/overview.md) |
| 3 | Capability Negotiation | 1 | Medium | ✓ Complete | Critical | [Deep Dive](../patterns/capability-negotiation/visora-analysis.md) |
| 4 | Module-Component-Command | 2 | Medium | ✓ Complete | High | [Overview](../patterns/module-component-command/overview.md) |
| 5 | Context Objects | 2 | Low | ✓ Complete | High | [Overview](../patterns/context-objects/overview.md) |
| 6 | Immutable Metadata | 2 | Low | ✓ Complete | High | [Overview](../patterns/immutable-metadata/overview.md) |
| 7 | Command Execution | 3 | Medium | ✓ Complete | High | [Overview](../patterns/command-execution/overview.md) |
| 8 | Template Method | 3 | Low | ✓ Complete | Medium | [Overview](../patterns/template-method/overview.md) |
| 9 | Registry Pattern | 3 | Medium | ✓ Complete | High | [Overview](../patterns/registry-pattern/overview.md) |
| 10 | Result Objects | 3 | Low | ✓ Complete | High | [Overview](../patterns/result-objects/overview.md) |
| 11 | Reflection-First Discovery | 4 | High | ✓ Complete | Medium | [Overview](../patterns/reflection-discovery/overview.md) |
| 12 | Factory Patterns | 4 | Low | ✓ Complete | Low | [Overview](../patterns/factory-patterns/overview.md) |
| 13 | Builder Pattern | 4 | Low | ✓ Complete | Low | [Overview](../patterns/builder-pattern/overview.md) |
| 14 | Module Lifecycle | 4 | High | ✓ Complete | High | [Overview](../patterns/module-lifecycle/overview.md) |
| 15 | Async Patterns | 5 | Medium | ✓ Complete | High | [Overview](../patterns/async-patterns/overview.md) |
| 16 | Multi-Surface Execution | 5 | Medium | ✓ Complete | High | [Overview](../patterns/multi-surface-execution/overview.md) |
| 17 | Testable Design | ∞ | Low | ✓ Complete | High | [Overview](../patterns/testable-design/overview.md) |

---

## Navigation

### By Tier
- [← Tier 1: Foundational](#tier-1-foundational-patterns)
- [← Tier 2: Structural](#tier-2-structural-patterns)
- [← Tier 3: Behavioral](#tier-3-behavioral-patterns)
- [← Tier 4: Implementation](#tier-4-implementation-patterns)
- [← Tier 5: Advanced](#tier-5-advanced-patterns)

### Related Documentation
- [COMPREHENSIVE_ARCHITECTURE_ANALYSIS.md](../../COMPREHENSIVE_ARCHITECTURE_ANALYSIS.md) - Full technical details
- [PATTERNS_QUICK_REFERENCE.md](../../PATTERNS_QUICK_REFERENCE.md) - Quick lookup guide
- [References Index](../index.md) - Documentation home

### Deep Dives (This Batch)
- [Plugin Architecture - VISORA Analysis](../patterns/plugin-architecture/visora-analysis.md)
- [Plugin Architecture - Meta-Platform Illustrations](../patterns/plugin-architecture/meta-platform-illustrations.md)
- [Capability Negotiation - VISORA Analysis](../patterns/capability-negotiation/visora-analysis.md)
- [Capability Negotiation - Meta-Platform Illustrations](../patterns/capability-negotiation/meta-platform-illustrations.md)

---

**Document Status:** ✓ Complete
**Next Steps:** Explore individual pattern deep-dives for implementation details and meta-platform adaptations.
