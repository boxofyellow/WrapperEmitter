using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;

namespace WrapperEmitter;

public static partial class Generator
{
    private static D CreateFactory<D>(IGenerator generator, string code, string @namespace, string className, IEnumerable<Type> types, bool usesUnsafe, ILogger logger, LogLevel logLevel)
        where D : Delegate
    {
        CSharpCompilationOptions? compilationOptions = generator.CompilationOptions;
        if (usesUnsafe)
        {
            compilationOptions ??= new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);
            compilationOptions = compilationOptions.WithAllowUnsafe(enabled: usesUnsafe);
        }

        try
        {
            var type = CreateType(code, @namespace, @className, types, generator.ParseOptions, compilationOptions, logger, logLevel);

            DateTime time = DateTime.UtcNow;

            var setupMethod = type.GetMethod(c_setupMethodName, BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw UnexpectedReflectionsException.FailedToFindMethod(type, c_setupMethodName);
            setupMethod.Invoke(obj: null, parameters: null);

            var factoryMethod = type.GetMethod(c_factoryMethodName, BindingFlags.Static | BindingFlags.Public)
                ?? throw UnexpectedReflectionsException.FailedToFindMethod(type, c_factoryMethodName);

            var result = RestrictedHelper.CreateDynamicMethodDelegate<D>(factoryMethod);
            logger.Log(logLevel, "Completed Factory Generation: {duration}", DateTime.UtcNow - time);

            return result;
        }
        catch (Exception e)
        {
            throw new UnexpectedReflectionsException(code, e);
        }
    }

    // Public for testing
    public static Type CreateType(string code, string @namespace, string className, IEnumerable<Type> types, CSharpParseOptions? parseOptions, CSharpCompilationOptions? compilationOptions, ILogger logger, LogLevel logLevel)
    {
        DateTime time = DateTime.UtcNow;
        var syntaxTree = GenerateSyntaxTree(code, parseOptions);
        logger.Log(logLevel, "Completed Syntax Generation: {duration}", DateTime.UtcNow - time);

        time = DateTime.UtcNow;
        var references = GetMetadataReferences(types);
        logger.Log(logLevel, "Completed Metadata References Generation: {duration}", DateTime.UtcNow - time);

        time = DateTime.UtcNow;
        var compilation = GenerateCompilation(syntaxTree, references, compilationOptions);
        logger.Log(logLevel, "Completed Compilation Generation: {duration}", DateTime.UtcNow - time);

        time = DateTime.UtcNow;
        var bytes = Compile(compilation, syntaxTree, logger);
        logger.Log(logLevel, "Completed Compile: {duration}", DateTime.UtcNow - time);

        time = DateTime.UtcNow;
        var assembly = Assembly.Load(bytes);
        var result = assembly.GetType($"{@namespace}.{className}")
            ?? throw UnexpectedReflectionsException.FailedToFindType();
        logger.Log(logLevel, "Completed Loading type: {duration}", DateTime.UtcNow - time);

        return result;
    }

    private static IEnumerable<Type> GetExpandTypes(Type type)
    {
        // We don't need Base classes or interfaces or method return types / parameters, those will all get picked up as dependencies as needed 
        // In Short typeof(List<Xyz>) will get us dependencies of typeof(List<>) not typeof(Xyz), so we need to pull those in our self
        // Oddly the same does not go for array. 🤷
        // NOTE: We don't have to worry about truly Generic Parameters, their Assembles (which is all we really needed) is the Assembly where the
        // parameter is used
        List<Type> result = new() { type };
        foreach (var argument in type.GetGenericArguments())
        {
            result.AddRange(GetExpandTypes(argument));
        }
        return result;
    }

    private static SyntaxTree GenerateSyntaxTree(string code, CSharpParseOptions? options)
    {
        var codeString = SourceText.From(code);
        return SyntaxFactory.ParseSyntaxTree(codeString, options);
    }

    private static List<MetadataReference> GetMetadataReferences(IEnumerable<Type> types)
    {
        var assemblyNames = types
            .SelectMany(x => GetExpandTypes(x))
            .Select(x => x.Assembly.GetName())
            .Distinct(AssemblyNameComparer.Instance);

        List<MetadataReference> result = new();
        Queue<AssemblyName> queue = new(assemblyNames);
        HashSet<AssemblyName> visited = new(AssemblyNameComparer.Instance);

        // I looked for ways to avoid this deep search.
        // We could try hard-coding these https://weblog.west-wind.com/posts/2022/Jun/07/Runtime-CSharp-Code-Compilation-Revisited-for-Roslyn 
        while (queue.TryDequeue(out var assemblyName))
        {
            visited.Add(assemblyName);
            var assembly = Assembly.Load(assemblyName);
            foreach (var dependency in assembly.GetReferencedAssemblies())
            {
                if (!visited.Contains(dependency))
                {
                    visited.Add(dependency);
                    queue.Enqueue(dependency);
                }
            }
            result.Add(MetadataReference.CreateFromFile(assembly.Location));
        }

        return result;
    }

    private static CSharpCompilation GenerateCompilation(SyntaxTree syntaxTree, List<MetadataReference> references, CSharpCompilationOptions? options)
        => CSharpCompilation.Create("Generated.dll",
            new[] { syntaxTree },
            references: references,
            options: options);

    private static byte[] Compile(CSharpCompilation compilation, SyntaxTree syntaxTree, ILogger logger)
    {
        using var peStream = new MemoryStream();
        var result = compilation.Emit(peStream);

        if (!result.Success)
        {
            StringBuilder builder = new();
            builder.AppendLine("Compilation done with error.");
            var failures = result.Diagnostics.Where(diagnostic => diagnostic.IsWarningAsError || diagnostic.Severity == DiagnosticSeverity.Error);

            foreach (var diagnostic in failures)
            {
                var position = syntaxTree.GetLineSpan(diagnostic.Location.SourceSpan).StartLinePosition;
                logger.LogError("{diagnostic.id}: [{diagnostic.line}:{diagnostic.column}] {diagnostic.message}", diagnostic.Id, position.Line, position.Character, diagnostic.GetMessage());
                builder.AppendLine($"{diagnostic.Id}: [{position.Line}:{position.Character}] {diagnostic.GetMessage()}");
            }

            throw new InvalidCSharpException(builder.ToString());
        }
        foreach (var diagnostic in result.Diagnostics)
        {
            var position = syntaxTree.GetLineSpan(diagnostic.Location.SourceSpan).StartLinePosition;
            logger.LogWarning("{diagnostic.id}: [{diagnostic.line}:{diagnostic.column}] {diagnostic.message}", diagnostic.Id, position.Line, position.Character, diagnostic.GetMessage());
        }

        peStream.Seek(0, SeekOrigin.Begin);

        return peStream.ToArray();
    }
}