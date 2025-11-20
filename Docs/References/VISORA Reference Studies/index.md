# VISORA Platform Patterns - AI Agent Reference Index

**Last Updated**: 2025-11-10
**VISORA Version**: .NET 9.0
**Purpose**: Comprehensive pattern catalog for AI agents building polyglot meta-platforms

---

## 🎯 Quick Navigation

**New to VISORA?** Start here:
- [AI Agent Entry Point](00-quick-start/ai-agent-entry-point.md) - Get oriented quickly
- [Pattern Catalog Overview](00-quick-start/pattern-catalog.md) - High-level pattern summary

**Looking for specific patterns?** Jump to:
- [Foundation Patterns](#tier-1-foundation-patterns) - Core architectural patterns (Tier 1)
- [Communication Patterns](#tier-2-communication--execution-patterns) - Cross-boundary patterns (Tier 2)
- [Data Patterns](#tier-3-data--metadata-patterns) - Metadata and serialization (Tier 3)
- [Structural Patterns](#tier-4-structural-patterns) - Organization patterns (Tier 4)
- [Quality Patterns](#tier-5-quality--testing-patterns) - Testing and reliability (Tier 5)

**Need implementation guidance?**
- [Decisions](#architectural-decisions) - Why VISORA chose these patterns
- [Conventions](#conventions--code-organization) - How to apply patterns consistently
- [Blueprints](#implementation-blueprints) - Step-by-step implementation guides
- [Comparisons](#pattern-comparisons) - When to use which patterns

---

## 📚 Documentation Structure

Each pattern is documented in **two separate files**:

1. **`visora-analysis.md`** - Deep dive into VISORA's implementation
   - What the pattern is and why VISORA uses it
   - Code examples from VISORA codebase with file references
   - Implementation details and considerations
   - Tradeoffs and alternatives

2. **`meta-platform-illustrations.md`** - **Illustrative** adaptation examples
   - ⚠️ **Note**: These are EXAMPLES to stimulate imagination, NOT prescriptions
   - Shows possible ways to adapt patterns for polyglot meta-platforms
   - Python and Node.js integration illustrations
   - Runtime bridging possibilities
   - These are thought experiments, not definitive designs

---

## Tier 1: Foundation Patterns

**Critical for Meta-Platform Design** - These patterns form the foundation of polyglot runtime hosting and cross-language integration.

### 1. Plugin Architecture (Unloadable Assemblies)

**Pattern**: Isolated plugin loading with unloadable contexts and shared type contracts

**VISORA Implementation**: McMaster.NETCore.Plugins with shared types list, unloadable assembly contexts

**Meta-Platform Relevance**: Foundation for hosting Python/Node.js runtimes in isolated domains with hot-reload capability

**Files**:
- [`patterns/plugin-architecture/visora-analysis.md`](patterns/plugin-architecture/visora-analysis.md)
  - How VISORA loads `.vixm.dll` modules in isolated contexts
  - Shared types pattern to prevent version conflicts
  - Unloading and hot-swapping mechanics
  - PluginLoader configuration and lifecycle

- [`patterns/plugin-architecture/meta-platform-illustrations.md`](patterns/plugin-architecture/meta-platform-illustrations.md)
  - **Illustrative examples** of hosting Python/Node.js runtimes
  - Possible isolation strategies for polyglot environments
  - Conceptual runtime bridging approaches
  - Hot-reload across language boundaries (thought experiments)

**When to read**: Building runtime host that loads code dynamically, need hot-swap capability, polyglot isolation

---

### 2. Capability Negotiation (Type-Safe DI Alternative)

**Pattern**: Type-safe service negotiation via capability providers instead of traditional DI containers

**VISORA Implementation**: `ICapabilityProvider` interface with `GetOptional<T>` and `GetRequired<T>` methods

**Meta-Platform Relevance**: Foundation for cross-runtime capability bridging (Python code accessing .NET services)

**Files**:
- [`patterns/capability-negotiation/visora-analysis.md`](patterns/capability-negotiation/visora-analysis.md)
  - Why VISORA chose capability providers over service locators
  - ICapabilityProvider interface design and implementation
  - CapabilityProviderBuilder pattern
  - Type-safe negotiation mechanics

- [`patterns/capability-negotiation/meta-platform-illustrations.md`](patterns/capability-negotiation/meta-platform-illustrations.md)
  - **Illustrative examples** of cross-language capability access
  - Conceptual Python framework calling .NET services
  - Possible Node.js integration patterns
  - Thought experiments on capability discovery across runtimes

**When to read**: Building service discovery across languages, need type-safe APIs, avoiding global service locators

---

### 3. Reflection-First Discovery (Convention over Configuration)

**Pattern**: Discover modules/components via reflection and conventions, no manifest files required

**VISORA Implementation**: Assembly scanning with `Assembly.GetTypes()`, filtering by conventions

**Meta-Platform Relevance**: Finding Python modules, Node.js packages without external configuration files

**Files**:
- [`patterns/reflection-discovery/visora-analysis.md`](patterns/reflection-discovery/visora-analysis.md)
  - VISORA's reflection-based type discovery
  - Convention patterns (VisoraModule implementations, naming patterns)
  - ModuleDiscoveryContext implementation
  - Performance considerations and filtering strategies

- [`patterns/reflection-discovery/meta-platform-illustrations.md`](patterns/reflection-discovery/meta-platform-illustrations.md)
  - **Illustrative examples** of discovering Python decorators/Node.js exports
  - Conceptual convention patterns for polyglot discovery
  - Possible introspection across runtime boundaries
  - Thought experiments on metadata extraction from other languages

**When to read**: Building module discovery, want convention-based loading, need to find code across languages

---

### 4. Module Lifecycle Management (4-Phase Lifecycle)

**Pattern**: Explicit Load → Initialize → Shutdown → Dispose lifecycle with async support

**VISORA Implementation**: ModuleHandle with EnsureInitializedAsync, ShutdownAsync, DisposeAsync

**Meta-Platform Relevance**: Coordinating lifecycles across Python/Node.js/other runtimes

**Files**:
- [`patterns/module-lifecycle/visora-analysis.md`](patterns/module-lifecycle/visora-analysis.md)
  - Four-phase lifecycle in detail
  - Idempotent initialization pattern
  - Graceful shutdown strategies
  - Async disposal and resource cleanup

- [`patterns/module-lifecycle/meta-platform-illustrations.md`](patterns/module-lifecycle/meta-platform-illustrations.md)
  - **Illustrative examples** of Python runtime lifecycle coordination
  - Conceptual Node.js startup/shutdown patterns
  - Possible dependency ordering across runtimes
  - Thought experiments on cross-runtime state management

**When to read**: Managing startup/shutdown sequences, coordinating multiple runtimes, need graceful cleanup

---

### 5. Context Objects (Rich Parameter Passing)

**Pattern**: Pass rich execution context through ModuleContext, ComponentContext, CommandContext

**VISORA Implementation**: Context records containing descriptors, capabilities, services, parameters

**Meta-Platform Relevance**: Passing data and metadata across runtime boundaries with proper marshaling

**Files**:
- [`patterns/context-objects/visora-analysis.md`](patterns/context-objects/visora-analysis.md)
  - Three context types (Module, Component, Command)
  - Context composition and propagation
  - Capability access through contexts
  - Immutability and thread-safety considerations

- [`patterns/context-objects/meta-platform-illustrations.md`](patterns/context-objects/meta-platform-illustrations.md)
  - **Illustrative examples** of context marshaling to Python/Node.js
  - Conceptual serialization strategies
  - Possible proxy object patterns for cross-runtime contexts
  - Thought experiments on maintaining type safety across boundaries

**When to read**: Passing complex parameters across boundaries, need rich execution context, marshaling data between languages

---

## Tier 2: Communication & Execution Patterns

**Important for Platform Robustness** - These patterns enable uniform communication and execution across boundaries.

### 6. Command Execution Pattern

**Pattern**: Abstract commands with ExecuteAsync returning CommandResult, supporting multiple surfaces

**VISORA Implementation**: VisoraCommand abstract class with CommandContext and CommandResult

**Meta-Platform Relevance**: Uniform invocation interface regardless of implementation language

**Files**:
- [`patterns/command-execution/visora-analysis.md`](patterns/command-execution/visora-analysis.md)
  - Command abstraction design
  - CommandDescriptor metadata
  - ExecuteAsync implementation patterns
  - Command registration and discovery

- [`patterns/command-execution/meta-platform-illustrations.md`](patterns/command-execution/meta-platform-illustrations.md)
  - **Illustrative examples** of Python functions as commands
  - Conceptual Node.js async command patterns
  - Possible decoration/registration approaches
  - Thought experiments on cross-language command invocation

**When to read**: Building command/action systems, need uniform invocation, executing code across languages

---

### 7. Async Patterns (Async/Await Throughout)

**Pattern**: ValueTask, async/await, CancellationToken propagation throughout the stack

**VISORA Implementation**: All I/O and lifecycle methods are async, ConfigureAwait(false) in libraries

**Meta-Platform Relevance**: Bridging .NET async with Python asyncio, Node.js promises

**Files**:
- [`patterns/async-patterns/visora-analysis.md`](patterns/async-patterns/visora-analysis.md)
  - ValueTask vs Task optimization
  - CancellationToken propagation patterns
  - ConfigureAwait usage in libraries
  - Async disposal (IAsyncDisposable)

- [`patterns/async-patterns/meta-platform-illustrations.md`](patterns/async-patterns/meta-platform-illustrations.md)
  - **Illustrative examples** of bridging async models
  - Conceptual Python asyncio integration
  - Possible Node.js Promise coordination
  - Thought experiments on async/await across runtimes

**When to read**: Coordinating async operations across languages, managing cancellation, optimizing async performance

---

### 8. Result Objects vs Exceptions

**Pattern**: Commands return result objects (Success/Failed/Cancelled) instead of throwing exceptions

**VISORA Implementation**: CommandResult readonly struct with outcome enum and optional payload

**Meta-Platform Relevance**: Propagating errors across runtime boundaries without losing semantics

**Files**:
- [`patterns/result-objects/visora-analysis.md`](patterns/result-objects/visora-analysis.md)
  - CommandResult structure and factory methods
  - When to use results vs exceptions
  - Payload handling and type safety
  - Error message propagation

- [`patterns/result-objects/meta-platform-illustrations.md`](patterns/result-objects/meta-platform-illustrations.md)
  - **Illustrative examples** of Python result objects
  - Conceptual error marshaling across boundaries
  - Possible Node.js Result/Either patterns
  - Thought experiments on exception translation

**When to read**: Handling errors across language boundaries, avoiding exception marshaling issues, user-facing operations

---

### 9. Multi-Surface Execution

**Pattern**: Same command executes on CLI, UI, Automation, or Remote surfaces uniformly

**VISORA Implementation**: CommandSurface enum in CommandContext, surface-agnostic execution

**Meta-Platform Relevance**: Invoke Python/Node.js code from any interface uniformly

**Files**:
- [`patterns/multi-surface-execution/visora-analysis.md`](patterns/multi-surface-execution/visora-analysis.md)
  - CommandSurface enum design
  - Surface-agnostic command implementation
  - Surface-specific adaptations (CLI vs UI)
  - Context propagation across surfaces

- [`patterns/multi-surface-execution/meta-platform-illustrations.md`](patterns/multi-surface-execution/meta-platform-illustrations.md)
  - **Illustrative examples** of invoking Python from multiple surfaces
  - Conceptual universal invocation layer
  - Possible surface abstraction for Node.js
  - Thought experiments on remote execution

**When to read**: Building multi-interface systems, need uniform invocation, executing across CLI/UI/API boundaries

---

## Tier 3: Data & Metadata Patterns

**Important for Interoperability** - These patterns enable language-agnostic metadata and data contracts.

### 10. Immutable Metadata (Sealed Records)

**Pattern**: Use sealed record types for all metadata (descriptors, inspection results)

**VISORA Implementation**: ModuleDescriptor, ComponentDescriptor, CommandDescriptor as sealed records

**Meta-Platform Relevance**: Language-agnostic contracts that serialize cleanly to JSON/protobuf

**Files**:
- [`patterns/immutable-metadata/visora-analysis.md`](patterns/immutable-metadata/visora-analysis.md)
  - Sealed record design principles
  - Descriptor factory methods
  - Value equality semantics
  - Serialization considerations

- [`patterns/immutable-metadata/meta-platform-illustrations.md`](patterns/immutable-metadata/meta-platform-illustrations.md)
  - **Illustrative examples** of JSON-based metadata contracts
  - Conceptual Python dataclass equivalents
  - Possible Node.js metadata patterns
  - Thought experiments on schema evolution

**When to read**: Defining cross-language contracts, need immutable metadata, serialization across boundaries

---

### 11. Factory Patterns

**Pattern**: Static factory methods for creating instances (Descriptor.Create, ModuleHandle.LoadAsync)

**VISORA Implementation**: Factory methods on descriptors and handles, consistent creation patterns

**Meta-Platform Relevance**: Runtime-specific instantiation strategies

**Files**:
- [`patterns/factory-patterns/visora-analysis.md`](patterns/factory-patterns/visora-analysis.md)
  - Factory method design in VISORA
  - Static vs instance factories
  - Async factory patterns
  - Error handling in factories

- [`patterns/factory-patterns/meta-platform-illustrations.md`](patterns/factory-patterns/meta-platform-illustrations.md)
  - **Illustrative examples** of runtime-specific factories
  - Conceptual Python module instantiation
  - Possible Node.js factory patterns
  - Thought experiments on polyglot object creation

**When to read**: Creating instances across runtimes, need consistent creation patterns, managing complex initialization

---

### 12. Builder Pattern

**Pattern**: Fluent builder APIs for complex configuration (CapabilityProviderBuilder)

**VISORA Implementation**: CapabilityProviderBuilder with Add<T> methods and Build()

**Meta-Platform Relevance**: Building cross-runtime service configurations programmatically

**Files**:
- [`patterns/builder-pattern/visora-analysis.md`](patterns/builder-pattern/visora-analysis.md)
  - Builder implementation in CapabilityProviderBuilder
  - Fluent interface design
  - Validation and error handling
  - Immutable product pattern

- [`patterns/builder-pattern/meta-platform-illustrations.md`](patterns/builder-pattern/meta-platform-illustrations.md)
  - **Illustrative examples** of configuration builders across languages
  - Conceptual Python builder patterns
  - Possible Node.js fluent APIs
  - Thought experiments on cross-runtime configuration

**When to read**: Building complex configurations, need fluent APIs, configuring cross-runtime services

---

## Tier 4: Structural Patterns

**Architectural Understanding** - These patterns organize code and manage complexity.

### 13. Layered Architecture

**Pattern**: Three-tier separation (Contracts → Core → Hosts) with dependency inversion

**VISORA Implementation**: Visora.Contracts (no deps), Visora.Core (depends on Contracts), Hosts (depend on Core)

**Meta-Platform Relevance**: Organizing polyglot platform into layers (Runtime Bridge → Core → Language Bindings)

**Files**:
- [`patterns/layered-architecture/visora-analysis.md`](patterns/layered-architecture/visora-analysis.md)
  - Three-tier layer design
  - Dependency direction and inversion
  - Project structure and references
  - Cross-cutting concerns handling

- [`patterns/layered-architecture/meta-platform-illustrations.md`](patterns/layered-architecture/meta-platform-illustrations.md)
  - **Illustrative examples** of meta-platform layering
  - Conceptual runtime bridge layer design
  - Possible language binding organization
  - Thought experiments on polyglot layer separation

**When to read**: Organizing large polyglot codebases, managing dependencies, separating concerns across runtimes

---

### 14. Registry Pattern

**Pattern**: Central registry for modules with lookup and lifecycle management (ModuleCatalog)

**VISORA Implementation**: ModuleCatalog maintaining ModuleHandle collection with GetById lookup

**Meta-Platform Relevance**: Tracking all loaded modules across multiple runtimes

**Files**:
- [`patterns/registry-pattern/visora-analysis.md`](patterns/registry-pattern/visora-analysis.md)
  - ModuleCatalog implementation
  - Registration and deduplication
  - Lookup strategies (by ID, by type)
  - Lifecycle coordination through registry

- [`patterns/registry-pattern/meta-platform-illustrations.md`](patterns/registry-pattern/meta-platform-illustrations.md)
  - **Illustrative examples** of multi-runtime registries
  - Conceptual unified module catalog
  - Possible cross-runtime lookup patterns
  - Thought experiments on distributed registries

**When to read**: Managing collections of modules/plugins, need central lookup, coordinating across runtimes

---

### 15. Template Method

**Pattern**: Abstract base classes define algorithm structure with virtual hooks

**VISORA Implementation**: VisoraModule base class with virtual InitializeAsync, DiscoverComponents

**Meta-Platform Relevance**: Common lifecycle pattern across different runtime loaders

**Files**:
- [`patterns/template-method/visora-analysis.md`](patterns/template-method/visora-analysis.md)
  - Abstract base class design
  - Virtual hook points (Initialize, Shutdown)
  - Default implementations and overrides
  - Lifecycle template pattern

- [`patterns/template-method/meta-platform-illustrations.md`](patterns/template-method/meta-platform-illustrations.md)
  - **Illustrative examples** of abstract runtime loaders
  - Conceptual Python loader base class
  - Possible Node.js loader patterns
  - Thought experiments on polyglot lifecycle templates

**When to read**: Defining extensible algorithms, need consistent lifecycle, creating base classes for runtimes

---

## Tier 5: Quality & Testing Patterns

**Ensuring Reliability** - These patterns ensure testability and quality across the platform.

### 16. Testable Design (Interface-Based)

**Pattern**: Program to interfaces, enable dependency injection for testing

**VISORA Implementation**: ICapabilityProvider, all core abstractions as interfaces or abstract classes

**Meta-Platform Relevance**: Mocking runtime bridges without loading actual Python/Node.js

**Files**:
- [`patterns/testable-design/visora-analysis.md`](patterns/testable-design/visora-analysis.md)
  - Interface design principles
  - Dependency injection for testability
  - Mocking strategies
  - Integration test patterns

- [`patterns/testable-design/meta-platform-illustrations.md`](patterns/testable-design/meta-platform-illustrations.md)
  - **Illustrative examples** of testing cross-runtime code
  - Conceptual mock runtime implementations
  - Possible integration test strategies
  - Thought experiments on polyglot testing

**When to read**: Writing testable code, need mocking strategies, testing cross-runtime integration

---

## Architectural Decisions

**Why VISORA chose these patterns** - Understanding the rationale behind design choices.

### Decision Records

Each decision record explains:
- The problem context
- Alternatives considered
- Decision rationale
- Tradeoffs and consequences
- When to revisit

**Files**:
- [`decisions/reflection-over-manifests.md`](decisions/reflection-over-manifests.md)
  - Why reflection-first instead of XML/JSON manifests
  - Tradeoffs: flexibility vs performance, discovery vs explicit configuration

- [`decisions/capability-vs-service-locator.md`](decisions/capability-vs-service-locator.md)
  - Why custom capability provider instead of traditional DI
  - Comparison with ASP.NET Core DI, Autofac, MEF

- [`decisions/async-everywhere.md`](decisions/async-everywhere.md)
  - Why async/await throughout the stack
  - Future-proofing for remote execution and I/O

- [`decisions/unloadable-plugins.md`](decisions/unloadable-plugins.md)
  - Why unloadable assembly contexts
  - Hot-swap requirements and memory management

- [`decisions/result-objects-not-exceptions.md`](decisions/result-objects-not-exceptions.md)
  - Why CommandResult instead of exceptions for commands
  - User-facing vs infrastructure error handling

- [`decisions/sealed-records.md`](decisions/sealed-records.md)
  - Why sealed records for metadata
  - Immutability, serialization, value equality benefits

**When to read**: Understanding tradeoffs, making similar decisions in your platform, evaluating alternatives

---

## Conventions & Code Organization

**Consistency and maintainability** - How to apply patterns uniformly.

### Convention Guides

**Files**:
- [`conventions/naming-conventions.md`](conventions/naming-conventions.md)
  - Module IDs: `visora.<subsystem>.<feature>`
  - Component IDs: `<module-id>.<aspect>`
  - Command IDs: `<subsystem>.<verb>[.<object>]`
  - Class naming, method naming, async suffix patterns

- [`conventions/project-structure.md`](conventions/project-structure.md)
  - Solution layout and project organization
  - Namespace hierarchy matching folder structure
  - Layer separation and dependencies
  - Reference management

- [`conventions/code-style.md`](conventions/code-style.md)
  - C# patterns used in VISORA
  - Async/await style guide
  - Error handling conventions
  - Resource disposal patterns

- [`conventions/assembly-conventions.md`](conventions/assembly-conventions.md)
  - Assembly naming (*.vixm.dll convention)
  - Packaging and deployment
  - Version folder structure
  - Shim generation patterns

**When to read**: Starting new modules, ensuring consistency, setting up project structure

---

## Implementation Blueprints

**Step-by-step guides** - How to implement specific features using VISORA patterns.

### Blueprint Guides

**Files**:
- [`blueprints/creating-modules.md`](blueprints/creating-modules.md)
  - Complete guide from scratch
  - ModuleDescriptor creation
  - Component discovery implementation
  - Lifecycle hook implementation
  - Best practices and gotchas

- [`blueprints/creating-components.md`](blueprints/creating-components.md)
  - ComponentDescriptor design
  - Command creation and registration
  - State management in components
  - Activation/deactivation patterns

- [`blueprints/creating-commands.md`](blueprints/creating-commands.md)
  - CommandDescriptor metadata
  - ExecuteAsync implementation
  - Parameter handling
  - Result and error handling strategies

- [`blueprints/building-hosts.md`](blueprints/building-hosts.md)
  - Host responsibilities and setup
  - ModuleCatalog configuration
  - Capability provider construction
  - Discovery and initialization flow

- [`blueprints/testing-strategies.md`](blueprints/testing-strategies.md)
  - Unit testing with mocks
  - Integration testing patterns
  - Capability injection in tests
  - Test project organization

**When to read**: Implementing specific features, following best practices, learning by example

---

## Pattern Comparisons

**When to use which patterns** - Decision guidance and alternatives.

### Comparison Guides

**Files**:
- [`comparisons/visora-vs-traditional-plugins.md`](comparisons/visora-vs-traditional-plugins.md)
  - VISORA vs MEF (Managed Extensibility Framework)
  - VISORA vs MAF (Managed Add-in Framework)
  - VISORA vs VSPackages
  - When to choose each approach

- [`comparisons/visora-vs-di-containers.md`](comparisons/visora-vs-di-containers.md)
  - Capability providers vs ASP.NET Core DI
  - VISORA vs Autofac/Ninject
  - Service locator anti-pattern comparison
  - When capabilities make sense

- [`comparisons/pattern-selection-guide.md`](comparisons/pattern-selection-guide.md)
  - Decision trees for pattern selection
  - Scenario-based recommendations
  - Tradeoff matrices
  - Anti-patterns to avoid

**When to read**: Choosing between alternatives, evaluating tradeoffs, understanding when patterns apply

---

## How to Use This Index

### For AI Agents Building Meta-Platforms

1. **Start with Quick Start**: Read [ai-agent-entry-point.md](00-quick-start/ai-agent-entry-point.md)
2. **Identify Relevant Patterns**: Scan Tier 1 (Foundation) patterns for your scenario
3. **Deep Dive on Pattern**: Read both `visora-analysis.md` and `meta-platform-illustrations.md`
4. **Check Decisions**: Understand why VISORA chose this pattern
5. **Review Conventions**: Learn how to apply consistently
6. **Follow Blueprints**: Implement step-by-step
7. **Compare Alternatives**: Validate your choice

### For Specific Scenarios

**Scenario: Building polyglot runtime host**
1. [Plugin Architecture](patterns/plugin-architecture/visora-analysis.md) - Foundation
2. [Module Lifecycle](patterns/module-lifecycle/visora-analysis.md) - Startup/shutdown
3. [Capability Negotiation](patterns/capability-negotiation/visora-analysis.md) - Cross-runtime services
4. [Building Hosts Blueprint](blueprints/building-hosts.md) - Implementation guide

**Scenario: Enabling Python code to call .NET services**
1. [Capability Negotiation](patterns/capability-negotiation/visora-analysis.md) - Service access
2. [Context Objects](patterns/context-objects/visora-analysis.md) - Passing parameters
3. [Result Objects](patterns/result-objects/visora-analysis.md) - Error handling
4. [Capability Negotiation Illustrations](patterns/capability-negotiation/meta-platform-illustrations.md) - Python examples

**Scenario: Discovering modules across multiple languages**
1. [Reflection Discovery](patterns/reflection-discovery/visora-analysis.md) - Discovery mechanics
2. [Registry Pattern](patterns/registry-pattern/visora-analysis.md) - Unified catalog
3. [Immutable Metadata](patterns/immutable-metadata/visora-analysis.md) - Language-agnostic contracts
4. [Reflection Discovery Illustrations](patterns/reflection-discovery/meta-platform-illustrations.md) - Polyglot examples

**Scenario: Uniform command execution across UI/CLI/API**
1. [Command Execution](patterns/command-execution/visora-analysis.md) - Command pattern
2. [Multi-Surface Execution](patterns/multi-surface-execution/visora-analysis.md) - Surface abstraction
3. [Context Objects](patterns/context-objects/visora-analysis.md) - Execution context
4. [Creating Commands Blueprint](blueprints/creating-commands.md) - Implementation

---

## Navigation Tips

- **File Path Format**: `patterns/<pattern-name>/{visora-analysis|meta-platform-illustrations}.md`
- **Line References**: Files reference VISORA code as `src/Project/File.cs:123-145`
- **Cross-References**: Documents link to related patterns extensively
- **Tier Priority**: Tier 1 patterns are most critical for meta-platforms
- **Illustrations vs Prescriptions**: `meta-platform-illustrations.md` files contain examples to stimulate ideas, not definitive designs

---

## Document Updates

Each document includes:
- Last updated date
- .NET version
- VISORA version reference
- Change history (for major updates)

---

## Contributing Notes

When adding new patterns:
1. Create folder under `patterns/`
2. Add both `visora-analysis.md` and `meta-platform-illustrations.md`
3. Update this index with summary
4. Cross-reference from related patterns
5. Add to appropriate tier

---

## Legend

- 🎯 Entry point for new readers
- ⚠️ Important caveats or limitations
- 💡 Key insight or design principle
- 🔗 Cross-reference to related content
- 📝 Example or code snippet
- 🏗️ Under construction or planned

---

**Total Documentation**: 16 patterns × 2 files + 6 decisions + 4 conventions + 5 blueprints + 3 comparisons = **~50 files**

**Estimated Total Lines**: ~35,000-40,000 lines of comprehensive AI-agent-optimized documentation
