# Factory Patterns - Meta-Platform Illustrations

**Last Updated:** November 10, 2025
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
2. [Runtime-Specific Factory Patterns](#runtime-specific-factory-patterns)
3. [Python Module Instantiation Factories](#python-module-instantiation-factories)
4. [Node.js Factory Functions](#nodejs-factory-functions)
5. [Cross-Runtime Object Creation](#cross-runtime-object-creation)
6. [Challenges](#challenges)
7. [Possibilities and Future Directions](#possibilities-and-future-directions)

---

## Conceptual Adaptation

### From .NET Factory Methods to Polyglot Patterns

**VISORA's .NET Approach:**
```csharp
// Static factory method
public static ModuleDescriptor Create(
    string id,
    string name,
    Version version) => new(id, name, version);

// Async factory method
public static Task<ModuleHandle> LoadAsync(
    string path,
    ModuleCatalogOptions options,
    CancellationToken ct);
```

**Cross-Language Challenge:**
- Different languages have different idioms
- Static methods work differently (or not at all)
- Async patterns vary significantly

**Conceptual Meta-Platform Model:**
```
Factory Pattern Abstraction
├─ .NET: Static methods (Type.Create())
├─ Python: @staticmethod or module-level functions
├─ Node.js: Exported factory functions
└─ Shared: JSON-based factory configuration
```

---

## Runtime-Specific Factory Patterns

### .NET Factory (Reference Implementation)

```csharp
public sealed record ModuleDescriptor(
    string Id,
    string Name,
    string Version)
{
    // Static factory method
    public static ModuleDescriptor Create(
        string id,
        string name,
        string version)
    {
        // Validation
        if (string.IsNullOrEmpty(id))
            throw new ArgumentException("ID required", nameof(id));

        // Construction
        return new ModuleDescriptor(id, name, version);
    }
}

// Usage:
var descriptor = ModuleDescriptor.Create("id", "Name", "1.0.0");
```

### Python Factory (Illustrative)

```python
from dataclasses import dataclass
from typing import Optional

@dataclass(frozen=True)
class ModuleDescriptor:
    id: str
    name: str
    version: str

    @staticmethod
    def create(id: str, name: str, version: str) -> 'ModuleDescriptor':
        """Factory method (mimics C# pattern)."""
        # Validation
        if not id:
            raise ValueError("ID required")

        # Construction
        return ModuleDescriptor(id=id, name=name, version=version)

# Usage:
descriptor = ModuleDescriptor.create(id="id", name="Name", version="1.0.0")
```

**Alternative: Module-Level Function**

```python
# module_descriptor.py
from dataclasses import dataclass

@dataclass(frozen=True)
class ModuleDescriptor:
    id: str
    name: str
    version: str

# Factory function at module level (Pythonic idiom)
def create_module_descriptor(
    id: str,
    name: str,
    version: str
) -> ModuleDescriptor:
    """Factory function for creating module descriptors."""
    if not id:
        raise ValueError("ID required")
    return ModuleDescriptor(id=id, name=name, version=version)

# Usage:
from module_descriptor import create_module_descriptor
descriptor = create_module_descriptor(id="id", name="Name", version="1.0.0")
```

### Node.js/TypeScript Factory (Illustrative)

```typescript
// ModuleDescriptor.ts
export interface ModuleDescriptor {
  readonly id: string;
  readonly name: string;
  readonly version: string;
}

// Factory function (TypeScript/JavaScript idiom)
export function createModuleDescriptor(
  id: string,
  name: string,
  version: string
): ModuleDescriptor {
  // Validation
  if (!id || id.trim() === '') {
    throw new Error('ID required');
  }

  // Construction and freeze for immutability
  return Object.freeze({
    id,
    name,
    version
  });
}

// Usage:
import { createModuleDescriptor } from './ModuleDescriptor';
const descriptor = createModuleDescriptor('id', 'Name', '1.0.0');
```

**Alternative: Class with Static Method**

```typescript
export class ModuleDescriptor {
  private constructor(
    public readonly id: string,
    public readonly name: string,
    public readonly version: string
  ) {
    Object.freeze(this);
  }

  // Static factory method (C#-like)
  static create(id: string, name: string, version: string): ModuleDescriptor {
    if (!id || id.trim() === '') {
      throw new Error('ID required');
    }
    return new ModuleDescriptor(id, name, version);
  }
}

// Usage:
const descriptor = ModuleDescriptor.create('id', 'Name', '1.0.0');
```

---

## Python Module Instantiation Factories

### Pattern 1: @staticmethod on Class

```python
from dataclasses import dataclass
from typing import Optional, Dict
import re

@dataclass(frozen=True)
class ModuleDescriptor:
    id: str
    name: str
    version: str
    description: Optional[str] = None
    tags: Optional[Dict[str, str]] = None

    @staticmethod
    def create(
        id: str,
        name: str,
        version: str,
        description: Optional[str] = None,
        tags: Optional[Dict[str, str]] = None
    ) -> 'ModuleDescriptor':
        """
        Factory method for creating validated module descriptors.

        Args:
            id: Module identifier (e.g., 'visora.analytics')
            name: Human-readable name
            version: Semantic version (e.g., '1.2.3')
            description: Optional description
            tags: Optional metadata tags

        Returns:
            Validated ModuleDescriptor instance

        Raises:
            ValueError: If inputs are invalid
        """
        # Validate ID format
        if not re.match(r'^[a-z0-9]+([.-][a-z0-9]+)*$', id):
            raise ValueError(f"Invalid module ID format: {id}")

        # Validate version format
        if not re.match(r'^\d+\.\d+\.\d+$', version):
            raise ValueError(f"Invalid version format: {version}")

        # Make tags immutable
        immutable_tags = dict(tags) if tags else None

        return ModuleDescriptor(
            id=id,
            name=name,
            version=version,
            description=description,
            tags=immutable_tags
        )

# Usage:
descriptor = ModuleDescriptor.create(
    id="analytics.module",
    name="Analytics Module",
    version="1.2.3",
    description="Real-time analytics",
    tags={"category": "analytics"}
)
```

### Pattern 2: Module-Level Factory Function

```python
# descriptors.py
from dataclasses import dataclass
from typing import Optional, Dict, List
from enum import Enum, auto

class ComponentKind(Enum):
    GENERIC = auto()
    SERVICE = auto()
    UI = auto()
    CONSOLE = auto()

@dataclass(frozen=True)
class ComponentDescriptor:
    id: str
    name: str
    description: Optional[str] = None
    tags: Optional[tuple] = None  # tuple is immutable
    kind: ComponentKind = ComponentKind.GENERIC

# Factory function (Pythonic idiom)
def create_component_descriptor(
    id: str,
    name: str,
    description: Optional[str] = None,
    tags: Optional[List[str]] = None,
    kind: ComponentKind = ComponentKind.GENERIC
) -> ComponentDescriptor:
    """
    Create a component descriptor with validation.

    This is the Pythonic way - a module-level function rather than
    a static method on the class.
    """
    if not id or not name:
        raise ValueError("ID and name are required")

    # Convert list to tuple for immutability
    immutable_tags = tuple(tags) if tags else None

    return ComponentDescriptor(
        id=id,
        name=name,
        description=description,
        tags=immutable_tags,
        kind=kind
    )

# Usage:
from descriptors import create_component_descriptor, ComponentKind

component = create_component_descriptor(
    id="analytics.charts",
    name="Chart Components",
    tags=["visualization", "charts"],
    kind=ComponentKind.UI
)
```

### Pattern 3: Async Factory (Python)

```python
import asyncio
from dataclasses import dataclass
from pathlib import Path
from typing import Any

@dataclass
class ModuleHandle:
    path: Path
    module_instance: Any
    descriptor: 'ModuleDescriptor'

    @staticmethod
    async def load_async(
        module_path: str,
        options: dict
    ) -> 'ModuleHandle':
        """
        Async factory for loading modules.

        Illustrative example showing async I/O during construction.
        """
        path = Path(module_path)

        # Simulate async I/O (e.g., reading module file)
        await asyncio.sleep(0.1)  # Placeholder for actual I/O

        # In real implementation:
        # - Load Python module dynamically (importlib)
        # - Discover module classes
        # - Instantiate and validate
        module_instance = await _load_module_instance(path)
        descriptor = module_instance.get_descriptor()

        return ModuleHandle(
            path=path,
            module_instance=module_instance,
            descriptor=descriptor
        )

async def _load_module_instance(path: Path) -> Any:
    """Helper to load module (async I/O)."""
    # Illustrative - actual implementation would use importlib
    import importlib.util

    spec = importlib.util.spec_from_file_location("module", path)
    if spec is None or spec.loader is None:
        raise ValueError(f"Cannot load module from {path}")

    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)

    # Find module class (by convention, ends with "Module")
    for attr_name in dir(module):
        attr = getattr(module, attr_name)
        if isinstance(attr, type) and attr_name.endswith("Module"):
            return attr()

    raise ValueError(f"No module class found in {path}")

# Usage:
handle = await ModuleHandle.load_async("analytics_module.py", options={})
print(f"Loaded: {handle.descriptor.name}")
```

### Pattern 4: Try-Create Pattern (Python)

```python
from typing import Optional, Tuple

def try_create_module_descriptor(
    id: str,
    name: str,
    version: str
) -> Tuple[bool, Optional[ModuleDescriptor]]:
    """
    Try to create a module descriptor without throwing exceptions.

    Returns:
        (success: bool, descriptor: Optional[ModuleDescriptor])
    """
    try:
        descriptor = ModuleDescriptor.create(id, name, version)
        return (True, descriptor)
    except (ValueError, TypeError):
        return (False, None)

# Usage:
success, descriptor = try_create_module_descriptor("id", "Name", "1.0.0")
if success:
    print(f"Created: {descriptor.name}")
else:
    print("Failed to create descriptor")
```

---

## Node.js Factory Functions

### Pattern 1: Exported Factory Function

```typescript
// moduleDescriptor.ts
export interface ModuleDescriptor {
  readonly id: string;
  readonly name: string;
  readonly version: string;
  readonly description?: string;
  readonly tags?: Readonly<Record<string, string>>;
}

/**
 * Factory function for creating module descriptors.
 *
 * This is the idiomatic JavaScript/TypeScript approach.
 */
export function createModuleDescriptor(
  id: string,
  name: string,
  version: string,
  description?: string,
  tags?: Record<string, string>
): ModuleDescriptor {
  // Validation
  if (!id || id.trim() === '') {
    throw new Error('Module ID is required');
  }

  if (!name || name.trim() === '') {
    throw new Error('Module name is required');
  }

  if (!/^\d+\.\d+\.\d+$/.test(version)) {
    throw new Error(`Invalid version format: ${version}`);
  }

  // Construction with immutability
  const descriptor: ModuleDescriptor = {
    id,
    name,
    version,
    description,
    tags: tags ? Object.freeze({ ...tags }) : undefined
  };

  return Object.freeze(descriptor);
}

// Usage:
import { createModuleDescriptor } from './moduleDescriptor';

const descriptor = createModuleDescriptor(
  'analytics.module',
  'Analytics Module',
  '1.2.3',
  'Real-time analytics',
  { category: 'analytics', author: 'Example Corp' }
);
```

### Pattern 2: Class with Static Factory

```typescript
// ModuleDescriptor.ts
export class ModuleDescriptor {
  private constructor(
    public readonly id: string,
    public readonly name: string,
    public readonly version: string,
    public readonly description?: string,
    public readonly tags?: Readonly<Record<string, string>>
  ) {
    Object.freeze(this);
  }

  /**
   * Static factory method (C#-like pattern).
   */
  static create(
    id: string,
    name: string,
    version: string,
    description?: string,
    tags?: Record<string, string>
  ): ModuleDescriptor {
    // Validation
    if (!id || id.trim() === '') {
      throw new Error('Module ID is required');
    }

    // Construction
    const immutableTags = tags ? Object.freeze({ ...tags }) : undefined;
    return new ModuleDescriptor(id, name, version, description, immutableTags);
  }
}

// Usage:
const descriptor = ModuleDescriptor.create(
  'analytics.module',
  'Analytics Module',
  '1.2.3',
  'Real-time analytics',
  { category: 'analytics' }
);
```

### Pattern 3: Async Factory (Node.js)

```typescript
// moduleHandle.ts
import { promises as fs } from 'fs';
import path from 'path';

export interface ModuleHandle {
  readonly path: string;
  readonly module: any;
  readonly descriptor: ModuleDescriptor;
}

/**
 * Async factory for loading modules.
 *
 * Illustrates async I/O during construction.
 */
export async function loadModuleAsync(
  modulePath: string,
  options: any
): Promise<ModuleHandle> {
  // Validation
  if (!modulePath) {
    throw new Error('Module path is required');
  }

  // Check file exists (async I/O)
  try {
    await fs.access(modulePath);
  } catch {
    throw new Error(`Module not found: ${modulePath}`);
  }

  // Load module dynamically (async in ES modules)
  const absolutePath = path.resolve(modulePath);
  const module = await import(absolutePath);

  // Discover module class (convention: export named 'module')
  if (!module.module) {
    throw new Error(`No module export found in ${modulePath}`);
  }

  const moduleInstance = new module.module();

  // Get descriptor
  const descriptor = moduleInstance.getDescriptor();
  if (!descriptor) {
    throw new Error(`Module ${modulePath} has no descriptor`);
  }

  // Return handle (frozen for immutability)
  return Object.freeze({
    path: modulePath,
    module: moduleInstance,
    descriptor
  });
}

// Usage:
const handle = await loadModuleAsync('./modules/analytics.js', {});
console.log(`Loaded: ${handle.descriptor.name}`);
```

### Pattern 4: Result Factories (Node.js)

```typescript
// commandResult.ts
export enum CommandOutcome {
  Success = 'success',
  Failed = 'failed',
  Cancelled = 'cancelled'
}

export interface CommandResult {
  readonly outcome: CommandOutcome;
  readonly message?: string;
  readonly payload?: any;
}

/**
 * Factory functions for command results.
 */
export function createSuccess(message?: string, payload?: any): CommandResult {
  return Object.freeze({
    outcome: CommandOutcome.Success,
    message,
    payload
  });
}

export function createFailure(message?: string, payload?: any): CommandResult {
  return Object.freeze({
    outcome: CommandOutcome.Failed,
    message,
    payload
  });
}

export function createCancellation(message?: string): CommandResult {
  return Object.freeze({
    outcome: CommandOutcome.Cancelled,
    message,
    payload: undefined
  });
}

// Usage:
import { createSuccess, createFailure } from './commandResult';

const result = createSuccess('Operation completed', { count: 42 });
```

---

## Cross-Runtime Object Creation

### Scenario: .NET Host Creates Objects for Python/Node.js

**Challenge:** How does a .NET host create module instances in other runtimes?

**Approach 1: JSON-Based Factory**

**.NET Host:**
```csharp
// Host prepares factory configuration
var factoryConfig = new
{
    Type = "ModuleDescriptor",
    Parameters = new
    {
        Id = "analytics.module",
        Name = "Analytics Module",
        Version = "1.2.3"
    }
};

string json = JsonSerializer.Serialize(factoryConfig);

// Send to Python/Node.js runtime
await SendToRuntimeAsync(json);
```

**Python Runtime:**
```python
import json

# Receive factory configuration
config_json = receive_from_host()
config = json.loads(config_json)

# Dispatch to appropriate factory
if config['Type'] == 'ModuleDescriptor':
    params = config['Parameters']
    descriptor = ModuleDescriptor.create(
        id=params['Id'],
        name=params['Name'],
        version=params['Version']
    )
```

**Node.js Runtime:**
```typescript
// Receive factory configuration
const configJson = receiveFromHost();
const config = JSON.parse(configJson);

// Dispatch to appropriate factory
if (config.Type === 'ModuleDescriptor') {
  const params = config.Parameters;
  const descriptor = createModuleDescriptor(
    params.Id,
    params.Name,
    params.Version
  );
}
```

### Approach 2: gRPC Factory Service

**Proto Definition:**
```protobuf
syntax = "proto3";

package visora;

service DescriptorFactory {
  rpc CreateModuleDescriptor(CreateModuleDescriptorRequest)
      returns (ModuleDescriptor);
}

message CreateModuleDescriptorRequest {
  string id = 1;
  string name = 2;
  string version = 3;
  optional string description = 4;
  map<string, string> tags = 5;
}

message ModuleDescriptor {
  string id = 1;
  string name = 2;
  string version = 3;
  optional string description = 4;
  map<string, string> tags = 5;
}
```

**.NET Client:**
```csharp
var client = new DescriptorFactory.DescriptorFactoryClient(channel);

var request = new CreateModuleDescriptorRequest
{
    Id = "analytics.module",
    Name = "Analytics Module",
    Version = "1.2.3"
};

var descriptor = await client.CreateModuleDescriptorAsync(request);
```

**Python Server:**
```python
class DescriptorFactoryServicer(descriptor_factory_pb2_grpc.DescriptorFactoryServicer):
    def CreateModuleDescriptor(self, request, context):
        # Use Python factory
        descriptor = ModuleDescriptor.create(
            id=request.id,
            name=request.name,
            version=request.version,
            description=request.description if request.HasField('description') else None,
            tags=dict(request.tags)
        )

        # Convert to protobuf message
        return descriptor_factory_pb2.ModuleDescriptor(
            id=descriptor.id,
            name=descriptor.name,
            version=descriptor.version,
            description=descriptor.description,
            tags=descriptor.tags
        )
```

### Approach 3: Remote Module Loading

**Scenario:** .NET host loads module from Python runtime.

**.NET Host:**
```csharp
public interface IRemoteModuleLoader
{
    Task<RemoteModuleHandle> LoadModuleAsync(string runtime, string path);
}

public class RemoteModuleLoader : IRemoteModuleLoader
{
    private readonly IRuntimeBridge _pythonBridge;
    private readonly IRuntimeBridge _nodeJsBridge;

    public async Task<RemoteModuleHandle> LoadModuleAsync(
        string runtime,
        string path)
    {
        var bridge = runtime switch
        {
            "python" => _pythonBridge,
            "nodejs" => _nodeJsBridge,
            _ => throw new NotSupportedException($"Runtime not supported: {runtime}")
        };

        // Send load command to runtime
        var request = new
        {
            Command = "LoadModule",
            Path = path
        };

        var response = await bridge.SendAsync(JsonSerializer.Serialize(request));
        var result = JsonSerializer.Deserialize<RemoteModuleHandle>(response);

        return result;
    }
}
```

**Python Runtime Bridge:**
```python
async def handle_command(command_json: str) -> str:
    """Handle commands from .NET host."""
    command = json.loads(command_json)

    if command['Command'] == 'LoadModule':
        path = command['Path']

        # Load module using Python factory
        handle = await ModuleHandle.load_async(path, options={})

        # Serialize result
        result = {
            'Path': str(handle.path),
            'DescriptorId': handle.descriptor.id,
            'DescriptorName': handle.descriptor.name,
            'DescriptorVersion': handle.descriptor.version
        }

        return json.dumps(result)
```

---

## Challenges

### Challenge 1: Language-Specific Idioms

**Problem:** Factory patterns differ across languages.

| Language | Idiomatic Factory Pattern |
|----------|---------------------------|
| C# | Static methods on class |
| Python | Module-level functions or @staticmethod |
| JavaScript/TypeScript | Exported functions |

**Implication:** Cross-language code generation must adapt to idioms.

**Solution:** Use code generation templates per language.

### Challenge 2: Async Patterns Vary

**Problem:** Async construction differs:

| Language | Async Pattern |
|----------|---------------|
| C# | `Task<T>`, `async/await` |
| Python | `asyncio`, `async def`, `await` |
| JavaScript/TypeScript | `Promise<T>`, `async/await` |

**Implication:** Async factories need runtime-specific implementations.

**Solution:**
- C#: `Task<ModuleHandle> LoadAsync(...)`
- Python: `async def load_async(...) -> ModuleHandle:`
- TypeScript: `async function loadAsync(...): Promise<ModuleHandle>`

### Challenge 3: Validation Consistency

**Problem:** Validation logic must be consistent across runtimes.

**Solution 1: Shared JSON Schema**

```json
{
  "definitions": {
    "ModuleDescriptor": {
      "properties": {
        "id": {
          "type": "string",
          "pattern": "^[a-z0-9]+([.-][a-z0-9]+)*$"
        },
        "version": {
          "type": "string",
          "pattern": "^\\d+\\.\\d+\\.\\d+$"
        }
      }
    }
  }
}
```

**Solution 2: Validation as a Service**

```
┌─────────────────────────────────┐
│  Validation Service (gRPC)      │
│  - Validate descriptors          │
│  - Enforce business rules        │
└─────────────────────────────────┘
         ↑         ↑         ↑
         │         │         │
    .NET Factory  Python   Node.js
                  Factory  Factory
```

### Challenge 4: Error Handling Differences

**Problem:** Exception types and conventions differ.

| Language | Error Mechanism |
|----------|-----------------|
| C# | Exceptions (`ArgumentException`, `InvalidOperationException`) |
| Python | Exceptions (`ValueError`, `TypeError`) |
| JavaScript/TypeScript | `Error` objects or `Promise` rejection |

**Solution:** Map exceptions to common error codes or result types.

---

## Possibilities and Future Directions

### Possibility 1: Universal Factory Protocol

**Concept:** Define a protocol for factory invocation across runtimes.

```json
{
  "factoryMethod": "ModuleDescriptor.Create",
  "parameters": {
    "id": "analytics.module",
    "name": "Analytics Module",
    "version": "1.2.3"
  },
  "targetRuntime": "python"
}
```

**Host dispatches to runtime, runtime invokes factory, returns result.**

### Possibility 2: Code Generation from Schema

**Concept:** Define descriptors once, generate factories for all runtimes.

```yaml
# descriptors.yaml
ModuleDescriptor:
  fields:
    - name: id
      type: string
      required: true
      validation: "^[a-z0-9]+([.-][a-z0-9]+)*$"
    - name: name
      type: string
      required: true
    - name: version
      type: string
      required: true
      validation: "^\d+\.\d+\.\d+$"
```

**Tool generates:**
- C#: `ModuleDescriptor.cs` with `Create()` method
- Python: `module_descriptor.py` with `create()` function
- TypeScript: `moduleDescriptor.ts` with `createModuleDescriptor()`

### Possibility 3: Factory Registry Service

**Concept:** Central registry where runtimes register factory methods.

```typescript
// Node.js module registers factory
await registryClient.registerFactory({
  runtime: 'nodejs',
  type: 'ModuleDescriptor',
  factoryEndpoint: 'http://localhost:5000/factories/module-descriptor'
});

// .NET host invokes via registry
const descriptor = await registryClient.invokeFactory({
  runtime: 'nodejs',
  type: 'ModuleDescriptor',
  parameters: { id: 'test', name: 'Test', version: '1.0.0' }
});
```

### Possibility 4: AI-Assisted Factory Generation

**Concept:** AI generates factory methods from type definitions.

```
Input: ModuleDescriptor type definition
Output: Factory method with validation, error handling, documentation
```

**Benefits:**
- Consistent validation logic
- Comprehensive error messages
- Documentation generation

---

## Summary

### Key Insights

1. **Factory Patterns Translate Well Across Languages**
   - C#: Static methods
   - Python: @staticmethod or module-level functions
   - TypeScript: Exported functions or static methods

2. **Async Factories Need Runtime-Specific Implementations**
   - `Task<T>` in C#
   - `Coroutine` in Python
   - `Promise<T>` in TypeScript

3. **Validation Must Be Consistent**
   - Shared JSON schemas
   - Centralized validation services
   - Code generation from schemas

4. **Error Handling Varies**
   - Map to common error codes
   - Use result types instead of exceptions
   - Document error conditions clearly

5. **Cross-Runtime Invocation is Possible**
   - JSON-based factory configuration
   - gRPC factory services
   - Remote module loading protocols

### Recommendations for Meta-Platform

1. **Define factory protocols** in JSON or protobuf
2. **Generate language bindings** from shared schemas
3. **Use JSON Schema** for validation consistency
4. **Implement factory services** for remote invocation
5. **Document idioms** for each language
6. **Test factories** across all runtimes
7. **Consider result types** over exceptions for cross-language compatibility

### Related Patterns

- **Immutable Metadata:** Factories create immutable objects
- **Builder Pattern:** Alternative for complex construction
- **Result Objects:** Factory methods for result types
- **Async Patterns:** Async factories for I/O-bound operations

---

**Last Updated:** November 10, 2025
**VISORA Version:** .NET 9.0
**Document Type:** Illustrative Examples / Thought Experiments
