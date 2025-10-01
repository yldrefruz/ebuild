using System.Text;
using CliFx;
using ebuild.api;
using ebuild.api.Toolchain;
using ebuild.Platforms;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;


namespace ebuild
{
    public static class EBuild
    {
    public static bool DisableLogging = false;
    public static bool VerboseEnabled = false;
        // TODO: move the logging to the serilog library.
        private class LoggerFormatter : ConsoleFormatter
        {
            public LoggerFormatter() : base(nameof(LoggerFormatter))
            {
            }

            public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider,
                TextWriter textWriter)
            {
                textWriter.WriteLine(
                    $"{logEntry.LogLevel} {logEntry.Category} : {logEntry.Formatter(logEntry.State, logEntry.Exception)}");
            }
        }

        private class FileLoggerProvider : ILoggerProvider
        {
            private readonly StreamWriter _logFileWriter;

            public FileLoggerProvider(StreamWriter logFileWriter)
            {
                _logFileWriter = logFileWriter;
            }

            public void Dispose()
            {
                _logFileWriter.Dispose();
            }

            public ILogger CreateLogger(string categoryName)
            {
                return new FileLogger(categoryName, _logFileWriter);
            }
        }

        private class FileLogger : ILogger
        {
            private readonly string _categoryName;
            private readonly StreamWriter _logFileWriter;

            public FileLogger(string categoryName, StreamWriter logFileWriter)
            {
                _categoryName = categoryName;
                _logFileWriter = logFileWriter;
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (!IsEnabled(logLevel))
                    return;
                var msg = formatter(state, exception);
                _logFileWriter.WriteLine(msg);
                _logFileWriter.Flush();
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return logLevel != LogLevel.None;
            }

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            {
                return null;
            }
        }

        public static readonly ILoggerFactory LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
            builder
                .AddConsole()
                .AddSimpleConsole(options => { options.SingleLine = true; })
                .AddFilter(level => !DisableLogging && (VerboseEnabled ? level >= LogLevel.Trace : level >= LogLevel.Information) && level != LogLevel.None)
                .AddConsoleFormatter<LoggerFormatter, ConsoleFormatterOptions>(options => { })
                .AddProvider(new FileLoggerProvider(
                    new StreamWriter(CreateLogFile(), Encoding.UTF8)))
        );

        private static FileStream CreateLogFile()
        {
            var logsDir = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ebuild", "logs");
            Directory.CreateDirectory(logsDir);
            var name = Path.Join(logsDir, $"{DateTime.Now:dd-MM-yyyy.HH-mm-ss-ff}.log");
            return File.Open(name, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite);
        }

        public static string FindEBuildApiDllPath()
        {
            return typeof(ModuleBase).Assembly.Location; // ModuleBase is in ebuild.api
        }

        static bool _initialized = false;
        public static void InitializeEBuild()
        {
            if (_initialized) return;
            _initialized = true;

            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;
            AutoRegisterServiceAttribute.RegisterAllInAssembly(typeof(EBuild).Assembly);
            PlatformRegistry.GetInstance().RegisterAllFromAssembly(typeof(EBuild).Assembly);
            IToolchainRegistry.Get().RegisterAllFromAssembly(typeof(EBuild).Assembly);
        }




        public static async Task<int> Main(string[] args)
        {
            InitializeEBuild();

            return await new CliApplicationBuilder()
                .AddCommandsFromThisAssembly()
                .Build()
                .RunAsync(args);
        }
    }
}