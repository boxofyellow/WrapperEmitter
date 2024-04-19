using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Microsoft.Extensions.Logging.Abstractions;
using WrapperEmitter.Example;

/*
| Method          | Mean                 | Error              | StdDev             |
|---------------- |---------------------:|-------------------:|-------------------:|
| NoOpt           |             3.662 ns |          0.0549 ns |          0.0487 ns |
| Moq             |    75,632,706.771 ns |  1,060,700.2668 ns |    992,179.6877 ns |
| NoOptMoq        |    75,682,183.800 ns |    968,762.2769 ns |    906,180.8349 ns |
| Generate        |            26.318 ns |          0.5237 ns |          0.4899 ns |
| NoOptGenerate   |            26.265 ns |          0.4082 ns |          0.3819 ns |
| NoCacheGenerate | 3,871,032,526.571 ns | 15,692,879.2861 ns | 13,911,328.7353 ns |

Not surprised that no cache is slow, each will do a build...
But when you let the cache do its thing, Generated vs Moq shows a nice speed up 
*/


[SimpleJob(RuntimeMoniker.Net60)]
[MarkdownExporter, AsciiDocExporter, HtmlExporter, CsvExporter, RPlotExporter]
public class SqlVisitorCreationBenchmarks
{
    private WrapperFactory? m_noOptFactory = null;
    private WrapperFactory? m_factory = null;

    [GlobalSetup]
    public void Setup()
    {
        m_factory = WrappedSqlVisitor.CreateFactory(asNoOpt: false, NullLogger.Instance);
        m_noOptFactory = WrappedSqlVisitor.CreateFactory(asNoOpt: true, NullLogger.Instance);
    }

    [Benchmark]
    public void NoOpt()
    {
        if (NoOptVisitor.Create(asNoOpt: true, NullLogger.Instance) is null)
        {
            throw new Exception("This should never happen...");
        }
    }

    [Benchmark]
    public void Moq()
    {
        if (MoqSqlVisitor.Create(asNoOpt: false, NullLogger.Instance) is null)
        {
            throw new Exception("This should never happen...");
        }
    }

    [Benchmark]
    public void NoOptMoq()
    {
        if (MoqSqlVisitor.Create(asNoOpt: true, NullLogger.Instance) is null)
        {
            throw new Exception("This should never happen...");
        }
    }

    [Benchmark]
    public void Generate()
    {
        SqlParseSidecar sidecar = new();
        if (m_factory!(sidecar) is null)
        {
            throw new Exception("This should never happen...");
        }
    }

    [Benchmark]
    public void NoOptGenerate()
    {
        SqlParseSidecar sidecar = new();
        if (m_noOptFactory!(sidecar) is null)
        {
            throw new Exception("This should never happen...");
        }
    }

    [Benchmark]
    public void NoCacheGenerate()
    {
        if (WrappedSqlVisitor.Create(asNoOpt: false, NullLogger.Instance) is null)
        {
            throw new Exception("This should never happen...");
        }
    }
}