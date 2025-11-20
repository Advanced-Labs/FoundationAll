# Template Method Pattern - Meta-Platform Illustrations

**Last Updated:** 2025-11-10
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
2. [Abstract Runtime Loader Templates](#abstract-runtime-loader-templates)
3. [Python Base Class Patterns (ABC)](#python-base-class-patterns-abc)
4. [Node.js Template Patterns](#nodejs-template-patterns)
5. [Cross-Language Template Coordination](#cross-language-template-coordination)
6. [Inheritance vs. Composition Trade-offs](#inheritance-vs-composition-trade-offs)
7. [Challenges & Considerations](#challenges--considerations)
8. [Possibilities & Future Directions](#possibilities--future-directions)

---

## Conceptual Adaptation

### From .NET Template Method to Polyglot Templates

**VISORA's .NET Pattern:**
```csharp
// Abstract base class with virtual hooks
public abstract class VisoraModule
{
    public abstract ModuleDescriptor Descriptor { get; }
    public virtual ValueTask InitializeAsync(...) => ValueTask.CompletedTask;
    public virtual ValueTask ShutdownAsync(...) => ValueTask.CompletedTask;
    public virtual IEnumerable<Type> DiscoverComponents(...) => /* ... */;
}

// Concrete implementation
public class MyModule : VisoraModule
{
    public override ModuleDescriptor Descriptor => /* ... */;
    public override ValueTask InitializeAsync(...) => /* custom logic */;
}
```

**Conceptual Meta-Platform Adaptation:**
```
┌──────────────────────────────────────────────────────┐
│         Language-Agnostic Lifecycle Protocol         │
│  1. load() or __init__ or constructor               │
│  2. get_descriptor() or descriptor property          │
│  3. initialize_async() or initializeAsync()          │
│  4. discover_components() or discoverComponents()    │
│  5. shutdown_async() or shutdownAsync()              │
│  6. dispose() or __del__ or destructor               │
└──────────────────────────────────────────────────────┘
                         │
        ┌────────────────┼────────────────┐
        ↓                ↓                ↓
┌──────────────┐  ┌──────────────┐  ┌──────────────┐
│Python (ABC)  │  │.NET (Virtual)│  │TypeScript    │
│              │  │              │  │ (Abstract)   │
│class Module  │  │abstract class│  │abstract class│
│ (ABC):       │  │VisoraModule  │  │VisoraModule  │
│  @abstract   │  │{             │  │{             │
│  descriptor  │  │  abstract    │  │  abstract    │
│             │  │  Descriptor  │  │  descriptor  │
└──────────────┘  └──────────────┘  └──────────────┘
```

**Key Adaptation Challenges:**
- Each language has different inheritance models
- Async patterns differ (async/await, Promises, callbacks)
- Default implementations vary (ABC abstract methods, virtual methods, etc.)

---

## Abstract Runtime Loader Templates

### ⚠️ ILLUSTRATIVE EXAMPLE: Abstract Module Loader

**Conceptual C# Template for Cross-Runtime Loading:**

```csharp
/// <summary>
/// Abstract template for loading modules across different runtimes.
/// ILLUSTRATIVE EXAMPLE - demonstrates template method across runtimes.
/// </summary>
public abstract class RuntimeModuleLoader<TModule, TDescriptor>
    where TModule : class
    where TDescriptor : class
{
    /// <summary>
    /// Template method: defines the algorithm for loading a module.
    /// </summary>
    public async Task<RuntimeModuleHandle<TModule, TDescriptor>> LoadAsync(
        string modulePath,
        CancellationToken cancellationToken = default)
    {
        // Step 1: Validate path (concrete)
        ValidatePath(modulePath);

        // Step 2: Resolve dependencies (hook - can be overridden)
        await ResolveDependenciesAsync(modulePath, cancellationToken);

        // Step 3: Load module (abstract - must be implemented)
        var module = await LoadModuleImplementationAsync(modulePath, cancellationToken);

        // Step 4: Extract descriptor (abstract - must be implemented)
        var descriptor = await ExtractDescriptorAsync(module, cancellationToken);

        // Step 5: Validate descriptor (concrete)
        ValidateDescriptor(descriptor);

        // Step 6: Wrap in handle (concrete)
        return CreateHandle(modulePath, module, descriptor);
    }

    /// <summary>
    /// Concrete step: Validate the module path.
    /// </summary>
    private void ValidatePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Module path cannot be empty", nameof(path));

        if (!File.Exists(path) && !Directory.Exists(path))
            throw new FileNotFoundException($"Module path not found: {path}");
    }

    /// <summary>
    /// Hook: Resolve dependencies before loading.
    /// Default implementation does nothing.
    /// Override to install packages, check prerequisites, etc.
    /// </summary>
    protected virtual Task ResolveDependenciesAsync(
        string modulePath,
        CancellationToken cancellationToken)
        => Task.CompletedTask;

    /// <summary>
    /// Abstract step: Load the module from the specified path.
    /// Must be implemented by concrete loaders (DotNetModuleLoader, PythonModuleLoader, etc.).
    /// </summary>
    protected abstract Task<TModule> LoadModuleImplementationAsync(
        string modulePath,
        CancellationToken cancellationToken);

    /// <summary>
    /// Abstract step: Extract the descriptor from the loaded module.
    /// Must be implemented by concrete loaders.
    /// </summary>
    protected abstract Task<TDescriptor> ExtractDescriptorAsync(
        TModule module,
        CancellationToken cancellationToken);

    /// <summary>
    /// Concrete step: Validate the descriptor.
    /// </summary>
    private void ValidateDescriptor(TDescriptor descriptor)
    {
        if (descriptor is null)
            throw new InvalidOperationException("Module returned null descriptor");

        // Additional validation (ID, version, etc.)
    }

    /// <summary>
    /// Concrete step: Create module handle.
    /// </summary>
    private RuntimeModuleHandle<TModule, TDescriptor> CreateHandle(
        string path,
        TModule module,
        TDescriptor descriptor)
        => new RuntimeModuleHandle<TModule, TDescriptor>(path, module, descriptor);
}

/// <summary>
/// Concrete loader for .NET modules.
/// </summary>
public sealed class DotNetModuleLoader
    : RuntimeModuleLoader<VisoraModule, ModuleDescriptor>
{
    protected override async Task<VisoraModule> LoadModuleImplementationAsync(
        string modulePath,
        CancellationToken cancellationToken)
    {
        // Use PluginLoader
        var loader = PluginLoader.CreateFromAssemblyFile(
            modulePath,
            sharedTypes: SharedTypes,
            isUnloadable: true);

        var assembly = loader.LoadDefaultAssembly();

        var moduleType = assembly.GetTypes()
            .FirstOrDefault(t => typeof(VisoraModule).IsAssignableFrom(t) && !t.IsAbstract);

        if (moduleType is null)
            throw new InvalidOperationException("No VisoraModule found");

        return (VisoraModule)Activator.CreateInstance(moduleType)!;
    }

    protected override Task<ModuleDescriptor> ExtractDescriptorAsync(
        VisoraModule module,
        CancellationToken cancellationToken)
        => Task.FromResult(module.Descriptor);
}

/// <summary>
/// Concrete loader for Python modules.
/// </summary>
public sealed class PythonModuleLoader
    : RuntimeModuleLoader<dynamic, ModuleDescriptor>
{
    protected override async Task ResolveDependenciesAsync(
        string modulePath,
        CancellationToken cancellationToken)
    {
        // Check for requirements.txt and install dependencies
        var requirementsPath = Path.Combine(modulePath, "requirements.txt");
        if (File.Exists(requirementsPath))
        {
            await RunPipInstallAsync(requirementsPath, cancellationToken);
        }
    }

    protected override async Task<dynamic> LoadModuleImplementationAsync(
        string modulePath,
        CancellationToken cancellationToken)
    {
        // Use Python.NET or subprocess to load module
        using (Py.GIL()) // Python.NET
        {
            dynamic sys = Py.Import("sys");
            sys.path.append(Path.GetDirectoryName(modulePath));

            var moduleName = Path.GetFileNameWithoutExtension(modulePath);
            dynamic module = Py.Import(moduleName);

            // Find module class
            // Assume convention: module has a class named after the file
            dynamic moduleClass = module.GetAttr(ToPascalCase(moduleName));
            return moduleClass();
        }
    }

    protected override async Task<ModuleDescriptor> ExtractDescriptorAsync(
        dynamic module,
        CancellationToken cancellationToken)
    {
        // Call Python module's descriptor property/method
        dynamic pythonDescriptor = module.descriptor;

        // Convert Python descriptor to .NET ModuleDescriptor
        return new ModuleDescriptor(
            Id: pythonDescriptor.id.ToString(),
            Name: pythonDescriptor.name.ToString(),
            Version: Version.Parse(pythonDescriptor.version.ToString()),
            Description: pythonDescriptor.description?.ToString(),
            Tags: null, // Parse if present
            RuntimeHints: null
        );
    }

    private async Task RunPipInstallAsync(string requirementsPath, CancellationToken ct)
    {
        // Run: pip install -r requirements.txt
        var psi = new ProcessStartInfo
        {
            FileName = "pip",
            Arguments = $"install -r \"{requirementsPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = Process.Start(psi);
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
            throw new InvalidOperationException("pip install failed");
    }

    private string ToPascalCase(string name) => /* ... */;
}
```

---

## Python Base Class Patterns (ABC)

### ⚠️ ILLUSTRATIVE EXAMPLE: Python Abstract Base Classes

**Python Template Method using ABC module:**

```python
"""
Python equivalent of VISORA's Template Method pattern using ABC.
ILLUSTRATIVE EXAMPLE - demonstrates pattern adaptation to Python.
"""
from abc import ABC, abstractmethod
from typing import List, Type, Optional
import asyncio


class ModuleDescriptor:
    """Immutable descriptor for a module."""
    def __init__(self, id: str, name: str, version: str, description: Optional[str] = None):
        self.id = id
        self.name = name
        self.version = version
        self.description = description


class VisoraModule(ABC):
    """
    Abstract base class for VISORA modules in Python.
    Uses Template Method pattern with abstract/default methods.
    """

    @property
    @abstractmethod
    def descriptor(self) -> ModuleDescriptor:
        """
        Module descriptor (ABSTRACT - must be implemented).
        Python equivalent of C#'s abstract property.
        """
        pass

    async def initialize_async(
        self,
        context: 'ModuleContext',
        cancellation_token: Optional[asyncio.Event] = None
    ) -> None:
        """
        Called when the module is being initialized.
        DEFAULT IMPLEMENTATION - does nothing.
        Override to customize initialization.

        This is the Python equivalent of C#'s:
        public virtual ValueTask InitializeAsync(...) => ValueTask.CompletedTask;
        """
        pass  # Default: no-op

    async def shutdown_async(
        self,
        context: 'ModuleContext',
        cancellation_token: Optional[asyncio.Event] = None
    ) -> None:
        """
        Called before the module is unloaded.
        DEFAULT IMPLEMENTATION - does nothing.
        Override to customize shutdown.
        """
        pass  # Default: no-op

    def discover_components(
        self,
        context: 'ModuleDiscoveryContext'
    ) -> List[Type['VisoraComponent']]:
        """
        Returns component types exposed by this module.
        DEFAULT IMPLEMENTATION - uses reflection to find all VisoraComponent subclasses.
        Override to customize component discovery.
        """
        # Default: reflection-based discovery
        return [
            cls for cls in self._enumerate_classes_in_module()
            if issubclass(cls, VisoraComponent) and cls is not VisoraComponent
        ]

    async def dispose_async(self) -> None:
        """
        Cleanup resources.
        DEFAULT IMPLEMENTATION - does nothing.
        Override to dispose of resources.
        """
        pass  # Default: no-op

    def _enumerate_classes_in_module(self) -> List[Type]:
        """
        Helper: Enumerate all classes defined in this module's file.
        """
        import inspect
        import sys

        module = sys.modules[self.__class__.__module__]
        return [
            obj for name, obj in inspect.getmembers(module)
            if inspect.isclass(obj) and obj.__module__ == module.__name__
        ]

    # Context manager support
    async def __aenter__(self):
        return self

    async def __aexit__(self, exc_type, exc_val, exc_tb):
        await self.dispose_async()


# Concrete module example
class MyPythonModule(VisoraModule):
    """
    Concrete module that overrides only what's needed.
    """

    def __init__(self):
        self._descriptor = ModuleDescriptor(
            id="example.python.module",
            name="My Python Module",
            version="1.0.0",
            description="Example Python module"
        )

    @property
    def descriptor(self) -> ModuleDescriptor:
        """Implement required abstract property."""
        return self._descriptor

    async def initialize_async(
        self,
        context,
        cancellation_token=None
    ) -> None:
        """Override to provide custom initialization."""
        print(f"Initializing {self.descriptor.name}...")
        # Custom initialization logic here

    # Did NOT override:
    # - shutdown_async (uses default)
    # - discover_components (uses default reflection)
    # - dispose_async (uses default)


# Full lifecycle override example
class ComplexPythonModule(VisoraModule):
    """
    Concrete module that overrides all lifecycle methods.
    """

    def __init__(self):
        self._descriptor = ModuleDescriptor(
            id="example.complex",
            name="Complex Module",
            version="1.0.0"
        )
        self._connection = None
        self._timer_task = None

    @property
    def descriptor(self) -> ModuleDescriptor:
        return self._descriptor

    async def initialize_async(
        self,
        context,
        cancellation_token=None
    ) -> None:
        """Custom initialization with async operations."""
        # Connect to a service
        self._connection = await self._connect_to_service()

        # Start a background task
        self._timer_task = asyncio.create_task(self._periodic_work())

    def discover_components(
        self,
        context
    ) -> List[Type['VisoraComponent']]:
        """Custom component discovery - filter by namespace."""
        components = super().discover_components(context)
        return [c for c in components if c.__module__.startswith("mycompany")]

    async def shutdown_async(
        self,
        context,
        cancellation_token=None
    ) -> None:
        """Custom shutdown logic."""
        if self._timer_task:
            self._timer_task.cancel()
            try:
                await self._timer_task
            except asyncio.CancelledError:
                pass

        if self._connection:
            await self._connection.close()

    async def dispose_async(self) -> None:
        """Clean up resources."""
        if self._connection:
            await self._connection.dispose()
            self._connection = None

    async def _connect_to_service(self):
        """Helper method."""
        # Simulate async connection
        await asyncio.sleep(0.1)
        return object()  # Placeholder

    async def _periodic_work(self):
        """Background task."""
        while True:
            await asyncio.sleep(5)
            # Do periodic work
```

---

## Node.js Template Patterns

### ⚠️ ILLUSTRATIVE EXAMPLE: TypeScript Abstract Classes

**TypeScript Template Method pattern:**

```typescript
/**
 * TypeScript equivalent of VISORA's Template Method pattern.
 * ILLUSTRATIVE EXAMPLE - demonstrates pattern adaptation to TypeScript.
 */

import { ModuleDescriptor, ComponentDescriptor } from './descriptors';
import { ModuleContext, ModuleDiscoveryContext } from './context';
import { VisoraComponent } from './component';

/**
 * Abstract base class for VISORA modules in TypeScript/Node.js.
 * Uses Template Method pattern with abstract/default methods.
 */
export abstract class VisoraModule {
    /**
     * Module descriptor (ABSTRACT - must be implemented).
     * TypeScript equivalent of C#'s abstract property.
     */
    abstract get descriptor(): ModuleDescriptor;

    /**
     * Called when the module is being initialized.
     * DEFAULT IMPLEMENTATION - does nothing.
     * Override to customize initialization.
     *
     * TypeScript equivalent of C#'s:
     * public virtual ValueTask InitializeAsync(...) => ValueTask.CompletedTask;
     */
    async initializeAsync(
        context: ModuleContext,
        cancellationToken?: AbortSignal
    ): Promise<void> {
        // Default: no-op
    }

    /**
     * Called before the module is unloaded.
     * DEFAULT IMPLEMENTATION - does nothing.
     * Override to customize shutdown.
     */
    async shutdownAsync(
        context: ModuleContext,
        cancellationToken?: AbortSignal
    ): Promise<void> {
        // Default: no-op
    }

    /**
     * Returns component types exposed by this module.
     * DEFAULT IMPLEMENTATION - uses reflection to find all VisoraComponent subclasses.
     * Override to customize component discovery.
     */
    discoverComponents(
        context: ModuleDiscoveryContext
    ): Array<new () => VisoraComponent> {
        // Default: reflection-based discovery
        return context.enumerateComponentCandidates();
    }

    /**
     * Cleanup resources.
     * DEFAULT IMPLEMENTATION - does nothing.
     * Override to dispose of resources.
     */
    async disposeAsync(): Promise<void> {
        // Default: no-op
    }
}

/**
 * Concrete module example - minimal override.
 */
export class MyNodeModule extends VisoraModule {
    private readonly _descriptor: ModuleDescriptor;

    constructor() {
        super();
        this._descriptor = {
            id: 'example.node.module',
            name: 'My Node Module',
            version: '1.0.0',
            description: 'Example Node.js module',
        };
    }

    get descriptor(): ModuleDescriptor {
        return this._descriptor;
    }

    async initializeAsync(
        context: ModuleContext,
        cancellationToken?: AbortSignal
    ): Promise<void> {
        console.log(`Initializing ${this.descriptor.name}...`);
        // Custom initialization logic
    }

    // Did NOT override:
    // - shutdownAsync (uses default)
    // - discoverComponents (uses default)
    // - disposeAsync (uses default)
}

/**
 * Concrete module example - full lifecycle override.
 */
export class ComplexNodeModule extends VisoraModule {
    private readonly _descriptor: ModuleDescriptor;
    private _connection?: any;
    private _intervalHandle?: NodeJS.Timeout;

    constructor() {
        super();
        this._descriptor = {
            id: 'example.complex',
            name: 'Complex Module',
            version: '1.0.0',
        };
    }

    get descriptor(): ModuleDescriptor {
        return this._descriptor;
    }

    async initializeAsync(
        context: ModuleContext,
        cancellationToken?: AbortSignal
    ): Promise<void> {
        // Connect to a service
        this._connection = await this.connectToService();

        // Start a timer
        this._intervalHandle = setInterval(() => {
            this.periodicWork();
        }, 5000);
    }

    discoverComponents(
        context: ModuleDiscoveryContext
    ): Array<new () => VisoraComponent> {
        // Custom discovery - filter by naming convention
        const components = super.discoverComponents(context);
        return components.filter(c => c.name.includes('Example'));
    }

    async shutdownAsync(
        context: ModuleContext,
        cancellationToken?: AbortSignal
    ): Promise<void> {
        // Stop timer
        if (this._intervalHandle) {
            clearInterval(this._intervalHandle);
            this._intervalHandle = undefined;
        }

        // Disconnect from service
        if (this._connection) {
            await this._connection.close();
        }
    }

    async disposeAsync(): Promise<void> {
        if (this._connection) {
            await this._connection.dispose();
            this._connection = undefined;
        }
    }

    private async connectToService(): Promise<any> {
        // Simulate async connection
        return new Promise(resolve => setTimeout(() => resolve({}), 100));
    }

    private periodicWork(): void {
        // Periodic work
    }
}
```

---

## Cross-Language Template Coordination

### Challenge: Enforcing Template Structure Across Languages

**Problem:** Each language has different mechanisms for abstract classes and virtual methods.

**Solution: Protocol Documentation + Runtime Validation**

```markdown
# VISORA Module Lifecycle Protocol

All modules, regardless of implementation language, MUST implement:

1. **Descriptor (Required)**
   - Property/method/attribute that returns module metadata
   - .NET: `public abstract ModuleDescriptor Descriptor { get; }`
   - Python: `@property @abstractmethod def descriptor(self) -> ModuleDescriptor:`
   - TypeScript: `abstract get descriptor(): ModuleDescriptor;`

2. **InitializeAsync (Optional)**
   - Called when module is being initialized
   - Default: no-op
   - .NET: `public virtual ValueTask InitializeAsync(...)`
   - Python: `async def initialize_async(self, ...): pass`
   - TypeScript: `async initializeAsync(...): Promise<void> {}`

3. **DiscoverComponents (Optional)**
   - Returns component types
   - Default: reflection-based discovery
   - .NET: `public virtual IEnumerable<Type> DiscoverComponents(...)`
   - Python: `def discover_components(self, ...) -> List[Type['VisoraComponent']]:`
   - TypeScript: `discoverComponents(...): Array<new () => VisoraComponent>`

... (etc for all lifecycle methods)
```

**Runtime validation in bridge:**

```csharp
public sealed class ProtocolValidator
{
    public void ValidateModule(IModuleHandle module)
    {
        // Check that descriptor is accessible
        try
        {
            var descriptor = module.Descriptor;
            if (descriptor == null)
                throw new ValidationException("Descriptor cannot be null");

            if (string.IsNullOrEmpty(descriptor.Id))
                throw new ValidationException("Descriptor.Id is required");
        }
        catch (Exception ex)
        {
            throw new ValidationException("Module does not implement required Descriptor", ex);
        }

        // Check that lifecycle methods are callable
        // (In runtime bridge, this is done via marshalling layer)
    }
}
```

---

## Inheritance vs. Composition Trade-offs

### Pattern: Template Method (Inheritance)

**Pros:**
- Clear hierarchy
- Polymorphic behavior
- Default implementations

**Cons:**
- Tight coupling to base class
- Single inheritance only (C#, Python)
- Fragile base class problem

### Alternative: Strategy Pattern (Composition)

**Composition-based approach:**

```python
# Instead of inheriting from VisoraModule, compose with strategies

class ModuleInitializer(ABC):
    @abstractmethod
    async def initialize_async(self, context, ct):
        pass

class ModuleShutdown(ABC):
    @abstractmethod
    async def shutdown_async(self, context, ct):
        pass

class Module:
    """Module using composition instead of inheritance."""
    def __init__(
        self,
        descriptor: ModuleDescriptor,
        initializer: Optional[ModuleInitializer] = None,
        shutdown: Optional[ModuleShutdown] = None
    ):
        self.descriptor = descriptor
        self._initializer = initializer
        self._shutdown = shutdown

    async def initialize_async(self, context, ct):
        if self._initializer:
            await self._initializer.initialize_async(context, ct)

    async def shutdown_async(self, context, ct):
        if self._shutdown:
            await self._shutdown.shutdown_async(context, ct)

# Usage
class MyInitializer(ModuleInitializer):
    async def initialize_async(self, context, ct):
        print("Custom initialization")

module = Module(
    descriptor=ModuleDescriptor(...),
    initializer=MyInitializer()
)
```

**Pros:**
- More flexible (can swap strategies)
- Avoids inheritance coupling
- Multiple "base classes" via multiple strategies

**Cons:**
- More objects to manage
- Less intuitive for simple cases
- More boilerplate

**VISORA's Choice:** Template Method (inheritance) for simplicity, especially for beginner-friendly module authoring.

---

## Challenges & Considerations

### 1. Language-Specific Async Models

**Challenge:** Each language has different async patterns.

**.NET:**
```csharp
public virtual async ValueTask InitializeAsync(...)
{
    await SomeOperationAsync();
}
```

**Python:**
```python
async def initialize_async(self, ...):
    await some_operation_async()
```

**TypeScript:**
```typescript
async initializeAsync(...): Promise<void> {
    await someOperationAsync();
}
```

**Consideration:** Runtime bridge must handle async/await semantics correctly when marshalling calls.

### 2. Default Implementation Semantics

**Challenge:** Different languages have different ways to express "default implementations."

**.NET:** Virtual methods with default body
**Python:** Concrete methods in abstract base class
**TypeScript:** Methods with default implementation in abstract class

**Consideration:** Document clearly which methods are required vs. optional.

### 3. Type System Differences

**Challenge:** .NET has strong typing, Python has duck typing, TypeScript has structural typing.

**Consideration:** Runtime bridge must validate types at boundaries.

### 4. Reflection Capabilities

**Challenge:** Component discovery uses reflection, which works differently per language.

**.NET:** `assembly.GetTypes()`
**Python:** `inspect.getmembers(module)`
**TypeScript:** Limited reflection (require explicit registration)

**Consideration:** May need explicit component registration for languages with limited reflection.

---

## Possibilities & Future Directions

### 1. Multi-Language Module Authoring

A single module could contain components in multiple languages:

```
MyModule/
├── module.json (descriptor)
├── dotnet/
│   └── DataAccessComponent.dll
├── python/
│   └── ml_component.py
└── nodejs/
    └── ui_component.ts
```

Each component uses template method in its native language.

### 2. Template Method with Middleware

Combine template method with middleware pattern:

```csharp
public abstract class MiddlewareModule : VisoraModule
{
    private readonly List<IModuleMiddleware> _middleware = new();

    protected void Use(IModuleMiddleware middleware)
        => _middleware.Add(middleware);

    public override async ValueTask InitializeAsync(
        ModuleContext context,
        CancellationToken ct)
    {
        // Pre-middleware
        foreach (var mw in _middleware)
            await mw.BeforeInitializeAsync(context, ct);

        // Hook
        await OnInitializeAsync(context, ct);

        // Post-middleware
        foreach (var mw in _middleware.AsEnumerable().Reverse())
            await mw.AfterInitializeAsync(context, ct);
    }

    protected abstract ValueTask OnInitializeAsync(ModuleContext context, CancellationToken ct);
}
```

### 3. Declarative Lifecycle Hooks

Use attributes/decorators instead of overriding methods:

```python
from visora import module, on_initialize, on_shutdown

@module(id="example", name="Example", version="1.0.0")
class MyModule:
    @on_initialize
    async def setup(self, context):
        # Initialization logic
        pass

    @on_shutdown
    async def teardown(self, context):
        # Shutdown logic
        pass
```

Framework discovers and calls decorated methods.

---

## Summary

A meta-platform adaptation of VISORA's Template Method pattern would:

**Per-Language Base Classes:**
- .NET: Abstract classes with virtual methods
- Python: ABC with abstractmethod and default implementations
- TypeScript: Abstract classes with default methods

**Common Lifecycle Protocol:**
- Documented contract for all languages
- Descriptor (required)
- Lifecycle hooks (optional with defaults)

**Runtime Bridge Coordination:**
- Validates that modules implement protocol
- Marshals lifecycle calls across runtimes
- Handles async/await differences

**Key Insight:** Template Method pattern adapts well to multiple languages, but requires careful documentation and runtime validation to ensure protocol compliance across different type systems and inheritance models.

---

**End of Document**
