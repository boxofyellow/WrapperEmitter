using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Octokit;

namespace WrapperEmitter.Example;

public static class ConnectionWrapper
{
    public static IConnection Create(Connection connection, ILogger logger)
    {
        ConnectionWrapSidecar sidecar = new(logger);
        ConnectionWrapGenerator generator = new();
        return generator.CreateInterfaceImplementation(implementation: connection, sidecar, out var _, logger: logger);
    }
}

public class ConnectionWrapSidecar
{
    private readonly ILogger m_logger;

    public ConnectionWrapSidecar(ILogger logger) => m_logger = logger;

    public DateTime StartingRestCall(Uri uri, [CallerMemberName] string callerName = "") 
    {
        lock (m_logger)
        {
            m_logger.LogInformation("[{callerName}] Start: {uri}", callerName, uri);
        }
        return DateTime.UtcNow;
    }

    public void EndedRestCall<T>(Uri uri, IApiResponse<T> response, DateTime start, [CallerMemberName] string callerName = "")
    {
        var type = typeof(T).FullTypeExpression();
        var duration = DateTime.UtcNow - start;
        var statusCode = response.HttpResponse.StatusCode;
        var requestId = response.HttpResponse.Headers.GetValueOrDefault("X-GitHub-Request-Id", string.Empty);
        var tokenExpiration = response.HttpResponse.Headers.GetValueOrDefault("github-authentication-token-expiration", string.Empty);

        lock (m_logger)
        {
            m_logger.LogInformation(
                "[{callerName}] Done: {uri} -> {type} {duration} {statusCode} requestId:{requestId} tokenExpiration:{tokenExpiration}",
                callerName, uri, type, duration, statusCode, requestId, tokenExpiration);
        }
    }
}

public class ConnectionWrapGenerator : IInterfaceGenerator<IConnection, Connection, ConnectionWrapSidecar>
{
    public string? PreMethodCall(MethodInfo method) 
        => ShouldInterjectCode(method) ? c_preMethod : null;

    public string? PostMethodCall(MethodInfo method)
        => ShouldInterjectCode(method) ? c_postMethod : null;

    public static bool ShouldInterjectCode(MethodInfo method)
    {
        var returnType = method.ReturnType;
        if (!returnType.IsGenericTypeOf(typeof(Task<>)))
        {
            return false;
        }
        var taskType = returnType.GetGenericArguments().Single();
        if (!taskType.IsGenericTypeOf(typeof(IApiResponse<>)))
        {
            return false;
        }
        return method.GetParameters().Any(x => x.ParameterType == typeof(Uri) && x.Name == c_uriVarName);
    }

    private const string c_uriVarName = "uri";
    private const string c_startVarName = $"{Generator.VariablePrefix}start";
    private const string c_preMethod = $"var {c_startVarName} = {Generator.SidecarVariableName}.{nameof(ConnectionWrapSidecar.StartingRestCall)}({c_uriVarName});";
    private const string c_postMethod = $"{Generator.SidecarVariableName}.{nameof(ConnectionWrapSidecar.EndedRestCall)}({c_uriVarName}, {Generator.ReturnVariableName}, {c_startVarName});";
}