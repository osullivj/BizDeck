using System;
using System.Threading.Tasks;
using EmbedIO.WebApi;
using Unosquare.Tubular;
using EmbedIO;

namespace BizDeck
{
    [AttributeUsage(AttributeTargets.Parameter)]
    public class JsonGridDataRequestAttribute : Attribute, IRequestDataAttribute<WebApiController, GridDataRequest>
    {
        public Task<GridDataRequest> GetRequestDataAsync(WebApiController controller, string parameterName)
            => controller.HttpContext.GetRequestDataAsync(RequestDeserializer.Json<GridDataRequest>);
    }
}