using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Microsoft.Extensions.Logging.Abstractions;
using WrapperEmitter.Example;

/*
| Method          | Mean                 | Error              | StdDev             |
|---------------- |---------------------:|-------------------:|-------------------:|
| NoOpt           |             3.750 ns |          0.1019 ns |          0.1702 ns |
| Moq             |    75,434,911.537 ns |  1,494,153.9438 ns |  1,994,652.5983 ns |
| NoOptMoq        |    76,476,686.545 ns |  1,522,272.5632 ns |  2,414,485.6008 ns |
| Generate        |     2,333,155.079 ns |     46,376.6258 ns |     72,202.8191 ns |
| NoOptGenerate   |     1,621,205.475 ns |     25,895.9877 ns |     22,956.1186 ns |
| NoCacheGenerate | 3,907,873,387.923 ns | 13,065,086.3708 ns | 10,909,938.8404 ns |

Not surprised that no cache is slow, each will do a build...
But when you let the cache do its thing, Generated vs Moq shows a nice speed up 
*/


[SimpleJob(RuntimeMoniker.Net60)]
[MarkdownExporter, AsciiDocExporter, HtmlExporter, CsvExporter, RPlotExporter]
public class SqlVisitorCreationBenchmarks
{
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
        if (GeneratedSqlVisitor.Create(asNoOpt: false, NullLogger.Instance) is null)
        {
            throw new Exception("This should never happen...");
        }
    }

    [Benchmark]
    public void NoOptGenerate()
    {
        if (GeneratedSqlVisitor.Create(asNoOpt: true, NullLogger.Instance) is null)
        {
            throw new Exception("This should never happen...");
        }
    }

    [Benchmark]
    public void NoCacheGenerate()
    {
        if (GeneratedSqlVisitor.Create(asNoOpt: false, disableCache: true, NullLogger.Instance) is null)
        {
            throw new Exception("This should never happen...");
        }
    }
}