using FluentAssertions;
using static Core.Utils.StringExtensions;

namespace Core.Tests.Utils;

[UnitTest]
public class PostProcessorTest
{
    [Fact]
    public void NormalizeWhitespaces_ReplacesWithSpaces()
    {
        "".NormalizeWhitespaces().Should().BeEmpty();
        "foo bar".NormalizeWhitespaces().Should().Be("foo bar");
        "foo\r\v\nbar".NormalizeWhitespaces().Should().Be("foo bar");
    }

    [Fact]
    public void NormalizeWhitespaces_TrimsInput()
    {
        " foo\f".NormalizeWhitespaces().Should().Be("foo");
    }
}
