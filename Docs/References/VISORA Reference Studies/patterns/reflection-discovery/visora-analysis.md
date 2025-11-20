# Reflection Discovery Pattern - VISORA Deep Dive

**Last Updated:** November 10, 2025
**VISORA Version:** .NET 9.0
**Pattern Tier:** Tier 1 (Foundational)
**Related Patterns:** Plugin Architecture, Module Lifecycle, Convention over Configuration

---

## Table of Contents

1. [Pattern Overview](#pattern-overview)
2. [Why Convention-Based Discovery?](#why-convention-based-discovery)
3. [VISORA Implementation](#visora-implementation)
4. [Code Examples](#code-examples)
5. [File References](#file-references)
6. [Convention Patterns](#convention-patterns)
7. [Filtering Strategies](#filtering-strategies)
8. [Performance Considerations](#performance-considerations)
9. [Alternatives Considered](#alternatives-considered)
10. [Testing Patterns](#testing-patterns)
11. [Best Practices](#best-practices)
12. [Advanced Topics](#advanced-topics)

---

## Pattern Overview

### What is Reflection Discovery?

**Definition:** A pattern where the host dynamically discovers and inspects module capabilities at runtime using reflection, guided by naming conventions and type inheritance checks, without requiring explicit manifest files or registration code.

**Key Characteristics:**
- **Convention-Based:** Uses naming patterns (`*.vixm.dll`) and type hierarchies
- **Dynamic Inspection:** Examines assemblies at runtime using `Assembly.GetTypes()`
- **Zero Configuration:** No XML/JSON manifests needed
- **Filtering:** Applies conventions to exclude non-candidate types
- **Deferred Evaluation:** Discovery happens lazily when needed

### Core Discovery Flow

```
┌─────────────────────────────────────────────────────────┐
│  1. File System Scan                                    │
│     Directory.EnumerateFiles("*.vixm.dll")              │
│                    ↓                                     │
│  2. Assembly Load                                       │
│     PluginLoader.CreateFromAssemblyFile(path)           │
│     assembly = loader.LoadDefaultAssembly()             │
│                    ↓                                     │
│  3. Type Discovery                                      │
│     assembly.GetTypes()                                 │
│     → Find VisoraModule implementation                  │
│                    ↓                                     │
│  4. Module Instantiation                                │
│     Activator.CreateInstance(moduleType)                │
│                    ↓                                     │
│  5. Component Discovery                                 │
│     module.DiscoverComponents(discoveryContext)         │
│     → context.EnumerateComponentCandidates()            │
│                    ↓                                     │
│  6. Type Filtering                                      │
│     Filter: !IsAbstract, !IsInterface, !IsNestedPrivate │
│     Filter: IsAssignableFrom(VisoraComponent)           │
│                    ↓                                     │
│  7. Component Instantiation                             │
│     Activator.CreateInstance(componentType)             │
│                    ↓                                     │
│  8. Command Discovery                                   │
│     component.CreateCommands(componentContext)          │
└─────────────────────────────────────────────────────────┘
```

---

## Why Convention-Based Discovery?

### Design Goals

1. **Zero Ceremony**
   - Developers shouldn't write boilerplate registration code
   - No XML/JSON manifests to maintain
   - Just implement the base class and it's discovered

2. **Self-Documenting**
   - `*.vixm.dll` naming makes modules obvious
   - Type hierarchy (`VisoraModule`, `VisoraComponent`) is self-explanatory
   - No hidden configuration files

3. **Flexibility**
   - Modules can override discovery logic
   - Custom filtering via `DiscoverComponents()`
   - Multiple components per module

4. **Maintainability**
   - Single source of truth: the code itself
   - No manifest/code synchronization issues
   - Refactoring is simpler

5. **AI-Friendly**
   - AI agents can generate modules without learning manifest formats
   - Conventions are easier to explain than configuration schemas
   - Less cognitive load

### Key Decision Points

**Why Not Manifest Files (XML/JSON)?**
- ❌ Requires synchronization between manifest and code
- ❌ Another file to maintain and version
- ❌ Harder to refactor (rename classes → update manifests)
- ❌ More verbose and error-prone

**Why Not Explicit Registration?**
```csharp
// ❌ NOT USED: Explicit registration approach
public class MyModule : VisoraModule
{
    public override void RegisterComponents(IComponentRegistry registry)
    {
        registry.Register<MyComponent1>();
        registry.Register<MyComponent2>();
        // Developers forget to add new components here
    }
}
```
- ❌ Easy to forget to register new components
- ❌ Boilerplate code in every module
- ❌ Doesn't provide value over convention

**Why Not Source Generators?**
- ⚠️ Compile-time generation doesn't work for runtime-loaded plugins
- ⚠️ Modules are compiled separately from the host
- ⚠️ Would still need runtime discovery of generator output
- ✅ Could be added later as an optimization (generate manifests at build time)

**Why Reflection is Acceptable:**
- ✅ Discovery happens once per module load (not per command execution)
- ✅ Results can be cached
- ✅ Performance cost is acceptable for flexibility gained
- ✅ Modern .NET reflection is fast enough for this use case

---

## VISORA Implementation

### Three-Level Discovery Hierarchy

```
Level 1: MODULE DISCOVERY
  ├─ Scan: *.vixm.dll files
  ├─ Load: Assembly via PluginLoader
  ├─ Find: typeof(VisoraModule).IsAssignableFrom(t)
  └─ Create: Activator.CreateInstance(moduleType)
       ↓
Level 2: COMPONENT DISCOVERY
  ├─ Call: module.DiscoverComponents(context)
  ├─ Scan: context.EnumerateComponentCandidates()
  ├─ Filter: !IsAbstract, !IsInterface, !IsNestedPrivate
  ├─ Filter: typeof(VisoraComponent).IsAssignableFrom(t)
  └─ Create: Activator.CreateInstance(componentType)
       ↓
Level 3: COMMAND DISCOVERY
  ├─ Call: component.CreateCommands(context)
  ├─ Method-Based: Component explicitly creates commands
  └─ No reflection needed (component controls this)
```

### Architecture

```
ModuleCatalog
  ├─ Uses: ModuleLocator.EnumerateCandidateFiles()
  │         └─ Returns: IEnumerable<string> (file paths)
  │
  ├─ For each file:
  │    └─ ModuleHandle.LoadAsync(path, options, token)
  │          ├─ Creates: PluginLoader (isolated context)
  │          ├─ Calls: loader.LoadDefaultAssembly()
  │          └─ Reflects: assembly.GetTypes() to find VisoraModule
  │
  └─ For inspection:
       └─ ModuleHandle.InspectAsync()
             ├─ Creates: ModuleDiscoveryContext
             ├─ Calls: module.DiscoverComponents(context)
             └─ For each component:
                   └─ Calls: component.CreateCommands(context)
```

---

## Code Examples

### Example 1: Module Discovery via Reflection

**File:** `/src/Visora.Core/Modules/ModuleHandle.cs:51-83`

```csharp
public static Task<ModuleHandle> LoadAsync(
    string assemblyPath,
    ModuleCatalogOptions options,
    CancellationToken cancellationToken)
{
    if (options is null) throw new ArgumentNullException(nameof(options));
    cancellationToken.ThrowIfCancellationRequested();

    var sharedTypes = options.GetSharedTypesArray();
    var loader = PluginLoader.CreateFromAssemblyFile(
        assemblyPath,
        sharedTypes: sharedTypes,
        isUnloadable: true);

    try
    {
        // STEP 1: Load the assembly
        var assembly = loader.LoadDefaultAssembly();

        // STEP 2: REFLECTION DISCOVERY - Find VisoraModule implementation
        var moduleType = assembly
            .GetTypes()  // ← Reflection: enumerate all types
            .FirstOrDefault(t => typeof(VisoraModule).IsAssignableFrom(t)
                && !t.IsAbstract);  // ← Convention: must be concrete

        if (moduleType is null)
            throw new InvalidOperationException(
                $"No VisoraModule implementation found in '{assemblyPath}'.");

        // STEP 3: Instantiate via reflection
        if (Activator.CreateInstance(moduleType) is not VisoraModule module)
            throw new InvalidOperationException(
                $"Unable to create module instance '{moduleType.FullName}'.");

        // STEP 4: Get descriptor
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

        return Task.FromResult(
            new ModuleHandle(assemblyPath, loader, assembly, module, context, options));
    }
    catch
    {
        loader.Dispose();
        throw;
    }
}
```

**Key Points:**
- **`assembly.GetTypes()`:** Reflection API to enumerate all types
- **`typeof(VisoraModule).IsAssignableFrom(t)`:** Check type hierarchy
- **`!t.IsAbstract`:** Filter out abstract base classes
- **`Activator.CreateInstance(moduleType)`:** Reflection-based instantiation
- **Error Handling:** Clear exceptions if no module found

---

### Example 2: Component Discovery via Convention

**File:** `/src/Visora.Contracts/Modules/ModuleDiscoveryContext.cs:23-32`

```csharp
/// <summary>
/// Provides helper data when a module is asked to describe its components.
/// </summary>
public sealed class ModuleDiscoveryContext
{
    public ModuleDiscoveryContext(Assembly moduleAssembly)
    {
        ModuleAssembly = moduleAssembly
            ?? throw new ArgumentNullException(nameof(moduleAssembly));
    }

    public Assembly ModuleAssembly { get; }

    /// <summary>
    /// Returns a basic reflection-driven component candidate set.
    /// </summary>
    public IEnumerable<Type> EnumerateComponentCandidates()
    {
        foreach (var type in ModuleAssembly.GetTypes())
        {
            // FILTERING CONVENTIONS:

            // Skip abstract classes (base classes, not instantiable)
            if (type.IsAbstract) continue;

            // Skip interfaces (not instantiable)
            if (type.IsInterface) continue;

            // Skip nested private types (implementation details)
            if (type.IsNestedPrivate) continue;

            // Only return types assignable to VisoraComponent
            if (typeof(VisoraComponent).IsAssignableFrom(type))
                yield return type;
        }
    }
}
```

**Key Points:**
- **`ModuleAssembly.GetTypes()`:** Second level of reflection
- **Convention Filters:** Multiple checks to exclude non-candidates
- **Yield Return:** Deferred evaluation for performance
- **Type Hierarchy Check:** `IsAssignableFrom(type)` pattern
- **Helper Method:** Modules can use this default or override

---

### Example 3: Default Component Discovery

**File:** `/src/Visora.Contracts/Modules/VisoraModule.cs:35-37`

```csharp
public abstract class VisoraModule : IAsyncDisposable
{
    public abstract ModuleDescriptor Descriptor { get; }

    public virtual ValueTask InitializeAsync(
        ModuleContext context,
        CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public virtual ValueTask ShutdownAsync(
        ModuleContext context,
        CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    /// <summary>
    /// Returns component types exposed by this module.
    /// DEFAULT IMPLEMENTATION: Uses reflection-based discovery.
    /// </summary>
    public virtual IEnumerable<Type> DiscoverComponents(
        ModuleDiscoveryContext context)
    {
        // DEFAULT: Use convention-based discovery
        return context.EnumerateComponentCandidates()
            .Where(t => typeof(VisoraComponent).IsAssignableFrom(t));
    }

    public virtual ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
```

**Key Points:**
- **Virtual Method:** Modules can override for custom discovery
- **Default Behavior:** Uses `EnumerateComponentCandidates()`
- **Additional Filtering:** Double-checks `IsAssignableFrom`
- **LINQ Query:** Declarative filtering

---

### Example 4: Full Inspection Flow

**File:** `/src/Visora.Core/Modules/ModuleHandle.cs:94-135`

```csharp
public async Task<ModuleInspection> InspectAsync(
    CancellationToken cancellationToken = default)
{
    // Ensure module is initialized
    await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

    // COMPONENT DISCOVERY
    var discoveryContext = new ModuleDiscoveryContext(Assembly);
    var componentTypes = Module.DiscoverComponents(discoveryContext).ToArray();
    var inspections = new List<ComponentInspection>();

    foreach (var componentType in componentTypes)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Instantiate component via reflection
        if (Activator.CreateInstance(componentType) is not VisoraComponent component)
            continue;

        var componentContext = new ComponentContext(Module, _context.Capabilities);
        await component.InitializeAsync(componentContext, cancellationToken)
            .ConfigureAwait(false);

        var descriptor = component.Descriptor;
        var commandInfos = new List<CommandInspection>();

        // COMMAND DISCOVERY (not reflection-based, component controls this)
        foreach (var command in component.CreateCommands(componentContext)
            ?? Array.Empty<VisoraCommand>())
        {
            if (command is null)
                continue;

            commandInfos.Add(new CommandInspection(
                command.Descriptor,
                command.GetType(),
                componentType));

            // Dispose command after inspection
            if (command is IAsyncDisposable asyncDisposable)
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            else if (command is IDisposable disposable)
                disposable.Dispose();
        }

        inspections.Add(new ComponentInspection(
            componentType,
            descriptor,
            commandInfos));

        // Dispose component after inspection
        if (component is IAsyncDisposable componentAsyncDisposable)
            await componentAsyncDisposable.DisposeAsync().ConfigureAwait(false);
        else if (component is IDisposable componentDisposable)
            componentDisposable.Dispose();
    }

    return new ModuleInspection(AssemblyPath, Descriptor, inspections);
}
```

**Key Points:**
- **Three-Level Discovery:** Module → Components → Commands
- **Lazy Instantiation:** Only create instances during inspection
- **Descriptor Extraction:** Get metadata from each level
- **Resource Cleanup:** Dispose inspection instances
- **Cancellation Support:** Check token between operations

---

### Example 5: Custom Discovery Logic

Modules can override `DiscoverComponents()` for custom logic:

```csharp
public class MyCustomModule : VisoraModule
{
    public override ModuleDescriptor Descriptor =>
        new("MyModule", "1.0.0", "Custom discovery example");

    public override IEnumerable<Type> DiscoverComponents(
        ModuleDiscoveryContext context)
    {
        // OPTION 1: Completely custom logic
        return context.ModuleAssembly.GetTypes()
            .Where(t => t.Namespace == "MyModule.Components")
            .Where(t => !t.IsAbstract)
            .Where(t => typeof(VisoraComponent).IsAssignableFrom(t));

        // OPTION 2: Filter default discovery
        var defaults = context.EnumerateComponentCandidates();
        return defaults.Where(t => !t.Name.EndsWith("Internal"));

        // OPTION 3: Explicit list (no reflection)
        return new[]
        {
            typeof(PublicComponent1),
            typeof(PublicComponent2)
        };
    }
}
```

---

## File References

### Core Discovery Files

1. **ModuleDiscoveryContext.cs**
   - Path: `/src/Visora.Contracts/Modules/ModuleDiscoveryContext.cs`
   - Lines: 11-33
   - Purpose: Provides `EnumerateComponentCandidates()` helper
   - Key Methods: `EnumerateComponentCandidates()`

2. **ModuleHandle.cs**
   - Path: `/src/Visora.Core/Modules/ModuleHandle.cs`
   - Lines: 51-83 (module discovery), 94-135 (inspection)
   - Purpose: Orchestrates assembly loading and type discovery
   - Key Methods: `LoadAsync()`, `InspectAsync()`

3. **VisoraModule.cs**
   - Path: `/src/Visora.Contracts/Modules/VisoraModule.cs`
   - Lines: 13-40
   - Purpose: Base class with default discovery implementation
   - Key Methods: `DiscoverComponents()`

4. **ModuleLocator.cs**
   - Path: `/src/Visora.Core/Modules/ModuleLocator.cs`
   - Lines: 8-52
   - Purpose: File system scanning for `*.vixm.dll` files
   - Key Methods: `EnumerateCandidateFiles()`

5. **ModuleCatalogOptions.cs**
   - Path: `/src/Visora.Core/Modules/ModuleCatalogOptions.cs`
   - Lines: 15-39
   - Purpose: Configuration for discovery (search paths, patterns)
   - Key Properties: `SearchPattern`, `ProbingPaths`

---

## Convention Patterns

### File Naming Convention

**Pattern:** `*.vixm.dll`
- **vixm** = "Visora IX Module"
- Examples:
  - `VSCC.vixm.dll`
  - `Visora.Shell.Commands.Core.vixm.dll`
  - `MyCustomModule.vixm.dll`

**Why This Pattern?**
- ✅ Clear distinction from regular assemblies
- ✅ Enables simple glob: `Directory.EnumerateFiles(dir, "*.vixm.dll")`
- ✅ Self-documenting in file system
- ✅ Prevents accidental loading of non-module DLLs

**Configuration:**
```csharp
var options = new ModuleCatalogOptions
{
    SearchPattern = "*.vixm.dll",  // Can be changed if needed
    RecurseSubdirectories = true
};
```

---

### Type Hierarchy Convention

**Module Discovery:**
```
Assembly.GetTypes()
  ├─ Filter: typeof(VisoraModule).IsAssignableFrom(type)
  ├─ Filter: !type.IsAbstract
  └─ Take: First match
```

**Component Discovery:**
```
Assembly.GetTypes()
  ├─ Filter: typeof(VisoraComponent).IsAssignableFrom(type)
  ├─ Filter: !type.IsAbstract
  ├─ Filter: !type.IsInterface
  ├─ Filter: !type.IsNestedPrivate
  └─ Return: All matches
```

**Type Hierarchy:**
```csharp
// Module hierarchy
public abstract class VisoraModule : IAsyncDisposable
    ↓
public class MyModule : VisoraModule  // ← Discoverable

// Component hierarchy
public abstract class VisoraComponent : IAsyncDisposable
    ↓
public class MyComponent : VisoraComponent  // ← Discoverable
```

---

### Filtering Conventions

**Standard Filters Applied:**

1. **`!type.IsAbstract`**
   - Excludes: Abstract base classes
   - Rationale: Cannot be instantiated
   - Example: `public abstract class BaseComponent` → excluded

2. **`!type.IsInterface`**
   - Excludes: Interface definitions
   - Rationale: Cannot be instantiated
   - Example: `public interface IComponent` → excluded

3. **`!type.IsNestedPrivate`**
   - Excludes: Private nested types
   - Rationale: Implementation details, not public components
   - Example:
     ```csharp
     public class MyComponent : VisoraComponent
     {
         private class InternalHelper : VisoraComponent { }  // ← excluded
     }
     ```

4. **`typeof(VisoraComponent).IsAssignableFrom(type)`**
   - Includes: Types that inherit from `VisoraComponent`
   - Rationale: Type safety, only valid components
   - Example: `public class MyComponent : VisoraComponent` → included

**Filter Execution Order:**
```csharp
public IEnumerable<Type> EnumerateComponentCandidates()
{
    foreach (var type in ModuleAssembly.GetTypes())
    {
        if (type.IsAbstract) continue;        // Fastest check first
        if (type.IsInterface) continue;       // Second fastest
        if (type.IsNestedPrivate) continue;   // Third fastest
        if (typeof(VisoraComponent).IsAssignableFrom(type))  // Slowest (type comparison)
            yield return type;
    }
}
```

---

## Filtering Strategies

### Performance-Optimized Filtering

**Principle:** Fast checks first, expensive checks last

```csharp
// ✅ GOOD: Cheap checks eliminate most types early
foreach (var type in assembly.GetTypes())
{
    if (type.IsAbstract) continue;     // Property access: ~1ns
    if (type.IsInterface) continue;    // Property access: ~1ns
    if (type.IsNestedPrivate) continue; // Property access: ~1ns

    // Expensive check only on remaining candidates
    if (typeof(VisoraComponent).IsAssignableFrom(type))  // Type comparison: ~10-100ns
        yield return type;
}

// ❌ BAD: Expensive check first
foreach (var type in assembly.GetTypes())
{
    // This runs for EVERY type, even ones that would be filtered by cheap checks
    if (typeof(VisoraComponent).IsAssignableFrom(type))
    {
        if (!type.IsAbstract && !type.IsInterface)
            yield return type;
    }
}
```

---

### Custom Filtering Strategies

**Strategy 1: Namespace Filtering**
```csharp
public override IEnumerable<Type> DiscoverComponents(
    ModuleDiscoveryContext context)
{
    return context.EnumerateComponentCandidates()
        .Where(t => t.Namespace?.StartsWith("MyModule.Public") == true);
}
```

**Strategy 2: Attribute-Based Discovery**
```csharp
[AttributeUsage(AttributeTargets.Class)]
public class ExportComponentAttribute : Attribute { }

public override IEnumerable<Type> DiscoverComponents(
    ModuleDiscoveryContext context)
{
    return context.EnumerateComponentCandidates()
        .Where(t => t.GetCustomAttribute<ExportComponentAttribute>() != null);
}
```

**Strategy 3: Explicit Exclusion**
```csharp
public override IEnumerable<Type> DiscoverComponents(
    ModuleDiscoveryContext context)
{
    var exclusions = new HashSet<Type>
    {
        typeof(InternalComponent),
        typeof(ExperimentalComponent)
    };

    return context.EnumerateComponentCandidates()
        .Where(t => !exclusions.Contains(t));
}
```

**Strategy 4: Conditional Discovery**
```csharp
public override IEnumerable<Type> DiscoverComponents(
    ModuleDiscoveryContext context)
{
    var candidates = context.EnumerateComponentCandidates().ToList();

    // Only expose advanced components if environment variable set
    if (Environment.GetEnvironmentVariable("ENABLE_ADVANCED") == "true")
    {
        return candidates;
    }
    else
    {
        return candidates.Where(t => !t.Name.Contains("Advanced"));
    }
}
```

---

## Performance Considerations

### Reflection Costs

**Cost Breakdown:**

| Operation | Cost (Approximate) | Frequency |
|-----------|-------------------|-----------|
| `Directory.EnumerateFiles()` | 1-10ms per directory | Once per catalog load |
| `Assembly.Load()` | 5-50ms per assembly | Once per module |
| `Assembly.GetTypes()` | 0.1-1ms per assembly | Once per module + once per component discovery |
| `Type.IsAbstract` check | ~1ns per type | Per type in assembly |
| `Type.IsAssignableFrom()` | ~10-100ns per type | Per candidate type |
| `Activator.CreateInstance()` | ~100-1000ns per instance | Once per module/component/command |

**Total Cost Example:**
- Assembly with 100 types
- 10 component candidates
- 3 actual components
- 5 commands total

```
GetTypes(): 0.5ms
Type checks (100 types × 100ns): 0.01ms
CreateInstance (3 components): 0.003ms
CreateInstance (5 commands): 0.005ms
─────────────────────────────────────
Total: ~0.52ms
```

**Conclusion:** Negligible for most scenarios (< 1ms per module).

---

### Caching Strategies

**Strategy 1: Cache Type Discovery Results**

```csharp
public class CachedModule : VisoraModule
{
    private Type[]? _cachedComponents;

    public override IEnumerable<Type> DiscoverComponents(
        ModuleDiscoveryContext context)
    {
        if (_cachedComponents is null)
        {
            _cachedComponents = context.EnumerateComponentCandidates()
                .ToArray();  // Cache results
        }

        return _cachedComponents;
    }
}
```

**Strategy 2: Lazy Initialization**

```csharp
public class LazyDiscoveryModule : VisoraModule
{
    private readonly Lazy<Type[]> _components;

    public LazyDiscoveryModule()
    {
        _components = new Lazy<Type[]>(() =>
        {
            // Discovery logic here
            return new[] { typeof(MyComponent) };
        });
    }

    public override IEnumerable<Type> DiscoverComponents(
        ModuleDiscoveryContext context)
    {
        return _components.Value;  // Thread-safe lazy initialization
    }
}
```

**Strategy 3: Host-Level Caching**

```csharp
// ModuleCatalog could cache inspection results
var inspection = await moduleHandle.InspectAsync();
_inspectionCache[moduleHandle.AssemblyPath] = inspection;

// Reuse cached inspection instead of re-inspecting
```

---

### Deferred Evaluation

**Why `yield return` is Important:**

```csharp
// ✅ GOOD: Deferred evaluation with yield
public IEnumerable<Type> EnumerateComponentCandidates()
{
    foreach (var type in ModuleAssembly.GetTypes())
    {
        if (type.IsAbstract) continue;
        if (typeof(VisoraComponent).IsAssignableFrom(type))
            yield return type;  // Only evaluated when enumerated
    }
}

// Caller can stop early
var first = EnumerateComponentCandidates().FirstOrDefault();
// If first match found, remaining types aren't checked

// ❌ BAD: Eager evaluation
public IEnumerable<Type> EnumerateComponentCandidates()
{
    var results = new List<Type>();
    foreach (var type in ModuleAssembly.GetTypes())
    {
        if (!type.IsAbstract && typeof(VisoraComponent).IsAssignableFrom(type))
            results.Add(type);
    }
    return results;  // All types checked even if caller only needs first
}
```

**Benefits:**
- ✅ Caller can stop enumeration early
- ✅ Lower memory allocation (no intermediate list)
- ✅ Better composability with LINQ

---

## Alternatives Considered

### Alternative 1: XML Manifest Files

**Example:**
```xml
<!-- MyModule.vixm.xml -->
<VisoraModule>
  <Descriptor>
    <Name>MyModule</Name>
    <Version>1.0.0</Version>
  </Descriptor>
  <Components>
    <Component type="MyModule.Components.Component1" />
    <Component type="MyModule.Components.Component2" />
  </Components>
</VisoraModule>
```

**Pros:**
- ✅ No reflection needed
- ✅ Fast parsing
- ✅ Can include metadata not in code

**Cons:**
- ❌ Synchronization issues (code changes → update manifest)
- ❌ Verbose and error-prone
- ❌ Refactoring difficulty (rename class → update manifest)
- ❌ Another file to maintain

**Verdict:** ❌ Rejected - Too much ceremony, maintenance burden

---

### Alternative 2: JSON Manifest Files

**Example:**
```json
{
  "module": {
    "name": "MyModule",
    "version": "1.0.0"
  },
  "components": [
    "MyModule.Components.Component1",
    "MyModule.Components.Component2"
  ]
}
```

**Pros:**
- ✅ Less verbose than XML
- ✅ Easy to parse
- ✅ Human-readable

**Cons:**
- ❌ Same synchronization issues as XML
- ❌ Still requires maintenance
- ❌ Refactoring difficulty

**Verdict:** ❌ Rejected - Same problems as XML

---

### Alternative 3: Source Generators

**Example:**
```csharp
// At compile time, source generator creates:
partial class MyModule
{
    public override IEnumerable<Type> DiscoverComponents(
        ModuleDiscoveryContext context)
    {
        // Generated at compile time via analysis
        return new[]
        {
            typeof(Component1),
            typeof(Component2)
        };
    }
}
```

**Pros:**
- ✅ No runtime reflection
- ✅ Compile-time validation
- ✅ Better performance

**Cons:**
- ⚠️ Doesn't work for runtime-loaded plugins (different compilation units)
- ⚠️ Modules compiled separately from host
- ⚠️ Would generate code modules can't reference
- ⚠️ Complex tooling

**Verdict:** ⚠️ Possible Future Enhancement - Could generate manifests, but doesn't eliminate runtime discovery

---

### Alternative 4: Explicit Registration API

**Example:**
```csharp
public class MyModule : VisoraModule
{
    public override IEnumerable<Type> DiscoverComponents(
        ModuleDiscoveryContext context)
    {
        // Manual registration
        return new[]
        {
            typeof(Component1),
            typeof(Component2)
        };
    }
}
```

**Pros:**
- ✅ No reflection
- ✅ Explicit and clear
- ✅ Full control

**Cons:**
- ❌ Boilerplate in every module
- ❌ Easy to forget new components
- ❌ Doesn't provide value over convention

**Verdict:** ✅ Available as Override - Modules can do this if needed, but default uses reflection

---

## Testing Patterns

### Pattern 1: Testing Discovery Without Loading

**Challenge:** Test discovery logic without actually loading assemblies

```csharp
[Fact]
public void EnumerateComponentCandidates_FiltersCorrectly()
{
    // Create a test assembly in memory
    var assemblyName = new AssemblyName("TestModule");
    var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(
        assemblyName,
        AssemblyBuilderAccess.Run);
    var moduleBuilder = assemblyBuilder.DefineDynamicModule("TestModule");

    // Define test types
    var abstractType = moduleBuilder.DefineType(
        "AbstractComponent",
        TypeAttributes.Public | TypeAttributes.Abstract,
        typeof(VisoraComponent));

    var concreteType = moduleBuilder.DefineType(
        "ConcreteComponent",
        TypeAttributes.Public,
        typeof(VisoraComponent));

    abstractType.CreateType();
    concreteType.CreateType();

    // Test discovery
    var context = new ModuleDiscoveryContext(assemblyBuilder);
    var candidates = context.EnumerateComponentCandidates().ToList();

    // Should find concrete but not abstract
    Assert.Single(candidates);
    Assert.Equal("ConcreteComponent", candidates[0].Name);
}
```

---

### Pattern 2: Testing Custom Discovery Logic

```csharp
public class CustomModule : VisoraModule
{
    public override ModuleDescriptor Descriptor =>
        new("Custom", "1.0", "Test");

    public override IEnumerable<Type> DiscoverComponents(
        ModuleDiscoveryContext context)
    {
        // Custom logic to test
        return context.EnumerateComponentCandidates()
            .Where(t => t.Namespace == "MyModule.Public");
    }
}

[Fact]
public async Task CustomDiscovery_FiltersToPublicNamespace()
{
    // Create test module assembly with types in different namespaces
    // ...

    var module = new CustomModule();
    var context = new ModuleDiscoveryContext(testAssembly);
    var components = module.DiscoverComponents(context).ToList();

    // Verify only public namespace types returned
    Assert.All(components, c => Assert.StartsWith("MyModule.Public", c.Namespace));
}
```

---

### Pattern 3: Integration Testing Full Discovery

```csharp
[Fact]
public async Task ModuleCatalog_DiscoversAllModules()
{
    var options = new ModuleCatalogOptions();
    options.ProbingPaths.Add(TestModulesDirectory);
    options.SearchPattern = "*.test.vixm.dll";

    var catalog = new ModuleCatalog(options);
    var modules = await catalog.LoadModulesAsync();

    Assert.NotEmpty(modules);
    Assert.All(modules, m => Assert.NotNull(m.Descriptor));

    // Test component discovery for each module
    foreach (var module in modules)
    {
        var inspection = await module.InspectAsync();
        Assert.NotNull(inspection);
        // Verify expected components found
    }
}
```

---

### Pattern 4: Mocking Discovery Context

```csharp
[Fact]
public void Module_UsesDiscoveryContext()
{
    var mockAssembly = new Mock<Assembly>();
    mockAssembly.Setup(a => a.GetTypes())
        .Returns(new[]
        {
            typeof(TestComponent1),
            typeof(TestComponent2)
        });

    var context = new ModuleDiscoveryContext(mockAssembly.Object);
    var module = new MyModule();

    var components = module.DiscoverComponents(context).ToList();

    Assert.Equal(2, components.Count);
}
```

---

## Best Practices

### 1. Design for Convention-Based Discovery

```csharp
// ✅ GOOD: Follow conventions, no custom logic needed
public class MyModule : VisoraModule
{
    public override ModuleDescriptor Descriptor =>
        new("MyModule", "1.0.0", "Standard module");

    // Components are discovered automatically
    // No DiscoverComponents override needed
}

public class StandardComponent : VisoraComponent
{
    public override ComponentDescriptor Descriptor =>
        new("Standard", "Does standard things");
}

// ❌ BAD: Fighting conventions
public class WeirdModule : VisoraModule
{
    public override IEnumerable<Type> DiscoverComponents(
        ModuleDiscoveryContext context)
    {
        // Overly complex custom logic
        return context.ModuleAssembly.GetTypes()
            .Where(t => t.GetCustomAttribute<MySpecialAttribute>() != null)
            .Where(t => t.Name.Contains("Component"))
            .Where(t => !t.Namespace.Contains("Internal"))
            // ... etc
    }
}
```

---

### 2. Override Discovery Only When Necessary

**When to Override:**
- ✅ Need to exclude specific components
- ✅ Want namespace-based organization
- ✅ Have conditional component registration
- ✅ Need attribute-based metadata

**When NOT to Override:**
- ❌ All components should be discovered
- ❌ Standard naming and hierarchy
- ❌ No special filtering needed

```csharp
// ✅ GOOD: Specific reason to override
public class ConditionalModule : VisoraModule
{
    public override IEnumerable<Type> DiscoverComponents(
        ModuleDiscoveryContext context)
    {
        var all = context.EnumerateComponentCandidates();

        // Exclude experimental components in production
        if (IsProduction)
        {
            return all.Where(t => !t.Namespace.Contains("Experimental"));
        }

        return all;
    }
}
```

---

### 3. Use Descriptive Naming

```csharp
// ✅ GOOD: Clear names that indicate purpose
public class TextEditorComponent : VisoraComponent { }
public class FileSystemComponent : VisoraComponent { }
public class GitIntegrationComponent : VisoraComponent { }

// ❌ BAD: Unclear names
public class Thing1 : VisoraComponent { }
public class Helper : VisoraComponent { }
public class Util : VisoraComponent { }
```

---

### 4. Avoid Nested Public Components

```csharp
// ✅ GOOD: Top-level components
namespace MyModule.Components
{
    public class EditorComponent : VisoraComponent { }
    public class ViewerComponent : VisoraComponent { }
}

// ❌ BAD: Nested public components (confusing)
public class EditorComponent : VisoraComponent
{
    // This will also be discovered!
    public class NestedComponent : VisoraComponent { }

    // Use private nested if needed
    private class InternalHelper : VisoraComponent { }  // Not discovered
}
```

---

### 5. Keep Discovery Fast

```csharp
// ✅ GOOD: Simple, fast filters
public override IEnumerable<Type> DiscoverComponents(
    ModuleDiscoveryContext context)
{
    return context.EnumerateComponentCandidates()
        .Where(t => t.Namespace == "MyModule.Public");
}

// ❌ BAD: Expensive operations during discovery
public override IEnumerable<Type> DiscoverComponents(
    ModuleDiscoveryContext context)
{
    return context.EnumerateComponentCandidates()
        .Where(t =>
        {
            // Don't do I/O during discovery!
            var configFile = Path.Combine(GetConfigPath(), t.Name + ".json");
            return File.Exists(configFile);
        });
}
```

---

## Advanced Topics

### Topic 1: Multiple Modules per Assembly

**Current Implementation:** Finds first `VisoraModule` implementation

```csharp
// In ModuleHandle.LoadAsync()
var moduleType = assembly
    .GetTypes()
    .FirstOrDefault(t => typeof(VisoraModule).IsAssignableFrom(t)
        && !t.IsAbstract);  // ← Takes FIRST match only
```

**Limitation:** Only one module per `.vixm.dll` assembly

**Workaround:** If you need multiple modules, create multiple assemblies

```
MyModuleSet/
  ├─ Module1.vixm.dll
  ├─ Module2.vixm.dll
  └─ Module3.vixm.dll
```

**Future Enhancement:** Could support multiple modules per assembly:

```csharp
// Potential future implementation
var moduleTypes = assembly
    .GetTypes()
    .Where(t => typeof(VisoraModule).IsAssignableFrom(t) && !t.IsAbstract)
    .ToArray();

foreach (var moduleType in moduleTypes)
{
    // Create separate ModuleHandle for each
}
```

---

### Topic 2: Conditional Component Discovery

**Use Case:** Different components for different environments

```csharp
public class EnvironmentAwareModule : VisoraModule
{
    public override ModuleDescriptor Descriptor =>
        new("EnvAware", "1.0", "Environment-specific components");

    public override IEnumerable<Type> DiscoverComponents(
        ModuleDiscoveryContext context)
    {
        var all = context.EnumerateComponentCandidates().ToList();

        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

        return environment switch
        {
            "Development" => all,  // All components in dev
            "Staging" => all.Where(t => !t.Name.Contains("Debug")),
            "Production" => all.Where(t => !t.Name.Contains("Debug"))
                               .Where(t => !t.Name.Contains("Experimental")),
            _ => Enumerable.Empty<Type>()
        };
    }
}
```

---

### Topic 3: Attribute-Based Metadata

**Use Case:** Additional discovery metadata via attributes

```csharp
[AttributeUsage(AttributeTargets.Class)]
public class ComponentMetadataAttribute : Attribute
{
    public string Category { get; set; }
    public bool RequiresLicense { get; set; }
}

[ComponentMetadata(Category = "Editor", RequiresLicense = true)]
public class PremiumEditorComponent : VisoraComponent
{
    // ...
}

public class MetadataAwareModule : VisoraModule
{
    public override IEnumerable<Type> DiscoverComponents(
        ModuleDiscoveryContext context)
    {
        return context.EnumerateComponentCandidates()
            .Where(t =>
            {
                var metadata = t.GetCustomAttribute<ComponentMetadataAttribute>();
                if (metadata?.RequiresLicense == true)
                {
                    return HasValidLicense();
                }
                return true;
            });
    }

    private bool HasValidLicense() => /* license check */;
}
```

---

### Topic 4: Discovery Performance Profiling

**Measuring Discovery Cost:**

```csharp
public class ProfilingModule : VisoraModule
{
    public override ModuleDescriptor Descriptor =>
        new("Profiling", "1.0", "Performance monitoring");

    public override IEnumerable<Type> DiscoverComponents(
        ModuleDiscoveryContext context)
    {
        var sw = Stopwatch.StartNew();

        var candidates = context.EnumerateComponentCandidates().ToList();

        sw.Stop();
        Console.WriteLine($"Discovery took {sw.ElapsedMilliseconds}ms, " +
                         $"found {candidates.Count} components");

        return candidates;
    }
}
```

**Optimization Techniques:**

1. **Cache Results:** Store discovery results after first call
2. **Parallel Processing:** Use `Parallel.ForEach` for large assemblies (rare)
3. **Pre-compute Metadata:** Generate discovery data at build time
4. **Lazy Discovery:** Only discover when inspection requested

---

### Topic 5: Type Loading Optimization

**Problem:** `Assembly.GetTypes()` loads all types, even if not needed

**Solution:** Use `Assembly.DefinedTypes` or `Assembly.GetExportedTypes()`

```csharp
// Standard approach
public IEnumerable<Type> EnumerateComponentCandidates()
{
    foreach (var type in ModuleAssembly.GetTypes())  // Loads ALL types
    {
        if (typeof(VisoraComponent).IsAssignableFrom(type))
            yield return type;
    }
}

// Optimized approach (only public types)
public IEnumerable<Type> EnumerateComponentCandidatesOptimized()
{
    foreach (var type in ModuleAssembly.GetExportedTypes())  // Only public types
    {
        if (typeof(VisoraComponent).IsAssignableFrom(type))
            yield return type;
    }
}
```

**Tradeoff:**
- `GetExportedTypes()`: Faster, but only finds public types
- `GetTypes()`: Slower, but finds all types (including internal)

**VISORA Choice:** Uses `GetTypes()` to support internal components

---

## Summary

### Key Takeaways

1. **Convention-Based Discovery**
   - VISORA uses reflection to discover modules, components, and commands
   - Conventions reduce boilerplate and maintenance burden
   - File naming (`*.vixm.dll`) and type hierarchy (`VisoraModule`, `VisoraComponent`)

2. **Three-Level Discovery**
   - **Level 1:** File system → `*.vixm.dll` files
   - **Level 2:** Assembly → `VisoraModule` implementation
   - **Level 3:** Module → `VisoraComponent` implementations

3. **Performance**
   - Reflection cost is negligible (< 1ms per module)
   - Deferred evaluation via `yield return`
   - Caching strategies available for optimization

4. **Flexibility**
   - Default behavior works for 90% of cases
   - Override `DiscoverComponents()` for custom logic
   - Support for filtering, attributes, conditional discovery

5. **Alternatives Rejected**
   - Manifest files (XML/JSON) → too much maintenance
   - Explicit registration → unnecessary boilerplate
   - Source generators → don't work for runtime plugins

---

### When to Use This Pattern

✅ **Use Convention-Based Discovery When:**
- Building plugin/module systems
- Want zero-ceremony extensibility
- Need runtime discovery of capabilities
- Supporting AI-generated modules

❌ **Consider Alternatives When:**
- Compile-time discovery is sufficient
- Performance is absolutely critical (< 1ms not acceptable)
- Need complex conditional logic better expressed in config
- Security requires explicit allow-lists (not discovery)

---

### Further Reading

- **Related Pattern:** Plugin Architecture - How modules are loaded and isolated
- **Related Pattern:** Module Lifecycle - What happens after discovery
- **Related Pattern:** Context Objects - How discovery context is designed
- **Implementation:** `/src/Visora.Core/Modules/ModuleHandle.cs`
- **Contracts:** `/src/Visora.Contracts/Modules/`
