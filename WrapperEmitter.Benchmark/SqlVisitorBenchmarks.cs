using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using WrapperEmitter.Example;

/*
| Method        | Mean           | Error       | StdDev      |
|-------------- |---------------:|------------:|------------:|
| NoOpt         |      0.4509 ns |   0.0022 ns |   0.0019 ns |
| Moq           | 29,791.2220 ns | 425.1956 ns | 376.9248 ns |
| NoOptMoq      | 31,894.1276 ns | 409.1199 ns | 382.6911 ns |
| Generate      |      0.6685 ns |   0.0255 ns |   0.0213 ns |
| NoOptGenerate |      0.6683 ns |   0.0058 ns |   0.0051 ns |

Why was Generate the smallest 🤷... I would assume real NoOpt would be fastest, followed by NoOptGenerated followed by Generated

But clearly Moq is much, **_MUCH_** slower.
*/

[SimpleJob(RuntimeMoniker.Net60)]
[MarkdownExporter, AsciiDocExporter, HtmlExporter, CsvExporter, RPlotExporter]
public class SqlVisitorBenchmarks
{
    private TSqlFragmentVisitor? m_noOpt;
    private TSqlFragmentVisitor? m_moqVisitor;
    private TSqlFragmentVisitor? m_noOptMoqVisitor;
    private TSqlFragmentVisitor? m_generatedVisitor;
    private TSqlFragmentVisitor? m_noOptGeneratedVisitor;
    private TSqlFragment? m_fragment;

    [GlobalSetup]
    public void Setup()
    {
        m_noOpt = NoOptVisitor.Create(asNoOpt: true, NullLogger.Instance);
        m_moqVisitor = MoqSqlVisitor.Create(asNoOpt: false, NullLogger.Instance);
        m_noOptMoqVisitor = MoqSqlVisitor.Create(asNoOpt: true, NullLogger.Instance);
        m_generatedVisitor = WrappedSqlVisitor.Create(asNoOpt: false, NullLogger.Instance);
        m_noOptGeneratedVisitor = WrappedSqlVisitor.Create(asNoOpt: true, NullLogger.Instance);

        TSqlParser parser = new TSql150Parser(initialQuotedIdentifiers: true);

        IList<ParseError> errors;
        using(TextReader reader = new StringReader(SqlConstant.LargeSql))
        {
            m_fragment = parser.Parse(reader, out errors);
        }

        if (errors.Any())
        {
            StringBuilder builder = new();
            foreach (var error in errors)
            {
                builder.AppendLine($"{error.Line}:{error.Offset} {error.Number} {error.Message}");
            }
            throw new ApplicationException(builder.ToString());
        }
    }

    [Benchmark]
    public void NoOpt() => m_noOpt!.Visit(m_fragment);

    [Benchmark]
    public void Moq() => m_moqVisitor!.Visit(m_fragment);

    [Benchmark]
    public void NoOptMoq() => m_noOptMoqVisitor!.Visit(m_fragment);

    [Benchmark]
    public void Generate() => m_generatedVisitor!.Visit(m_fragment);

    [Benchmark]
    public void NoOptGenerate() => m_noOptGeneratedVisitor!.Visit(m_fragment);
}