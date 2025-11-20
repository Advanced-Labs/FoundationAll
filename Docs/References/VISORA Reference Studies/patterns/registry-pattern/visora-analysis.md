# Registry Pattern - VISORA Deep Dive

**Last Updated:** 2025-11-10
**VISORA Version:** .NET 9.0
**Pattern Tier:** Tier 4 (Structural)
**Related Patterns:** Plugin Architecture, Factory Pattern, Repository Pattern

---

## Table of Contents

1. [Pattern Overview](#pattern-overview)
2. [Why VISORA Uses Registry Pattern](#why-visora-uses-registry-pattern)
3. [VISORA Implementation](#visora-implementation)
4. [Code Examples](#code-examples)
5. [File References](#file-references)
6. [Registration and Deduplication](#registration-and-deduplication)
7. [Lookup Strategies](#lookup-strategies)
8. [Lifecycle Coordination](#lifecycle-coordination-through-registry)
9. [Testing Registries](#testing-registries)
10. [Tradeoffs](#tradeoffs)
11. [Alternatives Considered](#alternatives-considered)
12. [Best Practices](#best-practices)
13. [Advanced Topics](#advanced-topics)

---

## Pattern Overview

### What is Registry Pattern?

**Definition:** A registry is a centralized collection that maintains references to objects of interest (typically singletons or long-lived objects), providing a global point of access for lookup and lifecycle management.

**Key Characteristics:**
- **Centralized Storage:** Single collection of registered objects
- **Lookup by Key:** Find objects by identifier (ID, name, type, etc.)
- **Lifecycle Management:** Registry coordinates initialization, activation, and disposal
- **Deduplication:** Prevents duplicate registrations
- **Discovery Support:** Can enumerate all registered items

### Registry vs. Service Locator vs. DI Container

```
Registry Pattern (VISORA ModuleCatalog)
├── Stores long-lived objects (modules)
├── Lookup by domain identifier (module ID)
├── Manages object lifecycle (load, init, dispose)
└── Provides enumeration (list all modules)

Service Locator (Anti-pattern)
├── Global static access point
├── Lookup by type
├── Hides dependencies
└── Hard to test

DI Container (e.g., Microsoft.Extensions.DependencyInjection)
├── Automatic dependency resolution
├── Constructor injection
├── Scoped lifetimes (transient, scoped, singleton)
└── Builds object graphs
```

**VISORA's Choice:** Registry pattern for modules (known, enumerable objects) + Capability provider for services (dynamic, optional dependencies).

---

## Why VISORA Uses Registry Pattern

### Design Goals

1. **Centralized Module Management**
   - Single source of truth for loaded modules
   - Easy to enumerate all available modules
   - Coordinated lifecycle management

2. **Deduplication**
   - Prevent loading the same module multiple times
   - Avoid version conflicts
   - Maintain consistency

3. **Lookup by ID**
   - Modules have stable identifiers
   - Enable commands like `visora modules inspect <id>`
   - Support dependency resolution

4. **Discovery Integration**
   - Registry coordinates with ModuleLocator for discovery
   - Maintains discovered modules
   - Supports incremental discovery

5. **Resource Management**
   - Registry owns module handles
   - Ensures proper disposal
   - Prevents resource leaks

### Key Decision Points

**Q: Why not just use a List<ModuleHandle>?**
**A:** A registry adds deduplication, lookup, and lifecycle semantics on top of a simple collection.

**Q: Why not use a DI container?**
**A:** Modules are discovered dynamically, not configured at startup. A registry is more appropriate for runtime-discovered objects.

**Q: Why centralized instead of distributed?**
**A:** VISORA runs in a single process (currently). A centralized registry is simpler and more performant.

---

## VISORA Implementation

### ModuleCatalog as Registry

**Core Registry Type:** `ModuleCatalog` in `/src/Visora.Core/Modules/ModuleCatalog.cs`

```
┌──────────────────────────────────────────────────────┐
│             ModuleCatalog (Registry)                 │
│  ┌────────────────────────────────────────────────┐ │
│  │  List<ModuleHandle> _modules                   │ │
│  │  - Stores all loaded modules                   │ │
│  │  - Maintains insertion order                   │ │
│  │  - Provides deduplication                      │ │
│  └────────────────────────────────────────────────┘ │
│                                                      │
│  Operations:                                         │
│  • DiscoverAsync()  → Find and register modules     │
│  • GetById()        → Lookup by module ID           │
│  • Modules          → Enumerate all                 │
│  • DisposeAsync()   → Cleanup all modules           │
└──────────────────────────────────────────────────────┘
            │
            ↓ delegates to
┌──────────────────────────────────────────────────────┐
│          ModuleLocator (Discovery)                   │
│  • EnumerateCandidateFiles() → Find .vixm.dll files │
└──────────────────────────────────────────────────────┘
            │
            ↓ creates
┌──────────────────────────────────────────────────────┐
│          ModuleHandle (Wrapper)                      │
│  • LoadAsync() → Load plugin assembly                │
│  • Descriptor  → Module metadata                     │
│  • Module      → VisoraModule instance               │
└──────────────────────────────────────────────────────┘
```

### Key Components

**1. Storage:**
```csharp
private readonly List<ModuleHandle> _modules = new();
```

**2. Public Interface:**
```csharp
public IReadOnlyList<ModuleHandle> Modules => _modules;
```

**3. Registration (with deduplication):**
```csharp
public async Task DiscoverAsync(ModuleCatalogOptions options, ...)
{
    foreach (var path in ModuleLocator.EnumerateCandidateFiles(options))
    {
        // Deduplication check
        if (_modules.Any(m => string.Equals(m.AssemblyPath, path, ...)))
            continue;

        var handle = await ModuleHandle.LoadAsync(path, options, ...);
        _modules.Add(handle);
    }
}
```

**4. Lookup:**
```csharp
public ModuleHandle? GetById(string moduleId)
    => _modules.FirstOrDefault(m => string.Equals(m.Descriptor.Id, moduleId, ...));
```

**5. Cleanup:**
```csharp
public async ValueTask DisposeAsync()
{
    foreach (var module in _modules)
        await module.DisposeAsync();
    _modules.Clear();
}
```

---

## Code Examples

### Complete ModuleCatalog Implementation

**File:** `/src/Visora.Core/Modules/ModuleCatalog.cs`

```csharp
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
    // Internal storage (registry)
    private readonly List<ModuleHandle> _modules = new();

    /// <summary>
    /// Gets the collection of loaded modules.
    /// Returns a read-only view to prevent external modification.
    /// </summary>
    public IReadOnlyList<ModuleHandle> Modules => _modules;

    /// <summary>
    /// Discovers modules based on the provided options.
    /// Maintains idempotence - won't load the same assembly twice.
    /// </summary>
    /// <param name="options">Configuration for module discovery.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task DiscoverAsync(
        ModuleCatalogOptions options,
        CancellationToken cancellationToken = default)
    {
        // Validate arguments
        if (options is null)
            throw new ArgumentNullException(nameof(options));

        // Use ModuleLocator to enumerate candidate files
        foreach (var path in ModuleLocator.EnumerateCandidateFiles(options))
        {
            // Support cancellation
            cancellationToken.ThrowIfCancellationRequested();

            // Deduplication: skip if already loaded
            if (_modules.Any(m => string.Equals(
                m.AssemblyPath, path, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            // Load the module and add to registry
            var handle = await ModuleHandle.LoadAsync(
                path, options, cancellationToken).ConfigureAwait(false);

            _modules.Add(handle);
        }
    }

    /// <summary>
    /// Looks up a module by its unique identifier.
    /// </summary>
    /// <param name="moduleId">The module ID (e.g., "visora.shell.commands.core").</param>
    /// <returns>The module handle if found, otherwise null.</returns>
    public ModuleHandle? GetById(string moduleId)
        => _modules.FirstOrDefault(m => string.Equals(
            m.Descriptor.Id, moduleId, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Disposes all loaded modules and clears the registry.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        // Dispose each module
        foreach (var module in _modules)
        {
            await module.DisposeAsync().ConfigureAwait(false);
        }

        // Clear the registry
        _modules.Clear();
    }
}
```

### Usage Example: CLI Host

**File:** `/src/Visora.CLI/Program.cs` (excerpt)

```csharp
private static async Task<int> HandleModulesListAsync(
    ParseResult parseResult,
    CancellationToken ct)
{
    // Create catalog options
    var options = CreateCatalogOptions(parseResult);

    // Create the registry
    await using var catalog = new ModuleCatalog();

    try
    {
        // Discover and register modules
        await catalog.DiscoverAsync(options, ct).ConfigureAwait(false);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Discovery failed: {ex.Message}");
        return 1;
    }

    // Check if any modules were found
    if (catalog.Modules.Count == 0)
    {
        Console.WriteLine("No modules found.");
        return 0;
    }

    // Enumerate registered modules
    Console.WriteLine($"Found {catalog.Modules.Count} module(s):");
    Console.WriteLine();

    foreach (var handle in catalog.Modules)
    {
        var desc = handle.Descriptor;
        Console.WriteLine($"  {desc.Id} v{desc.Version}");
        Console.WriteLine($"    {desc.Description ?? "(no description)"}");
        Console.WriteLine($"    Path: {handle.AssemblyPath}");
        Console.WriteLine();
    }

    return 0;
}
```

### Usage Example: Lookup by ID

```csharp
private static async Task<int> HandleModulesInspectAsync(
    ParseResult parseResult,
    CancellationToken ct)
{
    var moduleId = parseResult.GetValue(ModuleIdArgument);
    var options = CreateCatalogOptions(parseResult);

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

    // Lookup by ID
    var handle = catalog.GetById(moduleId);
    if (handle is null)
    {
        Console.Error.WriteLine($"Module '{moduleId}' not found.");
        return 2;
    }

    // Display module details
    Console.WriteLine($"Module: {handle.Descriptor.Name} v{handle.Descriptor.Version}");
    Console.WriteLine($"ID: {handle.Descriptor.Id}");
    Console.WriteLine($"Description: {handle.Descriptor.Description}");
    Console.WriteLine($"Path: {handle.AssemblyPath}");

    // Inspect components
    await handle.EnsureInitializedAsync(ct);
    var inspection = await handle.InspectAsync(ct);

    Console.WriteLine($"\nComponents: {inspection.Components.Count}");
    foreach (var component in inspection.Components)
    {
        Console.WriteLine($"  • {component.Descriptor.Name}");
        Console.WriteLine($"    Commands: {component.Commands.Count}");
    }

    return 0;
}
```

---

## File References

### Core Registry Files

| File | Lines | Purpose |
|------|-------|---------|
| `/src/Visora.Core/Modules/ModuleCatalog.cs` | 46 | Registry implementation |
| `/src/Visora.Core/Modules/ModuleHandle.cs` | ~160 | Registered object wrapper |
| `/src/Visora.Core/Modules/ModuleLocator.cs` | ~80 | Discovery helper |
| `/src/Visora.Core/Modules/ModuleCatalogOptions.cs` | ~50 | Registry configuration |

### Usage Examples

| File | Lines | Purpose |
|------|-------|---------|
| `/src/Visora.CLI/Program.cs` | 450+ | CLI host using registry |
| `/src/Visora.Terminal/Program.cs` | 100+ | Terminal host using registry |

### Related Contract Types

| File | Lines | Purpose |
|------|-------|---------|
| `/src/Visora.Contracts/Modules/ModuleDescriptor.cs` | ~30 | Module metadata (registry key) |
| `/src/Visora.Contracts/Modules/VisoraModule.cs` | 41 | Module abstraction |

---

## Registration and Deduplication

### Deduplication by Path

**Problem:** Multiple discovery paths might find the same assembly.

**Solution:** Deduplicate by assembly path during registration.

```csharp
public async Task DiscoverAsync(
    ModuleCatalogOptions options,
    CancellationToken cancellationToken = default)
{
    foreach (var path in ModuleLocator.EnumerateCandidateFiles(options))
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Deduplication check: case-insensitive path comparison
        if (_modules.Any(m => string.Equals(
            m.AssemblyPath, path, StringComparison.OrdinalIgnoreCase)))
        {
            continue; // Already loaded, skip
        }

        var handle = await ModuleHandle.LoadAsync(path, options, cancellationToken);
        _modules.Add(handle);
    }
}
```

**Key Points:**
- Uses `StringComparison.OrdinalIgnoreCase` for cross-platform compatibility
- Checks **before** loading (avoids unnecessary work)
- Path is normalized by `ModuleLocator` (full path)

### Deduplication by Module ID

**Alternative Strategy:** Deduplicate by module ID instead of path.

```csharp
// Conceptual example (not current implementation)
public async Task DiscoverAsync(
    ModuleCatalogOptions options,
    CancellationToken cancellationToken = default)
{
    foreach (var path in ModuleLocator.EnumerateCandidateFiles(options))
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Load module to get descriptor
        var handle = await ModuleHandle.LoadAsync(path, options, cancellationToken);

        // Deduplication by module ID
        var existing = GetById(handle.Descriptor.Id);
        if (existing is not null)
        {
            // Version conflict strategy:
            // - Keep existing
            // - Or: replace with newer version
            // - Or: report error

            await handle.DisposeAsync(); // Cleanup
            continue;
        }

        _modules.Add(handle);
    }
}
```

**Tradeoffs:**
- **Pro:** Detects true duplicates (same module ID, different paths)
- **Con:** Must load assembly to get ID (more expensive)
- **Con:** Requires version conflict resolution strategy

**Current VISORA:** Uses path-based deduplication (simpler, faster).

### Idempotence

**Calling DiscoverAsync multiple times is idempotent:**

```csharp
var catalog = new ModuleCatalog();

// First call: discovers modules
await catalog.DiscoverAsync(options);
Console.WriteLine($"Count: {catalog.Modules.Count}"); // e.g., 3

// Second call: no duplicates added
await catalog.DiscoverAsync(options);
Console.WriteLine($"Count: {catalog.Modules.Count}"); // Still 3
```

**Why This Matters:**
- Allows incremental discovery (add new probing paths)
- Safe to call multiple times without side effects
- Supports dynamic module loading scenarios

---

## Lookup Strategies

### 1. Lookup by Module ID

**Implementation:**

```csharp
public ModuleHandle? GetById(string moduleId)
    => _modules.FirstOrDefault(m => string.Equals(
        m.Descriptor.Id, moduleId, StringComparison.OrdinalIgnoreCase));
```

**Usage:**

```csharp
var module = catalog.GetById("visora.shell.commands.core");
if (module is not null)
{
    Console.WriteLine($"Found: {module.Descriptor.Name}");
}
```

**Complexity:** O(n) - linear search
**Improvement:** Could use Dictionary<string, ModuleHandle> for O(1) lookup

### 2. Enumeration (All Modules)

**Implementation:**

```csharp
public IReadOnlyList<ModuleHandle> Modules => _modules;
```

**Usage:**

```csharp
foreach (var module in catalog.Modules)
{
    Console.WriteLine(module.Descriptor.Name);
}
```

**Complexity:** O(n) to enumerate all

### 3. Filtering by Criteria

**Example: Find all modules with a specific tag**

```csharp
public IEnumerable<ModuleHandle> GetByTag(string tagKey, string tagValue)
{
    return _modules.Where(m =>
        m.Descriptor.Tags?.TryGetValue(tagKey, out var value) == true &&
        string.Equals(value, tagValue, StringComparison.OrdinalIgnoreCase));
}
```

**Usage:**

```csharp
var coreModules = catalog.GetByTag("category", "core");
foreach (var module in coreModules)
{
    Console.WriteLine(module.Descriptor.Name);
}
```

### 4. Lookup by Path

**Example: Find module by assembly path**

```csharp
public ModuleHandle? GetByPath(string assemblyPath)
{
    var normalizedPath = Path.GetFullPath(assemblyPath);
    return _modules.FirstOrDefault(m => string.Equals(
        m.AssemblyPath, normalizedPath, StringComparison.OrdinalIgnoreCase));
}
```

### 5. Advanced: Predicate-Based Lookup

**Generic lookup method:**

```csharp
public IEnumerable<ModuleHandle> FindAll(Predicate<ModuleHandle> predicate)
{
    if (predicate is null)
        throw new ArgumentNullException(nameof(predicate));

    return _modules.Where(m => predicate(m));
}
```

**Usage:**

```csharp
// Find all modules with version >= 2.0
var recentModules = catalog.FindAll(m =>
    m.Descriptor.Version >= new Version(2, 0));
```

---

## Lifecycle Coordination Through Registry

### Phase 1: Discovery and Registration

```csharp
var catalog = new ModuleCatalog();

// Discover and register modules
await catalog.DiscoverAsync(options, cancellationToken);

// At this point:
// - Modules are loaded (assemblies in memory)
// - Descriptors are available
// - Modules are NOT initialized yet
```

### Phase 2: Initialization

```csharp
// Initialize all modules
foreach (var handle in catalog.Modules)
{
    await handle.EnsureInitializedAsync(cancellationToken);
}

// At this point:
// - Module.InitializeAsync() has been called
// - Modules can set up state, register services, etc.
```

### Phase 3: Active Use

```csharp
// Lookup specific module
var module = catalog.GetById("visora.shell.commands.core");
if (module is not null)
{
    // Inspect components and commands
    var inspection = await module.InspectAsync(cancellationToken);

    // Execute commands, etc.
}
```

### Phase 4: Shutdown and Disposal

```csharp
// Option 1: Explicit shutdown
foreach (var handle in catalog.Modules)
{
    await handle.ShutdownAsync(cancellationToken);
}

// Option 2: DisposeAsync (includes shutdown)
await catalog.DisposeAsync();

// At this point:
// - Module.ShutdownAsync() called for each
// - Module.DisposeAsync() called
// - Plugin loaders disposed
// - Registry cleared
```

### Example: Full Lifecycle

```csharp
public async Task<int> RunApplicationAsync(CancellationToken ct)
{
    var options = CreateCatalogOptions();

    // Create registry
    await using var catalog = new ModuleCatalog();

    // Phase 1: Discovery
    Console.WriteLine("Discovering modules...");
    await catalog.DiscoverAsync(options, ct);
    Console.WriteLine($"Found {catalog.Modules.Count} module(s).");

    // Phase 2: Initialization
    Console.WriteLine("Initializing modules...");
    foreach (var handle in catalog.Modules)
    {
        await handle.EnsureInitializedAsync(ct);
        Console.WriteLine($"  Initialized: {handle.Descriptor.Name}");
    }

    // Phase 3: Active use
    Console.WriteLine("Application running...");
    // ... use modules

    // Phase 4: Shutdown (automatic via 'await using')
    Console.WriteLine("Shutting down...");
    return 0;
}
```

---

## Testing Registries

### Unit Test: Basic Registration

```csharp
[TestClass]
public class ModuleCatalogTests
{
    [TestMethod]
    public async Task DiscoverAsync_RegistersModules()
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
        Assert.IsTrue(catalog.Modules.Count > 0, "Should discover at least one module");
    }

    private string GetTestModulesPath()
    {
        // Return path to test modules directory
        return Path.Combine(AppContext.BaseDirectory, "TestModules");
    }
}
```

### Unit Test: Deduplication

```csharp
[TestMethod]
public async Task DiscoverAsync_DeduplicatesByPath()
{
    // Arrange
    var options = new ModuleCatalogOptions
    {
        Capabilities = CapabilityProviders.Empty
    };

    var testPath = GetTestModulesPath();
    options.ProbingPaths.Add(testPath);
    options.ProbingPaths.Add(testPath); // Add same path twice

    var catalog = new ModuleCatalog();

    // Act
    await catalog.DiscoverAsync(options, CancellationToken.None);
    var initialCount = catalog.Modules.Count;

    // Call again
    await catalog.DiscoverAsync(options, CancellationToken.None);
    var finalCount = catalog.Modules.Count;

    // Assert
    Assert.AreEqual(initialCount, finalCount,
        "Should not register duplicates on subsequent calls");
}
```

### Unit Test: Lookup by ID

```csharp
[TestMethod]
public async Task GetById_ReturnsModuleWhenExists()
{
    // Arrange
    var options = new ModuleCatalogOptions
    {
        Capabilities = CapabilityProviders.Empty
    };
    options.ProbingPaths.Add(GetTestModulesPath());

    var catalog = new ModuleCatalog();
    await catalog.DiscoverAsync(options, CancellationToken.None);

    // Assume we know a test module ID
    var knownModuleId = "test.module";

    // Act
    var module = catalog.GetById(knownModuleId);

    // Assert
    Assert.IsNotNull(module, "Should find module by ID");
    Assert.AreEqual(knownModuleId, module.Descriptor.Id,
        StringComparison.OrdinalIgnoreCase);
}
```

### Unit Test: Lookup Returns Null When Not Found

```csharp
[TestMethod]
public void GetById_ReturnsNullWhenNotFound()
{
    // Arrange
    var catalog = new ModuleCatalog();
    // No modules loaded

    // Act
    var module = catalog.GetById("nonexistent.module");

    // Assert
    Assert.IsNull(module, "Should return null for nonexistent ID");
}
```

### Unit Test: Disposal Cleans Up

```csharp
[TestMethod]
public async Task DisposeAsync_ClearsRegistry()
{
    // Arrange
    var options = new ModuleCatalogOptions
    {
        Capabilities = CapabilityProviders.Empty
    };
    options.ProbingPaths.Add(GetTestModulesPath());

    var catalog = new ModuleCatalog();
    await catalog.DiscoverAsync(options, CancellationToken.None);

    var initialCount = catalog.Modules.Count;
    Assert.IsTrue(initialCount > 0, "Should have modules before disposal");

    // Act
    await catalog.DisposeAsync();

    // Assert
    Assert.AreEqual(0, catalog.Modules.Count,
        "Registry should be empty after disposal");
}
```

---

## Tradeoffs

### Advantages

1. **Centralized Management**
   - Single source of truth for modules
   - Easy to enumerate all loaded modules
   - Consistent lifecycle management

2. **Deduplication**
   - Prevents loading the same module multiple times
   - Avoids resource waste
   - Maintains consistency

3. **Lookup Efficiency**
   - Fast lookup by ID (O(n), could be O(1) with dictionary)
   - Supports various lookup strategies
   - Flexible querying

4. **Lifecycle Coordination**
   - Registry owns module handles
   - Ensures proper disposal
   - Prevents resource leaks

5. **Testability**
   - Easy to mock or stub
   - Clear interface (DiscoverAsync, GetById, etc.)
   - Supports unit testing

### Disadvantages

1. **Global State**
   - Registry is typically a singleton per application
   - Can make testing harder if not carefully designed
   - Multiple registries might be needed for isolation

2. **Linear Lookup**
   - GetById is O(n) with current list-based implementation
   - Could be optimized with dictionary
   - Not a concern for typical module counts (< 100)

3. **Memory Overhead**
   - Registry holds references to all modules
   - Modules remain in memory until registry is disposed
   - Could be mitigated with weak references or lazy loading

4. **Coupling**
   - Code depends on the registry to find modules
   - Changes to registry interface affect all consumers
   - Could be mitigated with abstractions (IModuleCatalog)

---

## Alternatives Considered

### Alternative 1: No Registry (Direct References)

```csharp
// Load modules directly
var module1 = await ModuleHandle.LoadAsync(path1, options, ct);
var module2 = await ModuleHandle.LoadAsync(path2, options, ct);

// Use directly
await module1.EnsureInitializedAsync(ct);
await module2.EnsureInitializedAsync(ct);
```

**Pros:**
- Simpler (no registry abstraction)
- Direct references

**Cons:**
- No deduplication
- No centralized lookup
- Hard to enumerate all modules
- Manual lifecycle management

**Why Not Chosen:** VISORA needs to discover modules dynamically and provide a unified view.

### Alternative 2: DI Container

```csharp
var services = new ServiceCollection();

// Register modules as services
services.AddSingleton<ModuleHandle>(provider =>
    ModuleHandle.LoadAsync(path, options, ct).Result);

var serviceProvider = services.BuildServiceProvider();

// Lookup
var module = serviceProvider.GetService<ModuleHandle>();
```

**Pros:**
- Automatic dependency injection
- Scoped lifetimes
- Familiar pattern

**Cons:**
- Modules are discovered at runtime, not configured at startup
- Lookup by type, not by module ID
- Overkill for simple registry needs

**Why Not Chosen:** Modules are dynamically discovered, not statically configured. Registry is more appropriate.

### Alternative 3: Dictionary-Based Registry

```csharp
public class ModuleCatalog
{
    private readonly Dictionary<string, ModuleHandle> _modulesByPath = new();
    private readonly Dictionary<string, ModuleHandle> _modulesById = new();

    public ModuleHandle? GetById(string id)
        => _modulesById.TryGetValue(id, out var module) ? module : null;

    public ModuleHandle? GetByPath(string path)
        => _modulesByPath.TryGetValue(path, out var module) ? module : null;
}
```

**Pros:**
- O(1) lookup by ID
- O(1) lookup by path
- Faster for large module counts

**Cons:**
- More complex (maintain two dictionaries)
- Slightly more memory overhead
- Current list-based approach is sufficient

**Why Not Chosen:** Current module counts are small (< 100), so O(n) is acceptable. Could be optimized later if needed.

---

## Best Practices

### 1. Use Read-Only Views

**DO:**
```csharp
public IReadOnlyList<ModuleHandle> Modules => _modules;
```

**DON'T:**
```csharp
public List<ModuleHandle> Modules => _modules; // Exposes internal state!
```

### 2. Validate Before Registration

**DO:**
```csharp
public async Task RegisterAsync(ModuleHandle handle)
{
    if (handle is null)
        throw new ArgumentNullException(nameof(handle));

    // Check for duplicates
    if (_modules.Any(m => m.Descriptor.Id == handle.Descriptor.Id))
        throw new InvalidOperationException($"Module '{handle.Descriptor.Id}' already registered.");

    _modules.Add(handle);
}
```

**DON'T:**
```csharp
public void Register(ModuleHandle handle)
{
    _modules.Add(handle); // No validation!
}
```

### 3. Support Cancellation

**DO:**
```csharp
public async Task DiscoverAsync(
    ModuleCatalogOptions options,
    CancellationToken cancellationToken = default)
{
    foreach (var path in ModuleLocator.EnumerateCandidateFiles(options))
    {
        cancellationToken.ThrowIfCancellationRequested(); // ← Check cancellation
        // ...
    }
}
```

### 4. Dispose Properly

**DO:**
```csharp
public async ValueTask DisposeAsync()
{
    foreach (var module in _modules)
    {
        await module.DisposeAsync().ConfigureAwait(false);
    }
    _modules.Clear();
}
```

**DON'T:**
```csharp
public void Dispose()
{
    _modules.Clear(); // Doesn't dispose modules!
}
```

### 5. Use Case-Insensitive Comparisons

**DO:**
```csharp
public ModuleHandle? GetById(string moduleId)
    => _modules.FirstOrDefault(m => string.Equals(
        m.Descriptor.Id, moduleId, StringComparison.OrdinalIgnoreCase));
```

**DON'T:**
```csharp
public ModuleHandle? GetById(string moduleId)
    => _modules.FirstOrDefault(m => m.Descriptor.Id == moduleId); // Case-sensitive!
```

---

## Advanced Topics

### 1. Lazy Registration

**Concept:** Register module metadata without loading the assembly immediately.

```csharp
public class LazyModuleHandle
{
    public string Path { get; }
    public ModuleDescriptor? Descriptor { get; private set; }
    private ModuleHandle? _handle;

    public async Task<ModuleHandle> EnsureLoadedAsync(
        ModuleCatalogOptions options,
        CancellationToken ct)
    {
        if (_handle is null)
        {
            _handle = await ModuleHandle.LoadAsync(Path, options, ct);
            Descriptor = _handle.Descriptor;
        }
        return _handle;
    }
}

public class LazyModuleCatalog
{
    private readonly List<LazyModuleHandle> _modules = new();

    public void RegisterPath(string path)
    {
        _modules.Add(new LazyModuleHandle { Path = path });
    }

    public async Task<ModuleHandle> GetByIdAsync(
        string id,
        ModuleCatalogOptions options,
        CancellationToken ct)
    {
        foreach (var lazy in _modules)
        {
            var handle = await lazy.EnsureLoadedAsync(options, ct);
            if (string.Equals(handle.Descriptor.Id, id, StringComparison.OrdinalIgnoreCase))
                return handle;
        }
        return null;
    }
}
```

**Benefits:**
- Faster startup (don't load all modules immediately)
- Reduced memory usage (load on demand)

**Tradeoffs:**
- More complex
- First access is slower

### 2. Hierarchical Registry

**Concept:** Support parent-child registries for scoping.

```csharp
public class HierarchicalModuleCatalog : ModuleCatalog
{
    private readonly ModuleCatalog? _parent;

    public HierarchicalModuleCatalog(ModuleCatalog? parent = null)
    {
        _parent = parent;
    }

    public override ModuleHandle? GetById(string moduleId)
    {
        // Check local registry first
        var local = base.GetById(moduleId);
        if (local is not null)
            return local;

        // Fall back to parent
        return _parent?.GetById(moduleId);
    }
}
```

**Use Case:** Different scopes (global modules, user modules, project modules).

### 3. Event Notifications

**Concept:** Notify listeners when modules are added or removed.

```csharp
public class ObservableModuleCatalog : ModuleCatalog
{
    public event EventHandler<ModuleRegisteredEventArgs>? ModuleRegistered;
    public event EventHandler<ModuleUnregisteredEventArgs>? ModuleUnregistered;

    public override async Task DiscoverAsync(
        ModuleCatalogOptions options,
        CancellationToken cancellationToken = default)
    {
        var initialCount = Modules.Count;
        await base.DiscoverAsync(options, cancellationToken);

        // Notify for new modules
        for (int i = initialCount; i < Modules.Count; i++)
        {
            var handle = Modules[i];
            ModuleRegistered?.Invoke(this, new ModuleRegisteredEventArgs(handle));
        }
    }

    public override async ValueTask DisposeAsync()
    {
        // Notify before disposal
        foreach (var module in Modules)
        {
            ModuleUnregistered?.Invoke(this, new ModuleUnregisteredEventArgs(module));
        }

        await base.DisposeAsync();
    }
}
```

### 4. Distributed Registry (Conceptual)

**Concept:** Registry that spans multiple machines.

```csharp
public interface IDistributedModuleCatalog
{
    Task DiscoverAsync(string machineId, ModuleCatalogOptions options, CancellationToken ct);
    Task<ModuleHandle?> GetByIdAsync(string moduleId, CancellationToken ct);
    Task<IReadOnlyList<ModuleHandle>> GetAllAsync(CancellationToken ct);
}

public class GrpcDistributedCatalog : IDistributedModuleCatalog
{
    private readonly Dictionary<string, ModuleCatalog> _catalogsByMachine = new();

    public async Task DiscoverAsync(
        string machineId,
        ModuleCatalogOptions options,
        CancellationToken ct)
    {
        // Discover modules on specific machine
        var catalog = new ModuleCatalog();
        await catalog.DiscoverAsync(options, ct);
        _catalogsByMachine[machineId] = catalog;
    }

    public async Task<ModuleHandle?> GetByIdAsync(
        string moduleId,
        CancellationToken ct)
    {
        // Search across all machines
        foreach (var catalog in _catalogsByMachine.Values)
        {
            var module = catalog.GetById(moduleId);
            if (module is not null)
                return module;
        }
        return null;
    }
}
```

---

## Summary

VISORA's ModuleCatalog implements the Registry pattern to:

1. **Centralize module management:** Single source of truth
2. **Deduplicate registrations:** Prevent loading the same module multiple times
3. **Enable lookup:** Find modules by ID or other criteria
4. **Coordinate lifecycle:** Initialize, use, and dispose modules consistently
5. **Support enumeration:** List all registered modules

**Key Implementation Details:**
- Internal List<ModuleHandle> storage
- Deduplication by assembly path
- Case-insensitive ID lookup
- Async disposal of all modules

**Next Steps:**
- Review the Template Method pattern for module lifecycle hooks
- Review the Plugin Architecture pattern for how modules are loaded
- Review the Capability Negotiation pattern for cross-cutting concerns

---

**End of Document**
