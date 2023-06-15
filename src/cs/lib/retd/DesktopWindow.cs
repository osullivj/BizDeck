using System;
using System.Text.Json.Serialization;
using System.Windows.Automation;

namespace BizDeck
{
    // JSON serialization class for reading and writing layout.json.
    // Note that the property names match those in System.Windows.Automation.AutomationElement
    public class DesktopWindow
    {
        public DesktopWindow(AutomationElement element)
        {
            Name = element.Current.Name;
            ClassName = element.Current.ClassName;
            AutomationId = element.Current.AutomationId;
            FrameworkId = element.Current.FrameworkId;
            Top = element.Current.BoundingRectangle.Top;
            Left = element.Current.BoundingRectangle.Left;
            Bottom = element.Current.BoundingRectangle.Bottom;
            Right = element.Current.BoundingRectangle.Right;
        }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("class_name")]
        public string ClassName { get; set; }

        [JsonPropertyName("auto_id")]
        public string AutomationId { get; set; }

        [JsonPropertyName("fwk_id")]
        public string FrameworkId { get; set; }

        [JsonPropertyName("top")]
        public double Top { get; set; }

        [JsonPropertyName("left")]
        public double Left { get; set; }

        [JsonPropertyName("bottom")]
        public double Bottom { get; set; }

        [JsonPropertyName("right")]
        public double Right { get; set; }
    }
}

