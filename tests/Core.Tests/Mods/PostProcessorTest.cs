using Core.Mods;
using FluentAssertions;

namespace Core.Tests.Mods;

[UnitTest]
public class PostProcessorTest
{
    [Fact]
    public void DedupeRecordBlocks_ConsidersOnlyFirstLine()
    {
        PostProcessor.DedupeRecordBlocks([
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
        PostProcessor.DedupeRecordBlocks([
            "  RECORD foo\vbar ",
            "RECORD foo\tbar"
        ]).Should().BeEquivalentTo([
            "RECORD foo\tbar"
        ]);
    }

    [Fact]
    public void DedupeRecordBlocks_WorksForEmptyLines()
    {
        PostProcessor.DedupeRecordBlocks([
            "",
            "",
        ]).Should().BeEquivalentTo([
            ""
        ]);
    }

    [Fact]
    public void DedupeRecordBlocks_AssumesCommentsAlreadyRemoved()
    {
        PostProcessor.DedupeRecordBlocks([
            @"RECORD foo bar # first",
            @"RECORD foo bar # last"
        ]).Should().BeEquivalentTo([
            @"RECORD foo bar # first",
            @"RECORD foo bar # last"
        ]);
    }
}
