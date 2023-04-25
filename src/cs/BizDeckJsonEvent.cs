using System;
using System.Collections.Generic;
using System.Linq;
using Swan.Formatters;

namespace BizDeck
{
    public class BizDeckJsonEvent
    {
        public BizDeckJsonEvent(string type)
        {
            Type = type;
            Data = new Dictionary<string, string>();
        }

        [JsonProperty("type")]
        public string Type { get; private set; }

        [JsonProperty("data")]
        public object Data { get; set; }
    }


    public class JsDataRow
    {
        [JsonProperty("columns")]
        public List<string> Columns { get; set; }

        public static List<JsDataRow> GenerateLargeTable()
        {
            var random = new Random();
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var rows = new List<JsDataRow>();

            for (var i = 0; i < 1000; i++)
            {
                var row = new JsDataRow();
                var cols = new List<string>();
                for (var j = 0; j < 10; j++)
                {
                    cols.Add(new string(
                        Enumerable.Repeat(chars, 20)
                            .Select(s => s[random.Next(s.Length)]).ToArray()));
                }

                row.Columns = cols;
                rows.Add(row);
            }

            return rows;
        }
    }
}