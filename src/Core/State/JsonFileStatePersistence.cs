using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;
using Core.Packages.Installation;
using Core.Utils;
using Newtonsoft.Json;

namespace Core.State;

internal class JsonFileStatePersistence : IStatePersistence
{
    private const string StateV2FileName = "state.json";
    private const string StateV1FileName = "installed.json";

    private readonly IFileSystem fs;
    private readonly string stateV2FilePath;
    private readonly string stateV1FilePath;

    private static readonly JsonSerializerSettings JsonSerializerSettings = new()
    {
        Formatting = Formatting.None,
        DefaultValueHandling = DefaultValueHandling.Ignore,
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

    // TODO: this can be made better with some work
    // The beauty of JSON libraries setting non-null fields to null
    [SuppressMessage("ReSharper", "NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract")]
    public SavedState ReadState()
    {
        if (fs.File.Exists(stateV2FilePath))
        {
            var contents = fs.File.ReadAllText(stateV2FilePath);
            var state = JsonConvert.DeserializeObject<SavedState>(contents);
            var installTime = state.Install.Time ?? fs.File.GetLastWriteTimeUtc(stateV2FilePath);
            return state with
            {
                Install = state.Install with
                {
                    Mods = state.Install.Mods.SelectValues(pis => pis with
                    {
                        Time = pis.Time == default ? installTime : pis.Time,
                        Dependencies = pis.Dependencies ?? Array.Empty<string>(),
                        Files = pis.Files ?? Array.Empty<string>(),
                        ShadowedBy = pis.ShadowedBy ?? Array.Empty<string>()
                    })
                }
            };
        }

        if (fs.File.Exists(stateV1FilePath))
        {
            var contents = fs.File.ReadAllText(stateV1FilePath);
            var state = JsonConvert.DeserializeObject<Dictionary<string, IReadOnlyCollection<string>>>(contents);
            var installTime = fs.File.GetLastWriteTimeUtc(stateV1FilePath);
            return new SavedState(
                Install: new InstallationState(
                    Time: null,
                    Mods: state.AsEnumerable().ToDictionary(
                        kv => kv.Key,
                        kv => new PackageInstallationState(
                            Time: installTime, FsHash: null, Partial: false,
                            Dependencies: Array.Empty<string>(),
                            Files: kv.Value, ShadowedBy: Array.Empty<string>())
                    )
                )
            );
        }

        return SavedState.Empty();
    }

    public void WriteState(SavedState state)
    {
        // Remove state v1 on write if upgrading from a previous version
        fs.File.Delete(stateV1FilePath);

        fs.File.WriteAllText(stateV2FilePath, JsonConvert.SerializeObject(state, JsonSerializerSettings));
    }
}
