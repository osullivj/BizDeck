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
        // API add button request fields are the same as a ButtonDefinition
        // we require name and background; blink and mode can default
        private List<string> add_button_request_keys = new() { "name",  "background", "script"};

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
            try {
                JObject steps = JObject.Parse(load_steps_result.Message);
                var result_tuple = await PuppeteerDriver.Instance.PlaySteps(steps_name, steps).ConfigureAwait(true);
                if (!result_tuple.OK) {
                    throw HttpException.BadRequest(result_tuple.Message);
                }
                return JsonConvert.SerializeObject(result_tuple);
            }
            catch (Exception ex) {
                string error = $"/api/run/steps/{steps_name}: ex[{ex.Message}]";
                logger.Error(error);
                throw HttpException.BadRequest(error);
            }
        }

        [Route(HttpVerbs.Get, "/run/actions/{actions_name}")]
        public async Task<string> RunActions(string actions_name) {
            BizDeckResult load_actions_result = config_helper.LoadStepsOrActions(actions_name);
            if (!load_actions_result.OK) {
                return JsonConvert.SerializeObject(load_actions_result);
            }
            try {
                ActionsDriver actions_driver = new();
                JObject actions = JObject.Parse(load_actions_result.Message);
                var result_tuple = await actions_driver.PlayActions(actions_name, actions).ConfigureAwait(false);
                if (!result_tuple.OK) {
                    throw HttpException.BadRequest(result_tuple.Message);
                }
                return JsonConvert.SerializeObject(result_tuple);
            }
            catch (Exception ex) {
                string error = $"/api/run/actions/{actions_name}: ex[{ex.Message}]";
                logger.Error(error);
                throw HttpException.BadRequest(error);
            }
        }

        [Route(HttpVerbs.Get, "/shutdown")]
        public string Shutdown() {
            Server.Instance.Shutdown();
            return JsonConvert.SerializeObject(BizDeckResult.Success);
        }

        [Route(HttpVerbs.Post, "/add_button")]
        public async Task<string> AddButton([BizDeckData] JObject button_defn) {
            JToken script = null;
            ButtonDefinition bd = null;
            string background = null;
            try {
                // Extract the buttonDefinition fields from the object
                bd = button_defn.ToObject<ButtonDefinition>();
                // Fields not in ButtonDefinition
                script = (JToken)button_defn["script"];
                background = (string)button_defn["background"];
            }
            catch (Exception ex) {
                string error = $"/api/add_button: cannot extract button defn or script from [{button_defn}], ex[{ex.Message}]";
                logger.Error(error);
                throw HttpException.BadRequest(error);
            }
            // resume on any thread so we free this thread for more websock event handling
            BizDeckResult add_button_result = await config_helper.AddButton(bd.Name, script.ToString(), background, bd.Blink, bd.Mode);
            if (!add_button_result.OK) {
                logger.Error($"AddButton: add_button failed for name[{bd.Name}] in {button_defn}");
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