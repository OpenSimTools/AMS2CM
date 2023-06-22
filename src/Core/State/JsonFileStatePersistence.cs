using Newtonsoft.Json;

namespace Core.State;

internal class JsonFileStatePersistence : IStatePersistence
{
    private const string StateFileName = "state.json";
    private const string OldStateFileName = "installed.json";

    private readonly bool oldStateIsPrimary;
    private readonly string stateFile;
    private readonly string oldStateFile;

    private static readonly JsonSerializerSettings JsonSerializerSettings = new()
    {
        Formatting = Formatting.None,
        DefaultValueHandling = DefaultValueHandling.Ignore,
    };

    public JsonFileStatePersistence(string modsDir, bool oldStateIsPrimary)
    {
        this.oldStateIsPrimary = oldStateIsPrimary;
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
                        kv => new InternalModInstallationState(Partial: false, Files: kv.Value)
                    )
                )
            );
        }
        return InternalState.Empty();
    }

    public void WriteState(InternalState state)
    {
        // Write old state if it's primary or if it exists
        if (oldStateIsPrimary || File.Exists(oldStateFile))
        {
            var oldState = state.Install.Mods.ToDictionary(kv => kv.Key, kv => kv.Value.Files);
            File.WriteAllText(oldStateFile, JsonConvert.SerializeObject(oldState, JsonSerializerSettings));
        }
        // Write new state if it's primary or if it exists
        if (!oldStateIsPrimary || File.Exists(stateFile))
        {
            File.WriteAllText(stateFile, JsonConvert.SerializeObject(state, JsonSerializerSettings));
        }
    }

}

