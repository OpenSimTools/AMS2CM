using Core.Mods;

namespace Core.Tests.Mods;

[UnitTest]
public class PostProcessorTest
{
    [Fact]
    public void DedupeRecordBlocks_ConsidersOnlyFirstLine()
    {
        Assert.Equal(new[]
        {
            @"RECORDGROUP foo
                last"
        },
        PostProcessor.DedupeRecordBlocks(new[]
        {
            @"RECORDGROUP foo
                first",
            @"RECORDGROUP foo
                last"
        }));
    }

    [Fact]
    public void DedupeRecordBlocks_IgnoresRedundantWhitespaces()
    {
        Assert.Equal(new[]
        {
            "RECORD foo\tbar"
        },
        PostProcessor.DedupeRecordBlocks(new[]
        {
            "  RECORD foo\vbar ",
            "RECORD foo\tbar"
        }));
    }

    [Fact]
    public void DedupeRecordBlocks_WorksForEmptyLines()
    {
        Assert.Equal(new[]
        {
            ""
        },
        PostProcessor.DedupeRecordBlocks(new[]
        {
            "",
            "",
        }));
    }

    [Fact]
    public void DedupeRecordBlocks_AssumesCommentsAlreadyRemoved()
    {
        Assert.Equal(new[]
        {
            @"RECORD foo bar # first",
            @"RECORD foo bar # last"
        },
        PostProcessor.DedupeRecordBlocks(new[]
        {
            @"RECORD foo bar # first",
            @"RECORD foo bar # last"
        }));
    }
}
