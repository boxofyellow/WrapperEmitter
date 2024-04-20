using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Emit;

namespace WrapperEmitter;

public interface IMethodSetItem
{
    public MethodInfo Method {get; init; }
    public bool AsConcrete {get; init; }
    public string Name {get; init; }
}

public class MethodSet
{
    private class MethodSetItem : IComparable<MethodSetItem>, IMethodSetItem
    {
        public MethodInfo Method {get; init; }
        public bool AsConcrete {get; init; }
        public string Name {get; init; }
        private readonly int m_hash;

        public MethodSetItem(MethodInfo method, bool asConcrete, int count)
        {
            Method = method;
            AsConcrete = asConcrete;

            var declaringType = method.DeclaringType
                ?? throw UnexpectedReflectionsException.MissingDeclaringType(method);

            Name = $"{Generator.VariablePrefix}{Scrub(declaringType.FullTypeExpression())}{Generator.VariablePrefix}{asConcrete}{Generator.VariablePrefix}{method.Name}{Generator.VariablePrefix}{count}";

            HashCode hash = new();
            hash.Add(method);
            hash.Add(asConcrete);
            m_hash = hash.ToHashCode();
        }

        public override int GetHashCode() => m_hash;
        public override bool Equals(object? obj)
            => (obj is not null)
            && (obj is MethodSetItem other)
            && (AsConcrete == other.AsConcrete)
            && (Method == other.Method);

        public int CompareTo(MethodSetItem? other) => string.Compare(Name, other?.Name);

        /// <summary>
        /// List of characters that might find their way into our name that are not allowed
        /// </summary>
        private readonly static HashSet<char> s_invalidNameChars = new(".<>[] ,@*&".ToArray());

        static string Scrub(string text)
        {
            var chars = text.ToArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (s_invalidNameChars.Contains(chars[i]))
                {
                    chars[i] = '_';
                }
            }
            return new string(chars);
        }
    }

    private readonly HashSet<MethodSetItem> m_set = new();
    public IMethodSetItem Add(MethodInfo method, bool asConcrete)
    {
        MethodSetItem item = new(method, asConcrete, m_set.Count);
        if (!m_set.TryGetValue(item, out var result))
        {
            result = item;
            m_set.Add(item);
        }
        return result;
    }
    public IMethodSetItem[] Items => m_set.OrderBy(x => x).Cast<IMethodSetItem>().ToArray();
}

public static class RestrictedHelper
{
    private static readonly string s_bindingFlagFullTypeName = typeof(BindingFlags).FullTypeExpression();

    public static D CreateDelegate<D>(BindingFlags flags, string name, bool asConcrete)
        where D : Delegate
    {
        UnexpectedReflectionsException.ThrowIfOpenGenericType(typeof(D), nameof(CreateClosedGenericDelegate));
        var method = GetInstanceMethod(typeof(D), flags, name);

        // We can use method.CreateDelegate<D>(); since this should never be used on static methods
        // But using this to get more coverage
        return CreateDynamicMethodDelegate<D>(method, asConcrete);
    }

    public static string CreateDelegateText(string delegateTypeText, MethodInfo method, bool asConcrete)
        => $"{typeof(RestrictedHelper).FullTypeExpression()}.{nameof(CreateDelegate)}<{delegateTypeText}>({PassBindingFlags(method)}, \"{method.Name}\", {asConcrete.ToString().ToLower()})";

    public static MethodInfo GetOpenGenericMethod(Type delegateType, BindingFlags flags, string name)
    {
        UnexpectedReflectionsException.ThrowIfNotOpenGenericType(delegateType, nameof(CreateDelegate));
        UnexpectedReflectionsException.ThrowIfNotASubClassOfDelegate(delegateType);
        
        var genericMethod = GetInstanceMethod(delegateType, flags, name);
        return genericMethod;
    }

    public static string GetOpenGenericMethodText(string openDelegateTypeText, MethodInfo method)
        => $"{typeof(RestrictedHelper).FullTypeExpression()}.{nameof(GetOpenGenericMethod)}(typeof({openDelegateTypeText}), {PassBindingFlags(method)}, \"{method.Name}\")";

    public static readonly string CacheTypeText = typeof(ConcurrentDictionary<Type, Delegate>).FullTypeExpression();

    public static D CreateClosedGenericDelegate<D>(ConcurrentDictionary<Type, Delegate> cache, MethodInfo genericMethod, bool asConcrete = false)
        where D : Delegate
    {
        UnexpectedReflectionsException.ThrowIfNotGenericType(typeof(D), nameof(CreateDelegate));
        UnexpectedReflectionsException.ThrowIfNotCloseGenericType(typeof(D));
        UnexpectedReflectionsException.ThrowIfNotOpenGenericMethod(genericMethod);

        var result = cache.GetOrAdd(typeof(D), t =>
        {
            var method = genericMethod.MakeGenericMethod(t.GetGenericArguments());
            return CreateDynamicMethodDelegate<D>(method, asConcrete);
        });

        return (D)result;
    }

    public static string CreateClosedGenericDelegateText(string delegateTypeText, string cacheText, string methodText, bool asConcrete)
        => $"{typeof(RestrictedHelper).FullTypeExpression()}.{nameof(CreateClosedGenericDelegate)}<{delegateTypeText}>({cacheText}, {methodText}, {asConcrete.ToString().ToLower()})";

    // Ideally we would just call method.CreateDelegate<D>(), but that dose not work for Generic Virtual Methods
    // see https://github.com/dotnet/runtime/issues/100748
    // So we will have to do this instead
    // public for testing
    public static D CreateDynamicMethodDelegate<D>(MethodInfo method, bool asConcrete = false)
        where D : Delegate
    {
        UnexpectedReflectionsException.ThrowIfOpenGenericType(typeof(D), $"{nameof(Type.MakeGenericType)} on Delegate first");
        UnexpectedReflectionsException.ThrowIfOpenGenericMethod(method);

        var invokeMethod = ReflectionExtensions.GetDelegateInvokeMethod(typeof(D));

        Type? returnType = invokeMethod.ReturnType;
        var parameterTypes = invokeMethod.GetParameters().Select(x => x.ParameterType).ToArray();

        UnexpectedReflectionsException.ThrowIfMethodSignatureDoesNotMatch(returnType!, parameterTypes, method);

        if (returnType == typeof(void))
        {
            returnType = null;
        }

        var dynamicMethod = new DynamicMethod(
            name: string.Empty,  // This name does not show up in exceptions, so I see no need to try give it one for debugging
            returnType: returnType,
            parameterTypes: parameterTypes,
            restrictedSkipVisibility: true);

        var dIl = dynamicMethod.GetILGenerator();

        for (int i = 0; i < parameterTypes.Length; i++)
        {
            switch (i)
            {
                case 0:
                    dIl.Emit(OpCodes.Ldarg_0);
                    break;
                case 1:
                    dIl.Emit(OpCodes.Ldarg_1);
                    break;
                case 2:
                    dIl.Emit(OpCodes.Ldarg_2);
                    break;
                case 3:
                    dIl.Emit(OpCodes.Ldarg_3);
                    break;
                default:
                    dIl.Emit(OpCodes.Ldarg_S, i);
                    break;
            }
        }

        dIl.Emit(
            method.IsVirtual && !asConcrete 
                ? OpCodes.Callvirt
                : OpCodes.Call
            , method);


        dIl.Emit(OpCodes.Ret);

        try
        {
            return dynamicMethod.CreateDelegate<D>();
        }
        catch (Exception ex)
        {
            throw new UnexpectedReflectionsException(typeof(D), method, ex);
        }
    }

    // Public For Testing
    public static MethodInfo GetInstanceMethod(Type delegateType, BindingFlags flags, string name)
    {
        UnexpectedReflectionsException.ThrowIfStatic(flags);
        UnexpectedReflectionsException.ThrowIfNotASubClassOfDelegate(delegateType);

        var invokeMethod = ReflectionExtensions.GetDelegateInvokeMethod(delegateType);
        var parameters = invokeMethod.GetParameters();

        if (!parameters.Any())
        {
            throw UnexpectedReflectionsException.DelegateMissingParameters(delegateType);
        }

        var thatType = parameters.First().ParameterType;
        var parameterTypes = parameters.Skip(1).Select(x => x.ParameterType).ToArray();
        var parameterTexts = parameterTypes
            .Select(x => x.IsGenericParameter || x.IsGenericType
                ? x.FullTypeExpression(OpenGenericOption.Identify)
                : null)
            .ToArray();

        // FYI: this will be 0 for non generic methods
        var expectedGenericArgumentCount = delegateType.GetGenericArguments().Length;

        return thatType
            .GetMethods(flags)
            .FirstOrDefault(
                (m) => DoesMethodMatch(thatType, expectedGenericArgumentCount, name, parameterTypes, parameterTexts, m))
            ?? throw UnexpectedReflectionsException.FailedToFindMethod(thatType, name);
    }

    // public for testing.
    public static MethodInfo GetStaticMethod(Type delegateType, BindingFlags flags, Type staticType, string name)
    {
        UnexpectedReflectionsException.ThrowIfNotStatic(flags);
        UnexpectedReflectionsException.ThrowIfNotASubClassOfDelegate(delegateType);

        var invokeMethod = ReflectionExtensions.GetDelegateInvokeMethod(delegateType);
        var parameters = invokeMethod.GetParameters();

        var parameterTypes = parameters.Select(x => x.ParameterType).ToArray();
        var parameterTexts = parameterTypes
            .Select(x => x.IsGenericParameter || x.IsGenericType
                ? x.FullTypeExpression(OpenGenericOption.Identify)
                : null)
            .ToArray(); 

        var expectedGenericArgumentCount = delegateType.GetGenericArguments().Length;

        return staticType
            .GetMethods(flags)
            .FirstOrDefault(
                (m) => DoesMethodMatch(staticType, expectedGenericArgumentCount, name, parameterTypes, parameterTexts, m))
            ?? throw UnexpectedReflectionsException.FailedToFindMethod(staticType, name);
    }

    private static bool DoesMethodMatch(Type declaringType, int expectedGenericArgumentCount, string name, Type[] parameterTypes, string?[] parameterTexts, MethodInfo method)
    {
        var isGeneric = expectedGenericArgumentCount != 0;
        // NOTE: If we loosen the restrictions UnexpectedReflectionsException.ThrowIfMethodSignatureDoesNotMatch, we will want to revisit this check on declaringType 
        if (method.IsGenericMethod != isGeneric || !name.Equals(method.Name) || declaringType != method.DeclaringType)
        {
            return false;
        }

        var methodGenericArgumentCount = method.GetGenericArguments().Length;

        if (methodGenericArgumentCount != expectedGenericArgumentCount)
        {
            return false;
        }

        var methodParameterTypes = method.GetParameters().Select(x => x.ParameterType).ToArray();
        if (methodParameterTypes.Length != parameterTypes.Length)
        {
            return false;
        }

        for (int i = 0; i < parameterTypes.Length; i++)
        {
            var expectedType = parameterTypes[i];
            var methodType = methodParameterTypes[i];
            if (expectedType == methodType)
            {
                continue;
            }

            if (!isGeneric)
            {
                return false;
            }

            if (expectedType.IsGenericParameter != methodType.IsGenericParameter || 
                expectedType.IsGenericType != methodType.IsGenericType)
            {
                return false;
            }

            if (!expectedType.IsGenericParameter &&
                !expectedType.IsGenericType)
            {
                return false;
            }

            if (parameterTexts[i] != methodType.FullTypeExpression(OpenGenericOption.Identify))
            {
                return false;
            }
        }

        return true;
    }

    // public to aid in testing
    public static BindingFlags GetBindingFlags(MethodInfo method)
    {
        // We could be cheeky and use reflections....
        // https://github.com/dotnet/runtime/blob/b8964d8bc2d94bab9deb29791fb8eee7cb9ddc90/src/coreclr/System.Private.CoreLib/src/System/Reflection/RuntimeMethodInfo.CoreCLR.cs#L104
        // But that seams like a bad idea all the "other" reflections that we do is looking for things on types that we found elsewhere via enumeration
        // Just reaching out to some random private/internal method in the CLR seams brittle...

        // Yes we could just always BindingFlags.Static | BindingFlags.Instance, but since need to search we should filter as much as we can as cheaply as we can.
        return (method.IsPublic ? BindingFlags.Public : BindingFlags.NonPublic)
             | (method.IsStatic ? BindingFlags.Static : BindingFlags.Instance);
    }

    private static string PassBindingFlags(MethodInfo method)
        // NOTE: we could avoid this case in/out of int... but would make the call really long
        => $"({s_bindingFlagFullTypeName}){(int)GetBindingFlags(method)}";
}