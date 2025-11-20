# Module Lifecycle Pattern - VISORA Deep Dive

**Last Updated:** November 10, 2025
**VISORA Version:** .NET 9.0
**Pattern Tier:** Tier 1 (Foundational)
**Related Patterns:** Plugin Architecture, Reflection Discovery, Resource Management

---

## Table of Contents

1. [Pattern Overview](#pattern-overview)
2. [Why Four-Phase Lifecycle?](#why-four-phase-lifecycle)
3. [VISORA Implementation](#visora-implementation)
4. [Code Examples](#code-examples)
5. [File References](#file-references)
6. [Phases in Detail](#phases-in-detail)
7. [Idempotence Guarantees](#idempotence-guarantees)
8. [Dependency Ordering](#dependency-ordering)
9. [Error Handling](#error-handling)
10. [Testing Patterns](#testing-patterns)
11. [Best Practices](#best-practices)
12. [Advanced Topics](#advanced-topics)

---

## Pattern Overview

### What is Module Lifecycle?

**Definition:** A structured four-phase pattern for managing module state transitions from initial discovery through final cleanup, ensuring proper resource initialization, controlled shutdown, and complete disposal.

**Key Characteristics:**
- **Four Distinct Phases:** Load → Initialize → Shutdown → Dispose
- **Idempotent Operations:** Safe to call multiple times
- **Async Throughout:** All lifecycle methods are async
- **Graceful Degradation:** Handles errors at each phase
- **Resource Management:** Explicit cleanup and disposal

### The Four Phases

```
┌─────────────────────────────────────────────────────────────┐
│  PHASE 1: LOAD                                              │
│  ┌────────────────────────────────────────────────────────┐ │
│  │  - Discover .vixm.dll files                            │ │
│  │  - Create PluginLoader (isolated AssemblyLoadContext)  │ │
│  │  - Load assembly                                       │ │
│  │  - Find VisoraModule implementation                    │ │
│  │  - Instantiate module (parameterless constructor)      │ │
│  │  - Create ModuleContext                                │ │
│  │  Result: ModuleHandle (not yet initialized)            │ │
│  └────────────────────────────────────────────────────────┘ │
│                           ↓                                  │
│  PHASE 2: INITIALIZE                                        │
│  ┌────────────────────────────────────────────────────────┐ │
│  │  - Call module.InitializeAsync(context, token)         │ │
│  │  - Module performs one-time setup                      │ │
│  │  - Negotiate capabilities with host                    │ │
│  │  - Initialize state, register services                │ │
│  │  - Set _initialized flag to true                       │ │
│  │  Result: Module ready for use                          │ │
│  └────────────────────────────────────────────────────────┘ │
│                           ↓                                  │
│              [Module is active and usable]                  │
│                           ↓                                  │
│  PHASE 3: SHUTDOWN                                          │
│  ┌────────────────────────────────────────────────────────┐ │
│  │  - Call module.ShutdownAsync(context, token)           │ │
│  │  - Module saves state, releases resources              │ │
│  │  - Graceful cleanup (can fail safely)                  │ │
│  │  - Set _initialized flag to false                      │ │
│  │  Result: Module inactive but not unloaded              │ │
│  └────────────────────────────────────────────────────────┘ │
│                           ↓                                  │
│  PHASE 4: DISPOSE                                           │
│  ┌────────────────────────────────────────────────────────┐ │
│  │  - Call module.DisposeAsync()                          │ │
│  │  - Final cleanup                                       │ │
│  │  - Dispose PluginLoader                                │ │
│  │  - Unload AssemblyLoadContext                          │ │
│  │  - GC can collect module assembly                      │ │
│  │  Result: Module completely unloaded                    │ │
│  └────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
```

---

## Why Four-Phase Lifecycle?

### Design Goals

1. **Separation of Concerns**
   - **Load:** Discovery and instantiation (lightweight, can fail fast)
   - **Initialize:** Setup and resource acquisition (heavy, can be deferred)
   - **Shutdown:** Graceful cleanup (reversible, can be retried)
   - **Dispose:** Final cleanup (irreversible, must always complete)

2. **Lazy Initialization**
   - Load modules without initializing them (fast catalog building)
   - Initialize only when needed (save resources)
   - Enables inspection without activation

3. **Hot-Swapping**
   - Shutdown → Dispose → Load → Initialize sequence
   - No host restart required
   - State can be preserved and restored

4. **Resource Management**
   - Clear ownership of resources at each phase
   - Explicit cleanup order
   - No resource leaks

5. **Error Resilience**
   - Initialization failure doesn't crash host
   - Shutdown failure is logged but doesn't prevent disposal
   - Dispose always completes (even if shutdown failed)

### Key Decision Points

**Why Not Two-Phase (Load/Unload)?**
```csharp
// ❌ REJECTED: Simple Load/Unload
// Problem: No separation between instantiation and initialization
// Problem: Can't inspect without full initialization
// Problem: Cleanup is all-or-nothing

public interface IModule
{
    void Load();    // Does everything
    void Unload();  // Undoes everything
}
```

**Why Not Three-Phase (Load/Initialize/Dispose)?**
```csharp
// ❌ REJECTED: No explicit Shutdown
// Problem: No graceful shutdown phase
// Problem: Can't prepare for hot-swap
// Problem: State save happens in Dispose (which should be fast)

public interface IModule
{
    void Load();
    void Initialize();
    void Dispose();  // Must handle both shutdown and disposal
}
```

**Why Four-Phase?**
```csharp
// ✅ ACCEPTED: Four distinct phases
// ✅ Separation of concerns
// ✅ Lazy initialization
// ✅ Graceful shutdown before disposal
// ✅ Hot-swap support

public interface IModule
{
    // Phase 1: Lightweight instantiation
    // (Happens in constructor - no explicit method)

    // Phase 2: Heavy initialization
    ValueTask InitializeAsync(ModuleContext context, CancellationToken ct);

    // Phase 3: Graceful shutdown (can save state)
    ValueTask ShutdownAsync(ModuleContext context, CancellationToken ct);

    // Phase 4: Final cleanup (must be fast and reliable)
    ValueTask DisposeAsync();
}
```

---

## VISORA Implementation

### ModuleHandle: Lifecycle Orchestrator

**File:** `/src/Visora.Core/Modules/ModuleHandle.cs`

```
ModuleHandle
  ├─ _loader: PluginLoader (assembly isolation)
  ├─ _context: ModuleContext (capability access)
  ├─ _initialized: bool (state tracking)
  │
  ├─ LoadAsync() → static factory (PHASE 1)
  │    Creates ModuleHandle in loaded but uninitialized state
  │
  ├─ EnsureInitializedAsync() → idempotent (PHASE 2)
  │    Calls InitializeAsync if not already initialized
  │
  ├─ ShutdownAsync() → graceful cleanup (PHASE 3)
  │    Calls ShutdownAsync if initialized
  │
  └─ DisposeAsync() → final cleanup (PHASE 4)
       Calls ShutdownAsync → DisposeAsync → loader.Dispose()
```

### State Machine

```
[Created] ──LoadAsync()──> [Loaded]
                              │
                              │ EnsureInitializedAsync()
                              ↓
                         [Initialized] ←──┐
                              │           │ (idempotent)
                              │           │ EnsureInitializedAsync()
                              │           │
                              ├───────────┘
                              │
                              │ ShutdownAsync()
                              ↓
                         [Shutdown] ←─────┐
                              │           │ (idempotent)
                              │           │ ShutdownAsync()
                              ├───────────┘
                              │
                              │ DisposeAsync()
                              ↓
                         [Disposed]
                              │
                              X (terminal state)
```

---

## Code Examples

### Example 1: Phase 1 - Load

**File:** `/src/Visora.Core/Modules/ModuleHandle.cs:51-83`

```csharp
/// <summary>
/// PHASE 1: LOAD - Creates module handle without initialization
/// </summary>
public static Task<ModuleHandle> LoadAsync(
    string assemblyPath,
    ModuleCatalogOptions options,
    CancellationToken cancellationToken)
{
    if (options is null) throw new ArgumentNullException(nameof(options));
    cancellationToken.ThrowIfCancellationRequested();

    // Create unloadable plugin loader
    var sharedTypes = options.GetSharedTypesArray();
    var loader = PluginLoader.CreateFromAssemblyFile(
        assemblyPath,
        sharedTypes: sharedTypes,
        isUnloadable: true);  // ← Critical for hot-swapping

    try
    {
        // Load assembly
        var assembly = loader.LoadDefaultAssembly();

        // Find module implementation via reflection
        var moduleType = assembly
            .GetTypes()
            .FirstOrDefault(t => typeof(VisoraModule).IsAssignableFrom(t)
                && !t.IsAbstract);

        if (moduleType is null)
            throw new InvalidOperationException(
                $"No VisoraModule implementation found in '{assemblyPath}'.");

        // Instantiate module (Phase 1 only - no initialization yet)
        if (Activator.CreateInstance(moduleType) is not VisoraModule module)
            throw new InvalidOperationException(
                $"Unable to create module instance '{moduleType.FullName}'.");

        // Get descriptor
        var descriptor = module.Descriptor
            ?? throw new InvalidOperationException(
                $"Module '{moduleType.FullName}' returned a null descriptor.");

        // Create context for initialization (Phase 2)
        var capabilities = options.Capabilities
            ?? throw new InvalidOperationException(
                "Capabilities provider cannot be null.");

        var context = new ModuleContext(
            descriptor,
            options.Services,
            capabilities,
            options.Properties);

        // Return handle in LOADED state (not yet initialized)
        return Task.FromResult(
            new ModuleHandle(assemblyPath, loader, assembly, module, context, options));
    }
    catch
    {
        // Cleanup on failure
        loader.Dispose();
        throw;
    }
}
```

**Key Points:**
- **Static Factory Method:** Can't use constructor (needs to return Task)
- **No Initialization:** Module created but `InitializeAsync()` not called
- **Error Handling:** Dispose loader on failure
- **State:** ModuleHandle exists, `_initialized = false`

---

### Example 2: Phase 2 - Initialize

**File:** `/src/Visora.Core/Modules/ModuleHandle.cs:85-92`

```csharp
/// <summary>
/// PHASE 2: INITIALIZE - Idempotent initialization
/// </summary>
public async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
{
    // IDEMPOTENCE: Check if already initialized
    if (_initialized)
        return;  // ← Safe to call multiple times

    // Call module's initialization logic
    await Module.InitializeAsync(_context, cancellationToken).ConfigureAwait(false);

    // Mark as initialized
    _initialized = true;
}
```

**Key Points:**
- **Idempotent:** Safe to call multiple times (no-op if already initialized)
- **Async:** Module can perform async initialization (I/O, network, etc.)
- **Context Provided:** Module receives ModuleContext with capabilities
- **State Change:** `_initialized` flag set to `true`

**Module Implementation Example:**

```csharp
public class MyModule : VisoraModule
{
    private ILogger? _logger;
    private IConsoleHost? _console;

    public override ModuleDescriptor Descriptor =>
        new("MyModule", "1.0.0", "Example module");

    public override async ValueTask InitializeAsync(
        ModuleContext context,
        CancellationToken cancellationToken)
    {
        // PHASE 2 LOGIC:

        // 1. Negotiate capabilities
        _logger = context.Capabilities.GetOptional<ILogger>();
        _console = context.Capabilities.GetRequired<IConsoleHost>();

        // 2. Perform async initialization
        await LoadConfigurationAsync(cancellationToken);

        // 3. Register with host services
        var registry = context.Services?.GetService<IComponentRegistry>();
        registry?.Register(this);

        // 4. Log initialization
        _logger?.LogInformation("MyModule initialized");
    }

    private async Task LoadConfigurationAsync(CancellationToken ct)
    {
        // Example: Load config file asynchronously
        // await File.ReadAllTextAsync("config.json", ct);
    }
}
```

---

### Example 3: Phase 3 - Shutdown

**File:** `/src/Visora.Core/Modules/ModuleHandle.cs:137-144`

```csharp
/// <summary>
/// PHASE 3: SHUTDOWN - Graceful cleanup
/// </summary>
public async Task ShutdownAsync(CancellationToken cancellationToken = default)
{
    // IDEMPOTENCE: Check if already shutdown
    if (!_initialized)
        return;  // ← Safe to call if not initialized or already shutdown

    // Call module's shutdown logic
    await Module.ShutdownAsync(_context, cancellationToken).ConfigureAwait(false);

    // Mark as shutdown (not initialized)
    _initialized = false;
}
```

**Key Points:**
- **Idempotent:** Safe to call multiple times
- **Reversible:** After shutdown, can call `EnsureInitializedAsync()` again
- **Graceful:** Module can save state, close connections cleanly
- **State Change:** `_initialized` flag set to `false`

**Module Implementation Example:**

```csharp
public class MyModule : VisoraModule
{
    private DatabaseConnection? _db;
    private List<string> _unsavedData = new();

    public override async ValueTask ShutdownAsync(
        ModuleContext context,
        CancellationToken cancellationToken)
    {
        // PHASE 3 LOGIC:

        // 1. Save state
        await SavePendingDataAsync(cancellationToken);

        // 2. Close connections
        if (_db != null)
        {
            await _db.CloseAsync(cancellationToken);
            _db = null;
        }

        // 3. Unregister from host
        var registry = context.Services?.GetService<IComponentRegistry>();
        registry?.Unregister(this);

        // 4. Log shutdown
        _logger?.LogInformation("MyModule shutdown complete");
    }

    private async Task SavePendingDataAsync(CancellationToken ct)
    {
        if (_unsavedData.Count > 0)
        {
            // await File.WriteAllLinesAsync("state.txt", _unsavedData, ct);
            _unsavedData.Clear();
        }
    }
}
```

---

### Example 4: Phase 4 - Dispose

**File:** `/src/Visora.Core/Modules/ModuleHandle.cs:146-157`

```csharp
/// <summary>
/// PHASE 4: DISPOSE - Final cleanup and unload
/// </summary>
public async ValueTask DisposeAsync()
{
    try
    {
        // Ensure shutdown before disposal
        await ShutdownAsync().ConfigureAwait(false);

        // Call module's dispose logic
        await Module.DisposeAsync().ConfigureAwait(false);
    }
    finally
    {
        // ALWAYS dispose loader, even if shutdown/dispose failed
        _loader.Dispose();  // ← Unloads AssemblyLoadContext
    }
}
```

**Key Points:**
- **Ensures Shutdown:** Calls `ShutdownAsync()` first (idempotent, safe)
- **Module Disposal:** Calls module's `DisposeAsync()` for final cleanup
- **Always Unloads:** `finally` ensures loader disposal happens
- **Terminal State:** After this, module is completely unloaded

**Module Implementation Example:**

```csharp
public class MyModule : VisoraModule
{
    private IDisposable? _subscription;

    public override ValueTask DisposeAsync()
    {
        // PHASE 4 LOGIC:

        // 1. Dispose any remaining resources
        _subscription?.Dispose();
        _subscription = null;

        // 2. Clear references (help GC)
        _logger = null;
        _console = null;

        // 3. No logging (logger may be disposed)
        // No async work (should be fast)

        return ValueTask.CompletedTask;
    }
}
```

---

### Example 5: Full Lifecycle Flow

**File:** `/src/Visora.Core/Modules/ModuleCatalog.cs` (illustrative)

```csharp
public class ModuleCatalog : IAsyncDisposable
{
    private readonly List<ModuleHandle> _modules = new();
    private readonly ModuleCatalogOptions _options;

    public async Task LoadModulesAsync(CancellationToken ct = default)
    {
        // PHASE 1: LOAD all modules
        var files = ModuleLocator.EnumerateCandidateFiles(_options);

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();

            var handle = await ModuleHandle.LoadAsync(file, _options, ct);
            _modules.Add(handle);
        }

        // Modules now loaded but not initialized
    }

    public async Task InitializeAllAsync(CancellationToken ct = default)
    {
        // PHASE 2: INITIALIZE all modules
        foreach (var module in _modules)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                await module.EnsureInitializedAsync(ct);
            }
            catch (Exception ex)
            {
                // Log error but continue (isolation)
                Console.WriteLine($"Failed to initialize {module.Descriptor.Name}: {ex}");
            }
        }
    }

    public async Task ShutdownAllAsync(CancellationToken ct = default)
    {
        // PHASE 3: SHUTDOWN all modules (reverse order)
        for (int i = _modules.Count - 1; i >= 0; i--)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                await _modules[i].ShutdownAsync(ct);
            }
            catch (Exception ex)
            {
                // Log but continue (must shutdown all modules)
                Console.WriteLine($"Failed to shutdown {_modules[i].Descriptor.Name}: {ex}");
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        // PHASE 4: DISPOSE all modules
        foreach (var module in _modules)
        {
            try
            {
                await module.DisposeAsync();
            }
            catch (Exception ex)
            {
                // Log but continue (must dispose all)
                Console.WriteLine($"Failed to dispose module: {ex}");
            }
        }

        _modules.Clear();
    }
}
```

**Complete Flow:**
```csharp
await using var catalog = new ModuleCatalog(options);

// PHASE 1: Load
await catalog.LoadModulesAsync();

// PHASE 2: Initialize
await catalog.InitializeAllAsync();

// ... use modules ...

// PHASE 3: Shutdown
await catalog.ShutdownAllAsync();

// PHASE 4: Dispose (automatic via using)
```

---

## File References

### Core Lifecycle Files

1. **ModuleHandle.cs**
   - Path: `/src/Visora.Core/Modules/ModuleHandle.cs`
   - Lines: 51-83 (Load), 85-92 (Initialize), 137-144 (Shutdown), 146-157 (Dispose)
   - Purpose: Lifecycle orchestration
   - Key Methods: `LoadAsync()`, `EnsureInitializedAsync()`, `ShutdownAsync()`, `DisposeAsync()`

2. **VisoraModule.cs**
   - Path: `/src/Visora.Contracts/Modules/VisoraModule.cs`
   - Lines: 13-40
   - Purpose: Base class with lifecycle hooks
   - Key Methods: `InitializeAsync()`, `ShutdownAsync()`, `DisposeAsync()`

3. **ModuleContext.cs**
   - Path: `/src/Visora.Contracts/Modules/ModuleContext.cs`
   - Lines: 10-41
   - Purpose: Context passed to lifecycle methods
   - Key Properties: `Descriptor`, `Services`, `Capabilities`, `Properties`

4. **PluginLoader (McMaster.NETCore.Plugins)**
   - External library
   - Purpose: Provides unloadable AssemblyLoadContext
   - Key Methods: `CreateFromAssemblyFile()`, `LoadDefaultAssembly()`, `Dispose()`

---

## Phases in Detail

### Phase 1: Load

**Purpose:** Discover and instantiate module without heavy initialization

**Operations:**
1. Locate `.vixm.dll` file
2. Create `PluginLoader` with isolated context
3. Load assembly
4. Reflect to find `VisoraModule` implementation
5. `Activator.CreateInstance()` to instantiate
6. Create `ModuleContext`
7. Return `ModuleHandle`

**State After:** Module exists in memory, `_initialized = false`

**Characteristics:**
- ✅ Fast (< 50ms typically)
- ✅ Can fail without affecting other modules
- ✅ Enables inspection without initialization
- ✅ No async work in module (constructor only)

**When It Happens:**
- Application startup (catalog building)
- On-demand module discovery
- Before inspection

**Error Handling:**
```csharp
try
{
    var handle = await ModuleHandle.LoadAsync(path, options, ct);
    // Success
}
catch (FileNotFoundException)
{
    // Module file missing
}
catch (InvalidOperationException ex) when (ex.Message.Contains("No VisoraModule"))
{
    // Not a valid module
}
catch (Exception ex)
{
    // Other load errors
}
```

---

### Phase 2: Initialize

**Purpose:** Perform one-time setup and resource acquisition

**Operations:**
1. Call `module.InitializeAsync(context, token)`
2. Module negotiates capabilities
3. Module loads configuration
4. Module allocates resources (DB connections, etc.)
5. Module registers with host services
6. Set `_initialized = true`

**State After:** Module ready for use, `_initialized = true`

**Characteristics:**
- ⚠️ Can be slow (I/O, network, etc.)
- ✅ Idempotent (safe to call multiple times)
- ✅ Async (doesn't block)
- ✅ Deferred (happens on-demand via `EnsureInitializedAsync`)

**When It Happens:**
- Before first use
- Explicitly via `catalog.InitializeAllAsync()`
- Before inspection (in `InspectAsync()`)

**Module Responsibilities:**
- Negotiate capabilities with host
- Load configuration files
- Initialize state
- Register services
- DO NOT: Start background threads, open network listeners

**Error Handling:**
```csharp
try
{
    await handle.EnsureInitializedAsync(ct);
}
catch (OperationCanceledException)
{
    // Initialization cancelled
}
catch (Exception ex)
{
    // Initialization failed - module remains uninitialized
    // Can retry later
}
```

---

### Phase 3: Shutdown

**Purpose:** Gracefully prepare for disposal or hot-swap

**Operations:**
1. Call `module.ShutdownAsync(context, token)`
2. Module saves state
3. Module closes connections
4. Module releases resources
5. Module unregisters from host
6. Set `_initialized = false`

**State After:** Module inactive, `_initialized = false`, can be re-initialized

**Characteristics:**
- ✅ Reversible (can initialize again after shutdown)
- ✅ Idempotent (safe to call multiple times)
- ✅ Graceful (can take time to save state)
- ⚠️ Can fail (but logged, doesn't prevent disposal)

**When It Happens:**
- Before hot-swap
- Before application exit
- Explicitly via `catalog.ShutdownAllAsync()`

**Module Responsibilities:**
- Save pending data
- Close connections gracefully
- Release non-managed resources
- Unregister from host services
- DO NOT: Delete permanent data, break invariants

**Error Handling:**
```csharp
try
{
    await handle.ShutdownAsync(ct);
}
catch (Exception ex)
{
    // Log error but continue
    // Disposal will still happen
    _logger.LogWarning(ex, "Shutdown failed for {Module}", handle.Descriptor.Name);
}
```

---

### Phase 4: Dispose

**Purpose:** Final cleanup and assembly unload

**Operations:**
1. Ensure shutdown (idempotent)
2. Call `module.DisposeAsync()`
3. Module final cleanup
4. Dispose `PluginLoader`
5. Unload `AssemblyLoadContext`
6. GC can collect module

**State After:** Module completely unloaded, memory freed

**Characteristics:**
- ✅ Always completes (`finally` block)
- ✅ Terminal (can't be reversed)
- ✅ Fast (should not do I/O)
- ✅ Guaranteed (even if shutdown failed)

**When It Happens:**
- Hot-swap (before loading new version)
- Application shutdown
- Explicit disposal (`await handle.DisposeAsync()`)

**Module Responsibilities:**
- Dispose `IDisposable` resources
- Clear references (help GC)
- DO NOT: Async I/O, logging (services may be disposed)
- MUST: Be fast and reliable

**Error Handling:**
```csharp
try
{
    await handle.DisposeAsync();
}
catch (Exception ex)
{
    // Log error, but module is still disposed
    // Can't recover
    _logger.LogError(ex, "Disposal failed (non-critical)");
}
```

---

## Idempotence Guarantees

### Why Idempotence Matters

**Problem Without Idempotence:**
```csharp
// ❌ BAD: Not idempotent
public async Task InitializeAsync()
{
    // Called twice → creates two connections!
    _connection = new DatabaseConnection();
    await _connection.OpenAsync();
}
```

**Solution With Idempotence:**
```csharp
// ✅ GOOD: Idempotent
private bool _initialized;

public async Task EnsureInitializedAsync()
{
    if (_initialized) return;  // ← Guard

    _connection = new DatabaseConnection();
    await _connection.OpenAsync();
    _initialized = true;
}
```

### VISORA Idempotence Implementation

**Initialize:**
```csharp
public async Task EnsureInitializedAsync(CancellationToken ct = default)
{
    if (_initialized)
        return;  // ← Idempotent guard

    await Module.InitializeAsync(_context, ct).ConfigureAwait(false);
    _initialized = true;
}
```

**Shutdown:**
```csharp
public async Task ShutdownAsync(CancellationToken ct = default)
{
    if (!_initialized)
        return;  // ← Idempotent guard

    await Module.ShutdownAsync(_context, ct).ConfigureAwait(false);
    _initialized = false;
}
```

**Dispose:**
```csharp
public async ValueTask DisposeAsync()
{
    try
    {
        await ShutdownAsync().ConfigureAwait(false);  // ← Idempotent
        await Module.DisposeAsync().ConfigureAwait(false);
    }
    finally
    {
        _loader.Dispose();  // ← Safe to call multiple times (PluginLoader handles it)
    }
}
```

### Module-Level Idempotence

Modules should also implement idempotent lifecycle methods:

```csharp
public class IdempotentModule : VisoraModule
{
    private bool _configLoaded;
    private IDatabase? _db;

    public override async ValueTask InitializeAsync(
        ModuleContext context,
        CancellationToken ct)
    {
        // IDEMPOTENT: Safe to call multiple times
        if (!_configLoaded)
        {
            await LoadConfigAsync(ct);
            _configLoaded = true;
        }

        if (_db == null)
        {
            _db = await Database.ConnectAsync(ct);
        }

        // Other initialization...
    }

    public override async ValueTask ShutdownAsync(
        ModuleContext context,
        CancellationToken ct)
    {
        // IDEMPOTENT: Safe to call multiple times
        if (_db != null)
        {
            await _db.CloseAsync(ct);
            _db = null;
        }

        _configLoaded = false;
    }
}
```

---

## Dependency Ordering

### Problem: Module Dependencies

```
ModuleA depends on ModuleB
ModuleB depends on ModuleC

Initialization Order: C → B → A
Shutdown Order: A → B → C (reverse!)
```

### VISORA Current Implementation

**Current:** No built-in dependency management

```csharp
// Current: All modules initialized in file discovery order
foreach (var module in _modules)
{
    await module.EnsureInitializedAsync(ct);
}
```

**Limitation:** If `ModuleA` requires `ModuleB`, initialization may fail

### Workaround: Manual Ordering

```csharp
public class ModuleCatalog
{
    public async Task InitializeInOrderAsync(
        IEnumerable<string> moduleNames,
        CancellationToken ct)
    {
        foreach (var name in moduleNames)
        {
            var module = _modules.FirstOrDefault(m => m.Descriptor.Name == name);
            if (module != null)
            {
                await module.EnsureInitializedAsync(ct);
            }
        }
    }
}

// Usage:
await catalog.InitializeInOrderAsync(
    new[] { "CoreModule", "DatabaseModule", "UIModule" },
    ct);
```

### Future Enhancement: Dependency Graph

**Illustrative Example:**

```csharp
public class ModuleDescriptor
{
    public string Name { get; }
    public string Version { get; }

    // Future: Dependency declarations
    public IReadOnlyList<ModuleDependency> Dependencies { get; }
}

public record ModuleDependency(string ModuleName, string MinVersion);

// Topological sort for initialization order
public class DependencyResolver
{
    public List<ModuleHandle> ResolveOrder(IEnumerable<ModuleHandle> modules)
    {
        // Topological sort based on dependencies
        // Initialize in dependency order
        // Shutdown in reverse order
    }
}
```

---

## Error Handling

### Error Handling Strategy by Phase

| Phase | Error Severity | Recovery Strategy |
|-------|----------------|-------------------|
| **Load** | High | Fail fast, don't add to catalog |
| **Initialize** | Medium | Log, skip module, continue |
| **Shutdown** | Low | Log, continue to disposal |
| **Dispose** | Very Low | Log, continue (best effort) |

### Load Phase Errors

```csharp
foreach (var file in files)
{
    try
    {
        var handle = await ModuleHandle.LoadAsync(file, options, ct);
        _modules.Add(handle);
    }
    catch (FileNotFoundException ex)
    {
        _logger.LogWarning("Module file not found: {File}", file);
        // Don't add to catalog, continue with other modules
    }
    catch (InvalidOperationException ex) when (ex.Message.Contains("No VisoraModule"))
    {
        _logger.LogWarning("Not a valid module: {File}", file);
        // Don't add to catalog
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to load module: {File}", file);
        // Don't add to catalog
    }
}
```

### Initialize Phase Errors

```csharp
foreach (var module in _modules)
{
    try
    {
        await module.EnsureInitializedAsync(ct);
        _logger.LogInformation("Initialized: {Module}", module.Descriptor.Name);
    }
    catch (OperationCanceledException)
    {
        _logger.LogWarning("Initialization cancelled: {Module}", module.Descriptor.Name);
        throw;  // Propagate cancellation
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to initialize: {Module}", module.Descriptor.Name);
        // Module remains in catalog but uninitialized
        // Can retry later
    }
}
```

### Shutdown Phase Errors

```csharp
// Shutdown in reverse order
for (int i = _modules.Count - 1; i >= 0; i--)
{
    try
    {
        await _modules[i].ShutdownAsync(ct);
    }
    catch (Exception ex)
    {
        // Log but continue - must shutdown all modules
        _logger.LogWarning(ex, "Shutdown failed: {Module}", _modules[i].Descriptor.Name);
        // Continue to next module
    }
}
```

### Dispose Phase Errors

```csharp
foreach (var module in _modules)
{
    try
    {
        await module.DisposeAsync();
    }
    catch (Exception ex)
    {
        // Log but continue - must dispose all modules
        _logger.LogError(ex, "Disposal failed (non-critical): {Module}", module.Descriptor.Name);
        // Module is still disposed (PluginLoader disposed in finally)
    }
}
```

### Module-Level Error Handling

```csharp
public class RobustModule : VisoraModule
{
    public override async ValueTask InitializeAsync(
        ModuleContext context,
        CancellationToken ct)
    {
        try
        {
            // Initialization logic
            await LoadConfigAsync(ct);
            await ConnectToDatabaseAsync(ct);
        }
        catch (ConfigurationException ex)
        {
            // Handle specific errors
            _logger?.LogError(ex, "Configuration error");
            throw;  // Propagate to host
        }
        catch (Exception ex)
        {
            // Cleanup partial initialization
            await CleanupPartialStateAsync();
            throw;  // Propagate to host
        }
    }

    public override async ValueTask ShutdownAsync(
        ModuleContext context,
        CancellationToken ct)
    {
        // IMPORTANT: Don't throw from Shutdown
        // Log errors but continue cleanup

        try
        {
            await SaveStateAsync(ct);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to save state");
            // Continue with other cleanup
        }

        try
        {
            await _db?.CloseAsync(ct);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to close database");
            // Continue
        }
    }
}
```

---

## Testing Patterns

### Pattern 1: Testing Lifecycle Phases

```csharp
[Fact]
public async Task Module_FollowsFullLifecycle()
{
    // Arrange
    var options = new ModuleCatalogOptions();
    var testModulePath = "TestModule.vixm.dll";

    // Act & Assert: PHASE 1 (Load)
    var handle = await ModuleHandle.LoadAsync(testModulePath, options, CancellationToken.None);
    Assert.NotNull(handle);
    Assert.NotNull(handle.Module);
    Assert.NotNull(handle.Descriptor);

    // Act & Assert: PHASE 2 (Initialize)
    await handle.EnsureInitializedAsync();
    // Module should be initialized
    // (Check internal state if exposed for testing)

    // Act & Assert: PHASE 3 (Shutdown)
    await handle.ShutdownAsync();
    // Module should be shutdown

    // Act & Assert: PHASE 4 (Dispose)
    await handle.DisposeAsync();
    // Module should be disposed
}
```

### Pattern 2: Testing Idempotence

```csharp
[Fact]
public async Task EnsureInitializedAsync_IsIdempotent()
{
    // Arrange
    var handle = await LoadTestModuleAsync();

    // Act: Call multiple times
    await handle.EnsureInitializedAsync();
    await handle.EnsureInitializedAsync();
    await handle.EnsureInitializedAsync();

    // Assert: Should work without errors
    // Initialization should only happen once
}

[Fact]
public async Task ShutdownAsync_IsIdempotent()
{
    // Arrange
    var handle = await LoadTestModuleAsync();
    await handle.EnsureInitializedAsync();

    // Act: Call multiple times
    await handle.ShutdownAsync();
    await handle.ShutdownAsync();
    await handle.ShutdownAsync();

    // Assert: Should work without errors
}
```

### Pattern 3: Testing Re-initialization

```csharp
[Fact]
public async Task Module_CanBeReInitializedAfterShutdown()
{
    // Arrange
    var handle = await LoadTestModuleAsync();

    // Act & Assert: Initialize → Shutdown → Re-initialize
    await handle.EnsureInitializedAsync();
    await handle.ShutdownAsync();
    await handle.EnsureInitializedAsync();  // ← Should work

    // Cleanup
    await handle.DisposeAsync();
}
```

### Pattern 4: Testing Error Handling

```csharp
[Fact]
public async Task InitializeAsync_FailureDoesNotPreventRetry()
{
    // Arrange
    var failingModule = new FailingModule();
    failingModule.ShouldFailInitialize = true;

    // Act & Assert: First call fails
    await Assert.ThrowsAsync<InvalidOperationException>(
        () => failingModule.InitializeAsync(context, CancellationToken.None).AsTask());

    // Act: Retry after fixing
    failingModule.ShouldFailInitialize = false;
    await failingModule.InitializeAsync(context, CancellationToken.None);

    // Should succeed on retry
}
```

### Pattern 5: Testing Module Implementation

```csharp
public class TestableModule : VisoraModule
{
    public bool InitializeCalled { get; private set; }
    public bool ShutdownCalled { get; private set; }
    public bool DisposeCalled { get; private set; }

    public override ModuleDescriptor Descriptor =>
        new("Test", "1.0", "Test module");

    public override ValueTask InitializeAsync(
        ModuleContext context,
        CancellationToken ct)
    {
        InitializeCalled = true;
        return ValueTask.CompletedTask;
    }

    public override ValueTask ShutdownAsync(
        ModuleContext context,
        CancellationToken ct)
    {
        ShutdownCalled = true;
        return ValueTask.CompletedTask;
    }

    public override ValueTask DisposeAsync()
    {
        DisposeCalled = true;
        return ValueTask.CompletedTask;
    }
}

[Fact]
public async Task Module_LifecycleMethodsCalled()
{
    // Arrange
    var module = new TestableModule();
    var context = CreateTestContext();

    // Act
    await module.InitializeAsync(context, CancellationToken.None);
    await module.ShutdownAsync(context, CancellationToken.None);
    await module.DisposeAsync();

    // Assert
    Assert.True(module.InitializeCalled);
    Assert.True(module.ShutdownCalled);
    Assert.True(module.DisposeCalled);
}
```

---

## Best Practices

### 1. Keep Constructors Lightweight

```csharp
// ✅ GOOD: Constructor does minimal work
public class MyModule : VisoraModule
{
    public MyModule()
    {
        // Just initialize fields
        // No I/O, no heavy computation
    }

    public override async ValueTask InitializeAsync(...)
    {
        // Heavy work here
        await LoadConfigurationAsync();
    }
}

// ❌ BAD: Constructor does heavy work
public class BadModule : VisoraModule
{
    public BadModule()
    {
        // ❌ Don't do this in constructor!
        var config = File.ReadAllText("config.json");
        _db = new Database(config);
        _db.Connect();  // ❌ Blocks!
    }
}
```

### 2. Implement All Lifecycle Methods

```csharp
// ✅ GOOD: Explicit lifecycle management
public class CompleteModule : VisoraModule
{
    private IDatabase? _db;

    public override async ValueTask InitializeAsync(
        ModuleContext context,
        CancellationToken ct)
    {
        _db = await Database.ConnectAsync(ct);
    }

    public override async ValueTask ShutdownAsync(
        ModuleContext context,
        CancellationToken ct)
    {
        if (_db != null)
        {
            await _db.CloseAsync(ct);
            _db = null;
        }
    }

    public override ValueTask DisposeAsync()
    {
        _db?.Dispose();
        _db = null;
        return ValueTask.CompletedTask;
    }
}
```

### 3. Don't Throw from Shutdown

```csharp
// ✅ GOOD: Log errors, don't throw
public override async ValueTask ShutdownAsync(
    ModuleContext context,
    CancellationToken ct)
{
    try
    {
        await SaveStateAsync(ct);
    }
    catch (Exception ex)
    {
        _logger?.LogWarning(ex, "Failed to save state");
        // Don't throw - continue cleanup
    }

    try
    {
        await _connection?.CloseAsync(ct);
    }
    catch (Exception ex)
    {
        _logger?.LogWarning(ex, "Failed to close connection");
        // Don't throw
    }
}

// ❌ BAD: Throwing from shutdown
public override async ValueTask ShutdownAsync(...)
{
    await SaveStateAsync(ct);  // ❌ Can throw and prevent other cleanup
    await _connection?.CloseAsync(ct);  // ❌ May never execute
}
```

### 4. Make Dispose Fast and Synchronous

```csharp
// ✅ GOOD: Fast, synchronous dispose
public override ValueTask DisposeAsync()
{
    _subscription?.Dispose();
    _timer?.Dispose();
    _cancellationSource?.Dispose();

    // Clear references
    _db = null;
    _logger = null;

    return ValueTask.CompletedTask;
}

// ❌ BAD: Async I/O in dispose
public override async ValueTask DisposeAsync()
{
    // ❌ Don't do I/O in dispose!
    await File.WriteAllTextAsync("state.json", _state);

    // ❌ Don't use disposed services!
    _logger.LogInformation("Disposed");  // Logger may be disposed
}
```

### 5. Handle Cancellation Properly

```csharp
// ✅ GOOD: Respects cancellation
public override async ValueTask InitializeAsync(
    ModuleContext context,
    CancellationToken ct)
{
    ct.ThrowIfCancellationRequested();

    await LoadConfigAsync(ct);

    ct.ThrowIfCancellationRequested();

    await ConnectToDatabaseAsync(ct);

    // Cleanup on cancellation
    ct.Register(() =>
    {
        _db?.Dispose();
        _db = null;
    });
}

// ✅ GOOD: Shutdown is cancellable but best-effort
public override async ValueTask ShutdownAsync(
    ModuleContext context,
    CancellationToken ct)
{
    try
    {
        await SaveStateAsync(ct);
    }
    catch (OperationCanceledException)
    {
        _logger?.LogWarning("Shutdown cancelled, state not saved");
        // Don't propagate - continue cleanup
    }

    // Continue with non-cancellable cleanup
    _db?.Dispose();
}
```

---

## Advanced Topics

### Topic 1: Hot-Swapping Modules

**Scenario:** Update a module without restarting the host

```csharp
public async Task HotSwapModuleAsync(
    string moduleName,
    string newModulePath,
    CancellationToken ct)
{
    // 1. Find current module
    var oldModule = _modules.FirstOrDefault(m => m.Descriptor.Name == moduleName);
    if (oldModule == null)
        throw new InvalidOperationException($"Module {moduleName} not found");

    // 2. Save state (if module supports it)
    var state = await SaveModuleStateAsync(oldModule, ct);

    // 3. Shutdown old module
    await oldModule.ShutdownAsync(ct);

    // 4. Dispose old module (unload assembly)
    await oldModule.DisposeAsync();

    // 5. Load new module
    var newModule = await ModuleHandle.LoadAsync(newModulePath, _options, ct);

    // 6. Replace in catalog
    _modules.Remove(oldModule);
    _modules.Add(newModule);

    // 7. Initialize new module
    await newModule.EnsureInitializedAsync(ct);

    // 8. Restore state (if supported)
    await RestoreModuleStateAsync(newModule, state, ct);

    _logger.LogInformation("Hot-swapped module: {Module}", moduleName);
}
```

### Topic 2: Lazy Initialization with Inspection

**Use Case:** Inspect module without full initialization

```csharp
// ModuleHandle.InspectAsync already demonstrates this:
public async Task<ModuleInspection> InspectAsync(CancellationToken ct = default)
{
    // Initialize for inspection
    await EnsureInitializedAsync(ct).ConfigureAwait(false);

    // Discover components (creates temporary instances)
    var discoveryContext = new ModuleDiscoveryContext(Assembly);
    var componentTypes = Module.DiscoverComponents(discoveryContext).ToArray();

    // ... inspection logic ...

    // Components are disposed after inspection
    // Module remains initialized for reuse
}
```

### Topic 3: Async Disposal Best Practices

**IAsyncDisposable Pattern:**

```csharp
public class ProperAsyncDisposable : IAsyncDisposable
{
    private bool _disposed;

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        try
        {
            await DisposeAsyncCore().ConfigureAwait(false);
        }
        finally
        {
            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }

    protected virtual async ValueTask DisposeAsyncCore()
    {
        // Async disposal logic
        await Task.CompletedTask;
    }
}
```

### Topic 4: Component Lifecycle

Components also have a lifecycle, nested within module lifecycle:

```
Module Load
  ↓
Module Initialize
  ↓
  [Component Discovery]
  ↓
  Component Instantiate (for inspection)
    ↓
    Component Initialize
    ↓
    Component CreateCommands
    ↓
    Component Dispose (after inspection)
  ↓
Module Shutdown
  ↓
Module Dispose
```

**File:** `/src/Visora.Contracts/Components/VisoraComponent.cs`

```csharp
public abstract class VisoraComponent : IAsyncDisposable
{
    public abstract ComponentDescriptor Descriptor { get; }

    public virtual ValueTask InitializeAsync(
        ComponentContext context,
        CancellationToken ct = default)
        => ValueTask.CompletedTask;

    public virtual ValueTask ActivateAsync(
        ComponentContext context,
        CancellationToken ct = default)
        => ValueTask.CompletedTask;

    public virtual ValueTask DeactivateAsync(
        ComponentContext context,
        CancellationToken ct = default)
        => ValueTask.CompletedTask;

    public virtual ValueTask DisposeAsync()
        => ValueTask.CompletedTask;
}
```

---

## Summary

### Key Takeaways

1. **Four-Phase Lifecycle**
   - Load: Fast discovery and instantiation
   - Initialize: Heavy setup, deferred
   - Shutdown: Graceful cleanup
   - Dispose: Final cleanup and unload

2. **Idempotence**
   - All lifecycle methods are idempotent
   - Safe to call multiple times
   - Enables retry and error recovery

3. **Async Throughout**
   - All lifecycle methods are async
   - Supports I/O operations
   - Respects cancellation

4. **Error Handling**
   - Load: Fail fast
   - Initialize: Log and skip
   - Shutdown: Log and continue
   - Dispose: Always completes

5. **Resource Management**
   - Clear ownership at each phase
   - Explicit cleanup order
   - No resource leaks

---

### When to Use This Pattern

✅ **Use Four-Phase Lifecycle When:**
- Building plugin/module systems
- Need lazy initialization
- Supporting hot-swapping
- Managing complex resource lifecycles
- Isolating extension failures

❌ **Consider Simpler Patterns When:**
- Modules have no resources to manage
- No hot-swapping needed
- Startup time not a concern
- Simple load/unload sufficient

---

### Further Reading

- **Related Pattern:** Plugin Architecture - Assembly loading and isolation
- **Related Pattern:** Reflection Discovery - How modules are found
- **Related Pattern:** Resource Management - IDisposable and IAsyncDisposable
- **Implementation:** `/src/Visora.Core/Modules/ModuleHandle.cs`
- **Contracts:** `/src/Visora.Contracts/Modules/VisoraModule.cs`
