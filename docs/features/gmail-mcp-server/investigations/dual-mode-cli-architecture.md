# Investigation: Dual-Mode CLI Architecture (CLI + MCP)

**Feature**: gmail-mcp-server
**Status**: In Progress
**Created**: 2026-02-12

## Approach

Build a dual-mode .NET 10 global tool using System.CommandLine that supports both direct CLI operations and MCP server mode. This enables users to perform Gmail operations directly from the command line (e.g., `dotnet-gmail search "ball realty"`) or run as an MCP server (e.g., `dotnet-gmail mcp`) for AI assistant integration.

### Architecture

```
┌─────────────────────────────────────────────────────────┐
│  dotnet-gmail (Global Tool Entry Point)                │
├─────────────────────────────────────────────────────────┤
│                                                         │
│  ┌─────────────────┐         ┌────────────────────┐   │
│  │ CLI Mode        │         │ MCP Mode           │   │
│  │                 │         │                    │   │
│  │ search          │         │ mcp                │   │
│  │ read            │         │ └─> MCP Server     │   │
│  │ download        │         │     (stdio)        │   │
│  │ auth            │         │                    │   │
│  └────────┬────────┘         └──────────┬─────────┘   │
│           │                             │             │
│           └──────────┬──────────────────┘             │
│                      │                                 │
│         ┌────────────▼───────────────┐                │
│         │ Shared Gmail Service Layer │                │
│         │ - GmailService              │                │
│         │ - AuthenticationService     │                │
│         │ - Google.Apis.Gmail.v1      │                │
│         └─────────────────────────────┘                │
└─────────────────────────────────────────────────────────┘
```

### Key Components

1. **System.CommandLine Framework**
   - RootCommand with description: "Gmail MCP Server - CLI and MCP integration"
   - Subcommands for each operation
   - Shared dependency injection container

2. **CLI Commands**
   - `dotnet-gmail search <query> [--max-results <n>]` - Search Gmail messages
   - `dotnet-gmail read <message-id>` - Read a specific message
   - `dotnet-gmail download <message-id> <attachment-id> [--output <path>]` - Download attachment
   - `dotnet-gmail auth` - Authenticate with Gmail OAuth
   - Output formats: human-readable by default, `--robot` flag for JSON

3. **MCP Command**
   - `dotnet-gmail mcp` - Start MCP server on stdio
   - Exposes same operations as MCP tools
   - No console output (only stdio protocol messages)
   - Shares service layer with CLI commands

4. **Shared Service Layer**
   - `IGmailService` - Core Gmail operations
   - `IAuthenticationService` - OAuth flow management
   - Dependency injection for testability
   - Single configuration source

### Implementation Structure

```
src/
├── GmailMcp/
│   ├── GmailMcp.csproj
│   ├── Program.cs                      # Entry point, System.CommandLine setup
│   │
│   ├── Commands/
│   │   ├── SearchCommand.cs            # CLI: search messages
│   │   ├── ReadCommand.cs              # CLI: read message
│   │   ├── DownloadCommand.cs          # CLI: download attachment
│   │   ├── AuthCommand.cs              # CLI: OAuth authentication
│   │   └── McpCommand.cs               # MCP: start MCP server
│   │
│   ├── Mcp/
│   │   ├── GmailMcpServer.cs           # MCP server implementation
│   │   └── Tools/
│   │       ├── SearchMessagesTool.cs   # IServerTool implementation
│   │       ├── ReadMessageTool.cs      # IServerTool implementation
│   │       └── DownloadAttachmentTool.cs
│   │
│   ├── Services/
│   │   ├── IGmailService.cs            # Interface
│   │   ├── GmailService.cs             # Gmail API wrapper (shared)
│   │   ├── IAuthenticationService.cs   # Interface
│   │   └── AuthenticationService.cs    # OAuth flow (shared)
│   │
│   └── Models/
│       ├── MessageSearchRequest.cs
│       ├── MessageReadRequest.cs
│       ├── AttachmentDownloadRequest.cs
│       └── GmailMessage.cs
```

### Code Example (Program.cs)

```csharp
using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using GmailMcp.Commands;
using GmailMcp.Services;

namespace GmailMcp;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Set up dependency injection
        var services = new ServiceCollection();
        ConfigureServices(services);

        await using var serviceProvider = services.BuildServiceProvider();

        // Initialize services
        var authService = serviceProvider.GetRequiredService<IAuthenticationService>();
        await authService.EnsureConfigDirectoryAsync();

        // Create root command
        var rootCommand = new RootCommand(
            "Gmail MCP Server - CLI and MCP integration for Gmail")
        {
            SearchCommand.Create(serviceProvider),
            ReadCommand.Create(serviceProvider),
            DownloadCommand.Create(serviceProvider),
            AuthCommand.Create(serviceProvider),
            McpCommand.Create(serviceProvider)
        };

        return await rootCommand.InvokeAsync(args);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Gmail services
        services.AddSingleton<IGmailService, GmailService>();
        services.AddSingleton<IAuthenticationService, AuthenticationService>();
    }
}
```

### Code Example (McpCommand.cs)

```csharp
using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using GmailMcp.Mcp;
using GmailMcp.Services;

namespace GmailMcp.Commands;

public class McpCommand : Command
{
    private McpCommand() : base("mcp", "Start MCP server for AI assistant integration via stdio")
    {
        // MCP server operates on stdio protocol - no additional options needed
    }

    public static Command Create(IServiceProvider serviceProvider)
    {
        var command = new McpCommand();

        command.SetHandler(async () =>
        {
            var gmailService = serviceProvider.GetRequiredService<IGmailService>();
            var authService = serviceProvider.GetRequiredService<IAuthenticationService>();

            await GmailMcpServer.RunAsync(gmailService, authService, CancellationToken.None);
        });

        return command;
    }
}
```

### Code Example (SearchCommand.cs)

```csharp
using System.CommandLine;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using GmailMcp.Services;

namespace GmailMcp.Commands;

public class SearchCommand : Command
{
    private SearchCommand() : base("search", "Search Gmail messages")
    {
        var queryArgument = new Argument<string>(
            name: "query",
            description: "Search query (Gmail search syntax)");

        var maxResultsOption = new Option<int>(
            name: "--max-results",
            getDefaultValue: () => 10,
            description: "Maximum number of results");
        maxResultsOption.AddAlias("-n");

        var robotOption = new Option<bool>(
            name: "--robot",
            description: "Output as JSON for scripting");
        robotOption.AddAlias("-r");

        this.AddArgument(queryArgument);
        this.AddOption(maxResultsOption);
        this.AddOption(robotOption);
    }

    public static Command Create(IServiceProvider serviceProvider)
    {
        var command = new SearchCommand();

        command.SetHandler(async (query, maxResults, robot) =>
        {
            var gmailService = serviceProvider.GetRequiredService<IGmailService>();
            await ExecuteAsync(query, maxResults, robot, gmailService);
        },
        command.Arguments[0] as Argument<string> ?? throw new InvalidOperationException(),
        command.Options[0] as Option<int> ?? throw new InvalidOperationException(),
        command.Options[1] as Option<bool> ?? throw new InvalidOperationException());

        return command;
    }

    private static async Task ExecuteAsync(
        string query,
        int maxResults,
        bool robot,
        IGmailService gmailService)
    {
        if (!robot)
        {
            Console.WriteLine($"Searching for: \"{query}\"");
            Console.WriteLine();
        }

        var results = await gmailService.SearchMessagesAsync(query, maxResults);

        if (robot)
        {
            var json = JsonSerializer.Serialize(results, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            Console.WriteLine(json);
        }
        else
        {
            if (results.Count == 0)
            {
                Console.WriteLine("No results found.");
                return;
            }

            Console.WriteLine($"Found {results.Count} result(s):\n");

            for (int i = 0; i < results.Count; i++)
            {
                var result = results[i];
                Console.WriteLine($"[{i + 1}] {result.Subject}");
                Console.WriteLine($"    From: {result.From}");
                Console.WriteLine($"    Date: {result.Date:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine($"    ID: {result.Id}");
                Console.WriteLine();
            }
        }
    }
}
```

## Tradeoffs

| Pros | Cons |
|------|------|
| **Dual-purpose tool**: Same codebase serves CLI and MCP use cases | **Additional complexity**: More moving parts than MCP-only approach |
| **Developer UX**: Can test Gmail operations directly without MCP client | **Larger codebase**: More commands and handlers to maintain |
| **Shared services**: Single implementation for Gmail logic, reduced duplication | **CLI dependency**: Adds System.CommandLine package |
| **Debugging**: Easier to test and debug Gmail operations in CLI mode | **Cognitive overhead**: Two different usage patterns to document |
| **Script-friendly**: `--robot` flag enables JSON output for scripting | **Binary size**: Slightly larger due to CLI framework |
| **Consistent experience**: Same authentication and configuration for both modes | **Testing surface**: Need to test both CLI and MCP interfaces |
| **Better error messages**: CLI mode can show rich error output to console | |

## Alignment

- [x] **Follows architectural layering rules**: Clear separation between CLI commands, MCP tools, and shared services
- [x] **Developer Experience**: Both modes use same `dotnet tool install -g dotnet-gmail`
- [x] **Specification compliance**: MCP mode uses official C# SDK, CLI mode uses System.CommandLine
- [x] **Consistent with existing patterns**: Mirrors agent-session-search-tools dual-mode architecture

## Evidence

### System.CommandLine in .NET 10

From [Microsoft Learn - System.CommandLine](https://learn.microsoft.com/en-us/dotnet/standard/commandline/):
- Official Microsoft library for building command-line applications
- Strongly-typed argument and option parsing
- Built-in help generation
- Tab completion support
- Parse directives for debugging

### Reference Implementation: agent-session-search-tools

From examination of `E:\data\src\agent-session-search-tools`:
- Uses System.CommandLine with RootCommand pattern
- MCP mode via `McpCommand.Create(serviceProvider)`
- CLI mode via multiple command classes (SearchCommand, IndexCommand, etc.)
- Shared services via dependency injection
- Robot mode (`--robot`) for JSON output enables scripting

**Key Pattern**:
```csharp
var rootCommand = new RootCommand("Description")
{
    SearchCommand.Create(serviceProvider),
    McpCommand.Create(serviceProvider),
    // ... other commands
};
return await rootCommand.InvokeAsync(args);
```

### MCP Server in CLI Context

From `AgentJournal.Commands.McpCommand`:
- No console output in MCP mode (only stdio protocol)
- Shares same service instances with CLI commands
- Uses `await AgentJournalMcpServer.RunAsync(...)` pattern
- Services injected from DI container

### Benefits of Dual-Mode Architecture

**Testing & Development**:
- CLI commands can be tested without MCP client
- Easier to debug Gmail API interactions
- Can verify OAuth flow in isolation

**User Experience**:
- Quick one-off operations via CLI
- Persistent AI integration via MCP
- Single installation for both use cases

**Maintenance**:
- Shared service layer reduces duplication
- Same authentication for both modes
- Single codebase, single deployment

## Verdict

**Strongly recommended** - The dual-mode architecture provides superior developer experience and flexibility with minimal added complexity. Following the proven pattern from agent-session-search-tools, this approach enables both quick CLI operations and persistent MCP integration while sharing the same Gmail service layer. The System.CommandLine framework is Microsoft-official and provides excellent ergonomics for CLI development.

This approach is particularly valuable for:
1. **Development**: Test Gmail operations directly without MCP client
2. **Scripting**: Use `--robot` flag for JSON output in scripts
3. **Debugging**: Rich console output for troubleshooting
4. **Flexibility**: Users can choose CLI or MCP based on their workflow

### Recommended Implementation Order

1. Implement shared service layer (GmailService, AuthenticationService)
2. Implement AuthCommand for OAuth flow
3. Implement CLI commands (search, read, download) for testing
4. Implement MCP command and server wrapping same services
5. Add `--robot` flag for JSON output

## Alternative Considerations

1. **CLI-Only First**: Build CLI commands first, add MCP later
2. **Separate Tools**: Build `dotnet-gmail` (CLI) and `dotnet-gmail-mcp` (MCP) as separate packages
3. **MCP-First with Admin CLI**: Focus on MCP, add minimal admin commands (auth, config only)

## Sources

- [System.CommandLine documentation - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/standard/commandline/)
- [agent-session-search-tools reference implementation](E:\data\src\agent-session-search-tools)
- [MCP C# SDK Official Repository](https://github.com/modelcontextprotocol/csharp-sdk)
- [Build a Model Context Protocol (MCP) server in C#](https://devblogs.microsoft.com/dotnet/build-a-model-context-protocol-mcp-server-in-csharp/)
