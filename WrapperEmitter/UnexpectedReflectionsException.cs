using System.Reflection;

namespace WrapperEmitter;

public class UnexpectedReflectionsException : ApplicationException
{
    private UnexpectedReflectionsException(string message) : base(message) { }

    public UnexpectedReflectionsException(string code, Exception innerException)
      : base($"Caught Exception {innerException.Message} while processing \n{code}", innerException) {}

    public UnexpectedReflectionsException(Type delegateType, MethodInfo method, Exception innerException)
        : base($"Caught Exception creating {delegateType.FullTypeExpression()} delegate for {method}", innerException) {}

    public static UnexpectedReflectionsException CreatedObjectIsNull<T>()
        => new($"Dynamically Created object was null, expected {typeof(T).FullTypeExpression()}");

    public static UnexpectedReflectionsException FailedToFindType()
        => new("Failed to find Dynamically created type");

    public static UnexpectedReflectionsException FailedToGetAccessor()
        => new("Failed to get accessor method");

    public static UnexpectedReflectionsException FailedToGetEventHandlerType()
        => new("Failed to get event handler type");

    public static UnexpectedReflectionsException AmpersandFoundInTypeName(Type type, string expression)
        => new($"Ampersand found in type expression for {type} => {expression}");  // Do NOT call FullTypeExpression here, that is who throws this...

    public static UnexpectedReflectionsException PlusFoundInTypeName(Type type, string expression)
        => new($"Plus found in type expression for {type} => {expression}");  // Do NOT call FullTypeExpression here, that is who throws this...

    public static UnexpectedReflectionsException ArrayMissingElementType(Type type)
        => new($"{type} clams to be an array, but {nameof(type.GetElementType)} returned null");  // Do NOT call FullTypeExpression here, that is who throws this...

    public static UnexpectedReflectionsException NestedTypeMissingDeclaringType(Type type)
        => new($"{type} clams to be nested, but {nameof(type.DeclaringType)} returned null");  // Do NOT call FullTypeExpression here, that is who throws this...

    public static UnexpectedReflectionsException MissingDeclaringType(MethodInfo method)
        => new($"For Method {method} {nameof(method.DeclaringType)} yielded null");

    public static UnexpectedReflectionsException FailedToFindMethod(Type type, string name)
        => new($"Failed to find a method on {type.FullTypeExpression()} named {name}");

    public static UnexpectedReflectionsException DelegateMissingParameters(Type type)
        => new($"{type.FullTypeExpression()} does not have any parameters");

    public static void ThrowIfOpenGenericType(Type type, string alternative)
    {
        if (type.IsGenericTypeDefinition)
        {
            throw new UnexpectedReflectionsException($"{type.FullTypeExpression()} is an open generic type, used {alternative} instead");
        }
    }

    public static void ThrowIfNotOpenGenericType(Type type, string alternative)
    {
        if (!type.IsGenericTypeDefinition)
        {
            throw new UnexpectedReflectionsException($"{type.FullTypeExpression()} is not an open generic type, use {alternative} instead");
        }
    }

    public static void ThrowIfNotGenericType(Type type, string alternative)
    {
        if (!type.IsGenericType)
        {
            throw new UnexpectedReflectionsException($"{type.FullTypeExpression()} is not a generic type, use {alternative} instead");
        }
    }

    public static void ThrowIfNotCloseGenericType(Type type)
    {
        if (!type.IsGenericType || type.IsGenericTypeDefinition)
        {
            throw new UnexpectedReflectionsException($"{type.FullTypeExpression()} is not a closed generic type");
        }
    }

    public static void ThrowIfNotASubClassOfDelegate(Type type)
    {
        // Often you will see methods that include a check for type == typeof(Delegate)
        // but no, we really don't want that.
        if (!type.IsSubclassOf(typeof(Delegate)))
        {
            throw new UnexpectedReflectionsException($"{type.FullTypeExpression()} is not a delegate");
        }
    }

    public static void ThrowIfNotSuitableOverrideFactoryDelegateMethod<TBase, TSidecar>(MethodInfo invokeMethod, ParameterInfo[] parameters)
    {
        if (invokeMethod.ReturnType != typeof(TBase))
        {
            throw new UnexpectedReflectionsException($"{invokeMethod} does use the correct return type, expected {typeof(TBase).FullName}");
        }

        if (!parameters.Any())
        {
            throw new UnexpectedReflectionsException($"{invokeMethod} does not have any parameters, expected at least one");
        }

        if (parameters.Last().ParameterType != typeof(TSidecar))
        {
            throw new UnexpectedReflectionsException($"{invokeMethod} last parameter is not {typeof(TSidecar).FullName}");
        }
    }

    public static void ThrowIfNotOpenGenericMethod(MethodInfo method)
    {
        if (!method.IsGenericMethodDefinition)
        {
            throw new UnexpectedReflectionsException($"{method} is not an open generic method");
        }
    }

    public static void ThrowIfOpenGenericMethod(MethodInfo method)
    {
        if (method.IsGenericMethodDefinition)
        {
            throw new UnexpectedReflectionsException($"{method} is an open generic method");
        }
    }

    // TODO: (https://github.com/boxofyellow/WrapperEmitter/issues/3) Consider loosening these checks... many of these equal checks could be change
    //       to is assignable
    //       If we do that we should resist RestrictedHelper.DoesMethodMatch
    public static void ThrowIfMethodSignatureDoesNotMatch(Type returnType, Type[] parameterTypes, MethodInfo method)
    {
        if (returnType != method.ReturnType)
        {
            throw new UnexpectedReflectionsException($"Return type of {method} did not match {returnType} {method.ReturnType}");
        }

        List<Type> methodParameterTypes = new();
        if (!method.IsStatic)
        {
            methodParameterTypes.Add(method.DeclaringType ?? throw MissingDeclaringType(method));
        }
        methodParameterTypes.AddRange(method.GetParameters().Select(x => x.ParameterType));

        if (parameterTypes.Length != methodParameterTypes.Count)
        {
            throw new UnexpectedReflectionsException($"{method} has the wrong number of parameters");
        }

        int count = 0;
        foreach (var pType in methodParameterTypes)
        {
            if (pType != methodParameterTypes[count++])
            {
                throw new UnexpectedReflectionsException($"{method} has a parameter type mismatch");
            }
        }
    }

    public static void ThrowIfStatic(BindingFlags flags)
    {
        if (flags.HasFlag(BindingFlags.Static))
        {
            throw new UnexpectedReflectionsException($"{flags} is marked {BindingFlags.Static}");
        }
    }

    public static void ThrowIfNotStatic(BindingFlags flags)
    {
        if (!flags.HasFlag(BindingFlags.Static))
        {
            throw new UnexpectedReflectionsException($"{flags} is not marked {BindingFlags.Static}");
        }
    }
}