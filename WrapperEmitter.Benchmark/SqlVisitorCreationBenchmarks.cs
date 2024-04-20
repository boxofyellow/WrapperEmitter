using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Microsoft.Extensions.Logging.Abstractions;
using WrapperEmitter.Example;

/*
| Method                    | Mean                 | Error              | StdDev             | Median               |
|-------------------------- |---------------------:|-------------------:|-------------------:|---------------------:|
| NoOpt                     |             3.673 ns |          0.0603 ns |          0.0564 ns |             3.698 ns |
| Moq                       |    76,211,198.643 ns |    557,246.1168 ns |    493,984.1680 ns |    76,224,307.714 ns |
| NoOptMoq                  |    74,529,423.867 ns |    343,894.8195 ns |    304,853.8001 ns |    74,608,374.929 ns |
| Generate                  |            26.407 ns |          0.5367 ns |          0.6181 ns |            26.009 ns |
| NoOptGenerate             |            26.464 ns |          0.5499 ns |          0.5647 ns |            26.111 ns |
| GenerateRestricted        |            26.385 ns |          0.5302 ns |          0.5673 ns |            26.054 ns |
| NoOptGenerateRestricted   |            27.816 ns |          0.5834 ns |          0.5729 ns |            27.592 ns |
| NoCacheGenerate           | 3,870,597,512.467 ns | 13,574,715.0700 ns | 12,697,796.8988 ns | 3,867,108,463.000 ns |
| NoCacheGenerateRestricted | 2,538,320,862.733 ns | 11,422,147.9424 ns | 10,684,284.2721 ns | 2,535,383,783.000 ns |

Not surprised that no cache is slow, each will do a build...  But why is Restricted no cache that much faster.
But when you let the cache do its thing, Generated vs Moq shows a nice speed up 
*/

[SimpleJob(RuntimeMoniker.Net60)]
[MarkdownExporter, AsciiDocExporter, HtmlExporter, CsvExporter, RPlotExporter]
public class SqlVisitorCreationBenchmarks
{
    private WrapperFactory? m_noOptFactory = null;
    private WrapperFactory? m_factory = null;
    private WrapperFactory? m_noOptRestrictedFactory = null;
    private WrapperFactory? m_restrictedFactory = null;


    [GlobalSetup]
    public void Setup()
    {
        m_factory = WrappedSqlVisitor.CreateFactory(asNoOpt: false, useRestricted: false, NullLogger.Instance);
        m_noOptFactory = WrappedSqlVisitor.CreateFactory(asNoOpt: true, useRestricted: false, NullLogger.Instance);
        m_restrictedFactory = WrappedSqlVisitor.CreateFactory(asNoOpt: false, useRestricted: true, NullLogger.Instance);
        m_noOptRestrictedFactory = WrappedSqlVisitor.CreateFactory(asNoOpt: true, useRestricted: true, NullLogger.Instance);
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
    public void GenerateRestricted()
    {
        SqlParseSidecar sidecar = new();
        if (m_restrictedFactory!(sidecar) is null)
        {
            throw new Exception("This should never happen...");
        }
    }

    [Benchmark]
    public void NoOptGenerateRestricted()
    {
        SqlParseSidecar sidecar = new();
        if (m_noOptRestrictedFactory!(sidecar) is null)
        {
            throw new Exception("This should never happen...");
        }
    }

    [Benchmark]
    public void NoCacheGenerate()
    {
        if (WrappedSqlVisitor.Create(asNoOpt: false, useRestricted: false, NullLogger.Instance) is null)
        {
            throw new Exception("This should never happen...");
        }
    }

    [Benchmark]
    public void NoCacheGenerateRestricted()
    {
        if (WrappedSqlVisitor.Create(asNoOpt: false, useRestricted: true, NullLogger.Instance) is null)
        {
            throw new Exception("This should never happen...");
        }
    }
}