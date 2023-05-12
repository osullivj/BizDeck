using System;
using System.Threading;
using System.Threading.Tasks;
using EmbedIO;
using EmbedIO.Files;
using EmbedIO.Actions;
using CommandLine;

namespace BizDeck {

    class Program {
        private const bool UseFileCache = true;

        static void Main(string[] args) {
            // First parse cmd line opts...
            var parser = new Parser();
            var result = parser.ParseArguments<CmdLineOptions>(args);
            // first load the config
            var config_helper = new ConfigHelper(result.Value);
            var config = config_helper.LoadConfig();
            if (config.Console) {
                Win32.AllocConsole();
            }
            BizDeckLogger.InitLogging(config_helper);
            var logger = new BizDeckLogger(typeof(Program));
            var server = new Server(config_helper);
            server.Run();
            if (config.Console) {
                Console.ReadKey(true);
            }
        }
    }
}