namespace CsharpDuplicateDetector;

public sealed class CodeBlock
{
    public required string File { get; init; }
    public string? ClassName { get; init; }
    public string? MethodName { get; init; }
    public int Line { get; init; }
    public required List<string> Tokens { get; init; }

    private HashSet<string>? _tokenSet;
    public HashSet<string> TokenSet => _tokenSet ??= new HashSet<string>(Tokens);
}
