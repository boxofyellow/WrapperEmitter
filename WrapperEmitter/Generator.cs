using System.Reflection;
using Microsoft.CodeAnalysis;
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
    /// <param name="support">A GeneratorSupport that the generators are allowed to uses get compile time access those methods and types</param>
    /// <returns>arbitrary C# code to inject or null/string.Empty to omit it</returns>
    string? PreMethodCall(MethodInfo method, GeneratorSupport support) => null;

    /// <summary>
    /// Used to replace the "default" behavior to implement the method.
    /// Returning null/string.Empty string will use "default" implementation,  Any string returned must be valid C# code to run in that context
    /// (including a trailing ';' as needed).  For non-void method this is expected to be the right-hand-side of an assignment for return value.  For
    /// void method it should be the full implantation.
    /// Much like ShouldOverrideMethod, returning null/string.Empty for abstract methods when creating an override wrapper would result in an invalid
    /// class and an `InvalidCSharpException` exception will be thrown.
    /// Additionally for interfaces wraps, protected method's of implementation can't be accessed, and therefor requires a replacement, failure to
    /// provide one will also yield an `InvalidCSharpException` exception
    /// It has access to the method parameters values, the sidecar, (and for interfaces the fall back implementation)
    /// </summary>
    /// <param name="method">The method to consider</param>
    /// <param name="support">A GeneratorSupport that the generators are allowed to uses get compile time access those methods and types</param>
    /// <returns>arbitrary C# code to inject or null/string.Empty use the default implementation</returns>
    string? ReplaceMethodCall(MethodInfo method, GeneratorSupport support) => null;

    /// <summary>
    /// Used to interject arbitrary C# to run after the "implementation" of method.
    /// Returning null/string.Empty string will simply omit the call.  Any string returned must be valid C# code to run in that context (including a
    /// trailing ';' as needed)
    /// It has access to the method parameters values, the sidecar, (for interfaces the fall back implementation), (and for non-void methods the
    /// return value yielded by the implementation via `Generated.ReturnVariableName`)
    /// </summary>
    /// <param name="method">The method to consider</param>
    /// <param name="support">A GeneratorSupport that the generators are allowed to uses get compile time access those methods and types</param>
    /// <returns>arbitrary C# code to inject or null/string.Empty to omit it</returns>
    string? PostMethodCall(MethodInfo method, GeneratorSupport support) => null;

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
    /// <param name="support">A GeneratorSupport that the generators are allowed to uses get compile time access those methods and types</param>
    /// <returns>arbitrary C# code to inject or null/string.Empty to omit it</returns>
    string? PrePropertyCall(PropertyInfo property, bool forSet, GeneratorSupport support) => null;

    /// <summary>
    /// Used to replace the "default" behavior to implement the property as setter or getter.
    /// Returning null/string.Empty string will use "default" implementation,  Any string returned must be valid C# code to run in that context
    /// (including a trailing ';' as needed).  For setters this is expected to be the right-hand-side of an assignment for return value.  Much like
    /// ShouldOverrideProperty, returning null/string.Empty for abstract property when creating an override wrapper would result in an invalid
    /// class and an `InvalidCSharpException` exception will be thrown.
    /// It has access to the sidecar, (and for interfaces the fall back implementation), (and for setters new value)
    /// Additionally for interfaces wraps, protected method's of implementation can't be accessed, and therefor requires a replacement, failure to
    /// provide one will also yield an `InvalidCSharpException` exception
    /// </summary>
    /// <param name="property">The property to consider</param>
    /// <param name="forSet">When true this is for the properties' setter, when false it is for its getter</param>
    /// <param name="support">A GeneratorSupport that the generators are allowed to uses get compile time access those methods and types</param>
    /// <returns>arbitrary C# code to inject or null/string.Empty use the default implementation</returns>
    string? ReplacePropertyCall(PropertyInfo property, bool forSet, GeneratorSupport support) => null;

    /// <summary>
    /// Used to interject arbitrary C# to run after the "implementation" of property as setter or getter.
    /// Returning null/string.Empty string will simply omit the call.  Any string returned must be valid C# code to run in that context (including a
    /// trailing ';' as needed) 
    /// It has access to the sidecar, (and for interfaces the fall back implementation), (for setters new value), return value yielded by the
    /// implementation via `Generated.ReturnVariableName`
    /// </summary>
    /// <param name="property">The property to consider</param>
    /// <param name="forSet">When true this is for the properties' setter, when false it is for its getter</param>
    /// <param name="support">A GeneratorSupport that the generators are allowed to uses get compile time access those methods and types</param>
    /// <returns>arbitrary C# code to inject or null/string.Empty to omit it</returns>
    string? PostPropertyCall(PropertyInfo property, bool forSet, GeneratorSupport support) => null;

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
    /// <param name="support">A GeneratorSupport that the generators are allowed to uses get compile time access those methods and types</param>
    /// <returns>arbitrary C# code to inject or null/string.Empty to omit it</returns>    
    string? PreEventCall(EventInfo @event, bool forRemove, GeneratorSupport support) => null;

    /// <summary>
    /// Used to replace the "default" behavior to implement the event as remover or adder.
    /// Returning null/string.Empty string will use "default" implementation,  Any string returned must be valid C# code to run in that context
    /// (including a trailing ';' as needed). Much like ShouldOverrideEvent, returning null/string.Empty for abstract event when creating an
    /// override wrapper would result in an invalid class and an `InvalidCSharpException` exception will be thrown. It has access to the sidecar,
    /// (and for interfaces the fall back implementation), add the value being added or removed
    /// Additionally for interfaces wraps, protected method's of implementation can't be accessed, and therefor requires a replacement, failure to
    /// provide one will also yield an `InvalidCSharpException` exception
    /// </summary>
    /// <param name="event">The event to consider</param>
    /// <param name="forRemove">When true this is for the event's remover, when false it is for it's adder</param>
    /// <param name="support">A GeneratorSupport that the generators are allowed to uses get compile time access those methods and types</param>
    /// <returns>arbitrary C# code to inject or null/string.Empty use the default implementation</returns>
    string? ReplaceEventCall(EventInfo @event, bool forRemove, GeneratorSupport support) => null;

    /// <summary>
    /// Used to interject arbitrary C# to run after the "implementation" of event as remover or adder.
    /// Returning null/string.Empty string will simply omit the call.  Any string returned must be valid C# code to run in that context (including a
    /// trailing ';' as needed)
    /// It has access to the sidecar, (and for interfaces the fall back implementation), add the value being added or removed
    /// </summary>
    /// <param name="property">The event to consider</param>
    /// <param name="forRemove">When true this is for the event's remover, when false it is for it's adder</param>
    /// <returns>arbitrary C# code to inject or null/string.Empty to omit it</returns>
    string? PostEventCall(EventInfo @event, bool forRemove, GeneratorSupport support) => null;

    /// <summary>
    /// Any optional Parsing options required by the generated class
    /// </summary>
    CSharpParseOptions? ParseOptions => null;

    /// <summary>
    /// Any optional Compilation options required by the generator class 
    /// </summary>
    CSharpCompilationOptions? CompilationOptions => Generator.DefaultCompilationOptions;
}

/// <summary>
/// An instance of this class will be proved to all the code generating methods on the IGenerator.
/// The implementors of those methods can use this class methods to aid in their code generation.
/// Types added will have their assemblies added as references for the new crafted assembly/type
/// Methods added will be given all supported coded to invoke said method
/// </summary>
public class GeneratorSupport
{
    private readonly MethodSet m_restrictedMethods = new();
    private readonly HashSet<Type> m_types = new();

    /// <summary>
    /// Used to get compile time access to any method (regardless of it's accessability)
    /// Note: For generic methods, reflections (managed internally) will bee needed when the method is called the the first time a unique pair of
    ///       generic argument.  For all other methods, once create or factory delegate no more reflections will be required 🎉
    /// </summary>
    /// <param name="method">The method create the compile time access for</param>
    /// <param name="asConcrete">A flag to tell if the the method should be treated a possibly virtual
    ///   When asConcrete is false, virtual methods will use the type of the object passed to select which method to call at run time (aka like all
    ///   virtual methods 😀)
    ///   When acConcrete is true, virtual methods won't not act as virtual, and instead will all invoke the method on the declared type.  This is
    ///   useful when you want to access overridden methods on base classes.
    /// </param>
    /// <returns>The set item for this method that can used to get the required text to reference the item</returns>
    public IMethodSetItem AddRestrictedMethod(MethodInfo method, bool asConcrete)
    {
        m_types.Add(typeof(RestrictedHelper));
        var result = m_restrictedMethods.Add(method, asConcrete);
        if (method.DeclaringType is not null)
        {
            m_types.Add(method.DeclaringType);
            foreach (var parameter in method.GetParameters())
            {
                m_types.Add(parameter.ParameterType);
            }
            if (!method.IsGenericMethodDefinition)
            {
                foreach (var argument in method.GetGenericArguments())
                {
                    m_types.Add(argument);
                }
            }
        }
        return result;
    }

    /// <summary>
    /// Used to add any types who's references are needed in the generated class.
    /// Note: The only types that need to added are those whole are not already.  On the contract being full filled (aka the those types, their
    /// methods return or parameter types)
    /// </summary>
    /// <param name="types"></param>
    public void AddTypes(params Type[] types)
    {
        foreach (var type in types)
        {
            m_types.Add(type);
        }
    }

    public IMethodSetItem[] RestrictedMethods => m_restrictedMethods.Items;
    public IEnumerable<Type> Types => m_types;
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
    public static readonly string VariablePrefix = "___";  // Avoid Microsoft's __

    /// <summary>
    /// The name of the member variable that can be used within any of the injection points to reference the sidecar
    /// </summary>
    public static readonly string SidecarVariableName = $"{VariablePrefix}m_sidecar";

    /// <summary>
    /// Only used for Interface wrapper, the name of the member variable that can be used within any of the injection points to reference the
    /// implementation were default logic will be delegated to.
    /// </summary>
    public static readonly string ImplementationVariableName = $"{VariablePrefix}m_implementation";

    /// <summary>
    /// For methods with a return values and property getters, the name of local variable that will hold the return value.  This variable will only
    /// be in scope for the PostXYZCalls  
    /// </summary>
    public static readonly string ReturnVariableName = $"{VariablePrefix}result";

    /// <summary>
    /// The Default name used for the namespace of our generated classes if one is not provided
    /// </summary>
    public static readonly string DefaultNamespace = $"{VariablePrefix}Generated_Namespace";

    /// <summary>
    /// The Default name used for our generated class if one is not provided
    /// </summary>
    public static readonly string DefaultClassName = $"{VariablePrefix}Generated_ClassName";

    /// <summary>
    /// The name that we used when creating our helper child class
    /// </summary>
    public static readonly string RestrictedHelperClassName = $"{VariablePrefix}RestrictedHelper";

    /// <summary>
    /// Method name used within the generated child class that supports the RestrictedAccessHelper
    /// </summary>
    private const string c_restrictedHelperSetupMethodName = "Setup";

    /// <summary>
    /// Method name used for static method created to finish the setting static members within the RestrictedAccessHelper supporting class
    /// </summary>
    private static readonly string c_setupMethodName = $"{VariablePrefix}{c_restrictedHelperSetupMethodName}";

    /// <summary>
    /// Method name of the factory method added to our class
    /// </summary>
    private static readonly string c_factoryMethodName = $"{VariablePrefix}Factory";

    public readonly static CSharpCompilationOptions DefaultCompilationOptions = new (
        OutputKind.DynamicallyLinkedLibrary,
        optimizationLevel: OptimizationLevel.Release,
        assemblyIdentityComparer: DesktopAssemblyIdentityComparer.Default);

    // We need NonPublic here to pick up protected
    private const BindingFlags c_bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    /// <summary>
    /// The Factor Method signature returned by CreateInterfaceImplementationFactory
    /// </summary>
    /// <typeparam name="TImplementation">The object to which default behavior will be delegated to</typeparam>
    /// <typeparam name="TSidecar">The sidecar you wish to use, it is complexly up you</typeparam>
    /// <typeparam name="TInterface">The interface that should be implemented by your wrapped object</typeparam>
    /// <param name="implementation">The implementation to used by default to delegate work too</param>
    /// <param name="sidecar">The side care to use for the new object</param>
    /// <returns>The wrapped object</returns>
    public delegate TInterface CreateInterfaceImplementationDelegate<TImplementation, TSidecar, TInterface>(TImplementation implementation, TSidecar sideCar);

    /// <summary>
    /// Used to create a factor that can create wrapper object that fulfills an Interface contract from the provided generator that delegates its work the interface
    /// <param name="generator">The generator to use</param>
    /// <param name="code">This output parameter will hold the generated code</param>
    /// <param name="namespace">The name of the namespace to put this new type in (if not provided `DefaultNamespace` will be used)</param>
    /// <param name="className">The name of the class name use for the new type in (if not provided `DefaultClassName` will be used)</param>
    /// <param name="logger">Optional logger to track progress</param>
    /// <param name="logLevel">Optional log level to use with the logger</param>
    /// </summary>
    public static CreateInterfaceImplementationDelegate<TImplementation, TSidecar, TInterface> CreateInterfaceImplementationFactory<TInterface, TImplementation, TSidecar>(
        this IInterfaceGenerator<TInterface, TImplementation, TSidecar> generator,
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
        GeneratorSupport support = new();
        support.AddTypes(typeof(object), typeof(TInterface), typeof(TImplementation), typeof(TSidecar));

        (code, bool usesUnsafe) = GenerateCodeForInterface(generator, support, @namespace, className);
        logger.Log(logLevel, "Completed Code Generation: {duration}", DateTime.UtcNow - time);

        return CreateFactory<CreateInterfaceImplementationDelegate<TImplementation, TSidecar, TInterface>>(
            generator,
            code,
            @namespace,
            className,
            support.Types,
            usesUnsafe,
            logger,
            logLevel);
    }

    /// <summary>
    /// Used to create a wrapper object that fulfills an Interface contract from the provided generator that delegates its work the interface
    /// Note: The creation of the type is very expensive, so if you need to create multiple instances of this object you should call
    /// CreateInterfaceImplementationFactory once, and then use its factory as needed needed
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

        var factory = generator.CreateInterfaceImplementationFactory(out code, @namespace, className, logger, logLevel);
        DateTime time = DateTime.UtcNow;

        var result = factory(implementation, sidecar);
        logger.Log(logLevel, "Completed Instance Generation: {duration}", DateTime.UtcNow - time);

        return result;
    }

    /// <summary>
    /// Used to create a factor that can create a wrapper object that fulfills an bass class contract from the provided generator that delegates its work to that base class
    /// <typeparam name="TBase">The class that should be used as the base class of the generated type</typeparam>
    /// <typeparam name="TSidecar">The sidecar you wish to use, it is complexly up you</typeparam>
    /// <typeparam name="D">The Signature of the the factor to create
    ///                     The Return type of this delegate should be TBase
    ///                     The first parameters should match the parameters of the constructor of the base class that should be used to create object
    ///                     The final parameter should TSidecar
    /// </typeparam>
    /// <param name="generator">The generator to use</param>
    /// <param name="code">This output parameter will hold the generated code</param>
    /// <param name="namespace">The name of the namespace to put this new type in (if not provided `DefaultNamespace` will be used)</param>
    /// <param name="className">The name of the class name use for the new type in (if not provided `DefaultClassName` will be used)</param>
    /// <param name="logger">Optional logger to track progress</param>
    /// <param name="logLevel">Optional log level to use with the logger</param>
    /// </summary>
    public static D CreateOverrideImplementationFactory<TBase, TSidecar, D>(
        this IOverrideGenerator<TBase, TSidecar> generator,
        out string code,
        string? @namespace = null,
        string? className = null,
        ILogger? logger = null,
        LogLevel logLevel = LogLevel.Information
    )
        where TBase : class
        where D : Delegate
    {
        InvalidCSharpException.ThrowIfIsAnInterface<TBase>(nameof(TBase));
        InvalidCSharpException.ThrowIfSealed<TBase>(nameof(TBase));

        UnexpectedReflectionsException.ThrowIfNotASubClassOfDelegate(typeof(D));

        var invokeMethod = ReflectionExtensions.GetDelegateInvokeMethod(typeof(D));
        var invokeMethodParameters = invokeMethod.GetParameters();

        UnexpectedReflectionsException.ThrowIfNotSuitableOverrideFactoryDelegateMethod<TBase, TSidecar>(invokeMethod, invokeMethodParameters);

        logger ??= NullLogger.Instance;
        @namespace ??= DefaultNamespace;
        className ??= DefaultClassName;

        var constructorParametersTypes = invokeMethodParameters
            .Take(invokeMethodParameters.Length -1)
            .Select(x => x.ParameterType)
            .ToArray();

        ConstructorInfo constructor = typeof(TBase).GetConstructor(c_bindingFlags, constructorParametersTypes)
            ?? throw new InvalidCSharpException($"Failed to find constructor on {typeof(TBase).FullTypeExpression()} with the following parameters {string.Join(", ", constructorParametersTypes.Select(x => x.FullTypeExpression()))}");

        if (constructor.IsPrivate || (constructor.IsAssembly && !constructor.IsFamily))
        {
            throw new InvalidCSharpException($"Failed to find usable constructor on {typeof(TBase).FullTypeExpression()} with the following parameters {string.Join(", ", constructorParametersTypes.Select(x => x.FullTypeExpression()))}");
        }

        DateTime time = DateTime.UtcNow;
        GeneratorSupport support = new();
        support.AddTypes(typeof(object), typeof(TBase), typeof(TSidecar));

        (code, bool usesUnsafe) = GenerateCodeForOverride(generator, support, @namespace, className, constructor);
        logger.Log(logLevel, "Completed Code Generation: {duration}", DateTime.UtcNow - time);

        return CreateFactory<D>(
            generator,
            code,
            @namespace,
            className,
            support.Types,
            usesUnsafe,
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
        
        var isAsync = (returnType == typeof(Task) || returnType.IsGenericTypeOf(typeof(Task<>)))
            && !UnsafeMethod(method)  // Unsafe Methods can't be async
            && !method.GetParameters().Any(x => x.ParameterType.IsByRefLike) // ref structs types can't be used om async methods
            && generator.TreatMethodAsync(method);

        if (isAsync && returnType == typeof(Task))
        {
            // The method is not void...
            // but we will treat it like it was, aka "return" statements should not include a value
            isVoid = true;
        }
        return (isVoid, isAsync);
    }
}
