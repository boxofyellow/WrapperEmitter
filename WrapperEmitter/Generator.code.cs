
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

            // TODO: Do we need a recursive search here?
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

        StringBuilder result = new();

        try {
            result.AppendLine($"namespace {SanitizeName(@namespace)};");
            result.AppendLine($"public class {SanitizeName(className)} : {typeof(TBase).FullTypeExpression()}");
            result.AppendLine( "{");
            result.AppendLine($"    private readonly {typeof(TSidecar).FullTypeExpression()} {SidecarVariableName};");
            result.AppendLine($"    public {SanitizeName(className)}({declaration}) : base({call})");
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
        
        string modifiers;
        if (isInterface)
        {
            if (!string.IsNullOrEmpty(asyncText))
            {
                asyncText = $"{asyncText} ";
            }
            modifiers = $"{asyncText}{returnTypeText} {fullTypeExpression}.";
        }
        else
        {
            if (!string.IsNullOrEmpty(asyncText))
            {
                asyncText = $" {asyncText}";
            }
            AccessLevel level = method.GetAccessLevel();
            modifiers = $"{level.CodeText()} override{asyncText} {returnTypeText} ";
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
            if (!isInterface)
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
        // TODO: Should we check special Name
        // TODO: Can you have Generic Properties
        // TODO: Can you have Generic Indexers

        MethodInfo? getMethod = null;
        MethodInfo? setMethod = null;
        AccessLevel? getLevel = null;
        AccessLevel? setLevel = null;

        if (property.CanRead)
        {
            getMethod = property.GetGetMethod(nonPublic: true)
                ?? throw UnexpectedReflectionsException.FailedToGetAccessor();
            if (IsNotOverridable(getMethod) || !generator.ShouldOverrideProperty(property, forSet: false))
            {
                InvalidCSharpException.ThrowIfPropertyIsAbstract(getMethod, requireReplacementImplementation: !isInterface, forSet: false);
            }
            else
            {
                getLevel = getMethod.GetAccessLevel();
            }
        }
        if (property.CanWrite)
        {
            setMethod = property.GetSetMethod(nonPublic: true)
                ?? throw UnexpectedReflectionsException.FailedToGetAccessor();
            if (IsNotOverridable(setMethod) || !generator.ShouldOverrideProperty(property, forSet: true))
            {
                InvalidCSharpException.ThrowIfPropertyIsAbstract(setMethod, requireReplacementImplementation: !isInterface, forSet: true);
            }
            else
            {
                setLevel = setMethod.GetAccessLevel();
            }
        }
        if (getLevel is null && setLevel is null)
        {
            return;
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

        string modifiers;
        if (isInterface)
        {
            modifiers = $"{propertyTypeText} {fullTypeExpression}.";
        }
        else
        {
            modifiers = $"{maxLevel.CodeText()} override {propertyTypeText} ";
        }

        builder.AppendLine($"{modifiers}{name}{indexerDeclaration}");
        builder.AppendLine( "{");

        if (getLevel is not null)
        {
            AddAccessor(builder, isInterface, accessor: "get", getLevel.Value, maxLevel,
              pre: generator.PrePropertyCall(property, forSet:false),
              implementation: generator.ReplacePropertyCall(property, forSet: false),
              post: generator.PostPropertyCall(property, forSet:false),
              (requireReplacementImplementation) => InvalidCSharpException.ThrowIfPropertyIsAbstract(getMethod!, requireReplacementImplementation, forSet: false),
              defaultImplementation: $"{implementationReference}{callName}{indexerCall};",
              implementationPrefix: $"{propertyTypeText} {ReturnVariableName} = ",
              finalStatement: $"return {ReturnVariableName};");
        }

        if (setLevel is not null)
        {
            var setMethodReturnParameterModifiers = setMethod!.ReturnParameter.GetRequiredCustomModifiers();
            var isInit = setMethodReturnParameterModifiers.Contains(typeof(IsExternalInit));
            var accessor = isInit ? "init" : "set";

            AddAccessor(builder, isInterface, accessor, setLevel.Value, maxLevel,
              pre: generator.PrePropertyCall(property, forSet:true),
              implementation: generator.ReplacePropertyCall(property, forSet: true),
              post: generator.PostPropertyCall(property, forSet:true),
              (requireReplacementImplementation) => InvalidCSharpException.ThrowIfPropertyIsAbstract(setMethod!, requireReplacementImplementation, forSet: true),
              defaultImplementation: $"{implementationReference}{callName}{indexerCall} = value;",
              implementationPrefix: null,
              finalStatement: null);
        }

        builder.AppendLine( "}");
    }

    private static void AddTypeEvent(EventInfo @event, IGenerator generator, StringBuilder builder, bool isInterface, string fullTypeExpression, string implementationReference)
    {
        // TODO: Should we check special Name
        // TODO: Can you have Generic Events
        // TODO: Can you override just one?

        MethodInfo? addMethod = null;
        MethodInfo? removeMethod = null;
        AccessLevel? addLevel = null;
        AccessLevel? removeLevel = null;

        addMethod = @event.GetAddMethod(nonPublic: true)
            ?? throw UnexpectedReflectionsException.FailedToGetAccessor();
        if (IsNotOverridable(addMethod) || !generator.ShouldOverrideEvent(@event, forRemove: false))
        {
            InvalidCSharpException.ThrowIfEventIsAbstract(addMethod, requireReplacementImplementation: !isInterface, forRemove: false);
        }
        else
        {
            addLevel = addMethod.GetAccessLevel();
        }

        removeMethod = @event.GetRemoveMethod(nonPublic: true)
            ?? throw UnexpectedReflectionsException.FailedToGetAccessor();
        if (IsNotOverridable(removeMethod) || !generator.ShouldOverrideEvent(@event, forRemove: true))
        {
            InvalidCSharpException.ThrowIfEventIsAbstract(removeMethod, requireReplacementImplementation: !isInterface, forRemove: true);
        }
        else
        {
            removeLevel = removeMethod.GetAccessLevel();
        }

        if (addLevel is null && removeLevel is null)
        {
            return;
        }

        //event EventHandler SimpleInterfaceEvent
        //public virtual event EventHandler VirtualEvent

        // TODO how do you have a null event handler type?
        var eventTypeText = @event.EventHandlerType!.FullTypeExpression();
        var maxLevel = AccessLevelExtensions.Max(addLevel, removeLevel);
        var name = SanitizeName(@event.Name);

        string modifiers;
        if (isInterface)
        {
            modifiers = $"event {eventTypeText} {fullTypeExpression}.";
        }
        else
        {
            modifiers = $"{maxLevel.CodeText()} override event {eventTypeText} ";
        }

        builder.AppendLine($"{modifiers}{name}");
        builder.AppendLine( "{");
        if (addLevel is not null)
        {
            AddAccessor(builder, isInterface, accessor: "add", addLevel.Value, maxLevel,
              pre: generator.PreEventCall(@event, forRemove:false),
              implementation: generator.ReplaceEventCall(@event, forRemove: false),
              post: generator.PostEventCall(@event, forRemove:false),
              (requireReplacementImplementation) => InvalidCSharpException.ThrowIfEventIsAbstract(removeMethod!, requireReplacementImplementation, forRemove: false),
              defaultImplementation: $"{implementationReference}.{name} += value;",
              implementationPrefix: null,
              finalStatement: null);
        }

        if (removeLevel is not null)
        {
            AddAccessor(builder, isInterface, accessor: "remove", removeLevel.Value, maxLevel,
              pre: generator.PreEventCall(@event, forRemove:true),
              implementation: generator.ReplaceEventCall(@event, forRemove: true),
              post: generator.PostEventCall(@event, forRemove:true),
              (requireReplacementImplementation) => InvalidCSharpException.ThrowIfEventIsAbstract(removeMethod!, requireReplacementImplementation, forRemove: true),
              defaultImplementation: $"{implementationReference}.{name} -= value;",
              implementationPrefix: null,
              finalStatement: null);
        }

        builder.AppendLine( "}");

    }

    private static void AddAccessor(
        StringBuilder builder,
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
        builder.AppendLine($"    {level.TextIfNotMax(maxLevel)}{accessor}");
        builder.AppendLine( "    {");

        if (!string.IsNullOrEmpty(pre))
        {
            builder.AppendLine($"        {pre}");
        }

        if (string.IsNullOrEmpty(implementation))
        {
            if (!isInterface)
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
            // TODO: there are ! here, should we throw...
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

  private static string? GenericArgumentsText(MethodInfo methodInfo)
      => methodInfo.IsGenericMethod
          ? $"<{string.Join(", ", methodInfo.GetGenericArguments().Select(x => x.FullTypeExpression()))}>"
          : null;

  // We could do something smarter here like check if it reserved work, or we could just do all of them...
  public static string SanitizeName(string name) => $"@{name}";
}