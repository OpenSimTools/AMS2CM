using Core.Utils;
using FluentAssertions;
using FluentAssertions.Collections;

namespace Core.Tests.Utils;

public static class PathAssertionExtensions
{
    [CustomAssertion]
    public static AndConstraint<StringCollectionAssertions<IEnumerable<string>>> BeEquivalentPathTo(
        this StringCollectionAssertions assertions,
        IEnumerable<string> expectation,
        string because = "",
        params object[] becauseArgs) =>
        assertions.BeEquivalentTo(expectation, options => options.IgnoringCase(), because, becauseArgs);

    [CustomAssertion]
    public static AndConstraint<GenericCollectionAssertions<RootedPath>> BeEquivalentPathTo(
        this GenericCollectionAssertions<RootedPath> assertions,
        IEnumerable<RootedPath> expectation,
        string because = "",
        params object[] becauseArgs) =>
        assertions.BeEquivalentTo(expectation, options => options.ComparingByValue<RootedPath>(), because, becauseArgs);
}
