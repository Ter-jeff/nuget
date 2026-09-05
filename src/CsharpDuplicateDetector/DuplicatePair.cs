namespace CsharpDuplicateDetector;

public sealed record DuplicatePair(
    int Index1,
    int Index2,
    string File1,
    string? Class1,
    string? Method1,
    int LineNumber1,
    string File2,
    string? Class2,
    string? Method2,
    int LineNumber2,
    double JaccardSimilarity,
    double KeyJaccardSimilarity);
