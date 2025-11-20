# Reflection Discovery Pattern - Meta-Platform Illustrations

**Last Updated:** November 10, 2025
**VISORA Version:** .NET 9.0
**Pattern Tier:** Tier 1 (Foundational)
**Related Patterns:** Plugin Architecture, Module Lifecycle, Convention over Configuration

---

## ⚠️ IMPORTANT DISCLAIMER

**This document contains ILLUSTRATIVE EXAMPLES ONLY.**

The code examples in this document are:
- **Conceptual demonstrations** of how reflection discovery could work in other languages
- **NOT production-ready implementations**
- **NOT tested or validated**
- **NOT official VISORA components**
- **Intended to illustrate patterns**, not provide working code

VISORA is a .NET/C# platform. These examples show how the reflection discovery pattern might be adapted to other language ecosystems (Python, Node.js) in a conceptual meta-platform scenario. They are provided for educational and architectural discussion purposes only.

**Do NOT use these examples in production without:**
1. Thorough testing and validation
2. Security review
3. Performance profiling
4. Error handling implementation
5. Proper integration with your specific runtime

---

## Table of Contents

1. [Cross-Platform Discovery Challenges](#cross-platform-discovery-challenges)
2. [Python Discovery Illustration](#python-discovery-illustration)
3. [Node.js Discovery Illustration](#nodejs-discovery-illustration)
4. [Convention Patterns Across Languages](#convention-patterns-across-languages)
5. [Introspection Strategies](#introspection-strategies)
6. [Performance Considerations](#performance-considerations)
7. [Testing Cross-Language Discovery](#testing-cross-language-discovery)
8. [Architectural Patterns](#architectural-patterns)

---

## Cross-Platform Discovery Challenges

### The Multi-Runtime Problem

When extending VISORA to support modules written in different languages, we face unique discovery challenges:

```
┌─────────────────────────────────────────────────────────────┐
│                    VISORA Host (.NET)                       │
│  ┌────────────────────────────────────────────────────────┐ │
│  │         Convention-Based Discovery                      │ │
│  │  - File: *.vixm.dll                                     │ │
│  │  - Type: Assembly.GetTypes()                            │ │
│  │  - Filter: typeof(VisoraModule).IsAssignableFrom()     │ │
│  └────────────────────────────────────────────────────────┘ │
│                            ↓                                 │
│  ┌────────────────────────────────────────────────────────┐ │
│  │      How to discover Python/Node.js modules?           │ │
│  │                                                          │ │
│  │  Challenges:                                            │ │
│  │  - Different module systems                             │ │
│  │  - Different type systems                               │ │
│  │  - Different introspection mechanisms                   │ │
│  │  - No common Assembly concept                           │ │
│  └────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
```

### Key Challenges

1. **Module System Differences**
   - .NET: Assemblies with types
   - Python: Modules with classes and decorators
   - Node.js: CommonJS/ESM with exports

2. **Type System Differences**
   - .NET: Strong static typing, reflection API
   - Python: Dynamic typing, `inspect` module
   - Node.js: Dynamic typing, prototype-based

3. **Discovery Mechanisms**
   - .NET: `Assembly.GetTypes()`, `Type.IsAssignableFrom()`
   - Python: `importlib`, `inspect.getmembers()`, decorators
   - Node.js: `require()`, property inspection, metadata

4. **Convention Differences**
   - .NET: File naming (`*.vixm.dll`), type inheritance
   - Python: Decorators (`@module`), naming conventions
   - Node.js: Export patterns, package.json metadata

---

## Python Discovery Illustration

### Conceptual Approach

**Convention Pattern:**
- File naming: `*_vixm.py` or `*_module.py`
- Decorator-based: `@visora.module`, `@visora.component`
- Import-based discovery: `importlib` to load modules

### Illustrative Example: Python Module Discovery

```python
# ==========================================
# ILLUSTRATIVE EXAMPLE - NOT PRODUCTION CODE
# ==========================================

import importlib
import importlib.util
import inspect
from pathlib import Path
from typing import Type, List, Callable
from dataclasses import dataclass

# Illustrative decorator for marking modules
def visora_module(name: str, version: str, description: str):
    """
    Decorator to mark a class as a VISORA module.

    ILLUSTRATIVE ONLY - shows concept, not production implementation.
    """
    def decorator(cls):
        cls._visora_module_metadata = {
            'name': name,
            'version': version,
            'description': description,
            'is_visora_module': True
        }
        return cls
    return decorator

# Illustrative decorator for marking components
def visora_component(name: str, description: str):
    """
    Decorator to mark a class as a VISORA component.

    ILLUSTRATIVE ONLY - shows concept, not production implementation.
    """
    def decorator(cls):
        cls._visora_component_metadata = {
            'name': name,
            'description': description,
            'is_visora_component': True
        }
        return cls
    return decorator


@dataclass
class ModuleDescriptor:
    """Illustrative module descriptor"""
    name: str
    version: str
    description: str


class PythonModuleDiscovery:
    """
    ILLUSTRATIVE EXAMPLE: Python module discovery

    This shows how convention-based discovery might work for Python
    modules in a meta-platform scenario.

    WARNING: This is NOT production code!
    """

    def __init__(self, search_pattern: str = "*_vixm.py"):
        self.search_pattern = search_pattern
        self.discovered_modules = []

    def enumerate_candidate_files(self, search_paths: List[Path]) -> List[Path]:
        """
        ILLUSTRATIVE: Find candidate Python module files

        Similar to ModuleLocator.EnumerateCandidateFiles in VISORA .NET
        """
        candidates = []

        for search_path in search_paths:
            if not search_path.exists():
                continue

            # Find files matching pattern
            for file_path in search_path.rglob(self.search_pattern):
                # Skip __pycache__ and other directories
                if '__pycache__' in file_path.parts:
                    continue
                if file_path.name.startswith('_') and file_path.name != '__init__.py':
                    continue

                candidates.append(file_path)

        return candidates

    def load_module_from_file(self, file_path: Path):
        """
        ILLUSTRATIVE: Load a Python module from file

        Similar to ModuleHandle.LoadAsync in VISORA .NET
        """
        # Create module spec from file
        module_name = file_path.stem
        spec = importlib.util.spec_from_file_location(module_name, file_path)

        if spec is None or spec.loader is None:
            raise ImportError(f"Cannot load module from {file_path}")

        # Load the module
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)

        return module

    def find_module_class(self, module):
        """
        ILLUSTRATIVE: Find the module class via inspection

        Similar to assembly.GetTypes().FirstOrDefault(...) in VISORA .NET
        """
        # Get all classes in the module
        for name, obj in inspect.getmembers(module, inspect.isclass):
            # Check if it has our decorator metadata
            if hasattr(obj, '_visora_module_metadata'):
                metadata = obj._visora_module_metadata
                if metadata.get('is_visora_module', False):
                    return obj

        return None

    def discover_components(self, module_class):
        """
        ILLUSTRATIVE: Discover components within a module

        Similar to module.DiscoverComponents(context) in VISORA .NET
        """
        components = []

        # Get the module instance's module (the Python module it was defined in)
        module = inspect.getmodule(module_class)

        if module is None:
            return components

        # Iterate through all classes in the module
        for name, obj in inspect.getmembers(module, inspect.isclass):
            # Skip abstract classes (convention)
            if inspect.isabstract(obj):
                continue

            # Check for component metadata
            if hasattr(obj, '_visora_component_metadata'):
                metadata = obj._visora_component_metadata
                if metadata.get('is_visora_component', False):
                    components.append({
                        'class': obj,
                        'metadata': metadata
                    })

        return components

    def inspect_module(self, file_path: Path) -> dict:
        """
        ILLUSTRATIVE: Full inspection of a module file

        Similar to ModuleHandle.InspectAsync in VISORA .NET
        """
        # Load module
        module = self.load_module_from_file(file_path)

        # Find module class
        module_class = self.find_module_class(module)
        if module_class is None:
            raise ValueError(f"No @visora_module class found in {file_path}")

        # Get module metadata
        module_metadata = module_class._visora_module_metadata

        # Instantiate module
        module_instance = module_class()

        # Discover components
        components = self.discover_components(module_class)

        return {
            'file_path': str(file_path),
            'module_class': module_class,
            'module_instance': module_instance,
            'descriptor': ModuleDescriptor(
                name=module_metadata['name'],
                version=module_metadata['version'],
                description=module_metadata['description']
            ),
            'components': components
        }


# ==========================================
# ILLUSTRATIVE USAGE EXAMPLE
# ==========================================

# Example module file: my_editor_vixm.py
"""
@visora_module(name="MyEditor", version="1.0.0", description="A text editor module")
class MyEditorModule:
    def initialize(self, context):
        print("Module initializing...")

    def shutdown(self, context):
        print("Module shutting down...")


@visora_component(name="EditorCore", description="Core editor functionality")
class EditorCoreComponent:
    def initialize(self, context):
        print("Component initializing...")

    def create_commands(self, context):
        return []  # Commands would be returned here


@visora_component(name="EditorUI", description="Editor user interface")
class EditorUIComponent:
    def initialize(self, context):
        print("UI initializing...")
"""

# Discovery process:
"""
discovery = PythonModuleDiscovery(search_pattern="*_vixm.py")

search_paths = [Path("./modules"), Path("./plugins")]
candidate_files = discovery.enumerate_candidate_files(search_paths)

for file_path in candidate_files:
    try:
        inspection = discovery.inspect_module(file_path)
        print(f"Found module: {inspection['descriptor'].name}")
        print(f"  Components: {len(inspection['components'])}")
        for comp in inspection['components']:
            print(f"    - {comp['metadata']['name']}")
    except Exception as e:
        print(f"Failed to inspect {file_path}: {e}")
"""
```

### Python vs .NET Comparison

| Aspect | .NET (VISORA) | Python (Illustrative) |
|--------|---------------|----------------------|
| **File Pattern** | `*.vixm.dll` | `*_vixm.py` |
| **Discovery API** | `Assembly.GetTypes()` | `inspect.getmembers()` |
| **Type Check** | `typeof(T).IsAssignableFrom()` | `hasattr(obj, '_metadata')` |
| **Instantiation** | `Activator.CreateInstance()` | `module_class()` |
| **Filtering** | `!IsAbstract, !IsInterface` | `!inspect.isabstract()` |
| **Convention** | Inheritance-based | Decorator-based |

---

## Node.js Discovery Illustration

### Conceptual Approach

**Convention Pattern:**
- File naming: `*.vixm.js` or `*.vixm.mjs`
- Export-based: `module.exports.visoraModule` or `export const visoraModule`
- Property inspection for metadata

### Illustrative Example: Node.js Module Discovery

```javascript
// ==========================================
// ILLUSTRATIVE EXAMPLE - NOT PRODUCTION CODE
// ==========================================

const fs = require('fs');
const path = require('path');

/**
 * ILLUSTRATIVE: Node.js module discovery
 *
 * Shows how convention-based discovery might work for Node.js
 * modules in a meta-platform scenario.
 *
 * WARNING: This is NOT production code!
 */
class NodeModuleDiscovery {
    constructor(searchPattern = '*.vixm.js') {
        this.searchPattern = searchPattern;
        this.discoveredModules = [];
    }

    /**
     * ILLUSTRATIVE: Find candidate module files
     * Similar to ModuleLocator.EnumerateCandidateFiles in VISORA .NET
     */
    enumerateCandidateFiles(searchPaths) {
        const candidates = [];

        for (const searchPath of searchPaths) {
            if (!fs.existsSync(searchPath)) {
                continue;
            }

            // Recursively find .vixm.js files
            this._findFilesRecursive(searchPath, candidates);
        }

        return candidates;
    }

    _findFilesRecursive(dir, candidates) {
        const entries = fs.readdirSync(dir, { withFileTypes: true });

        for (const entry of entries) {
            const fullPath = path.join(dir, entry.name);

            if (entry.isDirectory()) {
                // Skip node_modules and other directories
                if (entry.name === 'node_modules' || entry.name.startsWith('.')) {
                    continue;
                }
                this._findFilesRecursive(fullPath, candidates);
            } else if (entry.isFile()) {
                // Check if matches pattern
                if (entry.name.endsWith('.vixm.js') || entry.name.endsWith('.vixm.mjs')) {
                    candidates.push(fullPath);
                }
            }
        }
    }

    /**
     * ILLUSTRATIVE: Load a module from file
     * Similar to ModuleHandle.LoadAsync in VISORA .NET
     */
    loadModuleFromFile(filePath) {
        // Require the module (this loads it into Node.js)
        // WARNING: This could execute arbitrary code!
        const moduleExports = require(filePath);

        return moduleExports;
    }

    /**
     * ILLUSTRATIVE: Find the module class/function
     * Similar to assembly.GetTypes().FirstOrDefault(...) in VISORA .NET
     */
    findModuleClass(moduleExports) {
        // Check for conventional export patterns

        // Pattern 1: module.exports = class MyModule { }
        if (typeof moduleExports === 'function' &&
            moduleExports.visoraModule) {
            return moduleExports;
        }

        // Pattern 2: module.exports.VisoraModule = class { }
        if (moduleExports.VisoraModule) {
            return moduleExports.VisoraModule;
        }

        // Pattern 3: Look for any export with visoraModule metadata
        for (const key of Object.keys(moduleExports)) {
            const exported = moduleExports[key];
            if (typeof exported === 'function' && exported.visoraModule) {
                return exported;
            }
        }

        return null;
    }

    /**
     * ILLUSTRATIVE: Discover components within a module
     * Similar to module.DiscoverComponents(context) in VISORA .NET
     */
    discoverComponents(moduleExports) {
        const components = [];

        // Look for exports with component metadata
        for (const key of Object.keys(moduleExports)) {
            const exported = moduleExports[key];

            // Check if it's a component class/function
            if (typeof exported === 'function' && exported.visoraComponent) {
                components.push({
                    class: exported,
                    metadata: exported.visoraComponent
                });
            }
        }

        return components;
    }

    /**
     * ILLUSTRATIVE: Full inspection of a module file
     * Similar to ModuleHandle.InspectAsync in VISORA .NET
     */
    inspectModule(filePath) {
        // Load module
        const moduleExports = this.loadModuleFromFile(filePath);

        // Find module class
        const moduleClass = this.findModuleClass(moduleExports);
        if (!moduleClass) {
            throw new Error(`No VISORA module found in ${filePath}`);
        }

        // Get module metadata
        const moduleMetadata = moduleClass.visoraModule;
        if (!moduleMetadata) {
            throw new Error(`Module class has no visoraModule metadata`);
        }

        // Instantiate module
        const moduleInstance = new moduleClass();

        // Discover components
        const components = this.discoverComponents(moduleExports);

        return {
            filePath: filePath,
            moduleClass: moduleClass,
            moduleInstance: moduleInstance,
            descriptor: {
                name: moduleMetadata.name,
                version: moduleMetadata.version,
                description: moduleMetadata.description
            },
            components: components
        };
    }
}

// ==========================================
// ILLUSTRATIVE USAGE EXAMPLE
// ==========================================

// Example module file: my-editor.vixm.js
/*
class MyEditorModule {
    initialize(context) {
        console.log('Module initializing...');
    }

    shutdown(context) {
        console.log('Module shutting down...');
    }
}

// Attach metadata to the class
MyEditorModule.visoraModule = {
    name: 'MyEditor',
    version: '1.0.0',
    description: 'A text editor module'
};

class EditorCoreComponent {
    initialize(context) {
        console.log('Component initializing...');
    }

    createCommands(context) {
        return [];
    }
}

EditorCoreComponent.visoraComponent = {
    name: 'EditorCore',
    description: 'Core editor functionality'
};

class EditorUIComponent {
    initialize(context) {
        console.log('UI initializing...');
    }
}

EditorUIComponent.visoraComponent = {
    name: 'EditorUI',
    description: 'Editor user interface'
};

module.exports = MyEditorModule;
module.exports.EditorCoreComponent = EditorCoreComponent;
module.exports.EditorUIComponent = EditorUIComponent;
*/

// Discovery process:
/*
const discovery = new NodeModuleDiscovery('*.vixm.js');

const searchPaths = ['./modules', './plugins'];
const candidateFiles = discovery.enumerateCandidateFiles(searchPaths);

for (const filePath of candidateFiles) {
    try {
        const inspection = discovery.inspectModule(filePath);
        console.log(`Found module: ${inspection.descriptor.name}`);
        console.log(`  Components: ${inspection.components.length}`);
        for (const comp of inspection.components) {
            console.log(`    - ${comp.metadata.name}`);
        }
    } catch (error) {
        console.error(`Failed to inspect ${filePath}:`, error.message);
    }
}
*/

module.exports = NodeModuleDiscovery;
```

### Node.js vs .NET Comparison

| Aspect | .NET (VISORA) | Node.js (Illustrative) |
|--------|---------------|------------------------|
| **File Pattern** | `*.vixm.dll` | `*.vixm.js` |
| **Loading** | `Assembly.Load()` | `require()` |
| **Discovery API** | `Assembly.GetTypes()` | `Object.keys()` + inspection |
| **Type Check** | `typeof(T).IsAssignableFrom()` | `obj.visoraModule !== undefined` |
| **Instantiation** | `Activator.CreateInstance()` | `new ModuleClass()` |
| **Metadata** | Attributes/Inheritance | Property-based |
| **Convention** | Inheritance-based | Export pattern-based |

---

## Convention Patterns Across Languages

### Unified Convention Strategy

To support multiple language runtimes, we need a unified approach to conventions:

```
┌────────────────────────────────────────────────────────────┐
│              Universal Convention Patterns                  │
│                                                             │
│  1. FILE NAMING                                             │
│     .NET:    *.vixm.dll                                     │
│     Python:  *_vixm.py or *.vixm.py                         │
│     Node.js: *.vixm.js or *.vixm.mjs                        │
│                                                             │
│  2. METADATA ATTACHMENT                                     │
│     .NET:    Type inheritance + Descriptor property         │
│     Python:  Decorators (@visora_module)                    │
│     Node.js: Static properties (Class.visoraModule)         │
│                                                             │
│  3. DISCOVERY MECHANISM                                     │
│     .NET:    Assembly.GetTypes() + IsAssignableFrom()       │
│     Python:  importlib + inspect.getmembers()               │
│     Node.js: require() + Object.keys()                      │
│                                                             │
│  4. FILTERING                                               │
│     .NET:    !IsAbstract, !IsInterface, !IsNestedPrivate    │
│     Python:  !inspect.isabstract(), decorator check         │
│     Node.js: Metadata property existence check              │
│                                                             │
│  5. INSTANTIATION                                           │
│     .NET:    Activator.CreateInstance(type)                 │
│     Python:  module_class()                                 │
│     Node.js: new ModuleClass()                              │
└────────────────────────────────────────────────────────────┘
```

### Convention Design Principles

1. **Language-Native Patterns**
   - Use idiomatic patterns for each language
   - Don't force .NET concepts onto Python/Node.js
   - Respect ecosystem conventions

2. **Metadata Consistency**
   - All modules must expose: name, version, description
   - All components must expose: name, description
   - Use native metadata mechanisms (attributes, decorators, properties)

3. **Discovery Uniformity**
   - Host should use similar algorithm for all languages
   - File scan → Load → Find module → Discover components
   - Results normalized to common format

4. **Error Handling**
   - Graceful degradation when module fails to load
   - Clear error messages about missing metadata
   - Isolation prevents failures from cascading

---

## Introspection Strategies

### Safe Introspection Without Execution

**Challenge:** How to inspect a module without executing arbitrary code?

#### .NET Approach (Current)

```csharp
// .NET can inspect types without instantiation
var assembly = Assembly.LoadFrom(path);
var types = assembly.GetTypes();  // No code executed yet

foreach (var type in types)
{
    // Inspect metadata without creating instance
    var attributes = type.GetCustomAttributes();
    var properties = type.GetProperties();
    var methods = type.GetMethods();
}

// Only instantiate when ready
var instance = Activator.CreateInstance(type);
```

**Safety:** High - Metadata available before execution

---

#### Python Approach (Illustrative - Limited Safety)

```python
# ==========================================
# ILLUSTRATIVE EXAMPLE - NOT PRODUCTION CODE
# ==========================================

import ast
import inspect

# OPTION 1: AST Parsing (Safe but limited)
def inspect_module_via_ast(file_path):
    """
    ILLUSTRATIVE: Parse Python file without executing it

    Pros: Safe, no code execution
    Cons: Can't see runtime metadata, decorators are just nodes
    """
    with open(file_path, 'r') as f:
        source = f.read()

    tree = ast.parse(source)

    classes = []
    for node in ast.walk(tree):
        if isinstance(node, ast.ClassDef):
            # Check for decorators
            for decorator in node.decorator_list:
                if isinstance(decorator, ast.Call):
                    if isinstance(decorator.func, ast.Name):
                        if decorator.func.id == 'visora_module':
                            # Found a module class!
                            classes.append({
                                'name': node.name,
                                'is_module': True,
                                # Limited info available from AST
                            })

    return classes

# OPTION 2: Import with restricted execution (Complex, security risks)
# This would require sandboxing, not shown here for safety reasons

# OPTION 3: Metadata files (Hybrid approach)
def inspect_module_via_metadata(file_path):
    """
    ILLUSTRATIVE: Use companion metadata file

    Example: my_module_vixm.py → my_module_vixm.meta.json
    """
    import json

    meta_path = file_path.replace('.py', '.meta.json')
    if not os.path.exists(meta_path):
        return None

    with open(meta_path, 'r') as f:
        metadata = json.load(f)

    return metadata
```

**Tradeoff:** Python requires execution for full introspection, AST provides limited info

---

#### Node.js Approach (Illustrative - Execution Required)

```javascript
// ==========================================
// ILLUSTRATIVE EXAMPLE - NOT PRODUCTION CODE
// ==========================================

// OPTION 1: Require (executes code)
function inspectViaRequire(filePath) {
    // WARNING: This executes the module code!
    const moduleExports = require(filePath);
    return moduleExports;
}

// OPTION 2: VM context (sandboxed execution)
function inspectViaSandbox(filePath) {
    const vm = require('vm');
    const fs = require('fs');

    const code = fs.readFileSync(filePath, 'utf-8');

    // Create isolated context
    const sandbox = {
        module: { exports: {} },
        exports: {},
        require: require,  // Or limited require
        console: console   // Or limited console
    };

    try {
        vm.runInNewContext(code, sandbox, {
            filename: filePath,
            timeout: 1000  // Prevent infinite loops
        });

        return sandbox.module.exports;
    } catch (error) {
        console.error(`Sandbox execution failed: ${error.message}`);
        return null;
    }
}

// OPTION 3: Static analysis (very limited for JS)
function inspectViaStaticAnalysis(filePath) {
    // Would need JavaScript parser (e.g., Babel, Acorn)
    // Can find exports, but not runtime metadata
    const fs = require('fs');
    const acorn = require('acorn');  // Hypothetical

    const code = fs.readFileSync(filePath, 'utf-8');
    const ast = acorn.parse(code, { sourceType: 'module' });

    // Walk AST to find exports
    // Limited - can't see runtime property assignments
}
```

**Tradeoff:** Node.js requires execution for full introspection, static analysis very limited

---

### Hybrid Approach: Metadata + Introspection

**Best Practice:** Generate metadata files at build time, use introspection at runtime

```json
// my-module.vixm.json (generated at build time)
{
    "runtime": "nodejs",
    "entryPoint": "./my-module.vixm.js",
    "module": {
        "name": "MyModule",
        "version": "1.0.0",
        "description": "Example module",
        "class": "MyModuleClass"
    },
    "components": [
        {
            "name": "ComponentA",
            "description": "First component",
            "class": "ComponentA"
        },
        {
            "name": "ComponentB",
            "description": "Second component",
            "class": "ComponentB"
        }
    ]
}
```

**Workflow:**
1. **Build Time:** Static analysis generates `.vixm.json` metadata
2. **Discovery Time:** Host reads metadata files (fast, safe)
3. **Load Time:** Host loads actual module based on metadata
4. **Runtime:** Host uses introspection to verify metadata

---

## Performance Considerations

### Discovery Performance Across Runtimes

| Operation | .NET | Python | Node.js |
|-----------|------|--------|---------|
| **File Scan** | Fast | Fast | Fast |
| **Load Module** | Medium (Assembly load) | Slow (import + execute) | Medium (require + execute) |
| **Type Discovery** | Fast (GetTypes) | Slow (getmembers) | Fast (Object.keys) |
| **Metadata Check** | Fast (reflection) | Medium (hasattr) | Fast (property access) |
| **Instantiation** | Medium (Activator) | Fast (call) | Fast (new) |

**Key Insight:** Python and Node.js pay cost at import/require time (code execution), .NET pays at reflection time (but no execution).

---

### Optimization Strategies

#### Strategy 1: Metadata Caching

```javascript
// ILLUSTRATIVE EXAMPLE
class CachedModuleDiscovery {
    constructor() {
        this.metadataCache = new Map();
    }

    inspectModule(filePath) {
        // Check cache first
        const cached = this.metadataCache.get(filePath);
        if (cached) {
            // Check if file hasn't changed
            const stats = fs.statSync(filePath);
            if (stats.mtimeMs === cached.timestamp) {
                return cached.inspection;
            }
        }

        // Perform discovery
        const inspection = this._performInspection(filePath);

        // Cache result
        const stats = fs.statSync(filePath);
        this.metadataCache.set(filePath, {
            inspection,
            timestamp: stats.mtimeMs
        });

        return inspection;
    }
}
```

---

#### Strategy 2: Lazy Discovery

```python
# ILLUSTRATIVE EXAMPLE

class LazyModuleDiscovery:
    def __init__(self):
        self._discovered_files = None
        self._loaded_modules = {}

    @property
    def discovered_files(self):
        """Lazy file discovery"""
        if self._discovered_files is None:
            self._discovered_files = self._scan_files()
        return self._discovered_files

    def get_module(self, file_path):
        """Lazy module loading"""
        if file_path not in self._loaded_modules:
            self._loaded_modules[file_path] = self._load_module(file_path)
        return self._loaded_modules[file_path]
```

---

#### Strategy 3: Parallel Discovery

```csharp
// ILLUSTRATIVE: Parallel module inspection

public async Task<List<ModuleInspection>> InspectAllModulesAsync(
    IEnumerable<string> filePaths,
    CancellationToken cancellationToken)
{
    // For Python/Node.js modules, we might need to invoke
    // separate processes for isolation

    var tasks = filePaths.Select(async path =>
    {
        // Determine runtime
        var runtime = DetectRuntime(path);

        // Invoke runtime-specific discovery
        return runtime switch
        {
            "dotnet" => await InspectDotNetModule(path, cancellationToken),
            "python" => await InspectPythonModule(path, cancellationToken),
            "nodejs" => await InspectNodeJsModule(path, cancellationToken),
            _ => throw new NotSupportedException($"Unknown runtime for {path}")
        };
    });

    return (await Task.WhenAll(tasks)).ToList();
}
```

---

## Testing Cross-Language Discovery

### Testing Strategy

```csharp
// ILLUSTRATIVE: Multi-runtime discovery tests

[Theory]
[InlineData("dotnet", "TestModule.vixm.dll")]
[InlineData("python", "test_module_vixm.py")]
[InlineData("nodejs", "test-module.vixm.js")]
public async Task CanDiscoverModuleInRuntime(string runtime, string fileName)
{
    // Arrange
    var testModulePath = Path.Combine(TestDataDirectory, runtime, fileName);
    var discovery = new MultiRuntimeDiscovery();

    // Act
    var inspection = await discovery.InspectModuleAsync(testModulePath);

    // Assert
    Assert.NotNull(inspection);
    Assert.Equal("TestModule", inspection.Descriptor.Name);
    Assert.True(inspection.Components.Count > 0);
}
```

---

## Architectural Patterns

### Pattern 1: Runtime Adapter

```csharp
// ILLUSTRATIVE EXAMPLE

public interface IModuleRuntimeAdapter
{
    string RuntimeName { get; }
    bool CanHandle(string filePath);
    Task<ModuleInspection> InspectAsync(string filePath, CancellationToken ct);
    Task<IModuleHandle> LoadAsync(string filePath, CancellationToken ct);
}

public class DotNetRuntimeAdapter : IModuleRuntimeAdapter
{
    public string RuntimeName => "dotnet";

    public bool CanHandle(string filePath) =>
        filePath.EndsWith(".vixm.dll", StringComparison.OrdinalIgnoreCase);

    public Task<ModuleInspection> InspectAsync(string filePath, CancellationToken ct)
    {
        // Use existing ModuleHandle.InspectAsync
        // ...
    }

    // ...
}

public class PythonRuntimeAdapter : IModuleRuntimeAdapter
{
    public string RuntimeName => "python";

    public bool CanHandle(string filePath) =>
        filePath.EndsWith("_vixm.py", StringComparison.OrdinalIgnoreCase);

    public Task<ModuleInspection> InspectAsync(string filePath, CancellationToken ct)
    {
        // Invoke Python discovery script via subprocess
        // Parse results back to ModuleInspection
        // ...
    }

    // ...
}

public class MultiRuntimeModuleCatalog
{
    private readonly List<IModuleRuntimeAdapter> _adapters = new();

    public MultiRuntimeModuleCatalog()
    {
        _adapters.Add(new DotNetRuntimeAdapter());
        _adapters.Add(new PythonRuntimeAdapter());
        _adapters.Add(new NodeJsRuntimeAdapter());
    }

    public async Task<List<ModuleInspection>> DiscoverAllAsync(
        string searchPath,
        CancellationToken ct)
    {
        var results = new List<ModuleInspection>();

        // Scan for all potential module files
        var allFiles = Directory.EnumerateFiles(
            searchPath,
            "*vixm*",
            SearchOption.AllDirectories);

        foreach (var file in allFiles)
        {
            // Find appropriate adapter
            var adapter = _adapters.FirstOrDefault(a => a.CanHandle(file));
            if (adapter != null)
            {
                var inspection = await adapter.InspectAsync(file, ct);
                results.Add(inspection);
            }
        }

        return results;
    }
}
```

---

### Pattern 2: Interop Bridge

```csharp
// ILLUSTRATIVE: Bridging between runtimes

public class PythonInteropBridge
{
    private readonly Process _pythonProcess;

    public async Task<ModuleInspection> InspectPythonModuleAsync(string filePath)
    {
        // Start Python process with discovery script
        var psi = new ProcessStartInfo
        {
            FileName = "python",
            Arguments = $"-c \"import sys; sys.path.insert(0, '{DiscoveryScriptPath}'); " +
                       $"from visora_discovery import inspect_module; " +
                       $"import json; " +
                       $"print(json.dumps(inspect_module('{filePath}')))\"",
            RedirectStandardOutput = true,
            UseShellExecute = false
        };

        using var process = Process.Start(psi);
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        // Parse JSON result
        var result = JsonSerializer.Deserialize<PythonModuleInspection>(output);

        // Convert to VISORA ModuleInspection
        return ConvertToModuleInspection(result);
    }
}
```

---

## Summary

### Key Takeaways

1. **Convention-Based Discovery is Language-Specific**
   - Each language has native idioms for metadata (attributes, decorators, properties)
   - Discovery mechanisms differ (reflection, import introspection, require inspection)
   - File patterns should follow language conventions

2. **Introspection Challenges**
   - .NET: Safe reflection without execution
   - Python: Requires import (execution) for full introspection
   - Node.js: Requires require (execution) for full introspection
   - Mitigation: Metadata files + sandboxing

3. **Performance Varies**
   - .NET: Pay reflection cost, no execution cost
   - Python/Node.js: Pay execution cost at import/require time
   - Caching and lazy loading essential for all runtimes

4. **Unified Architecture**
   - Runtime adapter pattern isolates language-specific logic
   - Common inspection result format
   - Host orchestrates discovery across runtimes

5. **Security Considerations**
   - Loading Python/Node.js modules executes code
   - Sandboxing required for untrusted modules
   - Metadata files reduce need for execution during discovery

---

### Implementation Recommendations

If building a meta-platform VISORA:

1. **Start with .NET** - Already implemented, proven
2. **Add Python** - Use decorator pattern, subprocess isolation
3. **Add Node.js** - Use export pattern, VM context sandboxing
4. **Implement Runtime Adapters** - Isolate language-specific logic
5. **Use Hybrid Discovery** - Metadata files + runtime introspection
6. **Cache Aggressively** - Discovery is expensive, cache results
7. **Test Thoroughly** - Each runtime has unique failure modes

---

### Further Reading

- **Related Pattern:** Plugin Architecture - How modules are loaded across runtimes
- **Related Pattern:** Module Lifecycle - Managing initialization across languages
- **Security:** Sandboxing and isolation techniques
- **Performance:** Profiling discovery across runtimes
