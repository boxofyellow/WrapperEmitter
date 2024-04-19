using Microsoft.VisualStudio.TestTools.UnitTesting.Logging;
using Moq;

namespace WrapperEmitter.Tests;

[TestClass]
public class GeneratorTests
{
    private const string c_item = "Item"; // The name index getter/setter

    [AssemblyCleanup()]
    public static void AssemblyCleanup() => TestLogger.Instance.EmitTimingInfo();

    [TestMethod]
    public void CreateInterfaceImplementation_SmokeMin()
    {
        var generator = new MinOpGenerator<I1, C1, DoNotCareType, bool>();
        var wrap = generator.CreateInterfaceImplementation(
            implementation: new(),
            sidecar: true,
            out var code,
            logger: TestLogger.Instance);
        Log(code);

        Assert.IsNotNull(wrap);
    }

    [TestMethod]
    public void CreateOverrideImplementation_SmokeMin()
    {
        var generator = new MinOpGenerator<DoNotCareType, DoNotCareType, C1, bool>();

        var factory = generator.CreateOverrideImplementationFactory<C1, bool, Func<bool, C1>>(
            out var code,
            logger: TestLogger.Instance);
        Log(code);
        Assert.IsNotNull(factory);

        var wrap = factory(true);
        Assert.IsNotNull(wrap);
    }

    [TestMethod]
    public void CreateInterfaceImplementation_SmokeMax()
    {
        var generator = new MaxOpGenerator<I1, C1, DoNotCareType, bool>();
        var wrap = generator.CreateInterfaceImplementation(
            implementation: new(),
            sidecar: true,
            out var code,
            logger: TestLogger.Instance);
          Log(code);

        Assert.IsNotNull(wrap);
    }

    [TestMethod]
    public void CreateOverrideImplementation_SmokeMax()
    {
        var generator = new MaxOpGenerator<DoNotCareType, DoNotCareType, C1, bool>();
        var factory = generator.CreateOverrideImplementationFactory<C1, bool, Func<bool, C1>>(
            out var code,
            logger: TestLogger.Instance);
        Log(code);
        Assert.IsNotNull(factory);

        var wrap = factory(true);
        Assert.IsNotNull(wrap);
    }

    [TestMethod]
    public void CreateOverrideImplementation_Init()
    {
        var generator = new MaxOpGenerator<DoNotCareType, DoNotCareType, C3, bool>();
        var factory = generator.CreateOverrideImplementationFactory<C3, bool, Func<bool, C3>>(
            out var code,
            logger: TestLogger.Instance);
        Log(code);
        Assert.IsNotNull(factory);

        var wrap = factory(true);
        Assert.IsNotNull(wrap);
    }

    [TestMethod]
    public async Task CreateInterfaceImplementation_TrackingSidecar_CallsPrePost()
    {
        await TestSidecarAsync<I1, C1>(
            methods: new[] {
                new TrackingSidecar<I1, C1>.CallableWithReturn<int>(nameof(I1.SimpleInterfaceMethod)),
                new TrackingSidecar<I1, C1>.Callable(nameof(I1.SimpleInterfaceVoidMethod)),
                // We are going to await ths Task<string>, so we will treat it as string
                new TrackingSidecar<I1, C1>.CallableWithReturn<string>(nameof(I1.SimpleInterfaceAsync)),
                // This one has a return value... but as ref struct it can't be a generic argument
                new TrackingSidecar<I1, C1>.Callable(nameof(I1.SimpleInterfaceRefStructMethod)),
                new TrackingSidecar<I1, C1>.Callable(nameof(I1.SimpleInterfaceRefStructParameter)),
                new TrackingSidecar<I1, C1>.CallableWithReturn<Task>(nameof(I1.SimpleInterfaceRefStructParameterAsync)),
            },
            getters: new TrackingSidecar<I1, C1>.CallableWithReturn[] {
              new TrackingSidecar<I1, C1>.CallableWithReturn<char>(nameof(I1.SimpleInterfaceProperty)),
              new TrackingSidecar<I1, C1>.CallableWithReturn<int>(c_item, $"{typeof(I1).FullName}."),
            },
            setters: new TrackingSidecar<I1, C1>.Callable[]
            {
                new(nameof(I1.SimpleInterfaceProperty)),
            },
            adders: new[]{nameof(I1.SimpleInterfaceEvent)},
            removers: new[]{nameof(I1.SimpleInterfaceEvent)},
            async (sidecar) => 
            {
                var wrap = sidecar.CreateInterfaceImplementation(
                    implementation: new(),
                    sidecar: sidecar,
                    out var code,
                    logger: TestLogger.Instance);
                Log(code);

                wrap.ToString();
                wrap.GetHashCode();
                wrap.Equals(new());
                wrap.SimpleInterfaceMethod();
                wrap.SimpleInterfaceVoidMethod();
                _ = wrap.SimpleInterfaceProperty;
                wrap.SimpleInterfaceProperty = default;
                _ = await wrap.SimpleInterfaceAsync();
                wrap.SimpleInterfaceEvent += EmptyHandler;
                wrap.SimpleInterfaceEvent -= EmptyHandler;
                wrap.SimpleInterfaceRefStructMethod();
                wrap.SimpleInterfaceRefStructParameter(new());
                await wrap.SimpleInterfaceRefStructParameterAsync(new());

                _ = wrap[1, 2.0, TimeSpan.Zero];
                // This one des not have a setter, it could...
            }
        );
    }

    [TestMethod]
    public async Task CreateOverrideImplementation_TrackingSidecar_CallsPrePost()
    {
        await TestSidecarAsync<I1, C1>(
            methods: new[] {
                new TrackingSidecar<I1, C1>.CallableWithReturn<string>(nameof(C1.ToString)),
                new TrackingSidecar<I1, C1>.CallableWithReturn<int>(nameof(C1.GetHashCode)),
                new TrackingSidecar<I1, C1>.CallableWithReturn<bool>(nameof(C1.Equals)),
                new TrackingSidecar<I1, C1>.CallableWithReturn<int>(nameof(C1.VirtualMethod)),
                new TrackingSidecar<I1, C1>.Callable(nameof(C1.VirtualVoidMethod)),
                // We are going to await this Task (not Task<>), so we will treat it as void.
                new TrackingSidecar<I1, C1>.Callable(nameof(C1.VirtualAsync)),
                // This one has a return value... but as ref struct it can't be a generic argument
                new TrackingSidecar<I1, C1>.Callable(nameof(C1.VirtualRefStruct)),
                new TrackingSidecar<I1, C1>.Callable(nameof(C1.VirtualRefStructParameter)),
                new TrackingSidecar<I1, C1>.CallableWithReturn<Task>(nameof(C1.VirtualRefStructParameterAsync)),
            },
            getters: new TrackingSidecar<I1, C1>.CallableWithReturn[] {
                new TrackingSidecar<I1, C1>.CallableWithReturn<char>(nameof(C1.VirtualProperty)),
                new TrackingSidecar<I1, C1>.CallableWithReturn<int>(c_item),
            },
           setters: new TrackingSidecar<I1, C1>.Callable[] {
                new (nameof(C1.VirtualProperty)),
                new (c_item),
            },
            adders: new[]{nameof(C1.VirtualEvent)},
            removers: new[]{nameof(C1.VirtualEvent)},
            async (sidecar) => 
            {
                var factory = sidecar.CreateOverrideImplementationFactory<C1, ITrackingSidecar, Func<ITrackingSidecar, C1>>(
                    out var code,
                    logger: TestLogger.Instance);
                Log(code);

                var wrap = factory(sidecar);

                wrap.ToString();
                wrap.GetHashCode();
                wrap.Equals(new());
                wrap.VirtualMethod();
                wrap.VirtualVoidMethod();
                _ = wrap.VirtualProperty;
                wrap.VirtualProperty = default;
                await wrap.VirtualAsync();
                wrap.VirtualEvent += EmptyHandler;
                wrap.VirtualEvent -= EmptyHandler;
                wrap.VirtualRefStruct();
                wrap.VirtualRefStructParameter(new());
                await wrap.VirtualRefStructParameterAsync(new());

                _ = wrap["just a string"];
                wrap["just another string", "and", "of", "course", "this", "is", "valid", "C#", "🙃"] = default;
            }
        );
    }

    [TestMethod]
    public async Task CreateInterfaceImplementation_TrackingSidecar_List()
    {
        var prefix = $"{typeof(IList<int>).FullTypeExpression().Replace("@","")}.";
        await TestSidecarAsync<IList<int>, List<int>>(
            methods: new [] {
                new TrackingSidecar<IList<int>, List<int>>.Callable(nameof(List<string>.Add)),
            },
            getters: new TrackingSidecar<IList<int>, List<int>>.CallableWithReturn[] {
                new TrackingSidecar<IList<int>, List<int>>.CallableWithReturn<int>(c_item, prefix),
                new TrackingSidecar<IList<int>, List<int>>.CallableWithReturn<int>(nameof(List<string>.Count)),
            },
            setters: new TrackingSidecar<IList<int>, List<int>>.Callable[] {
                new(c_item, prefix)
            },
            adders: Array.Empty<string>(),
            removers: Array.Empty<string>(),
            (sidecar) => 
            {
                var wrap = sidecar.CreateInterfaceImplementation(
                    implementation: new(),
                    sidecar: sidecar,
                    out var code,
                    logger: TestLogger.Instance);
                Log(code);

                wrap.Add(100);
                _ = wrap[0];
                wrap[0] = 200;
                _ = wrap.Count;

                return Task.CompletedTask;
            }
        );
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task CreateInterfaceImplementation_ReturnValidatingSidecar(bool doOverrides)
    {
        ReturnValidatingSidecar sidecar = new(doOverrides);
        C2 vanilla = new ();

        // Just making sure our test is setup right
        await ReturnValidatingSidecar.AssertAreNotEqual(sidecar.InterfaceCallableItems, vanilla, sidecar);
        await sidecar.AssertEventAreConfiguredCorrectly(vanilla);

        var wrap = sidecar.CreateInterfaceImplementation(
            implementation: vanilla,
            sidecar: sidecar,
            out var code,
            logger: TestLogger.Instance);
        Log(code);

        await ReturnValidatingSidecar.AssertMatches(shouldEqual: !doOverrides, sidecar.InterfaceCallableItems, vanilla, wrap);
        await ReturnValidatingSidecar.AssertMatches(shouldEqual: doOverrides, sidecar.InterfaceCallableItems, sidecar, wrap);
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task CreateOverrideImplementation_ReturnValidatingSidecar(bool doOverrides)
    {
        ReturnValidatingSidecar sidecar = new(doOverrides);
        C2 vanilla = new ();

        // Just making sure our test is setup right
        await ReturnValidatingSidecar.AssertAreNotEqual(sidecar.ClassCallableItems, vanilla, sidecar);
        await sidecar.AssertEventAreConfiguredCorrectly(vanilla);

        var factory = sidecar.CreateOverrideImplementationFactory<C2, IReturnValidatingSidecar, Func<IReturnValidatingSidecar, C2>>(
            out var code,
            logger: TestLogger.Instance);
        Log(code);

        var wrap = factory(sidecar);

        await ReturnValidatingSidecar.AssertMatches(shouldEqual: !doOverrides, sidecar.ClassCallableItems, vanilla, wrap);
        await ReturnValidatingSidecar.AssertMatches(shouldEqual: doOverrides, sidecar.ClassCallableItems, sidecar, wrap);
    }

    [TestMethod]
    public void CreateInterfaceImplementation_Inheritance()
    {
        var generator = new MinOpGenerator<ITop, InterfaceInheritances, DoNotCareType, bool>();
        var wrap = generator.CreateInterfaceImplementation(
            implementation: new (),
            sidecar: true,
            out var code,
            logger: TestLogger.Instance);
        Log(code);

        Assert.IsNotNull(wrap);
    }

    [TestMethod]
    unsafe public void CreateInterfaceImplementation_Protected()
    {
        var generator = new MaxOpGenerator<IProtectedInterface, ProtectedImplementer, DoNotCareType, bool>();
        var wrap = generator.CreateInterfaceImplementation(
            implementation: new (),
            sidecar: true,
            out var code,
            logger: TestLogger.Instance);

        Log(code);

        Assert.IsNotNull(wrap);

        int pointerTarget = 9;
        int* p = &pointerTarget;
        wrap.DoAll<string, int>(input: "blarg", setFoo: 5, setOtherFoo: 7, unsafeParam: p, unsafeOut: out var unsafeOut);
    }

    [TestMethod]
    public void CreateOverrideImplementation_Protected()
    {
        var generator = new MaxOpGenerator<DoNotCareType, DoNotCareType, ProtectedImplementer, bool>();
        var factory = generator.CreateOverrideImplementationFactory<ProtectedImplementer, bool, Func<bool, ProtectedImplementer>>(
            out var code,
            logger: TestLogger.Instance);
        Log(code);

        var wrap = factory(true);

        Assert.IsNotNull(wrap);
    }

    private delegate ConstructorClass ConstructorClassFactory(int i, ref double d, out long l, bool sideCar);
    [TestMethod]
    public void CreateOverrideImplementation_Constructor()
    {
        int i = 100;
        double d = 200;

        var expectedI = i;
        var expectedD = d;

        var actualI = i;
        var actualD = d;
        ConstructorClass expected = new (expectedI, ref expectedD, out var expectedL);

        var generator = new MinOpGenerator<DoNotCareType, DoNotCareType, ConstructorClass, bool>();
        var factory = generator.CreateOverrideImplementationFactory<ConstructorClass, bool, ConstructorClassFactory>(
            out var code,
            logger: TestLogger.Instance);
        Log(code);
        Assert.IsNotNull(factory);

        var wrap = factory(actualI, ref actualD, out var actualL, true);
        Assert.IsNotNull(wrap);

        Assert.AreEqual(expected.I, wrap.I);
        Assert.AreEqual(expected.D, wrap.D);
        Assert.AreEqual(expectedI, actualI);
        Assert.AreEqual(expectedD, actualD);
        Assert.AreEqual(expectedL, actualL);
    }

    [TestMethod]
    public void CreateInterfaceImplementation_Combined()
    {
        var generator = new MaxOpGenerator<ICombined, InterfaceCombined, DoNotCareType, bool>();
        var wrap = generator.CreateInterfaceImplementation(
            implementation: new (),
            sidecar: true,
            out var code,
            logger: TestLogger.Instance);
        Log(code);

        Assert.IsNotNull(wrap);
    }

    private static async Task TestSidecarAsync<TInterface, TImplementation>(
        IEnumerable<TrackingSidecar<TInterface, TImplementation>.Callable> methods,
        IEnumerable<TrackingSidecar<TInterface, TImplementation>.CallableWithReturn> getters,
        IEnumerable<TrackingSidecar<TInterface, TImplementation>.Callable> setters,
        IEnumerable<string> adders,
        IEnumerable<string> removers,
        Func<TrackingSidecar<TInterface, TImplementation>, Task> wrapActionAsync
    )
        where TImplementation : class, TInterface
        where TInterface : class
    {
        var sidecarMock = new Mock<TrackingSidecar<TInterface, TImplementation>>
        {
            CallBase = true
        };

        List<(TrackingSidecar<TInterface, TImplementation>.Callable callable, string name)> callableItems = new(methods.Select(x => (x, x.Name)));
        AddCallableItemsToList(getters, "get");
        AddCallableItemsToList(setters, "set");
        AddToCallableNamesList(adders, "add");
        AddToCallableNamesList(removers, "remove");

        foreach (var item in callableItems)
        {
            // We use Callbase, that will catch everything we don't setup here
            // All of those will get dumped to the log
            sidecarMock.Setup(TrackingSidecar<TInterface, TImplementation>.PreCallMockExpression(item.name));
            sidecarMock.Setup(item.callable.PostCallMockExpression(item.name));
        }

        await wrapActionAsync(sidecarMock.Object);

        foreach (var item in callableItems)
        {
            sidecarMock.Verify(TrackingSidecar<TInterface, TImplementation>.PreCallMockExpression(item.name), Times.Once);
            sidecarMock.Verify(item.callable.PostCallMockExpression(item.name), Times.Once);
        }

        void AddCallableItemsToList(IEnumerable<TrackingSidecar<TInterface, TImplementation>.Callable> items, string prefix)
        {
            callableItems.AddRange(items.Select(x => (x, $"{prefix}_{x.Name}")));
        }
        void AddToCallableNamesList(IEnumerable<string> items, string prefix)
        {
            callableItems.AddRange(items.Select(x => (new TrackingSidecar<TInterface, TImplementation>.Callable(x), $"{prefix}_{x}")));
        }
    }

    private static void EmptyHandler(object? sender, EventArgs e) { }

    private static void Log(string x) => Logger.LogMessage("{0}", x);

}