# VISORA Capability Providers vs Traditional DI Containers

**Last Updated:** 2025-11-10
**VISORA Version:** .NET 9.0
**Audience:** Architects evaluating dependency management strategies

---

## Table of Contents

1. [Overview](#overview)
2. [Capability Providers Explained](#capability-providers-explained)
3. [Traditional DI Containers](#traditional-di-containers)
4. [Capability Provider vs DI Container](#capability-provider-vs-di-container)
5. [VISORA vs ASP.NET Core DI](#visora-vs-aspnet-core-di)
6. [VISORA vs Autofac](#visora-vs-autofac)
7. [VISORA vs Ninject](#visora-vs-ninject)
8. [Service Locator Anti-Pattern](#service-locator-anti-pattern)
9. [Comparison Tables](#comparison-tables)
10. [When to Use Each](#when-to-use-each)
11. [Decision Criteria](#decision-criteria)
12. [Hybrid Approaches](#hybrid-approaches)
13. [Cross-References](#cross-references)

---

## Overview

### What Are Capability Providers?

**Capability providers** in VISORA are a lightweight mechanism for modules to discover and access host-provided APIs:

```csharp
// Module requests capabilities from host
public override async ValueTask InitializeAsync(
    ModuleContext context,
    CancellationToken cancellationToken = default)
{
    // Optional capability - graceful degradation
    var logger = context.Capabilities.GetOptional<ILogger>();
    logger?.LogInformation("Starting");

    // Required capability - throws if missing
    var fileSystem = context.Capabilities.GetRequired<IFileSystem>();
}
```

**Key Characteristics**:
- **Host-provided**: Host controls what capabilities are available
- **Explicit**: Modules explicitly request capabilities
- **Optional**: Modules can gracefully handle missing capabilities
- **Type-safe**: Capabilities are retrieved by type
- **No magic**: Simple dictionary-based lookup

### What Are DI Containers?

**Dependency Injection (DI) containers** manage object creation and lifetime:

```csharp
// Register services
services.AddSingleton<ILogger, ConsoleLogger>();
services.AddTransient<IRepository, SqlRepository>();

// Resolve services (via constructor injection)
public class MyService
{
    private readonly ILogger _logger;

    public MyService(ILogger logger) // Injected by container
    {
        _logger = logger;
    }
}
```

**Key Characteristics**:
- **Container-managed**: Container controls object creation
- **Implicit**: Dependencies declared via constructor parameters
- **Required by default**: Missing dependencies cause exceptions
- **Lifetime management**: Singleton, Scoped, Transient
- **Auto-wiring**: Automatic dependency resolution

---

## Capability Providers Explained

### How Capability Providers Work

**File**: `/src/Visora.Contracts/Common/ICapabilityProvider.cs`

```csharp
public interface ICapabilityProvider
{
    bool TryGet<TCapability>(out TCapability? capability)
        where TCapability : class;
}

// Extension methods
public static class CapabilityProviderExtensions
{
    public static TCapability? GetOptional<TCapability>(
        this ICapabilityProvider provider)
        where TCapability : class
    {
        return provider.TryGet(out TCapability? capability)
            ? capability
            : null;
    }

    public static TCapability GetRequired<TCapability>(
        this ICapabilityProvider provider)
        where TCapability : class
    {
        return provider.TryGet(out TCapability? capability)
            ? capability!
            : throw new InvalidOperationException(
                $"Required capability '{typeof(TCapability).FullName}' not available.");
    }
}
```

### Simple Implementation

```csharp
public sealed class BasicCapabilityProvider : ICapabilityProvider
{
    private readonly Dictionary<Type, object> _capabilities = new();

    public void Register<T>(T instance) where T : class
    {
        _capabilities[typeof(T)] = instance;
    }

    public bool TryGet<TCapability>(out TCapability? capability)
        where TCapability : class
    {
        if (_capabilities.TryGetValue(typeof(TCapability), out var instance))
        {
            capability = instance as TCapability;
            return capability != null;
        }

        capability = null;
        return false;
    }
}

// Usage in host
var capabilities = new BasicCapabilityProvider();
capabilities.Register<ILogger>(new ConsoleLogger());
capabilities.Register<IFileSystem>(new FileSystem());
```

### Design Principles

1. **Explicit over implicit**: Modules explicitly request capabilities
2. **Optional by default**: Missing capabilities don't cause failures
3. **Host controls availability**: Host decides what APIs to expose
4. **No automatic construction**: Host creates instances, not container
5. **Simple lookup**: Dictionary-based, no complex resolution

---

## Traditional DI Containers

### ASP.NET Core DI

**Built-in DI container** in ASP.NET Core:

```csharp
// Startup.cs
public void ConfigureServices(IServiceCollection services)
{
    services.AddSingleton<ILogger, ConsoleLogger>();
    services.AddScoped<IRepository, SqlRepository>();
    services.AddTransient<IEmailService, EmailService>();
}

// Usage (constructor injection)
public class UserController : ControllerBase
{
    private readonly ILogger _logger;
    private readonly IRepository _repo;

    public UserController(ILogger logger, IRepository repo)
    {
        _logger = logger;
        _repo = repo;
    }
}
```

### Autofac

**Feature-rich third-party container**:

```csharp
var builder = new ContainerBuilder();

// Registration
builder.RegisterType<ConsoleLogger>().As<ILogger>().SingleInstance();
builder.RegisterType<SqlRepository>().As<IRepository>().InstancePerLifetimeScope();

// Modules
builder.RegisterModule<DataModule>();

// Build container
var container = builder.Build();

// Resolution
using (var scope = container.BeginLifetimeScope())
{
    var logger = scope.Resolve<ILogger>();
}
```

### Ninject

**Kernel-based container**:

```csharp
var kernel = new StandardKernel();

// Binding
kernel.Bind<ILogger>().To<ConsoleLogger>().InSingletonScope();
kernel.Bind<IRepository>().To<SqlRepository>().InThreadScope();

// Resolution
var logger = kernel.Get<ILogger>();
```

---

## Capability Provider vs DI Container

### Conceptual Comparison

| Aspect | Capability Provider | DI Container |
|--------|---------------------|--------------|
| **Purpose** | Host API exposure | Dependency management |
| **Scope** | Host ↔ Module boundary | Within application |
| **Resolution** | Explicit request | Constructor injection |
| **Default** | Optional (graceful) | Required (throws) |
| **Lifetime** | Host-managed | Container-managed |
| **Construction** | Manual (host creates) | Automatic (container creates) |
| **Complexity** | Simple (dictionary) | Complex (graph resolution) |

### Side-by-Side Example

**Capability Provider (VISORA)**:
```csharp
// Host provides capabilities
var capabilities = new BasicCapabilityProvider();
capabilities.Register<ILogger>(new ConsoleLogger());
capabilities.Register<IFileSystem>(new FileSystem());

// Module requests capabilities
public override async ValueTask InitializeAsync(ModuleContext context, ...)
{
    // Optional - returns null if missing
    var logger = context.Capabilities.GetOptional<ILogger>();

    // Required - throws if missing
    var fileSystem = context.Capabilities.GetRequired<IFileSystem>();

    // Check availability
    if (context.Capabilities.TryGet<IAdvancedFeature>(out var feature))
    {
        // Use feature
    }
}
```

**DI Container (ASP.NET Core)**:
```csharp
// Register services
services.AddSingleton<ILogger, ConsoleLogger>();
services.AddSingleton<IFileSystem, FileSystem>();

// Constructor injection (implicit)
public class MyService
{
    private readonly ILogger _logger;
    private readonly IFileSystem _fileSystem;

    public MyService(ILogger logger, IFileSystem fileSystem)
    {
        _logger = logger; // Injected automatically
        _fileSystem = fileSystem;
    }
}

// Resolution
var service = serviceProvider.GetService<MyService>();
```

---

## VISORA vs ASP.NET Core DI

### Comparison Table

| Feature | VISORA | ASP.NET Core DI |
|---------|--------|-----------------|
| **Registration** | Manual, explicit | Fluent API (`AddSingleton`, etc.) |
| **Resolution** | Manual (`GetOptional`/`GetRequired`) | Automatic (constructor) |
| **Lifetime** | Host-managed | Singleton/Scoped/Transient |
| **Scopes** | None (single provider) | Hierarchical scopes |
| **Graph resolution** | No | Yes (resolves dependency trees) |
| **Optional deps** | Built-in (`GetOptional`) | Via `IServiceProvider.GetService` |
| **Circular deps** | Not applicable | Detected and prevented |

### Code Comparison

**ASP.NET Core DI**:
```csharp
// Registration
services.AddSingleton<ILogger, ConsoleLogger>();
services.AddSingleton<HttpClient>();
services.AddTransient<IUserService, UserService>();

// Automatic injection
public class UserService : IUserService
{
    private readonly ILogger _logger;
    private readonly HttpClient _httpClient;

    // Constructor injection (automatic)
    public UserService(ILogger logger, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
    }
}

// Container resolves entire graph
var userService = serviceProvider.GetRequiredService<IUserService>();
```

**VISORA Capability Provider**:
```csharp
// Registration
var capabilities = new BasicCapabilityProvider();
capabilities.Register<ILogger>(new ConsoleLogger());
capabilities.Register<HttpClient>(new HttpClient());

// Manual retrieval (explicit)
public override async ValueTask InitializeAsync(ModuleContext context, ...)
{
    // Module explicitly requests capabilities
    var logger = context.Capabilities.GetRequired<ILogger>();
    var httpClient = context.Capabilities.GetOptional<HttpClient>();
}
```

### When to Use Each

**Use VISORA Capability Provider When**:
- Crossing host ↔ module boundary
- Want explicit capability negotiation
- Need graceful degradation (optional capabilities)
- Building plugin systems
- Want simple, predictable lookup

**Use ASP.NET Core DI When**:
- Within host or module (internal dependencies)
- Want automatic dependency resolution
- Need lifetime management (Scoped, Transient)
- Building web applications
- Need dependency graph resolution

---

## VISORA vs Autofac

### Comparison Table

| Feature | VISORA | Autofac |
|---------|--------|---------|
| **Complexity** | Low | High |
| **Features** | Basic (dictionary) | Rich (modules, decorators, interceptors) |
| **Performance** | Fast (simple lookup) | Slower (complex resolution) |
| **Modules** | VISORA modules (different concept) | Autofac modules (DI modules) |
| **Registration** | Manual | Fluent + convention-based |
| **Lifetime** | Host-managed | Many options (Instance, LifetimeScope, etc.) |

### Code Comparison

**Autofac**:
```csharp
var builder = new ContainerBuilder();

// Rich registration API
builder.RegisterType<ConsoleLogger>().As<ILogger>().SingleInstance();
builder.RegisterType<FileSystem>().As<IFileSystem>().InstancePerLifetimeScope();

// Modules (DI modules, not VISORA modules)
builder.RegisterModule<LoggingModule>();

// Named instances
builder.RegisterType<SqlRepository>()
    .Named<IRepository>("sql");

// Decorators
builder.RegisterDecorator<ILogger, TimestampLogger>();

// Build and resolve
var container = builder.Build();
using (var scope = container.BeginLifetimeScope())
{
    var logger = scope.Resolve<ILogger>();
}
```

**VISORA**:
```csharp
// Simple registration
var capabilities = new BasicCapabilityProvider();
capabilities.Register<ILogger>(new ConsoleLogger());
capabilities.Register<IFileSystem>(new FileSystem());

// No decorators, no lifetimes, no scopes - just lookup
var logger = capabilities.GetRequired<ILogger>();
```

### When to Use Each

**Use VISORA When**:
- Simplicity is paramount
- Crossing host ↔ module boundary
- No need for complex features

**Use Autofac When**:
- Need rich DI features (decorators, interceptors)
- Complex dependency graphs
- Within host or module internals

---

## VISORA vs Ninject

### Comparison Table

| Feature | VISORA | Ninject |
|---------|--------|---------|
| **Binding** | Manual registration | Kernel binding |
| **Resolution** | Explicit | Via kernel |
| **Conventions** | None (explicit) | Convention-based scanning |
| **Injection** | Manual retrieval | Constructor/property/method |
| **Performance** | Fast | Slower (reflection-heavy) |

### Code Comparison

**Ninject**:
```csharp
var kernel = new StandardKernel();

// Binding
kernel.Bind<ILogger>().To<ConsoleLogger>().InSingletonScope();
kernel.Bind<IFileSystem>().To<FileSystem>();

// Named bindings
kernel.Bind<IRepository>().To<SqlRepository>().Named("sql");
kernel.Bind<IRepository>().To<InMemoryRepository>().Named("memory");

// Conditional bindings
kernel.Bind<ILogger>().To<FileLogger>()
    .When(request => request.Target.Type == typeof(ImportantService));

// Resolution
var logger = kernel.Get<ILogger>();
var sqlRepo = kernel.Get<IRepository>("sql");
```

**VISORA**:
```csharp
// Simple, explicit registration
var capabilities = new BasicCapabilityProvider();
capabilities.Register<ILogger>(new ConsoleLogger());
capabilities.Register<IFileSystem>(new FileSystem());

// Simple, explicit retrieval
var logger = capabilities.GetRequired<ILogger>();
```

---

## Service Locator Anti-Pattern

### What is Service Locator?

**Service Locator** is an anti-pattern where classes request dependencies from a global registry:

```csharp
// Anti-pattern: Service Locator
public class MyService
{
    public void DoWork()
    {
        var logger = ServiceLocator.Current.GetInstance<ILogger>();  // ❌
        var repo = ServiceLocator.Current.GetInstance<IRepository>(); // ❌
        logger.Log("Working");
    }
}
```

**Why it's an anti-pattern**:
- Hides dependencies (not visible in constructor)
- Couples code to service locator infrastructure
- Difficult to test (must mock service locator)
- Runtime errors for missing dependencies
- Violates Dependency Inversion Principle

### Is VISORA a Service Locator?

**No, with caveats**:

**VISORA Capability Provider is NOT a Service Locator because**:
1. **Boundary-specific**: Only used at host ↔ module boundary
2. **Context-provided**: Passed explicitly via `ModuleContext`
3. **Not global**: Not a static singleton
4. **Optional-first**: Encourages graceful degradation
5. **Limited scope**: Used only in lifecycle methods

**How VISORA avoids Service Locator issues**:

```csharp
// VISORA: Capability provider passed via context
public override async ValueTask InitializeAsync(
    ModuleContext context,  // ✅ Explicit parameter
    CancellationToken cancellationToken = default)
{
    // Retrieve at initialization, store as field
    _logger = context.Capabilities.GetOptional<ILogger>();  // ✅

    // NOT: context.Capabilities is not used throughout the codebase
}

// Use stored reference throughout module
private void DoWork()
{
    _logger?.LogInformation("Working");  // ✅ Use stored reference
}
```

**Service Locator would look like**:
```csharp
// Anti-pattern: Service Locator (NOT VISORA)
public void DoWork()
{
    // ❌ Requesting dependency every time it's needed
    var logger = ServiceLocator.Current.GetInstance<ILogger>();
    logger.Log("Working");
}
```

### Best Practices

**Do (VISORA way)**:
```csharp
public sealed class MyModule : Module
{
    private ILogger? _logger;
    private IFileSystem? _fileSystem;

    public override async ValueTask InitializeAsync(ModuleContext context, ...)
    {
        // ✅ Retrieve once at initialization
        _logger = context.Capabilities.GetOptional<ILogger>();
        _fileSystem = context.Capabilities.GetRequired<IFileSystem>();
    }

    public override async ValueTask ShutdownAsync(ModuleContext context, ...)
    {
        // ✅ Use stored references
        _logger?.LogInformation("Shutting down");
    }
}
```

**Don't (Service Locator way)**:
```csharp
public sealed class BadModule : Module
{
    private ModuleContext? _context;

    public override async ValueTask InitializeAsync(ModuleContext context, ...)
    {
        _context = context;  // ❌ Storing context
    }

    public override async ValueTask ShutdownAsync(ModuleContext context, ...)
    {
        // ❌ Retrieving capabilities in every method
        var logger = _context!.Capabilities.GetOptional<ILogger>();
        logger?.LogInformation("Shutting down");
    }
}
```

---

## Comparison Tables

### Registration

| Framework | Registration Style | Example |
|-----------|-------------------|---------|
| **VISORA** | Manual, explicit | `capabilities.Register<ILogger>(new ConsoleLogger())` |
| **ASP.NET Core DI** | Fluent API | `services.AddSingleton<ILogger, ConsoleLogger>()` |
| **Autofac** | Fluent + modules | `builder.RegisterType<ConsoleLogger>().As<ILogger>()` |
| **Ninject** | Kernel binding | `kernel.Bind<ILogger>().To<ConsoleLogger>()` |

### Resolution

| Framework | Resolution Style | Example |
|-----------|------------------|---------|
| **VISORA** | Explicit retrieval | `context.Capabilities.GetOptional<ILogger>()` |
| **ASP.NET Core DI** | Constructor injection | `public MyService(ILogger logger)` |
| **Autofac** | Resolve from scope | `scope.Resolve<ILogger>()` |
| **Ninject** | Get from kernel | `kernel.Get<ILogger>()` |

### Lifetime Management

| Framework | Lifetimes | Control |
|-----------|-----------|---------|
| **VISORA** | Host-managed | Host creates and owns instances |
| **ASP.NET Core DI** | Singleton/Scoped/Transient | Container manages lifetime |
| **Autofac** | Instance/LifetimeScope/Many | Container manages lifetime |
| **Ninject** | Singleton/Thread/Transient | Kernel manages lifetime |

---

## When to Use Each

### Use VISORA Capability Provider When

✅ Building plugin systems
✅ Crossing host ↔ module boundary
✅ Want explicit capability negotiation
✅ Need optional dependencies (graceful degradation)
✅ Simplicity is important
✅ Host controls available APIs

### Use Traditional DI Container When

✅ Within host or module internals
✅ Need automatic dependency resolution
✅ Want constructor injection
✅ Need lifetime management (Scoped, Transient)
✅ Building web applications
✅ Complex dependency graphs

---

## Decision Criteria

### Capability Provider vs DI Container Matrix

| Criterion | Capability Provider | DI Container |
|-----------|---------------------|--------------|
| **Boundary** | Host ↔ Module | Internal |
| **Complexity** | Low | Medium to High |
| **Auto-wiring** | No (explicit) | Yes (implicit) |
| **Optional deps** | Built-in | Via special methods |
| **Graph resolution** | No | Yes |
| **Performance** | Fast (dictionary) | Slower (resolution) |
| **Learning curve** | Low | Medium to High |

---

## Hybrid Approaches

### Combining Capability Provider + DI Container

VISORA and DI containers can coexist:

**Pattern 1: DI Container Inside Host**

```csharp
// Host uses DI container internally
public class MyHost
{
    private readonly IServiceProvider _services;
    private readonly ICapabilityProvider _capabilities;

    public MyHost()
    {
        // Setup DI container for host internals
        var services = new ServiceCollection();
        services.AddSingleton<ILogger, ConsoleLogger>();
        services.AddSingleton<IFileSystem, FileSystem>();
        _services = services.BuildServiceProvider();

        // Expose services as capabilities to modules
        _capabilities = new ServiceProviderCapabilityAdapter(_services);
    }
}

// Adapter: DI Container → Capability Provider
public class ServiceProviderCapabilityAdapter : ICapabilityProvider
{
    private readonly IServiceProvider _services;

    public ServiceProviderCapabilityAdapter(IServiceProvider services)
    {
        _services = services;
    }

    public bool TryGet<TCapability>(out TCapability? capability)
        where TCapability : class
    {
        capability = _services.GetService<TCapability>();
        return capability != null;
    }
}
```

**Pattern 2: DI Container Inside Module**

```csharp
// Module uses DI container for internal dependencies
public sealed class MyModule : Module
{
    private IServiceProvider? _internalServices;

    public override async ValueTask InitializeAsync(ModuleContext context, ...)
    {
        // Get capabilities from host
        var logger = context.Capabilities.GetOptional<ILogger>();

        // Setup DI container for module internals
        var services = new ServiceCollection();
        services.AddSingleton(logger);  // Register host-provided capability
        services.AddTransient<InternalService>();
        services.AddTransient<InternalRepository>();

        _internalServices = services.BuildServiceProvider();
    }
}
```

---

## Cross-References

### Related Documentation

- **[Capability Negotiation Pattern](../patterns/capability-negotiation/visora-analysis.md)**: Deep dive into capabilities
- **[Creating Modules](../blueprints/creating-modules.md)**: Using capabilities in modules
- **[Building Hosts](../blueprints/building-hosts.md)**: Providing capabilities from hosts

### Related Decisions

- **[ADR-002: Capability vs Service Locator](../decisions/capability-vs-service-locator.md)**: Why capabilities, not service locator

---

**End of Document**
