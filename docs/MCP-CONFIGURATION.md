# MCP Configuration Guide

## Overview

### What is MCP Mode?

The Model Context Protocol (MCP) is a standardized protocol that allows AI assistants like Claude to interact with external tools and services. When `dotnet-gmail` runs in MCP mode, it acts as a server that exposes Gmail functionality through a well-defined interface that AI assistants can use to help you manage your email.

### MCP vs CLI Mode

**MCP Mode:**
- Runs as a background server
- Integrates directly with AI assistants (Claude Desktop, etc.)
- Enables conversational Gmail management
- Tools are invoked automatically by the AI
- Best for interactive, AI-assisted workflows

**CLI Mode:**
- Command-line interface for direct tool usage
- Manual command execution
- Scriptable and automatable
- Best for automation and scripting scenarios

### Benefits of MCP Integration

- **Natural Language Interface:** Ask Claude to search, read, or manage emails using natural language
- **Contextual Awareness:** Claude can remember conversation context and follow-up questions
- **Multi-Step Operations:** Chain multiple Gmail operations together seamlessly
- **Rich Formatting:** Claude can present email content in readable, formatted output
- **Interactive Refinement:** Refine searches and operations through conversation

---

## Claude Desktop Configuration

### Step 1: Locate Configuration File

The Claude Desktop configuration file location varies by operating system:

**Windows:**
```
%APPDATA%\Claude\claude_desktop_config.json
```
Full path typically: `C:\Users\YourUsername\AppData\Roaming\Claude\claude_desktop_config.json`

**macOS:**
```
~/Library/Application Support/Claude/claude_desktop_config.json
```

**Linux:**
```
~/.config/Claude/claude_desktop_config.json
```

### Step 2: Add Gmail MCP Server Configuration

Open the `claude_desktop_config.json` file in a text editor and add the following configuration:

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

If you already have other MCP servers configured, add the `gmail` entry to the existing `mcpServers` object:

```json
{
  "mcpServers": {
    "existing-server": {
      "command": "some-command",
      "args": ["arg1"]
    },
    "gmail": {
      "command": "dotnet-gmail",
      "args": ["mcp"]
    }
  }
}
```

### Step 3: Ensure dotnet-gmail is in PATH

The `dotnet-gmail` command must be accessible from your system PATH. You can verify this by running:

**Windows (PowerShell):**
```powershell
Get-Command dotnet-gmail
```

**macOS/Linux:**
```bash
which dotnet-gmail
```

If the command is not found, install `dotnet-gmail` as a global tool:

```bash
dotnet tool install --global dotnet-gmail
```

Or ensure the .NET tools directory is in your PATH:
- Windows: `%USERPROFILE%\.dotnet\tools`
- macOS/Linux: `~/.dotnet/tools`

### Step 4: Restart Claude Desktop

After saving the configuration file, completely quit and restart Claude Desktop for the changes to take effect.

### Step 5: Verify Connection

Once Claude Desktop restarts, you can verify the Gmail MCP server is connected by asking Claude:

```
Can you check if the Gmail tools are available?
```

Claude should confirm it has access to Gmail tools like `search_messages`, `read_message`, and `download_attachment`.

---

## Visual Studio Code Configuration

Visual Studio Code with GitHub Copilot can automatically discover and configure MCP servers published to NuGet with the required `server.json` metadata.

### Prerequisites

- [Visual Studio Code](https://code.visualstudio.com/)
- [GitHub Copilot extension](https://marketplace.visualstudio.com/items?itemName=GitHub.copilot)
- [.NET 10 SDK or higher](https://dotnet.microsoft.com/download/dotnet) (required for `dnx` command)

### Automatic Configuration from NuGet

1. **Search for the package on NuGet.org:**

   Visit [https://www.nuget.org/packages/GmailMcp](https://www.nuget.org/packages/GmailMcp)

2. **Copy the MCP Server configuration:**

   On the package page, click the **"MCP Server"** tab. You'll see automatically generated configuration JSON like this:

   ```json
   {
     "inputs": [],
     "servers": {
       "GmailMcp": {
         "type": "stdio",
         "command": "dnx",
         "args": [
           "GmailMcp@0.0.5",
           "--yes"
         ]
       }
     }
   }
   ```

   The `dnx` command (DotNet eXecute) is a .NET global tool runner that automatically downloads and runs .NET tools from NuGet.

3. **Add to VS Code configuration:**

   You have two options for configuration scope:

   **Option A: Workspace-specific (recommended for project-based work)**

   - Create a `.vscode` folder in your workspace root (if it doesn't exist)
   - Create or edit `.vscode/mcp.json`
   - Paste the copied JSON configuration

   **Option B: Global (available across all workspaces)**

   - Open VS Code Command Palette (Ctrl+Shift+P or Cmd+Shift+P)
   - Type "MCP: Add Server" and select it
   - Choose "Global" as the configuration target
   - Select "stdio" as the server type
   - Enter command: `dnx`
   - Enter args: `GmailMcp@0.0.5 --yes`
   - Enter server ID: `GmailMcp`

4. **Verify the configuration:**

   Your `.vscode/mcp.json` should look like this:

   ```json
   {
     "inputs": [],
     "servers": {
       "GmailMcp": {
         "type": "stdio",
         "command": "dnx",
         "args": [
           "GmailMcp@0.0.5",
           "--yes"
         ]
       }
     }
   }
   ```

5. **Reload VS Code:**

   After saving the configuration, reload the VS Code window:
   - Open Command Palette (Ctrl+Shift+P or Cmd+Shift+P)
   - Type "Developer: Reload Window" and select it

6. **Verify the MCP server is loaded:**

   - Open GitHub Copilot Chat
   - Switch to **Agent** mode (click the mode selector at the top)
   - Click the **"Select tools"** icon (wrench icon)
   - You should see "GmailMcp" listed with its available tools:
     - `search_messages`
     - `read_message`
     - `download_attachment`

### Manual Configuration (Advanced)

If you prefer to configure manually without using `dnx`, you can point directly to a locally installed tool:

```json
{
  "servers": {
    "GmailMcp": {
      "type": "stdio",
      "command": "dotnet-gmail",
      "args": ["mcp"]
    }
  }
}
```

**Requirements for manual configuration:**
- The `dotnet-gmail` tool must be installed globally: `dotnet tool install -g GmailMcp`
- The .NET tools directory must be in your PATH

### Testing the Integration

Once configured, test the MCP server with GitHub Copilot:

1. Open GitHub Copilot Chat in VS Code
2. Switch to **Agent** mode
3. Try these example prompts:

```
Search my Gmail for unread messages from my boss
```

```
Show me the latest email with "invoice" in the subject
```

```
Read the message with ID 18d4f2a3b5c6e7f8
```

GitHub Copilot will request permission to run the Gmail tools. You can:
- **Continue** - Allow for this prompt only
- **Current session** - Always allow in the current session
- **Current workspace** - Always allow for the current workspace
- **Always allow** - Always allow without asking

### Troubleshooting VS Code

**Error: "The command 'dnx' needed to run GmailMcp was not found"**

Solution: Install .NET 10 SDK or higher, which includes the `dnx` command.
- Download from: https://dotnet.microsoft.com/download/dotnet/10.0
- After installation, restart VS Code

**Error: "Server failed to start"**

Solutions:
1. Check that you've authenticated: Run `dotnet-gmail auth` from terminal
2. Verify credentials exist in `~/.gmail-mcp/`
3. Check VS Code Output panel (View → Output → GitHub Copilot Chat) for detailed errors

**MCP server not appearing in tools list**

Solutions:
1. Verify `mcp.json` is in the correct location (`.vscode/mcp.json` for workspace)
2. Reload VS Code window (Command Palette → "Developer: Reload Window")
3. Check that the `mcp.json` syntax is valid JSON (no trailing commas, proper quotes)

**GitHub Copilot doesn't use the tools**

Solutions:
1. Verify you're in **Agent** mode (not regular Chat mode)
2. Check that the tools are enabled in the "Select tools" menu
3. Explicitly reference the tool by name: `Using #search_messages, find my unread emails`

---

## Available MCP Tools

### search_messages

Search for Gmail messages using Gmail's query syntax.

**Parameters:**
- `query` (string, required): Gmail search query
- `maxResults` (integer, optional): Maximum number of results to return (default: 10, max: 100)

**Description:**

Searches your Gmail mailbox using the same query syntax available in the Gmail web interface. Returns a list of messages matching the query with key metadata.

**Gmail Query Syntax Examples:**

- `from:john@example.com` - Messages from a specific sender
- `to:alice@example.com` - Messages to a specific recipient
- `subject:invoice` - Messages with "invoice" in the subject
- `has:attachment` - Messages with attachments
- `is:unread` - Unread messages
- `is:starred` - Starred messages
- `after:2024/01/01` - Messages after a specific date
- `before:2024/02/01` - Messages before a specific date
- `newer_than:7d` - Messages from the last 7 days
- `older_than:1y` - Messages older than 1 year
- `from:john@example.com has:attachment` - Combine multiple criteria

**Sample Interaction:**

```
User: "Search my Gmail for unread messages from the last 3 days"

Claude: [Uses search_messages with query "is:unread newer_than:3d"]

Found 5 unread messages from the last 3 days:

1. From: notifications@github.com
   Subject: [Repository] New pull request
   Date: Feb 10, 2024
   ID: 18d4a2b1c3e5f678

2. From: team@company.com
   Subject: Weekly team sync notes
   Date: Feb 11, 2024
   ID: 19e5b3c2d4f6a789

[...]
```

### read_message

Retrieve the full content of a specific Gmail message.

**Parameters:**
- `messageId` (string, required): The ID of the message to read

**Description:**

Fetches and displays the complete content of a Gmail message, including headers, body text, and metadata about attachments. The message ID can be obtained from search results or previous interactions.

**Returned Data:**
- Message ID
- Thread ID
- Subject
- From (sender name and email)
- To (recipient name and email)
- CC/BCC (if present)
- Date sent
- Full message body (plain text and/or HTML)
- Attachment metadata (filename, size, MIME type, attachment ID)
- Labels/Categories
- Snippet (preview text)

**Sample Interaction:**

```
User: "Read the message with ID 18d4a2b1c3e5f678"

Claude: [Uses read_message with messageId "18d4a2b1c3e5f678"]

Message Details:
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

From: notifications@github.com
To: you@example.com
Subject: [Repository] New pull request
Date: February 10, 2024 at 3:45 PM

A new pull request has been opened:

Pull Request #42: Add user authentication feature
Author: @contributor
Repository: your-org/your-repo

Description:
This PR adds JWT-based authentication to the API endpoints...

[View on GitHub](https://github.com/...)

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Attachments: None
Labels: INBOX, UNREAD
```

### download_attachment

Download an attachment from a Gmail message to your local filesystem.

**Parameters:**
- `messageId` (string, required): The ID of the message containing the attachment
- `attachmentId` (string, required): The ID of the specific attachment
- `savePath` (string, optional): Directory where the file should be saved (defaults to current directory)
- `filename` (string, optional): Custom filename for the downloaded file (defaults to original filename)

**Description:**

Downloads a specific attachment from a Gmail message. The attachment ID can be obtained from the `read_message` tool output. If no custom filename is provided, the original filename from the email is used.

**Sample Interaction:**

```
User: "Download the invoice attachment from that last message"

Claude: [Uses download_attachment with messageId and attachmentId from previous context]

Successfully downloaded attachment:
- Original filename: Invoice_Feb2024.pdf
- Saved to: C:\Users\YourName\Downloads\Invoice_Feb2024.pdf
- File size: 245 KB

The invoice PDF has been saved to your Downloads folder.
```

---

## Example Interactions

### Example 1: Finding Recent Emails from a Specific Person

```
User: "Find all emails from sarah@example.com in the last week"

Claude: [Uses search_messages with query "from:sarah@example.com newer_than:7d"]

I found 3 messages from sarah@example.com in the last week:

1. Subject: "Q1 Budget Review"
   Date: February 8, 2024
   Preview: "Hi team, please review the attached budget..."
   ID: 18a1b2c3d4e5f678

2. Subject: "Team lunch tomorrow?"
   Date: February 10, 2024
   Preview: "Hey! Are we still on for lunch at..."
   ID: 19b2c3d4e5f6a789

3. Subject: "Re: Project timeline"
   Date: February 11, 2024
   Preview: "Thanks for the update. I think we should..."
   ID: 20c3d4e5f6a7b890

Would you like me to read any of these messages in full?
```

### Example 2: Reading a Specific Email

```
User: "Yes, show me the budget review email"

Claude: [Uses read_message with messageId "18a1b2c3d4e5f678"]

Here's the full email:

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

From: Sarah Johnson <sarah@example.com>
To: Team <team@company.com>
Subject: Q1 Budget Review
Date: February 8, 2024 at 9:30 AM

Hi team,

Please review the attached budget spreadsheet for Q1. I've highlighted
the areas where we're tracking above/below our projections.

Key points:
- Marketing spend is 15% under budget
- Engineering costs are slightly over due to new hires
- Overall we're within 2% of target

Let's discuss in Friday's meeting.

Best,
Sarah

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Attachments:
1. Q1_Budget_2024.xlsx (156 KB)
   Attachment ID: ANGjdJ8...

Would you like me to download the spreadsheet?
```

### Example 3: Finding and Downloading Attachments

```
User: "Find all emails with PDF attachments from last month"

Claude: [Uses search_messages with query "has:attachment filename:pdf newer_than:1m"]

I found 8 messages with PDF attachments from the last month. Here are the most recent:

1. From: invoices@vendor.com
   Subject: "Invoice #12345"
   Date: February 5, 2024
   Attachment: Invoice_12345.pdf (89 KB)

2. From: hr@company.com
   Subject: "Benefits enrollment form"
   Date: January 28, 2024
   Attachment: Benefits_2024.pdf (234 KB)

3. From: reports@analytics.com
   Subject: "Monthly analytics report"
   Date: January 25, 2024
   Attachment: Analytics_Jan2024.pdf (1.2 MB)

Which attachments would you like to download?

User: "Download the invoice"

Claude: [Uses download_attachment for the invoice]

Downloaded successfully:
- File: Invoice_12345.pdf
- Location: C:\Users\YourName\Downloads\Invoice_12345.pdf
- Size: 89 KB

The invoice PDF is now in your Downloads folder.
```

### Example 4: Complex Search Queries

```
User: "Show me starred emails about the 'alpha project' that have attachments"

Claude: [Uses search_messages with query "is:starred alpha project has:attachment"]

I found 4 starred messages about the alpha project with attachments:

1. From: project-lead@company.com
   Subject: "Alpha Project - Final Specifications"
   Date: January 15, 2024
   Attachments: specs.pdf, diagrams.zip

2. From: design@company.com
   Subject: "Alpha UI mockups ready for review"
   Date: January 22, 2024
   Attachments: mockups_v3.fig

[...]

Would you like to read any of these messages or download their attachments?
```

---

## Troubleshooting

### Server Not Starting

**Problem:** Claude Desktop shows the Gmail MCP server as disconnected or failed to start.

**Solutions:**

1. **Check Authentication:**
   - Ensure you've completed the initial authentication flow
   - Run `dotnet-gmail auth status` to verify authentication
   - Re-authenticate if needed: `dotnet-gmail auth login`

2. **Verify Installation:**
   - Check that `dotnet-gmail` is installed: `dotnet tool list --global`
   - Reinstall if necessary: `dotnet tool update --global dotnet-gmail`

3. **Check .NET Runtime:**
   - Ensure .NET 8.0 or later is installed: `dotnet --version`
   - Download from https://dotnet.microsoft.com if needed

4. **Review Logs:**
   - Check Claude Desktop logs for error messages
   - Look for stderr output from the MCP server

### No Tools Available

**Problem:** Claude says it doesn't have access to Gmail tools.

**Solutions:**

1. **Verify Configuration Syntax:**
   - Open `claude_desktop_config.json` and validate JSON syntax
   - Use a JSON validator (https://jsonlint.com) if needed
   - Ensure proper comma placement between entries

2. **Check Command Path:**
   - Verify `dotnet-gmail` is in PATH
   - Try using absolute path in config:
     ```json
     {
       "mcpServers": {
         "gmail": {
           "command": "C:\\Users\\YourName\\.dotnet\\tools\\dotnet-gmail.exe",
           "args": ["mcp"]
         }
       }
     }
     ```

3. **Restart Required:**
   - Completely quit Claude Desktop (not just close window)
   - Check Task Manager/Activity Monitor to ensure process is terminated
   - Restart Claude Desktop

### Permission Errors

**Problem:** Error messages about insufficient permissions or OAuth scopes.

**Solutions:**

1. **Re-authenticate with Correct Scopes:**
   ```bash
   dotnet-gmail auth login
   ```
   This will request the necessary Gmail API scopes

2. **Check Google Account Permissions:**
   - Visit https://myaccount.google.com/permissions
   - Verify the dotnet-gmail application has access
   - Remove and re-authorize if needed

3. **Verify OAuth Credentials:**
   - Ensure OAuth credentials are properly configured
   - Check for expired tokens or revoked access

### Common Error Messages

**Error: "Failed to retrieve credentials"**
- Solution: Run `dotnet-gmail auth login` to authenticate

**Error: "Rate limit exceeded"**
- Solution: Gmail API has rate limits. Wait a few minutes and try again
- For persistent issues, check your Google Cloud Console quota settings

**Error: "Message not found"**
- Solution: The message ID may be invalid or the message was deleted
- Verify the message ID from a fresh search

**Error: "Attachment not found"**
- Solution: The attachment ID may be invalid
- Use `read_message` first to get valid attachment IDs

**Error: "Invalid query syntax"**
- Solution: Check Gmail query syntax documentation
- Simplify the query and test incrementally

**Error: "Unable to write file"**
- Solution: Check that the save path exists and is writable
- Verify disk space is available

---

## Environment Variables

### Optional Configuration

The following environment variables can be used to customize `dotnet-gmail` behavior:

**GMAIL_CREDENTIALS_PATH**
- Custom path for OAuth credentials file
- Default: `~/.dotnet-gmail/credentials.json`
- Example: `GMAIL_CREDENTIALS_PATH=/custom/path/credentials.json`

**GMAIL_TOKEN_PATH**
- Custom path for OAuth token storage
- Default: `~/.dotnet-gmail/token.json`
- Example: `GMAIL_TOKEN_PATH=/custom/path/token.json`

**GMAIL_APPLICATION_NAME**
- Custom application name for API requests
- Default: `dotnet-gmail`
- Example: `GMAIL_APPLICATION_NAME=MyCustomApp`

### Setting Environment Variables for MCP

To use environment variables with Claude Desktop MCP configuration:

**Windows (claude_desktop_config.json):**
```json
{
  "mcpServers": {
    "gmail": {
      "command": "dotnet-gmail",
      "args": ["mcp"],
      "env": {
        "GMAIL_CREDENTIALS_PATH": "C:\\custom\\path\\credentials.json"
      }
    }
  }
}
```

**macOS/Linux (claude_desktop_config.json):**
```json
{
  "mcpServers": {
    "gmail": {
      "command": "dotnet-gmail",
      "args": ["mcp"],
      "env": {
        "GMAIL_CREDENTIALS_PATH": "/custom/path/credentials.json"
      }
    }
  }
}
```

---

## Additional Resources

- **Gmail Search Operators:** https://support.google.com/mail/answer/7190
- **Model Context Protocol Specification:** https://modelcontextprotocol.io
- **Claude Desktop Documentation:** https://claude.ai/desktop
- **dotnet-gmail GitHub Repository:** [Your repository URL]

---

## Need Help?

If you encounter issues not covered in this guide:

1. Check the [GitHub Issues](your-repo-url/issues) for similar problems
2. Review Gmail API quota limits in Google Cloud Console
3. Verify your .NET and dotnet-gmail versions are up to date
4. Create a new issue with detailed error messages and steps to reproduce

Happy emailing with Claude!
