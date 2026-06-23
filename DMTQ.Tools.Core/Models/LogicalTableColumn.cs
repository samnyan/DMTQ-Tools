namespace DMTQ.Tools.Core.Models;

public sealed record LogicalTableColumn(
    string Key,
    string DisplayName,
    string? LanguageCode,
    string SourceColumnName,
    bool Editable);
