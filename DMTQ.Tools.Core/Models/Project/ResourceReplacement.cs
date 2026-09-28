namespace DMTQ.Tools.Core.Models.Project;

/// <summary>One platform resource replacement to apply as a project transaction.</summary>
public sealed record ResourceReplacement(string SourceFilePath, string FileName, string Platform, bool Compressed);
