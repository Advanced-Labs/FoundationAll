# 12 - Troubleshooting & Performance

**Part of:** VISOR Protocol & Implementation Guide (Gen1 V1)
**Last Updated:** 2025-11-10

---

## Overview

Common issues, debugging techniques, performance characteristics, and known limitations of VISOR MVP.

---

## 12.1 Common Issues

### Issue: VISOR Fence Not Detected

**Symptoms:**
- Message sent, but no envelopes parsed
- ChatGPTVisor logs "No VISOR blocks found"

**Causes:**
1. Incorrect fence syntax (missing backticks, wrong label)
2. Fence not complete in buffer
3. Fence trimmed by buffer management

**Debug:**
```csharp
// Check regex match
var matches = _visorFenceRegex.Matches(buffer);
Logger.LogDebug($"Found {matches.Count} matches");

// Check pattern
var pattern = @"```visor\s*\n(.*?)\n```";  // Must match exactly
```

**Solutions:**
- Verify fence syntax: ` ```VISOR\n...\n``` `
- Check buffer size (should be > fence length)
- Increase buffer retention (currently 100KB)

---

### Issue: Parsing Errors

**Symptoms:**
- "Failed to parse V-block" in logs
- Empty envelope list returned

**Causes:**
1. Malformed JSON in payload
2. Invalid prolog (not `mcp(...)`)
3. Unbalanced braces in body
4. Missing semicolon after V-call

**Debug:**
```csharp
// Enable verbose logging
Logger.LogDebug($"Prolog: '{prolog}'");
Logger.LogDebug($"Body: '{body}'");

// Try parsing manually
try {
    var doc = JsonDocument.Parse(body);
} catch (JsonException ex) {
    Logger.LogError(ex, "JSON parse failed");
}
```

**Solutions:**
- Validate JSON with linter
- Check trailing commas (allowed in json1s)
- Verify prolog starts with `mcp(`
- Ensure braces balanced

---

### Issue: Downstream Buffer Not Flushing

**Symptoms:**
- Messages received by gateway but not sent to LLM
- Messages stuck in `_downstreamBuffer`

**Causes:**
1. State stuck in Generating (completion detection failed)
2. Linger timer not firing
3. `NotifyGenerationComplete()` not called

**Debug:**
```csharp
Logger.LogDebug($"SendArbiter state: {_state}");
Logger.LogDebug($"Downstream buffer count: {_downstreamBuffer.Count}");
Logger.LogDebug($"Linger timer enabled: {_lingerTimer.Enabled}");
```

**Solutions:**
- Check for `message_stream_complete` in SSE stream
- Verify linger timer (200ms) not cancelled
- Manually call `NotifyGenerationComplete()` if stuck
- Check logs for transition messages

---

### Issue: WebSocket Connection Dropped

**Symptoms:**
- "Gateway WebSocket closed by server"
- Messages stop flowing

**Causes:**
1. Network interruption
2. Gateway restart
3. Connection timeout
4. Client sent invalid message

**Debug:**
```csharp
Logger.LogDebug($"WebSocket state: {_gatewayWebSocket.State}");
Logger.LogDebug($"Last message time: {_lastMessageTime}");
```

**Solutions:**
- Implement reconnection logic (not in MVP)
- Add exponential backoff
- Check network stability
- Validate messages before sending

---

## 12.2 Performance Characteristics

### Latency Budget

| Component | Typical | P95 | P99 | Notes |
|-----------|---------|-----|-----|-------|
| **Upstream micro-batch** | 75ms | 150ms | 200ms | Fixed timer |
| **Downstream linger** | 200ms | 250ms | 300ms | Fixed timer |
| **VBlockPreParser** | <1ms | 2ms | 5ms | Depends on fence count |
| **Json1sParser** | <1ms | 3ms | 8ms | Depends on payload size |
| **WebSocket send** | <10ms | 50ms | 100ms | Network dependent |
| **Total round trip** | ~300ms | 500ms | 800ms | End-to-end |

**Measured on:** Local dev machine (2025-11-10)

---

### Throughput

| Metric | Value | Notes |
|--------|-------|-------|
| **Messages/sec (upstream)** | ~13 | Limited by 75ms batch timer |
| **Messages/sec (downstream)** | ~5 | Limited by 200ms linger |
| **Max message size** | ~8KB | WebSocket buffer size |
| **Max fence size** | ~100KB | Buffer trimming threshold |
| **Connections supported** | Unlimited | No hard limit (dev mode) |

---

### Resource Usage

| Resource | Per Connection | Notes |
|----------|---------------|-------|
| **Memory** | ~50KB | Queue + buffers |
| **CPU** | <1% | Mostly idle |
| **Network** | ~10KB/s | Message overhead |
| **Threads** | 2 | Send loop + receive loop |

**Measured with:** 10 concurrent connections, 1 msg/sec each

---

### Bottlenecks

**Identified:**
1. **Regex matching** - Compiled regex helps, but still O(n)
2. **JSON parsing** - System.Text.Json is fast, but still overhead
3. **String allocations** - Many string copies (could use Span<char>)
4. **Lock contention** - Multiple locks in hot path
5. **Timer overhead** - Two timers per connection (75ms, 200ms)

**Not Bottlenecks (yet):**
- WebSocket I/O (very fast)
- Envelope serialization (deterministic writer is efficient)
- Connection management (low overhead)

---

## 12.3 Known Limitations

### ChatGPTVisor

| Limitation | Impact | Mitigation |
|------------|--------|------------|
| **Hardcoded timings** | Can't optimize per use case | Accept defaults for MVP |
| **Fixed buffer size** | Large messages may be trimmed | Keep messages < 100KB |
| **No reconnection** | Connection loss is permanent | Manual reconnect required |
| **DevTools dependency** | Breaks if ChatGPT changes API | Monitor for API changes |
| **Single connection** | No failover | Accept risk for MVP |

---

### VisorGateway

| Limitation | Impact | Mitigation |
|------------|--------|------------|
| **No authentication** | Dev mode only | Enable auth before production |
| **No rate limiting** | Can be overwhelmed | Add rate limiting |
| **No max message size** | Memory issues possible | Add size limit |
| **No connection timeout** | Idle connections stay open | Add timeout |
| **No max connections** | Resource exhaustion possible | Add limit |

---

### PulseScheduler

| Limitation | Impact | Mitigation |
|------------|--------|------------|
| **Main engine dormant** | Sophisticated features unused | Activate in Sprint 11-12 |
| **No byte batching** | Can't optimize by payload size | Use shims for MVP |
| **No fair-share** | One connection can starve others | Monitor and adjust |
| **No credit accrual** | No burst capacity | Use simple credit for MVP |

---

### Parsing

| Limitation | Impact | Mitigation |
|------------|--------|------------|
| **No streaming parser** | Must have full message | Buffer appropriately |
| **No caching** | Re-parse identical messages | Accept overhead for MVP |
| **No parallel parsing** | Single-threaded | OK for MVP load |
| **No schema validation** | Invalid payloads not caught early | Validate in services |

---

## 12.4 Debugging Techniques

### Enable Verbose Logging

```csharp
// In ChatGPTVisor.cs
Logger.LogLevel = LogLevel.Debug;

// Check logs for:
// - [VISOR-STREAM] messages (SSE parsing)
// - [VISOR-PARSE] messages (parsing pipeline)
// - [VISOR-UPSTREAM] messages (LLM → Gateway)
// - [VISOR-DOWNSTREAM] messages (Gateway → LLM)
// - [VISOR-DOM] messages (DOM injection)
```

### Inspect State

```csharp
// SendArbiter state
Logger.LogDebug($"State: {_state}");
Logger.LogDebug($"Upstream queue: {_upstreamQueue.Count}");
Logger.LogDebug($"Downstream buffer: {_downstreamBuffer.Count}");

// Connection state
Logger.LogDebug($"WebSocket: {_gatewayWebSocket.State}");
Logger.LogDebug($"Credit: {queue.Credit}");
Logger.LogDebug($"Queue count: {queue.Count}");
```

### Test Parsing Manually

```csharp
// Extract and test parsing
var message = @"```VISOR
mcp() { { \"name\": \"echo\" } };
```";

var envelopes = VisorParsePipeline.ExtractAndParse(message);
Assert.NotEmpty(envelopes);
```

### Monitor Timers

```csharp
// Check timer status
Logger.LogDebug($"MicroBatch enabled: {_microBatchTimer.Enabled}");
Logger.LogDebug($"Linger enabled: {_lingerTimer.Enabled}");
Logger.LogDebug($"DOM debounce enabled: {_domDebounceTimer.Enabled}");
```

---

## 12.5 Performance Tuning

### Reduce Latency

**Upstream:**
- Lower MicroBatchIntervalMs (currently 75ms)
- Trade-off: More network round-trips

**Downstream:**
- Lower LingerDelayMs (currently 200ms)
- Trade-off: More frequent DOM injections

**Parsing:**
- Use compiled regex (already done)
- Cache parsed results (not implemented)
- Use Span<char> for zero-copy (not implemented)

---

### Increase Throughput

**Batch Larger:**
- Increase MicroBatchIntervalMs (accumulate more messages)
- Trade-off: Higher latency

**Parallel Processing:**
- Parse multiple blocks concurrently
- Trade-off: Higher CPU usage

**Buffer Management:**
- Increase buffer size (currently 100KB)
- Trade-off: Higher memory usage

---

### Reduce Memory

**Smaller Buffers:**
- Decrease buffer retention (currently 100KB)
- Trade-off: May trim incomplete fences

**Connection Pooling:**
- Reuse connection objects
- Trade-off: Added complexity

**String Interning:**
- Intern common strings (op names, subjects)
- Trade-off: GC pressure

---

## 12.6 Monitoring & Metrics

### Key Metrics to Track

**Throughput:**
- Messages/sec upstream
- Messages/sec downstream
- Envelopes parsed/sec

**Latency:**
- Micro-batch delay (avg, p95, p99)
- Linger delay (avg, p95, p99)
- End-to-end latency

**Errors:**
- Parse failures/sec
- WebSocket disconnects/sec
- Buffer overflows/sec

**Resource:**
- Memory per connection
- CPU usage per gateway
- WebSocket connections active

---

### Logging Best Practices

**Structured Logging:**
```csharp
Logger.LogInformation(
    "Envelope processed: id={EnvelopeId}, op={Op}, latency={LatencyMs}ms",
    envelope.Id, envelope.Op, latencyMs);
```

**Log Levels:**
- **Debug:** Verbose details (parsing, state transitions)
- **Information:** Normal operations (envelope received, sent)
- **Warning:** Recoverable issues (buffer full, parsing failed)
- **Error:** Serious issues (WebSocket disconnect, handler exception)

---

## 12.7 Production Readiness Checklist

Before deploying to production:

- [ ] Enable authentication on VisorGateway
- [ ] Add rate limiting per connection
- [ ] Set max message size limit
- [ ] Set max connections limit
- [ ] Add connection timeout
- [ ] Implement reconnection logic in ChatGPTVisor
- [ ] Add monitoring and alerting
- [ ] Load test with realistic traffic
- [ ] Add circuit breakers for downstream services
- [ ] Document runbooks for common issues
- [ ] Set up log aggregation
- [ ] Configure distributed tracing
- [ ] Add health checks and readiness probes
- [ ] Test failover scenarios
- [ ] Document security considerations

---

**See Also:**
- [04 - ChatGPTVisor](./04_ChatGPTVisor.md) - Client-side implementation details
- [05 - VisorGateway](./05_VisorGateway.md) - Server-side implementation details
- [07 - Batching & Pulsing](./07_Batching_Pulsing.md) - PulseScheduler limitations
- [11 - Evolution Roadmap](./11_Evolution_Roadmap.md) - Future improvements
