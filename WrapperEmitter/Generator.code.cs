using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace WrapperEmitter;

public static partial class Generator
{
    private static string GenerateCodeForInterface<TInterface, TImplementation, TSidecar>(
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

            // You might think a recursive search is required here but it is not
            // See CreateInterfaceImplementation_Inheritance
            foreach (var type in typeof(TInterface).GetInterfaces().Append(typeof(TInterface)))
            {
                AddTypeMethodsPropertiesAndEvents(generator, result, type);
            }

            result.AppendLine( "}");
        }
        catch (Exception e)
        {
            throw new InvalidCSharpException(result, e);
        }
        return result.ToString();
    }

    private static string GenerateCodeForOverride<TBase, TSidecar>(
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

        StringBuilder result = new();

        try {
            result.AppendLine($"namespace {SanitizeName(@namespace)};");
            result.AppendLine($"public class {SanitizeName(className)} : {typeof(TBase).FullTypeExpression()}");
            result.AppendLine( "{");
            result.AppendLine($"    private readonly {typeof(TSidecar).FullTypeExpression()} {SidecarVariableName};");
            result.AppendLine($"    public {unsafeText}{SanitizeName(className)}({declaration}) : base({call})");
            result.AppendLine( "    {");
            result.AppendLine($"        {SidecarVariableName} = {localSidecar};");
            result.AppendLine( "    }");

            AddTypeMethodsPropertiesAndEvents(generator, result, typeof(TBase));

            result.AppendLine( "}");
        }
        catch (Exception e)
        {
            throw new InvalidCSharpException(result, e);
        }
        return result.ToString();
    }

    private static void AddTypeMethodsPropertiesAndEvents(IGenerator generator, StringBuilder builder, Type type)
    {
        var isInterface = type.IsInterface;
        var fullTypeExpression = type.FullTypeExpression();

        string implementationReference = isInterface
            ? $"(({fullTypeExpression}){ImplementationVariableName})"
            : "base";

        foreach (var method in type.GetMethods(c_bindingFlags))
        {
            AddTypeMethod(method, generator, builder, isInterface, fullTypeExpression, implementationReference);
        }

        foreach (var property in type.GetProperties(c_bindingFlags))
        {
            AddTypeProperty(property, generator, builder, isInterface, fullTypeExpression, implementationReference);
        }

        foreach (var @event in type.GetEvents(c_bindingFlags))
        {
            AddTypeEvent(@event, generator, builder, isInterface, fullTypeExpression, implementationReference);
        }
    }

    private static void AddTypeMethod(MethodInfo method, IGenerator generator, StringBuilder builder, bool isInterface, string fullTypeExpression, string implementationReference)
    {
        if (method.IsSpecialName)
        {
            // These will get picked up by properties/events as needed
            return;
        }

        if (IsNotOverridable(method) || !generator.ShouldOverrideMethod(method))
        {
            InvalidCSharpException.ThrowIfMethodIsAbstract(method, requireReplacementImplementation: !isInterface);
            return;
        }

        var returnType = method.ReturnType;
        var returnTypeText = returnType.FullTypeExpression();

        var unsafeText = UnsafeMethod(method) ? "unsafe " : null;
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
                resultTypeText = returnType.GetGenericArguments().Single().FullTypeExpression();
            }
        }
        
        AccessLevel level = method.GetAccessLevel();
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
    }

    private static void AddTypeProperty(PropertyInfo property, IGenerator generator, StringBuilder builder, bool isInterface, string fullTypeExpression, string implementationReference)
    {
        // TODO:(https://github.com/boxofyellow/WrapperEmitter/issues/1) Should we check special Name (non of our example/test have any yet...)
        // So it looks like generic properties like `TAbc SimpleInterfaceGenericProperty<TAbc> { get => default; }` are not a thing, so no special handling is needed
        // Same goes for indexer 🎉

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
            return;
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

        var unsafeText = UnsafeMethod(getMethod, setMethod) ? "unsafe " : null;

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
            AddAccessor(builder, name, isInterface, accessor: "get", getLevel.Value, maxLevel,
                pre: generator.PrePropertyCall(property, forSet:false),
                implementation: generator.ReplacePropertyCall(property, forSet: false),
                post: generator.PostPropertyCall(property, forSet:false),
                (requireReplacementImplementation) => InvalidCSharpException.ThrowIfPropertyIsAbstract(method, requireReplacementImplementation, forSet: false),
                defaultImplementation: $"{implementationReference}{callName}{indexerCall};",
                implementationPrefix: $"{propertyTypeText} {ReturnVariableName} = ",
                finalStatement: $"return {ReturnVariableName};");
        }

        if (setLevel is not null)
        {
            var setMethodReturnParameterModifiers = setMethod!.ReturnParameter.GetRequiredCustomModifiers();
            var isInit = setMethodReturnParameterModifiers.Contains(typeof(IsExternalInit));
            var accessor = isInit ? "init" : "set";

            AddAccessor(builder, name, isInterface, accessor, setLevel.Value, maxLevel,
                pre: generator.PrePropertyCall(property, forSet:true),
                implementation: generator.ReplacePropertyCall(property, forSet: true),
                post: generator.PostPropertyCall(property, forSet:true),
                (requireReplacementImplementation) => InvalidCSharpException.ThrowIfPropertyIsAbstract(method, requireReplacementImplementation, forSet: true),
                defaultImplementation: $"{implementationReference}{callName}{indexerCall} = value;",
                implementationPrefix: null,
                finalStatement: null);
        }

        builder.AppendLine( "}");
    }

    private static void AddTypeEvent(EventInfo @event, IGenerator generator, StringBuilder builder, bool isInterface, string fullTypeExpression, string implementationReference)
    {
        // TODO:(https://github.com/boxofyellow/WrapperEmitter/issues/1) Should we check special Name
        // Just like Properties you can have generic events
        // You can't override just one (adder/remover) and they can't have different modifiers

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
            return;
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

        var unsafeText = UnsafeMethod(addMethod, removeMethod) ? "unsafe " : null;

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

        AddAccessor(builder, name, isInterface, accessor: "add", addLevel, maxLevel,
            pre: generator.PreEventCall(@event, forRemove:false),
            implementation: generator.ReplaceEventCall(@event, forRemove: false),
            post: generator.PostEventCall(@event, forRemove:false),
            (requireReplacementImplementation) => InvalidCSharpException.ThrowIfEventIsAbstract(addMethod, requireReplacementImplementation, forRemove: false),
            defaultImplementation: $"{implementationReference}.{name} += value;",
            implementationPrefix: null,
            finalStatement: null);

        AddAccessor(builder, name, isInterface, accessor: "remove", removeLevel, maxLevel,
            pre: generator.PreEventCall(@event, forRemove:true),
            implementation: generator.ReplaceEventCall(@event, forRemove: true),
            post: generator.PostEventCall(@event, forRemove:true),
            (requireReplacementImplementation) => InvalidCSharpException.ThrowIfEventIsAbstract(removeMethod, requireReplacementImplementation, forRemove: true),
            defaultImplementation: $"{implementationReference}.{name} -= value;",
            implementationPrefix: null,
            finalStatement: null);

        builder.AppendLine( "}");
    }

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

    private static bool IsNotOverridable(MethodInfo method)
        // IsAssembly => IsInternal
        => method.IsPrivate || method.IsAssembly || method.IsFinal || !method.IsVirtual || 
          // "Do not override object.Finalize. Instead, provide a destructor."  Thanks C#, then why are they have IsVirtual as True 🤷?
          (method.DeclaringType == typeof(object) && method.Name == "Finalize");


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

    private static string? GenericArgumentsText(MethodInfo methodInfo)
        => methodInfo.IsGenericMethod
            ? $"<{string.Join(", ", methodInfo.GetGenericArguments().Select(x => x.FullTypeExpression()))}>"
            : null;

    // We could do something smarter here like check if it reserved work, or we could just do all of them...
    public static string SanitizeName(string name) => $"@{name}";
}