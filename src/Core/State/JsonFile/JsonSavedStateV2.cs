using Newtonsoft.Json;

namespace Core.State.JsonFile;

record JsonSavedStateV2(
    JsonInstallationStateV2 Install
);

record JsonInstallationStateV2(
    // needed for backward compatibility
    DateTime? Time,
    IReadOnlyDictionary<string, JsonPackageInstallationStateV2> Mods
);

record JsonPackageInstallationStateV2(
    DateTime Time,
    [JsonProperty("FsHash")]
    int? VersionHash,
    bool Partial,
    IReadOnlyCollection<string> Dependencies,
    IReadOnlyCollection<string> Files,
    IReadOnlyCollection<string> ShadowedBy
);
