# 📧 Gmail MCP Server

> Dual-mode Gmail integration: Direct CLI operations + MCP server for AI assistants

![.NET 8.0 | 10.0](https://img.shields.io/badge/.NET-8.0%20%7C%2010.0-512BD4?logo=dotnet)
![NuGet](https://img.shields.io/nuget/v/GmailMcp?logo=nuget)
![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)
![Model Context Protocol](https://img.shields.io/badge/MCP-Enabled-blue)

---

## Overview

Gmail MCP Server is a versatile .NET global tool that bridges your Gmail account with both human users and AI assistants. It provides two distinct modes of operation:

**CLI Mode** - Direct command-line interface for searching messages, reading emails, and downloading attachments using familiar terminal commands.

**MCP Mode** - Model Context Protocol server that enables AI assistants (like Claude) to interact with your Gmail account through a standardized protocol.

### What Problems It Solves

- **AI Email Integration**: Give AI assistants secure, controlled access to your Gmail without sharing passwords
- **Terminal Email Access**: Quickly search and read emails from the command line for scripting and automation
- **OAuth2 Security**: Browser-based authentication flow with local credential storage
- **Cross-Platform**: Works on Windows, macOS, and Linux with .NET runtime

---

## ✨ Features

- 🔄 **Dual-mode architecture** - Switch between CLI and MCP server modes seamlessly
- 🔐 **OAuth2 auto-authentication** - Secure browser-based authentication with automatic token refresh
- 📧 **Gmail operations** - Search messages with Gmail query syntax, read full email content, download attachments
- 🤖 **MCP integration** - Standardized protocol for AI assistants (Claude Desktop, Cline, etc.)
- 📦 **Easy installation** - Available as .NET global tool via NuGet
- 🔍 **Advanced search** - Full Gmail query syntax support (from:, subject:, is:unread, etc.)
- 💻 **Script-friendly** - JSON output mode for automation and integration
- 🌐 **Cross-platform** - Runs anywhere .NET 8.0 or 10.0 is supported

---

## 🚀 Quick Start

```bash
# Install the global tool
dotnet tool install -g GmailMcp

# Authenticate with your Gmail account
dotnet-gmail auth

# Search for messages (CLI mode)
dotnet-gmail search "from:example@gmail.com"

# Read a specific message
dotnet-gmail read <message-id>

# Start MCP server for AI assistants
dotnet-gmail mcp
```

---

## 📦 Installation

### Prerequisites

- **.NET 8.0 or 10.0 SDK** - [Download here](https://dotnet.microsoft.com/download)
- **Google Cloud Console account** - Required for OAuth2 credentials
- **Gmail account** - The account you want to access

### Step 1: Google Cloud Console Setup

1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Create a new project or select an existing one
3. Enable the **Gmail API** for your project
4. Navigate to **Credentials** > **Create Credentials** > **OAuth 2.0 Client ID**
5. Choose **Desktop app** as the application type
6. Download the credentials JSON file
7. Rename it to `gcp-oauth.keys.json`
8. Place it in `~/.gmail-mcp/` (create the directory if needed)

**File location:**
- Windows: `%USERPROFILE%\.gmail-mcp\gcp-oauth.keys.json`
- macOS/Linux: `~/.gmail-mcp/gcp-oauth.keys.json`

### Step 2: Install the Tool

```bash
dotnet tool install -g GmailMcp
```

### Step 3: Authenticate

```bash
dotnet-gmail auth
```

This will:
1. Open your default browser
2. Prompt you to sign in to Google
3. Request permission to access your Gmail
4. Save the OAuth2 tokens locally in `~/.gmail-mcp/token.json`

---

## 🔧 Usage

### CLI Mode

#### Authentication

```bash
# Authenticate and store credentials
dotnet-gmail auth
```

#### Search Messages

```bash
# Basic search
dotnet-gmail search "from:john@example.com"

# Search with subject
dotnet-gmail search "subject:invoice"

# Complex query
dotnet-gmail search "from:support@company.com is:unread after:2024/01/01"

# Limit results (default: 10)
dotnet-gmail search "from:boss@company.com" --max-results 5

# JSON output for scripting
dotnet-gmail search "is:unread" --robot
```

**Common search operators:**
- `from:user@example.com` - Emails from specific sender
- `to:user@example.com` - Emails to specific recipient
- `subject:keyword` - Subject contains keyword
- `is:unread` - Unread messages only
- `has:attachment` - Messages with attachments
- `after:2024/01/01` - Messages after date
- `before:2024/12/31` - Messages before date

#### Read Message

```bash
# Read message by ID (get ID from search results)
dotnet-gmail read 18d4f2a3b5c6e7f8

# JSON output
dotnet-gmail read 18d4f2a3b5c6e7f8 --robot
```

#### Download Attachment

```bash
# Download to current directory
dotnet-gmail download <message-id> <attachment-id>

# Download to specific directory
dotnet-gmail download <message-id> <attachment-id> --output ./downloads

# Custom filename
dotnet-gmail download <message-id> <attachment-id> --filename report.pdf
```

### MCP Mode

#### Quick MCP Setup

Add the following to your Claude Desktop configuration file:

**macOS**: `~/Library/Application Support/Claude/claude_desktop_config.json`
**Windows**: `%APPDATA%\Claude\claude_desktop_config.json`

```json
{
  "mcpServers": {
    "gmail": {
      "command": "dotnet-gmail",
      "args": ["mcp"]
    }
  }
}
```

Restart Claude Desktop after making changes.

For detailed MCP configuration instructions, troubleshooting, and advanced setup options, see the **[MCP Configuration Guide](docs/MCP-CONFIGURATION.md)**.

#### Available MCP Tools

Once configured, Claude can use these tools:

| Tool | Parameters | Description |
|------|------------|-------------|
| `search_messages` | `query`, `maxResults` | Search Gmail using query syntax, returns message metadata |
| `read_message` | `messageId` | Read full message content including body and attachments |
| `download_attachment` | `messageId`, `attachmentId`, `outputDirectory`, `filename` | Download specific attachment to disk |

**Example Claude prompts:**
- "Search my Gmail for unread messages from support@example.com"
- "Read the message with ID 18d4f2a3b5c6e7f8"
- "Download the PDF attachment from that email"
- "Find all emails from my boss in the last week with attachments"

---

## 🏗️ Architecture

```
┌─────────────────────────────────────────────────┐
│              Gmail MCP Server                   │
│                                                 │
│  ┌──────────────┐         ┌─────────────────┐  │
│  │   CLI Mode   │         │   MCP Mode      │  │
│  │              │         │                 │  │
│  │  • auth      │         │  • stdio server │  │
│  │  • search    │         │  • tool calls   │  │
│  │  • read      │         │  • JSON-RPC     │  │
│  │  • download  │         │                 │  │
│  └──────┬───────┘         └────────┬────────┘  │
│         │                          │           │
│         └──────────┬───────────────┘           │
│                    │                           │
│         ┌──────────▼──────────┐                │
│         │  Shared Services    │                │
│         │                     │                │
│         │  • AuthService      │                │
│         │  • GmailService     │                │
│         └──────────┬──────────┘                │
│                    │                           │
│         ┌──────────▼──────────┐                │
│         │    Gmail API v1     │                │
│         │  (Google.Apis)      │                │
│         └─────────────────────┘                │
└─────────────────────────────────────────────────┘
```

**Key Components:**
- **Program.cs** - Entry point with dependency injection setup
- **Commands** - CLI command implementations (auth, search, read, download)
- **Services** - Shared business logic (authentication, Gmail operations)
- **MCP Server** - Model Context Protocol server and tool definitions
- **Models** - Data structures for messages and attachments

---

## 🔐 Authentication & Security

### OAuth2 Flow

Gmail MCP Server uses OAuth2 for secure authentication:

1. **Client credentials** (`gcp-oauth.keys.json`) stored in `~/.gmail-mcp/`
2. **User consent** obtained through browser-based Google login
3. **Access tokens** stored locally in `~/.gmail-mcp/token.json`
4. **Automatic refresh** when tokens expire

### Security Best Practices

- **Never commit** `gcp-oauth.keys.json` or `token.json` to version control
- **Restrict OAuth scopes** to minimum required (currently uses `GmailReadonlyScope`)
- **Review granted permissions** in [Google Account settings](https://myaccount.google.com/permissions)
- **Rotate credentials** if you suspect compromise

### Setting Up Google Cloud Credentials

**Detailed Steps:**

1. Navigate to [Google Cloud Console](https://console.cloud.google.com/)
2. Create a new project:
   - Click "Select a project" > "New Project"
   - Name it (e.g., "Gmail MCP Access")
   - Click "Create"

3. Enable Gmail API:
   - Go to "APIs & Services" > "Library"
   - Search for "Gmail API"
   - Click "Enable"

4. Create OAuth 2.0 credentials:
   - Go to "APIs & Services" > "Credentials"
   - Click "+ CREATE CREDENTIALS" > "OAuth client ID"
   - If prompted, configure the OAuth consent screen:
     - User Type: "External"
     - App name: "Gmail MCP Server"
     - User support email: your email
     - Developer contact: your email
     - Save and continue through remaining steps
   - Application type: "Desktop app"
   - Name: "Gmail MCP Desktop"
   - Click "Create"

5. Download credentials:
   - Click the download icon (⬇) next to your newly created OAuth client
   - Save as `gcp-oauth.keys.json`
   - Move to `~/.gmail-mcp/gcp-oauth.keys.json`

6. First authentication:
   ```bash
   dotnet-gmail auth
   ```
   - Browser opens automatically
   - Sign in with your Google account
   - Grant requested permissions
   - Token saved to `~/.gmail-mcp/token.json`

---

## 🛠️ Development

### Clone & Build

```bash
# Clone repository
git clone https://github.com/yourusername/dotnet-gmail-mcp.git
cd dotnet-gmail-mcp

# Build solution
dotnet build

# Run locally
dotnet run --project src/GmailMcp
```

### Local Testing

#### Test CLI Commands

```bash
# Build and install locally
dotnet pack src/GmailMcp
dotnet tool install -g --add-source ./src/GmailMcp/nupkg GmailMcp

# Test authentication
dotnet-gmail auth

# Test search
dotnet-gmail search "is:unread" --max-results 5

# Test read
dotnet-gmail read <message-id>

# Uninstall when done
dotnet tool uninstall -g GmailMcp
```

#### Test MCP Integration

```bash
# Start MCP server in one terminal
dotnet-gmail mcp

# In another terminal, send MCP protocol messages via stdin
# (or configure Claude Desktop to use local build)
```

### Project Structure

```
dotnet-gmail-mcp/
├── src/
│   └── GmailMcp/
│       ├── Commands/           # CLI command implementations
│       │   ├── AuthCommand.cs
│       │   ├── SearchCommand.cs
│       │   ├── ReadCommand.cs
│       │   ├── DownloadCommand.cs
│       │   └── McpCommand.cs
│       ├── Services/           # Business logic layer
│       │   ├── IAuthenticationService.cs
│       │   ├── AuthenticationService.cs
│       │   ├── IGmailService.cs
│       │   └── GmailService.cs
│       ├── Models/             # Data structures
│       │   ├── GmailMessage.cs
│       │   └── GmailAttachment.cs
│       ├── Mcp/                # MCP server and tools
│       │   ├── GmailMcpServer.cs
│       │   └── Tools/
│       │       ├── SearchMessagesTool.cs
│       │       ├── ReadMessageTool.cs
│       │       └── DownloadAttachmentTool.cs
│       ├── Program.cs          # Entry point and DI setup
│       └── GmailMcp.csproj     # Project configuration
└── README.md
```

---

## 📋 MCP Tools Reference

### search_messages

Search Gmail messages using Gmail query syntax.

**Parameters:**
- `query` (string, required) - Gmail search query (e.g., "from:user@example.com", "subject:meeting", "is:unread")
- `maxResults` (int, optional) - Maximum number of results to return (default: 10, max: 100)

**Returns:**
JSON array of message objects with:
- `id` - Message ID
- `threadId` - Thread ID
- `subject` - Email subject
- `from` - Sender email address
- `to` - Array of recipient email addresses
- `date` - ISO 8601 formatted date
- `hasAttachments` - Boolean indicating if attachments exist
- `attachmentCount` - Number of attachments

**Example:**
```json
{
  "query": "from:boss@company.com is:unread",
  "maxResults": 5
}
```

### read_message

Read full content of a specific Gmail message.

**Parameters:**
- `messageId` (string, required) - Unique message identifier from search results

**Returns:**
JSON object with:
- `id` - Message ID
- `threadId` - Thread ID
- `subject` - Email subject
- `from` - Sender email address
- `to` - Array of recipient email addresses
- `date` - ISO 8601 formatted date
- `body` - Plain text email body
- `attachments` - Array of attachment objects with `id`, `filename`, `mimeType`, `size`

**Example:**
```json
{
  "messageId": "18d4f2a3b5c6e7f8"
}
```

### download_attachment

Download an attachment from a Gmail message to local disk.

**Parameters:**
- `messageId` (string, required) - Message containing the attachment
- `attachmentId` (string, required) - Attachment identifier from message
- `outputDirectory` (string, optional) - Directory to save file (default: current directory)
- `filename` (string, optional) - Custom filename (default: original filename)

**Returns:**
JSON object with:
- `filePath` - Full path to downloaded file
- `filename` - Name of the saved file
- `size` - File size in bytes

**Example:**
```json
{
  "messageId": "18d4f2a3b5c6e7f8",
  "attachmentId": "ANGjdJ8wK...",
  "outputDirectory": "./downloads",
  "filename": "report.pdf"
}
```

---

## 🤝 Contributing

Contributions are welcome! Please feel free to submit issues, fork the repository, and create pull requests.

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

---

## 📄 License

This project is licensed under the MIT License. See [LICENSE](LICENSE) file for details.

---

## 🔗 Related Projects

- [Model Context Protocol](https://github.com/modelcontextprotocol) - MCP specification and SDKs
- [Claude Desktop](https://claude.ai/download) - AI assistant with MCP support
- [Google APIs .NET](https://github.com/googleapis/google-api-dotnet-client) - Gmail API client library

---

**Built with ❤️ using .NET and Model Context Protocol**
