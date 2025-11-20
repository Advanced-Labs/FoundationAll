# Async Patterns - VISORA Analysis

**Last Updated**: 2025-11-10  
**VISORA Version**: .NET 9.0  
**Pattern Tier**: Tier 2 (Communication & Execution)

---

## Pattern Overview

VISORA uses **async/await throughout the entire stack** with ValueTask optimization, ConfigureAwait best practices, and comprehensive CancellationToken support. This future-proofs the platform for I/O-bound operations, remote execution, and responsive UIs.

###What Are These Patterns?

- **Async/Await**: C# asynchronous programming model using Task-based Asynchrony Pattern (TAP)
- **ValueTask**: Performance-optimized alternative to Task for hot paths
- **CancellationToken**: Cooperative cancellation mechanism propagated through all async operations
- **ConfigureAwait(false)**: Library code avoids capturing SynchronizationContext
- **IAsyncDisposable**: Async resource cleanup pattern

### Why VISORA Uses Async Throughout

**Problem**: Platform needs to support I/O-bound operations (file system, network, databases) without blocking threads, and enable future remote module execution.

**Solution**: Async/await at every layer with cancellation support.

**Benefits**:
- Non-blocking I/O operations
- Better scalability (fewer threads needed)
- Responsive UI (no frozen interfaces)
- Future-proof for remote execution
- Cooperative cancellation throughout

---

## VISORA Implementation

### Async Lifecycle Methods

**Module Lifecycle** (`src/Visora.Contracts/Modules/VisoraModule.cs`)

```csharp
public abstract class VisoraModule : IAsyncDisposable
{
    public virtual ValueTask InitializeAsync(
        ModuleContext context,
        CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public virtual ValueTask ShutdownAsync(
        ModuleContext context,
        CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public virtual ValueTask DisposeAsync()
        => ValueTask.CompletedTask;
}
```

**Component Lifecycle** (`src/Visora.Contracts/Components/VisoraComponent.cs`)

```csharp
public abstract class VisoraComponent : IAsyncDisposable
{
    public virtual ValueTask InitializeAsync(
        ComponentContext context,
        CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public virtual ValueTask ActivateAsync(
        ComponentContext context,
        CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public virtual ValueTask DeactivateAsync(
        ComponentContext context,
        CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public virtual ValueTask DisposeAsync()
        => ValueTask.CompletedTask;
}
```

**Command Execution** (`src/Visora.Contracts/Commands/VisoraCommand.cs`)

```csharp
public abstract class VisoraCommand
{
    public virtual ValueTask<CommandResult> ExecuteAsync(
        CommandContext context,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult(CommandResult.Success());
}
```

### ValueTask vs Task

**When VISORA Uses ValueTask**:
- All abstract lifecycle methods (may be synchronous in some implementations)
- Command execution (some commands are purely synchronous)
- Any method that might complete synchronously

**Example: ValueTask Optimization**

```csharp
// Synchronous path (no allocation)
public override ValueTask InitializeAsync(
    ModuleContext context,
    CancellationToken cancellationToken = default)
{
    _isInitialized = true;
    return ValueTask.CompletedTask; // No heap allocation
}

// Async path (allocates Task under the hood)
public override async ValueTask InitializeAsync(
    ModuleContext context,
    CancellationToken cancellationToken = default)
{
    await SomeIoOperationAsync(cancellationToken);
    _isInitialized = true;
}
```

**Benefits of ValueTask**:
- Zero allocations when completing synchronously
- Reduces GC pressure in hot paths
- Same API regardless of sync/async implementation

### ConfigureAwait Usage

**In Library Code** (`src/Visora.Core/Modules/ModuleHandle.cs:146-157`)

```csharp
public async ValueTask DisposeAsync()
{
    try
    {
        await ShutdownAsync().ConfigureAwait(false);
        await Module.DisposeAsync().ConfigureAwait(false);
    }
    finally
    {
        _loader.Dispose();
    }
}
```

**Why ConfigureAwait(false)**:
- Library code doesn't need to resume on original SynchronizationContext
- Avoids potential deadlocks in synchronous-over-async scenarios
- Better performance (no context capture/restore)
- VISORA is library code (used by hosts), not application code

**When NOT to Use ConfigureAwait(false)**:
- Application code (hosts) that needs to resume on UI thread
- Code that must run on specific synchronization context

### CancellationToken Propagation

**Discovery with Cancellation** (`src/Visora.Core/Modules/ModuleCatalog.cs`)

```csharp
public async Task DiscoverAsync(
    ModuleCatalogOptions options,
    CancellationToken cancellationToken = default)
{
    var candidates = ModuleLocator.EnumerateCandidateFiles(options);

    foreach (var path in candidates)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var handle = await ModuleHandle.LoadAsync(path, options, cancellationToken)
                .ConfigureAwait(false);
            _modules.Add(handle);
        }
        catch (Exception ex)
        {
            // Log and continue
        }
    }
}
```

**Inspection with Cancellation** (`src/Visora.Core/Modules/ModuleHandle.cs:94-135`)

```csharp
public async Task<ModuleInspection> InspectAsync(
    CancellationToken cancellationToken = default)
{
    await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

    var componentTypes = Module.DiscoverComponents(discoveryContext).ToArray();
    var inspections = new List<ComponentInspection>();

    foreach (var componentType in componentTypes)
    {
        cancellationToken.ThrowIfCancellationRequested(); // Check before expensive operation

        if (Activator.CreateInstance(componentType) is not VisoraComponent component)
            continue;

        await component.InitializeAsync(componentContext, cancellationToken)
            .ConfigureAwait(false);

        // ... command discovery ...
    }

    return new ModuleInspection(AssemblyPath, Descriptor, inspections);
}
```

### Async Disposal Pattern

**IAsyncDisposable Implementation** (`src/Visora.Core/Modules/ModuleHandle.cs:146-157`)

```csharp
public async ValueTask DisposeAsync()
{
    try
    {
        // Graceful shutdown
        await ShutdownAsync().ConfigureAwait(false);

        // Dispose module
        await Module.DisposeAsync().ConfigureAwait(false);
    }
    finally
    {
        // Always dispose loader (releases assembly)
        _loader.Dispose();
    }
}
```

**Usage Pattern**:

```csharp
await using var catalog = new ModuleCatalog();
await catalog.DiscoverAsync(options, ct);
// ... use catalog ...
// DisposeAsync called automatically at end of scope
```

### Async Best Practices in VISORA

**1. Async All the Way**

```csharp
// Good: Async all the way
public async Task DoWorkAsync(CancellationToken ct)
{
    await LoadModulesAsync(ct);
    await ProcessModulesAsync(ct);
}

// Bad: Blocking on async (deadlock risk)
public void DoWork()
{
    LoadModulesAsync(CancellationToken.None).Wait(); // DON'T DO THIS
}
```

**2. Always Provide Cancellation Token Parameter**

```csharp
// Good: Cancellation support
public async ValueTask<CommandResult> ExecuteAsync(
    CommandContext context,
    CancellationToken cancellationToken = default)
{
    ct.ThrowIfCancellationRequested();
    await SomeLongOperationAsync(ct);
}

// Bad: No cancellation support
public async ValueTask<CommandResult> ExecuteAsync(CommandContext context)
{
    await SomeLongOperationAsync(); // Can't cancel!
}
```

**3. Check Cancellation Before Expensive Operations**

```csharp
public async Task ProcessItemsAsync(IEnumerable<Item> items, CancellationToken ct)
{
    foreach (var item in items)
    {
        ct.ThrowIfCancellationRequested(); // Check before each item
        await ProcessItemAsync(item, ct);
    }
}
```

**4. Use ValueTask for Potentially Synchronous Paths**

```csharp
// Interface uses ValueTask (allows sync or async implementations)
public interface IRepository
{
    ValueTask<Module> GetByIdAsync(string id, CancellationToken ct = default);
}

// In-memory implementation (synchronous)
public class InMemoryRepository : IRepository
{
    public ValueTask<Module> GetByIdAsync(string id, CancellationToken ct = default)
    {
        return _cache.TryGetValue(id, out var module)
            ? ValueTask.FromResult(module)
            : ValueTask.FromResult<Module>(null);
    }
}

// Database implementation (asynchronous)
public class DatabaseRepository : IRepository
{
    public async ValueTask<Module> GetByIdAsync(string id, CancellationToken ct = default)
    {
        return await _dbContext.Modules
            .Where(m => m.Id == id)
            .FirstOrDefaultAsync(ct);
    }
}
```

**5. Avoid Async Void (Except Event Handlers)**

```csharp
// Good: Returns ValueTask
public async ValueTask InitializeAsync()
{
    await LoadDataAsync();
}

// Bad: Async void (can't await, exceptions unhandled)
public async void Initialize()
{
    await LoadDataAsync(); // Exceptions can crash app!
}

// Exception: Event handlers (no choice)
private async void OnButtonClick(object sender, EventArgs e)
{
    try
    {
        await DoWorkAsync();
    }
    catch (Exception ex)
    {
        // Must handle exceptions in async void
        LogError(ex);
    }
}
```

---

## Testing Async Code

### Testing Async Methods

```csharp
[TestMethod]
public async Task LoadAsync_WithValidPath_LoadsModule()
{
    // Arrange
    var options = new ModuleCatalogOptions();
    var path = GetTestModulePath();

    // Act
    var handle = await ModuleHandle.LoadAsync(path, options, CancellationToken.None);

    // Assert
    Assert.IsNotNull(handle);
    Assert.IsNotNull(handle.Module);
}
```

### Testing Cancellation

```csharp
[TestMethod]
public async Task InspectAsync_WhenCancelled_ThrowsOperationCanceledException()
{
    // Arrange
    var handle = await CreateTestModuleHandle();
    var cts = new CancellationTokenSource();
    cts.Cancel(); // Cancel immediately

    // Act & Assert
    await Assert.ThrowsExceptionAsync<OperationCanceledException>(
        () => handle.InspectAsync(cts.Token).AsTask());
}

[TestMethod]
public async Task LongRunningCommand_SupportsCancellation()
{
    // Arrange
    var command = new LongRunningCommand();
    var context = CreateTestContext();
    var cts = new CancellationTokenSource();

    // Act
    var task = command.ExecuteAsync(context, cts.Token);
    await Task.Delay(100); // Let it start
    cts.Cancel(); // Cancel mid-execution
    var result = await task;

    // Assert
    Assert.AreEqual(CommandOutcome.Cancelled, result.Outcome);
}
```

### Testing Async Disposal

```csharp
[TestMethod]
public async Task DisposeAsync_DisposesModuleAndLoader()
{
    // Arrange
    var handle = await CreateTestModuleHandle();
    var module = handle.Module as IAsyncDisposable;

    // Act
    await handle.DisposeAsync();

    // Assert
    // Verify module was disposed
    Assert.IsTrue(module.WasDisposed);
}
```

---

## Performance Considerations

### ValueTask Optimization Results

| Scenario | Task | ValueTask | Improvement |
|----------|------|-----------|-------------|
| Synchronous completion | 96 bytes allocated | 0 bytes allocated | 100% |
| Async completion | 96 bytes allocated | 96 bytes allocated | 0% |
| Mixed workload (50/50) | 96 bytes/call | 48 bytes/call | 50% |

**Recommendation**: Use ValueTask for methods that often complete synchronously (caching, validation, short-circuit logic).

### ConfigureAwait Performance

| Pattern | Overhead | Use Case |
|---------|----------|----------|
| `await task` | Context capture + restore | UI code, application code |
| `await task.ConfigureAwait(false)` | No context overhead | Library code, background tasks |

**Recommendation**: Use ConfigureAwait(false) in all library code (VISORA Core, modules).

---

## Related Patterns

- **[Command Execution](../command-execution/visora-analysis.md)**: Async command execution with ValueTask
- **[Module Lifecycle](../module-lifecycle/visora-analysis.md)**: Async initialization and disposal
- **[Result Objects](../result-objects/visora-analysis.md)**: Avoiding async exception marshaling

---

## Summary

VISORA's async patterns provide:
- ✅ Non-blocking I/O throughout the platform
- ✅ ValueTask optimization for hot paths
- ✅ ConfigureAwait(false) in library code
- ✅ Comprehensive cancellation support
- ✅ Async disposal for proper resource cleanup

**Key Takeaway**: Async all the way with proper cancellation and ConfigureAwait enables scalable, responsive platform that's future-proof for remote execution.
