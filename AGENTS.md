# DMTQ-Tools — Game Table Manager (MAUI Blazor Hybrid)

A .NET 10 MAUI Blazor Hybrid app for managing game patch data tables (CSV import, logical table editing, song/pattern management, platform-aware export).

## Project

- **Stack:** .NET 10 + MAUI + Blazor Hybrid + FluentUI Blazor 4.14 + bUnit 2
- **Entry:** `DMTQ-Tools/MauiProgram.cs` → `App.xaml` → `MainPage.xaml` (hosts BlazorWebView)
- **Blazor root:** `DMTQ-Tools/Components/Routes.razor` — router scans MAUI assembly + RCL assembly

## Projects

| Project | Role |
|---|---|
| `DMTQ.Tools.Core/` | Models (Song, GameTable, PatchPackage…) + Services (import/export/edit/validation) |
| `DMTQ.Tools.Components/` | Razor Class Library — all `.razor` pages + layouts + `app.css` + FluentUI |
| `DMTQ-Tools/` | MAUI host — `MauiProgram.cs`, `App.xaml`, `MainPage.xaml`, thin DI registration |
| `DMTQ.Tools.Core.Tests/` | 100 MSTest unit tests (self-contained, no external data) |
| `DMTQ.Tools.UITests/` | 13 bUnit Blazor component tests (UI render verification) |

## Commands

```bash
# Build (Windows)
dotnet build DMTQ-Tools/DMTQ-Tools.csproj -f net10.0-windows10.0.19041.0

# Core unit tests (fast, no UI)
dotnet test DMTQ.Tools.Core.Tests/DMTQ.Tools.Core.Tests.csproj

# UI tests (bUnit Blazor component tests, no MAUI workload needed)
dotnet test DMTQ.Tools.UITests/DMTQ.Tools.UITests.csproj

# Full solution tests
dotnet test DMTQ-Tools.sln
```

## Manual UI Testing (CDP via DevFlow)

Debug build → launch app → connect Chrome DevTools Protocol → inspect → close.

```bash
# 1. Build
dotnet build DMTQ-Tools/DMTQ-Tools.csproj -f net10.0-windows10.0.19041.0

# 2. Launch app in background (Windows)
Start-Process -FilePath "DMTQ-Tools\bin\Debug\net10.0-windows10.0.19041.0\win-x64\DMTQ-Tools.exe" -WindowStyle Hidden

# 3. Wait for CDP (poll until WebView initializes)
maui devflow webview status --platform windows
# First call may return "Agent connected but CDP not ready" — retry after 2–3s.
# Once ready: "Connected: CDP ready (1 WebView)"

# 4. Inspect the running page
maui devflow webview source                                    # full page HTML
maui devflow webview Runtime evaluate "document.title"          # run JS
maui devflow webview Runtime evaluate "JSON.stringify(         # measure element widths
  [...document.querySelectorAll('.fluent-grid,form')].map(
    el => ({cls:el.className, w:el.offsetWidth})))"

# 5. Close app
powershell -Command "Stop-Process -Name 'DMTQ-Tools' -Force"
```

**Note:** CDP requires the DevFlow agent (`AddMauiDevFlowAgent()`) and Blazor bridge
(`AddMauiBlazorDevFlowTools()`) registered in `MauiProgram.cs` — both already present
in Debug builds.

## Architecture

```
Pages (@inject IProjectState + IProjectWorkflow)
    ↓
Workflow (GameTableManagerWorkflow : IProjectWorkflow)
    ↓
Core Services (SongEditService, SongCatalogService, PlatformPackageExporter…)
    ↓
CSV Models (GameTable, GameTableRow, GameTableCell) ← import/export boundary
```

- **Pages inject interfaces** (`IProjectState`, `IProjectWorkflow`) — never concrete MAUI types. This allows bUnit UI tests without MAUI workloads.
- **Song model is flat** — 22 string properties (Name, Genre, ArtistName…), no SourceFields dictionaries. SongPattern has 9 flat fields.
- **CSV import/export** uses `GameTable`/`GameTableCell` models. Domain models (Song/SongPattern) are projected from CSV at service layer.
- **Platform export** supports Full mode (all files written every time). Delta mode (skip unchanged) was removed — always rewrites manifest with computed checksums.
- **Platform import** validates decompressed MD5 against `patch_new.csv` `checksum` on every file. Mismatched files are logged to `IntegrityErrors` and skipped.
- **FluentUI Blazor 4.14** integrated in RCL's `_Imports.razor`.
- **Dialog pattern:** Pages use `IDialogService.ShowDialogAsync<TComponent, TData>(...)` with `IDialogContentComponent<TData>`. Built-in footer (PrimaryAction/SecondaryAction) replaces custom buttons.

## Conventions

- **TDD**: always write a failing test → implement → verify pass → commit.
- **Namespaces**: `DMTQ.Tools.Core.Models`, `DMTQ.Tools.Core.Services`, `DMTQ_Tools.Services`, `DMTQ_Tools.Components`.
- **Pages** use `@bind` on flat properties, never `SourceFields`/`Dictionary<string,string>` for field storage.
- **Tests** use MSTest + FluentAssertions (`.Should()`). bUnit tests extend `BlazorUITestBase`.
- **Commits**: small, single-purpose, descriptive.
- **No new MAUI workload** requirements for running tests (RCL isolates UI from MAUI SDK).

## Notes

- `IProjectFilePicker` and `IFolderPicker` are Core interfaces for file/folder selection — MAUI implementations in `DMTQ-Tools/Services/`, faked in bUnit tests.
- Entity models (`Song`, `Achievement`, `Quest`, `Item`) use backing fields with `StringComparer.OrdinalIgnoreCase` setters so JSON deserialization preserves case-insensitive dictionary lookups.
- `patch_table_builder/` is the legacy CSV tooling (separate project, not part of this solution).
- `external/` may contain sample patch packages for manual testing — tests are self-contained and do not depend on this directory.
