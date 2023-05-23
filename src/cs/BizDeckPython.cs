using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Scripting.Hosting;
using IronPython.Hosting;

// IronPython integration
namespace BizDeck {

    public class BizDeckPython {
        private ConfigHelper config_helper;
        private BizDeckLogger logger;
        private ScriptEngine python_engine;

        public BizDeckPython(ConfigHelper ch) {
            logger = new(this);
            config_helper = ch;
            python_engine = IronPython.Hosting.Python.CreateEngine();
        }

        public async Task<(bool, string)> RunScript(string script_path) {
            string error = null;
            bool ok = true;
            try {
                string python_source = await File.ReadAllTextAsync(script_path);
                ScriptSource python_script = python_engine.CreateScriptSourceFromString(python_source);
                var result = python_script.Execute();
                logger.Info($"RunScript: {result}");
            }
            catch (Exception ex) {
                ok = false;
                error = $"{script_path} failed {ex}";
                logger.Error($"RunScript: {error}");
            }
            return (ok, error);
        }
    }
}
