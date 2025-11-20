# Factory Patterns - VISORA Deep Dive

**Last Updated:** November 10, 2025
**VISORA Version:** .NET 9.0
**Pattern Tier:** Tier 3 (Data & Metadata)
**Related Patterns:** Immutable Metadata, Async Patterns, Module Lifecycle

---

## Table of Contents

1. [Pattern Overview](#pattern-overview)
2. [Why VISORA Uses Factory Patterns](#why-visora-uses-factory-patterns)
3. [VISORA Implementation](#visora-implementation)
4. [Code Examples](#code-examples)
5. [File References](#file-references)
6. [Static vs Instance Factories](#static-vs-instance-factories)
7. [Async Factory Patterns](#async-factory-patterns)
8. [Error Handling in Factories](#error-handling-in-factories)
9. [Testing Factories](#testing-factories)
10. [Best Practices](#best-practices)
11. [Advanced Topics](#advanced-topics)
12. [Common Pitfalls](#common-pitfalls)

---

## Pattern Overview

### What are Factory Patterns?

**Definition:** Factory patterns provide an interface for creating objects without exposing the instantiation logic to the client. They encapsulate object construction, enabling flexibility, consistency, and control over the creation process.

**Key Characteristics:**
- **Encapsulation:** Hide complex construction logic
- **Consistency:** Ensure objects are created correctly
- **Flexibility:** Can return different types or cached instances
- **Validation:** Centralize validation and error handling
- **Named Parameters:** Factory methods often use named parameters for clarity

### Types of Factory Patterns in VISORA

```
Factory Pattern Hierarchy
├─ Static Factory Methods
│  ├─ Descriptor.Create()      (Immutable metadata)
│  ├─ CommandResult.Success()  (Result objects)
│  └─ CommandResult.Failed()
│
├─ Async Factory Methods
│  ├─ ModuleHandle.LoadAsync() (Plugin loading)
│  └─ Custom async factories
│
└─ Builder + Factory Combination
   └─ CapabilityProviderBuilder.Build() (See Builder pattern)
```

**Pattern Formula:**
```
public static ReturnType Create/LoadAsync(parameters)
{
    // Validation
    // Construction
    // Return instance
}
```

---

## Why VISORA Uses Factory Patterns

### Design Goals

1. **Named Construction**
   - `ModuleDescriptor.Create()` is clearer than `new ModuleDescriptor(...)`
   - Named parameters document intent
   - Self-documenting code

2. **Validation and Error Handling**
   - Centralize validation logic
   - Throw meaningful exceptions
   - Ensure invariants before construction

3. **Encapsulation of Complexity**
   - ModuleHandle.LoadAsync() hides plugin loading details
   - Clients don't need to know about PluginLoader, reflection, etc.
   - Single entry point for complex operations

4. **Consistent API Surface**
   - All descriptors use `.Create()`
   - All result objects use `.Success()`, `.Failed()`, etc.
   - Predictable naming conventions

5. **Async Construction**
   - .NET constructors can't be async
   - Factory methods can return `Task<T>`
   - Enables I/O during construction (loading assemblies, etc.)

6. **Alternative Constructors**
   - Provide multiple ways to create objects
   - `CommandResult.Success()`, `CommandResult.Failed()`, `CommandResult.Cancelled()`
   - Each factory method documents its specific use case

### Key Decision Points

**Q: Why not use constructors directly?**
**A:**
- Constructors can't be async
- Constructors can't have different return types
- Factory methods have descriptive names (Create, Load, Success, Failed)

**Q: Why static methods instead of separate factory classes?**
**A:**
- Simpler API (no need to instantiate factory)
- Co-located with the type being created
- Common .NET idiom (DateTime.Parse, TimeSpan.FromSeconds, etc.)

---

## VISORA Implementation

### Pattern 1: Descriptor Factory Methods

**ModuleDescriptor.Create()**

**File:** `/src/Visora.Contracts/Modules/ModuleDescriptor.cs` (lines 14-21)

```csharp
public sealed record ModuleDescriptor(
    string Id,
    string Name,
    Version Version,
    string? Description = null,
    IReadOnlyDictionary<string, string>? Tags = null,
    ModuleRuntimeHints? RuntimeHints = null)
{
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

**Design Decisions:**
- Static method co-located with record
- Named parameters for clarity
- Returns new instance via positional constructor
- No validation (assumes valid inputs) - could add if needed

**Usage:**
```csharp
var descriptor = ModuleDescriptor.Create(
    id: "visora.shell.commands.core",
    name: "Shell Commands Core",
    version: new Version(1, 0, 0),
    description: "Core shell utilities");
```

**ComponentDescriptor.Create()**

**File:** `/src/Visora.Contracts/Components/ComponentDescriptor.cs` (lines 16-17)

```csharp
public sealed record ComponentDescriptor(
    string Id,
    string Name,
    string? Description = null,
    IReadOnlyCollection<string>? Tags = null,
    ComponentKind Kind = ComponentKind.Generic)
{
    public static ComponentDescriptor Create(
        string id,
        string name,
        string? description = null,
        IReadOnlyCollection<string>? tags = null,
        ComponentKind kind = ComponentKind.Generic)
        => new(id, name, description, tags, kind);
}
```

**CommandDescriptor.Create()**

**File:** `/src/Visora.Contracts/Commands/CommandDescriptor.cs` (lines 20-30)

```csharp
public sealed record CommandDescriptor(
    string Id,
    string Title,
    string? Description = null,
    CommandKind Kind = CommandKind.General,
    IReadOnlyCollection<string>? Aliases = null,
    IReadOnlyCollection<string>? Keywords = null,
    bool IsVisible = true,
    bool IsInstanceScoped = false,
    CommandUiHint? Ui = null)
{
    public static CommandDescriptor Create(
        string id,
        string title,
        string? description = null,
        CommandKind kind = CommandKind.General,
        IReadOnlyCollection<string>? aliases = null,
        IReadOnlyCollection<string>? keywords = null,
        bool isVisible = true,
        bool isInstanceScoped = false,
        CommandUiHint? ui = null)
        => new(id, title, description, kind, aliases, keywords,
               isVisible, isInstanceScoped, ui);
}
```

### Pattern 2: Result Object Factories

**CommandResult Factory Methods**

**File:** `/src/Visora.Contracts/Commands/CommandResult.cs` (inferred from usage)

```csharp
public readonly record struct CommandResult(
    CommandOutcome Outcome,
    string? Message = null,
    object? Payload = null)
{
    // Static factory methods for each outcome type
    public static CommandResult Success(string? message = null, object? payload = null)
        => new(CommandOutcome.Success, message, payload);

    public static CommandResult Cancelled(string? message = null)
        => new(CommandOutcome.Cancelled, message, payload: null);

    public static CommandResult Failed(string? message = null, object? payload = null)
        => new(CommandOutcome.Failed, message, payload);

    public enum CommandOutcome
    {
        Success,
        Cancelled,
        Failed
    }
}
```

**Design Decisions:**
- Each outcome has a dedicated factory method
- Method names describe the outcome (self-documenting)
- Optional message and payload
- Cancelled doesn't have payload (cancelled operations produce no result)

**Usage Examples:**
```csharp
// Success with message and payload
return CommandResult.Success(
    message: "Operation completed successfully",
    payload: new { RecordsProcessed = 42 });

// Cancellation with reason
if (cancellationToken.IsCancellationRequested)
    return CommandResult.Cancelled("User cancelled operation");

// Failure with error details
return CommandResult.Failed(
    message: $"Error: {ex.Message}",
    payload: new { Exception = ex.GetType().Name });
```

### Pattern 3: Async Factory Methods

**ModuleHandle.LoadAsync()**

**File:** `/src/Visora.Core/Modules/ModuleHandle.cs` (lines 51-83)

```csharp
public sealed class ModuleHandle : IAsyncDisposable
{
    private ModuleHandle(
        string assemblyPath,
        PluginLoader loader,
        Assembly assembly,
        VisoraModule module,
        ModuleContext context,
        ModuleCatalogOptions options)
    {
        // Private constructor - forces use of factory method
        AssemblyPath = assemblyPath;
        _loader = loader;
        Assembly = assembly;
        Module = module;
        _context = context;
        _options = options;
    }

    public static Task<ModuleHandle> LoadAsync(
        string assemblyPath,
        ModuleCatalogOptions options,
        CancellationToken cancellationToken)
    {
        if (options is null) throw new ArgumentNullException(nameof(options));
        cancellationToken.ThrowIfCancellationRequested();

        // Complex construction logic:
        // 1. Create plugin loader
        var sharedTypes = options.GetSharedTypesArray();
        var loader = PluginLoader.CreateFromAssemblyFile(
            assemblyPath,
            sharedTypes: sharedTypes,
            isUnloadable: true);

        try
        {
            // 2. Load assembly
            var assembly = loader.LoadDefaultAssembly();

            // 3. Discover module type via reflection
            var moduleType = assembly
                .GetTypes()
                .FirstOrDefault(t => typeof(VisoraModule).IsAssignableFrom(t)
                                     && !t.IsAbstract);

            if (moduleType is null)
                throw new InvalidOperationException(
                    $"No VisoraModule implementation found in '{assemblyPath}'.");

            // 4. Instantiate module
            if (Activator.CreateInstance(moduleType) is not VisoraModule module)
                throw new InvalidOperationException(
                    $"Unable to create module instance '{moduleType.FullName}'.");

            // 5. Validate descriptor
            var descriptor = module.Descriptor
                ?? throw new InvalidOperationException(
                    $"Module '{moduleType.FullName}' returned a null descriptor.");

            // 6. Create context
            var capabilities = options.Capabilities
                ?? throw new InvalidOperationException(
                    "Capabilities provider cannot be null.");
            var context = new ModuleContext(
                descriptor, options.Services, capabilities, options.Properties);

            // 7. Return handle
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
}
```

**Design Decisions:**
- Private constructor prevents direct instantiation
- Static factory method is the only entry point
- Complex 7-step construction process hidden from clients
- Error handling with cleanup (dispose loader on exception)
- Returns `Task<ModuleHandle>` (could be async if needed I/O)
- Validation at each step with meaningful exceptions

**Usage:**
```csharp
var handle = await ModuleHandle.LoadAsync(
    assemblyPath: "MyModule.vixm.dll",
    options: catalogOptions,
    cancellationToken: ct);

// handle.Module, handle.Descriptor, etc. are now available
```

---

## Code Examples

### Example 1: Using Descriptor Factories in Real Code

**From:** `/src/Visora.Shell.Commands.Core/ShellCommandsModule.cs`

```csharp
public sealed class ShellCommandsModule : Module
{
    private static readonly ModuleDescriptor Info = ModuleDescriptor.Create(
        id: "visora.shell.commands.core",
        name: "Visora Shell Commands (Core)",
        version: new Version(0, 1, 0),
        description: "Shell-based commands for core Visora functions and diagnostics.",
        tags: new Dictionary<string, string>
        {
            ["category"] = "shell",
            ["author"] = "Advanced Labs"
        }.AsReadOnly());

    public override ModuleDescriptor Descriptor => Info;
}
```

**Pattern:**
- Static field initialized with factory method
- Named parameters make intent clear
- Property returns the static field

**From:** `/src/Visora.Shell.Commands.Core/Commands/PingCommand.cs`

```csharp
public sealed class PingCommand : VisoraCommand
{
    private static readonly CommandDescriptor Info = CommandDescriptor.Create(
        id: "shell.ping",
        title: "Ping",
        description: "Checks connectivity with the Visora host.",
        kind: CommandKind.Automation,
        keywords: new[] { "diagnostics", "ping" });

    public override CommandDescriptor Descriptor => Info;

    public override ValueTask<CommandResult> ExecuteAsync(
        CommandContext context,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var surface = context.Surface.ToString();
        var message = $"Pong from '{context.Module.Descriptor.Name}' " +
                      $"via {surface} @ {now:O}";
        var payload = new
        {
            Timestamp = now,
            Surface = context.Surface,
            ModuleId = context.Module.Descriptor.Id
        };

        // Using result factory method
        return ValueTask.FromResult(CommandResult.Success(message, payload));
    }
}
```

**Pattern:**
- Descriptor created with factory
- Result created with factory (Success)
- Both use named parameters

### Example 2: Module Loading in CLI

**From:** `/src/Visora.CLI/Program.cs` (approximate lines 140-160)

```csharp
private static async Task<int> ExecuteModulesListCommand(
    ParseResult parseResult,
    CancellationToken ct)
{
    var options = CreateCatalogOptions(parseResult);

    await using var catalog = new ModuleCatalog();
    try
    {
        await catalog.DiscoverAsync(options, ct).ConfigureAwait(false);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Discovery failed: {ex.Message}");
        return 1;
    }

    // Each module was loaded with ModuleHandle.LoadAsync
    foreach (var handle in catalog.Modules)
    {
        Console.WriteLine($"{handle.Descriptor.Id} - {handle.Descriptor.Name} " +
                          $"v{handle.Descriptor.Version}");
    }

    return 0;
}
```

**Behind the scenes in ModuleCatalog.DiscoverAsync:**
```csharp
public async Task DiscoverAsync(
    ModuleCatalogOptions options,
    CancellationToken cancellationToken = default)
{
    var candidateFiles = ModuleLocator.EnumerateCandidateFiles(options);

    foreach (var file in candidateFiles)
    {
        try
        {
            // Factory method used here
            var handle = await ModuleHandle.LoadAsync(file, options, cancellationToken);
            _modules.Add(handle);
        }
        catch (Exception ex)
        {
            // Log or ignore failed loads
            Console.WriteLine($"Failed to load {file}: {ex.Message}");
        }
    }
}
```

### Example 3: Custom Factory with Validation

```csharp
public sealed record ModuleDescriptorValidated(
    string Id,
    string Name,
    Version Version,
    string? Description = null)
{
    // Factory with validation
    public static ModuleDescriptorValidated Create(
        string id,
        string name,
        Version version,
        string? description = null)
    {
        // Validate ID format
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Module ID cannot be empty", nameof(id));

        if (!Regex.IsMatch(id, @"^[a-z0-9]+([.-][a-z0-9]+)*$"))
            throw new ArgumentException(
                "Module ID must be lowercase alphanumeric with dots or hyphens",
                nameof(id));

        // Validate name
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Module name cannot be empty", nameof(name));

        // Validate version
        if (version is null)
            throw new ArgumentNullException(nameof(version));

        // All validations passed, create instance
        return new ModuleDescriptorValidated(id, name, version, description);
    }
}

// Usage:
try
{
    var descriptor = ModuleDescriptorValidated.Create(
        id: "valid.module.id",
        name: "Valid Module",
        version: new Version(1, 0, 0));
}
catch (ArgumentException ex)
{
    Console.WriteLine($"Invalid descriptor: {ex.Message}");
}
```

### Example 4: Factory with Caching

```csharp
public sealed class CommandDescriptorCache
{
    private static readonly ConcurrentDictionary<string, CommandDescriptor> Cache = new();

    public static CommandDescriptor GetOrCreate(
        string id,
        string title,
        Func<CommandDescriptor> factory)
    {
        return Cache.GetOrAdd(id, _ => factory());
    }

    // Usage:
    public static CommandDescriptor PingDescriptor =>
        GetOrCreate(
            "shell.ping",
            "Ping",
            () => CommandDescriptor.Create(
                id: "shell.ping",
                title: "Ping",
                description: "Checks connectivity"));
}
```

---

## File References

### Descriptor Factory Methods

| File | Lines | Factory Method |
|------|-------|----------------|
| `/src/Visora.Contracts/Modules/ModuleDescriptor.cs` | 14-21 | `ModuleDescriptor.Create()` |
| `/src/Visora.Contracts/Components/ComponentDescriptor.cs` | 16-17 | `ComponentDescriptor.Create()` |
| `/src/Visora.Contracts/Commands/CommandDescriptor.cs` | 20-30 | `CommandDescriptor.Create()` |

### Result Object Factories

| File | Lines | Factory Methods |
|------|-------|-----------------|
| `/src/Visora.Contracts/Commands/CommandResult.cs` | N/A | `Success()`, `Failed()`, `Cancelled()` |

### Async Factory Methods

| File | Lines | Factory Method |
|------|-------|----------------|
| `/src/Visora.Core/Modules/ModuleHandle.cs` | 51-83 | `ModuleHandle.LoadAsync()` |

### Usage Examples

| File | Lines | Usage Pattern |
|------|-------|---------------|
| `/src/Visora.Shell.Commands.Core/ShellCommandsModule.cs` | ~15-25 | Using `ModuleDescriptor.Create()` |
| `/src/Visora.Shell.Commands.Core/Commands/PingCommand.cs` | ~10-17 | Using `CommandDescriptor.Create()` |
| `/src/Visora.Shell.Commands.Core/Commands/PingCommand.cs` | ~25-35 | Using `CommandResult.Success()` |
| `/src/Visora.Core/Modules/ModuleCatalog.cs` | ~40-60 | Using `ModuleHandle.LoadAsync()` |

---

## Static vs Instance Factories

### Static Factories (VISORA's Choice)

**Advantages:**
- No need to instantiate factory class
- Co-located with the type being created
- Simpler API surface
- Common .NET idiom

**Example:**
```csharp
var descriptor = ModuleDescriptor.Create(...);  // Static method
```

**When to Use:**
- Creating simple objects
- No shared state needed across factory calls
- Factory logic is closely tied to the type

### Instance Factories (Alternative)

**Advantages:**
- Can maintain state (e.g., caches, connection pools)
- Can be injected via DI
- Can be mocked for testing

**Example:**
```csharp
public interface IModuleHandleFactory
{
    Task<ModuleHandle> LoadAsync(string path, CancellationToken ct);
}

public class ModuleHandleFactory : IModuleHandleFactory
{
    private readonly ModuleCatalogOptions _options;

    public ModuleHandleFactory(ModuleCatalogOptions options)
    {
        _options = options;
    }

    public Task<ModuleHandle> LoadAsync(string path, CancellationToken ct)
    {
        return ModuleHandle.LoadAsync(path, _options, ct);
    }
}

// Usage:
var factory = new ModuleHandleFactory(options);
var handle = await factory.LoadAsync("module.dll", ct);
```

**When to Use:**
- Need to inject factory as dependency
- Factory maintains state (cache, pool, etc.)
- Multiple factory implementations exist

### Comparison Table

| Aspect | Static Factory | Instance Factory |
|--------|----------------|------------------|
| **Invocation** | `Type.Create()` | `factory.Create()` |
| **State** | Stateless (or static state) | Can have instance state |
| **DI** | Not injectable | Injectable |
| **Testing** | Hard to mock | Easy to mock |
| **Simplicity** | Simpler | More complex |
| **VISORA Usage** | Descriptors, Results | Not currently used |

---

## Async Factory Patterns

### Why Async Factories?

**Problem:** .NET constructors cannot be async.

```csharp
// ❌ This is not allowed:
public class ModuleHandle
{
    public async ModuleHandle(string path) // ❌ Constructors can't be async
    {
        await LoadAssemblyAsync(path);
    }
}
```

**Solution:** Use async static factory methods.

```csharp
// ✅ This works:
public class ModuleHandle
{
    private ModuleHandle(...) { /* sync construction */ }

    public static async Task<ModuleHandle> LoadAsync(string path)
    {
        var assembly = await LoadAssemblyAsync(path);
        return new ModuleHandle(path, assembly);
    }
}
```

### VISORA's Async Factory: ModuleHandle.LoadAsync

**Pattern Breakdown:**

```csharp
public static Task<ModuleHandle> LoadAsync(
    string assemblyPath,
    ModuleCatalogOptions options,
    CancellationToken cancellationToken)
{
    // Step 1: Validate inputs
    if (options is null) throw new ArgumentNullException(nameof(options));
    cancellationToken.ThrowIfCancellationRequested();

    // Step 2: Create plugin loader (sync)
    var loader = PluginLoader.CreateFromAssemblyFile(...);

    try
    {
        // Step 3: Load assembly (sync, but could be async)
        var assembly = loader.LoadDefaultAssembly();

        // Step 4: Reflection-based discovery (sync)
        var moduleType = assembly.GetTypes()...;

        // Step 5: Instantiate and validate (sync)
        var module = Activator.CreateInstance(moduleType) as VisoraModule;
        var descriptor = module.Descriptor ?? throw ...;

        // Step 6: Create context (sync)
        var context = new ModuleContext(...);

        // Step 7: Return handle
        return Task.FromResult(new ModuleHandle(...));
    }
    catch
    {
        loader.Dispose(); // Cleanup on error
        throw;
    }
}
```

**Key Points:**
- Returns `Task<ModuleHandle>` (not `async Task<ModuleHandle>`)
- Currently synchronous (uses `Task.FromResult`)
- Signature allows for future async operations
- Could become truly async if plugin loading becomes I/O-bound

### When to Use Async Factories

**Use Cases:**
1. **I/O Operations:** Loading files, network calls, database queries
2. **Long-Running Operations:** Complex computations that should be cancellable
3. **Resource Initialization:** Setting up connections, warming caches
4. **Future-Proofing:** Even if currently sync, async signature allows evolution

**Example: Truly Async Factory**

```csharp
public sealed class RemoteModuleHandle
{
    private RemoteModuleHandle(...) { }

    public static async Task<RemoteModuleHandle> LoadFromUrlAsync(
        string url,
        CancellationToken cancellationToken)
    {
        // Truly async operation: download module from URL
        using var client = new HttpClient();
        var bytes = await client.GetByteArrayAsync(url, cancellationToken);

        // Save to temp file
        var tempPath = Path.GetTempFileName();
        await File.WriteAllBytesAsync(tempPath, bytes, cancellationToken);

        // Load locally
        var handle = await ModuleHandle.LoadAsync(tempPath, options, cancellationToken);

        return new RemoteModuleHandle(handle, tempPath);
    }
}
```

### Async Factory Best Practices

1. **Accept CancellationToken**
   ```csharp
   public static async Task<T> LoadAsync(..., CancellationToken ct)
   {
       ct.ThrowIfCancellationRequested(); // Check at start
       // ... operations that honor ct
   }
   ```

2. **Use ConfigureAwait(false) in Libraries**
   ```csharp
   var data = await LoadDataAsync().ConfigureAwait(false);
   ```

3. **Handle Exceptions and Cleanup**
   ```csharp
   try
   {
       var resource = CreateResource();
       var data = await LoadDataAsync();
       return new Handle(resource, data);
   }
   catch
   {
       resource?.Dispose(); // Cleanup on error
       throw;
   }
   ```

4. **Return Task<T>, Not async Task<T>, If Sync**
   ```csharp
   // ✅ Good (if operation is actually sync):
   public static Task<T> LoadAsync(...)
   {
       var result = SyncOperation();
       return Task.FromResult(result);
   }

   // ❌ Avoid (unnecessary async state machine):
   public static async Task<T> LoadAsync(...)
   {
       var result = SyncOperation();
       return result;
   }
   ```

---

## Error Handling in Factories

### Validation Exceptions

**Pattern:** Validate inputs and throw `ArgumentException` or `ArgumentNullException`.

```csharp
public static ModuleDescriptor Create(string id, string name, Version version)
{
    if (string.IsNullOrWhiteSpace(id))
        throw new ArgumentException("ID cannot be empty", nameof(id));

    if (string.IsNullOrWhiteSpace(name))
        throw new ArgumentException("Name cannot be empty", nameof(name));

    if (version is null)
        throw new ArgumentNullException(nameof(version));

    return new ModuleDescriptor(id, name, version);
}
```

### Construction Failures

**Pattern:** Throw `InvalidOperationException` with descriptive messages.

```csharp
public static Task<ModuleHandle> LoadAsync(string path, ...)
{
    var loader = PluginLoader.CreateFromAssemblyFile(path, ...);

    try
    {
        var assembly = loader.LoadDefaultAssembly();
        var moduleType = assembly.GetTypes()...;

        if (moduleType is null)
            throw new InvalidOperationException(
                $"No VisoraModule implementation found in '{path}'.");

        var module = Activator.CreateInstance(moduleType) as VisoraModule;
        if (module is null)
            throw new InvalidOperationException(
                $"Unable to create module instance '{moduleType.FullName}'.");

        return Task.FromResult(new ModuleHandle(...));
    }
    catch
    {
        loader.Dispose(); // IMPORTANT: Cleanup on failure
        throw;
    }
}
```

### Resource Cleanup on Failure

**Always clean up resources if factory fails:**

```csharp
public static async Task<DatabaseConnection> ConnectAsync(string connectionString)
{
    var connection = new SqlConnection(connectionString);
    try
    {
        await connection.OpenAsync();
        return new DatabaseConnection(connection);
    }
    catch
    {
        connection.Dispose(); // Cleanup on failure
        throw;
    }
}
```

### Try-Create Pattern (Alternative)

**Pattern:** Return bool/out parameter instead of throwing exceptions.

```csharp
public static bool TryCreate(
    string id,
    string name,
    Version version,
    out ModuleDescriptor? descriptor)
{
    if (string.IsNullOrWhiteSpace(id) ||
        string.IsNullOrWhiteSpace(name) ||
        version is null)
    {
        descriptor = null;
        return false;
    }

    descriptor = new ModuleDescriptor(id, name, version);
    return true;
}

// Usage:
if (ModuleDescriptor.TryCreate(id, name, version, out var descriptor))
{
    // Success
    Console.WriteLine($"Created: {descriptor.Name}");
}
else
{
    // Failure (no exception thrown)
    Console.WriteLine("Invalid descriptor inputs");
}
```

**When to Use Try-Create:**
- Failure is expected and not exceptional
- Performance-critical paths (avoid exception overhead)
- User input validation (failures are common)

**When to Use Exceptions:**
- Failures indicate programming errors
- Construction is critical (must succeed or fail loudly)
- Simplifies code (no bool checks everywhere)

---

## Testing Factories

### Example 1: Testing Descriptor Factories

```csharp
[TestClass]
public class ModuleDescriptorTests
{
    [TestMethod]
    public void Create_WithValidInputs_ReturnsDescriptor()
    {
        // Arrange
        var id = "test.module";
        var name = "Test Module";
        var version = new Version(1, 0, 0);

        // Act
        var descriptor = ModuleDescriptor.Create(id, name, version);

        // Assert
        Assert.AreEqual(id, descriptor.Id);
        Assert.AreEqual(name, descriptor.Name);
        Assert.AreEqual(version, descriptor.Version);
    }

    [TestMethod]
    public void Create_WithOptionalParameters_ReturnsDescriptor()
    {
        // Arrange & Act
        var descriptor = ModuleDescriptor.Create(
            id: "test.module",
            name: "Test Module",
            version: new Version(1, 0, 0),
            description: "Test description",
            tags: new Dictionary<string, string> { ["key"] = "value" }.AsReadOnly());

        // Assert
        Assert.AreEqual("Test description", descriptor.Description);
        Assert.IsNotNull(descriptor.Tags);
        Assert.AreEqual("value", descriptor.Tags["key"]);
    }
}
```

### Example 2: Testing Async Factories

```csharp
[TestClass]
public class ModuleHandleTests
{
    [TestMethod]
    public async Task LoadAsync_WithValidModule_ReturnsHandle()
    {
        // Arrange
        var modulePath = GetTestModulePath();
        var options = new ModuleCatalogOptions
        {
            Capabilities = CapabilityProviders.Empty
        };

        // Act
        var handle = await ModuleHandle.LoadAsync(modulePath, options, CancellationToken.None);

        // Assert
        Assert.IsNotNull(handle);
        Assert.IsNotNull(handle.Module);
        Assert.IsNotNull(handle.Descriptor);
        Assert.AreEqual("test.module", handle.Descriptor.Id);
    }

    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public async Task LoadAsync_WithInvalidModule_ThrowsException()
    {
        // Arrange
        var invalidPath = "invalid.dll";
        var options = new ModuleCatalogOptions();

        // Act & Assert
        await ModuleHandle.LoadAsync(invalidPath, options, CancellationToken.None);
    }
}
```

### Example 3: Testing Result Factories

```csharp
[TestClass]
public class CommandResultTests
{
    [TestMethod]
    public void Success_ReturnsSuccessOutcome()
    {
        // Act
        var result = CommandResult.Success("Operation succeeded");

        // Assert
        Assert.AreEqual(CommandOutcome.Success, result.Outcome);
        Assert.AreEqual("Operation succeeded", result.Message);
    }

    [TestMethod]
    public void Success_WithPayload_ReturnsSuccessWithPayload()
    {
        // Arrange
        var payload = new { Count = 42 };

        // Act
        var result = CommandResult.Success("Success", payload);

        // Assert
        Assert.AreEqual(CommandOutcome.Success, result.Outcome);
        Assert.IsNotNull(result.Payload);
    }

    [TestMethod]
    public void Failed_ReturnsFailedOutcome()
    {
        // Act
        var result = CommandResult.Failed("Operation failed");

        // Assert
        Assert.AreEqual(CommandOutcome.Failed, result.Outcome);
        Assert.AreEqual("Operation failed", result.Message);
    }
}
```

### Example 4: Mocking Factories for Testing

```csharp
public interface IModuleHandleFactory
{
    Task<ModuleHandle> LoadAsync(string path, CancellationToken ct);
}

[TestClass]
public class ModuleCatalogTests
{
    [TestMethod]
    public async Task DiscoverAsync_UsesFactory()
    {
        // Arrange
        var mockFactory = new Mock<IModuleHandleFactory>();
        var mockHandle = new Mock<ModuleHandle>();

        mockFactory
            .Setup(f => f.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockHandle.Object);

        var catalog = new ModuleCatalog(mockFactory.Object);

        // Act
        await catalog.DiscoverAsync(options, CancellationToken.None);

        // Assert
        mockFactory.Verify(f => f.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }
}
```

---

## Best Practices

### 1. Use Named Parameters in Factory Methods

**✅ Good:**
```csharp
var descriptor = ModuleDescriptor.Create(
    id: "test.module",
    name: "Test Module",
    version: new Version(1, 0, 0));
```

**❌ Avoid:**
```csharp
var descriptor = ModuleDescriptor.Create(
    "test.module",     // What is this?
    "Test Module",     // What is this?
    new Version(1, 0, 0));
```

### 2. Make Constructors Private for Async Factories

**✅ Good:**
```csharp
public class ModuleHandle
{
    private ModuleHandle(...) { } // Private constructor

    public static Task<ModuleHandle> LoadAsync(...) { } // Only entry point
}
```

**❌ Avoid:**
```csharp
public class ModuleHandle
{
    public ModuleHandle(...) { } // Public constructor

    public static Task<ModuleHandle> LoadAsync(...) { } // Two ways to create!
}
```

### 3. Validate Inputs at Factory Entry

```csharp
public static ModuleDescriptor Create(string id, string name, Version version)
{
    // Validate first, construct last
    if (string.IsNullOrWhiteSpace(id))
        throw new ArgumentException("ID cannot be empty", nameof(id));

    if (string.IsNullOrWhiteSpace(name))
        throw new ArgumentException("Name cannot be empty", nameof(name));

    if (version is null)
        throw new ArgumentNullException(nameof(version));

    return new ModuleDescriptor(id, name, version);
}
```

### 4. Use Descriptive Factory Method Names

**✅ Good:**
```csharp
CommandResult.Success()
CommandResult.Failed()
CommandResult.Cancelled()
```

**❌ Avoid:**
```csharp
CommandResult.Create(CommandOutcome.Success) // Less clear
```

### 5. Clean Up Resources on Factory Failure

```csharp
public static Task<ModuleHandle> LoadAsync(...)
{
    var loader = PluginLoader.CreateFromAssemblyFile(...);
    try
    {
        // Construction logic
        return Task.FromResult(new ModuleHandle(...));
    }
    catch
    {
        loader.Dispose(); // IMPORTANT
        throw;
    }
}
```

### 6. Use Static Fields for Constant Descriptors

```csharp
public class PingCommand
{
    // ✅ Create once, reuse
    private static readonly CommandDescriptor Info = CommandDescriptor.Create(...);

    public override CommandDescriptor Descriptor => Info;
}
```

---

## Advanced Topics

### Topic 1: Generic Factory Methods

```csharp
public static class DescriptorFactory
{
    public static TDescriptor Create<TDescriptor>(Func<TDescriptor> factory)
        where TDescriptor : class
    {
        var descriptor = factory();
        if (descriptor is null)
            throw new InvalidOperationException("Factory returned null");
        return descriptor;
    }
}

// Usage:
var descriptor = DescriptorFactory.Create(() =>
    ModuleDescriptor.Create("id", "Name", new Version(1, 0, 0)));
```

### Topic 2: Lazy Factories

```csharp
public class LazyModuleHandle
{
    private readonly Lazy<Task<ModuleHandle>> _lazyHandle;

    public LazyModuleHandle(string path, ModuleCatalogOptions options)
    {
        _lazyHandle = new Lazy<Task<ModuleHandle>>(
            () => ModuleHandle.LoadAsync(path, options, CancellationToken.None));
    }

    public Task<ModuleHandle> GetHandleAsync() => _lazyHandle.Value;
}

// Usage:
var lazyHandle = new LazyModuleHandle("module.dll", options);
// Module not loaded yet...
var handle = await lazyHandle.GetHandleAsync(); // Loaded on first access
```

### Topic 3: Factory with Dependency Injection

```csharp
public interface IDescriptorFactory
{
    ModuleDescriptor CreateModuleDescriptor(string id, string name, Version version);
}

public class DescriptorFactory : IDescriptorFactory
{
    private readonly IValidator<ModuleDescriptor> _validator;

    public DescriptorFactory(IValidator<ModuleDescriptor> validator)
    {
        _validator = validator;
    }

    public ModuleDescriptor CreateModuleDescriptor(string id, string name, Version version)
    {
        var descriptor = ModuleDescriptor.Create(id, name, version);

        var validationResult = _validator.Validate(descriptor);
        if (!validationResult.IsValid)
            throw new ValidationException(validationResult.Errors);

        return descriptor;
    }
}
```

---

## Common Pitfalls

### Pitfall 1: Forgetting to Dispose on Factory Failure

```csharp
// ❌ BAD: Resource leak on exception
public static Task<ModuleHandle> LoadAsync(...)
{
    var loader = PluginLoader.CreateFromAssemblyFile(...);
    var assembly = loader.LoadDefaultAssembly(); // May throw
    return Task.FromResult(new ModuleHandle(...));
}

// ✅ GOOD: Cleanup on exception
public static Task<ModuleHandle> LoadAsync(...)
{
    var loader = PluginLoader.CreateFromAssemblyFile(...);
    try
    {
        var assembly = loader.LoadDefaultAssembly();
        return Task.FromResult(new ModuleHandle(...));
    }
    catch
    {
        loader.Dispose(); // Cleanup
        throw;
    }
}
```

### Pitfall 2: Unnecessary Async State Machine

```csharp
// ❌ BAD: async keyword not needed
public static async Task<ModuleDescriptor> LoadAsync(...)
{
    var descriptor = ModuleDescriptor.Create(...); // Sync operation
    return descriptor;
}

// ✅ GOOD: Return Task.FromResult
public static Task<ModuleDescriptor> LoadAsync(...)
{
    var descriptor = ModuleDescriptor.Create(...);
    return Task.FromResult(descriptor);
}
```

### Pitfall 3: Exposing Public Constructor with Async Factory

```csharp
// ❌ BAD: Two ways to create object
public class ModuleHandle
{
    public ModuleHandle(...) { } // Public
    public static Task<ModuleHandle> LoadAsync(...) { }
}

// Clients can bypass factory:
var handle = new ModuleHandle(...); // Skips validation, async logic!

// ✅ GOOD: Force factory usage
public class ModuleHandle
{
    private ModuleHandle(...) { } // Private
    public static Task<ModuleHandle> LoadAsync(...) { }
}
```

---

## Summary

### Key Takeaways

1. **Factory Methods Provide Control**
   - Validation, error handling, resource management
   - Named parameters for clarity
   - Alternative constructors (Success, Failed, etc.)

2. **Static Factories for Simplicity**
   - Co-located with type
   - No need for factory instances
   - Common .NET idiom

3. **Async Factories for I/O**
   - Constructors can't be async
   - Factory methods return `Task<T>`
   - Enable cancellation and async I/O

4. **Error Handling is Critical**
   - Validate inputs early
   - Clean up resources on failure
   - Throw meaningful exceptions

5. **Testing Benefits**
   - Easy to test (static methods)
   - Can wrap in interfaces for mocking
   - Validation logic is centralized

### Common Patterns in VISORA

- **Descriptor.Create()**: Static factories for immutable metadata
- **CommandResult.Success/Failed()**: Named factories for result types
- **ModuleHandle.LoadAsync()**: Async factory for complex construction

### Anti-Patterns to Avoid

- ❌ Public constructors alongside async factories
- ❌ Forgetting to clean up on factory failure
- ❌ Unnecessary async state machines
- ❌ Positional parameters without names

### Related Patterns

- **Immutable Metadata:** Factories create immutable objects
- **Builder Pattern:** Alternative for complex construction
- **Async Patterns:** Async factories enable async construction
- **Result Objects:** Factories for different result types

---

**Last Updated:** November 10, 2025
**VISORA Version:** .NET 9.0
**Pattern Tier:** Tier 3 (Data & Metadata)
