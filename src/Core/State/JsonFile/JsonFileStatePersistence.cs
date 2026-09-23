using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;
using Core.Packages.Installation;
using Core.Utils;
using Newtonsoft.Json;

namespace Core.State.JsonFile;

internal class JsonFileStatePersistence : IStatePersistence
{
    internal const int UnkownVersionHash = 42;

    private const string StateV2FileName = "state.json";
    private const string StateV1FileName = "installed.json";

    private readonly IFileSystem fs;
    private readonly string stateV2FilePath;
    private readonly string stateV1FilePath;

    private static readonly JsonSerializerSettings JsonSerializerSettings = new()
    {
        Formatting = Formatting.None,
        DefaultValueHandling = DefaultValueHandling.Ignore,
        DateTimeZoneHandling = DateTimeZoneHandling.Utc
    };

    public JsonFileStatePersistence(string modsPath) :
        this(new FileSystem(), Path.Combine(modsPath, StateV2FileName), Path.Combine(modsPath, StateV1FileName))
    {
    }

    internal JsonFileStatePersistence(IFileSystem fs, string stateV2FilePath, string stateV1FilePath)
    {
        this.fs = fs;
        this.stateV2FilePath = stateV2FilePath;
        this.stateV1FilePath = stateV1FilePath;
    }

    // The beauty of JSON libraries setting non-null fields to null
    [SuppressMessage("ReSharper", "NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract")]
    public SavedState ReadState()
    {
        if (fs.File.Exists(stateV2FilePath))
        {
            var contents = fs.File.ReadAllText(stateV2FilePath);
            var jsonState = JsonConvert.DeserializeObject<JsonSavedStateV2>(contents, JsonSerializerSettings);
            if (jsonState is null)
            {
                return SavedState.Empty();
            }
            var installTime = jsonState.Install.Time ?? fs.File.GetLastWriteTimeUtc(stateV2FilePath);
            return new SavedState(
                Installation: jsonState.Install.Mods.SelectValues(pis => new PackageInstallationState(
                    Time: pis.Time == default ? installTime : pis.Time,
                    VersionHash: pis.VersionHash,
                    Partial: pis.Partial,
                    Dependencies: pis.Dependencies ?? Array.Empty<string>(),
                    Files: pis.Files ?? Array.Empty<string>(),
                    ShadowedBy: pis.ShadowedBy ?? Array.Empty<string>())
                ).ToImmutableDictionary()
            );
        }

        if (fs.File.Exists(stateV1FilePath))
        {
            var contents = fs.File.ReadAllText(stateV1FilePath);
            var jsonState = JsonConvert.DeserializeObject<Dictionary<string, IReadOnlyCollection<string>>>(contents, JsonSerializerSettings);
            if (jsonState is null)
            {
                return SavedState.Empty();
            }
            var installTime = fs.File.GetLastWriteTimeUtc(stateV1FilePath);
            return new SavedState(
                Installation: jsonState.AsEnumerable().ToDictionary(
                    kv => kv.Key,
                    kv => new PackageInstallationState(
                        Time: installTime,
                        VersionHash: UnkownVersionHash, // Fake hash to force reinstall on older versions
                        Partial: false,
                        Dependencies: Array.Empty<string>(),
                        Files: kv.Value, ShadowedBy: Array.Empty<string>())
                )
            );
        }

        return SavedState.Empty();
    }

    public void WriteState(SavedState state)
    {
        var jsonState = new JsonSavedStateV2(
            Install: new JsonInstallationStateV2(
                Time: null,
                Mods: state.Installation.SelectValues(pis => new JsonPackageInstallationStateV2(
                    Time: pis.Time.UtcDateTime,
                    VersionHash: pis.VersionHash,
                    Partial: pis.Partial,
                    Dependencies: pis.Dependencies,
                    Files: pis.Files,
                    ShadowedBy: pis.ShadowedBy
                ))
            ));

        // Remove state v1 on write if upgrading from a previous version
        fs.File.Delete(stateV1FilePath);

        fs.File.WriteAllText(stateV2FilePath, JsonConvert.SerializeObject(jsonState, JsonSerializerSettings));
    }
}
