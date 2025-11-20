# Template Method Pattern - VISORA Deep Dive

**Last Updated:** 2025-11-10
**VISORA Version:** .NET 9.0
**Pattern Tier:** Tier 4 (Structural)
**Related Patterns:** Strategy Pattern, Factory Pattern, Lifecycle Management

---

## Table of Contents

1. [Pattern Overview](#pattern-overview)
2. [Why VISORA Uses Template Method](#why-visora-uses-template-method)
3. [VISORA Implementation](#visora-implementation)
4. [Code Examples](#code-examples)
5. [File References](#file-references)
6. [Virtual Hook Points](#virtual-hook-points)
7. [Default Implementations and Overrides](#default-implementations-and-overrides)
8. [Lifecycle Template Pattern](#lifecycle-template-pattern)
9. [Testing Template Methods](#testing-template-methods)
10. [Tradeoffs](#tradeoffs)
11. [Alternatives Considered](#alternatives-considered)
12. [Best Practices](#best-practices)
13. [Advanced Topics](#advanced-topics)

---

## Pattern Overview

### What is Template Method Pattern?

**Definition:** The Template Method pattern defines the skeleton of an algorithm in a base class, allowing subclasses to override specific steps without changing the algorithm's structure.

**Key Characteristics:**
- **Abstract Base Class:** Defines the template and abstract/virtual methods
- **Lifecycle Hooks:** Virtual methods that subclasses can override
- **Default Behavior:** Base class provides sensible defaults
- **Controlled Extension:** Only specific points can be customized
- **Inversion of Control:** Framework calls into your code

### Classic Template Method Structure

```csharp
public abstract class AbstractClass
{
    // Template method (defines algorithm)
    public void TemplateMethod()
    {
        Step1();
        Step2(); // Hook - can be overridden
        Step3();
    }

    // Concrete step
    private void Step1() { /* ... */ }

    // Hook - subclasses can override
    protected virtual void Step2() { /* default */ }

    // Concrete step
    private void Step3() { /* ... */ }
}

public class ConcreteClass : AbstractClass
{
    // Override hook to customize behavior
    protected override void Step2()
    {
        // Custom implementation
    }
}
```

### VISORA's Application

VISORA uses Template Method for:
1. **Module lifecycle:** VisoraModule defines hooks for initialization, shutdown
2. **Component lifecycle:** VisoraComponent defines hooks for activation, deactivation
3. **Component discovery:** VisoraModule provides default discovery, can be overridden
4. **Command creation:** VisoraComponent defines how to create commands

---

## Why VISORA Uses Template Method

### Design Goals

1. **Standardized Lifecycle**
   - All modules follow the same lifecycle pattern
   - Framework controls when lifecycle methods are called
   - Modules customize behavior at specific points

2. **Sensible Defaults**
   - Base classes provide default implementations
   - Modules only override what they need
   - Reduces boilerplate code

3. **Extensibility Points**
   - Clear, documented places to extend behavior
   - Controlled extension (only override hooks)
   - Framework maintains control of algorithm

4. **Inversion of Control**
   - Framework calls module code (not vice versa)
   - Modules are "plugins" that respond to framework events
   - Enables dynamic loading and lifecycle management

5. **Consistency**
   - All modules follow the same patterns
   - Easy to understand new modules
   - Predictable behavior

### Key Decision Points

**Q: Why not have modules implement interfaces instead of inherit base classes?**
**A:** Base classes provide default implementations, reducing boilerplate. Interfaces would force every module to implement every method.

**Q: Why virtual methods instead of events or callbacks?**
**A:** Virtual methods are simpler, more performant, and provide type safety at compile time.

**Q: Why async lifecycle methods?**
**A:** Modules may need to do I/O (load resources, connect to services), which should be async.

---

## VISORA Implementation

### VisoraModule: Template for Module Lifecycle

**File:** `/src/Visora.Contracts/Modules/VisoraModule.cs`

```
┌────────────────────────────────────────────────────────┐
│              VisoraModule (Abstract)                   │
│  ┌──────────────────────────────────────────────────┐ │
│  │  Template: Module Lifecycle                      │ │
│  │  1. Create instance (constructor)                │ │
│  │  2. Read Descriptor (abstract property)          │ │
│  │  3. InitializeAsync() → HOOK                     │ │
│  │  4. DiscoverComponents() → HOOK                  │ │
│  │  5. (Active use)                                 │ │
│  │  6. ShutdownAsync() → HOOK                       │ │
│  │  7. DisposeAsync() → HOOK                        │ │
│  └──────────────────────────────────────────────────┘ │
│                                                        │
│  Abstract/Virtual Members:                             │
│  • Descriptor (abstract property)                      │
│  • InitializeAsync() (virtual, default no-op)         │
│  • ShutdownAsync() (virtual, default no-op)           │
│  • DiscoverComponents() (virtual, default reflection) │
│  • DisposeAsync() (virtual, default no-op)            │
└────────────────────────────────────────────────────────┘
                         │
                         │ inherits
                         ↓
┌────────────────────────────────────────────────────────┐
│          ConcreteModule (User-Defined)                 │
│  • Implements Descriptor property                      │
│  • Overrides InitializeAsync() (if needed)             │
│  • Overrides DiscoverComponents() (if needed)          │
│  • Overrides ShutdownAsync() (if needed)               │
└────────────────────────────────────────────────────────┘
```

### VisoraComponent: Template for Component Lifecycle

**File:** `/src/Visora.Contracts/Components/VisoraComponent.cs`

```
┌────────────────────────────────────────────────────────┐
│            VisoraComponent (Abstract)                  │
│  ┌──────────────────────────────────────────────────┐ │
│  │  Template: Component Lifecycle                   │ │
│  │  1. Create instance                              │ │
│  │  2. Read Descriptor (abstract property)          │ │
│  │  3. InitializeAsync() → HOOK                     │ │
│  │  4. ActivateAsync() → HOOK                       │ │
│  │  5. CreateCommands() → HOOK                      │ │
│  │  6. (Active use)                                 │ │
│  │  7. DeactivateAsync() → HOOK                     │ │
│  │  8. DisposeAsync() → HOOK                        │ │
│  └──────────────────────────────────────────────────┘ │
│                                                        │
│  Abstract/Virtual Members:                             │
│  • Descriptor (abstract property)                      │
│  • InitializeAsync() (virtual, default no-op)         │
│  • ActivateAsync() (virtual, default no-op)           │
│  • DeactivateAsync() (virtual, default no-op)         │
│  • CreateCommands() (virtual, default empty array)    │
│  • DisposeAsync() (virtual, default no-op)            │
└────────────────────────────────────────────────────────┘
                         │
                         │ inherits
                         ↓
┌────────────────────────────────────────────────────────┐
│       ConcreteComponent (User-Defined)                 │
│  • Implements Descriptor property                      │
│  • Overrides CreateCommands() to register commands     │
│  • Overrides lifecycle hooks (if needed)               │
└────────────────────────────────────────────────────────┘
```

---

## Code Examples

### VisoraModule Template Definition

**File:** `/src/Visora.Contracts/Modules/VisoraModule.cs`

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Visora.Contracts.Components;

namespace Visora.Contracts.Modules;

/// <summary>
/// Base type for Visora modules.
/// Uses Template Method pattern to define module lifecycle.
/// </summary>
public abstract class VisoraModule : IAsyncDisposable
{
    /// <summary>
    /// Describes the module to hosts.
    /// ABSTRACT - must be implemented by concrete modules.
    /// </summary>
    public abstract ModuleDescriptor Descriptor { get; }

    /// <summary>
    /// Called when the module is being initialized.
    /// VIRTUAL - default implementation does nothing.
    /// Override to set up module state, register services, etc.
    /// </summary>
    /// <param name="context">Module execution context with capabilities and services.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public virtual ValueTask InitializeAsync(
        ModuleContext context,
        CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask; // Default: no-op

    /// <summary>
    /// Called before the module is unloaded.
    /// VIRTUAL - default implementation does nothing.
    /// Override to clean up resources, disconnect from services, etc.
    /// </summary>
    /// <param name="context">Module execution context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public virtual ValueTask ShutdownAsync(
        ModuleContext context,
        CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask; // Default: no-op

    /// <summary>
    /// Returns component types exposed by this module.
    /// VIRTUAL - default implementation uses reflection to find all VisoraComponent types.
    /// Override to customize component discovery (filtering, ordering, etc.).
    /// </summary>
    /// <param name="context">Discovery context with assembly information.</param>
    /// <returns>Enumerable of component types.</returns>
    public virtual IEnumerable<Type> DiscoverComponents(
        ModuleDiscoveryContext context)
        => context.EnumerateComponentCandidates()
            .Where(t => typeof(VisoraComponent).IsAssignableFrom(t));
    // Default: reflection-based discovery

    /// <summary>
    /// Cleanup resources.
    /// VIRTUAL - default implementation does nothing.
    /// Override to dispose of unmanaged resources.
    /// </summary>
    public virtual ValueTask DisposeAsync() => ValueTask.CompletedTask;
    // Default: no-op
}
```

### Concrete Module Example: Minimal Override

**File:** `/src/Visora.Shell.Commands.Core/ShellCommandsModule.cs`

```csharp
using System;
using System.Collections.Generic;
using Visora.Core;
using Visora.Contracts.Modules;

namespace Visora.Shell.Commands.Core;

/// <summary>
/// Concrete module that overrides only Descriptor (minimal override).
/// Uses default implementations for all lifecycle methods.
/// </summary>
public sealed class ShellCommandsModule : Module
{
    // Static descriptor (efficient)
    private static readonly ModuleDescriptor ModuleInfo = ModuleDescriptor.Create(
        id: "visora.shell.commands.core",
        name: "Visora Shell Commands",
        version: new Version(0, 1, 0),
        description: "Baseline commands for diagnostics and exploration.",
        runtimeHints: ModuleRuntimeHints.Create(
            executableRelativePath: null,
            exposeExecutableGlobally: false,
            aliases: new[] { "vscc" }));

    // REQUIRED: Implement abstract Descriptor property
    public override ModuleDescriptor Descriptor => ModuleInfo;

    // OPTIONAL: Override DiscoverComponents (this example uses default)
    public override IEnumerable<Type> DiscoverComponents(ModuleDiscoveryContext context)
        => base.DiscoverComponents(context);
    // Note: Could omit this override entirely to use default behavior

    // Did NOT override:
    // - InitializeAsync (uses default no-op)
    // - ShutdownAsync (uses default no-op)
    // - DisposeAsync (uses default no-op)
}
```

### Concrete Module Example: Full Lifecycle Override

```csharp
/// <summary>
/// Concrete module that overrides all lifecycle methods.
/// </summary>
public sealed class DatabaseModule : Module
{
    private IDbConnection? _connection;

    private static readonly ModuleDescriptor ModuleInfo = ModuleDescriptor.Create(
        id: "example.database",
        name: "Database Module",
        version: new Version(1, 0, 0),
        description: "Provides database connectivity.");

    public override ModuleDescriptor Descriptor => ModuleInfo;

    // Override InitializeAsync to set up state
    public override async ValueTask InitializeAsync(
        ModuleContext context,
        CancellationToken cancellationToken = default)
    {
        // Get connection string from capabilities
        var config = context.Capabilities.GetOptional<IConfiguration>();
        var connectionString = config?["Database:ConnectionString"];

        if (connectionString != null)
        {
            // Open database connection
            _connection = new SqlConnection(connectionString);
            await _connection.OpenAsync(cancellationToken);
        }
    }

    // Override DiscoverComponents to filter by name
    public override IEnumerable<Type> DiscoverComponents(
        ModuleDiscoveryContext context)
    {
        // Only discover components with "Database" in the name
        return base.DiscoverComponents(context)
            .Where(t => t.Name.Contains("Database"));
    }

    // Override ShutdownAsync to clean up
    public override async ValueTask ShutdownAsync(
        ModuleContext context,
        CancellationToken cancellationToken = default)
    {
        if (_connection != null)
        {
            await _connection.CloseAsync();
        }
    }

    // Override DisposeAsync to dispose resources
    public override async ValueTask DisposeAsync()
    {
        if (_connection != null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }
}
```

### VisoraComponent Template Definition

**File:** `/src/Visora.Contracts/Components/VisoraComponent.cs`

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Visora.Contracts.Commands;

namespace Visora.Contracts.Components;

/// <summary>
/// Base class for module components.
/// Uses Template Method pattern to define component lifecycle.
/// </summary>
public abstract class VisoraComponent : IAsyncDisposable
{
    /// <summary>
    /// Descriptor describing the component.
    /// ABSTRACT - must be implemented by concrete components.
    /// </summary>
    public abstract ComponentDescriptor Descriptor { get; }

    /// <summary>
    /// Called once when the component instance is created.
    /// VIRTUAL - default implementation does nothing.
    /// Override to initialize component state.
    /// </summary>
    public virtual ValueTask InitializeAsync(
        ComponentContext context,
        CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    /// <summary>
    /// Called when the component is activated (e.g., loaded into a host UI shell).
    /// VIRTUAL - default implementation does nothing.
    /// Override to subscribe to events, start timers, etc.
    /// </summary>
    public virtual ValueTask ActivateAsync(
        ComponentContext context,
        CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    /// <summary>
    /// Called when the component is deactivated.
    /// VIRTUAL - default implementation does nothing.
    /// Override to unsubscribe from events, stop timers, etc.
    /// </summary>
    public virtual ValueTask DeactivateAsync(
        ComponentContext context,
        CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    /// <summary>
    /// Optional command registration for this component.
    /// VIRTUAL - default implementation returns empty array.
    /// Override to register commands.
    /// </summary>
    public virtual IEnumerable<VisoraCommand> CreateCommands(ComponentContext context)
        => Array.Empty<VisoraCommand>();

    /// <summary>
    /// Cleanup resources.
    /// VIRTUAL - default implementation does nothing.
    /// </summary>
    public virtual ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
```

### Concrete Component Example

**File:** `/src/Visora.Shell.Commands.Core/Components/CoreUtilitiesComponent.cs`

```csharp
using System.Collections.Generic;
using Visora.Core;
using Visora.Contracts.Components;
using Visora.Contracts.Commands;
using Visora.Shell.Commands.Core.Commands;

namespace Visora.Shell.Commands.Core.Components;

/// <summary>
/// Concrete component that overrides Descriptor and CreateCommands.
/// </summary>
public sealed class CoreUtilitiesComponent : Component
{
    private static readonly ComponentDescriptor Info =
        ComponentDescriptor.Create(
            id: "visora.shell.commands.core.utilities",
            name: "Core Utilities",
            description: "Diagnostics and helper commands for Visora shell experiments.",
            tags: new[] { "core", "shell", "diagnostics" },
            kind: ComponentKind.Console);

    // REQUIRED: Implement Descriptor
    public override ComponentDescriptor Descriptor => Info;

    // REQUIRED: Override CreateCommands to register commands
    public override IEnumerable<VisoraCommand> CreateCommands(ComponentContext context)
    {
        yield return new PingCommand();
        yield return new EnvironmentInfoCommand();
        yield return new ModuleProbeCommand();
    }

    // Did NOT override:
    // - InitializeAsync (uses default)
    // - ActivateAsync (uses default)
    // - DeactivateAsync (uses default)
    // - DisposeAsync (uses default)
}
```

---

## File References

### Template Definitions (Contracts)

| File | Lines | Purpose |
|------|-------|---------|
| `/src/Visora.Contracts/Modules/VisoraModule.cs` | 41 | Module lifecycle template |
| `/src/Visora.Contracts/Components/VisoraComponent.cs` | 45 | Component lifecycle template |
| `/src/Visora.Contracts/Commands/VisoraCommand.cs` | ~30 | Command execution template |

### Concrete Implementations (Examples)

| File | Lines | Purpose |
|------|-------|---------|
| `/src/Visora.Shell.Commands.Core/ShellCommandsModule.cs` | 26 | Minimal module override |
| `/src/Visora.Shell.Commands.Core/Components/CoreUtilitiesComponent.cs` | ~30 | Component with command registration |
| `/src/Visora.Core/Module.cs` | ~30 | Intermediate base class |
| `/src/Visora.Core/Component.cs` | ~30 | Intermediate base class |

### Supporting Types

| File | Lines | Purpose |
|------|-------|---------|
| `/src/Visora.Contracts/Modules/ModuleContext.cs` | ~30 | Context passed to lifecycle hooks |
| `/src/Visora.Contracts/Components/ComponentContext.cs` | ~20 | Context passed to component hooks |
| `/src/Visora.Contracts/Modules/ModuleDiscoveryContext.cs` | ~40 | Context for component discovery |

---

## Virtual Hook Points

### Module Lifecycle Hooks

| Hook | Type | Default | When Called | Purpose |
|------|------|---------|-------------|---------|
| `Descriptor` | Abstract Property | N/A | On access | Provide module metadata |
| `InitializeAsync()` | Virtual Method | No-op | After loading | Set up state, register services |
| `DiscoverComponents()` | Virtual Method | Reflection | During inspection | Return component types |
| `ShutdownAsync()` | Virtual Method | No-op | Before unload | Clean up, disconnect |
| `DisposeAsync()` | Virtual Method | No-op | On disposal | Release resources |

### Component Lifecycle Hooks

| Hook | Type | Default | When Called | Purpose |
|------|------|---------|-------------|---------|
| `Descriptor` | Abstract Property | N/A | On access | Provide component metadata |
| `InitializeAsync()` | Virtual Method | No-op | On creation | Initialize state |
| `ActivateAsync()` | Virtual Method | No-op | When activated | Subscribe to events, start services |
| `CreateCommands()` | Virtual Method | Empty | During inspection | Register commands |
| `DeactivateAsync()` | Virtual Method | No-op | When deactivated | Unsubscribe, stop services |
| `DisposeAsync()` | Virtual Method | No-op | On disposal | Release resources |

### Hook Execution Order

**Module Lifecycle:**
```
1. Constructor
2. Access Descriptor
3. InitializeAsync() ← HOOK
4. DiscoverComponents() ← HOOK
5. (Active use)
6. ShutdownAsync() ← HOOK
7. DisposeAsync() ← HOOK
```

**Component Lifecycle:**
```
1. Constructor
2. Access Descriptor
3. InitializeAsync() ← HOOK
4. ActivateAsync() ← HOOK
5. CreateCommands() ← HOOK
6. (Active use)
7. DeactivateAsync() ← HOOK
8. DisposeAsync() ← HOOK
```

---

## Default Implementations and Overrides

### Strategy 1: Minimal Override (Common Case)

**When to use:** Module/component doesn't need custom initialization or cleanup.

```csharp
public sealed class SimpleModule : Module
{
    private static readonly ModuleDescriptor Info = ModuleDescriptor.Create(
        id: "simple.module",
        name: "Simple Module",
        version: new Version(1, 0, 0));

    // Only override what's required
    public override ModuleDescriptor Descriptor => Info;

    // Everything else uses defaults
}
```

**Pros:**
- Minimal code
- Uses sensible defaults
- Easy to understand

**Cons:**
- Limited customization

### Strategy 2: Selective Override

**When to use:** Module needs some customization but not full lifecycle control.

```csharp
public sealed class LoggingModule : Module
{
    private ILogger? _logger;

    public override ModuleDescriptor Descriptor => /* ... */;

    // Override only InitializeAsync to get logger
    public override ValueTask InitializeAsync(
        ModuleContext context,
        CancellationToken cancellationToken = default)
    {
        _logger = context.Capabilities.GetOptional<ILogger>();
        _logger?.LogInformation($"Module {Descriptor.Name} initialized.");
        return ValueTask.CompletedTask;
    }

    // Don't override ShutdownAsync, DiscoverComponents, DisposeAsync
}
```

**Pros:**
- Override only what you need
- Rest uses defaults
- Clear intent

**Cons:**
- Must remember to call base if needed (not applicable here, base is no-op)

### Strategy 3: Full Override

**When to use:** Module needs complete control over lifecycle.

```csharp
public sealed class ComplexModule : Module
{
    private IService? _service;
    private Timer? _timer;

    public override ModuleDescriptor Descriptor => /* ... */;

    public override async ValueTask InitializeAsync(
        ModuleContext context,
        CancellationToken cancellationToken = default)
    {
        _service = context.Capabilities.GetRequired<IService>();
        await _service.ConnectAsync(cancellationToken);

        _timer = new Timer(OnTimerTick, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
    }

    public override IEnumerable<Type> DiscoverComponents(
        ModuleDiscoveryContext context)
    {
        // Custom discovery logic
        return context.EnumerateComponentCandidates()
            .Where(t => t.Namespace?.StartsWith("MyCompany") == true);
    }

    public override async ValueTask ShutdownAsync(
        ModuleContext context,
        CancellationToken cancellationToken = default)
    {
        _timer?.Dispose();
        _timer = null;

        if (_service != null)
        {
            await _service.DisconnectAsync(cancellationToken);
        }
    }

    public override ValueTask DisposeAsync()
    {
        _timer?.Dispose();
        _service?.Dispose();
        return ValueTask.CompletedTask;
    }

    private void OnTimerTick(object? state)
    {
        // Periodic work
    }
}
```

**Pros:**
- Complete control
- Clear lifecycle management

**Cons:**
- More code
- Must handle all cleanup

---

## Lifecycle Template Pattern

### Framework Control Flow

**Who calls whom:**

```
┌─────────────────────────────────────────────────────┐
│                  Framework (Core)                   │
│  ┌───────────────────────────────────────────────┐ │
│  │  ModuleHandle.LoadAsync()                     │ │
│  │    1. Load assembly                           │ │
│  │    2. Create module instance (constructor)    │ │
│  │    3. Read Descriptor                         │ │
│  └───────────────────────────────────────────────┘ │
│                        │                            │
│                        ↓                            │
│  ┌───────────────────────────────────────────────┐ │
│  │  ModuleHandle.EnsureInitializedAsync()        │ │
│  │    → Calls Module.InitializeAsync() ──────────┼─┼─→ [Your Code]
│  └───────────────────────────────────────────────┘ │
│                        │                            │
│                        ↓                            │
│  ┌───────────────────────────────────────────────┐ │
│  │  ModuleHandle.InspectAsync()                  │ │
│  │    → Calls Module.DiscoverComponents() ───────┼─┼─→ [Your Code]
│  │    → Creates component instances              │ │
│  │    → Calls Component.CreateCommands() ────────┼─┼─→ [Your Code]
│  └───────────────────────────────────────────────┘ │
│                        │                            │
│                        ↓                            │
│  ┌───────────────────────────────────────────────┐ │
│  │  ModuleHandle.ShutdownAsync()                 │ │
│  │    → Calls Module.ShutdownAsync() ────────────┼─┼─→ [Your Code]
│  └───────────────────────────────────────────────┘ │
│                        │                            │
│                        ↓                            │
│  ┌───────────────────────────────────────────────┐ │
│  │  ModuleHandle.DisposeAsync()                  │ │
│  │    → Calls Module.DisposeAsync() ─────────────┼─┼─→ [Your Code]
│  └───────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────┘
```

**Key Insight:** Framework (ModuleHandle) controls the lifecycle. Your module responds to lifecycle events.

### Template Method in Action

**Framework Code (ModuleHandle):**

```csharp
// File: /src/Visora.Core/Modules/ModuleHandle.cs (simplified)
public sealed class ModuleHandle : IAsyncDisposable
{
    private readonly VisoraModule _module;
    private readonly ModuleContext _context;
    private bool _initialized;

    // Template: Ensure initialization before use
    public async Task EnsureInitializedAsync(CancellationToken ct = default)
    {
        if (_initialized)
            return; // Idempotent

        // Call user's hook
        await _module.InitializeAsync(_context, ct);

        _initialized = true;
    }

    // Template: Inspection workflow
    public async Task<ModuleInspection> InspectAsync(CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);

        // Call user's hook to discover components
        var componentTypes = _module.DiscoverComponents(discoveryContext).ToArray();

        var inspections = new List<ComponentInspection>();
        foreach (var componentType in componentTypes)
        {
            // Create component instance
            var component = (VisoraComponent)Activator.CreateInstance(componentType);

            // Call user's hooks
            await component.InitializeAsync(componentContext, ct);
            var commands = component.CreateCommands(componentContext).ToArray();

            inspections.Add(new ComponentInspection(componentType, component.Descriptor, commands));

            // Cleanup
            await component.DisposeAsync();
        }

        return new ModuleInspection(AssemblyPath, _module.Descriptor, inspections);
    }

    // Template: Shutdown workflow
    public async Task ShutdownAsync(CancellationToken ct = default)
    {
        if (!_initialized)
            return;

        // Call user's hook
        await _module.ShutdownAsync(_context, ct);

        _initialized = false;
    }

    // Template: Disposal workflow
    public async ValueTask DisposeAsync()
    {
        try
        {
            await ShutdownAsync();
            await _module.DisposeAsync();
        }
        finally
        {
            // Framework cleanup (plugin loader, etc.)
        }
    }
}
```

---

## Testing Template Methods

### Unit Test: Verify Hook is Called

```csharp
[TestClass]
public class VisoraModuleTemplateTests
{
    [TestMethod]
    public async Task InitializeAsync_IsCalled()
    {
        // Arrange
        var module = new TestModule();
        var context = CreateTestContext();

        // Act
        await module.InitializeAsync(context, CancellationToken.None);

        // Assert
        Assert.IsTrue(module.InitializeCalled, "InitializeAsync should have been called");
    }

    private class TestModule : VisoraModule
    {
        public bool InitializeCalled { get; private set; }

        public override ModuleDescriptor Descriptor
            => ModuleDescriptor.Create("test", "Test", new Version(1, 0, 0));

        public override ValueTask InitializeAsync(
            ModuleContext context,
            CancellationToken cancellationToken = default)
        {
            InitializeCalled = true;
            return ValueTask.CompletedTask;
        }
    }
}
```

### Unit Test: Verify Default Behavior

```csharp
[TestMethod]
public async Task InitializeAsync_DefaultIsNoOp()
{
    // Arrange
    var module = new MinimalModule();
    var context = CreateTestContext();

    // Act & Assert (should not throw)
    await module.InitializeAsync(context, CancellationToken.None);
}

private class MinimalModule : VisoraModule
{
    public override ModuleDescriptor Descriptor
        => ModuleDescriptor.Create("minimal", "Minimal", new Version(1, 0, 0));

    // Uses default InitializeAsync (no override)
}
```

### Unit Test: Verify Override Behavior

```csharp
[TestMethod]
public async Task DiscoverComponents_CanBeOverridden()
{
    // Arrange
    var module = new FilteringModule();
    var context = new ModuleDiscoveryContext(module.GetType().Assembly);

    // Act
    var components = module.DiscoverComponents(context).ToArray();

    // Assert
    Assert.IsTrue(components.Length > 0);
    Assert.IsTrue(components.All(t => t.Name.Contains("Test")),
        "Should only return components with 'Test' in name");
}

private class FilteringModule : VisoraModule
{
    public override ModuleDescriptor Descriptor
        => ModuleDescriptor.Create("filtering", "Filtering", new Version(1, 0, 0));

    public override IEnumerable<Type> DiscoverComponents(
        ModuleDiscoveryContext context)
    {
        // Override: filter by name
        return base.DiscoverComponents(context)
            .Where(t => t.Name.Contains("Test"));
    }
}
```

---

## Tradeoffs

### Advantages

1. **Sensible Defaults**
   - Base class provides working defaults
   - Subclasses only override what they need
   - Reduces boilerplate

2. **Controlled Extension**
   - Only specific points can be customized
   - Framework maintains control of algorithm
   - Prevents breaking changes

3. **Code Reuse**
   - Common logic in base class
   - Subclasses reuse base implementation
   - DRY principle

4. **Consistency**
   - All modules follow same pattern
   - Predictable behavior
   - Easy to learn

5. **Inversion of Control**
   - Framework calls into module code
   - Enables dynamic loading and lifecycle management

### Disadvantages

1. **Inheritance Coupling**
   - Subclasses tightly coupled to base class
   - Changes to base class can affect subclasses
   - Hard to change base class without breaking subclasses

2. **Limited Flexibility**
   - Can only override at predefined points
   - Algorithm structure is fixed
   - Composition might be more flexible in some cases

3. **Hidden Dependencies**
   - Subclass behavior depends on base class implementation
   - Calling base methods can have hidden side effects
   - Must read base class code to understand full behavior

4. **Testing Complexity**
   - Must test base class and subclasses separately
   - Interactions between base and subclass can be complex

---

## Alternatives Considered

### Alternative 1: Interface-Based (No Template)

```csharp
public interface IModule
{
    ModuleDescriptor Descriptor { get; }
    ValueTask InitializeAsync(ModuleContext context, CancellationToken ct);
    ValueTask ShutdownAsync(ModuleContext context, CancellationToken ct);
    IEnumerable<Type> DiscoverComponents(ModuleDiscoveryContext context);
    ValueTask DisposeAsync();
}

// User must implement every method
public class MyModule : IModule
{
    public ModuleDescriptor Descriptor => /* ... */;

    public ValueTask InitializeAsync(ModuleContext context, CancellationToken ct)
        => ValueTask.CompletedTask; // Must write this even if no-op

    public ValueTask ShutdownAsync(ModuleContext context, CancellationToken ct)
        => ValueTask.CompletedTask; // Must write this even if no-op

    // ... etc
}
```

**Pros:**
- No inheritance coupling
- More flexible (can implement multiple interfaces)

**Cons:**
- Lots of boilerplate
- No default implementations
- Every module must implement every method

**Why Not Chosen:** Too much boilerplate for simple modules.

### Alternative 2: Delegation (Strategy Pattern)

```csharp
public interface IModuleInitializer
{
    ValueTask InitializeAsync(ModuleContext context, CancellationToken ct);
}

public sealed class Module
{
    private readonly IModuleInitializer? _initializer;

    public Module(ModuleDescriptor descriptor, IModuleInitializer? initializer = null)
    {
        Descriptor = descriptor;
        _initializer = initializer;
    }

    public ModuleDescriptor Descriptor { get; }

    public ValueTask InitializeAsync(ModuleContext context, CancellationToken ct)
        => _initializer?.InitializeAsync(context, ct) ?? ValueTask.CompletedTask;
}
```

**Pros:**
- No inheritance
- Highly composable
- Can swap strategies at runtime

**Cons:**
- More complex
- Requires more objects (descriptor + strategies)
- Less intuitive for simple cases

**Why Not Chosen:** Overkill for VISORA's needs. Template Method is simpler.

---

## Best Practices

### 1. Provide Sensible Defaults

**DO:**
```csharp
public virtual ValueTask InitializeAsync(ModuleContext context, CancellationToken ct)
    => ValueTask.CompletedTask; // Default: no-op
```

**DON'T:**
```csharp
public abstract ValueTask InitializeAsync(ModuleContext context, CancellationToken ct);
// Forces every subclass to implement, even if they don't need it
```

### 2. Make Abstract Only What's Required

**DO:**
```csharp
public abstract ModuleDescriptor Descriptor { get; }
// Every module MUST have a descriptor
```

**DON'T:**
```csharp
public abstract ValueTask InitializeAsync(...);
// Not every module needs custom initialization
```

### 3. Document Hook Points Clearly

**DO:**
```csharp
/// <summary>
/// Called when the module is being initialized.
/// VIRTUAL - default implementation does nothing.
/// Override to set up module state, register services, etc.
/// </summary>
public virtual ValueTask InitializeAsync(...)
```

### 4. Use Async Throughout

**DO:**
```csharp
public virtual async ValueTask InitializeAsync(...)
{
    await SomeAsyncOperationAsync();
}
```

**DON'T:**
```csharp
public virtual void Initialize(...)
{
    // Blocking - bad for I/O operations
}
```

### 5. Support Cancellation

**DO:**
```csharp
public virtual ValueTask InitializeAsync(
    ModuleContext context,
    CancellationToken cancellationToken = default)
{
    cancellationToken.ThrowIfCancellationRequested();
    // ...
}
```

---

## Advanced Topics

### 1. Template Method with State Machine

Combine template method with state tracking:

```csharp
public abstract class StatefulModule : VisoraModule
{
    private ModuleState _state = ModuleState.Created;

    protected ModuleState State => _state;

    public override async ValueTask InitializeAsync(
        ModuleContext context,
        CancellationToken ct)
    {
        if (_state != ModuleState.Created)
            throw new InvalidOperationException("Module already initialized");

        _state = ModuleState.Initializing;
        await OnInitializeAsync(context, ct);
        _state = ModuleState.Initialized;
    }

    protected abstract ValueTask OnInitializeAsync(ModuleContext context, CancellationToken ct);

    private enum ModuleState
    {
        Created,
        Initializing,
        Initialized,
        ShuttingDown,
        Disposed
    }
}
```

### 2. Template Method with Events

Notify listeners before/after hooks:

```csharp
public abstract class ObservableModule : VisoraModule
{
    public event EventHandler? Initializing;
    public event EventHandler? Initialized;

    public override async ValueTask InitializeAsync(
        ModuleContext context,
        CancellationToken ct)
    {
        Initializing?.Invoke(this, EventArgs.Empty);

        await OnInitializeAsync(context, ct);

        Initialized?.Invoke(this, EventArgs.Empty);
    }

    protected virtual ValueTask OnInitializeAsync(
        ModuleContext context,
        CancellationToken ct)
        => ValueTask.CompletedTask;
}
```

### 3. Template Method with Validation

Validate before calling hooks:

```csharp
public abstract class ValidatedModule : VisoraModule
{
    public override async ValueTask InitializeAsync(
        ModuleContext context,
        CancellationToken ct)
    {
        // Validate context
        if (context is null)
            throw new ArgumentNullException(nameof(context));

        if (context.Capabilities is null)
            throw new ArgumentException("Context must have capabilities");

        // Call hook
        await OnInitializeAsync(context, ct);
    }

    protected abstract ValueTask OnInitializeAsync(
        ModuleContext context,
        CancellationToken ct);
}
```

---

## Summary

VISORA uses the Template Method pattern to:

1. **Define lifecycle algorithms:** Module/component lifecycle is defined in base classes
2. **Provide sensible defaults:** Base classes provide default implementations (often no-ops)
3. **Enable customization:** Subclasses override virtual methods at specific points
4. **Maintain control:** Framework controls when hooks are called
5. **Reduce boilerplate:** Subclasses only override what they need

**Key Implementation Details:**
- Abstract properties for required data (Descriptor)
- Virtual methods for optional hooks (InitializeAsync, etc.)
- Default implementations that do nothing (no-op)
- Async throughout with cancellation support

**Next Steps:**
- Review the Testable Design pattern for how interfaces enable testing
- Review the Lifecycle Management pattern for coordination
- Review the Factory Pattern for object creation

---

**End of Document**
