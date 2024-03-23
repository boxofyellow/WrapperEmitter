// TODO: Tests for Generic Methods
// TODO: Tests for Multiple interfaces
// TODO: Maybe collect timing for all the pipelines for creation
// TODO: More tests that Events still "work"

using Microsoft.VisualStudio.TestTools.UnitTesting.Logging;
using Moq;

namespace WrapperEmitter.Tests;

[TestClass]
public class GeneratorTests
{
    private const string c_item = "Item"; // The name index getter/setter

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
        var wrap = generator.CreateOverrideImplementation(
            constructorArguments: Generator.NoParams,
            sidecar: true,
            out var code,
            logger: TestLogger.Instance);
        Log(code);

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
        var wrap = generator.CreateOverrideImplementation(
            constructorArguments: Generator.NoParams,
            sidecar: true,
            out var code,
            logger: TestLogger.Instance);
        Log(code);

        Assert.IsNotNull(wrap);
    }

    [TestMethod]
    public void CreateOverrideImplementation_Init()
    {
        var generator = new MaxOpGenerator<DoNotCareType, DoNotCareType, C3, bool>();
        var wrap = generator.CreateOverrideImplementation(
            constructorArguments: Generator.NoParams,
            sidecar: true,
            out var code,
            logger: TestLogger.Instance);
        Log(code);

        Assert.IsNotNull(wrap);
    }

    [TestMethod]
    public async Task CreateInterfaceImplementation_TrackingSidecar_CallsPrePost()
    {
        await TestSidecarAsync(
            methodNames: new[] {
                new TrackingSidecar.CallableWithReturn<int>(nameof(I1.SimpleInterfaceMethod)),
                new TrackingSidecar.Callable(nameof(I1.SimpleInterfaceVoidMethod)),
                // We are going to await ths Task<string>, so we will treat it as string
                new TrackingSidecar.CallableWithReturn<string>(nameof(I1.SimpleInterfaceAsync)),
            },
            getters: new TrackingSidecar.CallableWithReturn[] {
              new TrackingSidecar.CallableWithReturn<char>(nameof(I1.SimpleInterfaceProperty)),
              new TrackingSidecar.CallableWithReturn<int>(c_item),
            },
            setters: new[]{nameof(I1.SimpleInterfaceProperty)},
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

                _ = wrap[1, 2.0, TimeSpan.Zero];
                // This one des not have a setter, it could...
            }
        );
    }

    [TestMethod]
    public async Task CreateOverrideImplementation_TrackingSidecar_CallsPrePost()
    {
        await TestSidecarAsync(
            methodNames: new[] {
                new TrackingSidecar.CallableWithReturn<string>(nameof(C1.ToString)),
                new TrackingSidecar.CallableWithReturn<int>(nameof(C1.GetHashCode)),
                new TrackingSidecar.CallableWithReturn<bool>(nameof(C1.Equals)),
                new TrackingSidecar.CallableWithReturn<int>(nameof(C1.VirtualMethod)),
                new TrackingSidecar.Callable(nameof(C1.VirtualVoidMethod)),
                // We are going to await this Task (not Task<>), so we will treat it as void.
                new TrackingSidecar.Callable(nameof(C1.VirtualAsync)),
            },
            getters: new TrackingSidecar.CallableWithReturn[] {
                new TrackingSidecar.CallableWithReturn<char>(nameof(C1.VirtualProperty)),
                new TrackingSidecar.CallableWithReturn<int>(c_item),
            },
            setters: new[]{nameof(C1.VirtualProperty), c_item},
            adders: new[]{nameof(C1.VirtualEvent)},
            removers: new[]{nameof(C1.VirtualEvent)},
            async (sidecar) => 
            {
                var wrap = sidecar.CreateOverrideImplementation(
                    constructorArguments: Generator.NoParams,
                    sidecar: sidecar,
                    out var code,
                    logger: TestLogger.Instance);
                Log(code);

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

                _ = wrap["just a string"];
                wrap["just another string", "and", "of", "course", "this", "is", "valid", "C#", "🙃"] = default;
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

        // Just making out test is setup right
        await ReturnValidatingSidecar.AssertAreNotEqual(sidecar.InterfaceCallableItems, vanilla, sidecar);

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

        // Just making out test is setup right
        await ReturnValidatingSidecar.AssertAreNotEqual(sidecar.InterfaceCallableItems, vanilla, sidecar);

        var wrap = sidecar.CreateOverrideImplementation(
            constructorArguments: Generator.NoParams,
            sidecar: sidecar,
            out var code,
            logger: TestLogger.Instance);
        Log(code);

        await ReturnValidatingSidecar.AssertMatches(shouldEqual: !doOverrides, sidecar.ClassCallableItems, vanilla, wrap);
        await ReturnValidatingSidecar.AssertMatches(shouldEqual: doOverrides, sidecar.ClassCallableItems, sidecar, wrap);
    }

    [TestMethod]
    public void CreateInterfaceImplementation_UsesCache()
    {
        var sidecar = new MinOpGenerator<I2, C2, DoNotCareType, bool>();
        TestCreateImplementationCache(
            (className) => sidecar.CreateInterfaceImplementation(
                      implementation: new(),
                      sidecar: true,
                      out var _,
                      className: className,
                      logger: TestLogger.Instance));
    }

    [TestMethod]
    public void CreateOverrideImplementation_UsesCache() 
    {
        ReturnValidatingSidecar sidecar = new(doOverrides: true);
        TestCreateImplementationCache(
            (className) => sidecar.CreateOverrideImplementation(
                      constructorArguments: Generator.NoParams,
                      sidecar: sidecar,
                      out var _,
                      className: className,
                      logger: TestLogger.Instance));
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
    public void CreateInterfaceImplementation_Protected()
    {
        var generator = new MaxOpGenerator<IProtectedInterface, ProtectedImplementer, DoNotCareType, bool>(handleProtectedInterfaces: false);

        Assert.ThrowsException<InvalidCSharpException>(() => generator.CreateInterfaceImplementation(
            implementation: new (),
            sidecar: true,
            out _));

        generator = new MaxOpGenerator<IProtectedInterface, ProtectedImplementer, DoNotCareType, bool>(handleProtectedInterfaces: true);
        var wrap = generator.CreateInterfaceImplementation(
            implementation: new (),
            sidecar: true,
            out var code,
            logger: TestLogger.Instance);

        Log(code);

        Assert.IsNotNull(wrap);
    }

    [TestMethod]
    public void CreateOverrideImplementation_Protected()
    {
        var generator = new MaxOpGenerator<DoNotCareType, DoNotCareType, ProtectedImplementer, bool>();
        var wrap = generator.CreateOverrideImplementation(
            constructorArguments: Generator.NoParams,
            sidecar: true,
            out var code,
            logger: TestLogger.Instance);
        Log(code);

        Assert.IsNotNull(wrap);
    }

    [TestMethod]
    public void CreateOverrideImplementation_Constructor()
    {
        int i = 100;
        double d = 200;
        long l = 3000;

        var expectedI = i;
        var expectedD = d;
        ConstructorClass expected = new (expectedI, ref expectedD, out var expectedL);

        var constructorArguments = new []
        {
            new ConstructorArguments(typeof(int), i),
            new ConstructorArguments(typeof(double).MakeByRefType(), d),
            new ConstructorArguments(typeof(long).MakeByRefType(), l),
        };
        var generator = new MinOpGenerator<DoNotCareType, DoNotCareType, ConstructorClass, bool>();
        var wrap = generator.CreateOverrideImplementation(
            constructorArguments: constructorArguments,
            sidecar: true,
            out var code,
            logger: TestLogger.Instance);
        Log(code);
        Assert.IsNotNull(wrap);

        var actualI = (int)constructorArguments[0].Value!;
        var actualD = (double)constructorArguments[1].Value!;
        var actualL = (long)constructorArguments[2].Value!;
        Assert.AreEqual(expected.I, wrap.I);
        Assert.AreEqual(expected.D, wrap.D);
        Assert.AreEqual(expectedI, actualI);
        Assert.AreEqual(expectedD, actualD);
        Assert.AreEqual(expectedL, actualL);
    }

    private static void TestCreateImplementationCache(Func<string, object> create)
    {
        // by picking a "random" class name that will make sure we won't bump into anything already in the cache 
        var className = $"{Generator.DefaultClassName}_{Guid.NewGuid():N}";
        Type? type = null;
        Dictionary<string, int> counts = new();

        var expected2 = new [] {"Completed Code Generation", "Completed Instance Generation"};
        var expected1 = new [] {
            "Completed Syntax Generation",
            "Completed Metadata References Generation",
            "Completed Completion Generation",
            "Completed Compile",
            "Completed Loading type",
        };
        HashSet<string> both = new(expected1.Union(expected2));

        for (int i = 0; i < 2; i++)
        {
            Log($"Starting {i}");
            TestLogger.Instance.Clear();
            var wrap = create(className);
            Log($"Done {i}");

            if (type is null) 
            {
                type = wrap.GetType();
            }
            else
            {
                Assert.AreEqual(type, wrap.GetType());
            }

            var message = TestLogger.Instance.Messages.Select(x => x.Split(':').First());
            if (i == 0)
            {
                var x = string.Join('|', both.Where(x => message.Count(y => x == y) != 1));
                if (!string.IsNullOrEmpty(x))
                {
                    throw new Exception(x);
                }

                Assert.IsTrue(both.All(x => message.Count(y => x == y) == 1));
            }
            else
            {
                Assert.IsTrue(expected2.All(x => message.Count(y => x == y) == 1));
            }
        }
        Assert.IsTrue(
          TestLogger.Instance.Messages
            .Select(x => x.Split(':').First())
            .All(x => both.Contains(x)));
    }

    private static async Task TestSidecarAsync(IEnumerable<TrackingSidecar.Callable> methodNames, IEnumerable<TrackingSidecar.CallableWithReturn> getters, IEnumerable<string> setters, IEnumerable<string> adders, IEnumerable<string> removers, Func<TrackingSidecar, Task> wrapActionAsync)
    {
        var sidecarMock = new Mock<TrackingSidecar>
        {
            CallBase = true
        };

        List<(TrackingSidecar.Callable callable, string name)> callableItems = new(methodNames.Select(x => (x, x.Name)));
        AddCallableItemsToList(getters, "get");
        AddToCallableNamesList(setters, "set");
        AddToCallableNamesList(adders, "add");
        AddToCallableNamesList(removers, "remove");

        foreach (var item in callableItems)
        {
            // With the out .CallBase() on these, we won't run these, and only log their output
            // So any thing that does come from these that lands in the log, did not have a Setup for them
            sidecarMock.Setup(TrackingSidecar.PreCallMockExpression(item.name));
            sidecarMock.Setup(item.callable.PostCallMockExpression(item.name));
        }

        await wrapActionAsync(sidecarMock.Object);

        foreach (var item in callableItems)
        {
            sidecarMock.Verify(TrackingSidecar.PreCallMockExpression(item.name), Times.Once);
            sidecarMock.Verify(item.callable.PostCallMockExpression(item.name), Times.Once);
        }

        void AddCallableItemsToList(IEnumerable<TrackingSidecar.Callable> items, string prefix)
        {
            callableItems.AddRange(items.Select(x => (x, $"{prefix}_{x.Name}")));
        }
        void AddToCallableNamesList(IEnumerable<string> items, string prefix)
        {
            callableItems.AddRange(items.Select(x => (new TrackingSidecar.Callable(x), $"{prefix}_{x}")));
        }
    }

    private static void EmptyHandler(object? sender, EventArgs e) { }

    private static void Log(string x) => Logger.LogMessage(x.Replace("{", "{{").Replace("}", "}}"));

}