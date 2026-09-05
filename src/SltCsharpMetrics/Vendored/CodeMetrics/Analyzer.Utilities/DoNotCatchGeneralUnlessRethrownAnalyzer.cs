using System.Collections.Immutable;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Analyzer.Utilities;

internal abstract class DoNotCatchGeneralUnlessRethrownAnalyzer : DiagnosticAnalyzer
{
	/// <summary>
	/// Walks an IOperation tree to find catch blocks that handle general types without rethrowing them.
	/// </summary>
	private sealed class DisallowGeneralCatchUnlessRethrowWalker : OperationWalker
	{
		private readonly Func<INamedTypeSymbol, bool> _isDisallowedCatchType;

		private readonly bool _checkAnonymousFunctions;

		private readonly Stack<bool> _seenRethrowInCatchClauses = new Stack<bool>();

		public ISet<ICatchClauseOperation> CatchClausesForDisallowedTypesWithoutRethrow { get; } = new HashSet<ICatchClauseOperation>();


		public DisallowGeneralCatchUnlessRethrowWalker(Func<INamedTypeSymbol, bool> isDisallowedCatchType, bool checkAnonymousFunctions)
		{
			_isDisallowedCatchType = isDisallowedCatchType;
			_checkAnonymousFunctions = checkAnonymousFunctions;
		}

		public override void VisitAnonymousFunction(IAnonymousFunctionOperation operation)
		{
			if (_checkAnonymousFunctions)
			{
				base.VisitAnonymousFunction(operation);
			}
		}

		public override void VisitCatchClause(ICatchClauseOperation operation)
		{
			_seenRethrowInCatchClauses.Push(item: false);
			Visit(operation.Filter);
			Visit(operation.Handler);
			if (!_seenRethrowInCatchClauses.Pop() && IsDisallowedCatch(operation) && !MightBeFilteringBasedOnTheCaughtException(operation))
			{
				CatchClausesForDisallowedTypesWithoutRethrow.Add(operation);
			}
		}

		public override void VisitThrow(IThrowOperation operation)
		{
			if (_seenRethrowInCatchClauses.Count > 0 && !_seenRethrowInCatchClauses.Peek())
			{
				_seenRethrowInCatchClauses.Pop();
				_seenRethrowInCatchClauses.Push(item: true);
			}
			base.VisitThrow(operation);
		}

		private bool IsDisallowedCatch(ICatchClauseOperation operation)
		{
			if (operation.ExceptionType is INamedTypeSymbol arg)
			{
				return _isDisallowedCatchType(arg);
			}
			return false;
		}

		private static bool MightBeFilteringBasedOnTheCaughtException(ICatchClauseOperation operation)
		{
			if (operation.ExceptionDeclarationOrExpression != null)
			{
				return operation.Filter != null;
			}
			return false;
		}
	}

	private readonly bool _shouldCheckLambdas;

	private readonly string? _enablingMethodAttributeFullyQualifiedName;

	private readonly bool _allowExcludedSymbolNames;

	private bool RequiresAttributeOnMethod => !string.IsNullOrEmpty(_enablingMethodAttributeFullyQualifiedName);

	protected DoNotCatchGeneralUnlessRethrownAnalyzer(bool shouldCheckLambdas, string? enablingMethodAttributeFullyQualifiedName = null, bool allowExcludedSymbolNames = false)
	{
		_shouldCheckLambdas = shouldCheckLambdas;
		_enablingMethodAttributeFullyQualifiedName = enablingMethodAttributeFullyQualifiedName;
		_allowExcludedSymbolNames = allowExcludedSymbolNames;
	}

	protected abstract Diagnostic CreateDiagnostic(IMethodSymbol containingMethod, SyntaxToken catchKeyword);

	protected virtual bool IsConfiguredDisallowedExceptionType(INamedTypeSymbol namedTypeSymbol, IMethodSymbol containingMethod, Compilation compilation, AnalyzerOptions analyzerOptions, CancellationToken cancellationToken)
	{
		return false;
	}

	public override void Initialize(AnalysisContext context)
	{
		context.EnableConcurrentExecution();
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze);
		context.RegisterCompilationStartAction(delegate(CompilationStartAnalysisContext compilationStartAnalysisContext)
		{
			CompilationStartAnalysisContext compilationStartAnalysisContext2 = compilationStartAnalysisContext;
			INamedTypeSymbol requiredAttributeType = null;
			if (!RequiresAttributeOnMethod || (requiredAttributeType = GetRequiredAttributeType(compilationStartAnalysisContext2.Compilation)) != null)
			{
				IReadOnlyCollection<INamedTypeSymbol> disallowedCatchTypes = GetDisallowedCatchTypes(compilationStartAnalysisContext2.Compilation);
				compilationStartAnalysisContext2.RegisterOperationBlockAction(delegate(OperationBlockAnalysisContext operationBlockAnalysisContext)
				{
					IMethodSymbol method;
					if (operationBlockAnalysisContext.OwningSymbol.Kind == SymbolKind.Method)
					{
						method = (IMethodSymbol)operationBlockAnalysisContext.OwningSymbol;
						if ((!RequiresAttributeOnMethod || method.HasAnyAttribute(requiredAttributeType)) && (!_allowExcludedSymbolNames || !operationBlockAnalysisContext.Options.IsConfiguredToSkipAnalysis(SupportedDiagnostics[0], method, operationBlockAnalysisContext.Compilation)))
						{
							ImmutableArray<IOperation>.Enumerator enumerator = operationBlockAnalysisContext.OperationBlocks.GetEnumerator();
							while (enumerator.MoveNext())
							{
								IOperation current = enumerator.Current;
								DisallowGeneralCatchUnlessRethrowWalker disallowGeneralCatchUnlessRethrowWalker = new DisallowGeneralCatchUnlessRethrowWalker(IsDisallowedCatchType, _shouldCheckLambdas);
								disallowGeneralCatchUnlessRethrowWalker.Visit(current);
								foreach (ICatchClauseOperation item in disallowGeneralCatchUnlessRethrowWalker.CatchClausesForDisallowedTypesWithoutRethrow)
								{
									operationBlockAnalysisContext.ReportDiagnostic(CreateDiagnostic(method, item.Syntax.GetFirstToken()));
								}
							}
						}
					}
					bool IsDisallowedCatchType(INamedTypeSymbol type)
					{
						if (!disallowedCatchTypes.Contains(type))
						{
							return IsConfiguredDisallowedExceptionType(type, method, compilationStartAnalysisContext2.Compilation, compilationStartAnalysisContext2.Options, compilationStartAnalysisContext2.CancellationToken);
						}
						return true;
					}
				});
			}
		});
	}

	private INamedTypeSymbol? GetRequiredAttributeType(Compilation compilation)
	{
		return compilation.GetOrCreateTypeByMetadataName(_enablingMethodAttributeFullyQualifiedName);
	}

	private static IReadOnlyCollection<INamedTypeSymbol> GetDisallowedCatchTypes(Compilation compilation)
	{
		return ImmutableHashSet.CreateRange(new INamedTypeSymbol[3]
		{
			compilation.GetSpecialType(SpecialType.System_Object),
			compilation.GetOrCreateTypeByMetadataName("System.Exception"),
			compilation.GetOrCreateTypeByMetadataName("System.SystemException")
		}.WhereNotNull());
	}
}
