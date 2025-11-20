# Layered Architecture Pattern - VISORA Deep Dive

**Last Updated:** 2025-11-10
**VISORA Version:** .NET 9.0
**Pattern Tier:** Tier 4 (Structural)
**Related Patterns:** Plugin Architecture, Dependency Injection, Registry Pattern

---

## Table of Contents

1. [Pattern Overview](#pattern-overview)
2. [Why VISORA Uses Layered Architecture](#why-visora-uses-layered-architecture)
3. [VISORA Implementation](#visora-implementation)
4. [Code Examples](#code-examples)
5. [File References](#file-references)
6. [Layer Responsibilities](#layer-responsibilities)
7. [Dependency Direction and Inversion](#dependency-direction-and-inversion)
8. [Cross-Cutting Concerns](#cross-cutting-concerns)
9. [Testing Strategies Per Layer](#testing-strategies-per-layer)
10. [Tradeoffs](#tradeoffs)
11. [Alternatives Considered](#alternatives-considered)
12. [Best Practices](#best-practices)
13. [Advanced Topics](#advanced-topics)

---

## Pattern Overview

### What is Layered Architecture?

**Definition:** A layered architecture organizes code into horizontal layers where each layer has a specific responsibility and depends only on layers below it. In VISORA, this creates a three-tier structure with clear separation of concerns and dependency inversion.

**Key Characteristics:**
- **Clear Separation:** Each layer has distinct responsibilities
- **Dependency Inversion:** Higher layers depend on abstractions, not implementations
- **Unidirectional Dependencies:** Dependencies flow in one direction
- **Substitutability:** Implementations can be swapped without affecting dependents
- **Testability:** Each layer can be tested independently

### VISORA's Three-Tier Architecture

```
┌─────────────────────────────────────────────────────────┐
│                   LAYER 3: HOSTS                        │
│  ┌─────────────┐  ┌──────────────┐  ┌────────────────┐ │
│  │ Visora.CLI  │  │Visora.Terminal│  │ Visora.Shell   │ │
│  │  (Console)  │  │   (WPF UI)    │  │  (Foundation)  │ │
│  └─────────────┘  └──────────────┘  └────────────────┘ │
│       │                  │                   │          │
│       └──────────────────┼───────────────────┘          │
│                          ↓                               │
└─────────────────────────────────────────────────────────┘
                           │
                  Depends on abstractions
                           │
                           ↓
┌─────────────────────────────────────────────────────────┐
│              LAYER 2: CORE INFRASTRUCTURE               │
│  ┌─────────────────────────────────────────────────┐   │
│  │              Visora.Core                         │   │
│  │  • ModuleCatalog (registry)                      │   │
│  │  • ModuleHandle (lifecycle)                      │   │
│  │  • ModuleLocator (discovery)                     │   │
│  │  • CapabilityProviders (DI implementation)       │   │
│  │  • Module, Component (concrete base classes)     │   │
│  └─────────────────────────────────────────────────┘   │
│       │                                                  │
│       └─────────────────────┐                           │
│                             ↓                            │
└─────────────────────────────────────────────────────────┘
                              │
                   Depends on contracts only
                              │
                              ↓
┌─────────────────────────────────────────────────────────┐
│          LAYER 1: CONTRACTS (ABSTRACTIONS)              │
│  ┌─────────────────────────────────────────────────┐   │
│  │           Visora.Contracts                       │   │
│  │  • VisoraModule (abstract)                       │   │
│  │  • VisoraComponent (abstract)                    │   │
│  │  • VisoraCommand (abstract)                      │   │
│  │  • ICapabilityProvider (interface)               │   │
│  │  • Descriptor types (records)                    │   │
│  │  • Context objects (sealed classes)              │   │
│  └─────────────────────────────────────────────────┘   │
│                                                          │
│  NO DEPENDENCIES - Pure abstractions                    │
└─────────────────────────────────────────────────────────┘
```

**Critical Insight:** The Contracts layer has ZERO dependencies, enabling true dependency inversion.

---

## Why VISORA Uses Layered Architecture

### Design Goals

1. **Dependency Inversion**
   - Abstractions are owned by the contracts layer
   - Implementation details are isolated in Core
   - Hosts depend on stable contracts, not volatile implementations

2. **Multiple Host Support**
   - CLI, Terminal UI, and WPF can all share the same Core
   - Each host provides its own capabilities
   - Modules work across all hosts without modification

3. **Testability**
   - Each layer can be tested independently
   - Contracts define the "test seams"
   - Mock implementations can replace Core services

4. **Maintainability**
   - Clear boundaries reduce coupling
   - Changes to implementation don't affect contracts
   - Each layer has focused responsibilities

5. **Extensibility**
   - New hosts can be added without changing Core
   - New Core implementations can be swapped in
   - Modules depend only on stable contracts

### Key Decision Points

**Q: Why not a vertical slice architecture?**
**A:** VISORA needs to support multiple hosts (CLI, Terminal, WPF) that share infrastructure. Vertical slices would duplicate this shared logic.

**Q: Why not a flat namespace?**
**A:** Clear layers make dependencies explicit and prevent circular references.

**Q: Why separate Contracts from Core?**
**A:** To enable true dependency inversion - modules and hosts depend on abstractions, not implementations.

---

## VISORA Implementation

### Project Structure

```
Visora.sln
├── src/
│   ├── Visora.Contracts/          [Layer 1: Contracts]
│   │   ├── Modules/
│   │   ├── Components/
│   │   ├── Commands/
│   │   ├── Common/
│   │   └── Visora.Contracts.csproj
│   │
│   ├── Visora.Core/               [Layer 2: Core]
│   │   ├── Modules/
│   │   │   ├── ModuleCatalog.cs
│   │   │   ├── ModuleHandle.cs
│   │   │   ├── ModuleLocator.cs
│   │   │   └── ModuleCatalogOptions.cs
│   │   ├── Capabilities/
│   │   │   └── CapabilityProviders.cs
│   │   ├── Module.cs
│   │   ├── Component.cs
│   │   └── Visora.Core.csproj
│   │
│   ├── Visora.CLI/                [Layer 3: Host]
│   │   ├── Program.cs
│   │   └── Visora.CLI.csproj
│   │
│   ├── Visora.Terminal/           [Layer 3: Host]
│   │   ├── Program.cs
│   │   ├── MainWindow.xaml
│   │   └── Visora.Terminal.csproj
│   │
│   └── Visora.Shell/              [Layer 3: Host]
│       ├── (scaffolding)
│       └── Visora.Shell.csproj
```

### Namespace Organization

Each layer has its own root namespace:

```csharp
// Layer 1: Contracts
namespace Visora.Contracts.Modules;
namespace Visora.Contracts.Components;
namespace Visora.Contracts.Commands;
namespace Visora.Contracts.Common;

// Layer 2: Core
namespace Visora.Core.Modules;
namespace Visora.Core.Capabilities;
namespace Visora.Core;

// Layer 3: Hosts
namespace Visora.CLI;
namespace Visora.Terminal;
namespace Visora.Shell;
```

---

## Code Examples

### Layer 1: Contracts (No Dependencies)

**File:** `/src/Visora.Contracts/Visora.Contracts.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>Visora.Contracts</RootNamespace>
  </PropertyGroup>

  <!-- NO DEPENDENCIES - This is critical! -->
</Project>
```

**Abstract Base Class Example:**

```csharp
// File: /src/Visora.Contracts/Modules/VisoraModule.cs
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Visora.Contracts.Components;

namespace Visora.Contracts.Modules;

/// <summary>
/// Base type for Visora modules.
/// Defines lifecycle hooks that concrete modules can override.
/// </summary>
public abstract class VisoraModule : IAsyncDisposable
{
    /// <summary>
    /// Describes the module to hosts.
    /// Must be implemented by concrete modules.
    /// </summary>
    public abstract ModuleDescriptor Descriptor { get; }

    /// <summary>
    /// Called when the module is being initialized.
    /// Default implementation does nothing.
    /// </summary>
    public virtual ValueTask InitializeAsync(
        ModuleContext context,
        CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    /// <summary>
    /// Called before the module is unloaded.
    /// Default implementation does nothing.
    /// </summary>
    public virtual ValueTask ShutdownAsync(
        ModuleContext context,
        CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    /// <summary>
    /// Returns component types exposed by this module.
    /// Default implementation uses reflection to find all VisoraComponent types.
    /// </summary>
    public virtual IEnumerable<Type> DiscoverComponents(
        ModuleDiscoveryContext context)
        => context.EnumerateComponentCandidates()
            .Where(t => typeof(VisoraComponent).IsAssignableFrom(t));

    /// <summary>
    /// Cleanup resources.
    /// Default implementation does nothing.
    /// </summary>
    public virtual ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
```

**Interface Example:**

```csharp
// File: /src/Visora.Contracts/Common/ICapabilityProvider.cs
namespace Visora.Contracts.Common;

/// <summary>
/// Provides runtime capabilities to modules, components, and commands.
/// Acts as a service locator with type-safe lookups.
/// </summary>
public interface ICapabilityProvider
{
    /// <summary>
    /// Attempts to retrieve a capability of the specified type.
    /// </summary>
    /// <typeparam name="TCapability">The capability type to retrieve.</typeparam>
    /// <param name="capability">The retrieved capability, or null if not available.</param>
    /// <returns>True if the capability is available, false otherwise.</returns>
    bool TryGet<TCapability>(out TCapability? capability)
        where TCapability : class;
}

/// <summary>
/// Extension methods for ICapabilityProvider.
/// </summary>
public static class CapabilityProviderExtensions
{
    /// <summary>
    /// Gets a capability or returns null if not available.
    /// </summary>
    public static TCapability? GetOptional<TCapability>(
        this ICapabilityProvider provider)
        where TCapability : class
        => provider.TryGet(out TCapability? capability) ? capability : null;

    /// <summary>
    /// Gets a capability or throws if not available.
    /// </summary>
    public static TCapability GetRequired<TCapability>(
        this ICapabilityProvider provider)
        where TCapability : class
        => provider.TryGet(out TCapability? capability)
            ? capability!
            : throw new InvalidOperationException(
                $"Required capability '{typeof(TCapability).FullName}' not available.");
}
```

**Descriptor Record Example:**

```csharp
// File: /src/Visora.Contracts/Modules/ModuleDescriptor.cs
namespace Visora.Contracts.Modules;

/// <summary>
/// Immutable descriptor for a module.
/// Uses record syntax for value equality and immutability.
/// </summary>
public sealed record ModuleDescriptor(
    string Id,
    string Name,
    Version Version,
    string? Description = null,
    IReadOnlyDictionary<string, string>? Tags = null,
    ModuleRuntimeHints? RuntimeHints = null)
{
    /// <summary>
    /// Factory method for creating descriptors.
    /// </summary>
    public static ModuleDescriptor Create(
        string id,
        string name,
        Version version,
        string? description = null,
        IReadOnlyDictionary<string, string>? tags = null,
        ModuleRuntimeHints? runtimeHints = null)
        => new(id, name, version, description, tags, runtimeHints);
}
```

### Layer 2: Core (Depends on Contracts Only)

**File:** `/src/Visora.Core/Visora.Core.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>Visora.Core</RootNamespace>
    <Platforms>x64</Platforms>
  </PropertyGroup>

  <ItemGroup>
    <!-- External dependency for plugin loading -->
    <PackageReference Include="McMaster.NETCore.Plugins" Version="2.0.0" />
  </ItemGroup>

  <ItemGroup>
    <!-- Layer dependency: Core depends on Contracts -->
    <ProjectReference Include="..\Visora.Shared\Visora.Shared.csproj" />
    <ProjectReference Include="..\Visora.Contracts\Visora.Contracts.csproj" />
  </ItemGroup>
</Project>
```

**Implementation Example: ModuleCatalog**

```csharp
// File: /src/Visora.Core/Modules/ModuleCatalog.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Visora.Core.Modules;

/// <summary>
/// Discovers and manages Visora modules.
/// Implements the Registry pattern for module management.
/// </summary>
public sealed class ModuleCatalog : IAsyncDisposable
{
    private readonly List<ModuleHandle> _modules = new();

    /// <summary>
    /// Gets the collection of loaded modules.
    /// </summary>
    public IReadOnlyList<ModuleHandle> Modules => _modules;

    /// <summary>
    /// Discovers modules based on the provided options.
    /// Maintains idempotence - won't load the same assembly twice.
    /// </summary>
    public async Task DiscoverAsync(
        ModuleCatalogOptions options,
        CancellationToken cancellationToken = default)
    {
        if (options is null) throw new ArgumentNullException(nameof(options));

        // Use ModuleLocator to find candidate assemblies
        foreach (var path in ModuleLocator.EnumerateCandidateFiles(options))
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Deduplication: skip if already loaded
            if (_modules.Any(m => string.Equals(
                m.AssemblyPath, path, StringComparison.OrdinalIgnoreCase)))
                continue;

            // Load and add the module
            var handle = await ModuleHandle.LoadAsync(
                path, options, cancellationToken).ConfigureAwait(false);
            _modules.Add(handle);
        }
    }

    /// <summary>
    /// Looks up a module by its ID.
    /// </summary>
    public ModuleHandle? GetById(string moduleId)
        => _modules.FirstOrDefault(m => string.Equals(
            m.Descriptor.Id, moduleId, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Disposes all loaded modules.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        foreach (var module in _modules)
        {
            await module.DisposeAsync().ConfigureAwait(false);
        }

        _modules.Clear();
    }
}
```

**Implementation Example: CapabilityProviders**

```csharp
// File: /src/Visora.Core/Capabilities/CapabilityProviders.cs
using System;
using System.Collections.Generic;
using Visora.Contracts.Common;

namespace Visora.Core.Capabilities;

/// <summary>
/// Factory for creating ICapabilityProvider implementations.
/// </summary>
public static class CapabilityProviders
{
    /// <summary>
    /// Gets an empty capability provider (all lookups fail).
    /// </summary>
    public static ICapabilityProvider Empty { get; } = new NullCapabilityProvider();

    /// <summary>
    /// Creates a builder for constructing capability providers.
    /// </summary>
    public static CapabilityProviderBuilder CreateBuilder()
        => new CapabilityProviderBuilder();

    /// <summary>
    /// Null object pattern: capability provider that provides nothing.
    /// </summary>
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

/// <summary>
/// Builder for constructing capability providers.
/// Uses the Builder pattern for fluent configuration.
/// </summary>
public sealed class CapabilityProviderBuilder
{
    private readonly Dictionary<Type, object> _registrations = new();

    /// <summary>
    /// Adds a capability to the provider.
    /// </summary>
    public CapabilityProviderBuilder Add<TCapability>(TCapability capability)
        where TCapability : class
    {
        if (capability is null)
            throw new ArgumentNullException(nameof(capability));

        _registrations[typeof(TCapability)] = capability;
        return this;
    }

    /// <summary>
    /// Builds the capability provider.
    /// </summary>
    public ICapabilityProvider Build()
    {
        if (_registrations.Count == 0)
            return CapabilityProviders.Empty;

        return new DictionaryCapabilityProvider(
            new Dictionary<Type, object>(_registrations));
    }

    /// <summary>
    /// Dictionary-based capability provider implementation.
    /// </summary>
    private sealed class DictionaryCapabilityProvider : ICapabilityProvider
    {
        private readonly IReadOnlyDictionary<Type, object> _registrations;

        public DictionaryCapabilityProvider(
            IReadOnlyDictionary<Type, object> registrations)
        {
            _registrations = registrations;
        }

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

### Layer 3: Hosts (Depend on Core and Contracts)

**File:** `/src/Visora.CLI/Visora.CLI.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>Visora.CLI</RootNamespace>
    <AssemblyName>visora</AssemblyName>
    <Platforms>x64</Platforms>
  </PropertyGroup>

  <ItemGroup>
    <!-- CLI-specific dependency -->
    <PackageReference Include="System.CommandLine" Version="2.0.0-rc" />
  </ItemGroup>

  <ItemGroup>
    <!-- Host depends on Core (which transitively includes Contracts) -->
    <ProjectReference Include="..\Visora.Core\Visora.Core.csproj" />
  </ItemGroup>
</Project>
```

**Host Example: CLI Program**

```csharp
// File: /src/Visora.CLI/Program.cs (simplified excerpt)
using System;
using System.CommandLine;
using System.Threading;
using System.Threading.Tasks;
using Visora.Core.Modules;
using Visora.Core.Capabilities;

namespace Visora.CLI;

internal class Program
{
    private static async Task<int> Main(string[] args)
    {
        var root = new RootCommand("Visora CLI host and passthrough runner.");
        root.Subcommands.Add(BuildModulesCommand());

        return await root.InvokeAsync(args);
    }

    private static Command BuildModulesCommand()
    {
        var modules = new Command("modules",
            "Discover, inspect, and deploy Visora modules.");

        modules.Subcommands.Add(BuildModulesListCommand());

        return modules;
    }

    private static Command BuildModulesListCommand()
    {
        var list = new Command("list", "Lists discovered modules.");

        list.SetAction(async (ParseResult parseResult, CancellationToken ct) =>
        {
            // Create catalog options (Layer 2 Core type)
            var options = new ModuleCatalogOptions
            {
                Capabilities = CapabilityProviders.Empty
            };
            options.ProbingPaths.Add(AppContext.BaseDirectory);
            options.ProbingPaths.Add(Path.Combine(
                AppContext.BaseDirectory, "modules"));

            // Use ModuleCatalog (Layer 2 Core type)
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

            // Display results
            if (catalog.Modules.Count == 0)
            {
                Console.WriteLine("No modules found.");
                return 0;
            }

            Console.WriteLine($"Found {catalog.Modules.Count} module(s):");
            foreach (var module in catalog.Modules)
            {
                // Access Layer 1 Contract types
                Console.WriteLine($"  {module.Descriptor.Id} v{module.Descriptor.Version}");
                Console.WriteLine($"    {module.Descriptor.Description}");
            }

            return 0;
        });

        return list;
    }
}
```

---

## File References

### Layer 1: Contracts

| File | Lines | Purpose |
|------|-------|---------|
| `/src/Visora.Contracts/Visora.Contracts.csproj` | 10 | Project file with NO dependencies |
| `/src/Visora.Contracts/Modules/VisoraModule.cs` | 41 | Abstract base class for modules |
| `/src/Visora.Contracts/Modules/ModuleDescriptor.cs` | ~30 | Immutable module metadata |
| `/src/Visora.Contracts/Modules/ModuleContext.cs` | ~30 | Context passed to module lifecycle methods |
| `/src/Visora.Contracts/Components/VisoraComponent.cs` | 45 | Abstract base class for components |
| `/src/Visora.Contracts/Components/ComponentDescriptor.cs` | ~30 | Immutable component metadata |
| `/src/Visora.Contracts/Commands/VisoraCommand.cs` | ~30 | Abstract base class for commands |
| `/src/Visora.Contracts/Commands/CommandDescriptor.cs` | ~50 | Immutable command metadata |
| `/src/Visora.Contracts/Commands/CommandContext.cs` | ~40 | Context passed to command execution |
| `/src/Visora.Contracts/Commands/CommandResult.cs` | ~40 | Immutable result of command execution |
| `/src/Visora.Contracts/Common/ICapabilityProvider.cs` | ~30 | Capability lookup interface |

### Layer 2: Core

| File | Lines | Purpose |
|------|-------|---------|
| `/src/Visora.Core/Visora.Core.csproj` | 16 | Project file depending on Contracts |
| `/src/Visora.Core/Modules/ModuleCatalog.cs` | 46 | Module registry implementation |
| `/src/Visora.Core/Modules/ModuleHandle.cs` | ~160 | Loaded module wrapper with lifecycle |
| `/src/Visora.Core/Modules/ModuleLocator.cs` | ~80 | File system scanning for modules |
| `/src/Visora.Core/Modules/ModuleCatalogOptions.cs` | ~50 | Configuration for module discovery |
| `/src/Visora.Core/Capabilities/CapabilityProviders.cs` | ~80 | ICapabilityProvider implementations |
| `/src/Visora.Core/Module.cs` | ~30 | Concrete module base class |
| `/src/Visora.Core/Component.cs` | ~30 | Concrete component base class |

### Layer 3: Hosts

| File | Lines | Purpose |
|------|-------|---------|
| `/src/Visora.CLI/Visora.CLI.csproj` | ~20 | CLI host project |
| `/src/Visora.CLI/Program.cs` | ~450 | CLI application entry point |
| `/src/Visora.Terminal/Visora.Terminal.csproj` | ~30 | Terminal UI host project |
| `/src/Visora.Terminal/Program.cs` | ~100 | WPF terminal entry point |
| `/src/Visora.Shell/Visora.Shell.csproj` | ~15 | Shell host project (scaffolding) |

---

## Layer Responsibilities

### Layer 1: Contracts (Abstractions Only)

**Responsibilities:**
- Define abstract base classes (VisoraModule, VisoraComponent, VisoraCommand)
- Define interfaces (ICapabilityProvider)
- Define immutable metadata types (Descriptors as records)
- Define context objects for passing state
- Provide extension methods for convenience

**What NOT to include:**
- ❌ Implementation details
- ❌ External dependencies (except .NET BCL)
- ❌ Infrastructure concerns (logging, caching, etc.)
- ❌ Host-specific types

**Key Principle:** Contracts should be stable and change infrequently. They are the "north star" that all other layers align to.

### Layer 2: Core (Infrastructure Implementation)

**Responsibilities:**
- Implement module discovery (ModuleLocator)
- Implement module registry (ModuleCatalog)
- Implement module lifecycle (ModuleHandle)
- Implement capability provider (CapabilityProviders)
- Provide concrete base classes (Module, Component)
- Handle plugin loading (using McMaster.NETCore.Plugins)

**What NOT to include:**
- ❌ UI-specific code
- ❌ Host-specific behavior
- ❌ Business logic for specific domains

**Key Principle:** Core provides reusable infrastructure that all hosts can leverage.

### Layer 3: Hosts (Application Entry Points)

**Responsibilities:**
- Provide application entry point (Main method)
- Configure ModuleCatalogOptions
- Create CapabilityProvider with host-specific capabilities
- Initialize ModuleCatalog and discover modules
- Handle host-specific UI/CLI concerns
- Coordinate module lifecycle

**What NOT to include:**
- ❌ Low-level module loading logic
- ❌ Contract definitions

**Key Principle:** Hosts compose Core services to create specific user experiences.

---

## Dependency Direction and Inversion

### Dependency Flow

```
┌──────────────┐
│    Hosts     │  ← Application layer
│  (Layer 3)   │    Depends on everything below
└──────┬───────┘
       │ depends on
       ↓
┌──────────────┐
│     Core     │  ← Infrastructure layer
│  (Layer 2)   │    Depends on Contracts only
└──────┬───────┘
       │ depends on
       ↓
┌──────────────┐
│  Contracts   │  ← Abstraction layer
│  (Layer 1)   │    Depends on nothing
└──────────────┘
```

### Dependency Inversion Principle

**Traditional Layering Problem:**
```
┌────────────┐
│    CLI     │ ─┐
└────────────┘  │
                │
┌────────────┐  │
│  Terminal  │ ─┤ All depend on concrete Core
└────────────┘  │
                │
┌────────────┐  │
│   Shell    │ ─┘
└────────────┘
       │
       ↓
┌────────────┐
│    Core    │ ← Concrete implementations
└────────────┘
```

**Problem:** Changes to Core break all hosts.

**VISORA's Solution (Dependency Inversion):**
```
┌────────────┐     ┌────────────┐     ┌────────────┐
│    CLI     │────→│ Contracts  │←────│  Terminal  │
└────────────┘     │(Abstracts) │     └────────────┘
                   └─────▲──────┘
                         │
┌────────────┐           │
│   Shell    │───────────┘
└────────────┘
                         │
                   ┌─────┴──────┐
                   │    Core    │
                   │ (Implements│
                   │ Contracts) │
                   └────────────┘
```

**Benefits:**
- Core can be replaced without affecting hosts
- Hosts and Core both depend on stable abstractions
- Changes to Core implementation don't break hosts (as long as contracts remain stable)

### Example: ICapabilityProvider

**Contract (owned by Layer 1):**
```csharp
// /src/Visora.Contracts/Common/ICapabilityProvider.cs
public interface ICapabilityProvider
{
    bool TryGet<TCapability>(out TCapability? capability)
        where TCapability : class;
}
```

**Implementation (provided by Layer 2):**
```csharp
// /src/Visora.Core/Capabilities/CapabilityProviders.cs
public static class CapabilityProviders
{
    public static ICapabilityProvider Empty { get; }
    public static CapabilityProviderBuilder CreateBuilder() => new();
}
```

**Usage (by Layer 3):**
```csharp
// /src/Visora.CLI/Program.cs
var capabilities = CapabilityProviders.CreateBuilder()
    .Add<IConsoleHost>(consoleHost)
    .Build();

var options = new ModuleCatalogOptions
{
    Capabilities = capabilities  // Pass interface, not implementation
};
```

**Key Insight:** CLI depends on the interface, not the implementation. Core can change its implementation strategy without breaking CLI.

---

## Cross-Cutting Concerns

### How VISORA Handles Cross-Cutting Concerns

Cross-cutting concerns (logging, validation, error handling, etc.) can violate layer boundaries if not handled carefully. VISORA uses several strategies:

### 1. Capability Provider Pattern

Cross-cutting services are exposed as capabilities:

```csharp
// Define interface in Contracts
public interface ILogger
{
    void Log(string message);
}

// Implement in Core or Host
public class ConsoleLogger : ILogger
{
    public void Log(string message)
        => Console.WriteLine($"[LOG] {message}");
}

// Provide via capability provider
var capabilities = CapabilityProviders.CreateBuilder()
    .Add<ILogger>(new ConsoleLogger())
    .Build();

// Use in modules
public class MyModule : Module
{
    public override async ValueTask InitializeAsync(
        ModuleContext context,
        CancellationToken cancellationToken = default)
    {
        var logger = context.Capabilities.GetOptional<ILogger>();
        logger?.Log("Module initializing...");
    }
}
```

### 2. Extension Methods (in Contracts)

Utility methods that work on contract types:

```csharp
// File: /src/Visora.Contracts/Common/ICapabilityProvider.cs
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

### 3. Context Objects

Pass cross-cutting data through context objects:

```csharp
public sealed class CommandContext
{
    public VisoraModule Module { get; }
    public VisoraComponent? Component { get; }
    public CommandSurface Surface { get; }
    public ICapabilityProvider Capabilities { get; }  // ← Cross-cutting services
    public IReadOnlyDictionary<string, object?> Parameters { get; }
    public CancellationToken CancellationToken { get; }  // ← Cancellation support
}
```

### 4. Validation in Constructors

Defensive programming at layer boundaries:

```csharp
public ModuleContext(
    ModuleDescriptor descriptor,
    IServiceProvider? services,
    ICapabilityProvider capabilities,
    IReadOnlyDictionary<string, object?>? properties = null)
{
    // Validate at entry to layer
    Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
    Services = services;
    Capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
    Properties = properties ?? EmptyProperties;
}
```

---

## Testing Strategies Per Layer

### Layer 1: Contracts Testing

**Focus:** Verify contracts are well-defined and usable.

```csharp
[TestClass]
public class VisoraModuleTests
{
    [TestMethod]
    public void Descriptor_MustBeImplemented()
    {
        // Arrange
        var module = new TestModule();

        // Act
        var descriptor = module.Descriptor;

        // Assert
        Assert.IsNotNull(descriptor);
        Assert.IsFalse(string.IsNullOrEmpty(descriptor.Id));
    }

    [TestMethod]
    public async Task InitializeAsync_HasDefaultImplementation()
    {
        // Arrange
        var module = new MinimalModule();
        var context = CreateTestContext();

        // Act & Assert (should not throw)
        await module.InitializeAsync(context, CancellationToken.None);
    }

    private class TestModule : VisoraModule
    {
        public override ModuleDescriptor Descriptor
            => ModuleDescriptor.Create("test", "Test", new Version(1, 0, 0));
    }

    private class MinimalModule : VisoraModule
    {
        public override ModuleDescriptor Descriptor
            => ModuleDescriptor.Create("minimal", "Minimal", new Version(1, 0, 0));
    }
}
```

### Layer 2: Core Testing

**Focus:** Test infrastructure implementations with mocked contracts.

```csharp
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
        options.ProbingPaths.Add(GetTestModulesPath());

        var catalog = new ModuleCatalog();

        // Act
        await catalog.DiscoverAsync(options, CancellationToken.None);

        // Assert
        Assert.IsTrue(catalog.Modules.Count > 0);
    }

    [TestMethod]
    public async Task DiscoverAsync_DeduplicatesByPath()
    {
        // Arrange
        var options = new ModuleCatalogOptions
        {
            Capabilities = CapabilityProviders.Empty
        };
        var testPath = GetTestModulePath();
        options.ProbingPaths.Add(testPath);
        options.ProbingPaths.Add(testPath); // Same path twice

        var catalog = new ModuleCatalog();

        // Act
        await catalog.DiscoverAsync(options, CancellationToken.None);

        // Assert
        var count = catalog.Modules.Count;
        await catalog.DiscoverAsync(options, CancellationToken.None);
        Assert.AreEqual(count, catalog.Modules.Count,
            "Should not load duplicates");
    }

    [TestMethod]
    public void GetById_ReturnsModuleWhenExists()
    {
        // Arrange
        var catalog = new ModuleCatalog();
        // ... load a known module

        // Act
        var module = catalog.GetById("known.module.id");

        // Assert
        Assert.IsNotNull(module);
        Assert.AreEqual("known.module.id", module.Descriptor.Id);
    }
}
```

### Layer 3: Host Testing

**Focus:** Integration tests that verify hosts correctly use Core services.

```csharp
[TestClass]
public class CLIIntegrationTests
{
    [TestMethod]
    public async Task ModulesList_ProducesOutput()
    {
        // Arrange
        var output = new StringWriter();
        Console.SetOut(output);

        var args = new[] { "modules", "list" };

        // Act
        var exitCode = await Program.Main(args);

        // Assert
        Assert.AreEqual(0, exitCode);
        var outputText = output.ToString();
        Assert.IsTrue(outputText.Contains("module(s)"));
    }

    [TestMethod]
    public async Task ModulesInspect_ShowsComponentDetails()
    {
        // Arrange
        var output = new StringWriter();
        Console.SetOut(output);

        var args = new[] { "modules", "inspect", "visora.shell.commands.core" };

        // Act
        var exitCode = await Program.Main(args);

        // Assert
        Assert.AreEqual(0, exitCode);
        var outputText = output.ToString();
        Assert.IsTrue(outputText.Contains("Components:"));
    }
}
```

### Test Organization by Layer

```
Visora.Tests/
├── Contracts.Tests/        [Layer 1 Tests]
│   ├── Modules/
│   │   ├── VisoraModuleTests.cs
│   │   └── ModuleDescriptorTests.cs
│   ├── Components/
│   │   ├── VisoraComponentTests.cs
│   │   └── ComponentDescriptorTests.cs
│   └── Common/
│       └── ICapabilityProviderTests.cs
│
├── Core.Tests/             [Layer 2 Tests]
│   ├── Modules/
│   │   ├── ModuleCatalogTests.cs
│   │   ├── ModuleHandleTests.cs
│   │   └── ModuleLocatorTests.cs
│   └── Capabilities/
│       └── CapabilityProvidersTests.cs
│
└── CLI.Tests/              [Layer 3 Tests]
    ├── CommandLineTests.cs
    └── IntegrationTests.cs
```

---

## Tradeoffs

### Advantages

1. **Clear Separation of Concerns**
   - Each layer has a single, well-defined purpose
   - Easy to understand where code belongs
   - Reduces coupling between concerns

2. **Multiple Host Support**
   - CLI, Terminal, and WPF share the same Core
   - Consistent behavior across hosts
   - Modules work everywhere without modification

3. **Testability**
   - Each layer can be tested independently
   - Contracts provide test seams
   - Easy to mock dependencies

4. **Replaceability**
   - Core implementation can be swapped
   - Hosts can be added without changing Core
   - Modules don't care about implementation details

5. **Dependency Inversion**
   - Changes to implementation don't break dependents
   - Stable abstractions reduce breaking changes
   - Easier to maintain long-term

### Disadvantages

1. **Indirection**
   - More projects to navigate
   - Additional abstractions to understand
   - Slightly more complex than flat structure

2. **Performance**
   - Virtual method calls (mitigated by modern JIT)
   - Additional allocations for context objects
   - Generally negligible in practice

3. **Learning Curve**
   - Developers must understand layer boundaries
   - Must know which layer to modify
   - Requires discipline to maintain

4. **Vertical Concerns**
   - Some features span multiple layers
   - Must coordinate changes across layers
   - Can lead to "shotgun surgery" anti-pattern

---

## Alternatives Considered

### Alternative 1: Flat Structure (No Layers)

```
Visora/
├── Module.cs
├── Component.cs
├── Command.cs
├── ModuleCatalog.cs
└── CLI.cs
```

**Pros:**
- Simpler to start
- Less ceremony
- Direct dependencies

**Cons:**
- Tight coupling
- Hard to test
- Difficult to support multiple hosts
- Changes propagate everywhere

**Why Not Chosen:** VISORA needs to support multiple hosts (CLI, Terminal, WPF) with shared infrastructure. A flat structure would lead to circular dependencies and tight coupling.

### Alternative 2: Vertical Slice Architecture

```
Visora/
├── Modules/
│   ├── ModuleContracts.cs
│   ├── ModuleCore.cs
│   └── ModuleCLI.cs
├── Components/
│   ├── ComponentContracts.cs
│   ├── ComponentCore.cs
│   └── ComponentCLI.cs
└── Commands/
    ├── CommandContracts.cs
    ├── CommandCore.cs
    └── CommandCLI.cs
```

**Pros:**
- Feature-centric organization
- Easy to find related code
- Good for microservices

**Cons:**
- Duplicates shared infrastructure across slices
- Hard to share code between CLI, Terminal, WPF
- Module/Component/Command are hierarchical, not peers

**Why Not Chosen:** VISORA's domain model is hierarchical (Module → Component → Command), not flat slices.

### Alternative 3: Onion/Clean Architecture (More Layers)

```
Domain (innermost)
  ↓
Application
  ↓
Infrastructure
  ↓
Presentation (outermost)
```

**Pros:**
- Very strict separation
- Domain model completely isolated
- Good for complex business logic

**Cons:**
- More layers = more complexity
- VISORA's domain is simple (Module/Component/Command)
- Over-engineering for this use case

**Why Not Chosen:** VISORA's domain logic is straightforward and doesn't warrant the additional complexity of 4+ layers.

---

## Best Practices

### 1. Keep Contracts Stable

**DO:**
```csharp
// Add optional parameters with defaults
public sealed record ModuleDescriptor(
    string Id,
    string Name,
    Version Version,
    string? Description = null,  // ← Optional, has default
    IReadOnlyDictionary<string, string>? Tags = null);
```

**DON'T:**
```csharp
// Change required parameters (breaking change!)
public sealed record ModuleDescriptor(
    string Id,
    string Name,
    Version Version,
    string RequiredNewField);  // ← Breaking change!
```

### 2. Use Dependency Injection via Capabilities

**DO:**
```csharp
public override async ValueTask InitializeAsync(
    ModuleContext context,
    CancellationToken cancellationToken = default)
{
    // Use capability provider
    var logger = context.Capabilities.GetOptional<ILogger>();
    logger?.Log("Initializing...");
}
```

**DON'T:**
```csharp
public override async ValueTask InitializeAsync(
    ModuleContext context,
    CancellationToken cancellationToken = default)
{
    // Static dependency - can't test or replace
    Logger.Global.Log("Initializing...");
}
```

### 3. Validate at Layer Boundaries

**DO:**
```csharp
public ModuleCatalog(IModuleLoader loader)
{
    _loader = loader ?? throw new ArgumentNullException(nameof(loader));
}
```

**DON'T:**
```csharp
public ModuleCatalog(IModuleLoader loader)
{
    _loader = loader;  // Might be null!
}
```

### 4. Return Abstractions, Not Implementations

**DO:**
```csharp
public IReadOnlyList<ModuleHandle> Modules => _modules;
```

**DON'T:**
```csharp
public List<ModuleHandle> Modules => _modules;  // Exposes implementation!
```

### 5. Use Async Throughout

**DO:**
```csharp
public async Task DiscoverAsync(
    ModuleCatalogOptions options,
    CancellationToken cancellationToken = default)
{
    foreach (var path in ModuleLocator.EnumerateCandidateFiles(options))
    {
        cancellationToken.ThrowIfCancellationRequested();
        var handle = await ModuleHandle.LoadAsync(path, options, cancellationToken);
        _modules.Add(handle);
    }
}
```

**DON'T:**
```csharp
public void Discover(ModuleCatalogOptions options)
{
    // Blocking, no cancellation support
    foreach (var path in ModuleLocator.EnumerateCandidateFiles(options))
    {
        var handle = ModuleHandle.Load(path, options);
        _modules.Add(handle);
    }
}
```

### 6. Document Layer Boundaries

Include XML comments that explain which layer a type belongs to:

```csharp
/// <summary>
/// Base type for Visora modules (Layer 1: Contracts).
/// Implementations are typically provided in modules,
/// using Layer 2 (Core) services for infrastructure.
/// </summary>
public abstract class VisoraModule : IAsyncDisposable
{
    // ...
}
```

---

## Advanced Topics

### 1. Shared Types in Plugin Architecture

VISORA's plugin system uses shared types to enable communication across assembly load contexts:

```csharp
// File: /src/Visora.Core/Modules/ModuleCatalogOptions.cs
internal static class ModuleCatalogDefaults
{
    internal static readonly Type[] SharedTypes =
    {
        // All contract types must be shared
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

**Why This Matters:**
- Plugins are loaded in isolated AssemblyLoadContexts
- Without shared types, `VisoraModule` from the plugin wouldn't match `VisoraModule` from the host
- Shared types ensure type identity across boundaries

### 2. Capability Provider as Alternative to Service Locator

**Service Locator (Anti-Pattern):**
```csharp
// Global static state - hard to test
public static class ServiceLocator
{
    public static ILogger Logger { get; set; }
}

// Usage
ServiceLocator.Logger.Log("message");
```

**Capability Provider (VISORA Pattern):**
```csharp
// Passed explicitly via context
public interface ICapabilityProvider
{
    bool TryGet<TCapability>(out TCapability? capability)
        where TCapability : class;
}

// Usage
var logger = context.Capabilities.GetOptional<ILogger>();
logger?.Log("message");
```

**Key Differences:**
- No global state
- Testable (pass mock provider)
- Explicit (context shows capabilities are available)
- Scoped (different contexts can have different capabilities)

### 3. Context Objects as Capability Carriers

Context objects serve multiple purposes:
1. **Carry State:** Descriptor, parent references
2. **Provide Services:** Capabilities, service provider
3. **Enable Cancellation:** CancellationToken
4. **Pass Parameters:** Dictionaries for flexible data

```csharp
public sealed class CommandContext
{
    // State
    public VisoraModule Module { get; }
    public VisoraComponent? Component { get; }
    public CommandSurface Surface { get; }

    // Services
    public ICapabilityProvider Capabilities { get; }

    // Parameters
    public IReadOnlyDictionary<string, object?> Parameters { get; }

    // Cancellation
    public CancellationToken CancellationToken { get; }
}
```

### 4. Layer Evolution Strategy

**Adding New Capabilities:**

1. **Add to Contracts (Layer 1):**
```csharp
// New interface in Contracts
public interface INewCapability
{
    Task DoSomethingAsync();
}
```

2. **Implement in Core (Layer 2):**
```csharp
// Implementation in Core
public class DefaultNewCapability : INewCapability
{
    public Task DoSomethingAsync() => Task.CompletedTask;
}
```

3. **Provide in Host (Layer 3):**
```csharp
// Host configures capability
var capabilities = CapabilityProviders.CreateBuilder()
    .Add<INewCapability>(new DefaultNewCapability())
    .Build();
```

4. **Use in Modules:**
```csharp
// Modules access via context
var capability = context.Capabilities.GetOptional<INewCapability>();
await capability?.DoSomethingAsync();
```

**Key Insight:** New capabilities can be added without breaking existing code (as long as they're optional).

### 5. Cross-Layer Transactions

When operations span multiple layers, use the Unit of Work pattern:

```csharp
// Conceptual example (not implemented in VISORA)
public interface IUnitOfWork : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);
    Task RollbackAsync(CancellationToken cancellationToken);
}

// Host coordinates transaction
public async Task<CommandResult> ExecuteCommandWithTransactionAsync(
    VisoraCommand command,
    CommandContext context)
{
    var unitOfWork = context.Capabilities.GetOptional<IUnitOfWork>();
    if (unitOfWork is null)
    {
        // No transaction support, execute directly
        return await command.ExecuteAsync(context);
    }

    try
    {
        var result = await command.ExecuteAsync(context);
        if (result.Outcome == CommandOutcome.Success)
        {
            await unitOfWork.CommitAsync(context.CancellationToken);
        }
        else
        {
            await unitOfWork.RollbackAsync(context.CancellationToken);
        }
        return result;
    }
    catch
    {
        await unitOfWork.RollbackAsync(CancellationToken.None);
        throw;
    }
}
```

---

## Summary

VISORA's layered architecture provides:

1. **Clear Separation:** Three distinct layers with focused responsibilities
2. **Dependency Inversion:** Contracts layer has no dependencies, enabling stable abstractions
3. **Multiple Hosts:** CLI, Terminal, WPF share the same Core infrastructure
4. **Testability:** Each layer can be tested independently
5. **Extensibility:** New hosts and modules can be added without changing existing code

**Key Pattern:** Contracts → Core → Hosts, with dependencies flowing downward and depending on abstractions.

**Next Steps:**
- Review the Registry Pattern for how ModuleCatalog manages modules
- Review the Template Method pattern for how VisoraModule/Component define lifecycle hooks
- Review the Capability Negotiation pattern for cross-cutting concerns

---

**End of Document**
