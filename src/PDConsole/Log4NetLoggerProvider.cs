using System;
using log4net;
using log4net.Repository;
using Microsoft.Extensions.Logging;

namespace PDConsole
{
    /// <summary>
    /// Bridges Microsoft.Extensions.Logging to a pre-configured log4net repository.
    /// Used in single-file deployments where apache.log4net.Extensions.Logging's
    /// <c>AddLog4Net()</c> reads <c>Assembly.CodeBase</c> to locate log4net.config,
    /// which is unsupported in a single-file bundle.
    /// </summary>
    internal sealed class Log4NetLoggerProvider : ILoggerProvider
    {
        private readonly string _repositoryName;

        public Log4NetLoggerProvider(ILoggerRepository repository)
        {
            _repositoryName = repository.Name;
        }

        public ILogger CreateLogger(string categoryName) =>
            new Log4NetLogger(LogManager.GetLogger(_repositoryName, categoryName));

        public void Dispose()
        {
        }
    }

    /// <summary>
    /// Adapts a log4net <see cref="ILog"/> to the Microsoft.Extensions.Logging
    /// <see cref="ILogger"/> abstraction.
    /// </summary>
    internal sealed class Log4NetLogger : ILogger
    {
        private readonly ILog _log;

        public Log4NetLogger(ILog log)
        {
            _log = log;
        }

        public IDisposable BeginScope<TState>(TState state) => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => logLevel switch
        {
            LogLevel.Trace or LogLevel.Debug => _log.IsDebugEnabled,
            LogLevel.Information => _log.IsInfoEnabled,
            LogLevel.Warning => _log.IsWarnEnabled,
            LogLevel.Error => _log.IsErrorEnabled,
            LogLevel.Critical => _log.IsFatalEnabled,
            _ => false
        };

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception,
            Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            var message = formatter(state, exception);
            if (string.IsNullOrEmpty(message) && exception == null) return;

            switch (logLevel)
            {
                case LogLevel.Trace:
                case LogLevel.Debug:
                    _log.Debug(message, exception);
                    break;
                case LogLevel.Information:
                    _log.Info(message, exception);
                    break;
                case LogLevel.Warning:
                    _log.Warn(message, exception);
                    break;
                case LogLevel.Error:
                    _log.Error(message, exception);
                    break;
                case LogLevel.Critical:
                    _log.Fatal(message, exception);
                    break;
            }
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            private NullScope()
            {
            }

            public void Dispose()
            {
            }
        }
    }
}
