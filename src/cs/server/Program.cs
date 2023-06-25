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
            BizDeckResult load_config_result = CmdLineOptions.InitAndLoadConfigHelper(args);
            if (!load_config_result.OK)
                return;
            if (ConfigHelper.Instance.BizDeckConfig.Console) {
                Win32.AllocConsole();
            }
            BizDeckLogger.InitLogging();
            var logger = new BizDeckLogger(typeof(Program));
            Server.Instance.Run();
            if (ConfigHelper.Instance.BizDeckConfig.Console) {
                Console.ReadKey(true);
            }
        }
    }
}