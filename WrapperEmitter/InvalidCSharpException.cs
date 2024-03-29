using System.Reflection;
using System.Text;

namespace WrapperEmitter;

public class InvalidCSharpException : ApplicationException
{
    public InvalidCSharpException(string message) : base(message) { }

    public InvalidCSharpException(StringBuilder code, Exception innerException)
      : base($"Caught Exception {innerException.Message} while processing \n{code}", innerException) {}

    public static void ThrowIfNotAnInterface<T>(string name)
    {
        if (!typeof(T).IsInterface)
        {
            throw new InvalidCSharpException($"{name} MUST be an interface, got {typeof(T).FullTypeExpression()}");
        }
    }

    public static void ThrowIfIsAnInterface<T>(string name)
    {
        if (typeof(T).IsInterface)
        {
            throw new InvalidCSharpException($"{name} must NOT be an interface, got {typeof(T).FullTypeExpression()}");
        }
    }

    public static void ThrowIfSealed<T>(string name)
    {
        if (typeof(T).IsSealed)
        {
            throw new InvalidCSharpException($"{name} must NOT be sealed, got {typeof(T).FullTypeExpression()}");
        }
    }

    public static void ThrowIfNotAssignable(int i, ConstructorArgument argument)
    {
        if (!argument.IsAssignable())
        {
            throw new InvalidCSharpException($"Position: {i} Value {argument.Value?.GetType().FullTypeExpression()} can't be assigned to {argument.Type.FullTypeExpression()}");
        }
    }

    public static InvalidCSharpException CannotAccessDefaultProtectedInterfaceAccessor(string name)
        => new InvalidCSharpException($"{name} cannot access default protected interface accessor.");

    public static InvalidCSharpException UnAccessibleMethod(MethodInfo methodInfo)
        => new InvalidCSharpException($"{methodInfo.Name} on {methodInfo.ReflectedType?.FullTypeExpression()} is not accessible.");

    public static void ThrowIfMethodIsAbstract(MethodInfo methodInfo, bool requireReplacementImplementation)
        => ThrowIfAbstract(methodInfo, requireReplacementImplementation, nameof(IGenerator.ShouldOverrideMethod), nameof(IGenerator.ReplaceMethodCall), @for: null);

    public static void ThrowIfPropertyIsAbstract(MethodInfo methodInfo, bool requireReplacementImplementation, bool? forSet = null)
        => ThrowIfAbstract(methodInfo, requireReplacementImplementation, nameof(IGenerator.ShouldOverrideProperty), nameof(IGenerator.ReplacePropertyCall), $" ({nameof(forSet)}:{forSet}) ");

    public static void ThrowIfEventIsAbstract(MethodInfo methodInfo, bool requireReplacementImplementation, bool? forRemove = null)
        => ThrowIfAbstract(methodInfo, requireReplacementImplementation, nameof(IGenerator.ShouldOverrideEvent), nameof(IGenerator.ReplaceEventCall), $" ({nameof(forRemove)}:{forRemove}) ");

    private static void ThrowIfAbstract(MethodInfo methodInfo, bool requireReplacementImplementation, string shouldName, string replaceName, string? @for)
    {
        if (methodInfo.IsAbstract)
        {
            throw new InvalidCSharpException($"{methodInfo.Name} on {methodInfo.ReflectedType?.FullTypeExpression()} is Abstract {shouldName}{@for} should return true{(requireReplacementImplementation ? $" and {replaceName}{@for} should provide an implementation" : null)}.");
        }
    }
}