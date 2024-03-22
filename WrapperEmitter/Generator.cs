using System.Reflection;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace WrapperEmitter;

/// <summary>
/// Implement this interface to gain control over injected C# code when creating a wrapped object that need to implement the provided interface  
/// </summary>
/// <typeparam name="TInterface">The interface that should be implemented by your wrapped object</typeparam>
/// <typeparam name="TImplementation">The object to which default behavior will be delegated to</typeparam>
/// <typeparam name="TSidecar">The sidecar you wish to use, it is complexly up you</typeparam>
public interface IInterfaceGenerator<TInterface, TImplementation, TSidecar> : IGenerator
    where TImplementation : TInterface
    where TInterface : class
{ }

/// <summary>
/// Implement this interface to gain control over injected C# code when creating a wrapped object that will used the provided base class
/// </summary>
/// <typeparam name="TBase">The class that should be used as the base class of the generated type</typeparam>
/// <typeparam name="TSidecar">The sidecar you wish to use, it is complexly up you</typeparam>
public interface IOverrideGenerator<TBase, TSidecar> : IGenerator
    where TBase : class
{ }

/// <summary>
/// The methods that can be used to control the injected C# for both `IInterfaceGenerator` and `IOverrideGenerator`
/// </summary>
public interface IGenerator
{
    /// <summary>
    /// This will be called for every method that _is_ overridable, returning false will skip including this method all together.
    /// Omitting methods that are truly abstract will result in generating an invalid class and an `InvalidCSharpException` exception will be thrown.
    /// NOTE: For methods that are not overridable (private, internal, sealed, ect.) this will not be called
    /// </summary>
    /// <param name="method">The method to check if it should be overwritten</param>
    /// <returns>true to override the method, false otherwise</returns>
    bool ShouldOverrideMethod(MethodInfo method) => true;

    /// <summary>
    /// For methods who's return type is Task (or Task<T>) they can be treated as async method (aka declared with async and use await, and return T).
    /// This will be called for methods that are being included (and have have the correct return type), if it returns true the method will be added
    /// to the class as an async method. 
    /// </summary>
    /// <param name="method">The method to check if it should be declared as an async method</param>
    /// <returns>true to treat the method as an async method</returns>
    bool TreatMethodAsync(MethodInfo method) => true;

    /// <summary>
    /// Used to interject arbitrary C# to run before the "implementation" of method.
    /// Returning null/string.Empty string will simply omit the call.  Any string returned must be valid C# code to run in that context (including a
    /// trailing ';' as needed)
    /// It has access to the method parameters values, the sidecar, (and for interfaces the fall back implementation)
    /// </summary>
    /// <param name="method">The method to consider</param>
    /// <returns>arbitrary C# code to inject or null/string.Empty to omit it</returns>
    string? PreMethodCall(MethodInfo method) => null;

    /// <summary>
    /// Used to replace the "default" behavior to implement the method.
    /// Returning null/string.Empty string will use "default" implementation,  Any string returned must be valid C# code to run in that context
    /// (including a trailing ';' as needed).  For non-void method this is expected to be the right-hand-side of an assignment for return value.  For
    /// void method it should be the full implantation.
    /// Much like ShouldOverrideMethod, returning null/string.Empty for abstract methods when creating an override wrapper would result in an invalid
    /// class and an `InvalidCSharpException` exception will be thrown.
    /// It has access to the method parameters values, the sidecar, (and for interfaces the fall back implementation)
    /// </summary>
    /// <param name="method">The method to consider</param>
    /// <returns>arbitrary C# code to inject or null/string.Empty use the default implementation</returns>
    string? ReplaceMethodCall(MethodInfo method) => null;

    /// <summary>
    /// Used to interject arbitrary C# to run after the "implementation" of method.
    /// Returning null/string.Empty string will simply omit the call.  Any string returned must be valid C# code to run in that context (including a
    /// trailing ';' as needed)
    /// It has access to the method parameters values, the sidecar, (for interfaces the fall back implementation), (and for non-void methods the
    /// return value yielded by the implementation via `Generated.ReturnVariableName`)
    /// </summary>
    /// <param name="method">The method to consider</param>
    /// <returns>arbitrary C# code to inject or null/string.Empty to omit it</returns>
    string? PostMethodCall(MethodInfo method) => null;

    /// <summary>
    /// This will be called for every property that _is_ overridable, returning false will skip
    /// including this property all together.  Omitting properties that are truly abstract will result in generating an invalid class and an 
    /// `InvalidCSharpException` exception will be thrown.
    /// NOTE: For properties that are not overridable (private, internal, sealed, ect.) this will not be called
    /// </summary>
    /// <param name="property">The property to check if it should be overwritten</param>
    /// <returns>true to override the property, false otherwise</returns>
    bool ShouldOverrideProperty(PropertyInfo property) => true;

    /// <summary>
    /// Used to interject arbitrary C# to run before the "implementation" of property as setter or getter.
    /// Returning null/string.Empty string will simply omit the call.  Any string returned must be valid C# code to run in that context (including a
    /// trailing ';' as needed)
    /// It has access to the sidecar, (and for interfaces the fall back implementation), (and for setters new value)
    /// </summary>
    /// <param name="property">The property to consider</param>
    /// <param name="forSet">When true this is for the properties' setter, when false it is for its getter</param>
    /// <returns>arbitrary C# code to inject or null/string.Empty to omit it</returns>
    string? PrePropertyCall(PropertyInfo property, bool forSet) => null;

    /// <summary>
    /// Used to replace the "default" behavior to implement the property as setter or getter.
    /// Returning null/string.Empty string will use "default" implementation,  Any string returned must be valid C# code to run in that context
    /// (including a trailing ';' as needed).  For setters this is expected to be the right-hand-side of an assignment for return value.  Much like
    /// ShouldOverrideProperty, returning null/string.Empty for abstract property when creating an override wrapper would result in an invalid
    /// class and an `InvalidCSharpException` exception will be thrown.
    /// It has access to the sidecar, (and for interfaces the fall back implementation), (and for setters new value)
    /// </summary>
    /// <param name="property">The property to consider</param>
    /// <param name="forSet">When true this is for the properties' setter, when false it is for its getter</param>
    /// <returns>arbitrary C# code to inject or null/string.Empty use the default implementation</returns>
    string? ReplacePropertyCall(PropertyInfo property, bool forSet) => null;

    /// <summary>
    /// Used to interject arbitrary C# to run after the "implementation" of property as setter or getter.
    /// Returning null/string.Empty string will simply omit the call.  Any string returned must be valid C# code to run in that context (including a
    /// trailing ';' as needed) 
    /// It has access to the sidecar, (and for interfaces the fall back implementation), (for setters new value), return value yielded by the
    /// implementation via `Generated.ReturnVariableName`
    /// </summary>
    /// <param name="property">The property to consider</param>
    /// <param name="forSet">When true this is for the properties' setter, when false it is for its getter</param>
    /// <returns>arbitrary C# code to inject or null/string.Empty to omit it</returns>
    string? PostPropertyCall(PropertyInfo property, bool forSet) => null;

    /// <summary>
    /// This will be called for every event that _is_ overridable, returning false will skip including this event all together.  Omitting events that
    /// are truly abstract will result in generating an invalid class and an `InvalidCSharpException` exception will be thrown.
    /// NOTE: For properties that are not overridable (private, internal, sealed, ect.) this will not be called
    /// </summary>
    /// <param name="event">The event to check if it should be overwritten</param>
    /// <returns>true to override the event, false otherwise</returns>
    bool ShouldOverrideEvent(EventInfo @event) => true;

    /// <summary>
    /// Used to interject arbitrary C# to run before the "implementation" of event as remover or adder.
    /// Returning null/string.Empty string will simply omit the call.  Any string returned must be valid C# code to run in that context (including a
    /// trailing ';' as needed)
    /// It has access to the sidecar, (for interfaces the fall back implementation), add the value being added or removed
    /// </summary>
    /// <param name="event">The event to consider</param>
    /// <param name="forRemove">When true this is for the event's remover, when false it is for it's adder</param>
    /// <returns>arbitrary C# code to inject or null/string.Empty to omit it</returns>    
    string? PreEventCall(EventInfo @event, bool forRemove) => null;

    /// <summary>
    /// Used to replace the "default" behavior to implement the event as remover or adder.
    /// Returning null/string.Empty string will use "default" implementation,  Any string returned must be valid C# code to run in that context
    /// (including a trailing ';' as needed). Much like ShouldOverrideEvent, returning null/string.Empty for abstract event when creating an
    /// override wrapper would result in an invalid class and an `InvalidCSharpException` exception will be thrown. It has access to the sidecar,
    /// (and for interfaces the fall back implementation), add the value being added or removed
    /// </summary>
    /// <param name="event">The event to consider</param>
    /// <param name="forRemove">When true this is for the event's remover, when false it is for it's adder</param>
    /// <returns>arbitrary C# code to inject or null/string.Empty use the default implementation</returns>
    string? ReplaceEventCall(EventInfo @event, bool forRemove) => null;

    /// <summary>
    /// Used to interject arbitrary C# to run after the "implementation" of event as remover or adder.
    /// Returning null/string.Empty string will simply omit the call.  Any string returned must be valid C# code to run in that context (including a
    /// trailing ';' as needed)
    /// It has access to the sidecar, (and for interfaces the fall back implementation), add the value being added or removed
    /// </summary>
    /// <param name="property">The event to consider</param>
    /// <param name="forRemove">When true this is for the event's remover, when false it is for it's adder</param>
    /// <returns>arbitrary C# code to inject or null/string.Empty to omit it</returns>
    string? PostEventCall(EventInfo @event, bool forRemove) => null;

    /// <summary>
    /// Used to include assembly references for types used within your injected C# code.  A few notes
    ///   - Types for the Interface being implemented or base class being Sub-classed, and the sidecar already handled
    ///   - You only need to include types they appear solely in your injected code
    ///   - You don't need to worry about duping this list that will be handled internally
    /// This will be invoked after the code generation is completed allowing you the opertunity to collect the types through that processes.  That is
    /// however often not strictly needed, if there is small list of know types that might be included you could return a static list
    /// </summary>
    /// <returns>Additional types would assembly references will need to be included to compile the generated class</returns>
    IEnumerable<Type> ExtraTypes => Array.Empty<Type>();

    /// <summary>
    /// Any optional Parsing options required the generated class
    /// </summary>
    CSharpParseOptions? ParseOptions => null;
}


/// <summary>
/// This class holds the logic for generating/caching the dynamic Types and instantiating instances of those types
/// 
/// This file holds those methods that for are expected to interact with under normal usage, other public methods are public to aid in testing  
/// </summary>
public static partial class Generator
{
    /// <summary>
    /// A Prefix use to help ensure things injected do conflict with variable/methods that already exist
    /// </summary>
    public const string VariablePrefix = "___";  // Avoid Microsoft's __

    /// <summary>
    /// The name of the member variable that can be used within any of the injection points to reference the sidecar
    /// </summary>
    public const string SidecarVariableName = $"{VariablePrefix}m_sidecar";

    /// <summary>
    /// Only used for Interface wrapper, the name of the member variable that can be used within any of the injection points to reference the
    /// implementation were default logic will be delegated to.
    /// </summary>
    public const string ImplementationVariableName = $"{VariablePrefix}m_implementation";

    /// <summary>
    /// For methods with a return values and property getters, the name of local variable that will hold the return value.  This variable will only
    /// be in scope for the PostXYZCalls  
    /// </summary>
    public const string ReturnVariableName = $"{VariablePrefix}result";

    public const string DefaultNamespace = $"{VariablePrefix}Generated_Namespace";
    public const string DefaultClassName = $"{VariablePrefix}Generated_ClassName";


    public readonly static (Type Type, object? Value)[] NoParams = Array.Empty<(Type Type, object? Value)>();

    // We need NonPublic here to pick up protected
    // TODO: Can interfaces have protected items?  If so does that work?
    private const BindingFlags c_bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    /// <summary>
    /// Used to create a wrapper object that fulfills an Interface contract from the provided generator that delegates its work the interface
    /// <param name="generator">The generator to use</param>
    /// <param name="implementation">The implementation to used by default to delegate work too</param>
    /// <param name="sidecar">The side care to use for the new object</param>
    /// <param name="code">This output parameter will hold the generated code</param>
    /// <param name="namespace">The name of the namespace to put this new type in (if not provided `DefaultNamespace` will be used)</param>
    /// <param name="className">The name of the class name use for the new type in (if not provided `DefaultClassName` will be used)</param>
    /// <param name="logger">Optional logger to track progress</param>
    /// <param name="logLevel">Optional log level to use with the logger</param>
    /// </summary>
    public static TInterface CreateInterfaceImplementation<TInterface, TImplementation, TSidecar>(
        this IInterfaceGenerator<TInterface, TImplementation, TSidecar> generator,
        TImplementation implementation,
        TSidecar sidecar,
        out string code,
        string? @namespace = null,
        string? className = null,
        ILogger? logger = null,
        LogLevel logLevel = LogLevel.Information
    )
        where TImplementation : TInterface
        where TInterface : class
    {
        InvalidCSharpException.ThrowIfNotAnInterface<TInterface>(nameof(TInterface));
        @namespace ??= DefaultNamespace;
        className ??= DefaultClassName;
        logger ??= NullLogger.Instance;

        DateTime time = DateTime.UtcNow;
        code = GenerateCodeForInterface(generator, @namespace, className);
        logger.Log(logLevel, "Completed Code Generation: {duration}", DateTime.UtcNow - time);

        return CreateObject<TInterface>(
            generator,
            code,
            @namespace,
            className,
            constructorValues: new object?[] {implementation, sidecar},
            extraTypes: new [] {typeof(object), typeof(TInterface), typeof(TImplementation), typeof(TSidecar)},
            logger,
            logLevel);
    }

    /// <summary>
    /// Used to create a wrapper object that fulfills an bass class contract from the provided generator that delegates its work to that base class
    /// <param name="generator">The generator to use</param>
    /// <param name="constructorArguments">A listing of paired (type and value) for the constructor of the base class that should be used to create the object</param>
    /// <param name="sidecar">The side care to use for the new object</param>
    /// <param name="code">This output parameter will hold the generated code</param>
    /// <param name="namespace">The name of the namespace to put this new type in (if not provided `DefaultNamespace` will be used)</param>
    /// <param name="className">The name of the class name use for the new type in (if not provided `DefaultClassName` will be used)</param>
    /// <param name="logger">Optional logger to track progress</param>
    /// <param name="logLevel">Optional log level to use with the logger</param>
    /// </summary>
    public static TBase CreateOverrideImplementation<TBase, TSidecar>(
        this IOverrideGenerator<TBase, TSidecar> generator,
        // TODO: Add tests with constructor arguments (with out/ref) 
        IEnumerable<(Type Type, object? Value)> constructorArguments,
        TSidecar sidecar,
        out string code,
        string? @namespace = null,
        string? className = null,
        ILogger? logger = null,
        LogLevel logLevel = LogLevel.Information
    )
        where TBase : class
    {
        InvalidCSharpException.ThrowIfIsAnInterface<TBase>(nameof(TBase));
        // TODO: Add a check for sealed

        // TODO: Check that constructorArguments is valid

        logger ??= NullLogger.Instance;
        @namespace ??= DefaultNamespace;
        className ??= DefaultClassName;

        ConstructorInfo constructor = typeof(TBase).GetConstructor(c_bindingFlags, constructorArguments.Select(x => x.Type).ToArray())
            ?? throw new InvalidCSharpException($"Failed to find constructor on {typeof(TBase).FullTypeExpression} with the following parameters {string.Join(", ", constructorArguments.Select(x => x.Type.FullTypeExpression()))}");

        if (constructor.IsPrivate || constructor.IsAssembly)
        {
            throw new InvalidCSharpException($"Failed to find usable constructor on {typeof(TBase).FullTypeExpression} with the following parameters {string.Join(", ", constructorArguments.Select(x => x.Type.FullTypeExpression()))}");
        }

        DateTime time = DateTime.UtcNow;
        code = GenerateCodeForOverride(generator, @namespace, className, constructor);
        logger.Log(logLevel, "Completed Code Generation: {duration}", DateTime.UtcNow - time);

        return CreateObject<TBase>(
            generator,
            code,
            @namespace,
            className,
            constructorValues: constructorArguments.Select(x => x.Value).Append(sidecar).ToArray(),
            extraTypes: new[] { typeof(object), typeof(TBase), typeof(TSidecar) },
            logger,
            logLevel);
    }

    /// <summary>
    /// Helper method - for determining if a method should be treated as void (has no return value) and or should be treated as async (and should use await)
    /// </summary>
    /// <param name="generator">the generated to check</param>
    /// <param name="method">the method to check</param>
    /// <returns>
    /// <param name="IsVoid">True when the method is void, or should be treated that way</param>
    /// <param name="IsAsync">True when the method should be treated as async</param>
    /// </returns>
    public static (bool IsVoid, bool IsAsync) TreatAs(this IGenerator generator, MethodInfo method)
    {
        Type returnType = method.ReturnType;
        var isVoid = returnType == typeof(void);
        var isAsync = returnType == typeof(Task) || (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>));
        if (isAsync)
        {
            isAsync = generator.TreatMethodAsync(method);
        }
        if (isAsync && returnType == typeof(Task))
        {
            // TODO: Comment about why we are doing this
            isVoid = true;
        }
        return (isVoid, isAsync);
    }
}
