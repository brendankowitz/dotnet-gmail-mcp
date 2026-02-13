# Investigation: Official C# MCP SDK with Google.Apis.Gmail

**Feature**: gmail-mcp-server
**Status**: In Progress
**Created**: 2026-02-12

## Approach

Build a .NET 10 global tool that implements the Model Context Protocol using the official C# SDK maintained by Microsoft. The server will use Google.Apis.Gmail.v1 for Gmail integration, stdio transport for MCP communication, and OAuth2 authentication following the same pattern as the TypeScript reference implementation.

### Architecture

```
┌─────────────────────────────────────────────┐
│  Claude Desktop / MCP Client                │
└─────────────────┬───────────────────────────┘
                  │ stdio (stdin/stdout)
┌─────────────────▼───────────────────────────┐
│  .NET Global Tool (gmail-mcp)               │
│  ┌─────────────────────────────────────┐   │
│  │ MCP Server (stdio transport)        │   │
│  │ - ModelContextProtocol.Server       │   │
│  │ - Tool: search_messages             │   │
│  │ - Tool: read_message                │   │
│  │ - Tool: download_attachment         │   │
│  └──────────────┬──────────────────────┘   │
│                 │                            │
│  ┌──────────────▼──────────────────────┐   │
│  │ Gmail Service Layer                 │   │
│  │ - Google.Apis.Gmail.v1              │   │
│  │ - OAuth2 Authentication             │   │
│  └──────────────┬──────────────────────┘   │
└─────────────────┼───────────────────────────┘
                  │
┌─────────────────▼───────────────────────────┐
│  Gmail API (REST)                           │
└─────────────────────────────────────────────┘
```

### Key Components

1. **MCP Server Layer**
   - Use `ModelContextProtocol` NuGet package
   - Stdio transport via `.WithStdioServerTransport()`
   - Tool registration using dependency injection
   - Schema validation with .NET type system

2. **Gmail Integration**
   - `Google.Apis.Gmail.v1` NuGet package
   - `Google.Apis.Auth.OAuth2` for authentication
   - UserCredential with refresh token support
   - Scopes: `https://www.googleapis.com/auth/gmail.modify`

3. **Authentication Flow**
   - OAuth keys stored in `~/.gmail-mcp/gcp-oauth.keys.json`
   - Credentials cached in `~/.gmail-mcp/credentials.json`
   - Auto-launch browser for OAuth consent (using `System.Diagnostics.Process`)
   - Local HTTP server on port 3000 for OAuth callback
   - Support both Desktop and Web application credentials

4. **Global Tool Packaging**
   - Target: `net10.0` (LTS until November 2028)
   - PackAsTool: true
   - ToolCommandName: `gmail-mcp`
   - Support for `gmail-mcp auth` command for initial setup

### Implementation Structure

```
src/
├── GmailMcp/
│   ├── GmailMcp.csproj
│   ├── Program.cs                    # Entry point, MCP server setup
│   ├── Tools/
│   │   ├── SearchMessagesTool.cs     # IServerTool implementation
│   │   ├── ReadMessageTool.cs        # IServerTool implementation
│   │   └── DownloadAttachmentTool.cs # IServerTool implementation
│   ├── Services/
│   │   ├── GmailService.cs           # Gmail API wrapper
│   │   └── AuthenticationService.cs  # OAuth flow management
│   └── Models/
│       ├── MessageSearchRequest.cs
│       ├── MessageReadRequest.cs
│       └── AttachmentDownloadRequest.cs
```

### Code Example (Program.cs)

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol;
using Google.Apis.Gmail.v1;
using Google.Apis.Auth.OAuth2;

var builder = Host.CreateApplicationBuilder(args);

// Check for auth command
if (args.Length > 0 && args[0] == "auth")
{
    await AuthenticationService.RunAuthenticationFlowAsync();
    return;
}

// Configure MCP Server
builder.Services.AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

// Register Gmail service
builder.Services.AddSingleton<IGmailService, GmailService>();
builder.Services.AddSingleton<IAuthenticationService, AuthenticationService>();

var app = builder.Build();
await app.RunAsync();
```

## Tradeoffs

| Pros | Cons |
|------|------|
| **Official SDK**: Microsoft-maintained, production-ready (used in Xbox Gaming Copilot, Copilot Studio) | **Newer ecosystem**: Less community examples than TypeScript SDK |
| **.NET 10 LTS**: Supported until November 2028, enterprise-ready | **Learning curve**: Teams unfamiliar with .NET ecosystem |
| **Strong typing**: Compile-time safety for MCP schemas and Gmail API | **Build tooling**: Requires .NET SDK installed for development |
| **Native performance**: Better performance than TypeScript/Node.js for I/O operations | **Cross-platform distribution**: Users need .NET runtime (unless using NativeAOT) |
| **Google.Apis.Gmail**: Well-established, official Google client library with refresh token support | **Package size**: Larger than minimal TypeScript implementation |
| **Global tool**: Easy installation via `dotnet tool install -g gmail-mcp` | **Version management**: Users may have different .NET versions |
| **Dependency Injection**: Built-in DI for clean architecture and testability | **Complexity**: More sophisticated than simple script approach |
| **async/await**: Native async support throughout the stack | |

## Alignment

- [x] **Follows architectural layering rules**: Clear separation between MCP layer, service layer, and API client
- [x] **Developer Experience**: Works with minimal setup - `dotnet tool install -g gmail-mcp` and `gmail-mcp auth`
- [x] **Specification compliance**: Uses official MCP C# SDK following latest protocol spec
- [x] **Consistent with existing patterns**: Mirrors TypeScript implementation's auth flow and credential storage

## Evidence

### MCP C# SDK Production Usage

From [Visual Studio Magazine (April 2025)](https://visualstudiomagazine.com/articles/2025/04/14/trending-model-context-protocol-for-ai-agents-gets-csharp-sdk.aspx) and [.NET Blog](https://devblogs.microsoft.com/dotnet/build-a-model-context-protocol-mcp-server-in-csharp/):
- Microsoft released MCP C# SDK in preview (November 2025)
- Despite preview status, SDK is production-ready with usage in Xbox Gaming Copilot and Copilot Studio
- Maintained in collaboration with Microsoft, indicating long-term support commitment

### .NET 10 Availability and Support

From [.NET Blog (Announcing .NET 10)](https://devblogs.microsoft.com/dotnet/announcing-dotnet-10/) and [Microsoft Support](https://support.microsoft.com/en-us/topic/-net-10-0-update-february-10-2026-8f92b4a7-5f32-4943-a6aa-4abac452bb34):
- Released November 11, 2025
- Long Term Support (LTS) until November 14, 2028
- Fully supported throughout 2026 and beyond

### Stdio Transport in MCP C# SDK

From [MCP C# SDK Repository](https://github.com/modelcontextprotocol/csharp-sdk) and [DeepWiki](https://deepwiki.com/modelcontextprotocol/csharp-sdk/4.1-stdio-transport):
- `.WithStdioServerTransport()` configures stdio communication
- Handles process management, stream buffering, and error handling automatically
- Particularly useful for local integrations and command-line tools
- Implements IServerTransport interface for standardized communication

### Google.Apis.Gmail Authentication

From [Google for Developers - OAuth 2.0](https://developers.google.com/api-client-library/dotnet/guide/aaa_oauth) and [OAuth 2.0 Namespace](https://cloud.google.com/dotnet/docs/reference/Google.Apis/latest/Google.Apis.Auth.OAuth2):
- `Google.Apis.Auth` and `Google.Apis.Gmail.v1` provide official OAuth2 support
- UserCredential supports refresh tokens for persistent authentication
- Access tokens expire after ~1 hour, refresh tokens enable automatic renewal
- Recommended to use official libraries for security and OAuth best practices

### .NET Global Tool Packaging

From [Andrew Lock - Packaging .NET 10 Tools](https://andrewlock.net/exploring-dotnet-10-preview-features-7-packaging-self-contained-and-native-aot-dotnet-tools-for-nuget/) and [solrevdev](https://solrevdev.com/2025/11/14/upgrading-seedfolder-to-dotnet-10-lts.html):
- Self-contained and NativeAOT options available for deployment without runtime dependency
- Multi-targeting recommended: `net8.0;net9.0;net10.0` for broad compatibility
- Global tools installable via `dotnet tool install -g <package-name>`
- Best practice: Keep previous LTS (net8.0) alongside net10.0

### Reference Implementation Analysis

From examination of `E:\data\src\Gmail-MCP-Server`:
- TypeScript implementation uses `@modelcontextprotocol/sdk` with stdio transport
- OAuth flow: local HTTP server on port 3000, auto-launch browser, store credentials
- Configuration directory: `~/.gmail-mcp/` with `gcp-oauth.keys.json` and `credentials.json`
- Supports both Desktop app and Web application OAuth credentials
- Command structure: `npx <package> auth` for authentication, no args for server mode

## Verdict

**Recommended for implementation** - The official C# MCP SDK provides a production-ready foundation with enterprise support, strong typing, and native async performance. The approach mirrors the proven TypeScript implementation while leveraging .NET's advantages in type safety and dependency injection. With .NET 10 LTS support until 2028 and Microsoft's commitment to the MCP ecosystem, this provides a stable, maintainable solution.

### Alternative Approaches to Consider

1. **Native AOT Deployment**: Package as self-contained executable to eliminate .NET runtime dependency
2. **HTTP Transport Alternative**: Use `ModelContextProtocol.AspNetCore` for HTTP-based MCP (useful for containerized deployments)
3. **Minimal API Approach**: Build lightweight server without DI framework for reduced complexity

## Sources

- [.NET and .NET Core official support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)
- [Announcing .NET 10](https://devblogs.microsoft.com/dotnet/announcing-dotnet-10/)
- [MCP C# SDK Official Repository](https://github.com/modelcontextprotocol/csharp-sdk)
- [Build a Model Context Protocol (MCP) server in C#](https://devblogs.microsoft.com/dotnet/build-a-model-context-protocol-mcp-server-in-csharp/)
- [Trending Model Context Protocol for AI Agents Gets C# SDK](https://visualstudiomagazine.com/articles/2025/04/14/trending-model-context-protocol-for-ai-agents-gets-csharp-sdk.aspx)
- [OAuth 2.0 | API Client Library for .NET](https://developers.google.com/api-client-library/dotnet/guide/aaa_oauth)
- [Packaging self-contained and native AOT .NET tools for NuGet](https://andrewlock.net/exploring-dotnet-10-preview-features-7-packaging-self-contained-and-native-aot-dotnet-tools-for-nuget/)
- [Package authoring best practices - NuGet](https://learn.microsoft.com/en-us/nuget/create-packages/package-authoring-best-practices)
