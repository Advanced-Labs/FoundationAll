# Creating VISORA Components - Step-by-Step Guide

**Last Updated:** 2025-11-10
**VISORA Version:** .NET 9.0
**Skill Level:** Intermediate
**Estimated Time:** 20-40 minutes

---

## Table of Contents

1. [Overview](#overview)
2. [Prerequisites](#prerequisites)
3. [Component Fundamentals](#component-fundamentals)
4. [Step-by-Step Implementation](#step-by-step-implementation)
5. [Component Descriptor Design](#component-descriptor-design)
6. [Command Registration](#command-registration)
7. [Component Lifecycle](#component-lifecycle)
8. [State Management](#state-management)
9. [Testing Components](#testing-components)
10. [Common Mistakes](#common-mistakes)
11. [Advanced Topics](#advanced-topics)
12. [Best Practices](#best-practices)
13. [Reference Examples](#reference-examples)
14. [Cross-References](#cross-references)

---

## Overview

### What is a VISORA Component?

A **component** is a mid-level organizational unit that groups related functionality within a module. Components:

- Package related commands and services
- Have their own lifecycle (initialize, activate, deactivate)
- Can maintain state during their lifetime
- Are discovered automatically by their parent module
- Can be activated/deactivated dynamically

**Key Characteristics:**
- **Contained by modules**: Each component belongs to exactly one module
- **Command providers**: Components create and register commands
- **Lifecycle participants**: Initialize, activate, deactivate, dispose
- **State containers**: Can maintain state between activations

### Component Architecture

```
Module
  └─ Component 1 (e.g., CoreUtilitiesComponent)
       ├─ ComponentDescriptor (metadata)
       ├─ InitializeAsync() (one-time setup)
       ├─ ActivateAsync() (when loaded into UI)
       ├─ CreateCommands() (register commands)
       ├─ DeactivateAsync() (when unloaded from UI)
       └─ DisposeAsync() (cleanup)
  └─ Component 2 (e.g., DiagnosticsComponent)
       └─ [same structure]
```

### Component vs Module

| Aspect | Module | Component |
|--------|--------|-----------|
| **Scope** | Top-level assembly | Contained within module |
| **Discovery** | By file naming (`.vixm.dll`) | By reflection in module |
| **Purpose** | Package related components | Group related commands |
| **Lifecycle** | Init → Shutdown | Init → Activate → Deactivate → Dispose |
| **Count** | One per assembly | Many per module |

---

## Prerequisites

### Required Knowledge

- **Modules**: Understanding of VISORA modules ([Creating Modules](./creating-modules.md))
- **C# fundamentals**: Classes, inheritance, async/await
- **Component lifecycle**: Understanding of initialization patterns

### Required References

Components are defined within module projects, so you need:

```xml
<ItemGroup>
  <ProjectReference Include="..\Visora.Contracts\Visora.Contracts.csproj" />
  <ProjectReference Include="..\Visora.Core\Visora.Core.csproj" />
</ItemGroup>
```

---

## Component Fundamentals

### The Component Hierarchy

VISORA provides two base classes:

1. **`VisoraComponent`** (abstract class in `Visora.Contracts`)
   - Defines the contract for all components
   - Pure abstraction with no implementation
   - Located at: `/src/Visora.Contracts/Components/VisoraComponent.cs`

2. **`Component`** (concrete base in `Visora.Core`)
   - Default implementation of `VisoraComponent`
   - Provides common functionality
   - Located at: `/src/Visora.Core/Component.cs`

**Typical inheritance chain:**

```
VisoraComponent (abstract contract)
    ↓
Component (default implementation)
    ↓
YourCustomComponent (your code)
```

### Component Base Class Structure

**File**: `/src/Visora.Contracts/Components/VisoraComponent.cs`

```csharp
public abstract class VisoraComponent : IAsyncDisposable
{
    // REQUIRED: Metadata describing this component
    public abstract ComponentDescriptor Descriptor { get; }

    // OPTIONAL: One-time initialization
    public virtual ValueTask InitializeAsync(
        ComponentContext context,
        CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    // OPTIONAL: Called when component is activated (e.g., loaded into UI)
    public virtual ValueTask ActivateAsync(
        ComponentContext context,
        CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    // OPTIONAL: Called when component is deactivated
    public virtual ValueTask DeactivateAsync(
        ComponentContext context,
        CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    // OPTIONAL: Register commands for this component
    public virtual IEnumerable<VisoraCommand> CreateCommands(
        ComponentContext context)
        => Array.Empty<VisoraCommand>();

    // OPTIONAL: Cleanup resources
    public virtual ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
```

### Component Discovery

Components are discovered by their parent module:

```
Module.DiscoverComponents()
    ↓
Scans assembly for VisoraComponent subclasses
    ↓
Host instantiates each component
    ↓
Host calls InitializeAsync on each component
    ↓
Host calls CreateCommands to get command list
```

---

## Step-by-Step Implementation

### Step 1: Create Component Class

In your module project, create a new file:

**File**: `Components/MyFeatureComponent.cs`

```csharp
using System;
using System.Collections.Generic;
using Visora.Contracts.Components;
using Visora.Contracts.Commands;
using Visora.Core;

namespace MyFeature.Module.Components;

/// <summary>
/// Provides core functionality for MyFeature.
/// </summary>
public sealed class MyFeatureComponent : Component
{
    // Static descriptor - created once, immutable
    private static readonly ComponentDescriptor Info = ComponentDescriptor.Create(
        id: "mycompany.myfeature.core",
        name: "My Feature Core",
        description: "Core commands and utilities for MyFeature.",
        tags: new[] { "core", "utilities" },
        kind: ComponentKind.Generic);

    public override ComponentDescriptor Descriptor => Info;
}
```

### Step 2: Verify Module Discovery

Ensure your module's `DiscoverComponents()` will find this component:

```csharp
// In MyFeatureModule.cs
public override IEnumerable<Type> DiscoverComponents(ModuleDiscoveryContext context)
{
    // Default implementation automatically finds all VisoraComponent subclasses
    return base.DiscoverComponents(context);
}
```

The default implementation will automatically find `MyFeatureComponent` because:
1. It's `public`
2. It inherits from `Component` (which inherits from `VisoraComponent`)
3. It's in the same assembly as the module

### Step 3: Add Commands (Optional)

Override `CreateCommands()` to register commands:

```csharp
public override IEnumerable<VisoraCommand> CreateCommands(ComponentContext context)
{
    yield return new StatusCommand();
    yield return new ConfigureCommand();
    yield return new ResetCommand();
}
```

We'll cover command creation in detail in [Creating Commands](./creating-commands.md).

### Step 4: Add Lifecycle Logic (Optional)

Override lifecycle methods if needed:

```csharp
public override async ValueTask InitializeAsync(
    ComponentContext context,
    CancellationToken cancellationToken = default)
{
    // One-time initialization
    await LoadConfigurationAsync(cancellationToken);
}

public override ValueTask ActivateAsync(
    ComponentContext context,
    CancellationToken cancellationToken = default)
{
    // Called when component becomes active (e.g., UI loaded)
    return ValueTask.CompletedTask;
}

public override ValueTask DeactivateAsync(
    ComponentContext context,
    CancellationToken cancellationToken = default)
{
    // Called when component is deactivated
    return ValueTask.CompletedTask;
}
```

### Step 5: Build and Test

```bash
dotnet build
```

Test that your component is discoverable:

```csharp
var module = new MyFeatureModule();
var context = new ModuleDiscoveryContext(
    typeof(MyFeatureModule).Assembly,
    mockCapabilities.Object);

var componentTypes = module.DiscoverComponents(context).ToList();
Assert.Contains(componentTypes, t => t == typeof(MyFeatureComponent));
```

---

## Component Descriptor Design

### ComponentDescriptor Structure

**File**: `/src/Visora.Contracts/Components/ComponentDescriptor.cs`

```csharp
public sealed record ComponentDescriptor(
    string Id,
    string Name,
    string? Description = null,
    IReadOnlyCollection<string>? Tags = null,
    ComponentKind Kind = ComponentKind.Generic);

public enum ComponentKind
{
    Generic,          // General-purpose component
    Service,          // Background service
    Ui,              // UI-focused component
    Console,         // CLI/console component
    ShellExtension   // Shell integration
}
```

### Descriptor Fields Explained

| Field | Required | Description | Example |
|-------|----------|-------------|---------|
| **Id** | ✅ Yes | Unique identifier (extends module ID) | `"mycompany.myfeature.core"` |
| **Name** | ✅ Yes | Human-readable display name | `"My Feature Core"` |
| **Description** | ❌ No | Detailed description | `"Core commands and utilities..."` |
| **Tags** | ❌ No | Classification tags | `["core", "utilities"]` |
| **Kind** | ❌ No | Component category | `ComponentKind.Console` |

### Naming Your Component (ID Field)

Components extend their module's ID:

```
<module-id>.<component-aspect>
```

**Examples:**

```csharp
// Module: "mycompany.myfeature"
id: "mycompany.myfeature.core"         // Core functionality
id: "mycompany.myfeature.utilities"    // Utility commands
id: "mycompany.myfeature.diagnostics"  // Diagnostic tools
id: "mycompany.myfeature.ui"           // UI-specific features

// Module: "visora.shell.commands.core"
id: "visora.shell.commands.core.utilities"  // From VISORA codebase
```

**Naming Rules:**
- Start with full module ID
- Add one aspect segment
- Use lowercase
- Be descriptive

### Component Kinds

Choose the appropriate `ComponentKind`:

| Kind | Use Case | Example |
|------|----------|---------|
| **Generic** | Default, general-purpose | Generic utilities |
| **Service** | Background services, no UI | File watcher, cache manager |
| **Ui** | UI-heavy components | Settings panel, visualizer |
| **Console** | CLI/console focused | Shell commands, scripts |
| **ShellExtension** | IDE shell integration | Menu items, toolbars |

**Example from VISORA codebase** (`CoreUtilitiesComponent.cs:12-17`):

```csharp
private static readonly ComponentDescriptor Info = ComponentDescriptor.Create(
    id: "visora.shell.commands.core.utilities",
    name: "Core Utilities",
    description: "Diagnostics and helper commands for Visora shell experiments.",
    tags: new[] { "core", "shell", "diagnostics" },
    kind: ComponentKind.Console);
```

### Tags for Organization

Tags help categorize and filter components:

```csharp
tags: new[] { "core", "diagnostics", "utilities" }
tags: new[] { "git", "integration", "vcs" }
tags: new[] { "ui", "windows", "wpf" }
```

**Common tags:**
- **Functional**: "core", "utilities", "diagnostics"
- **Integration**: "git", "github", "docker"
- **Platform**: "windows", "linux", "macos"
- **UI**: "wpf", "console", "terminal"

---

## Command Registration

### CreateCommands() Method

Components register commands by overriding `CreateCommands()`:

```csharp
public override IEnumerable<VisoraCommand> CreateCommands(ComponentContext context)
{
    yield return new Command1();
    yield return new Command2();
    yield return new Command3();
}
```

### When is CreateCommands() Called?

```
Module loaded
    ↓
Components discovered
    ↓
Component.InitializeAsync() called
    ↓
Component.CreateCommands() called  ← Here
    ↓
Host registers all commands
```

**Note**: `CreateCommands()` is called once per component instance, after `InitializeAsync()`.

### Passing Context to Commands

You can use `ComponentContext` to configure commands:

```csharp
public override IEnumerable<VisoraCommand> CreateCommands(ComponentContext context)
{
    var logger = context.Capabilities.GetOptional<ILogger>();

    yield return new StatusCommand(logger);
    yield return new ConfigureCommand(_configuration);
    yield return new ResetCommand(_stateManager);
}
```

### Conditional Command Registration

Register commands based on runtime conditions:

```csharp
public override IEnumerable<VisoraCommand> CreateCommands(ComponentContext context)
{
    // Always available
    yield return new BasicCommand();

    // Only on Windows
    if (OperatingSystem.IsWindows())
    {
        yield return new WindowsSpecificCommand();
    }

    // Only if capability is available
    if (context.Capabilities.TryGet<IGitIntegration>(out _))
    {
        yield return new GitCommand();
    }

    // Only in debug builds
    #if DEBUG
    yield return new DebugCommand();
    #endif
}
```

### Empty Command List

If a component doesn't provide commands:

```csharp
// Option 1: Don't override (default returns empty)
// (No CreateCommands override needed)

// Option 2: Explicit empty
public override IEnumerable<VisoraCommand> CreateCommands(ComponentContext context)
    => Array.Empty<VisoraCommand>();

// Option 3: Explicit empty with yield
public override IEnumerable<VisoraCommand> CreateCommands(ComponentContext context)
{
    yield break;  // Empty enumerable
}
```

---

## Component Lifecycle

### Lifecycle States

```
┌──────────────┐
│   CREATED    │  ← Component instance created
└──────┬───────┘
       │
       ▼
┌──────────────┐
│ INITIALIZING │  ← InitializeAsync() called (once)
└──────┬───────┘
       │
       ▼
┌──────────────┐
│  INITIALIZED │  ← CreateCommands() called
└──────┬───────┘
       │
       ▼
┌──────────────┐
│  ACTIVATING  │  ← ActivateAsync() called
└──────┬───────┘
       │
       ▼
┌──────────────┐
│    ACTIVE    │  ← Component is running
└──────┬───────┘
       │
       ▼
┌──────────────┐
│ DEACTIVATING │  ← DeactivateAsync() called
└──────┬───────┘
       │
       ▼
┌──────────────┐
│  DISPOSED    │  ← DisposeAsync() called
└──────────────┘
```

**Notes:**
- **InitializeAsync**: Called once, one-time setup
- **ActivateAsync/DeactivateAsync**: Can be called multiple times
- **DisposeAsync**: Called once, final cleanup

### InitializeAsync - One-Time Setup

Use for initialization that should happen once:

```csharp
private ILogger? _logger;
private Configuration? _config;

public override async ValueTask InitializeAsync(
    ComponentContext context,
    CancellationToken cancellationToken = default)
{
    _logger = context.Capabilities.GetOptional<ILogger>();
    _logger?.LogInformation("Initializing {ComponentName}", Descriptor.Name);

    // Load configuration
    _config = await LoadConfigurationAsync(cancellationToken);

    // Initialize resources
    await InitializeResourcesAsync(cancellationToken);

    _logger?.LogInformation("Initialization complete");
}
```

**Use cases:**
- Loading configuration files
- Establishing database connections
- Initializing caches
- Creating service clients

### ActivateAsync - Component Activation

Called when the component becomes active (e.g., loaded into UI):

```csharp
private Timer? _refreshTimer;

public override ValueTask ActivateAsync(
    ComponentContext context,
    CancellationToken cancellationToken = default)
{
    _logger?.LogInformation("Activating {ComponentName}", Descriptor.Name);

    // Start background refresh
    _refreshTimer = new Timer(
        callback: _ => RefreshData(),
        state: null,
        dueTime: TimeSpan.Zero,
        period: TimeSpan.FromSeconds(30));

    return ValueTask.CompletedTask;
}
```

**Use cases:**
- Starting periodic tasks
- Subscribing to events
- Showing UI elements
- Enabling features

### DeactivateAsync - Component Deactivation

Called when the component is deactivated:

```csharp
public override ValueTask DeactivateAsync(
    ComponentContext context,
    CancellationToken cancellationToken = default)
{
    _logger?.LogInformation("Deactivating {ComponentName}", Descriptor.Name);

    // Stop refresh timer
    _refreshTimer?.Dispose();
    _refreshTimer = null;

    return ValueTask.CompletedTask;
}
```

**Use cases:**
- Stopping periodic tasks
- Unsubscribing from events
- Hiding UI elements
- Disabling features

### DisposeAsync - Final Cleanup

Called once before the component is destroyed:

```csharp
public override async ValueTask DisposeAsync()
{
    _refreshTimer?.Dispose();

    if (_database != null)
    {
        await _database.DisposeAsync();
    }

    await base.DisposeAsync();
}
```

**Use cases:**
- Releasing unmanaged resources
- Closing connections
- Flushing caches
- Final cleanup

### Component Context

**File**: `/src/Visora.Contracts/Components/ComponentContext.cs`

```csharp
public sealed class ComponentContext
{
    // Parent module
    public VisoraModule Module { get; }

    // Component descriptor
    public ComponentDescriptor Descriptor { get; }

    // Capability provider
    public ICapabilityProvider Capabilities { get; }

    // Execution surface
    public Surface Surface { get; }

    // Host properties
    public IReadOnlyDictionary<string, object?> Properties { get; }
}
```

**Accessing context:**

```csharp
public override ValueTask InitializeAsync(
    ComponentContext context,
    CancellationToken cancellationToken = default)
{
    // Get parent module info
    var moduleName = context.Module.Descriptor.Name;

    // Get execution surface
    var isCli = context.Surface == Surface.Cli;

    // Access capabilities
    var logger = context.Capabilities.GetOptional<ILogger>();

    return ValueTask.CompletedTask;
}
```

---

## State Management

### Component State Patterns

Components can maintain state between activations:

```csharp
public sealed class MyFeatureComponent : Component
{
    // Configuration (loaded once)
    private Configuration? _config;

    // Services (initialized once)
    private ILogger? _logger;
    private HttpClient? _httpClient;

    // State (can change)
    private int _requestCount;
    private DateTime _lastRefresh;

    // Active resources (created on activate, disposed on deactivate)
    private Timer? _timer;
    private CancellationTokenSource? _cts;
}
```

### Thread-Safe State

If your component is accessed from multiple threads:

```csharp
private readonly object _lock = new();
private int _counter;

public void IncrementCounter()
{
    lock (_lock)
    {
        _counter++;
    }
}
```

Or use concurrent collections:

```csharp
private readonly ConcurrentDictionary<string, object> _cache = new();
```

### State Persistence

Save state to disk:

```csharp
public override async ValueTask DeactivateAsync(
    ComponentContext context,
    CancellationToken cancellationToken = default)
{
    // Save state before deactivation
    var state = new ComponentState
    {
        Counter = _counter,
        LastRefresh = _lastRefresh
    };

    var json = JsonSerializer.Serialize(state);
    await File.WriteAllTextAsync("state.json", json, cancellationToken);
}

public override async ValueTask ActivateAsync(
    ComponentContext context,
    CancellationToken cancellationToken = default)
{
    // Restore state on activation
    if (File.Exists("state.json"))
    {
        var json = await File.ReadAllTextAsync("state.json", cancellationToken);
        var state = JsonSerializer.Deserialize<ComponentState>(json);
        _counter = state.Counter;
        _lastRefresh = state.LastRefresh;
    }
}
```

---

## Testing Components

### Unit Testing Setup

```csharp
using Xunit;
using Moq;
using MyFeature.Module.Components;

public class MyFeatureComponentTests
{
    [Fact]
    public void Component_HasValidDescriptor()
    {
        // Arrange & Act
        var component = new MyFeatureComponent();

        // Assert
        Assert.NotNull(component.Descriptor);
        Assert.Equal("mycompany.myfeature.core", component.Descriptor.Id);
        Assert.Equal("My Feature Core", component.Descriptor.Name);
    }
}
```

### Testing Initialization

```csharp
[Fact]
public async Task InitializeAsync_LoadsConfiguration()
{
    // Arrange
    var component = new MyFeatureComponent();
    var mockModule = new Mock<VisoraModule>();
    var mockCapabilities = new Mock<ICapabilityProvider>();

    var context = new ComponentContext(
        module: mockModule.Object,
        descriptor: component.Descriptor,
        capabilities: mockCapabilities.Object,
        surface: Surface.Cli,
        properties: new Dictionary<string, object?>());

    // Act
    await component.InitializeAsync(context);

    // Assert
    // Verify initialization succeeded
}
```

### Testing Command Registration

```csharp
[Fact]
public void CreateCommands_ReturnsExpectedCommands()
{
    // Arrange
    var component = new MyFeatureComponent();
    var mockModule = new Mock<VisoraModule>();
    var mockCapabilities = new Mock<ICapabilityProvider>();

    var context = new ComponentContext(
        module: mockModule.Object,
        descriptor: component.Descriptor,
        capabilities: mockCapabilities.Object,
        surface: Surface.Cli,
        properties: new Dictionary<string, object?>());

    // Act
    var commands = component.CreateCommands(context).ToList();

    // Assert
    Assert.NotEmpty(commands);
    Assert.Contains(commands, c => c.Descriptor.Id == "myfeature.status");
}
```

### Testing Lifecycle

```csharp
[Fact]
public async Task Lifecycle_InitializeActivateDeactivateDispose_Succeeds()
{
    // Arrange
    var component = new MyFeatureComponent();
    var context = CreateTestContext();

    // Act & Assert
    await component.InitializeAsync(context);
    await component.ActivateAsync(context);
    await component.DeactivateAsync(context);
    await component.DisposeAsync();

    // No exceptions = success
}
```

---

## Common Mistakes

### Mistake 1: Non-Public Component Class

**Problem:**
```csharp
internal class MyFeatureComponent : Component  ❌
```

**Solution:**
```csharp
public sealed class MyFeatureComponent : Component  ✅
```

Components must be `public` for reflection discovery.

### Mistake 2: Missing Parameterless Constructor

**Problem:**
```csharp
public class MyFeatureComponent : Component
{
    public MyFeatureComponent(ILogger logger)  ❌
    {
        // Can't be instantiated by reflection
    }
}
```

**Solution:**
```csharp
public class MyFeatureComponent : Component
{
    private ILogger? _logger;

    public MyFeatureComponent()  ✅
    {
        // Parameterless constructor required
    }

    public override ValueTask InitializeAsync(ComponentContext context, ...)
    {
        _logger = context.Capabilities.GetOptional<ILogger>();
        return ValueTask.CompletedTask;
    }
}
```

### Mistake 3: Wrong Component ID Format

**Problem:**
```csharp
id: "core"  ❌  // Missing module prefix
id: "MyFeature.Core"  ❌  // Wrong casing
```

**Solution:**
```csharp
id: "mycompany.myfeature.core"  ✅  // Extends module ID
```

### Mistake 4: Blocking Lifecycle Methods

**Problem:**
```csharp
public override ValueTask InitializeAsync(...)
{
    Thread.Sleep(1000);  ❌  // Blocking
    return ValueTask.CompletedTask;
}
```

**Solution:**
```csharp
public override async ValueTask InitializeAsync(...)
{
    await Task.Delay(1000, cancellationToken);  ✅
}
```

### Mistake 5: Not Cleaning Up in Deactivate

**Problem:**
```csharp
private Timer? _timer;

public override ValueTask ActivateAsync(...)
{
    _timer = new Timer(...);
    return ValueTask.CompletedTask;
}

// Missing: DeactivateAsync to dispose timer  ❌
```

**Solution:**
```csharp
public override ValueTask DeactivateAsync(...)
{
    _timer?.Dispose();  ✅
    _timer = null;
    return ValueTask.CompletedTask;
}
```

### Mistake 6: State Mutation in CreateCommands

**Problem:**
```csharp
public override IEnumerable<VisoraCommand> CreateCommands(...)
{
    _counter++;  ❌  // Mutating state during command creation
    yield return new MyCommand();
}
```

**Solution:**
```csharp
public override IEnumerable<VisoraCommand> CreateCommands(...)
{
    // Don't mutate state here - just return commands
    yield return new MyCommand();  ✅
}
```

---

## Advanced Topics

### Topic 1: Multiple Components Per Module

A module can have many components:

```csharp
namespace MyFeature.Module.Components;

// Core functionality
public sealed class CoreComponent : Component { }

// Diagnostics
public sealed class DiagnosticsComponent : Component { }

// UI integration
public sealed class UiComponent : Component { }
```

All will be discovered automatically.

### Topic 2: Component Communication

Components can communicate via capabilities:

```csharp
// Component A provides a capability
public sealed class ComponentA : Component
{
    private MyService _service = new();

    public override ValueTask InitializeAsync(ComponentContext context, ...)
    {
        // Register service as capability
        context.Capabilities.Register<IMyService>(_service);
        return ValueTask.CompletedTask;
    }
}

// Component B consumes the capability
public sealed class ComponentB : Component
{
    private IMyService? _service;

    public override ValueTask InitializeAsync(ComponentContext context, ...)
    {
        _service = context.Capabilities.GetOptional<IMyService>();
        return ValueTask.CompletedTask;
    }
}
```

### Topic 3: Lazy Initialization

Defer expensive initialization until first use:

```csharp
private readonly Lazy<ExpensiveResource> _resource;

public MyFeatureComponent()
{
    _resource = new Lazy<ExpensiveResource>(() => new ExpensiveResource());
}

public override IEnumerable<VisoraCommand> CreateCommands(ComponentContext context)
{
    yield return new MyCommand(_resource);  // Pass lazy instance
}
```

### Topic 4: Background Tasks

Run background work in components:

```csharp
private CancellationTokenSource? _cts;
private Task? _backgroundTask;

public override ValueTask ActivateAsync(...)
{
    _cts = new CancellationTokenSource();
    _backgroundTask = Task.Run(() => BackgroundWork(_cts.Token));
    return ValueTask.CompletedTask;
}

private async Task BackgroundWork(CancellationToken cancellationToken)
{
    while (!cancellationToken.IsCancellationRequested)
    {
        await PerformWorkAsync(cancellationToken);
        await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
    }
}

public override async ValueTask DeactivateAsync(...)
{
    _cts?.Cancel();
    if (_backgroundTask != null)
    {
        await _backgroundTask;
    }
}
```

---

## Best Practices

### 1. Use Sealed Classes

```csharp
public sealed class MyFeatureComponent : Component  ✅
```

Prevents inheritance and enables optimizations.

### 2. Static Descriptors

```csharp
private static readonly ComponentDescriptor Info = ...;  ✅
```

Create descriptor once as static field.

### 3. Minimal State

Keep component state minimal:

```csharp
// Good: Minimal state
private Configuration? _config;
private ILogger? _logger;

// Bad: Too much state
private Dictionary<string, List<ComplexObject>> _cache;  ❌
```

### 4. Proper Resource Management

Always pair resource creation with disposal:

```csharp
// Create in InitializeAsync or ActivateAsync
public override ValueTask ActivateAsync(...)
{
    _timer = new Timer(...);
    return ValueTask.CompletedTask;
}

// Dispose in DeactivateAsync or DisposeAsync
public override ValueTask DeactivateAsync(...)
{
    _timer?.Dispose();
    _timer = null;
    return ValueTask.CompletedTask;
}
```

### 5. Defensive Capability Access

```csharp
// Optional capabilities
var logger = context.Capabilities.GetOptional<ILogger>();
logger?.LogInformation("...");

// Required capabilities
var fileSystem = context.Capabilities.GetRequired<IFileSystem>();
```

### 6. Respect Surface Type

Adapt behavior based on execution surface:

```csharp
public override IEnumerable<VisoraCommand> CreateCommands(ComponentContext context)
{
    yield return new CoreCommand();

    // Only register UI commands in GUI
    if (context.Surface == Surface.Wpf)
    {
        yield return new ShowDialogCommand();
    }
}
```

---

## Reference Examples

### Complete Component Example

**File**: `src/Visora.Shell.Commands.Core/Components/CoreUtilitiesComponent.cs`

```csharp
using System;
using System.Collections.Generic;
using Visora.Contracts.Components;
using Visora.Contracts.Commands;
using Visora.Core;
using Visora.Shell.Commands.Core.Commands;

namespace Visora.Shell.Commands.Core.Components;

public sealed class CoreUtilitiesComponent : Component
{
    private static readonly ComponentDescriptor Info = ComponentDescriptor.Create(
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

---

## Cross-References

### Related Documentation

- **[Creating Modules](./creating-modules.md)**: Parent topic
- **[Creating Commands](./creating-commands.md)**: Next step
- **[Testing Strategies](./testing-strategies.md)**: Testing guide
- **[Template Method Pattern](../patterns/template-method/visora-analysis.md)**: Lifecycle pattern
- **[Context Objects Pattern](../patterns/context-objects/visora-analysis.md)**: ComponentContext
- **[Naming Conventions](../conventions/naming-conventions.md)**: Component naming

### Related Decisions

- **[ADR-003: Async Everywhere](../decisions/async-everywhere.md)**: Why lifecycle is async
- **[ADR-006: Sealed Records](../decisions/sealed-records.md)**: Descriptor immutability

---

**End of Document**
