# DMTQ-Tools — Game Table Manager (MAUI Blazor Hybrid)

A .NET 10 MAUI Blazor Hybrid app for managing game patch data tables (CSV import, logical table editing, song/pattern management, platform-aware export).

## Project

- **Stack:** .NET 10 + MAUI + Blazor Hybrid + FluentUI Blazor 4.11 + bUnit 2
- **Entry:** `DMTQ-Tools/MauiProgram.cs` → `App.xaml` → `MainPage.xaml` (hosts BlazorWebView)
- **Blazor root:** `DMTQ-Tools/Components/Routes.razor` — router scans MAUI assembly + RCL assembly

## Projects

| Project | Role |
|---|---|
| `DMTQ.Tools.Core/` | Models (Song, GameTable, PatchPackage…) + Services (import/export/edit/validation) |
| `DMTQ.Tools.Components/` | Razor Class Library — all `.razor` pages + layouts + `app.css` + FluentUI |
| `DMTQ-Tools/` | MAUI host — `MauiProgram.cs`, `App.xaml`, `MainPage.xaml`, thin DI registration |
| `DMTQ.Tools.Core.Tests/` | 65 MSTest unit tests (service layer) |
| `DMTQ.Tools.UITests/` | 10 bUnit Blazor component tests (UI render verification) |

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
- **Platform export** supports Delta (skip unchanged) and Full modes, MD5 checksums.
- **FluentUI Blazor 4.11** integrated in RCL's `_Imports.razor`.

## Conventions

- **TDD**: always write a failing test → implement → verify pass → commit.
- **Namespaces**: `DMTQ.Tools.Core.Models`, `DMTQ.Tools.Core.Services`, `DMTQ_Tools.Services`, `DMTQ_Tools.Components`.
- **Pages** use `@bind` on flat properties, never `SourceFields`/`Dictionary<string,string>` for field storage.
- **Tests** use MSTest + FluentAssertions (`.Should()`). bUnit tests extend `BlazorUITestBase`.
- **Commits**: small, single-purpose, descriptive.
- **No new MAUI workload** requirements for running tests (RCL isolates UI from MAUI SDK).

## Notes

- FolderPicker (`CommunityToolkit.Maui.Storage`) was removed from RCL pages — can be re-added via an injected abstraction.
- `patch_table_builder/` is the legacy CSV tooling (separate project, not part of this solution).
- `external/` may contain sample patch packages for manual testing.
