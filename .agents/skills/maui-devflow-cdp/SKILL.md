---
name: maui-devflow-cdp
description: >-
  Inspect, debug, and automate a running .NET MAUI Blazor Hybrid app via the
  maui devflow CLI (Chrome DevTools Protocol bridge + native MAUI Agent API).
  Use when testing or debugging a MAUI Blazor Hybrid app on Windows, inspecting
  the Blazor WebView DOM, finding elements and their bounding boxes, clicking
  or filling forms in the WebView, checking visual tree, taking screenshots,
  or reading app logs — all from the terminal without needing Playwright.
allowed-tools: Bash(maui:*)
---

# MAUI DevFlow CDP — Blazor Hybrid Debugging

## Quick start

```bash
# 1. Check the app is running (Windows requires --platform)
maui devflow list --platform windows

# 2. See what page is currently showing
maui devflow webview source

# 3. Read h1 or any element text
maui devflow webview Runtime evaluate "document.querySelector('h1')?.textContent"

# 4. Navigate to another Blazor page
maui devflow webview Runtime evaluate "location.assign('/tables')"

# 5. Find all links on the page
maui devflow webview Runtime evaluate \
  "JSON.stringify([...document.querySelectorAll('a')].map(a=>({text:a.textContent.trim(),href:a.getAttribute('href')})))"
```

## Architecture

Two layers — learn which tool to reach for:

| Layer | CLI prefix | Sees |
|---|---|---|
| **Native MAUI** | `maui devflow ui *` | Window, MainPage, BlazorWebView (one big rectangle) |
| **Blazor WebView** | `maui devflow webview *` | HTML elements, CSS selectors, JS runtime |

**Rule of thumb:** everything inside the Blazor page is `webview` territory. Window size, native alerts, screenshot file output are `ui` territory.

## Core operations

### Page inspection (no screenshot needed)

```bash
# Full HTML source
maui devflow webview source

# Single element HTML
maui devflow webview DOM getOuterHTML "fluent-card"

# Run arbitrary JS — preferred for everything
maui devflow webview Runtime evaluate "document.title"
maui devflow webview Runtime evaluate "JSON.stringify(document.querySelector('h1').textContent)"

# Find elements by CSS selector
maui devflow webview DOM querySelectorAll ".fluent-nav-link"

# Check CDP connection
maui devflow webview status
```

### Get element position & size

```bash
# Bounding box of any element (returns {x,y,width,height})
maui devflow webview Runtime evaluate \
  "JSON.stringify(document.querySelector('fluent-card').getBoundingClientRect())"

# Window / WebView dimensions (native layer)
maui devflow ui query --selector BlazorWebView
# → returns bounds: {x:0, y:0, width:1392, height:727}
```

### Navigation

```bash
# Blazor client-side navigation (fast, no page reload)
maui devflow webview Runtime evaluate "location.assign('/songs')"

# Read current URL
maui devflow webview Runtime evaluate "location.href"

# Reload current page (triggers Blazor re-render)
maui devflow webview Page reload

# Shell route navigation (native MAUI)
maui devflow ui navigate "//blazor"
```

### Click & interact

```bash
# Click by CSS selector inside WebView
maui devflow webview Runtime evaluate \
  "document.querySelector('a[href=\"tables\"]').click()"

# Fill a form field
maui devflow webview Runtime evaluate \
  "(()=>{const e=document.querySelector('fluent-text-field');e.value='hello';e.dispatchEvent(new Event('input',{bubbles:true}));return e.value})()"

# Tap native MAUI element (by element ID from ui query)
maui devflow ui tap 5dd50f34a93f      # tap the BlazorWebView itself

# Native UI interaction (buttons outside the WebView)
maui devflow ui query                # find elements
maui devflow ui tap --text "Create"  # tap by visible text
```

### Verification / assertion

```bash
# Check page h1
maui devflow webview Runtime evaluate "document.querySelector('h1')?.textContent"

# Count elements
maui devflow webview Runtime evaluate "document.querySelectorAll('fluent-card').length"

# Check element visibility
maui devflow webview Runtime evaluate \
  "document.querySelector('fluent-button[appearance=\"accent\"]')?.hasAttribute('disabled')"

# Native element property
maui devflow ui property 5dd50f34a93f Bounds
```

### App state & logs

```bash
# Device / app info
maui devflow device app-info
maui devflow device device-info
maui devflow device display

# App logs (--follow for real-time)
maui devflow logs --limit 20
maui devflow logs --source webview --follow

# Screenshot (use on multimodal models)
maui devflow ui screenshot -o screen.png
maui devflow webview Page captureScreenshot
```

## Common workflows

### "What page is showing and what can I click?"

```bash
maui devflow webview Runtime evaluate "document.querySelector('h1')?.textContent"
maui devflow webview Runtime evaluate \
  "JSON.stringify([...document.querySelectorAll('a')].map(a=>({text:a.textContent.trim(),href:a.getAttribute('href')})))"
```

### "Navigate to X and verify it worked"

```bash
maui devflow webview Runtime evaluate "location.assign('/tables')"
# wait ~2s for Blazor to render
maui devflow webview Runtime evaluate "document.querySelector('h1')?.textContent === 'Tables'"
```

### "Find an element's position for hit-testing"

```bash
maui devflow webview Runtime evaluate \
  "JSON.stringify(document.querySelector('.fluent-nav-link[href=\"tables\"]')?.getBoundingClientRect())"
```

## Gotchas

| Symptom | Cause | Fix |
|---|---|---|
| `maui devflow list` times out | Windows needs explicit platform | Add `--platform windows` |
| `webview snapshot` → `Error: Uncaught` | Chobitsu DOM snapshot broken in preview.11 | Use `source` or `Runtime evaluate` instead |
| `Input dispatchClickEvent` → `Error: Uncaught` | CDP Input domain unreliable | Use `Runtime evaluate "...click()"` instead |
| `Page navigate` changes URL but Blazor doesn't render | Blazor intercepts client-side routing | Use `location.assign('/route')` in `Runtime evaluate` |
| `ui tree` / `ui query` doesn't show HTML elements | UI tree only sees native MAUI controls | Use `webview` commands for Blazor content |
| `Runtime evaluate` times out after `location.assign` | CDP waits for page load that never comes (SPA) | Fire-and-forget: `location.assign` returns before Blazor re-renders; poll `h1` separately |

## Reference

All commands have `--help`. See [REFERENCE.md](REFERENCE.md) for the full command list.
