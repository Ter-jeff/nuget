using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SltCsharpMetrics.Tests;

[TestClass]
public class MetricsCalculatorTests
{
    private static (SyntaxNode Root, SemanticModel Model) Compile(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));

        var compilation = CSharpCompilation.Create(
            "MetricsCalculatorTestsAssembly",
            new[] { tree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var root = tree.GetRoot();
        var model = compilation.GetSemanticModel(tree);
        return (root, model);
    }

    private static MethodDeclarationSyntax FirstMethod(SyntaxNode root, string name) =>
        root.DescendantNodes().OfType<MethodDeclarationSyntax>().First(m => m.Identifier.Text == name);

    private static ClassDeclarationSyntax FirstClass(SyntaxNode root, string name) =>
        root.DescendantNodes().OfType<ClassDeclarationSyntax>().First(c => c.Identifier.Text == name);

    [TestMethod]
    public void ComputeCyclomaticComplexity_StraightLineCode_ReturnsOne()
    {
        var (root, _) = Compile(@"
            class C
            {
                void M()
                {
                    var x = 1;
                    var y = x + 1;
                }
            }");

        var method = FirstMethod(root, "M");
        Assert.AreEqual(1, MetricsCalculator.ComputeCyclomaticComplexity(method));
    }

    [TestMethod]
    public void ComputeCyclomaticComplexity_CountsEachDecisionPoint()
    {
        var (root, _) = Compile(@"
            class C
            {
                void M(bool a, bool b)
                {
                    if (a && b) { }
                    while (a || b) { }
                    var x = a ? 1 : 2;
                }
            }");

        var method = FirstMethod(root, "M");

        // base complexity(1) + if(1) + &&(1) + while(1) + ||(1) + ternary(1) = 6
        Assert.AreEqual(6, MetricsCalculator.ComputeCyclomaticComplexity(method));
    }

    [TestMethod]
    public void ComputeExecutableLines_DoesNotCountBlockSyntaxItself()
    {
        var (root, _) = Compile(@"
            class C
            {
                void M(bool a)
                {
                    if (a)
                    {
                        var x = 1;
                    }
                }
            }");

        var method = FirstMethod(root, "M");

        // the if-statement line and the var-declaration line; the wrapping block is not a counted line.
        Assert.AreEqual(2, MetricsCalculator.ComputeExecutableLines(method));
    }

    [TestMethod]
    public void ComputeExecutableLines_StatementsOnSameLine_CountAsOneLine()
    {
        var (root, _) = Compile("class C { void M() { var x = 1; var y = 2; } }");

        var method = FirstMethod(root, "M");
        Assert.AreEqual(1, MetricsCalculator.ComputeExecutableLines(method));
    }

    [TestMethod]
    public void ComputeSourceLines_CountsFullLineSpanInclusive()
    {
        var (root, _) = Compile("class C\n{\n    void M()\n    {\n        var x = 1;\n    }\n}");

        var method = FirstMethod(root, "M");

        // "void M()" through the closing "}" of the method body: 4 lines.
        Assert.AreEqual(4, MetricsCalculator.ComputeSourceLines(method));
    }

    [TestMethod]
    public void ComputeDepthOfInheritance_DeeperClassesHaveGreaterDepth()
    {
        var (root, model) = Compile(@"
            class A { }
            class B : A { }
            class C : B { }");

        var symbolA = (INamedTypeSymbol)model.GetDeclaredSymbol(FirstClass(root, "A"))!;
        var symbolB = (INamedTypeSymbol)model.GetDeclaredSymbol(FirstClass(root, "B"))!;
        var symbolC = (INamedTypeSymbol)model.GetDeclaredSymbol(FirstClass(root, "C"))!;

        var depthA = MetricsCalculator.ComputeDepthOfInheritance(symbolA);
        var depthB = MetricsCalculator.ComputeDepthOfInheritance(symbolB);
        var depthC = MetricsCalculator.ComputeDepthOfInheritance(symbolC);

        Assert.IsTrue(depthB > depthA, $"Expected depth(B)={depthB} > depth(A)={depthA}");
        Assert.IsTrue(depthC > depthB, $"Expected depth(C)={depthC} > depth(B)={depthB}");
    }

    [TestMethod]
    public void ComputeCoupledTypes_ReferencedTypeIsIncluded_SelfIsExcluded()
    {
        var (root, model) = Compile(@"
            class Bar { }
            class Foo
            {
                void M()
                {
                    Bar b = new Bar();
                }
            }");

        var fooClass = FirstClass(root, "Foo");
        var fooSymbol = (INamedTypeSymbol)model.GetDeclaredSymbol(fooClass)!;
        var method = FirstMethod(root, "M");

        var coupled = MetricsCalculator.ComputeCoupledTypes(model, method, fooSymbol);

        Assert.IsTrue(coupled.Any(t => t.Name == "Bar"), "Expected coupling to include 'Bar'.");
        Assert.IsFalse(coupled.Any(t => SymbolEqualityComparer.Default.Equals(t, fooSymbol)), "Self-coupling should be excluded.");
    }

    [TestMethod]
    public void ComputeMaintainabilityIndex_IsBoundedBetweenZeroAndOneHundred()
    {
        var (root, _) = Compile(@"
            class C
            {
                void Trivial()
                {
                    var x = 1;
                }

                void Complex(int n)
                {
                    for (var i = 0; i < n; i++)
                    {
                        if (i % 2 == 0 && i > 0)
                        {
                            for (var j = 0; j < i; j++)
                            {
                                if (j % 3 == 0 || j == 1)
                                {
                                    var y = j * i + (j > i ? 1 : -1);
                                }
                            }
                        }
                    }
                }
            }");

        var trivial = FirstMethod(root, "Trivial");
        var complex = FirstMethod(root, "Complex");

        var trivialCc = MetricsCalculator.ComputeCyclomaticComplexity(trivial);
        var trivialLoc = MetricsCalculator.ComputeExecutableLines(trivial);
        var complexCc = MetricsCalculator.ComputeCyclomaticComplexity(complex);
        var complexLoc = MetricsCalculator.ComputeExecutableLines(complex);

        var trivialMi = MetricsCalculator.ComputeMaintainabilityIndex(trivial, trivialCc, trivialLoc);
        var complexMi = MetricsCalculator.ComputeMaintainabilityIndex(complex, complexCc, complexLoc);

        Assert.IsTrue(trivialMi is >= 0 and <= 100);
        Assert.IsTrue(complexMi is >= 0 and <= 100);
        Assert.IsTrue(trivialMi > complexMi, $"Expected trivial MI ({trivialMi}) > complex MI ({complexMi}).");
    }
}
