using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace WrapperEmitter.Example;

public delegate TSqlFragmentVisitor WrapperFactory(SqlParseSidecar sidecar);

public static class WrappedSqlVisitor
{
    public static WrapperFactory CreateFactory(bool asNoOpt, bool useRestricted, ILogger logger)
    {
        SqlParseGenerator generator = new(asNoOpt, useRestricted);

        return generator.CreateOverrideImplementationFactory<TSqlFragmentVisitor, SqlParseSidecar, WrapperFactory>(
            out var _,
            logger: logger);
    }
    public static TSqlFragmentVisitor Create(bool asNoOpt, bool useRestricted, ILogger logger)
    {
        SqlParseSidecar sidecar = new();
        SqlParseGenerator generator = new(asNoOpt, useRestricted);

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
    private readonly bool m_useRestricted;
    public SqlParseGenerator(bool asNoOpt, bool useRestricted)
    {
        m_asNoOpt = asNoOpt;
        m_useRestricted = useRestricted;
    }

    public bool ShouldOverrideProperty(PropertyInfo property) => false;
    public bool ShouldOverrideEvent(EventInfo @event) => false;

    public bool ShouldOverrideMethod(MethodInfo method) 
        => method.Name == nameof(TSqlFragmentVisitor.ExplicitVisit) && method.GetParameters().Length == 1;

    public string? PreMethodCall(MethodInfo method, GeneratorSupport support)
        => m_asNoOpt ? null : $"{Generator.SidecarVariableName}.{nameof(SqlParseSidecar.BeforeCallback)}({method.GetParameters().Single().Name});";

    public string? ReplaceMethodCall(MethodInfo method, GeneratorSupport support)
    {
        if (m_useRestricted)
        {
            var item = support.AddRestrictedMethod(method, asConcrete: true);
            return Generator.RestrictedHelperCallText("this", item);
        }
        return null;
    }

    public string? PostMethodCall(MethodInfo method, GeneratorSupport support)
        => m_asNoOpt ? null : $"{Generator.SidecarVariableName}.{nameof(SqlParseSidecar.AfterCallback)}({method.GetParameters().Single().Name});";
}