using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting.Logging;
using Moq;

namespace WrapperEmitter.Tests;

public class MaxOpGenerator<TInterface, TImplementation, TBase, TSidecar> : IInterfaceGenerator<TInterface, TImplementation, TSidecar>, IOverrideGenerator<TBase, TSidecar>
    where TImplementation : TInterface
    where TInterface : class
    where TBase : class
{ 
    private bool m_handleProtectedInterfaces;
    public MaxOpGenerator(bool handleProtectedInterfaces = true) => m_handleProtectedInterfaces = handleProtectedInterfaces;

    public string? ReplaceMethodCall(MethodInfo method) => HandleProtectedInterface(method);
    public string? ReplacePropertyCall(PropertyInfo property, bool forSet)
        => HandleProtectedInterface(forSet ? property.GetSetMethod(nonPublic: true) : property.GetGetMethod(nonPublic: true)); 
    public string? ReplaceEventCall(EventInfo @event, bool forRemove)
        => HandleProtectedInterface(forRemove ? @event.GetRemoveMethod(nonPublic: true) : @event.GetAddMethod(nonPublic: true));

    private string? HandleProtectedInterface(MethodInfo? method)
    {
        if (method is null)
        {
            throw new ApplicationException("Failed to get a method... how did that happen?");
        }

        if (!m_handleProtectedInterfaces)
        {
            // Skip all this logic... good luck!
            return null;
        }

        if (!method.DeclaringType!.IsInterface)
        {
            // Not an interface, leave it alone.
            return null;
        }

        if (!method.IsFamily)
        {
            // Not public, leave it alone.
            return null;
        }

        StringBuilder result = new();
        bool isVoid;
        if (method.IsSpecialName)
        {
            isVoid = method.ReturnType == typeof(void);
        }
        else
        {
            bool isAsync;
            (isVoid, isAsync) = this.TreatAs(method);
            if (isAsync)
            {
                // We need something to await
                result.AppendLine($"await {typeof(Task).FullTypeExpression()}.{nameof(Task.Yield)}();");
            }
        }
        if (isVoid)
        {
            result.AppendLine("/* Nothing to return */");
        }
        else
        {
            // This our bet bet to get "something" that will compile
            result.AppendLine("default;");
        }
        return result.ToString();
    }
}

// TODO: With new base MaxOpGenerator, it looks like this is not working...
public class MinOpGenerator<TInterface, TImplementation, TBase, TSidecar> : MaxOpGenerator<TInterface, TImplementation, TBase, TSidecar>
    where TImplementation : TInterface
    where TInterface : class
    where TBase : class
{
    public bool ShouldOverrideMethod(MethodInfo method) => ShouldOverride(method);
    public bool ShouldOverrideProperty(PropertyInfo property)
        => ShouldOverride(property.GetSetMethod(nonPublic: true) ?? property.GetGetMethod(nonPublic: true)); 
    public bool ShouldOverrideEvent(EventInfo @event)
        => ShouldOverride(@event.GetRemoveMethod(nonPublic: true) ?? @event.GetAddMethod(nonPublic: true));
    public bool TreatMethodAsync(MethodInfo method) => false;
    private static bool ShouldOverride(MethodInfo? method)
    {
        if (method is null)
        {
            throw new ApplicationException("Failed to get a method... how did that happen?");
        }
        var declaringType = method.DeclaringType;
        if (declaringType is null)
        {
            return false;
        }
        return declaringType.IsInterface && method.IsAbstract;
    }
}

public ref struct RefStruct { }

public interface DoNotCareType { }

public interface I1
{
    int SimpleInterfaceMethod() => 1;
    void SimpleInterfaceVoidMethod() { }
    char SimpleInterfaceProperty
    {
        get => 'a';
        set { /* no opt */ }
    }
    event EventHandler SimpleInterfaceEvent
    {
        add { /* no opt */ }
        remove { /* no opt */ }
    }
    Task<string> SimpleInterfaceAsync() => Task.FromResult("a string");
    double[] SimpleInterfaceArrayReturn => new[]{2.0};
    int this[int i, double d, TimeSpan ts] => 3;
    int SimpleInterfaceMethodParameters(TimeSpan ts, int i = -1, double d = -2.0, char c = 'b', string s = "s", int? ni = null, Task<DateTime>? t = null, Task<Dictionary<(int?, int[]), IList<int[]?>[]>>? wtf = null, params object?[] other) => 4;

    TAbc SimpleInterfaceGenericMethod<TAbc, TXyz>(TAbc abc, TXyz xyz) where TAbc : TXyz => abc;

    int SimpleInterfaceOutRefMethod(out int o, ref int r)
    {
        r++;
        o = 5;
        return o + r;
    }

    void SimpleInterfaceKeywords(
        int @int, string @string, double @double, float @float, char @char, bool @bool, byte @bytes, sbyte @sbyte, long @long, ulong @ulong, uint @uint, ushort @ushort, short @short, decimal @decimal, object @object, int @void,
        int @for, int @foreach, int @in, int @do, int @while, int @if, int @else, int @switch, int @case, int @using, int @lock, int @new, int @try, int @catch, int @finally, int @throw, int @goto, int @return, int @break, int @continue,
        int @class, int @interface, int @namespace, int @struct, int @enum, int @delegate, int @event,
        int @base, int @this, int @as, int @is, int @true, int @false, int @null, int @typeof, int @sizeof,
        int @private, int @public, int @protected, int @internal, int @override, int @virtual, int @abstract, int @readonly, int @static, int @const,
        int @out, int @ref, int @params, int @default) { }

    RefStruct SimpleInterfaceRefStructMethod() => new ();

    unsafe int* SimpleInterfacePointer(long* a, double*[] b, bool?* c, Guid?*[] d, DateTime*[]? e) => null;

#pragma warning disable IDE1006 // Naming Styles
    void @return() { }
    int @default { get => default; }
#pragma warning restore IDE1006 // Naming Styles
}

public interface ITrackingSidecar
{
    void PreCall(string description, MethodBase? callingMethod, params object?[] args);
    void PostCallWithReturn<T>(T returnValue, string description, [CallerMemberName] string callerName = "", [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = -1);
    void PostCallWithoutReturn(string description, [CallerMemberName] string callerName = "", [CallerFilePath] string callerFilePath = "", [CallerLineNumber] int callerLineNumber = -1);
}

public interface I2
{
    int this[int i] { get; set; }
    int ReadonlyProperty { get; }
    int ReadWriteProperty { get; set; }
    int Function();
    void Method(out int o, ref int r);
    Task<int> TaskMethodAsync();
    Task<int> TaskMethodToNotAsync();
}

public interface IBottom
{
    int FooBottom();
}

public interface IMiddle : IBottom
{
    int FooMiddle();
}
public interface ITop : IMiddle
{
    int FooTop();
}

public interface IOther
{
    int FooOther();
}

public interface ICombined : ITop, IOther 
{ 
    int FooCombined();
}

public interface IProtectedInterface
{
    protected int FooMethod(); 

    int Foo { get; protected set; }

    int OtherFoo { protected get; set; }

    protected event EventHandler FooEvent
    {
        add { /* no opt */ }
        remove { /* no opt */ }
    }
}

public class C1 : I1 
{
    public virtual int VirtualMethod() => 6;
    public virtual void VirtualVoidMethod() { }
    public virtual char VirtualProperty { get; set; }
    public virtual event EventHandler VirtualEvent
    {
        add { /* no opt */ }
        remove { /* no opt */ }
    }

    public virtual Task VirtualAsync() => Task.CompletedTask;
    public virtual int this[params object?[] args]
    {
        get => 7;
        set { /* no opt */ }
    }
    protected virtual int ProtectedVirtualMethod() => 8;

    public virtual RefStruct VirtualRefStruct() => new ();

    public virtual unsafe int* VirtualPointer(long* a) => null;
}

public class C2 : I2
{
    public C2() => Array.Fill(BackingArray, 1000);
    public int[] BackingArray = new int[10];
    public int BackingInt;
    public virtual int this[int i]
    {
        get => BackingArray[i];
        set => BackingArray[i] = value;
    }

    public virtual int ReadonlyProperty => 1;

    public virtual int ReadWriteProperty 
    {
      get => BackingInt;
      set => BackingInt = value;
    }

    public virtual int Function() => 3;

    public virtual void Method(out int o, ref int r)
    {
        r++;
        o = r * 2;
    }

    public virtual Task<int> TaskMethodAsync() => Task.FromResult(4);

    public virtual Task<int> TaskMethodToNotAsync() => Task.FromResult(5);
}

public class C3
{
    public virtual int X { get; init; }
}

public class TrackingSidecar : 
      ITrackingSidecar,
      IInterfaceGenerator<I1, C1, ITrackingSidecar>,
      IOverrideGenerator<C1, ITrackingSidecar>
{
    public record Callable(string Name)
    {
        public virtual Expression<Action<TrackingSidecar>>PostCallMockExpression(string description)
            => (x) => x.PostCallWithoutReturn(description, Name, It.IsAny<string>(), It.IsAny<int>());
    }

    public abstract record CallableWithReturn(string Name) : Callable (Name) { }
    public record CallableWithReturn<T>(string Name) : CallableWithReturn(Name)
    {
        public override Expression<Action<TrackingSidecar>>PostCallMockExpression(string description)
          => (x) => x.PostCallWithReturn(It.IsAny<T>(), description, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>());
    }

    public static Expression<Action<TrackingSidecar>> PreCallMockExpression(string description) 
        => (x) => x.PreCall(description, It.IsAny<MethodBase?>(), It.IsAny<object?[]>());

    public virtual void PreCall(string description, MethodBase? callingMethod, params object?[] args)
    {
        Log($"Pre {description}: [{callingMethod}]");
        foreach (var arg in args)
        {
            var typeText = (arg is null) ? "<null>" : args.GetType().FullTypeExpression();
            Log($"  {typeText}: {arg}");
        }
    }

    public virtual void PostCallWithReturn<T>(T returnValue, string description, string callerName, string callerFilePath, int callerLineNumber) 
        => Log($"Post {description}: {typeof(T).FullTypeExpression()} Return Value:[{returnValue}] Caller Name:[{callerName}] Caller File Path[{callerFilePath}] Caller Line Number:[{callerLineNumber}]");

    public virtual void PostCallWithoutReturn(string description, string callerName, string callerFilePath, int callerLineNumber)
        => Log($"Post {description}: Caller Name:[{callerName}] Caller File Path[{callerFilePath}] Caller Line Number:[{callerLineNumber}]");

    public string? PreMethodCall(MethodInfo methodInfo) => PreCall(methodInfo);
    public string? PostMethodCall(MethodInfo methodInfo) => PostCall(methodInfo);

    public string? PrePropertyCall(PropertyInfo propertyInfo, bool forSet)
        => PreCall((forSet ? propertyInfo.GetSetMethod(nonPublic: true)! : propertyInfo.GetGetMethod(nonPublic: true))!);
    public string? PostPropertyCall(PropertyInfo propertyInfo, bool forSet)
        => PostCall((forSet ? propertyInfo.GetSetMethod(nonPublic: true)! : propertyInfo.GetGetMethod(nonPublic: true))!);

    public string? PreEventCall(EventInfo eventInfo, bool forRemove)
        => PreCall(forRemove ? eventInfo.GetRemoveMethod(nonPublic: true)! : eventInfo.GetAddMethod(nonPublic: true)!);
    public string? PostEventCall(EventInfo eventInfo, bool forRemove)
        => PostCall(forRemove ? eventInfo.GetRemoveMethod(nonPublic: true)! : eventInfo.GetAddMethod(nonPublic: true)!);

    static private string PreCall(MethodInfo info)
    {
        // During the pre call we can't attempt to read out parameters (b/c they have not be initialized... they are out parameters after all)
        // ref struct and pointers can't be boxed, so we can pass them alone...
        var paramText = string.Join(", ", 
            info.GetParameters()
                .Where(x => !(x.IsOut || x.ParameterType.IsByRefLike || x.ParameterType.IsPointer)).Select(x => Generator.SanitizeName(x.Name!)));
        if (!string.IsNullOrEmpty(paramText))
        {
            paramText = $", {paramText}";
        }

        return $"{Generator.SidecarVariableName}.{nameof(PreCall)}(\"{info.Name}\", {typeof(MethodBase).FullTypeExpression()}.{nameof(MethodBase.GetCurrentMethod)}(){paramText});";
    }

    private string PostCall(MethodInfo methodInfo)
    {
        var returnType = methodInfo.ReturnType;
        bool isVoid;
        if (methodInfo.IsSpecialName)
        {
            // This more or less means it is not a "normal" method AKA can't be async (it can be Task and those should not be swopped out to void)
            isVoid = returnType == typeof(void);
        }
        else
        {
            (isVoid, _) = this.TreatAs(methodInfo);
        }

        // ref structs can't be used as generic arguments (and neither can pointers)
        return isVoid || returnType.IsByRefLike || returnType.IsPointer
            ? $"{Generator.SidecarVariableName}.{nameof(PostCallWithoutReturn)}(\"{methodInfo.Name}\");"
            : $"{Generator.SidecarVariableName}.{nameof(PostCallWithReturn)}({Generator.ReturnVariableName}, \"{methodInfo.Name}\");";
    }

    private static void Log(string x) => Logger.LogMessage(x.Replace("{", "{{").Replace("}", "}}"));
}

public interface IReturnValidatingSidecar : I2 { }

public class ReturnValidatingSidecar : 
    C2,
    IReturnValidatingSidecar,
    IInterfaceGenerator<I2, C2, IReturnValidatingSidecar>,
    IOverrideGenerator<C2, IReturnValidatingSidecar>
{
    public List<Callable<I2>> InterfaceCallableItems = new ()
    {
        new(x => x[0]),
        new(x => { x[1] = 100; return x[1]; }),
        new(x => x.ReadonlyProperty),
        new(x => { x.ReadWriteProperty = 200; return x.ReadWriteProperty; }),
        new(x => x.Function()),
        new(x => { int r = 300; x.Method(out int o, ref r); return o; }),
        new(x => { int r = 400; x.Method(out int o, ref r); return r; }),
        new(async x => await x.TaskMethodAsync()),
        new(async x => await x.TaskMethodToNotAsync()),
    };

    public List<Callable<C2>> ClassCallableItems = new ()
    {
        new(x => x[2]),
        new(x => { x[3] = 500; return x[3]; }),
        new(x => x.ReadonlyProperty),
        new(x => { x.ReadWriteProperty = 600; return x.ReadWriteProperty; }),
        new(x => x.Function()),
        new(x => { int r = 700; x.Method(out int o, ref r); return o; }),
        new(x => { int r = 800; x.Method(out int o, ref r); return r; }),
        new(async x => await x.TaskMethodAsync()),
        new(async x => await x.TaskMethodToNotAsync()),
    };

    public class Callable<T> where T : I2
    {
        private readonly string m_text;
        private readonly Func<T, int>? m_func;
        private readonly Func<T, Task<int>>? m_funcAsync;
        public Callable(Func<T, int> func, [CallerArgumentExpression("func")] string text = "")
        {
            m_func = func;
            m_text = text;
            m_funcAsync = null;
        }

        public Callable(Func<T, Task<int>> funcAsync, [CallerArgumentExpression("funcAsync")] string text = "")
        {
            m_funcAsync = funcAsync;
            m_text = text;
            m_func = null;
        }

        private async Task<int>GetValueAsync(T obj)
        {
            if (m_funcAsync is not null)
            {
                return await m_funcAsync(obj);
            }
            return m_func!(obj);
        }

        public async Task AssertAreEqualAsync(T expected, T actual)
        {
            var expectedValue = await GetValueAsync(expected);
            var actualValue = await GetValueAsync(actual);
            Assert.AreEqual(expectedValue, actualValue, $"For {m_text} did NOT match, Expected: {expectedValue} Got: {actualValue}");
        }

        public async Task AssertAreNotEqualAsync(T expected, T actual)
        {
            var expectedValue = await GetValueAsync(expected);
            var actualValue = await GetValueAsync(actual);
            Assert.AreNotEqual(expectedValue, actualValue, $"For {m_text} DID match, Got {expectedValue}  For both");
        }
    }

    public static async Task AssertAreEqual<T>(List<Callable<T>> callableItems, T expected, T actual) where T : I2
    {
        foreach (var item in callableItems)
        {
            await item.AssertAreEqualAsync(expected, actual);
        }
    }

    public static async Task AssertAreNotEqual<T>(List<Callable<T>> callableItems, T expected, T actual) where T : I2
    {
        foreach (var item in callableItems)
        {
            await item.AssertAreNotEqualAsync(expected, actual);
        }
    }

    public static async Task AssertMatches<T>(bool shouldEqual, List<Callable<T>> callableItems, T expected, T actual) where T : I2
    {
        if (shouldEqual)
        {
            await AssertAreEqual(callableItems, expected, actual);
        }
        else
        {
            await AssertAreNotEqual(callableItems, expected, actual);
        }
    }

    private readonly bool m_doOverrides;
    public ReturnValidatingSidecar(bool doOverrides) => m_doOverrides = doOverrides;
    public bool TreatMethodAsync(MethodInfo methodInfo) => methodInfo.Name != nameof(I2.TaskMethodToNotAsync);
    public bool ShouldOverrideMethod(MethodInfo methodInfo)
    {
        if (methodInfo.DeclaringType == typeof(C2))
        {
            return m_doOverrides;
        }
        else if (methodInfo.DeclaringType == typeof(I2))
        {
            return m_doOverrides || methodInfo.IsAbstract;
        }
        else
        {
            return false;
        }
    }

    public bool ShouldOverrideProperty(PropertyInfo propertyInfo) 
        => ShouldOverrideMethod((propertyInfo.GetSetMethod(nonPublic: true) ?? propertyInfo.GetGetMethod(nonPublic: true))!);
    public bool ShouldOverrideEvent(EventInfo eventInfo) 
        => ShouldOverrideMethod((eventInfo.GetRemoveMethod(nonPublic: true) ?? eventInfo.GetAddMethod(nonPublic: true))!);

    public string? ReplaceMethodCall(MethodInfo methodInfo)
    {
        if (!m_doOverrides)
        {
            return null;
        }
        return methodInfo.Name switch
        {
            nameof(I2.Method) => $"{Generator.SidecarVariableName}.{nameof(I2.Method)}(out o, ref r);",
            nameof(I2.TaskMethodAsync) => $"await {Generator.SidecarVariableName}.{nameof(I2.TaskMethodAsync)}();",
            _ => $"{Generator.SidecarVariableName}.{methodInfo.Name}();",
        };
    }

    public string? ReplacePropertyCall(PropertyInfo propertyInfo, bool forSet)
    {
        if (!m_doOverrides)
        {
            return null;
        }
        if (propertyInfo.GetIndexParameters().Any())
        {
            return forSet
                ? $"{Generator.SidecarVariableName}[i] = value;"
                : $"{Generator.SidecarVariableName}[i];";
        }
        else
        {
            return forSet
                ? $"{Generator.SidecarVariableName}.{propertyInfo.Name} = value;"
                : $"{Generator.SidecarVariableName}.{propertyInfo.Name};";
        }
    }
    string? ReplaceEventCall(EventInfo eventInfo, bool forRemove) => throw new NotImplementedException("Need to Handel Events");

    public override int this[int i]
    {
        get => 2 * base[i];
        set => base[i] = -value; 
    }
    public override int ReadonlyProperty => -base.ReadonlyProperty;
    public override int ReadWriteProperty
    {
        get => 2 * base.ReadWriteProperty;
        set => base.ReadWriteProperty = -value;
    }
    public override int Function() => -base.Function();
    public override void Method(out int o, ref int r)
    {
        r--;
        o = r / 2;
    }
    public override async Task<int> TaskMethodAsync() => -await base.TaskMethodAsync();
    public override async Task<int> TaskMethodToNotAsync() => -await base.TaskMethodToNotAsync();
}

public class InterfaceInheritances : ITop
{
    public int FooBottom() => default;
    public int FooMiddle() => default;
    public int FooTop() => default;
}

public class InterfaceCombined : ICombined
{
    public int FooBottom() => default;
    public int FooCombined() => default;
    public int FooMiddle() => default;
    public int FooOther() => default;
    public int FooTop() => default;
}

public class ProtectedImplementer : IProtectedInterface
{
    public virtual int Foo { get => default; set {} }
    
    public virtual int OtherFoo { get => default; set {} }

    int IProtectedInterface.FooMethod() => default;

    protected virtual int BarMethod() => default;
    public virtual int Bar { protected get => default; set {} }
    public virtual int OtherBar { get => default; protected set {} }

    protected virtual event EventHandler BarEvent
    {
        add { /* No Opt */ }
        remove { /* No Opt */ }
    }
}

public class ConstructorClass
{
    public int I { get; init; }
    public double D { get; init; }
    public ConstructorClass(int i, ref double d, out long l)
    {
        I = i;
        D = d;

        d *= 10.0;
        l = 10;
    }
}