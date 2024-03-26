using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Logging;

namespace WrapperEmitter;

/// <summary>
/// See https://github.com/dotnet/runtime/blob/dc553fe5e0b931f4d6fd481a0ba270bde644a19d/src/libraries/System.Private.CoreLib/src/System/Reflection/AssemblyName.cs#L294-L299
/// TL;DR - we need to manage our only Equality
/// https://www.dotnetframework.org/default.aspx/4@0/4@0/untmp/DEVDIV_TFS/Dev10/Releases/RTMRel/ndp/cdf/src/NetFx40/System@Activities/Microsoft/VisualBasic/Activities/AssemblyNameEqualityComparer@cs/1305376/AssemblyNameEqualityComparer@cs
/// </summary>
public class AssemblyNameComparer : IEqualityComparer<AssemblyName>, IComparer<AssemblyName>
{
    public readonly static AssemblyNameComparer Instance = new();

    public int Compare(AssemblyName? x, AssemblyName? y) => string.Compare(x?.FullName, y?.FullName);

    public bool Equals(AssemblyName? x, AssemblyName? y) 
        => ReferenceEquals(x, y)
        || ((x is not null)
            && (y is not null)
            && string.Equals(x.Name, y.Name, StringComparison.OrdinalIgnoreCase)
            && x.Version == y.Version
            && x.CultureName == y.CultureName);

    public int GetHashCode([DisallowNull] AssemblyName obj)
    {
        HashCode hash = new();
        hash.Add((obj.Name ?? string.Empty).ToUpper());
        hash.Add(obj.Version);
        hash.Add(obj.CultureName);
        return hash.ToHashCode();
    }
}

public class ClassCreationDefinition
{
    public readonly string Code;
    public readonly string Namespace;
    public readonly string ClassName;
    public readonly CSharpParseOptions? ParseOptions;
    public readonly CSharpCompilationOptions? CompilationOptions;
    public ISet<AssemblyName> AssemblyNames => m_assemblyNames.ToHashSet(AssemblyNameComparer.Instance);
    private readonly AssemblyName[] m_assemblyNames;
    private readonly int m_hashCode;

    public ClassCreationDefinition(string code, string @namespace, string className, IEnumerable<Type> types, CSharpParseOptions? parseOptions, CSharpCompilationOptions? compilationOptions)
    {
        Code = code;
        Namespace = @namespace;
        ClassName = className;

        ParseOptions = parseOptions;
        CompilationOptions = compilationOptions;

        m_assemblyNames = types
            .SelectMany(x => GetExpandTypes(x))
            .Select(x => x.Assembly.GetName())
            .Distinct(AssemblyNameComparer.Instance)
            .OrderBy(x => x, AssemblyNameComparer.Instance)
            .ToArray();

        HashCode hash = new();
        hash.Add(code);
        hash.Add(parseOptions);
        hash.Add(compilationOptions);

        foreach (var assemblyName in m_assemblyNames)
        {
            hash.Add(assemblyName, AssemblyNameComparer.Instance);
        }

        m_hashCode = hash.ToHashCode();
    }

    public override int GetHashCode() => m_hashCode;

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(this, obj))
        {
            return true;
        }
        if (obj is ClassCreationDefinition other
            && Code == other.Code
            && ParseOptions == other.ParseOptions
            && (
                ((CompilationOptions is not null) && CompilationOptions.Equals(other.CompilationOptions))
                || (CompilationOptions is null && other.CompilationOptions is null ))
            && m_assemblyNames.Length == other.m_assemblyNames.Length)
        {
            for (int i = 0; i < m_assemblyNames.Length; i++)
            {
                if (!AssemblyNameComparer.Instance.Equals(m_assemblyNames[i], other.m_assemblyNames[i]))
                {
                    return false;
                }
            }
            return true;
        }
        return false;
    }

    private static IEnumerable<Type> GetExpandTypes(Type type)
    {
        // We don't need Base classes or interfaces or method return types / parameters, those will all get picked up as dependencies as needed 
        // In Short typeof(List<Xyz>) will get us dependencies of typeof(List<>) not typeof(Xyz), so we need to pull those in our self
        // Oddly the same does not go for array. 🤷
        List<Type> result = new() { type };
        foreach (var argument in type.GetGenericArguments())
        {
            result.AddRange(GetExpandTypes(argument));
        }
        return result;
    }
}

public class TypeCache
{
    private readonly ConcurrentDictionary<ClassCreationDefinition, object> m_lockObjects = new();
    private readonly ConcurrentDictionary<ClassCreationDefinition, Type> m_cache = new();

    public bool TryGetType(ClassCreationDefinition key, [NotNullWhen(true)] out Type? type)
        => m_cache.TryGetValue(key, out type);

    public Type GetOrCreate(ClassCreationDefinition key, Func<ClassCreationDefinition, ILogger, LogLevel, Type> create, ILogger logger, LogLevel logLevel)
    {
        if (m_cache.TryGetValue(key, out var result))
        {
            return result;
        }

        lock (m_lockObjects.GetOrAdd(key, () => new object()))

        if (m_cache.TryGetValue(key, out result))
        {
            return result;
        }

        result = create(key, logger, logLevel);

        m_cache[key] = result;

        return result;
    }
}