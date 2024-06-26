This library can be used to create "wrapped" objects dynamically (at runtime) allowing for a little meta-programming to fulfil contracts like
interfaces or sub-classing existing base classes.

You can think of it as very similar to [DispatchProxy](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.dispatchproxy), expect it defers most[^1] of the reflection magic to the creation of the object.  The minimal reflections use later should mean much better performance after the object is created.  Plus I'm not sure how that approach works for types that cannot be boxed 🤷

[^1]: We still need a little reflections magic to make protected generic methods work to fulfill interface contracts.  It is only needed on the first time the generic method is invoked for give pairing of generic arguments.

# Interfaces Contracts

You can create a class that implements the provided interface and then an instance of that class will be created.  An instance of something that
already implements the interface is required, and by default all the interfaces methods will be delegated to the existing implementation.
Additional you can change that default behavior to use any combination of ...
1. Run arbitrary C# code before the delegated implementation
1. Replace the delegated implementation with any arbitrary C# code
1. Run arbitrary C# code after the delegated implementation

# Sub-classing Contracts

You can create a class that uses the provided type as its base class and then an instance of that class will be created.  Here an additional instance
of an object that can be delegated is not required, instead all over-written methods will be delegate to that base class.  However any (and all -
including optional) parameters for the base class's construct is required.

Just like interfaces this default behavior can be changed.

One thing to call out, that base class may be abstract, if any (and all) abstract partitions of contract must be replaced with valid C# code you
provide.

# Sidecars

Both wrappers also require a sidecar, this can be any type that you want and is strictly there to make injecting arbitrary C# easer.  A member
variable will be added to generated class and can be referenced within any of the injected arbitrary code.

You can use to store any required state to support your arbitrary code.  It can also be used to reduce the about code that needs to be injected.
While you **_could_** include arbitrarily large blocks of code at any given point.  Doing so has some real draw backs
1. All this injected C# code can't be seen by your tools (IDE, linters, vulnerability checks, ect.), it is likely harder on your human helpers too 😜
1. Writing code this way is hard... sliding your code along side other code can be hard
1. You will need to keep track of the types you are referencing so that their decencies can be added to the dynamic assembly
1. No `using` statements are added top of the generated class file, so you need to fully qualify all types

So in short you can put all the logic needed for the various injection hooks in side your sidecar object.  You can then references it within your
injected C# code using the string `Generator.SidecarVariableName` 

# To Use

To use this library you need to implement one these interface (`IInterfaceGenerator` or `IOverrideGenerator`).  Both of these have have the same method
but each has its own extension method (`CreateInterfaceImplementation` and `CreateOverrideImplementation` respectively) that will create the wrapped
object when invoked.

# Limitations
- Can't override strictly internal method
- For override wrappers from types that use a factory for their creation it is very difficult and required recreating their factory method
- Can't create wrappers for objects declared within in-memory assemblies (all though you can use them as the instances of the implementation required for interface wrappers or as instances of sidecars for any kind of wrapper)

# Items To Add to this doc
- [ ] Unsafe Code info
- [ ] External references
- [ ] The `RestrictedAccessHelper`

# Resources

_Some_ of the resources used to create this
- https://weblog.west-wind.com/posts/2022/Jun/07/Runtime-CSharp-Code-Compilation-Revisited-for-Roslyn
- https://www.cshandler.com/2015/10/compiling-c-60-code-using-roslyn.html
- https://github.com/laurentkempe/DynamicRun
- https://www.youtube.com/watch?v=0H66H8PxcB8