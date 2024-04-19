using System.Collections.Specialized;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting.Logging;

namespace WrapperEmitter.Tests;

[TestClass]
public class ReflectionExtensionsTests
{
    private static readonly (Type Type, string Expression)[] s_types = new [] {
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
    };

    private static readonly (Type Type, string Expression)[] s_openTypes = new [] {
        (typeof(IList<>), "@System.@Collections.@Generic.@IList<>"),
        (typeof(List<>), "@System.@Collections.@Generic.@List<>"),
        (typeof(IDictionary<, >), "@System.@Collections.@Generic.@IDictionary<, >"),
        // FYI: These Yields an "Unexpected use of an unbound generic name"
        //(FooParameterType(3), "@System.@Collections.@Generic.@Dictionary<@System.@Collections.@Generic.@List<>, >"),
        //(FooParameterType(4), "@System.@Collections.@Generic.@Dictionary<@System.@Collections.@Generic.@List<>, @System.@Int32>"),
    };

    private static readonly (Type Type, string Expression)[] s_identifiedTypes = new [] {
        (typeof(IList<>), "@System.@Collections.@Generic.@IList<{0}>"),
        (typeof(List<>), "@System.@Collections.@Generic.@List<{0}>"),
        (typeof(IDictionary<, >), "@System.@Collections.@Generic.@IDictionary<{0}, {1}>"),
        (FooParameterType(0), "@System.@Collections.@Generic.@List<{0}>"),
        (FooParameterType(1), "@System.@Collections.@Generic.@List<{1}>"),
        (FooParameterType(2), "@System.@Collections.@Generic.@Dictionary<{1}, {0}>"),
        (FooParameterType(3), "@System.@Collections.@Generic.@Dictionary<@System.@Collections.@Generic.@List<{1}>, {1}>"),
        (FooParameterType(4), "@System.@Collections.@Generic.@Dictionary<@System.@Collections.@Generic.@List<{1}>, @System.@Int32>")
    };

    private static MethodBase Foo<X, Y>(List<X> x0, List<Y> x1, Dictionary<Y, X> x2, Dictionary<List<Y>, Y> x3, Dictionary<List<Y>, int> x4)
        where Y : struct // Doing this so that Y can't be null, and can there for be used as a Dictionary Key
        => MethodBase.GetCurrentMethod()!;

    private static Type FooParameterType(int parameterIndex)
    {
        // the fact that we have to provide types for X/Y does not really matter b/c we get open version of the method back
        var method = (MethodInfo)Foo<char, char>(default!, default!, default!, default!, default!);
        return method.GetParameters()[parameterIndex].ParameterType;
    }

    [DataTestMethod]
    [DataRow(OpenGenericOption.Name)]
    [DataRow(OpenGenericOption.LeaveOpen)]
    [DataRow(OpenGenericOption.Identify)]
    public void FullTypeExpression_ExpectedValues(OpenGenericOption openGenericOption) => AssertExpectedValues(s_types, openGenericOption);

    [DataTestMethod]
    [DataRow(OpenGenericOption.Name)]
    [DataRow(OpenGenericOption.LeaveOpen)]
    [DataRow(OpenGenericOption.Identify)]
    public void FullTypeExpression_More_ExpectedValues(OpenGenericOption openGenericOption) => AssertExpectedValues(MoreTestObject.Types, openGenericOption);

    [DataTestMethod]
    [DataRow(OpenGenericOption.Name)]
    [DataRow(OpenGenericOption.LeaveOpen)]
    [DataRow(OpenGenericOption.Identify)]
    public void FullTypeExpression_AsCode(OpenGenericOption openGenericOption) => AssertAsCode(s_types, openGenericOption);

    [DataTestMethod]
    [DataRow(OpenGenericOption.Name)]
    [DataRow(OpenGenericOption.LeaveOpen)]
    [DataRow(OpenGenericOption.Identify)]
    public void FullTypeExpression_More_AsCode(OpenGenericOption openGenericOption) => AssertAsCode(MoreTestObject.Types, openGenericOption);

    [TestMethod]
    public void FullTypeExpression_Open_ExpectedValues() => AssertExpectedValues(s_openTypes, OpenGenericOption.LeaveOpen);

    [TestMethod]
    public void FullTypeExpression_Open_AsCode() => AssertAsCode(s_openTypes, OpenGenericOption.LeaveOpen);

    [TestMethod]
    public void FullTypeExpression_MoreOpen_ExpectedValues() => AssertExpectedValues(MoreTestObject.OpenTypes, OpenGenericOption.LeaveOpen);

    [TestMethod]
    public void FullTypeExpression_MoreOpen_AsCode() => AssertAsCode(MoreTestObject.OpenTypes, OpenGenericOption.LeaveOpen);

    [TestMethod]
    public void FullTypeExpression_Identified_ExpectedValues() => AssertExpectedValues(s_identifiedTypes, OpenGenericOption.Identify);

    private static void AssertExpectedValues((Type Type, string Expression)[] types, OpenGenericOption openGenericOption)
    {
        foreach (var item in types)
        {
            Assert.AreEqual(item.Expression, item.Type.FullTypeExpression(openGenericOption), $"Expression:{item.Expression} | Type:{item.Type} | Text:{item.Type.FullTypeExpression(openGenericOption)} ");
        }   
    }

    private static void AssertAsCode((Type Type, string Expression)[] types, OpenGenericOption openGenericOption)
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
{string.Join(Environment.NewLine, types.Select(x => $"typeof({x.Type.FullTypeExpression(openGenericOption)}),"))}
    }}; 
}}
";
        Logger.LogMessage("{0}", code);
        var type = Generator.CreateType(
            code,
            @namespace,
            className,
            types.Select(x => x.Type),
            parseOptions: null,
            compilationOptions: Generator.DefaultCompilationOptions,
            NullLogger.Instance,
            LogLevel.None);
        var field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.Static)
            ?? throw new ApplicationException($"Failed to find {fieldName}");
        
        var codeTypes = (object[])field.GetValue(obj: null)!;

        Assert.AreEqual(types.Length, codeTypes.Length);

        for (int i = 0; i < types.Length; i++)
        {
            Assert.AreEqual(types[i].Type, codeTypes[i], $"Expression:{types[i].Expression} | Type:{types[i].Type} | Actual:{codeTypes[i]} ");
        }
    }
}