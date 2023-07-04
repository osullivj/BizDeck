using System;
using System.Threading.Tasks;
using EmbedIO;
using EmbedIO.Routing;
using EmbedIO.WebApi;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BizDeck {
    // See JsonDataAttribute in Embedio; the JSON parsing requires a fixed
    // schema or object model as the marshalling target. We want to parse
    // schemaless JSON with NewtonSoft, so we create our own attribute
    // for use in BizDeckApiController on the /api/add_button method.
    // https://unosquare.github.io/embedio/wiki/Cookbook.html#custom-deserialization-of-request-data-pass-anything-as-a-controller-method-parameter

    [AttributeUsage(AttributeTargets.Parameter)]
    public class BizDeckDataAttribute : Attribute, IRequestDataAttribute<WebApiController> {
        private BizDeckLogger logger;

        public BizDeckDataAttribute() {
            logger = new(this);
        }

        public async Task<object?> GetRequestDataAsync(WebApiController controller, Type type, string parameterName) {
            string body;
            using (var reader = controller.HttpContext.OpenRequestText()) {
                body = await reader.ReadToEndAsync().ConfigureAwait(false);
            }
            try {
                return JObject.Parse(body);
            }
            catch (Exception ex) {
                logger.Error($"GetRequestDataAsync: JSON parse error {ex.Message}");
                logger.Error($"GetRequestDataAsync: erroring JSON[{body}]");
                throw HttpException.BadRequest($"JSON parse failure {ex.Message}");
            }
        }
    }
}