using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BizDeck
{
    /// <summary>
    /// Layout profile that represents the top level desktop windows
    /// </summary>
    public class BizDeckLayout
    {
        public BizDeckLayout()
        {
        }

        public BizDeckLayout(List<DesktopWindow> new_layout)
        {
            this.DesktopWindowList = new_layout;
        }

        [JsonPropertyName("desktop_window_list")]
        public List<DesktopWindow> DesktopWindowList { get; set; }
    }
}
