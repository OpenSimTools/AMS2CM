using Core.Mods.Installation.Installers;
using FluentAssertions;

namespace Core.Tests.Mods;

[UnitTest]
public class ContainedDirsRootFinderTest
{
    private static readonly string[] RootDirs = ["R1", "R2"];
    private readonly ContainedDirsRootFinder rootFinder = new(RootDirs);

    [Fact]
    public void FromDirectoryList_FindsDirsAtRoot()
    {
        rootFinder.FromDirectoryList(
            [
                Path.Combine("R1"),
            ]).Roots.Should().BeEquivalentTo([
                "",
            ]);
    }

    [Fact]
    public void FromDirectoryList_FindsDirsWhenNamePrefixOfOtherDirs()
    {
        rootFinder.FromDirectoryList(
            [
                Path.Combine("D1", "R1"),
                Path.Combine("D11", "R1")
            ]).Roots.Should().BeEquivalentTo([
                "D1",
                "D11"
            ]);
    }

    [Fact]
    public void FromDirectoryList_IgnoresNestedRoots()
    {
        rootFinder.FromDirectoryList(
            [
                Path.Combine("D1", "D2", "R1"),
                Path.Combine("D1", "R1"),
                Path.Combine("D1", "D3", "R1")
            ]).Roots.Should().BeEquivalentTo([
                "D1"
            ]);

        // Empty path is a special case
        rootFinder.FromDirectoryList(
            [
                Path.Combine("D1", "R1"),
                Path.Combine("R1"),
                Path.Combine("D2", "R2")
            ]).Roots.Should().BeEquivalentTo([
                ""
            ]);
    }

    [Fact]
    public void FromDirectoryList_IsCaseInsensitive()
    {
        rootFinder.FromDirectoryList(
            [
                Path.Combine("d1", "r1"),
            ]).Roots.Should().BeEquivalentTo([
                "d1",
            ]);
    }

    [Fact]
    public void FromDirectoryList_FindsDirsInSubDirs()
    {
        rootFinder.FromDirectoryList(
            [
                Path.Combine("D1", "R1"),
                Path.Combine("D2", "D3", "R2"),
                Path.Combine("D4")
            ]).Roots.Should().BeEquivalentTo([
                "D1",
                Path.Combine("D2", "D3")
            ]);
    }
}
