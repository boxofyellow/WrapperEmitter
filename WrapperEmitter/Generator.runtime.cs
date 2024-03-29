using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;

namespace WrapperEmitter;

public static partial class Generator
{
    private static T CreateObject<T>(IGenerator generator, string code, string @namespace, string className, object?[] constructorValues, Type[] extraTypes, ILogger logger, LogLevel logLevel)
    {
        List<Type> allType = new(extraTypes);
        allType.AddRange(generator.ExtraTypes);

        try
        {
            ClassCreationDefinition key = new(code, @namespace, @className, allType, generator.ParseOptions, generator.CompilationOptions);
            var type = m_typeCache.GetOrCreate(key, CreateType, logger, logLevel);

            DateTime time = DateTime.UtcNow;
            var obj = Activator.CreateInstance(type, constructorValues)
                ?? throw UnexpectedReflectionsException.CreatedObjectIsNull<T>();
            logger.Log(logLevel, "Completed Instance Generation: {duration}", DateTime.UtcNow - time);

            // The cast should be safe... this is our created type and it should be cast-able, after all thats the whole point of...
            return (T)obj;
        }
        catch (Exception e)
        {
            throw new UnexpectedReflectionsException(code, e);
        }
    }

    // Public for testing
    public static Type CreateType(ClassCreationDefinition definition, ILogger logger, LogLevel logLevel)
    {
        DateTime time = DateTime.UtcNow;
        var syntaxTree = GenerateSyntaxTree(definition.Code, definition.ParseOptions);
        logger.Log(logLevel, "Completed Syntax Generation: {duration}", DateTime.UtcNow - time);

        time = DateTime.UtcNow;
        var references = GetMetadataReferences(definition.AssemblyNames);
        logger.Log(logLevel, "Completed Metadata References Generation: {duration}", DateTime.UtcNow - time);

        time = DateTime.UtcNow;
        var complication = GenerateCompilation(syntaxTree, references, definition.CompilationOptions);
        logger.Log(logLevel, "Completed Completion Generation: {duration}", DateTime.UtcNow - time);

        time = DateTime.UtcNow;
        var bytes = Compile(complication, syntaxTree, logger);
        logger.Log(logLevel, "Completed Compile: {duration}", DateTime.UtcNow - time);

        time = DateTime.UtcNow;
        var assembly = Assembly.Load(bytes);
        var result = assembly.GetType($"{definition.Namespace}.{definition.ClassName}")
            ?? throw UnexpectedReflectionsException.FailedToFindType();
        logger.Log(logLevel, "Completed Loading type: {duration}", DateTime.UtcNow - time);

        return result;
    }

    private static SyntaxTree GenerateSyntaxTree(string code, CSharpParseOptions? options)
    {
        var codeString = SourceText.From(code);
        return SyntaxFactory.ParseSyntaxTree(codeString, options);
    }

    private static List<MetadataReference> GetMetadataReferences(ISet<AssemblyName> assemblyNames)
    {
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

    private static byte[] Compile(CSharpCompilation complication, SyntaxTree syntaxTree, ILogger logger)
    {
        using var peStream = new MemoryStream();
        var result = complication.Emit(peStream);

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

    private readonly static TypeCache m_typeCache = new();
}