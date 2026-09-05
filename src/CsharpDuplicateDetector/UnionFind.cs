namespace CsharpDuplicateDetector;

public sealed class UnionFind
{
    private readonly int[] _parent;

    public UnionFind(int size)
    {
        _parent = Enumerable.Range(0, size).ToArray();
    }

    public int Find(int x) => _parent[x] == x ? x : (_parent[x] = Find(_parent[x]));

    public void Union(int a, int b)
    {
        var rootA = Find(a);
        var rootB = Find(b);
        if (rootA != rootB)
        {
            _parent[rootA] = rootB;
        }
    }

    public List<List<int>> GetGroups(IEnumerable<int> membersOfInterest)
    {
        var groups = new Dictionary<int, List<int>>();
        foreach (var i in membersOfInterest)
        {
            var root = Find(i);
            if (!groups.TryGetValue(root, out var list))
            {
                groups[root] = list = new List<int>();
            }

            list.Add(i);
        }

        return groups.Values.ToList();
    }
}
