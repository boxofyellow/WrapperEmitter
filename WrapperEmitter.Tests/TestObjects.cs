using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.TestTools.UnitTesting.Logging;
using Moq;

namespace WrapperEmitter.Tests;

public class MaxOpGenerator<TInterface, TImplementation, TBase, TSidecar> : IInterfaceGenerator<TInterface, TImplementation, TSidecar>, IOverrideGenerator<TBase, TSidecar>
    where TImplementation : TInterface
    where TInterface : class
    where TBase : class
{ }

public class MinOpGenerator<TInterface, TImplementation, TBase, TSidecar> : MaxOpGenerator<TInterface, TImplementation, TBase, TSidecar>,
    IGenerator // Need to include this so this class implement these methods
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

public class RestrictedGenerator<TInterface, TImplementation, TBase, TSidecar> : MaxOpGenerator<TInterface, TImplementation, TBase, TSidecar>,
    IGenerator // Need to include this so this class implement these methods
    where TImplementation : TInterface
    where TInterface : class
    where TBase : class
{
    public string? ReplaceMethodCall(MethodInfo method, GeneratorSupport support) => ReplaceCall(method, support);
    public string? ReplacePropertyCall(PropertyInfo property, bool forSet, GeneratorSupport support) 
        => ReplaceCall(forSet ? property.GetSetMethod(nonPublic: true) : property.GetGetMethod(nonPublic: true), support);
    public string? ReplaceEventCall(EventInfo @event, bool forRemove, GeneratorSupport support)
        => ReplaceCall(forRemove ? @event.GetRemoveMethod(nonPublic: true) : @event.GetAddMethod(nonPublic: true), support);

    private string? ReplaceCall(MethodInfo? method, GeneratorSupport support)
    {
        if (method is null)
        {
            throw UnexpectedReflectionsException.FailedToGetAccessor();
        }

        (var _, var asAsync) = Generator.TreatAs(this, method);

        var item = support.AddRestrictedMethod(method, asConcrete: true);

        var thatVariableName = method.DeclaringType!.IsInterface 
                             ? Generator.ImplementationVariableName
                             : "this";

        return (asAsync ? "await " : string.Empty) + Generator.RestrictedHelperCallText(thatVariableName, item);
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

    void SimpleInterfaceRefStructParameter(RefStruct @ref) { }

    Task SimpleInterfaceRefStructParameterAsync(RefStruct @ref) => Task.CompletedTask;

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
    event EventHandler? Event;
    void RaiseEvent();
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

    protected Tout FooGeneric<Tin, Tout>(Tin input);

    int Foo { get; protected set; }

    int OtherFoo { protected get; set; }

    // this protected init can never be be referenced
    // this can only be used during a construct, and interfaces can't have a construct
    protected int InitFoo { get; init; }

    unsafe protected int* FooUnsafe<X>(int* p);

    protected event EventHandler FooEvent
    {
        add { /* no opt */ }
        remove { /* no opt */ }
    }

    unsafe public sealed (int fooMethod, Tout fooGeneric, int foo, int otherFoo) DoAll<Tin, Tout>(Tin input, int setFoo, int setOtherFoo, int *unsafeParam, out int* unsafeOut)
    {
        Foo = setFoo;
        OtherFoo = setOtherFoo;
        unsafeOut = FooUnsafe<int>(unsafeParam);
        return (
            FooMethod(),
            FooGeneric<Tin, Tout>(input),
            Foo,
            OtherFoo
        );
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

    public virtual void VirtualRefStructParameter(RefStruct @ref) { }

    public virtual Task VirtualRefStructParameterAsync(RefStruct @ref) => Task.CompletedTask;

    public virtual unsafe int* VirtualPointer(long* a) => null;
}

public class C2 : I2
{
    public C2() => Array.Fill(BackingArray, 1000);
    public int[] BackingArray = new int[10];
    public int BackingInt;

    public virtual event EventHandler? Event;

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

    public virtual void RaiseEvent() => Event?.Invoke(this, EventArgs.Empty);
}

public class C3
{
    public virtual int X { get; init; }
}

public class TrackingSidecar<TInterface, TImplementation> : 
    ITrackingSidecar,
    IInterfaceGenerator<TInterface, TImplementation, ITrackingSidecar>,
    IOverrideGenerator<TImplementation, ITrackingSidecar>
    where TImplementation : class, TInterface
    where TInterface : class
{
    public record Callable(string Name, string? Prefix = null)
    {
        public virtual Expression<Action<TrackingSidecar<TInterface, TImplementation>>> PostCallMockExpression(string description)
            => (x) => x.PostCallWithoutReturn(description, $"{Prefix}{Name}", It.IsAny<string>(), It.IsAny<int>());
    }

    public abstract record CallableWithReturn(string Name, string? Prefix = null) : Callable (Name, Prefix) { }
    public record CallableWithReturn<T>(string Name, string? Prefix = null) : CallableWithReturn(Name, Prefix)
    {
        public override Expression<Action<TrackingSidecar<TInterface, TImplementation>>> PostCallMockExpression(string description)
          => (x) => x.PostCallWithReturn(It.IsAny<T>(), description, $"{Prefix}{Name}", It.IsAny<string>(), It.IsAny<int>());
    }

    public static Expression<Action<TrackingSidecar<TInterface, TImplementation>>> PreCallMockExpression(string description) 
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

    public string? PreMethodCall(MethodInfo method, GeneratorSupport support) => PreCall(method);
    public string? PostMethodCall(MethodInfo method, GeneratorSupport support) => PostCall(method);

    public string? PrePropertyCall(PropertyInfo property, bool forSet, GeneratorSupport support)
        => PreCall((forSet ? property.GetSetMethod(nonPublic: true)! : property.GetGetMethod(nonPublic: true))!);
    public string? PostPropertyCall(PropertyInfo property, bool forSet, GeneratorSupport support)
        => PostCall((forSet ? property.GetSetMethod(nonPublic: true)! : property.GetGetMethod(nonPublic: true))!);

    public string? PreEventCall(EventInfo @event, bool forRemove, GeneratorSupport support)
        => PreCall(forRemove ? @event.GetRemoveMethod(nonPublic: true)! : @event.GetAddMethod(nonPublic: true)!);
    public string? PostEventCall(EventInfo @event, bool forRemove, GeneratorSupport support)
        => PostCall(forRemove ? @event.GetRemoveMethod(nonPublic: true)! : @event.GetAddMethod(nonPublic: true)!);

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

    private string PostCall(MethodInfo method)
    {
        var returnType = method.ReturnType;
        bool isVoid;
        if (method.IsSpecialName)
        {
            // This more or less means it is not a "normal" method AKA can't be async (it can be Task and those should not be swopped out to void)
            isVoid = returnType == typeof(void);
        }
        else
        {
            (isVoid, _) = this.TreatAs(method);
        }

        // ref structs can't be used as generic arguments (and neither can pointers)
        return isVoid || returnType.IsByRefLike || returnType.IsPointer
            ? $"{Generator.SidecarVariableName}.{nameof(PostCallWithoutReturn)}(\"{method.Name}\");"
            : $"{Generator.SidecarVariableName}.{nameof(PostCallWithReturn)}({Generator.ReturnVariableName}, \"{method.Name}\");";
    }

    private static void Log(string x) => Logger.LogMessage("{0}", x);
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
        Bag.EventCallable<I2>(),
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
        Bag.EventCallable<C2>(),
    };

    private class Bag
    {
        public int Value;
        public void EventCallBack(object? sender, EventArgs e) => Value++;

        public static Callable<T> EventCallable<T>() where T : I2 => new(x =>
        {
            Bag bag = new();
            x.Event += bag.EventCallBack;
            x.RaiseEvent();
            x.Event -= bag.EventCallBack;
            x.RaiseEvent();
            return bag.Value;
        });
    }

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

        public async Task<int>GetValueAsync(T obj)
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

    public async Task AssertEventAreConfiguredCorrectly<T>(T t) where T : I2
    {
        var mine = await Bag.EventCallable<C2>().GetValueAsync(this);
        Assert.AreEqual(m_events.Length, mine, $"For events we should be expected to call them once for each of our handlers");
        var @default = await Bag.EventCallable<T>().GetValueAsync(t);
        Assert.AreEqual(1, @default, $"For events we should be expected to call them exactly once with the original implementation");
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

    // using 3 here to since if we only used 2... we could break remove and get the same number.
    private readonly EventHandler?[] m_events = new EventHandler?[3];
    private readonly bool m_doOverrides;
    public ReturnValidatingSidecar(bool doOverrides) => m_doOverrides = doOverrides;
    public bool TreatMethodAsync(MethodInfo method) => method.Name != nameof(I2.TaskMethodToNotAsync);
    public bool ShouldOverrideMethod(MethodInfo method)
    {
        if (method.DeclaringType == typeof(C2))
        {
            return m_doOverrides;
        }
        else if (method.DeclaringType == typeof(I2))
        {
            return m_doOverrides || method.IsAbstract;
        }
        else
        {
            return false;
        }
    }

    public bool ShouldOverrideProperty(PropertyInfo property) 
        => ShouldOverrideMethod((property.GetSetMethod(nonPublic: true) ?? property.GetGetMethod(nonPublic: true))!);
    public bool ShouldOverrideEvent(EventInfo @event) 
        => ShouldOverrideMethod((@event.GetRemoveMethod(nonPublic: true) ?? @event.GetAddMethod(nonPublic: true))!);

    public string? ReplaceMethodCall(MethodInfo method, GeneratorSupport support)
    {
        if (!m_doOverrides)
        {
            return null;
        }
        return method.Name switch
        {
            nameof(I2.Method) => $"{Generator.SidecarVariableName}.{nameof(I2.Method)}(out o, ref r);",
            nameof(I2.TaskMethodAsync) => $"await {Generator.SidecarVariableName}.{nameof(I2.TaskMethodAsync)}();",
            _ => $"{Generator.SidecarVariableName}.{method.Name}();",
        };
    }

    public string? ReplacePropertyCall(PropertyInfo property, bool forSet, GeneratorSupport support)
    {
        if (!m_doOverrides)
        {
            return null;
        }
        if (property.GetIndexParameters().Any())
        {
            return forSet
                ? $"{Generator.SidecarVariableName}[i] = value;"
                : $"{Generator.SidecarVariableName}[i];";
        }
        else
        {
            return forSet
                ? $"{Generator.SidecarVariableName}.{property.Name} = value;"
                : $"{Generator.SidecarVariableName}.{property.Name};";
        }
    }
    public string? ReplaceEventCall(EventInfo @event, bool forRemove, GeneratorSupport support)
    {
        if (!m_doOverrides)
        {
            return null;
        }
        return forRemove
            ? $"{Generator.SidecarVariableName}.{@event.Name} -= value;"
            : $"{Generator.SidecarVariableName}.{@event.Name} += value;";
    }

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

    public override event EventHandler? Event
    {
        add 
        {
            for (var i = 0; i < m_events.Length; i++)
            {
                m_events[i] += value;
            }
        }
        remove
        {
            for (var i = 0; i < m_events.Length; i++)
            {
                m_events[i] -= value;
            }
        }
    }

    public override void RaiseEvent()
    {
        for (var i = 0; i < m_events.Length; i++)
        {
            m_events[i]?.Invoke(this, EventArgs.Empty);
        }
    }
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

    int IProtectedInterface.InitFoo { get => default; init {} }

    int IProtectedInterface.FooMethod() => default;

    Tout IProtectedInterface.FooGeneric<Tin, Tout>(Tin input) => default!;

    unsafe int* IProtectedInterface.FooUnsafe<X>(int* p) => p;

    protected Tout FooGeneric2<Tin, Tout>(Tin input) => default!;

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