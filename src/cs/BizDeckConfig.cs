using System.Collections.Generic;
using System.Text.Json.Serialization;

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
        }

        [JsonPropertyName("usb_hid_device_index")]
        public int USBHIDDeviceIndex { get; set; }

        [JsonPropertyName("http_server_port")]
        public int HTTPServerPort { get; set; }

        [JsonPropertyName("browser_recorder_port")]
        public int BrowserRecorderPort { get; set; }

        [JsonPropertyName("browser_json_list_timeout")]
        public int BrowserJsonListTimeout{ get; set; }

        [JsonPropertyName("browser_websock_timeout")]
        public int BrowserWebsockTimeout { get; set; }

        [JsonPropertyName("browser_user_data_dir")]
        public string BrowserUserDataDir { get; set; }

        [JsonPropertyName("browser_path")]
        public string BrowserPath { get; set; }

        [JsonPropertyName("selector_index")]
        public int SelectorIndex { get; set; }

        [JsonPropertyName("console")]
        public bool Console { get; set; }

        [JsonPropertyName("devtools")]
        public bool DevTools { get; set; }

        [JsonPropertyName("headless")]
        public bool Headless { get; set; }

        [JsonPropertyName("icon_font_family")]
        public string IconFontFamily { get; set; }

        [JsonPropertyName("icon_font_size")]
        public int IconFontSize{ get; set; }

        [JsonPropertyName("button_list")]
        public List<ButtonDefinition> ButtonList { get; set; }

        [JsonPropertyName("background_icons")]
        public List<string> BackgroundIcons { get; set; }

        [JsonPropertyName("background_default")]
        public string BackgroundDefault { get; set; }
    }
}
