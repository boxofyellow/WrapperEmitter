using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace WrapperEmitter;

/// <summary>
/// This part of the partial class handles constructing the string that will be come the code to be compiled.
/// Consumer of this Library should only need to interact with the methods in Generator.cs
/// </summary>
public static partial class Generator
{
    /// <summary>
    /// This will generate the code that will be used for an interface implementation
    /// NOTE: the UseUnsafe value only pertains code managed by this class, if the generators injects is own `unsafe` blocks, the generator is expected track/manage that.
    /// </summary>
    /// <typeparam name="TInterface">The interface to create an implementation for</typeparam>
    /// <typeparam name="TImplementation">The default implementation to fall back to</typeparam>
    /// <typeparam name="TSidecar">The side care to support the processes</typeparam>
    /// <param name="generator">The generator used to determine which members should use the fall back implementation and other customization</param>
    /// <param name="namespace">The namespace for the new class</param>
    /// <param name="className">The name for the new class</param>
    /// <returns>The text for the code of the new class, along with a flag to indicated if Unsafe code needs to be enabled to compile</returns>
    /// <exception cref="InvalidCSharpException">Can be throw if the request would result in something that can be compiled</exception>
    private static (string Code, bool UsesUnsafe) GenerateCodeForInterface<TInterface, TImplementation, TSidecar>(
        IInterfaceGenerator<TInterface, TImplementation, TSidecar> generator,
        string @namespace,
        string className
    )
        where TImplementation : TInterface
        where TInterface : class
    {
        StringBuilder result = new();

        try
        {
            result.AppendLine($"namespace {SanitizeName(@namespace)};");
            result.AppendLine($"public class {SanitizeName(className)} : {typeof(TInterface).FullTypeExpression()}");
            result.AppendLine( "{");
            result.AppendLine($"    private readonly {typeof(TImplementation).FullTypeExpression()} {ImplementationVariableName};");
            result.AppendLine($"    private readonly {typeof(TSidecar).FullTypeExpression()} {SidecarVariableName};");
            result.AppendLine($"    public {SanitizeName(className)}({typeof(TImplementation).FullTypeExpression()} implementation, {typeof(TSidecar).FullTypeExpression()} sidecar)");
            result.AppendLine( "    {");
            result.AppendLine($"        {ImplementationVariableName} = implementation;");
            result.AppendLine($"        {SidecarVariableName} = sidecar;");
            result.AppendLine( "    }");

            var resultUsesUnsafe = false;
            // You might think a recursive search is required here but it is not
            // See CreateInterfaceImplementation_Inheritance
            foreach (var type in typeof(TInterface).GetInterfaces().Append(typeof(TInterface)))
            {
                var usesUnsafe = AddTypeMethodsPropertiesAndEvents(generator, result, type);
                resultUsesUnsafe |= usesUnsafe;
            }

            result.AppendLine( "}");
            return (result.ToString(), resultUsesUnsafe);
        }
        catch (Exception e)
        {
            throw new InvalidCSharpException(result, e);
        }
    }

    /// <summary>
    /// This will generate the code that will be used for an override implementation
    /// NOTE: the UseUnsafe value only pertains code managed by this class, if the generators injects is own `unsafe` blocks, the generator is expected track/manage that.
    /// </summary>
    /// <typeparam name="TBase">The type to use as base class</typeparam>
    /// <typeparam name="TSidecar">The side care to support the processes</typeparam>
    /// <param name="generator">The generator used to determine which members should use the fall back implementation and other customization</param>
    /// <param name="namespace">The namespace for the new class</param>
    /// <param name="className">The name for the new class</param>
    /// <param name="constructor">The constructor from the base class that should be used</param>
    /// <returns>The text for the code of the new class, along with a flag to indicated if Unsafe code needs to be enabled to compile</returns>
    /// <exception cref="InvalidCSharpException">Can be throw if the request would result in something that can be compiled</exception>
    private static (string Code, bool UsesUnsafe) GenerateCodeForOverride<TBase, TSidecar>(
        IOverrideGenerator<TBase, TSidecar> generator,
        string @namespace,
        string className,
        ConstructorInfo constructor
    )
        where TBase : class
    {
        var localSidecar = $"{VariablePrefix}sidecar";
        (var declaration, var call) = CodeReparation(constructor.GetParameters());
        if (!string.IsNullOrEmpty(declaration))
        {
            declaration += ", ";
        }
        declaration += $"{typeof(TSidecar).FullTypeExpression()} {VariablePrefix}sidecar";

        var unsafeText = constructor.GetParameters().Any(x => x.ParameterType.ContainsPointer()) ? "unsafe " : null;

        StringBuilder code = new();

        try {
            code.AppendLine($"namespace {SanitizeName(@namespace)};");
            code.AppendLine($"public class {SanitizeName(className)} : {typeof(TBase).FullTypeExpression()}");
            code.AppendLine( "{");
            code.AppendLine($"    private readonly {typeof(TSidecar).FullTypeExpression()} {SidecarVariableName};");
            code.AppendLine($"    public {unsafeText}{SanitizeName(className)}({declaration}) : base({call})");
            code.AppendLine( "    {");
            code.AppendLine($"        {SidecarVariableName} = {localSidecar};");
            code.AppendLine( "    }");

            var usesUnsafe = AddTypeMethodsPropertiesAndEvents(generator, code, typeof(TBase));

            code.AppendLine( "}");
            return (code.ToString(), usesUnsafe);
        }
        catch (Exception e)
        {
            throw new InvalidCSharpException(code, e);
        }
    }

    /// <summary>
    /// This will added all the Members for the given type to the string builder collecting our code.
    /// </summary>
    /// <param name="generator">The generator used to determine which members should use the fall back implementation and other customization</param>
    /// <param name="builder">The builder to add code to</param>
    /// <param name="type">the type to collect members from</param>
    /// <returns>true if any of the members required unsafe code</returns>
    private static bool AddTypeMethodsPropertiesAndEvents(IGenerator generator, StringBuilder builder, Type type)
    {
        var result = false;

        var isInterface = type.IsInterface;
        var fullTypeExpression = type.FullTypeExpression();

        // For interfaces implementations we want to make sure to case our default implementation we should cast it to the interface we are
        // implementing we don't need to do that for overrides, base should work just fine.
        string implementationReference = isInterface
            ? $"(({fullTypeExpression}){ImplementationVariableName})"
            : "base";

        foreach (var method in type.GetMethods(c_bindingFlags))
        {
            var usesUnsafe = AddTypeMethod(method, generator, builder, isInterface, fullTypeExpression, implementationReference);
            result |= usesUnsafe;
        }

        foreach (var property in type.GetProperties(c_bindingFlags))
        {
            var usesUnsafe = AddTypeProperty(property, generator, builder, isInterface, fullTypeExpression, implementationReference);
            result |= usesUnsafe;
        }

        foreach (var @event in type.GetEvents(c_bindingFlags))
        {
            var usesUnsafe = AddTypeEvent(@event, generator, builder, isInterface, fullTypeExpression, implementationReference);
            result |= usesUnsafe;
        }

        return result;
    }

    /// <summary>
    /// Add a Method to the builder
    /// </summary>
    /// <param name="method">The method to add</param>
    /// <param name="generator">The generator used to determine which members should use the fall back implementation and other customization</param>
    /// <param name="builder">The builder to add code to</param>
    /// <param name="isInterface">indicates if the implementation is for an interface</param>
    /// <param name="fullTypeExpression">The text that represents the type we are implementing</param>
    /// <param name="implementationReference">The text that we should use to reference the default behavior</param>
    /// <returns>true if this method require unsafe code</returns>
    private static bool AddTypeMethod(MethodInfo method, IGenerator generator, StringBuilder builder, bool isInterface, string fullTypeExpression, string implementationReference)
    {
        var result = false;
        if (method.IsSpecialName)
        {
            // These will get picked up by properties/events as needed
            return result;
        }

        if (IsNotOverridable(method) || !generator.ShouldOverrideMethod(method))
        {
            InvalidCSharpException.ThrowIfMethodIsAbstract(method, requireReplacementImplementation: !isInterface);
            return result;
        }

        var returnType = method.ReturnType;
        var returnTypeText = returnType.FullTypeExpression();

        var unsafeMethod = UnsafeMethod(method);
        result |= unsafeMethod;
        var unsafeText = unsafeMethod ? "unsafe " : null;
        (var isVoid, var isAsync) = generator.TreatAs(method);

        string? asyncText = null;
        string? awaitText = null;
        string resultTypeText = returnTypeText;
        if (isAsync)
        {
            awaitText = "await ";
            asyncText = "async";
            if (!isVoid)
            {
                // since this will be an async method, we need get the new "return type" for this method
                // Changing our Type<T> to just T
                // If this was Just a Type, then isVoid will be true, and we won't emit "return XYZ;"
                resultTypeText = returnType.GetGenericArguments().Single().FullTypeExpression();
            }
        }
        
        AccessLevel level = method.GetAccessLevel();

        // The modifiers will need to be tweaked a little based if this is interface or override implementation 
        string modifiers;
        if (isInterface)
        {
            if (!string.IsNullOrEmpty(asyncText))
            {
                asyncText = $"{asyncText} ";
            }
            modifiers = $"{unsafeText}{asyncText}{returnTypeText} {fullTypeExpression}.";
        }
        else
        {
            if (!string.IsNullOrEmpty(asyncText))
            {
                asyncText = $" {asyncText}";
            }
            modifiers = $"{level.CodeText()} override{asyncText} {unsafeText}{returnTypeText} ";
        }

        var genericArguments = GenericArgumentsText(method);
        (var declaration, var call) = CodeReparation(method.GetParameters());

        var sanitizedName = SanitizeName(method.Name);

        builder.AppendLine($"{modifiers}{sanitizedName}{genericArguments}({declaration})");
        builder.AppendLine( "{");

        var pre = generator.PreMethodCall(method);
        if (!string.IsNullOrEmpty(pre))
        {
            builder.AppendLine($"    {pre}");
        }

        var implementation = generator.ReplaceMethodCall(method);
        if (string.IsNullOrEmpty(implementation))
        {
            if (isInterface)
            {
                if (level == AccessLevel.Protected)
                {
                    throw InvalidCSharpException.CannotAccessDefaultProtectedInterfaceAccessor(method.Name);
                }
            }
            else
            {
                InvalidCSharpException.ThrowIfMethodIsAbstract(method, requireReplacementImplementation: true);
            }
            implementation = $"{awaitText}{implementationReference}.{sanitizedName}{genericArguments}({call});";
        }

        if (isVoid)
        {
            builder.AppendLine($"    {implementation}");
        }
        else
        {
            builder.AppendLine($"    {resultTypeText} {ReturnVariableName} = {implementation}");
        }

        var post = generator.PostMethodCall(method);
        if (!string.IsNullOrEmpty(post))
        {
            builder.AppendLine($"    {post}");
        }

        if (!isVoid)
        {
            builder.AppendLine($"    return {ReturnVariableName};");
        }

        builder.AppendLine( "}");
        return result;
    }


    /// <summary>
    /// Add a Property to the builder
    /// </summary>
    /// <param name="property">The property to add</param>
    /// <param name="generator">The generator used to determine which members should use the fall back implementation and other customization</param>
    /// <param name="builder">The builder to add code to</param>
    /// <param name="isInterface">indicates if the implementation is for an interface</param>
    /// <param name="fullTypeExpression">The text that represents the type we are implementing</param>
    /// <param name="implementationReference">The text that we should use to reference the default behavior</param>
    /// <returns>true if this method require unsafe code</returns>
    private static bool AddTypeProperty(PropertyInfo property, IGenerator generator, StringBuilder builder, bool isInterface, string fullTypeExpression, string implementationReference)
    {
        // TODO:(https://github.com/boxofyellow/WrapperEmitter/issues/1) Should we check special Name (non of our example/test have any yet...)
        // So it looks like generic properties like `TAbc SimpleInterfaceGenericProperty<TAbc> { get => default; }` are not a thing, so no special handling is needed
        // Same goes for indexer 🎉

        var result = false;

        MethodInfo? getMethod = null;
        MethodInfo? setMethod = null;

        if (property.CanRead)
        {
            getMethod = property.GetGetMethod(nonPublic: true)
                ?? throw UnexpectedReflectionsException.FailedToGetAccessor();
        }
        if (property.CanWrite)
        {
            setMethod = property.GetSetMethod(nonPublic: true)
                ?? throw UnexpectedReflectionsException.FailedToGetAccessor();
        }

        MethodInfo method = getMethod ?? setMethod ?? throw UnexpectedReflectionsException.FailedToGetAccessor();
        if (IsNotOverridable(method) || !generator.ShouldOverrideProperty(property))
        {
            InvalidCSharpException.ThrowIfPropertyIsAbstract(method, requireReplacementImplementation: !isInterface);
            return result;
        }

        AccessLevel? getLevel = null;
        AccessLevel? setLevel = null;
        if (getMethod is not null)
        {
            getLevel = getMethod.GetAccessLevel();
        }
        if (setMethod is not null)
        {
            setLevel = setMethod.GetAccessLevel();
        }

        var propertyTypeText = property.PropertyType.FullTypeExpression();
        var maxLevel = AccessLevelExtensions.Max(getLevel, setLevel);

        var name = SanitizeName(property.Name);
        string? indexerDeclaration = null;
        string? indexerCall = null;
        string callName;
        if (property.GetIndexParameters().Any())
        {
            name = "this";
            callName = "";
            (var declaration, var call) = CodeReparation(property.GetIndexParameters());
            indexerDeclaration = $"[{declaration}]";
            indexerCall = $"[{call}]";
        }
        else
        {
            callName = $".{name}";
        }

        var unsafeProperty = UnsafeMethod(getMethod, setMethod);
        result |= unsafeProperty; 
        var unsafeText = unsafeProperty ? "unsafe " : null;

        string modifiers;
        if (isInterface)
        {
            modifiers = $"{unsafeText}{propertyTypeText} {fullTypeExpression}.";
        }
        else
        {
            modifiers = $"{maxLevel.CodeText()} override {unsafeText}{propertyTypeText} ";
        }

        builder.AppendLine($"{modifiers}{name}{indexerDeclaration}");
        builder.AppendLine( "{");

        if (getLevel is not null)
        {
            var forSet = false;
            AddAccessor(builder, name, isInterface, accessor: "get", getLevel.Value, maxLevel,
                pre: generator.PrePropertyCall(property, forSet),
                implementation: generator.ReplacePropertyCall(property, forSet),
                post: generator.PostPropertyCall(property, forSet),
                (requireReplacementImplementation) => InvalidCSharpException.ThrowIfPropertyIsAbstract(method, requireReplacementImplementation, forSet),
                defaultImplementation: $"{implementationReference}{callName}{indexerCall};",
                implementationPrefix: $"{propertyTypeText} {ReturnVariableName} = ",
                finalStatement: $"return {ReturnVariableName};");
        }

        if (setLevel is not null)
        {
            var setMethodReturnParameterModifiers = setMethod!.ReturnParameter.GetRequiredCustomModifiers();
            var isInit = setMethodReturnParameterModifiers.Contains(typeof(IsExternalInit));
            var accessor = isInit ? "init" : "set";

            var forSet = true;
            AddAccessor(builder, name, isInterface, accessor, setLevel.Value, maxLevel,
                pre: generator.PrePropertyCall(property, forSet),
                implementation: generator.ReplacePropertyCall(property, forSet),
                post: generator.PostPropertyCall(property, forSet),
                (requireReplacementImplementation) => InvalidCSharpException.ThrowIfPropertyIsAbstract(method, requireReplacementImplementation, forSet),
                defaultImplementation: $"{implementationReference}{callName}{indexerCall} = value;",
                implementationPrefix: null,
                finalStatement: null);
        }

        builder.AppendLine( "}");
        return result;
    }

    /// <summary>
    /// Add an Event to the builder
    /// </summary>
    /// <param name="event">The event to add</param>
    /// <param name="generator">The generator used to determine which members should use the fall back implementation and other customization</param>
    /// <param name="builder">The builder to add code to</param>
    /// <param name="isInterface">indicates if the implementation is for an interface</param>
    /// <param name="fullTypeExpression">The text that represents the type we are implementing</param>
    /// <param name="implementationReference">The text that we should use to reference the default behavior</param>
    /// <returns>true if this method require unsafe code</returns>
    private static bool AddTypeEvent(EventInfo @event, IGenerator generator, StringBuilder builder, bool isInterface, string fullTypeExpression, string implementationReference)
    {
        // TODO:(https://github.com/boxofyellow/WrapperEmitter/issues/1) Should we check special Name
        // Just like Properties you can have generic events
        // You can't override just one (adder/remover) and they can't have different modifiers

        var result = false;

        MethodInfo addMethod;
        MethodInfo removeMethod;

        addMethod = @event.GetAddMethod(nonPublic: true)
            ?? throw UnexpectedReflectionsException.FailedToGetAccessor();
        removeMethod = @event.GetRemoveMethod(nonPublic: true)
            ?? throw UnexpectedReflectionsException.FailedToGetAccessor();

        // We could check removeMethod here but the result would be the same
        if (IsNotOverridable(addMethod) || !generator.ShouldOverrideEvent(@event))
        {
            InvalidCSharpException.ThrowIfEventIsAbstract(addMethod, requireReplacementImplementation: !isInterface);
            return result;
        }

        var addLevel = addMethod.GetAccessLevel();
        var removeLevel = removeMethod.GetAccessLevel();

        // If this post it to believed this should not be null
        // https://stackoverflow.com/questions/78029989/why-does-eventinfo-eventhandlertype-return-a-nullable-type-value
        Type handlerType = @event.EventHandlerType
            ?? throw UnexpectedReflectionsException.FailedToGetEventHandlerType();

        var eventTypeText = handlerType.FullTypeExpression();
        var maxLevel = AccessLevelExtensions.Max(addLevel, removeLevel);
        var name = SanitizeName(@event.Name);

        var unsafeEvent = UnsafeMethod(addMethod, removeMethod); 
        result |= unsafeEvent;
        var unsafeText = unsafeEvent ? "unsafe " : null;

        string modifiers;
        if (isInterface)
        {
            modifiers = $"{unsafeText}event {eventTypeText} {fullTypeExpression}.";
        }
        else
        {
            modifiers = $"{maxLevel.CodeText()} override {unsafeText}event {eventTypeText} ";
        }

        builder.AppendLine($"{modifiers}{name}");
        builder.AppendLine( "{");

        {
            var forRemove = false;
            AddAccessor(builder, name, isInterface, accessor: "add", addLevel, maxLevel,
                pre: generator.PreEventCall(@event, forRemove),
                implementation: generator.ReplaceEventCall(@event, forRemove),
                post: generator.PostEventCall(@event, forRemove),
                (requireReplacementImplementation) => InvalidCSharpException.ThrowIfEventIsAbstract(addMethod, requireReplacementImplementation, forRemove),
                defaultImplementation: $"{implementationReference}.{name} += value;",
                implementationPrefix: null,
                finalStatement: null);
        }

        {
            var forRemove = true;
            AddAccessor(builder, name, isInterface, accessor: "remove", removeLevel, maxLevel,
                pre: generator.PreEventCall(@event, forRemove),
                implementation: generator.ReplaceEventCall(@event, forRemove),
                post: generator.PostEventCall(@event, forRemove),
                (requireReplacementImplementation) => InvalidCSharpException.ThrowIfEventIsAbstract(removeMethod, requireReplacementImplementation, forRemove),
                defaultImplementation: $"{implementationReference}.{name} -= value;",
                implementationPrefix: null,
                finalStatement: null);
        }

        builder.AppendLine( "}");
        return result;
    }

    /// <summary>
    /// Add an Accessor (getter/setter or adder/remover) to the builder
    /// </summary>
    /// <param name="builder">The builder to add code to</param>
    /// <param name="name">The name of property/event</param>
    /// <param name="isInterface">Indicates if the implementation is for an interface</param>
    /// <param name="accessor">Access name (get, set, init, add, remove)</param>
    /// <param name="level">the access level of the accessor</param>
    /// <param name="maxLevel">The least restrictive access level between this accessor and its optional pair</param>
    /// <param name="pre">The code to run before the implementation</param>
    /// <param name="implementation">The implementation for this method</param>
    /// <param name="post">The code to run after the implementation</param>
    /// <param name="checkForAbstract">Them to call to check in the event</param>
    /// <param name="defaultImplementation">What to use for the if implementation is not populated</param>
    /// <param name="implementationPrefix">What should be added before the implementation (aka "Xyz xyz =" )</param>
    /// <param name="finalStatement">the last time to include in the method (aka "return xyz;")</param>
    private static void AddAccessor(
        StringBuilder builder,
        string name,
        bool isInterface,
        string accessor,
        AccessLevel level,
        AccessLevel maxLevel,
        string? pre,
        string? implementation,
        string? post,
        Action<bool> checkForAbstract,
        string defaultImplementation,
        string? implementationPrefix,
        string? finalStatement)
    {
        builder.AppendLine($"    {(isInterface ? string.Empty : level.TextIfNotMax(maxLevel))}{accessor}");
        builder.AppendLine( "    {");

        if (!string.IsNullOrEmpty(pre))
        {
            builder.AppendLine($"        {pre}");
        }

        if (string.IsNullOrEmpty(implementation))
        {
            if (isInterface)
            {
                if (level == AccessLevel.Protected)
                {
                    throw InvalidCSharpException.CannotAccessDefaultProtectedInterfaceAccessor($"{accessor} {name}");
                }
            }
            else
            {
                checkForAbstract(!isInterface);
            }
            implementation = defaultImplementation;
        }
        builder.AppendLine($"        {implementationPrefix}{implementation}");

        if (!string.IsNullOrEmpty(post))
        {
            builder.AppendLine($"        {post}");
        }

        if (!string.IsNullOrEmpty(finalStatement))
        {
            builder.AppendLine($"        {finalStatement}");
        }
        builder.AppendLine( "    }");
    }

    /// <summary>
    /// Checks if the provided method is overridable when generating wrap they 
    /// </summary>
    /// <param name="method"></param>
    /// <returns>True when the method can't be overwritten</returns>
    private static bool IsNotOverridable(MethodInfo method)
        // IsAssembly => IsInternal
        => method.IsPrivate || method.IsAssembly || method.IsFinal || !method.IsVirtual || 
          // "Do not override object.Finalize. Instead, provide a destructor."  Thanks C#, then why are they have IsVirtual as True 🤷?
          (method.DeclaringType == typeof(object) && method.Name == "Finalize");


    /// <summary>
    /// Given an array of parameters this will format for use in code
    /// </summary>
    /// <param name="parameters">The parameters to format</param>
    /// <returns>A string that can be use for declaration of these parameters, and a string for making calls with these</returns>
    private static (string Declaration, string Call) CodeReparation(ParameterInfo[] parameters)
    {
        return (
            // https://learn.microsoft.com/en-us/dotnet/api/system.reflection.parameterinfo.name?view=net-9.0#remarks
            // This suggests that the Name should not be null (since we don't use this on MethodInfo.ReturnParameter ParameterInfo)
            string.Join(", ", parameters.Select(x => $"{KeyWord(x)}{x.ParameterType.FullTypeExpression()} {SanitizeName(x.Name!)}")),
            string.Join(", ", parameters.Select(x => $"{KeyWord(x)}{SanitizeName(x.Name!)}"))
        );

        static string? KeyWord(ParameterInfo parameter)
            => parameter.IsOut
               ? "out "
               : parameter.ParameterType.IsByRef
                  ? "ref "
                  : null;
    }

    /// <summary>
    /// Computes if the given methods requires the `unsafe` keyword 
    /// </summary>
    /// <param name="methods">the methods to check</param>
    /// <returns>True if any of them require unsafe handling</returns>
    private static bool UnsafeMethod(params MethodInfo?[] methods)
    {
        foreach (var method in methods)
        {
            if (method is not null)
            {
                if (method.ReturnType.ContainsPointer() || method.GetParameters().Any(x => x.ParameterType.ContainsPointer()))
                {
                    return true;
                }
            }
        }
        return false; 
    }

    /// <summary>
    /// Text that can represent the generic arguments of a method
    /// </summary>
    /// <param name="method">The method to check</param>
    /// <returns>The string to represent any generic arguments or null if it does not have any</returns>
    private static string? GenericArgumentsText(MethodInfo method)
        => method.IsGenericMethod
            ? $"<{string.Join(", ", method.GetGenericArguments().Select(x => x.FullTypeExpression()))}>"
            : null;

    /// <summary>
    /// Return a string that can safely reference part of a namespace, type, method, parameter by name
    /// NOTE: We could do something smarter here like check if it reserved word, or we could just do all of them...
    /// </summary>
    /// <param name="name">The name to check</param>
    /// <returns>A sanitized version of the name</returns>
    public static string SanitizeName(string name) => $"@{name}";
}