﻿using Microsoft.Extensions.Logging;
using ScenarioAlerter.AlertServices;
using System.Text;

namespace ScenarioAlerter
{
    public interface IScenarioAlerter
    {
        public void Run();
    }

    public class Alerter : IScenarioAlerter
    {
        private readonly ILogger<IScenarioAlerter> _logger;
        private readonly IAlertService _alertService;
        private readonly AlerterOptions _options;

        private string LastReadLine;
        private string LogFile;
        private FileSystemWatcher watcher;

        public Alerter(ILogger<IScenarioAlerter> logger, IAlertService alertService,
            AlerterOptions options)
        {
            _logger = logger;
            _alertService = alertService;
            _options = options;

            LogFile = _options.LogFile;
            ValidateLogFilePath(LogFile);

            this.Run();
        }

        public void Run()
        {
            var fileDirectory = Path.GetDirectoryName(LogFile);
            var fileName = Path.GetFileName(LogFile);

            watcher = new FileSystemWatcher(fileDirectory);
            watcher.Filter = fileName;

            watcher.NotifyFilter = NotifyFilters.Attributes
                     | NotifyFilters.CreationTime
                     | NotifyFilters.DirectoryName
                     | NotifyFilters.FileName
                     | NotifyFilters.LastAccess
                     | NotifyFilters.LastWrite
                     | NotifyFilters.Security
                     | NotifyFilters.Size;

            watcher.Changed += OnChanged;
            watcher.Error += OnError;

            watcher.EnableRaisingEvents = true;

            Thread t = new Thread(RefreshFile);
            t.IsBackground = true;
            t.Start();
        }

        private void ValidateLogFilePath(string path)
        {
            if (!File.Exists(path))
            {
                _logger.LogCritical($"No file at {path} exists!");
                throw new FileNotFoundException($"No file at {path} exists!");
            }
        }

        public static string RemoveTimestampFromLogMessage(string message)
        {
            return message.Split("] ")[1];
        }

        private void RefreshFile()
        {
            while (true)
            {

                using (var fs = new FileStream(LogFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    Thread.Sleep(500);
            }
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {

            if (e.ChangeType != WatcherChangeTypes.Changed)
            {
                return;
            }

            var lastLine = ReadLines($"{e.FullPath}").LastOrDefault();

            if (lastLine != null && lastLine != LastReadLine)
            {
                try
                {
                    var message = RemoveTimestampFromLogMessage(lastLine);
                    _alertService.SendAlertAsync($"{message}");
                }
                catch
                {
                    _logger.LogWarning($"No timestamp was found. Not a valid log message. An alert will not be sent for message: {lastLine}");
                }
            }

            LastReadLine = lastLine;
        }
        public IEnumerable<string> ReadLines(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var sr = new StreamReader(fs, Encoding.UTF8))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    yield return line;
                }
            }
        }

        private static void OnError(object sender, ErrorEventArgs e) =>
PrintException(e.GetException());

        private static void PrintException(Exception? ex)
        {
            if (ex != null)
            {
                Console.WriteLine($"Message: {ex.Message}");
                Console.WriteLine("Stacktrace:");
                Console.WriteLine(ex.StackTrace);
                Console.WriteLine();
                PrintException(ex.InnerException);
            }
        }
    }


    public class AlerterOptions
    {
        public string LogFile { get; set; }
        public string AlertMethod { get; set; }
    }
}
