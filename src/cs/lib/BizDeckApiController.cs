using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EmbedIO;
using EmbedIO.Routing;
using EmbedIO.WebApi;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BizDeck {
    public class BizDeckApiController : WebApiController {
        private ConfigHelper config_helper;
        private BizDeckLogger logger;
        private CacheEntryConverter cache_entry_converter = new();
        private List<string> add_button_request_keys = new() { "script_name", "script", "background" };

        public BizDeckApiController(ConfigHelper ch) {
            config_helper = ch;
            logger = new(this);
        }

        // http://localhost:9271/api/status
        [Route(HttpVerbs.Get, "/status")]
        public Task<BizDeckStatus> Status() => Task.FromResult(BizDeckStatus.Instance);

        // http://localhost:9271/api/config
        [Route(HttpVerbs.Get, "/config")]
        public Task<BizDeckConfig> Config() => Task.FromResult(config_helper.BizDeckConfig);

        [Route(HttpVerbs.Get, "/cache/{group?}/{cache_key?}")]
        public async Task GetCacheEntry(string group, string cache_key) {
            CacheEntry cache_entry = DataCache.Instance.GetCacheEntry(group, cache_key);
            if (cache_entry == null) {
                throw HttpException.NotFound();
            }
            HttpContext.Response.ContentType = "application/json";
            using (var writer = HttpContext.OpenResponseText()) {
                await writer.WriteAsync(JsonConvert.SerializeObject(cache_entry, cache_entry_converter));
            }
        }

        [Route(HttpVerbs.Get, "/run/apps/{app?}")]
        public async Task<string> RunApp(string app) {
            AppDriver app_driver = new();
            var result_tuple = await app_driver.PlayApp(app);
            return JsonConvert.SerializeObject(result_tuple);
        }

        [Route(HttpVerbs.Get, "/run/steps/{steps_name}")]
        public async Task<string> RunSteps(string steps_name) {
            BizDeckResult load_steps_result = config_helper.LoadStepsOrActions(steps_name);
            if (!load_steps_result.OK) {
                return JsonConvert.SerializeObject(load_steps_result);
            }
            PuppeteerDriver steps_driver = new();
            JObject steps = JObject.Parse(load_steps_result.Message);
            var result_tuple = await steps_driver.PlaySteps(steps_name, steps).ConfigureAwait(false);
            return JsonConvert.SerializeObject(result_tuple);
        }

        [Route(HttpVerbs.Get, "/run/actions/{actions_name}")]
        public async Task<string> RunActions(string actions_name) {
            BizDeckResult load_actions_result = config_helper.LoadStepsOrActions(actions_name);
            if (!load_actions_result.OK) {
                return JsonConvert.SerializeObject(load_actions_result);
            }
            ActionsDriver actions_driver = new();
            JObject actions = JObject.Parse(load_actions_result.Message);
            var result_tuple = await actions_driver.PlayActions(actions_name, actions).ConfigureAwait(false);
            return JsonConvert.SerializeObject(result_tuple);
        }

        [Route(HttpVerbs.Get, "/shutdown")]
        public string Shutdown() {
            Server.Instance.Shutdown();
            return JsonConvert.SerializeObject(BizDeckResult.Success);
        }

        [Route(HttpVerbs.Post, "/add_button")]
        public async Task<string> AddButton([BizDeckData] JObject button_defn) {
            string script_name = null;
            JToken script = null;
            string background = null;
            try {
                if (add_button_request_keys.TrueForAll(s => button_defn.ContainsKey(s))) {
                    script_name = (string)button_defn["script_name"];
                    script = (JToken)button_defn["script"];
                    background = (string)button_defn["background"];
                }
            }
            catch (Exception ex) {
                string error = $"AddButton: cannot extract parse script data from [{button_defn}], ex[{ex.Message}]";
                logger.Error(error);
                return JsonConvert.SerializeObject(new BizDeckResult(error));
            }
            // resume on any thread so we free this thread for more websock event handling
            BizDeckResult add_button_result = await config_helper.AddButton(script_name, script.ToString(), background);
            if (!add_button_result.OK) {
                logger.Error($"AddButton: add_button failed for name[{script_name}] in {button_defn}");
                throw HttpException.BadRequest($"JSON parse failure {add_button_result.Message}");
            }
            else {
                // We don't have a websock context here as this is an HTTP POST. So we
                // cannot do a Server.SendConfig() as it requires a websock context.
                // TODO: rewire SendConfig etc to use broadcast events when no
                // websock context is supplied.
                BizDeckResult rebuild_result = Server.Instance.RebuildButtonMaps();
                return JsonConvert.SerializeObject(rebuild_result);
            }
        }
    }
}