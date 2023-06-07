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
            var parser = new Parser();
            var result = parser.ParseArguments<CmdLineOptions>(args);
            // Create and init config singleton
            ConfigHelper.Instance.Init(result.Value);
            var config = ConfigHelper.Instance.LoadConfig();
            if (config.Console) {
                Win32.AllocConsole();
            }
            BizDeckLogger.InitLogging();
            var logger = new BizDeckLogger(typeof(Program));
            var server = new Server();
            server.Run();
            if (config.Console) {
                Console.ReadKey(true);
            }
        }
    }
}