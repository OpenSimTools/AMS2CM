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

    public InternalState ReadState()
    {
        // Always favour new state if present
        if (File.Exists(stateFile))
        {
            var contents = File.ReadAllText(stateFile);
            return JsonConvert.DeserializeObject<InternalState>(contents);
        }

        // Fallback to old state when new state is not present
        if (File.Exists(oldStateFile))
        {
            var contents = File.ReadAllText(oldStateFile);
            var oldState = JsonConvert.DeserializeObject<Dictionary<string, IReadOnlyCollection<string>>>(contents);
            var installTime = File.GetLastWriteTimeUtc(oldStateFile);
            return new InternalState(
                Install: new(
                    Time: installTime,
                    Mods: oldState.AsEnumerable().ToDictionary(
                        kv => kv.Key,
                        kv => new InternalModInstallationState(FsHash: null, Partial: false, Files: kv.Value)
                    )
                )
            );
        }

        return InternalState.Empty();
    }

    public void WriteState(InternalState state)
    {
        // Remove old state if upgrading from a previous version
        File.Delete(oldStateFile);

        File.WriteAllText(stateFile, JsonConvert.SerializeObject(state, JsonSerializerSettings));
    }
}

