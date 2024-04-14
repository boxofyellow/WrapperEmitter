using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using UnitTestLogging = Microsoft.VisualStudio.TestTools.UnitTesting.Logging;

namespace WrapperEmitter.Tests;

public class TestLogger : ILogger
{
    public static readonly IReadOnlySet<string> TimingsTraces = new HashSet<string>{
        "Completed Code Generation: {duration}",
        "Completed Syntax Generation: {duration}",
        "Completed Metadata References Generation: {duration}",
        "Completed Compilation Generation: {duration}",
        "Completed Compile: {duration}",
        "Completed Loading type: {duration}",
        "Completed Instance Generation: {duration}",
    };

    private class TimingInfo
    {
        public TimingInfo(string format) => Format = format;
        public readonly string Format;
        public readonly List<TimeSpan> Durations = new();

        public override string ToString()
        {
            var durations = Durations.OrderBy(x => x).ToArray();
            TimeSpan sum = TimeSpan.Zero;
            foreach (var duration in durations)
            {
                sum += duration;
            }
            var min = durations.First();
            var max = durations.Last();
            var median = durations[durations.Length / 2];
            return $"Sum {sum} | {Format} | Min {min} | Max {max} | Medium {median} | Avg {sum/durations.Length} | Count {durations.Length}";
        }
    }


    public static readonly TestLogger Instance = new();
    public IEnumerable<string> Messages => m_messages;
    public void Clear()
    {
        lock (m_messages)
        m_messages.Clear();
    }

    private readonly List<string> m_messages = new();

    private readonly Dictionary<string, TimingInfo> m_timingInfos = new();

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
        if (state is IReadOnlyCollection<KeyValuePair<string, object?>> items)
        {
            if (TryGet<string>(items, "{OriginalFormat}", out var format)
                && TimingsTraces.Contains(format)
                && TryGet<TimeSpan>(items, "duration", out var duration))
            {
                lock(m_timingInfos)
                {
                    if (!m_timingInfos.TryGetValue(format, out var info))
                    {
                        info = new(format);
                        m_timingInfos[format] = info;
                    }
                    info.Durations.Add(duration);
                }
            }
        }
    }

    public void EmitTimingInfo()
    {
        UnitTestLogging.Logger.LogMessage("Timing Summaries");
        lock (m_timingInfos)
        {
            foreach (var item in m_timingInfos.Values)
            {
                UnitTestLogging.Logger.LogMessage("{0}", item.ToString());
            }
        }
    }

    private bool TryGet<T>(IReadOnlyCollection<KeyValuePair<string, object?>> items, string key, [NotNullWhen(true)] out T? value)
    {
        foreach (var item in items)
        {
            if (item.Key == key && item.Value is T returnValue)
            {
                value = returnValue;
                return true;
            }
        }

        value = default;
        return false;
    }
}