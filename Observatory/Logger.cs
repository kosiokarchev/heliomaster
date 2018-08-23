using System;
using System.Configuration;
using Python.Runtime;

namespace heliomaster {
    public enum LoggingLevel {
        Debug    = 10,
        Info     = 20,
        Warning  = 30,
        Error    = 40,
        Critical = 50
    }

    [SettingsSerializeAs(SettingsSerializeAs.Xml)]
    public class LoggerParameters {
        public bool IsEnabled { get; set; }
        public int BackupCount { get; set; }
        public string Filename { get; set; }

        public LoggingLevel Level;
    }

    public static class Logger {
        private static void put(string msg, LoggingLevel level = LoggingLevel.Debug) {
            Console.WriteLine($"{DateTime.Now:s}: {level}: {msg}");
            if (Py.Running && Py.logger != null) {
                Py.Run(() => {
                    Py.logger.log((int) level, msg);
                });
            }
        }

        public static void debug(string msg) => put(msg, LoggingLevel.Debug);
        public static void info(string msg) => put(msg, LoggingLevel.Info);
        public static void warning(string msg) => put(msg, LoggingLevel.Warning);
        public static void error(string msg) => put(msg, LoggingLevel.Error);
        public static void critical(string msg) => put(msg, LoggingLevel.Critical);
    }
}
