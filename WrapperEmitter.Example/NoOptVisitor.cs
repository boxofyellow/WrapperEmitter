using Microsoft.Extensions.Logging;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace WrapperEmitter.Example;

public class NoOptVisitor : TSqlFragmentVisitor
{
    public static TSqlFragmentVisitor Create(bool asNoOpt, ILogger logger) => new NoOptVisitor();
}