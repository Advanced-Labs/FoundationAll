# Multi-Surface Execution Pattern - VISORA Analysis

**Last Updated**: 2025-11-10
**VISORA Version**: .NET 9.0
**Pattern Tier**: Tier 2 (Communication & Execution)

---

## Pattern Overview

The **Multi-Surface Execution Pattern** in VISORA enables commands to execute across different invocation surfaces (CLI, UI, Automation, Remote) without code changes. The same command logic runs everywhere, with surface-specific adaptations only when necessary.

### What is This Pattern?

Multi-surface execution means:
- Commands execute identically across CLI, UI, automation, and remote contexts
- Surface information passed via `CommandContext.Surface` enum
- Commands can optionally adapt behavior based on surface
- UI hints enable rich menu integration without coupling to UI code
- Testing occurs on all surfaces through the same test infrastructure

### Why VISORA Uses This Pattern

**Problem**: Traditional applications duplicate logic across UI, CLI, and API layers. Changes require updating multiple code paths.

**Solution**: Single command implementation with surface awareness through context.

**Benefits**:
- Write once, run anywhere (CLI, UI, automation, remote)
- Reduced code duplication
- Consistent behavior across surfaces
- Easy to add new surfaces without changing commands
- Testable on any surface

---

## VISORA Implementation

### CommandSurface Enum

**Definition** (`src/Visora.Contracts/Commands/CommandContext.cs:49-57`)

```csharp
/// <summary>
/// Identifies the surface through which a command is invoked.
/// </summary>
public enum CommandSurface
{
    /// <summary>Direct programmatic invocation (tests, automation scripts)</summary>
    Programmatic,

    /// <summary>Text-based shell or command-line interface</summary>
    TextShell,

    /// <summary>Graphical user interface (WPF, web UI)</summary>
    Ui,

    /// <summary>Automation or background task execution</summary>
    Automation,

    /// <summary>Remote invocation (RPC, HTTP API, inter-process)</summary>
    Remote
}
```

**Key Characteristics**:
- Enum (not interface) for simplicity and serialization
- Covers primary invocation contexts
- Extensible (can add new surfaces without breaking existing code)
- Part of `CommandContext` passed to every command execution

### CommandContext with Surface

**Definition** (`src/Visora.Contracts/Commands/CommandContext.cs:8-47`)

```csharp
/// <summary>
/// Execution context for a command, including surface information.
/// </summary>
public sealed class CommandContext
{
    public VisoraModule Module { get; }
    public VisoraComponent? Component { get; }

    /// <summary>
    /// Surface through which command was invoked.
    /// </summary>
    public CommandSurface Surface { get; }

    public ICapabilityProvider Capabilities { get; }
    public IReadOnlyDictionary<string, object?> Parameters { get; }
    public CancellationToken CancellationToken { get; }

    public CommandContext(
        VisoraModule module,
        VisoraComponent? component,
        CommandSurface surface,
        ICapabilityProvider capabilities,
        IReadOnlyDictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        Module = module ?? throw new ArgumentNullException(nameof(module));
        Component = component;
        Surface = surface;
        Capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
        Parameters = parameters ?? EmptyParameters;
        CancellationToken = cancellationToken;
    }

    private static readonly IReadOnlyDictionary<string, object?> EmptyParameters =
        new Dictionary<string, object?>();
}
```

---

## Surface-Agnostic Command Design

### Pattern: Default Surface-Agnostic Implementation

**Most commands should be surface-agnostic:**

```csharp
public sealed class PingCommand : VisoraCommand
{
    private static readonly CommandDescriptor Info = CommandDescriptor.Create(
        id: "shell.ping",
        title: "Ping",
        description: "Checks connectivity with the Visora host.");

    public override CommandDescriptor Descriptor => Info;

    public override ValueTask<CommandResult> ExecuteAsync(
        CommandContext context,
        CancellationToken cancellationToken = default)
    {
        // Surface-agnostic: works the same on CLI, UI, automation
        var now = DateTimeOffset.UtcNow;
        var message = $"Pong from '{context.Module.Descriptor.Name}' @ {now:O}";

        var payload = new
        {
            Timestamp = now,
            Surface = context.Surface,  // Include for diagnostics
            ModuleId = context.Module.Descriptor.Id
        };

        return ValueTask.FromResult(CommandResult.Success(message, payload));
    }
}
```

**Why This Works**:
- Logic is pure and doesn't depend on surface
- Returns structured data (`CommandResult`) that any surface can handle
- Surface information logged for diagnostics
- No special-casing per surface

### Pattern: Surface-Aware Behavior (When Needed)

**Some commands may need to adapt to surface:**

```csharp
public sealed class ConfirmDeleteCommand : VisoraCommand
{
    public override async ValueTask<CommandResult> ExecuteAsync(
        CommandContext context,
        CancellationToken cancellationToken = default)
    {
        // Get confirmation based on surface
        bool confirmed = context.Surface switch
        {
            CommandSurface.Ui => await GetUiConfirmationAsync(context, cancellationToken),
            CommandSurface.TextShell => GetCliConfirmation(context),
            CommandSurface.Programmatic => GetProgrammaticConfirmation(context),
            CommandSurface.Automation => true, // Auto-confirm in automation
            CommandSurface.Remote => GetRemoteConfirmation(context),
            _ => false
        };

        if (!confirmed)
        {
            return CommandResult.Cancelled("User declined confirmation");
        }

        // Proceed with delete...
        await PerformDeleteAsync(context, cancellationToken);
        return CommandResult.Success("Deleted successfully");
    }

    private async Task<bool> GetUiConfirmationAsync(
        CommandContext context,
        CancellationToken ct)
    {
        // Get UI dialog capability
        var uiDialogs = context.Capabilities.GetOptional<IUiDialogs>();
        if (uiDialogs is null)
            return false;

        return await uiDialogs.ShowConfirmationAsync(
            "Are you sure you want to delete this item?",
            ct);
    }

    private bool GetCliConfirmation(CommandContext context)
    {
        // Get console capability
        var console = context.Capabilities.GetOptional<IConsoleHost>();
        if (console is null)
            return false;

        console.Write("Delete? (y/n): ");
        var response = console.ReadLine();
        return response?.ToLowerInvariant() == "y";
    }

    private bool GetProgrammaticConfirmation(CommandContext context)
    {
        // Check for confirmation parameter
        if (context.Parameters.TryGetValue("confirm", out var confirmObj) &&
            confirmObj is bool confirm)
        {
            return confirm;
        }

        return false; // Require explicit confirmation
    }

    private bool GetRemoteConfirmation(CommandContext context)
    {
        // Remote calls must provide confirmation parameter
        return GetProgrammaticConfirmation(context);
    }
}
```

**When to Adapt by Surface**:
- User interaction (confirmation dialogs, input prompts)
- Progress reporting (console vs UI progress bar)
- Output formatting (rich UI vs plain text)
- Security constraints (different permissions per surface)

---

## Surface-Specific Adaptations

### Example 1: Progress Reporting

```csharp
public sealed class ImportCommand : VisoraCommand
{
    public override async ValueTask<CommandResult> ExecuteAsync(
        CommandContext context,
        CancellationToken cancellationToken = default)
    {
        var items = GetItemsToImport(context);
        var progress = CreateProgressReporter(context);

        var imported = 0;
        var total = items.Count;

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await ImportItemAsync(item, cancellationToken);
            imported++;

            // Report progress (surface-specific)
            progress.Report(imported, total, $"Imported {item.Name}");
        }

        return CommandResult.Success(
            $"Imported {imported} of {total} items",
            new { ImportedCount = imported, TotalCount = total });
    }

    private IProgressReporter CreateProgressReporter(CommandContext context)
    {
        return context.Surface switch
        {
            CommandSurface.Ui => new UiProgressReporter(context.Capabilities),
            CommandSurface.TextShell => new ConsoleProgressReporter(context.Capabilities),
            _ => new NullProgressReporter() // Silent for automation/programmatic
        };
    }
}

public interface IProgressReporter
{
    void Report(int current, int total, string message);
}

public class ConsoleProgressReporter : IProgressReporter
{
    private readonly IConsoleHost? _console;

    public ConsoleProgressReporter(ICapabilityProvider capabilities)
    {
        _console = capabilities.GetOptional<IConsoleHost>();
    }

    public void Report(int current, int total, string message)
    {
        _console?.WriteLine($"[{current}/{total}] {message}");
    }
}

public class UiProgressReporter : IProgressReporter
{
    private readonly IUiProgress? _uiProgress;

    public UiProgressReporter(ICapabilityProvider capabilities)
    {
        _uiProgress = capabilities.GetOptional<IUiProgress>();
    }

    public void Report(int current, int total, string message)
    {
        var percentage = (double)current / total * 100.0;
        _uiProgress?.Update(percentage, message);
    }
}

public class NullProgressReporter : IProgressReporter
{
    public void Report(int current, int total, string message) { }
}
```

### Example 2: Output Formatting

```csharp
public sealed class ListModulesCommand : VisoraCommand
{
    public override async ValueTask<CommandResult> ExecuteAsync(
        CommandContext context,
        CancellationToken cancellationToken = default)
    {
        var catalog = context.Capabilities.GetRequired<ModuleCatalog>();
        var modules = catalog.Modules;

        // Surface-specific output
        switch (context.Surface)
        {
            case CommandSurface.Ui:
                // Return structured data for UI grid/list
                return CommandResult.Success(
                    $"Found {modules.Count} modules",
                    new
                    {
                        Modules = modules.Select(m => new
                        {
                            m.Descriptor.Id,
                            m.Descriptor.Name,
                            Version = m.Descriptor.Version.ToString(),
                            m.Descriptor.Description
                        }).ToArray()
                    });

            case CommandSurface.TextShell:
                // Format as table for console
                var console = context.Capabilities.GetOptional<IConsoleHost>();
                if (console != null)
                {
                    FormatAsTable(console, modules);
                }

                return CommandResult.Success($"Listed {modules.Count} modules");

            default:
                // Return raw data for programmatic/automation
                return CommandResult.Success(
                    $"Found {modules.Count} modules",
                    new { Modules = modules.Select(m => m.Descriptor).ToArray() });
        }
    }

    private void FormatAsTable(IConsoleHost console, IReadOnlyList<ModuleHandle> modules)
    {
        console.WriteLine("ID                           | Name                | Version");
        console.WriteLine("-----------------------------+---------------------+---------");

        foreach (var module in modules)
        {
            console.WriteLine(
                $"{module.Descriptor.Id,-28} | {module.Descriptor.Name,-19} | {module.Descriptor.Version}");
        }
    }
}
```

---

## CommandUiHint for Menu Integration

### UI Hints in Command Descriptor

**Definition** (`src/Visora.Contracts/Commands/CommandDescriptor.cs:32-36`)

```csharp
/// <summary>
/// UI integration hints for menu placement, icons, and gestures.
/// </summary>
public sealed record CommandUiHint(
    string? MenuPath = null,          // e.g., "File/New Project"
    string? Icon = null,              // Icon identifier
    string? DefaultGesture = null);   // e.g., "Ctrl+N"
```

**Used in CommandDescriptor:**

```csharp
public sealed record CommandDescriptor(
    string Id,
    string Title,
    string? Description = null,
    CommandKind Kind = CommandKind.General,
    IReadOnlyCollection<string>? Aliases = null,
    IReadOnlyCollection<string>? Keywords = null,
    bool IsVisible = true,
    bool IsInstanceScoped = false,
    CommandUiHint? Ui = null);  // ← UI hints here
```

### Example: Command with UI Hints

```csharp
public sealed class NewProjectCommand : VisoraCommand
{
    private static readonly CommandDescriptor Info = CommandDescriptor.Create(
        id: "file.new.project",
        title: "New Project",
        description: "Create a new project from template.",
        kind: CommandKind.General,
        ui: new CommandUiHint(
            MenuPath: "File/New/Project",
            Icon: "DocumentAdd",
            DefaultGesture: "Ctrl+Shift+N"));

    public override CommandDescriptor Descriptor => Info;

    public override async ValueTask<CommandResult> ExecuteAsync(
        CommandContext context,
        CancellationToken cancellationToken = default)
    {
        // Surface-agnostic logic
        var templateId = GetRequiredParameter<string>(context, "templateId");
        var projectName = GetRequiredParameter<string>(context, "projectName");

        var project = await CreateProjectAsync(templateId, projectName, cancellationToken);

        return CommandResult.Success(
            $"Created project: {projectName}",
            new { ProjectPath = project.Path });
    }

    private T GetRequiredParameter<T>(CommandContext context, string name)
    {
        if (context.Parameters.TryGetValue(name, out var value) && value is T typed)
            return typed;

        throw new InvalidOperationException(
            $"Required parameter '{name}' of type {typeof(T)} not found");
    }
}
```

### UI Host Using Hints

**Illustrative UI integration:**

```csharp
public class MenuBuilder
{
    public void BuildMenus(IEnumerable<CommandDescriptor> commands)
    {
        foreach (var descriptor in commands.Where(d => d.Ui != null))
        {
            var menuPath = descriptor.Ui!.MenuPath;
            var icon = descriptor.Ui.Icon;
            var gesture = descriptor.Ui.DefaultGesture;

            // Create menu item
            var menuItem = new MenuItem
            {
                Header = descriptor.Title,
                Command = new CommandAdapter(descriptor.Id),
                Icon = ResolveIcon(icon),
                InputGestureText = gesture
            };

            // Place in menu hierarchy
            PlaceInMenu(menuPath, menuItem);
        }
    }

    private void PlaceInMenu(string? path, MenuItem item)
    {
        if (string.IsNullOrEmpty(path))
        {
            // Top-level menu
            _mainMenu.Items.Add(item);
            return;
        }

        // Navigate hierarchy: "File/New/Project" → File → New → Project
        var parts = path.Split('/');
        var currentMenu = _mainMenu;

        for (int i = 0; i < parts.Length - 1; i++)
        {
            var submenu = FindOrCreateSubmenu(currentMenu, parts[i]);
            currentMenu = submenu;
        }

        currentMenu.Items.Add(item);
    }
}
```

---

## Testing Across Surfaces

### Testing on Programmatic Surface

```csharp
[TestClass]
public class MultiSurfaceTests
{
    [TestMethod]
    public async Task Command_ExecutesOnProgrammaticSurface()
    {
        // Arrange
        var command = new PingCommand();
        var context = new CommandContext(
            module: CreateMockModule(),
            component: null,
            surface: CommandSurface.Programmatic,  // ← Programmatic surface
            capabilities: CapabilityProviders.Empty,
            parameters: new Dictionary<string, object?>());

        // Act
        var result = await command.ExecuteAsync(context, CancellationToken.None);

        // Assert
        Assert.AreEqual(CommandOutcome.Success, result.Outcome);
        Assert.IsNotNull(result.Payload);
    }
}
```

### Testing on TextShell Surface

```csharp
[TestMethod]
public async Task ListModules_FormatsForTextShell()
{
    // Arrange
    var mockConsole = new MockConsoleHost();
    var capabilities = CapabilityProviders.CreateBuilder()
        .Add<IConsoleHost>(mockConsole)
        .Add<ModuleCatalog>(CreateTestCatalog())
        .Build();

    var command = new ListModulesCommand();
    var context = new CommandContext(
        module: CreateMockModule(),
        component: null,
        surface: CommandSurface.TextShell,  // ← TextShell surface
        capabilities: capabilities);

    // Act
    var result = await command.ExecuteAsync(context, CancellationToken.None);

    // Assert
    Assert.AreEqual(CommandOutcome.Success, result.Outcome);

    // Verify console output
    var output = mockConsole.GetOutput();
    Assert.IsTrue(output.Contains("ID"));
    Assert.IsTrue(output.Contains("Name"));
    Assert.IsTrue(output.Contains("Version"));
}
```

### Testing on UI Surface

```csharp
[TestMethod]
public async Task ConfirmDelete_ShowsUiDialog()
{
    // Arrange
    var mockDialogs = new MockUiDialogs();
    mockDialogs.ConfirmationResult = true;  // Simulate user clicking "Yes"

    var capabilities = CapabilityProviders.CreateBuilder()
        .Add<IUiDialogs>(mockDialogs)
        .Build();

    var command = new ConfirmDeleteCommand();
    var context = new CommandContext(
        module: CreateMockModule(),
        component: null,
        surface: CommandSurface.Ui,  // ← UI surface
        capabilities: capabilities,
        parameters: new Dictionary<string, object?> { ["itemId"] = "123" });

    // Act
    var result = await command.ExecuteAsync(context, CancellationToken.None);

    // Assert
    Assert.AreEqual(CommandOutcome.Success, result.Outcome);
    Assert.IsTrue(mockDialogs.WasConfirmationShown);
}
```

### Testing Surface Adaptation

```csharp
[TestClass]
public class SurfaceAdaptationTests
{
    [DataTestMethod]
    [DataRow(CommandSurface.Programmatic)]
    [DataRow(CommandSurface.TextShell)]
    [DataRow(CommandSurface.Ui)]
    [DataRow(CommandSurface.Automation)]
    [DataRow(CommandSurface.Remote)]
    public async Task Command_WorksOnAllSurfaces(CommandSurface surface)
    {
        // Arrange
        var command = new PingCommand();
        var context = CreateContextForSurface(surface);

        // Act
        var result = await command.ExecuteAsync(context, CancellationToken.None);

        // Assert
        Assert.AreEqual(CommandOutcome.Success, result.Outcome);
        Assert.IsNotNull(result.Message);
    }

    private CommandContext CreateContextForSurface(CommandSurface surface)
    {
        // Create appropriate capabilities per surface
        var capabilitiesBuilder = CapabilityProviders.CreateBuilder();

        if (surface == CommandSurface.TextShell)
        {
            capabilitiesBuilder.Add<IConsoleHost>(new MockConsoleHost());
        }
        else if (surface == CommandSurface.Ui)
        {
            capabilitiesBuilder.Add<IUiDialogs>(new MockUiDialogs());
            capabilitiesBuilder.Add<IUiProgress>(new MockUiProgress());
        }

        return new CommandContext(
            module: CreateMockModule(),
            component: null,
            surface: surface,
            capabilities: capabilitiesBuilder.Build());
    }
}
```

---

## Best Practices

### 1. Prefer Surface-Agnostic Design

```csharp
// ✅ Good: Surface-agnostic
public override ValueTask<CommandResult> ExecuteAsync(
    CommandContext context,
    CancellationToken ct)
{
    var data = ProcessData();
    return ValueTask.FromResult(CommandResult.Success("Done", data));
}

// ❌ Avoid: Unnecessary surface-specific code
public override ValueTask<CommandResult> ExecuteAsync(
    CommandContext context,
    CancellationToken ct)
{
    if (context.Surface == CommandSurface.Ui)
    {
        // Different logic for UI
    }
    else
    {
        // Different logic for CLI
    }
}
```

### 2. Use Capabilities for Surface-Specific Features

```csharp
// ✅ Good: Use capabilities
var console = context.Capabilities.GetOptional<IConsoleHost>();
if (console != null)
{
    console.WriteLine("Progress: 50%");
}

// ❌ Bad: Check surface directly
if (context.Surface == CommandSurface.TextShell)
{
    Console.WriteLine("Progress: 50%");  // Couples to Console.WriteLine
}
```

### 3. Return Structured Data

```csharp
// ✅ Good: Structured payload
return CommandResult.Success(
    "Found 42 items",
    new { Count = 42, Items = items });

// ❌ Avoid: Surface-specific formatting in payload
return CommandResult.Success(
    "42 items\nID | Name\n----+-----\n...");  // Pre-formatted string
```

### 4. Use UI Hints for Discoverability

```csharp
// ✅ Good: Provide UI hints
private static readonly CommandDescriptor Info = CommandDescriptor.Create(
    id: "edit.find",
    title: "Find",
    ui: new CommandUiHint(
        MenuPath: "Edit/Find",
        Icon: "Search",
        DefaultGesture: "Ctrl+F"));

// ❌ Missing: No UI hints (harder to integrate in menus)
private static readonly CommandDescriptor Info = CommandDescriptor.Create(
    id: "edit.find",
    title: "Find");
```

### 5. Test on Multiple Surfaces

```csharp
// ✅ Good: Test on all relevant surfaces
[DataTestMethod]
[DataRow(CommandSurface.Programmatic)]
[DataRow(CommandSurface.TextShell)]
[DataRow(CommandSurface.Ui)]
public async Task CommandWorks(CommandSurface surface) { ... }

// ❌ Incomplete: Only test on one surface
[TestMethod]
public async Task CommandWorks()
{
    var context = new CommandContext(..., CommandSurface.Programmatic, ...);
    // Only tested on Programmatic!
}
```

---

## Related Patterns

- **[Command Execution](../command-execution/visora-analysis.md)**: Core command execution pattern
- **[Context Objects](../context-objects/visora-analysis.md)**: CommandContext design
- **[Capability Negotiation](../capability-negotiation/visora-analysis.md)**: Surface-specific capabilities
- **[Multi-Surface Execution - Meta-Platform](./meta-platform-illustrations.md)**: Cross-language multi-surface patterns

---

## Summary

VISORA's Multi-Surface Execution pattern provides:
- ✅ Unified command execution across CLI, UI, automation, remote
- ✅ Surface awareness through `CommandContext.Surface`
- ✅ Optional surface-specific adaptations via capabilities
- ✅ UI integration hints for menu/icon/gesture registration
- ✅ Testable on all surfaces through same infrastructure

**Key Takeaway**: Write commands once with surface-agnostic logic. Adapt only when necessary using capabilities and context. This enables commands to run anywhere—CLI, UI, automation, remote—without code duplication.
