using Core.Mods.Installation.Installers;
using FluentAssertions;

namespace Core.Tests.Mods.Installation.Installers;

[UnitTest]
public class BaseModInstallerTest
{
    [Fact]
    public void DedupeRecordBlocks_ConsidersOnlyFirstLine()
    {
        BaseModInstaller.DedupeRecordBlocks([
            @"RECORDGROUP foo
                first",
            @"RECORDGROUP foo
                last"
        ]).Should().BeEquivalentTo([
            @"RECORDGROUP foo
                last"
        ]);
    }

    [Fact]
    public void DedupeRecordBlocks_IgnoresRedundantWhitespaces()
    {
        BaseModInstaller.DedupeRecordBlocks([
            "  RECORD foo\vbar ",
            "RECORD foo\tbar"
        ]).Should().BeEquivalentTo([
            "RECORD foo\tbar"
        ]);
    }

    [Fact]
    public void DedupeRecordBlocks_WorksForEmptyLines()
    {
        BaseModInstaller.DedupeRecordBlocks([
            "",
            "",
        ]).Should().BeEquivalentTo([
            ""
        ]);
    }

    [Fact]
    public void DedupeRecordBlocks_AssumesCommentsAlreadyRemoved()
    {
        BaseModInstaller.DedupeRecordBlocks([
            @"RECORD foo bar # first",
            @"RECORD foo bar # last"
        ]).Should().BeEquivalentTo([
            @"RECORD foo bar # first",
            @"RECORD foo bar # last"
        ]);
    }
}
