using System;
using System.IO;
using Swan.Logging;

namespace BizDeck
{
    public class BizDeckLogger
    {
        private Object bizDeckObject;
        public BizDeckLogger( Object bdo)
        {
            bizDeckObject = bdo;
        }

        public void Debug(string msg) {
            Logger.Debug($"{System.Environment.CurrentManagedThreadId} {msg}", bizDeckObject.GetType().Name);
        }

        public void Info(string msg) {
            Logger.Info($"{System.Environment.CurrentManagedThreadId} {msg}", bizDeckObject.GetType().Name);
        }

        public void Error(string msg)
        {
            Logger.Error($"{System.Environment.CurrentManagedThreadId} {msg}", bizDeckObject.GetType().Name);
        }

        public static void InitLogging(string log_file_base_name = "biz_deck")
        {
            // Swan's FileLogger takes care of inserting a date
            // suffix in the log path as 2nd paran true means "daily"
            string[] log_path_array = { ConfigHelper.Instance.LogDir,
                                        $"{log_file_base_name}.log" };
            var log_path = Path.Combine(log_path_array);
            var logger = new FileLogger(log_path, true);
            Logger.RegisterLogger(logger);
            // Now logging has been initialised, we can tell config_helper to create
            // it's own logger instance.
            ConfigHelper.Instance.CreateLogger();
        }
    }
}