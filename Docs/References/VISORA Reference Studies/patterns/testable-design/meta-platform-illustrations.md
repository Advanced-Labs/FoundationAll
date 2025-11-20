# Testable Design - Meta-Platform Illustrations

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
2. [Testing Cross-Runtime Code](#testing-cross-runtime-code)
3. [Mock Runtime Implementations](#mock-runtime-implementations)
4. [Python Testing Patterns](#python-testing-patterns)
5. [Node.js Testing Patterns](#nodejs-testing-patterns)
6. [Integration Testing Strategies](#integration-testing-strategies)
7. [Challenges & Considerations](#challenges--considerations)
8. [Possibilities & Future Directions](#possibilities--future-directions)

---

## Conceptual Adaptation

### From Single-Runtime Testing to Cross-Runtime Testing

**VISORA's .NET Testing Pattern:**
```csharp
// Mock capability provider
var mockCapabilities = new Mock<ICapabilityProvider>();

// Test module in isolation
var module = new TestModule();
await module.InitializeAsync(context);

// Verify interactions
mockCapabilities.Verify(c => c.TryGet<ILogger>(out It.Ref<ILogger>.IsAny));
```

**Conceptual Meta-Platform Adaptation:**
```
┌──────────────────────────────────────────────────────┐
│        Multi-Runtime Testing Challenges              │
│                                                      │
│  1. Unit Tests (Per Runtime)                        │
│     • Python: unittest, pytest, mock                │
│     • Node.js: Jest, Mocha, Sinon                   │
│     • .NET: MSTest, xUnit, Moq                      │
│                                                      │
│  2. Integration Tests (Cross-Runtime)               │
│     • Test runtime bridge marshalling               │
│     • Verify protocol compliance                    │
│     • Mock remote runtime responses                 │
│                                                      │
│  3. E2E Tests (Full System)                         │
│     • All runtimes running together                 │
│     • Real modules in each runtime                  │
│     • Network/IPC communication                     │
└──────────────────────────────────────────────────────┘
```

---

## Testing Cross-Runtime Code

### ⚠️ ILLUSTRATIVE EXAMPLE: Testing Runtime Bridge

**Conceptual Test Strategy:**

```csharp
/// <summary>
/// Integration test for cross-runtime module loading.
/// ILLUSTRATIVE EXAMPLE - demonstrates testing cross-runtime scenarios.
/// </summary>
[TestClass]
public class CrossRuntimeIntegrationTests
{
    [TestMethod]
    public async Task RuntimeBridge_CanLoadPythonModule()
    {
        // Arrange: Set up Python runtime mock
        var pythonRuntime = new MockPythonRuntime();
        pythonRuntime.RegisterModule("test_module.py", new
        {
            id = "test.python.module",
            name = "Test Python Module",
            version = "1.0.0"
        });

        var bridge = new RuntimeBridge();
        bridge.RegisterRuntime(ModuleRuntime.Python, pythonRuntime);

        // Act: Load Python module through bridge
        var handle = await bridge.LoadModuleAsync(
            "test_module.py",
            ModuleRuntime.Python,
            CancellationToken.None);

        // Assert: Module loaded correctly
        Assert.IsNotNull(handle);
        Assert.AreEqual("test.python.module", handle.Descriptor.Id);
        Assert.AreEqual(ModuleRuntime.Python, handle.Runtime);
    }

    [TestMethod]
    public async Task RuntimeBridge_InitializeAsync_MarshalsProperly()
    {
        // Arrange
        var pythonRuntime = new MockPythonRuntime();
        var bridge = new RuntimeBridge();
        bridge.RegisterRuntime(ModuleRuntime.Python, pythonRuntime);

        var handle = await bridge.LoadModuleAsync(
            "test_module.py",
            ModuleRuntime.Python,
            CancellationToken.None);

        var context = new ModuleContext(
            descriptor: handle.Descriptor,
            services: null,
            capabilities: CapabilityProviders.Empty);

        // Act: Call initialize through bridge (marshals to Python)
        await handle.InitializeAsync(context, CancellationToken.None);

        // Assert: Initialize was called in Python runtime
        Assert.IsTrue(pythonRuntime.InitializeCalled);
        Assert.AreEqual(1, pythonRuntime.InitializeCallCount);
    }

    [TestMethod]
    public async Task RuntimeBridge_HandlesSerializationErrors()
    {
        // Arrange: Mock runtime that fails to serialize
        var failingRuntime = new FailingMockRuntime();
        var bridge = new RuntimeBridge();
        bridge.RegisterRuntime(ModuleRuntime.Python, failingRuntime);

        var handle = await bridge.LoadModuleAsync(
            "test_module.py",
            ModuleRuntime.Python,
            CancellationToken.None);

        var context = new ModuleContext(
            descriptor: handle.Descriptor,
            services: null,
            capabilities: CapabilityProviders.Empty);

        // Act & Assert: Should handle serialization error gracefully
        await Assert.ThrowsExceptionAsync<SerializationException>(async () =>
        {
            await handle.InitializeAsync(context, CancellationToken.None);
        });
    }
}

/// <summary>
/// Mock Python runtime for testing.
/// </summary>
public sealed class MockPythonRuntime : IModuleRuntime
{
    private readonly Dictionary<string, object> _modules = new();

    public bool InitializeCalled { get; private set; }
    public int InitializeCallCount { get; private set; }

    public void RegisterModule(string path, object descriptor)
    {
        _modules[path] = descriptor;
    }

    public Task<ModuleDescriptor> LoadModuleAsync(
        string path,
        CancellationToken ct)
    {
        if (!_modules.TryGetValue(path, out var descriptor))
            throw new FileNotFoundException($"Module not found: {path}");

        // Simulate loading by deserializing descriptor
        var desc = (dynamic)descriptor;
        return Task.FromResult(new ModuleDescriptor(
            Id: desc.id,
            Name: desc.name,
            Version: Version.Parse(desc.version),
            Description: null,
            Tags: null,
            RuntimeHints: null
        ));
    }

    public Task InitializeModuleAsync(
        string moduleId,
        string contextJson,
        CancellationToken ct)
    {
        InitializeCalled = true;
        InitializeCallCount++;

        // Simulate Python module initialization
        // In real implementation, would call Python via IPC or Python.NET

        return Task.CompletedTask;
    }

    // ... other IModuleRuntime methods
}
```

---

## Mock Runtime Implementations

### ⚠️ ILLUSTRATIVE EXAMPLE: Test Doubles for Runtimes

**Strategy: Create lightweight runtime mocks for testing the bridge:**

```csharp
/// <summary>
/// Fake Python runtime that returns predictable results.
/// </summary>
public sealed class FakePythonRuntime : IModuleRuntime
{
    private readonly Dictionary<string, FakeModule> _modules = new();

    public void AddModule(string id, string path)
    {
        _modules[path] = new FakeModule
        {
            Id = id,
            Name = $"Fake {id}",
            Version = "1.0.0",
            Path = path
        };
    }

    public Task<ModuleDescriptor> LoadModuleAsync(string path, CancellationToken ct)
    {
        if (!_modules.TryGetValue(path, out var module))
            return Task.FromException<ModuleDescriptor>(
                new FileNotFoundException($"Module not found: {path}"));

        return Task.FromResult(new ModuleDescriptor(
            Id: module.Id,
            Name: module.Name,
            Version: Version.Parse(module.Version),
            Description: null,
            Tags: null,
            RuntimeHints: null
        ));
    }

    public Task InitializeModuleAsync(
        string moduleId,
        string contextJson,
        CancellationToken ct)
    {
        // Find module
        var module = _modules.Values.FirstOrDefault(m => m.Id == moduleId);
        if (module == null)
            throw new InvalidOperationException($"Module not loaded: {moduleId}");

        // Mark as initialized
        module.IsInitialized = true;

        return Task.CompletedTask;
    }

    public Task<string> InspectModuleAsync(
        string moduleId,
        CancellationToken ct)
    {
        var module = _modules.Values.FirstOrDefault(m => m.Id == moduleId);
        if (module == null)
            throw new InvalidOperationException($"Module not loaded: {moduleId}");

        // Return fake inspection JSON
        return Task.FromResult($$"""
        {
            "assemblyPath": "{{module.Path}}",
            "descriptor": {
                "id": "{{module.Id}}",
                "name": "{{module.Name}}",
                "version": "{{module.Version}}"
            },
            "components": []
        }
        """);
    }

    private class FakeModule
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public bool IsInitialized { get; set; }
    }
}

// Usage in tests
[TestMethod]
public async Task PolyglotCatalog_CanDiscoverPythonModules()
{
    // Arrange
    var fakePython = new FakePythonRuntime();
    fakePython.AddModule("test.python", "/modules/test.py");

    var catalog = new PolyglotModuleCatalog();
    catalog.RegisterRuntime(ModuleRuntime.Python, fakePython);

    var options = new PolyglotModuleCatalogOptions
    {
        EnabledRuntimes = { ModuleRuntime.Python }
    };

    // Act
    await catalog.DiscoverAsync(options);

    // Assert
    Assert.AreEqual(1, catalog.GetModules(ModuleRuntime.Python).Count);
}
```

---

## Python Testing Patterns

### ⚠️ ILLUSTRATIVE EXAMPLE: Python Unit Tests with unittest.mock

**Python testing using unittest and mock:**

```python
"""
Python unit tests for VISORA modules.
ILLUSTRATIVE EXAMPLE - demonstrates Python testing patterns.
"""
import unittest
from unittest.mock import Mock, AsyncMock, patch, MagicMock
import asyncio

from visora import VisoraModule, ModuleDescriptor, ModuleContext
from visora.capabilities import ICapabilityProvider


class TestVisoraModule(unittest.IsolatedAsyncioTestCase):
    """Test case for VISORA modules in Python."""

    def setUp(self):
        """Set up test fixtures."""
        self.mock_capabilities = Mock(spec=ICapabilityProvider)
        self.context = ModuleContext(
            descriptor=ModuleDescriptor("test", "Test", "1.0.0"),
            capabilities=self.mock_capabilities,
            services=None,
            properties={}
        )

    async def test_module_initialize_async_called(self):
        """Test that initialize_async is called."""
        # Arrange
        module = TestModule()

        # Act
        await module.initialize_async(self.context)

        # Assert
        self.assertTrue(module.initialize_called)

    async def test_module_uses_capability(self):
        """Test that module can access capabilities."""
        # Arrange
        mock_logger = Mock()
        mock_logger.log = Mock()

        self.mock_capabilities.try_get = Mock(return_value=mock_logger)

        module = LoggingModule()

        # Act
        await module.initialize_async(self.context)

        # Assert
        self.mock_capabilities.try_get.assert_called_once()
        mock_logger.log.assert_called_once()

    async def test_module_shutdown_cleans_up(self):
        """Test that shutdown cleans up resources."""
        # Arrange
        module = StatefulModule()
        await module.initialize_async(self.context)
        self.assertTrue(module.is_initialized)

        # Act
        await module.shutdown_async(self.context)

        # Assert
        self.assertFalse(module.is_initialized)

    @patch('my_module.some_external_service')
    async def test_module_with_external_dependency(self, mock_service):
        """Test module with mocked external dependency."""
        # Arrange
        mock_service.connect = AsyncMock(return_value=True)
        module = ExternalDependencyModule()

        # Act
        await module.initialize_async(self.context)

        # Assert
        mock_service.connect.assert_called_once()


class TestModule(VisoraModule):
    """Test module implementation."""

    def __init__(self):
        self._descriptor = ModuleDescriptor(
            id="test.module",
            name="Test Module",
            version="1.0.0"
        )
        self.initialize_called = False

    @property
    def descriptor(self):
        return self._descriptor

    async def initialize_async(self, context, cancellation_token=None):
        self.initialize_called = True


class LoggingModule(VisoraModule):
    """Module that uses logger capability."""

    def __init__(self):
        self._descriptor = ModuleDescriptor(
            id="logging.module",
            name="Logging Module",
            version="1.0.0"
        )

    @property
    def descriptor(self):
        return self._descriptor

    async def initialize_async(self, context, cancellation_token=None):
        logger = context.capabilities.try_get(ILogger)
        if logger:
            logger.log("Initializing...")


class StatefulModule(VisoraModule):
    """Module with state that can be tested."""

    def __init__(self):
        self._descriptor = ModuleDescriptor(
            id="stateful.module",
            name="Stateful Module",
            version="1.0.0"
        )
        self.is_initialized = False

    @property
    def descriptor(self):
        return self._descriptor

    async def initialize_async(self, context, cancellation_token=None):
        self.is_initialized = True

    async def shutdown_async(self, context, cancellation_token=None):
        self.is_initialized = False


# Run tests
if __name__ == '__main__':
    unittest.main()
```

### ⚠️ ILLUSTRATIVE EXAMPLE: Python Tests with pytest

**Using pytest for more concise tests:**

```python
"""
Python tests using pytest.
ILLUSTRATIVE EXAMPLE - demonstrates pytest patterns.
"""
import pytest
from unittest.mock import Mock, AsyncMock
import asyncio

from visora import VisoraModule, ModuleDescriptor, ModuleContext


@pytest.fixture
def mock_capabilities():
    """Fixture: Mock capability provider."""
    return Mock()


@pytest.fixture
def module_context(mock_capabilities):
    """Fixture: Module context for tests."""
    return ModuleContext(
        descriptor=ModuleDescriptor("test", "Test", "1.0.0"),
        capabilities=mock_capabilities,
        services=None,
        properties={}
    )


@pytest.mark.asyncio
async def test_module_initialize_called(module_context):
    """Test that initialize is called."""
    # Arrange
    module = TestModule()

    # Act
    await module.initialize_async(module_context)

    # Assert
    assert module.initialize_called


@pytest.mark.asyncio
async def test_module_with_capability(module_context, mock_capabilities):
    """Test module using capability."""
    # Arrange
    mock_logger = Mock()
    mock_capabilities.try_get.return_value = mock_logger

    module = LoggingModule()

    # Act
    await module.initialize_async(module_context)

    # Assert
    mock_capabilities.try_get.assert_called_once()
    mock_logger.log.assert_called_once_with("Initializing...")


@pytest.mark.asyncio
async def test_module_lifecycle(module_context):
    """Test full module lifecycle."""
    # Arrange
    module = StatefulModule()

    # Act & Assert: Initialize
    await module.initialize_async(module_context)
    assert module.is_initialized

    # Act & Assert: Shutdown
    await module.shutdown_async(module_context)
    assert not module.is_initialized


@pytest.mark.parametrize("module_id,expected_name", [
    ("test.module1", "Module 1"),
    ("test.module2", "Module 2"),
    ("test.module3", "Module 3"),
])
def test_module_descriptor_formats(module_id, expected_name):
    """Parameterized test for descriptor validation."""
    descriptor = ModuleDescriptor(module_id, expected_name, "1.0.0")
    assert descriptor.id == module_id
    assert descriptor.name == expected_name
```

---

## Node.js Testing Patterns

### ⚠️ ILLUSTRATIVE EXAMPLE: TypeScript Tests with Jest

**Jest testing patterns for TypeScript:**

```typescript
/**
 * Jest unit tests for VISORA modules in TypeScript.
 * ILLUSTRATIVE EXAMPLE - demonstrates Jest testing patterns.
 */
import { VisoraModule } from '../src/module';
import { ModuleDescriptor, createModuleDescriptor } from '../src/descriptors';
import { ModuleContext } from '../src/context';
import { ICapabilityProvider } from '../src/capabilities';

describe('VisoraModule', () => {
    let mockCapabilities: jest.Mocked<ICapabilityProvider>;
    let moduleContext: ModuleContext;

    beforeEach(() => {
        // Set up mocks
        mockCapabilities = {
            tryGet: jest.fn(),
            getOptional: jest.fn(),
            getRequired: jest.fn(),
        };

        // Create test context
        moduleContext = {
            descriptor: createModuleDescriptor({
                id: 'test.module',
                name: 'Test Module',
                version: '1.0.0',
            }),
            capabilities: mockCapabilities,
            services: null,
            properties: {},
        };
    });

    describe('initializeAsync', () => {
        it('should be called successfully', async () => {
            // Arrange
            const module = new TestModule();

            // Act
            await module.initializeAsync(moduleContext);

            // Assert
            expect(module.initializeCalled).toBe(true);
        });

        it('should access capabilities during initialization', async () => {
            // Arrange
            const mockLogger = { log: jest.fn() };
            mockCapabilities.tryGet.mockReturnValue(mockLogger);

            const module = new LoggingModule();

            // Act
            await module.initializeAsync(moduleContext);

            // Assert
            expect(mockCapabilities.tryGet).toHaveBeenCalledTimes(1);
            expect(mockLogger.log).toHaveBeenCalledWith('Initializing...');
        });

        it('should handle initialization errors', async () => {
            // Arrange
            const module = new FailingModule();

            // Act & Assert
            await expect(
                module.initializeAsync(moduleContext)
            ).rejects.toThrow('Initialization failed');
        });
    });

    describe('shutdownAsync', () => {
        it('should clean up resources', async () => {
            // Arrange
            const module = new StatefulModule();
            await module.initializeAsync(moduleContext);
            expect(module.isInitialized).toBe(true);

            // Act
            await module.shutdownAsync(moduleContext);

            // Assert
            expect(module.isInitialized).toBe(false);
        });
    });

    describe('lifecycle', () => {
        it('should complete full lifecycle successfully', async () => {
            // Arrange
            const module = new TestModule();

            // Act & Assert: Initialize
            await module.initializeAsync(moduleContext);
            expect(module.initializeCalled).toBe(true);

            // Act & Assert: Shutdown
            await module.shutdownAsync(moduleContext);
            expect(module.shutdownCalled).toBe(true);

            // Act & Assert: Dispose
            await module.disposeAsync();
            expect(module.disposeCalled).toBe(true);
        });
    });
});

// Test module implementations
class TestModule extends VisoraModule {
    private readonly _descriptor: ModuleDescriptor;
    public initializeCalled = false;
    public shutdownCalled = false;
    public disposeCalled = false;

    constructor() {
        super();
        this._descriptor = createModuleDescriptor({
            id: 'test.module',
            name: 'Test Module',
            version: '1.0.0',
        });
    }

    get descriptor(): ModuleDescriptor {
        return this._descriptor;
    }

    async initializeAsync(context: ModuleContext): Promise<void> {
        this.initializeCalled = true;
    }

    async shutdownAsync(context: ModuleContext): Promise<void> {
        this.shutdownCalled = true;
    }

    async disposeAsync(): Promise<void> {
        this.disposeCalled = true;
    }
}

class LoggingModule extends VisoraModule {
    private readonly _descriptor: ModuleDescriptor;

    constructor() {
        super();
        this._descriptor = createModuleDescriptor({
            id: 'logging.module',
            name: 'Logging Module',
            version: '1.0.0',
        });
    }

    get descriptor(): ModuleDescriptor {
        return this._descriptor;
    }

    async initializeAsync(context: ModuleContext): Promise<void> {
        const logger = context.capabilities.tryGet(ILogger);
        logger?.log('Initializing...');
    }
}

class StatefulModule extends VisoraModule {
    private readonly _descriptor: ModuleDescriptor;
    public isInitialized = false;

    constructor() {
        super();
        this._descriptor = createModuleDescriptor({
            id: 'stateful.module',
            name: 'Stateful Module',
            version: '1.0.0',
        });
    }

    get descriptor(): ModuleDescriptor {
        return this._descriptor;
    }

    async initializeAsync(context: ModuleContext): Promise<void> {
        this.isInitialized = true;
    }

    async shutdownAsync(context: ModuleContext): Promise<void> {
        this.isInitialized = false;
    }
}

class FailingModule extends VisoraModule {
    private readonly _descriptor: ModuleDescriptor;

    constructor() {
        super();
        this._descriptor = createModuleDescriptor({
            id: 'failing.module',
            name: 'Failing Module',
            version: '1.0.0',
        });
    }

    get descriptor(): ModuleDescriptor {
        return this._descriptor;
    }

    async initializeAsync(context: ModuleContext): Promise<void> {
        throw new Error('Initialization failed');
    }
}
```

---

## Integration Testing Strategies

### ⚠️ ILLUSTRATIVE EXAMPLE: Cross-Runtime Integration Tests

**Testing modules across multiple runtimes:**

```csharp
/// <summary>
/// Integration tests for cross-runtime module interaction.
/// ILLUSTRATIVE EXAMPLE.
/// </summary>
[TestClass]
public class CrossRuntimeIntegrationTests
{
    [TestMethod]
    public async Task DotNetModule_CanCallPythonModule()
    {
        // Arrange: Set up both runtimes
        var dotnetRuntime = new DotNetModuleRuntime();
        var pythonRuntime = new PythonModuleRuntime();

        var bridge = new RuntimeBridge();
        bridge.RegisterRuntime(ModuleRuntime.DotNet, dotnetRuntime);
        bridge.RegisterRuntime(ModuleRuntime.Python, pythonRuntime);

        var catalog = new PolyglotModuleCatalog(bridge);

        // Load .NET module
        var dotnetModule = await catalog.LoadModuleAsync(
            "DotNetModule.dll",
            ModuleRuntime.DotNet);

        // Load Python module
        var pythonModule = await catalog.LoadModuleAsync(
            "python_module.py",
            ModuleRuntime.Python);

        // Act: .NET module calls Python module via capability
        var pythonCapability = await bridge.GetCapabilityAsync<IPythonService>(
            pythonModule.Descriptor.Id);

        var result = await pythonCapability.CallMethodAsync(
            "process_data",
            new { input = "test data" });

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("processed", result.status);
    }

    [TestMethod]
    public async Task PolyglotCatalog_DiscoveryIncludesAllRuntimes()
    {
        // Arrange
        var catalog = CreatePolyglotCatalog();

        var options = new PolyglotModuleCatalogOptions
        {
            EnabledRuntimes =
            {
                ModuleRuntime.DotNet,
                ModuleRuntime.Python,
                ModuleRuntime.NodeJs
            }
        };

        options.DotNetOptions.ProbingPaths.Add(GetDotNetModulesPath());
        options.PythonOptions.ProbingPaths.Add(GetPythonModulesPath());
        options.NodeJsOptions.ProbingPaths.Add(GetNodeModulesPath());

        // Act
        await catalog.DiscoverAsync(options);

        // Assert
        Assert.IsTrue(catalog.GetModules(ModuleRuntime.DotNet).Count > 0,
            "Should find .NET modules");
        Assert.IsTrue(catalog.GetModules(ModuleRuntime.Python).Count > 0,
            "Should find Python modules");
        Assert.IsTrue(catalog.GetModules(ModuleRuntime.NodeJs).Count > 0,
            "Should find Node.js modules");
    }

    private PolyglotModuleCatalog CreatePolyglotCatalog()
    {
        var bridge = new RuntimeBridge();
        // Register real or mock runtimes
        return new PolyglotModuleCatalog(bridge);
    }

    private string GetDotNetModulesPath() => /* ... */;
    private string GetPythonModulesPath() => /* ... */;
    private string GetNodeModulesPath() => /* ... */;
}
```

---

## Challenges & Considerations

### 1. Language-Specific Test Frameworks

**Challenge:** Each language has different testing conventions.

**.NET:** MSTest, xUnit, NUnit
**Python:** unittest, pytest, nose
**TypeScript/Node.js:** Jest, Mocha, Jasmine

**Consideration:** Document testing patterns for each language. Provide example test projects.

### 2. Mocking Across Runtimes

**Challenge:** Mocking cross-runtime calls is complex.

**Example:** .NET test mocking Python module

**Solutions:**
- Use test doubles for runtimes (FakePythonRuntime)
- Record/replay pattern for IPC calls
- Contract testing (verify protocol compliance)

### 3. Async Testing Differences

**Challenge:** Each language has different async testing support.

**.NET:** `async Task` tests work naturally
**Python:** Requires `unittest.IsolatedAsyncioTestCase` or pytest-asyncio
**TypeScript/Jest:** Supports `async/await` in tests

**Consideration:** Ensure all test frameworks support async properly.

### 4. Integration Test Environment

**Challenge:** Running all runtimes in CI/CD.

**Considerations:**
- Docker containers with all runtimes
- CI matrix with runtime-specific jobs
- Conditional integration tests (skip if runtime not available)

### 5. Test Data Sharing

**Challenge:** Test data (descriptors, payloads) must work across runtimes.

**Solution:** Use JSON fixtures that all runtimes can consume:

```json
// test-fixtures/module-descriptor.json
{
  "id": "test.module",
  "name": "Test Module",
  "version": "1.0.0",
  "description": "Test module for integration tests"
}
```

All runtimes load the same JSON for consistent test data.

---

## Possibilities & Future Directions

### 1. Contract Testing

Verify that modules in different runtimes implement the same protocol:

```csharp
[TestClass]
public class ModuleContractTests
{
    [TestMethod]
    public async Task PythonModule_ImplementsModuleProtocol()
    {
        // Arrange
        var pythonModule = await LoadPythonModule("test_module.py");

        // Act & Assert: Verify protocol compliance
        ContractValidator.ValidateModuleProtocol(pythonModule);
    }
}

public static class ContractValidator
{
    public static void ValidateModuleProtocol(IModuleHandle module)
    {
        // Check required properties
        Assert.IsNotNull(module.Descriptor);
        Assert.IsFalse(string.IsNullOrEmpty(module.Descriptor.Id));

        // Check lifecycle methods are callable
        // ... (validate all protocol requirements)
    }
}
```

### 2. Property-Based Testing

Use property-based testing (FsCheck, Hypothesis) to test cross-runtime invariants:

```csharp
[TestMethod]
public void ModuleDescriptor_RoundTripsAcrossRuntimes()
{
    Prop.ForAll<string, string, Version>((id, name, version) =>
    {
        // Arrange: Create descriptor in .NET
        var dotnetDescriptor = new ModuleDescriptor(id, name, version, null, null, null);

        // Act: Serialize and deserialize through Python runtime
        var json = JsonSerializer.Serialize(dotnetDescriptor);
        var pythonDescriptor = PythonRuntime.DeserializeDescriptor(json);
        var roundtripJson = PythonRuntime.SerializeDescriptor(pythonDescriptor);
        var roundtripDescriptor = JsonSerializer.Deserialize<ModuleDescriptor>(roundtripJson);

        // Assert: Values are preserved
        return roundtripDescriptor.Id == id &&
               roundtripDescriptor.Name == name &&
               roundtripDescriptor.Version == version;
    }).QuickCheckThrowOnFailure();
}
```

### 3. Mutation Testing

Test the quality of tests themselves:

```bash
# Use Stryker.NET for .NET tests
dotnet stryker

# Use mutpy for Python tests
mutpy --target visora --unit-test tests

# Use Stryker for TypeScript tests
npx stryker run
```

### 4. Snapshot Testing

Verify that module inspection results are consistent:

```typescript
// Jest snapshot testing
it('should match snapshot for module inspection', async () => {
    const module = await loadModule('test_module.js');
    const inspection = await module.inspectAsync();

    expect(inspection).toMatchSnapshot();
});
```

### 5. Performance Testing

Test performance characteristics across runtimes:

```csharp
[TestMethod]
public async Task ModuleInitialization_CompletesWithinTimeout()
{
    // Arrange
    var module = await LoadModule("heavy_module.dll");
    var sw = Stopwatch.StartNew();

    // Act
    await module.InitializeAsync(context);

    // Assert
    sw.Stop();
    Assert.IsTrue(sw.ElapsedMilliseconds < 1000,
        "Initialization should complete within 1 second");
}
```

---

## Summary

A meta-platform adaptation of VISORA's testable design would:

**Per-Runtime Unit Tests:**
- .NET: MSTest/xUnit with Moq
- Python: unittest/pytest with mock
- TypeScript: Jest with mocking

**Cross-Runtime Integration Tests:**
- Test runtime bridge marshalling
- Verify protocol compliance
- Mock runtime implementations

**Key Testing Strategies:**
- Mock capability providers
- Test doubles for modules/components
- Contract testing for protocol compliance
- Integration tests with all runtimes

**Key Insight:** Testable design becomes more critical in a multi-runtime system, as the complexity of interactions increases. Each runtime layer must be independently testable, with clear contracts verified through integration tests.

---

**End of Document**
