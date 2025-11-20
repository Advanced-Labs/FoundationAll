# Creating VISORA Modules - Step-by-Step Guide

**Last Updated:** 2025-11-10
**VISORA Version:** .NET 9.0
**Skill Level:** Intermediate
**Estimated Time:** 30-60 minutes

---

## Table of Contents

1. [Overview](#overview)
2. [Prerequisites](#prerequisites)
3. [Module Fundamentals](#module-fundamentals)
4. [Step-by-Step Implementation](#step-by-step-implementation)
5. [Module Descriptor Design](#module-descriptor-design)
6. [Component Discovery](#component-discovery)
7. [Lifecycle Management](#lifecycle-management)
8. [Testing Your Module](#testing-your-module)
9. [Deployment](#deployment)
10. [Common Mistakes](#common-mistakes)
11. [Advanced Topics](#advanced-topics)
12. [Best Practices](#best-practices)
13. [Reference Examples](#reference-examples)
14. [Cross-References](#cross-references)

---

## Overview

### What is a VISORA Module?

A **module** is the top-level unit of extensibility in VISORA. Modules are self-contained assemblies that:

- Package related functionality (components, commands, services)
- Declare metadata through descriptors
- Participate in lifecycle events (initialization, shutdown)
- Discover and expose components
- Can be dynamically loaded and unloaded

**Key Characteristics:**
- **Isolation**: Runs in its own `AssemblyLoadContext`
- **Self-describing**: Uses reflection + naming conventions (no manifest files)
- **Unloadable**: Can be unloaded to free resources
- **Discoverable**: Host finds modules through `.vixm.dll` naming convention

### Module Architecture

```
┌────────────────────────────────────────────────┐
│           YourModule.vixm.dll                  │
├────────────────────────────────────────────────┤
│  MyCustomModule : Module                       │
│    ├─ ModuleDescriptor (metadata)              │
│    ├─ InitializeAsync() (startup logic)        │
│    ├─ ShutdownAsync() (cleanup logic)          │
│    └─ DiscoverComponents() (find components)   │
│                                                 │
│  Component Classes:                            │
│    ├─ FeatureAComponent : Component            │
│    ├─ FeatureBComponent : Component            │
│    └─ UtilitiesComponent : Component           │
│                                                 │
│  Dependencies:                                  │
│    ├─ Visora.Contracts.dll (shared types)      │
│    └─ Your NuGet packages (isolated)           │
└────────────────────────────────────────────────┘
```

---

## Prerequisites

### Required Knowledge

- **C# fundamentals**: Classes, inheritance, async/await
- **.NET 9.0**: Understanding of modern .NET project structure
- **NuGet**: Package management basics
- **Git**: Version control (optional but recommended)

### Development Environment

1. **.NET 9.0 SDK** or later
2. **IDE**: Visual Studio 2022, JetBrains Rider, or VS Code
3. **VISORA platform**: Clone the repository or reference packages

### Required References

Your module project must reference:

```xml
<ItemGroup>
  <ProjectReference Include="..\Visora.Contracts\Visora.Contracts.csproj" />
  <ProjectReference Include="..\Visora.Core\Visora.Core.csproj" />
</ItemGroup>
```

Or via NuGet (when published):

```xml
<ItemGroup>
  <PackageReference Include="Visora.Contracts" Version="0.1.0" />
  <PackageReference Include="Visora.Core" Version="0.1.0" />
</ItemGroup>
```

---

## Module Fundamentals

### The Module Hierarchy

VISORA provides two base classes:

1. **`VisoraModule`** (abstract class in `Visora.Contracts`)
   - Defines the contract for all modules
   - Pure abstraction with no implementation
   - Located at: `/src/Visora.Contracts/Modules/VisoraModule.cs`

2. **`Module`** (concrete base in `Visora.Core`)
   - Default implementation of `VisoraModule`
   - Provides component discovery logic
   - Located at: `/src/Visora.Core/Module.cs`

**Typical inheritance chain:**

```
VisoraModule (abstract contract)
    ↓
Module (default implementation)
    ↓
YourCustomModule (your code)
```

### Module Base Class Structure

**File**: `/src/Visora.Contracts/Modules/VisoraModule.cs`

```csharp
public abstract class VisoraModule : IAsyncDisposable
{
    // REQUIRED: Metadata describing this module
    public abstract ModuleDescriptor Descriptor { get; }

    // OPTIONAL: Called when module is initialized
    public virtual ValueTask InitializeAsync(
        ModuleContext context,
        CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    // OPTIONAL: Called before module is unloaded
    public virtual ValueTask ShutdownAsync(
        ModuleContext context,
        CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    // OPTIONAL: Discover components in this module
    public virtual IEnumerable<Type> DiscoverComponents(
        ModuleDiscoveryContext context)
        => context.EnumerateComponentCandidates()
            .Where(t => typeof(VisoraComponent).IsAssignableFrom(t));

    // OPTIONAL: Cleanup resources
    public virtual ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
```

### Module Discovery Mechanism

VISORA discovers modules through:

1. **Naming Convention**: Files ending with `.vixm.dll`
2. **Reflection**: Scanning assemblies for `VisoraModule` subclasses
3. **Instantiation**: Creating instances via parameterless constructor
4. **Validation**: Checking descriptor validity

---

## Step-by-Step Implementation

### Step 1: Create a New Class Library Project

**Using .NET CLI:**

```bash
# Navigate to your modules directory
cd /path/to/Visora/src

# Create a new class library targeting .NET 9.0
dotnet new classlib -n MyFeature.Module -f net9.0

# Navigate into the project
cd MyFeature.Module
```

**Using Visual Studio:**
1. File → New → Project
2. Select "Class Library (.NET)"
3. Name: `MyFeature.Module`
4. Target Framework: .NET 9.0

### Step 2: Configure the Project File

Edit `MyFeature.Module.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <!-- Target .NET 9.0 -->
    <TargetFramework>net9.0</TargetFramework>

    <!-- Enable nullable reference types -->
    <Nullable>enable</Nullable>

    <!-- CRITICAL: Output must end with .vixm.dll -->
    <AssemblyName>MyFeature.vixm</AssemblyName>

    <!-- Metadata -->
    <Version>0.1.0</Version>
    <Authors>Your Name</Authors>
    <Description>My custom VISORA module</Description>
  </PropertyGroup>

  <ItemGroup>
    <!-- Reference VISORA contracts and core -->
    <ProjectReference Include="..\Visora.Contracts\Visora.Contracts.csproj" />
    <ProjectReference Include="..\Visora.Core\Visora.Core.csproj" />
  </ItemGroup>

</Project>
```

**Critical Setting:**
```xml
<AssemblyName>MyFeature.vixm</AssemblyName>
```

This ensures the compiled assembly is named `MyFeature.vixm.dll`, which is how VISORA identifies module assemblies.

### Step 3: Create Your Module Class

Delete the default `Class1.cs` and create `MyFeatureModule.cs`:

```csharp
using System;
using System.Collections.Generic;
using Visora.Core;
using Visora.Contracts.Modules;

namespace MyFeature.Module;

/// <summary>
/// Main module class for MyFeature.
/// </summary>
public sealed class MyFeatureModule : Module
{
    // Static descriptor - created once, immutable
    private static readonly ModuleDescriptor ModuleInfo = ModuleDescriptor.Create(
        id: "mycompany.myfeature",
        name: "My Feature Module",
        version: new Version(0, 1, 0),
        description: "Provides custom functionality for X, Y, and Z.",
        runtimeHints: ModuleRuntimeHints.Create(
            executableRelativePath: null,
            exposeExecutableGlobally: false,
            aliases: new[] { "myfeat" }));

    // REQUIRED: Return the module descriptor
    public override ModuleDescriptor Descriptor => ModuleInfo;

    // OPTIONAL: Override to customize component discovery
    public override IEnumerable<Type> DiscoverComponents(ModuleDiscoveryContext context)
        => base.DiscoverComponents(context);
}
```

### Step 4: Build the Module

```bash
dotnet build
```

**Expected output:**
```
Build succeeded.
    0 Warning(s)
    0 Error(s)

Output: bin/Debug/net9.0/MyFeature.vixm.dll
```

### Step 5: Verify Module Discovery

Create a simple test to verify the module can be loaded:

```csharp
using Visora.Core.Modules;

// Create module catalog
var catalog = new ModuleCatalog();

// Configure discovery
var options = new ModuleCatalogOptions
{
    ModuleSearchPath = "/path/to/bin/Debug/net9.0"
};

// Discover modules
await catalog.DiscoverAsync(options);

// Check if your module was found
var myModule = catalog.GetById("mycompany.myfeature");
Console.WriteLine($"Module loaded: {myModule?.Descriptor.Name}");
```

---

## Module Descriptor Design

### ModuleDescriptor Structure

**File**: `/src/Visora.Contracts/Modules/ModuleDescriptor.cs`

```csharp
public sealed record ModuleDescriptor(
    string Id,
    string Name,
    Version Version,
    string? Description = null,
    IReadOnlyDictionary<string, string>? Tags = null,
    ModuleRuntimeHints? RuntimeHints = null);
```

### Descriptor Fields Explained

| Field | Required | Description | Example |
|-------|----------|-------------|---------|
| **Id** | ✅ Yes | Unique identifier (dot-separated) | `"visora.shell.commands.core"` |
| **Name** | ✅ Yes | Human-readable display name | `"Visora Shell Commands"` |
| **Version** | ✅ Yes | Semantic version | `new Version(0, 1, 0)` |
| **Description** | ❌ No | Detailed description | `"Baseline commands for..."` |
| **Tags** | ❌ No | Key-value metadata | `new Dictionary<string,string>()` |
| **RuntimeHints** | ❌ No | Deployment hints | See below |

### Naming Your Module (ID Field)

Follow the **hierarchical dot-notation** pattern:

```
<vendor>.<subsystem>.<feature>[.<specialization>]
```

**Examples:**

```csharp
// Good examples
id: "acme.diagnostics.network"           // Third-party vendor
id: "acme.diagnostics.network.tcp"       // With specialization
id: "mycompany.integration.github"       // Integration module

// VISORA first-party modules use "visora" prefix
id: "visora.shell.commands.core"
id: "visora.terminal.emulation"

// Bad examples
❌ "NetworkDiagnostics"                   // Not dot-separated
❌ "network.diagnostics"                  // Missing vendor
❌ "acme.diagnostics.network.tcp.ipv6"   // Too deep (5 levels)
```

**Naming Rules:**
- Use **lowercase** for all segments
- Maximum **4 segments** (vendor + 3 levels)
- No underscores or hyphens (use dots only)
- Be descriptive but concise

### Runtime Hints

Runtime hints provide deployment and execution metadata:

```csharp
ModuleRuntimeHints.Create(
    executableRelativePath: "tools/mytool.exe",      // Path to bundled executable
    exposeExecutableGlobally: true,                   // Add to PATH?
    aliases: new[] { "myfeat", "mf" })               // Command aliases
```

**Use Cases:**
- **executableRelativePath**: Module bundles a CLI tool
- **exposeExecutableGlobally**: Tool should be globally accessible
- **aliases**: Short names for convenience

**Example from VISORA codebase** (`ShellCommandsModule.cs:10-18`):

```csharp
private static readonly ModuleDescriptor ModuleInfo = ModuleDescriptor.Create(
    id: "visora.shell.commands.core",
    name: "Visora Shell Commands",
    version: new Version(0, 1, 0),
    description: "Baseline commands for diagnostics and exploration.",
    runtimeHints: ModuleRuntimeHints.Create(
        executableRelativePath: null,
        exposeExecutableGlobally: false,
        aliases: new[] { "vscc" }));
```

---

## Component Discovery

### How Discovery Works

When a module is loaded, the host calls `DiscoverComponents()` to find all component classes:

```
Module.DiscoverComponents(context)
    ↓
ModuleDiscoveryContext.EnumerateComponentCandidates()
    ↓
Assembly.GetTypes() → Filter by VisoraComponent
    ↓
Returns Type[] to host
    ↓
Host instantiates components
```

### Default Discovery Implementation

**File**: `/src/Visora.Contracts/Modules/VisoraModule.cs:35-37`

```csharp
public virtual IEnumerable<Type> DiscoverComponents(ModuleDiscoveryContext context)
    => context.EnumerateComponentCandidates()
        .Where(t => typeof(VisoraComponent).IsAssignableFrom(t));
```

**How it works:**
1. `EnumerateComponentCandidates()` scans the module assembly
2. Filters for types inheriting from `VisoraComponent`
3. Returns all matching types
4. Host instantiates each component

### Custom Discovery (Advanced)

You can override discovery to:
- Filter components based on runtime conditions
- Add components from external assemblies
- Implement conditional loading

**Example: Conditional component loading**

```csharp
public override IEnumerable<Type> DiscoverComponents(ModuleDiscoveryContext context)
{
    var components = base.DiscoverComponents(context).ToList();

    // Only include advanced features if capability is available
    if (context.Capabilities.TryGet<IAdvancedFeatures>(out _))
    {
        components.Add(typeof(AdvancedComponent));
    }

    // Filter by platform
    if (OperatingSystem.IsWindows())
    {
        components.Add(typeof(WindowsSpecificComponent));
    }

    return components;
}
```

### Discovery Context

**File**: `/src/Visora.Contracts/Modules/ModuleDiscoveryContext.cs`

```csharp
public sealed class ModuleDiscoveryContext
{
    public Assembly ModuleAssembly { get; }
    public ICapabilityProvider Capabilities { get; }

    public IEnumerable<Type> EnumerateComponentCandidates()
    {
        // Scan assembly for public, non-abstract classes
        return ModuleAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.IsPublic);
    }
}
```

---

## Lifecycle Management

### Module Lifecycle States

```
┌──────────────┐
│   CREATED    │  ← Module instance created via reflection
└──────┬───────┘
       │
       ▼
┌──────────────┐
│ INITIALIZING │  ← InitializeAsync() called
└──────┬───────┘
       │
       ▼
┌──────────────┐
│   ACTIVE     │  ← Module is running, components are active
└──────┬───────┘
       │
       ▼
┌──────────────┐
│  SHUTTING    │  ← ShutdownAsync() called
│     DOWN     │
└──────┬───────┘
       │
       ▼
┌──────────────┐
│  DISPOSED    │  ← DisposeAsync() called, context unloaded
└──────────────┘
```

### InitializeAsync - Startup Logic

Called once when the module is first loaded.

**Signature:**

```csharp
public virtual ValueTask InitializeAsync(
    ModuleContext context,
    CancellationToken cancellationToken = default)
```

**Use this for:**
- Opening database connections
- Loading configuration files
- Registering event handlers
- Initializing caches
- Validating prerequisites

**Example:**

```csharp
private ILogger? _logger;
private DatabaseConnection? _database;

public override async ValueTask InitializeAsync(
    ModuleContext context,
    CancellationToken cancellationToken = default)
{
    // Get optional capabilities
    _logger = context.Capabilities.GetOptional<ILogger>();
    _logger?.LogInformation("Initializing {ModuleName}", Descriptor.Name);

    // Load configuration
    var configPath = Path.Combine(
        context.Properties["ModulePath"] as string ?? "",
        "config.json");

    if (File.Exists(configPath))
    {
        var config = await LoadConfigAsync(configPath, cancellationToken);
        _database = await DatabaseConnection.OpenAsync(
            config.ConnectionString,
            cancellationToken);
    }

    _logger?.LogInformation("Module initialized successfully");
}
```

### ShutdownAsync - Cleanup Logic

Called before the module is unloaded.

**Signature:**

```csharp
public virtual ValueTask ShutdownAsync(
    ModuleContext context,
    CancellationToken cancellationToken = default)
```

**Use this for:**
- Saving state to disk
- Closing database connections
- Unregistering event handlers
- Flushing caches
- Canceling background tasks

**Example:**

```csharp
public override async ValueTask ShutdownAsync(
    ModuleContext context,
    CancellationToken cancellationToken = default)
{
    _logger?.LogInformation("Shutting down {ModuleName}", Descriptor.Name);

    // Cancel background tasks
    _cancellationTokenSource?.Cancel();

    // Wait for tasks to complete (with timeout)
    if (_backgroundTask != null)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        try
        {
            await _backgroundTask.WaitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            _logger?.LogWarning("Background task did not complete in time");
        }
    }

    // Close database
    if (_database != null)
    {
        await _database.CloseAsync(cancellationToken);
    }

    _logger?.LogInformation("Shutdown complete");
}
```

### DisposeAsync - Final Cleanup

Called after shutdown to free resources.

**Example:**

```csharp
public override async ValueTask DisposeAsync()
{
    _cancellationTokenSource?.Dispose();

    if (_database != null)
    {
        await _database.DisposeAsync();
    }

    await base.DisposeAsync();
}
```

### Module Context

The `ModuleContext` provides runtime information:

**File**: `/src/Visora.Contracts/Modules/ModuleContext.cs`

```csharp
public sealed class ModuleContext
{
    // Your module's descriptor
    public ModuleDescriptor Descriptor { get; }

    // Optional DI container
    public IServiceProvider? Services { get; }

    // Capability provider (host APIs)
    public ICapabilityProvider Capabilities { get; }

    // Host-provided metadata (paths, settings, etc.)
    public IReadOnlyDictionary<string, object?> Properties { get; }
}
```

**Accessing context properties:**

```csharp
public override ValueTask InitializeAsync(
    ModuleContext context,
    CancellationToken cancellationToken = default)
{
    // Get module's installation path
    var modulePath = context.Properties["ModulePath"] as string;

    // Get host version
    var hostVersion = context.Properties["HostVersion"] as Version;

    // Access capabilities
    var logger = context.Capabilities.GetOptional<ILogger>();
    var fileSystem = context.Capabilities.GetRequired<IFileSystem>();

    return ValueTask.CompletedTask;
}
```

---

## Testing Your Module

### Unit Testing Setup

Create a test project:

```bash
dotnet new xunit -n MyFeature.Module.Tests
cd MyFeature.Module.Tests
dotnet add reference ../MyFeature.Module/MyFeature.Module.csproj
dotnet add package Moq
```

### Test Module Instantiation

```csharp
using Xunit;
using MyFeature.Module;

public class MyFeatureModuleTests
{
    [Fact]
    public void Module_HasValidDescriptor()
    {
        // Arrange & Act
        var module = new MyFeatureModule();

        // Assert
        Assert.NotNull(module.Descriptor);
        Assert.Equal("mycompany.myfeature", module.Descriptor.Id);
        Assert.Equal("My Feature Module", module.Descriptor.Name);
        Assert.Equal(new Version(0, 1, 0), module.Descriptor.Version);
    }
}
```

### Test Component Discovery

```csharp
using Moq;
using Visora.Contracts;
using Visora.Contracts.Modules;

[Fact]
public void DiscoverComponents_ReturnsExpectedTypes()
{
    // Arrange
    var module = new MyFeatureModule();
    var mockCapabilities = new Mock<ICapabilityProvider>();

    var context = new ModuleDiscoveryContext(
        moduleAssembly: typeof(MyFeatureModule).Assembly,
        capabilities: mockCapabilities.Object);

    // Act
    var components = module.DiscoverComponents(context).ToList();

    // Assert
    Assert.NotEmpty(components);
    Assert.Contains(components, t => t.Name == "MyFeatureComponent");
}
```

### Test Initialization

```csharp
[Fact]
public async Task InitializeAsync_CompletesSuccessfully()
{
    // Arrange
    var module = new MyFeatureModule();
    var mockCapabilities = new Mock<ICapabilityProvider>();

    var context = new ModuleContext(
        descriptor: module.Descriptor,
        services: null,
        capabilities: mockCapabilities.Object,
        properties: new Dictionary<string, object?>());

    // Act
    await module.InitializeAsync(context);

    // Assert - no exceptions thrown
    Assert.True(true);
}
```

### Integration Testing with ModuleCatalog

```csharp
[Fact]
public async Task Module_CanBeLoadedByCatalog()
{
    // Arrange
    var catalog = new ModuleCatalog();
    var options = new ModuleCatalogOptions
    {
        ModuleSearchPath = GetBuildOutputPath()
    };

    // Act
    await catalog.DiscoverAsync(options);
    var module = catalog.GetById("mycompany.myfeature");

    // Assert
    Assert.NotNull(module);
    Assert.Equal("My Feature Module", module.Descriptor.Name);
}

private string GetBuildOutputPath()
{
    var assembly = typeof(MyFeatureModule).Assembly;
    return Path.GetDirectoryName(assembly.Location)!;
}
```

---

## Deployment

### Deployment Checklist

- [ ] Module builds successfully
- [ ] Assembly name ends with `.vixm.dll`
- [ ] All dependencies are included or referenced
- [ ] Module has valid descriptor
- [ ] Module has at least one component
- [ ] Tests pass

### Deployment Locations

VISORA searches for modules in:

1. **ModuleSearchPath**: Configured directory
2. **./modules**: Relative to host executable
3. **~/.visora/modules**: User profile directory

### Copy Module to Deployment Location

```bash
# Option 1: Copy to modules folder
cp bin/Debug/net9.0/MyFeature.vixm.dll /path/to/visora/modules/

# Option 2: Use post-build event in .csproj
<Target Name="PostBuild" AfterTargets="PostBuildEvent">
  <Exec Command="xcopy /Y $(TargetPath) C:\Visora\modules\" />
</Target>
```

### Publishing for Distribution

**Create a NuGet package:**

```xml
<!-- Add to .csproj -->
<PropertyGroup>
  <GeneratePackageOnBuild>true</GeneratePackageOnBuild>
  <PackageId>MyCompany.MyFeature.VisoraModule</PackageId>
  <Title>My Feature Module for VISORA</Title>
  <Description>Provides custom functionality...</Description>
  <PackageTags>visora;module;extension</PackageTags>
</PropertyGroup>
```

```bash
dotnet pack -c Release
```

---

## Common Mistakes

### Mistake 1: Wrong Assembly Name

**Problem:**
```xml
<AssemblyName>MyFeature</AssemblyName>  ❌
```

**Output:** `MyFeature.dll` (not discoverable)

**Solution:**
```xml
<AssemblyName>MyFeature.vixm</AssemblyName>  ✅
```

**Output:** `MyFeature.vixm.dll` (discoverable)

### Mistake 2: Non-Public Module Class

**Problem:**
```csharp
internal class MyFeatureModule : Module  ❌
```

**Solution:**
```csharp
public sealed class MyFeatureModule : Module  ✅
```

Modules must be `public` for reflection to find them.

### Mistake 3: Missing Parameterless Constructor

**Problem:**
```csharp
public class MyFeatureModule : Module
{
    public MyFeatureModule(ILogger logger)  ❌
    {
        // Reflection can't instantiate this
    }
}
```

**Solution:**
```csharp
public class MyFeatureModule : Module
{
    public MyFeatureModule()  ✅
    {
        // Parameterless constructor required
    }
}
```

Get dependencies via `ModuleContext.Capabilities` instead.

### Mistake 4: Invalid Module ID

**Problem:**
```csharp
id: "MyFeature"  ❌  // Not hierarchical
id: "My Feature" ❌  // Contains space
id: "my-feature" ❌  // Contains hyphen
```

**Solution:**
```csharp
id: "mycompany.myfeature"  ✅  // Proper dot-notation
```

### Mistake 5: Blocking InitializeAsync

**Problem:**
```csharp
public override ValueTask InitializeAsync(ModuleContext context, ...)
{
    Thread.Sleep(5000);  ❌  // Blocking synchronous call
    return ValueTask.CompletedTask;
}
```

**Solution:**
```csharp
public override async ValueTask InitializeAsync(ModuleContext context, ...)
{
    await Task.Delay(5000, cancellationToken);  ✅  // Proper async
}
```

### Mistake 6: Not Handling Cancellation

**Problem:**
```csharp
public override async ValueTask InitializeAsync(
    ModuleContext context,
    CancellationToken cancellationToken = default)
{
    await LongRunningOperationAsync();  ❌  // Ignores cancellation
}
```

**Solution:**
```csharp
public override async ValueTask InitializeAsync(
    ModuleContext context,
    CancellationToken cancellationToken = default)
{
    await LongRunningOperationAsync(cancellationToken);  ✅
}
```

---

## Advanced Topics

### Topic 1: Module Dependencies

Modules can depend on other modules:

```csharp
public override async ValueTask InitializeAsync(
    ModuleContext context,
    CancellationToken cancellationToken = default)
{
    // Get required module capability
    var gitModule = context.Capabilities.GetRequired<IGitModule>();

    // Use the dependency
    var repos = await gitModule.GetRepositoriesAsync(cancellationToken);
}
```

### Topic 2: Background Services

Run background tasks in your module:

```csharp
private CancellationTokenSource? _cts;
private Task? _backgroundTask;

public override ValueTask InitializeAsync(
    ModuleContext context,
    CancellationToken cancellationToken = default)
{
    _cts = new CancellationTokenSource();
    _backgroundTask = Task.Run(() => BackgroundWork(_cts.Token), _cts.Token);
    return ValueTask.CompletedTask;
}

private async Task BackgroundWork(CancellationToken cancellationToken)
{
    while (!cancellationToken.IsCancellationRequested)
    {
        // Do periodic work
        await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
    }
}

public override async ValueTask ShutdownAsync(
    ModuleContext context,
    CancellationToken cancellationToken = default)
{
    _cts?.Cancel();

    if (_backgroundTask != null)
    {
        await _backgroundTask;
    }
}
```

### Topic 3: Module Configuration

Load module-specific configuration:

```csharp
private ModuleConfiguration? _config;

public override async ValueTask InitializeAsync(
    ModuleContext context,
    CancellationToken cancellationToken = default)
{
    var modulePath = context.Properties["ModulePath"] as string;
    var configPath = Path.Combine(modulePath!, "config.json");

    if (File.Exists(configPath))
    {
        var json = await File.ReadAllTextAsync(configPath, cancellationToken);
        _config = JsonSerializer.Deserialize<ModuleConfiguration>(json);
    }
}
```

---

## Best Practices

### 1. Use Sealed Classes

```csharp
public sealed class MyFeatureModule : Module  ✅
```

Prevents further inheritance and enables compiler optimizations.

### 2. Static Descriptors

```csharp
private static readonly ModuleDescriptor ModuleInfo = ...;  ✅
```

Create descriptor once as a static field, not in a property getter.

### 3. Async All The Way

```csharp
// Good
public override async ValueTask InitializeAsync(...)
{
    await LoadDataAsync(cancellationToken);
}

// Bad
public override ValueTask InitializeAsync(...)
{
    LoadDataAsync(cancellationToken).GetAwaiter().GetResult();  ❌
    return ValueTask.CompletedTask;
}
```

### 4. Respect Cancellation Tokens

```csharp
public override async ValueTask InitializeAsync(
    ModuleContext context,
    CancellationToken cancellationToken = default)
{
    cancellationToken.ThrowIfCancellationRequested();

    await Step1Async(cancellationToken);
    await Step2Async(cancellationToken);
    await Step3Async(cancellationToken);
}
```

### 5. Defensive Capability Access

```csharp
// Use GetOptional for optional capabilities
var logger = context.Capabilities.GetOptional<ILogger>();
logger?.LogInformation("Starting...");

// Use GetRequired for required capabilities
var fileSystem = context.Capabilities.GetRequired<IFileSystem>();
```

### 6. Proper Exception Handling

```csharp
public override async ValueTask InitializeAsync(
    ModuleContext context,
    CancellationToken cancellationToken = default)
{
    try
    {
        await InitializeCoreServicesAsync(cancellationToken);
    }
    catch (Exception ex)
    {
        var logger = context.Capabilities.GetOptional<ILogger>();
        logger?.LogError(ex, "Failed to initialize module");
        throw;  // Re-throw to signal failure
    }
}
```

---

## Reference Examples

### Complete Module Example

**File**: `src/Visora.Shell.Commands.Core/ShellCommandsModule.cs`

```csharp
using System;
using System.Collections.Generic;
using Visora.Core;
using Visora.Contracts.Modules;

namespace Visora.Shell.Commands.Core;

public sealed class ShellCommandsModule : Module
{
    private static readonly ModuleDescriptor ModuleInfo = ModuleDescriptor.Create(
        id: "visora.shell.commands.core",
        name: "Visora Shell Commands",
        version: new Version(0, 1, 0),
        description: "Baseline commands for diagnostics and exploration.",
        runtimeHints: ModuleRuntimeHints.Create(
            executableRelativePath: null,
            exposeExecutableGlobally: false,
            aliases: new[] { "vscc" }));

    public override ModuleDescriptor Descriptor => ModuleInfo;

    public override IEnumerable<Type> DiscoverComponents(ModuleDiscoveryContext context)
        => base.DiscoverComponents(context);
}
```

---

## Cross-References

### Related Documentation

- **[Creating Components](./creating-components.md)**: Next step after creating a module
- **[Creating Commands](./creating-commands.md)**: Adding executable commands
- **[Testing Strategies](./testing-strategies.md)**: Comprehensive testing guide
- **[Module Lifecycle Pattern](../patterns/module-lifecycle/visora-analysis.md)**: Deep dive into lifecycle
- **[Plugin Architecture Pattern](../patterns/plugin-architecture/visora-analysis.md)**: Understanding module loading
- **[Reflection Discovery Pattern](../patterns/reflection-discovery/visora-analysis.md)**: How discovery works
- **[Naming Conventions](../conventions/naming-conventions.md)**: Module naming rules

### Related Decisions

- **[ADR-001: Reflection Over Manifests](../decisions/reflection-over-manifests.md)**: Why no manifest files
- **[ADR-003: Async Everywhere](../decisions/async-everywhere.md)**: Why all lifecycle methods are async
- **[ADR-004: Unloadable Plugins](../decisions/unloadable-plugins.md)**: Module unloading mechanism

---

**End of Document**
