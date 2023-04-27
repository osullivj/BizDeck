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

        public void Info(string msg) {
            Logger.Info($"{System.Environment.CurrentManagedThreadId} {msg}", bizDeckObject.GetType().Name);
        }

        public void Error(string msg)
        {
            Logger.Error($"{System.Environment.CurrentManagedThreadId} {msg}", bizDeckObject.GetType().Name);
        }

        public static void InitLogging(string log_dir)
        {
            // Swan's FileLogger takes care of inserting a date
            // suffix in the log path as 2nd paran true means "daily"
            var log_path = Path.Combine(new string[] { log_dir, "biz_deck.log" });
            var logger = new FileLogger(log_path, true);
            Logger.RegisterLogger(logger);
        }
    }
}