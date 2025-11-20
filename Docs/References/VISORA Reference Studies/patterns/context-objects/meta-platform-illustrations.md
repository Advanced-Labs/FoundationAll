# Context Objects Pattern - Meta-Platform Illustrations

**Last Updated:** November 10, 2025
**VISORA Version:** .NET 9.0
**Pattern Tier:** Tier 1 (Foundational)
**Related Patterns:** Capability Negotiation, Parameter Objects, Dependency Injection

---

## ⚠️ IMPORTANT DISCLAIMER

**This document contains ILLUSTRATIVE EXAMPLES ONLY.**

The code examples in this document are:
- **Conceptual demonstrations** of how context objects could work across different language runtimes
- **NOT production-ready implementations**
- **NOT tested or validated**
- **NOT official VISORA components**
- **Intended to illustrate patterns**, not provide working code

VISORA is a .NET/C# platform. These examples show how the context objects pattern might be adapted to marshal contexts across language boundaries in a conceptual meta-platform scenario. They are provided for educational and architectural discussion purposes only.

**Do NOT use these examples in production without:**
1. Thorough testing and validation
2. Security review
3. Performance profiling
4. Error handling implementation
5. Proper integration with your specific runtime

---

## Table of Contents

1. [Cross-Language Context Challenges](#cross-language-context-challenges)
2. [Python Context Objects Illustration](#python-context-objects-illustration)
3. [Node.js Context Objects Illustration](#nodejs-context-objects-illustration)
4. [Serialization Strategies](#serialization-strategies)
5. [Proxy Patterns for Transparent Access](#proxy-patterns-for-transparent-access)
6. [Type Safety Across Boundaries](#type-safety-across-boundaries)
7. [Testing Cross-Language Contexts](#testing-cross-language-contexts)
8. [Architectural Patterns](#architectural-patterns)

---

## Cross-Language Context Challenges

### The Multi-Language Context Problem

When VISORA hosts modules in different language runtimes, we need to marshal contexts across language boundaries:

```
┌──────────────────────────────────────────────────────────┐
│                  VISORA Host (.NET)                      │
│  ┌────────────────────────────────────────────────────┐  │
│  │           ModuleContext (C#)                       │  │
│  │  - Descriptor: ModuleDescriptor                    │  │
│  │  - Services: IServiceProvider?                     │  │
│  │  - Capabilities: ICapabilityProvider               │  │
│  │  - Properties: IReadOnlyDictionary<string, object> │  │
│  └────────────────────────────────────────────────────┘  │
│                          ↓                                │
│           How to pass this to Python/Node.js?            │
│                          ↓                                │
│  ┌────────────────────────────────────────────────────┐  │
│  │         Context Marshaling Layer                   │  │
│  │  - Serialize context to JSON                       │  │
│  │  - Create proxy for capability calls               │  │
│  │  - Handle type conversions                         │  │
│  └────────────────────────────────────────────────────┘  │
│                          ↓                                │
└──────────────────────────────────────────────────────────┘
                           ↓
┌──────────────────────────────────────────────────────────┐
│              Python/Node.js Runtime                      │
│  ┌────────────────────────────────────────────────────┐  │
│  │         ModuleContext (Python/JavaScript)          │  │
│  │  - descriptor: dict/object                         │  │
│  │  - capabilities: CapabilityProxy                   │  │
│  │  - properties: dict/object                         │  │
│  └────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────┘
```

### Key Challenges

1. **Serialization**
   - .NET objects → JSON → Python/Node.js objects
   - Type information preservation
   - Nested object handling

2. **Capability Proxies**
   - .NET ICapabilityProvider → Python/Node.js proxy
   - Cross-process/cross-runtime calls
   - Type-safe access in dynamically-typed languages

3. **Reference Semantics**
   - .NET: Reference types
   - Python: Reference types
   - Node.js: Reference types
   - But across process boundaries → value semantics (copies)

4. **Type Conversions**
   - .NET types ↔ Python types
   - .NET types ↔ JavaScript types
   - Date/time, decimals, nulls

---

## Python Context Objects Illustration

### Conceptual Approach

**Strategy:**
1. Serialize .NET ModuleContext to JSON
2. Deserialize in Python as dictionary
3. Wrap in Python class for ergonomic access
4. Create proxy for capability calls back to .NET

### Illustrative Example: Python ModuleContext

```python
# ==========================================
# ILLUSTRATIVE EXAMPLE - NOT PRODUCTION CODE
# ==========================================

from typing import Dict, Any, Optional, TypeVar, Type, Generic
import json

T = TypeVar('T')

class ModuleDescriptor:
    """
    ILLUSTRATIVE: Python representation of ModuleDescriptor
    """
    def __init__(self, data: dict):
        self.name = data.get('name')
        self.version = data.get('version')
        self.description = data.get('description')

    def __repr__(self):
        return f"ModuleDescriptor(name={self.name}, version={self.version})"


class CapabilityProxy:
    """
    ILLUSTRATIVE: Proxy for calling .NET capabilities from Python

    This would communicate with .NET host via IPC to invoke capabilities.
    """

    def __init__(self, ipc_channel):
        self._ipc = ipc_channel

    def get_required(self, capability_type: str):
        """
        Get required capability, raises if not available.

        Example:
            console = context.capabilities.get_required('IConsoleHost')
        """
        # Send IPC request to .NET host
        request = {
            'command': 'get_capability',
            'type': capability_type,
            'required': True
        }

        response = self._ipc.send_and_wait(json.dumps(request))
        result = json.loads(response)

        if result.get('status') == 'error':
            raise RuntimeError(f"Required capability {capability_type} not available: {result.get('error')}")

        # Return proxy object for the capability
        return CapabilityInstance(capability_type, result.get('handle'), self._ipc)

    def get_optional(self, capability_type: str):
        """
        Get optional capability, returns None if not available.

        Example:
            logger = context.capabilities.get_optional('ILogger')
            if logger:
                logger.log_information("Hello")
        """
        try:
            request = {
                'command': 'get_capability',
                'type': capability_type,
                'required': False
            }

            response = self._ipc.send_and_wait(json.dumps(request))
            result = json.loads(response)

            if result.get('status') == 'success':
                return CapabilityInstance(capability_type, result.get('handle'), self._ipc)
            else:
                return None
        except:
            return None

    def is_available(self, capability_type: str) -> bool:
        """
        Check if capability is available.

        Example:
            if context.capabilities.is_available('IFileSystem'):
                fs = context.capabilities.get_required('IFileSystem')
        """
        request = {
            'command': 'check_capability',
            'type': capability_type
        }

        response = self._ipc.send_and_wait(json.dumps(request))
        result = json.loads(response)

        return result.get('available', False)


class CapabilityInstance:
    """
    ILLUSTRATIVE: Proxy for a specific capability instance

    Provides method calls that marshal to .NET via IPC.
    """

    def __init__(self, capability_type: str, handle: str, ipc_channel):
        self._type = capability_type
        self._handle = handle
        self._ipc = ipc_channel

    def __getattr__(self, method_name: str):
        """
        Dynamically handle method calls.

        Example:
            console.write_line("Hello")  # → IConsoleHost.WriteLineAsync("Hello")
        """
        def method_proxy(*args, **kwargs):
            # Convert Python method name to .NET convention
            # write_line → WriteLineAsync
            dotnet_method = self._to_pascal_case(method_name)
            if not dotnet_method.endswith('Async'):
                dotnet_method += 'Async'

            request = {
                'command': 'invoke_capability',
                'handle': self._handle,
                'method': dotnet_method,
                'args': args,
                'kwargs': kwargs
            }

            response = self._ipc.send_and_wait(json.dumps(request))
            result = json.loads(response)

            if result.get('status') == 'error':
                raise RuntimeError(f"Capability method failed: {result.get('error')}")

            return result.get('result')

        return method_proxy

    def _to_pascal_case(self, snake_case: str) -> str:
        """Convert snake_case to PascalCase"""
        components = snake_case.split('_')
        return ''.join(x.title() for x in components)


class ModuleContext:
    """
    ILLUSTRATIVE: Python representation of ModuleContext

    This is what Python modules receive when initialize() is called.
    """

    def __init__(self, data: dict, ipc_channel):
        # Descriptor
        self.descriptor = ModuleDescriptor(data.get('descriptor', {}))

        # Capabilities (proxy to .NET)
        self.capabilities = CapabilityProxy(ipc_channel)

        # Properties (plain dictionary)
        self.properties = data.get('properties', {})

        # Services (if provided, would need proxy too)
        # For now, we'll assume services are accessed via capabilities
        self._services_available = data.get('services_available', False)

    def get_property(self, key: str, default=None):
        """
        Get a property value with default.

        Example:
            debug = context.get_property('debug', False)
        """
        return self.properties.get(key, default)

    def __repr__(self):
        return f"ModuleContext(module={self.descriptor.name})"


# ==========================================
# ILLUSTRATIVE USAGE IN PYTHON MODULE
# ==========================================

class MyPythonModule:
    """
    Example Python module using context objects
    """

    def initialize(self, context: ModuleContext):
        """
        Initialize the module with context.

        This is called by VISORA host with marshaled context.
        """
        # Access descriptor
        print(f"Initializing {context.descriptor.name} v{context.descriptor.version}")

        # Access capabilities
        logger = context.capabilities.get_optional('ILogger')
        if logger:
            logger.log_information(f"Python module {context.descriptor.name} starting")

        console = context.capabilities.get_required('IConsoleHost')
        console.write_line("Hello from Python!")

        # Access properties
        debug = context.get_property('debug', False)
        if debug:
            print("Debug mode enabled")

        # Check capability availability
        if context.capabilities.is_available('IFileSystem'):
            fs = context.capabilities.get_required('IFileSystem')
            # Use file system
        else:
            print("File system not available")

    def shutdown(self, context: ModuleContext):
        """
        Shutdown the module.
        """
        print(f"Shutting down {context.descriptor.name}")


# ==========================================
# ILLUSTRATIVE: How host creates Python context
# ==========================================

def create_python_context_from_dotnet(dotnet_context_json: str, ipc_channel):
    """
    ILLUSTRATIVE: Convert .NET ModuleContext JSON to Python ModuleContext

    This would be called by the Python host wrapper.
    """
    data = json.loads(dotnet_context_json)
    return ModuleContext(data, ipc_channel)


# Example JSON from .NET:
"""
{
    "descriptor": {
        "name": "MyModule",
        "version": "1.0.0",
        "description": "Example module"
    },
    "services_available": true,
    "properties": {
        "debug": true,
        "theme": "dark"
    }
}
"""
```

---

## Node.js Context Objects Illustration

### Conceptual Approach

**Strategy:**
1. Serialize .NET ModuleContext to JSON
2. Deserialize in Node.js as JavaScript object
3. Wrap in ES6 class for ergonomic access
4. Create proxy for capability calls back to .NET

### Illustrative Example: Node.js ModuleContext

```javascript
// ==========================================
// ILLUSTRATIVE EXAMPLE - NOT PRODUCTION CODE
// ==========================================

/**
 * ILLUSTRATIVE: JavaScript representation of ModuleDescriptor
 */
class ModuleDescriptor {
    constructor(data) {
        this.name = data.name;
        this.version = data.version;
        this.description = data.description;
    }

    toString() {
        return `${this.name} v${this.version}`;
    }
}

/**
 * ILLUSTRATIVE: Proxy for calling .NET capabilities from Node.js
 */
class CapabilityProxy {
    constructor(ipcChannel) {
        this._ipc = ipcChannel;
    }

    /**
     * Get required capability, throws if not available.
     *
     * Example:
     *   const console = context.capabilities.getRequired('IConsoleHost');
     */
    async getRequired(capabilityType) {
        const request = {
            command: 'get_capability',
            type: capabilityType,
            required: true
        };

        const response = await this._ipc.sendAndWait(JSON.stringify(request));
        const result = JSON.parse(response);

        if (result.status === 'error') {
            throw new Error(`Required capability ${capabilityType} not available: ${result.error}`);
        }

        return new CapabilityInstance(capabilityType, result.handle, this._ipc);
    }

    /**
     * Get optional capability, returns null if not available.
     *
     * Example:
     *   const logger = context.capabilities.getOptional('ILogger');
     *   if (logger) {
     *       await logger.logInformation("Hello");
     *   }
     */
    async getOptional(capabilityType) {
        try {
            const request = {
                command: 'get_capability',
                type: capabilityType,
                required: false
            };

            const response = await this._ipc.sendAndWait(JSON.stringify(request));
            const result = JSON.parse(response);

            if (result.status === 'success') {
                return new CapabilityInstance(capabilityType, result.handle, this._ipc);
            } else {
                return null;
            }
        } catch {
            return null;
        }
    }

    /**
     * Check if capability is available.
     *
     * Example:
     *   if (await context.capabilities.isAvailable('IFileSystem')) {
     *       const fs = await context.capabilities.getRequired('IFileSystem');
     *   }
     */
    async isAvailable(capabilityType) {
        const request = {
            command: 'check_capability',
            type: capabilityType
        };

        const response = await this._ipc.sendAndWait(JSON.stringify(request));
        const result = JSON.parse(response);

        return result.available || false;
    }
}

/**
 * ILLUSTRATIVE: Proxy for a specific capability instance
 */
class CapabilityInstance {
    constructor(capabilityType, handle, ipcChannel) {
        this._type = capabilityType;
        this._handle = handle;
        this._ipc = ipcChannel;

        // Use Proxy to intercept method calls
        return new Proxy(this, {
            get(target, property) {
                // If it's a known property, return it
                if (property in target) {
                    return target[property];
                }

                // Otherwise, treat it as a method call to .NET
                return async function(...args) {
                    return await target._invokeMethod(property, args);
                };
            }
        });
    }

    async _invokeMethod(methodName, args) {
        // Convert camelCase to PascalCase for .NET
        const dotnetMethod = this._toPascalCase(methodName);
        const dotnetMethodAsync = dotnetMethod.endsWith('Async') ? dotnetMethod : dotnetMethod + 'Async';

        const request = {
            command: 'invoke_capability',
            handle: this._handle,
            method: dotnetMethodAsync,
            args: args
        };

        const response = await this._ipc.sendAndWait(JSON.stringify(request));
        const result = JSON.parse(response);

        if (result.status === 'error') {
            throw new Error(`Capability method failed: ${result.error}`);
        }

        return result.result;
    }

    _toPascalCase(camelCase) {
        return camelCase.charAt(0).toUpperCase() + camelCase.slice(1);
    }
}

/**
 * ILLUSTRATIVE: JavaScript representation of ModuleContext
 */
class ModuleContext {
    constructor(data, ipcChannel) {
        // Descriptor
        this.descriptor = new ModuleDescriptor(data.descriptor || {});

        // Capabilities (proxy to .NET)
        this.capabilities = new CapabilityProxy(ipcChannel);

        // Properties (plain object)
        this.properties = data.properties || {};

        // Services availability flag
        this._servicesAvailable = data.services_available || false;
    }

    /**
     * Get a property value with default.
     *
     * Example:
     *   const debug = context.getProperty('debug', false);
     */
    getProperty(key, defaultValue = null) {
        return this.properties.hasOwnProperty(key) ? this.properties[key] : defaultValue;
    }

    toString() {
        return `ModuleContext(module=${this.descriptor.name})`;
    }
}

// ==========================================
// ILLUSTRATIVE USAGE IN NODE.JS MODULE
// ==========================================

/**
 * Example Node.js module using context objects
 */
class MyNodeModule {
    /**
     * Initialize the module with context.
     *
     * This is called by VISORA host with marshaled context.
     */
    async initialize(context) {
        // Access descriptor
        console.log(`Initializing ${context.descriptor.name} v${context.descriptor.version}`);

        // Access capabilities
        const logger = await context.capabilities.getOptional('ILogger');
        if (logger) {
            await logger.logInformation(`Node.js module ${context.descriptor.name} starting`);
        }

        const console = await context.capabilities.getRequired('IConsoleHost');
        await console.writeLine("Hello from Node.js!");

        // Access properties
        const debug = context.getProperty('debug', false);
        if (debug) {
            console.log("Debug mode enabled");
        }

        // Check capability availability
        if (await context.capabilities.isAvailable('IFileSystem')) {
            const fs = await context.capabilities.getRequired('IFileSystem');
            // Use file system
        } else {
            console.log("File system not available");
        }
    }

    async shutdown(context) {
        console.log(`Shutting down ${context.descriptor.name}`);
    }
}

// ==========================================
// ILLUSTRATIVE: How host creates Node.js context
// ==========================================

function createNodeContextFromDotNet(dotnetContextJson, ipcChannel) {
    /**
     * ILLUSTRATIVE: Convert .NET ModuleContext JSON to Node.js ModuleContext
     */
    const data = JSON.parse(dotnetContextJson);
    return new ModuleContext(data, ipcChannel);
}

// Example JSON from .NET (same as Python):
/*
{
    "descriptor": {
        "name": "MyModule",
        "version": "1.0.0",
        "description": "Example module"
    },
    "services_available": true,
    "properties": {
        "debug": true,
        "theme": "dark"
    }
}
*/

// Export for use
module.exports = {
    ModuleContext,
    ModuleDescriptor,
    CapabilityProxy,
    createNodeContextFromDotNet
};
```

---

## Serialization Strategies

### Context Serialization Pattern

**From .NET to JSON:**

```csharp
// ILLUSTRATIVE EXAMPLE

public class ContextSerializer
{
    public static string SerializeModuleContext(ModuleContext context)
    {
        var data = new
        {
            descriptor = new
            {
                name = context.Descriptor.Name,
                version = context.Descriptor.Version,
                description = context.Descriptor.Description
            },
            services_available = context.Services != null,
            properties = context.Properties.ToDictionary(
                kvp => kvp.Key,
                kvp => SerializeValue(kvp.Value))
        };

        return JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });
    }

    private static object? SerializeValue(object? value)
    {
        // Handle common type conversions
        return value switch
        {
            null => null,
            bool b => b,
            int i => i,
            long l => l,
            double d => d,
            string s => s,
            DateTime dt => dt.ToString("O"),  // ISO 8601
            // Add more type conversions as needed
            _ => value.ToString()  // Fallback to string
        };
    }
}

// Usage:
var contextJson = ContextSerializer.SerializeModuleContext(moduleContext);
// Send to Python/Node.js via IPC
```

**From JSON to Python/Node.js:**

```python
# ILLUSTRATIVE: Python deserialization
def deserialize_module_context(json_str: str, ipc_channel) -> ModuleContext:
    data = json.loads(json_str)

    # Convert ISO 8601 strings back to datetime
    if 'properties' in data:
        for key, value in data['properties'].items():
            if isinstance(value, str) and 'T' in value:
                try:
                    from datetime import datetime
                    data['properties'][key] = datetime.fromisoformat(value)
                except:
                    pass  # Keep as string if parsing fails

    return ModuleContext(data, ipc_channel)
```

```javascript
// ILLUSTRATIVE: Node.js deserialization
function deserializeModuleContext(jsonStr, ipcChannel) {
    const data = JSON.parse(jsonStr);

    // Convert ISO 8601 strings back to Date
    if (data.properties) {
        for (const [key, value] of Object.entries(data.properties)) {
            if (typeof value === 'string' && value.includes('T')) {
                try {
                    data.properties[key] = new Date(value);
                } catch {
                    // Keep as string if parsing fails
                }
            }
        }
    }

    return new ModuleContext(data, ipcChannel);
}
```

---

## Proxy Patterns for Transparent Access

### Problem: Capability Calls Across Process Boundaries

**Challenge:** When a Python/Node.js module calls a capability method, we need to:
1. Marshal the call to .NET host
2. Invoke the actual capability
3. Marshal the result back

### Illustrative Solution: Dynamic Proxy with IPC

**Python Proxy with Magic Methods:**

```python
# ILLUSTRATIVE EXAMPLE

class TransparentCapabilityProxy:
    """
    Uses Python's __getattr__ to dynamically handle any method call.
    """

    def __init__(self, capability_type: str, handle: str, ipc_channel):
        self._type = capability_type
        self._handle = handle
        self._ipc = ipc_channel

    def __getattr__(self, method_name: str):
        """
        Intercept any attribute/method access.

        Example:
            proxy.write_line("Hello")  # Dynamically creates write_line method
        """
        def async_method(*args, **kwargs):
            # All calls are async (await in Python)
            import asyncio
            return asyncio.create_task(self._invoke_async(method_name, args, kwargs))

        def sync_method(*args, **kwargs):
            # Synchronous wrapper
            import asyncio
            return asyncio.run(self._invoke_async(method_name, args, kwargs))

        # Return appropriate wrapper based on naming convention
        if method_name.startswith('_'):
            # Private method, don't proxy
            raise AttributeError(f"'{type(self).__name__}' object has no attribute '{method_name}'")

        # Default to async
        return async_method

    async def _invoke_async(self, method_name: str, args: tuple, kwargs: dict):
        """
        Perform the actual IPC call.
        """
        # Convert to .NET naming
        dotnet_method = self._to_dotnet_method_name(method_name)

        # Serialize arguments
        serialized_args = [self._serialize_arg(arg) for arg in args]
        serialized_kwargs = {k: self._serialize_arg(v) for k, v in kwargs.items()}

        # Build IPC request
        request = {
            'command': 'invoke_capability',
            'handle': self._handle,
            'method': dotnet_method,
            'args': serialized_args,
            'kwargs': serialized_kwargs
        }

        # Send via IPC
        response_json = await self._ipc.send_and_wait_async(json.dumps(request))
        response = json.loads(response_json)

        if response.get('status') == 'error':
            raise RuntimeError(f"Method {dotnet_method} failed: {response.get('error')}")

        # Deserialize result
        return self._deserialize_result(response.get('result'))

    def _to_dotnet_method_name(self, python_name: str) -> str:
        """
        Convert Python naming to .NET naming.

        Examples:
            write_line → WriteLineAsync
            log_information → LogInformationAsync
        """
        # Snake case to Pascal case
        pascal = ''.join(word.capitalize() for word in python_name.split('_'))

        # Add Async suffix if not present
        if not pascal.endswith('Async'):
            pascal += 'Async'

        return pascal

    def _serialize_arg(self, arg):
        """Serialize Python argument to JSON-safe format"""
        # Handle common types
        if arg is None or isinstance(arg, (bool, int, float, str)):
            return arg
        elif isinstance(arg, (list, tuple)):
            return [self._serialize_arg(x) for x in arg]
        elif isinstance(arg, dict):
            return {k: self._serialize_arg(v) for k, v in arg.items()}
        else:
            # Fallback to string
            return str(arg)

    def _deserialize_result(self, result):
        """Deserialize .NET result to Python"""
        # Would handle .NET → Python type conversions
        return result
```

**Node.js Proxy with ES6 Proxy:**

```javascript
// ILLUSTRATIVE EXAMPLE

class TransparentCapabilityProxy {
    constructor(capabilityType, handle, ipcChannel) {
        this._type = capabilityType;
        this._handle = handle;
        this._ipc = ipcChannel;

        // Use ES6 Proxy for transparent method interception
        return new Proxy(this, {
            get(target, property, receiver) {
                // If it's a known property, return it
                if (property in target) {
                    return target[property];
                }

                // Don't proxy private properties (starting with _)
                if (property.toString().startsWith('_')) {
                    return undefined;
                }

                // Dynamically create method proxy
                return async function(...args) {
                    return await target._invokeMethod(property, args);
                };
            }
        });
    }

    async _invokeMethod(methodName, args) {
        // Convert to .NET naming
        const dotnetMethod = this._toDotNetMethodName(methodName);

        // Serialize arguments
        const serializedArgs = args.map(arg => this._serializeArg(arg));

        // Build IPC request
        const request = {
            command: 'invoke_capability',
            handle: this._handle,
            method: dotnetMethod,
            args: serializedArgs
        };

        // Send via IPC
        const responseJson = await this._ipc.sendAndWaitAsync(JSON.stringify(request));
        const response = JSON.parse(responseJson);

        if (response.status === 'error') {
            throw new Error(`Method ${dotnetMethod} failed: ${response.error}`);
        }

        // Deserialize result
        return this._deserializeResult(response.result);
    }

    _toDotNetMethodName(jsName) {
        /**
         * Convert JavaScript naming to .NET naming.
         *
         * Examples:
         *   writeLine → WriteLineAsync
         *   logInformation → LogInformationAsync
         */
        // Camel case to Pascal case
        const pascal = jsName.charAt(0).toUpperCase() + jsName.slice(1);

        // Add Async suffix if not present
        return pascal.endsWith('Async') ? pascal : pascal + 'Async';
    }

    _serializeArg(arg) {
        // Handle common types
        if (arg === null || arg === undefined ||
            typeof arg === 'boolean' ||
            typeof arg === 'number' ||
            typeof arg === 'string') {
            return arg;
        } else if (Array.isArray(arg)) {
            return arg.map(x => this._serializeArg(x));
        } else if (typeof arg === 'object') {
            const result = {};
            for (const [key, value] of Object.entries(arg)) {
                result[key] = this._serializeArg(value);
            }
            return result;
        } else {
            return arg.toString();
        }
    }

    _deserializeResult(result) {
        // Would handle .NET → JavaScript type conversions
        return result;
    }
}

// Usage example:
/*
const console = await context.capabilities.getRequired('IConsoleHost');

// This looks like a normal method call, but is actually proxied to .NET!
await console.writeLine("Hello from Node.js!");
//              ↓
//    Converted to: WriteLineAsync("Hello from Node.js!")
//              ↓
//    Sent via IPC to .NET host
//              ↓
//    IConsoleHost.WriteLineAsync("Hello from Node.js!") executed in .NET
//              ↓
//    Result marshaled back to Node.js
*/
```

---

## Type Safety Across Boundaries

### Challenge: Maintaining Type Information

**.NET has strong static typing:**
```csharp
var console = capabilities.GetRequired<IConsoleHost>();  // ← Type-safe at compile time
await console.WriteLineAsync("Hello");  // ← Type-checked
```

**Python/Node.js have dynamic typing:**
```python
console = context.capabilities.get_required('IConsoleHost')  # ← String-based
await console.write_line("Hello")  # ← No compile-time checking
```

### Illustrative Solution: Type Hints and Runtime Validation

**Python with Type Hints:**

```python
# ILLUSTRATIVE EXAMPLE

from typing import Protocol, runtime_checkable

@runtime_checkable
class IConsoleHost(Protocol):
    """
    Type hints for IConsoleHost capability.

    This provides IDE autocomplete and type checking.
    """
    async def write_line(self, text: str) -> None: ...
    async def read_line(self) -> str: ...
    async def clear(self) -> None: ...

@runtime_checkable
class ILogger(Protocol):
    """Type hints for ILogger capability"""
    async def log_information(self, message: str) -> None: ...
    async def log_warning(self, message: str) -> None: ...
    async def log_error(self, message: str) -> None: ...

# Usage with type hints:
async def initialize(context: ModuleContext) -> None:
    # Type hint tells IDE that console is IConsoleHost
    console: IConsoleHost = await context.capabilities.get_required('IConsoleHost')

    # IDE can now autocomplete and type-check
    await console.write_line("Hello")  # ← Autocomplete available!
    await console.clear()  # ← Type-checked!

    # Optional capability with type hint
    logger: ILogger | None = await context.capabilities.get_optional('ILogger')
    if logger:
        await logger.log_information("Module started")
```

**Node.js with TypeScript:**

```typescript
// ILLUSTRATIVE EXAMPLE

// Type definitions for capabilities
interface IConsoleHost {
    writeLine(text: string): Promise<void>;
    readLine(): Promise<string>;
    clear(): Promise<void>;
}

interface ILogger {
    logInformation(message: string): Promise<void>;
    logWarning(message: string): Promise<void>;
    logError(message: string): Promise<void>;
}

interface ModuleContext {
    descriptor: ModuleDescriptor;
    capabilities: CapabilityProxy;
    properties: Record<string, any>;
}

// Usage with TypeScript:
async function initialize(context: ModuleContext): Promise<void> {
    // Type assertion for capability
    const console = await context.capabilities.getRequired('IConsoleHost') as IConsoleHost;

    // TypeScript can now type-check
    await console.writeLine("Hello");  // ← Type-checked!
    await console.clear();  // ← Autocomplete available!

    // Optional capability
    const logger = await context.capabilities.getOptional('ILogger') as ILogger | null;
    if (logger) {
        await logger.logInformation("Module started");
    }
}
```

---

## Testing Cross-Language Contexts

### Testing Strategy

```csharp
// ILLUSTRATIVE: Test context marshaling

[Fact]
public async Task ContextMarshalingToPython_WorksCorrectly()
{
    // Arrange
    var descriptor = new ModuleDescriptor("TestModule", "1.0", "Test");
    var capabilities = CapabilityProviders.CreateBuilder()
        .Add<ILogger>(new TestLogger())
        .Build();
    var properties = new Dictionary<string, object?>
    {
        ["debug"] = true,
        ["theme"] = "dark"
    };

    var context = new ModuleContext(descriptor, null, capabilities, properties);

    // Act: Serialize to JSON
    var json = ContextSerializer.SerializeModuleContext(context);

    // Assert: Verify JSON structure
    var data = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
    Assert.NotNull(data);
    Assert.Equal("TestModule", data["descriptor"]["name"]);
    Assert.Equal(true, data["properties"]["debug"]);

    // Act: Send to Python and get response
    var pythonResponse = await SendToPythonAndGetResponse(json);

    // Assert: Python successfully received and used context
    Assert.True(pythonResponse.Success);
}
```

---

## Architectural Patterns

### Pattern 1: Context Adapter

```csharp
// ILLUSTRATIVE EXAMPLE

public interface IContextAdapter
{
    string SerializeContext(ModuleContext context);
    Task<ModuleContext> DeserializeContextAsync(string serialized);
}

public class PythonContextAdapter : IContextAdapter
{
    public string SerializeContext(ModuleContext context)
    {
        // Serialize to Python-friendly JSON
        return JsonSerializer.Serialize(new
        {
            descriptor = new
            {
                name = context.Descriptor.Name,
                version = context.Descriptor.Version,
                description = context.Descriptor.Description
            },
            properties = ConvertToPythonTypes(context.Properties)
        }, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower  // Python convention
        });
    }

    private Dictionary<string, object?> ConvertToPythonTypes(
        IReadOnlyDictionary<string, object?> properties)
    {
        var result = new Dictionary<string, object?>();
        foreach (var (key, value) in properties)
        {
            result[key] = value switch
            {
                DateTime dt => dt.ToString("O"),  // ISO 8601 for Python
                decimal d => (double)d,  // Python doesn't have decimal
                _ => value
            };
        }
        return result;
    }

    public Task<ModuleContext> DeserializeContextAsync(string serialized)
    {
        // For responses from Python back to .NET
        throw new NotImplementedException();
    }
}

public class NodeJsContextAdapter : IContextAdapter
{
    public string SerializeContext(ModuleContext context)
    {
        // Serialize to Node.js-friendly JSON
        return JsonSerializer.Serialize(new
        {
            descriptor = new
            {
                name = context.Descriptor.Name,
                version = context.Descriptor.Version,
                description = context.Descriptor.Description
            },
            properties = ConvertToJavaScriptTypes(context.Properties)
        }, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase  // JavaScript convention
        });
    }

    private Dictionary<string, object?> ConvertToJavaScriptTypes(
        IReadOnlyDictionary<string, object?> properties)
    {
        var result = new Dictionary<string, object?>();
        foreach (var (key, value) in properties)
        {
            result[key] = value switch
            {
                DateTime dt => dt.ToString("O"),  // ISO 8601 for JavaScript
                decimal d => (double)d,  // JavaScript doesn't have decimal
                _ => value
            };
        }
        return result;
    }

    public Task<ModuleContext> DeserializeContextAsync(string serialized)
    {
        throw new NotImplementedException();
    }
}
```

---

### Pattern 2: Capability Bridge

```csharp
// ILLUSTRATIVE EXAMPLE

public class CapabilityBridge
{
    private readonly Dictionary<string, object> _capabilities = new();
    private int _nextHandle = 1;

    public string RegisterCapability<T>(T capability) where T : class
    {
        var handle = $"cap_{_nextHandle++}";
        _capabilities[handle] = capability;
        return handle;
    }

    public async Task<object?> InvokeCapabilityAsync(
        string handle,
        string methodName,
        object[] args)
    {
        if (!_capabilities.TryGetValue(handle, out var capability))
            throw new InvalidOperationException($"Capability handle not found: {handle}");

        var type = capability.GetType();
        var method = type.GetMethod(methodName);
        if (method == null)
            throw new InvalidOperationException(
                $"Method {methodName} not found on {type.Name}");

        // Invoke method
        var result = method.Invoke(capability, args);

        // Handle async methods
        if (result is Task task)
        {
            await task;
            // Get result from Task<T>
            var resultProperty = task.GetType().GetProperty("Result");
            return resultProperty?.GetValue(task);
        }

        return result;
    }
}

// Usage in multi-runtime host:
var bridge = new CapabilityBridge();
var consoleHandle = bridge.RegisterCapability(console);

// When Python calls capability:
// Request: { command: "invoke_capability", handle: "cap_1", method: "WriteLineAsync", args: ["Hello"] }
var result = await bridge.InvokeCapabilityAsync("cap_1", "WriteLineAsync", new object[] { "Hello" });
```

---

## Summary

### Key Takeaways

1. **Context Marshaling is Complex**
   - Serialize .NET contexts to JSON
   - Deserialize in Python/Node.js
   - Type information partially lost

2. **Proxy Pattern Essential**
   - Capability calls must cross process boundaries
   - Dynamic proxies enable transparent access
   - IPC required for communication

3. **Type Safety Challenging**
   - .NET: Strong static typing
   - Python/Node.js: Dynamic typing
   - Use type hints/TypeScript to improve safety

4. **Serialization Challenges**
   - Type conversions (.NET ↔ Python/Node.js)
   - Date/time, decimals, nulls
   - Naming conventions (PascalCase ↔ snake_case/camelCase)

5. **Testing Crucial**
   - Test serialization/deserialization
   - Test proxy calls
   - Test type conversions

---

### Implementation Recommendations

If building a meta-platform VISORA:

1. **Use JSON for Context Marshaling**
   - Universal format
   - Easy to debug
   - Well-supported in all languages

2. **Implement Capability Proxies**
   - Transparent method calls
   - IPC-based communication
   - Error handling and retries

3. **Provide Type Definitions**
   - Python: Protocol classes
   - Node.js: TypeScript definitions
   - Improve developer experience

4. **Adapter Pattern for Each Runtime**
   - PythonContextAdapter
   - NodeJsContextAdapter
   - Handle runtime-specific conversions

5. **Test Thoroughly**
   - Round-trip serialization tests
   - Proxy invocation tests
   - Type conversion tests

---

### Further Reading

- **Related Pattern:** Capability Negotiation - How capabilities work
- **Related Pattern:** Module Lifecycle - When contexts are created
- **IPC Patterns:** JSON-RPC, Protocol Buffers, gRPC
- **Serialization:** Type-safe serialization across languages
- **Proxy Patterns:** Dynamic proxies, transparent RPC
