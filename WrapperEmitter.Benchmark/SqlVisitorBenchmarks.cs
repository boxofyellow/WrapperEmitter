using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using WrapperEmitter.Example;

/*
| Method                  | Mean           | Error       | StdDev      |
|------------------------ |---------------:|------------:|------------:|
| NoOpt                   |      0.7614 ns |   0.0059 ns |   0.0055 ns |
| Moq                     | 29,867.2918 ns | 224.5972 ns | 210.0883 ns |
| NoOptMoq                | 30,031.1717 ns | 141.1773 ns | 132.0573 ns |
| Generate                |      0.4665 ns |   0.0067 ns |   0.0052 ns |
| NoOptGenerate           |      0.6765 ns |   0.0035 ns |   0.0031 ns |
| GenerateRestricted      |      0.6702 ns |   0.0036 ns |   0.0034 ns |
| NoOptGenerateRestricted |      0.4630 ns |   0.0036 ns |   0.0028 ns |

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
    private TSqlFragmentVisitor? m_generatedRestrictedVisitor;
    private TSqlFragmentVisitor? m_noOptGeneratedRestrictedVisitor;
    private TSqlFragment? m_fragment;

    [GlobalSetup]
    public void Setup()
    {
        m_noOpt = NoOptVisitor.Create(asNoOpt: true, NullLogger.Instance);
        m_moqVisitor = MoqSqlVisitor.Create(asNoOpt: false, NullLogger.Instance);
        m_noOptMoqVisitor = MoqSqlVisitor.Create(asNoOpt: true, NullLogger.Instance);
        m_generatedVisitor = WrappedSqlVisitor.Create(asNoOpt: false, useRestricted: false, NullLogger.Instance);
        m_noOptGeneratedVisitor = WrappedSqlVisitor.Create(asNoOpt: true, useRestricted: false, NullLogger.Instance);
        m_generatedRestrictedVisitor = WrappedSqlVisitor.Create(asNoOpt: false, useRestricted: true, NullLogger.Instance);
        m_noOptGeneratedRestrictedVisitor = WrappedSqlVisitor.Create(asNoOpt: true, useRestricted: true, NullLogger.Instance);

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

    [Benchmark]
    public void GenerateRestricted() => m_generatedRestrictedVisitor!.Visit(m_fragment);

    [Benchmark]
    public void NoOptGenerateRestricted() => m_noOptGeneratedRestrictedVisitor!.Visit(m_fragment);
}