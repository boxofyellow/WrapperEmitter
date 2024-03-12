using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Logging;

namespace WrapperEmitter;

// TODO Flesh this out....

public class AssemblyNameComparer : IEqualityComparer<AssemblyName>, IComparer<AssemblyName>
{
    public readonly static AssemblyNameComparer Instance = new();

    public int Compare(AssemblyName? x, AssemblyName? y) => string.Compare(x?.FullName, y?.FullName);

    public bool Equals(AssemblyName? x, AssemblyName? y)
    {
        if (x is null)
        {
            return y is null;
        }
        if (y is null)
        {
            return false;
        }
        return x.FullName == y.FullName;
    }

    public int GetHashCode([DisallowNull] AssemblyName obj)
        => obj.FullName.GetHashCode();
}

public class ClassCreationDefinition
{
    public readonly string Code;
    public readonly string Namespace;
    public readonly string ClassName;
    public readonly CSharpParseOptions? ParseOptions;
    public ISet<AssemblyName> AssemblyNames => m_assemblyNames.ToHashSet(AssemblyNameComparer.Instance);
    private readonly AssemblyName[] m_assemblyNames;
    private readonly int m_hashCode;

    public ClassCreationDefinition(string code, string @namespace, string className, IEnumerable<Type> types, CSharpParseOptions? parseOptions)
    {
        Code = code;
        Namespace = @namespace;
        ClassName = className;

        ParseOptions = parseOptions;

        m_assemblyNames = types
            .SelectMany(x => GetExpandTypes(x))
            .Select(x => x.Assembly.GetName())
            .Distinct(AssemblyNameComparer.Instance)
            .OrderBy(x => x, AssemblyNameComparer.Instance)
            .ToArray();

        HashCode hash = new();
        hash.Add(code);
        hash.Add(parseOptions);

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

    private IEnumerable<Type> GetExpandTypes(Type type)
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