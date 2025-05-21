using Core.Packages.Installation;
using Core.Utils;
using Newtonsoft.Json;

namespace Core.State;

internal class JsonFileStatePersistence : IStatePersistence
{
    private const string StateFileName = "state.json";
    private const string OldStateFileName = "installed.json";

    private readonly string stateFile;
    private readonly string oldStateFile;

    private static readonly JsonSerializerSettings JsonSerializerSettings = new()
    {
        Formatting = Formatting.None,
        DefaultValueHandling = DefaultValueHandling.Ignore,
    };

    public JsonFileStatePersistence(string modsDir)
    {
        stateFile = Path.Combine(modsDir, StateFileName);
        oldStateFile = Path.Combine(modsDir, OldStateFileName);
    }

    public SavedState ReadState()
    {
        // Always favour new state if present
        if (File.Exists(stateFile))
        {
            var contents = File.ReadAllText(stateFile);
            var state = JsonConvert.DeserializeObject<SavedState>(contents);
            // Fill mod install time if not present (for migration)
            return state with
            {
                Install = state.Install with
                {
                    Mods = state.Install.Mods.SelectValues(_ => _ with { Time = _.Time ?? state.Install.Time })
                }
            };
        }

        // Fallback to old state when new state is not present
        if (File.Exists(oldStateFile))
        {
            var contents = File.ReadAllText(oldStateFile);
            var oldState = JsonConvert.DeserializeObject<Dictionary<string, IReadOnlyCollection<string>>>(contents);
            var installTime = File.GetLastWriteTimeUtc(oldStateFile);
            return new SavedState(
                Install: new(
                    Time: installTime,
                    Mods: oldState.AsEnumerable().ToDictionary(
                        kv => kv.Key,
                        kv => new PackageInstallationState(
                            Time: installTime, FsHash: null, Partial: false,
                            Dependencies: Array.Empty<string>(),
                            Files: kv.Value)
                    )
                )
            );
        }

        return SavedState.Empty();
    }

    public void WriteState(SavedState state)
    {
        // Remove old state if upgrading from a previous version
        File.Delete(oldStateFile);

        File.WriteAllText(stateFile, JsonConvert.SerializeObject(state, JsonSerializerSettings));
    }
}

