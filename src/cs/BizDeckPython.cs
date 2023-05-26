﻿using System;
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
        private ScriptEngine action_engine;
        private ScriptScope action_scope;


        public BizDeckPython(ConfigHelper ch) {
            logger = new(this);
            config_helper = ch;
            // https://stackoverflow.com/questions/14139766/run-a-particular-python-function-in-c-sharp-with-ironpython
            action_engine = IronPython.Hosting.Python.CreateEngine();
            string biz_deck_py_path = Path.Combine(ch.PythonSourcePath, "bizdeck.py");
            // NB bizdeck.py just defines funcs, it has no __main__ executable code,
            // so to execute it is to load the functions into the scope.
            action_scope = action_engine.ExecuteFile(biz_deck_py_path);
            // Set global vars in bizdeck.py
            action_scope.SetVariable("BDRoot", config_helper.LocalAppDataPath);
            action_scope.SetVariable("Logger", logger);
        }

        public async Task<(bool, string)> RunActionFunction(string function, List<dynamic> args) {
            bool ok = false;
            string error = null;
            try {
                // IronPython cannot auto marshall between Python and C# tuples,
                // so we just return a string. null or empty is success.
                dynamic func = action_scope.GetVariable(function);
                string result = func(args);
                ok = String.IsNullOrWhiteSpace(result);
                return (ok, result);
            }
            catch (Exception ex) {
                error = $"func[{function}] failed {ex}";
                logger.Error($"RunActionFunction: {error}");
            }
            await Task.Delay(0);
            return (ok, error);
        }

        // Run batch script creates an instance of the Python runtime for the
        // script execution, just as if we'd run a script at the command line.
        // The options parameter allows us to pass in cmd line params and env vars.
        public async Task<(bool, string)> RunBatchScript(string script_path, Dictionary<string,object> options = null) {
            string error = null;
            bool ok = true;
            try {
                string python_source = await File.ReadAllTextAsync(script_path);
                ScriptEngine one_shot_python_engine = IronPython.Hosting.Python.CreateEngine(options);
                ScriptSource python_script = one_shot_python_engine.CreateScriptSourceFromString(python_source);
                var result = python_script.Execute();
                logger.Info($"RunScript: result[{result}] from [{script_path}]");
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
