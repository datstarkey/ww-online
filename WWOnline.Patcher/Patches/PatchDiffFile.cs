using System.Text;
using WWOnline.Patcher.Config;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace WWOnline.Patcher.Patches;

/// <summary>
/// A relocation an assembled REL patch chunk needs at load time: a branch from REL code to a
/// fixed main.dol address. The REL loader (OSLink) writes the final displacement, so the patch
/// can only point an existing main.dol relocation at a new target (see RelBytePatcher).
/// </summary>
/// <param name="Offset">Byte offset of the relocated field inside the chunk.</param>
/// <param name="Type">R_PPC_* relocation type name, e.g. "R_PPC_REL24".</param>
/// <param name="Symbol">Symbol the chunk referenced (for error messages and the diff file).</param>
/// <param name="Address">Resolved main.dol address of <paramref name="Symbol"/>.</param>
public sealed record PatchRelocation(uint Offset, string Type, string Symbol, uint Address);

/// <summary>One contiguous run of bytes written by a patch.</summary>
public sealed record PatchChunk(uint Address, byte[] Data, IReadOnlyList<PatchRelocation> Relocations)
{
    public uint End => Address + (uint)Data.Length;
}

/// <summary>
/// An assembled patch: the YAML diff AssemblePatchesStep writes to patch_diffs/ (one per .asm).
/// Keys are game-relative file paths ("sys/main.dol", "files/rels/d_a_ship.rel"); main.dol
/// chunks are keyed by RAM address, REL chunks by offset in the uncompressed REL.
/// </summary>
public sealed class PatchDiffFile
{
    public const string Suffix = "_diff.yaml";

    public string PatchName { get; }
    public bool IsOptional { get; }
    public IReadOnlyDictionary<string, IReadOnlyList<PatchChunk>> Files { get; }

    public PatchDiffFile(string patchName, bool isOptional, IReadOnlyDictionary<string, IReadOnlyList<PatchChunk>> files)
    {
        PatchName = patchName;
        IsOptional = isOptional;
        Files = files;
    }

    public static string PatchNameFromPath(string diffPath)
    {
        var name = Path.GetFileName(diffPath);
        return name.EndsWith(Suffix, StringComparison.OrdinalIgnoreCase) ? name[..^Suffix.Length] : Path.GetFileNameWithoutExtension(name);
    }

    public static PatchDiffFile Load(string path, bool isOptional)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(NullNamingConvention.Instance)
            .Build();
        var yaml = File.ReadAllText(path);
        var root = deserializer.Deserialize<Dictionary<string, Dictionary<string, Dictionary<string, object>>>>(yaml)
                   ?? new Dictionary<string, Dictionary<string, Dictionary<string, object>>>();

        var files = new Dictionary<string, IReadOnlyList<PatchChunk>>();
        foreach (var (filePath, chunks) in root)
        {
            var list = new List<PatchChunk>();
            foreach (var (addressStr, patchlet) in chunks ?? [])
            {
                var address = PatcherConfig.ParseHexOrDecimal(addressStr);
                byte[] data = [];
                if (patchlet.TryGetValue("Data", out var dataObj) && dataObj is List<object> dataList)
                    data = dataList.Select(b => (byte)PatcherConfig.ParseHexOrDecimal(b.ToString()!)).ToArray();

                var relocations = new List<PatchRelocation>();
                if (patchlet.TryGetValue("Relocations", out var relObj) && relObj is List<object> relList)
                {
                    foreach (var item in relList.OfType<Dictionary<object, object>>())
                    {
                        string Get(string key) => item.TryGetValue(key, out var v) ? v?.ToString() ?? "" :
                            throw new InvalidDataException($"{path}: relocation at 0x{address:X} has no {key}");
                        relocations.Add(new PatchRelocation(
                            PatcherConfig.ParseHexOrDecimal(Get("Offset")), Get("Type"), Get("Symbol"),
                            PatcherConfig.ParseHexOrDecimal(Get("Address"))));
                    }
                }
                list.Add(new PatchChunk(address, data, relocations));
            }
            files[filePath] = list.OrderBy(c => c.Address).ToList();
        }
        return new PatchDiffFile(PatchNameFromPath(path), isOptional, files);
    }

    /// <summary>Writes a diff in the Python-compatible format (hex ints, flow-style byte lists).</summary>
    public static void Write(string path, IReadOnlyDictionary<string, IReadOnlyList<PatchChunk>> files)
    {
        var sb = new StringBuilder();
        foreach (var (filePath, chunks) in files)
        {
            sb.Append(filePath).Append(":\n");
            foreach (var chunk in chunks.OrderBy(c => c.Address))
            {
                sb.Append($"  0x{chunk.Address:X8}:\n");
                sb.Append("    Data: [").Append(string.Join(", ", chunk.Data.Select(b => $"0x{b:X2}"))).Append("]\n");
                if (chunk.Relocations.Count > 0)
                {
                    sb.Append("    Relocations: [");
                    sb.Append(string.Join(", ", chunk.Relocations.Select(r =>
                        $"{{Offset: 0x{r.Offset:X}, Type: {r.Type}, Symbol: {r.Symbol}, Address: 0x{r.Address:X8}}}")));
                    sb.Append("]\n");
                }
            }
        }
        File.WriteAllText(path, sb.ToString());
    }
}
