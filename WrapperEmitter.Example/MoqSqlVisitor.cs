
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using Moq;
using Moq.Language.Flow;

namespace WrapperEmitter.Example;

public static class MoqSqlVisitor
{
    public static TSqlFragmentVisitor Create(bool asNoOpt, ILogger logger)
    {
        var watch = Stopwatch.StartNew();
        var parser = new MoqSqlParser(asNoOpt);
        logger.LogInformation("Create Sql Parser time: {elapsed}", watch.Elapsed);
        watch.Restart();
        var visitor = parser.Object;
        logger.LogInformation("Create Visitor time: {elapsed}", watch.Elapsed);
        watch.Restart();

        return visitor;
    }
}

public class MoqSqlParser : Mock<TSqlFragmentVisitor>
{
    public MoqSqlParser(bool asNoOpt)
    {
        CallBase = true;

        var isAny = typeof(It).GetMethods(BindingFlags.Static | BindingFlags.Public).Single(x => x.Name == nameof(It.IsAny));

        var callBackToAdd = asNoOpt
            ? typeof(MoqSqlParser).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic).Single(x => x.Name == nameof(AddNoOptCallback))
            : typeof(MoqSqlParser).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic).Single(x => x.Name == nameof(AddCallback));

        // The following is equivalent to (TSqlFragmentVisitor x)
        var parameter = Expression.Parameter(typeof(TSqlFragmentVisitor), "x");

        foreach (var method in typeof(TSqlFragmentVisitor).GetMethods().Where(x => x.Name == nameof(TSqlFragmentVisitor.ExplicitVisit) && x.GetParameters().Length == 1))
        {
            var parameterType = method.GetParameters().Single().ParameterType;

            // The following is equivalent to x => x.ExplicitVisit(It.IsAny<{parameterType}>())
            var explicitVisitCall = Expression.Call(parameter, method, Expression.Call(instance: default, isAny.MakeGenericMethod(parameterType)));
            var setupLambda = Expression.Lambda(delegateType: typeof(Action<TSqlFragmentVisitor>), explicitVisitCall, parameter);

            // so this is equivalent to mock.Setup(x => x.ExplicitVisit<{parameterType}>(It.IsAny<{parameterType}>())
            var setup = (ISetup<TSqlFragmentVisitor>)GetType()
                .GetMethod(nameof(Mock<TSqlFragmentVisitor>.Setup), types: new [] { setupLambda.GetType()})
                !.Invoke(this, new object [] { setupLambda })!;

            // this will add on the equivalent to setup.Callback(({parameterType} n) => Callback<{parameterType}>(method, n))
            // we can't use what we did to .Setup b/c we need a closure to pass method.
            callBackToAdd.MakeGenericMethod(parameterType).Invoke(this, new object[] {method, setup});
        }
    }

    private void Callback<T>(MethodInfo method, T node) where T : TSqlFragment
    {
        Before[typeof(T)] = 1 + Before.GetValueOrDefault(typeof(T));
        // Now call the base class's implantation
        var ftnPtr = method.MethodHandle.GetFunctionPointer();
        var action = (Action<T>)Activator.CreateInstance(typeof(Action<T>), Object, ftnPtr)!;
        action(node);
        After[typeof(T)] = 1 + After.GetValueOrDefault(typeof(T));
    }

    private void NoOptCallback<T>(MethodInfo method, T node) where T : TSqlFragment
    {
        // Now call the base class's implantation
        var ftnPtr = method.MethodHandle.GetFunctionPointer();
        var action = (Action<T>)Activator.CreateInstance(typeof(Action<T>), Object, ftnPtr)!;
        action(node);
    }

    private void AddCallback<T>(MethodInfo method, ISetup<TSqlFragmentVisitor> setup) where T : TSqlFragment
        => setup.Callback((T n) => Callback(method, n));

    private void AddNoOptCallback<T>(MethodInfo method, ISetup<TSqlFragmentVisitor> setup) where T : TSqlFragment
        => setup.Callback((T n) => NoOptCallback(method, n));


    public readonly Dictionary<Type, int> Before = new();
    public readonly Dictionary<Type, int> After = new();
}