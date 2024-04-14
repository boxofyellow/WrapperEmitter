using System.Linq.Expressions;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting.Logging;

namespace WrapperEmitter.Tests;

[TestClass]
public class RestrictedHelperTests
{
    private delegate MethodBase Empty_I0(IHierarchy_0 x);
    private delegate MethodBase Int_I0(IHierarchy_0 x, int i);
    private delegate MethodBase Int_I0<X>(IHierarchy_0 x, int i);
    private delegate MethodBase X_I0<X>(IHierarchy_0 x, X i);
    private delegate MethodBase XY_I0<X, Y>(IHierarchy_0 x, X i, Y j);
    private delegate MethodBase YX_I0<X, Y>(IHierarchy_0 x, Y i, X j);

    private delegate MethodBase Empty_I1(IHierarchy_1 x);
    private delegate MethodBase Int_I1(IHierarchy_1 x, int i);
    private delegate MethodBase YX_I1<X, Y>(IHierarchy_1 x, Y i, X j);

    private delegate MethodBase Empty_C0(Hierarchy_0 x);
    private delegate MethodBase X_C0<X>(Hierarchy_0 x, X i);
    private delegate MethodBase Empty_C0<X, Y>(Hierarchy_0 x);

    private delegate MethodBase Empty_C1(Hierarchy_1 x);
    private delegate MethodBase X_C1<X>(Hierarchy_1 x, X i);
    private delegate MethodBase YX_C1<X, Y>(Hierarchy_1 x, Y i, X j);

    private delegate MethodBase Static<X, Y, Z>();

    public interface IHierarchy_0
    {
        /*
        NOTE: By declaring these methods such that they return a reference to them self it make testing easier
              1. They are unique and comparable, as a result we can Assert that the value matches or not to complete tests
              2. Since they return their own reference it make finding their MethodInfo very easy, we just call them.
        ANOTHER NOTE: For Generic methods, this will return the open version of the method 🤷, that seems odd to me but it is what it is and truth be
                      told it is kind'a helpful for us here 🙃
        */
        public MethodBase Foo() => MethodBase.GetCurrentMethod()!;
        public MethodBase Foo(int i) => MethodBase.GetCurrentMethod()!;
        public MethodBase Foo<X>(int i) => MethodBase.GetCurrentMethod()!;
        public MethodBase Foo<X>(X i) => MethodBase.GetCurrentMethod()!;
        public MethodBase Foo<X, Y>(X i, Y j) => MethodBase.GetCurrentMethod()!;
        public MethodBase Foo<X, Y>(Y i, X j) => MethodBase.GetCurrentMethod()!;
        public MethodBase Bar => MethodBase.GetCurrentMethod()!;
        public static MethodBase Foo<X, Y, Z>() => MethodBase.GetCurrentMethod()!;
    }

    public class Hierarchy_0_Holder : IHierarchy_0 { }

    private interface IHierarchy_1 : IHierarchy_0
    {
        public new MethodBase Foo(int i) => MethodBase.GetCurrentMethod()!;
        public new MethodBase Foo<X, Y>(Y i, X j) => MethodBase.GetCurrentMethod()!;
        public new MethodBase Bar => MethodBase.GetCurrentMethod()!;
    }

    public class Hierarchy_1_Holder : IHierarchy_1 { }

    public class Hierarchy_0 : IHierarchy_0
    {
        MethodBase IHierarchy_0.Foo<X>(int i) => MethodBase.GetCurrentMethod()!;
        public virtual MethodBase Foo<X>(X i) => MethodBase.GetCurrentMethod()!;
        public MethodBase Foo<X, Y>() => MethodBase.GetCurrentMethod()!;
        public virtual MethodBase Bar => MethodBase.GetCurrentMethod()!;
        public static MethodBase Foo<X, Y, Z>() => MethodBase.GetCurrentMethod()!;
    }

    public class Hierarchy_1 : Hierarchy_0, IHierarchy_1
    {
        public override MethodBase Foo<X>(X i) => MethodBase.GetCurrentMethod()!;

        public MethodBase Foo<X, Y>(Y i, X j) => MethodBase.GetCurrentMethod()!;
        
        public override MethodBase Bar => MethodBase.GetCurrentMethod()!;

        public new static MethodBase Foo<X, Y, Z>() => MethodBase.GetCurrentMethod()!;
    }

    private class OverrideEverything : Hierarchy_1, IHierarchy_0, IHierarchy_1
    {
        MethodBase IHierarchy_0.Foo() => MethodBase.GetCurrentMethod()!;
        MethodBase IHierarchy_0.Foo(int i) => MethodBase.GetCurrentMethod()!;
        MethodBase IHierarchy_0.Foo<X>(int i) => MethodBase.GetCurrentMethod()!;
        MethodBase IHierarchy_0.Foo<X>(X i) => MethodBase.GetCurrentMethod()!;
        MethodBase IHierarchy_0.Foo<X, Y>(X i, Y j) => MethodBase.GetCurrentMethod()!;
        MethodBase IHierarchy_0.Foo<X, Y>(Y i, X j) => MethodBase.GetCurrentMethod()!;
        MethodBase IHierarchy_0.Bar => MethodBase.GetCurrentMethod()!;

        MethodBase IHierarchy_1.Foo(int i) => MethodBase.GetCurrentMethod()!;
        MethodBase IHierarchy_1.Foo<X, Y>(Y i, X j) => MethodBase.GetCurrentMethod()!;
        MethodBase IHierarchy_1.Bar => MethodBase.GetCurrentMethod()!;

        public override MethodBase Foo<X>(X i) => MethodBase.GetCurrentMethod()!;
        public override MethodBase Bar => MethodBase.GetCurrentMethod()!;

        public MethodBase Foo() => MethodBase.GetCurrentMethod()!;
        public MethodBase Foo(int i) => MethodBase.GetCurrentMethod()!;
        public MethodBase Foo<X>(int i) => MethodBase.GetCurrentMethod()!;
        public MethodBase Foo<X, Y>(X i, Y j) => MethodBase.GetCurrentMethod()!;
    }

    public readonly unsafe ref struct ReturnObject
    {
        public Type X {get; init;}
        public Type Y {get; init;}
        public int I {get; init;}
        public object O {get; init;}
        public object? N {get; init;}
        public string[] A {get; init;}
        public S1 S {get; init;}
        public int* P {get; init;}
        public RefStruct RS {get; init;}

        public struct S1
        {
            public int A { get; set; }
        }

        public ref struct RefStruct
        { 
            public int A { get; set; }
        }

        public void AssertEqual(ref ReturnObject other)
        {
            Assert.AreEqual(X, other.X);
            Assert.AreEqual(Y, other.Y);
            Assert.AreEqual(I, other.I);
            Assert.AreEqual(O, other.O);
            Assert.IsNotNull(O, "... Fix the test");
            Assert.AreEqual(N, other.N);
            Assert.IsNull(N, "... Fix the test");
            Assert.AreEqual(A, other.A);
            Assert.AreEqual(S.A, other.S.A);
            if (P != other.P)
            {
                Assert.Fail("Pointers did not match");
            }
            Assert.AreEqual(RS.A, other.RS.A);
        }
    }

    private unsafe delegate ReturnObject ParameterDelegate<X, Y>(ParameterTest that, int i, object o, object? n, string[] a, ReturnObject.S1 s, out int oi, ref int ri, int* p, ReturnObject.RefStruct rs);
    private delegate MethodBase ParameterPropertyGetDelegate(ParameterTest that);
    private delegate void ParameterPropertySetDelegate(ParameterTest that, MethodBase value);

    private delegate (MethodBase Method, int Index) ParameterPropertyGetIndexerDelegate(ParameterTest that, int i);
    private delegate void ParameterPropertySetIndexerDelegate(ParameterTest that, int i, (MethodBase Method, int Index) value);

    public class ParameterTest
    {
        public ParameterTest()
        {
            // Yup! -- Just call our setters to update the follow member vars
            Bar = Bar;
            this[IndexSetup] = this[IndexSetup];
            Assert.IsNotNull(m_setBase);
            Assert.IsNotNull(m_indexerSetBase);
        }

        public const int IndexSetup = 7;

        public MethodInfo SetMethod => (MethodInfo)m_setBase;
        public MethodInfo SetIndexMethod => (MethodInfo)m_indexerSetBase!.Value.Method;
        private MethodBase m_setBase { get; set; } = null!;
        private (MethodBase Method, int Index)? m_indexerSetBase { get; set; } = null!;

        public virtual unsafe ReturnObject Foo<X, Y>(int i, object o, object? n, string[] a, ReturnObject.S1 s, out int oi, ref int ri, int* p, ReturnObject.RefStruct rs)
        {
            oi = 4;
            ri *= 2; 

            return new ReturnObject{
                X = typeof(X),
                Y = typeof(Y),
                I = i,
                O = o,
                N = n,
                A = a,
                S = s,
                P = p,
                RS = rs,
            };
        }

        public virtual MethodBase Bar
        {
            get => MethodBase.GetCurrentMethod()!;
            set
            {
                m_setBase ??= MethodBase.GetCurrentMethod()!;
                Assert.AreEqual(Bar, value);
            }
        }

        public virtual (MethodBase Method, int Index) this[int i]
        {
            get => (MethodBase.GetCurrentMethod()!, i);
            set
            {
                m_indexerSetBase ??= new (MethodBase.GetCurrentMethod()!, i);
                Assert.AreEqual(this[i].Method, value.Method);
                Assert.AreEqual(this[i].Index, i);
            }
        }
    }


    private delegate MethodBase VisibilityInstanceDelegate<X>(VisibilityTest that);
    private delegate MethodBase VisibilityStaticDelegate<X>();
    public class VisibilityTest
    {
        public virtual MethodBase PublicFoo<X>() => MethodBase.GetCurrentMethod()!;
        private MethodBase PrivateFoo<X>() => MethodBase.GetCurrentMethod()!;
        internal virtual MethodBase InternalFoo<X>() => MethodBase.GetCurrentMethod()!;
        protected virtual MethodBase ProtectedFoo<X>() => MethodBase.GetCurrentMethod()!;
        protected internal virtual MethodBase ProtectedInternalInternalFoo<X>() => MethodBase.GetCurrentMethod()!;
        protected private virtual MethodBase ProtectedPrivateInternalFoo<X>() => MethodBase.GetCurrentMethod()!;

        public MethodBase PublicBar() => PublicFoo<int>();
        public MethodBase PrivateBar() => PrivateFoo<int>();
        public MethodBase InternalBar() => InternalFoo<int>();
        public MethodBase ProtectedBar() => ProtectedFoo<int>();
        public MethodBase ProtectedInternalInternalBar() => ProtectedInternalInternalFoo<int>();
        public MethodBase ProtectedPrivateInternalBar() => ProtectedPrivateInternalFoo<int>();

        public static MethodBase StaticPublicFoo<X>() => MethodBase.GetCurrentMethod()!;
        private static MethodBase StaticPrivateFoo<X>() => MethodBase.GetCurrentMethod()!;
        internal static MethodBase StaticInternalFoo<X>() => MethodBase.GetCurrentMethod()!;
        protected static MethodBase StaticProtectedFoo<X>() => MethodBase.GetCurrentMethod()!;
        protected static MethodBase StaticProtectedInternalInternalFoo<X>() => MethodBase.GetCurrentMethod()!;
        protected static MethodBase StaticProtectedPrivateInternalFoo<X>() => MethodBase.GetCurrentMethod()!;

        public static MethodBase StaticPublicBar() => StaticPublicFoo<int>();
        public static MethodBase StaticPrivateBar() => StaticPrivateFoo<int>();
        public static MethodBase StaticInternalBar() => StaticInternalFoo<int>();
        public static MethodBase StaticProtectedBar() => StaticProtectedFoo<int>();
        public static MethodBase StaticProtectedInternalInternalBar() => StaticProtectedInternalInternalFoo<int>();
        public static MethodBase StaticProtectedPrivateInternalBar() => StaticProtectedPrivateInternalFoo<int>();
    }


    private delegate void EventTestDelegate(EventTest that, EventHandler? value);
    public class EventTest
    {
        public int CallCount { get; private set; }

        public void RaiseEvent() => Event?.Invoke(this, EventArgs.Empty); 
        public event EventHandler? Event;

        public void Handler(object? sender, EventArgs args) => CallCount++;

        public static readonly MethodInfo AddMethod = typeof(EventTest).GetEvent(nameof(Event))!.GetAddMethod()!;
        public static readonly MethodInfo RemoveMethod = typeof(EventTest).GetEvent(nameof(Event))!.GetRemoveMethod()!;
    }

    [TestMethod]
    public void GetMethod_CreateDynamicMethodDelegate_Test()
    {
        IHierarchy_0 i0 = new Hierarchy_0_Holder();
        IHierarchy_1 i1 = new Hierarchy_1_Holder();
        Hierarchy_0 c0 = new();
        Hierarchy_1 c1 = new();
        OverrideEverything overrideEverything = new();

        var allObjects = new IHierarchy_0[] {i0, i1, c0, c1, overrideEverything};
        
        // Check our instance ones
        Check_GetMethod_CreateDynamicMethodDelegate<IHierarchy_0, Empty_I0>((x) => x.Foo(), i0, allObjects);
        Check_GetMethod_CreateDynamicMethodDelegate<IHierarchy_0, Int_I0>((x) => x.Foo(1), i0, allObjects);
        Check_GetMethod_CreateDynamicMethodDelegate<IHierarchy_0, Int_I0<bool>>((x) => x.Foo<bool>(1), i0, allObjects);
        Check_GetMethod_CreateDynamicMethodDelegate<IHierarchy_0, X_I0<bool>>((x) => x.Foo<bool>(true), i0, allObjects);
        Check_GetMethod_CreateDynamicMethodDelegate<IHierarchy_0, XY_I0<int, bool>>((x) => x.Foo<int, bool>(1, true), i0, allObjects);
        Check_GetMethod_CreateDynamicMethodDelegate<IHierarchy_0, YX_I0<int, bool>>((x) => x.Foo<int, bool>(true, 1), i0, allObjects);
        Check_GetMethod_CreateDynamicMethodDelegate<IHierarchy_0, Empty_I0>((x) => x.Bar, i0, allObjects);

        Check_GetMethod_CreateDynamicMethodDelegate<IHierarchy_1, Int_I1>((x) => x.Foo(1), i1, allObjects);
        Check_GetMethod_CreateDynamicMethodDelegate<IHierarchy_1, YX_I1<int ,bool>>((x) => x.Foo<int, bool>(true, 1), i1, allObjects);
        Check_GetMethod_CreateDynamicMethodDelegate<IHierarchy_1, Empty_I1>((x) => x.Bar, i1, allObjects);

        Check_GetMethod_CreateDynamicMethodDelegate<Hierarchy_0, X_C0<bool>>((x) => x.Foo<bool>(true), c0, allObjects);
        Check_GetMethod_CreateDynamicMethodDelegate<Hierarchy_0, Empty_C0<int, bool>>((x) => x.Foo<int, bool>(), c0, allObjects);
        Check_GetMethod_CreateDynamicMethodDelegate<Hierarchy_0, Empty_C0>((x) => x.Bar, c0, allObjects);

        Check_GetMethod_CreateDynamicMethodDelegate<Hierarchy_1, X_C1<bool>>((x) => x.Foo<bool>(true), c1, allObjects);
        Check_GetMethod_CreateDynamicMethodDelegate<Hierarchy_1, YX_C1<int, bool>>((x) => x.Foo<int, bool>(true, 1), c1, allObjects);
        Check_GetMethod_CreateDynamicMethodDelegate<Hierarchy_1, Empty_C1>((x) => x.Bar, c1, allObjects);

        // Check our static ones...
        Check_GetMethod_CreateDynamicMethodDelegate<Static<int, bool, char>>(IHierarchy_0.Foo<int, bool, char>(), that: null);
        Check_GetMethod_CreateDynamicMethodDelegate<Static<int, bool, char>>(Hierarchy_0.Foo<int, bool, char>(), that: null);
        Check_GetMethod_CreateDynamicMethodDelegate<Static<int, bool, char>>(Hierarchy_1.Foo<int, bool, char>(), that: null);
    }

    [TestMethod]
    public unsafe void CreateDynamicMethodDelegate_Parameter_Test()
    {
        int i = 7;
        object o = new();
        object? n = null;
        string[] a = new [] {"one", "two"};
        ReturnObject.S1 s = new() { A = 8 };
        int ri1 = 9;
        int ri2 = ri1;
        int* p = &i;
        ReturnObject.RefStruct rs = new() { A = 10 };

        ParameterTest that = new();
        var expected = that.Foo<int, string>(i, o, n, a, s, out int oi1, ref ri1, p, rs);

        var d = RestrictedHelper.CreateDynamicMethodDelegate<ParameterDelegate<int, string>>(
            typeof(ParameterTest).GetMethod(nameof(ParameterTest.Foo))!.MakeGenericMethod(typeof(int), typeof(string)));

        var actual = d(that, i, o, n, a, s, out int oi2, ref ri2, p, rs);
        expected.AssertEqual(ref actual);
        Assert.AreEqual(oi1, oi2);
        Assert.AreEqual(ri1, ri2);
    }

    [TestMethod]
    public unsafe void CreateDynamicMethodDelegate_Parameter_Property_Test()
    {
        ParameterTest that = new();

        var expected = that.Bar;
        var getMethod = (MethodInfo)expected;

        var get = RestrictedHelper.CreateDynamicMethodDelegate<ParameterPropertyGetDelegate>(getMethod);

        var actual = get(that);

        Assert.AreEqual(expected, actual);

        var set = RestrictedHelper.CreateDynamicMethodDelegate<ParameterPropertySetDelegate>(that.SetMethod);
        set(that, expected);
    }

    [TestMethod]
    public unsafe void CreateDynamicMethodDelegate_Parameter_Indexer_Test()
    {
        ParameterTest that = new();

        var expected = that[ParameterTest.IndexSetup];
        var getMethod = (MethodInfo)expected.Method;

        var get = RestrictedHelper.CreateDynamicMethodDelegate<ParameterPropertyGetIndexerDelegate>(getMethod);

        var actual = get(that, ParameterTest.IndexSetup);

        Assert.AreEqual(expected.Method, actual.Method);
        Assert.AreEqual(expected.Index, actual.Index);

        var set = RestrictedHelper.CreateDynamicMethodDelegate<ParameterPropertySetIndexerDelegate>(that.SetIndexMethod);
        set(that, ParameterTest.IndexSetup, expected);
    }

    [TestMethod]
    public void CreateDynamicMethodDelegate_Visibility_Test()
    {
        VisibilityTest that = new();
        Check_GetMethod_CreateDynamicMethodDelegate<VisibilityInstanceDelegate<int>>(that.PublicBar(), that);
        Check_GetMethod_CreateDynamicMethodDelegate<VisibilityInstanceDelegate<int>>(that.PrivateBar(), that);
        Check_GetMethod_CreateDynamicMethodDelegate<VisibilityInstanceDelegate<int>>(that.InternalBar(), that);
        Check_GetMethod_CreateDynamicMethodDelegate<VisibilityInstanceDelegate<int>>(that.ProtectedBar(), that);
        Check_GetMethod_CreateDynamicMethodDelegate<VisibilityInstanceDelegate<int>>(that.ProtectedInternalInternalBar(), that);
        Check_GetMethod_CreateDynamicMethodDelegate<VisibilityInstanceDelegate<int>>(that.ProtectedPrivateInternalBar(), that);

        Check_GetMethod_CreateDynamicMethodDelegate<VisibilityStaticDelegate<int>>(VisibilityTest.StaticPublicBar(), that: null);
        Check_GetMethod_CreateDynamicMethodDelegate<VisibilityStaticDelegate<int>>(VisibilityTest.StaticPrivateBar(), that: null);
        Check_GetMethod_CreateDynamicMethodDelegate<VisibilityStaticDelegate<int>>(VisibilityTest.StaticInternalBar(), that: null);
        Check_GetMethod_CreateDynamicMethodDelegate<VisibilityStaticDelegate<int>>(VisibilityTest.StaticProtectedBar(), that: null);
        Check_GetMethod_CreateDynamicMethodDelegate<VisibilityStaticDelegate<int>>(VisibilityTest.StaticProtectedInternalInternalBar(), that: null);
        Check_GetMethod_CreateDynamicMethodDelegate<VisibilityStaticDelegate<int>>(VisibilityTest.StaticProtectedPrivateInternalBar(), that: null);
    }

    [TestMethod]
    public void CreateDynamicMethodDelegate_Event_Test()
    {
        EventTest that = new();

        // Just double checking that the test is set up correctly
        Assert.AreEqual(0, that.CallCount, "... Fix the test");
        that.Event += that.Handler;
        that.RaiseEvent();
        Assert.AreEqual(1, that.CallCount, "... Fix the test");
        that.Event -= that.Handler;
        that.RaiseEvent();
        Assert.AreEqual(1, that.CallCount, "... Fix the test");
        // Now test the real thing

        var addDelegate = RestrictedHelper.CreateDynamicMethodDelegate<EventTestDelegate>(EventTest.AddMethod);
        addDelegate(that, that.Handler);
        that.RaiseEvent();
        Assert.AreEqual(2, that.CallCount);

        var removeDelegate = RestrictedHelper.CreateDynamicMethodDelegate<EventTestDelegate>(EventTest.RemoveMethod);
        removeDelegate(that, that.Handler);
        that.RaiseEvent();
        Assert.AreEqual(2, that.CallCount);
    }

    private (D, object?[]) Check_GetMethod_CreateDynamicMethodDelegate<D>(MethodBase methodBase, object? that)
        where D : Delegate
    {
        if (methodBase is MethodInfo method)
        {
            Assert.AreEqual(method.IsStatic, that is null, $"All Static Methods (and only Static Methods) should be tested with a null {nameof(that)} ... fix the test");
            Assert.IsNotNull(method.DeclaringType, "... Fix this test");

            var delegateType = typeof(D);
            Logger.LogMessage("{0} {1} {2}", delegateType, method, method.DeclaringType);

            var searchDelegateType = delegateType.IsGenericType
                                   ? delegateType.GetGenericTypeDefinition()
                                   : delegateType;

            var flags = RestrictedHelper.GetBindingFlags(method);

            var foundMethod = method.IsStatic
                            ? RestrictedHelper.GetStaticMethod(searchDelegateType, flags, method.DeclaringType, method.Name)
                            : RestrictedHelper.GetInstanceMethod(searchDelegateType, flags, method.Name);
            Assert.AreEqual(method, foundMethod, "RestrictedHelper.GetXyzMethod failed to find the correct method");

            var methodToConvert = method.IsGenericMethod
                                ? method.MakeGenericMethod(typeof(D).GetGenericArguments())
                                : method;
            
            var d = RestrictedHelper.CreateDynamicMethodDelegate<D>(methodToConvert);

            var args = new List<object?>();
            if (!method.IsStatic)
            {
                args.Add(that);
            }
            args.AddRange(methodToConvert.GetParameters().Select(x => GetDefaultValue(x.ParameterType)));
            var argsArray = args.ToArray();

            Assert.AreEqual(method, d.DynamicInvoke(argsArray), "Calling our delegate did not yield the correct result");
            return (d, argsArray);
        }
        else
        {
            Assert.Fail($"{nameof(methodBase)} was not a {nameof(MethodInfo)}, got {methodBase} ... fix the test classes");
            return (null, null); // This won't happen but the compiler does not know that.
        }
    }

    private void Check_GetMethod_CreateDynamicMethodDelegate<T, D>(Func<T, MethodBase> func, T that, object[] allObjects)
        where T : class
        where D : Delegate
        // We can't have `where D : Delegate(T ...)`, but that is the only for the test to be valid so we will check in the test.
    {
        var delegateType = typeof(D);
        Assert.AreEqual(typeof(T), delegateType.GetMethod(nameof(Empty_I0.Invoke))?.GetParameters().First().ParameterType, "... fix the test");

        (D d, var argsArray) = Check_GetMethod_CreateDynamicMethodDelegate<D>(func(that), that);

        if (new OverrideEverything() is T overrideEveryThing)
        {
            var method = func(that);
            var other = func(overrideEveryThing); 
            if (method.IsVirtual && other.DeclaringType == typeof(OverrideEverything))
            {
                Assert.AreNotEqual(method, other, "Checking that our assumptions about asserting equality on methods is valid ... fix the test");
            }
        }

        foreach (var o in allObjects)
        {
            if (o is T t)
            {
                argsArray[0] = t;
                Assert.AreEqual(
                    func(t),
                    d.DynamicInvoke(argsArray),
                    $"For {t.GetType()}, the func and delegate did not agree");
            }
        }
    }


    private static object GetDefaultValue(Type type)
    {
        // NOTE: I looked into using Expressions to avoid some of hoop jumping...
        //       I came to the conclusion it does not help us much... in short
        //       We would have the same problem with generic methods (AKA we don't know which types will used, 
        //       and we can't create an instance of open one)
        //       Maybe we can use use them to replace the IL generation, but hat seems very stable
        //       Maybe we could replace custom delegates with Func<> and Actions<>?
        //       That is problematic for parameters and return types that can be generic arguments (like ref structs and pointers)
        //       And they seem to be just as slow to create (all though we could cache them just the same)
        Expression<Func<object>> e = Expression.Lambda<Func<object>>(
            Expression.Convert(Expression.Default(type), typeof(object))
        );

        return e.Compile()();
    }
}