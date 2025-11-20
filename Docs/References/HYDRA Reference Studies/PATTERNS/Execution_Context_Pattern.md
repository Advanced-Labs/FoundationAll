# Execution Context Pattern - AsyncLocal Cross-Cutting Context Flow

**Pattern Category:** Cross-Cutting Concerns
**Complexity:** Medium
**Reusability:** Very High - applicable to any async .NET system needing implicit context

---

## Pattern Intent

Flow caller context (connection, session, user, trace) through async call chains without:
- Polluting method signatures with context parameters
- Manual parameter passing through intermediate layers
- Thread-local storage issues in async code

## Problem Being Solved

Services need caller information (who's calling, from where, with what auth):
- Adding context parameters to every method is verbose
- Intermediate layers don't need context but must pass it through
- Thread-local storage breaks with async/await (thread switches)
- Field injection happens once, but caller changes per request

## HYDRA Implementation

### Core: AsyncLocal Context Container

**File:** `Platform/Execution/HydraExecutionContext.cs` (65 lines)

```csharp
public sealed class HydraExecutionContext
{
    private static readonly AsyncLocal<HydraExecutionContext?> _current = new();

    public HydraConnection? Connection { get; }
    public HydraSession? Session { get; }
    public string? GatewayName { get; }

    private HydraExecutionContext(HydraConnection? connection, HydraSession? session, string? gatewayName)
    {
        Connection = connection;
        Session = session;
        GatewayName = gatewayName;
    }

    /// <summary>
    /// Gets the current execution context for this async flow.
    /// </summary>
    public static HydraExecutionContext? Current => _current.Value;

    /// <summary>
    /// Pushes a new execution context onto the async-local stack.
    /// Returns IDisposable to restore previous context.
    /// </summary>
    public static IDisposable Push(HydraConnection? connection, HydraSession? session, string? gatewayName)
    {
        var previous = _current.Value;
        _current.Value = new HydraExecutionContext(connection, session, gatewayName);
        return new ContextScope(previous);
    }

    private sealed class ContextScope : IDisposable
    {
        private readonly HydraExecutionContext? _previousContext;

        public ContextScope(HydraExecutionContext? previous)
        {
            _previousContext = previous;
        }

        public void Dispose()
        {
            _current.Value = _previousContext;  // Restore previous context
        }
    }
}
```

**Key Features:**
- **AsyncLocal:** Flows across await boundaries, not tied to thread
- **Immutable:** Context can't be mutated after creation
- **Stack-based:** Push/pop semantics via IDisposable
- **Null-safe:** Current can be null (no context pushed)

### Usage in ServiceRouter

**File:** `Platform/Foundations/Service Foundation/ServiceRouter.cs` (151 lines)

```csharp
public sealed class ServiceRouter
{
    public async Task<object?> InvokeAsync(
        HydraConnection connection,
        HydraSession session,
        string serviceName,
        string functionName,
        JToken? args,
        CancellationToken cancellationToken = default)
    {
        // ... parameter conversion, context creation ...

        // Push execution context before service invocation
        using (HydraExecutionContext.Push(connection, session, context.GatewayName))
        {
            var result = await service.InvokeAsync(functionName, parameters, context, cancellationToken);
            return result;
        }
        // Context auto-restored on dispose (via 'using')
    }
}
```

**Flow:**
1. Router receives connection + session
2. Push context onto async-local stack
3. Service invoked (has access to `HydraExecutionContext.Current`)
4. Service completes
5. Context popped (restored to previous value)

### Field Injection Attributes

**File:** `Platform/Infrastructures/DependencyInjection/CallerInjectionAttributes.cs` (36 lines)

```csharp
[AttributeUsage(AttributeTargets.Field)]
public sealed class InjectCallerConnectionAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Field)]
public sealed class InjectCallerSessionAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Field)]
public sealed class InjectCallerGatewayAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Field)]
public sealed class InjectCallerAccountAttribute : Attribute { }
```

### Field Injection Mechanism

**File:** `Platform/Infrastructures/DependencyInjection/HydraServiceActivator.cs` (129 lines)

```csharp
public sealed class HydraServiceActivator
{
    private static readonly ConcurrentDictionary<Type, InjectionDescriptor> _descriptorCache = new();

    public void InjectFields(object instance, InjectionDescriptor descriptor)
    {
        var context = HydraExecutionContext.Current;
        if (context == null) return;  // No context pushed

        // Inject connection fields
        foreach (var field in descriptor.ConnectionFields)
        {
            field.SetValue(instance, context.Connection);
        }

        // Inject session fields
        foreach (var field in descriptor.SessionFields)
        {
            field.SetValue(instance, context.Session);
        }

        // Inject gateway fields
        foreach (var field in descriptor.GatewayFields)
        {
            field.SetValue(instance, context.GatewayName);
        }

        // Inject account fields (derived from connection)
        foreach (var field in descriptor.AccountFields)
        {
            field.SetValue(instance, context.Connection?.Account);
        }
    }

    public InjectionDescriptor GetOrCreateDescriptor(Type type)
    {
        return _descriptorCache.GetOrAdd(type, t =>
        {
            var fields = t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            return new InjectionDescriptor
            {
                ConnectionFields = fields.Where(f => f.GetCustomAttribute<InjectCallerConnectionAttribute>() != null).ToList(),
                SessionFields = fields.Where(f => f.GetCustomAttribute<InjectCallerSessionAttribute>() != null).ToList(),
                GatewayFields = fields.Where(f => f.GetCustomAttribute<InjectCallerGatewayAttribute>() != null).ToList(),
                AccountFields = fields.Where(f => f.GetCustomAttribute<InjectCallerAccountAttribute>() != null).ToList()
            };
        });
    }
}
```

**Optimization:** Reflection done once per service type, cached in `_descriptorCache`

### Service Example

```csharp
[HydraService("ContextAwareService")]
public sealed class ContextAwareService : HydraServiceBase
{
    [InjectCallerConnection]
    private HydraConnection? _connection;

    [InjectCallerSession]
    private HydraSession? _session;

    [InjectCallerGateway]
    private string? _gatewayName;

    protected override Task<object?> OnInvokeAsync(
        string functionName,
        Dictionary<string, object?> parameters,
        ServiceInvocationContext context,
        CancellationToken cancellationToken)
    {
        // Fields automatically populated from HydraExecutionContext.Current

        // Can also access context directly
        var currentContext = HydraExecutionContext.Current;

        return Task.FromResult<object?>(new
        {
            // From injected fields
            connectionId = _connection?.ConnectionId,
            sessionId = _session?.Session.SessionId,
            gateway = _gatewayName,

            // From static accessor
            sameConnectionId = currentContext?.Connection?.ConnectionId,

            // From method parameter (also available)
            contextSessionId = context.SessionId
        });
    }
}
```

**Three ways to access context:**
1. `HydraExecutionContext.Current` (static accessor)
2. Injected fields (via attributes)
3. Method parameters (explicit)

---

## Pattern Structure

### Flow Diagram

```
┌──────────────────────────────────────────────────────┐
│             ServiceRouter                             │
│  using (HydraExecutionContext.Push(conn, sess, gw))  │
│  {                                                    │
│    ┌────────────────────────────────────────────┐   │
│    │       AsyncLocal<Context> Stack            │   │
│    │  [Current: Context(conn, sess, gw)]        │   │
│    └────────────────────────────────────────────┘   │
│                       ↓                               │
│    await service.InvokeAsync(...)                    │
│      ↓                                                │
│    ┌────────────────────────────────────────────┐   │
│    │       Service Instance                      │   │
│    │  Fields injected from Context:              │   │
│    │  - _connection (from Context.Connection)    │   │
│    │  - _session (from Context.Session)          │   │
│    │  - _gatewayName (from Context.GatewayName)  │   │
│    └────────────────────────────────────────────┘   │
│      ↓                                                │
│    Service business logic executes                   │
│    (can read HydraExecutionContext.Current)          │
│      ↓                                                │
│    return result;                                    │
│  }  ← Context auto-popped (IDisposable)              │
└──────────────────────────────────────────────────────┘
```

### AsyncLocal vs ThreadLocal

| Feature | `ThreadLocal<T>` | `AsyncLocal<T>` |
|---------|------------------|-----------------|
| Async/await safe | ❌ No (breaks on thread switch) | ✅ Yes (flows across awaits) |
| Thread-specific | ✅ Yes | ❌ No (async flow, not thread) |
| Use case | Synchronous code | Asynchronous code |

**Example showing ThreadLocal problem:**
```csharp
// ❌ WRONG: ThreadLocal breaks with async
ThreadLocal<Context> _context = new();

public async Task ProcessAsync()
{
    _context.Value = new Context("user1");
    await Task.Delay(100);  // Thread may change here!
    Console.WriteLine(_context.Value?.UserId);  // May be null or different user!
}
```

**Example showing AsyncLocal solution:**
```csharp
// ✅ CORRECT: AsyncLocal flows across awaits
AsyncLocal<Context> _context = new();

public async Task ProcessAsync()
{
    _context.Value = new Context("user1");
    await Task.Delay(100);  // Thread may change, but context preserved
    Console.WriteLine(_context.Value?.UserId);  // Still "user1"
}
```

---

## Key Design Decisions

### 1. AsyncLocal Instead of ThreadLocal
**Decision:** Use `AsyncLocal<T>` for context storage

**Rationale:**
- Flows across async boundaries (thread switches)
- Each async call chain gets isolated context
- No manual context passing required

**Alternative:** Explicit context parameter - verbose but explicit

### 2. Stack-Based Push/Pop with IDisposable
**Decision:** Return `IDisposable` from Push() to auto-restore context

**Rationale:**
- `using` statement guarantees cleanup
- Prevents context leaks (forgot to pop)
- Nesting supported (inner using blocks)

**Example:**
```csharp
using (HydraExecutionContext.Push(conn1, sess1, "Gateway1"))
{
    // Context1 active
    using (HydraExecutionContext.Push(conn2, sess2, "Gateway2"))
    {
        // Context2 active (nested)
    }
    // Context1 restored
}
// No context (null)
```

### 3. Immutable Context
**Decision:** Context properties are read-only after creation

**Rationale:**
- Prevents accidental mutation mid-request
- Safe for concurrent access
- Clear ownership (set once by router)

### 4. Field Injection + Static Accessor
**Decision:** Provide both field injection and static accessor

**Rationale:**
- Field injection: Convenient, cached per instance
- Static accessor: Flexible, works anywhere in call chain
- Developer choice based on preference

**Tradeoff:** Two ways to access same data (consistency?)

---

## Reproducing This Pattern in Other .NET Projects

### Step 1: Define Context Container

```csharp
public sealed class RequestContext
{
    private static readonly AsyncLocal<RequestContext?> _current = new();

    public string UserId { get; }
    public string TenantId { get; }
    public string RequestId { get; }

    private RequestContext(string userId, string tenantId, string requestId)
    {
        UserId = userId;
        TenantId = tenantId;
        RequestId = requestId;
    }

    public static RequestContext? Current => _current.Value;

    public static IDisposable Push(string userId, string tenantId, string requestId)
    {
        var previous = _current.Value;
        _current.Value = new RequestContext(userId, tenantId, requestId);
        return new ContextScope(previous);
    }

    private sealed class ContextScope : IDisposable
    {
        private readonly RequestContext? _previous;

        public ContextScope(RequestContext? previous) => _previous = previous;

        public void Dispose() => _current.Value = _previous;
    }
}
```

### Step 2: Push Context at Entry Point

```csharp
public class ApiController
{
    public async Task<IActionResult> HandleRequest(HttpRequest request)
    {
        var userId = request.Headers["X-User-ID"];
        var tenantId = request.Headers["X-Tenant-ID"];
        var requestId = Guid.NewGuid().ToString();

        using (RequestContext.Push(userId, tenantId, requestId))
        {
            var result = await ProcessRequest(request);
            return Ok(result);
        }
        // Context auto-cleaned up
    }

    private async Task<object> ProcessRequest(HttpRequest request)
    {
        // Deep in call stack, no context parameter needed
        var service = new BusinessService();
        return await service.ProcessAsync();
    }
}
```

### Step 3: Access Context in Services

```csharp
public class BusinessService
{
    public async Task<object> ProcessAsync()
    {
        // Access context anywhere in async call chain
        var context = RequestContext.Current;
        if (context == null)
            throw new InvalidOperationException("No request context available");

        Console.WriteLine($"Processing request {context.RequestId} for user {context.UserId}");

        // Context flows through awaits
        await Task.Delay(100);
        Console.WriteLine($"Still have context: {context.RequestId}");

        return new { success = true };
    }
}
```

### Step 4: (Optional) Add Field Injection

```csharp
[AttributeUsage(AttributeTargets.Field)]
public sealed class InjectUserIdAttribute : Attribute { }

public class ServiceActivator
{
    public void InjectFields(object instance)
    {
        var context = RequestContext.Current;
        if (context == null) return;

        var fields = instance.GetType()
            .GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(f => f.GetCustomAttribute<InjectUserIdAttribute>() != null);

        foreach (var field in fields)
        {
            field.SetValue(instance, context.UserId);
        }
    }
}

public class MyService
{
    [InjectUserId]
    private string? _userId;

    public void DoWork()
    {
        Console.WriteLine($"Working for user: {_userId}");
    }
}
```

---

## Tradeoffs & Constraints

### Advantages
✅ Clean method signatures (no context parameters)
✅ Async/await safe (flows across thread switches)
✅ Automatic cleanup (using statement)
✅ Works deep in call stack
✅ Testable (mock context in tests)

### Limitations
⚠️ Less explicit than parameters (implicit dependency)
⚠️ Null checks required (context may not be set)
⚠️ Field injection uses reflection (performance overhead)
⚠️ Debugging harder (where was context set?)

### When NOT to Use This Pattern
❌ Simple synchronous code (pass parameters directly)
❌ Context rarely needed (explicit parameter better)
❌ Very high performance requirements (parameter passing faster)
❌ Single-threaded, no async (ThreadLocal sufficient)

---

## Related Patterns

- **Ambient Context (Fowler):** Similar concept, implicit context access
- **Dependency Injection:** Field injection complementary to constructor DI
- **Chain of Responsibility:** Context flows through handler chain
- **ThreadLocal Storage:** Predecessor pattern (sync-only)

---

## Gen2 Evolution Notes

**Current (Gen1):** AsyncLocal + reflection-based field injection

**Future (Gen2):**
- Source generators replace reflection (compile-time field injection)
- Akka.NET context may use actor message headers
- W3C Trace Context standard for distributed tracing

**Migration Strategy:**
- Keep AsyncLocal pattern (works with actors)
- Replace field injection with source generation
- Add OpenTelemetry baggage for cross-service context

---

**Last Updated:** 2025-11-10
**Pattern Stability:** Very High - AsyncLocal is stable .NET pattern
**Code References:** HydraExecutionContext.cs:1-65, HydraServiceActivator.cs:1-129, ServiceRouter.cs:88-93
