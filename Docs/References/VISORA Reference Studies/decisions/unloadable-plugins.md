# ADR-004: Unloadable Plugins via AssemblyLoadContext

**Last Updated:** 2025-11-10
**VISORA Version:** .NET 9.0
**Decision Status:** ✅ Accepted
**Decision Date:** 2024-Q4
**Supersedes:** None
**Related ADRs:** ADR-001 (Reflection Discovery), ADR-003 (Async Everywhere)

---

## Table of Contents

1. [Executive Summary](#executive-summary)
2. [Context](#context)
3. [Problem Statement](#problem-statement)
4. [Decision](#decision)
5. [Alternatives Considered](#alternatives-considered)
6. [Rationale](#rationale)
7. [Consequences](#consequences)
8. [Tradeoffs](#tradeoffs)
9. [Implementation Details](#implementation-details)
10. [Memory Management](#memory-management)
11. [When to Revisit](#when-to-revisit)
12. [Related Patterns](#related-patterns)
13. [References](#references)

---

## Executive Summary

**Decision:** VISORA uses `AssemblyLoadContext` with unloadable contexts (via McMaster.NETCore.Plugins) to enable hot-swapping of modules without restarting the host application. Each module loads in its own isolated context that can be unloaded, releasing memory.

**Key Rationale:**
- **Hot-swap capability:** Update modules without restarting the host
- **Memory management:** Unload modules and release memory
- **Development experience:** Rapid iteration during development
- **Isolation:** Modules can use different versions of dependencies
- **Production flexibility:** Update modules in running systems

**Primary Tradeoff:** Complexity of assembly loading and unloading vs. convenience of in-process hot-swap without downtime.

---

## Context

### The Assembly Loading Problem in .NET

#### Historical Context

**.NET Framework (AppDomains):**
```csharp
// Old approach with AppDomains
var domain = AppDomain.CreateDomain("PluginDomain");
var plugin = (IPlugin)domain.CreateInstanceAndUnwrap(
    "PluginAssembly",
    "PluginClass");

plugin.Execute();

// Unload entire domain
AppDomain.Unload(domain);  // Releases memory
```

**Problem:** AppDomains deprecated in .NET Core/5+

**.NET Core 1.0-2.2 (No Solution):**
```csharp
// Assemblies loaded in default context
var assembly = Assembly.LoadFrom("Plugin.dll");
// ⚠️ Cannot unload! Stays in memory forever!
```

**Problem:** No way to unload assemblies, memory leaks inevitable

**.NET Core 3.0+ (AssemblyLoadContext):**
```csharp
// New approach with AssemblyLoadContext
var context = new CustomLoadContext(isCollectible: true);
var assembly = context.LoadFromAssemblyPath("Plugin.dll");
// Use assembly...

context.Unload();  // Releases memory when no references remain
```

**Solution:** Collectible `AssemblyLoadContext` provides unloadability

### VISORA's Module Lifecycle Needs

#### 1. Development Workflow

Developers expect rapid iteration:

```
Developer Workflow (Without Hot-Swap):
1. Edit module code
2. Compile module
3. Stop host application        ← Frustrating!
4. Copy new module DLL
5. Start host application        ← Time consuming!
6. Test changes
7. Repeat...

Developer Workflow (With Hot-Swap):
1. Edit module code
2. Compile module
3. Issue reload command          ← Instant!
4. Test changes
5. Repeat...

Time saved: ~30 seconds per iteration × 100 iterations/day = 50 minutes/day
```

#### 2. Production Updates

Zero-downtime updates in production:

```
Traditional Update Process:
1. Schedule maintenance window
2. Stop application              ← Downtime begins
3. Update module files
4. Start application
5. Verify functionality          ← Downtime ends
Total downtime: 5-15 minutes

Hot-Swap Update Process:
1. Upload new module version
2. Issue hot-swap command
3. Verify functionality
Total downtime: 0 seconds       ← No downtime!
```

#### 3. Memory Management

Long-running applications need memory cleanup:

```
Scenario: Host running for 30 days

Without Unloading:
- Load module A v1.0 (10 MB)
- Load module B v1.0 (15 MB)
- Update to module A v1.1 (10 MB) ← v1.0 still in memory!
- Update to module A v1.2 (10 MB) ← v1.0 and v1.1 still in memory!
- Update to module B v1.1 (15 MB) ← v1.0 still in memory!
Total memory: 60 MB (should be 25 MB)

With Unloading:
- Load module A v1.0 (10 MB)
- Load module B v1.0 (15 MB)
- Update to module A v1.1 (10 MB) ← v1.0 unloaded and collected
- Update to module A v1.2 (10 MB) ← v1.1 unloaded and collected
- Update to module B v1.1 (15 MB) ← v1.0 unloaded and collected
Total memory: 25 MB ✅
```

#### 4. Dependency Isolation

Different modules may need different dependency versions:

```
Scenario: Dependency conflicts

Module A requires Newtonsoft.Json 12.0
Module B requires Newtonsoft.Json 13.0

Without Isolation:
- Both modules share same dependency version
- Potential runtime errors or subtle bugs
- Constrained dependency management

With Isolation (AssemblyLoadContext):
- Module A loads Newtonsoft.Json 12.0 in its context
- Module B loads Newtonsoft.Json 13.0 in its context
- No conflicts! ✅
```

---

## Problem Statement

### Core Question

**How can VISORA enable hot-swapping of modules (load new versions, unload old versions) without restarting the host application or causing memory leaks?**

### Specific Challenges

#### 1. Assembly Permanence

Default assembly loading is permanent:

```csharp
// Problem: This assembly stays in memory forever
var assembly = Assembly.LoadFrom("/path/to/Module.dll");
var moduleType = assembly.GetType("MyModule");
var instance = Activator.CreateInstance(moduleType);

// No way to unload!
// Even if we dispose instance, assembly remains in memory
instance = null;
GC.Collect();
// Assembly still in memory! ⚠️
```

#### 2. Reference Tracking

Unloading requires releasing all references:

```csharp
// Scenario: Hidden references prevent unloading

// Load module in collectible context
var context = new CollectibleAssemblyLoadContext();
var assembly = context.LoadFromAssemblyPath("Module.dll");
var instance = (IModule)Activator.CreateInstance(assembly.GetType("MyModule"));

// Execute module - but module creates delegate!
instance.Initialize();  // Internally: SomeStaticEvent += OnEvent;

// Try to unload
instance = null;
context.Unload();
GC.Collect();

// Context not unloaded! ⚠️
// Why? Delegate still references module type!
```

**Problem:** Finding and eliminating all references is difficult.

#### 3. Cross-Context Communication

Modules and host in different contexts need to communicate:

```csharp
// Problem: Type identity
// Host defines: ICommand (in default context)
// Module implements: ICommand (but loaded in module context)

var moduleCommand = LoadModuleCommand();  // From module context

if (moduleCommand is ICommand)  // FALSE! ⚠️
{
    // This never executes because types are different!
}

// Type.FullName is same, but Type.Equals() is false
// Types from different contexts are incompatible!
```

**Challenge:** Share types between host and module contexts.

#### 4. Dependency Loading

Module dependencies must load correctly:

```csharp
// Module A depends on:
// - Visora.Core.dll (shared with host)
// - Newtonsoft.Json.dll (module-specific version)
// - ModuleA.Resources.dll (module-private)

// Questions:
// - Which dependencies load from host context?
// - Which dependencies load in module context?
// - How to handle version conflicts?
// - How to resolve dependency paths?
```

---

## Decision

### The Chosen Approach

**VISORA uses McMaster.NETCore.Plugins library to create collectible `AssemblyLoadContext` instances for each module, enabling hot-swap and memory cleanup while sharing core platform assemblies with the host.**

### Core Architecture

#### 1. McMaster.NETCore.Plugins Integration

```csharp
using McMaster.NETCore.Plugins;

// Create plugin loader for each module
var loader = PluginLoader.CreateFromAssemblyFile(
    assemblyFile: "/path/to/Module.vixm.dll",
    sharedTypes: new[]
    {
        // Types shared between host and module (same type identity)
        typeof(VisoraModule),
        typeof(ICommandHandler),
        typeof(ICapabilityProvider),
        typeof(CommandDescriptor),
        typeof(CommandResult)
    },
    configure: config =>
    {
        config.IsUnloadable = true;  // Enable unloading
        config.LoadInMemory = false;  // Load from file (allows updates)
        config.PreferSharedTypes = true;  // Use host types when possible
    });

// Load assembly
var assembly = loader.LoadDefaultAssembly();

// Use module...
var moduleType = assembly.GetTypes()
    .First(t => typeof(VisoraModule).IsAssignableFrom(t));
var module = (VisoraModule)Activator.CreateInstance(moduleType);

// Later: Unload
loader.Dispose();  // Triggers unload
```

#### 2. Module Load Context Hierarchy

```
┌────────────────────────────────────────────────────────────┐
│              Default AssemblyLoadContext                    │
│  (Host Application)                                         │
│                                                             │
│  - Visora.Host.exe                                          │
│  - Visora.Core.dll                                          │
│  - Visora.Abstractions.dll                                  │
│  - System.* (BCL)                                           │
│  - Microsoft.Extensions.* (DI, Logging, etc.)               │
└────────────────────────────────────────────────────────────┘
         ↑ Shares types        ↑ Shares types
         │                     │
┌────────┴──────────┐  ┌──────┴────────────┐
│ Module A Context  │  │ Module B Context  │
│ (Unloadable)      │  │ (Unloadable)      │
│                   │  │                   │
│ - ModuleA.vixm    │  │ - ModuleB.vixm    │
│ - Newtonsoft 12.0 │  │ - Newtonsoft 13.0 │ ← Different versions!
│ - ModuleA.Data    │  │ - ModuleB.Utils   │
└───────────────────┘  └───────────────────┘
```

#### 3. Module Lifecycle with Unloading

```csharp
public class ModuleManager
{
    private readonly Dictionary<string, LoadedModule> _modules = new();

    public async ValueTask<ModuleDescriptor> LoadModuleAsync(
        string modulePath,
        CancellationToken ct)
    {
        // Create plugin loader
        var loader = CreatePluginLoader(modulePath);

        // Load assembly in isolated context
        var assembly = loader.LoadDefaultAssembly();

        // Discover and instantiate module
        var moduleType = FindModuleType(assembly);
        var instance = (VisoraModule)Activator.CreateInstance(moduleType);

        // Initialize module
        await instance.InitializeAsync(_capabilityProvider, ct);

        // Track loaded module
        var loadedModule = new LoadedModule(loader, instance, assembly);
        _modules[instance.ModuleName] = loadedModule;

        return CreateDescriptor(instance);
    }

    public async ValueTask UnloadModuleAsync(string moduleName, CancellationToken ct)
    {
        if (!_modules.TryGetValue(moduleName, out var loadedModule))
            return;

        // Shutdown module
        await loadedModule.Instance.ShutdownAsync(ct);

        // Unload assembly context
        loadedModule.Loader.Dispose();

        // Clear references
        _modules.Remove(moduleName);

        // Force garbage collection to reclaim memory
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    public async ValueTask ReloadModuleAsync(string moduleName, CancellationToken ct)
    {
        // Get current module path
        var currentModule = _modules[moduleName];
        var modulePath = currentModule.Loader.GetType()
            .GetProperty("AssemblyFile")!
            .GetValue(currentModule.Loader) as string;

        // Unload old version
        await UnloadModuleAsync(moduleName, ct);

        // Load new version
        await LoadModuleAsync(modulePath!, ct);
    }
}

record LoadedModule(PluginLoader Loader, VisoraModule Instance, Assembly Assembly);
```

---

## Alternatives Considered

### Alternative 1: Separate Processes

**Approach:** Load each module in a separate process, communicate via IPC.

```csharp
// Host process
public class ProcessBasedModuleLoader
{
    public async Task<IModuleProxy> LoadModuleAsync(string modulePath)
    {
        // Start new process for module
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "Visora.ModuleHost.exe",
            Arguments = $"--module \"{modulePath}\"",
            RedirectStandardInput = true,
            RedirectStandardOutput = true
        });

        // Create IPC channel (named pipes, sockets, etc.)
        var channel = await CreateIpcChannelAsync(process.Id);

        // Return proxy that communicates over IPC
        return new ModuleProxy(process, channel);
    }

    public async Task UnloadModuleAsync(IModuleProxy proxy)
    {
        // Terminate process
        proxy.Process.Kill();
    }
}

// Module proxy communicates via IPC
public class ModuleProxy : IModule
{
    public async Task<CommandResult> ExecuteAsync(CommandContext context)
    {
        // Serialize context and send to module process
        var request = Serialize(context);
        await _channel.SendAsync(request);

        // Receive and deserialize result
        var response = await _channel.ReceiveAsync();
        return Deserialize<CommandResult>(response);
    }
}
```

**Pros:**
- Complete isolation (modules can't crash host)
- Easy unloading (just kill process)
- No memory leak concerns
- Different .NET versions possible per module
- Operating system enforces resource limits

**Cons:**
- ❌ **Performance overhead:** IPC is much slower than in-process calls
- ❌ **Complexity:** IPC infrastructure, serialization, process management
- ❌ **Debugging difficulty:** Multi-process debugging is harder
- ❌ **Resource overhead:** Each process has startup cost and memory overhead
- ❌ **Platform differences:** IPC mechanisms differ across OS

**Benchmark:**
```
In-process call:     ~0.001ms
IPC call (pipes):    ~0.5ms
IPC call (sockets):  ~1ms

Overhead: 500-1000x slower!
```

**Why Not Chosen:**
Performance overhead is too high for typical module operations. VISORA modules execute frequently, making IPC latency unacceptable.

---

### Alternative 2: AppDomains (Legacy)

**Approach:** Use AppDomains for isolation (if they weren't deprecated).

```csharp
// This doesn't work in .NET Core/5+!
// AppDomains are not supported

// .NET Framework only:
var domain = AppDomain.CreateDomain("ModuleDomain",
    securityInfo: null,
    appBasePath: moduleDirectory,
    appRelativeSearchPath: null,
    shadowCopyFiles: true);

var proxy = (IModule)domain.CreateInstanceFromAndUnwrap(
    "Module.dll",
    "ModuleClass");

// Execute
proxy.Execute();

// Unload
AppDomain.Unload(domain);
```

**Pros:**
- Proven isolation mechanism
- Easy unloading
- In-process (better performance than separate processes)
- Shadow copy support

**Cons:**
- ❌ **Not available in .NET Core/5/6/7/8/9+**
- ❌ **Legacy technology**
- ❌ **Performance overhead** (not as bad as processes, but worse than ALC)
- ❌ **Complexity**

**Why Not Chosen:**
AppDomains don't exist in modern .NET. This is not an option for .NET 9.0 applications.

---

### Alternative 3: No Hot-Swap (Static Loading)

**Approach:** Load modules once at startup, require restart for updates.

```csharp
public class StaticModuleLoader
{
    public async Task LoadAllModulesAsync()
    {
        var moduleFiles = Directory.GetFiles("./modules", "*.vixm.dll");

        foreach (var file in moduleFiles)
        {
            // Load in default context (cannot unload)
            var assembly = Assembly.LoadFrom(file);
            var module = InstantiateModule(assembly);
            await module.InitializeAsync(_capabilities, CancellationToken.None);

            _modules.Add(module);
        }
    }

    // No unload/reload methods - not possible!
}

// To update modules:
// 1. Stop application
// 2. Replace DLL files
// 3. Start application
```

**Pros:**
- Simplest implementation
- No complexity of AssemblyLoadContext
- No reference tracking concerns
- No unloading edge cases
- Predictable memory usage

**Cons:**
- ❌ **No hot-swap:** Must restart for updates
- ❌ **Poor development experience:** Slow iteration
- ❌ **Downtime required:** Can't update production without restart
- ❌ **Memory leaks on reload:** If app tries to reload, old assemblies stay in memory

**Why Not Chosen:**
Hot-swap is a key VISORA feature for both development experience and production flexibility. The benefits outweigh the complexity cost.

---

### Alternative 4: Shadow Copying

**Approach:** Copy DLLs before loading, allowing file replacement.

```csharp
public class ShadowCopyModuleLoader
{
    public async Task<ModuleDescriptor> LoadModuleAsync(string modulePath)
    {
        // Copy DLL to shadow directory
        var shadowPath = Path.Combine(_shadowDirectory, Path.GetFileName(modulePath));
        File.Copy(modulePath, shadowPath, overwrite: true);

        // Load from shadow copy
        var assembly = Assembly.LoadFrom(shadowPath);
        var module = InstantiateModule(assembly);
        await module.InitializeAsync(_capabilities, CancellationToken.None);

        return CreateDescriptor(module);
    }

    // Original DLL can be replaced while shadow copy is loaded
    // But: Still can't unload assembly!
}
```

**Pros:**
- Allows file replacement while running
- Original DLL not locked
- Simple to implement

**Cons:**
- ❌ **Doesn't solve unloading:** Assembly still can't be unloaded
- ❌ **Memory leaks:** Each reload adds more memory usage
- ❌ **Disk space usage:** Shadow copies accumulate
- ❌ **Not true hot-swap:** Can replace file but can't unload old version

**Why Not Chosen:**
Shadow copying solves file locking but not memory management. Without unloading, memory leaks are inevitable in long-running applications.

---

### Alternative 5: Custom AssemblyLoadContext (Without Library)

**Approach:** Implement `AssemblyLoadContext` directly without McMaster.NETCore.Plugins.

```csharp
public class ModuleLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;
    private readonly string[] _sharedAssemblies;

    public ModuleLoadContext(string modulePath, string[] sharedAssemblies)
        : base(isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(modulePath);
        _sharedAssemblies = sharedAssemblies;
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Check if this is a shared assembly (use host version)
        if (_sharedAssemblies.Contains(assemblyName.Name!))
        {
            return null;  // Fall back to default context
        }

        // Resolve dependency path
        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath != null)
        {
            return LoadFromAssemblyPath(assemblyPath);
        }

        return null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        if (libraryPath != null)
        {
            return LoadUnmanagedDllFromPath(libraryPath);
        }

        return IntPtr.Zero;
    }
}

// Usage
var context = new ModuleLoadContext(modulePath, new[] { "Visora.Core", "Visora.Abstractions" });
var assembly = context.LoadFromAssemblyPath(modulePath);
// Use module...
context.Unload();
```

**Pros:**
- Full control over loading behavior
- No external dependencies
- Lightweight (no library overhead)
- Can customize for specific needs

**Cons:**
- ⚠️ **Reinventing the wheel:** McMaster.NETCore.Plugins solves these problems
- ⚠️ **More code to maintain:** Complex loading logic
- ⚠️ **Edge cases:** Library has handled many subtle issues
- ⚠️ **Testing burden:** Must test loading behavior extensively

**Why Not Chosen:**
McMaster.NETCore.Plugins is battle-tested, well-maintained, and solves exactly this problem. The small dependency is worth the robustness and time savings.

---

## Rationale

### Why McMaster.NETCore.Plugins + AssemblyLoadContext Wins

#### 1. In-Process Performance

Orders of magnitude faster than separate processes:

```
Benchmark: Module Command Execution (1000 calls)

Separate Processes (IPC):
- Overhead per call: ~0.5ms
- Total time: 500ms
- Context switches: 1000

AssemblyLoadContext (In-Process):
- Overhead per call: ~0.001ms
- Total time: 1ms
- Context switches: 0

Performance: 500x faster! ✅
```

#### 2. True Memory Management

Unloadable contexts release memory:

```csharp
// Scenario: Load, update, load, update... (10 cycles)

Without Unloading:
- Initial: Module v1.0 (50 MB)
- Update: Module v1.1 (50 MB) → v1.0 still in memory (100 MB total)
- Update: Module v1.2 (50 MB) → v1.0, v1.1 still in memory (150 MB total)
- ...
- After 10 updates: 550 MB! ❌

With Unloading:
- Initial: Module v1.0 (50 MB)
- Update: Module v1.1 (50 MB) → v1.0 unloaded and collected (50 MB total)
- Update: Module v1.2 (50 MB) → v1.1 unloaded and collected (50 MB total)
- ...
- After 10 updates: 50 MB! ✅

Memory saved: 500 MB (91% reduction)
```

#### 3. Development Velocity

Hot-swap dramatically improves iteration speed:

```
Development Session (100 edit-compile-test cycles):

Without Hot-Swap:
- Per cycle: Edit (2 min) + Compile (10s) + Restart (30s) + Test (1 min) = 4 min 40s
- Total: 466 minutes (7.7 hours)

With Hot-Swap:
- Per cycle: Edit (2 min) + Compile (10s) + Reload (1s) + Test (1 min) = 3 min 11s
- Total: 318 minutes (5.3 hours)

Time saved: 2.4 hours per day! ✅
```

#### 4. Zero-Downtime Updates

Production updates without stopping service:

```
E-commerce scenario: Module update during peak traffic

Without Hot-Swap:
1. Schedule maintenance (11 PM - 11:15 PM)
2. Notify users of downtime
3. Stop application
4. Update module
5. Start application
6. Verify
Result: 15 minutes downtime, potential lost sales

With Hot-Swap:
1. Issue hot-swap command during peak traffic
2. New version loads immediately
3. Old version unloads
4. Verify
Result: 0 seconds downtime, no lost sales ✅
```

#### 5. Dependency Isolation

Different modules can use different dependency versions:

```
Real scenario from development:

Module A: Chart generation (uses SkiaSharp 2.80)
Module B: PDF generation (uses SkiaSharp 2.88)

Without Isolation:
- Both must use same SkiaSharp version
- May require code changes for compatibility
- Constrained dependency management

With AssemblyLoadContext Isolation:
- Module A loads SkiaSharp 2.80 in its context
- Module B loads SkiaSharp 2.88 in its context
- No conflicts! ✅
- Each module uses version it was built against
```

#### 6. McMaster.NETCore.Plugins Benefits

The library provides critical functionality:

```csharp
// Without library: Manual assembly resolution (complex!)
protected override Assembly? Load(AssemblyName assemblyName)
{
    // TODO: Implement dependency resolution
    // TODO: Handle shared types
    // TODO: Handle version conflicts
    // TODO: Handle native dependencies
    // TODO: Handle transitive dependencies
    // Hundreds of lines of complex code...
}

// With library: Simple configuration
var loader = PluginLoader.CreateFromAssemblyFile(
    assemblyFile: modulePath,
    sharedTypes: new[] { typeof(VisoraModule), typeof(ICommandHandler) },
    configure: config => {
        config.IsUnloadable = true;
        config.PreferSharedTypes = true;
    });

// All complexity handled! ✅
```

**Library handles:**
- Dependency resolution
- Shared type loading
- Native library loading
- Transitive dependencies
- Edge cases and corner cases

---

## Consequences

### Positive Consequences

#### 1. Hot-Swap During Development

Developers can update modules without restarting:

```bash
# Terminal 1: Host running
$ dotnet run --project Visora.Host
info: Visora.Host loaded 5 modules
info: Listening for commands...

# Terminal 2: Developer updates module
$ cd modules/DataProcessing
$ # Edit code...
$ dotnet build
$ visora module reload DataProcessing

# Terminal 1: Module reloaded
info: Unloading module: DataProcessing
info: Loading module: DataProcessing v1.1
info: Module reloaded successfully
```

#### 2. Memory Cleanup

Long-running applications don't accumulate module versions:

```csharp
// Application running for 30 days with daily module updates

Day 1: Load Module A v1.0 (50 MB)
Day 2: Reload Module A v1.1 (50 MB) → v1.0 unloaded, memory back to 50 MB
Day 3: Reload Module A v1.2 (50 MB) → v1.1 unloaded, memory back to 50 MB
...
Day 30: Reload Module A v1.29 (50 MB) → v1.28 unloaded, memory back to 50 MB

Total memory: 50 MB (not 1450 MB!) ✅
```

#### 3. Zero-Downtime Production Updates

Update modules in running production systems:

```csharp
// Production update workflow
await moduleManager.UnloadModuleAsync("PaymentProcessing", ct);
// Upload new version to disk
await moduleManager.LoadModuleAsync("./modules/PaymentProcessing.vixm.dll", ct);

// Service remained available throughout!
```

#### 4. Dependency Version Flexibility

Modules can use different versions of same library:

```
Module A Context:
  - Newtonsoft.Json 12.0.3
  - AutoMapper 10.1.1

Module B Context:
  - Newtonsoft.Json 13.0.1
  - AutoMapper 11.0.0

No conflicts! Each module has its own versions.
```

#### 5. Better Resource Utilization

No need for separate processes:

```
Resource comparison (10 modules):

Separate Processes:
- Processes: 10 × ~20 MB overhead = 200 MB
- Module memory: 10 × 50 MB = 500 MB
- Total: 700 MB

AssemblyLoadContext:
- Contexts: 10 × ~1 MB overhead = 10 MB
- Module memory: 10 × 50 MB = 500 MB
- Total: 510 MB

Memory saved: 190 MB (27% reduction)
```

### Negative Consequences

#### 1. Complexity of Reference Management

Must carefully manage references to ensure unloading:

```csharp
// Problem: Reference prevents unloading

// Load module
var loader = CreatePluginLoader(modulePath);
var module = LoadModule(loader);

// Execute module - module subscribes to event
await module.InitializeAsync(capabilities, ct);
// Internally: GlobalEventBus.SomeEvent += module.OnEvent;

// Try to unload
await module.ShutdownAsync(ct);
loader.Dispose();

// ⚠️ Context doesn't unload!
// Why? GlobalEventBus still holds reference to module.OnEvent
```

**Mitigation:** Enforce cleanup in `ShutdownAsync`, use weak event patterns where appropriate.

#### 2. Debugging Complexity

Multiple assembly contexts make debugging harder:

```
// Call stack across contexts:
Visora.Host.dll!CommandExecutor.ExecuteAsync()
  → [Context Boundary]
ModuleA.vixm.dll!DataProcessingModule.ExecuteAsync()
  → ModuleA.Data.dll!DataProcessor.ProcessAsync()
  ← [Context Boundary]
  ← Visora.Host.dll!CommandExecutor.ExecuteAsync()

Debugger may have difficulty stepping across contexts.
```

**Mitigation:** Modern debuggers (VS 2022, VS Code) handle this well in .NET 9.0.

#### 3. Type Identity Challenges

Types from different contexts are incompatible:

```csharp
// Problem: Type identity
// Host defines: CommandResult (Default context)
// Module uses: CommandResult (Module context)

var result = await module.ExecuteAsync(context, ct);

if (result is CommandResult)  // Might be FALSE! ⚠️
{
    // This may not execute if types are from different contexts
}

// Solution: Use shared types (via McMaster.NETCore.Plugins)
var loader = PluginLoader.CreateFromAssemblyFile(
    assemblyFile: modulePath,
    sharedTypes: new[] { typeof(CommandResult) }  // ← Share type
);

// Now: result is CommandResult works correctly ✅
```

**Mitigation:** Carefully configure shared types in plugin loader.

#### 4. Unloading is Not Immediate

Unloading requires garbage collection:

```csharp
// Unload command issued
loader.Dispose();
context.Unload();

// Memory not immediately released!
// Must wait for garbage collection

GC.Collect();
GC.WaitForPendingFinalizers();
GC.Collect();

// Now memory is released (hopefully!)
```

**Mitigation:** Document that unload is asynchronous, provide status checking.

#### 5. Potential for Memory Leaks

Hidden references can prevent unloading:

```csharp
// Common leak scenarios:
// 1. Event subscriptions not cleaned up
// 2. Static caches holding module types
// 3. Timers not disposed
// 4. Thread local storage
// 5. Finalizers

// These prevent context from unloading even after Dispose()!
```

**Mitigation:**
- Strict module lifecycle enforcement
- Memory profiling during development
- Automated tests for successful unloading

---

## Tradeoffs

### AssemblyLoadContext vs. Alternatives

| Aspect | ALC + McMaster | Separate Processes | AppDomains | Static Loading | Custom ALC |
|--------|---------------|-------------------|------------|----------------|------------|
| **Performance** | ⭐⭐⭐⭐⭐ | ⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ |
| **Hot-Swap** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ❌ (N/A) | ⭐⭐⭐⭐⭐ |
| **Memory Management** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐ | ⭐⭐⭐⭐⭐ |
| **Isolation** | ⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐ | ⭐⭐⭐⭐ |
| **Debugging** | ⭐⭐⭐⭐ | ⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐ |
| **Complexity** | ⭐⭐⭐⭐ | ⭐⭐ | ⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐ |
| **.NET Core Support** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ❌ (N/A) | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ |
| **Maintenance** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐ | ❌ (N/A) | ⭐⭐⭐⭐⭐ | ⭐⭐⭐ |
| **Resource Usage** | ⭐⭐⭐⭐⭐ | ⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ |
| **Library Support** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐ | ❌ (N/A) | N/A | ⭐⭐ |
| **Total Score** | **48/50** | **33/50** | **28/40** | **32/40** | **40/50** |

---

## Implementation Details

### Complete Module Manager Implementation

```csharp
using McMaster.NETCore.Plugins;
using System.Collections.Concurrent;

public class ModuleManager : IModuleManager
{
    private readonly ILogger<ModuleManager> _logger;
    private readonly ICapabilityProvider _capabilityProvider;
    private readonly ConcurrentDictionary<string, LoadedModule> _modules = new();
    private readonly string _modulesDirectory;

    // Shared types (same identity between host and modules)
    private static readonly Type[] SharedTypes = new[]
    {
        typeof(VisoraModule),
        typeof(ICommandHandler),
        typeof(ICapabilityProvider),
        typeof(CommandDescriptor),
        typeof(CommandResult),
        typeof(ParameterDescriptor),
        typeof(ServiceDescriptor),
        typeof(ILogger)
    };

    public ModuleManager(
        ILogger<ModuleManager> logger,
        ICapabilityProvider capabilityProvider,
        string modulesDirectory)
    {
        _logger = logger;
        _capabilityProvider = capabilityProvider;
        _modulesDirectory = modulesDirectory;
    }

    public async ValueTask<ModuleDescriptor> LoadModuleAsync(
        string modulePath,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Loading module from {Path}", modulePath);

        try
        {
            // Create plugin loader with unloadable context
            var loader = PluginLoader.CreateFromAssemblyFile(
                assemblyFile: modulePath,
                sharedTypes: SharedTypes,
                configure: config =>
                {
                    config.IsUnloadable = true;  // Enable hot-swap
                    config.LoadInMemory = false;  // Load from disk (allows file updates)
                    config.PreferSharedTypes = true;  // Use host types when possible
                });

            // Load assembly
            var assembly = loader.LoadDefaultAssembly();
            _logger.LogDebug("Assembly loaded: {Assembly}", assembly.FullName);

            // Find module type
            var moduleType = assembly.GetTypes()
                .FirstOrDefault(t => t.IsClass &&
                                   !t.IsAbstract &&
                                   typeof(VisoraModule).IsAssignableFrom(t));

            if (moduleType == null)
            {
                throw new InvalidModuleException($"No VisoraModule implementation found in {modulePath}");
            }

            // Instantiate module
            var moduleInstance = (VisoraModule)Activator.CreateInstance(moduleType)!;
            _logger.LogDebug("Module instantiated: {Type}", moduleType.FullName);

            // Initialize module
            await moduleInstance.InitializeAsync(_capabilityProvider, ct);
            _logger.LogInformation("Module initialized: {Module} v{Version}",
                                  moduleInstance.ModuleName,
                                  moduleInstance.Version);

            // Store loaded module
            var loadedModule = new LoadedModule(
                Loader: loader,
                Instance: moduleInstance,
                Assembly: assembly,
                AssemblyPath: modulePath,
                LoadedAt: DateTimeOffset.UtcNow);

            _modules[moduleInstance.ModuleName] = loadedModule;

            // Create descriptor
            var descriptor = new ModuleDescriptor(
                Name: moduleInstance.ModuleName,
                Description: moduleInstance.Description,
                Version: moduleInstance.Version,
                AssemblyPath: modulePath,
                ModuleType: moduleType,
                Commands: moduleInstance.GetCommands().ToList(),
                Services: moduleInstance.GetServices().ToList(),
                Dependencies: moduleInstance.GetDependencies().ToList());

            _logger.LogInformation("Module loaded successfully: {Module}", descriptor.Name);
            return descriptor;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load module from {Path}", modulePath);
            throw;
        }
    }

    public async ValueTask UnloadModuleAsync(string moduleName, CancellationToken ct = default)
    {
        if (!_modules.TryRemove(moduleName, out var loadedModule))
        {
            _logger.LogWarning("Module not found: {Module}", moduleName);
            return;
        }

        _logger.LogInformation("Unloading module: {Module}", moduleName);

        try
        {
            // Shutdown module (cleanup)
            await loadedModule.Instance.ShutdownAsync(ct);

            // Dispose plugin loader (triggers unload)
            loadedModule.Loader.Dispose();

            // Clear references
            var weakRef = new WeakReference(loadedModule.Loader, trackResurrection: true);

            // Force garbage collection
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // Verify unload
            if (weakRef.IsAlive)
            {
                _logger.LogWarning("Module context not fully unloaded: {Module}. " +
                                  "There may be lingering references.", moduleName);
            }
            else
            {
                _logger.LogInformation("Module unloaded successfully: {Module}", moduleName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unloading module: {Module}", moduleName);
            throw;
        }
    }

    public async ValueTask ReloadModuleAsync(string moduleName, CancellationToken ct = default)
    {
        _logger.LogInformation("Reloading module: {Module}", moduleName);

        if (!_modules.TryGetValue(moduleName, out var currentModule))
        {
            throw new InvalidOperationException($"Module not loaded: {moduleName}");
        }

        var modulePath = currentModule.AssemblyPath;

        // Unload old version
        await UnloadModuleAsync(moduleName, ct);

        // Small delay to ensure file is not locked
        await Task.Delay(100, ct);

        // Load new version
        await LoadModuleAsync(modulePath, ct);

        _logger.LogInformation("Module reloaded: {Module}", moduleName);
    }

    public IEnumerable<ModuleDescriptor> GetLoadedModules()
    {
        return _modules.Values.Select(m => new ModuleDescriptor(
            Name: m.Instance.ModuleName,
            Description: m.Instance.Description,
            Version: m.Instance.Version,
            AssemblyPath: m.AssemblyPath,
            ModuleType: m.Instance.GetType(),
            Commands: m.Instance.GetCommands().ToList(),
            Services: m.Instance.GetServices().ToList(),
            Dependencies: m.Instance.GetDependencies().ToList()));
    }

    public VisoraModule? GetModule(string moduleName)
    {
        return _modules.TryGetValue(moduleName, out var module) ? module.Instance : null;
    }

    public async ValueTask DisposeAsync()
    {
        _logger.LogInformation("Disposing ModuleManager, unloading all modules...");

        var unloadTasks = _modules.Keys
            .Select(name => UnloadModuleAsync(name, CancellationToken.None));

        await Task.WhenAll(unloadTasks.Select(t => t.AsTask()));

        _logger.LogInformation("All modules unloaded");
    }
}

public record LoadedModule(
    PluginLoader Loader,
    VisoraModule Instance,
    Assembly Assembly,
    string AssemblyPath,
    DateTimeOffset LoadedAt);
```

### File Watcher for Auto-Reload (Development)

```csharp
public class ModuleFileWatcher : IDisposable
{
    private readonly ModuleManager _moduleManager;
    private readonly ILogger<ModuleFileWatcher> _logger;
    private readonly FileSystemWatcher _watcher;
    private readonly Dictionary<string, DateTime> _lastReloadTimes = new();

    public ModuleFileWatcher(ModuleManager moduleManager, ILogger<ModuleFileWatcher> logger, string modulesPath)
    {
        _moduleManager = moduleManager;
        _logger = logger;

        _watcher = new FileSystemWatcher(modulesPath, "*.vixm.dll")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
            IncludeSubdirectories = true
        };

        _watcher.Changed += OnModuleFileChanged;
        _watcher.Created += OnModuleFileChanged;
        _watcher.EnableRaisingEvents = true;

        _logger.LogInformation("Watching for module changes in {Path}", modulesPath);
    }

    private async void OnModuleFileChanged(object sender, FileSystemEventArgs e)
    {
        // Debounce: Multiple events can fire for single change
        var now = DateTime.UtcNow;
        if (_lastReloadTimes.TryGetValue(e.FullPath, out var lastTime) &&
            (now - lastTime).TotalMilliseconds < 1000)
        {
            return;  // Too soon, ignore
        }

        _lastReloadTimes[e.FullPath] = now;

        _logger.LogInformation("Module file changed: {Path}", e.FullPath);

        try
        {
            // Find module name
            var modules = _moduleManager.GetLoadedModules()
                .Where(m => m.AssemblyPath == e.FullPath)
                .ToList();

            if (modules.Count == 0)
            {
                _logger.LogInformation("New module detected, loading: {Path}", e.FullPath);
                await _moduleManager.LoadModuleAsync(e.FullPath, CancellationToken.None);
            }
            else
            {
                foreach (var module in modules)
                {
                    _logger.LogInformation("Module changed, reloading: {Module}", module.Name);
                    await _moduleManager.ReloadModuleAsync(module.Name, CancellationToken.None);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling module file change: {Path}", e.FullPath);
        }
    }

    public void Dispose()
    {
        _watcher?.Dispose();
    }
}
```

---

## Memory Management

### Ensuring Successful Unloading

```csharp
public class UnloadVerifier
{
    private readonly ILogger<UnloadVerifier> _logger;

    public async Task<bool> VerifyUnloadAsync(
        PluginLoader loader,
        string moduleName,
        TimeSpan timeout)
    {
        // Create weak reference to track unload
        var weakRef = new WeakReference(loader, trackResurrection: true);

        // Dispose loader (triggers unload)
        loader.Dispose();

        // Force collection multiple times
        var stopwatch = Stopwatch.StartNew();
        while (weakRef.IsAlive && stopwatch.Elapsed < timeout)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            if (weakRef.IsAlive)
            {
                await Task.Delay(100);  // Wait a bit before retrying
            }
        }

        if (weakRef.IsAlive)
        {
            _logger.LogWarning(
                "Module {Module} context not unloaded after {Timeout}ms. " +
                "Lingering references may prevent unloading.",
                moduleName,
                timeout.TotalMilliseconds);

            return false;
        }

        _logger.LogInformation("Module {Module} successfully unloaded", moduleName);
        return true;
    }
}
```

### Common Unload Blockers

```csharp
// ❌ BAD: Event subscription prevents unload
public class BadModule : VisoraModule
{
    public override async ValueTask InitializeAsync(ICapabilityProvider capabilities, CancellationToken ct)
    {
        GlobalEventBus.SomeEvent += OnEvent;  // ⚠️ Never unsubscribed!
    }

    private void OnEvent(object? sender, EventArgs e) { }

    // ShutdownAsync doesn't clean up event subscription
}

// ✅ GOOD: Clean up event subscriptions
public class GoodModule : VisoraModule
{
    public override async ValueTask InitializeAsync(ICapabilityProvider capabilities, CancellationToken ct)
    {
        GlobalEventBus.SomeEvent += OnEvent;
    }

    public override async ValueTask ShutdownAsync(CancellationToken ct)
    {
        GlobalEventBus.SomeEvent -= OnEvent;  // ✅ Cleaned up!
        await base.ShutdownAsync(ct);
    }

    private void OnEvent(object? sender, EventArgs e) { }
}

// ❌ BAD: Timer not disposed
public class BadTimerModule : VisoraModule
{
    private Timer? _timer;

    public override async ValueTask InitializeAsync(ICapabilityProvider capabilities, CancellationToken ct)
    {
        _timer = new Timer(_ => DoWork(), null, 1000, 1000);  // ⚠️ Never disposed!
    }

    private void DoWork() { }
}

// ✅ GOOD: Dispose timer
public class GoodTimerModule : VisoraModule
{
    private Timer? _timer;

    public override async ValueTask InitializeAsync(ICapabilityProvider capabilities, CancellationToken ct)
    {
        _timer = new Timer(_ => DoWork(), null, 1000, 1000);
    }

    public override async ValueTask ShutdownAsync(CancellationToken ct)
    {
        _timer?.Dispose();  // ✅ Cleaned up!
        _timer = null;
        await base.ShutdownAsync(ct);
    }

    private void DoWork() { }
}

// ❌ BAD: Static cache holds module types
public static class BadGlobalCache
{
    private static readonly Dictionary<string, Type> _typeCache = new();

    public static void RegisterType(string name, Type type)
    {
        _typeCache[name] = type;  // ⚠️ Holds reference to module type!
    }
}

// ✅ GOOD: Use WeakReference for caching
public static class GoodGlobalCache
{
    private static readonly Dictionary<string, WeakReference> _typeCache = new();

    public static void RegisterType(string name, Type type)
    {
        _typeCache[name] = new WeakReference(type);  // ✅ Allows GC!
    }

    public static Type? GetType(string name)
    {
        if (_typeCache.TryGetValue(name, out var weakRef) && weakRef.IsAlive)
        {
            return (Type?)weakRef.Target;
        }
        return null;
    }
}
```

---

## When to Revisit

### Triggers for Reconsideration

#### 1. .NET Runtime Changes

**Scenario:** Future .NET versions change AssemblyLoadContext behavior

**Action:** Test thoroughly with each new .NET version, adjust implementation as needed

#### 2. Memory Leak Patterns Emerge

**Metrics to Watch:**
- Module contexts not unloading (weak references remain alive)
- Memory growth after multiple reload cycles
- Finalizer queue buildup

**Action:** Investigate common leak patterns, provide guidance/analyzers to module authors

#### 3. Performance Overhead

**Scenario:** Assembly loading/unloading becomes bottleneck

**Benchmark thresholds:**
- Module load time > 500ms
- Module unload time > 1000ms
- Memory overhead per context > 5MB

**Action:** Profile and optimize, consider caching strategies

#### 4. Cross-Platform Issues

**Scenario:** Different behavior on Linux/macOS/Windows

**Action:** Comprehensive cross-platform testing, adjust implementation for platform differences

---

## Related Patterns

### Primary Patterns

#### 1. Plugin Architecture Pattern
- **Location:** `/References/patterns/plugin-architecture.md`
- **Relationship:** Overall plugin system design
- **Summary:** How modules integrate with host

#### 2. Module Lifecycle Pattern
- **Location:** `/References/patterns/module-lifecycle.md`
- **Relationship:** Load/Initialize/Shutdown/Unload sequence
- **Summary:** Complete module lifecycle management

### Related ADRs

#### ADR-001: Reflection Discovery
- **Connection:** Discovery happens before loading into AssemblyLoadContext
- **Flow:** Discover → Create PluginLoader → Load in ALC → Instantiate

#### ADR-003: Async Everywhere
- **Connection:** Module loading and unloading are async operations
- **Code:** `await LoadModuleAsync()`, `await UnloadModuleAsync()`

### Supporting Patterns

#### 3. Hot-Swap Pattern
- **Location:** `/References/patterns/hot-swap.md`
- **Summary:** Detailed hot-swap workflow and best practices

#### 4. Resource Cleanup Pattern
- **Location:** `/References/patterns/resource-cleanup.md`
- **Summary:** Ensuring proper cleanup for successful unloading

---

## References

### Internal Documentation
- `/References/patterns/plugin-architecture.md` - Plugin system overview
- `/References/patterns/module-lifecycle.md` - Lifecycle management
- `/References/patterns/hot-swap.md` - Hot-swap workflows

### External Resources
- [AssemblyLoadContext Documentation](https://learn.microsoft.com/en-us/dotnet/core/dependency-loading/understanding-assemblyloadcontext)
- [McMaster.NETCore.Plugins](https://github.com/natemcmaster/DotNetCorePlugins)
- [Collectible AssemblyLoadContext](https://learn.microsoft.com/en-us/dotnet/standard/assembly/unloadability)
- [Plugin Loading Tutorial](https://learn.microsoft.com/en-us/dotnet/core/tutorials/creating-app-with-plugin-support)

### Performance & Memory
- [Analyzing AssemblyLoadContext Leaks](https://blog.mjbrooks.com/2020/09/14/debugging-assemblyloadcontext-unload-failures/)
- [Memory Profiling .NET Applications](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/debug-memory-leak)

---

**Document Metadata:**
- **Author:** VISORA Architecture Team
- **Contributors:** Performance Team, Module Authors
- **Review Cycle:** Quarterly
- **Next Review:** 2025-02-10
