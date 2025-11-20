# 7. Execution Context (AsyncLocal)

## 7.1 HydraExecutionContext

**File:** `/src/Hydra/Hydra.Server/Platform/Foundations/Service Foundation/HydraExecutionContext.cs` (64 lines)
**Status:** ✅ Working

### AsyncLocal Implementation

**Storage (Line 14):**
```csharp
private static readonly AsyncLocal<HydraExecutionContext?> _current = new();
```

**Current Property (Line 22):**
```csharp
public static HydraExecutionContext? Current => _current.Value;
```

### How It Flows Across Async Boundaries

**AsyncLocal<T> Behavior:**
- Value automatically captured when `await` suspends
- Value flows to continuation (even on different thread)
- Each async call chain has its own value
- No manual parameter passing required

**Example:**
```csharp
using (HydraExecutionContext.Push(connection, session, gateway))
{
    await SomeAsyncMethod();  // Context flows here
    await AnotherAsyncMethod(); // And here
    // Even across thread pool threads!
}
// Context automatically restored to previous value
```

### Push/Pop Pattern

**Push Method (Lines 29-36):**
```csharp
public static IDisposable Push(
    HydraConnection? connection,
    HydraSession? session,
    string? gatewayName)
{
    var previous = _current.Value;
    var newContext = new HydraExecutionContext(connection, session, gatewayName);
    _current.Value = newContext;
    
    return new ContextScope(previous);  // Disposes → restore
}
```

**ContextScope (Lines 45-63):**
```csharp
private sealed class ContextScope : IDisposable
{
    private readonly HydraExecutionContext? _previousContext;
    private bool _disposed;

    public ContextScope(HydraExecutionContext? previousContext)
    {
        _previousContext = previousContext;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _current.Value = _previousContext;  // Restore!
            _disposed = true;
        }
    }
}
```

**Nested Contexts Supported:**
```csharp
using (HydraExecutionContext.Push(conn1, sess1, "GW1"))
{
    // Context: conn1, sess1, GW1
    
    using (HydraExecutionContext.Push(conn2, sess2, "GW2"))
    {
        // Context: conn2, sess2, GW2 (inner)
    }
    
    // Context: conn1, sess1, GW1 (restored)
}
// Context: null (restored)
```

### Context Properties

**Fields (Lines 16-20):**
```csharp
public HydraConnection? Connection { get; }
public HydraSession? Session { get; }
public string? GatewayName { get; }
```

All properties are immutable (init-only), preventing accidental mutation.

## 7.2 Field Injection

### Caller Injection Attributes

**File:** `/src/Hydra/Hydra.Server/Platform/Infrastructures/DependencyInjection/CallerInjectionAttributes.cs` (35 lines)

**Available Attributes:**

1. **[InjectCallerConnection]** (Lines 8-11)
   - Injects `HydraConnection?`
   - Source: `HydraExecutionContext.Current?.Connection`

2. **[InjectCallerSession]** (Lines 16-19)
   - Injects `HydraSession?`
   - Source: `HydraExecutionContext.Current?.Session`

3. **[InjectCallerGateway]** (Lines 24-27)
   - Injects `string?` (gateway name)
   - Source: `HydraExecutionContext.Current?.GatewayName`

4. **[InjectCallerAccount]** (Lines 32-35)
   - Injects `Account?`
   - Source: Resolved from `Connection.AccountId` via `AccountRepository`

**Usage:**
```csharp
public class MyService : HydraServiceBase
{
    [InjectCallerConnection]
    private HydraConnection? _connection;
    
    [InjectCallerSession]
    private HydraSession? _session;
    
    [InjectCallerGateway]
    private string? _gatewayName;
    
    [InjectCallerAccount]
    private Account? _account;
    
    protected override async Task<object?> OnInvokeAsync(...)
    {
        var userId = _account?.UserName;  // Use injected field
    }
}
```

### HydraServiceActivator

**File:** `/src/Hydra/Hydra.Server/Platform/Infrastructures/DependencyInjection/HydraServiceActivator.cs` (130 lines)
**Status:** ✅ Working (has tests)

**Caching Strategy (Lines 19-24):**
```csharp
private readonly ConcurrentDictionary<Type, InjectionDescriptor> _injectionCache = new();
private readonly AccountRepository _accountRepository;
```

**CreateInstance (Lines 38-54):**
```csharp
public object CreateInstance(Type serviceType)
{
    // Create instance via Activator
    var instance = Activator.CreateInstance(serviceType);
    
    // Get or build injection descriptor (cached)
    var descriptor = _injectionCache.GetOrAdd(serviceType, BuildInjectionDescriptor);
    
    // Inject fields from current context
    InjectFields(instance, descriptor);
    
    return instance;
}
```

**BuildInjectionDescriptor (Lines 56-88):**
```csharp
private InjectionDescriptor BuildInjectionDescriptor(Type serviceType)
{
    var descriptor = new InjectionDescriptor();
    
    // Scan all fields (public, non-public, instance)
    var fields = serviceType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
    
    foreach (var field in fields)
    {
        // Check for [InjectCallerConnection]
        if (field.GetCustomAttribute<InjectCallerConnectionAttribute>() != null)
            descriptor.ConnectionFields.Add(field);
        
        // Check for [InjectCallerSession]
        if (field.GetCustomAttribute<InjectCallerSessionAttribute>() != null)
            descriptor.SessionFields.Add(field);
        
        // Check for [InjectCallerGateway]
        if (field.GetCustomAttribute<InjectCallerGatewayAttribute>() != null)
            descriptor.GatewayFields.Add(field);
        
        // Check for [InjectCallerAccount]
        if (field.GetCustomAttribute<InjectCallerAccountAttribute>() != null)
            descriptor.AccountFields.Add(field);
    }
    
    return descriptor;  // Cached for future instances
}
```

**InjectFields (Lines 90-119):**
```csharp
private void InjectFields(object instance, InjectionDescriptor descriptor)
{
    var context = HydraExecutionContext.Current;  // Read AsyncLocal
    
    // Inject connection fields
    foreach (var field in descriptor.ConnectionFields)
    {
        field.SetValue(instance, context?.Connection);
    }
    
    // Inject session fields
    foreach (var field in descriptor.SessionFields)
    {
        field.SetValue(instance, context?.Session);
    }
    
    // Inject gateway fields
    foreach (var field in descriptor.GatewayFields)
    {
        field.SetValue(instance, context?.GatewayName);
    }
    
    // Inject account fields (requires lookup)
    foreach (var field in descriptor.AccountFields)
    {
        var accountId = context?.Connection?.AccountId;
        var account = accountId != null ? _accountRepository.GetById(accountId) : null;
        field.SetValue(instance, account);
    }
}
```

**InjectionDescriptor (Lines 121-128):**
```csharp
private sealed class InjectionDescriptor
{
    public List<FieldInfo> ConnectionFields { get; } = new();
    public List<FieldInfo> SessionFields { get; } = new();
    public List<FieldInfo> GatewayFields { get; } = new();
    public List<FieldInfo> AccountFields { get; } = new();
}
```

**Performance Optimization:**
- Reflection scan done **once per type** (cached in `ConcurrentDictionary`)
- Subsequent instances use cached descriptor
- Field.SetValue is fast (direct memory write)

## 7.3 ServiceRouter Usage

**File:** `/src/Hydra/Hydra.Server/Platform/Foundations/Service Foundation/ServiceRouter.cs` (Lines 88-93)

**Context Push Before Service Invocation:**
```csharp
using (HydraExecutionContext.Push(connection, session, context.GatewayName))
{
    var result = await service.InvokeAsync(functionName, parameters, context, cancellationToken);
    Logger.LogDebug($"Invocation completed: Service='{serviceName}', Function='{functionName}'", "ServiceRouter");
    return result;
}
// Context automatically popped via Dispose()
```

**Flow:**
1. ServiceRouter.InvokeAsync called by gateway
2. Push connection, session, gateway name to AsyncLocal
3. Service.InvokeAsync called (context flows across await)
4. Service activator reads HydraExecutionContext.Current
5. Injected fields populated
6. Service executes with access to caller info
7. using block exits → context restored

## 7.4 Benefits

**No Explicit Parameter Passing:**
- Service methods don't need `(HydraConnection connection, HydraSession session)` params
- Cleaner signatures
- Protocol-agnostic (same signature for MCP, VISOR, HTTP)

**Type-Safe:**
- Fields are strongly typed (`HydraConnection?`, not `object`)
- Compile-time checking
- IntelliSense support

**Performance:**
- Reflection done once per type (cached)
- AsyncLocal is thread-safe and fast
- No allocations per call (descriptor reused)

**Testable:**
- Tests can push custom context before calling service
- Nested contexts for test isolation
- Null context handling for unit tests

**Clean Code:**
```csharp
// WITHOUT field injection:
public async Task<object?> HandleMcp(
    HydraConnection connection,      // Boilerplate
    HydraSession session,             // Boilerplate
    string gatewayName,               // Boilerplate
    Dictionary<string, object?> params)
{
    var userId = connection.AccountId; // Use it
}

// WITH field injection:
[InjectCallerConnection]
private HydraConnection? _connection;

public async Task<object?> HandleMcp(
    Dictionary<string, object?> params)  // Clean signature
{
    var userId = _connection?.AccountId; // Use it
}
```
