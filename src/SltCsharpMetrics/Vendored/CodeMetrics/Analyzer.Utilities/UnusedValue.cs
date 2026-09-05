using System.Runtime.InteropServices;

namespace Analyzer.Utilities;

/// <summary>
/// A placeholder value type for <see cref="T:System.Collections.Concurrent.ConcurrentDictionary`2" /> used as a set.
/// </summary>
[StructLayout(LayoutKind.Sequential, Size = 1)]
internal readonly struct UnusedValue
{
}
