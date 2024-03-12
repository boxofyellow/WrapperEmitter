using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using WrapperEmitter.Example;

/*
| Method        | Mean           | Error       | StdDev        |
|-------------- |---------------:|------------:|--------------:|
| NoOpt         |      0.6715 ns |   0.0405 ns |     0.0416 ns |
| Moq           | 31,454.7013 ns | 623.9603 ns | 1,125.1290 ns |
| NoOptMoq      | 30,894.3092 ns | 477.6735 ns |   446.8160 ns |
| Generate      |      0.4922 ns |   0.0320 ns |     0.0300 ns |
| NoOptGenerate |      0.6910 ns |   0.0411 ns |     0.0534 ns |

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
        m_generatedVisitor = GeneratedSqlVisitor.Create(asNoOpt: false, NullLogger.Instance);
        m_noOptGeneratedVisitor = GeneratedSqlVisitor.Create(asNoOpt: true, NullLogger.Instance);

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