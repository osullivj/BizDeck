using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace BizDeck
{
    public class JsonUtils
    {
        private static readonly JsonSerializerSettings JsonSerializerSettings = new JsonSerializerSettings
        {
            DateTimeZoneHandling = DateTimeZoneHandling.Utc
        };

        public static void InitDefaultSettings()
        {
            JsonConvert.DefaultSettings = () => JsonSerializerSettings;
        }

        public static string SerializeToJson(object obj)
        {
            return JsonConvert.SerializeObject(obj);
        }

        public static T DeserializeFromJson<T>(string json)
        {
            if (json == null)
            {
                return default;
            }

            try
            {
                return JsonConvert.DeserializeObject<T>(json);
            }
            catch (JsonSerializationException e)
            {
                throw new Exception(json, e);
            }
        }
    }
}