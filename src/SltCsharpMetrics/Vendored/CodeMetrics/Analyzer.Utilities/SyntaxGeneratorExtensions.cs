using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Editing;

namespace Analyzer.Utilities;

internal static class SyntaxGeneratorExtensions
{
	private const string LeftIdentifierName = "left";

	private const string RightIdentifierName = "right";

	private const string ReferenceEqualsMethodName = "ReferenceEquals";

	private const string EqualsMethodName = "Equals";

	private const string CompareToMethodName = "CompareTo";

	private const string SystemNotImplementedExceptionTypeName = "System.NotImplementedException";

	/// <summary>
	/// Creates a default declaration for an operator equality overload.
	/// </summary>
	/// <param name="generator">
	/// The <see cref="T:Microsoft.CodeAnalysis.Editing.SyntaxGenerator" /> used to create the declaration.
	/// </param>
	/// <param name="containingType">
	/// A symbol specifying the type of the operands of the comparison operator.
	/// </param>
	/// <returns>
	/// A <see cref="T:Microsoft.CodeAnalysis.SyntaxNode" /> representing the declaration.
	/// </returns>
	public static SyntaxNode DefaultOperatorEqualityDeclaration(this SyntaxGenerator generator, INamedTypeSymbol containingType)
	{
		SyntaxNode syntaxNode = generator.IdentifierName("left");
		SyntaxNode syntaxNode2 = generator.IdentifierName("right");
		List<SyntaxNode> list = new List<SyntaxNode>();
		if (containingType.TypeKind == TypeKind.Class)
		{
			list.Add(generator.IfStatement(generator.InvocationExpression(generator.IdentifierName("ReferenceEquals"), syntaxNode, generator.NullLiteralExpression()), new SyntaxNode[1] { generator.ReturnStatement(generator.InvocationExpression(generator.IdentifierName("ReferenceEquals"), syntaxNode2, generator.NullLiteralExpression())) }));
		}
		list.Add(generator.ReturnStatement(generator.InvocationExpression(generator.MemberAccessExpression(syntaxNode, "Equals"), syntaxNode2)));
		return generator.ComparisonOperatorDeclaration(OperatorKind.Equality, containingType, list.ToArray());
	}

	/// <summary>
	/// Creates a reference to a named type suitable for use in accessing a static member of the type.
	/// </summary>
	/// <param name="generator">The <see cref="T:Microsoft.CodeAnalysis.Editing.SyntaxGenerator" /> used to create the type reference.</param>
	/// <param name="typeSymbol">The named type to reference.</param>
	/// <returns>A <see cref="T:Microsoft.CodeAnalysis.SyntaxNode" /> representing the type reference expression.</returns>
	public static SyntaxNode TypeExpressionForStaticMemberAccess(this SyntaxGenerator generator, INamedTypeSymbol typeSymbol)
	{
		int rawKind = generator.QualifiedName(generator.IdentifierName("ignored"), generator.IdentifierName("ignored")).RawKind;
		int rawKind2 = generator.MemberAccessExpression(generator.IdentifierName("ignored"), "ignored").RawKind;
		SyntaxNode expression2 = generator.TypeExpression(typeSymbol);
		return QualifiedNameToMemberAccess(rawKind, rawKind2, expression2, generator);
		static SyntaxNode QualifiedNameToMemberAccess(int qualifiedNameSyntaxKind, int memberAccessExpressionSyntaxKind, SyntaxNode expression, SyntaxGenerator generator)
		{
			if (expression.RawKind == qualifiedNameSyntaxKind)
			{
				SyntaxNode expression3 = QualifiedNameToMemberAccess(qualifiedNameSyntaxKind, memberAccessExpressionSyntaxKind, expression.ChildNodes().First(), generator);
				SyntaxNode memberName = expression.ChildNodes().Last();
				return generator.MemberAccessExpression(expression3, memberName);
			}
			return expression;
		}
	}

	/// <summary>
	/// Creates a default declaration for an operator inequality overload.
	/// </summary>
	/// <param name="generator">
	/// The <see cref="T:Microsoft.CodeAnalysis.Editing.SyntaxGenerator" /> used to create the declaration.
	/// </param>
	/// <param name="containingType">
	/// A symbol specifying the type of the operands of the comparison operator.
	/// </param>
	/// <returns>
	/// A <see cref="T:Microsoft.CodeAnalysis.SyntaxNode" /> representing the declaration.
	/// </returns>
	public static SyntaxNode DefaultOperatorInequalityDeclaration(this SyntaxGenerator generator, INamedTypeSymbol containingType)
	{
		SyntaxNode left = generator.IdentifierName("left");
		SyntaxNode right = generator.IdentifierName("right");
		SyntaxNode syntaxNode = generator.ReturnStatement(generator.LogicalNotExpression(generator.ValueEqualsExpression(left, right)));
		return generator.ComparisonOperatorDeclaration(OperatorKind.Inequality, containingType, syntaxNode);
	}

	/// <summary>
	/// Creates a default declaration for an operator less than overload.
	/// </summary>
	/// <param name="generator">
	/// The <see cref="T:Microsoft.CodeAnalysis.Editing.SyntaxGenerator" /> used to create the declaration.
	/// </param>
	/// <param name="containingType">
	/// A symbol specifying the type of the operands of the comparison operator.
	/// </param>
	/// <returns>
	/// A <see cref="T:Microsoft.CodeAnalysis.SyntaxNode" /> representing the declaration.
	/// </returns>
	public static SyntaxNode DefaultOperatorLessThanDeclaration(this SyntaxGenerator generator, INamedTypeSymbol containingType)
	{
		SyntaxNode syntaxNode = generator.IdentifierName("left");
		SyntaxNode syntaxNode2 = generator.IdentifierName("right");
		SyntaxNode expression = ((containingType.TypeKind != TypeKind.Class) ? generator.LessThanExpression(generator.InvocationExpression(generator.MemberAccessExpression(syntaxNode, generator.IdentifierName("CompareTo")), syntaxNode2), generator.LiteralExpression(0)) : generator.ConditionalExpression(generator.InvocationExpression(generator.IdentifierName("ReferenceEquals"), syntaxNode, generator.NullLiteralExpression()), generator.LogicalNotExpression(generator.InvocationExpression(generator.IdentifierName("ReferenceEquals"), syntaxNode2, generator.NullLiteralExpression())), generator.LessThanExpression(generator.InvocationExpression(generator.MemberAccessExpression(syntaxNode, generator.IdentifierName("CompareTo")), syntaxNode2), generator.LiteralExpression(0))));
		SyntaxNode syntaxNode3 = generator.ReturnStatement(expression);
		return generator.ComparisonOperatorDeclaration(OperatorKind.LessThan, containingType, syntaxNode3);
	}

	/// <summary>
	/// Creates a default declaration for an operator less than or equal overload.
	/// </summary>
	/// <param name="generator">
	/// The <see cref="T:Microsoft.CodeAnalysis.Editing.SyntaxGenerator" /> used to create the declaration.
	/// </param>
	/// <param name="containingType">
	/// A symbol specifying the type of the operands of the comparison operator.
	/// </param>
	/// <returns>
	/// A <see cref="T:Microsoft.CodeAnalysis.SyntaxNode" /> representing the declaration.
	/// </returns>
	public static SyntaxNode DefaultOperatorLessThanOrEqualDeclaration(this SyntaxGenerator generator, INamedTypeSymbol containingType)
	{
		SyntaxNode syntaxNode = generator.IdentifierName("left");
		SyntaxNode syntaxNode2 = generator.IdentifierName("right");
		SyntaxNode expression = ((containingType.TypeKind != TypeKind.Class) ? generator.LessThanOrEqualExpression(generator.InvocationExpression(generator.MemberAccessExpression(syntaxNode, generator.IdentifierName("CompareTo")), syntaxNode2), generator.LiteralExpression(0)) : generator.LogicalOrExpression(generator.InvocationExpression(generator.IdentifierName("ReferenceEquals"), syntaxNode, generator.NullLiteralExpression()), generator.LessThanOrEqualExpression(generator.InvocationExpression(generator.MemberAccessExpression(syntaxNode, generator.IdentifierName("CompareTo")), syntaxNode2), generator.LiteralExpression(0))));
		SyntaxNode syntaxNode3 = generator.ReturnStatement(expression);
		return generator.ComparisonOperatorDeclaration(OperatorKind.LessThanOrEqual, containingType, syntaxNode3);
	}

	/// <summary>
	/// Creates a default declaration for an operator greater than overload.
	/// </summary>
	/// <param name="generator">
	/// The <see cref="T:Microsoft.CodeAnalysis.Editing.SyntaxGenerator" /> used to create the declaration.
	/// </param>
	/// <param name="containingType">
	/// A symbol specifying the type of the operands of the comparison operator.
	/// </param>
	/// <returns>
	/// A <see cref="T:Microsoft.CodeAnalysis.SyntaxNode" /> representing the declaration.
	/// </returns>
	public static SyntaxNode DefaultOperatorGreaterThanDeclaration(this SyntaxGenerator generator, INamedTypeSymbol containingType)
	{
		SyntaxNode syntaxNode = generator.IdentifierName("left");
		SyntaxNode syntaxNode2 = generator.IdentifierName("right");
		SyntaxNode expression = ((containingType.TypeKind != TypeKind.Class) ? generator.GreaterThanExpression(generator.InvocationExpression(generator.MemberAccessExpression(syntaxNode, generator.IdentifierName("CompareTo")), syntaxNode2), generator.LiteralExpression(0)) : generator.LogicalAndExpression(generator.LogicalNotExpression(generator.InvocationExpression(generator.IdentifierName("ReferenceEquals"), syntaxNode, generator.NullLiteralExpression())), generator.GreaterThanExpression(generator.InvocationExpression(generator.MemberAccessExpression(syntaxNode, generator.IdentifierName("CompareTo")), syntaxNode2), generator.LiteralExpression(0))));
		SyntaxNode syntaxNode3 = generator.ReturnStatement(expression);
		return generator.ComparisonOperatorDeclaration(OperatorKind.GreaterThan, containingType, syntaxNode3);
	}

	/// <summary>
	/// Creates a default declaration for an operator greater than or equal overload.
	/// </summary>
	/// <param name="generator">
	/// The <see cref="T:Microsoft.CodeAnalysis.Editing.SyntaxGenerator" /> used to create the declaration.
	/// </param>
	/// <param name="containingType">
	/// A symbol specifying the type of the operands of the comparison operator.
	/// </param>
	/// <returns>
	/// A <see cref="T:Microsoft.CodeAnalysis.SyntaxNode" /> representing the declaration.
	/// </returns>
	public static SyntaxNode DefaultOperatorGreaterThanOrEqualDeclaration(this SyntaxGenerator generator, INamedTypeSymbol containingType)
	{
		SyntaxNode syntaxNode = generator.IdentifierName("left");
		SyntaxNode syntaxNode2 = generator.IdentifierName("right");
		SyntaxNode expression = ((containingType.TypeKind != TypeKind.Class) ? generator.GreaterThanOrEqualExpression(generator.InvocationExpression(generator.MemberAccessExpression(syntaxNode, generator.IdentifierName("CompareTo")), syntaxNode2), generator.LiteralExpression(0)) : generator.ConditionalExpression(generator.InvocationExpression(generator.IdentifierName("ReferenceEquals"), syntaxNode, generator.NullLiteralExpression()), generator.InvocationExpression(generator.IdentifierName("ReferenceEquals"), syntaxNode2, generator.NullLiteralExpression()), generator.GreaterThanOrEqualExpression(generator.InvocationExpression(generator.MemberAccessExpression(syntaxNode, generator.IdentifierName("CompareTo")), syntaxNode2), generator.LiteralExpression(0))));
		SyntaxNode syntaxNode3 = generator.ReturnStatement(expression);
		return generator.ComparisonOperatorDeclaration(OperatorKind.GreaterThanOrEqual, containingType, syntaxNode3);
	}

	private static SyntaxNode ComparisonOperatorDeclaration(this SyntaxGenerator generator, OperatorKind operatorKind, INamedTypeSymbol containingType, params SyntaxNode[] statements)
	{
		return generator.OperatorDeclaration(operatorKind, new SyntaxNode[2]
		{
			generator.ParameterDeclaration("left", generator.TypeExpression(containingType)),
			generator.ParameterDeclaration("right", generator.TypeExpression(containingType))
		}, generator.TypeExpression(SpecialType.System_Boolean), Accessibility.Public, DeclarationModifiers.Static, statements);
	}

	/// <summary>
	/// Creates a default declaration for an override of <see cref="M:System.Object.Equals(System.Object)" />.
	/// </summary>
	/// <param name="generator">
	/// The <see cref="T:Microsoft.CodeAnalysis.Editing.SyntaxGenerator" /> used to create the declaration.
	/// </param>
	/// <param name="compilation">The compilation</param>
	/// <param name="containingType">
	/// A symbol specifying the type in which the declaration is to be created.
	/// </param>
	/// <returns>
	/// A <see cref="T:Microsoft.CodeAnalysis.SyntaxNode" /> representing the declaration.
	/// </returns>
	public static SyntaxNode DefaultEqualsOverrideDeclaration(this SyntaxGenerator generator, Compilation compilation, INamedTypeSymbol containingType)
	{
		SyntaxNode syntaxNode = generator.IdentifierName("obj");
		List<SyntaxNode> list = new List<SyntaxNode>();
		if (containingType.TypeKind == TypeKind.Class)
		{
			list.AddRange(new SyntaxNode[2]
			{
				generator.IfStatement(generator.InvocationExpression(generator.IdentifierName("ReferenceEquals"), generator.ThisExpression(), syntaxNode), new SyntaxNode[1] { generator.ReturnStatement(generator.TrueLiteralExpression()) }),
				generator.IfStatement(generator.InvocationExpression(generator.IdentifierName("ReferenceEquals"), syntaxNode, generator.NullLiteralExpression()), new SyntaxNode[1] { generator.ReturnStatement(generator.FalseLiteralExpression()) })
			});
		}
		list.AddRange(generator.DefaultMethodBody(compilation));
		return generator.MethodDeclaration("Equals", new SyntaxNode[1] { generator.ParameterDeclaration(syntaxNode.ToString(), generator.TypeExpression(SpecialType.System_Object)) }, null, generator.TypeExpression(SpecialType.System_Boolean), Accessibility.Public, DeclarationModifiers.Override, list);
	}

	/// <summary>
	/// Creates a default declaration for an override of <see cref="M:System.Object.GetHashCode" />.
	/// </summary>
	/// <param name="generator">
	/// The <see cref="T:Microsoft.CodeAnalysis.Editing.SyntaxGenerator" /> used to create the declaration.
	/// </param>
	/// <param name="compilation">The compilation</param>
	/// <returns>
	/// A <see cref="T:Microsoft.CodeAnalysis.SyntaxNode" /> representing the declaration.
	/// </returns>
	public static SyntaxNode DefaultGetHashCodeOverrideDeclaration(this SyntaxGenerator generator, Compilation compilation)
	{
		return generator.MethodDeclaration("GetHashCode", null, null, generator.TypeExpression(SpecialType.System_Int32), Accessibility.Public, DeclarationModifiers.Override, generator.DefaultMethodBody(compilation));
	}

	/// <summary>
	/// Creates a default set of statements to place within a generated method body.
	/// </summary>
	/// <param name="generator">
	/// The <see cref="T:Microsoft.CodeAnalysis.Editing.SyntaxGenerator" /> used to create the statements.
	/// </param>
	/// <param name="compilation">The compilation</param>
	/// <returns>
	/// An sequence containing a single statement that throws <see cref="T:System.NotImplementedException" />.
	/// </returns>
	public static IEnumerable<SyntaxNode> DefaultMethodBody(this SyntaxGenerator generator, Compilation compilation)
	{
		yield return generator.DefaultMethodStatement(compilation);
	}

	public static SyntaxNode DefaultMethodStatement(this SyntaxGenerator generator, Compilation compilation)
	{
		return generator.ThrowStatement(generator.ObjectCreationExpression(generator.TypeExpression(compilation.GetOrCreateTypeByMetadataName("System.NotImplementedException"))));
	}

	public static SyntaxNode? TryGetContainingDeclaration(this SyntaxGenerator generator, SyntaxNode? node, DeclarationKind kind)
	{
		if (node == null)
		{
			return null;
		}
		for (DeclarationKind declarationKind = generator.GetDeclarationKind(node); declarationKind != kind; declarationKind = generator.GetDeclarationKind(node))
		{
			node = generator.GetDeclaration(node.Parent);
			if (node == null)
			{
				return null;
			}
		}
		return node;
	}
}
