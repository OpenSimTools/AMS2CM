namespace Core.Tests.Utils;

using static Core.Utils.StringExtensions;

public class StringExtensionsTest
{
    [Fact]
    public void NormalizeWhitespacesReplacesWithSpaces()
    {
        Assert.Equal("", "".NormalizeWhitespaces());
        Assert.Equal("foo bar", "foo bar".NormalizeWhitespaces());
        Assert.Equal("foo bar", "foo\r\v\nbar".NormalizeWhitespaces());
    }

    [Fact]
    public void NormalizeWhitespacesTrimsInput()
    {
        Assert.Equal("foo", " foo\f".NormalizeWhitespaces());
    }
}