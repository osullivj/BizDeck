using System;
using System.Threading;
using System.Threading.Tasks;
using EmbedIO;
using EmbedIO.Files;
using EmbedIO.Actions;
using CommandLine;

namespace BizDeck {

    class Program {

        static void Main(string[] args) {
            // First parse cmd line opts...
            var config = CmdLineOptions.InitAndLoadConfigHelper(args);
            if (config.Console) {
                Win32.AllocConsole();
            }
            BizDeckLogger.InitLogging();
            var logger = new BizDeckLogger(typeof(Program));
            Server.Instance.Run();
            if (config.Console) {
                Console.ReadKey(true);
            }
        }
    }
}