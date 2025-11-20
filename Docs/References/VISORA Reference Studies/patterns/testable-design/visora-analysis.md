# Testable Design Pattern - VISORA Deep Dive

**Last Updated:** 2025-11-10
**VISORA Version:** .NET 9.0
**Pattern Tier:** Tier 5 (Quality)
**Related Patterns:** Dependency Injection, Interface Segregation, SOLID Principles

---

## Table of Contents

1. [Pattern Overview](#pattern-overview)
2. [Why VISORA Uses Testable Design](#why-visora-uses-testable-design)
3. [VISORA Implementation](#visora-implementation)
4. [Code Examples](#code-examples)
5. [File References](#file-references)
6. [Dependency Injection for Testability](#dependency-injection-for-testability)
7. [Mocking Strategies](#mocking-strategies)
8. [Integration Test Patterns](#integration-test-patterns)
9. [Testing Async Code](#testing-async-code)
10. [Testing Lifecycle Methods](#testing-lifecycle-methods)
11. [Tradeoffs](#tradeoffs)
12. [Best Practices](#best-practices)
13. [Advanced Topics](#advanced-topics)

---

## Pattern Overview

### What is Testable Design?

**Definition:** Testable design is the practice of structuring code so that it can be easily verified through automated tests, typically by using abstractions (interfaces, abstract classes) and dependency injection to enable mocking and isolation.

**Key Characteristics:**
- **Interfaces over Implementations:** Depend on abstractions, not concrete types
- **Dependency Injection:** Dependencies passed in, not created internally
- **Seams for Testing:** Clear boundaries where behavior can be substituted
- **Single Responsibility:** Each class has one reason to change (easier to test)
- **Deterministic Behavior:** Minimize side effects, make outcomes predictable

### Testing Pyramid

```
        ┌─────────────────┐
        │   E2E Tests     │  ← Slow, expensive, few
        │   (Full system) │
        ├─────────────────┤
        │ Integration     │  ← Medium speed, moderate coverage
        │    Tests        │
        ├─────────────────┤
        │   Unit Tests    │  ← Fast, cheap, many
        │   (Isolated)    │
        └─────────────────┘
```

**VISORA's Focus:**
- **Unit Tests:** Test individual components in isolation (using mocks)
- **Integration Tests:** Test module loading, discovery, lifecycle
- **E2E Tests:** Test full CLI/Terminal workflows (future)

---

## Why VISORA Uses Testable Design

### Design Goals

1. **Verify Correctness**
   - Tests prove that modules, components, commands work as expected
   - Regression prevention (tests catch breaking changes)
   - Confidence in refactoring

2. **Enable Mocking**
   - ICapabilityProvider can be mocked
   - Abstract base classes can be stubbed
   - Module/component behavior can be isolated

3. **Support Continuous Integration**
   - Fast unit tests run on every commit
   - Integration tests verify end-to-end workflows
   - Automated validation before deployment

4. **Document Behavior**
   - Tests serve as living documentation
   - Examples of how to use APIs
   - Expected behavior is codified

5. **Facilitate Development**
   - Test-driven development (TDD) possible
   - Faster feedback loops
   - Easier to reproduce bugs

### Key Decision Points

**Q: Why interfaces instead of concrete classes?**
**A:** Interfaces define contracts and can be mocked. Concrete classes couple tests to implementation details.

**Q: Why dependency injection via constructor/context?**
**A:** Allows tests to inject mock dependencies instead of real ones.

**Q: Why abstract base classes with virtual methods?**
**A:** Allows tests to create minimal implementations that override only what's needed.

---

## VISORA Implementation

### Interface-Based Design

VISORA uses interfaces for key abstractions:

```
┌─────────────────────────────────────────────────────┐
│             Interfaces (Testable Seams)             │
│  ┌───────────────────────────────────────────────┐ │
│  │  ICapabilityProvider                          │ │
│  │  - TryGet<T>(out T capability)                │ │
│  │  → Can be mocked with test implementations    │ │
│  └───────────────────────────────────────────────┘ │
│                                                     │
│  ┌───────────────────────────────────────────────┐ │
│  │  IAsyncDisposable                             │ │
│  │  - DisposeAsync()                             │ │
│  │  → Standard interface for cleanup             │ │
│  └───────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────┘
```

### Abstract Base Classes

VISORA uses abstract base classes with virtual methods:

```
┌─────────────────────────────────────────────────────┐
│          Abstract Base Classes (Testable)           │
│  ┌───────────────────────────────────────────────┐ │
│  │  VisoraModule (abstract)                      │ │
│  │  - Descriptor (abstract property)             │ │
│  │  - InitializeAsync() (virtual, default no-op) │ │
│  │  - ShutdownAsync() (virtual, default no-op)   │ │
│  │  → Can create test subclasses with minimal    │ │
│  │    implementation                              │ │
│  └───────────────────────────────────────────────┘ │
│                                                     │
│  ┌───────────────────────────────────────────────┐ │
│  │  VisoraComponent (abstract)                   │ │
│  │  - Descriptor (abstract property)             │ │
│  │  - CreateCommands() (virtual, default empty)  │ │
│  │  → Can create test components easily          │ │
│  └───────────────────────────────────────────────┘ │
│                                                     │
│  ┌───────────────────────────────────────────────┐ │
│  │  VisoraCommand (abstract)                     │ │
│  │  - ExecuteAsync() (virtual, default success)  │ │
│  │  → Can create test commands easily            │ │
│  └───────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────┘
```

### Dependency Injection via Context

VISORA passes dependencies through context objects:

```
┌─────────────────────────────────────────────────────┐
│         Context Objects (Dependency Carriers)       │
│  ┌───────────────────────────────────────────────┐ │
│  │  ModuleContext                                │ │
│  │  - Descriptor: ModuleDescriptor               │ │
│  │  - Capabilities: ICapabilityProvider ← Mock!  │ │
│  │  - Services: IServiceProvider?     ← Mock!    │ │
│  │  - Properties: Dictionary          ← Test data│ │
│  └───────────────────────────────────────────────┘ │
│                                                     │
│  ┌───────────────────────────────────────────────┐ │
│  │  ComponentContext                             │ │
│  │  - Module: VisoraModule            ← Mock!    │ │
│  │  - Capabilities: ICapabilityProvider ← Mock!  │ │
│  └───────────────────────────────────────────────┘ │
│                                                     │
│  ┌───────────────────────────────────────────────┐ │
│  │  CommandContext                               │ │
│  │  - Module: VisoraModule            ← Mock!    │ │
│  │  - Component: VisoraComponent?     ← Mock!    │ │
│  │  - Capabilities: ICapabilityProvider ← Mock!  │ │
│  │  - Parameters: Dictionary          ← Test data│ │
│  └───────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────┘
```

---

## Code Examples

### Testable Interface: ICapabilityProvider

**Interface Definition:**

```csharp
// File: /src/Visora.Contracts/Common/ICapabilityProvider.cs
namespace Visora.Contracts.Common;

/// <summary>
/// Interface for capability lookup.
/// TESTABLE: Can be mocked with test implementations.
/// </summary>
public interface ICapabilityProvider
{
    bool TryGet<TCapability>(out TCapability? capability)
        where TCapability : class;
}
```

**Test Mock Implementation:**

```csharp
/// <summary>
/// Mock capability provider for tests.
/// </summary>
public sealed class MockCapabilityProvider : ICapabilityProvider
{
    private readonly Dictionary<Type, object> _capabilities = new();

    public void Add<T>(T capability) where T : class
    {
        _capabilities[typeof(T)] = capability;
    }

    public bool TryGet<TCapability>(out TCapability? capability)
        where TCapability : class
    {
        if (_capabilities.TryGetValue(typeof(TCapability), out var value))
        {
            capability = (TCapability)value;
            return true;
        }

        capability = null;
        return false;
    }
}

// Usage in test
[TestMethod]
public async Task Module_CanAccessCapabilities()
{
    // Arrange
    var mockLogger = new Mock<ILogger>();
    var capabilities = new MockCapabilityProvider();
    capabilities.Add<ILogger>(mockLogger.Object);

    var context = new ModuleContext(
        descriptor: CreateTestDescriptor(),
        services: null,
        capabilities: capabilities);

    var module = new TestModule();

    // Act
    await module.InitializeAsync(context);

    // Assert
    mockLogger.Verify(l => l.Log(It.IsAny<string>()), Times.Once);
}
```

### Testable Abstract Class: VisoraModule

**Test Implementation:**

```csharp
/// <summary>
/// Minimal test module for unit tests.
/// </summary>
public sealed class TestModule : VisoraModule
{
    private readonly ModuleDescriptor _descriptor;
    public bool InitializeCalled { get; private set; }
    public bool ShutdownCalled { get; private set; }

    public TestModule()
    {
        _descriptor = ModuleDescriptor.Create(
            id: "test.module",
            name: "Test Module",
            version: new Version(1, 0, 0));
    }

    public override ModuleDescriptor Descriptor => _descriptor;

    public override ValueTask InitializeAsync(
        ModuleContext context,
        CancellationToken cancellationToken = default)
    {
        InitializeCalled = true;
        return ValueTask.CompletedTask;
    }

    public override ValueTask ShutdownAsync(
        ModuleContext context,
        CancellationToken cancellationToken = default)
    {
        ShutdownCalled = true;
        return ValueTask.CompletedTask;
    }
}

// Usage in test
[TestMethod]
public async Task Module_InitializeAsync_IsCalled()
{
    // Arrange
    var module = new TestModule();
    var context = CreateTestContext();

    // Act
    await module.InitializeAsync(context);

    // Assert
    Assert.IsTrue(module.InitializeCalled);
}
```

### Testable Command Execution

**Test Implementation:**

```csharp
/// <summary>
/// Test command that records execution.
/// </summary>
public sealed class TestCommand : VisoraCommand
{
    private static readonly CommandDescriptor Info = CommandDescriptor.Create(
        id: "test.command",
        title: "Test Command");

    public override CommandDescriptor Descriptor => Info;

    public bool ExecuteCalled { get; private set; }
    public CommandContext? LastContext { get; private set; }

    public override ValueTask<CommandResult> ExecuteAsync(
        CommandContext context,
        CancellationToken cancellationToken = default)
    {
        ExecuteCalled = true;
        LastContext = context;
        return ValueTask.FromResult(CommandResult.Success("Test executed"));
    }
}

// Usage in test
[TestMethod]
public async Task Command_ExecuteAsync_ReceivesContext()
{
    // Arrange
    var command = new TestCommand();
    var context = CreateTestCommandContext();

    // Act
    var result = await command.ExecuteAsync(context);

    // Assert
    Assert.IsTrue(command.ExecuteCalled);
    Assert.IsNotNull(command.LastContext);
    Assert.AreEqual(CommandOutcome.Success, result.Outcome);
}
```

---

## File References

### Testable Interfaces

| File | Lines | Purpose |
|------|-------|---------|
| `/src/Visora.Contracts/Common/ICapabilityProvider.cs` | ~30 | Capability lookup interface (mockable) |

### Testable Abstract Classes

| File | Lines | Purpose |
|------|-------|---------|
| `/src/Visora.Contracts/Modules/VisoraModule.cs` | 41 | Module base class (virtual hooks) |
| `/src/Visora.Contracts/Components/VisoraComponent.cs` | 45 | Component base class (virtual hooks) |
| `/src/Visora.Contracts/Commands/VisoraCommand.cs` | ~30 | Command base class (virtual hooks) |

### Context Objects (Dependency Carriers)

| File | Lines | Purpose |
|------|-------|---------|
| `/src/Visora.Contracts/Modules/ModuleContext.cs` | ~30 | Module execution context |
| `/src/Visora.Contracts/Components/ComponentContext.cs` | ~20 | Component execution context |
| `/src/Visora.Contracts/Commands/CommandContext.cs` | ~40 | Command execution context |

### Testable Infrastructure

| File | Lines | Purpose |
|------|-------|---------|
| `/src/Visora.Core/Capabilities/CapabilityProviders.cs` | ~80 | Capability provider implementations |
| `/src/Visora.Core/Modules/ModuleCatalog.cs` | 46 | Registry (can be tested in isolation) |
| `/src/Visora.Core/Modules/ModuleHandle.cs` | ~160 | Module lifecycle (integration tests) |

---

## Dependency Injection for Testability

### Pattern: Constructor Injection (Not Used in VISORA Modules)

**Why not used:** Modules are discovered and instantiated via reflection. Constructor dependencies would require a DI container.

**Alternative:** Context-based injection (capabilities, services passed via context objects).

### Pattern: Context-Based Injection (VISORA's Approach)

**Advantage:** Modules can be instantiated without a DI container, yet still receive dependencies.

```csharp
public sealed class DatabaseModule : Module
{
    private IDbConnection? _connection;

    public override async ValueTask InitializeAsync(
        ModuleContext context,
        CancellationToken cancellationToken = default)
    {
        // Get dependency from context
        var config = context.Capabilities.GetOptional<IConfiguration>();
        if (config != null)
        {
            var connectionString = config["Database:ConnectionString"];
            _connection = new SqlConnection(connectionString);
            await _connection.OpenAsync(cancellationToken);
        }
    }
}

// Test: Inject mock configuration
[TestMethod]
public async Task DatabaseModule_InitializeAsync_UsesConfiguration()
{
    // Arrange
    var mockConfig = new Mock<IConfiguration>();
    mockConfig.Setup(c => c["Database:ConnectionString"])
        .Returns("Test Connection String");

    var capabilities = new MockCapabilityProvider();
    capabilities.Add<IConfiguration>(mockConfig.Object);

    var context = new ModuleContext(
        descriptor: CreateTestDescriptor(),
        services: null,
        capabilities: capabilities);

    var module = new DatabaseModule();

    // Act
    await module.InitializeAsync(context);

    // Assert
    mockConfig.Verify(c => c["Database:ConnectionString"], Times.Once);
}
```

### Pattern: Capability Provider as Service Locator

**Service Locator is typically an anti-pattern**, but VISORA's ICapabilityProvider is different:
- **Passed explicitly** (via context), not global
- **Testable** (can be mocked)
- **Optional dependencies** (modules check if capability is available)

```csharp
// Module uses optional capability
public override async ValueTask InitializeAsync(
    ModuleContext context,
    CancellationToken cancellationToken = default)
{
    // Optional dependency
    var logger = context.Capabilities.GetOptional<ILogger>();
    logger?.Log("Initializing module...");

    // Required dependency
    var database = context.Capabilities.GetRequired<IDatabase>();
    await database.ConnectAsync();
}

// Test: Provide only what's needed
[TestMethod]
public async Task Module_WorksWithoutLogger()
{
    // Arrange
    var mockDatabase = new Mock<IDatabase>();
    var capabilities = new MockCapabilityProvider();
    capabilities.Add<IDatabase>(mockDatabase.Object);
    // No logger provided

    var context = CreateContext(capabilities);
    var module = new TestModule();

    // Act & Assert (should not throw)
    await module.InitializeAsync(context);
}
```

---

## Mocking Strategies

### Strategy 1: Manual Mocks

**When to use:** Simple scenarios, no complex verification needed.

```csharp
public sealed class ManualMockLogger : ILogger
{
    public List<string> Messages { get; } = new();

    public void Log(string message)
    {
        Messages.Add(message);
    }
}

// Usage
[TestMethod]
public void Module_LogsInitialization()
{
    // Arrange
    var logger = new ManualMockLogger();
    var capabilities = new MockCapabilityProvider();
    capabilities.Add<ILogger>(logger);

    // Act
    var module = new TestModule();
    module.InitializeAsync(CreateContext(capabilities)).Wait();

    // Assert
    Assert.AreEqual(1, logger.Messages.Count);
    Assert.IsTrue(logger.Messages[0].Contains("Initializing"));
}
```

### Strategy 2: Moq Framework

**When to use:** Complex verification, setup/verify patterns.

```csharp
// Using Moq (NuGet: Moq)
[TestMethod]
public async Task Module_CallsDatabaseConnect()
{
    // Arrange
    var mockDatabase = new Mock<IDatabase>();
    mockDatabase.Setup(db => db.ConnectAsync(It.IsAny<CancellationToken>()))
        .ReturnsAsync(true);

    var capabilities = new MockCapabilityProvider();
    capabilities.Add<IDatabase>(mockDatabase.Object);

    var context = CreateContext(capabilities);
    var module = new DatabaseModule();

    // Act
    await module.InitializeAsync(context);

    // Assert
    mockDatabase.Verify(
        db => db.ConnectAsync(It.IsAny<CancellationToken>()),
        Times.Once);
}
```

### Strategy 3: Stub Implementations

**When to use:** Need functional behavior, not just verification.

```csharp
public sealed class InMemoryDatabase : IDatabase
{
    private readonly Dictionary<string, object> _data = new();

    public Task<bool> ConnectAsync(CancellationToken ct)
    {
        // Simulate connection
        return Task.FromResult(true);
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken ct)
    {
        _data.TryGetValue(key, out var value);
        return Task.FromResult((T?)value);
    }

    public Task SetAsync<T>(string key, T value, CancellationToken ct)
    {
        _data[key] = value!;
        return Task.CompletedTask;
    }
}

// Usage
[TestMethod]
public async Task Module_CanReadWriteDatabase()
{
    // Arrange
    var database = new InMemoryDatabase();
    var capabilities = new MockCapabilityProvider();
    capabilities.Add<IDatabase>(database);

    // Act
    await database.SetAsync("key", "value", CancellationToken.None);
    var result = await database.GetAsync<string>("key", CancellationToken.None);

    // Assert
    Assert.AreEqual("value", result);
}
```

### Strategy 4: Test Doubles for Abstract Classes

**When to use:** Need minimal implementation of abstract base class.

```csharp
// Test double: minimal module
public sealed class MinimalModule : VisoraModule
{
    private readonly ModuleDescriptor _descriptor;

    public MinimalModule(string id = "test.module")
    {
        _descriptor = ModuleDescriptor.Create(
            id: id,
            name: "Minimal Test Module",
            version: new Version(1, 0, 0));
    }

    public override ModuleDescriptor Descriptor => _descriptor;

    // Uses default implementations for everything else
}

// Usage
[TestMethod]
public void ModuleCatalog_CanRegisterMinimalModule()
{
    // Arrange
    var catalog = new ModuleCatalog();
    var module = new MinimalModule();

    // Act
    // ... (register module in catalog)

    // Assert
    Assert.IsNotNull(catalog.GetById("test.module"));
}
```

---

## Integration Test Patterns

### Pattern: End-to-End Module Lifecycle

```csharp
[TestClass]
public class ModuleLifecycleIntegrationTests
{
    [TestMethod]
    public async Task FullModuleLifecycle_Succeeds()
    {
        // Arrange
        var options = new ModuleCatalogOptions
        {
            Capabilities = CapabilityProviders.Empty
        };
        options.ProbingPaths.Add(GetTestModulesPath());

        var catalog = new ModuleCatalog();

        // Act 1: Discovery
        await catalog.DiscoverAsync(options, CancellationToken.None);

        // Assert 1: Module found
        Assert.IsTrue(catalog.Modules.Count > 0, "Should discover at least one module");

        var handle = catalog.Modules.First();

        // Act 2: Initialize
        await handle.EnsureInitializedAsync(CancellationToken.None);

        // Assert 2: Module initialized
        Assert.IsNotNull(handle.Module);

        // Act 3: Inspect
        var inspection = await handle.InspectAsync(CancellationToken.None);

        // Assert 3: Components and commands found
        Assert.IsTrue(inspection.Components.Count > 0, "Should have components");

        // Act 4: Shutdown
        await handle.ShutdownAsync(CancellationToken.None);

        // Act 5: Dispose
        await handle.DisposeAsync();

        // Assert 5: No exceptions thrown
    }

    private string GetTestModulesPath()
    {
        // Return path to test modules directory
        return Path.Combine(AppContext.BaseDirectory, "TestModules");
    }
}
```

### Pattern: Command Execution Integration Test

```csharp
[TestMethod]
public async Task Command_ExecuteAsync_IntegrationTest()
{
    // Arrange: Set up full context
    var module = new TestModule();
    var component = new TestComponent();

    var capabilities = CapabilityProviders.CreateBuilder()
        .Add<ILogger>(new ConsoleLogger())
        .Build();

    var context = new CommandContext(
        module: module,
        component: component,
        surface: CommandSurface.Programmatic,
        capabilities: capabilities,
        parameters: new Dictionary<string, object?>
        {
            ["input"] = "test value"
        },
        cancellationToken: CancellationToken.None);

    var command = new TestCommand();

    // Act
    var result = await command.ExecuteAsync(context, CancellationToken.None);

    // Assert
    Assert.AreEqual(CommandOutcome.Success, result.Outcome);
    Assert.IsNotNull(result.Payload);
}
```

---

## Testing Async Code

### Pattern: Testing Async Methods

```csharp
[TestMethod]
public async Task Module_InitializeAsync_CompletesSuccessfully()
{
    // Arrange
    var module = new TestModule();
    var context = CreateTestContext();

    // Act
    await module.InitializeAsync(context, CancellationToken.None);

    // Assert
    Assert.IsTrue(module.InitializeCalled);
}
```

### Pattern: Testing Cancellation

```csharp
[TestMethod]
public async Task Module_InitializeAsync_SupportsCancellation()
{
    // Arrange
    var module = new SlowModule(); // Module with long-running initialization
    var context = CreateTestContext();
    var cts = new CancellationTokenSource();

    // Act
    var task = module.InitializeAsync(context, cts.Token);
    cts.Cancel(); // Cancel immediately

    // Assert
    await Assert.ThrowsExceptionAsync<OperationCanceledException>(async () =>
    {
        await task;
    });
}
```

### Pattern: Testing Async Exceptions

```csharp
[TestMethod]
public async Task Module_InitializeAsync_ThrowsOnError()
{
    // Arrange
    var module = new FailingModule();
    var context = CreateTestContext();

    // Act & Assert
    await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () =>
    {
        await module.InitializeAsync(context);
    });
}
```

---

## Testing Lifecycle Methods

### Pattern: Testing Initialization

```csharp
[TestMethod]
public async Task Module_InitializeAsync_SetsUpState()
{
    // Arrange
    var module = new StatefulModule();
    var context = CreateTestContext();

    // Pre-condition: State is not initialized
    Assert.IsFalse(module.IsInitialized);

    // Act
    await module.InitializeAsync(context);

    // Assert: State is initialized
    Assert.IsTrue(module.IsInitialized);
}
```

### Pattern: Testing Shutdown

```csharp
[TestMethod]
public async Task Module_ShutdownAsync_CleansUpState()
{
    // Arrange
    var module = new StatefulModule();
    var context = CreateTestContext();

    await module.InitializeAsync(context);
    Assert.IsTrue(module.IsInitialized);

    // Act
    await module.ShutdownAsync(context);

    // Assert: State is cleaned up
    Assert.IsFalse(module.IsInitialized);
}
```

### Pattern: Testing Disposal

```csharp
[TestMethod]
public async Task Module_DisposeAsync_ReleasesResources()
{
    // Arrange
    var module = new ResourceModule();
    var context = CreateTestContext();

    await module.InitializeAsync(context);
    Assert.IsNotNull(module.ManagedResource);

    // Act
    await module.DisposeAsync();

    // Assert: Resource released
    Assert.IsNull(module.ManagedResource);
}
```

---

## Tradeoffs

### Advantages

1. **Confidence in Changes**
   - Tests catch regressions
   - Safe refactoring
   - Documentation of expected behavior

2. **Faster Development**
   - Quick feedback loops
   - Easier to reproduce bugs
   - Less manual testing

3. **Better Design**
   - Forces separation of concerns
   - Encourages loose coupling
   - Clear interfaces

4. **Continuous Integration**
   - Automated verification
   - Catch issues early
   - Prevent broken builds

### Disadvantages

1. **Additional Code**
   - Tests are code that must be maintained
   - More files to manage
   - Initial time investment

2. **Slower Initial Development**
   - Writing tests takes time
   - Learning curve for test frameworks
   - May feel like overhead initially

3. **False Sense of Security**
   - Tests only prove what they test
   - Can miss edge cases
   - Integration gaps

4. **Brittle Tests**
   - Over-mocking can make tests fragile
   - Implementation details leak into tests
   - Tests may need updating with refactoring

---

## Best Practices

### 1. Test Behavior, Not Implementation

**DO:**
```csharp
[TestMethod]
public async Task Command_ReturnsSuccessWithValidInput()
{
    // Arrange
    var command = new MyCommand();
    var context = CreateContext(new { input = "valid" });

    // Act
    var result = await command.ExecuteAsync(context);

    // Assert
    Assert.AreEqual(CommandOutcome.Success, result.Outcome);
}
```

**DON'T:**
```csharp
[TestMethod]
public void Command_CallsPrivateMethodInCorrectOrder()
{
    // Testing implementation details makes tests brittle
}
```

### 2. Use Descriptive Test Names

**DO:**
```csharp
[TestMethod]
public async Task Module_InitializeAsync_ThrowsWhenCapabilitiesAreNull()
```

**DON'T:**
```csharp
[TestMethod]
public void Test1()
```

### 3. Follow AAA Pattern (Arrange, Act, Assert)

**DO:**
```csharp
[TestMethod]
public async Task Command_ExecuteAsync_ReturnsSuccess()
{
    // Arrange
    var command = new TestCommand();
    var context = CreateContext();

    // Act
    var result = await command.ExecuteAsync(context);

    // Assert
    Assert.AreEqual(CommandOutcome.Success, result.Outcome);
}
```

### 4. One Assertion Per Test (Generally)

**DO:**
```csharp
[TestMethod]
public void Descriptor_Id_IsNotEmpty()
{
    var descriptor = CreateDescriptor();
    Assert.IsFalse(string.IsNullOrEmpty(descriptor.Id));
}

[TestMethod]
public void Descriptor_Name_IsNotEmpty()
{
    var descriptor = CreateDescriptor();
    Assert.IsFalse(string.IsNullOrEmpty(descriptor.Name));
}
```

**EXCEPTION:** Related assertions can be grouped:
```csharp
[TestMethod]
public void Descriptor_HasRequiredFields()
{
    var descriptor = CreateDescriptor();
    Assert.IsFalse(string.IsNullOrEmpty(descriptor.Id));
    Assert.IsFalse(string.IsNullOrEmpty(descriptor.Name));
    Assert.IsNotNull(descriptor.Version);
}
```

### 5. Use Test Helpers

**DO:**
```csharp
private ModuleContext CreateTestContext(
    ICapabilityProvider? capabilities = null)
{
    return new ModuleContext(
        descriptor: ModuleDescriptor.Create("test", "Test", new Version(1, 0, 0)),
        services: null,
        capabilities: capabilities ?? CapabilityProviders.Empty);
}

[TestMethod]
public async Task Test1()
{
    var context = CreateTestContext();
    // ...
}
```

---

## Advanced Topics

### 1. Test Data Builders

```csharp
public sealed class ModuleDescriptorBuilder
{
    private string _id = "test.module";
    private string _name = "Test Module";
    private Version _version = new Version(1, 0, 0);
    private string? _description;

    public ModuleDescriptorBuilder WithId(string id)
    {
        _id = id;
        return this;
    }

    public ModuleDescriptorBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public ModuleDescriptorBuilder WithVersion(Version version)
    {
        _version = version;
        return this;
    }

    public ModuleDescriptorBuilder WithDescription(string description)
    {
        _description = description;
        return this;
    }

    public ModuleDescriptor Build()
    {
        return ModuleDescriptor.Create(_id, _name, _version, _description);
    }
}

// Usage
var descriptor = new ModuleDescriptorBuilder()
    .WithId("custom.id")
    .WithName("Custom Name")
    .Build();
```

### 2. Custom Assertions

```csharp
public static class ModuleAssert
{
    public static void IsValidDescriptor(ModuleDescriptor descriptor)
    {
        Assert.IsNotNull(descriptor);
        Assert.IsFalse(string.IsNullOrEmpty(descriptor.Id));
        Assert.IsFalse(string.IsNullOrEmpty(descriptor.Name));
        Assert.IsNotNull(descriptor.Version);
    }
}

// Usage
[TestMethod]
public void Module_HasValidDescriptor()
{
    var module = new TestModule();
    ModuleAssert.IsValidDescriptor(module.Descriptor);
}
```

### 3. Parameterized Tests

```csharp
[TestClass]
public class CommandResultTests
{
    [DataTestMethod]
    [DataRow(CommandOutcome.Success, true)]
    [DataRow(CommandOutcome.Failed, false)]
    [DataRow(CommandOutcome.Cancelled, false)]
    public void CommandResult_IsSuccess_ReturnsCorrectValue(
        CommandOutcome outcome,
        bool expectedIsSuccess)
    {
        // Arrange
        var result = new CommandResult(outcome);

        // Act
        var isSuccess = result.Outcome == CommandOutcome.Success;

        // Assert
        Assert.AreEqual(expectedIsSuccess, isSuccess);
    }
}
```

---

## Summary

VISORA's testable design enables:

1. **Unit Testing:** Isolated testing of modules, components, commands using mocks
2. **Integration Testing:** End-to-end testing of module lifecycle
3. **Mocking:** ICapabilityProvider and abstract classes can be mocked
4. **Dependency Injection:** Context-based injection of dependencies
5. **Async Testing:** Testing async lifecycle methods with cancellation support

**Key Patterns:**
- Interfaces for abstractions (ICapabilityProvider)
- Abstract base classes with virtual methods (VisoraModule, VisoraComponent)
- Context objects for dependency injection
- Result objects instead of exceptions (CommandResult)

**Next Steps:**
- Review the Capability Negotiation pattern for DI details
- Review the Template Method pattern for virtual hooks
- Review the Async Patterns for testing async code

---

**End of Document**
