# VISORA Platform Patterns - AI Agent Entry Point

**Last Updated**: 2025-11-10
**VISORA Version**: .NET 9.0
**Target Audience**: AI agents learning VISORA patterns for meta-platform development

---

## 🎯 Welcome AI Agent!

You've discovered the VISORA platform pattern catalog. This documentation helps you understand the experimental patterns used in VISORA—a .NET 9.0 research project exploring **plugin architectures, capability negotiation, and modular platform design**.

### What is VISORA?

VISORA is an **IDE-like extensible platform** where every capability (shells, GUI panes, services, automations) is a loadable module. It experiments with:

- **Reflection-first discovery** (no manifest files)
- **Capability negotiation** (type-safe service access without traditional DI)
- **Unloadable plugins** (hot-swap modules without restart)
- **Multi-surface commands** (same code runs on CLI, UI, automation)
- **Async-first design** (ValueTask, cancellation throughout)

### Why This Documentation Exists

VISORA is an **R&D project** exploring patterns for a future **polyglot meta-platform** (a system that hosts multiple language runtimes—Python, Node.js, etc.—and bridges capabilities across them). These docs:

1. **Analyze VISORA's patterns** in depth
2. **Illustrate possible adaptations** for polyglot meta-platforms (⚠️ examples, not prescriptions)
3. **Explain architectural decisions** and tradeoffs
4. **Provide implementation guidance** for reproducing patterns on .NET

---

## 📖 How to Use This Documentation

### Step 1: Orient Yourself

You're here → **AI Agent Entry Point** (this file)

**Next**: Read the [Pattern Catalog Overview](pattern-catalog.md) for a high-level summary of all patterns.

**Then**: Return to [index.md](../index.md) to navigate to specific patterns based on your needs.

### Step 2: Understand the Structure

Every pattern has **two separate documents**:

1. **`visora-analysis.md`** — Deep dive into VISORA's actual implementation
   - What it is and why VISORA uses it
   - Code examples with file references (e.g., `src/Visora.Core/Modules/ModuleCatalog.cs:45`)
   - Implementation details and tradeoffs

2. **`meta-platform-illustrations.md`** — **Illustrative** adaptations for polyglot platforms
   - ⚠️ **These are examples to spark imagination, NOT prescriptive designs**
   - Shows possible ways to adapt for Python/Node.js integration
   - Conceptual code (not production-ready)
   - Thought experiments on cross-language patterns

### Step 3: Find What You Need

**I'm building a plugin system:**
- Start with [Plugin Architecture](../patterns/plugin-architecture/visora-analysis.md)
- Then [Module Lifecycle](../patterns/module-lifecycle/visora-analysis.md)

**I need cross-language service access:**
- Start with [Capability Negotiation](../patterns/capability-negotiation/visora-analysis.md)
- Then [Context Objects](../patterns/context-objects/visora-analysis.md)

**I'm discovering modules across runtimes:**
- Start with [Reflection Discovery](../patterns/reflection-discovery/visora-analysis.md)
- Then [Immutable Metadata](../patterns/immutable-metadata/visora-analysis.md)

**I want uniform invocation (CLI/UI/API):**
- Start with [Command Execution](../patterns/command-execution/visora-analysis.md)
- Then [Multi-Surface Execution](../patterns/multi-surface-execution/visora-analysis.md)

---

## 🏗️ VISORA Architecture at a Glance

### Three-Tier Architecture

```
┌─────────────────────────────────────────┐
│         Hosts (Applications)            │
│   Visora.CLI, Visora.Terminal, etc.    │
│  - Discover and load modules            │
│  - Provide capabilities to modules      │
└────────────────┬────────────────────────┘
                 │ depends on
┌────────────────▼────────────────────────┐
│         Core (Infrastructure)           │
│       Visora.Core, Visora.Shared        │
│  - ModuleCatalog, ModuleHandle          │
│  - CapabilityProviders                  │
│  - Discovery, lifecycle management      │
└────────────────┬────────────────────────┘
                 │ depends on
┌────────────────▼────────────────────────┐
│        Contracts (Abstractions)         │
│           Visora.Contracts              │
│  - VisoraModule, VisoraComponent        │
│  - VisoraCommand, ICapabilityProvider   │
│  - Descriptors (metadata records)       │
└─────────────────────────────────────────┘
```

**Key principle**: Contracts have NO dependencies. Core depends only on Contracts. Hosts depend on Core.

### Module-Component-Command Hierarchy

```
VisoraModule (*.vixm.dll)
  │
  ├─ ModuleDescriptor (metadata: id, name, version)
  │
  └─ Components (discovered via reflection)
       │
       ├─ VisoraComponent
       │    ├─ ComponentDescriptor (metadata: id, kind)
       │    └─ Commands
       │         │
       │         └─ VisoraCommand
       │              ├─ CommandDescriptor (metadata: id, title, UI hints)
       │              └─ ExecuteAsync(CommandContext) → CommandResult
       │
       └─ (more components...)
```

**Flow**: Modules are discovered → Loaded → Initialized → Components discovered → Commands created

### Capability Negotiation

Instead of traditional DI containers, VISORA uses **capability providers**:

```csharp
// Host provides capabilities
var capabilities = CapabilityProviders.CreateBuilder()
    .Add<ILogger>(logger)
    .Add<IFileSystem>(fileSystem)
    .Build();

// Module/component/command negotiates capabilities
var logger = context.Capabilities.GetOptional<ILogger>();
if (logger != null) {
    await logger.LogAsync("Hello from module");
}
```

**Why?** Type-safe, explicit dependencies. No global service locators. Hosts control what's exposed.

---

## 🔑 Key Concepts Glossary

**Module**: A loadable `.vixm.dll` assembly containing components and commands. Implements `VisoraModule` base class.

**Component**: A logical grouping of related commands within a module. Implements `VisoraComponent` base class.

**Command**: An executable unit of work. Implements `VisoraCommand` base class with `ExecuteAsync` method.

**Descriptor**: Immutable metadata record (e.g., `ModuleDescriptor`, `CommandDescriptor`). Describes entities without behavior.

**Context**: Rich parameter object passed through lifecycle methods (`ModuleContext`, `ComponentContext`, `CommandContext`).

**Capability Provider**: Service negotiation interface (`ICapabilityProvider`) for type-safe service access.

**Module Catalog**: Registry (`ModuleCatalog`) that discovers, loads, and manages module lifecycles.

**Module Handle**: Wrapper (`ModuleHandle`) around a loaded module assembly, managing its lifecycle.

**Command Surface**: Enum indicating where a command is invoked from (`CLI`, `UI`, `Automation`, `Programmatic`, `Remote`).

**Command Result**: Outcome of command execution (`Success`, `Cancelled`, `Failed`) with optional message/payload.

---

## 📊 Pattern Tiers Explained

Patterns are organized into **5 tiers by meta-platform relevance**:

### Tier 1: Foundation Patterns (CRITICAL)
These form the core of polyglot runtime hosting:
- Plugin Architecture (unloadable assemblies)
- Capability Negotiation (cross-runtime service access)
- Reflection Discovery (finding code without manifests)
- Module Lifecycle (startup/shutdown coordination)
- Context Objects (rich parameter passing)

### Tier 2: Communication & Execution (IMPORTANT)
Enable uniform communication across boundaries:
- Command Execution (uniform invocation interface)
- Async Patterns (bridging async models)
- Result Objects (error propagation across boundaries)
- Multi-Surface Execution (CLI/UI/API uniformity)

### Tier 3: Data & Metadata (IMPORTANT)
Language-agnostic contracts:
- Immutable Metadata (sealed records for contracts)
- Factory Patterns (runtime-specific instantiation)
- Builder Pattern (fluent configuration APIs)

### Tier 4: Structural (ARCHITECTURAL)
Code organization and complexity management:
- Layered Architecture (separation of concerns)
- Registry Pattern (central module tracking)
- Template Method (extensible algorithms)

### Tier 5: Quality & Testing (RELIABILITY)
Ensuring testability and reliability:
- Testable Design (interface-based, mockable)

---

## ⚠️ Important Caveats

### About Meta-Platform Illustrations

Files named `meta-platform-illustrations.md` contain:
- **Conceptual examples** of how VISORA patterns might adapt to polyglot scenarios
- **Thought experiments**, not production designs
- **Python and Node.js** integration ideas
- **Stimulate your imagination**—not prescribe your implementation

**These are NOT**:
- Definitive designs for the meta-platform
- Production-ready code
- The only way to adapt these patterns
- Comprehensive solutions

**Use them to**:
- Understand pattern applicability
- Explore possibilities
- Inspire your own designs
- Learn from thought experiments

### About VISORA Itself

VISORA is an **incomplete R&D project**:
- Some patterns are experiments that won't be reused exactly
- It's a .NET-only platform exploring ideas for a future polyglot platform
- Not all features are implemented (e.g., domain events, remote execution)
- It's a learning artifact, not a finished product

---

## 🚀 Quick Start Paths

### Path 1: I'm Building a Plugin System

1. Read: [Plugin Architecture Analysis](../patterns/plugin-architecture/visora-analysis.md)
2. Read: [Module Lifecycle Analysis](../patterns/module-lifecycle/visora-analysis.md)
3. Explore: [Building Hosts Blueprint](../blueprints/building-hosts.md)
4. Review: [Unloadable Plugins Decision](../decisions/unloadable-plugins.md)

**Time**: ~2 hours reading, understand core mechanics

### Path 2: I'm Enabling Cross-Language Integration

1. Read: [Capability Negotiation Analysis](../patterns/capability-negotiation/visora-analysis.md)
2. Read: [Context Objects Analysis](../patterns/context-objects/visora-analysis.md)
3. Explore: [Capability Negotiation Illustrations](../patterns/capability-negotiation/meta-platform-illustrations.md)
4. Review: [Capability vs Service Locator Decision](../decisions/capability-vs-service-locator.md)

**Time**: ~2 hours reading, understand service bridging

### Path 3: I'm Building a Multi-Language Module System

1. Read: [Reflection Discovery Analysis](../patterns/reflection-discovery/visora-analysis.md)
2. Read: [Registry Pattern Analysis](../patterns/registry-pattern/visora-analysis.md)
3. Read: [Immutable Metadata Analysis](../patterns/immutable-metadata/visora-analysis.md)
4. Explore: [Reflection Discovery Illustrations](../patterns/reflection-discovery/meta-platform-illustrations.md)

**Time**: ~2.5 hours reading, understand discovery across languages

### Path 4: I'm Creating a Unified Invocation Layer

1. Read: [Command Execution Analysis](../patterns/command-execution/visora-analysis.md)
2. Read: [Multi-Surface Execution Analysis](../patterns/multi-surface-execution/visora-analysis.md)
3. Read: [Result Objects Analysis](../patterns/result-objects/visora-analysis.md)
4. Explore: [Creating Commands Blueprint](../blueprints/creating-commands.md)

**Time**: ~2 hours reading, understand uniform execution

---

## 🎓 Learning Strategy

### For Quick Context (30 minutes)
1. Read this entry point (you're here)
2. Skim [Pattern Catalog](pattern-catalog.md)
3. Pick one Tier 1 pattern relevant to your task
4. Read its `visora-analysis.md`

### For Deep Understanding (4-6 hours)
1. Read this entry point
2. Read full [Pattern Catalog](pattern-catalog.md)
3. Read all Tier 1 pattern analyses (5 patterns)
4. Read relevant Tier 2 patterns for your scenario
5. Review architectural decisions
6. Explore meta-platform illustrations

### For Implementation (8-12 hours)
1. Complete "Deep Understanding" path
2. Read relevant blueprints (creating modules, components, commands)
3. Read conventions (naming, structure, style)
4. Study comparisons (when to use which patterns)
5. Implement a small example module
6. Review testable design patterns

---

## 📚 Reference Quick Links

**Core VISORA Code Locations:**
- Module abstraction: `src/Visora.Contracts/Modules/VisoraModule.cs`
- Component abstraction: `src/Visora.Contracts/Components/VisoraComponent.cs`
- Command abstraction: `src/Visora.Contracts/Commands/VisoraCommand.cs`
- Module catalog: `src/Visora.Core/Modules/ModuleCatalog.cs`
- Module handle: `src/Visora.Core/Modules/ModuleHandle.cs`
- Capability provider: `src/Visora.Core/Capabilities/CapabilityProviders.cs`
- CLI host: `src/Visora.CLI/Program.cs`
- Example module: `src/Visora.Shell.Commands.Core/ShellCommandsModule.cs`

**Documentation Structure:**
- Master index: [`../index.md`](../index.md)
- Pattern catalog: [`pattern-catalog.md`](pattern-catalog.md)
- Patterns: [`../patterns/<pattern-name>/`](../patterns/)
- Decisions: [`../decisions/`](../decisions/)
- Conventions: [`../conventions/`](../conventions/)
- Blueprints: [`../blueprints/`](../blueprints/)
- Comparisons: [`../comparisons/`](../comparisons/)

---

## 💡 Key Insights for Meta-Platform Development

### Insight 1: Isolation is Key
VISORA's unloadable plugin pattern → Your meta-platform needs runtime isolation (Python/Node.js in separate domains)

### Insight 2: Type Safety Across Boundaries
VISORA's capability provider → Your meta-platform needs type-safe proxies for cross-language calls

### Insight 3: Convention Over Configuration
VISORA's reflection discovery → Your meta-platform can discover Python modules via decorators, Node.js via exports

### Insight 4: Uniform Lifecycles
VISORA's 4-phase lifecycle → Your meta-platform needs consistent startup/shutdown across runtimes

### Insight 5: Language-Agnostic Metadata
VISORA's sealed records → Your meta-platform needs JSON/protobuf contracts for cross-language metadata

### Insight 6: Result Objects Across Boundaries
VISORA's CommandResult → Your meta-platform avoids exception marshaling nightmares

### Insight 7: Multi-Surface Abstraction
VISORA's CommandSurface → Your meta-platform can invoke Python from CLI/UI/API uniformly

---

## 🔄 Your Workflow

1. **Identify your scenario** (plugin system, cross-language integration, discovery, invocation)
2. **Find relevant patterns** in [index.md](../index.md) or [pattern catalog](pattern-catalog.md)
3. **Read VISORA analysis** to understand the pattern deeply
4. **Explore illustrations** to see polyglot adaptation ideas
5. **Check decisions** to understand tradeoffs
6. **Follow blueprints** for implementation guidance
7. **Apply to your meta-platform** with your own adaptations

---

## 📞 Navigation Help

**Lost?** Return to [index.md](../index.md) for full navigation

**Want overview?** Read [pattern-catalog.md](pattern-catalog.md)

**Ready to dive in?** Pick a Tier 1 pattern from [index.md](../index.md#tier-1-foundation-patterns)

**Need implementation help?** Check [blueprints](../blueprints/)

**Comparing alternatives?** See [comparisons](../comparisons/)

---

## 🎯 Next Steps

1. ✅ You've read the entry point (this file)
2. ⏭️ Read [Pattern Catalog Overview](pattern-catalog.md)
3. ⏭️ Return to [Master Index](../index.md)
4. ⏭️ Choose your learning path based on scenario
5. ⏭️ Start with Tier 1 patterns most relevant to you

---

**Happy learning! This documentation is designed to help you cherry-pick the best ideas from VISORA for your own meta-platform designs.**

---

**Document Status**: Complete
**Related**: [pattern-catalog.md](pattern-catalog.md), [index.md](../index.md)
