using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.CodeAnalysis;

namespace Analyzer.Utilities.Lightup;

internal static class LightupHelpers
{
	private static readonly ConcurrentDictionary<Type, ConcurrentDictionary<OperationKind, bool>> s_supportedOperationWrappers = new ConcurrentDictionary<Type, ConcurrentDictionary<OperationKind, bool>>();

	internal static bool CanWrapOperation(IOperation? operation, Type? underlyingType)
	{
		Type underlyingType2 = underlyingType;
		IOperation operation2 = operation;
		if (operation2 == null)
		{
			return true;
		}
		if (underlyingType2 == null)
		{
			return false;
		}
		ConcurrentDictionary<OperationKind, bool> orAdd = s_supportedOperationWrappers.GetOrAdd(underlyingType2, (Type _) => new ConcurrentDictionary<OperationKind, bool>());
		if (!orAdd.TryGetValue(operation2.Kind, out var value))
		{
			return orAdd.GetOrAdd(operation2.Kind, (OperationKind kind) => underlyingType2.GetTypeInfo().IsAssignableFrom(operation2.GetType().GetTypeInfo()));
		}
		return value;
	}

	internal static Func<TOperation, TProperty> CreateOperationPropertyAccessor<TOperation, TProperty>(Type? type, string propertyName, TProperty fallbackResult) where TOperation : IOperation
	{
		return CreatePropertyAccessor<TOperation, TProperty>(type, "operation", propertyName, fallbackResult);
	}

	internal static Func<TSyntax, TProperty> CreateSyntaxPropertyAccessor<TSyntax, TProperty>(Type? type, string propertyName, TProperty fallbackResult) where TSyntax : SyntaxNode
	{
		return CreatePropertyAccessor<TSyntax, TProperty>(type, "syntax", propertyName, fallbackResult);
	}

	internal static Func<TSymbol, TProperty> CreateSymbolPropertyAccessor<TSymbol, TProperty>(Type? type, string propertyName, TProperty fallbackResult) where TSymbol : ISymbol
	{
		return CreatePropertyAccessor<TSymbol, TProperty>(type, "symbol", propertyName, fallbackResult);
	}

	private static Func<T, TProperty> CreatePropertyAccessor<T, TProperty>(Type? type, string parameterName, string propertyName, TProperty fallbackResult)
	{
		TProperty fallbackResult2 = fallbackResult;
		if (!TryGetProperty<T, TProperty>(type, propertyName, out PropertyInfo propertyInfo))
		{
			return (T instance) => FallbackAccessor(instance, fallbackResult2);
		}
		ParameterExpression parameterExpression = Expression.Parameter(typeof(T), parameterName);
		Expression expression = Expression.Call(type.GetTypeInfo().IsAssignableFrom(typeof(T).GetTypeInfo()) ? ((Expression)parameterExpression) : ((Expression)Expression.Convert(parameterExpression, type)), propertyInfo.GetMethod);
		if (!typeof(TProperty).GetTypeInfo().IsAssignableFrom(propertyInfo.PropertyType.GetTypeInfo()))
		{
			expression = Expression.Convert(expression, typeof(TProperty));
		}
		return Expression.Lambda<Func<T, TProperty>>(expression, new ParameterExpression[1] { parameterExpression }).Compile();
		static TProperty FallbackAccessor(T instance, TProperty fallbackResult)
		{
			if (instance == null)
			{
				throw new NullReferenceException();
			}
			return fallbackResult;
		}
	}

	internal static Func<TSyntax, TProperty, TSyntax> CreateSyntaxWithPropertyAccessor<TSyntax, TProperty>(Type? type, string propertyName, TProperty fallbackResult) where TSyntax : SyntaxNode
	{
		return CreateWithPropertyAccessor<TSyntax, TProperty>(type, "syntax", propertyName, fallbackResult);
	}

	internal static Func<TSymbol, TProperty, TSymbol> CreateSymbolWithPropertyAccessor<TSymbol, TProperty>(Type? type, string propertyName, TProperty fallbackResult) where TSymbol : ISymbol
	{
		return CreateWithPropertyAccessor<TSymbol, TProperty>(type, "symbol", propertyName, fallbackResult);
	}

	private static Func<T, TProperty, T> CreateWithPropertyAccessor<T, TProperty>(Type? type, string parameterName, string propertyName, TProperty fallbackResult)
	{
		TProperty fallbackResult2 = fallbackResult;
		if (!TryGetProperty<T, TProperty>(type, propertyName, out PropertyInfo property))
		{
			return (T instance, TProperty value) => FallbackAccessor(instance, value, fallbackResult2);
		}
		MethodInfo methodInfo = type.GetTypeInfo().GetDeclaredMethods("With" + propertyName).SingleOrDefault((MethodInfo m) => !m.IsStatic && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType.Equals(property.PropertyType));
		if ((object)methodInfo == null)
		{
			return (T instance, TProperty value) => FallbackAccessor(instance, value, fallbackResult2);
		}
		ParameterExpression parameterExpression = Expression.Parameter(typeof(T), parameterName);
		ParameterExpression parameterExpression2 = Expression.Parameter(typeof(TProperty), methodInfo.GetParameters()[0].Name);
		Expression instance2 = (type.GetTypeInfo().IsAssignableFrom(typeof(T).GetTypeInfo()) ? ((Expression)parameterExpression) : ((Expression)Expression.Convert(parameterExpression, type)));
		Expression expression = (property.PropertyType.GetTypeInfo().IsAssignableFrom(typeof(TProperty).GetTypeInfo()) ? ((Expression)parameterExpression2) : ((Expression)Expression.Convert(parameterExpression2, property.PropertyType)));
		return Expression.Lambda<Func<T, TProperty, T>>(Expression.Call(instance2, methodInfo, expression), new ParameterExpression[2] { parameterExpression, parameterExpression2 }).Compile();
		static T FallbackAccessor(T instance, TProperty newValue, TProperty fallbackResult)
		{
			if (instance == null)
			{
				throw new NullReferenceException();
			}
			if (object.Equals(newValue, fallbackResult))
			{
				return instance;
			}
			throw new NotSupportedException();
		}
	}

	internal static Func<T, TArg, TValue> CreateAccessorWithArgument<T, TArg, TValue>(Type? type, string parameterName, Type argumentType, string argumentName, string methodName, TValue fallbackResult)
	{
		TValue fallbackResult2 = fallbackResult;
		if (!TryGetMethod<T, TValue>(type, methodName, out MethodInfo methodInfo))
		{
			return (T instance, TArg _) => FallbackAccessor(instance, fallbackResult2);
		}
		ParameterExpression parameterExpression = Expression.Parameter(typeof(T), parameterName);
		ParameterExpression parameterExpression2 = Expression.Parameter(typeof(TArg), argumentName);
		Expression instance2 = (type.GetTypeInfo().IsAssignableFrom(typeof(T).GetTypeInfo()) ? ((Expression)parameterExpression) : ((Expression)Expression.Convert(parameterExpression, type)));
		Expression expression = (argumentType.GetTypeInfo().IsAssignableFrom(typeof(TArg).GetTypeInfo()) ? ((Expression)parameterExpression2) : ((Expression)Expression.Convert(parameterExpression2, type)));
		Expression expression2 = Expression.Call(instance2, methodInfo, expression);
		if (!typeof(TValue).GetTypeInfo().IsAssignableFrom(methodInfo.ReturnType.GetTypeInfo()))
		{
			expression2 = Expression.Convert(expression2, typeof(TValue));
		}
		return Expression.Lambda<Func<T, TArg, TValue>>(expression2, new ParameterExpression[2] { parameterExpression, parameterExpression2 }).Compile();
		static TValue FallbackAccessor(T instance, TValue fallbackResult)
		{
			if (instance == null)
			{
				throw new NullReferenceException();
			}
			return fallbackResult;
		}
	}

	private static void VerifyTypeArgument<T>(Type type)
	{
		if (!typeof(T).GetTypeInfo().IsAssignableFrom(type.GetTypeInfo()))
		{
			throw new InvalidOperationException();
		}
	}

	private static void VerifyResultTypeCompatibility<TValue>(Type resultType)
	{
		if (!typeof(TValue).GetTypeInfo().IsAssignableFrom(resultType.GetTypeInfo()) && (!resultType.GetTypeInfo().IsEnum || !typeof(TValue).GetTypeInfo().IsEnum || !Enum.GetUnderlyingType(typeof(TValue)).GetTypeInfo().IsAssignableFrom(Enum.GetUnderlyingType(resultType).GetTypeInfo())))
		{
			throw new InvalidOperationException();
		}
	}

	private static bool TryGetProperty<T, TProperty>([NotNullWhen(true)] Type? type, string propertyName, [NotNullWhen(true)] out PropertyInfo? propertyInfo)
	{
		if ((object)type == null)
		{
			propertyInfo = null;
			return false;
		}
		VerifyTypeArgument<T>(type);
		propertyInfo = type.GetTypeInfo().GetDeclaredProperty(propertyName);
		if ((object)propertyInfo == null)
		{
			return false;
		}
		VerifyResultTypeCompatibility<TProperty>(propertyInfo.PropertyType);
		return true;
	}

	private static bool TryGetMethod<T, TReturn>([NotNullWhen(true)] Type? type, string methodName, [NotNullWhen(true)] out MethodInfo? methodInfo)
	{
		if ((object)type == null)
		{
			methodInfo = null;
			return false;
		}
		VerifyTypeArgument<T>(type);
		methodInfo = type.GetTypeInfo().GetDeclaredMethod(methodName);
		if ((object)methodInfo == null)
		{
			return false;
		}
		VerifyResultTypeCompatibility<TReturn>(methodInfo.ReturnType);
		return true;
	}
}
