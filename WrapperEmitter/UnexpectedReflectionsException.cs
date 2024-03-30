using System.Reflection;

namespace WrapperEmitter;

public class UnexpectedReflectionsException : ApplicationException
{
    private UnexpectedReflectionsException(string message) : base(message) { }

    public UnexpectedReflectionsException(string code, Exception innerException)
      : base($"Caught Exception {innerException.Message} while processing \n{code}", innerException) {}

    public static UnexpectedReflectionsException CreatedObjectIsNull<T>()
        => new($"Dynamically Created object was null, expected {typeof(T).FullTypeExpression}");

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

    public static UnexpectedReflectionsException ByRefMissingElementType(Type type)
        => new($"{type} clams to be an by ref, but {nameof(type.GetElementType)} returned null");  // Do NOT call FullTypeExpression here, We might start using it there.

    public static UnexpectedReflectionsException NestedTypeMissingDeclaringType(Type type)
        => new($"{type} clams to be nested, but {nameof(type.DeclaringType)} returned null");  // Do NOT call FullTypeExpression here, that is who throws this...
}