# HYDRA Service Foundation Guide (Gen1 V1 - Temporary)
**Last Updated:** 2025-11-09
**Status:** Operational but temporary - Gen2 replacement planned
**Lifetime:** This architecture will be replaced in Gen2
**Purpose:** Practical guide for working with current service system

## 0. Important Context: This Is Temporary

### Why Document Something Temporary?
- Gen1 services are operational and used in production
- New services will be added before Gen2
- Understanding current system enables smooth Gen2 migration
- AI agents need to maintain existing services during Gen1 lifetime

### What's Being Replaced in Gen2?
The documentation mentions "generational" architecture evolution (V1, V2, etc.) for various platform components, but specific Gen2 service system replacement plans are not yet documented. The current system is explicitly marked as operational but temporary.

**Known temporary components:**
- MessageRouter reflection-based dispatch (line 6 of `MessageRouter.cs`: "Reflection-based message router")
- Manual service registration via `GetFunctions()` (metadata must be manually maintained)
- Per-session instance caching in static dictionaries (see `HydraServiceBase.cs:19-21`)

### Migration Strategy
- Services written now should minimize Gen2 migration work
- Follow patterns in this guide
- Avoid deep coupling to reflection system
- Keep business logic separate from infrastructure
- Business logic in `OnInvokeAsync` will likely remain compatible

## 1. Quick Start: Creating Your First Service

### Minimal Service Template
```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hydra.Server.Backends.Services;
using Hydra.Server.Backends.Models;

namespace Hydra.Server.Platform.CoreServices;

[HydraService("MyService", Description = "Does something useful")]
public sealed class MyService : HydraServiceBase
{
    public MyService() : base("MyService", maxInstancesPerSession: 1) { }

    protected override async Task<object?> OnInvokeAsync(
        string functionName,
        Dictionary<string, object?> parameters,
        ServiceInvocationContext context,
        CancellationToken cancellationToken)
    {
        return functionName switch
        {
            "doSomething" => await DoSomething(parameters, cancellationToken),
            _ => throw new NotSupportedException($"Unknown function: {functionName}")
        };
    }

    private async Task<object?> DoSomething(
        Dictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        // Extract parameter
        if (!parameters.TryGetValue("input", out var inputObj))
            throw new ArgumentException("Missing 'input' parameter");

        var input = inputObj?.ToString() ?? "";

        // Your logic here
        await Task.CompletedTask; // Example async work

        return new { success = true, echo = input };
    }

    public override IReadOnlyList<ServiceFunctionDescriptor> GetFunctions()
    {
        return new List<ServiceFunctionDescriptor>
        {
            new ServiceFunctionDescriptor
            {
                Name = "doSomething",
                Description = "Does something useful",
                IsStreaming = false,
                Parameters = new List<ServiceParameterDescriptor>
                {
                    new ServiceParameterDescriptor
                    {
                        Name = "input",
                        Description = "Input string",
                        Required = true,
                        ParameterType = typeof(string)
                    }
                },
                ReturnType = typeof(object)
            }
        };
    }
}
```

### Where to Put It
- **Location**: `src/Hydra/Hydra.Server/Platform/Core Services/[ServiceName]/`
- **Naming**: `[ServiceName].cs`
- **Namespace**: `Hydra.Server.Platform.CoreServices` or `Hydra.Server.Backends.Services`

### How It Gets Discovered
1. `ServiceRegistry.Global.DiscoverServices()` scans assembly for `[HydraService]` attributes (`ServiceRegistry.cs:38-54`)
2. Service registered automatically at startup (`App.xaml.cs:63`)
3. Available to all gateways immediately via `ServiceRouter`

## 2. Core Interfaces & Base Classes

### 2.1 IHydraService Interface
**File**: `src/Hydra/Hydra.Server/Platform/Foundations/Service Foundation/IHydraService.cs` (71 lines)
**Status**: ✅ Stable for Gen1

**Key Members**:
```csharp
public interface IHydraService
{
    string Name { get; }
    bool IsEnabled { get; }
    ServiceStats Stats { get; }
    int MaxInstancesPerSession { get; }

    Task<object?> InvokeAsync(
        string functionName,
        Dictionary<string, object?> parameters,
        ServiceInvocationContext context,
        CancellationToken cancellationToken = default);

    IReadOnlyList<ServiceFunctionDescriptor> GetFunctions();
    Task SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default);
    Task RestartAsync(CancellationToken cancellationToken = default);

    event EventHandler<ServiceStats>? StatsUpdated;
}
```

**What You Implement**: Usually inherit `HydraServiceBase` instead of implementing this directly.

### 2.2 HydraServiceBase Abstract Class
**File**: `src/Hydra/Hydra.Server/Platform/Foundations/Service Foundation/HydraServiceBase.cs` (214 lines)
**Status**: ✅ Operational

**What It Provides**:
- Static per-session instance registry (`_instanceRegistry`, line 19-21)
- Stats tracking (requests, errors, activity) (`_stats`, line 23)
- Enable/disable functionality (`IsEnabled`, line 34)
- Lifecycle management (restart, dispose)
- Activity tracking with 15-second timeout (line 198)

**What You Override**:
- `OnInvokeAsync()` - **Required**: Your dispatch logic (line 156-160)
- `OnRestartAsync()` - **Optional**: Cleanup/reinit logic (line 179)
- `GetFunctions()` - **Required**: Function metadata (line 150)

**What You Don't Touch**:
- `InvokeAsync()` - Base class handles this (line 120-148)
- Stats management - Automatic (line 131, 137, 143)
- Instance caching - Automatic (line 45-65)

**Instance Management**:
```csharp
// Called by ServiceRegistry - you don't call this directly
public static T GetOrCreateInstance<T>(string sessionId, Func<T> factory)
    where T : HydraServiceBase
// Returns existing instance or creates new one per session
// Line 45-65 of HydraServiceBase.cs
```

### 2.3 ServiceInvocationContext
**File**: `src/Hydra/Hydra.Server/Platform/Foundations/Service Foundation/ServiceInvocationContext.cs` (43 lines)

**Fields**:
```csharp
public sealed class ServiceInvocationContext
{
    public string SessionId { get; set; }      // Unique session identifier
    public string GatewayName { get; set; }     // e.g., "Hydra MCP Gateway"
    public string? ConnectionId { get; set; }   // Optional connection ID
    public string? UserId { get; set; }         // Optional user ID
    public HydraSession? Runtime { get; set; }  // Hydrated session object
    public Dictionary<string, object?> Metadata { get; set; }
}
```

**Usage**: Provided to `OnInvokeAsync`, gives you caller context.

## 3. Service Attributes

### 3.1 HydraServiceAttribute (Class-Level)
**File**: `src/Hydra/Hydra.Server/Platform/Foundations/Service Foundation/HydraServiceAttribute.cs` (lines 10-48)

```csharp
[HydraService(
    "ServiceName",                    // Required
    Description = "What it does",     // Optional
    MaxInstancesPerSession = 1,       // Default: 1 (one per session)
    BootPriority = 5000,              // Lower = starts earlier
    AutoStart = false                 // Start on app boot?
)]
```

**Required**: `Name`
**Optional**: `Description`, `MaxInstancesPerSession`, `BootPriority`, `AutoStart`

**Example from HydraSecurityService** (`HydraSecurityService.cs:15-19`):
```csharp
[HydraService(
    "HydraSecurityService",
    Description = "OAuth/OpenIddict provider & security registry",
    AutoStart = true,
    BootPriority = 100)]
```

### 3.2 HydraServiceFunctionAttribute (Method-Level)
**File**: `HydraServiceAttribute.cs` (lines 56-81)
**Status**: ⚠️ **Not currently used** in Gen1

Attributes exist but Gen1 doesn't use method-level function discovery. Services dispatch manually in `OnInvokeAsync` switch statement.

**Gen2**: Will likely use these for automatic dispatch.

### 3.3 HydraServiceParamAttribute (Parameter-Level)
**File**: `HydraServiceAttribute.cs` (lines 87-113)
**Status**: ⚠️ **Not currently used** in Gen1

**Note**: HydraSecurityService shows these attributes in use (`HydraSecurityService.cs:82-86`), but they're for documentation only in Gen1.

## 4. Execution Context & Caller Injection

### 4.1 HydraExecutionContext (AsyncLocal)
**File**: `src/Hydra/Hydra.Server/Platform/Execution/HydraExecutionContext.cs` (65 lines)

**What It Does**: Flows caller information across async boundaries without explicit parameters using `AsyncLocal<T>` (line 14).

**Access Pattern**:
```csharp
var context = HydraExecutionContext.Current;  // Line 30
var connection = context?.Connection;
var session = context?.Session;
var gatewayName = context?.GatewayName;
```

**When It's Set**: `ServiceRouter` pushes it before calling your service (`ServiceRouter.cs:88-93`):
```csharp
// Line 88 of ServiceRouter.cs
using (HydraExecutionContext.Push(connection, session, context.GatewayName))
{
    var result = await service.InvokeAsync(functionName, parameters, context, default);
    // ...
}
```

### 4.2 Field Injection Attributes
**File**: `src/Hydra/Hydra.Server/Platform/Infrastructures/DependencyInjection/CallerInjectionAttributes.cs` (36 lines)

**Available Attributes**:
```csharp
[InjectCallerConnection]
private HydraConnection? _connection;

[InjectCallerSession]
private HydraSession? _session;

[InjectCallerGateway]
private string? _gatewayName;

[InjectCallerAccount]
private Account? _account;
```

**When Injected**: During service activation by `HydraServiceActivator` (`HydraServiceActivator.cs:90-119`)

**How It Works**:
1. Activator scans fields for injection attributes (line 56-88)
2. Builds cached `InjectionDescriptor` per service type (line 48)
3. Injects from `HydraExecutionContext.Current` (line 90-119)

**Example**:
```csharp
[HydraService("ContextService")]
public sealed class ContextService : HydraServiceBase
{
    [InjectCallerConnection]
    private HydraConnection? _connection;

    [InjectCallerSession]
    private HydraSession? _session;

    protected override Task<object?> OnInvokeAsync(...)
    {
        var connectionId = _connection?.ConnectionId;
        var sessionId = _session?.Session.SessionId;
        // Use them...
    }
}
```

## 5. Service Discovery & Registration

### 5.1 ServiceRegistry (Singleton)
**File**: `src/Hydra/Hydra.Server/Platform/Foundations/Service Foundation/ServiceRegistry.cs` (271 lines)

**Bootstrap Flow** (from `App.xaml.cs:62-68`):
```csharp
// In App.xaml.cs OnStartup
Backends.Services.ServiceRegistry.Global.Initialize(_host.Services);
Backends.Services.ServiceRegistry.Global.DiscoverServices();

await Backends.Services.ServiceBootManager.AutoStartHostedServicesAsync(
    Backends.Services.ServiceRegistry.Global, _host.Services, CancellationToken.None);
```

**What DiscoverServices() Does** (`ServiceRegistry.cs:38-54`):
1. Scans executing assembly for `[HydraService]` attributes (line 40-43)
2. Registers each service type by name (line 46-50)
3. Stores metadata (name, description, priority)
4. Logs discovery count (line 53)

**Registration Methods** (Manual - usually not needed):
```csharp
ServiceRegistry.Global.Register("name", typeof(MyService));  // Line 61
ServiceRegistry.Global.Unregister("name");                    // Line 92
```

### 5.2 Instance Management
**Per-Session Caching** (`ServiceRegistry.cs:157-227`):
```csharp
// Automatic - called by ServiceRouter
var service = _serviceRegistry.GetOrCreateInstance(serviceName, sessionId);
// Line 82 of ServiceRouter.cs
```

**How It Works**:
1. Uses reflection to call `HydraServiceBase.GetOrCreateInstance<T>` (line 180-188)
2. Tries Lamar container first (line 198-202)
3. Falls back to `ActivatorUtilities.CreateInstance` with DI (line 206-207)
4. Caches instance in static dictionary keyed by `serviceName` + `sessionId`

**Lifecycle**:
- Created on first invocation for a session
- Cached for subsequent calls in same session (via static `_instanceRegistry`)
- Activity tracked with 15-second timeout (`HydraServiceBase.cs:189-201`)
- Cleaned up when session ends (via `RemoveInstance`, line 87-97)

## 6. Service Invocation Flow

### 6.1 Complete Call Path

```
Gateway receives message
    ↓
ServiceRouter.InvokeAsync(connection, session, serviceName, functionName, args)
  [ServiceRouter.cs:40-100]
    ↓
Convert JToken args → Dictionary<string, object?>
  [Line 60-68: Extracts properties from JObject]
    ↓
Create ServiceInvocationContext (sessionId, connectionId, gatewayName)
  [Line 71-76]
    ↓
Create per-call DI scope (if IHydraScopeFactory available)
  [Line 79: using var scope = _scopeFactory?.CreateForCall(...)]
    ↓
ServiceRegistry.GetOrCreateInstance(serviceName, sessionId)
  [Line 82: var service = _serviceRegistry.GetOrCreateInstance(...)]
  └─> HydraServiceBase.GetOrCreateInstance<T>(sessionId, factory)
      [HydraServiceBase.cs:45-65]
      ├─> Check _instanceRegistry for existing (line 50-53)
      ├─> Create new via factory if not found (line 55)
      └─> Cache in _instanceRegistry[serviceName][sessionId] (line 61)
    ↓
Inject service fields ([InjectCaller*])
  [Line 85: scope?.Inject(service)]
  └─> HydraServiceActivator.InjectFields(instance, descriptor)
      [HydraServiceActivator.cs:90-119]
    ↓
Push HydraExecutionContext
  [Line 88: using (HydraExecutionContext.Push(connection, session, ...))]
    ↓
Service.InvokeAsync(functionName, parameters, context, cancellationToken)
  [Line 90]
    ↓
HydraServiceBase.InvokeAsync (base implementation)
  [HydraServiceBase.cs:120-148]
    ├─> Check IsEnabled (line 126-129)
    ├─> Increment RequestsReceived (line 131)
    ├─> Touch instance activity (line 132)
    ├─> Call OnInvokeAsync() ← YOUR CODE HERE (line 136)
    ├─> Increment RequestsServed or Errors (line 137, 143)
    └─> RaiseStatsUpdated (line 138, 144)
    ↓
Return result
    ↓
HydraExecutionContext.Pop() (automatic via using)
  [Line 93 - disposed by using statement]
    ↓
DI scope disposed
  [Line 79 - disposed by using statement]
    ↓
Result returned to gateway
```

### 6.2 Parameter Conversion
**Gateway Provides**: `JToken` (from JSON)
**ServiceRouter Converts**: `Dictionary<string, object?>` (`ServiceRouter.cs:60-68`)
**Your Service Extracts**: Specific types

**Conversion Code** (`ServiceRouter.cs:60-68`):
```csharp
var parameters = new Dictionary<string, object?>();
if (args != null && args.Type == JTokenType.Object)
{
    foreach (var prop in ((JObject)args).Properties())
    {
        parameters[prop.Name] = prop.Value.ToObject<object>();
    }
}
```

**Example Extraction**:
```csharp
private async Task<object?> MyFunction(
    Dictionary<string, object?> parameters,
    CancellationToken cancellationToken)
{
    if (!parameters.TryGetValue("text", out var textObj))
        throw new ArgumentException("Missing 'text' parameter");

    var text = textObj?.ToString();
    if (string.IsNullOrEmpty(text))
        throw new ArgumentException("'text' cannot be empty");

    // Process text...
    return new { result = text.ToUpper() };
}
```

## 7. DI Integration (Lamar)

### 7.1 HydraCompositionRegistry
**File**: `src/Hydra/Hydra.Server/Platform/Infrastructures/DependencyInjection/HydraCompositionRegistry.cs` (72 lines)

**What Gets Registered** (lines 22-69):
- **Singletons**:
  - `IDocumentStore` (RavenDB) - line 22-38
  - `HydraStoreSystemV1` - line 45-46
  - `ISessionManager` / `HydraSessionManager` - line 47
  - `IHydraScopeFactory` - line 48
  - Storage drivers - line 51-52
  - Session repository - line 55
  - Visor components - line 58-61

- **Scoped**:
  - `IAsyncDocumentSession` - line 41-42 (new RavenDB session per scope)

- **Services**: Auto-discovered via `[HydraService]` (bootstrap in `App.xaml.cs`)

**Constructor Injection** (for dependencies):
```csharp
public sealed class MyService : HydraServiceBase
{
    private readonly IDocumentStore _store;

    // DI container will inject IDocumentStore
    public MyService(IDocumentStore store) : base("MyService")
    {
        _store = store;
    }
}
```

### 7.2 Service Lifetimes
- **Services themselves**: Per-session singleton (one instance per session via static registry)
- **Dependencies**: As configured in DI (Singleton/Scoped/Transient)
- **Per-call scope**: Created for each invocation (`ServiceRouter.cs:79`)

**How It Works**:
1. `ServiceRouter` creates DI scope per call (line 79)
2. Service instance retrieved from per-session cache
3. Service injected into scope to access scoped dependencies (line 85)
4. Scope disposed after invocation completes

## 8. Working Examples

### 8.1 HydraCoreService (Simplest)
**File**: `src/Hydra/Hydra.Server/Platform/Core Services/HydraCoreService.cs` (64 lines)

**Purpose**: Core utility functions, including echo
**Pattern**: Single function, simple parameter extraction

```csharp
[HydraService("HydraCoreService", Description = "Core utility functions")]
public sealed class HydraCoreService : HydraServiceBase
{
    public HydraCoreService() : base("HydraCoreService", maxInstancesPerSession: 1)
    {
    }

    protected override Task<object?> OnInvokeAsync(
        string functionName,
        Dictionary<string, object?> parameters,
        ServiceInvocationContext context,
        CancellationToken cancellationToken)
    {
        return functionName switch
        {
            "echo" => Task.FromResult<object?>(
                parameters.TryGetValue("args", out var args) && args is JsonObject obj
                    ? obj
                    : new JsonObject()),
            _ => throw new ArgumentException($"Unknown function: {functionName}")
        };
    }

    public override IReadOnlyList<ServiceFunctionDescriptor> GetFunctions()
    {
        return new List<ServiceFunctionDescriptor>
        {
            new ServiceFunctionDescriptor
            {
                Name = "echo",
                Description = "Echo back the arguments",
                IsStreaming = false,
                Parameters = new List<ServiceParameterDescriptor>
                {
                    new ServiceParameterDescriptor
                    {
                        Name = "args",
                        Description = "Arguments to echo",
                        Required = true,
                        ParameterType = typeof(JsonObject)
                    }
                },
                ReturnType = typeof(JsonObject)
            }
        };
    }
}
```

### 8.2 Service with State
```csharp
[HydraService("Counter")]
public sealed class CounterService : HydraServiceBase
{
    private int _count = 0;  // Per-session state (instance is per-session)

    public CounterService() : base("Counter") { }

    protected override Task<object?> OnInvokeAsync(
        string functionName,
        Dictionary<string, object?> parameters,
        ServiceInvocationContext context,
        CancellationToken cancellationToken)
    {
        return functionName switch
        {
            "increment" => Task.FromResult<object?>(++_count),
            "get" => Task.FromResult<object?>(new { count = _count }),
            "reset" => Task.FromResult<object?>(_count = 0),
            _ => throw new NotSupportedException($"Unknown: {functionName}")
        };
    }

    public override IReadOnlyList<ServiceFunctionDescriptor> GetFunctions()
    {
        return new List<ServiceFunctionDescriptor>
        {
            new ServiceFunctionDescriptor { Name = "increment", Description = "Increment counter" },
            new ServiceFunctionDescriptor { Name = "get", Description = "Get current count" },
            new ServiceFunctionDescriptor { Name = "reset", Description = "Reset to zero" }
        };
    }
}
```

**Note**: State persists within a session because each session gets its own instance (via `_instanceRegistry[serviceName][sessionId]`).

### 8.3 Service with Dependencies (HydraSecurityService Pattern)
**File**: `src/Hydra/Hydra.Server/Platform/Core Services/HydraSecurityService.cs` (170 lines)

```csharp
[HydraService(
    "DataService",
    Description = "Data access service",
    MaxInstancesPerSession = 1)]
public sealed class DataService : HydraServiceBase
{
    private readonly IDocumentStore _store;

    // Constructor injection - Lamar will provide IDocumentStore
    public DataService(IDocumentStore store) : base("DataService")
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    protected override async Task<object?> OnInvokeAsync(
        string functionName,
        Dictionary<string, object?> parameters,
        ServiceInvocationContext context,
        CancellationToken cancellationToken)
    {
        return functionName switch
        {
            "getData" => await GetData(parameters, cancellationToken),
            "saveData" => await SaveData(parameters, cancellationToken),
            _ => throw new NotSupportedException($"Unknown: {functionName}")
        };
    }

    private async Task<object?> GetData(
        Dictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        var id = (string)parameters["id"]!;

        using var session = _store.OpenAsyncSession();
        var data = await session.LoadAsync<MyDocument>(id, cancellationToken);

        return data;
    }

    private async Task<object?> SaveData(
        Dictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        var doc = (MyDocument)parameters["document"]!;

        using var session = _store.OpenAsyncSession();
        await session.StoreAsync(doc, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);

        return new { success = true };
    }

    public override IReadOnlyList<ServiceFunctionDescriptor> GetFunctions()
    {
        return new List<ServiceFunctionDescriptor>
        {
            new ServiceFunctionDescriptor { Name = "getData", Description = "Load document by ID" },
            new ServiceFunctionDescriptor { Name = "saveData", Description = "Save document" }
        };
    }
}
```

### 8.4 Service with Caller Context
```csharp
[HydraService("ContextService")]
public sealed class ContextService : HydraServiceBase
{
    [InjectCallerConnection]
    private HydraConnection? _connection;

    [InjectCallerSession]
    private HydraSession? _session;

    [InjectCallerGateway]
    private string? _gatewayName;

    public ContextService() : base("ContextService") { }

    protected override Task<object?> OnInvokeAsync(
        string functionName,
        Dictionary<string, object?> parameters,
        ServiceInvocationContext context,
        CancellationToken cancellationToken)
    {
        return functionName switch
        {
            "whoami" => Task.FromResult<object?>(new
            {
                connectionId = _connection?.ConnectionId,
                sessionId = _session?.Session.SessionId,
                accountId = _connection?.AccountId,
                gateway = _gatewayName,
                // Also available from context parameter:
                contextSessionId = context.SessionId,
                contextGateway = context.GatewayName
            }),
            _ => throw new NotSupportedException($"Unknown: {functionName}")
        };
    }

    public override IReadOnlyList<ServiceFunctionDescriptor> GetFunctions()
    {
        return new List<ServiceFunctionDescriptor>
        {
            new ServiceFunctionDescriptor
            {
                Name = "whoami",
                Description = "Get caller context information"
            }
        };
    }
}
```

## 9. MessageRouter & Dispatch (Temporary)

### 9.1 Current Implementation
**File**: `src/Hydra/Hydra.Server/Platform/Core Services/VisorHydraService/MessageRouter.cs` (217 lines)
**Status**: ✅ Working but temporary (see line 6: "Reflection-based message router")

**How It Works**:
- Reflection-based handler discovery (line 155-175)
- `[MessageHandler("op", "subject")]` attributes on methods (line 204-215)
- Fallback to op-only handler if exact match not found (line 56-65)
- Runtime method invocation via reflection (line 103)

**Discovery** (`MessageRouter.cs:155-175`):
```csharp
private void DiscoverHandlers()
{
    var type = _target.GetType();
    var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

    foreach (var method in methods)
    {
        var attr = method.GetCustomAttribute<MessageHandlerAttribute>();
        if (attr != null)
        {
            var key = BuildKey(attr.Op, attr.Subject);
            _handlers[key] = method;
            // ...
        }
    }
}
```

**Routing** (`MessageRouter.cs:47-128`):
```csharp
public async Task<object?> RouteAsync(
    string op,
    string? subject,
    Dictionary<string, object?> parameters,
    CancellationToken cancellationToken)
{
    var key = BuildKey(op, subject);  // "op" or "op:subject"

    if (!_handlers.TryGetValue(key, out var method))
    {
        // Fallback to op-only handler if subject-specific not found
        if (!string.IsNullOrEmpty(subject) && _handlers.TryGetValue(op, out method))
        {
            // Use op-only handler
        }
        else
        {
            throw new InvalidOperationException($"No handler found for op='{op}', subject='{subject}'");
        }
    }

    // Invoke via reflection...
    var result = method.Invoke(_target, args);
    // Handle async results...
}
```

**Example Usage** (in VisorHydraService - would need to find actual file):
```csharp
// In VisorHydraService.cs
private readonly MessageRouter _router;

public VisorHydraService()
{
    _router = new MessageRouter(this);
}

[MessageHandler("mcp")]
private async Task<HydraEnvelope> HandleMcp(
    Dictionary<string, object?> parameters,
    CancellationToken cancellationToken)
{
    // Route to XMCP client or direct service
    // ...
}

[MessageHandler("echo", "request")]
private async Task<HydraEnvelope> HandleEchoRequest(
    Dictionary<string, object?> parameters,
    CancellationToken cancellationToken)
{
    // Handle echo request
    // ...
}
```

### 9.2 Why It's Temporary
- **Reflection overhead**: Method discovery and invocation via reflection (line 103, 158)
- **Limited routing flexibility**: Simple op/subject string matching
- **Hard to test**: Tight coupling to reflection
- **Doesn't scale**: Complex routing rules require code changes

### 9.3 Replacement Plan
**Gen2 plans are not yet documented** for service dispatch replacement. The system is marked as temporary but specific replacement strategy is TBD.

**Likely improvements** based on industry patterns:
- Code-generated dispatch (avoid reflection)
- More sophisticated routing DSL
- Better testability/debuggability
- Performance optimizations

## 10. Service Stats & Monitoring

### 10.1 ServiceStats
**File**: `src/Hydra/Hydra.Server/Platform/Foundations/Service Foundation/ServiceStats.cs` (156 lines)

**Fields** (lines 12-18):
```csharp
private int _contextInstances;        // Total instances created
private int _activeInstances;         // Active in last 15 seconds
private long _requestsReceived;
private long _requestsServed;
private long _messagesSent;
private long _errors;
private DateTime _lastActivityTime;
```

**Thread-Safe Access** (lines 20-56):
All properties use `lock (_lock)` for thread-safe access.

**15-Second Activity Window** (`ServiceStats.cs:25-36`):
```csharp
public int ActiveInstances
{
    get
    {
        lock (_lock)
        {
            var timeSinceLastActivity = DateTime.UtcNow - _lastActivityTime;
            return timeSinceLastActivity.TotalSeconds <= 15 ? _activeInstances : 0;
        }
    }
}
```

**Access**:
```csharp
var stats = myService.Stats;
Console.WriteLine($"Requests: {stats.RequestsReceived}/{stats.RequestsServed}");
Console.WriteLine($"Errors: {stats.Errors}");
Console.WriteLine($"Active instances: {stats.ActiveInstances}");
Console.WriteLine(stats.ToString());  // Line 145-153: formatted string
```

**Events**:
```csharp
myService.StatsUpdated += (sender, stats) => {
    Console.WriteLine($"Stats updated: {stats}");
};
```

### 10.2 Automatic Tracking
**Base class increments counters** (`HydraServiceBase.cs:120-148`):
- Line 131: `_stats.IncrementRequestsReceived()`
- Line 132: `TouchInstance(sessionId)` - updates activity timestamp
- Line 137: `_stats.IncrementRequestsServed()` on success
- Line 143: `_stats.IncrementErrors()` on exception
- Line 138, 144: `RaiseStatsUpdated()` after changes

**Activity Tracking** (`HydraServiceBase.cs:189-201`):
```csharp
private void TouchInstance(string sessionId)
{
    _instanceActivity[sessionId] = DateTime.UtcNow;
    UpdateInstanceCounts();
}

private void UpdateInstanceCounts()
{
    var now = DateTime.UtcNow;
    var activeCount = _instanceActivity.Count(kvp => now - kvp.Value < TimeSpan.FromSeconds(15));
    _stats.SetInstanceCounts(_instanceActivity.Count, activeCount);
}
```

### 10.3 Cleanup
- Instances inactive for 15+ seconds show as 0 `ActiveInstances` (automatic via property getter)
- Manual cleanup via `HydraServiceBase.RemoveInstance(serviceName, sessionId)` (line 87-97)
- Stats persist for reporting until service is removed

## 11. Known Limitations & Workarounds

### 11.1 Function Attributes Not Used
**Issue**: `HydraServiceFunctionAttribute` and `HydraServiceParamAttribute` exist (`HydraServiceAttribute.cs:56-113`) but aren't processed in Gen1
**Workaround**: Manual dispatch in `OnInvokeAsync` switch statement
**Manual Metadata**: Must maintain `GetFunctions()` in sync with switch cases
**Gen2**: Will likely use these for automatic dispatch

**Example - Attributes exist but ignored**:
```csharp
// These attributes are NOT processed in Gen1 (documentation only)
[HydraServiceFunction("register_resource")]
public Task<bool> RegisterResourceAsync(
    [HydraServiceParam("resource_id")] string id,
    // ...
)

// You MUST still implement manual dispatch:
protected override Task<object?> OnInvokeAsync(...)
{
    return functionName switch
    {
        "register_resource" => await RegisterResourceAsync(...),
        // Manual extraction from parameters dictionary
    };
}
```

### 11.2 Reflection Overhead
**Issue**: `MessageRouter` uses reflection for handler discovery and invocation
**Impact**: Performance overhead on routing
**Workaround**:
- Handlers are discovered once and cached (line 155-175)
- Consider if MessageRouter pattern is needed for your service
- Most services use simple switch-based dispatch instead

**Gen2**: Code generation or compiled dispatch

### 11.3 Manual Function Metadata
**Issue**: `GetFunctions()` must be manually maintained and kept in sync with `OnInvokeAsync` switch cases
**Risk**: Metadata drift (switch has function that GetFunctions doesn't advertise, or vice versa)
**Workaround**:
- Keep them adjacent in code
- Add comments linking them
- Test coverage for both

**Example of keeping in sync**:
```csharp
protected override Task<object?> OnInvokeAsync(...)
{
    return functionName switch
    {
        "doX" => DoX(...),
        "doY" => DoY(...),
        "doZ" => DoZ(...),  // Added new function - don't forget GetFunctions()!
        _ => throw new NotSupportedException(...)
    };
}

public override IReadOnlyList<ServiceFunctionDescriptor> GetFunctions()
{
    return new List<ServiceFunctionDescriptor>
    {
        new ServiceFunctionDescriptor { Name = "doX", ... },
        new ServiceFunctionDescriptor { Name = "doY", ... },
        new ServiceFunctionDescriptor { Name = "doZ", ... },  // Keep in sync!
    };
}
```

**Gen2**: Auto-generation from attributes

### 11.4 Session-Based Instance Limits
**Issue**: `MaxInstancesPerSession` enforced per-session only, no cross-session coordination
**Example**: If `MaxInstancesPerSession = 1`, you can have 1000 sessions each with 1 instance = 1000 total instances
**Workaround**: Use external coordination if you need global instance limits
**Gen2**: Actor model may handle this better

### 11.5 No Dynamic Service Loading
**Issue**: Services discovered only at startup (`ServiceRegistry.cs:38`, called in `App.xaml.cs:63`)
**Workaround**: Restart application required for new services
**Impact**: Cannot hot-reload services without restart
**Gen2**: Hot-reloadable services may be planned

### 11.6 Static Instance Registry
**Issue**: Per-session instances stored in static `ConcurrentDictionary` (`HydraServiceBase.cs:19-21`)
**Implications**:
- Memory leak if sessions aren't cleaned up properly
- No built-in eviction policy beyond manual `RemoveInstance`
- Testing requires `ClearAllInstances()` (line 114-118)

**Workaround**: Ensure proper session lifecycle management

## 12. Testing Your Service

### 12.1 Unit Testing Pattern
**Example from**: `src/Hydra/Hydra.Tests/Unit/Backends/ServiceRegistryAndRouterTests.cs` (lines 296-318)

```csharp
using Xunit;
using Hydra.Server.Backends.Services;

[Trait("Category", "Unit")]
public class MyServiceTests
{
    [Fact]
    public async Task MyService_DoSomething_ReturnsExpected()
    {
        // Arrange
        var service = new MyService();
        var parameters = new Dictionary<string, object?>
        {
            ["input"] = "test"
        };
        var context = new ServiceInvocationContext
        {
            SessionId = "test-session-" + Guid.NewGuid(),
            ConnectionId = "test-connection",
            GatewayName = "test-gateway"
        };

        // Act
        var result = await service.InvokeAsync(
            "doSomething",
            parameters,
            context,
            CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        // More assertions...
    }

    [Fact]
    public async Task MyService_InvalidFunction_ThrowsNotSupported()
    {
        // Arrange
        var service = new MyService();
        var context = new ServiceInvocationContext
        {
            SessionId = "test-session-" + Guid.NewGuid()
        };

        // Act & Assert
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            service.InvokeAsync("unknownFunction", new Dictionary<string, object?>(), context));
    }
}
```

**Key Points**:
- Create service instance directly (no registry needed for unit tests)
- Use unique session IDs to avoid cross-test pollution
- Test both success and error paths
- Test parameter validation

### 12.2 Integration Testing
**Example from**: `ServiceRegistryAndRouterTests.cs` (lines 210-233)

```csharp
[Fact]
public async Task ServiceRouter_RouteAsync_ValidRequest_ReturnsResult()
{
    // Arrange
    var registry = new ServiceRegistry();
    registry.Register("TestService", typeof(TestService));
    var router = new ServiceRouter(registry);

    var context = new ServiceInvocationContext
    {
        SessionId = $"session-{Guid.NewGuid()}",  // Unique session per test
        GatewayName = "TestGateway"
    };

    // Act
    var result = await router.RouteAsync(
        "TestService",
        "TestFunction",
        new Dictionary<string, object?>(),
        context);

    // Assert
    Assert.NotNull(result);
    Assert.Equal("TestResult", result);
}
```

**Key Points**:
- Tests full routing path through `ServiceRouter`
- Uses actual `ServiceRegistry`
- Verifies instance creation and caching
- Use unique session IDs to avoid shared state

### 12.3 Test Organization
**Unit Tests**: Test service logic in isolation
- File: `src/Hydra/Hydra.Tests/Unit/Backends/[ServiceName]Tests.cs`
- Pattern: Direct service instantiation
- Focus: Business logic, parameter handling, error cases

**Integration Tests**: Test via ServiceRouter
- File: `src/Hydra/Hydra.Tests/Integration/Backends/[Scenario]Tests.cs`
- Pattern: Full stack through router
- Focus: Discovery, routing, DI integration

**Test Cleanup**:
```csharp
public void Dispose()
{
    // Clear static instance registry after tests
    HydraServiceBase.ClearAllInstances();
}
```

## 13. Migration Considerations for Gen2

### 13.1 What Will Stay Compatible
Based on current architecture analysis:

- **Business logic in `OnInvokeAsync`**: Core switch-based dispatch likely portable
- **Service attribute metadata**: `[HydraService]` metadata will likely be reused
- **Parameter extraction patterns**: `Dictionary<string, object?>` handling
- **Return value types**: `object?` / `Task<object?>` pattern
- **Constructor DI**: Services using constructor injection
- **ServiceInvocationContext**: Context object structure

### 13.2 What Will Change
Based on temporary/limitation markers:

- **Dispatch mechanism**: No more manual switch statements (attributes will be processed)
- **Discovery mechanism**: More automated (possibly hot-reload capable)
- **Instance management**: Likely replacing static dictionary with actor model or similar
- **MessageRouter**: Reflection-based routing will be replaced
- **Function metadata**: `GetFunctions()` may be auto-generated from attributes

### 13.3 Writing Gen2-Ready Services Now

**Do**:
- ✅ Keep business logic simple and testable in separate methods
- ✅ Use dependency injection for external dependencies (IDocumentStore, etc.)
- ✅ Document function signatures in `GetFunctions()` (will inform migration)
- ✅ Avoid reflection in your service code
- ✅ Use `[HydraServiceFunction]` and `[HydraServiceParam]` attributes (documentation now, functional later)
- ✅ Keep switch cases and `GetFunctions()` metadata in sync
- ✅ Write comprehensive tests for business logic

**Don't**:
- ❌ Rely on `ServiceRegistry` internals (use only public API)
- ❌ Assume session-based singleton behavior in your logic
- ❌ Tightly couple to current infrastructure (keep adapters thin)
- ❌ Use undocumented APIs or private members
- ❌ Store critical state only in memory (use persisted storage for important data)
- ❌ Use `MessageRouter` unless necessary (prefer simple switch dispatch)

**Example - Gen2-Ready Service**:
```csharp
[HydraService("MyService")]
public sealed class MyService : HydraServiceBase
{
    private readonly IDocumentStore _store;

    public MyService(IDocumentStore store) : base("MyService")
    {
        _store = store;
    }

    // Gen1: Manual dispatch (required now)
    protected override Task<object?> OnInvokeAsync(
        string functionName,
        Dictionary<string, object?> parameters,
        ServiceInvocationContext context,
        CancellationToken cancellationToken)
    {
        return functionName switch
        {
            "process" => ProcessAsync(parameters, cancellationToken),
            _ => throw new NotSupportedException($"Unknown: {functionName}")
        };
    }

    // Gen2-Ready: Attributes + separate method (clean business logic)
    [HydraServiceFunction("process", Description = "Process data")]
    private async Task<object?> ProcessAsync(
        [HydraServiceParam("data", Description = "Data to process")]
        Dictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        // Clean business logic - easily testable
        var data = (string)parameters["data"]!;
        var result = await BusinessLogic.Process(data);
        return new { success = true, result };
    }

    // Keep metadata in sync (Gen1 requirement, Gen2 may auto-generate)
    public override IReadOnlyList<ServiceFunctionDescriptor> GetFunctions()
    {
        return new List<ServiceFunctionDescriptor>
        {
            new ServiceFunctionDescriptor
            {
                Name = "process",
                Description = "Process data",
                Parameters = new List<ServiceParameterDescriptor>
                {
                    new ServiceParameterDescriptor
                    {
                        Name = "data",
                        Required = true,
                        ParameterType = typeof(string)
                    }
                }
            }
        };
    }
}
```

### 13.4 Expected Migration Effort
**Estimation** (no official plans documented):
- **Low-effort services** (simple, attribute-decorated): Likely minimal changes
- **Medium-effort services** (DI dependencies, stateful): Update dispatch, test
- **High-effort services** (MessageRouter users, complex state): Significant refactoring

**No automated migration tooling** is currently documented.

## 14. Quick Reference Tables

### 14.1 Service Lifecycle Events
| Event | When | What To Do | File Reference |
|-------|------|------------|----------------|
| Constructor | First call for session | Initialize fields, store DI dependencies | `HydraServiceBase.cs:26-31` |
| OnInvokeAsync | Every call | Process request, dispatch to function | `HydraServiceBase.cs:156-160` |
| OnRestartAsync | Manual restart | Cleanup and reinit | `HydraServiceBase.cs:179` |
| Dispose | Session end | Release resources | Manual via `IDisposable` |

### 14.2 Common Patterns
| Pattern | Use When | Example File |
|---------|----------|--------------|
| Simple dispatch | Few functions, no dependencies | `HydraCoreService.cs` (64 lines) |
| State management | Need per-session state | Counter example (Section 8.2) |
| DI integration | Need external dependencies | `HydraSecurityService.cs` (170 lines) |
| Caller context | Need connection/session info | Section 8.4 example |
| MessageRouter | Complex op/subject routing | `MessageRouter.cs` (217 lines) |

### 14.3 Where Things Live
| Component | Location | Lines |
|-----------|----------|-------|
| Your service | `src/Hydra/Hydra.Server/Platform/Core Services/[Name]/` | - |
| IHydraService | `Platform/Foundations/Service Foundation/IHydraService.cs` | 71 |
| HydraServiceBase | `Platform/Foundations/Service Foundation/HydraServiceBase.cs` | 214 |
| ServiceRegistry | `Platform/Foundations/Service Foundation/ServiceRegistry.cs` | 271 |
| ServiceRouter | `Platform/Foundations/Service Foundation/ServiceRouter.cs` | 151 |
| MessageRouter | `Platform/Core Services/VisorHydraService/MessageRouter.cs` | 217 |
| Attributes | `Platform/Foundations/Service Foundation/HydraServiceAttribute.cs` | 115 |
| ServiceStats | `Platform/Foundations/Service Foundation/ServiceStats.cs` | 156 |
| CallerInjection | `Platform/Infrastructures/DependencyInjection/CallerInjectionAttributes.cs` | 36 |
| HydraServiceActivator | `Platform/Infrastructures/DependencyInjection/HydraServiceActivator.cs` | 129 |
| HydraExecutionContext | `Platform/Execution/HydraExecutionContext.cs` | 65 |
| HydraCompositionRegistry | `Platform/Infrastructures/DependencyInjection/HydraCompositionRegistry.cs` | 72 |
| Bootstrap | `Hydra.Server/App.xaml.cs` | 466 (lines 62-68 for services) |

## 15. Troubleshooting

### Service Not Discovered
**Symptoms**: Service not found when invoked
**Checks**:
- ✅ `[HydraService]` attribute present on class
- ✅ Class inherits from `HydraServiceBase` or implements `IHydraService`
- ✅ `DiscoverServices()` is called in `App.xaml.cs:63`
- ✅ Service is in the executing assembly
- ✅ Check logs for discovery count: "Service discovery complete: N service(s) registered"

**Debug**:
```csharp
// Check if registered
var services = ServiceRegistry.Global.GetAll();
Console.WriteLine($"Registered: {string.Join(", ", services)}");

var type = ServiceRegistry.Global.GetServiceType("MyService");
Console.WriteLine($"Type: {type?.FullName ?? "NOT FOUND"}");
```

### OnInvokeAsync Not Called
**Symptoms**: Function invocation fails or doesn't reach your code
**Checks**:
- ✅ `IsEnabled = true` (check `service.IsEnabled` property)
- ✅ Function name matches exactly (case-sensitive)
- ✅ ServiceRouter is routing to correct service name
- ✅ No exception in base `InvokeAsync` (check logs for "Service disabled")

**Debug**:
```csharp
protected override Task<object?> OnInvokeAsync(...)
{
    Logger.LogInformation($"OnInvokeAsync called: function={functionName}", "MyService");
    // ... rest of implementation
}
```

### Field Injection Not Working
**Symptoms**: `[InjectCaller*]` fields are null
**Checks**:
- ✅ Attributes are correct (`[InjectCallerConnection]`, etc.)
- ✅ Fields are instance fields (not static)
- ✅ `HydraServiceActivator` is configured in DI (it is by default)
- ✅ `HydraExecutionContext` is pushed by `ServiceRouter` (line 88)
- ✅ Service invoked through `ServiceRouter` (not direct instantiation in tests)

**Debug**:
```csharp
protected override Task<object?> OnInvokeAsync(...)
{
    var context = HydraExecutionContext.Current;
    Logger.LogInformation(
        $"Context: {(context != null ? "PRESENT" : "NULL")}, " +
        $"Connection: {(_connection != null ? "INJECTED" : "NULL")}",
        "MyService");
}
```

### State Not Persisting Between Calls
**Understanding**:
- Services are **per-session singletons** - state persists within same session
- Different sessions = different instances = different state
- Instance stored in `_instanceRegistry[serviceName][sessionId]`

**If state not persisting in same session**:
- ✅ Check session ID is actually the same (`context.SessionId`)
- ✅ Verify `MaxInstancesPerSession` is 1 (not -1)
- ✅ Check instance isn't being recreated (debug `GetOrCreateInstance`)

**For cross-session state**:
- ❌ Don't use instance fields
- ✅ Use external storage (RavenDB, etc.)

```csharp
// WRONG - different sessions won't see this
private int _count = 0;

// RIGHT - persists across sessions
private readonly IDocumentStore _store;
private async Task<int> GetCount(string key)
{
    using var session = _store.OpenAsyncSession();
    var doc = await session.LoadAsync<CountDocument>(key);
    return doc?.Count ?? 0;
}
```

### Stats Not Updating
**Symptoms**: `service.Stats` shows zeros or stale data
**Checks**:
- ✅ Base class `InvokeAsync` is being called (don't override it)
- ✅ Calls are actually reaching the service (check logs)
- ✅ Subscribe to `StatsUpdated` event to see updates
- ✅ Active instances show 0 if no activity in last 15 seconds (expected)

**Debug**:
```csharp
myService.StatsUpdated += (sender, stats) => {
    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Stats updated: {stats}");
};
```

### Service Cleanup / Memory Leaks
**Symptoms**: Memory usage grows over time, services not cleaned up
**Checks**:
- ✅ Sessions are being closed properly (`SessionManager.CloseAsync`)
- ✅ `RemoveInstance` is called when session ends
- ✅ Verify cleanup in shutdown (`App.xaml.cs:409-437`)

**Manual Cleanup**:
```csharp
// When session ends
HydraServiceBase.RemoveInstance("MyService", sessionId);

// For testing
HydraServiceBase.ClearAllInstances();
```

---

## Appendix A: Complete File Inventory

### Core Service Foundation (Platform/Foundations/Service Foundation/)
1. `IHydraService.cs` - 71 lines - Core interface
2. `HydraServiceBase.cs` - 214 lines - Base implementation with instance management
3. `ServiceInvocationContext.cs` - 43 lines - Context passed to services
4. `ServiceRegistry.cs` - 271 lines - Discovery and registration
5. `ServiceRouter.cs` - 151 lines - Routing and invocation
6. `HydraServiceAttribute.cs` - 115 lines - Service, function, param attributes
7. `ServiceFunctionDescriptor.cs` - 64 lines - Function metadata classes
8. `ServiceStats.cs` - 156 lines - Thread-safe statistics
9. `ServiceBootManager.cs` - (referenced but not read) - Auto-start logic

### DI Integration (Platform/Infrastructures/DependencyInjection/)
10. `HydraCompositionRegistry.cs` - 72 lines - Lamar service registration
11. `CallerInjectionAttributes.cs` - 36 lines - Field injection attributes
12. `HydraServiceActivator.cs` - 129 lines - Field injection mechanism

### Execution Context (Platform/Execution/)
13. `HydraExecutionContext.cs` - 65 lines - AsyncLocal context flow

### Example Services (Platform/Core Services/)
14. `HydraCoreService.cs` - 64 lines - Simplest example (echo)
15. `HydraSecurityService.cs` - 170 lines - DI integration, hosted service
16. `VisorHydraService/MessageRouter.cs` - 217 lines - Reflection-based routing

### Bootstrap
17. `App.xaml.cs` - 466 lines - Application startup (lines 62-68 for service init)

### Tests
18. `Hydra.Tests/Unit/Backends/ServiceRegistryAndRouterTests.cs` - 365 lines - Unit/integration tests

---

## Appendix B: Invocation Trace with Line Numbers

Complete call trace through the system:

```
1. Gateway receives message
   └─> Creates HydraConnection, HydraSession

2. ServiceRouter.InvokeAsync(connection, session, serviceName, functionName, args)
   File: ServiceRouter.cs, Lines: 40-100

   2a. Convert JToken args → Dictionary<string, object?>
       Lines: 60-68
       Code: foreach (var prop in ((JObject)args).Properties())
             parameters[prop.Name] = prop.Value.ToObject<object>();

   2b. Create ServiceInvocationContext
       Lines: 71-76
       Code: var context = new ServiceInvocationContext
             {
                 SessionId = session.Session.SessionId,
                 GatewayName = "Hydra Gateway",
                 ConnectionId = connection.ConnectionId
             };

   2c. Create per-call DI scope
       Line: 79
       Code: using var scope = _scopeFactory?.CreateForCall(session, connection);

3. ServiceRegistry.GetOrCreateInstance(serviceName, sessionId)
   File: ServiceRegistry.cs, Lines: 157-227

   3a. Get service type from registry
       Lines: 173-177
       Code: var type = GetServiceType(serviceName);
             if (type == null) throw new InvalidOperationException(...);

   3b. Use reflection to call HydraServiceBase.GetOrCreateInstance<T>
       Lines: 180-188
       Code: var method = typeof(HydraServiceBase).GetMethod("GetOrCreateInstance", ...);
             var genericMethod = method.MakeGenericMethod(type);

   3c. Create factory delegate
       Lines: 195-223
       - Tries Lamar container first (lines 198-202)
       - Falls back to ActivatorUtilities (lines 206-207)
       - Last resort: Activator.CreateInstance (line 210)

4. HydraServiceBase.GetOrCreateInstance<T>(sessionId, factory)
   File: HydraServiceBase.cs, Lines: 45-65

   4a. Check instance registry
       Lines: 50-53
       Code: if (sessionInstances.TryGetValue(sessionId, out var existingInstance))
                 return (T)existingInstance;

   4b. Create new instance via factory
       Line: 55
       Code: var newInstance = factory();

   4c. Cache in registry
       Line: 61-62
       Code: sessionInstances[sessionId] = newInstance;
             newInstance._stats.AddInstance();

5. Inject service into scope
   File: ServiceRouter.cs, Line: 85
   Code: scope?.Inject(service);

   └─> HydraServiceActivator.InjectFields(instance, descriptor)
       File: HydraServiceActivator.cs, Lines: 90-119

       5a. Get current execution context
           Line: 92
           Code: var context = HydraExecutionContext.Current;

       5b. Inject connection fields
           Lines: 95-98
           Code: foreach (var field in descriptor.ConnectionFields)
                     field.SetValue(instance, context?.Connection);

       5c. Inject session fields (lines 101-104)
       5d. Inject gateway fields (lines 107-110)
       5e. Inject account fields (lines 113-118)

6. Push HydraExecutionContext
   File: ServiceRouter.cs, Line: 88
   Code: using (HydraExecutionContext.Push(connection, session, context.GatewayName))

   └─> HydraExecutionContext.Push(...)
       File: HydraExecutionContext.cs, Lines: 36-43
       Code: var newContext = new HydraExecutionContext(connection, session, gatewayName);
             _current.Value = newContext;
             return new ContextScope(previous);

7. Service.InvokeAsync(functionName, parameters, context, cancellationToken)
   File: ServiceRouter.cs, Line: 90
   Code: var result = await service.InvokeAsync(functionName, parameters, context, default);

8. HydraServiceBase.InvokeAsync (base implementation)
   File: HydraServiceBase.cs, Lines: 120-148

   8a. Check if enabled
       Lines: 126-129
       Code: if (!IsEnabled)
                 throw new InvalidOperationException($"Service '{Name}' is disabled.");

   8b. Increment stats and touch instance
       Lines: 131-132
       Code: _stats.IncrementRequestsReceived();
             TouchInstance(context.SessionId);

   8c. Call OnInvokeAsync() ← YOUR CODE EXECUTES HERE
       Line: 136
       Code: var result = await OnInvokeAsync(functionName, parameters, context, cancellationToken);

   8d. Increment stats on success
       Lines: 137-138
       Code: _stats.IncrementRequestsServed();
             RaiseStatsUpdated();

   8e. Increment stats on error
       Lines: 141-146
       Code: catch (Exception ex)
             {
                 _stats.IncrementErrors();
                 RaiseStatsUpdated();
                 Logger.LogError(ex, ...);
                 throw;
             }

9. Return result
   File: ServiceRouter.cs, Line: 91-92
   Code: Logger.LogDebug($"Invocation completed: Service='{serviceName}'", ...);
         return result;

10. Dispose execution context (automatic)
    File: ServiceRouter.cs, Line: 88 (using statement ends)

    └─> ContextScope.Dispose()
        File: HydraExecutionContext.cs, Lines: 56-62
        Code: _current.Value = _previousContext;

11. Dispose DI scope (automatic)
    File: ServiceRouter.cs, Line: 79 (using statement ends)

12. Result returned to gateway
```

---

**End of Guide**

This document provides a complete practical reference for working with HYDRA Gen1 services. Remember: this architecture is temporary and will be replaced in Gen2, but following these patterns will minimize migration effort.
