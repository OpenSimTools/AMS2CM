using System.Globalization;
using System.IO.Abstractions.TestingHelpers;
using Core.Packages.Installation;
using Core.State;
using Core.State.JsonFile;
using Core.Utils;
using FluentAssertions;

namespace Core.Tests.State.JsonFile;

[IntegrationTest]
public class JsonFileStatePersistenceTest
{
    private const string StateV2File = "v2";
    private const string StateV1File = "v1";

    [Fact]
    public void ReadState_EmptyStateWhenNoStateFileExists()
    {
        var fs = new MockFileSystem();
        var sp = new JsonFileStatePersistence(fs, StateV2File, StateV1File);

        sp.ReadState().Should().Be(SavedState.Empty());
    }

    [Fact]
    public void ReadState_V2()
    {
        var fs = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { StateV2File, new MockFileData(
                    """
                    {
                        "Install": {
                            "Mods": {
                                "M": {
                                    "Time": "2007-06-05T11:22:33+04",
                                    "FsHash": 101,
                                    "Dependencies": ["D"],
                                    "Files": ["F"],
                                    "ShadowedBy": ["S"],
                                }
                            }
                        }
                    }
                    """)
            }
        });
        var sp = new JsonFileStatePersistence(fs, fs.Path.GetFullPath(StateV2File), "NotUsed");

        var state = sp.ReadState();
        state.Installation.Keys.Should().Contain("M");

        var mod = state.Installation["M"];
        mod.Time.Should().Be(DateTimeOffset.Parse("2007-06-05T11:22:33+04").ToUniversalTime());
        mod.VersionHash.Should().Be(101);
        mod.Dependencies.Should().Contain("D");
        mod.Files.Should().Contain("F");
        mod.ShadowedBy.Should().Contain("S");
    }

    [Fact]
    public void ReadState_V2DefaultValues()
    {
        var fileWriteTime = DateTimeOffset.Now.AddDays(-1);
        var fs = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { StateV2File, new MockFileData(
                """
                {
                    "Install": {
                        "Mods": {
                            "M": {
                            }
                        }
                    }
                }
                """) { LastWriteTime = fileWriteTime }
            }
        });
        var sp = new JsonFileStatePersistence(fs, fs.Path.GetFullPath(StateV2File), "NotUsed");

        var state = sp.ReadState();
        state.Installation.Keys.Should().Contain("M");

        var mod = state.Installation["M"];
        mod.Time.Should().Be(fileWriteTime.ToUniversalTime());
        mod.VersionHash.Should().BeNull();
        mod.Dependencies.Should().BeEmpty();
        mod.Files.Should().BeEmpty();
        mod.ShadowedBy.Should().BeEmpty();
    }

    [Fact]
    public void ReadState_V2ModsInstallTimeDefaultsToGlobal()
    {
        var fs = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { StateV2File,
                """
                {
                    "Install": {
                        "Time": "1970-01-01T00:00:00Z",
                        "Mods": {
                            "M": {
                            }
                        }
                    }
                }
                """
            }
        });
        var sp = new JsonFileStatePersistence(fs, fs.Path.GetFullPath(StateV2File), "NotUsed");

        var state = sp.ReadState();
        state.Installation.Keys.Should().Contain("M");

        var mod = state.Installation["M"];
        mod.Time.Should().Be(DateTimeOffset.UnixEpoch);
    }

    [Fact]
    public void ReadState_V1()
    {
        var fileWriteTime = DateTimeOffset.Now.AddDays(-1);
        var fs = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { StateV1File, new MockFileData(
                """
                {
                    "M": ["F"]
                }
                """) { LastWriteTime = fileWriteTime }
            }
        });
        var sp = new JsonFileStatePersistence(fs, "NotUsed", fs.Path.GetFullPath(StateV1File));

        var state = sp.ReadState();
        state.Installation.Keys.Should().Contain("M");

        var mod = state.Installation["M"];
        mod.Time.Should().Be(fileWriteTime.ToUniversalTime());
        mod.VersionHash.Should().Be(JsonFileStatePersistence.UnkownVersionHash);
        mod.Dependencies.Should().BeEmpty();
        mod.Files.Should().Contain("F");
        mod.ShadowedBy.Should().BeEmpty();
    }

    [Fact]
    public void ReadState_FavoursLatestStateFile()
    {
        var fs = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { StateV2File,
                """
                {
                    "Install": {
                        "Mods": {
                            "V2": {
                            }
                        }
                    }
                }
                """
            },
            { StateV1File,
                """
                {
                    "V1": []
                }
                """
            }
        });
        var sp = new JsonFileStatePersistence(fs, fs.Path.GetFullPath(StateV2File), fs.Path.GetFullPath(StateV1File));

        var state = sp.ReadState();

        state.Installation.Keys.Should().Contain("V2");
    }

    [Fact]
    public void WriteState_V2WritesUtc()
    {
        var fs = new MockFileSystem();
        var sp = new JsonFileStatePersistence(fs, fs.Path.GetFullPath(StateV2File), "NotUsed");

        var localTime = DateTimeOffset.UnixEpoch.ToOffset(TimeSpan.FromHours(4));

        sp.WriteState(new SavedState(
            Installation: new Dictionary<string, PackageInstallationState>
            {
                ["P"] = new(
                    Time: localTime,
                    VersionHash: null,
                    Partial: false,
                    Dependencies: [],
                    Files: [],
                    ShadowedBy: []),
            }
        ));

        fs.GetFile(StateV2File).TextContents.Should().Contain(""""Time":"1970-01-01T00:00:00Z"""");
    }

    [Fact]
    public void WriteState_DeletesPreviousStates()
    {
        var fs = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { StateV1File, "NotUsed" }
        });
        var sp = new JsonFileStatePersistence(fs, fs.Path.GetFullPath(StateV2File), fs.Path.GetFullPath(StateV1File));

        sp.WriteState(SavedState.Empty());

        fs.AllFiles.Should().BeEquivalentTo(fs.Path.GetFullPath(StateV2File));
    }

    [Fact]
    public void WriteReadLoop()
    {
        var fs = new MockFileSystem();
        var sp = new JsonFileStatePersistence(fs, fs.Path.GetFullPath(StateV2File), "NotUsed");

        var writtenState = new SavedState(
            Installation: new Dictionary<string, PackageInstallationState>
            {
                ["P"] = new(
                    Time: DateTimeOffset.UtcNow, VersionHash: 42, Partial: true,
                    Dependencies: ["D"],
                    Files: ["F"],
                    ShadowedBy: ["S"]),
            }
        );

        sp.WriteState(writtenState);
        var readState = sp.ReadState();

        readState.Should().BeEquivalentTo(writtenState);
    }
}
