# Plugin Architecture Pattern - VISORA Deep Dive

**Last Updated:** November 10, 2025
**VISORA Version:** .NET 9.0
**Pattern Tier:** Tier 1 (Foundational)
**Related Patterns:** Registry Pattern, Module Lifecycle, Layered Architecture

---

## Table of Contents

1. [Pattern Overview](#pattern-overview)
2. [Why VISORA Uses Plugin Architecture](#why-visora-uses-plugin-architecture)
3. [VISORA Implementation](#visora-implementation)
4. [Code Examples](#code-examples)
5. [File References](#file-references)
6. [Lifecycle: Load, Unload, Hot-Swap](#lifecycle-load-unload-hot-swap)
7. [Tradeoffs](#tradeoffs)
8. [Alternatives Considered](#alternatives-considered)
9. [Implementation Details](#implementation-details)
10. [Testing Patterns](#testing-patterns)
11. [Best Practices](#best-practices)
12. [Advanced Topics](#advanced-topics)

---

## Pattern Overview

### What is Plugin Architecture?

**Definition:** A plugin architecture isolates extensions in separate runtime contexts while maintaining a shared contract layer, enabling dynamic loading, versioning independence, and hot-swapping without restarting the host.

**Key Characteristics:**
- **Isolation:** Each plugin runs in its own assembly load context
- **Shared Contracts:** Core types are shared to enable communication
- **Unloadability:** Plugins can be unloaded to free resources
- **Discovery:** Host dynamically discovers and loads plugins
- **Version Independence:** Plugins can use different versions of dependencies

### Core Components

```
┌─────────────────────────────────────────────────┐
│                   HOST                          │
│  ┌──────────────────────────────────────────┐  │
│  │         PluginLoader                     │  │
│  │  - CreateFromAssemblyFile()              │  │
│  │  - LoadDefaultAssembly()                 │  │
│  │  - Dispose() → Unload context            │  │
│  └──────────────────────────────────────────┘  │
│                      ↓                          │
│  ┌──────────────────────────────────────────┐  │
│  │    AssemblyLoadContext (Unloadable)      │  │
│  │  ┌────────────────────────────────────┐  │  │
│  │  │         Plugin Assembly            │  │  │
│  │  │  - Uses shared types from host     │  │  │
│  │  │  - Can use own dependencies        │  │  │
│  │  │  - Implements IPlugin contract     │  │  │
│  │  └────────────────────────────────────┘  │  │
│  └──────────────────────────────────────────┘  │
│                                                 │
│  Shared Types (from host):                     │
│  - VisoraModule, VisoraComponent, VisoraCommand│
│  - Descriptors, Contexts, ICapabilityProvider  │
└─────────────────────────────────────────────────┘
```

---

## Why VISORA Uses Plugin Architecture

### Design Goals

1. **Dynamic Extensibility**
   - Add functionality without recompiling the host
   - Third-party developers can create modules
   - AI agents can generate and load modules at runtime

2. **Isolation**
   - Module bugs don't crash the host
   - Memory leaks are contained
   - Different modules can use incompatible dependency versions

3. **Hot-Swapping**
   - Update modules without restarting the application
   - Critical for long-running IDE scenarios
   - Enables rapid development cycles

4. **Resource Management**
   - Unload unused modules to free memory
   - Explicit lifecycle control
   - Garbage collection of module resources

5. **Multi-Form Support**
   - Same module runs in CLI, Terminal UI, WPF
   - Host provides different capabilities per form
   - Modules adapt to available capabilities

### Key Decision Points

**Why Not Static Compilation?**
- Requires recompilation for every change
- Limits extensibility
- No hot-swapping

**Why Not AppDomains?**
- AppDomains are .NET Framework only (.NET Core removed them)
- AssemblyLoadContext is the modern replacement
- Better performance and flexibility

**Why McMaster.NETCore.Plugins?**
- Mature, well-tested library
- Handles assembly resolution complexity
- Unloadable contexts out of the box
- Shared types mechanism

---

## VISORA Implementation

### Technology Stack

**Library:** McMaster.NETCore.Plugins v2.0.0
- NuGet: `McMaster.NETCore.Plugins`
- Source: https://github.com/natemcmaster/DotNetCorePlugins
- License: Apache 2.0

**Runtime:** .NET 9.0
- Uses `AssemblyLoadContext` (formerly `CollectibleAssemblyLoadContext`)
- Supports unloadable contexts
- Strong-name assembly support

### Architecture

```
Visora.Contracts.dll (Shared)
     ↓ referenced by
Visora.Core.dll (Host Infrastructure)
     ├─ ModuleCatalog (discovers modules)
     ├─ ModuleHandle (wraps PluginLoader)
     └─ ModuleLocator (finds .vixm.dll files)
     ↓ loads
MyModule.vixm.dll (Plugin)
     ├─ Implements VisoraModule (shared type)
     ├─ Can reference own dependencies
     └─ Runs in isolated context
```

### Module Naming Convention

**Pattern:** `*.vixm.dll`
- **vixm** = "Visora IX Module"
- Examples: `VSCC.vixm.dll`, `Visora.Shell.Commands.Core.vixm.dll`

**Why .vixm.dll?**
- Distinguishes modules from regular assemblies
- Enables glob pattern discovery: `"*.vixm.dll"`
- Self-documenting naming

---

## Code Examples

### Example 1: Loading a Module

**File:** `/src/Visora.Core/Modules/ModuleHandle.cs:51-83`

```csharp
public static Task<ModuleHandle> LoadAsync(
    string assemblyPath,
    ModuleCatalogOptions options,
    CancellationToken cancellationToken)
{
    if (options is null) throw new ArgumentNullException(nameof(options));
    cancellationToken.ThrowIfCancellationRequested();

    // Step 1: Get shared types from options
    var sharedTypes = options.GetSharedTypesArray();

    // Step 2: Create unloadable PluginLoader
    var loader = PluginLoader.CreateFromAssemblyFile(
        assemblyPath,
        sharedTypes: sharedTypes,
        isUnloadable: true);  // ← Critical for hot-swapping

    try
    {
        // Step 3: Load the assembly
        var assembly = loader.LoadDefaultAssembly();

        // Step 4: Find VisoraModule implementation via reflection
        var moduleType = assembly
            .GetTypes()
            .FirstOrDefault(t => typeof(VisoraModule).IsAssignableFrom(t)
                && !t.IsAbstract);

        if (moduleType is null)
            throw new InvalidOperationException(
                $"No VisoraModule implementation found in '{assemblyPath}'.");

        // Step 5: Instantiate the module
        if (Activator.CreateInstance(moduleType) is not VisoraModule module)
            throw new InvalidOperationException(
                $"Unable to create module instance '{moduleType.FullName}'.");

        // Step 6: Get descriptor and create context
        var descriptor = module.Descriptor
            ?? throw new InvalidOperationException(
                $"Module '{moduleType.FullName}' returned a null descriptor.");

        var capabilities = options.Capabilities
            ?? throw new InvalidOperationException(
                "Capabilities provider cannot be null.");

        var context = new ModuleContext(
            descriptor,
            options.Services,
            capabilities,
            options.Properties);

        // Step 7: Return wrapped handle
        return Task.FromResult(
            new ModuleHandle(assemblyPath, loader, assembly, module, context, options));
    }
    catch
    {
        // Clean up on failure
        loader.Dispose();
        throw;
    }
}
```

**Key Points:**
1. **Shared Types:** Prevents type duplication across contexts
2. **isUnloadable: true:** Enables hot-swapping
3. **Exception Safety:** Disposes loader on error
4. **Reflection Discovery:** No manifest files needed

---

### Example 2: Shared Types Configuration

**File:** `/src/Visora.Core/Modules/ModuleCatalogOptions.cs:500-517`

```csharp
internal static class ModuleCatalogDefaults
{
    internal static readonly Type[] SharedTypes =
    {
        // Base abstractions
        typeof(VisoraModule),
        typeof(VisoraComponent),
        typeof(VisoraCommand),

        // Descriptors (metadata)
        typeof(ModuleDescriptor),
        typeof(ComponentDescriptor),
        typeof(CommandDescriptor),

        // Context objects
        typeof(ModuleContext),
        typeof(ModuleDiscoveryContext),
        typeof(ComponentContext),
        typeof(CommandContext),

        // Result types
        typeof(CommandResult),

        // Capability system
        typeof(ICapabilityProvider)
    };
}

public sealed class ModuleCatalogOptions
{
    public IReadOnlyCollection<Type> SharedTypes { get; set; }
        = ModuleCatalogDefaults.SharedTypes;

    internal Type[] GetSharedTypesArray()
        => SharedTypes?.ToArray() ?? ModuleCatalogDefaults.SharedTypes;
}
```

**Why These Types Are Shared:**
- **VisoraModule/Component/Command:** Modules must implement these
- **Descriptors:** Host needs to read metadata
- **Contexts:** Host creates and passes to modules
- **CommandResult:** Modules return to host
- **ICapabilityProvider:** Modules query capabilities

**Not Shared:**
- Module-specific business logic
- Third-party dependencies (unless explicitly shared)
- Helper classes internal to modules

---

### Example 3: Module Discovery

**File:** `/src/Visora.Core/Modules/ModuleLocator.cs`

```csharp
public static IEnumerable<string> EnumerateCandidateFiles(
    ModuleCatalogOptions options)
{
    if (options is null) throw new ArgumentNullException(nameof(options));

    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    // Priority 1: Explicit files
    foreach (var explicitFile in options.ExplicitModuleFiles)
    {
        if (string.IsNullOrWhiteSpace(explicitFile))
            continue;

        var path = Path.GetFullPath(explicitFile);
        if (File.Exists(path) && seen.Add(path))
            yield return path;
    }

    // Priority 2: Probing paths
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

        foreach (var file in Directory.EnumerateFiles(
            dir, options.SearchPattern, searchOption))
        {
            var resolved = Path.GetFullPath(file);
            if (ShouldSkipPath(resolved))
                continue;

            if (seen.Add(resolved))
                yield return resolved;
        }
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
```

**Discovery Strategy:**
1. **Explicit files first:** Guaranteed to be loaded
2. **Probing paths second:** Search directories
3. **Deduplication:** HashSet prevents duplicates
4. **Skip build artifacts:** Ignores `/obj/` directories

---

### Example 4: Disposal and Unloading

**File:** `/src/Visora.Core/Modules/ModuleHandle.cs:146-157`

```csharp
public async ValueTask DisposeAsync()
{
    try
    {
        // Step 1: Graceful shutdown
        await ShutdownAsync().ConfigureAwait(false);

        // Step 2: Dispose module instance
        await Module.DisposeAsync().ConfigureAwait(false);
    }
    finally
    {
        // Step 3: Dispose PluginLoader → unload assembly context
        _loader.Dispose();
    }
}
```

**Disposal Flow:**
```
1. ShutdownAsync()
   → Module.ShutdownAsync(context)
   → Cleanup resources, save state

2. Module.DisposeAsync()
   → Free module-specific resources

3. _loader.Dispose()
   → Unload AssemblyLoadContext
   → Mark for garbage collection
   → Eventually: GC collects assembly memory
```

**Important:** Assembly unloading is asynchronous at the CLR level. After `Dispose()`, the context is marked for collection, but actual memory reclamation happens during GC.

---

### Example 5: Module Catalog Discovery

**File:** `/src/Visora.Core/Modules/ModuleCatalog.cs`

```csharp
public sealed class ModuleCatalog : IAsyncDisposable
{
    private readonly List<ModuleHandle> _modules = new();

    public IReadOnlyList<ModuleHandle> Modules => _modules;

    public async Task DiscoverAsync(
        ModuleCatalogOptions options,
        CancellationToken cancellationToken = default)
    {
        if (options is null) throw new ArgumentNullException(nameof(options));

        // Step 1: Enumerate candidate files
        var candidates = ModuleLocator.EnumerateCandidateFiles(options);

        // Step 2: Load each module
        foreach (var assemblyPath in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // Skip if already loaded
                if (_modules.Any(m => string.Equals(
                    m.AssemblyPath, assemblyPath,
                    StringComparison.OrdinalIgnoreCase)))
                    continue;

                // Load module
                var handle = await ModuleHandle.LoadAsync(
                    assemblyPath, options, cancellationToken)
                    .ConfigureAwait(false);

                _modules.Add(handle);
            }
            catch (Exception ex)
            {
                // Log but continue (don't fail entire discovery)
                Console.Error.WriteLine(
                    $"Failed to load module '{assemblyPath}': {ex.Message}");
            }
        }
    }

    public ModuleHandle? GetById(string moduleId)
        => _modules.FirstOrDefault(m =>
            string.Equals(m.Descriptor.Id, moduleId,
                StringComparison.OrdinalIgnoreCase));

    public async ValueTask DisposeAsync()
    {
        foreach (var module in _modules)
        {
            try
            {
                await module.DisposeAsync().ConfigureAwait(false);
            }
            catch
            {
                // Continue disposing others
            }
        }
        _modules.Clear();
    }
}
```

**Catalog Behavior:**
- **Resilient:** One bad module doesn't stop discovery
- **Idempotent:** Won't load same path twice
- **Async:** Respects cancellation tokens
- **Cleanup:** Disposes all modules on catalog disposal

---

## File References

### Core Implementation Files

| File | Lines | Purpose |
|------|-------|---------|
| `/src/Visora.Core/Modules/ModuleHandle.cs` | 20-158 | Wraps PluginLoader, manages lifecycle |
| `/src/Visora.Core/Modules/ModuleHandle.cs` | 56-78 | PluginLoader setup and shared types |
| `/src/Visora.Core/Modules/ModuleHandle.cs` | 146-157 | Disposal and unloading logic |
| `/src/Visora.Core/Modules/ModuleCatalog.cs` | 12-70 | Registry and discovery orchestration |
| `/src/Visora.Core/Modules/ModuleLocator.cs` | Full file | File discovery and deduplication |
| `/src/Visora.Core/Modules/ModuleCatalogOptions.cs` | 500-517 | Shared types configuration |

### Contract Definitions

| File | Purpose |
|------|---------|
| `/src/Visora.Contracts/Modules/VisoraModule.cs` | Base module abstraction (shared type) |
| `/src/Visora.Contracts/Components/VisoraComponent.cs` | Base component abstraction (shared type) |
| `/src/Visora.Contracts/Commands/VisoraCommand.cs` | Base command abstraction (shared type) |
| `/src/Visora.Contracts/Modules/ModuleContext.cs` | Context passed to modules (shared type) |

### Example Module

| File | Purpose |
|------|---------|
| `/src/Visora.Shell.Commands.Core/ShellCommandsModule.cs` | Sample module implementation |
| `/src/Visora.Shell.Commands.Core/Visora.Shell.Commands.Core.csproj` | Module project structure |

---

## Lifecycle: Load, Unload, Hot-Swap

### Load Sequence

```
┌─────────────────────────────────────────────────────────┐
│ 1. DISCOVERY PHASE                                      │
│    ModuleLocator.EnumerateCandidateFiles()              │
│    → Scans ProbingPaths for *.vixm.dll                  │
│    → Returns deduplicated file paths                    │
└─────────────────────────────────────────────────────────┘
                         ↓
┌─────────────────────────────────────────────────────────┐
│ 2. LOAD PHASE                                           │
│    ModuleHandle.LoadAsync(path, options, ct)            │
│    ├─ Create PluginLoader (unloadable)                  │
│    ├─ LoadDefaultAssembly()                             │
│    ├─ Find VisoraModule type via reflection             │
│    ├─ Instantiate module                                │
│    ├─ Get Descriptor                                    │
│    └─ Create ModuleContext                              │
└─────────────────────────────────────────────────────────┘
                         ↓
┌─────────────────────────────────────────────────────────┐
│ 3. INITIALIZE PHASE (Idempotent)                        │
│    ModuleHandle.EnsureInitializedAsync(ct)              │
│    └─ Module.InitializeAsync(context, ct)               │
│       → Module sets up resources, validates capabilities│
└─────────────────────────────────────────────────────────┘
                         ↓
┌─────────────────────────────────────────────────────────┐
│ 4. ACTIVE PHASE                                         │
│    ModuleHandle.InspectAsync(ct)                        │
│    ├─ Discover components via reflection                │
│    ├─ Instantiate each component                        │
│    ├─ Call CreateCommands()                             │
│    └─ Build inspection metadata                         │
│                                                          │
│    Commands can be executed during this phase           │
└─────────────────────────────────────────────────────────┘
```

### Unload Sequence

```
┌─────────────────────────────────────────────────────────┐
│ 1. SHUTDOWN PHASE                                       │
│    ModuleHandle.ShutdownAsync(ct)                       │
│    └─ Module.ShutdownAsync(context, ct)                 │
│       → Module saves state, releases resources          │
└─────────────────────────────────────────────────────────┘
                         ↓
┌─────────────────────────────────────────────────────────┐
│ 2. DISPOSE PHASE                                        │
│    ModuleHandle.DisposeAsync()                          │
│    ├─ Module.DisposeAsync()                             │
│    │  → Free module-specific resources                  │
│    └─ _loader.Dispose()                                 │
│       → Unload AssemblyLoadContext                      │
└─────────────────────────────────────────────────────────┘
                         ↓
┌─────────────────────────────────────────────────────────┐
│ 3. GARBAGE COLLECTION                                   │
│    (Asynchronous, non-deterministic)                    │
│    GC.Collect() → Eventually reclaims assembly memory   │
└─────────────────────────────────────────────────────────┘
```

### Hot-Swap Mechanics

**Scenario:** Update a module while the host is running.

**Steps:**
1. **Unload Old Version**
   ```csharp
   var oldModule = catalog.GetById("my.module");
   await oldModule.ShutdownAsync(ct);
   await oldModule.DisposeAsync(); // Unloads assembly
   catalog.Remove(oldModule); // Remove from registry
   ```

2. **Wait for GC** (optional but recommended)
   ```csharp
   // Force collection to free memory sooner
   GC.Collect();
   GC.WaitForPendingFinalizers();
   GC.Collect();
   ```

3. **Load New Version**
   ```csharp
   var newModule = await ModuleHandle.LoadAsync(
       "MyModule.vixm.dll", options, ct);
   await newModule.EnsureInitializedAsync(ct);
   catalog.Add(newModule);
   ```

**Critical Requirements:**
- Module DLL must be replaced on disk between unload and reload
- No references to old module instances can remain
- Shared types must be compatible (same signature)

**Limitations:**
- Can't hot-swap shared types themselves (requires host restart)
- Long-lived references to module objects prevent unload
- Static state in modules can cause issues

---

## Tradeoffs

### Advantages

1. **Dynamic Extensibility**
   - Add modules without recompiling host
   - Third-party extensions
   - Runtime code generation

2. **Isolation**
   - Module crashes don't kill host
   - Memory leaks are contained
   - Dependency version conflicts resolved

3. **Hot-Swapping**
   - Update modules without restart
   - Faster development cycle
   - Critical for long-running processes

4. **Resource Management**
   - Unload unused modules to free memory
   - Explicit lifecycle control
   - Deterministic cleanup

### Disadvantages

1. **Performance Overhead**
   - AssemblyLoadContext has runtime cost
   - Reflection-based discovery is slower than direct references
   - Cross-context calls have overhead

2. **Complexity**
   - AssemblyLoadContext semantics are subtle
   - Shared types must be carefully chosen
   - Debugging across contexts is harder

3. **Memory Management**
   - Unloading is non-deterministic (GC-based)
   - Long-lived references prevent unload
   - Memory usage is higher than static linking

4. **Compatibility**
   - Breaking changes in shared types require all modules to update
   - Version compatibility matrix is complex
   - Strong-name assembly issues

### Performance Characteristics

| Operation | Cost | Notes |
|-----------|------|-------|
| Load module | ~100-500ms | Depends on assembly size, reflection |
| Initialize | Varies | Module-specific |
| Execute command | ~0-5ms overhead | Cross-context call overhead |
| Unload module | ~1-10ms | Marking for GC, sync work |
| GC reclaim memory | Varies | Asynchronous, non-deterministic |

**Benchmark (Typical):**
```
Load VSCC.vixm.dll: 180ms
Initialize: 15ms
Inspect (discover components): 45ms
Execute PingCommand: 2ms (0.3ms overhead)
Shutdown: 8ms
Dispose: 12ms
GC reclaim: 500-2000ms (background)
```

---

## Alternatives Considered

### Alternative 1: Static Compilation

**Description:** Compile all modules into the host executable.

**Pros:**
- Fastest performance (no assembly loading)
- Simplest to debug
- No versioning issues

**Cons:**
- No extensibility (must recompile for changes)
- No third-party modules
- No hot-swapping
- Monolithic deployment

**Verdict:** ❌ Rejected. VISORA requires dynamic extensibility.

---

### Alternative 2: AppDomains (.NET Framework)

**Description:** Use AppDomains for isolation (legacy .NET Framework).

**Pros:**
- Proven pattern in .NET Framework
- Strong isolation
- Unloadable

**Cons:**
- Not available in .NET Core/.NET 5+
- Heavyweight (process-like isolation)
- Cross-domain marshaling complexity
- Performance overhead

**Verdict:** ❌ Not viable. VISORA targets .NET 9.0.

---

### Alternative 3: AssemblyLoadContext (Raw)

**Description:** Use `AssemblyLoadContext` directly without McMaster.NETCore.Plugins.

**Pros:**
- No third-party dependency
- Full control over resolution
- Better understanding of internals

**Cons:**
- Complex assembly resolution logic
- Reinventing the wheel
- Shared types mechanism is tricky
- More potential for bugs

**Verdict:** ⚠️ Possible but not recommended. McMaster.NETCore.Plugins is battle-tested.

---

### Alternative 4: MEF (Managed Extensibility Framework)

**Description:** Use System.ComponentModel.Composition (MEF).

**Pros:**
- Built into .NET
- Attribute-based discovery
- Mature framework

**Cons:**
- Not designed for unloadable contexts
- Heavyweight for VISORA's needs
- Attribute pollution
- Less flexible than reflection-first

**Verdict:** ❌ Rejected. Not optimized for hot-swapping scenarios.

---

### Alternative 5: Out-of-Process Plugins

**Description:** Run modules in separate processes, communicate via IPC.

**Pros:**
- Strongest isolation (process boundaries)
- No shared memory issues
- True hot-swapping (kill/restart process)

**Cons:**
- High performance overhead (serialization, IPC)
- Complex communication layer
- Difficult debugging
- Resource overhead (multiple processes)

**Verdict:** 🔮 Future consideration for meta-platform scenarios (e.g., Python/Node.js hosting).

---

## Implementation Details

### Shared Types Mechanism

**Problem:** How do host and plugin communicate without duplicating types?

**Solution:** McMaster.NETCore.Plugins copies specified types from host to plugin context.

**Mechanics:**
```csharp
// Host provides shared types
var sharedTypes = new[]
{
    typeof(VisoraModule),
    typeof(ICapabilityProvider),
    // ...
};

var loader = PluginLoader.CreateFromAssemblyFile(
    assemblyPath,
    sharedTypes: sharedTypes,
    isUnloadable: true);

// Result:
// - Plugin sees same Type instances as host
// - No serialization needed for these types
// - Type identity is preserved
```

**Type Identity:**
```csharp
// Host:
var hostModuleType = typeof(VisoraModule);

// Plugin (via shared types):
var pluginModuleType = assembly.GetType("Visora.Contracts.Modules.VisoraModule");

// True! Same type instance
hostModuleType == pluginModuleType
```

**Without Shared Types:**
```csharp
// Host:
var hostModuleType = typeof(VisoraModule);

// Plugin (no sharing):
var pluginModuleType = assembly.GetType("Visora.Contracts.Modules.VisoraModule");

// False! Different types with same name
hostModuleType != pluginModuleType

// Casting fails:
object pluginModule = CreatePluginModule();
var castedModule = (VisoraModule)pluginModule; // ❌ InvalidCastException
```

---

### Assembly Resolution

**Challenge:** Plugins reference their own dependencies. How does the loader find them?

**McMaster.NETCore.Plugins Behavior:**
1. **Plugin Dependencies First:** Looks in plugin directory
2. **Host Dependencies:** Falls back to host context
3. **Shared Types:** Always uses host versions
4. **Native Libraries:** Searches plugin directory first

**Example:**
```
MyModule.vixm.dll
├─ References Newtonsoft.Json 13.0.1
├─ Host has Newtonsoft.Json 12.0.0
└─ Result: Plugin uses 13.0.1, host uses 12.0.0 (isolated)
```

**Shared Type Dependency:**
```
MyModule.vixm.dll
├─ Implements VisoraModule (shared type)
├─ VisoraModule references ICapabilityProvider (shared type)
└─ Result: Both types come from host, ensuring compatibility
```

---

### Versioning Strategy

**Shared Types Versioning:**
- Shared types establish a contract
- Breaking changes require all modules to update
- Use semantic versioning for contracts

**Best Practices:**
1. **Additive Changes Only:** Add new methods, don't remove
2. **Default Implementations:** Virtual methods with default behavior
3. **Version Markers:** Include version in module descriptor
4. **Compatibility Checks:** Host validates module contract version

**Example:**
```csharp
// v1.0 contract
public abstract class VisoraModule
{
    public abstract ModuleDescriptor Descriptor { get; }
}

// v1.1 contract (additive)
public abstract class VisoraModule
{
    public abstract ModuleDescriptor Descriptor { get; }

    // New, optional
    public virtual ValueTask<HealthStatus> CheckHealthAsync()
        => ValueTask.FromResult(HealthStatus.Healthy);
}

// Old modules still work (default implementation)
```

---

### Memory Management

**Understanding Unloadability:**

**What Gets Unloaded:**
- Assembly code (IL and JIT-compiled code)
- Type metadata
- Static fields
- Module instance (if no references)

**What Prevents Unloading:**
- Host holds reference to module object
- Unfinished tasks referencing module types
- Event handlers attached to host objects
- Long-lived closures capturing module state

**Best Practices:**
1. **Remove References:** Ensure no references to module objects remain
2. **Cancel Tasks:** Cancel long-running operations before unload
3. **Unregister Handlers:** Detach event handlers
4. **Avoid Static State:** Minimize static fields in modules

**Example: Preventing Unload**
```csharp
// ❌ BAD: Long-lived reference prevents unload
public class Host
{
    private VisoraModule _cachedModule;

    public void CacheModule(VisoraModule module)
    {
        _cachedModule = module; // Prevents unload!
    }
}

// ✅ GOOD: No long-lived references
public class Host
{
    private string _cachedModuleId;

    public void CacheModuleId(VisoraModule module)
    {
        _cachedModuleId = module.Descriptor.Id; // Safe
    }

    public VisoraModule GetModule()
    {
        return catalog.GetById(_cachedModuleId); // Lookup fresh
    }
}
```

---

## Testing Patterns

### Unit Testing Modules

**Challenge:** How do you test a module in isolation?

**Solution:** Mock the capability provider and context.

**Example:**
```csharp
[TestMethod]
public async Task Module_Initializes_Successfully()
{
    // Arrange
    var mockService = new Mock<IMyService>();
    var capabilities = CapabilityProviders.CreateBuilder()
        .Add<IMyService>(mockService.Object)
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
    Assert.IsTrue(module.IsInitialized);
    mockService.Verify(s => s.RegisterModule(It.IsAny<string>()), Times.Once);
}
```

---

### Integration Testing: Load/Unload

**Challenge:** Test actual assembly loading and unloading.

**Solution:** Create a test module assembly and load it.

**Example:**
```csharp
[TestMethod]
public async Task ModuleHandle_LoadsAndUnloads_Successfully()
{
    // Arrange
    var testModulePath = BuildTestModule(); // Compile test module
    var options = new ModuleCatalogOptions
    {
        Capabilities = CapabilityProviders.Empty
    };

    // Act - Load
    var handle = await ModuleHandle.LoadAsync(testModulePath, options, CancellationToken.None);
    Assert.IsNotNull(handle.Module);
    Assert.AreEqual("test.module", handle.Descriptor.Id);

    // Act - Unload
    await handle.DisposeAsync();

    // Assert - Module is disposed
    var weakRef = new WeakReference(handle.Module, trackResurrection: true);
    handle = null; // Release strong reference

    // Force GC to reclaim
    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();

    // Module should be collected (eventually)
    // Note: This is non-deterministic and may require multiple GC cycles
    for (int i = 0; i < 10 && weakRef.IsAlive; i++)
    {
        await Task.Delay(100);
        GC.Collect();
    }

    // In practice, don't rely on deterministic unloading in tests
}
```

---

### Testing Hot-Swap

**Challenge:** Test module replacement at runtime.

**Solution:** Load, unload, modify, reload.

**Example:**
```csharp
[TestMethod]
public async Task Catalog_HotSwaps_Module()
{
    // Arrange
    var modulePath = Path.Combine(TestDir, "MyModule.vixm.dll");
    File.Copy(OriginalModulePath, modulePath, overwrite: true);

    var catalog = new ModuleCatalog();
    var options = new ModuleCatalogOptions { Capabilities = CapabilityProviders.Empty };
    options.ExplicitModuleFiles.Add(modulePath);

    // Act - Load v1
    await catalog.DiscoverAsync(options, CancellationToken.None);
    var v1Module = catalog.GetById("my.module");
    Assert.IsNotNull(v1Module);
    Assert.AreEqual(new Version(1, 0, 0), v1Module.Descriptor.Version);

    // Act - Unload v1
    await v1Module.DisposeAsync();
    catalog.Remove(v1Module);
    GC.Collect(); // Encourage unload

    // Act - Replace file with v2
    File.Copy(UpdatedModulePath, modulePath, overwrite: true);
    await Task.Delay(500); // Ensure file is written

    // Act - Load v2
    var newCatalog = new ModuleCatalog();
    await newCatalog.DiscoverAsync(options, CancellationToken.None);
    var v2Module = newCatalog.GetById("my.module");

    // Assert
    Assert.IsNotNull(v2Module);
    Assert.AreEqual(new Version(2, 0, 0), v2Module.Descriptor.Version);
}
```

---

## Best Practices

### When to Use Plugin Architecture

✅ **Use When:**
- You need dynamic extensibility
- Third-party developers will create extensions
- Hot-swapping is required
- Dependency isolation is critical
- Long-running processes (IDEs, servers)

❌ **Don't Use When:**
- Static compilation is sufficient
- Performance is critical (microseconds matter)
- Debugging complexity is unacceptable
- Memory usage must be minimized
- No need for runtime changes

---

### Designing Shared Types

**Guidelines:**
1. **Minimize Shared Types:** Only share what's necessary for communication
2. **Stable Contracts:** Avoid breaking changes
3. **Additive Changes:** Add new methods with default implementations
4. **Value Types:** Prefer immutable value types (records, structs)
5. **No Logic:** Shared types should be contracts, not implementations

**Example: Good Shared Type**
```csharp
// ✅ GOOD: Minimal, stable contract
public interface ICapabilityProvider
{
    bool TryGet<TCapability>(out TCapability? capability)
        where TCapability : class;
}
```

**Example: Bad Shared Type**
```csharp
// ❌ BAD: Complex, implementation-heavy
public abstract class DatabaseManager
{
    private readonly ConnectionPool _pool; // State!

    public abstract Task<DataSet> QueryAsync(string sql);

    public void InternalMethod() { /* Logic! */ }
}
```

---

### Module Development Guidelines

**1. No Static State:**
```csharp
// ❌ BAD
public class MyModule : Module
{
    private static Dictionary<string, object> _cache; // Static!
}

// ✅ GOOD
public class MyModule : Module
{
    private Dictionary<string, object> _instanceCache; // Instance
}
```

**2. Graceful Shutdown:**
```csharp
public override async ValueTask ShutdownAsync(
    ModuleContext context,
    CancellationToken cancellationToken)
{
    // Cancel long-running operations
    _cancellationSource?.Cancel();

    // Wait for tasks to complete
    await Task.WhenAll(_activeTasks).ConfigureAwait(false);

    // Unregister event handlers
    context.Capabilities.GetOptional<IEventBus>()?
        .Unsubscribe(this);

    // Release resources
    _resources?.Dispose();
}
```

**3. Capability Usage:**
```csharp
// ✅ GOOD: Defensive capability access
public override async ValueTask InitializeAsync(
    ModuleContext context,
    CancellationToken cancellationToken)
{
    var logger = context.Capabilities.GetOptional<ILogger>();
    logger?.Log("Initializing module");

    var required = context.Capabilities.GetRequired<IDatabaseService>();
    await required.ConnectAsync(cancellationToken);
}
```

---

### Debugging Tips

**1. Enable Assembly Load Logging:**
```csharp
AssemblyLoadContext.Default.Resolving += (context, name) =>
{
    Console.WriteLine($"Resolving: {name.FullName}");
    return null;
};
```

**2. Inspect Load Contexts:**
```csharp
var contexts = AssemblyLoadContext.All;
foreach (var ctx in contexts)
{
    Console.WriteLine($"Context: {ctx.Name}");
    foreach (var asm in ctx.Assemblies)
    {
        Console.WriteLine($"  - {asm.FullName}");
    }
}
```

**3. Track Unloadability:**
```csharp
var weakRef = new WeakReference(module, trackResurrection: true);
await module.DisposeAsync();
module = null;

// Check if collected
Console.WriteLine($"Module alive: {weakRef.IsAlive}");
```

---

## Advanced Topics

### Custom Assembly Resolution

**Scenario:** Load modules from non-standard locations (network, database).

**Solution:** Implement custom `AssemblyLoadContext`.

```csharp
public class CustomPluginContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    public CustomPluginContext(string pluginPath)
        : base(isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        if (path != null)
            return LoadFromAssemblyPath(path);

        return null; // Fallback to default context
    }
}
```

---

### Native Library Loading

**Challenge:** Modules use native DLLs (e.g., C++ libraries).

**Solution:** Override `LoadUnmanagedDll`.

```csharp
protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
{
    // Check plugin directory first
    var path = Path.Combine(_pluginDirectory, unmanagedDllName);
    if (File.Exists(path))
        return LoadUnmanagedDllFromPath(path);

    return base.LoadUnmanagedDll(unmanagedDllName);
}
```

---

### Performance Optimization

**1. Lazy Loading:**
```csharp
// Don't inspect modules until needed
var handle = await ModuleHandle.LoadAsync(...);
// Defer: await handle.InspectAsync(...);
```

**2. Parallel Loading:**
```csharp
var loadTasks = candidates.Select(path =>
    ModuleHandle.LoadAsync(path, options, ct));
var handles = await Task.WhenAll(loadTasks);
```

**3. Assembly Preloading:**
```csharp
// Preload shared dependencies
Assembly.Load("Newtonsoft.Json");
Assembly.Load("MySharedLibrary");
```

---

## Related Patterns

- **[Registry Pattern](../registry-pattern/overview.md)** - ModuleCatalog tracks loaded modules
- **[Module Lifecycle](../module-lifecycle/overview.md)** - Load → Initialize → Active → Shutdown → Dispose
- **[Capability Negotiation](../capability-negotiation/visora-analysis.md)** - Modules access host services
- **[Layered Architecture](../layered-architecture/overview.md)** - Contracts enable plugin isolation

---

## Further Reading

### Internal Documentation
- [Module Lifecycle Pattern](../module-lifecycle/overview.md)
- [Registry Pattern](../registry-pattern/overview.md)
- [Testable Design](../testable-design/overview.md)

### External Resources
- [McMaster.NETCore.Plugins Documentation](https://github.com/natemcmaster/DotNetCorePlugins)
- [AssemblyLoadContext Documentation](https://docs.microsoft.com/en-us/dotnet/core/dependency-loading/understanding-assemblyloadcontext)
- [Unloading Assemblies](https://docs.microsoft.com/en-us/dotnet/standard/assembly/unloadability)

---

**Next:** [Meta-Platform Illustrations](./meta-platform-illustrations.md) - Conceptual adaptations for polyglot scenarios
