# Testing Patterns - Multi-Layer Test Strategy

**Pattern Category:** Quality Assurance / Testing
**Complexity:** Medium
**Reusability:** High - applicable to any layered .NET architecture

---

## Pattern Intent

Provide comprehensive test coverage with:
- Unit tests for business logic isolation
- Integration tests for component interactions
- End-to-end tests for complete scenarios
- UI automation (FAT framework) for WPF applications
- Test vectors for parsing validation

## Problem Being Solved

Testing complex multi-layer systems requires:
- Isolating business logic from infrastructure
- Testing component boundaries (gateways, services)
- Validating end-to-end flows without manual testing
- Ensuring parsing correctness across format variations
- Automating UI interactions for WPF applications

## HYDRA Implementation

### Test Organization Structure

```
Hydra.Tests/
├── Unit/                           # Isolated component tests
│   ├── Visor/
│   │   └── EchoToolTests.cs
│   ├── VisorGateway/
│   │   ├── ServiceDispatchTests.cs
│   │   ├── GatewayParsingIntegrationTests.cs
│   │   └── PulseSchedulerTests.cs
│   ├── Backends/
│   │   └── ServiceRegistryAndRouterTests.cs
│   ├── Gateway/
│   │   └── DeterministicWriterTests.cs
│   └── Routing/
│
├── Integration/                    # Cross-component tests
│   └── [Various integration scenarios]
│
├── Visor/                          # End-to-end VISOR tests
│   └── VisorEndToEndTests.cs
│
├── VisorParsingTests.cs            # Parsing validation
└── VisorParsing_DiagnosticTests.cs # Edge cases & errors
```

---

### Pattern 1: Unit Tests - Business Logic Isolation

**Example:** `Hydra.Tests/Unit/Visor/EchoToolTests.cs`

```csharp
[Trait("Category", "Unit")]
public class EchoToolTests
{
    [Fact]
    public async Task EchoTool_WithSimpleInput_ReturnsEcho()
    {
        // Arrange
        var tool = new EchoTool();
        var parameters = new Dictionary<string, object?>
        {
            ["text"] = "hello"
        };

        // Act
        var result = await tool.InvokeAsync(parameters, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        var echoResult = Assert.IsType<EchoResult>(result);
        Assert.Equal("hello", echoResult.Echo);
    }

    [Fact]
    public async Task EchoTool_WithNullInput_ThrowsArgumentException()
    {
        // Arrange
        var tool = new EchoTool();
        var parameters = new Dictionary<string, object?>();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => tool.InvokeAsync(parameters, CancellationToken.None));
    }
}
```

**Characteristics:**
- No external dependencies (DB, network, filesystem)
- Fast execution (< 10ms per test)
- Focused on single component
- Arrange-Act-Assert pattern

---

### Pattern 2: Integration Tests - Component Boundaries

**Example:** `Hydra.Tests/Unit/Backends/ServiceRegistryAndRouterTests.cs` (365 lines)

```csharp
[Trait("Category", "Integration")]
public class ServiceRegistryAndRouterTests
{
    [Fact]
    public async Task ServiceRegistry_DiscoverServices_FindsAttributedServices()
    {
        // Arrange
        var registry = new ServiceRegistry();

        // Act
        registry.DiscoverServices();  // Scans assembly for [HydraService]

        // Assert
        var services = registry.GetAllServiceNames();
        Assert.Contains("HydraCoreService", services);
    }

    [Fact]
    public async Task ServiceRouter_InvokeAsync_ValidRequest_ReturnsResult()
    {
        // Arrange
        var registry = new ServiceRegistry();
        registry.Register("TestService", typeof(TestService));

        var router = new ServiceRouter(registry);
        var connection = new HydraConnection { ConnectionId = "test-conn" };
        var session = CreateTestSession();  // Unique session ID per test

        // Act
        var result = await router.InvokeAsync(
            connection,
            session,
            "TestService",
            "testFunction",
            JToken.FromObject(new { input = "test" }),
            CancellationToken.None);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ServiceRouter_PerSessionInstance_SameSessionReturnsSameInstance()
    {
        // Arrange
        var registry = new ServiceRegistry();
        registry.Register("Counter", typeof(CounterService));

        var router = new ServiceRouter(registry);
        var session = CreateTestSession();

        // Act - Invoke twice with same session
        var result1 = await router.InvokeAsync(/* sessionId: "sess1" */);
        var result2 = await router.InvokeAsync(/* sessionId: "sess1" */);

        // Assert - State persists (counter incremented)
        Assert.Equal(1, ((dynamic)result1).count);
        Assert.Equal(2, ((dynamic)result2).count);  // Same instance
    }

    private HydraSession CreateTestSession()
    {
        // CRITICAL: Unique session ID per test to avoid cross-test pollution
        return new HydraSession
        {
            Session = new Session { SessionId = $"test-session-{Guid.NewGuid()}" }
        };
    }
}
```

**Characteristics:**
- Tests multiple components together (Registry + Router + Service)
- May use real DI container (Lamar)
- Tests component contracts and boundaries
- **Critical:** Unique session IDs to avoid static registry pollution

---

### Pattern 3: End-to-End Tests - Complete Scenarios

**Example:** `Hydra.Tests/Visor/VisorEndToEndTests.cs`

```csharp
[Trait("Category", "E2E")]
public class VisorEndToEndTests
{
    [Fact]
    public async Task VisorE2E_EchoTool_CompleteRoundTrip()
    {
        // Arrange: Start gateway and service
        var gateway = new VisorGateway(new VisorDeterministicWriter(), port: 7778);
        var service = new VisorHydraService();

        gateway.EnvelopesReceived += async (sender, e) =>
        {
            // Dispatch to service
            foreach (var envelope in e.Envelopes)
            {
                var response = await service.HandleAsync(envelope);
                gateway.EnqueueEnvelope(e.ConnectionId, response);
            }
        };

        await gateway.StartAsync(CancellationToken.None);

        // Act: Connect client, send VISOR message
        using var client = new ClientWebSocket();
        await client.ConnectAsync(new Uri("ws://localhost:7778/visor"), CancellationToken.None);

        var visorMessage = """
        ```VISOR json1s
        mcp({ "method": "echo", "params": { "text": "hello" } })
        ```
        """;

        await client.SendAsync(
            Encoding.UTF8.GetBytes(visorMessage),
            WebSocketMessageType.Text,
            endOfMessage: true,
            CancellationToken.None);

        // Assert: Receive response
        var buffer = new byte[4096];
        var result = await client.ReceiveAsync(buffer, CancellationToken.None);
        var response = Encoding.UTF8.GetString(buffer, 0, result.Count);

        Assert.Contains("hello", response);

        // Cleanup
        await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
        await gateway.StopAsync(CancellationToken.None);
    }
}
```

**Characteristics:**
- Tests complete flow: Client → Gateway → Service → Gateway → Client
- Uses real network (WebSocket)
- May take longer (100-500ms)
- Requires cleanup (stop gateway, close connections)

---

### Pattern 4: Parsing Tests with Test Vectors

**Example:** `Hydra.Tests/VisorParsingTests.cs`

```csharp
public class VisorParsingTests
{
    [Theory]
    [InlineData("mcp({ \"method\": \"tools/list\" })", "tools/list")]
    [InlineData("mcp({ \"method\": \"echo\", \"params\": { \"text\": \"hello\" } })", "echo")]
    public void Json1sParser_ValidMcpCall_ParsesCorrectly(string input, string expectedMethod)
    {
        // Act
        var envelopes = Json1sParser.Parse("mcp(", input);

        // Assert
        Assert.Single(envelopes);
        var payload = envelopes[0].Payload;
        Assert.Equal(expectedMethod, payload.GetProperty("method").GetString());
    }

    [Fact]
    public void VBlockPreParser_MultipleFencedBlocks_ExtractsAll()
    {
        // Arrange
        var message = """
        Chat text.

        ```VISOR json1s
        mcp({ "method": "tools/list" })
        ```

        More chat.

        ```VISOR json1s
        mcp({ "method": "echo" })
        ```
        """;

        // Act
        var blocks = VBlockPreParser.Extract(message);

        // Assert
        Assert.Equal(2, blocks.Count);
    }

    [Fact]
    public void VBlockPreParser_UnbalancedBraces_ReturnsPartialResults()
    {
        // Arrange
        var message = """
        ```VISOR json1s
        mcp({ "method": "bad", "params": { "x": 1 )  // Mismatched parens
        ```
        """;

        // Act
        var blocks = VBlockPreParser.Extract(message);

        // Assert - Graceful degradation
        Assert.Empty(blocks);  // Invalid block skipped, no exception thrown
    }
}
```

**Characteristics:**
- Theory tests with multiple input variations
- Tests edge cases (malformed input, empty input, special characters)
- Validates error recovery (partial results, no exceptions)
- Fast (pure parsing, no I/O)

---

### Pattern 5: FAT Framework - UI Automation

**Location:** `Platform/Infrastructures/Quality Control/Automated Runtime Testing Framework/FAT/`

**Purpose:** Automate WPF UI interactions without manual testing

**Example Scenario:** `FAT/Scenarios/Visor/Visor_Echo_Scenario.cs`

```csharp
public class Visor_Echo_Scenario : FATScenarioBase
{
    public override async Task RunAsync()
    {
        // 1. Wait for main window
        var mainWindow = await WaitForWindow("Hydra Main Window", timeoutSeconds: 10);

        // 2. Click "Start VISOR Gateway" button
        var startButton = FindButton(mainWindow, "StartVisorGatewayButton");
        await ClickButton(startButton);

        // 3. Wait for gateway to start (check status label)
        await WaitForText(mainWindow, "StatusLabel", "Gateway Running", timeoutSeconds: 5);

        // 4. Send test message via browser
        var browser = FindControl<Browser>(mainWindow, "ChatGPTBrowser");
        await browser.InjectVisorMessage("mcp({ \"method\": \"echo\", \"params\": { \"text\": \"test\" } })");

        // 5. Wait for response in logs
        var logsWindow = await WaitForWindow("Logs", timeoutSeconds: 5);
        await WaitForLogEntry(logsWindow, "VISOR-ECHO", "test", timeoutSeconds: 10);

        // 6. Verify success
        Assert.True(IsVisorGatewayRunning(mainWindow));
    }
}
```

**FAT Framework Features:**
- Window discovery and waiting
- Control finding by name/type
- Simulated clicks, text input
- Log monitoring
- Screenshot on failure
- Parallel scenario execution

**Challenges:**
- Flaky (timing-sensitive)
- Requires UI thread access
- Slow (seconds per scenario)
- Hard to debug

---

### Pattern 6: Test Vectors for Parsing

**Location:** `pipeline/vectors/` (test data)

**Purpose:** Reusable test cases for parser validation

**Example Vector File:** `pipeline/vectors/visor-basic.json`

```json
{
  "name": "Basic VISOR Parsing",
  "vectors": [
    {
      "input": "```VISOR json1s\nmcp({ \"method\": \"tools/list\" })\n```",
      "expected": {
        "blockCount": 1,
        "envelopeCount": 1,
        "op": "mcp",
        "payloadMethod": "tools/list"
      }
    },
    {
      "input": "```VISOR json1s\nmcp({ \"method\": \"echo\", \"params\": { \"text\": \"hello\" } })\n```",
      "expected": {
        "blockCount": 1,
        "envelopeCount": 1,
        "op": "mcp",
        "payloadMethod": "echo"
      }
    }
  ]
}
```

**Usage:** `Tools/VisorParseCli` loads vectors and runs parser against each

---

## Key Testing Principles

### 1. Isolation via Unique Session IDs
**Problem:** Static per-session instance registry causes test pollution

**Solution:** Generate unique session ID per test

```csharp
// ❌ WRONG: Reused session ID across tests
var session = new HydraSession { Session = new Session { SessionId = "test" } };

// ✅ CORRECT: Unique session ID per test
var session = new HydraSession { Session = new Session { SessionId = $"test-{Guid.NewGuid()}" } };
```

**Why:** Service instances cached per session. Reused session ID = shared state across tests.

### 2. Arrange-Act-Assert Pattern
**Structure every test:**
```csharp
[Fact]
public async Task TestName()
{
    // Arrange: Setup test data, mocks, dependencies
    var service = new MyService();
    var input = "test";

    // Act: Execute the system under test
    var result = await service.ProcessAsync(input);

    // Assert: Verify outcome
    Assert.Equal("expected", result);
}
```

### 3. Test Naming Convention
**Pattern:** `[MethodName]_[Scenario]_[ExpectedBehavior]`

**Examples:**
- `EchoTool_WithSimpleInput_ReturnsEcho`
- `ServiceRouter_InvalidServiceName_ThrowsException`
- `VBlockPreParser_UnbalancedBraces_ReturnsPartialResults`

### 4. Trait-Based Organization
**Use:** `[Trait("Category", "...")]` for test filtering

```csharp
[Trait("Category", "Unit")]       // Fast, isolated
[Trait("Category", "Integration")]  // Component boundaries
[Trait("Category", "E2E")]         // Complete scenarios
[Trait("Category", "FAT")]         // UI automation
```

**Run specific category:** `dotnet test --filter "Category=Unit"`

### 5. Theory Tests for Variations
**Use:** `[Theory]` + `[InlineData]` for multiple inputs

```csharp
[Theory]
[InlineData("", "mcp")]  // Empty input
[InlineData("{ }", "mcp")]  // Empty object
[InlineData("{ \"method\": \"tools/list\" }", "mcp")]  // Valid
public void Json1sParser_VariousInputs_ParsesCorrectly(string input, string expectedOp)
{
    var envelopes = Json1sParser.Parse("mcp(", input);
    Assert.Equal(expectedOp, envelopes[0].Op);
}
```

---

## Reproducing This Pattern in Other .NET Projects

### Step 1: Organize Test Structure

```
YourProject.Tests/
├── Unit/           # Fast, isolated tests
├── Integration/    # Component boundary tests
├── E2E/            # Complete scenario tests
└── Vectors/        # Test data files
```

### Step 2: Write Unit Test Template

```csharp
[Trait("Category", "Unit")]
public class MyServiceTests
{
    [Fact]
    public async Task MyMethod_ValidInput_ReturnsExpected()
    {
        // Arrange
        var service = new MyService();

        // Act
        var result = await service.MyMethodAsync("test");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("expected", result.Value);
    }

    [Fact]
    public async Task MyMethod_InvalidInput_ThrowsException()
    {
        // Arrange
        var service = new MyService();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => service.MyMethodAsync(null!));
    }
}
```

### Step 3: Write Integration Test with DI

```csharp
[Trait("Category", "Integration")]
public class ServiceIntegrationTests : IDisposable
{
    private readonly IServiceProvider _serviceProvider;

    public ServiceIntegrationTests()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IMyRepository, InMemoryRepository>();
        services.AddScoped<MyService>();

        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task Service_WithRepository_PersistsData()
    {
        // Arrange
        var service = _serviceProvider.GetRequiredService<MyService>();

        // Act
        await service.SaveAsync(new Data { Id = "1", Value = "test" });
        var result = await service.LoadAsync("1");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("test", result.Value);
    }

    public void Dispose()
    {
        (_serviceProvider as IDisposable)?.Dispose();
    }
}
```

### Step 4: Write E2E Test

```csharp
[Trait("Category", "E2E")]
public class ApiE2ETests
{
    [Fact]
    public async Task API_PostAndGet_ReturnsData()
    {
        // Arrange: Start in-memory test server
        var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();

        // Act: POST data
        var postResponse = await client.PostAsJsonAsync("/api/data", new { value = "test" });
        postResponse.EnsureSuccessStatusCode();

        var id = await postResponse.Content.ReadFromJsonAsync<string>();

        // Act: GET data
        var getResponse = await client.GetAsync($"/api/data/{id}");
        var data = await getResponse.Content.ReadFromJsonAsync<DataDto>();

        // Assert
        Assert.Equal("test", data?.Value);
    }
}
```

---

## Tradeoffs & Constraints

### Advantages
✅ Multi-layer coverage (unit → integration → E2E)
✅ Fast feedback (unit tests < 1 second)
✅ Isolated tests (unique session IDs)
✅ Reusable test vectors
✅ UI automation (FAT framework)

### Limitations
⚠️ FAT tests flaky (timing-sensitive)
⚠️ E2E tests slow (seconds per test)
⚠️ Test maintenance overhead (update tests with code)
⚠️ Per-session caching complicates testing

### When NOT to Use This Pattern
❌ Simple libraries (unit tests sufficient)
❌ No UI (skip FAT framework)
❌ Stateless services (session isolation unnecessary)

---

## Related Patterns

- **Test Pyramid:** More unit tests, fewer integration tests, fewest E2E tests
- **Arrange-Act-Assert:** Standard test structure
- **Test Doubles:** Mocks, stubs, fakes for isolation
- **Page Object Model:** FAT framework uses this for UI abstraction

---

## Gen2 Evolution Notes

**Current (Gen1):** xUnit, FAT framework, manual test vectors

**Future (Gen2):**
- Property-based testing (FsCheck) for edge case generation
- Contract testing (Pact) for service boundaries
- Snapshot testing for regression detection
- Performance benchmarks (BenchmarkDotNet)

**Migration Strategy:**
- Keep test structure (Unit/Integration/E2E)
- Add property-based tests for parsers
- Replace manual vectors with generated ones

---

**Last Updated:** 2025-11-10
**Pattern Stability:** High - test organization patterns are stable
**Code References:** Hydra.Tests/ directory structure, various test files
