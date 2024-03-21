using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace WrapperEmitter.Example;

// TODO : Fix this name
public static class GeneratedSqlVisitor
{

    public static TSqlFragmentVisitor Create(bool asNoOpt, ILogger logger)
    {
        SqlParseSidecar sidecar = new();
        SqlParseGenerator generator = new(asNoOpt, disableCache: false);

        return generator.CreateOverrideImplementation(
            constructorArguments: Generator.NoParams,
            sidecar,
            out var _,
            logger: logger);
    }

    public static TSqlFragmentVisitor Create(bool asNoOpt, bool disableCache, ILogger logger)
    {
        SqlParseSidecar sidecar = new();
        SqlParseGenerator generator = new(asNoOpt, disableCache);

        return generator.CreateOverrideImplementation(
            constructorArguments: Generator.NoParams,
            sidecar,
            out var _,
            logger: logger);
    }
}

public class SqlParseSidecar
{
    public void BeforeCallback<T>(T node) where T : TSqlFragment 
        => Before[typeof(T)] = 1 + Before.GetValueOrDefault(typeof(T));
    public void AfterCallback<T>(T node) where T : TSqlFragment 
        => After[typeof(T)] = 1 + After.GetValueOrDefault(typeof(T));
    public readonly Dictionary<Type, int> Before = new();
    public readonly Dictionary<Type, int> After = new();
}

public class SqlParseGenerator : IOverrideGenerator<TSqlFragmentVisitor, SqlParseSidecar>
{
    private readonly bool m_asNoOpt;
    private readonly string? m_stabilizer;
    public SqlParseGenerator(bool asNoOpt, bool disableCache)
    {
        m_asNoOpt = asNoOpt;
        // by adding "random" comment it will case the code to be different, and will result in cache misses
        m_stabilizer = disableCache ? $"// {Guid.NewGuid()}" : null;
    }

    public bool ShouldOverrideProperty(PropertyInfo propertyInfo, bool forSet) => false;
    public bool ShouldOverrideEvent(EventInfo eventInfo) => false;

    public bool ShouldOverrideMethod(MethodInfo methodInfo) 
        => methodInfo.Name == nameof(TSqlFragmentVisitor.ExplicitVisit) && methodInfo.GetParameters().Length == 1;

    public string? PreMethodCall(MethodInfo methodInfo)
        => m_asNoOpt ? m_stabilizer : $"{Generator.SidecarVariableName}.{nameof(SqlParseSidecar.BeforeCallback)}({methodInfo.GetParameters().Single().Name}); {m_stabilizer}";
    public string? PostMethodCall(MethodInfo methodInfo)
        => m_asNoOpt ? m_stabilizer : $"{Generator.SidecarVariableName}.{nameof(SqlParseSidecar.AfterCallback)}({methodInfo.GetParameters().Single().Name}); {m_stabilizer}";
}