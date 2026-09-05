using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CsharpDuplicateDetector.Tests;

[TestClass]
public class JaccardComparerTests
{
    [TestMethod]
    public void SetJaccard_BothEmpty_ReturnsOne()
    {
        var result = JaccardComparer.SetJaccard(new HashSet<string>(), new HashSet<string>());
        Assert.AreEqual(1.0, result);
    }

    [TestMethod]
    public void SetJaccard_IdenticalSets_ReturnsOne()
    {
        var a = new HashSet<string> { "foo", "bar", "baz" };
        var b = new HashSet<string> { "foo", "bar", "baz" };
        Assert.AreEqual(1.0, JaccardComparer.SetJaccard(a, b));
    }

    [TestMethod]
    public void SetJaccard_DisjointSets_ReturnsZero()
    {
        var a = new HashSet<string> { "foo" };
        var b = new HashSet<string> { "bar" };
        Assert.AreEqual(0.0, JaccardComparer.SetJaccard(a, b));
    }

    [TestMethod]
    public void SetJaccard_PartialOverlap_ReturnsExpectedRatio()
    {
        var a = new HashSet<string> { "a", "b", "c" };
        var b = new HashSet<string> { "b", "c", "d" };
        Assert.AreEqual(2.0 / 4.0, JaccardComparer.SetJaccard(a, b), 1e-9);
    }

    [TestMethod]
    public void MultisetJaccard_AccountsForDuplicateTokenCounts()
    {
        var a = new List<string> { "x", "x", "y" };
        var b = new List<string> { "x", "y", "y" };

        // min(x)=1, min(y)=1 -> minSum=2; max(x)=2, max(y)=2 -> maxSum=4
        Assert.AreEqual(2.0 / 4.0, JaccardComparer.MultisetJaccard(a, b), 1e-9);
    }

    [TestMethod]
    public void MultisetJaccard_BothEmpty_ReturnsZero()
    {
        Assert.AreEqual(0.0, JaccardComparer.MultisetJaccard(new List<string>(), new List<string>()));
    }
}
