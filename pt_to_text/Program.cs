using DMTQ.Tools.Core.Models.Pattern;
using DMTQ.Tools.Core.Services.Pattern;

// Share the editor's validated PT implementation; no server or credentials required.
if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: pt_to_text <chart.pt> [more.pt ...]");
    return 1;
}
var failed = false;
var binary = new PatternBinarySerializer();
var text = new PatternTextSerializer();
foreach (var path in args)
{
    try
    {
        var doc = binary.Deserialize(File.ReadAllBytes(path), PatternFormat.Pt);
        var output = Path.ChangeExtension(path, ".txt");
        File.WriteAllText(output, text.Serialize(doc));
        Console.WriteLine($"{path}: PT 0x{doc.Header.PtVersionCode:X4}, encrypted={doc.WasEncrypted}, {doc.CommandCount} events -> {output}");
    }
    catch (Exception error) when (error is IOException or ArgumentException or OverflowException)
    {
        Console.Error.WriteLine($"{path}: {error.Message}");
        failed = true;
    }
}
return failed ? 1 : 0;
