namespace DMTQ.Tools.Core.Models.Pattern;

/// <summary>Persisted index of pattern files found in a patch project.</summary>
public sealed class PatternIndexDocument
{
    /// <summary>Current serialized index schema version.</summary>
    public int SchemaVersion { get; set; } = 3;

    /// <summary>UTC time when the index was last refreshed.</summary>
    public DateTimeOffset? LastIndexedAtUtc { get; set; }

    /// <summary>Patterns found under the project's Patterns directory.</summary>
    public Dictionary<int, PatternIndexEntry> Patterns { get; set; } = [];
}

/// <summary>Metadata for all versions of one pattern ID.</summary>
public sealed class PatternIndexEntry
{
    /// <summary>Song pattern rows that reference this pattern ID.</summary>
    public List<PatternSongReference> SongPatterns { get; set; } = [];

    /// <summary>Whether an _EARPHONE version exists for this pattern.</summary>
    public bool HasHeadphone { get; set; }

    /// <summary>Metadata for the standard pattern file, when present.</summary>
    public PatternFileIndex? Standard { get; set; }

    /// <summary>Metadata for the optional _EARPHONE pattern file.</summary>
    public PatternFileIndex? Headphone { get; set; }
}

/// <summary>Song identity associated with a songPattern row.</summary>
public sealed record PatternSongReference(
    int PatternId,
    int SongId,
    string SongName,
    int Line = 0,
    int Signature = 0,
    int Difficulty = 0);

/// <summary>Metadata parsed or measured from one pattern file.</summary>
public sealed class PatternFileIndex
{
    /// <summary>Path relative to the patch project root, using forward slashes.</summary>
    public string RelativePath { get; set; } = string.Empty;

    /// <summary>Pattern file size in bytes.</summary>
    public long FileSize { get; set; }

    /// <summary>Last modification time reported by the file system.</summary>
    public DateTimeOffset LastWriteTimeUtc { get; set; }

    /// <summary>Number of tracks when the bytes file was successfully parsed.</summary>
    public int? TrackCount { get; set; }

    /// <summary>Number of sounds when the bytes file was successfully parsed.</summary>
    public int? SoundCount { get; set; }

    /// <summary>Number of events when the bytes file was successfully parsed.</summary>
    public int? EventCount { get; set; }

    /// <summary>Parsing error, or null when the file was read successfully.</summary>
    public string? ParseError { get; set; }
}

/// <summary>Progress reported while refreshing the pattern index.</summary>
public sealed record PatternIndexProgress(int Completed, int Total, string RelativePath);
