# Dynamic Grain Unloading Test Harness

This playground project tests the dynamic grain loading and unloading features in the custom Orleans fork.

## Structure

- **TestGrains/** - Dynamic grain assembly containing `ITestGrain` and `ICalculatorGrain`
- **TestHost/** - Orleans silo host with HTTP API for load/unload operations

## Phase A: Single-Silo Testing

### Build

```bash
cd Sources/Playgrounds/DynamicGrainUnloading
dotnet build
```

### Run TestHost

```bash
cd TestHost
dotnet run
```

Or with custom configuration:

```bash
dotnet run --SiloPort=11111 --GatewayPort=30000 --IsPrimary=true
```

### API Endpoints

#### Status Check
```bash
curl http://localhost:5000/api/grainmanagement/status
```

#### Load TestGrains Assembly
```bash
curl -X POST http://localhost:5000/api/grainmanagement/load \
  -H "Content-Type: application/json" \
  -d '{"assemblyPath": "FULL_PATH_TO/TestGrains/bin/Debug/net8.0/TestGrains.dll"}'
```

#### Test Grain Invocation
```bash
curl -X POST http://localhost:5000/api/test/hello \
  -H "Content-Type: application/json" \
  -d '{"grainId": "user-123", "name": "World"}'
```

#### Activate Multiple Grains
```bash
curl -X POST http://localhost:5000/api/test/activate-multiple \
  -H "Content-Type: application/json" \
  -d '{"count": 5, "prefix": "user"}'
```

#### Unload TestGrains Assembly
```bash
curl -X POST http://localhost:5000/api/grainmanagement/unload \
  -H "Content-Type: application/json" \
  -d '{"assemblyPath": "FULL_PATH_TO/TestGrains/bin/Debug/net8.0/TestGrains.dll", "timeoutSeconds": 30}'
```

#### Memory Stats
```bash
curl http://localhost:5000/api/grainmanagement/memory
```

## Phase B: Multi-Silo Testing

### Start Primary Silo (A)
```bash
cd TestHost
dotnet run --urls="http://localhost:5000" --SiloPort=11111 --GatewayPort=30000 --IsPrimary=true
```

### Start Secondary Silo (B)
```bash
cd TestHost
dotnet run --urls="http://localhost:5001" --SiloPort=11112 --GatewayPort=30001 --IsPrimary=false --PrimarySiloPort=11111
```

### Start Tertiary Silo (C) - Optional
```bash
cd TestHost
dotnet run --urls="http://localhost:5002" --SiloPort=11113 --GatewayPort=30002 --IsPrimary=false --PrimarySiloPort=11111
```

### Test Scenarios

#### Scenario 1: Type on A only
1. Load assembly on Silo A only
2. Activate grains (should all go to A)
3. Unload from A
4. Verify calls fail (no silo has the type)

#### Scenario 2: Types on A+B, unload only A
1. Load assembly on both A and B
2. Activate grains on both
3. Unload from A only
4. Verify B still works

#### Scenario 3: Load/unload cycles
1. Perform 10+ load/unload cycles
2. Verify no memory leaks
3. Test forced deactivation with slow grains

## Success Criteria

### ✅ Phase A (Single-Silo)
- [ ] TestGrains builds cleanly
- [ ] TestHost starts without errors
- [ ] Load API succeeds
- [ ] Grain invocations work
- [ ] Unload API succeeds
- [ ] All 7 unload phases execute
- [ ] Grains receive `TypeUnloading` deactivation reason
- [ ] Memory reclamation verified

### ✅ Phase B (Multi-Silo)
- [ ] Multiple silos form cluster
- [ ] Load on specific silos works
- [ ] Manifest differs per silo
- [ ] Unload from one silo doesn't affect others
- [ ] Client calls route correctly
- [ ] Load/unload cycles stable

## Logs

Check console output for:
- Reflection-based shared type discovery
- Grain activation/deactivation
- 7-phase unload orchestration:
  1. Validate & prepare
  2. Deactivate grains
  3. Update silo manifest
  4. Propagate to cluster
  5. Clear caches
  6. Unload assembly
  7. Publish event
