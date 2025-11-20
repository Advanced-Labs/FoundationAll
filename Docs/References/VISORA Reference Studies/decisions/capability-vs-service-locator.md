# ADR-002: Capability Provider Over Service Locator

**Last Updated:** 2025-11-10
**VISORA Version:** .NET 9.0
**Decision Status:** ✅ Accepted
**Decision Date:** 2024-Q4
**Supersedes:** None
**Related ADRs:** ADR-001 (Reflection Discovery), ADR-005 (Result Objects)

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
9. [Implementation Details](#implementation-details)
10. [Comparison Matrix](#comparison-matrix)
11. [When to Revisit](#when-to-revisit)
12. [Related Patterns](#related-patterns)
13. [References](#references)

---

## Executive Summary

**Decision:** VISORA implements a custom `ICapabilityProvider` interface for type-safe host-to-module service negotiation instead of using traditional dependency injection containers or service locator patterns.

**Key Rationale:**
- **Type-safe negotiation:** Compile-time verification of capability contracts
- **Host control:** Platform explicitly decides what modules can access
- **Explicit dependencies:** Clear declaration of what modules need
- **Boundary clarity:** Clean separation between host and module concerns
- **Testability:** Easy mocking and capability substitution in tests

**Primary Tradeoff:** Custom infrastructure investment vs. leveraging mature DI frameworks, with benefits outweighing the maintenance cost.

---

## Context

### The Service Access Challenge

In plugin architectures, modules need access to host-provided services. This creates a fundamental tension:

**Modules want:**
- Access to host functionality (logging, configuration, data access)
- Type-safe APIs with IntelliSense
- Clear contracts about what's available
- Testability via mocking

**Host wants:**
- Control over what modules can access
- Ability to version and evolve services
- Isolation between modules
- Security boundaries

### Traditional Solutions and Their Issues

#### Service Locator Anti-Pattern

```csharp
// Classic service locator (considered anti-pattern)
public class MyModule
{
    public void Execute()
    {
        var logger = ServiceLocator.Get<ILogger>();  // Hidden dependency!
        var config = ServiceLocator.Get<IConfiguration>();  // Runtime failure risk
        logger.Log("Processing...");
    }
}
```

**Problems:**
- Hidden dependencies (not in constructor)
- Runtime failures when services missing
- Hard to test (global static state)
- No compile-time verification

#### Traditional Dependency Injection

```csharp
// Standard DI approach
public class MyModule
{
    private readonly ILogger _logger;
    private readonly IConfiguration _config;

    public MyModule(ILogger logger, IConfiguration config)
    {
        _logger = logger;
        _config = config;
    }
}
```

**Problems for plugin scenarios:**
- Host and module share DI container (coupling)
- Modules can potentially access anything registered
- No clear boundary between host and module services
- Version conflicts between host and module dependencies

### VISORA's Unique Requirements

#### 1. Host-Controlled Access

The host must explicitly grant capabilities:

```
✅ Host decides: "This module can access logging and configuration"
❌ Module decides: "I'll use whatever's in the container"
```

#### 2. Versioning and Evolution

Services must evolve without breaking modules:

```csharp
// Version 1
ILogger { void Log(string message); }

// Version 2 (with more capabilities)
ILogger {
    void Log(string message);
    void LogStructured(string template, params object[] args);  // New!
}

// Modules built against V1 must still work
```

#### 3. Security Boundaries

Some capabilities should be restricted:

```csharp
// Trusted modules only
IFileSystem { ... }
IDatabaseAccess { ... }

// All modules
ILogger { ... }
IConfiguration { ... }
```

#### 4. Testability

Easy capability mocking in tests:

```csharp
[Test]
public void TestModule()
{
    var mockCapabilities = new MockCapabilityProvider()
        .WithLogger(mockLogger)
        .WithConfiguration(mockConfig);

    var module = new MyModule();
    await module.InitializeAsync(mockCapabilities);
}
```

---

## Problem Statement

### Core Question

**How do modules access host services in a type-safe, testable, and future-proof way without coupling to the host's internal dependency injection infrastructure?**

### Specific Challenges

#### 1. Dependency Transparency

Modules should explicitly declare what they need:

```csharp
// Bad: Hidden dependencies
public class ModuleA : VisoraModule
{
    public override async ValueTask InitializeAsync(???)
    {
        var logger = ???;  // Where does this come from?
        var db = ???;      // How do we get this?
    }
}

// Good: Explicit capability acquisition
public class ModuleA : VisoraModule
{
    private ILogger? _logger;

    public override async ValueTask InitializeAsync(ICapabilityProvider capabilities, CancellationToken ct)
    {
        _logger = capabilities.GetRequiredCapability<ILogger>();  // Clear!
        // Missing capability = immediate failure with clear message
    }
}
```

#### 2. Version Compatibility

Host services evolve, but old modules must continue working:

```csharp
// Host provides ILogger v2
capabilities.RegisterCapability<ILogger>(new LoggerV2(...));

// Module built against ILogger v1
var logger = capabilities.GetRequiredCapability<ILogger>();  // Should this work?
```

#### 3. Security and Trust Boundaries

Not all modules should access sensitive capabilities:

```csharp
// How do we enforce this?
public class UntrustedModule : VisoraModule
{
    public override async ValueTask InitializeAsync(???)
    {
        var fileSystem = ???;  // Should NOT be accessible!
        var database = ???;    // Should NOT be accessible!
    }
}
```

#### 4. Testing and Mocking

Capability injection must be mockable:

```csharp
[Test]
public async Task TestModuleInitialization()
{
    // How do we provide fake capabilities?
    var mockProvider = ???;

    var module = new MyModule();
    await module.InitializeAsync(mockProvider, CancellationToken.None);

    // Verify module used capabilities correctly
}
```

---

## Decision

### The Chosen Approach

**VISORA uses a custom `ICapabilityProvider` interface for type-safe, explicit, host-controlled service negotiation between the platform and modules.**

### Core Design

#### 1. The ICapabilityProvider Interface

```csharp
/// <summary>
/// Provides type-safe access to host capabilities for modules.
/// </summary>
public interface ICapabilityProvider
{
    /// <summary>
    /// Gets a capability of the specified type, or null if not available.
    /// </summary>
    TCapability? GetCapability<TCapability>() where TCapability : class;

    /// <summary>
    /// Gets a required capability, throwing if not available.
    /// </summary>
    TCapability GetRequiredCapability<TCapability>() where TCapability : class;

    /// <summary>
    /// Checks if a capability is available.
    /// </summary>
    bool HasCapability<TCapability>() where TCapability : class;

    /// <summary>
    /// Gets all capabilities of the specified type (for multi-instance scenarios).
    /// </summary>
    IEnumerable<TCapability> GetCapabilities<TCapability>() where TCapability : class;
}
```

#### 2. Module Initialization Pattern

```csharp
public abstract class VisoraModule
{
    public abstract string ModuleName { get; }
    public abstract string Description { get; }

    // Capability provider passed during initialization
    public virtual async ValueTask InitializeAsync(
        ICapabilityProvider capabilities,
        CancellationToken ct)
    {
        // Modules acquire capabilities here
        await ValueTask.CompletedTask;
    }
}
```

#### 3. Module Usage Pattern

```csharp
public class DataProcessingModule : VisoraModule
{
    private ILogger? _logger;
    private IConfiguration? _config;
    private IDataAccess? _dataAccess;

    public override string ModuleName => "Data Processing";
    public override string Description => "Process and transform data";

    public override async ValueTask InitializeAsync(
        ICapabilityProvider capabilities,
        CancellationToken ct)
    {
        // Required capabilities - fail fast if missing
        _logger = capabilities.GetRequiredCapability<ILogger>();
        _config = capabilities.GetRequiredCapability<IConfiguration>();

        // Optional capabilities - graceful degradation
        _dataAccess = capabilities.GetCapability<IDataAccess>();

        if (_dataAccess == null)
        {
            _logger.LogWarning("DataAccess capability not available, some features disabled");
        }

        _logger.LogInformation("Initialized {Module}", ModuleName);
        await ValueTask.CompletedTask;
    }
}
```

#### 4. Host Configuration Pattern

```csharp
public class VisoraHost
{
    private readonly ICapabilityProvider _capabilityProvider;

    public VisoraHost(IServiceProvider hostServices)
    {
        // Build capability provider from host services
        _capabilityProvider = new CapabilityProvider()
            // Core capabilities (all modules)
            .RegisterCapability<ILogger>(hostServices.GetRequiredService<ILogger>())
            .RegisterCapability<IConfiguration>(hostServices.GetRequiredService<IConfiguration>())

            // Optional capabilities (trusted modules only)
            .RegisterCapability<IFileSystem>(hostServices.GetRequiredService<IFileSystem>())
            .RegisterCapability<IDatabase>(hostServices.GetRequiredService<IDatabase>());
    }

    public async ValueTask LoadModuleAsync(VisoraModule module, CancellationToken ct)
    {
        // Initialize module with capability provider
        await module.InitializeAsync(_capabilityProvider, ct);
    }
}
```

---

## Alternatives Considered

### Alternative 1: ASP.NET Core Dependency Injection

**Approach:** Use the built-in Microsoft.Extensions.DependencyInjection container shared between host and modules.

```csharp
public class DataProcessingModule : VisoraModule
{
    private readonly ILogger _logger;
    private readonly IConfiguration _config;

    // Constructor injection from shared DI container
    public DataProcessingModule(ILogger<DataProcessingModule> logger, IConfiguration config)
    {
        _logger = logger;
        _config = config;
    }

    public override async ValueTask InitializeAsync(CancellationToken ct)
    {
        _logger.LogInformation("Initialized");
        await ValueTask.CompletedTask;
    }
}

// Host setup
var services = new ServiceCollection();
services.AddSingleton<ILogger, Logger>();
services.AddSingleton<IConfiguration, Configuration>();

var provider = services.BuildServiceProvider();

// Module instantiation
var module = ActivatorUtilities.CreateInstance<DataProcessingModule>(provider);
```

**Pros:**
- Mature, well-tested framework
- Excellent documentation and community support
- Built-in lifetime management (singleton, scoped, transient)
- Integration with ASP.NET Core ecosystem
- Constructor injection (clear dependencies)

**Cons:**
- ❌ **No access control:** Modules can request any registered service
- ❌ **Tight coupling:** Modules depend on host's DI configuration
- ❌ **No versioning support:** Interface changes break modules immediately
- ❌ **Testing complexity:** Must set up entire DI container for tests
- ❌ **No opt-in/opt-out:** Can't restrict capabilities per module
- ❌ **Hidden host internals:** Modules might access host implementation details

**Why Not Chosen:**
The lack of access control was a deal-breaker. There's no way to prevent a module from requesting `IDatabase` or other sensitive services. The tight coupling between host and module DI containers also violated our boundary separation principle.

---

### Alternative 2: Autofac DI Container

**Approach:** Use Autofac's advanced features like named registrations and module scanning.

```csharp
// Host setup
var builder = new ContainerBuilder();

// Register capabilities with named registrations
builder.RegisterType<Logger>().Named<ILogger>("HostLogger");
builder.RegisterType<Configuration>().Named<IConfiguration>("HostConfig");

// Register module
builder.RegisterType<DataProcessingModule>()
       .WithParameter(
           (pi, ctx) => pi.ParameterType == typeof(ILogger),
           (pi, ctx) => ctx.ResolveNamed<ILogger>("HostLogger"))
       .WithParameter(
           (pi, ctx) => pi.ParameterType == typeof(IConfiguration),
           (pi, ctx) => ctx.ResolveNamed<IConfiguration>("HostConfig"));

var container = builder.Build();

// Module usage
public class DataProcessingModule : VisoraModule
{
    public DataProcessingModule(ILogger logger, IConfiguration config)
    {
        // Constructor injection
    }
}
```

**Pros:**
- More powerful than MS DI (named registrations, decorators, etc.)
- Assembly scanning capabilities
- Lifetime scopes for isolation
- Property injection support
- Module system for organizing registrations

**Cons:**
- ❌ **Still no true access control:** Complex but not impossible to access other services
- ❌ **Heavy dependency:** Large framework for plugin scenarios
- ❌ **Configuration complexity:** Named registrations become unwieldy
- ❌ **Versioning unsolved:** Same interface compatibility issues
- ❌ **Testing overhead:** Full container setup required

**Why Not Chosen:**
Autofac's advanced features didn't solve the core access control problem. It's also a heavy dependency for what VISORA needs, and the configuration complexity would burden module authors.

---

### Alternative 3: MEF (Managed Extensibility Framework)

**Approach:** Use MEF's import/export attribute system.

```csharp
// Host exports
[Export(typeof(ILogger))]
public class Logger : ILogger { }

[Export(typeof(IConfiguration))]
public class Configuration : IConfiguration { }

// Module imports
public class DataProcessingModule : VisoraModule
{
    [Import]
    public ILogger Logger { get; set; }

    [Import]
    public IConfiguration Configuration { get; set; }

    [ImportMany]  // Multiple exports
    public IEnumerable<IDataProvider> DataProviders { get; set; }

    public override async ValueTask InitializeAsync(CancellationToken ct)
    {
        // Imports automatically satisfied by MEF
        Logger.LogInformation("Initialized");
        await ValueTask.CompletedTask;
    }
}

// Host composition
var catalog = new AggregateCatalog();
catalog.Catalogs.Add(new DirectoryCatalog("./modules"));

var container = new CompositionContainer(catalog);
container.SatisfyImportsOnce(module);
```

**Pros:**
- Designed specifically for plugin scenarios
- Attribute-based metadata
- Catalog system for discovery
- Lazy loading support
- Multiple export handling (ImportMany)

**Cons:**
- ❌ **Legacy status:** Not actively developed, considered dated
- ❌ **No access control:** Any import can be satisfied
- ❌ **Attribute noise:** Heavy attribute usage
- ❌ **Complex debugging:** Composition failures are cryptic
- ❌ **Limited type safety:** Export/Import by string name possible
- ❌ **No modern async support**

**Why Not Chosen:**
MEF is largely considered legacy in modern .NET. Its lack of access control and legacy status made it unsuitable for VISORA's forward-looking architecture.

---

### Alternative 4: Static Service Locator

**Approach:** Global static registry for services.

```csharp
// Global service locator
public static class ServiceLocator
{
    private static readonly Dictionary<Type, object> _services = new();

    public static void Register<T>(T service) where T : class
    {
        _services[typeof(T)] = service;
    }

    public static T Get<T>() where T : class
    {
        return (T)_services[typeof(T)];
    }
}

// Host setup
ServiceLocator.Register<ILogger>(new Logger());
ServiceLocator.Register<IConfiguration>(new Configuration());

// Module usage
public class DataProcessingModule : VisoraModule
{
    public override async ValueTask InitializeAsync(CancellationToken ct)
    {
        var logger = ServiceLocator.Get<ILogger>();  // Implicit dependency
        var config = ServiceLocator.Get<IConfiguration>();

        logger.LogInformation("Initialized");
        await ValueTask.CompletedTask;
    }
}
```

**Pros:**
- Simple implementation
- No framework dependencies
- Easy to understand
- Minimal boilerplate

**Cons:**
- ❌ **Anti-pattern:** Widely considered bad practice
- ❌ **Hidden dependencies:** Not visible in signatures
- ❌ **Global mutable state:** Testing nightmare
- ❌ **No compile-time safety:** Runtime errors only
- ❌ **Tight coupling:** Modules depend on global state
- ❌ **No isolation:** All modules share same locator
- ❌ **Thread safety concerns**

**Why Not Chosen:**
Service locator is considered an anti-pattern for good reasons. Hidden dependencies and global state make testing difficult and coupling tight. This was ruled out early in the design process.

---

### Alternative 5: Context Object Pattern

**Approach:** Pass a context object containing all services.

```csharp
// Context with all services
public class ModuleContext
{
    public ILogger Logger { get; init; }
    public IConfiguration Configuration { get; init; }
    public IFileSystem? FileSystem { get; init; }  // Optional
    public IDatabase? Database { get; init; }      // Optional

    // Validation
    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Logger);
        ArgumentNullException.ThrowIfNull(Configuration);
    }
}

// Module usage
public class DataProcessingModule : VisoraModule
{
    private ModuleContext? _context;

    public override async ValueTask InitializeAsync(ModuleContext context, CancellationToken ct)
    {
        context.Validate();
        _context = context;

        _context.Logger.LogInformation("Initialized");
        await ValueTask.CompletedTask;
    }
}
```

**Pros:**
- Simple and explicit
- All services in one place
- Easy to mock in tests
- No framework dependency
- Clear initialization contract

**Cons:**
- ❌ **Fixed contract:** Adding services breaks existing modules
- ❌ **No versioning:** Interface changes affect all modules
- ❌ **Type proliferation:** Need different contexts for different module types?
- ❌ **Optional service handling:** Lots of nullability checks
- ❌ **No type-safe extensibility:** Can't add new services without changing interface

**Why Not Chosen:**
While simpler than capability provider, the context object lacks extensibility. Every new service requires changing the `ModuleContext` interface, breaking all modules. The capability provider's generic design avoids this limitation.

---

## Rationale

### Why ICapabilityProvider Wins

#### 1. Type-Safe Negotiation

Compile-time verification with generics:

```csharp
// Compile-time type checking
var logger = capabilities.GetRequiredCapability<ILogger>();
//   ^                                          ^
//   |                                          |
//   Correct return type                        Type parameter ensures correctness

// Compare to service locator (runtime only):
var logger = (ILogger)ServiceLocator.Get(typeof(ILogger));  // Cast required, error-prone
```

#### 2. Explicit Dependencies

Clear declaration at initialization:

```csharp
public override async ValueTask InitializeAsync(ICapabilityProvider capabilities, CancellationToken ct)
{
    // Every capability acquisition is visible and intentional
    _logger = capabilities.GetRequiredCapability<ILogger>();
    _config = capabilities.GetRequiredCapability<IConfiguration>();
    _dataAccess = capabilities.GetCapability<IDataAccess>();  // Optional
}
```

**Benefits:**
- Code review: Easy to see what module needs
- Testing: Mock exactly what's acquired
- Documentation: Self-documenting dependencies

#### 3. Host Control

Platform explicitly decides what's available:

```csharp
public class VisoraHost
{
    private ICapabilityProvider CreateCapabilitiesFor(ModuleDescriptor module)
    {
        var provider = new CapabilityProvider();

        // All modules get these
        provider.RegisterCapability<ILogger>(_logger);
        provider.RegisterCapability<IConfiguration>(_config);

        // Trusted modules only
        if (module.IsTrusted)
        {
            provider.RegisterCapability<IFileSystem>(_fileSystem);
            provider.RegisterCapability<IDatabase>(_database);
        }

        return provider;
    }
}
```

**Access Control Achieved:**
- Host controls capability exposure
- Per-module customization possible
- Security boundaries enforced

#### 4. Versioning and Evolution

Capability interfaces can evolve:

```csharp
// Version 1
public interface ILogger
{
    void Log(string message);
}

// Version 2 (extends V1)
public interface ILogger
{
    void Log(string message);
    void LogStructured(string template, params object[] args);  // New!
}

// Old modules work without recompilation
var logger = capabilities.GetRequiredCapability<ILogger>();
logger.Log("Simple message");  // Still works!

// New modules can use extended interface
if (logger is IExtendedLogger extended)
{
    extended.LogStructured("User {UserId} logged in", userId);
}
```

#### 5. Testability

Easy mocking for unit tests:

```csharp
[Test]
public async Task TestModuleInitialization()
{
    // Arrange: Create mock capabilities
    var mockLogger = new Mock<ILogger>();
    var mockConfig = new Mock<IConfiguration>();

    var capabilities = new CapabilityProvider()
        .RegisterCapability(mockLogger.Object)
        .RegisterCapability(mockConfig.Object);

    // Act: Initialize module
    var module = new DataProcessingModule();
    await module.InitializeAsync(capabilities, CancellationToken.None);

    // Assert: Verify capability usage
    mockLogger.Verify(l => l.LogInformation(It.IsAny<string>()), Times.Once);
}
```

#### 6. Boundary Clarity

Clear separation between host and module concerns:

```
┌─────────────────────────────────────────────────────────┐
│                      VISORA Host                         │
│  ┌────────────────────────────────────────────────┐    │
│  │          ICapabilityProvider                    │    │
│  │  (Boundary Interface - Explicit Contract)      │    │
│  └────────────────────────────────────────────────┘    │
│         │                                    │           │
│         │ Provides                          │ Controls  │
│         ▼                                    ▼           │
│  Host Services                        Module Loading    │
│  - Logging                            - Discovery       │
│  - Config                             - Initialization  │
│  - Data Access                        - Lifecycle       │
└─────────────────────────────────────────────────────────┘
         │
         │ Consumes Capabilities
         ▼
┌─────────────────────────────────────────────────────────┐
│                    Module                                │
│  InitializeAsync(ICapabilityProvider capabilities)      │
│  {                                                       │
│      _logger = capabilities.GetRequiredCapability<...>; │
│  }                                                       │
└─────────────────────────────────────────────────────────┘
```

---

## Consequences

### Positive Consequences

#### 1. Strong Type Safety

All capability access is type-checked at compile time:

```csharp
var logger = capabilities.GetRequiredCapability<ILogger>();
logger.Log("Message");  // IntelliSense available, type-safe
```

#### 2. Clear Dependency Graph

Easy to analyze what modules need:

```csharp
// Static analysis tool can extract:
// DataProcessingModule depends on: ILogger, IConfiguration, IDataAccess
```

#### 3. Enhanced Security

Host controls capability exposure per module:

```csharp
// Untrusted module gets limited capabilities
var limitedProvider = new CapabilityProvider()
    .RegisterCapability<ILogger>(sandboxedLogger);

await untrustedModule.InitializeAsync(limitedProvider, ct);
```

#### 4. Simplified Testing

Mock capabilities trivially:

```csharp
var testProvider = new CapabilityProvider()
    .RegisterCapability(mockLogger)
    .RegisterCapability(mockConfig);
```

#### 5. Future-Proof Design

New capabilities added without breaking modules:

```csharp
// Host adds new capability
provider.RegisterCapability<INewFeature>(newFeature);

// Old modules: Unaffected
// New modules: Can opt-in
var feature = capabilities.GetCapability<INewFeature>();
if (feature != null) { /* use it */ }
```

### Negative Consequences

#### 1. Custom Infrastructure

Must maintain ICapabilityProvider implementation:

```csharp
// Code we own and must maintain
public class CapabilityProvider : ICapabilityProvider
{
    private readonly Dictionary<Type, object> _capabilities = new();

    public T? GetCapability<T>() where T : class
    {
        return _capabilities.TryGetValue(typeof(T), out var capability)
            ? (T)capability
            : null;
    }

    // ... more implementation
}
```

**Mitigation:** Implementation is straightforward (~200 lines), well-tested, and stable.

#### 2. Less Familiar Pattern

Developers coming from ASP.NET Core expect constructor injection:

```csharp
// Expected pattern (doesn't work in VISORA)
public MyModule(ILogger logger, IConfiguration config)
{
    _logger = logger;
    _config = config;
}

// VISORA pattern (different)
public override async ValueTask InitializeAsync(ICapabilityProvider capabilities, ...)
{
    _logger = capabilities.GetRequiredCapability<ILogger>();
    _config = capabilities.GetRequiredCapability<IConfiguration>();
}
```

**Mitigation:** Clear documentation, examples, and module templates. Benefits outweigh learning curve.

#### 3. Verbose Acquisition

More code than constructor injection:

```csharp
// Constructor injection (concise)
public MyModule(ILogger logger) => _logger = logger;

// Capability provider (more verbose)
public override async ValueTask InitializeAsync(ICapabilityProvider capabilities, CancellationToken ct)
{
    _logger = capabilities.GetRequiredCapability<ILogger>();
    await ValueTask.CompletedTask;
}
```

**Mitigation:** Code generation templates, snippets, and analyzers can reduce boilerplate.

#### 4. Runtime Registration

Capabilities registered at runtime, not compile-time:

```csharp
// If host forgets to register capability:
provider.RegisterCapability<ILogger>(logger);
// provider.RegisterCapability<IConfiguration>(config);  // Oops! Forgot this

// Module initialization fails at runtime
_config = capabilities.GetRequiredCapability<IConfiguration>();  // Exception!
```

**Mitigation:** Host initialization unit tests verify all required capabilities registered.

---

## Tradeoffs

### Capability Provider vs. Alternatives

| Aspect | Capability Provider | MS DI | Autofac | MEF | Service Locator | Context Object |
|--------|-------------------|-------|---------|-----|----------------|----------------|
| **Type Safety** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐ | ⭐⭐⭐⭐⭐ |
| **Access Control** | ⭐⭐⭐⭐⭐ | ⭐ | ⭐⭐ | ⭐ | ⭐ | ⭐⭐⭐ |
| **Explicit Dependencies** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐ | ⭐ | ⭐⭐⭐⭐⭐ |
| **Versioning Support** | ⭐⭐⭐⭐⭐ | ⭐⭐ | ⭐⭐ | ⭐⭐ | ⭐⭐ | ⭐⭐ |
| **Testability** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐ | ⭐⭐⭐⭐⭐ |
| **Boundary Clarity** | ⭐⭐⭐⭐⭐ | ⭐⭐ | ⭐⭐ | ⭐⭐⭐ | ⭐ | ⭐⭐⭐⭐ |
| **Learning Curve** | ⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐ | ⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ |
| **Ecosystem Support** | ⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐ |
| **Maintenance Burden** | ⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ |
| **Performance** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ |
| **Total Score** | **47/50** | **37/50** | **34/50** | **25/50** | **26/50** | **39/50** |

### Our Prioritization

1. **Access Control** (⭐⭐⭐⭐⭐): Non-negotiable for security
2. **Boundary Clarity** (⭐⭐⭐⭐⭐): Essential for maintainability
3. **Versioning Support** (⭐⭐⭐⭐⭐): Critical for long-term evolution
4. **Type Safety** (⭐⭐⭐⭐): Important for developer experience
5. **Ecosystem Support** (⭐⭐⭐): Nice to have, not critical

---

## Comparison Matrix

### Detailed Feature Comparison

#### Access Control Mechanisms

| Pattern | How It Works | Effectiveness | Notes |
|---------|--------------|--------------|-------|
| **Capability Provider** | Host explicitly registers capabilities per module | ✅ Complete control | Host decides exactly what each module can access |
| **MS DI** | All registered services available to all consumers | ❌ No control | Any module can request any service |
| **Autofac** | Named registrations and lifetime scopes | ⚠️ Complex | Possible but requires intricate setup |
| **MEF** | All exports available to all importers | ❌ No control | No built-in access restrictions |
| **Service Locator** | Global registry, anyone can access | ❌ No control | Worst case - completely open |
| **Context Object** | Host creates context per module | ✅ Good control | Must create different context types for different needs |

#### Dependency Visibility

| Pattern | Constructor Signature | Initialization Code | Visibility |
|---------|----------------------|---------------------|------------|
| **Capability Provider** | `InitializeAsync(ICapabilityProvider, CT)` | `capabilities.GetRequired<T>()` | ✅ Clear at initialization |
| **MS DI** | `MyModule(T1, T2, T3)` | Constructor assignment | ✅ Very clear in constructor |
| **Autofac** | `MyModule(T1, T2, T3)` | Constructor assignment | ✅ Very clear in constructor |
| **MEF** | `MyModule()` | `[Import] T Property` | ⚠️ Scattered attributes |
| **Service Locator** | `MyModule()` | `ServiceLocator.Get<T>()` | ❌ Hidden in implementation |
| **Context Object** | `Initialize(Context)` | Access via context properties | ✅ Clear at initialization |

#### Testing Scenarios

```csharp
// Capability Provider
[Test]
public async Task TestWithCapabilities()
{
    var mockLogger = new Mock<ILogger>();
    var caps = new CapabilityProvider().RegisterCapability(mockLogger.Object);
    var module = new MyModule();
    await module.InitializeAsync(caps, default);
    mockLogger.Verify(l => l.Log(It.IsAny<string>()));
}

// MS DI
[Test]
public void TestWithMSDI()
{
    var services = new ServiceCollection();
    services.AddSingleton<ILogger>(new Mock<ILogger>().Object);
    services.AddSingleton<IConfig>(new Mock<IConfig>().Object);
    var provider = services.BuildServiceProvider();
    var module = ActivatorUtilities.CreateInstance<MyModule>(provider);
    // Test module...
}

// Service Locator
[Test]
public void TestWithServiceLocator()
{
    ServiceLocator.Clear();  // Clear global state
    ServiceLocator.Register(new Mock<ILogger>().Object);
    var module = new MyModule();
    // Test...
    ServiceLocator.Clear();  // Don't forget cleanup!
}
```

**Winner:** Capability Provider and MS DI tie for testability, but capability provider wins on access control.

---

## Implementation Details

### Core Implementation

```csharp
/// <summary>
/// Provides type-safe capability negotiation between host and modules.
/// </summary>
public class CapabilityProvider : ICapabilityProvider
{
    private readonly Dictionary<Type, object> _capabilities = new();
    private readonly Dictionary<Type, List<object>> _multiCapabilities = new();
    private readonly ILogger? _logger;

    public CapabilityProvider(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Registers a capability for modules to acquire.
    /// </summary>
    public CapabilityProvider RegisterCapability<TCapability>(TCapability capability)
        where TCapability : class
    {
        ArgumentNullException.ThrowIfNull(capability);

        var type = typeof(TCapability);
        _capabilities[type] = capability;

        _logger?.LogDebug("Registered capability: {Type}", type.Name);

        return this;  // Fluent API
    }

    /// <summary>
    /// Registers multiple instances of the same capability type.
    /// </summary>
    public CapabilityProvider RegisterCapabilities<TCapability>(IEnumerable<TCapability> capabilities)
        where TCapability : class
    {
        ArgumentNullException.ThrowIfNull(capabilities);

        var type = typeof(TCapability);
        if (!_multiCapabilities.ContainsKey(type))
        {
            _multiCapabilities[type] = new List<object>();
        }

        _multiCapabilities[type].AddRange(capabilities.Cast<object>());

        _logger?.LogDebug("Registered {Count} capabilities of type: {Type}",
                         capabilities.Count(), type.Name);

        return this;
    }

    /// <summary>
    /// Gets a capability if available, otherwise returns null.
    /// </summary>
    public TCapability? GetCapability<TCapability>() where TCapability : class
    {
        var type = typeof(TCapability);

        if (_capabilities.TryGetValue(type, out var capability))
        {
            _logger?.LogTrace("Retrieved capability: {Type}", type.Name);
            return (TCapability)capability;
        }

        _logger?.LogTrace("Capability not available: {Type}", type.Name);
        return null;
    }

    /// <summary>
    /// Gets a required capability, throwing if not available.
    /// </summary>
    public TCapability GetRequiredCapability<TCapability>() where TCapability : class
    {
        var capability = GetCapability<TCapability>();

        if (capability == null)
        {
            var type = typeof(TCapability);
            _logger?.LogError("Required capability not available: {Type}", type.Name);
            throw new CapabilityNotFoundException(type);
        }

        return capability;
    }

    /// <summary>
    /// Checks if a capability is available.
    /// </summary>
    public bool HasCapability<TCapability>() where TCapability : class
    {
        return _capabilities.ContainsKey(typeof(TCapability));
    }

    /// <summary>
    /// Gets all capabilities of the specified type.
    /// </summary>
    public IEnumerable<TCapability> GetCapabilities<TCapability>() where TCapability : class
    {
        var type = typeof(TCapability);

        // Check multi-capability registration
        if (_multiCapabilities.TryGetValue(type, out var multiCaps))
        {
            return multiCaps.Cast<TCapability>();
        }

        // Check single capability registration
        if (_capabilities.TryGetValue(type, out var singleCap))
        {
            return new[] { (TCapability)singleCap };
        }

        return Enumerable.Empty<TCapability>();
    }
}

/// <summary>
/// Exception thrown when a required capability is not available.
/// </summary>
public class CapabilityNotFoundException : Exception
{
    public Type CapabilityType { get; }

    public CapabilityNotFoundException(Type capabilityType)
        : base($"Required capability not found: {capabilityType.FullName}")
    {
        CapabilityType = capabilityType;
    }
}
```

### Host Integration

```csharp
public class VisoraHostBuilder
{
    private readonly IServiceProvider _hostServices;
    private readonly List<Type> _standardCapabilities = new();
    private readonly List<Type> _trustedCapabilities = new();

    public VisoraHostBuilder WithStandardCapability<TCapability>()
        where TCapability : class
    {
        _standardCapabilities.Add(typeof(TCapability));
        return this;
    }

    public VisoraHostBuilder WithTrustedCapability<TCapability>()
        where TCapability : class
    {
        _trustedCapabilities.Add(typeof(TCapability));
        return this;
    }

    public VisoraHost Build()
    {
        // Create capability factory
        var factory = new CapabilityProviderFactory(
            _hostServices,
            _standardCapabilities,
            _trustedCapabilities);

        return new VisoraHost(factory);
    }
}

public class CapabilityProviderFactory
{
    private readonly IServiceProvider _hostServices;
    private readonly IReadOnlyList<Type> _standardCapabilities;
    private readonly IReadOnlyList<Type> _trustedCapabilities;

    public CapabilityProviderFactory(
        IServiceProvider hostServices,
        IReadOnlyList<Type> standardCapabilities,
        IReadOnlyList<Type> trustedCapabilities)
    {
        _hostServices = hostServices;
        _standardCapabilities = standardCapabilities;
        _trustedCapabilities = trustedCapabilities;
    }

    public ICapabilityProvider CreateForModule(ModuleDescriptor module)
    {
        var provider = new CapabilityProvider();

        // Register standard capabilities (all modules)
        foreach (var type in _standardCapabilities)
        {
            var service = _hostServices.GetRequiredService(type);
            provider.RegisterCapability(type, service);
        }

        // Register trusted capabilities (if module is trusted)
        if (module.IsTrusted)
        {
            foreach (var type in _trustedCapabilities)
            {
                var service = _hostServices.GetRequiredService(type);
                provider.RegisterCapability(type, service);
            }
        }

        return provider;
    }
}
```

### Module Helper Extensions

```csharp
/// <summary>
/// Extension methods for common capability acquisition patterns.
/// </summary>
public static class CapabilityProviderExtensions
{
    /// <summary>
    /// Gets capability with fallback default.
    /// </summary>
    public static TCapability GetCapabilityOrDefault<TCapability>(
        this ICapabilityProvider provider,
        TCapability defaultValue)
        where TCapability : class
    {
        return provider.GetCapability<TCapability>() ?? defaultValue;
    }

    /// <summary>
    /// Gets capability or creates a new instance.
    /// </summary>
    public static TCapability GetCapabilityOrCreate<TCapability>(
        this ICapabilityProvider provider,
        Func<TCapability> factory)
        where TCapability : class
    {
        return provider.GetCapability<TCapability>() ?? factory();
    }

    /// <summary>
    /// Tries to get capability with out parameter pattern.
    /// </summary>
    public static bool TryGetCapability<TCapability>(
        this ICapabilityProvider provider,
        out TCapability? capability)
        where TCapability : class
    {
        capability = provider.GetCapability<TCapability>();
        return capability != null;
    }

    /// <summary>
    /// Gets all required capabilities at once.
    /// </summary>
    public static (T1, T2) GetRequiredCapabilities<T1, T2>(
        this ICapabilityProvider provider)
        where T1 : class
        where T2 : class
    {
        return (
            provider.GetRequiredCapability<T1>(),
            provider.GetRequiredCapability<T2>()
        );
    }

    /// <summary>
    /// Gets all required capabilities at once (3-tuple).
    /// </summary>
    public static (T1, T2, T3) GetRequiredCapabilities<T1, T2, T3>(
        this ICapabilityProvider provider)
        where T1 : class
        where T2 : class
        where T3 : class
    {
        return (
            provider.GetRequiredCapability<T1>(),
            provider.GetRequiredCapability<T2>(),
            provider.GetRequiredCapability<T3>()
        );
    }
}
```

---

## When to Revisit

### Triggers for Reconsideration

#### 1. .NET Ecosystem Changes

**Scenario:** Microsoft introduces first-class plugin isolation in the framework

**Example:** Hypothetical .NET 11 feature:
```csharp
services.AddPluginIsolation(options =>
{
    options.ExportService<ILogger>().ToAllPlugins();
    options.ExportService<IFileSystem>().ToTrustedPluginsOnly();
});
```

**Action:** Evaluate migrating to framework solution if it provides equivalent access control.

#### 2. Performance Concerns

**Current Overhead:** Dictionary lookups are O(1) but not zero-cost

**Metrics to Watch:**
- Capability acquisition > 1ms per call
- Memory overhead > 10MB for capability provider instances
- Throughput degradation in high-frequency scenarios

**Potential Solution:** Introduce compiled expression trees for capability access.

#### 3. Community Feedback

**Scenario:** Module authors consistently struggle with capability pattern

**Indicators:**
- High support ticket volume
- Common mistakes in module implementations
- Community requests for traditional DI

**Action:** Consider hybrid approach allowing both patterns:
```csharp
// Option 1: Capability provider (current)
await module.InitializeAsync(capabilities, ct);

// Option 2: Constructor injection (new)
module = ActivatorUtilities.CreateInstance<MyModule>(provider);
```

#### 4. Advanced Capability Scenarios

**Future needs:**
- Capability versioning (ILogger v1 vs v2)
- Capability composition (combine multiple capabilities)
- Dynamic capability injection (capabilities change at runtime)
- Capability proxies for telemetry/logging

**Action:** Extend `ICapabilityProvider` with advanced features without breaking existing modules.

---

## Related Patterns

### Primary Patterns

#### 1. Capability Negotiation Pattern
- **Location:** `/References/patterns/capability-negotiation.md`
- **Relationship:** Detailed implementation guide for this ADR
- **Summary:** How modules negotiate capabilities with the host

#### 2. Module Lifecycle Pattern
- **Location:** `/References/patterns/module-lifecycle.md`
- **Relationship:** Capability acquisition happens during initialization phase
- **Summary:** Complete module lifecycle from discovery to shutdown

### Related ADRs

#### ADR-001: Reflection Over Manifests
- **Connection:** Discovered modules initialize with capability provider
- **Flow:** Discovery → Loading → Initialization (with capabilities)

#### ADR-005: Result Objects Not Exceptions
- **Connection:** Capability provider could use Result<T> for missing capabilities
- **Current:** Uses exceptions for required capabilities, null for optional

### Supporting Patterns

#### 3. Dependency Injection Patterns
- **Location:** `/References/patterns/dependency-injection.md`
- **Summary:** How capability provider relates to traditional DI

#### 4. Service Boundary Pattern
- **Location:** `/References/patterns/service-boundaries.md`
- **Summary:** Clean separation between host and module concerns

---

## References

### Internal Documentation
- `/References/patterns/capability-negotiation.md` - Implementation guide
- `/References/patterns/module-initialization.md` - Initialization sequences
- `/References/blueprints/module-template.md` - Module starter template

### External Resources
- [Dependency Inversion Principle](https://en.wikipedia.org/wiki/Dependency_inversion_principle)
- [Service Locator Anti-Pattern](https://blog.ploeh.dk/2010/02/03/ServiceLocatorisanAnti-Pattern/)
- [Capability-Based Security](https://en.wikipedia.org/wiki/Capability-based_security)

### Inspirations
- **Capability-Based Security:** OS-level capability systems
- **OAuth Scopes:** Permission negotiation patterns
- **MEF Contracts:** Typed export/import matching
- **Kubernetes RBAC:** Resource access control

---

## Appendix: Complete Example

### Host Setup

```csharp
// Program.cs
var builder = Host.CreateDefaultBuilder(args);

builder.ConfigureServices(services =>
{
    // Host services (internal to host)
    services.AddSingleton<ILogger, SerilogLogger>();
    services.AddSingleton<IConfiguration>(ctx => ctx.GetRequiredService<IConfiguration>());
    services.AddSingleton<IFileSystem, FileSystem>();
    services.AddSingleton<IDatabase, PostgresDatabase>();

    // VISORA host
    services.AddSingleton<VisoraHost>();
});

var host = builder.Build();

// Create VISORA host with capability configuration
var visoraHost = new VisoraHostBuilder(host.Services)
    .WithStandardCapability<ILogger>()
    .WithStandardCapability<IConfiguration>()
    .WithTrustedCapability<IFileSystem>()
    .WithTrustedCapability<IDatabase>()
    .Build();

// Discover and load modules
await visoraHost.DiscoverAndLoadModulesAsync("./modules");

await host.RunAsync();
```

### Module Implementation

```csharp
// DataProcessingModule.cs
public class DataProcessingModule : VisoraModule
{
    private ILogger? _logger;
    private IConfiguration? _config;
    private IDatabase? _database;

    public override string ModuleName => "Data Processing";
    public override string Description => "Processes and transforms data";

    public override async ValueTask InitializeAsync(
        ICapabilityProvider capabilities,
        CancellationToken ct)
    {
        // Required capabilities - fail fast
        _logger = capabilities.GetRequiredCapability<ILogger>();
        _config = capabilities.GetRequiredCapability<IConfiguration>();

        // Optional capabilities - graceful degradation
        _database = capabilities.GetCapability<IDatabase>();

        if (_database == null)
        {
            _logger.LogWarning("Database capability not available, using in-memory cache");
        }

        _logger.LogInformation("Initialized {Module} v{Version}", ModuleName, Version);

        await ValueTask.CompletedTask;
    }

    public override IEnumerable<CommandDescriptor> GetCommands()
    {
        yield return new CommandDescriptor(
            Name: "process-data",
            Description: "Process data from source to destination",
            Parameters: [
                new ParameterDescriptor("source", typeof(string), true),
                new ParameterDescriptor("destination", typeof(string), true)
            ],
            Handler: typeof(ProcessDataCommandHandler)
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

---

**Document Metadata:**
- **Author:** VISORA Architecture Team
- **Contributors:** Security Review Board, Module Authors
- **Review Cycle:** Quarterly
- **Next Review:** 2025-02-10
