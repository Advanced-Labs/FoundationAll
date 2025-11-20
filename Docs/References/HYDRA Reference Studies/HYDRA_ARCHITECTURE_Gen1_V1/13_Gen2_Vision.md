# 13. What's Next (Gen2 Vision)

## 13.1 Planned Technologies

### Akka.NET (Distributed Component Model)
**Purpose:** Location-transparent actors, distributed systems primitives
**Benefits:**
- Services become actors (location-independent)
- Akka Cluster for multi-node deployment
- Akka.Persistence for event sourcing
- Akka.Streams for backpressure-aware data flow
- Hot code swapping via actor restart

**Impact on HYDRA:**
- ServiceRouter becomes Akka router
- Services extend `ReceiveActor`
- HydraEnvelope remains canonical message format
- Gateways send messages to actor references

### Wolverine (Message Bus & Sagas)
**Purpose:** Durable messaging, saga orchestration, long-running workflows
**Benefits:**
- Guaranteed message delivery
- Saga pattern for multi-step workflows
- Scheduled/delayed messages
- Retry policies with exponential backoff
- Dead letter queues

**Impact on HYDRA:**
- ServiceRouter integrates Wolverine transport
- Long-running MCP tool calls become sagas
- Message persistence in PostgreSQL/RavenDB
- Automatic retry for transient failures

### EventStoreDB/KurrentDB (Event Sourcing)
**Purpose:** Event-sourced persistence, temporal queries, event replay
**Benefits:**
- Complete audit trail (every state change recorded)
- Time-travel queries ("what was state at time T?")
- Event replay for debugging
- Projections for read models
- Stream processing

**Impact on HYDRA:**
- All envelopes stored as events
- Service state derived from event streams
- Debugging: replay conversations
- Analytics: query historical interactions

### Quartz.NET (Scheduling)
**Purpose:** Job scheduling, recurring tasks, cron-like execution
**Benefits:**
- Schedule PulseScheduler flushes
- Recurring cleanup jobs
- Time-based workflow triggers
- Persistent job storage

**Impact on HYDRA:**
- Replace PulseScheduler main engine
- Scheduled envelope delivery
- Periodic health checks
- Automatic session cleanup

## 13.2 Why Gen2

### Current Gen1 Limitations

1. **Single Process:**
   - All components in one WPF application
   - No horizontal scaling
   - Single point of failure

2. **In-Memory State:**
   - Service instances in static dictionaries
   - Lost on process restart
   - No state migration

3. **Synchronous Routing:**
   - ServiceRouter uses reflection + direct calls
   - Blocking waits for service responses
   - No backpressure beyond credit-based queue

4. **Manual Lifecycle:**
   - Services started via AutoStart
   - No automatic restart on failure
   - No rolling updates

5. **Limited Observability:**
   - Basic logging
   - No distributed tracing (traceparent unused)
   - No metrics aggregation

### Gen2 Solutions

1. **Distributed Deployment:**
   - Akka Cluster for multi-node
   - Services can run on different machines
   - Automatic failover

2. **Event-Sourced State:**
   - All state in EventStoreDB
   - Replay events to rebuild state
   - No data loss on restart

3. **Asynchronous Messaging:**
   - Wolverine message bus
   - Fire-and-forget with guarantees
   - Backpressure via Akka.Streams

4. **Self-Healing:**
   - Akka supervision: restart failed actors
   - Circuit breakers for external calls
   - Health checks + automatic recovery

5. **Full Observability:**
   - OpenTelemetry integration
   - Jaeger for distributed tracing
   - Prometheus metrics
   - Grafana dashboards

## 13.3 Migration Path

### Phase 1: Event Sourcing (No Breaking Changes)
**Goal:** Record all envelopes as events without changing interfaces

**Steps:**
1. Add EventStoreDB persistence
2. Write all incoming envelopes to event stream
3. Write all outgoing envelopes to event stream
4. Keep current ServiceRouter (dual-write)
5. Add projections for read models

**Benefit:** Complete audit trail, zero code changes

### Phase 2: Wolverine Integration (Hybrid Mode)
**Goal:** Use Wolverine for async messaging while keeping sync option

**Steps:**
1. Install Wolverine transport
2. Add Wolverine message handlers alongside [MessageHandler]
3. Configure Wolverine to use RavenDB for persistence
4. Route specific ops (e.g., long-running MCP calls) via Wolverine
5. Keep synchronous routing for fast ops

**Benefit:** Durable messaging for slow operations

### Phase 3: Akka.NET Migration (Services Become Actors)
**Goal:** Convert services to actors for location transparency

**Steps:**
1. Create `HydraServiceActor` base class extending `ReceiveActor`
2. Migrate services one-by-one to actor model
3. ServiceRouter routes to actor references instead of direct calls
4. Deploy Akka.Cluster with seed nodes
5. Test multi-node deployment

**Benefit:** Distributed services, horizontal scaling

### Phase 4: Full Gen2 (Event-Driven Architecture)
**Goal:** Pure event-sourced, actor-based, distributed system

**Steps:**
1. Remove all static state dictionaries
2. All state derived from EventStoreDB projections
3. All inter-service calls via Akka messages
4. All gateway→service via Wolverine bus
5. Quartz.NET for scheduled jobs
6. OpenTelemetry for observability

**Benefit:** Production-grade distributed system

## 13.4 Envelope Compatibility

### Gen1 → Gen2: NO BREAKING CHANGES

**HydraEnvelope v0.2 remains canonical:**
- Gen2 actors receive/send same envelope structure
- Wolverine handlers accept HydraEnvelope
- EventStoreDB stores envelopes as events
- Parsing pipeline unchanged (VBlockPreParser, Json1sParser)

**Evolvability:**
- Gen1 clients can talk to Gen2 servers (forward compatible)
- Gen2 servers can talk to Gen1 clients (backward compatible)
- Unknown envelope fields ignored by both

### Service Signature Stability

**Gen1 Service Method:**
```csharp
[MessageHandler("mcp")]
public async Task<object?> HandleMcp(
    Dictionary<string, object?> parameters,
    CancellationToken cancellationToken)
```

**Gen2 Actor Handler:**
```csharp
public class VisorActor : HydraServiceActor
{
    protected override void OnReceive()
    {
        Receive<HydraEnvelope>(envelope => {
            if (envelope.Op == "mcp") {
                var result = await HandleMcp(ExtractParameters(envelope));
                Sender.Tell(CreateResponseEnvelope(result));
            }
        });
    }
}
```

**Same business logic, different plumbing**

## 13.5 VISOR Protocol Stability

**VISOR fence format unchanged:**
```
```visor
mcp() { ... }
```
```

**Parsing pipeline unchanged:**
- ChatGPTVisor still uses VBlockPreParser
- VisorGateway still uses VisorParsePipeline
- Json1sParser still creates HydraEnvelope

**Gen2 only changes routing layer:**
- VisorGateway sends to Wolverine instead of events
- Wolverine routes to Akka actors instead of ServiceRouter
- Response path reversed

## 13.6 Estimated Timeline

| Phase | Duration | Key Milestone |
|-------|----------|---------------|
| Phase 1: EventStoreDB | 2-3 sprints | All envelopes persisted |
| Phase 2: Wolverine | 3-4 sprints | Durable messaging live |
| Phase 3: Akka.NET | 4-6 sprints | Multi-node deployment |
| Phase 4: Full Gen2 | 2-3 sprints | Remove legacy code |
| **Total** | **11-16 sprints** | **Production-ready Gen2** |

**Assumption:** 2-week sprints, 1-2 developers

## 13.7 Risk Mitigation

**Risks:**
1. EventStoreDB learning curve
2. Akka.NET complexity
3. Wolverine/Akka integration
4. State migration bugs
5. Performance regression

**Mitigations:**
1. Spike: EventStoreDB POC (1 sprint)
2. Training: Akka.NET Bootcamp
3. Integration testing at each phase
4. Dual-write during migration (rollback option)
5. Load testing before Gen2 production

## 13.8 Success Criteria

Gen2 is successful when:
- ✅ Multi-node deployment with automatic failover
- ✅ Zero data loss on process restart
- ✅ <100ms p99 latency (same as Gen1)
- ✅ Horizontal scaling (add nodes → more throughput)
- ✅ Full distributed tracing (Jaeger)
- ✅ Saga-based workflows for long-running MCP calls
- ✅ Backward compatible with Gen1 clients
