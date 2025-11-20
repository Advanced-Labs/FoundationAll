# Multi-Surface Execution - Meta-Platform Illustrations

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
2. [Universal Python Command Invocation](#universal-python-command-invocation)
3. [Universal Node.js Command Invocation](#universal-nodejs-command-invocation)
4. [Surface Abstraction in Python](#surface-abstraction-in-python)
5. [Surface Abstraction in Node.js](#surface-abstraction-in-nodejs)
6. [Cross-Language Multi-Surface Examples](#cross-language-multi-surface-examples)
7. [I/O Model Challenges](#io-model-challenges)
8. [Surface Detection Patterns](#surface-detection-patterns)
9. [Challenges & Considerations](#challenges--considerations)

---

## Conceptual Adaptation

### From .NET CommandSurface to Polyglot Surface Abstraction

**VISORA's .NET Pattern:**
```csharp
public enum CommandSurface
{
    Programmatic,  // Tests, automation
    TextShell,     // CLI
    Ui,            // WPF, web UI
    Automation,    // Background tasks
    Remote         // RPC, HTTP
}

// Commands receive surface in context
public override ValueTask<CommandResult> ExecuteAsync(
    CommandContext context,  // context.Surface tells us where we're running
    CancellationToken ct)
```

**Conceptual Meta-Platform Adaptation:**
```
Universal Surface Contract
  │
  ├─ .NET: CommandSurface enum
  │  └─ Passed via CommandContext
  │
  ├─ Python: SurfaceType enum
  │  └─ Passed via execution_context dict
  │
  ├─ Node.js: SurfaceType enum/string
  │  └─ Passed via context object
  │
  └─ Common Values (cross-language)
     ├─ "programmatic" / "PROGRAMMATIC"
     ├─ "text_shell" / "TEXT_SHELL"
     ├─ "ui" / "UI"
     ├─ "automation" / "AUTOMATION"
     └─ "remote" / "REMOTE"
```

---

## Universal Python Command Invocation

### ⚠️ ILLUSTRATIVE EXAMPLE: Python Command with Surface Awareness

**Conceptual Approach:** Python commands receive surface information in execution context.

**Illustrative Python Code:**

```python
# ⚠️ ILLUSTRATIVE EXAMPLE - visora_surface.py

from enum import Enum
from typing import Optional, Dict, Any
from dataclasses import dataclass

class SurfaceType(str, Enum):
    """
    Execution surface types.
    Mirrors .NET CommandSurface.
    """
    PROGRAMMATIC = "programmatic"
    TEXT_SHELL = "text_shell"
    UI = "ui"
    AUTOMATION = "automation"
    REMOTE = "remote"


@dataclass
class ExecutionContext:
    """
    Execution context for Python commands.
    Mirrors .NET CommandContext.
    """
    surface: SurfaceType
    module_id: str
    capabilities: Dict[str, Any]
    parameters: Dict[str, Any]

    def get_capability(self, name: str) -> Optional[Any]:
        """Get capability by name (returns None if not available)."""
        return self.capabilities.get(name)


class VisoraCommand:
    """
    Base class for Python commands (surface-agnostic).
    """

    def get_descriptor(self) -> dict:
        """Return command metadata."""
        raise NotImplementedError()

    async def execute(self, context: ExecutionContext) -> dict:
        """
        Execute command and return result.
        Context includes surface information.
        """
        raise NotImplementedError()


# ⚠️ ILLUSTRATIVE EXAMPLE: Surface-agnostic command

class PingCommand(VisoraCommand):
    """
    Simple ping command that works on all surfaces.
    """

    def get_descriptor(self) -> dict:
        return {
            "id": "python.ping",
            "title": "Ping",
            "description": "Checks connectivity with Python module",
            "kind": "automation"
        }

    async def execute(self, context: ExecutionContext) -> dict:
        """
        Surface-agnostic implementation.
        Same logic for CLI, UI, automation, remote.
        """
        import datetime

        timestamp = datetime.datetime.utcnow().isoformat()

        # Include surface in payload for diagnostics
        return {
            "outcome": "success",
            "message": f"Pong from Python module @ {timestamp}",
            "payload": {
                "timestamp": timestamp,
                "surface": context.surface.value,
                "module_id": context.module_id
            }
        }


# ⚠️ ILLUSTRATIVE EXAMPLE: Surface-aware command

class ConfirmActionCommand(VisoraCommand):
    """
    Command that adapts behavior based on surface.
    """

    def get_descriptor(self) -> dict:
        return {
            "id": "python.confirm_action",
            "title": "Confirm Action",
            "description": "Confirms an action with user (surface-specific)",
            "kind": "general"
        }

    async def execute(self, context: ExecutionContext) -> dict:
        """
        Surface-aware: different confirmation logic per surface.
        """

        # Get confirmation based on surface
        if context.surface == SurfaceType.UI:
            confirmed = await self._ui_confirmation(context)

        elif context.surface == SurfaceType.TEXT_SHELL:
            confirmed = self._cli_confirmation(context)

        elif context.surface == SurfaceType.PROGRAMMATIC:
            confirmed = self._programmatic_confirmation(context)

        elif context.surface == SurfaceType.AUTOMATION:
            confirmed = True  # Auto-confirm in automation

        elif context.surface == SurfaceType.REMOTE:
            confirmed = self._remote_confirmation(context)

        else:
            return {
                "outcome": "failed",
                "message": f"Unsupported surface: {context.surface}"
            }

        if not confirmed:
            return {
                "outcome": "cancelled",
                "message": "User declined confirmation"
            }

        # Proceed with action...
        return {
            "outcome": "success",
            "message": "Action confirmed and executed"
        }

    async def _ui_confirmation(self, context: ExecutionContext) -> bool:
        """Get confirmation from UI dialog."""
        ui_dialogs = context.get_capability("ui_dialogs")
        if ui_dialogs is None:
            return False

        # Call UI dialog capability
        return await ui_dialogs.show_confirmation(
            "Are you sure?",
            "Confirm Action"
        )

    def _cli_confirmation(self, context: ExecutionContext) -> bool:
        """Get confirmation from CLI prompt."""
        console = context.get_capability("console")
        if console is None:
            return False

        response = console.readline("Confirm? (y/n): ")
        return response.lower().strip() == 'y'

    def _programmatic_confirmation(self, context: ExecutionContext) -> bool:
        """Get confirmation from parameters."""
        return context.parameters.get("confirm", False)

    def _remote_confirmation(self, context: ExecutionContext) -> bool:
        """Remote calls must provide confirmation in parameters."""
        return self._programmatic_confirmation(context)
```

---

### ⚠️ ILLUSTRATIVE EXAMPLE: Python Multi-Surface Runner

**Python host that can execute commands on different surfaces:**

```python
# ⚠️ ILLUSTRATIVE EXAMPLE - python_multi_surface_runner.py

import asyncio
from typing import Dict, Any

class PythonCommandRunner:
    """
    Runs Python commands on different surfaces.
    """

    def __init__(self, module_id: str):
        self.module_id = module_id
        self.commands: Dict[str, VisoraCommand] = {}
        self.capabilities: Dict[str, Any] = {}

    def register_command(self, command: VisoraCommand):
        """Register a command."""
        descriptor = command.get_descriptor()
        self.commands[descriptor["id"]] = command

    def register_capability(self, name: str, capability: Any):
        """Register a capability."""
        self.capabilities[name] = capability

    async def execute_on_cli(self, command_id: str, params: dict) -> dict:
        """Execute command on CLI surface."""
        return await self._execute(
            command_id,
            SurfaceType.TEXT_SHELL,
            params
        )

    async def execute_on_ui(self, command_id: str, params: dict) -> dict:
        """Execute command on UI surface."""
        return await self._execute(
            command_id,
            SurfaceType.UI,
            params
        )

    async def execute_programmatically(self, command_id: str, params: dict) -> dict:
        """Execute command programmatically (tests, scripts)."""
        return await self._execute(
            command_id,
            SurfaceType.PROGRAMMATIC,
            params
        )

    async def execute_remotely(self, command_id: str, params: dict) -> dict:
        """Execute command via remote invocation."""
        return await self._execute(
            command_id,
            SurfaceType.REMOTE,
            params
        )

    async def _execute(
        self,
        command_id: str,
        surface: SurfaceType,
        params: dict
    ) -> dict:
        """Internal execution with surface context."""

        # Get command
        command = self.commands.get(command_id)
        if command is None:
            return {
                "outcome": "failed",
                "message": f"Command not found: {command_id}"
            }

        # Create execution context
        context = ExecutionContext(
            surface=surface,
            module_id=self.module_id,
            capabilities=self.capabilities,
            parameters=params
        )

        # Execute
        try:
            result = await command.execute(context)
            return result

        except Exception as ex:
            return {
                "outcome": "failed",
                "message": f"Command execution failed: {str(ex)}",
                "payload": {
                    "error_type": type(ex).__name__,
                    "error_message": str(ex)
                }
            }


# ⚠️ ILLUSTRATIVE USAGE

async def demo_multi_surface():
    """Demonstrate same command on different surfaces."""

    # Setup runner
    runner = PythonCommandRunner("python.demo")
    runner.register_command(PingCommand())
    runner.register_command(ConfirmActionCommand())

    # Execute ping on CLI
    print("=== Executing on CLI ===")
    result = await runner.execute_on_cli("python.ping", {})
    print(f"Result: {result}")

    # Execute ping on UI
    print("\n=== Executing on UI ===")
    result = await runner.execute_on_ui("python.ping", {})
    print(f"Result: {result}")

    # Execute ping programmatically
    print("\n=== Executing Programmatically ===")
    result = await runner.execute_programmatically("python.ping", {})
    print(f"Result: {result}")

    # Execute confirm on different surfaces
    print("\n=== Confirm on Programmatic (with confirm=True) ===")
    result = await runner.execute_programmatically(
        "python.confirm_action",
        {"confirm": True}
    )
    print(f"Result: {result}")


if __name__ == "__main__":
    asyncio.run(demo_multi_surface())
```

---

## Universal Node.js Command Invocation

### ⚠️ ILLUSTRATIVE EXAMPLE: TypeScript Command with Surface Awareness

**Illustrative TypeScript Code:**

```typescript
// ⚠️ ILLUSTRATIVE EXAMPLE - visoraSurface.ts

/**
 * Execution surface types.
 * Mirrors .NET CommandSurface.
 */
export enum SurfaceType {
    Programmatic = 'programmatic',
    TextShell = 'text_shell',
    Ui = 'ui',
    Automation = 'automation',
    Remote = 'remote'
}

/**
 * Execution context for Node.js commands.
 */
export interface ExecutionContext {
    surface: SurfaceType;
    moduleId: string;
    capabilities: Map<string, any>;
    parameters: Record<string, any>;
}

/**
 * Base interface for commands.
 */
export interface VisoraCommand {
    getDescriptor(): CommandDescriptor;
    execute(context: ExecutionContext): Promise<CommandResult>;
}

export interface CommandDescriptor {
    id: string;
    title: string;
    description?: string;
    kind?: string;
    ui?: {
        menuPath?: string;
        icon?: string;
        defaultGesture?: string;
    };
}

export interface CommandResult {
    outcome: 'success' | 'cancelled' | 'failed';
    message?: string;
    payload?: any;
}


// ⚠️ ILLUSTRATIVE EXAMPLE: Surface-agnostic command

export class PingCommand implements VisoraCommand {
    getDescriptor(): CommandDescriptor {
        return {
            id: 'nodejs.ping',
            title: 'Ping',
            description: 'Checks connectivity with Node.js module',
            kind: 'automation'
        };
    }

    async execute(context: ExecutionContext): Promise<CommandResult> {
        // Surface-agnostic: works the same on all surfaces
        const timestamp = new Date().toISOString();

        return {
            outcome: 'success',
            message: `Pong from Node.js module @ ${timestamp}`,
            payload: {
                timestamp,
                surface: context.surface,
                moduleId: context.moduleId
            }
        };
    }
}


// ⚠️ ILLUSTRATIVE EXAMPLE: Surface-aware command

export class FileReadCommand implements VisoraCommand {
    getDescriptor(): CommandDescriptor {
        return {
            id: 'nodejs.file.read',
            title: 'Read File',
            description: 'Reads file with surface-specific output'
        };
    }

    async execute(context: ExecutionContext): Promise<CommandResult> {
        const fs = require('fs').promises;
        const path = context.parameters.path;

        if (!path) {
            return {
                outcome: 'failed',
                message: 'Parameter "path" is required'
            };
        }

        try {
            const content = await fs.readFile(path, 'utf8');

            // Surface-specific output formatting
            switch (context.surface) {
                case SurfaceType.Ui:
                    // Return structured data for UI grid/viewer
                    return {
                        outcome: 'success',
                        message: `Read ${content.length} characters`,
                        payload: {
                            path,
                            length: content.length,
                            content,
                            lines: content.split('\n').length,
                            encoding: 'utf8'
                        }
                    };

                case SurfaceType.TextShell:
                    // Format for console display
                    const preview = this.formatForConsole(content);
                    return {
                        outcome: 'success',
                        message: preview,
                        payload: { path, length: content.length }
                    };

                default:
                    // Raw data for programmatic/automation/remote
                    return {
                        outcome: 'success',
                        message: `Read file: ${path}`,
                        payload: { path, content }
                    };
            }

        } catch (error: any) {
            return {
                outcome: 'failed',
                message: `Error reading file: ${error.message}`,
                payload: {
                    path,
                    errorType: error.code || error.name,
                    errorMessage: error.message
                }
            };
        }
    }

    private formatForConsole(content: string): string {
        const lines = content.split('\n');
        const preview = lines.slice(0, 10).join('\n');

        if (lines.length > 10) {
            return `${preview}\n... (${lines.length - 10} more lines)`;
        }

        return preview;
    }
}
```

---

### ⚠️ ILLUSTRATIVE EXAMPLE: Node.js Multi-Surface Runner

```typescript
// ⚠️ ILLUSTRATIVE EXAMPLE - nodeMultiSurfaceRunner.ts

export class NodeCommandRunner {
    private commands = new Map<string, VisoraCommand>();
    private capabilities = new Map<string, any>();

    constructor(private moduleId: string) {}

    registerCommand(command: VisoraCommand): void {
        const descriptor = command.getDescriptor();
        this.commands.set(descriptor.id, command);
    }

    registerCapability(name: string, capability: any): void {
        this.capabilities.set(name, capability);
    }

    async executeOnCli(
        commandId: string,
        params: Record<string, any>
    ): Promise<CommandResult> {
        return this.execute(commandId, SurfaceType.TextShell, params);
    }

    async executeOnUi(
        commandId: string,
        params: Record<string, any>
    ): Promise<CommandResult> {
        return this.execute(commandId, SurfaceType.Ui, params);
    }

    async executeProgrammatically(
        commandId: string,
        params: Record<string, any>
    ): Promise<CommandResult> {
        return this.execute(commandId, SurfaceType.Programmatic, params);
    }

    async executeRemotely(
        commandId: string,
        params: Record<string, any>
    ): Promise<CommandResult> {
        return this.execute(commandId, SurfaceType.Remote, params);
    }

    private async execute(
        commandId: string,
        surface: SurfaceType,
        params: Record<string, any>
    ): Promise<CommandResult> {
        const command = this.commands.get(commandId);

        if (!command) {
            return {
                outcome: 'failed',
                message: `Command not found: ${commandId}`
            };
        }

        const context: ExecutionContext = {
            surface,
            moduleId: this.moduleId,
            capabilities: this.capabilities,
            parameters: params
        };

        try {
            return await command.execute(context);
        } catch (error: any) {
            return {
                outcome: 'failed',
                message: `Command execution failed: ${error.message}`,
                payload: {
                    errorType: error.constructor.name,
                    errorMessage: error.message
                }
            };
        }
    }
}


// ⚠️ ILLUSTRATIVE USAGE

async function demoMultiSurface() {
    const runner = new NodeCommandRunner('nodejs.demo');

    // Register commands
    runner.registerCommand(new PingCommand());
    runner.registerCommand(new FileReadCommand());

    // Execute on different surfaces
    console.log('=== Executing on CLI ===');
    let result = await runner.executeOnCli('nodejs.ping', {});
    console.log('Result:', result);

    console.log('\n=== Executing on UI ===');
    result = await runner.executeOnUi('nodejs.ping', {});
    console.log('Result:', result);

    console.log('\n=== Executing Programmatically ===');
    result = await runner.executeProgrammatically('nodejs.ping', {});
    console.log('Result:', result);

    console.log('\n=== File read on CLI ===');
    result = await runner.executeOnCli('nodejs.file.read', {
        path: 'example.txt'
    });
    console.log('Result:', result);

    console.log('\n=== File read on UI ===');
    result = await runner.executeOnUi('nodejs.file.read', {
        path: 'example.txt'
    });
    console.log('Result:', result);
}

demoMultiSurface();
```

---

## Surface Abstraction in Python

### ⚠️ ILLUSTRATIVE PATTERN: Surface-Specific Capabilities

```python
# ⚠️ ILLUSTRATIVE EXAMPLE - surface_capabilities.py

from abc import ABC, abstractmethod
from typing import Optional

class IConsoleHost(ABC):
    """Console host capability for CLI surface."""

    @abstractmethod
    def write(self, text: str) -> None:
        """Write text to console."""
        pass

    @abstractmethod
    def writeline(self, text: str) -> None:
        """Write line to console."""
        pass

    @abstractmethod
    def readline(self, prompt: str = "") -> str:
        """Read line from console."""
        pass


class IUiDialogs(ABC):
    """UI dialog capability for UI surface."""

    @abstractmethod
    async def show_confirmation(
        self,
        message: str,
        title: str = "Confirm"
    ) -> bool:
        """Show confirmation dialog."""
        pass

    @abstractmethod
    async def show_message(
        self,
        message: str,
        title: str = "Message"
    ) -> None:
        """Show message dialog."""
        pass


class StdoutConsoleHost(IConsoleHost):
    """Implementation for CLI surface using stdout."""

    def write(self, text: str) -> None:
        import sys
        sys.stdout.write(text)
        sys.stdout.flush()

    def writeline(self, text: str) -> None:
        print(text)

    def readline(self, prompt: str = "") -> str:
        return input(prompt)


class SurfaceCapabilitiesFactory:
    """
    Creates appropriate capabilities based on surface.
    """

    @staticmethod
    def create_for_surface(surface: SurfaceType) -> dict:
        """Create capabilities appropriate for surface."""

        capabilities = {}

        if surface == SurfaceType.TEXT_SHELL:
            capabilities["console"] = StdoutConsoleHost()

        elif surface == SurfaceType.UI:
            # In real implementation, would create actual UI dialogs
            capabilities["ui_dialogs"] = MockUiDialogs()

        # Add common capabilities available on all surfaces
        capabilities["logger"] = create_logger(surface)

        return capabilities


class MockUiDialogs(IUiDialogs):
    """Mock UI dialogs for testing/illustration."""

    async def show_confirmation(
        self,
        message: str,
        title: str = "Confirm"
    ) -> bool:
        print(f"[UI Dialog] {title}: {message}")
        return True  # Mock: always confirm

    async def show_message(
        self,
        message: str,
        title: str = "Message"
    ) -> None:
        print(f"[UI Dialog] {title}: {message}")
```

---

## Surface Abstraction in Node.js

### ⚠️ ILLUSTRATIVE PATTERN: Surface-Specific I/O Adapters

```typescript
// ⚠️ ILLUSTRATIVE EXAMPLE - surfaceAdapters.ts

/**
 * Console host capability for CLI surface.
 */
export interface IConsoleHost {
    write(text: string): void;
    writeLine(text: string): void;
    readLine(prompt?: string): Promise<string>;
}

/**
 * UI dialogs capability for UI surface.
 */
export interface IUiDialogs {
    showConfirmation(message: string, title?: string): Promise<boolean>;
    showMessage(message: string, title?: string): Promise<void>;
    showError(message: string, title?: string): Promise<void>;
}

/**
 * Implementation for CLI surface.
 */
export class NodeConsoleHost implements IConsoleHost {
    write(text: string): void {
        process.stdout.write(text);
    }

    writeLine(text: string): void {
        console.log(text);
    }

    async readLine(prompt: string = ''): Promise<string> {
        const readline = require('readline');
        const rl = readline.createInterface({
            input: process.stdin,
            output: process.stdout
        });

        return new Promise((resolve) => {
            rl.question(prompt, (answer: string) => {
                rl.close();
                resolve(answer);
            });
        });
    }
}

/**
 * Mock UI dialogs for testing/illustration.
 */
export class MockUiDialogs implements IUiDialogs {
    async showConfirmation(
        message: string,
        title: string = 'Confirm'
    ): Promise<boolean> {
        console.log(`[UI Dialog] ${title}: ${message}`);
        return true; // Mock: always confirm
    }

    async showMessage(
        message: string,
        title: string = 'Message'
    ): Promise<void> {
        console.log(`[UI Dialog] ${title}: ${message}`);
    }

    async showError(
        message: string,
        title: string = 'Error'
    ): Promise<void> {
        console.error(`[UI Dialog] ${title}: ${message}`);
    }
}

/**
 * Factory for creating surface-specific capabilities.
 */
export class SurfaceCapabilitiesFactory {
    static createForSurface(surface: SurfaceType): Map<string, any> {
        const capabilities = new Map<string, any>();

        switch (surface) {
            case SurfaceType.TextShell:
                capabilities.set('console', new NodeConsoleHost());
                break;

            case SurfaceType.Ui:
                capabilities.set('uiDialogs', new MockUiDialogs());
                break;

            // Automation and Remote: minimal capabilities
            case SurfaceType.Automation:
            case SurfaceType.Remote:
            case SurfaceType.Programmatic:
                // No interactive capabilities
                break;
        }

        // Common capabilities available on all surfaces
        capabilities.set('logger', createLogger(surface));

        return capabilities;
    }
}

function createLogger(surface: SurfaceType): any {
    // Return surface-appropriate logger
    return console; // Simplified for illustration
}
```

---

## Cross-Language Multi-Surface Examples

### ⚠️ ILLUSTRATIVE SCENARIO: .NET Host Invoking Python/Node.js Commands on Different Surfaces

```csharp
// ⚠️ ILLUSTRATIVE EXAMPLE

public class PolyglotMultiSurfaceHost
{
    private readonly PythonCommandRunner _pythonRunner;
    private readonly NodeCommandRunner _nodeRunner;

    public async Task<CommandResult> ExecuteCommandOnSurface(
        string commandId,
        CommandSurface surface,
        Dictionary<string, object?> parameters,
        CancellationToken ct)
    {
        // Determine runtime from command ID
        if (commandId.StartsWith("python."))
        {
            return await ExecutePythonCommandOnSurface(
                commandId, surface, parameters, ct);
        }
        else if (commandId.StartsWith("nodejs."))
        {
            return await ExecuteNodeCommandOnSurface(
                commandId, surface, parameters, ct);
        }
        else
        {
            // .NET command
            return await ExecuteDotNetCommandOnSurface(
                commandId, surface, parameters, ct);
        }
    }

    private async Task<CommandResult> ExecutePythonCommandOnSurface(
        string commandId,
        CommandSurface surface,
        Dictionary<string, object?> parameters,
        CancellationToken ct)
    {
        // Map .NET surface to Python surface string
        var pythonSurface = MapSurfaceToPython(surface);

        // Create Python execution context
        var contextJson = JsonSerializer.Serialize(new
        {
            surface = pythonSurface,
            module_id = "python.module",
            capabilities = CreateCapabilitiesForSurface(surface),
            parameters = parameters
        });

        // Execute via Python.NET
        using (Py.GIL())
        {
            dynamic runner = GetPythonRunner();
            dynamic resultDict = await runner.execute(
                commandId,
                Py.kw("context_json", contextJson));

            // Convert Python result to .NET CommandResult
            return MapPythonResult(resultDict);
        }
    }

    private string MapSurfaceToPython(CommandSurface surface) => surface switch
    {
        CommandSurface.Programmatic => "programmatic",
        CommandSurface.TextShell => "text_shell",
        CommandSurface.Ui => "ui",
        CommandSurface.Automation => "automation",
        CommandSurface.Remote => "remote",
        _ => throw new ArgumentException($"Unknown surface: {surface}")
    };
}
```

---

## I/O Model Challenges

### Challenge: Different I/O Paradigms per Surface

**CLI Surface:**
- Synchronous I/O (stdin/stdout)
- Blocking reads
- Line-oriented

**UI Surface:**
- Event-driven
- Asynchronous callbacks
- Message-based

**Automation Surface:**
- No I/O (headless)
- Logging only
- Result-based

**Remote Surface:**
- Network I/O
- Async messaging
- Serialization required

### ⚠️ ILLUSTRATIVE SOLUTION: Abstracted I/O Layer

```python
# ⚠️ ILLUSTRATIVE EXAMPLE

from abc import ABC, abstractmethod

class IInputOutput(ABC):
    """Abstract I/O interface."""

    @abstractmethod
    async def prompt_user(self, message: str) -> str:
        """Prompt user for input."""
        pass

    @abstractmethod
    async def display_message(self, message: str) -> None:
        """Display message to user."""
        pass

    @abstractmethod
    async def report_progress(self, current: int, total: int) -> None:
        """Report progress."""
        pass


class CliInputOutput(IInputOutput):
    """CLI I/O implementation."""

    async def prompt_user(self, message: str) -> str:
        return input(f"{message}: ")

    async def display_message(self, message: str) -> None:
        print(message)

    async def report_progress(self, current: int, total: int) -> None:
        percentage = (current / total) * 100
        print(f"Progress: {percentage:.1f}% ({current}/{total})")


class UiInputOutput(IInputOutput):
    """UI I/O implementation (via callbacks/events)."""

    def __init__(self, ui_context):
        self.ui_context = ui_context

    async def prompt_user(self, message: str) -> str:
        return await self.ui_context.show_input_dialog(message)

    async def display_message(self, message: str) -> None:
        await self.ui_context.show_message(message)

    async def report_progress(self, current: int, total: int) -> None:
        percentage = (current / total) * 100
        await self.ui_context.update_progress_bar(percentage)


class NullInputOutput(IInputOutput):
    """Null I/O for automation/remote (no interaction)."""

    async def prompt_user(self, message: str) -> str:
        raise RuntimeError("Cannot prompt user in automation mode")

    async def display_message(self, message: str) -> None:
        pass  # Silent

    async def report_progress(self, current: int, total: int) -> None:
        pass  # Silent


def create_io_for_surface(surface: SurfaceType, context) -> IInputOutput:
    """Factory for creating I/O implementation per surface."""

    if surface == SurfaceType.TEXT_SHELL:
        return CliInputOutput()
    elif surface == SurfaceType.UI:
        return UiInputOutput(context)
    else:
        return NullInputOutput()
```

---

## Surface Detection Patterns

### ⚠️ ILLUSTRATIVE PATTERN: Auto-Detecting Execution Surface

```typescript
// ⚠️ ILLUSTRATIVE EXAMPLE

export class SurfaceDetector {
    /**
     * Attempt to auto-detect current execution surface.
     */
    static detectSurface(): SurfaceType {
        // Check for UI context
        if (this.isUiContext()) {
            return SurfaceType.Ui;
        }

        // Check for CLI/TTY
        if (this.isCliContext()) {
            return SurfaceType.TextShell;
        }

        // Check for automation (no TTY, specific env vars)
        if (this.isAutomationContext()) {
            return SurfaceType.Automation;
        }

        // Check for remote (HTTP headers, network context)
        if (this.isRemoteContext()) {
            return SurfaceType.Remote;
        }

        // Default: programmatic
        return SurfaceType.Programmatic;
    }

    private static isUiContext(): boolean {
        // Check for browser/electron environment
        return typeof window !== 'undefined' && typeof document !== 'undefined';
    }

    private static isCliContext(): boolean {
        // Check for TTY (terminal)
        return process.stdout.isTTY && process.stdin.isTTY;
    }

    private static isAutomationContext(): boolean {
        // Check for CI environment variables
        return !!(
            process.env.CI ||
            process.env.JENKINS_HOME ||
            process.env.GITHUB_ACTIONS
        );
    }

    private static isRemoteContext(): boolean {
        // Check for HTTP context (simplified)
        return !!(
            process.env.HTTP_HOST ||
            process.env.REMOTE_ADDR
        );
    }
}


// Usage
const surface = SurfaceDetector.detectSurface();
console.log(`Detected surface: ${surface}`);
```

---

## Challenges & Considerations

### Technical Challenges

**1. I/O Paradigm Mismatches**
- CLI: Blocking I/O
- UI: Event-driven async
- Automation: No I/O
- **Solution:** Abstract I/O layer with async interface

**2. Progress Reporting**
- CLI: Line-by-line updates
- UI: Progress bars, real-time updates
- Automation: Silent or log-based
- **Solution:** Surface-specific progress reporters

**3. Error Display**
- CLI: Text error messages
- UI: Dialog boxes with icons
- Remote: JSON error objects
- **Solution:** Structured error results, surface adapts display

**4. User Interaction**
- Some surfaces support interaction (CLI, UI)
- Others don't (automation, remote)
- **Solution:** Graceful degradation, parameter-based alternatives

**5. Security Constraints**
- UI: More permissions (file dialogs)
- Remote: Restricted permissions
- **Solution:** Capability-based access control per surface

---

## Related Documentation

- [Multi-Surface Execution - VISORA Analysis](./visora-analysis.md) - Actual .NET implementation
- [Command Execution - VISORA Analysis](../command-execution/visora-analysis.md)
- [Async Patterns - Meta-Platform](../async-patterns/meta-platform-illustrations.md)

---

## Further Reading

### Surface Detection
- [Node.js TTY Documentation](https://nodejs.org/api/tty.html)
- [Python sys.stdin.isatty()](https://docs.python.org/3/library/sys.html#sys.stdin)

### UI Frameworks
- [Electron](https://www.electronjs.org/) - Cross-platform desktop apps
- [Tkinter](https://docs.python.org/3/library/tkinter.html) - Python GUI
- [React](https://react.dev/) - Web UI

---

**Remember:** These are ILLUSTRATIVE EXAMPLES to inspire exploration, not production-ready solutions. Multi-surface execution across runtimes requires careful abstraction design, comprehensive testing on all surfaces, and clear documentation of surface-specific behaviors.
