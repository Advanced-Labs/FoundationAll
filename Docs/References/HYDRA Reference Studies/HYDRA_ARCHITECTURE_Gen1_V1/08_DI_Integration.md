# 8. DI Integration (Lamar)

## 8.1 HydraCompositionRegistry

**File:** `/src/Hydra/Hydra.Server/Platform/Infrastructures/DependencyInjection/HydraCompositionRegistry.cs` (74 lines)
**Technology:** Lamar (StructureMap successor)

### Registered Services by Lifetime

#### SINGLETON Services

**IDocumentStore (Lines 22-38):**
```csharp
For<IDocumentStore>().Use(ctx =>
{
    var config = ctx.GetInstance<IConfiguration>();
    var urls = config.GetSection("RavenDB:Urls").Get<string[]>() ?? new[] { "http://localhost:8080" };
    var database = config.GetValue<string>("RavenDB:Database") ?? "HydraStore";

    var store = new DocumentStore
    {
        Urls = urls,
        Database = database
    };

    store.Initialize();
    return store;
}).Singleton();
```

**Other Singletons (Lines 45-61):**
```csharp
For<HydraStoreSystemV1>().Use<HydraStoreSystemV1>().Singleton();
For<IHydraStoreSystem>().Use(ctx => ctx.GetInstance<HydraStoreSystemV1>()).Singleton();
For<ISessionManager>().Use<HydraSessionManager>().Singleton();
For<IHydraScopeFactory>().Use<HydraScopeFactory>().Singleton();
For<IHydraStoreDriver>().Use<RavenHydraSecurityStoreDriver>().Singleton();
For<RavenHydraSecurityStoreDriver>().Use<RavenHydraSecurityStoreDriver>().Singleton();
For<ISessionRepository>().Use<RavenSessionRepository>().Singleton();
For<IDeterministicWriter>().Use<VisorDeterministicWriter>().Singleton();
For<ChatGPTVisor>().Use(ctx => /* factory lambda */).Singleton();
```

#### SCOPED Services

**IAsyncDocumentSession (Lines 41-42):**
```csharp
For<IAsyncDocumentSession>().Use(ctx =>
    ctx.GetInstance<IDocumentStore>().OpenAsyncSession()
).Scoped();
```

**Scoped Lifetime:**
- New instance per HTTP request (for web endpoints)
- New instance per service invocation scope
- Disposed at end of scope

### Auto-Discovery Scanner

**Configuration (Lines 64-69):**
```csharp
Scan(s =>
{
    s.AssemblyContainingType<HydraCompositionRegistry>();  // Scan Hydra.Server assembly
    s.LookForRegistries();                                 // Find other ServiceRegistry classes
    s.WithDefaultConventions();                            // Register using default conventions
});
```

**Conventions:**
- Interfaces → Implementations (e.g., `IFoo` → `Foo`)
- Single implementation auto-registered
- Multiple implementations require explicit registration

## 8.2 Service Lifetimes

### Singleton
**When to use:**
- Stateless services
- Expensive to create (database connections)
- Shared across all users

**Examples:**
- `IDocumentStore` - RavenDB connection pool
- `HydraStoreSystemV1` - Storage system
- `ISessionManager` - Session registry

### Scoped
**When to use:**
- Per-request state
- Unit-of-work pattern
- Transaction boundaries

**Examples:**
- `IAsyncDocumentSession` - RavenDB session (transactions)

### Transient
**When to use:**
- Lightweight, stateful
- Different instance per call

**Examples:**
- (Not used in current codebase)

## 8.3 Service Activation

### HydraServiceActivator

**File:** `/src/Hydra/Hydra.Server/Platform/Infrastructures/DependencyInjection/HydraServiceActivator.cs` (130 lines)

**Purpose:** Create service instances with field injection

**CreateInstance (Lines 38-54):**
```csharp
public object CreateInstance(Type serviceType)
{
    var instance = Activator.CreateInstance(serviceType);
    var descriptor = _injectionCache.GetOrAdd(serviceType, BuildInjectionDescriptor);
    InjectFields(instance, descriptor);
    return instance;
}
```

**Field Injection During Activation:**
1. Reflection scans for `[InjectCaller*]` attributes (cached per type)
2. Reads `HydraExecutionContext.Current` from AsyncLocal
3. Sets field values via reflection
4. Returns activated instance

## 8.4 Service Bootstrapping

### App.xaml.cs Initialization

**File:** `/src/Hydra/Hydra.Server/App.xaml.cs`

**Phase 1: Lamar Container (Lines 54-60):**
```csharp
_host = Host.CreateDefaultBuilder(e.Args)
    .UseLamar((ctx, services) =>
    {
        services.IncludeRegistry(new HydraCompositionRegistry(ctx.Configuration));
    })
    .Build();
```

**Phase 2: ServiceRegistry.Global (Lines 62-64):**
```csharp
Backends.Services.ServiceRegistry.Global.Initialize(_host.Services);
Backends.Services.ServiceRegistry.Global.DiscoverServices();
Logger.LogInformation("[App] DI container initialized, starting auto-start services...", "Boot");
```

**Phase 3: Auto-Start Services (Lines 67-68):**
```csharp
await Backends.Services.ServiceBootManager.AutoStartHostedServicesAsync(
    Backends.Services.ServiceRegistry.Global,
    _host.Services,
    CancellationToken.None);
```

**Boot Order (by BootPriority):**
1. XmcpClientHydraService (Priority 3)
2. VisorHydraService (Priority 5)
3. HydraStoreSystemV1 (Priority 90)
4. HydraSecurityService (Priority 100)

## 8.5 Service Discovery via [HydraService]

### Attribute Definition

**File:** `/src/Hydra/Hydra.Server/Platform/Foundations/Service Foundation/HydraServiceAttribute.cs` (48 lines)

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class HydraServiceAttribute : Attribute
{
    public string Name { get; }
    public string? Description { get; set; }
    public int MaxInstancesPerSession { get; set; } = 1;
    public int BootPriority { get; set; } = 5000;  // Lower = starts earlier
    public bool AutoStart { get; set; } = false;
}
```

### Discovery Process

**ServiceRegistry.DiscoverServices (Lines 38-54):**
```csharp
public void DiscoverServices()
{
    var assembly = Assembly.GetExecutingAssembly();
    var serviceTypes = assembly.GetTypes()
        .Where(t => t.IsClass && !t.IsAbstract && typeof(IHydraService).IsAssignableFrom(t))
        .Where(t => t.GetCustomAttribute<HydraServiceAttribute>() != null);

    int count = 0;
    foreach (var type in serviceTypes)
    {
        var attr = type.GetCustomAttribute<HydraServiceAttribute>()!;
        Register(attr.Name, type);
        count++;
    }
    
    Logger.LogInformation($"[ServiceRegistry] Discovered {count} services", "ServiceDiscovery");
}
```

**Filters:**
1. Class (not interface)
2. Not abstract
3. Implements `IHydraService`
4. Has `[HydraService]` attribute

### Discovered Services

| Service | AutoStart | BootPriority | File |
|---------|-----------|--------------|------|
| XmcpClientHydraService | ✅ | 3 | `/Xmcp/XmcpClientHydraService.cs` |
| VisorHydraService | ✅ | 5 | `/Platform/Core Services/VisorHydraService/VisorHydraService.cs` |
| HydraStoreSystemV1 | ✅ | 90 | `/Platform/Foundations/Storage Foundation/HydraStoreSystemV1.cs` |
| HydraSecurityService | ✅ | 100 | `/Platform/Core Services/HydraSecurityService.cs` |
| HydraCoreService | ❌ | 5000 | `/Platform/Core Services/HydraCoreService.cs` |
| ChatGPTService | ❌ | 5000 | `/Components/Services/UI Dependent/ChatGPTService.cs` |

## 8.6 Per-Call Scope Creation

### IHydraScopeFactory

**Purpose:** Create scoped DI containers for service calls

**Implementation:** `HydraScopeFactory.cs`

**Usage:**
```csharp
using (var scope = _scopeFactory.CreateScope())
{
    var service = scope.ServiceProvider.GetRequiredService<IMyService>();
    await service.DoWorkAsync();
    // Scoped services (like IAsyncDocumentSession) disposed here
}
```

**Benefits:**
- Isolated transactions per call
- Automatic disposal of scoped resources
- Thread-safe (each scope independent)

## 8.7 Lamar vs ServiceRegistry

### Dual DI System

**Lamar (Primary):**
- Compile-time DI
- Constructor injection
- Singleton/Scoped lifetimes
- Infrastructure services

**ServiceRegistry (Runtime):**
- Reflection-based discovery
- Per-session singletons
- Dynamic service routing
- Business logic services

**Why Both?**
- Lamar: Infrastructure (databases, gateways, repos)
- ServiceRegistry: Services (dynamic, per-session, protocol-agnostic)

**Integration:**
```csharp
// Lamar creates ServiceRegistry infrastructure
services.AddSingleton<ISessionManager, HydraSessionManager>();

// ServiceRegistry discovers [HydraService] classes
ServiceRegistry.Global.DiscoverServices();

// ServiceRouter uses both:
var service = ServiceRegistry.GetOrCreateInstance(serviceName, sessionId, lamarServiceProvider);
```
