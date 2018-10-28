using System;
using System.Configuration;

namespace heliomaster {
    public enum LoggingLevel {
        Debug    = 10,
        Info     = 20,
        Warning  = 30,
        Error    = 40,
        Critical = 50
    }

    /// <summary>
    /// Parameters describing a Python <a href="https://docs.python.org/3/library/logging.handlers.html#logging.handlers.TimedRotatingFileHandler">TimedRotatingHandler</a>
    /// as passed to <c>libhm.logger_setup</c>.
    /// </summary>
    [SettingsSerializeAs(SettingsSerializeAs.Xml)]
    public class LoggerParameters {
        public bool IsEnabled { get; set; }
        public int BackupCount { get; set; }
        public string Filename { get; set; }

        public LoggingLevel Level;
    }

    /// <summary>
    /// The global application logging facility. Imitates the Python logging tools by defining methods for each
    /// <see cref="LoggingLevel"/>.
    /// </summary>
    public static class Logger {
        /// <summary>
        /// Utility function that logs a message using <see cref="Py.logger"/>
        /// </summary>
        /// <param name="msg">The message to log.</param>
        /// <param name="level">The <see cref="LoggingLevel"/> if the message.</param>
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
