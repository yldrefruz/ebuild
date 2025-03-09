using System.CommandLine;
using System.Text;
using ebuild.api;
using ebuild.Commands;
using ebuild.Compilers;
using ebuild.Platforms;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;


namespace ebuild;

public static class EBuild
{
    public static bool DisableLogging = false;

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
            .AddFilter(level => !DisableLogging && level >= Config.Get().MinLogLevel && level != LogLevel.None)
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

    public static async Task<int> Main(string[] args)
    {
        PlatformRegistry.GetInstance().RegisterAllFromAssembly(typeof(EBuild).Assembly);
        CompilerRegistry.GetInstance().RegisterAllFromAssembly(typeof(EBuild).Assembly);


        var rootCommand = new RootCommand
        {
            new BuildCommand(),
            new GenerateCommand(),
            new PropertyCommand(),
            new CheckCommand()
        };

        return await rootCommand.InvokeAsync(args);
    }
}