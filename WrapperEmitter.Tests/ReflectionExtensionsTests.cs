using System.Collections.Specialized;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting.Logging;

namespace WrapperEmitter.Tests;

[TestClass]
public class ReflectionExtensionsTests
{
    private readonly (Type Type, string Expression)[] s_types = new [] {
        (typeof(int), "@System.@Int32"),
        (typeof(object), "@System.@Object"),
        (typeof(string), "@System.@String"),
        (typeof(long), "@System.@Int64"),
        (typeof(double), "@System.@Double"),
        (typeof(float), "@System.@Single"),
        (typeof(char), "@System.@Char"),
        (typeof(bool), "@System.@Boolean"),
        (typeof(Guid), "@System.@Guid"),
        (typeof(DateTime), "@System.@DateTime"),
        (typeof(void), "void"),
        (typeof(int[]), "@System.@Int32[]"),
        (typeof(int[][]), "@System.@Int32[][]"),
        (typeof(int[][][]), "@System.@Int32[][][]"),
        (typeof(int[,]), "@System.@Int32[,]"),
        (typeof(int[,][]), "@System.@Int32[,][]"),
        (typeof(int[][,]), "@System.@Int32[][,]"),
        (typeof(int?), "@System.@Nullable<@System.@Int32>"),
        (typeof(int?[]), "@System.@Nullable<@System.@Int32>[]"),
        (typeof(Type), "@System.@Type"),
        (typeof(IList<int>), "@System.@Collections.@Generic.@IList<@System.@Int32>"),
        (typeof(IList<int[]>), "@System.@Collections.@Generic.@IList<@System.@Int32[]>"),
        (typeof(IList<int>[]), "@System.@Collections.@Generic.@IList<@System.@Int32>[]"),
        (typeof(IList<int[]>[]), "@System.@Collections.@Generic.@IList<@System.@Int32[]>[]"),
        (typeof(IList<int?>), "@System.@Collections.@Generic.@IList<@System.@Nullable<@System.@Int32>>"),
        (typeof(IList<int?[]>[]), "@System.@Collections.@Generic.@IList<@System.@Nullable<@System.@Int32>[]>[]"),
        (typeof(List<int>), "@System.@Collections.@Generic.@List<@System.@Int32>"),
        (typeof(List<int[]>), "@System.@Collections.@Generic.@List<@System.@Int32[]>"),
        (typeof(List<int>[]), "@System.@Collections.@Generic.@List<@System.@Int32>[]"),
        (typeof(List<int[]>[]), "@System.@Collections.@Generic.@List<@System.@Int32[]>[]"),
        (typeof(List<int?>), "@System.@Collections.@Generic.@List<@System.@Nullable<@System.@Int32>>"),
        (typeof(List<int?[]>[]), "@System.@Collections.@Generic.@List<@System.@Nullable<@System.@Int32>[]>[]"),
        (typeof(IDictionary<int,long>), "@System.@Collections.@Generic.@IDictionary<@System.@Int32, @System.@Int64>"),
        (typeof(List<Dictionary<int?[][,],long[][,]>[][,]>), "@System.@Collections.@Generic.@List<@System.@Collections.@Generic.@Dictionary<@System.@Nullable<@System.@Int32>[][,], @System.@Int64[][,]>[][,]>"),
        (typeof((int A, long B)), "@System.@ValueTuple<@System.@Int32, @System.@Int64>"),
        (typeof((int A, long B)?), "@System.@Nullable<@System.@ValueTuple<@System.@Int32, @System.@Int64>>"),
        (typeof((int A, long B)[]), "@System.@ValueTuple<@System.@Int32, @System.@Int64>[]"),
        (typeof((List<Dictionary<int?[][,],long[][,]>[][,]> A, long B)[]), "@System.@ValueTuple<@System.@Collections.@Generic.@List<@System.@Collections.@Generic.@Dictionary<@System.@Nullable<@System.@Int32>[][,], @System.@Int64[][,]>[][,]>, @System.@Int64>[]"),
        (typeof((int A, int B, int C, int D, int E, int F, int G, int H, int J, int K, int L, int N, int M, int O, int P, int Q, int R, int S, int T, int U, int V, int W, int X, int Y, int Z)), 
          "@System.@ValueTuple<@System.@Int32, @System.@Int32, @System.@Int32, @System.@Int32, @System.@Int32, @System.@Int32, @System.@Int32, @System.@ValueTuple<@System.@Int32, @System.@Int32, @System.@Int32, @System.@Int32, @System.@Int32, @System.@Int32, @System.@Int32, @System.@ValueTuple<@System.@Int32, @System.@Int32, @System.@Int32, @System.@Int32, @System.@Int32, @System.@Int32, @System.@Int32, @System.@ValueTuple<@System.@Int32, @System.@Int32, @System.@Int32, @System.@Int32>>>>"),
        (typeof((int A, (int B, int C) D)), "@System.@ValueTuple<@System.@Int32, @System.@ValueTuple<@System.@Int32, @System.@Int32>>"),
        (typeof((int @int, long @long)), "@System.@ValueTuple<@System.@Int32, @System.@Int64>"),

#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
        (typeof(int*), "@System.@Int32*"),
        (typeof(Type*), "@System.@Type*"),
        (typeof(int*[]), "@System.@Int32*[]"),
        (typeof(int[]*), "@System.@Int32[]*"),
        (typeof(int*[]*), "@System.@Int32*[]*"),
        (typeof(int*[]*[,]), "@System.@Int32*[]*[,]"),
        (typeof(int**), "@System.@Int32**"),
        (typeof(int?*), "@System.@Nullable<@System.@Int32>*"),
        (typeof((int A, long B)*[]**[,]***[,,]****), "@System.@ValueTuple<@System.@Int32, @System.@Int64>*[]**[,]***[,,]****"),
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type

        // Nested Class, just a radom one from the CLR
        (typeof(NameObjectCollectionBase.@KeysCollection), "@System.@Collections.@Specialized.@NameObjectCollectionBase.@KeysCollection"),
        // This external type needs to have it reference included, and it is hidden within this Generics (and Arrays, but those don't need special handling)
        // This would have failed b/c without special handling we would not notice we need to include the reference
        (typeof(List<AccessLevel?[]>[]), "@System.@Collections.@Generic.@List<@System.@Nullable<@WrapperEmitter.@AccessLevel>[]>[]"),

        /*
        TODO: These can't included b/c of follow TODO from ReflectionExtensions.cs
        // TODO: falling back to type.Name here allows use deal with open generic, but it also changes typeof(List<>) => "List<T>"
        (typeof(IList<>), "@System.@Collections.@Generic.@IList<T>"),
        (typeof(List<>), "@System.@Collections.@Generic.@List<T>"),
        (typeof(IDictionary<,>), "@System.@Collections.@Generic.@IDictionary<TKey,TValue>"),
        */
    };

    [TestMethod]
    public void FullTypeExpression_ExpectedValues() => AssertExpectedValues(s_types);

    [TestMethod]
    public void FullTypeExpression_More_ExpectedValues() => AssertExpectedValues(MoreTestObject.Types);

    [TestMethod]
    public void FullTypeExpression_AsCode() => AssertAsCode(s_types);

    [TestMethod]
    public void FullTypeExpression_More_AsCode() => AssertAsCode(MoreTestObject.Types);

    private static void AssertExpectedValues((Type Type, string Expression)[] types)
    {
        foreach (var item in types)
        {
            Assert.AreEqual(item.Expression, item.Type.FullTypeExpression(), $"Expression:{item.Expression} | Type:{item.Type} | Text:{item.Type.FullTypeExpression()} ");
        }   
    }

    private static void AssertAsCode((Type Type, string Expression)[] types)
    {
        var @namespace = "TestNamespace";
        var className = "TestClassName";
        var fieldName = "TestField";
        var code = $@"
namespace {@namespace};
public static class {className}
{{
    // Using object here because we want the 'simplest' type that will meet our needs
    public static readonly object[] {fieldName} = new object[]
    {{
{string.Join(Environment.NewLine, types.Select(x => $"typeof({x.Type.FullTypeExpression()}),"))}
    }}; 
}}
";
        Logger.LogMessage(code.Replace("{", "{{").Replace("}", "}}"));
        ClassCreationDefinition definition = new(
            code,
            @namespace,
            className,
            types.Select(x => x.Type),
            parseOptions: null,
            compilationOptions: Generator.DefaultCompilationOptions);

        var type = Generator.CreateType(definition, NullLogger.Instance, LogLevel.None);
        var field = type.GetField(fieldName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            ?? throw new ApplicationException($"Failed to find {fieldName}");
        
        var codeTypes = (object[])field.GetValue(obj: null)!;

        Assert.AreEqual(types.Length, codeTypes.Length);

        for (int i = 0; i < types.Length; i++)
        {
            Assert.AreEqual(types[i].Type, codeTypes[i], $"Expression:{types[i].Expression} | Type:{types[i].Type} | Actual:{codeTypes[i]} ");
        }
    }
}