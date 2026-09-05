namespace CsharpDuplicateDetector.Tests;

[TestClass]
public class UnionFindTests
{
    [TestMethod]
    public void Find_WithoutUnion_EachElementIsItsOwnRoot()
    {
        var unionFind = new UnionFind(3);
        Assert.AreEqual(0, unionFind.Find(0));
        Assert.AreEqual(1, unionFind.Find(1));
        Assert.AreEqual(2, unionFind.Find(2));
    }

    [TestMethod]
    public void Union_MergesTwoElementsIntoSameRoot()
    {
        var unionFind = new UnionFind(2);
        unionFind.Union(0, 1);
        Assert.AreEqual(unionFind.Find(0), unionFind.Find(1));
    }

    [TestMethod]
    public void GetGroups_ChainedUnions_ProduceOneClusterForConnectedMembers()
    {
        var unionFind = new UnionFind(5);
        unionFind.Union(0, 1);
        unionFind.Union(1, 2);
        // index 3 and 4 stay unconnected from {0,1,2} and from each other.

        var groups = unionFind.GetGroups(new[] { 0, 1, 2, 3, 4 });

        Assert.AreEqual(3, groups.Count);
        var clusterOf012 = groups.Single(g => g.Contains(0));
        CollectionAssert.AreEquivalent(new[] { 0, 1, 2 }, clusterOf012);
    }

    [TestMethod]
    public void GetGroups_OnlyIncludesRequestedMembers()
    {
        var unionFind = new UnionFind(4);
        unionFind.Union(0, 1);
        unionFind.Union(2, 3);

        var groups = unionFind.GetGroups(new[] { 0, 1 });

        Assert.AreEqual(1, groups.Count);
        CollectionAssert.AreEquivalent(new[] { 0, 1 }, groups[0]);
    }
}
