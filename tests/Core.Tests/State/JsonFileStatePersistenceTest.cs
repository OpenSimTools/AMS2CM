using System.IO.Abstractions.TestingHelpers;
using Core.State;
using FluentAssertions;

namespace Core.Tests.State;

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
    public void ReadState_V2DefaultValues()
    {
        var fileWriteTime = DateTime.Today.AddDays(-1);
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
        state.Install.Time.Should().BeNull();
        state.Install.Mods.Keys.Should().Contain("M");

        var mod = state.Install.Mods["M"];
        mod.Time.Should().Be(fileWriteTime);
        mod.FsHash.Should().BeNull();
        mod.Partial.Should().BeFalse();
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
        state.Install.Time.Should().Be(DateTime.UnixEpoch);
        state.Install.Mods.Keys.Should().Contain("M");

        var mod = state.Install.Mods["M"];
        mod.Time.Should().Be(DateTime.UnixEpoch);
    }

    [Fact]
    public void ReadState_V1DefaultValues()
    {
        var fileWriteTime = DateTime.Today.AddDays(-1);
        var fs = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { StateV1File, new MockFileData(
                """
                {
                    "M": []
                }
                """) { LastWriteTime = fileWriteTime }
            }
        });
        var sp = new JsonFileStatePersistence(fs, "NotUsed", fs.Path.GetFullPath(StateV1File));

        var state = sp.ReadState();
        state.Install.Time.Should().BeNull();
        state.Install.Mods.Keys.Should().Contain("M");

        var mod = state.Install.Mods["M"];
        mod.Time.Should().Be(fileWriteTime);
        mod.FsHash.Should().BeNull();
        mod.Partial.Should().BeFalse();
        mod.Dependencies.Should().BeEmpty();
        mod.Files.Should().BeEmpty();
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

        state.Install.Mods.Keys.Should().Contain("V2");
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
}
