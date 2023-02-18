using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BizDeck
{
    /// <summary>
    /// Layout profile that represents the top level desktop windows
    /// </summary>
    public class BizDeckLayoutRules
    {
        public BizDeckLayoutRules()
        {
            this.IgnoreList = new List<DesktopWindowRule>();
            this.IncludeList = new List<DesktopWindowRule>();
        }

        [JsonPropertyName("ignore_list")]
        public List<DesktopWindowRule> IgnoreList { get; set; }

        [JsonPropertyName("include_list")]
        public List<DesktopWindowRule> IncludeList { get; set; }
    }
}
