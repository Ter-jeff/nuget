namespace CsharpDuplicateDetector;

public static class JaccardComparer
{
    public static double SetJaccard(HashSet<string> a, HashSet<string> b)
    {
        if (a.Count == 0 && b.Count == 0)
        {
            return 1.0;
        }

        var intersection = a.Count(b.Contains);
        var union = a.Count + b.Count - intersection;
        return union == 0 ? 0.0 : (double)intersection / union;
    }

    public static double MultisetJaccard(List<string> a, List<string> b)
    {
        var countsA = a.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
        var countsB = b.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());

        var keys = new HashSet<string>(countsA.Keys);
        keys.UnionWith(countsB.Keys);

        long minSum = 0;
        long maxSum = 0;
        foreach (var key in keys)
        {
            var ca = countsA.GetValueOrDefault(key);
            var cb = countsB.GetValueOrDefault(key);
            minSum += Math.Min(ca, cb);
            maxSum += Math.Max(ca, cb);
        }

        return maxSum == 0 ? 0.0 : (double)minSum / maxSum;
    }
}
