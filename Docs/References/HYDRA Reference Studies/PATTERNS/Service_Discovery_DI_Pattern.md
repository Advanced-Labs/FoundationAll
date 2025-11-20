# Service Discovery & DI Pattern - Attribute-Based Registration with Scoped Activation

**Pattern Category:** Framework / Infrastructure
**Complexity:** Medium-High
**Reusability:** High - applicable to plugin-based .NET systems

---

## Pattern Intent

Provide automatic service discovery and registration with:
- Attribute-based metadata for declarative configuration
- Reflection-based runtime discovery (no manual registration)
- Per-session instance caching for stateful services
- Scoped DI activation per invocation
- Field injection via AsyncLocal for caller context

## Problem Being Solved

When building an extensible service platform:
- Manual registration is error-prone (forget to register → runtime failure)
- Services need session-scoped state (one instance per user session)
- Per-call dependencies need scoped lifetimes (DB sessions, etc.)
- Services need caller context (connection, user, session) without explicit parameters
- Adding new services should require minimal boilerplate

## HYDRA Implementation

### Core Interfaces & Base Classes

**File:** `Platform/Foundations/Service Foundation/IHydraService.cs` (71 lines)

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

**File:** `Platform/Foundations/Service Foundation/HydraServiceBase.cs` (214 lines)

```csharp
public abstract class HydraServiceBase : IHydraService
{
    // Per-session instance registry (static)
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, HydraServiceBase>>
        _instanceRegistry = new();

    protected HydraServiceBase(string name, int maxInstancesPerSession = 1)
    {
        Name = name;
        MaxInstancesPerSession = maxInstancesPerSession;
    }

    // Static factory: Get or create instance for session
    public static T GetOrCreateInstance<T>(string sessionId, Func<T> factory) where T : HydraServiceBase
    {
        var serviceName = typeof(T).Name;
        var sessionInstances = _instanceRegistry.GetOrAdd(serviceName, _ => new());

        if (sessionInstances.TryGetValue(sessionId, out var existingInstance))
            return (T)existingInstance;

        var newInstance = factory();
        sessionInstances[sessionId] = newInstance;
        newInstance._stats.AddInstance();

        return newInstance;
    }

    // Template method: derived classes implement business logic
    protected abstract Task<object?> OnInvokeAsync(
        string functionName,
        Dictionary<string, object?> parameters,
        ServiceInvocationContext context,
        CancellationToken cancellationToken);

    // Public invoke: handles stats, enabled checks, error tracking
    public async Task<object?> InvokeAsync(
        string functionName,
        Dictionary<string, object?> parameters,
        ServiceInvocationContext context,
        CancellationToken cancellationToken)
    {
        if (!IsEnabled)
            throw new InvalidOperationException($"Service '{Name}' is disabled.");

        _stats.IncrementRequestsReceived();
        TouchInstance(context.SessionId);

        try
        {
            var result = await OnInvokeAsync(functionName, parameters, context, cancellationToken);
            _stats.IncrementRequestsServed();
            RaiseStatsUpdated();
            return result;
        }
        catch
        {
            _stats.IncrementErrors();
            RaiseStatsUpdated();
            throw;
        }
    }

    public abstract IReadOnlyList<ServiceFunctionDescriptor> GetFunctions();
}
```

**Key Concepts:**
- **Static registry:** Per-session instances cached in static `ConcurrentDictionary`
- **Template method:** `OnInvokeAsync` for business logic, base class handles infrastructure
- **Activity tracking:** 15-second timeout for "active" instance detection
- **Stats:** Automatic request/reply/error tracking

---

### Attribute-Based Discovery

**File:** `Platform/Foundations/Service Foundation/HydraServiceAttribute.cs` (115 lines)

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class HydraServiceAttribute : Attribute
{
    public HydraServiceAttribute(string name)
    {
        Name = name;
    }

    public string Name { get; }
    public string? Description { get; set; }
    public int MaxInstancesPerSession { get; set; } = 1;
    public int BootPriority { get; set; } = 5000;  // Lower = starts earlier
    public bool AutoStart { get; set; } = false;
}

// Method-level (not currently used in Gen1, but designed for Gen2)
[AttributeUsage(AttributeTargets.Method)]
public sealed class HydraServiceFunctionAttribute : Attribute
{
    public string Name { get; set; }
    public string? Description { get; set; }
    public bool IsStreaming { get; set; }
}

// Parameter-level (not currently used in Gen1)
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class HydraServiceParamAttribute : Attribute
{
    public string Name { get; set; }
    public string? Description { get; set; }
    public bool Required { get; set; }
}
```

**Example Service:**
```csharp
[HydraService("EchoService", Description = "Echoes input back to caller")]
public sealed class EchoService : HydraServiceBase
{
    public EchoService() : base("EchoService", maxInstancesPerSession: 1) { }

    protected override Task<object?> OnInvokeAsync(
        string functionName,
        Dictionary<string, object?> parameters,
        ServiceInvocationContext context,
        CancellationToken cancellationToken)
    {
        return functionName switch
        {
            "echo" => Task.FromResult<object?>(parameters),
            _ => throw new NotSupportedException($"Unknown function: {functionName}")
        };
    }

    public override IReadOnlyList<ServiceFunctionDescriptor> GetFunctions()
    {
        return new List<ServiceFunctionDescriptor>
        {
            new ServiceFunctionDescriptor
            {
                Name = "echo",
                Description = "Echoes parameters back",
                IsStreaming = false
            }
        };
    }
}
```

---

### Service Registry (Discovery & Lifecycle)

**File:** `Platform/Foundations/Service Foundation/ServiceRegistry.cs` (271 lines)

```csharp
public sealed class ServiceRegistry
{
    private readonly Dictionary<string, Type> _serviceTypes = new();
    private readonly Dictionary<string, HydraServiceAttribute> _metadata = new();

    public static ServiceRegistry Global { get; } = new ServiceRegistry();

    /// <summary>
    /// Scans assembly for [HydraService] attributes and registers automatically.
    /// </summary>
    public void DiscoverServices()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var types = assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<HydraServiceAttribute>() != null)
            .Where(t => typeof(IHydraService).IsAssignableFrom(t));

        foreach (var type in types)
        {
            var attr = type.GetCustomAttribute<HydraServiceAttribute>()!;
            _serviceTypes[attr.Name] = type;
            _metadata[attr.Name] = attr;
        }

        Logger.LogInformation($"Service discovery complete: {_serviceTypes.Count} service(s) registered", "ServiceRegistry");
    }

    /// <summary>
    /// Gets or creates service instance for specific session.
    /// Uses reflection to call HydraServiceBase.GetOrCreateInstance<T>.
    /// </summary>
    public IHydraService GetOrCreateInstance(string serviceName, string sessionId)
    {
        var type = GetServiceType(serviceName);
        if (type == null)
            throw new InvalidOperationException($"Service '{serviceName}' not registered");

        // Use reflection to call static generic method: HydraServiceBase.GetOrCreateInstance<T>
        var method = typeof(HydraServiceBase).GetMethod(
            "GetOrCreateInstance",
            BindingFlags.Public | BindingFlags.Static);

        var genericMethod = method!.MakeGenericMethod(type);

        // Create factory delegate for service instantiation
        Func<HydraServiceBase> factory = () =>
        {
            // Try Lamar container first
            if (_serviceProvider != null)
            {
                try
                {
                    var instance = _serviceProvider.GetService(type) as HydraServiceBase;
                    if (instance != null) return instance;
                }
                catch { /* Fallback to activator */ }
            }

            // Fallback: ActivatorUtilities with DI
            return (HydraServiceBase)ActivatorUtilities.CreateInstance(_serviceProvider!, type);
        };

        // Invoke: GetOrCreateInstance<T>(sessionId, factory)
        return (IHydraService)genericMethod.Invoke(null, new object[] { sessionId, factory })!;
    }

    public Type? GetServiceType(string name) => _serviceTypes.GetValueOrDefault(name);
    public IReadOnlyList<string> GetAllServiceNames() => _serviceTypes.Keys.ToList();
}
```

**Discovery Flow:**
1. `DiscoverServices()` scans assembly for `[HydraService]` attributes
2. Registers service type by name
3. At invocation, `GetOrCreateInstance()` creates instance via DI factory
4. Instance cached per session in static registry

---

### Service Router (Invocation Orchestration)

**File:** `Platform/Foundations/Service Foundation/ServiceRouter.cs` (151 lines)

```csharp
public sealed class ServiceRouter
{
    private readonly ServiceRegistry _serviceRegistry;
    private readonly IHydraScopeFactory? _scopeFactory;

    public async Task<object?> InvokeAsync(
        HydraConnection connection,
        HydraSession session,
        string serviceName,
        string functionName,
        JToken? args,
        CancellationToken cancellationToken = default)
    {
        // 1. Convert JToken args → Dictionary<string, object?>
        var parameters = new Dictionary<string, object?>();
        if (args != null && args.Type == JTokenType.Object)
        {
            foreach (var prop in ((JObject)args).Properties())
            {
                parameters[prop.Name] = prop.Value.ToObject<object>();
            }
        }

        // 2. Create invocation context
        var context = new ServiceInvocationContext
        {
            SessionId = session.Session.SessionId,
            ConnectionId = connection.ConnectionId,
            GatewayName = "Service Gateway",
            Runtime = session
        };

        // 3. Create per-call DI scope
        using var scope = _scopeFactory?.CreateForCall(session, connection);

        // 4. Get or create service instance (per-session singleton)
        var service = _serviceRegistry.GetOrCreateInstance(serviceName, context.SessionId);

        // 5. Inject caller context into service fields
        scope?.Inject(service);

        // 6. Push execution context (AsyncLocal)
        using (HydraExecutionContext.Push(connection, session, context.GatewayName))
        {
            // 7. Invoke service
            var result = await service.InvokeAsync(functionName, parameters, context, cancellationToken);
            return result;
        }
        // Execution context auto-popped via Dispose
    }
}
```

**Orchestration Steps:**
1. **Parameter conversion:** JToken → `Dictionary<string, object?>`
2. **Context creation:** `ServiceInvocationContext` with session/connection IDs
3. **DI scope creation:** Per-call scope for DB sessions, etc.
4. **Instance retrieval:** Per-session singleton from registry
5. **Field injection:** Inject caller context into service fields
6. **Context push:** `AsyncLocal<ExecutionContext>` for cross-cutting access
7. **Invocation:** Call service with unwrapped parameters

---

### Field Injection via AsyncLocal

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

**File:** `Platform/Execution/HydraExecutionContext.cs` (65 lines)

```csharp
public sealed class HydraExecutionContext
{
    private static readonly AsyncLocal<HydraExecutionContext?> _current = new();

    public HydraConnection? Connection { get; }
    public HydraSession? Session { get; }
    public string? GatewayName { get; }

    public static HydraExecutionContext? Current => _current.Value;

    public static IDisposable Push(HydraConnection? connection, HydraSession? session, string? gatewayName)
    {
        var previous = _current.Value;
        _current.Value = new HydraExecutionContext(connection, session, gatewayName);
        return new ContextScope(previous);  // Restore on dispose
    }

    private sealed class ContextScope : IDisposable
    {
        private readonly HydraExecutionContext? _previousContext;

        public ContextScope(HydraExecutionContext? previous) => _previousContext = previous;

        public void Dispose() => _current.Value = _previousContext;
    }
}
```

**File:** `Platform/Infrastructures/DependencyInjection/HydraServiceActivator.cs` (129 lines)

```csharp
public sealed class HydraServiceActivator
{
    private static readonly ConcurrentDictionary<Type, InjectionDescriptor> _descriptorCache = new();

    public void InjectFields(object instance, InjectionDescriptor descriptor)
    {
        var context = HydraExecutionContext.Current;
        if (context == null) return;

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

**Example Service with Field Injection:**
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

    protected override Task<object?> OnInvokeAsync(...)
    {
        // Fields automatically populated from HydraExecutionContext.Current
        var connectionId = _connection?.ConnectionId;
        var sessionId = _session?.Session.SessionId;
        var gateway = _gatewayName;

        return Task.FromResult<object?>(new
        {
            connectionId,
            sessionId,
            gateway
        });
    }
}
```

---

### DI Integration (Lamar)

**File:** `Platform/Infrastructures/DependencyInjection/HydraCompositionRegistry.cs` (72 lines)

```csharp
public static class HydraCompositionRegistry
{
    public static IServiceCollection ConfigureHydraServices(this IServiceCollection services)
    {
        // Singleton: RavenDB document store
        services.AddSingleton<IDocumentStore>(sp =>
        {
            var store = new DocumentStore
            {
                Urls = new[] { "http://localhost:8080" },
                Database = "Hydra"
            };
            store.Initialize();
            return store;
        });

        // Scoped: RavenDB session (per-call)
        services.AddScoped<IAsyncDocumentSession>(sp =>
        {
            var store = sp.GetRequiredService<IDocumentStore>();
            return store.OpenAsyncSession();
        });

        // Singleton: Storage system
        services.AddSingleton<HydraStoreSystemV1>();

        // Singleton: Session manager
        services.AddSingleton<ISessionManager, HydraSessionManager>();

        // Singleton: Scope factory
        services.AddSingleton<IHydraScopeFactory, HydraScopeFactory>();

        // ... other infrastructure

        return services;
    }
}
```

**Bootstrap in App.xaml.cs:**
```csharp
protected override async void OnStartup(StartupEventArgs e)
{
    // Build DI container (Lamar)
    _host = Host.CreateDefaultBuilder()
        .UseLamar((context, registry) =>
        {
            registry.ConfigureHydraServices();
        })
        .Build();

    // Initialize service registry with DI provider
    ServiceRegistry.Global.Initialize(_host.Services);

    // Discover services via reflection
    ServiceRegistry.Global.DiscoverServices();

    // Auto-start services with AutoStart = true
    await ServiceBootManager.AutoStartHostedServicesAsync(
        ServiceRegistry.Global,
        _host.Services,
        CancellationToken.None);

    // Start gateways
    await _gatewayRegistry.StartAllAsync(CancellationToken.None);
}
```

---

## Pattern Structure

### Architecture Diagram

```
┌──────────────────────────────────────────────────────────┐
│              ServiceRegistry (Singleton)                  │
│  - DiscoverServices() scans [HydraService]               │
│  - Stores service types + metadata                       │
│  - GetOrCreateInstance() uses reflection + DI            │
└────────────┬─────────────────────────────────────────────┘
             │
             │ Manages
             ↓
┌──────────────────────────────────────────────────────────┐
│         HydraServiceBase._instanceRegistry                │
│  Static ConcurrentDictionary<ServiceName, Dict<Session>> │
│  - Per-session singleton instances                       │
│  - GetOrCreateInstance<T>(sessionId, factory)            │
└────────────┬─────────────────────────────────────────────┘
             │
             │ Creates
             ↓
┌──────────────────────────────────────────────────────────┐
│               Concrete Service Instances                  │
│  [HydraService("Echo")] EchoService : HydraServiceBase   │
│  [HydraService("Security")] SecurityService : ...        │
└──────────────────────────────────────────────────────────┘
```

### Invocation Flow

```
1. Gateway receives request
   ServiceRouter.InvokeAsync(connection, session, "EchoService", "echo", args)
     ↓
2. Create per-call DI scope
   using var scope = _scopeFactory.CreateForCall(session, connection)
     ↓
3. Get or create service instance (per-session)
   var service = ServiceRegistry.Global.GetOrCreateInstance("EchoService", sessionId)
     ├─> Reflection: HydraServiceBase.GetOrCreateInstance<EchoService>()
     ├─> Factory: ActivatorUtilities.CreateInstance(_serviceProvider, typeof(EchoService))
     ├─> DI injects: IDocumentStore, ILogger, etc. into constructor
     └─> Cache in _instanceRegistry["EchoService"][sessionId]
     ↓
4. Inject caller context into service fields
   scope.Inject(service)
     ├─> HydraServiceActivator.InjectFields(service, descriptor)
     ├─> descriptor cached per service type (reflection once)
     └─> Set fields marked [InjectCallerConnection], [InjectCallerSession], etc.
     ↓
5. Push AsyncLocal execution context
   using (HydraExecutionContext.Push(connection, session, gatewayName))
     ↓
6. Invoke service
   var result = await service.InvokeAsync("echo", parameters, context, cancellationToken)
     ├─> Base class: Check IsEnabled, increment stats
     ├─> Call: OnInvokeAsync("echo", parameters, context, cancellationToken)
     └─> Service business logic executes
     ↓
7. Return result
   ← result
     ↓
8. Auto-cleanup (using statements)
   - HydraExecutionContext.Pop() (restore previous context)
   - DI scope.Dispose() (release scoped dependencies)
```

---

## Key Design Decisions

### 1. Static Per-Session Instance Registry
**Decision:** Use static `ConcurrentDictionary` for per-session service instances

**Rationale:**
- Services often need per-session state (conversation history, user preferences)
- Static registry survives DI scope disposal
- Thread-safe for concurrent access across gateways

**Tradeoff:** Manual cleanup required (memory leak if sessions not removed)

**Implementation:** Lines 19-21 of HydraServiceBase.cs

### 2. Reflection-Based Discovery
**Decision:** Scan assembly for `[HydraService]` attributes at startup

**Rationale:**
- No manual registration (developer adds attribute, done)
- Single source of truth (attribute on class)
- Metadata available for UI dashboards (service list, descriptions)

**Tradeoff:** Reflection overhead at startup (acceptable, runs once)

**Implementation:** Lines 38-54 of ServiceRegistry.cs

### 3. DI Factory + Per-Session Caching
**Decision:** Use DI to create instances, but cache per session

**Rationale:**
- Constructor injection for dependencies (IDocumentStore, ILogger)
- Per-session singleton for stateful services
- Best of both worlds: DI composition + session isolation

**Implementation:** Lines 157-227 of ServiceRegistry.cs

### 4. Field Injection via AsyncLocal
**Decision:** Use `AsyncLocal<ExecutionContext>` + field injection for caller context

**Rationale:**
- Avoids polluting method signatures with context parameters
- Flows automatically across async boundaries
- Service methods stay focused on business logic

**Tradeoff:** Less explicit than parameter injection, harder to trace

**Implementation:** HydraExecutionContext.cs:1-65, HydraServiceActivator.cs:90-119

### 5. Two-Phase Activation: Constructor + Field Injection
**Decision:** Constructor DI for infrastructure, field injection for caller context

**Rationale:**
- Constructor: long-lived dependencies (DB, caches) injected once
- Fields: per-call context (connection, session) injected before each invocation
- Separation: service instance reused, but caller context changes per call

**Why Not Constructor for Context?** Service instances are per-session singletons. Connection changes per request within a session.

---

## Reproducing This Pattern in Other .NET Projects

### Step 1: Define Service Attribute

```csharp
[AttributeUsage(AttributeTargets.Class)]
public sealed class MyServiceAttribute : Attribute
{
    public MyServiceAttribute(string name)
    {
        Name = name;
    }

    public string Name { get; }
    public string? Description { get; set; }
}
```

### Step 2: Create Service Base Class

```csharp
public abstract class ServiceBase
{
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ServiceBase>>
        _instances = new();

    public string Name { get; }

    protected ServiceBase(string name)
    {
        Name = name;
    }

    public static T GetOrCreateInstance<T>(string sessionId, Func<T> factory) where T : ServiceBase
    {
        var serviceName = typeof(T).Name;
        var sessionInstances = _instances.GetOrAdd(serviceName, _ => new());

        if (sessionInstances.TryGetValue(sessionId, out var existing))
            return (T)existing;

        var newInstance = factory();
        sessionInstances[sessionId] = newInstance;
        return newInstance;
    }

    public abstract Task<object?> HandleAsync(
        string operation,
        Dictionary<string, object?> parameters,
        CancellationToken cancellationToken);
}
```

### Step 3: Implement Service Registry

```csharp
public sealed class ServiceRegistry
{
    private readonly Dictionary<string, Type> _serviceTypes = new();
    private IServiceProvider? _serviceProvider;

    public void DiscoverServices()
    {
        var types = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => t.GetCustomAttribute<MyServiceAttribute>() != null)
            .Where(t => typeof(ServiceBase).IsAssignableFrom(t));

        foreach (var type in types)
        {
            var attr = type.GetCustomAttribute<MyServiceAttribute>()!;
            _serviceTypes[attr.Name] = type;
        }
    }

    public ServiceBase GetOrCreateInstance(string serviceName, string sessionId)
    {
        var type = _serviceTypes[serviceName];

        var method = typeof(ServiceBase).GetMethod("GetOrCreateInstance", BindingFlags.Public | BindingFlags.Static)!;
        var genericMethod = method.MakeGenericMethod(type);

        Func<ServiceBase> factory = () => (ServiceBase)ActivatorUtilities.CreateInstance(_serviceProvider!, type);

        return (ServiceBase)genericMethod.Invoke(null, new object[] { sessionId, factory })!;
    }

    public void Initialize(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }
}
```

### Step 4: Create Example Service

```csharp
[MyService("Calculator", Description = "Performs calculations")]
public sealed class CalculatorService : ServiceBase
{
    private readonly ILogger<CalculatorService> _logger;
    private int _operationCount = 0;  // Per-session state

    // Constructor DI
    public CalculatorService(ILogger<CalculatorService> logger) : base("Calculator")
    {
        _logger = logger;
    }

    public override Task<object?> HandleAsync(
        string operation,
        Dictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        _operationCount++;  // Track per-session usage

        return operation switch
        {
            "add" => Task.FromResult<object?>(Add(parameters)),
            "multiply" => Task.FromResult<object?>(Multiply(parameters)),
            _ => throw new NotSupportedException()
        };
    }

    private object Add(Dictionary<string, object?> parameters)
    {
        var a = Convert.ToInt32(parameters["a"]);
        var b = Convert.ToInt32(parameters["b"]);
        _logger.LogInformation($"Add operation #{_operationCount}: {a} + {b}");
        return new { result = a + b, operationCount = _operationCount };
    }

    private object Multiply(Dictionary<string, object?> parameters)
    {
        var a = Convert.ToInt32(parameters["a"]);
        var b = Convert.ToInt32(parameters["b"]);
        _logger.LogInformation($"Multiply operation #{_operationCount}: {a} * {b}");
        return new { result = a * b, operationCount = _operationCount };
    }
}
```

### Step 5: Bootstrap

```csharp
var services = new ServiceCollection();
services.AddLogging();
// Add other dependencies...

var serviceProvider = services.BuildServiceProvider();

var registry = new ServiceRegistry();
registry.Initialize(serviceProvider);
registry.DiscoverServices();  // Scans for [MyService]

// Usage
var sessionId = "user-123";
var calculator = registry.GetOrCreateInstance("Calculator", sessionId);

var result = await calculator.HandleAsync("add", new Dictionary<string, object?>
{
    ["a"] = 10,
    ["b"] = 20
}, CancellationToken.None);

Console.WriteLine(result);  // { result = 30, operationCount = 1 }

// Same session, instance reused
var result2 = await calculator.HandleAsync("multiply", new Dictionary<string, object?>
{
    ["a"] = 5,
    ["b"] = 6
}, CancellationToken.None);

Console.WriteLine(result2);  // { result = 30, operationCount = 2 }  ← State preserved
```

---

## Tradeoffs & Constraints

### Advantages
✅ Zero-boilerplate service registration (just add attribute)
✅ Per-session state isolation (each user gets own instance)
✅ Full DI support for dependencies
✅ Field injection for caller context (clean method signatures)
✅ Automatic stats tracking

### Limitations
⚠️ Reflection overhead at startup (discovery) and per-invocation (generic method call)
⚠️ Static registry requires manual cleanup (memory leak potential)
⚠️ Field injection less explicit than parameters
⚠️ Per-session instances increase memory footprint vs stateless services

### When NOT to Use This Pattern
❌ Stateless services (use transient DI instead)
❌ Extreme performance requirements (reflection overhead)
❌ Single-user systems (per-session caching unnecessary)
❌ Simple function dispatch (overkill for 2-3 services)

---

## Related Patterns

- **Template Method:** Base class controls invocation flow, derived classes implement logic
- **Factory Pattern:** `GetOrCreateInstance` factory with DI composition
- **Registry Pattern:** Central service discovery
- **Ambient Context (via AsyncLocal):** Caller context flows without explicit parameters

---

## Gen2 Evolution Notes

**Current (Gen1):** Reflection-based, static registry, simple routing

**Future (Gen2):**
- Source generators replace reflection (compile-time service discovery)
- Akka.NET actors replace static registry (distributed, location-transparent)
- Attribute-based routing replaces manual switch statements
- Hot-reloadable services

**Migration Strategy:**
- Attributes remain stable (`[HydraService]` evolves to `[AkkaService]`)
- Business logic in `OnInvokeAsync` mostly portable
- Static registry replaced by Akka receptionist pattern

---

**Last Updated:** 2025-11-10
**Pattern Stability:** Medium - Gen2 will replace reflection with source generation
**Code References:** HydraServiceBase.cs:1-214, ServiceRegistry.cs:1-271, ServiceRouter.cs:1-151
