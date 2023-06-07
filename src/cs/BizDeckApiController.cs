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

        public BizDeckApiController(ConfigHelper ch) {
            config_helper = ch;
        }

        // http://localhost:9271/api/status
        [Route(HttpVerbs.Get, "/status")]
        public Task<BizDeckStatus> Status() => Task.FromResult(BizDeckStatus.Instance);

        // http://localhost:9271/api/config
        [Route(HttpVerbs.Get, "/config")]
        public Task<BizDeckConfig> Config() => Task.FromResult(config_helper.BizDeckConfig);

        [Route(HttpVerbs.Get, "/cache/{group?}/{key?}")]
        public Task<CacheEntry> GetCacheEntry(string group, string cache_key) {
            CacheEntry cache_entry = DataCache.Instance.GetCacheEntry(group, cache_key);
            if (cache_entry == null) {
                throw HttpException.NotFound();
            }
            return Task.FromResult<CacheEntry>(cache_entry);
        }

        [Route(HttpVerbs.Get, "/run/app/{app?}")]
        public async Task<string> RunApp(string app) {
            AppDriver app_driver = new();
            var result_tuple = await app_driver.PlayApp(app);
            return JsonConvert.SerializeObject(result_tuple);
        }

        [Route(HttpVerbs.Get, "/run/steps/{steps_name}")]
        public async Task<string> RunSteps(string steps_name) {
            (bool ok, string json_or_err) = config_helper.LoadStepsOrActions(steps_name);
            if (!ok) {
                return JsonConvert.SerializeObject(json_or_err);
            }
            PuppeteerDriver steps_driver = new();
            JObject steps = JObject.Parse(json_or_err);
            var result_tuple = await steps_driver.PlaySteps(steps_name, steps).ConfigureAwait(false);
            return JsonConvert.SerializeObject(result_tuple);
        }

        [Route(HttpVerbs.Get, "/run/actions/{actions_name}")]
        public async Task<string> RunActions(string actions_name) {
            (bool ok, string json_or_err) = config_helper.LoadStepsOrActions(actions_name); 
            if (!ok) {
                return JsonConvert.SerializeObject(json_or_err);
            }
            ActionsDriver actions_driver = new();
            JObject actions = JObject.Parse(json_or_err);
            var result_tuple = await actions_driver.PlayActions(actions_name, actions).ConfigureAwait(false);
            return JsonConvert.SerializeObject(result_tuple);
        }
    }
}