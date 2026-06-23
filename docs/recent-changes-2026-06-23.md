# 近期变更摘要 (2026-06-23)

## 资源模型重构

**旧结构**（已删除）:
- `PatchPackage.Manifest` — `PatchFileEntry` 列表
- `PatchPackage.Platforms` — `PlatformPackageRecord` 列表（含 `BaselineManifestEntries`）
- `ResourceFile` — 只存 `PackageRelativePath` + `Platform`/`IncludedPlatforms`

**新结构**:
```
ResourceFile
├── FileName          (主键，如 "dlc/d3_a0.unity3d")
├── Category          ("preview" | "dlc" | "Fonts" | "slang")
├── Compressed        (bool)
├── AcquireOnDemand   (int)
└── PlatformManifest[] ← 每个平台一条
    ├── Platform      ("android" | "ios" | "share")
    ├── Exist         (文件是否存在)
    ├── SourceFileSize / SourceChecksum / SourceCompressedFileSize / SourceCompressedChecksum
    └── Checksum      (实际文件 MD5)
```

- **preview/slang**: `Platform = "share"`，只存一条，所有平台共用
- **dlc/Fonts**: 按平台独立存储，导入时分别写入 `resources/{platform}/{path}`
- **导出规则**: 无跨平台 fallback；iOS 导出绝不读取 `resources/android/`，反之亦然

## Song 实体清理

**删除的字段**（从未填充或属于交叉实体链接）:
- `TitlesByLanguage`, `DescriptionsByLanguage` — 被 `Localizations` 取代
- `ItemNamesByLanguage`, `ProductIds`, `ItemIds`, `CategoryIds` — Entity 间不应有关联
- `GetTitle()`, `GetDescription()`, `GetItemName()` helper 方法

**类型变更 `string` → `int`**:
- Song: `Id`, `ItemId`, `CostGamePoint`, `CostGameCash`, `Flag`, `TrackId`
- SongPattern: `PatternId`, `SongId`, `Line`, `Signature`, `Difficulty`, `PointType`, `PointValue`
- SongLocalization: `SongId`

## Songs.razor 简化

- 语言列表固定为 `["CN", "JP", "KR", "TW", "US"]`，不再从数据动态计算
- `DisplayTitle` 使用 `Localizations[lang].FullName ?? song.Name`
- `_cachedLanguages` 预初始化，防止 `FluentSelect` 将 `selectedLanguage` 置 null

## Import Pipeline

- 全部走 `PlatformPackageImporter`（分平台导入）
- `slang` 表作为共享资源处理（`Category=slang`, `Platform=share`）
- Pattern 去重: 全局按 `(SongId, Signature, Line)` 去重
- QuestMission: 按 per-quest 索引，无空壳 mission

## Export Pipeline

- 全部走 `PlatformPackageExporter`（分平台导出）
- 表格 CSV 通过 `ExportEntityTablesAsync` 生成 13 种表 × 5 语言
- 导出默认输出 `android` + `ios`，不依赖"已导入平台"历史
- `PatchPackageExporter` / `Importer` / `Validator` 已从 DI 移除

## 关键原则

1. **Entity 独立** — Song、Item、Product 之间不交叉存储
2. **无跨平台 fallback** — dlc/Fonts 严格按平台隔离
3. **只有分平台导出** — 不存在"全量合并"概念
4. **5 种语言固定** — CN/JP/KR/TW/US，不随数据变化
