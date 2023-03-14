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
            this.ButtonMap = new List<ButtonMapping>();
        }

        [JsonPropertyName("usb_hid_device_index")]
        public int USBHIDDeviceIndex { get; set; }

        [JsonPropertyName("http_server_port")]
        public int HTTPServerPort { get; set; }

        [JsonPropertyName("edge_recorder_port")]
        public int EdgeRecorderPort { get; set; }

        [JsonPropertyName("edge_json_list_timeout")]
        public int EdgeJsonListTimeout{ get; set; }

        [JsonPropertyName("edge_user_data_dir")]
        public string EdgeUserDataDir { get; set; }

        [JsonPropertyName("edge_path")]
        public string EdgePath { get; set; }

        [JsonPropertyName("console")]
        public bool Console { get; set; }

        [JsonPropertyName("button_map")]
        public List<ButtonMapping> ButtonMap { get; set; }
    }
}
