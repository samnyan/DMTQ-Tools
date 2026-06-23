namespace DMTQ.Tools.Core.Models.Project;

public sealed record PatchFileEntry(
    string FileName,
    long FileSize,
    string Checksum,
    long CompressedFileSize,
    string CompressedChecksum,
    int AcquireOnDemand,
    bool Compressed,
    string Platform,
    string Tag);
