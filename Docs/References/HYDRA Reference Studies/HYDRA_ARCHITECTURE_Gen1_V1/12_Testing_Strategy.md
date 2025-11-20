# 12. Testing Strategy

## Test Organization

**Main Test Project:** `/src/Hydra/Hydra.Tests/Hydra.Tests.csproj`
- **Framework:** .NET 9.0
- **Test Framework:** xUnit 2.9.3
- **Mocking:** Moq 4.20.72
- **Total Files:** 38 test files
- **Total Methods:** 319+ tests

## Test Categories

Tests organized using xUnit `[Trait]` attributes:

| Category | Description | Example Files |
|----------|-------------|---------------|
| **Unit** | Fast, isolated with mocks | ServiceRouterTests, ParsingTests |
| **Integration** | Component interactions | GatewayServiceIntegrationTests |
| **Visor** | VISOR protocol end-to-end | VisorEndToEndTests |
| **Vectors** | Acceptance/FAT scenarios | (User flow tests) |

## Key Test Files

### Echo Handler Tests
**File:** `/src/Hydra/Hydra.Tests/Unit/Visor/EchoToolTests.cs` (159 lines)

**Test Coverage:**
1. `Echo_WithSimpleMessage_ReturnsEchoedMessage` - Basic functionality
2. `Echo_WithEmptyString_ReturnsEchoPrefix` - Edge case handling
3. `Echo_WithUnicodeCharacters_ReturnsEchoedUnicode` - Encoding (世界 🌍 Привет)
4. `Echo_WithMultilineString_ReturnsEchoedMultiline` - Multi-line support
5. `Echo_WithSpecialCharacters_ReturnsEchoedSpecialChars` - Tabs, quotes, backslashes
6. `Echo_WithLongMessage_ReturnsEchoedLongMessage` - 10,000 character messages
7. `EchoMcpServer_IsEnabled_ReturnsFalseByDefault` - Environment check
8. `EchoMcpServer_IsEnabled_ReturnsTrueWhenEnvVarSet` - HYDRA_TEST_ECHO=1
9. `EchoMcpServer_IsEnabled_ReturnsFalseWhenEnvVarNotOne` - Validation

### VISOR Parsing Tests
**File:** `/src/Hydra/Hydra.Tests/VisorParsingTests.cs`

**Test Scenarios:**
1. `Parse_HappyPath_SingleMcpCall_ReturnsOneEnvelope` - Basic parsing
2. `Parse_TwoMcpBodies_SeparatedBySemicolon_ReturnsTwoEnvelopes` - Multiple calls
3. `Parse_HeadersPriority_MapsToQosPriority` - `.headers` → `Qos.Priority`
4. `Parse_TrailingComma_ToleratedAndParsedCorrectly` - JSON tolerance
5. `Parse_MalformedJson_ReturnsEmptyList` - Error handling
6. `Parse_MetaTraceparent_CopiedToTraceTraceparent` - Distributed tracing
7. `Parse_NonMcpProlog_ReturnsEmptyList` - Protocol validation
8. `Parse_MetaSchemaAndContentType_CopiedToEnvelope` - Metadata fields
9. `Parse_ComplexNestedPayload_PreservesStructure` - Deep JSON
10. `Parse_MetaBothTraceparentAndSchema_BothFieldsMap` - Multiple metadata

**File:** `/src/Hydra/Hydra.Tests/VisorParsing_DiagnosticTests.cs`
- Diagnostic tests with `ITestOutputHelper` for debugging
- Same coverage with detailed output

### End-to-End Tests
**File:** `/src/Hydra/Hydra.Tests/Visor/VisorEndToEndTests.cs` (480 lines)

**Test Flows:**
1. **EchoRoundTrip_SendsVisorBlock_ReceivesCanonicalizedEchoResponse:**
   - Opens WebSocket to `/visor`
   - Sends VISOR fenced block with `mcp()` call
   - Waits for JSON response frame
   - Validates canonical ordering and payload
   - **Path:** TestClient → VisorGateway → VisorHydraService → XmcpClientHydraService → Echo

2. **DeterministicOrdering_SendsSameEnvelopeTwice_ReceivesByteIdenticalCanonicalizedOutput:**
   - Sends identical envelope twice
   - Verifies byte-identical responses
   - Tests `VisorDeterministicWriter` canonicalization
   - Validates idempotency

**Components Tested:**
- WebSocket connection handling
- VISOR fence parsing
- Echo MCP server integration
- Deterministic JSON serialization
- Envelope routing and response

### Gateway Service Integration Tests
**File:** `/src/Hydra/Hydra.Tests/Integration/Backends/GatewayServiceIntegrationTests.cs`

**Test Coverage:**
1. `ServiceRouter_CallsService_EndToEnd` - Complete Gateway → Service flow
2. `ServiceRouter_ReusesSameServiceInstance_ForSameSession` - Session lifecycle
3. `ServiceRouter_CreatesDifferentInstances_ForDifferentSessions` - Isolation
4. `ServiceRouter_CollectsStats_OnSuccessfulCall` - Metrics
5. `ServiceRouter_CollectsStats_OnFailedCall` - Error tracking
6. `GatewayRegistry_ManagesMultipleGateways` - Multi-gateway
7. `ServiceInvocation_PassesContext_Correctly` - Context propagation

### Execution Context Tests
**File:** `/src/Hydra/Hydra.Tests/Unit/Routing/ServiceRouterExecutionContextTests.cs`

**Key Tests:**
- AsyncLocal flow across async boundaries
- Nested context Push/Pop
- Context restoration on scope exit
- Null context handling

### Field Injection Tests
**File:** `/src/Hydra/Hydra.Tests/Unit/Injection/ActivatorInjectionTests.cs`

**Test Coverage:**
- `[InjectCallerConnection]` attribute
- `[InjectCallerSession]` attribute
- `[InjectCallerGateway]` attribute
- `[InjectCallerAccount]` attribute
- Null context handling
- Reflection caching performance

## Test Helpers

### TestWebSocketClient
Helper class for WebSocket testing:
```csharp
public class TestWebSocketClient
{
    public async Task ConnectAsync(string url);
    public async Task SendAsync(string message);
    public async Task<string> ReceiveAsync();
    public async Task CloseAsync();
}
```

### VISOR Fence Builders
Test utilities for constructing VISOR messages:
```csharp
public static string BuildVisorFence(string mcpCall);
public static string BuildMultipleVisorCalls(params string[] calls);
```

## Test Status

**Passing Tests:** All 319+ tests pass
**Skipped Tests:** None found (no `[Fact(Skip =` patterns)
**Failing Tests:** None reported

## Coverage Areas

### Well-Covered ✅
- VISOR parsing pipeline (VBlockPreParser, Json1sParser)
- ServiceRouter and execution context
- Field injection (HydraServiceActivator)
- Echo handler (9 comprehensive tests)
- Deterministic JSON serialization
- PulseScheduler (3 test files)
- Connection management
- Gateway integration flows

### Needs Coverage ⚠️
- ChatGPTVisor state machine (complex, no dedicated tests)
- VisorGateway WebSocket connection edge cases
- Auth handshake flows (when implemented)
- Error propagation paths
- Credit-based flow control under load

### Future Test Needs 🚧
- End-to-end VISOR with ChatGPT (requires browser automation)
- Multi-client concurrent WebSocket connections
- Large message batching (>1000 envelopes)
- Network failure recovery
- Token refresh flows

## Test Execution

**Run all tests:**
```bash
dotnet test src/Hydra/Hydra.Tests/Hydra.Tests.csproj
```

**Run specific category:**
```bash
dotnet test --filter "Category=Unit"
dotnet test --filter "Category=Integration"
dotnet test --filter "Category=Visor"
```

**Run with coverage:**
```bash
dotnet test /p:CollectCoverage=true
```

## Test Best Practices

1. **AAA Pattern:** Arrange-Act-Assert consistently used
2. **Clear Naming:** Test names describe scenario and expected outcome
3. **Test Isolation:** No shared state between tests
4. **Mocking:** Moq used for external dependencies
5. **Async/Await:** Proper async test patterns
6. **Edge Cases:** Unicode, special characters, large data
7. **Error Handling:** Tests for malformed input

## Continuous Integration

Tests integrated into CI pipeline:
- Run on every commit
- Block merge if tests fail
- Coverage reports generated
- Performance regression detection (future)
