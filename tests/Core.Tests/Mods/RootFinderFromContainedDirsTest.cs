using Core.Mods;

namespace Core.Tests.Mods;

public class RootFinderFromContainedDirsTest
{
    private static readonly string[] RootDirs = ["R1", "R2"];
    private readonly RootFinderFromContainedDirs rootFinder = new(RootDirs);

    [Fact]
    public void FromFileList_FindsDirsInSubDirs()
    {
        Assert.Equal(
            [
                "D1",
                Path.Combine("D2", "D3")
            ],
            rootFinder.FromFileList(
            [
                Path.Combine("D1", "R1", "F"),
                Path.Combine("D2", "D3", "R2", "F"),
                Path.Combine("D4", "F"),
                Path.Combine("F"),
            ]));
    }

    [Fact]
    public void FromFileList_IgnoresMatchingFiles()
    {
        Assert.Empty(
            rootFinder.FromFileList(
            [
                Path.Combine("D1", "R1"),
                Path.Combine("D2", "D3", "R2")
            ]));
    }

    [Fact]
    public void FromFileList_IgnoresNestedRoots()
    {
        Assert.Equal(
            [
                "D1"
            ],
            rootFinder.FromFileList(
            [
                Path.Combine("D1", "D2", "R2", "F"),
                Path.Combine("D1", "R1", "F")
            ]));
    }
}