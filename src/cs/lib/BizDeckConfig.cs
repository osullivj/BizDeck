using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BizDeck
{
    /// <summary>
    /// Configuration profile that represents the mapping of buttons on a Stream Deck device
    /// to icons, as well as HTTP port, USD device index, and true/false for a DOS box console.
    /// </summary>
    public class BizDeckConfig
    {
        public BizDeckConfig()
        {
            this.ButtonList = new List<ButtonDefinition>();
            this.BrowserMap = new Dictionary<string, BrowserLaunch>();
        }

        [JsonProperty("usb_hid_device_index")]
        public int USBHIDDeviceIndex { get; set; }

        [JsonProperty("http_host_name")]
        public string HTTPHostName { get; set; }

        [JsonProperty("http_server_port")]
        public int HTTPServerPort { get; set; }

        [JsonProperty("browser_recorder_port")]
        public int BrowserRecorderPort { get; set; }

        [JsonProperty("browser_json_list_timeout")]
        public int BrowserJsonListTimeout{ get; set; }

        [JsonProperty("browser_websock_timeout")]
        public int BrowserWebsockTimeout { get; set; }

        [JsonProperty("reuse_browser_instances")]
        public bool ReuseBrowserInstances { get; set; }

        [JsonProperty("default_browser")]
        public string DefaultBrowser { get; set; }

        [JsonProperty("blink_interval")]
        public int BlinkInterval { get; set; }

        [JsonProperty("console")]
        public bool Console { get; set; }

        [JsonProperty("debug_logging")]
        public bool DebugLogging { get; set; }

        [JsonProperty("http_get_timeout")]
        public int HttpGetTimeout { get; set; }

        [JsonProperty("icon_font_family")]
        public string IconFontFamily { get; set; }

        [JsonProperty("icon_font_size")]
        public int IconFontSize{ get; set; }

        [JsonProperty("deck_brightness_percentage")]
        public int DeckBrightnessPercentage { get; set; }

        [JsonProperty("secrets_path")]
        public string SecretsPath { get; set; }

        [JsonProperty("browser_map")]
        public Dictionary<string, BrowserLaunch> BrowserMap { get; set; }

        [JsonProperty("button_list")]
        public List<ButtonDefinition> ButtonList { get; set; }

        [JsonProperty("background_icons")]
        public List<string> BackgroundIcons { get; set; }

        [JsonProperty("background_default")]
        public string BackgroundDefault { get; set; }
    }
}
