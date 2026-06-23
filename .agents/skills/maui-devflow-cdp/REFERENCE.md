# maui devflow — Full Command Reference

> Generated from `maui devflow --help` + subcommand exploration.  
> Prerequisite: app must have `Microsoft.Maui.DevFlow.Agent` + `Microsoft.Maui.DevFlow.Blazor` NuGet packages registered in `MauiProgram.cs`.

## Global options (apply to all commands)

| Flag | Description |
|---|---|
| `-ap, --agent-port` | Agent HTTP port (auto: broker → .mauidevflow → 9223) |
| `-ah, --agent-host` | Agent HTTP host |
| `--device` | Device id for ADB forwarding (Android) |
| `-p, --platform` | `maccatalyst`, `android`, `ios`, `windows` |
| `--json` | Machine-readable JSON output |
| `--no-json` | Force human-readable even when piped |
| `-v, --verbose` | Verbose output |
| `--ci` | CI mode — non-interactive, fail on first error |

## `maui devflow webview` — Blazor CDP

```bash
# Top-level
maui devflow webview status          # CDP connection status
maui devflow webview webviews        # List available WebViews
maui devflow webview source          # Full HTML source
maui devflow webview snapshot        # Simplified DOM with refs (⚠️ broken in preview.11)

# DOM domain
maui devflow webview DOM getDocument                    # Document root node
maui devflow webview DOM getOuterHTML <selector>        # Element HTML
maui devflow webview DOM querySelector <selector>       # First match
maui devflow webview DOM querySelectorAll <selector>    # All matches

# Input domain
maui devflow webview Input dispatchClickEvent <selector>  # ⚠️ unreliable
maui devflow webview Input fill <selector> <text>         # Fill form field
maui devflow webview Input insertText <text>              # Insert at cursor

# Page domain
maui devflow webview Page navigate <url>      # Navigate URL
maui devflow webview Page reload              # Reload page
maui devflow webview Page captureScreenshot   # Base64 screenshot

# Runtime domain
maui devflow webview Runtime evaluate <js>    # Run JavaScript — MOST RELIABLE

# Browser domain
maui devflow webview Browser getVersion       # Browser version info
```

## `maui devflow ui` — Native MAUI

```bash
maui devflow ui tree [--depth N] [--fields id,type,text,bounds]    # Visual tree
maui devflow ui query [--type X] [--automationId X] [--text X]     # Find elements
maui devflow ui query --selector "Button:visible"                   # CSS selector
maui devflow ui query --format compact                              # id,type,text,bounds only
maui devflow ui query --wait-until exists --timeout 5               # Wait for element
maui devflow ui element <id>                                        # Full element details
maui devflow ui property <id> <prop>                                # Single property
maui devflow ui set-property <id> <prop> <value>                    # Live edit
maui devflow ui tap <id>                                            # Tap by element ID
maui devflow ui tap --automationId X                                # Tap by AutomationId
maui devflow ui tap --text "Submit"                                 # Tap by text
maui devflow ui tap ... --and-screenshot after.png                  # Screenshot after
maui devflow ui fill <id> <text>                                    # Fill text
maui devflow ui clear <id>                                          # Clear text
maui devflow ui scroll --dx N --dy N                                # Scroll by delta
maui devflow ui scroll --element <id>                               # Scroll into view
maui devflow ui scroll --item-index N                               # CollectionView item
maui devflow ui hit-test <x> <y>                                    # Elements at point
maui devflow ui navigate <route>                                    # Shell route
maui devflow ui resize <w> <h>                                      # Window size
maui devflow ui screenshot [-o path.png]                            # Screenshot
maui devflow ui assert <prop> <expected> --id <id>                  # Assertion
maui devflow ui assert <prop> <expected> --automationId X           # Assert by ID
maui devflow ui alert                                               # Detect/dismiss alerts
maui devflow ui status                                               # Agent connection
```

## `maui devflow device`

```bash
maui devflow device app-info         # Name, version, package
maui devflow device device-info      # Manufacturer, model, OS
maui devflow device display          # Density, size, orientation
maui devflow device battery          # Level, state, power source
maui devflow device connectivity     # Network access, profiles
maui devflow device geolocation      # GPS coordinates
maui devflow device permissions      # Permission statuses
maui devflow device sensors          # Monitor sensors
maui devflow device version-tracking # Version history
```

## `maui devflow logs`

```bash
maui devflow logs --limit 50                    # Recent entries
maui devflow logs --source webview              # WebView logs only
maui devflow logs --follow                      # Real-time stream
maui devflow logs --follow --replay 20          # Stream + replay last 20
```

## `maui devflow network`

```bash
maui devflow network list                       # Recent requests
maui devflow network list --limit 20            # Last 20
maui devflow network list --host api.example    # Filter by host
maui devflow network list --method POST         # Filter by method
maui devflow network detail <id>                # Full request/response
maui devflow network clear                      # Clear buffer
```

## `maui devflow storage`

```bash
maui devflow storage roots                       # List storage roots
maui devflow storage preferences                 # Manage key-value prefs
maui devflow storage secure-storage              # Encrypted store
maui devflow storage files                       # Sandboxed file ops
```

## Agent management

```bash
maui devflow list                                # All connected agents
maui devflow list --platform windows             # Windows (required on Win)
maui devflow agent status                        # Selected agent health
maui devflow agent wait                          # Wait for agent
maui devflow diagnose                            # Full health check
maui devflow init                                # Init workspace skills
maui devflow mcp                                 # Start MCP server for AI
maui devflow batch                               # JSONL stdin → JSONL stdout
maui devflow commands                             # Machine-readable schema
```
