# Feature: Gmail MCP Server

**Status**: Exploring
**Created**: 2026-02-12

## Problem Statement
Enable AI assistants (like Claude) to interact with Gmail programmatically through the Model Context Protocol (MCP). The server needs to provide essential Gmail operations (search messages, read messages, download attachments) with automatic authentication, making Gmail integration seamless and secure for AI-powered workflows.

## Constraints
- Must use .NET 10 (LTS release) for long-term support until November 2028
- Must implement MCP protocol using the official C# SDK maintained by Microsoft
- Must support auto-authentication flow similar to the reference TypeScript implementation (E:\data\src\Gmail-MCP-Server)
- Must be deployable as a .NET global tool for easy installation and distribution
- Must follow .NET best practices for 2026 (cloud-native architecture, containerization support, security)
- Initial scope limited to: search messages, read message, download attachment
- Must store OAuth credentials securely in user's home directory (~/.gmail-mcp/)
- Must support both Desktop and Web application OAuth credentials from Google Cloud Platform

## Investigations
| Investigation | Status | Summary |
|--------------|--------|---------|
| [official-csharp-mcp-sdk](investigations/official-csharp-mcp-sdk.md) | In Progress | Use official Microsoft-maintained MCP C# SDK with Google.Apis.Gmail.v1, .NET 10 LTS, and stdio transport. Production-ready approach mirroring TypeScript reference implementation. |
| [dual-mode-cli-architecture](investigations/dual-mode-cli-architecture.md) | In Progress | Build dual-mode tool using System.CommandLine supporting both direct CLI operations (`dotnet-gmail search "query"`) and MCP server mode (`dotnet-gmail mcp`). Shares Gmail service layer between modes. |

## Decision
*No ADR yet - investigations in progress*
