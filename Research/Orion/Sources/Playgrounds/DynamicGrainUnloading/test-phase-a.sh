#!/bin/bash

# Phase A Testing Script
# This script tests single-silo dynamic load/unload

set -e

TESTGRAINS_DLL="D:/dev/FOUNDATION/Research/Orion/Sources/Playgrounds/DynamicGrainUnloading/TestGrains/bin/Debug/net8.0/TestGrains.dll"
BASE_URL="http://localhost:5000"

echo "=== Phase A: Single-Silo Dynamic Load/Unload Test ==="
echo

# Wait for TestHost to be ready
echo "1. Waiting for TestHost to start..."
sleep 5

# Check status
echo "2. Checking TestHost status..."
curl -s $BASE_URL/api/grainmanagement/status | jq .
echo

# Check memory before load
echo "3. Memory before load:"
curl -s $BASE_URL/api/grainmanagement/memory | jq .
echo

# Load TestGrains assembly
echo "4. Loading TestGrains assembly..."
LOAD_RESULT=$(curl -s -X POST $BASE_URL/api/grainmanagement/load \
  -H "Content-Type: application/json" \
  -d "{\"assemblyPath\": \"$TESTGRAINS_DLL\"}")
echo "$LOAD_RESULT" | jq .
echo

# Check if load was successful
if echo "$LOAD_RESULT" | jq -e '.success == true' > /dev/null; then
    echo "✅ Load successful!"
else
    echo "❌ Load failed!"
    echo "$LOAD_RESULT" | jq .
    exit 1
fi

# Memory after load
echo "5. Memory after load:"
curl -s $BASE_URL/api/grainmanagement/memory | jq .
echo

# Activate multiple grains
echo "6. Activating 5 grains..."
ACTIVATE_RESULT=$(curl -s -X POST $BASE_URL/api/test/activate-multiple \
  -H "Content-Type: application/json" \
  -d '{"count": 5, "prefix": "test-user"}')
echo "$ACTIVATE_RESULT" | jq .
echo

# Test a single grain call
echo "7. Testing single grain call..."
HELLO_RESULT=$(curl -s -X POST $BASE_URL/api/test/hello \
  -H "Content-Type: application/json" \
  -d '{"grainId": "test-user-123", "name": "World"}')
echo "$HELLO_RESULT" | jq .
echo

# Test calculator grain
echo "8. Testing Calculator grain..."
CALC_RESULT=$(curl -s -X POST $BASE_URL/api/test/calculator/add \
  -H "Content-Type: application/json" \
  -d '{"a": 10, "b": 15}')
echo "$CALC_RESULT" | jq .
echo

# Unload the assembly
echo "9. Unloading TestGrains assembly..."
UNLOAD_RESULT=$(curl -s -X POST $BASE_URL/api/grainmanagement/unload \
  -H "Content-Type: application/json" \
  -d "{\"assemblyPath\": \"$TESTGRAINS_DLL\", \"timeoutSeconds\": 30}")
echo "$UNLOAD_RESULT" | jq .
echo

# Check if unload was successful
if echo "$UNLOAD_RESULT" | jq -e '.success == true' > /dev/null; then
    echo "✅ Unload successful!"
    echo "   Deactivated grains: $(echo "$UNLOAD_RESULT" | jq -r '.activeGrainsDeactivated')"
    echo "   Duration: $(echo "$UNLOAD_RESULT" | jq -r '.duration') ms"
else
    echo "❌ Unload failed!"
    echo "$UNLOAD_RESULT" | jq .
    exit 1
fi

# Memory after unload
echo "10. Memory after unload (after GC):"
sleep 2
curl -s $BASE_URL/api/grainmanagement/memory | jq .
echo

# Try to invoke a grain (should fail)
echo "11. Attempting to invoke grain after unload (should fail)..."
FAIL_RESULT=$(curl -s -X POST $BASE_URL/api/test/hello \
  -H "Content-Type: application/json" \
  -d '{"grainId": "test-user-123", "name": "World"}' || echo '{"expected": "error"}')
echo "$FAIL_RESULT" | jq .
echo

echo "=== Phase A Test Complete ==="
