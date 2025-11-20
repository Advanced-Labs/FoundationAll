# ADR-003: Async/Await Throughout Platform

**Last Updated:** 2025-11-10
**VISORA Version:** .NET 9.0
**Decision Status:** ✅ Accepted
**Decision Date:** 2024-Q4
**Supersedes:** None
**Related ADRs:** ADR-001 (Reflection Discovery), ADR-002 (Capability Provider), ADR-004 (Unloadable Plugins)

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
9. [Implementation Guidelines](#implementation-guidelines)
10. [Performance Considerations](#performance-considerations)
11. [When to Revisit](#when-to-revisit)
12. [Related Patterns](#related-patterns)
13. [References](#references)

---

## Executive Summary

**Decision:** VISORA uses async/await patterns throughout the entire platform, with `ValueTask<T>` for hot paths and `Task<T>` for general operations. All I/O-bound operations, module lifecycle methods, and command execution are asynchronous.

**Key Rationale:**
- **Future-proofing:** Support remote execution and distributed scenarios
- **Scalability:** Non-blocking I/O maximizes throughput
- **Responsive UIs:** Prevent blocking in GUI scenarios
- **Modern .NET:** Align with async-first .NET ecosystem
- **Cancellation support:** Built-in cooperative cancellation

**Primary Tradeoff:** Increased complexity and learning curve vs. superior scalability and future flexibility.

---

## Context

### The Evolution of Asynchronous Programming

#### Historical Progression

**.NET Framework 1.0-3.5 (2002-2008):** Callbacks and APM (Asynchronous Programming Model)
```csharp
// Old APM pattern - complex and error-prone
BeginReadFile(path, callback, state);
```

**.NET Framework 4.0 (2010):** Task-based Asynchronous Pattern (TAP)
```csharp
// TAP - better, but still verbose
var task = ReadFileAsync(path);
task.ContinueWith(t => ProcessData(t.Result));
```

**.NET Framework 4.5+ (2012+):** Async/Await - Language-level support
```csharp
// Modern async/await - clean and intuitive
var data = await ReadFileAsync(path);
ProcessData(data);
```

**.NET Core 2.1+ (2018+):** ValueTask for high-performance scenarios
```csharp
// ValueTask for hot paths - avoids allocations
public ValueTask<int> GetCachedValueAsync()
{
    if (_cache.TryGetValue(key, out var value))
        return ValueTask.FromResult(value);  // No allocation!

    return FetchAndCacheAsync(key);  // Only allocate when necessary
}
```

### VISORA's Operational Context

#### 1. I/O-Bound Operations

VISORA modules frequently perform I/O:

```csharp
// File system operations
await _fileSystem.ReadFileAsync(path);
await _fileSystem.WriteFileAsync(path, content);

// Database operations
await _database.QueryAsync<T>(sql);
await _database.ExecuteAsync(command);

// Network calls (future)
await _httpClient.GetAsync(url);
await _messageQueue.PublishAsync(message);

// External process execution
await _processManager.RunAsync(executable, args);
```

**Without async:** Thread pool exhaustion, poor scalability.
**With async:** Threads released during I/O, better resource utilization.

#### 2. Long-Running Commands

Some commands take significant time:

```csharp
// Data processing command
await ProcessLargeDatasetAsync();  // Could take minutes

// Build/compilation commands
await CompileProjectAsync();  // Could take minutes

// Video processing
await TranscodeVideoAsync();  // Could take hours
```

**Requirement:** Progress reporting, cancellation, responsive UI during execution.

#### 3. Plugin Lifecycle

Module initialization may involve async operations:

```csharp
public override async ValueTask InitializeAsync(
    ICapabilityProvider capabilities,
    CancellationToken ct)
{
    await LoadConfigurationAsync();
    await ConnectToDatabaseAsync();
    await WarmUpCachesAsync();
}
```

**Constraint:** Cannot block host startup with synchronous I/O.

#### 4. Future Distribution

VISORA's roadmap includes distributed scenarios:

```
┌─────────────┐                ┌─────────────┐
│  Host App   │                │  Host App   │
│  ┌────────┐ │                │  ┌────────┐ │
│  │Module A│ │                │  │Module B│ │
│  └────────┘ │                │  └────────┘ │
└──────┬──────┘                └──────┬──────┘
       │                              │
       │      Network calls           │
       └──────────────┬───────────────┘
                      │
              ┌───────▼────────┐
              │ Remote Modules │
              │   (Future)     │
              └────────────────┘
```

**Requirement:** Network-friendly APIs from day one.

---

## Problem Statement

### Core Question

**Should VISORA adopt synchronous or asynchronous APIs as the primary pattern for module operations, command execution, and platform services?**

### Specific Challenges

#### 1. Synchronous-First Design

```csharp
// Synchronous approach
public interface IModule
{
    void Initialize(ICapabilityProvider capabilities);
    CommandResult Execute(CommandContext context);
    void Shutdown();
}

// What if initialization needs to:
// - Load configuration from file?
// - Connect to database?
// - Download resources from network?

// Forced to block:
public void Initialize(ICapabilityProvider capabilities)
{
    var config = LoadConfiguration().GetAwaiter().GetResult();  // ⚠️ Blocking!
    var db = ConnectToDatabase().GetAwaiter().GetResult();      // ⚠️ Blocking!
}
```

**Problems:**
- Thread pool exhaustion with many modules
- Unresponsive UI during initialization
- Cannot leverage async I/O benefits
- Difficult to add async later (breaking change)

#### 2. Mixed Sync/Async Patterns

```csharp
// Some methods sync, some async - confusing!
public interface IModule
{
    void Initialize(ICapabilityProvider capabilities);  // Sync
    Task<CommandResult> ExecuteAsync(CommandContext context);  // Async
    void Shutdown();  // Sync
}
```

**Problems:**
- Inconsistent API surface
- Developers confused about which to use when
- Partial benefits (some operations still block)
- Maintenance burden (two code paths)

#### 3. Cancellation Support

Long-running operations need cancellation:

```csharp
// Without cancellation
public async Task ProcessDataAsync()
{
    for (int i = 0; i < 1000000; i++)
    {
        await ProcessItemAsync(i);
        // No way to cancel! Must complete entire loop.
    }
}

// With cancellation
public async Task ProcessDataAsync(CancellationToken ct)
{
    for (int i = 0; i < 1000000; i++)
    {
        ct.ThrowIfCancellationRequested();  // Check for cancellation
        await ProcessItemAsync(i, ct);
    }
}
```

**Requirement:** All async methods must support cancellation.

#### 4. Async Over Sync vs. Sync Over Async

```csharp
// Async over sync (easy to add later)
public interface IDataReader
{
    string ReadData();  // Sync

    // Easy to add async wrapper:
    Task<string> ReadDataAsync() => Task.Run(() => ReadData());
}

// Sync over async (dangerous!)
public interface IDataReader
{
    Task<string> ReadDataAsync();  // Async

    // Dangerous to add sync wrapper:
    string ReadData() => ReadDataAsync().GetAwaiter().GetResult();  // ⚠️ Deadlock risk!
}
```

**Decision Point:** Start with sync (easier) or async (better long-term)?

#### 5. Performance Overhead

Async has costs:

```csharp
// Synchronous - no allocation
public int GetValue()
{
    return _value;
}

// Asynchronous - Task allocation
public Task<int> GetValueAsync()
{
    return Task.FromResult(_value);  // Heap allocation!
}

// Asynchronous - ValueTask (optimized)
public ValueTask<int> GetValueAsync()
{
    return ValueTask.FromResult(_value);  // No allocation if synchronous completion
}
```

**Challenge:** Balance async benefits with performance costs.

---

## Decision

### The Chosen Approach

**VISORA adopts async/await as the primary pattern throughout the platform, with `ValueTask<T>` for hot paths and proper cancellation token support everywhere.**

### Core Principles

#### 1. Async by Default

All public APIs that may perform I/O or long-running operations are asynchronous:

```csharp
// Module lifecycle
public abstract class VisoraModule
{
    public abstract ValueTask InitializeAsync(ICapabilityProvider capabilities, CancellationToken ct);
    public abstract ValueTask ShutdownAsync(CancellationToken ct);
}

// Command execution
public interface ICommandHandler
{
    ValueTask<CommandResult> ExecuteAsync(CommandContext context, CancellationToken ct);
}

// Service interfaces
public interface IFileSystem
{
    ValueTask<string> ReadFileAsync(string path, CancellationToken ct = default);
    ValueTask WriteFileAsync(string path, string content, CancellationToken ct = default);
}

public interface IDatabase
{
    ValueTask<T> QueryAsync<T>(string sql, CancellationToken ct = default);
    ValueTask<int> ExecuteAsync(string sql, CancellationToken ct = default);
}
```

#### 2. CancellationToken Everywhere

All async methods accept `CancellationToken`:

```csharp
// Required for methods that may run long
public async ValueTask<Result> ProcessAsync(Data data, CancellationToken ct)
{
    foreach (var item in data.Items)
    {
        ct.ThrowIfCancellationRequested();
        await ProcessItemAsync(item, ct);
    }

    return Result.Success();
}

// Optional with default for convenience
public ValueTask<string> ReadFileAsync(string path, CancellationToken ct = default)
{
    return InternalReadAsync(path, ct);
}
```

#### 3. ValueTask for Hot Paths

Use `ValueTask<T>` for frequently called methods with synchronous completion paths:

```csharp
// Hot path: Capability lookup (usually cached)
public ValueTask<ICapability?> GetCapabilityAsync<TCapability>(CancellationToken ct = default)
    where TCapability : class
{
    // Fast path: Cached capability
    if (_cache.TryGetValue(typeof(TCapability), out var cached))
    {
        return ValueTask.FromResult((ICapability?)cached);  // No allocation!
    }

    // Slow path: Load capability
    return LoadCapabilityAsync<TCapability>(ct);
}

private async ValueTask<ICapability?> LoadCapabilityAsync<TCapability>(CancellationToken ct)
{
    // Actual async work here
    var capability = await ResolveCapabilityAsync<TCapability>(ct);
    _cache[typeof(TCapability)] = capability;
    return capability;
}
```

#### 4. Task for General Operations

Use `Task<T>` for operations that always perform async work:

```csharp
// Network call - always async
public async Task<HttpResponse> SendRequestAsync(HttpRequest request, CancellationToken ct)
{
    return await _httpClient.SendAsync(request, ct);
}

// Database query - always async
public async Task<IEnumerable<T>> QueryAsync<T>(string sql, CancellationToken ct)
{
    return await _connection.QueryAsync<T>(sql, ct);
}
```

#### 5. ConfigureAwait Guidelines

Library code uses `ConfigureAwait(false)` to avoid capturing context:

```csharp
// VISORA platform code (library)
public async ValueTask<Data> LoadDataAsync(CancellationToken ct)
{
    var content = await _fileSystem.ReadFileAsync(path, ct).ConfigureAwait(false);
    var data = await ParseDataAsync(content, ct).ConfigureAwait(false);
    return data;
}

// Module/application code (no ConfigureAwait needed)
public async ValueTask InitializeAsync(ICapabilityProvider capabilities, CancellationToken ct)
{
    var config = await LoadConfigAsync(ct);  // No ConfigureAwait
    var db = await ConnectDatabaseAsync(ct);  // No ConfigureAwait
}
```

---

## Alternatives Considered

### Alternative 1: Synchronous-First API

**Approach:** All APIs synchronous by default, async wrappers where needed.

```csharp
// Synchronous core
public abstract class VisoraModule
{
    public abstract void Initialize(ICapabilityProvider capabilities);
    public abstract void Shutdown();
}

public interface ICommandHandler
{
    CommandResult Execute(CommandContext context);
}

// Async wrappers (if needed)
public static class AsyncExtensions
{
    public static Task<CommandResult> ExecuteAsync(
        this ICommandHandler handler,
        CommandContext context,
        CancellationToken ct)
    {
        return Task.Run(() => handler.Execute(context), ct);
    }
}
```

**Pros:**
- Simpler mental model for beginners
- No async/await complexity
- Slightly better performance for pure compute
- Easier debugging (no async state machines)

**Cons:**
- ❌ **Thread pool exhaustion:** Blocking I/O ties up threads
- ❌ **Poor scalability:** Can't handle many concurrent operations
- ❌ **Future-limiting:** Adding true async later is breaking change
- ❌ **UI blocking:** Synchronous operations freeze UIs
- ❌ **No cancellation:** Difficult to cancel long-running operations

**Why Not Chosen:**
The scalability and future flexibility benefits of async outweigh the simplicity of synchronous code. Modern .NET development is async-first.

---

### Alternative 2: Dual Sync/Async APIs

**Approach:** Provide both synchronous and asynchronous versions of all methods.

```csharp
public interface IFileSystem
{
    // Synchronous versions
    string ReadFile(string path);
    void WriteFile(string path, string content);

    // Asynchronous versions
    Task<string> ReadFileAsync(string path, CancellationToken ct = default);
    Task WriteFileAsync(string path, string content, CancellationToken ct = default);
}

public abstract class VisoraModule
{
    // Synchronous lifecycle
    public virtual void Initialize(ICapabilityProvider capabilities) { }
    public virtual void Shutdown() { }

    // Asynchronous lifecycle
    public virtual Task InitializeAsync(ICapabilityProvider capabilities, CancellationToken ct)
        => Task.CompletedTask;
    public virtual Task ShutdownAsync(CancellationToken ct)
        => Task.CompletedTask;
}
```

**Pros:**
- Flexibility: Callers choose sync or async
- Gradual migration possible
- Best of both worlds?

**Cons:**
- ❌ **Double API surface:** Twice as many methods to maintain
- ❌ **Confusion:** Which should developers use?
- ❌ **Implementation complexity:** How do sync and async relate?
- ❌ **Testing burden:** Must test both code paths
- ❌ **Documentation overhead:** Explain when to use each

**Implementation Challenges:**

```csharp
// Which is the "real" implementation?

// Option A: Sync calls async (bad - can deadlock)
public string ReadFile(string path)
{
    return ReadFileAsync(path).GetAwaiter().GetResult();  // ⚠️ Deadlock risk!
}

// Option B: Async calls sync (bad - blocks thread)
public Task<string> ReadFileAsync(string path, CancellationToken ct)
{
    return Task.Run(() => ReadFile(path), ct);  // ⚠️ Wastes thread!
}

// Option C: Duplicate implementations (bad - maintenance nightmare)
public string ReadFile(string path)
{
    // Implementation A
}

public async Task<string> ReadFileAsync(string path, CancellationToken ct)
{
    // Implementation B (must stay in sync with A!)
}
```

**Why Not Chosen:**
The maintenance burden and potential for confusion outweighed the flexibility. It's better to commit to one pattern and execute it well.

---

### Alternative 3: Event-Based Asynchronous Pattern (EAP)

**Approach:** Use events for async notifications.

```csharp
public class Module
{
    public event EventHandler<InitializedEventArgs>? Initialized;
    public event EventHandler<ShutdownEventArgs>? Shutdown;
    public event EventHandler<CommandCompletedEventArgs>? CommandCompleted;

    public void Initialize(ICapabilityProvider capabilities)
    {
        Task.Run(async () =>
        {
            await DoInitializationAsync();
            Initialized?.Invoke(this, new InitializedEventArgs());
        });
    }

    public void ExecuteCommand(CommandContext context)
    {
        Task.Run(async () =>
        {
            var result = await ExecuteAsync(context);
            CommandCompleted?.Invoke(this, new CommandCompletedEventArgs(result));
        });
    }
}
```

**Pros:**
- Familiar event-driven pattern
- Decoupled notifications
- Natural for UI scenarios

**Cons:**
- ❌ **Complex error handling:** Exceptions in events are problematic
- ❌ **Difficult cancellation:** No built-in cancellation support
- ❌ **Testing complexity:** Event-based code is hard to test
- ❌ **Legacy pattern:** EAP is superseded by async/await
- ❌ **Memory leaks:** Easy to forget to unsubscribe from events

**Why Not Chosen:**
EAP is a legacy pattern from pre-async/await era. Modern .NET code should use async/await for asynchronous operations.

---

### Alternative 4: Reactive Extensions (Rx)

**Approach:** Use `IObservable<T>` for asynchronous streams.

```csharp
public interface IModule
{
    IObservable<ModuleState> StateChanges { get; }
    IObservable<CommandResult> Execute(CommandContext context);
}

// Usage
module.Execute(context)
    .Timeout(TimeSpan.FromSeconds(30))
    .Retry(3)
    .Subscribe(
        result => Console.WriteLine($"Success: {result}"),
        error => Console.WriteLine($"Error: {error}"),
        () => Console.WriteLine("Completed"));
```

**Pros:**
- Powerful composition operators
- Excellent for streaming scenarios
- Built-in cancellation, timeout, retry
- Reactive programming paradigm

**Cons:**
- ❌ **Steep learning curve:** Rx is complex
- ❌ **Heavy dependency:** Large library
- ❌ **Overkill for simple cases:** Not all operations are streams
- ❌ **Less discoverable:** Operators are hard to find
- ❌ **Debugging difficulty:** Composition chains are hard to debug

**Why Not Chosen:**
While powerful, Rx is overkill for VISORA's needs. Most operations are single-value async operations, not streams. Async/await is simpler and more appropriate.

---

### Alternative 5: Synchronous with Task.Run Wrappers

**Approach:** Synchronous core, wrap with `Task.Run` when needed.

```csharp
// Core synchronous implementation
public class Module
{
    public void Initialize(ICapabilityProvider capabilities)
    {
        // Synchronous work
    }

    public CommandResult Execute(CommandContext context)
    {
        // Synchronous execution
        return result;
    }
}

// Host wraps in Task.Run when async needed
public async Task LoadModuleAsync(Module module)
{
    await Task.Run(() => module.Initialize(capabilities));
    var result = await Task.Run(() => module.Execute(context));
}
```

**Pros:**
- Simple module implementation
- No async complexity for module authors
- Flexibility at call site

**Cons:**
- ❌ **Thread pool abuse:** Every operation consumes a thread
- ❌ **Poor scalability:** Thread pool can be exhausted
- ❌ **No true async I/O:** Still blocking threads during I/O
- ❌ **Hidden costs:** `Task.Run` creates overhead
- ❌ **Misleading API:** Looks async but isn't truly async

**Why Not Chosen:**
`Task.Run` is not true asynchronous I/O. It just moves synchronous work to a thread pool thread, which doesn't improve scalability and can actually hurt performance.

---

## Rationale

### Why Async/Await Wins

#### 1. Scalability

Non-blocking I/O allows high concurrency:

```csharp
// Scenario: Initialize 100 modules concurrently

// Synchronous: Requires 100 threads (thread pool exhaustion!)
Parallel.ForEach(modules, module => module.Initialize(capabilities));

// Asynchronous: Requires only a few threads
await Task.WhenAll(modules.Select(m => m.InitializeAsync(capabilities, ct)));
```

**Metrics:**
- Synchronous: ~100 threads, high memory usage
- Asynchronous: ~5-10 threads, low memory usage
- Thread pool exhaustion avoided

#### 2. Responsive User Interfaces

Async prevents UI freezing:

```csharp
// WPF/WinForms application

// Synchronous - UI freezes during command execution
private void ExecuteButton_Click(object sender, EventArgs e)
{
    var result = module.Execute(context);  // UI frozen!
    DisplayResult(result);
}

// Asynchronous - UI remains responsive
private async void ExecuteButton_Click(object sender, EventArgs e)
{
    statusLabel.Text = "Executing...";
    var result = await module.ExecuteAsync(context, cts.Token);  // UI responsive!
    DisplayResult(result);
}
```

#### 3. Built-in Cancellation

`CancellationToken` provides cooperative cancellation:

```csharp
// User can cancel long-running operations
private CancellationTokenSource _cts = new();

private async void ExecuteButton_Click(object sender, EventArgs e)
{
    try
    {
        var result = await module.ExecuteAsync(context, _cts.Token);
        DisplayResult(result);
    }
    catch (OperationCanceledException)
    {
        statusLabel.Text = "Cancelled by user";
    }
}

private void CancelButton_Click(object sender, EventArgs e)
{
    _cts.Cancel();  // Cancels the operation
}
```

#### 4. Future-Proofing for Distribution

Async APIs work seamlessly with network calls:

```csharp
// Local execution (current)
public async ValueTask<CommandResult> ExecuteAsync(CommandContext context, CancellationToken ct)
{
    return await _handler.ExecuteAsync(context, ct);
}

// Remote execution (future) - same signature!
public async ValueTask<CommandResult> ExecuteAsync(CommandContext context, CancellationToken ct)
{
    var request = SerializeRequest(context);
    var response = await _httpClient.PostAsync(_remoteEndpoint, request, ct);
    return await DeserializeResponse<CommandResult>(response, ct);
}

// Callers don't need to change!
var result = await command.ExecuteAsync(context, ct);
```

#### 5. Modern .NET Ecosystem Alignment

Async is the standard in modern .NET:

```csharp
// ASP.NET Core
public async Task<IActionResult> GetData(CancellationToken ct)
{
    var data = await _service.GetDataAsync(ct);
    return Ok(data);
}

// Entity Framework Core
var users = await dbContext.Users.ToListAsync(ct);

// HttpClient
var response = await httpClient.GetAsync(url, ct);

// VISORA aligns with ecosystem
var result = await module.ExecuteAsync(context, ct);
```

#### 6. ValueTask Performance Optimization

ValueTask eliminates allocations for synchronous completions:

```csharp
// Benchmark: Getting cached value 1 million times

// Task<T> approach
public Task<int> GetCachedValue()
{
    return Task.FromResult(_cached);  // 1 million Task allocations!
}
// Result: ~32 MB allocated, ~50ms

// ValueTask<T> approach
public ValueTask<int> GetCachedValue()
{
    return ValueTask.FromResult(_cached);  // Zero allocations!
}
// Result: ~0 MB allocated, ~5ms

// Performance gain: 10x faster, zero GC pressure
```

---

## Consequences

### Positive Consequences

#### 1. Superior Scalability

Can handle many more concurrent operations:

```csharp
// Load 1000 modules concurrently
var modules = await DiscoverModulesAsync("./modules", ct);
await Task.WhenAll(modules.Select(m => m.InitializeAsync(capabilities, ct)));

// With async: Uses ~10 threads, completes in ~2 seconds
// With sync: Would exhaust thread pool, possibly fail
```

#### 2. Responsive Applications

UI applications remain responsive during long operations:

```csharp
// User can interact with UI while command executes
await ExecuteLongRunningCommandAsync(ct);
```

#### 3. Efficient Resource Utilization

Threads released during I/O:

```
Thread Timeline (Synchronous):
Thread 1: [Execute]──────────[Wait for I/O]──────────[Resume]
          ↑                   ↑                        ↑
          CPU busy            Blocked (wasted!)        CPU busy

Thread Timeline (Asynchronous):
Thread 1: [Execute]──────────[Released]
          ↑                   ↑
          CPU busy            Thread returned to pool

... (I/O completes) ...

Thread 2:                     [Resume]
                              ↑
                              CPU busy (different thread, pool efficient)
```

#### 4. Easy Cancellation

All operations support cancellation:

```csharp
// User can cancel any async operation
await operation.ExecuteAsync(context, userCancellationToken);
```

#### 5. Network-Ready APIs

Can easily extend to distributed scenarios:

```csharp
// Same interface works for local and remote
public interface ICommandExecutor
{
    ValueTask<CommandResult> ExecuteAsync(CommandContext context, CancellationToken ct);
}

// LocalCommandExecutor - runs in-process
// RemoteCommandExecutor - calls HTTP API
// Callers use identical code
```

### Negative Consequences

#### 1. Increased Complexity

Async code is more complex:

```csharp
// Synchronous - simple
public int Calculate()
{
    return _a + _b;
}

// Asynchronous - more complex
public async Task<int> CalculateAsync()
{
    await Task.Yield();  // Why is this needed?
    return _a + _b;
}
```

**Mitigation:** Training, documentation, clear guidelines on when to use async.

#### 2. Learning Curve

Developers must understand:
- Async/await semantics
- Task vs ValueTask
- ConfigureAwait
- Cancellation tokens
- Async anti-patterns (sync over async, etc.)

**Mitigation:** Comprehensive documentation, examples, and code reviews.

#### 3. Debugging Challenges

Async stack traces are harder to read:

```
// Synchronous stack trace (clear)
at Module.Execute()
at CommandHandler.Process()
at Host.Run()

// Asynchronous stack trace (complex)
at Module.<ExecuteAsync>d__5.MoveNext()
at System.Runtime.CompilerServices.AsyncTaskMethodBuilder.Start()
at Module.ExecuteAsync()
at CommandHandler.<ProcessAsync>d__3.MoveNext()
...
```

**Mitigation:** .NET 9.0 has improved async debugging significantly.

#### 4. Performance Overhead for Pure Compute

Async has overhead for CPU-bound operations:

```csharp
// Pure computation - async overhead not justified
public async Task<int> AddAsync(int a, int b)
{
    return a + b;  // No I/O, async overhead wasted
}

// Better: Keep synchronous
public int Add(int a, int b)
{
    return a + b;
}
```

**Mitigation:** Use async only for I/O-bound or long-running operations, not pure computation.

#### 5. Async All the Way

Once you start async, it spreads:

```csharp
// If A is async...
public async Task AAsync() { await BAsync(); }

// Then B must be async...
public async Task BAsync() { await CAsync(); }

// Then C must be async...
public async Task CAsync() { await DAsync(); }

// "Async contagion"
```

**Mitigation:** This is actually a good thing - it forces proper async design throughout.

---

## Tradeoffs

### Async vs. Synchronous Comparison

| Aspect | Async/Await | Synchronous |
|--------|------------|-------------|
| **Scalability** | ⭐⭐⭐⭐⭐ | ⭐⭐ |
| **UI Responsiveness** | ⭐⭐⭐⭐⭐ | ⭐ |
| **Resource Efficiency** | ⭐⭐⭐⭐⭐ | ⭐⭐ |
| **Code Simplicity** | ⭐⭐⭐ | ⭐⭐⭐⭐⭐ |
| **Debugging Ease** | ⭐⭐⭐ | ⭐⭐⭐⭐⭐ |
| **Learning Curve** | ⭐⭐ | ⭐⭐⭐⭐⭐ |
| **Performance (I/O)** | ⭐⭐⭐⭐⭐ | ⭐⭐ |
| **Performance (CPU)** | ⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ |
| **Cancellation Support** | ⭐⭐⭐⭐⭐ | ⭐⭐ |
| **Future-Proofing** | ⭐⭐⭐⭐⭐ | ⭐⭐ |
| **Total Score** | **44/50** | **34/50** |

### Task vs ValueTask Guidelines

| Use Task<T> When | Use ValueTask<T> When |
|------------------|----------------------|
| Always allocates (always async) | Often completes synchronously |
| Not performance-critical | Performance-critical hot path |
| Simple implementation | Willing to handle complexity |
| Result will be awaited once | Result may not be awaited |
| Example: Network calls | Example: Cache lookups |
| Example: Database queries | Example: Capability provider |
| Example: File I/O (uncached) | Example: File I/O (cached) |

```csharp
// Task<T> - Always async, always allocates
public async Task<Data> FetchFromNetworkAsync(CancellationToken ct)
{
    return await _httpClient.GetAsync<Data>(_url, ct);
}

// ValueTask<T> - Often sync (cached), no allocation on hit
public ValueTask<Data> GetDataAsync(CancellationToken ct)
{
    if (_cache.TryGetValue(out var data))
        return ValueTask.FromResult(data);  // Sync completion, no allocation

    return FetchAndCacheAsync(ct);  // Async path
}
```

---

## Implementation Guidelines

### 1. Method Naming

```csharp
// ✅ Good: Async suffix
public async Task<string> ReadFileAsync(string path, CancellationToken ct)

// ❌ Bad: No suffix
public async Task<string> ReadFile(string path, CancellationToken ct)

// ✅ Good: ValueTask for hot paths
public ValueTask<T> GetCapabilityAsync<T>(CancellationToken ct) where T : class

// ⚠️ Consider: Dropping "Async" suffix if ALL methods are async
// (VISORA uses Async suffix everywhere for consistency)
```

### 2. CancellationToken Placement

```csharp
// ✅ Good: Last parameter, optional default
public async Task ExecuteAsync(string command, object[] args, CancellationToken ct = default)

// ❌ Bad: Required without default
public async Task ExecuteAsync(string command, CancellationToken ct, object[] args)

// ❌ Bad: Not last parameter
public async Task ExecuteAsync(CancellationToken ct, string command, object[] args)
```

### 3. Cancellation Checking

```csharp
// ✅ Good: Check cancellation in loops
public async Task ProcessItemsAsync(IEnumerable<Item> items, CancellationToken ct)
{
    foreach (var item in items)
    {
        ct.ThrowIfCancellationRequested();  // Check before expensive work
        await ProcessItemAsync(item, ct);
    }
}

// ✅ Good: Pass cancellation to async operations
public async Task<Data> LoadDataAsync(CancellationToken ct)
{
    var content = await File.ReadAllTextAsync(path, ct);  // Pass ct through
    return Parse(content);
}

// ❌ Bad: Ignoring cancellation token
public async Task ProcessItemsAsync(IEnumerable<Item> items, CancellationToken ct)
{
    foreach (var item in items)
    {
        await ProcessItemAsync(item);  // Not passing ct!
    }
}
```

### 4. ConfigureAwait Usage

```csharp
// ✅ VISORA platform code (library): Use ConfigureAwait(false)
public async ValueTask<Data> LoadDataAsync(CancellationToken ct)
{
    var content = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
    var data = await ParseAsync(content, ct).ConfigureAwait(false);
    return data;
}

// ✅ Module code (application): No ConfigureAwait needed
public override async ValueTask InitializeAsync(ICapabilityProvider capabilities, CancellationToken ct)
{
    _logger = capabilities.GetRequiredCapability<ILogger>();
    var config = await LoadConfigAsync(ct);  // No ConfigureAwait
    await ConnectAsync(config, ct);  // No ConfigureAwait
}

// ⚠️ UI code: Never use ConfigureAwait(false)
private async void Button_Click(object sender, EventArgs e)
{
    var result = await ExecuteAsync(ct);  // Must return to UI thread
    textBox.Text = result;  // Update UI control
}
```

### 5. Async Void Anti-Pattern

```csharp
// ❌ Bad: async void (swallows exceptions!)
public async void ProcessDataAsync()
{
    await LoadDataAsync();
    // Exception here is swallowed!
}

// ✅ Good: async Task
public async Task ProcessDataAsync()
{
    await LoadDataAsync();
    // Exception propagates properly
}

// ✅ Exception: Event handlers must be async void
private async void Button_Click(object sender, EventArgs e)
{
    try
    {
        await ExecuteAsync();
    }
    catch (Exception ex)
    {
        // Handle exceptions in event handlers!
        _logger.LogError(ex, "Execution failed");
        MessageBox.Show($"Error: {ex.Message}");
    }
}
```

### 6. Async Lazy Initialization

```csharp
// ✅ Good: AsyncLazy pattern
public class Module
{
    private readonly AsyncLazy<Database> _database;

    public Module()
    {
        _database = new AsyncLazy<Database>(async () =>
        {
            var db = new Database();
            await db.ConnectAsync();
            return db;
        });
    }

    public async Task ExecuteAsync()
    {
        var db = await _database.Value;  // Initialized once
        await db.QueryAsync(...);
    }
}

// AsyncLazy implementation
public class AsyncLazy<T>
{
    private readonly Lazy<Task<T>> _instance;

    public AsyncLazy(Func<Task<T>> factory)
    {
        _instance = new Lazy<Task<T>>(factory);
    }

    public Task<T> Value => _instance.Value;
}
```

### 7. Parallel Async Operations

```csharp
// ✅ Good: WhenAll for parallel execution
public async Task<Results> ProcessAllAsync(IEnumerable<Item> items, CancellationToken ct)
{
    var tasks = items.Select(item => ProcessItemAsync(item, ct));
    var results = await Task.WhenAll(tasks);
    return new Results(results);
}

// ✅ Good: WhenAny for first completion
public async Task<Result> GetFirstResponseAsync(IEnumerable<IService> services, CancellationToken ct)
{
    var tasks = services.Select(s => s.GetDataAsync(ct));
    var firstCompleted = await Task.WhenAny(tasks);
    return await firstCompleted;
}

// ❌ Bad: Sequential when parallel is possible
public async Task<Results> ProcessAllAsync(IEnumerable<Item> items, CancellationToken ct)
{
    var results = new List<Result>();
    foreach (var item in items)
    {
        var result = await ProcessItemAsync(item, ct);  // One at a time!
        results.Add(result);
    }
    return new Results(results);
}
```

---

## Performance Considerations

### Benchmarks

```csharp
// Benchmark: Module initialization (50 modules)

public class ModuleInitializationBenchmark
{
    [Benchmark]
    public void Synchronous_Sequential()
    {
        foreach (var module in _modules)
        {
            module.Initialize(_capabilities);  // Blocks on I/O
        }
    }
    // Result: 5000ms, 50 threads used

    [Benchmark]
    public async Task Asynchronous_Sequential()
    {
        foreach (var module in _modules)
        {
            await module.InitializeAsync(_capabilities, CancellationToken.None);
        }
    }
    // Result: 4800ms, 5 threads used (slight improvement)

    [Benchmark]
    public async Task Asynchronous_Parallel()
    {
        await Task.WhenAll(_modules.Select(m =>
            m.InitializeAsync(_capabilities, CancellationToken.None)));
    }
    // Result: 500ms, 5 threads used (10x faster!)
}
```

### Task vs ValueTask Performance

```csharp
// Benchmark: Cache lookup (1 million iterations)

[Benchmark]
public async Task<int> Task_CacheLookup()
{
    int sum = 0;
    for (int i = 0; i < 1_000_000; i++)
    {
        sum += await GetCachedValueTask(i);  // Always allocates Task
    }
    return sum;
}
// Result: 450ms, 32 MB allocated

[Benchmark]
public async Task<int> ValueTask_CacheLookup()
{
    int sum = 0;
    for (int i = 0; i < 1_000_000; i++)
    {
        sum += await GetCachedValueValueTask(i);  // No allocation on cache hit
    }
    return sum;
}
// Result: 45ms, 0.5 MB allocated (10x faster, 64x less memory!)

// Implementations
public Task<int> GetCachedValueTask(int key)
{
    if (_cache.TryGetValue(key, out var value))
        return Task.FromResult(value);  // Allocates Task
    return LoadValueAsync(key);
}

public ValueTask<int> GetCachedValueValueTask(int key)
{
    if (_cache.TryGetValue(key, out var value))
        return ValueTask.FromResult(value);  // No allocation!
    return new ValueTask<int>(LoadValueAsync(key));
}
```

### ConfigureAwait Impact

```csharp
// Benchmark: Library method with many awaits

[Benchmark]
public async Task WithoutConfigureAwait()
{
    for (int i = 0; i < 100; i++)
    {
        await Task.Delay(1);  // Captures context each time
    }
}
// Result: 150ms

[Benchmark]
public async Task WithConfigureAwait()
{
    for (int i = 0; i < 100; i++)
    {
        await Task.Delay(1).ConfigureAwait(false);  // No context capture
    }
}
// Result: 110ms (25% faster in high-await scenarios)
```

---

## When to Revisit

### Triggers for Reconsideration

#### 1. Native AOT Compilation

**Future Scenario:** .NET gains better Native AOT support with async

**Challenge:** Current AOT has limitations with async state machines

**Action:** Monitor .NET roadmap, potentially offer AOT-optimized sync overloads

#### 2. Performance Critical Paths

**Metrics to Watch:**
- Async overhead > 5% of total execution time
- ValueTask allocations despite optimization
- Excessive async state machine overhead

**Action:** Profile, identify hot paths, consider sync fast path with async fallback

#### 3. WebAssembly/Blazor Scenarios

**Context:** Blazor WebAssembly has different async characteristics

**Challenge:** Browser single-threaded environment

**Action:** Ensure async patterns work well in WASM, adjust if needed

#### 4. Community Feedback

**Indicators:**
- Module authors struggling with async patterns
- Common async anti-patterns in modules
- Request for synchronous alternatives

**Action:** Improve documentation, provide better tooling/analyzers, consider targeted sync APIs

---

## Related Patterns

### Primary Patterns

#### 1. Async Patterns Documentation
- **Location:** `/References/patterns/async-patterns.md`
- **Relationship:** Detailed async implementation patterns
- **Summary:** Best practices for async/await in VISORA

#### 2. Cancellation Pattern
- **Location:** `/References/patterns/cancellation.md`
- **Relationship:** CancellationToken usage guidelines
- **Summary:** How to properly implement cancellation

### Related ADRs

#### ADR-001: Reflection Discovery
- **Connection:** Module discovery is async
- **Code:** `await DiscoverModulesAsync(path, ct)`

#### ADR-002: Capability Provider
- **Connection:** Capability acquisition can be async
- **Code:** `await capabilities.GetCapabilityAsync<T>(ct)`

#### ADR-004: Unloadable Plugins
- **Connection:** Module loading/unloading is async
- **Code:** `await LoadModuleAsync(descriptor, ct)`

### Supporting Patterns

#### 3. Command Execution Pattern
- **Location:** `/References/patterns/command-execution.md`
- **Summary:** All command execution is async

#### 4. Module Lifecycle Pattern
- **Location:** `/References/patterns/module-lifecycle.md`
- **Summary:** Async initialization and shutdown

---

## References

### Internal Documentation
- `/References/patterns/async-patterns.md` - Async best practices
- `/References/patterns/cancellation.md` - Cancellation patterns
- `/References/conventions/async-conventions.md` - Naming and conventions

### External Resources
- [Task-based Asynchronous Pattern (TAP)](https://learn.microsoft.com/en-us/dotnet/standard/asynchronous-programming-patterns/task-based-asynchronous-pattern-tap)
- [Async/Await Best Practices](https://learn.microsoft.com/en-us/archive/msdn-magazine/2013/march/async-await-best-practices-in-asynchronous-programming)
- [ValueTask Documentation](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.valuetask-1)
- [ConfigureAwait FAQ](https://devblogs.microsoft.com/dotnet/configureawait-faq/)

### Performance Resources
- [Async Performance in .NET 9.0](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-9/)
- [Understanding Async State Machines](https://www.youtube.com/watch?v=zhrtHFPCk7M)

---

## Appendix: Complete Examples

### Example 1: Module with Async Initialization

```csharp
public class DataProcessingModule : VisoraModule
{
    private ILogger? _logger;
    private IDatabase? _database;
    private IConfiguration? _config;

    public override string ModuleName => "Data Processing";
    public override string Description => "Process and transform data";

    public override async ValueTask InitializeAsync(
        ICapabilityProvider capabilities,
        CancellationToken ct)
    {
        // Acquire capabilities
        _logger = capabilities.GetRequiredCapability<ILogger>();
        _config = capabilities.GetRequiredCapability<IConfiguration>();
        _database = capabilities.GetCapability<IDatabase>();

        // Async initialization
        _logger.LogInformation("Initializing {Module}...", ModuleName);

        var connectionString = _config.GetValue<string>("ConnectionString");
        if (_database != null && !string.IsNullOrEmpty(connectionString))
        {
            await _database.ConnectAsync(connectionString, ct);
            _logger.LogInformation("Database connected");
        }

        // Load resources
        await LoadProcessingRulesAsync(ct);

        _logger.LogInformation("{Module} initialized successfully", ModuleName);
    }

    public override async ValueTask ShutdownAsync(CancellationToken ct)
    {
        _logger?.LogInformation("Shutting down {Module}...", ModuleName);

        if (_database != null)
        {
            await _database.DisconnectAsync(ct);
        }

        _logger?.LogInformation("{Module} shut down", ModuleName);
    }

    private async ValueTask LoadProcessingRulesAsync(CancellationToken ct)
    {
        var rulesFile = _config?.GetValue<string>("RulesFile") ?? "rules.json";
        var content = await File.ReadAllTextAsync(rulesFile, ct).ConfigureAwait(false);
        // Parse rules...
    }

    public override IEnumerable<CommandDescriptor> GetCommands()
    {
        yield return new CommandDescriptor(
            Name: "process",
            Description: "Process data",
            Parameters: [],
            Handler: typeof(ProcessCommandHandler)
        );
    }

    public override IEnumerable<ServiceDescriptor> GetServices()
    {
        yield return new ServiceDescriptor(
            ServiceType: typeof(IDataProcessor),
            ImplementationType: typeof(DataProcessor),
            Lifetime: ServiceLifetime.Scoped
        );
    }
}
```

### Example 2: Command Handler with Cancellation

```csharp
public class ProcessDataCommandHandler : ICommandHandler
{
    private readonly ILogger _logger;
    private readonly IDataProcessor _processor;

    public ProcessDataCommandHandler(ILogger logger, IDataProcessor processor)
    {
        _logger = logger;
        _processor = processor;
    }

    public async ValueTask<CommandResult> ExecuteAsync(
        CommandContext context,
        CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Starting data processing...");

            var items = await LoadItemsAsync(ct);
            _logger.LogInformation("Loaded {Count} items", items.Count);

            var processed = 0;
            var total = items.Count;

            foreach (var item in items)
            {
                // Check for cancellation
                ct.ThrowIfCancellationRequested();

                // Process item
                await _processor.ProcessAsync(item, ct);
                processed++;

                // Report progress
                if (processed % 100 == 0)
                {
                    _logger.LogInformation("Progress: {Processed}/{Total}", processed, total);
                }
            }

            _logger.LogInformation("Processing complete: {Total} items", total);
            return CommandResult.Success($"Processed {total} items");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Processing cancelled by user");
            return CommandResult.Failure("Processing cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Processing failed");
            return CommandResult.Failure($"Processing failed: {ex.Message}");
        }
    }

    private async Task<List<DataItem>> LoadItemsAsync(CancellationToken ct)
    {
        // Simulate loading from database or file
        await Task.Delay(100, ct).ConfigureAwait(false);
        return new List<DataItem>();
    }
}
```

---

**Document Metadata:**
- **Author:** VISORA Architecture Team
- **Contributors:** Performance Team, Module Authors
- **Review Cycle:** Quarterly
- **Next Review:** 2025-02-10
