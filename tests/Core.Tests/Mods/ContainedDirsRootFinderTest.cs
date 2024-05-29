using Core.Mods;

namespace Core.Tests.Mods;

public class ContainedDirsRootFinderTest
{
    private static readonly string[] RootDirs = ["R1", "R2"];
    private readonly ContainedDirsRootFinder rootFinder = new(RootDirs);

    [Fact]
    public void FromFileList_FindsDirsAtRoot()
    {
        Assert.Equal(
            [
                "",
            ],
            rootFinder.FromDirectoryList(
            [
                Path.Combine("R1"),
            ]));
    }

    [Fact]
    public void FromFileList_FindsDirsInSubDirs()
    {
        Assert.Equal(
            [
                "D1",
                Path.Combine("D2", "D3")
            ],
            rootFinder.FromDirectoryList(
            [
                Path.Combine("D1", "R1"),
                Path.Combine("D2", "D3", "R2"),
                Path.Combine("D4")
            ]));
    }

    [Fact]
    public void FromFileList_IgnoresNestedRoots()
    {
        Assert.Equal(
            [
                "D1"
            ],
            rootFinder.FromDirectoryList(
            [
                Path.Combine("D1", "D2", "R2"),
                Path.Combine("D1", "R1")
            ]));
    }

    [Fact]
    public void FromFileList_IsCaseInsensitive()
    {
        Assert.Equal(
            [
                "d1",
            ],
            rootFinder.FromDirectoryList(
            [
                Path.Combine("d1", "r1"),
            ]));
    }
}