using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MacroForge;

public static class ProfileStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static void Save(string path, MacroProfile profile)
        => File.WriteAllText(path, JsonSerializer.Serialize(profile, Options));

    public static MacroProfile? Load(string path)
        => JsonSerializer.Deserialize<MacroProfile>(File.ReadAllText(path), Options);
}
