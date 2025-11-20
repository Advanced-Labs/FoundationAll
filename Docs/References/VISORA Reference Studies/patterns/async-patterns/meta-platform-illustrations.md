# Async Patterns - Meta-Platform Illustrations

**Last Updated:** 2025-11-10
**VISORA Version:** .NET 9.0
**Document Type:** Illustrative Examples / Thought Experiments

---

## ⚠️ IMPORTANT DISCLAIMER

**This document contains ILLUSTRATIVE EXAMPLES and THOUGHT EXPERIMENTS only.**

These are NOT:
- ❌ Prescriptive designs
- ❌ Proven implementations
- ❌ Production-ready code
- ❌ Official recommendations

These ARE:
- ✅ Conceptual explorations
- ✅ Inspiration for possibilities
- ✅ Starting points for investigation
- ✅ Creative adaptations of VISORA patterns

**Use these examples to spark imagination, not as blueprints.**

---

## Table of Contents

1. [Conceptual Adaptation](#conceptual-adaptation)
2. [Bridging .NET Task ↔ Python asyncio](#bridging-net-task--python-asyncio)
3. [Bridging .NET Task ↔ Node.js Promises](#bridging-net-task--nodejs-promises)
4. [Cancellation Across Runtimes](#cancellation-across-runtimes)
5. [Event Loop Considerations](#event-loop-considerations)
6. [Async Semantics Challenges](#async-semantics-challenges)
7. [Illustrative Integration Examples](#illustrative-integration-examples)
8. [Challenges & Considerations](#challenges--considerations)

---

## Conceptual Adaptation

### From .NET Async/Await to Polyglot Async Patterns

**VISORA's .NET Pattern:**
```
.NET Async Model
  ├─ Task/ValueTask (represents async operation)
  ├─ async/await (language keywords)
  ├─ CancellationToken (cooperative cancellation)
  ├─ ConfigureAwait(false) (library code optimization)
  └─ IAsyncDisposable (async cleanup)
```

**Conceptual Meta-Platform Adaptation:**
```
Meta-Platform Async Bridge
  │
  ├─ .NET Layer
  │  ├─ Task<T> / ValueTask<T>
  │  ├─ CancellationToken
  │  └─ async/await
  │
  ├─ Python Layer
  │  ├─ asyncio.Future / asyncio.Task
  │  ├─ asyncio.CancelledError
  │  └─ async/await (Python 3.5+)
  │
  ├─ Node.js Layer
  │  ├─ Promise<T>
  │  ├─ AbortController/AbortSignal
  │  └─ async/await (ES2017+)
  │
  └─ Bridge Coordinator
     ├─ Maps .NET Task ↔ Python Future ↔ Node.js Promise
     ├─ Propagates cancellation across boundaries
     └─ Handles event loop differences
```

---

## Bridging .NET Task ↔ Python asyncio

### ⚠️ ILLUSTRATIVE EXAMPLE: .NET to Python Async Bridge

**Conceptual Approach:** Bridge .NET Task to Python asyncio.Future using Python.NET.

**Illustrative C# Code:**

```csharp
// ⚠️ ILLUSTRATIVE EXAMPLE - NOT PRODUCTION CODE

using Python.Runtime;
using System.Threading.Tasks;

public class DotNetToPythonAsyncBridge
{
    /// <summary>
    /// Execute Python async function and await result in .NET
    /// </summary>
    public async Task<T> AwaitPythonAsync<T>(dynamic pythonAsyncFunc, CancellationToken ct)
    {
        using (Py.GIL())
        {
            // Get Python's asyncio module
            dynamic asyncio = Py.Import("asyncio");

            // Create a Python task from the coroutine
            dynamic pythonTask = asyncio.create_task(pythonAsyncFunc);

            // Bridge cancellation: .NET CancellationToken → Python task cancellation
            var cancellationRegistration = ct.Register(() =>
            {
                using (Py.GIL())
                {
                    pythonTask.cancel();
                }
            });

            try
            {
                // Poll Python task until completion
                while (true)
                {
                    ct.ThrowIfCancellationRequested();

                    if (pythonTask.done())
                    {
                        // Check for Python exception
                        if (pythonTask.cancelled())
                        {
                            throw new OperationCanceledException("Python task was cancelled");
                        }

                        dynamic exception = pythonTask.exception();
                        if (exception != null)
                        {
                            throw new InvalidOperationException(
                                $"Python task failed: {exception}");
                        }

                        // Get result
                        dynamic result = pythonTask.result();
                        return (T)result;
                    }

                    // Release GIL and yield to Python event loop
                    await Task.Delay(10, ct); // Small delay to prevent tight loop
                }
            }
            finally
            {
                cancellationRegistration.Dispose();
            }
        }
    }

    /// <summary>
    /// Execute .NET async method from Python
    /// </summary>
    public dynamic CreatePythonAwaitable(Task task)
    {
        using (Py.GIL())
        {
            dynamic asyncio = Py.Import("asyncio");

            // Create Python Future
            dynamic loop = asyncio.get_event_loop();
            dynamic future = loop.create_future();

            // Complete Python future when .NET task completes
            _ = task.ContinueWith(t =>
            {
                using (Py.GIL())
                {
                    if (t.IsCanceled)
                    {
                        future.cancel();
                    }
                    else if (t.IsFaulted)
                    {
                        future.set_exception(Py.kw("exception",
                            new PyString(t.Exception.Message)));
                    }
                    else
                    {
                        // For Task<T>, get result
                        if (t.GetType().IsGenericType)
                        {
                            var resultProp = t.GetType().GetProperty("Result");
                            var result = resultProp?.GetValue(t);
                            future.set_result(result.ToPython());
                        }
                        else
                        {
                            future.set_result(Py.None);
                        }
                    }
                }
            });

            return future;
        }
    }
}
```

**Illustrative Python Code:**

```python
# ⚠️ ILLUSTRATIVE EXAMPLE

import asyncio
from typing import Any

class PythonAsyncModule:
    """
    Python module with async methods callable from .NET
    """

    async def async_operation(self, param: str) -> dict:
        """
        Async method that can be awaited from .NET
        """
        # Simulate async work
        await asyncio.sleep(1)

        return {
            "status": "completed",
            "input": param,
            "timestamp": "2025-11-10T12:00:00Z"
        }

    async def cancellable_operation(self, duration: float) -> str:
        """
        Operation that respects cancellation
        """
        try:
            await asyncio.sleep(duration)
            return "Completed successfully"
        except asyncio.CancelledError:
            # Cleanup on cancellation
            return "Cancelled"

    async def call_dotnet_async(self, dotnet_bridge, dotnet_task):
        """
        Python calling .NET async method
        """
        # Get awaitable from .NET Task
        dotnet_future = dotnet_bridge.CreatePythonAwaitable(dotnet_task)

        # Await the .NET operation from Python
        result = await dotnet_future

        return result
```

---

## Bridging .NET Task ↔ Node.js Promises

### ⚠️ ILLUSTRATIVE EXAMPLE: .NET to Node.js Promise Bridge

**Conceptual Approach:** Use subprocess with JSON-RPC or gRPC to bridge async operations.

**Illustrative C# Code (JSON-RPC Approach):**

```csharp
// ⚠️ ILLUSTRATIVE EXAMPLE

public class DotNetToNodeAsyncBridge
{
    private readonly JsonRpcClient _rpcClient;

    public DotNetToNodeAsyncBridge(Process nodeProcess)
    {
        _rpcClient = new JsonRpcClient(
            nodeProcess.StandardInput,
            nodeProcess.StandardOutput);
    }

    /// <summary>
    /// Call Node.js async function and await result in .NET
    /// </summary>
    public async Task<T> InvokeNodeAsyncMethod<T>(
        string method,
        object parameters,
        CancellationToken ct)
    {
        try
        {
            // Send JSON-RPC request
            var requestId = Guid.NewGuid().ToString();
            var request = new
            {
                jsonrpc = "2.0",
                id = requestId,
                method = method,
                @params = parameters
            };

            var requestJson = JsonSerializer.Serialize(request);
            await _rpcClient.SendAsync(requestJson, ct);

            // Wait for response
            var responseJson = await _rpcClient.ReceiveAsync(ct);
            var response = JsonSerializer.Deserialize<JsonRpcResponse>(responseJson);

            if (response.Error != null)
            {
                throw new InvalidOperationException(
                    $"Node.js error: {response.Error.Message}");
            }

            return JsonSerializer.Deserialize<T>(response.Result.ToString());
        }
        catch (OperationCanceledException)
        {
            // Send cancellation message to Node.js
            await SendCancellationAsync(method, ct);
            throw;
        }
    }

    private async Task SendCancellationAsync(string operationId, CancellationToken ct)
    {
        var cancelRequest = new
        {
            jsonrpc = "2.0",
            method = "cancel",
            @params = new { operationId }
        };

        var json = JsonSerializer.Serialize(cancelRequest);
        await _rpcClient.SendAsync(json, ct);
    }
}

public class JsonRpcResponse
{
    public string Id { get; set; }
    public object Result { get; set; }
    public JsonRpcError Error { get; set; }
}

public class JsonRpcError
{
    public int Code { get; set; }
    public string Message { get; set; }
}
```

**Illustrative Node.js Code:**

```javascript
// ⚠️ ILLUSTRATIVE EXAMPLE - node_async_bridge.js

const readline = require('readline');

class NodeAsyncBridge {
    constructor() {
        this.operations = new Map();
        this.setupRpcListener();
    }

    setupRpcListener() {
        const rl = readline.createInterface({
            input: process.stdin,
            output: process.stdout,
            terminal: false
        });

        rl.on('line', async (line) => {
            try {
                const request = JSON.parse(line);
                await this.handleRequest(request);
            } catch (error) {
                console.error('RPC Error:', error);
            }
        });
    }

    async handleRequest(request) {
        const { id, method, params } = request;

        if (method === 'cancel') {
            // Handle cancellation
            const controller = this.operations.get(params.operationId);
            if (controller) {
                controller.abort();
                this.operations.delete(params.operationId);
            }
            return;
        }

        // Create AbortController for cancellation
        const controller = new AbortController();
        this.operations.set(id, controller);

        try {
            // Execute async method
            const result = await this.executeMethod(
                method,
                params,
                controller.signal
            );

            // Send success response
            this.sendResponse({
                jsonrpc: '2.0',
                id: id,
                result: result
            });
        } catch (error) {
            // Send error response
            this.sendResponse({
                jsonrpc: '2.0',
                id: id,
                error: {
                    code: -32000,
                    message: error.message
                }
            });
        } finally {
            this.operations.delete(id);
        }
    }

    async executeMethod(method, params, signal) {
        // Route to actual async methods
        switch (method) {
            case 'fetchData':
                return await this.fetchData(params, signal);

            case 'processFile':
                return await this.processFile(params, signal);

            default:
                throw new Error(`Unknown method: ${method}`);
        }
    }

    async fetchData(params, signal) {
        // Simulate async operation with cancellation support
        return new Promise((resolve, reject) => {
            const timeout = setTimeout(() => {
                resolve({
                    data: params.query,
                    timestamp: new Date().toISOString()
                });
            }, 2000);

            // Handle cancellation
            signal.addEventListener('abort', () => {
                clearTimeout(timeout);
                reject(new Error('Operation cancelled'));
            });
        });
    }

    async processFile(params, signal) {
        const fs = require('fs').promises;

        // Check for cancellation
        if (signal.aborted) {
            throw new Error('Operation cancelled');
        }

        const content = await fs.readFile(params.path, 'utf8');

        return {
            path: params.path,
            size: content.length,
            lines: content.split('\n').length
        };
    }

    sendResponse(response) {
        console.log(JSON.stringify(response));
    }
}

// Start bridge
const bridge = new NodeAsyncBridge();
```

---

## Cancellation Across Runtimes

### ⚠️ ILLUSTRATIVE PATTERN: Unified Cancellation Token

**Challenge:** Propagate cancellation from .NET CancellationToken to Python/Node.js.

**Conceptual Architecture:**

```
.NET Host
  ├─ CancellationTokenSource.Cancel()
  │
  ├─ Cancellation Propagator
  │  │
  │  ├─ To Python Module
  │  │  └─ asyncio.Task.cancel()
  │  │
  │  └─ To Node.js Module
  │     └─ AbortController.abort()
  │
  └─ Cleanup Coordinator
     └─ Ensure all runtimes acknowledge cancellation
```

**Illustrative C# Implementation:**

```csharp
// ⚠️ ILLUSTRATIVE EXAMPLE

public class CrossRuntimeCancellationManager
{
    private readonly Dictionary<string, ICancellable> _operations = new();

    public async Task<T> ExecuteWithCancellationAsync<T>(
        string operationId,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken ct)
    {
        var cancellable = new CancellableOperation(operationId);
        _operations[operationId] = cancellable;

        // Register cancellation propagation
        using var registration = ct.Register(() =>
        {
            cancellable.Cancel();
            NotifyRuntimesCancellation(operationId);
        });

        try
        {
            return await operation(ct);
        }
        finally
        {
            _operations.Remove(operationId);
        }
    }

    private void NotifyRuntimesCancellation(string operationId)
    {
        // Notify Python runtime
        NotifyPythonCancellation(operationId);

        // Notify Node.js runtime
        NotifyNodeCancellation(operationId);
    }

    private void NotifyPythonCancellation(string operationId)
    {
        // ⚠️ ILLUSTRATIVE
        using (Py.GIL())
        {
            dynamic cancellationRegistry = Py.Import("visora_cancellation");
            cancellationRegistry.cancel_operation(operationId);
        }
    }

    private void NotifyNodeCancellation(string operationId)
    {
        // ⚠️ ILLUSTRATIVE - send via JSON-RPC
        var cancelMessage = new
        {
            type = "cancellation",
            operationId = operationId
        };

        // Send to Node.js process
    }
}

public interface ICancellable
{
    void Cancel();
}

public class CancellableOperation : ICancellable
{
    private readonly string _id;
    private bool _cancelled;

    public CancellableOperation(string id)
    {
        _id = id;
    }

    public void Cancel()
    {
        _cancelled = true;
    }
}
```

**Illustrative Python Cancellation Handler:**

```python
# ⚠️ ILLUSTRATIVE EXAMPLE - visora_cancellation.py

import asyncio
from typing import Dict

# Global registry of cancellable operations
_operations: Dict[str, asyncio.Task] = {}

def register_operation(operation_id: str, task: asyncio.Task):
    """Register an operation for potential cancellation"""
    _operations[operation_id] = task

def cancel_operation(operation_id: str):
    """Cancel operation by ID (called from .NET)"""
    task = _operations.get(operation_id)
    if task and not task.done():
        task.cancel()
        print(f"Cancelled Python operation: {operation_id}")

async def cancellable_async_operation(operation_id: str, work_func):
    """
    Wrapper for async operations that can be cancelled from .NET
    """
    task = asyncio.create_task(work_func())
    register_operation(operation_id, task)

    try:
        result = await task
        return result
    except asyncio.CancelledError:
        print(f"Operation {operation_id} was cancelled")
        raise
    finally:
        if operation_id in _operations:
            del _operations[operation_id]
```

**Illustrative Node.js Cancellation Handler:**

```javascript
// ⚠️ ILLUSTRATIVE EXAMPLE

class CancellationRegistry {
    constructor() {
        this.controllers = new Map();
    }

    /**
     * Register an operation with an AbortController
     */
    register(operationId, controller) {
        this.controllers.set(operationId, controller);
    }

    /**
     * Cancel operation by ID (called from .NET)
     */
    cancel(operationId) {
        const controller = this.controllers.get(operationId);
        if (controller) {
            controller.abort();
            this.controllers.delete(operationId);
            console.log(`Cancelled Node.js operation: ${operationId}`);
        }
    }

    /**
     * Wrapper for async operations that can be cancelled
     */
    async cancellableAsyncOperation(operationId, asyncFunc) {
        const controller = new AbortController();
        this.register(operationId, controller);

        try {
            const result = await asyncFunc(controller.signal);
            return result;
        } catch (error) {
            if (error.name === 'AbortError') {
                console.log(`Operation ${operationId} was cancelled`);
            }
            throw error;
        } finally {
            this.controllers.delete(operationId);
        }
    }
}

// Global registry
const cancellationRegistry = new CancellationRegistry();

// Example usage
async function performWork(operationId, params, signal) {
    return await cancellationRegistry.cancellableAsyncOperation(
        operationId,
        async (signal) => {
            // Long-running work with cancellation checks
            for (let i = 0; i < 100; i++) {
                if (signal.aborted) {
                    throw new Error('Cancelled');
                }

                await new Promise(resolve => setTimeout(resolve, 100));
                // Do work...
            }

            return "Completed";
        }
    );
}
```

---

## Event Loop Considerations

### Challenge: Different Event Loop Models

**Event Loop Comparison:**

| Runtime | Event Loop Model | Characteristics |
|---------|-----------------|-----------------|
| .NET | ThreadPool + SynchronizationContext | Multi-threaded, work-stealing queue |
| Python asyncio | Single-threaded event loop | Cooperative multitasking, no parallelism |
| Node.js | libuv event loop | Single-threaded, non-blocking I/O |

### ⚠️ ILLUSTRATIVE PATTERN: Event Loop Coordination

```csharp
// ⚠️ ILLUSTRATIVE EXAMPLE

public class EventLoopCoordinator
{
    private readonly PythonEventLoop _pythonLoop;
    private readonly NodeEventLoop _nodeLoop;

    public async Task CoordinateAsync(
        Func<Task> dotnetWork,
        Func<Task> pythonWork,
        Func<Task> nodeWork,
        CancellationToken ct)
    {
        // Run all event loops concurrently
        var dotnetTask = Task.Run(dotnetWork, ct);
        var pythonTask = _pythonLoop.EnqueueAsync(pythonWork, ct);
        var nodeTask = _nodeLoop.EnqueueAsync(nodeWork, ct);

        // Wait for all to complete
        await Task.WhenAll(dotnetTask, pythonTask, nodeTask);
    }
}

public class PythonEventLoop
{
    private readonly BlockingCollection<Func<Task>> _workQueue = new();
    private readonly Task _loopTask;

    public PythonEventLoop()
    {
        _loopTask = Task.Run(RunEventLoop);
    }

    private async Task RunEventLoop()
    {
        using (Py.GIL())
        {
            dynamic asyncio = Py.Import("asyncio");
            dynamic loop = asyncio.get_event_loop();

            while (!_workQueue.IsCompleted)
            {
                if (_workQueue.TryTake(out var work, 100))
                {
                    // Execute work in Python event loop
                    await work();
                }

                // Give Python event loop a chance to process
                loop.call_soon(loop.stop);
                loop.run_forever();
            }
        }
    }

    public Task EnqueueAsync(Func<Task> work, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<bool>();

        _workQueue.Add(async () =>
        {
            try
            {
                await work();
                tcs.SetResult(true);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        return tcs.Task;
    }
}
```

---

## Async Semantics Challenges

### Challenge 1: Task Completion vs Promise Resolution

**.NET Task:**
- Can be cancelled (IsCanceled)
- Can fault (IsFaulted)
- Can complete successfully (IsCompletedSuccessfully)

**Python asyncio.Future:**
- Can be cancelled
- Can have exception set
- Can have result set

**Node.js Promise:**
- Can resolve (success)
- Can reject (failure)
- No built-in cancellation (use AbortController)

**⚠️ ILLUSTRATIVE MAPPING:**

```csharp
// ⚠️ ILLUSTRATIVE EXAMPLE

public static class AsyncSemanticsBridge
{
    public static async Task<T> FromPythonFuture<T>(dynamic pythonFuture)
    {
        using (Py.GIL())
        {
            // Poll until done
            while (!pythonFuture.done())
            {
                await Task.Delay(10);
            }

            // Map Python semantics to .NET
            if (pythonFuture.cancelled())
            {
                throw new OperationCanceledException("Python future was cancelled");
            }

            dynamic exception = pythonFuture.exception();
            if (exception != null)
            {
                throw new InvalidOperationException($"Python error: {exception}");
            }

            return (T)pythonFuture.result();
        }
    }

    public static async Task<T> FromNodePromise<T>(
        string promiseId,
        JsonRpcClient rpcClient)
    {
        var response = await rpcClient.WaitForResponseAsync(promiseId);

        if (response.Error != null)
        {
            // Map Node.js rejection to .NET exception
            throw new InvalidOperationException(
                $"Node.js promise rejected: {response.Error.Message}");
        }

        return JsonSerializer.Deserialize<T>(response.Result.ToString());
    }
}
```

---

## Illustrative Integration Examples

### Example 1: Module with Mixed Async Operations

**⚠️ ILLUSTRATIVE EXAMPLE:**

```csharp
public class PolyglotAsyncModule : VisoraModule
{
    private readonly DotNetToPythonAsyncBridge _pythonBridge;
    private readonly DotNetToNodeAsyncBridge _nodeBridge;

    public override async ValueTask<CommandResult> ExecuteMixedAsyncCommand(
        CommandContext context,
        CancellationToken ct)
    {
        try
        {
            // 1. .NET async operation
            var dotnetData = await FetchFromDatabaseAsync(ct);

            // 2. Python async operation (ML processing)
            var pythonResult = await _pythonBridge.AwaitPythonAsync<ProcessedData>(
                pythonModule.process_data(dotnetData.ToPython()),
                ct);

            // 3. Node.js async operation (generate report)
            var nodeResult = await _nodeBridge.InvokeNodeAsyncMethod<Report>(
                "generateReport",
                new { data = pythonResult },
                ct);

            return CommandResult.Success("Mixed async operation completed", nodeResult);
        }
        catch (OperationCanceledException)
        {
            return CommandResult.Cancelled("Operation was cancelled");
        }
        catch (Exception ex)
        {
            return CommandResult.Failed($"Error: {ex.Message}");
        }
    }
}
```

---

## Challenges & Considerations

### Technical Challenges

**1. Event Loop Integration**
- .NET doesn't have a single global event loop
- Python asyncio requires running event loop
- Node.js has implicit event loop
- **Solution:** Dedicated threads for Python/Node.js event loops

**2. GIL (Global Interpreter Lock) in Python**
- Limits true parallelism in Python
- Blocks other threads when GIL is held
- **Solution:** Release GIL between operations, use multiprocessing for CPU-bound work

**3. Async/Await Syntax Differences**
- .NET: `async Task<T>`
- Python: `async def func() -> T`
- Node.js: `async function() { return T }`
- **Solution:** Abstraction layer that normalizes signatures

**4. Exception Propagation**
- Different exception hierarchies
- Stack traces don't cross runtime boundaries
- **Solution:** Serialize exception details, use result objects

**5. Deadlock Risks**
- Blocking wait on async from sync context
- Event loop starvation
- **Solution:** Async all the way, proper event loop management

---

## Related Documentation

- [Async Patterns - VISORA Analysis](./visora-analysis.md) - Actual .NET implementation
- [Plugin Architecture - Meta-Platform](../plugin-architecture/meta-platform-illustrations.md)
- [Result Objects - Meta-Platform](../result-objects/meta-platform-illustrations.md)

---

## Further Reading

### Python Async
- [Python asyncio Documentation](https://docs.python.org/3/library/asyncio.html)
- [Understanding Python's asyncio](https://realpython.com/async-io-python/)

### Node.js Async
- [Node.js Event Loop](https://nodejs.org/en/docs/guides/event-loop-timers-and-nexttick/)
- [Promises/A+ Specification](https://promisesaplus.com/)
- [AbortController API](https://developer.mozilla.org/en-US/docs/Web/API/AbortController)

### Cross-Runtime Patterns
- [Task-based Asynchronous Pattern (TAP)](https://docs.microsoft.com/en-us/dotnet/standard/asynchronous-programming-patterns/task-based-asynchronous-pattern-tap)

---

**Remember:** These are ILLUSTRATIVE EXAMPLES to inspire exploration, not production-ready solutions. Bridging async semantics across runtimes requires careful design, extensive testing, and deep understanding of each runtime's threading and event loop model.
