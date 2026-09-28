namespace DMTQ.Tools.Core.Models.Entity;

/// <summary>One distinct category ID and its associated product IDs.</summary>
public sealed record ProductCategorySummary(string Id, string Name, string? Notes, IReadOnlyList<string> ProductIds);
