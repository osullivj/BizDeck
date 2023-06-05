using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EmbedIO;
using EmbedIO.Routing;
using EmbedIO.Utilities;
using EmbedIO.WebApi;
using Unosquare.Tubular;

namespace BizDeck {
    public class BizDeckApiController : WebApiController {
        private ConfigHelper config_helper;
        private BizDeckStatus status;

        public BizDeckApiController(ConfigHelper ch, BizDeckStatus stat) {
            config_helper = ch;
            status = stat;
        }

        // http://localhost:9271/api/status
        [Route(HttpVerbs.Get, "/status")]
        public Task<BizDeckStatus> Status() => Task.FromResult(status);

        // http://localhost:9271/api/config
        [Route(HttpVerbs.Get, "/config")]
        public Task<BizDeckConfig> Config() => Task.FromResult(config_helper.BizDeckConfig);

        [Route(HttpVerbs.Get, "/cache/{group?}/{key?}")]
        public Task<CacheEntry> GetPeople(string group, string key) {
            CacheEntry cache_entry = DataCache.Instance.GetCacheEntry(group, key);
            if (cache_entry == null) {
                throw HttpException.NotFound();
            }
            return Task.FromResult<CacheEntry>(cache_entry);
        }
    }
}