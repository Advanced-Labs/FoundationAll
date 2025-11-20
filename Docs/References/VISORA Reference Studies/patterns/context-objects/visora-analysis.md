# Context Objects Pattern - VISORA Deep Dive

**Last Updated:** November 10, 2025
**VISORA Version:** .NET 9.0
**Pattern Tier:** Tier 1 (Foundational)
**Related Patterns:** Capability Negotiation, Parameter Objects, Dependency Injection

---

## Table of Contents

1. [Pattern Overview](#pattern-overview)
2. [Why Context Objects?](#why-context-objects)
3. [VISORA Implementation](#visora-implementation)
4. [Code Examples](#code-examples)
5. [File References](#file-references)
6. [Context Composition](#context-composition)
7. [Propagation Patterns](#propagation-patterns)
8. [Immutability and Thread Safety](#immutability-and-thread-safety)
9. [Capabilities Access](#capabilities-access)
10. [Testing Patterns](#testing-patterns)
11. [Best Practices](#best-practices)
12. [Advanced Topics](#advanced-topics)

---

## Pattern Overview

### What are Context Objects?

**Definition:** A pattern where rich parameter objects encapsulate all runtime context (services, capabilities, configuration, state) needed by modules, components, or commands, replacing primitive parameters and avoiding parameter explosion.

**Key Characteristics:**
- **Single Parameter:** One context object instead of many parameters
- **Rich Metadata:** Contains descriptors, capabilities, services, properties
- **Hierarchical:** ModuleContext → ComponentContext → CommandContext
- **Immutable:** Once created, cannot be modified (thread-safe)
- **Type-Safe:** Strongly-typed access to capabilities

### Core Hierarchy

```
┌───────────────────────────────────────────────────────────┐
│                    ModuleContext                          │
│  ┌─────────────────────────────────────────────────────┐  │
│  │  - Descriptor: ModuleDescriptor                     │  │
│  │  - Services: IServiceProvider?                      │  │
│  │  - Capabilities: ICapabilityProvider                │  │
│  │  - Properties: IReadOnlyDictionary<string, object?> │  │
│  └─────────────────────────────────────────────────────┘  │
│                           ↓                                │
│              Used by VisoraModule lifecycle               │
│                           ↓                                │
└───────────────────────────────────────────────────────────┘
                            ↓
┌───────────────────────────────────────────────────────────┐
│                   ComponentContext                        │
│  ┌─────────────────────────────────────────────────────┐  │
│  │  - Module: VisoraModule                             │  │
│  │  - Capabilities: ICapabilityProvider                │  │
│  └─────────────────────────────────────────────────────┘  │
│                           ↓                                │
│             Used by VisoraComponent lifecycle             │
│                           ↓                                │
└───────────────────────────────────────────────────────────┘
                            ↓
┌───────────────────────────────────────────────────────────┐
│                    CommandContext                         │
│  ┌─────────────────────────────────────────────────────┐  │
│  │  - Module: VisoraModule                             │  │
│  │  - Component: VisoraComponent?                      │  │
│  │  - Surface: CommandSurface                          │  │
│  │  - Capabilities: ICapabilityProvider                │  │
│  │  - Parameters: IReadOnlyDictionary<string, object?> │  │
│  │  - CancellationToken: CancellationToken             │  │
│  └─────────────────────────────────────────────────────┘  │
│                           ↓                                │
│              Used by VisoraCommand execution              │
│                           ↓                                │
└───────────────────────────────────────────────────────────┘
```

---

## Why Context Objects?

### Problem: Parameter Explosion

**Without Context Objects:**

```csharp
// ❌ PROBLEM: Too many parameters, hard to evolve
public class MyModule : VisoraModule
{
    public override ValueTask InitializeAsync(
        IServiceProvider services,
        ILogger logger,
        IConsoleHost console,
        IFileSystem fileSystem,
        IConfiguration config,
        IDictionary<string, object> properties,
        string moduleName,
        string moduleVersion,
        CancellationToken cancellationToken)
    {
        // Which parameters are required?
        // What if we need to add more?
        // How do we make some optional?
    }
}

// ❌ PROBLEM: Breaking changes when adding parameters
// Version 1:
public ValueTask InitializeAsync(ILogger logger, IConsoleHost console);

// Version 2 (BREAKING!):
public ValueTask InitializeAsync(ILogger logger, IConsoleHost console, IFileSystem fs);
```

**Problems:**
- ❌ Too many parameters (cognitive overload)
- ❌ Hard to add new parameters (breaking changes)
- ❌ Unclear which are required vs optional
- ❌ No structure or organization
- ❌ Difficult to mock for testing

---

### Solution: Context Objects

**With Context Objects:**

```csharp
// ✅ SOLUTION: Single context parameter
public class MyModule : VisoraModule
{
    public override ValueTask InitializeAsync(
        ModuleContext context,  // ← Single parameter!
        CancellationToken cancellationToken)
    {
        // Extract what we need
        var logger = context.Capabilities.GetOptional<ILogger>();
        var console = context.Capabilities.GetRequired<IConsoleHost>();
        var fs = context.Capabilities.GetOptional<IFileSystem>();

        // Access metadata
        var moduleName = context.Descriptor.Name;
        var moduleVersion = context.Descriptor.Version;

        // Access host services
        var registry = context.Services?.GetService<IRegistry>();

        // Access properties
        var debugMode = context.Properties.TryGetValue("debug", out var debug)
            ? (bool)debug
            : false;
    }
}

// ✅ SOLUTION: Non-breaking evolution
// Version 1:
public sealed class ModuleContext
{
    public ModuleDescriptor Descriptor { get; }
    public ICapabilityProvider Capabilities { get; }
}

// Version 2 (NON-BREAKING!):
public sealed class ModuleContext
{
    public ModuleDescriptor Descriptor { get; }
    public ICapabilityProvider Capabilities { get; }
    public IServiceProvider? Services { get; }  // ← Added, existing code works
}
```

**Benefits:**
- ✅ Single parameter (simple signature)
- ✅ Non-breaking evolution (add to context)
- ✅ Clear structure (organized by concern)
- ✅ Optional vs required (explicit in usage)
- ✅ Easy to mock for testing

---

### Key Decision Points

**Why Not Primitive Parameters?**
- ❌ Parameter explosion (10+ parameters common)
- ❌ Breaking changes when adding features
- ❌ No organization

**Why Not Service Locator?**
```csharp
// ❌ ANTI-PATTERN: Global service locator
GlobalServices.Get<ILogger>();  // Hidden dependency, hard to test
```
- ❌ Hidden dependencies
- ❌ Global state
- ❌ Hard to test

**Why Not Dependency Injection Container?**
```csharp
// ❌ PROBLEM: Modules can't use constructor injection
public class MyModule : VisoraModule
{
    // Constructor with parameters never called!
    // Modules are created via Activator.CreateInstance()
    public MyModule(ILogger logger, IConsoleHost console) { }
}
```
- ❌ Modules created by reflection (parameterless constructor)
- ❌ Can't use standard DI constructor injection
- ❌ Context objects solve this

**Why Context Objects?**
- ✅ Single parameter pattern
- ✅ Non-breaking evolution
- ✅ Explicit dependencies
- ✅ Testable
- ✅ Type-safe access

---

## VISORA Implementation

### Three Context Types

VISORA defines three context types for the three-level hierarchy:

```
1. ModuleContext
   ├─ Used by: VisoraModule.InitializeAsync(), ShutdownAsync()
   ├─ Scope: Module lifetime
   └─ Contains: Descriptor, Services, Capabilities, Properties

2. ComponentContext
   ├─ Used by: VisoraComponent.InitializeAsync(), ActivateAsync(), etc.
   ├─ Scope: Component lifetime
   └─ Contains: Module, Capabilities

3. CommandContext
   ├─ Used by: VisoraCommand.ExecuteAsync()
   ├─ Scope: Single command execution
   └─ Contains: Module, Component, Surface, Capabilities, Parameters, CancellationToken
```

### Design Principles

1. **Immutability**
   - All context objects are immutable after construction
   - Properties are `{ get; }` only
   - Collections are `IReadOnlyDictionary` or `IReadOnlyList`

2. **Progressive Disclosure**
   - ModuleContext: Most complete (module initialization)
   - ComponentContext: Subset (component needs)
   - CommandContext: Most specific (command execution)

3. **Capability-Based**
   - All contexts include `ICapabilityProvider`
   - Capabilities flow through hierarchy
   - Type-safe access pattern

4. **No Nulls (Except Optional)**
   - Required properties are non-nullable
   - Optional properties are nullable with `?`
   - Clear which can be null

---

## Code Examples

### Example 1: ModuleContext

**File:** `/src/Visora.Contracts/Modules/ModuleContext.cs`

```csharp
/// <summary>
/// Context supplied to modules during initialization and lifecycle events.
/// </summary>
public sealed class ModuleContext
{
    public ModuleContext(
        ModuleDescriptor descriptor,
        IServiceProvider? services,
        ICapabilityProvider capabilities,
        IReadOnlyDictionary<string, object?>? properties = null)
    {
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        Services = services;  // ← Optional
        Capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
        Properties = properties ?? EmptyProperties;
    }

    private static readonly IReadOnlyDictionary<string, object?> EmptyProperties =
        new Dictionary<string, object?>(capacity: 0);

    /// <summary>
    /// Descriptor provided by the module.
    /// </summary>
    public ModuleDescriptor Descriptor { get; }

    /// <summary>
    /// Optional host service provider.
    /// </summary>
    public IServiceProvider? Services { get; }  // ← Nullable

    /// <summary>
    /// Provides access to negotiated host capabilities.
    /// </summary>
    public ICapabilityProvider Capabilities { get; }

    /// <summary>
    /// Arbitrary host-provided metadata.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Properties { get; }
}
```

**Usage:**

```csharp
public class MyModule : VisoraModule
{
    public override async ValueTask InitializeAsync(
        ModuleContext context,
        CancellationToken ct)
    {
        // 1. Access descriptor
        var name = context.Descriptor.Name;
        var version = context.Descriptor.Version;

        // 2. Access capabilities (required)
        var console = context.Capabilities.GetRequired<IConsoleHost>();
        var logger = context.Capabilities.GetOptional<ILogger>();

        // 3. Access services (optional)
        if (context.Services != null)
        {
            var registry = context.Services.GetService<IComponentRegistry>();
            registry?.Register(this);
        }

        // 4. Access properties
        if (context.Properties.TryGetValue("debug", out var debugValue)
            && debugValue is bool debug && debug)
        {
            logger?.LogDebug("Debug mode enabled");
        }

        // 5. Use capabilities
        await console.WriteLineAsync($"Initializing {name} v{version}", ct);
    }
}
```

**Key Points:**
- **Immutable:** All properties are read-only
- **Non-Null Capabilities:** Always available
- **Optional Services:** May be null
- **Empty Properties:** Never null (empty dictionary if not provided)

---

### Example 2: ComponentContext

**File:** `/src/Visora.Contracts/Components/ComponentContext.cs`

```csharp
/// <summary>
/// Provides runtime services to components.
/// </summary>
public sealed class ComponentContext
{
    public ComponentContext(
        VisoraModule module,
        ICapabilityProvider capabilities)
    {
        Module = module ?? throw new ArgumentNullException(nameof(module));
        Capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
    }

    public VisoraModule Module { get; }

    public ICapabilityProvider Capabilities { get; }
}
```

**Usage:**

```csharp
public class MyComponent : VisoraComponent
{
    private ILogger? _logger;

    public override ComponentDescriptor Descriptor =>
        new("MyComponent", "Does useful things");

    public override ValueTask InitializeAsync(
        ComponentContext context,
        CancellationToken ct)
    {
        // 1. Access parent module
        var moduleName = context.Module.Descriptor.Name;

        // 2. Access capabilities
        _logger = context.Capabilities.GetOptional<ILogger>();
        var console = context.Capabilities.GetRequired<IConsoleHost>();

        _logger?.LogInformation("Component {Component} in module {Module}",
            Descriptor.Name, moduleName);

        return ValueTask.CompletedTask;
    }
}
```

**Key Points:**
- **Simpler Than ModuleContext:** Only Module and Capabilities
- **Module Reference:** Access parent module
- **Shared Capabilities:** Same capability provider as module

---

### Example 3: CommandContext

**File:** `/src/Visora.Contracts/Commands/CommandContext.cs`

```csharp
/// <summary>
/// Execution context for a command.
/// </summary>
public sealed class CommandContext
{
    public CommandContext(
        VisoraModule module,
        VisoraComponent? component,
        CommandSurface surface,
        ICapabilityProvider capabilities,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        Module = module ?? throw new ArgumentNullException(nameof(module));
        Component = component;  // ← Nullable (some commands are module-level)
        Surface = surface;
        Capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
        CancellationToken = cancellationToken;
        Parameters = parameters ?? EmptyParameters;
    }

    private static readonly IReadOnlyDictionary<string, object?> EmptyParameters =
        new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>());

    public VisoraModule Module { get; }

    public VisoraComponent? Component { get; }  // ← Nullable

    public CommandSurface Surface { get; }

    public ICapabilityProvider Capabilities { get; }

    public IReadOnlyDictionary<string, object?> Parameters { get; }

    public CancellationToken CancellationToken { get; }
}

/// <summary>
/// Enumerates command invocation surfaces.
/// </summary>
public enum CommandSurface
{
    Programmatic,
    TextShell,
    Ui,
    Automation,
    Remote
}
```

**Usage:**

```csharp
public class SaveFileCommand : VisoraCommand
{
    public override CommandDescriptor Descriptor =>
        new("save", "Save current file");

    public override async ValueTask<CommandResult> ExecuteAsync(
        CommandContext context,
        CancellationToken ct)
    {
        // 1. Access hierarchy
        var moduleName = context.Module.Descriptor.Name;
        var componentName = context.Component?.Descriptor.Name ?? "N/A";

        // 2. Access capabilities
        var logger = context.Capabilities.GetOptional<ILogger>();
        var fileSystem = context.Capabilities.GetRequired<IFileSystem>();

        // 3. Access parameters (type-safe)
        if (!context.Parameters.TryGetValue("path", out var pathObj)
            || pathObj is not string path)
        {
            return CommandResult.Error("Missing 'path' parameter");
        }

        // 4. Check surface (adapt behavior)
        if (context.Surface == CommandSurface.TextShell)
        {
            // In text shell, show progress
            await context.Capabilities.GetRequired<IConsoleHost>()
                .WriteLineAsync("Saving...", ct);
        }

        // 5. Use cancellation token
        await fileSystem.WriteFileAsync(path, "content", context.CancellationToken);

        logger?.LogInformation("Saved file: {Path}", path);

        return CommandResult.Success();
    }
}
```

**Key Points:**
- **Most Complete Context:** Includes everything needed for execution
- **Optional Component:** Some commands are module-level
- **Surface Awareness:** Adapt behavior based on invocation surface
- **Parameters Dictionary:** Type-safe access with TryGetValue pattern
- **Cancellation Token:** Embedded in context

---

### Example 4: Context Creation

**How contexts are created:**

```csharp
// ModuleContext created during module load
// File: /src/Visora.Core/Modules/ModuleHandle.cs:74
var context = new ModuleContext(
    descriptor,
    options.Services,
    options.Capabilities,
    options.Properties);

// ComponentContext created during component initialization
// File: /src/Visora.Core/Modules/ModuleHandle.cs:108
var componentContext = new ComponentContext(
    Module,
    _context.Capabilities);  // ← Shared from module context

// CommandContext created during command execution
// File: (Illustrative - would be in command executor)
var commandContext = new CommandContext(
    module,
    component,
    CommandSurface.Programmatic,
    capabilities,
    parameters,
    cancellationToken);
```

---

## File References

### Core Context Files

1. **ModuleContext.cs**
   - Path: `/src/Visora.Contracts/Modules/ModuleContext.cs`
   - Lines: 10-41
   - Purpose: Module lifecycle context
   - Properties: Descriptor, Services, Capabilities, Properties

2. **ComponentContext.cs**
   - Path: `/src/Visora.Contracts/Components/ComponentContext.cs`
   - Lines: 10-21
   - Purpose: Component lifecycle context
   - Properties: Module, Capabilities

3. **CommandContext.cs**
   - Path: `/src/Visora.Contracts/Commands/CommandContext.cs`
   - Lines: 14-45
   - Purpose: Command execution context
   - Properties: Module, Component, Surface, Capabilities, Parameters, CancellationToken

4. **ModuleHandle.cs** (context usage)
   - Path: `/src/Visora.Core/Modules/ModuleHandle.cs`
   - Lines: 74 (ModuleContext creation), 108 (ComponentContext creation)
   - Purpose: Shows how contexts are created and passed

---

## Context Composition

### What Each Context Contains

**ModuleContext:**
```
┌─────────────────────────────────────────────┐
│            ModuleContext                    │
│  ┌───────────────────────────────────────┐  │
│  │  Descriptor (required)                │  │
│  │    - Name, Version, Description       │  │
│  ├───────────────────────────────────────┤  │
│  │  Services (optional)                  │  │
│  │    - Host IServiceProvider            │  │
│  ├───────────────────────────────────────┤  │
│  │  Capabilities (required)              │  │
│  │    - ICapabilityProvider              │  │
│  ├───────────────────────────────────────┤  │
│  │  Properties (optional, never null)    │  │
│  │    - Host metadata dictionary         │  │
│  └───────────────────────────────────────┘  │
└─────────────────────────────────────────────┘
```

**ComponentContext:**
```
┌─────────────────────────────────────────────┐
│          ComponentContext                   │
│  ┌───────────────────────────────────────┐  │
│  │  Module (required)                    │  │
│  │    - Parent VisoraModule              │  │
│  ├───────────────────────────────────────┤  │
│  │  Capabilities (required)              │  │
│  │    - ICapabilityProvider (shared)     │  │
│  └───────────────────────────────────────┘  │
└─────────────────────────────────────────────┘
```

**CommandContext:**
```
┌─────────────────────────────────────────────┐
│           CommandContext                    │
│  ┌───────────────────────────────────────┐  │
│  │  Module (required)                    │  │
│  │    - Parent VisoraModule              │  │
│  ├───────────────────────────────────────┤  │
│  │  Component (optional)                 │  │
│  │    - Parent VisoraComponent or null   │  │
│  ├───────────────────────────────────────┤  │
│  │  Surface (required)                   │  │
│  │    - Where command was invoked        │  │
│  ├───────────────────────────────────────┤  │
│  │  Capabilities (required)              │  │
│  │    - ICapabilityProvider (shared)     │  │
│  ├───────────────────────────────────────┤  │
│  │  Parameters (optional, never null)    │  │
│  │    - Command arguments dictionary     │  │
│  ├───────────────────────────────────────┤  │
│  │  CancellationToken (always present)   │  │
│  │    - For async cancellation           │  │
│  └───────────────────────────────────────┘  │
└─────────────────────────────────────────────┘
```

### Composition Patterns

**1. Capabilities Flow Through Hierarchy**

```csharp
// Host creates capabilities once
var capabilities = CapabilityProviders.CreateBuilder()
    .Add<ILogger>(logger)
    .Add<IConsoleHost>(console)
    .Build();

// ModuleContext receives capabilities
var moduleContext = new ModuleContext(descriptor, services, capabilities, props);

// ComponentContext shares same capabilities
var componentContext = new ComponentContext(module, capabilities);  // ← Same instance

// CommandContext shares same capabilities
var commandContext = new CommandContext(module, component, surface, capabilities, params, ct);

// All three contexts have access to same capabilities!
```

**2. Hierarchical References**

```csharp
// Command can access full hierarchy
public override ValueTask<CommandResult> ExecuteAsync(CommandContext context, CancellationToken ct)
{
    // Access module
    var moduleName = context.Module.Descriptor.Name;

    // Access component (if present)
    var componentName = context.Component?.Descriptor.Name;

    // Access capabilities (shared)
    var logger = context.Capabilities.GetOptional<ILogger>();

    // Full context available in single parameter
}
```

---

## Propagation Patterns

### Pattern 1: Context Flows Downward

```
Host
  ↓ Creates ModuleContext
Module.InitializeAsync(ModuleContext)
  ↓ Extracts capabilities from ModuleContext
  ↓ Creates ComponentContext
Component.InitializeAsync(ComponentContext)
  ↓ Extracts capabilities from ComponentContext
  ↓ Creates CommandContext
Command.ExecuteAsync(CommandContext)
  ↓ Uses capabilities from CommandContext
```

### Pattern 2: Context Creation Points

**ModuleContext:** Created once per module load

```csharp
// File: ModuleHandle.cs:74
var context = new ModuleContext(
    descriptor,
    options.Services,
    options.Capabilities,
    options.Properties);

// Stored for module lifetime
_context = context;
```

**ComponentContext:** Created per component inspection

```csharp
// File: ModuleHandle.cs:108
var componentContext = new ComponentContext(
    Module,
    _context.Capabilities);  // ← From module context

await component.InitializeAsync(componentContext, ct);
```

**CommandContext:** Created per command execution

```csharp
// Illustrative - command executor creates this
var commandContext = new CommandContext(
    module,
    component,
    determineSurface(),
    capabilities,
    extractParameters(args),
    cancellationToken);

var result = await command.ExecuteAsync(commandContext, cancellationToken);
```

### Pattern 3: Context Sharing

**Capabilities are shared across all contexts:**

```csharp
// Single capability provider instance
var capabilities = CapabilityProviders.CreateBuilder()
    .Add<ILogger>(logger)
    .Build();

var moduleContext = new ModuleContext(..., capabilities, ...);
var componentContext = new ComponentContext(..., capabilities);
var commandContext = new CommandContext(..., capabilities, ...);

// All three contexts reference the SAME ICapabilityProvider instance
Assert.Same(moduleContext.Capabilities, componentContext.Capabilities);
Assert.Same(componentContext.Capabilities, commandContext.Capabilities);
```

---

## Immutability and Thread Safety

### Why Immutability?

1. **Thread Safety**
   - Multiple commands executing concurrently
   - Shared contexts must be safe
   - Immutability guarantees safety

2. **Predictability**
   - Context doesn't change during lifetime
   - No hidden mutations
   - Easier to reason about

3. **Caching**
   - Can safely cache contexts
   - No need to check for changes
   - Better performance

### Implementation

**All properties are read-only:**

```csharp
public sealed class ModuleContext
{
    // ✅ Read-only properties (no setters)
    public ModuleDescriptor Descriptor { get; }
    public IServiceProvider? Services { get; }
    public ICapabilityProvider Capabilities { get; }
    public IReadOnlyDictionary<string, object?> Properties { get; }

    // ✅ Set via constructor only
    public ModuleContext(
        ModuleDescriptor descriptor,
        IServiceProvider? services,
        ICapabilityProvider capabilities,
        IReadOnlyDictionary<string, object?>? properties = null)
    {
        Descriptor = descriptor;
        Services = services;
        Capabilities = capabilities;
        Properties = properties ?? EmptyProperties;
    }

    // ❌ No methods that modify state
    // ❌ No setters
    // ❌ No mutable collections
}
```

**Collections are immutable:**

```csharp
// ✅ IReadOnlyDictionary (not IDictionary)
public IReadOnlyDictionary<string, object?> Properties { get; }

// Caller can't do this:
// context.Properties.Add("key", "value");  // ← Compile error!

// ✅ Empty dictionary is shared (immutable)
private static readonly IReadOnlyDictionary<string, object?> EmptyProperties =
    new Dictionary<string, object?>(capacity: 0);
```

### Thread Safety Guarantees

```csharp
// ✅ SAFE: Multiple threads can read context concurrently
await Task.WhenAll(
    Task.Run(() => command1.ExecuteAsync(context, ct)),
    Task.Run(() => command2.ExecuteAsync(context, ct)),
    Task.Run(() => command3.ExecuteAsync(context, ct))
);

// All three commands share same context safely!
```

---

## Capabilities Access

### Type-Safe Capability Access

**Pattern:**

```csharp
// Required capability (throws if not available)
var console = context.Capabilities.GetRequired<IConsoleHost>();
await console.WriteLineAsync("Hello", ct);

// Optional capability (returns null if not available)
var logger = context.Capabilities.GetOptional<ILogger>();
logger?.LogInformation("Starting");

// Check availability
if (context.Capabilities.IsAvailable<IFileSystem>())
{
    var fs = context.Capabilities.GetRequired<IFileSystem>();
    // Use fs
}
```

**Implementation:**

```csharp
public interface ICapabilityProvider
{
    T GetRequired<T>() where T : class;
    T? GetOptional<T>() where T : class;
    bool IsAvailable<T>() where T : class;
}
```

### Capability-First Design

**Modules request capabilities, host provides:**

```csharp
public class MyModule : VisoraModule
{
    private ILogger? _logger;
    private IConsoleHost _console = null!;

    public override ValueTask InitializeAsync(
        ModuleContext context,
        CancellationToken ct)
    {
        // Request capabilities via context
        _logger = context.Capabilities.GetOptional<ILogger>();
        _console = context.Capabilities.GetRequired<IConsoleHost>();

        // Module can work with or without logger
        // Module requires console (fails if not available)

        return ValueTask.CompletedTask;
    }
}
```

---

## Testing Patterns

### Pattern 1: Mock Contexts

```csharp
[Fact]
public async Task Module_InitializesWithContext()
{
    // Arrange: Create test context
    var descriptor = new ModuleDescriptor("Test", "1.0", "Test module");

    var mockLogger = new Mock<ILogger>();
    var capabilities = CapabilityProviders.CreateBuilder()
        .Add<ILogger>(mockLogger.Object)
        .Build();

    var context = new ModuleContext(
        descriptor,
        services: null,
        capabilities,
        properties: null);

    // Act
    var module = new MyModule();
    await module.InitializeAsync(context, CancellationToken.None);

    // Assert
    mockLogger.Verify(l => l.LogInformation(It.IsAny<string>()), Times.Once);
}
```

### Pattern 2: Test Builders

```csharp
public class TestContextBuilder
{
    private ModuleDescriptor _descriptor = new("Test", "1.0", "Test");
    private readonly List<(Type, object)> _capabilities = new();
    private readonly Dictionary<string, object?> _properties = new();

    public TestContextBuilder WithDescriptor(ModuleDescriptor descriptor)
    {
        _descriptor = descriptor;
        return this;
    }

    public TestContextBuilder WithCapability<T>(T capability) where T : class
    {
        _capabilities.Add((typeof(T), capability));
        return this;
    }

    public TestContextBuilder WithProperty(string key, object? value)
    {
        _properties[key] = value;
        return this;
    }

    public ModuleContext Build()
    {
        var capBuilder = CapabilityProviders.CreateBuilder();
        foreach (var (type, instance) in _capabilities)
        {
            // Use reflection to call Add<T>
            var method = typeof(CapabilityProviderBuilder)
                .GetMethod("Add")
                .MakeGenericMethod(type);
            method.Invoke(capBuilder, new[] { instance });
        }

        return new ModuleContext(
            _descriptor,
            services: null,
            capBuilder.Build(),
            _properties);
    }
}

// Usage:
var context = new TestContextBuilder()
    .WithDescriptor(new ModuleDescriptor("Test", "1.0", "Test"))
    .WithCapability<ILogger>(mockLogger.Object)
    .WithCapability<IConsoleHost>(mockConsole.Object)
    .WithProperty("debug", true)
    .Build();
```

### Pattern 3: Minimal Contexts

```csharp
public static class TestContexts
{
    public static ModuleContext CreateMinimal()
    {
        return new ModuleContext(
            new ModuleDescriptor("Test", "1.0", "Test"),
            services: null,
            CapabilityProviders.Empty,
            properties: null);
    }

    public static ComponentContext CreateComponentContext(VisoraModule module)
    {
        return new ComponentContext(
            module,
            CapabilityProviders.Empty);
    }

    public static CommandContext CreateCommandContext()
    {
        var module = new TestModule();
        return new CommandContext(
            module,
            component: null,
            CommandSurface.Programmatic,
            CapabilityProviders.Empty,
            parameters: null,
            CancellationToken.None);
    }
}
```

---

## Best Practices

### 1. Use Context, Don't Abuse It

```csharp
// ✅ GOOD: Extract what you need early
public override ValueTask InitializeAsync(
    ModuleContext context,
    CancellationToken ct)
{
    // Extract capabilities at start
    _logger = context.Capabilities.GetOptional<ILogger>();
    _console = context.Capabilities.GetRequired<IConsoleHost>();

    // Don't store context itself
    // _context = context;  // ❌ Don't do this
}

// ❌ BAD: Storing context for later use
private ModuleContext _context;  // ❌ Don't store context

public override ValueTask InitializeAsync(ModuleContext context, CancellationToken ct)
{
    _context = context;  // ❌ Anti-pattern
}

public void SomeMethod()
{
    var logger = _context.Capabilities.GetOptional<ILogger>();  // ❌ Late binding
}
```

**Why?**
- Context is temporary (for initialization)
- Store capabilities, not context
- Clear what you depend on

### 2. Don't Pass Contexts Down

```csharp
// ✅ GOOD: Extract and pass specific capabilities
public override ValueTask InitializeAsync(
    ModuleContext context,
    CancellationToken ct)
{
    var logger = context.Capabilities.GetOptional<ILogger>();
    var console = context.Capabilities.GetRequired<IConsoleHost>();

    // Pass specific capabilities
    _helper = new MyHelper(logger, console);
}

// ❌ BAD: Passing entire context
public override ValueTask InitializeAsync(
    ModuleContext context,
    CancellationToken ct)
{
    // Don't pass context to helper classes
    _helper = new MyHelper(context);  // ❌ Anti-pattern
}
```

**Why?**
- Helpers shouldn't know about contexts
- Hidden dependencies
- Testing becomes harder

### 3. Parameters Should Be Typed

```csharp
// ✅ GOOD: Type-safe parameter extraction
public override ValueTask<CommandResult> ExecuteAsync(
    CommandContext context,
    CancellationToken ct)
{
    // Extract with type checking
    if (!context.Parameters.TryGetValue("path", out var pathObj)
        || pathObj is not string path)
    {
        return ValueTask.FromResult(
            CommandResult.Error("Missing or invalid 'path' parameter"));
    }

    // path is now string (type-safe)
    ProcessPath(path);
}

// ❌ BAD: Unsafe casting
public override ValueTask<CommandResult> ExecuteAsync(
    CommandContext context,
    CancellationToken ct)
{
    // Unsafe cast (can throw at runtime)
    var path = (string)context.Parameters["path"];  // ❌ Can throw KeyNotFoundException or InvalidCastException
}
```

### 4. Use Empty Collections, Not Null

```csharp
// ✅ GOOD: Empty collection instead of null
private static readonly IReadOnlyDictionary<string, object?> EmptyProperties =
    new Dictionary<string, object?>(capacity: 0);

public ModuleContext(...)
{
    Properties = properties ?? EmptyProperties;  // ← Never null
}

// Caller never needs null check
foreach (var prop in context.Properties)  // ✅ Works even if no properties
{
    // ...
}

// ❌ BAD: Nullable collection
public IReadOnlyDictionary<string, object?>? Properties { get; }  // ❌ Caller must null-check

// Caller must check
if (context.Properties != null)
{
    foreach (var prop in context.Properties) { }
}
```

### 5. CancellationToken in Context

```csharp
// ✅ GOOD: CommandContext includes CancellationToken
public sealed class CommandContext
{
    public CancellationToken CancellationToken { get; }
}

public override async ValueTask<CommandResult> ExecuteAsync(
    CommandContext context,
    CancellationToken ct)  // ← Also passed as parameter for consistency
{
    // Can use either (should be same):
    await SomeOperationAsync(context.CancellationToken);
    await SomeOperationAsync(ct);
}

// Why both?
// - Parameter is .NET convention
// - Context property is convenient
// - Usually the same token
```

---

## Advanced Topics

### Topic 1: Context Extension Methods

**Pattern:** Add convenience methods via extension methods

```csharp
public static class ModuleContextExtensions
{
    public static bool IsDebugMode(this ModuleContext context)
    {
        return context.Properties.TryGetValue("debug", out var debug)
            && debug is bool debugBool
            && debugBool;
    }

    public static ILogger GetLoggerOrDefault(this ModuleContext context)
    {
        return context.Capabilities.GetOptional<ILogger>()
            ?? NullLogger.Instance;
    }

    public static T GetRequiredProperty<T>(
        this ModuleContext context,
        string key)
    {
        if (!context.Properties.TryGetValue(key, out var value))
            throw new InvalidOperationException($"Missing property: {key}");

        if (value is not T typedValue)
            throw new InvalidOperationException(
                $"Property '{key}' is not of type {typeof(T).Name}");

        return typedValue;
    }
}

// Usage:
if (context.IsDebugMode())
{
    var logger = context.GetLoggerOrDefault();
    logger.LogDebug("Debug info");
}
```

### Topic 2: Context Validation

```csharp
public static class ContextValidator
{
    public static void ValidateModuleContext(ModuleContext context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        if (context.Descriptor == null)
            throw new InvalidOperationException("Descriptor is null");

        if (string.IsNullOrWhiteSpace(context.Descriptor.Name))
            throw new InvalidOperationException("Descriptor.Name is empty");

        if (context.Capabilities == null)
            throw new InvalidOperationException("Capabilities is null");
    }

    public static void RequireCapability<T>(
        ModuleContext context,
        string errorMessage = null) where T : class
    {
        if (!context.Capabilities.IsAvailable<T>())
        {
            throw new InvalidOperationException(
                errorMessage ?? $"Required capability {typeof(T).Name} not available");
        }
    }
}

// Usage:
public override ValueTask InitializeAsync(
    ModuleContext context,
    CancellationToken ct)
{
    ContextValidator.ValidateModuleContext(context);
    ContextValidator.RequireCapability<IConsoleHost>(context);

    // Continue with initialization
}
```

### Topic 3: Scoped Contexts

**Pattern:** Create derived contexts for specific scopes

```csharp
public class ScopedCommandContext
{
    private readonly CommandContext _baseContext;
    private readonly IDisposable _scope;

    public ScopedCommandContext(
        CommandContext baseContext,
        IServiceScope scope)
    {
        _baseContext = baseContext;
        _scope = (IDisposable)scope;
    }

    // Delegate to base context
    public VisoraModule Module => _baseContext.Module;
    public ICapabilityProvider Capabilities => _baseContext.Capabilities;
    public IReadOnlyDictionary<string, object?> Parameters => _baseContext.Parameters;
    public CancellationToken CancellationToken => _baseContext.CancellationToken;

    // Add scoped services
    public T GetScopedService<T>() where T : class
    {
        return ((IServiceScope)_scope).ServiceProvider.GetRequiredService<T>();
    }

    public void Dispose()
    {
        _scope.Dispose();
    }
}

// Usage:
using var scopedContext = new ScopedCommandContext(context, serviceScope);
var scopedService = scopedContext.GetScopedService<IScopedService>();
```

### Topic 4: Context Builders for Complex Scenarios

```csharp
public class ModuleContextBuilder
{
    private ModuleDescriptor? _descriptor;
    private IServiceProvider? _services;
    private readonly CapabilityProviderBuilder _capabilitiesBuilder =
        CapabilityProviders.CreateBuilder();
    private readonly Dictionary<string, object?> _properties = new();

    public ModuleContextBuilder WithDescriptor(
        string name,
        string version,
        string description)
    {
        _descriptor = new ModuleDescriptor(name, version, description);
        return this;
    }

    public ModuleContextBuilder WithServices(IServiceProvider services)
    {
        _services = services;
        return this;
    }

    public ModuleContextBuilder AddCapability<T>(T capability) where T : class
    {
        _capabilitiesBuilder.Add(capability);
        return this;
    }

    public ModuleContextBuilder AddProperty(string key, object? value)
    {
        _properties[key] = value;
        return this;
    }

    public ModuleContext Build()
    {
        if (_descriptor == null)
            throw new InvalidOperationException("Descriptor not set");

        return new ModuleContext(
            _descriptor,
            _services,
            _capabilitiesBuilder.Build(),
            _properties);
    }
}

// Usage:
var context = new ModuleContextBuilder()
    .WithDescriptor("MyModule", "1.0.0", "Description")
    .AddCapability<ILogger>(logger)
    .AddCapability<IConsoleHost>(console)
    .AddProperty("debug", true)
    .Build();
```

---

## Summary

### Key Takeaways

1. **Single Parameter Pattern**
   - One context object instead of many parameters
   - Non-breaking evolution
   - Clear structure

2. **Three Context Types**
   - ModuleContext: Module lifecycle
   - ComponentContext: Component lifecycle
   - CommandContext: Command execution

3. **Immutability**
   - All contexts are immutable
   - Thread-safe by design
   - Predictable behavior

4. **Capabilities Flow**
   - Same ICapabilityProvider instance shared
   - Type-safe access pattern
   - Required vs optional explicit

5. **Testing-Friendly**
   - Easy to mock
   - Builder pattern for complex scenarios
   - Minimal contexts for simple tests

---

### When to Use This Pattern

✅ **Use Context Objects When:**
- Building plugin/module systems
- Methods need access to multiple services
- API evolution is important (non-breaking)
- Testing with mocks is required
- Thread safety is a concern

❌ **Consider Alternatives When:**
- Methods need only 1-2 parameters (not worth it)
- Parameters never change (no evolution needed)
- No shared state or services
- Simple, static configuration only

---

### Further Reading

- **Related Pattern:** Capability Negotiation - How capabilities work
- **Related Pattern:** Parameter Objects - General pattern
- **Alternative:** Dependency Injection - Constructor injection (doesn't work for modules)
- **Implementation:** `/src/Visora.Contracts/Modules/ModuleContext.cs`
- **Implementation:** `/src/Visora.Contracts/Components/ComponentContext.cs`
- **Implementation:** `/src/Visora.Contracts/Commands/CommandContext.cs`
