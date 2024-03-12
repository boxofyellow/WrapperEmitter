using Microsoft.Extensions.Logging;
using UnitTestLogging = Microsoft.VisualStudio.TestTools.UnitTesting.Logging;

namespace WrapperEmitter.Tests;

public class TestLogger : ILogger
{
    public static readonly TestLogger Instance = new();
    public IEnumerable<string> Messages => m_messages;
    public void Clear()
    {
        lock (m_messages)
        m_messages.Clear();
    }

    private readonly List<string> m_messages = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        Log(LogLevel.Information, new EventId(), state, exception: null, (s, e) => $"{nameof(BeginScope)} state: {s}, exception: {e}");
        return null;
    }

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var text = formatter(state, exception);
        lock (m_messages)
        {
            m_messages.Add(text);
        }
        UnitTestLogging.Logger.LogMessage("{0} {1} {2} {3}", logLevel, eventId, exception, text);
    }
}