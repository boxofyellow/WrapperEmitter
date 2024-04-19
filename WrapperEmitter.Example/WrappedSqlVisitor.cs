using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace WrapperEmitter.Example;

public delegate TSqlFragmentVisitor WrapperFactory(SqlParseSidecar sidecar);

public static class WrappedSqlVisitor
{
    public static WrapperFactory CreateFactory(bool asNoOpt, ILogger logger)
    {
        SqlParseGenerator generator = new(asNoOpt);

        return generator.CreateOverrideImplementationFactory<TSqlFragmentVisitor, SqlParseSidecar, WrapperFactory>(
            out var _,
            logger: logger);
    }
    public static TSqlFragmentVisitor Create(bool asNoOpt, ILogger logger)
    {
        SqlParseSidecar sidecar = new();
        SqlParseGenerator generator = new(asNoOpt);

        var factory = generator.CreateOverrideImplementationFactory<TSqlFragmentVisitor, SqlParseSidecar, WrapperFactory>(
            out var _,
            logger: logger);
        return factory(sidecar);
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
    public SqlParseGenerator(bool asNoOpt) => m_asNoOpt = asNoOpt;

    public bool ShouldOverrideProperty(PropertyInfo property) => false;
    public bool ShouldOverrideEvent(EventInfo @event) => false;

    public bool ShouldOverrideMethod(MethodInfo method) 
        => method.Name == nameof(TSqlFragmentVisitor.ExplicitVisit) && method.GetParameters().Length == 1;

    public string? PreMethodCall(MethodInfo method)
        => m_asNoOpt ? null : $"{Generator.SidecarVariableName}.{nameof(SqlParseSidecar.BeforeCallback)}({method.GetParameters().Single().Name});";
    public string? PostMethodCall(MethodInfo method)
        => m_asNoOpt ? null : $"{Generator.SidecarVariableName}.{nameof(SqlParseSidecar.AfterCallback)}({method.GetParameters().Single().Name});";
}