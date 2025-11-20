# VISORA Testing Strategies - Comprehensive Guide

**Last Updated:** 2025-11-10
**VISORA Version:** .NET 9.0
**Skill Level:** Intermediate to Advanced
**Estimated Time:** 60-90 minutes to implement

---

## Table of Contents

1. [Overview](#overview)
2. [Testing Philosophy](#testing-philosophy)
3. [Test Project Setup](#test-project-setup)
4. [Unit Testing Modules](#unit-testing-modules)
5. [Unit Testing Components](#unit-testing-components)
6. [Unit Testing Commands](#unit-testing-commands)
7. [Integration Testing](#integration-testing)
8. [Testing with ModuleCatalog](#testing-with-modulecatalog)
9. [Mocking ICapabilityProvider](#mocking-icapabilityprovider)
10. [Testing Async Lifecycle](#testing-async-lifecycle)
11. [Testing Cancellation](#testing-cancellation)
12. [Test Organization](#test-organization)
13. [Best Practices](#best-practices)
14. [Common Testing Patterns](#common-testing-patterns)
15. [Cross-References](#cross-references)

---

## Overview

### Why Testing Matters

Testing VISORA modules, components, and commands ensures:

- **Correctness**: Features work as designed
- **Reliability**: Code handles edge cases and errors gracefully
- **Maintainability**: Refactoring doesn't break existing functionality
- **Documentation**: Tests serve as executable documentation
- **Confidence**: Safe to make changes

### Testing Layers

```
┌─────────────────────────────────────────────┐
│  End-to-End Tests                           │
│  - Full host + modules + commands           │
│  - Real execution environment               │
└─────────────────────────────────────────────┘
                   ↓
┌─────────────────────────────────────────────┐
│  Integration Tests                          │
│  - ModuleCatalog + multiple modules         │
│  - Component discovery + command registry   │
└─────────────────────────────────────────────┘
                   ↓
┌─────────────────────────────────────────────┐
│  Unit Tests                                 │
│  - Individual modules, components, commands │
│  - Mocked dependencies                      │
│  - Fast, isolated                           │
└─────────────────────────────────────────────┘
```

---

## Testing Philosophy

### VISORA Testing Principles

1. **Test behavior, not implementation**: Focus on what the code does, not how
2. **Isolation**: Tests should not depend on each other
3. **Speed**: Unit tests should run in milliseconds
4. **Clarity**: Test names should describe what is being tested
5. **Realistic**: Use realistic test data and scenarios

### Test Naming Convention

```csharp
[Fact]
public void MethodName_StateUnderTest_ExpectedBehavior()
{
    // Example: ExecuteAsync_WithValidInput_ReturnsSuccess
}
```

### AAA Pattern

All tests follow Arrange-Act-Assert:

```csharp
[Fact]
public async Task Example_Test()
{
    // Arrange: Set up test data and dependencies
    var module = new MyModule();
    var context = CreateMockContext();

    // Act: Execute the code under test
    var result = await module.InitializeAsync(context);

    // Assert: Verify the outcome
    Assert.NotNull(result);
}
```

---

## Test Project Setup

### Creating a Test Project

```bash
# Create xUnit test project
dotnet new xunit -n MyModule.Tests -f net9.0

# Add reference to module project
dotnet add MyModule.Tests reference MyModule/MyModule.csproj

# Add testing packages
dotnet add MyModule.Tests package Moq
dotnet add MyModule.Tests package FluentAssertions
dotnet add MyModule.Tests package Microsoft.NET.Test.Sdk
```

### Project Structure

```
MyModule.Tests/
├── MyModule.Tests.csproj
├── ModuleTests/
│   ├── MyModuleTests.cs
│   └── ModuleLifecycleTests.cs
├── ComponentTests/
│   ├── CoreComponentTests.cs
│   └── ComponentLifecycleTests.cs
├── CommandTests/
│   ├── StatusCommandTests.cs
│   └── ConfigureCommandTests.cs
├── IntegrationTests/
│   ├── ModuleCatalogTests.cs
│   └── EndToEndTests.cs
└── Helpers/
    ├── TestContextFactory.cs
    └── MockCapabilityProvider.cs
```

### Test Project Configuration

**File**: `MyModule.Tests.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="xunit" Version="2.6.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.4" />
    <PackageReference Include="Moq" Version="4.20.70" />
    <PackageReference Include="FluentAssertions" Version="6.12.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\MyModule\MyModule.csproj" />
    <ProjectReference Include="..\..\Visora.Contracts\Visora.Contracts.csproj" />
    <ProjectReference Include="..\..\Visora.Core\Visora.Core.csproj" />
  </ItemGroup>

</Project>
```

---

## Unit Testing Modules

### Testing Module Descriptor

```csharp
using Xunit;
using MyModule;

public class MyModuleTests
{
    [Fact]
    public void Module_HasValidDescriptor()
    {
        // Arrange & Act
        var module = new MyFeatureModule();

        // Assert
        Assert.NotNull(module.Descriptor);
        Assert.Equal("mycompany.myfeature", module.Descriptor.Id);
        Assert.Equal("My Feature Module", module.Descriptor.Name);
        Assert.NotNull(module.Descriptor.Version);
        Assert.True(module.Descriptor.Version >= new Version(0, 1, 0));
    }

    [Fact]
    public void Module_Descriptor_IsImmutable()
    {
        // Arrange
        var module = new MyFeatureModule();

        // Act
        var descriptor1 = module.Descriptor;
        var descriptor2 = module.Descriptor;

        // Assert
        Assert.Same(descriptor1, descriptor2);
    }
}
```

### Testing Module Initialization

```csharp
[Fact]
public async Task InitializeAsync_CompletesSuccessfully()
{
    // Arrange
    var module = new MyFeatureModule();
    var mockCapabilities = new Mock<ICapabilityProvider>();
    var context = new ModuleContext(
        descriptor: module.Descriptor,
        services: null,
        capabilities: mockCapabilities.Object,
        properties: new Dictionary<string, object?>());

    // Act
    await module.InitializeAsync(context);

    // Assert - no exceptions thrown
}

[Fact]
public async Task InitializeAsync_WithLogger_LogsStartMessage()
{
    // Arrange
    var module = new MyFeatureModule();
    var mockLogger = new Mock<ILogger>();
    var mockCapabilities = new Mock<ICapabilityProvider>();

    mockCapabilities
        .Setup(c => c.TryGet(out It.Ref<ILogger?>.IsAny))
        .Returns((out ILogger? logger) =>
        {
            logger = mockLogger.Object;
            return true;
        });

    var context = new ModuleContext(
        descriptor: module.Descriptor,
        services: null,
        capabilities: mockCapabilities.Object,
        properties: new Dictionary<string, object?>());

    // Act
    await module.InitializeAsync(context);

    // Assert
    mockLogger.Verify(
        l => l.LogInformation(It.IsAny<string>(), It.IsAny<object[]>()),
        Times.AtLeastOnce);
}
```

### Testing Component Discovery

```csharp
[Fact]
public void DiscoverComponents_ReturnsExpectedComponentTypes()
{
    // Arrange
    var module = new MyFeatureModule();
    var mockCapabilities = new Mock<ICapabilityProvider>();
    var context = new ModuleDiscoveryContext(
        moduleAssembly: typeof(MyFeatureModule).Assembly,
        capabilities: mockCapabilities.Object);

    // Act
    var componentTypes = module.DiscoverComponents(context).ToList();

    // Assert
    Assert.NotEmpty(componentTypes);
    Assert.Contains(componentTypes, t => t.Name == "CoreComponent");
    Assert.Contains(componentTypes, t => t.Name == "DiagnosticsComponent");
}

[Fact]
public void DiscoverComponents_OnlyReturnsPublicTypes()
{
    // Arrange
    var module = new MyFeatureModule();
    var mockCapabilities = new Mock<ICapabilityProvider>();
    var context = new ModuleDiscoveryContext(
        moduleAssembly: typeof(MyFeatureModule).Assembly,
        capabilities: mockCapabilities.Object);

    // Act
    var componentTypes = module.DiscoverComponents(context).ToList();

    // Assert
    Assert.All(componentTypes, type => Assert.True(type.IsPublic));
}
```

### Testing Module Shutdown

```csharp
[Fact]
public async Task ShutdownAsync_DisposesResourcesProperly()
{
    // Arrange
    var module = new MyFeatureModule();
    var context = CreateMockContext();

    await module.InitializeAsync(context);

    // Act
    await module.ShutdownAsync(context);

    // Assert
    // Verify resources were cleaned up (module-specific checks)
}

[Fact]
public async Task ShutdownAsync_CanBeCalledWithoutInitialize()
{
    // Arrange
    var module = new MyFeatureModule();
    var context = CreateMockContext();

    // Act & Assert - should not throw
    await module.ShutdownAsync(context);
}
```

---

## Unit Testing Components

### Testing Component Descriptor

```csharp
public class CoreComponentTests
{
    [Fact]
    public void Component_HasValidDescriptor()
    {
        // Arrange & Act
        var component = new CoreComponent();

        // Assert
        Assert.NotNull(component.Descriptor);
        Assert.Equal("mycompany.myfeature.core", component.Descriptor.Id);
        Assert.Equal("Core Component", component.Descriptor.Name);
        Assert.Equal(ComponentKind.Generic, component.Descriptor.Kind);
    }
}
```

### Testing Component Initialization

```csharp
[Fact]
public async Task InitializeAsync_LoadsConfiguration()
{
    // Arrange
    var component = new CoreComponent();
    var mockModule = new Mock<VisoraModule>();
    var mockCapabilities = new Mock<ICapabilityProvider>();

    var context = new ComponentContext(
        module: mockModule.Object,
        descriptor: component.Descriptor,
        capabilities: mockCapabilities.Object,
        surface: Surface.Cli,
        properties: new Dictionary<string, object?>());

    // Act
    await component.InitializeAsync(context);

    // Assert
    // Verify configuration was loaded (component-specific)
}
```

### Testing Component Lifecycle

```csharp
[Fact]
public async Task Lifecycle_InitializeActivateDeactivateDispose_Succeeds()
{
    // Arrange
    var component = new CoreComponent();
    var context = CreateComponentContext();

    // Act & Assert
    await component.InitializeAsync(context);
    await component.ActivateAsync(context);
    await component.DeactivateAsync(context);
    await component.DisposeAsync();

    // No exceptions = success
}

[Fact]
public async Task ActivateAsync_StartsBackgroundTasks()
{
    // Arrange
    var component = new CoreComponent();
    var context = CreateComponentContext();

    await component.InitializeAsync(context);

    // Act
    await component.ActivateAsync(context);

    // Assert
    // Verify background tasks were started (component-specific)
}

[Fact]
public async Task DeactivateAsync_StopsBackgroundTasks()
{
    // Arrange
    var component = new CoreComponent();
    var context = CreateComponentContext();

    await component.InitializeAsync(context);
    await component.ActivateAsync(context);

    // Act
    await component.DeactivateAsync(context);

    // Assert
    // Verify background tasks were stopped (component-specific)
}
```

### Testing Command Registration

```csharp
[Fact]
public void CreateCommands_ReturnsExpectedCommands()
{
    // Arrange
    var component = new CoreComponent();
    var context = CreateComponentContext();

    // Act
    var commands = component.CreateCommands(context).ToList();

    // Assert
    Assert.NotEmpty(commands);
    Assert.Contains(commands, c => c.Descriptor.Id == "myfeature.status");
    Assert.Contains(commands, c => c.Descriptor.Id == "myfeature.configure");
}

[Fact]
public void CreateCommands_AllCommandsHaveValidDescriptors()
{
    // Arrange
    var component = new CoreComponent();
    var context = CreateComponentContext();

    // Act
    var commands = component.CreateCommands(context).ToList();

    // Assert
    Assert.All(commands, command =>
    {
        Assert.NotNull(command.Descriptor);
        Assert.NotNull(command.Descriptor.Id);
        Assert.NotNull(command.Descriptor.Title);
    });
}
```

---

## Unit Testing Commands

### Testing Command Descriptor

```csharp
public class StatusCommandTests
{
    [Fact]
    public void Command_HasValidDescriptor()
    {
        // Arrange & Act
        var command = new StatusCommand();

        // Assert
        Assert.NotNull(command.Descriptor);
        Assert.Equal("myfeature.status", command.Descriptor.Id);
        Assert.Equal("Status", command.Descriptor.Title);
        Assert.Equal(CommandKind.Tool, command.Descriptor.Kind);
    }
}
```

### Testing Command Execution

```csharp
[Fact]
public async Task ExecuteAsync_WithNoParameters_ReturnsSuccess()
{
    // Arrange
    var command = new StatusCommand();
    var context = CreateCommandContext();

    // Act
    var result = await command.ExecuteAsync(context);

    // Assert
    Assert.NotNull(result);
    Assert.True(result.IsSuccess);
    Assert.NotNull(result.Message);
}

[Fact]
public async Task ExecuteAsync_WithValidParameters_ReturnsSuccess()
{
    // Arrange
    var command = new GreetCommand();
    var parameters = new Dictionary<string, object?>
    {
        ["name"] = "Alice"
    };
    var context = CreateCommandContext(parameters);

    // Act
    var result = await command.ExecuteAsync(context);

    // Assert
    Assert.True(result.IsSuccess);
    Assert.Contains("Alice", result.Message);
}

[Fact]
public async Task ExecuteAsync_WithMissingRequiredParameter_ReturnsFailure()
{
    // Arrange
    var command = new RequireParameterCommand();
    var context = CreateCommandContext(); // No parameters

    // Act
    var result = await command.ExecuteAsync(context);

    // Assert
    Assert.False(result.IsSuccess);
    Assert.Contains("Missing required parameter", result.Message);
}
```

### Testing Command Error Handling

```csharp
[Fact]
public async Task ExecuteAsync_WithInvalidInput_ReturnsFailure()
{
    // Arrange
    var command = new ValidatingCommand();
    var parameters = new Dictionary<string, object?>
    {
        ["value"] = -1 // Invalid value
    };
    var context = CreateCommandContext(parameters);

    // Act
    var result = await command.ExecuteAsync(context);

    // Assert
    Assert.False(result.IsSuccess);
    Assert.Contains("Invalid", result.Message);
}

[Fact]
public async Task ExecuteAsync_WhenExceptionOccurs_ReturnsFailure()
{
    // Arrange
    var command = new FaultyCommand(); // Command that throws
    var context = CreateCommandContext();

    // Act
    var result = await command.ExecuteAsync(context);

    // Assert
    Assert.False(result.IsSuccess);
}
```

### Testing Command with Capabilities

```csharp
[Fact]
public async Task ExecuteAsync_WithLogger_LogsExecution()
{
    // Arrange
    var command = new StatusCommand();
    var mockLogger = new Mock<ILogger>();
    var mockCapabilities = new Mock<ICapabilityProvider>();

    mockCapabilities
        .Setup(c => c.TryGet(out It.Ref<ILogger?>.IsAny))
        .Returns((out ILogger? logger) =>
        {
            logger = mockLogger.Object;
            return true;
        });

    var context = CreateCommandContext(
        capabilities: mockCapabilities.Object);

    // Act
    await command.ExecuteAsync(context);

    // Assert
    mockLogger.Verify(
        l => l.LogInformation(It.IsAny<string>(), It.IsAny<object[]>()),
        Times.AtLeastOnce);
}
```

---

## Integration Testing

### Testing Module with Catalog

```csharp
public class ModuleCatalogIntegrationTests
{
    [Fact]
    public async Task ModuleCatalog_CanDiscoverModule()
    {
        // Arrange
        var options = new ModuleCatalogOptions
        {
            Capabilities = CreateMockCapabilities(),
            RecurseSubdirectories = false
        };

        options.ProbingPaths.Add(GetTestModulePath());

        await using var catalog = new ModuleCatalog();

        // Act
        await catalog.DiscoverAsync(options);

        // Assert
        Assert.NotEmpty(catalog.Modules);
        var module = catalog.GetById("mycompany.myfeature");
        Assert.NotNull(module);
    }

    [Fact]
    public async Task ModuleCatalog_InitializesAllModules()
    {
        // Arrange
        var capabilities = CreateMockCapabilities();
        var options = new ModuleCatalogOptions
        {
            Capabilities = capabilities
        };

        options.ProbingPaths.Add(GetTestModulePath());

        await using var catalog = new ModuleCatalog();
        await catalog.DiscoverAsync(options);

        // Act
        foreach (var handle in catalog.Modules)
        {
            var context = new ModuleContext(
                descriptor: handle.Descriptor,
                services: null,
                capabilities: capabilities,
                properties: new Dictionary<string, object?>());

            await handle.Module.InitializeAsync(context);
        }

        // Assert
        Assert.All(catalog.Modules, handle =>
        {
            Assert.NotNull(handle.Module);
        });
    }

    private string GetTestModulePath()
    {
        var assemblyPath = typeof(MyFeatureModule).Assembly.Location;
        return Path.GetDirectoryName(assemblyPath)!;
    }
}
```

### Testing Component Discovery Integration

```csharp
[Fact]
public async Task Module_DiscoveryAndInitialization_ProducesCommands()
{
    // Arrange
    var module = new MyFeatureModule();
    var capabilities = CreateMockCapabilities();

    var moduleContext = new ModuleContext(
        descriptor: module.Descriptor,
        services: null,
        capabilities: capabilities,
        properties: new Dictionary<string, object?>());

    await module.InitializeAsync(moduleContext);

    // Discover components
    var discoveryContext = new ModuleDiscoveryContext(
        moduleAssembly: typeof(MyFeatureModule).Assembly,
        capabilities: capabilities);

    var componentTypes = module.DiscoverComponents(discoveryContext).ToList();

    // Act - Instantiate components and collect commands
    var allCommands = new List<VisoraCommand>();
    foreach (var componentType in componentTypes)
    {
        var component = (VisoraComponent)Activator.CreateInstance(componentType)!;

        var componentContext = new ComponentContext(
            module: module,
            descriptor: component.Descriptor,
            capabilities: capabilities,
            surface: Surface.Cli,
            properties: new Dictionary<string, object?>());

        await component.InitializeAsync(componentContext);

        var commands = component.CreateCommands(componentContext);
        allCommands.AddRange(commands);
    }

    // Assert
    Assert.NotEmpty(allCommands);
    Assert.All(allCommands, cmd => Assert.NotNull(cmd.Descriptor));
}
```

---

## Testing with ModuleCatalog

### Complete End-to-End Test

```csharp
[Fact]
public async Task EndToEnd_LoadModuleAndExecuteCommand()
{
    // Arrange - Create catalog and discover modules
    var capabilities = CreateRealCapabilities();
    var options = new ModuleCatalogOptions
    {
        Capabilities = capabilities
    };

    options.ProbingPaths.Add(GetTestModulePath());

    await using var catalog = new ModuleCatalog();
    await catalog.DiscoverAsync(options);

    // Initialize modules
    foreach (var handle in catalog.Modules)
    {
        var context = new ModuleContext(
            descriptor: handle.Descriptor,
            services: null,
            capabilities: capabilities,
            properties: new Dictionary<string, object?>());

        await handle.Module.InitializeAsync(context);
    }

    // Build command registry
    var registry = new CommandRegistry();
    foreach (var handle in catalog.Modules)
    {
        var discoveryContext = new ModuleDiscoveryContext(
            moduleAssembly: handle.Module.GetType().Assembly,
            capabilities: capabilities);

        var componentTypes = handle.Module.DiscoverComponents(discoveryContext);

        foreach (var componentType in componentTypes)
        {
            var component = (VisoraComponent)Activator.CreateInstance(componentType)!;

            var componentContext = new ComponentContext(
                module: handle.Module,
                descriptor: component.Descriptor,
                capabilities: capabilities,
                surface: Surface.Cli,
                properties: new Dictionary<string, object?>());

            await component.InitializeAsync(componentContext);

            var commands = component.CreateCommands(componentContext);
            foreach (var command in commands)
            {
                registry.Register(command);
            }
        }
    }

    // Act - Execute a command
    var testCommand = registry.Resolve("myfeature.status");
    Assert.NotNull(testCommand);

    var commandContext = CreateCommandContext(
        capabilities: capabilities);

    var result = await testCommand.ExecuteAsync(commandContext);

    // Assert
    Assert.True(result.IsSuccess);
}
```

---

## Mocking ICapabilityProvider

### Simple Mock Capability Provider

```csharp
public class MockCapabilityProvider : ICapabilityProvider
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
```

### Using Moq for Capability Provider

```csharp
private ICapabilityProvider CreateMockCapabilities()
{
    var mock = new Mock<ICapabilityProvider>();

    // Setup logger
    var mockLogger = new Mock<ILogger>();
    mock.Setup(c => c.TryGet(out It.Ref<ILogger?>.IsAny))
        .Returns((out ILogger? logger) =>
        {
            logger = mockLogger.Object;
            return true;
        });

    // Setup file system
    var mockFileSystem = new Mock<IFileSystem>();
    mock.Setup(c => c.TryGet(out It.Ref<IFileSystem?>.IsAny))
        .Returns((out IFileSystem? fs) =>
        {
            fs = mockFileSystem.Object;
            return true;
        });

    return mock.Object;
}
```

---

## Testing Async Lifecycle

### Testing Async Initialization with Delays

```csharp
[Fact]
public async Task InitializeAsync_WithAsyncWork_CompletesCorrectly()
{
    // Arrange
    var module = new AsyncInitializingModule();
    var context = CreateMockContext();

    // Act
    var stopwatch = Stopwatch.StartNew();
    await module.InitializeAsync(context);
    stopwatch.Stop();

    // Assert
    Assert.True(stopwatch.ElapsedMilliseconds >= 100); // Async work took time
}
```

### Testing Concurrent Initialization

```csharp
[Fact]
public async Task InitializeAsync_MultipleConcurrentCalls_AreSafe()
{
    // Arrange
    var module = new MyFeatureModule();
    var context = CreateMockContext();

    // Act
    var tasks = Enumerable.Range(0, 10)
        .Select(_ => module.InitializeAsync(context))
        .ToArray();

    // Assert - should not throw
    await Task.WhenAll(tasks);
}
```

---

## Testing Cancellation

### Testing Command Cancellation

```csharp
[Fact]
public async Task ExecuteAsync_WhenCancelled_ReturnsCancelled()
{
    // Arrange
    var command = new LongRunningCommand();
    var context = CreateCommandContext();
    var cts = new CancellationTokenSource();

    // Act
    var task = command.ExecuteAsync(context, cts.Token);
    cts.CancelAfter(TimeSpan.FromMilliseconds(100));

    var result = await task;

    // Assert
    Assert.Equal(CommandResultKind.Cancelled, result.Kind);
}

[Fact]
public async Task ExecuteAsync_RespectsCancellationToken()
{
    // Arrange
    var command = new CancellableCommand();
    var context = CreateCommandContext();
    var cts = new CancellationTokenSource();

    // Act
    var task = command.ExecuteAsync(context, cts.Token);
    cts.Cancel();

    // Assert
    await Assert.ThrowsAsync<OperationCanceledException>(() => task.AsTask());
}
```

### Testing Module Shutdown Cancellation

```csharp
[Fact]
public async Task ShutdownAsync_WithTimeout_CompletesInTime()
{
    // Arrange
    var module = new MyFeatureModule();
    var context = CreateMockContext();

    await module.InitializeAsync(context);

    var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

    // Act & Assert
    await module.ShutdownAsync(context, cts.Token);
    Assert.False(cts.IsCancellationRequested);
}
```

---

## Test Organization

### Test File Structure

Each production file should have a corresponding test file:

```
MyModule/
  MyModule.cs          →  MyModule.Tests/MyModuleTests.cs
  CoreComponent.cs     →  MyModule.Tests/CoreComponentTests.cs
  StatusCommand.cs     →  MyModule.Tests/StatusCommandTests.cs
```

### Test Fixtures

Use fixtures for shared setup:

```csharp
public class ModuleTestFixture : IDisposable
{
    public MyFeatureModule Module { get; }
    public ICapabilityProvider Capabilities { get; }

    public ModuleTestFixture()
    {
        Module = new MyFeatureModule();
        Capabilities = CreateMockCapabilities();
    }

    public void Dispose()
    {
        // Cleanup
    }
}

public class MyModuleTests : IClassFixture<ModuleTestFixture>
{
    private readonly ModuleTestFixture _fixture;

    public MyModuleTests(ModuleTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void Test_UsesFixture()
    {
        var module = _fixture.Module;
        // Test using shared fixture
    }
}
```

---

## Best Practices

### 1. Use Descriptive Test Names

```csharp
// Good
[Fact]
public async Task ExecuteAsync_WithMissingParameter_ReturnsFailureWithMessage()

// Bad
[Fact]
public async Task Test1()
```

### 2. Test One Thing Per Test

```csharp
// Good - tests one behavior
[Fact]
public void Module_HasCorrectId()
{
    var module = new MyModule();
    Assert.Equal("mycompany.myfeature", module.Descriptor.Id);
}

// Bad - tests multiple things
[Fact]
public void Module_IsValid()
{
    var module = new MyModule();
    Assert.NotNull(module.Descriptor);
    Assert.Equal("mycompany.myfeature", module.Descriptor.Id);
    Assert.True(module.Descriptor.Version > new Version(0, 0, 0));
    // Too much in one test
}
```

### 3. Use Theory for Parameterized Tests

```csharp
[Theory]
[InlineData("Alice", "Hello Alice")]
[InlineData("Bob", "Hello Bob")]
[InlineData("", "Hello World")]
public async Task GreetCommand_WithDifferentNames_ReturnsExpectedGreeting(
    string name,
    string expected)
{
    var command = new GreetCommand();
    var parameters = new Dictionary<string, object?> { ["name"] = name };
    var context = CreateCommandContext(parameters);

    var result = await command.ExecuteAsync(context);

    Assert.Contains(expected, result.Message);
}
```

### 4. Clean Up Resources

```csharp
[Fact]
public async Task Test_WithResources()
{
    // Use 'await using' for IAsyncDisposable
    await using var catalog = new ModuleCatalog();

    // Use 'using' for IDisposable
    using var cts = new CancellationTokenSource();

    // Test logic here
}
```

### 5. Test Negative Cases

```csharp
[Fact]
public async Task ExecuteAsync_WithInvalidInput_ReturnsFailure()
{
    // Test what happens when things go wrong
}

[Fact]
public async Task InitializeAsync_WhenCapabilityMissing_HandlesGracefully()
{
    // Test missing optional dependencies
}
```

---

## Common Testing Patterns

### Pattern 1: Test Context Factory

```csharp
public static class TestContextFactory
{
    public static ModuleContext CreateModuleContext(
        ModuleDescriptor? descriptor = null,
        ICapabilityProvider? capabilities = null)
    {
        return new ModuleContext(
            descriptor: descriptor ?? CreateDefaultDescriptor(),
            services: null,
            capabilities: capabilities ?? CreateMockCapabilities(),
            properties: new Dictionary<string, object?>());
    }

    public static ComponentContext CreateComponentContext(
        VisoraModule? module = null,
        ComponentDescriptor? descriptor = null,
        ICapabilityProvider? capabilities = null)
    {
        return new ComponentContext(
            module: module ?? new Mock<VisoraModule>().Object,
            descriptor: descriptor ?? CreateDefaultComponentDescriptor(),
            capabilities: capabilities ?? CreateMockCapabilities(),
            surface: Surface.Cli,
            properties: new Dictionary<string, object?>());
    }

    // Similar methods for CommandContext, etc.
}
```

### Pattern 2: Fluent Assertion Extensions

```csharp
using FluentAssertions;

[Fact]
public async Task ExecuteAsync_ReturnsSuccess()
{
    var result = await command.ExecuteAsync(context);

    result.IsSuccess.Should().BeTrue();
    result.Message.Should().NotBeNullOrEmpty();
    result.Payload.Should().NotBeNull();
}
```

---

## Cross-References

### Related Documentation

- **[Creating Modules](./creating-modules.md)**: Module implementation
- **[Creating Components](./creating-components.md)**: Component implementation
- **[Creating Commands](./creating-commands.md)**: Command implementation
- **[Building Hosts](./building-hosts.md)**: Host implementation
- **[Testable Design Pattern](../patterns/testable-design/visora-analysis.md)**: Design for testability

### Related Decisions

- **[ADR-003: Async Everywhere](../decisions/async-everywhere.md)**: Testing async code

---

**End of Document**
