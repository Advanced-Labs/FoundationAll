# 07 - Batching & Pulsing System

**Part of:** VISOR Protocol & Implementation Guide (Gen1 V1)
**Last Updated:** 2025-11-10

---

## Overview

The PulseScheduler component has **dual implementations**: a sophisticated main engine (dormant) and simple shim API (active).

---

## 7.1 PulseScheduler Main Engine (DORMANT 💤)

**File:** `PulseScheduler.cs` (148 lines)
**Status:** 💤 Coded but NOT integrated

### Designed Features

| Feature | Implementation | Purpose |
|---------|---------------|---------|
| **ConcurrentQueue buffering** | `ConcurrentQueue<PulseItem>` | Thread-safe message queue |
| **Timer-based max latency** | `Timer` fires every MaxLatencyMs | Force flush on timeout |
| **Byte-based batching** | MaxBytes threshold (65KB) | Batch by size, not count |
| **Credit-based flow** | MinCredit check before flush | Backpressure control |
| **Fair-share scheduling** | FairShare flag | Distribute bandwidth fairly |
| **Queue depth monitoring** | QueueDepthWarn threshold | Warn on queue buildup |

### Why Dormant?

**FlushAsync() has placeholder** (Line 126):
```csharp
private async Task FlushAsync()
{
    var itemsToFlush = new List<PulseItem>();
    var bytesToFlush = 0;

    while (_queue.TryDequeue(out var item))
    {
        itemsToFlush.Add(item);
        bytesToFlush += item.Size;

        if (bytesToFlush >= _options.MaxBytes)
            break;
    }

    // Process items (placeholder for actual delivery logic)
    await Task.Delay(1); // Simulate async work ← PLACEHOLDER!
}
```

**No actual delivery logic implemented!**

---

## 7.2 PulseScheduler Shims (ACTIVE ✅)

**File:** `PulseScheduler.Shims.cs` (154 lines)
**Status:** ✅ Used by VisorGateway

### Implementation

Simple list-based storage:
```csharp
private readonly List<ScheduledPulse> _legacyPulses = new();
private readonly object _legacyLock = new();
```

### Methods

**Start() / Stop()** - No-ops:
```csharp
public void Start(int intervalMs = 100)
{
    _logger.LogDebug("PulseScheduler Start() called (legacy API)");
}
```

**Schedule()** - Add to list:
```csharp
public void Schedule(string connectionId, HydraEnvelope envelope, int priority = 0)
{
    lock (_legacyLock)
    {
        _legacyPulses.Add(new ScheduledPulse {
            ConnectionId = connectionId,
            Envelope = envelope,
            Priority = priority,
            ScheduledTime = DateTime.UtcNow
        });
    }
}
```

**GetDuePulses()** - Priority sort + return:
```csharp
public List<HydraEnvelope> GetDuePulses(string connectionId, int maxCount)
{
    lock (_legacyLock)
    {
        // 1. Filter by connectionId
        // 2. Check max latency
        // 3. Sort by priority (descending), then FIFO
        // 4. Take maxCount
        // 5. Remove from list
        // 6. Return envelopes
    }
}
```

---

## 7.3 Feature Comparison

| Feature | Main Engine | Shims API | Used? |
|---------|-------------|-----------|-------|
| **Storage** | ConcurrentQueue | List | Shims |
| **Thread safety** | Lock-free queue | Lock per operation | Shims |
| **Priority** | Not implemented | ✅ OrderByDescending | Shims |
| **Max latency** | Timer-based | Check on GetDuePulses | Shims |
| **Byte batching** | MaxBytes threshold | ❌ Not implemented | Neither |
| **Credit flow** | MinCredit check | ❌ Not implemented | Neither |
| **Fair-share** | FairShare flag | ❌ Not implemented | Neither |
| **Queue monitoring** | QueueDepthWarn | ❌ Not implemented | Neither |
| **Background processing** | ProcessAsync loop | ❌ Pull-based | Neither |

---

## 7.4 Configuration

**PulseSchedulerOptions** (66 lines):

| Option | Default | Shims Use? | Main Use? | VisorGateway Override |
|--------|---------|------------|-----------|----------------------|
| MaxLatencyMs | 100 | ✅ Yes | 💤 N/A | **5000** |
| MaxBytes | 65536 | ❌ No | 💤 N/A | (default) |
| MinCredit | 1 | ❌ No | 💤 N/A | (default) |
| FairShare | true | ❌ No | 💤 N/A | (default) |
| QueueDepthWarn | 1000 | ❌ No | 💤 N/A | (default) |

**VisorGateway overrides MaxLatencyMs** (Line 65):
```csharp
var pulseOptions = Options.Create(new PulseSchedulerOptions
{
    MaxLatencyMs = 5000  // 5 seconds, not 100ms default
});
```

---

## 7.5 Integration Status

### VisorGateway Usage

| Line | Call | Implementation |
|------|------|---------------|
| 116 | `_pulseScheduler.Start()` | No-op shim |
| 166 | `SchedulePulse(...)` → `_pulseScheduler.Schedule(...)` | Shim (adds to list) |
| 332 | `GetDuePulses(...)` | Shim (priority sort) |
| 224 | `ClearConnection(...)` | Shim (remove by connectionId) |

**Main engine never called!**

---

## 7.6 Why Main Engine is Dormant

1. **MVP priority:** Simple shims sufficient for Sprint 9.1
2. **Placeholder delivery:** FlushAsync has no actual send logic
3. **Integration complexity:** Main engine needs background task + delivery pipeline
4. **Testing required:** Sophisticated features need extensive testing
5. **Future activation:** Planned for later sprint when byte batching needed

---

## 7.7 Activation Roadmap

**To activate main engine:**

1. Implement actual delivery logic in FlushAsync()
2. Start ProcessAsync() background task
3. Switch VisorGateway to use main API instead of shims
4. Add byte-size tracking to envelopes
5. Test credit accrual and fair-share logic
6. Benchmark performance vs shims
7. Monitor queue depth in production

**Estimated:** Sprint 11-12

---

**See Also:**
- [05 - VisorGateway](./05_VisorGateway.md) - How shims are used
- [08 - Flow Control](./08_Flow_Control.md) - VisorConnectionQueue credit system
- [11 - Evolution Roadmap](./11_Evolution_Roadmap.md) - Future plans
