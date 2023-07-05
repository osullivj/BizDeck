using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace BizDeck {
    [JsonConverter(typeof(StringEnumConverter))]
    public enum ButtonMode {
        Permanent,      // for ButtonAction==System buttons like Pager or BizDeckGUI
        Persistent,     // Regular Apps, Actions or Steps button
        KillOnClick,    // Button disappears on first click
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum ButtonImplType {
        System,         // Button has built in impl like Pager
        Actions,        // Button triggers BizDeck actions script
        Steps,          // Button triggers browser steps script
        Apps,           // an app launch button
    }

    public class ButtonDefinition {
        private static Dictionary<ButtonImplType, string> impl_type_map = 
                                    new Dictionary<ButtonImplType, string> {
            {ButtonImplType.System, "system" },
            {ButtonImplType.Actions, "actions" },
            {ButtonImplType.Steps, "steps"},
            {ButtonImplType.Apps, "apps"}
        };

        public ButtonDefinition() {
            // Set isn't supplied by config. Unless we set it true, it will default
            // to false, being a bool. We want the initial state to always be set.
            Set = true;
            // Also supply default values that may be omitted from /api/add_button
            Mode = ButtonMode.Persistent;
            Blink = false;
        }

        [JsonProperty("name")]
        public string Name { get; set; }

        // Indexes are assigned by ConfigHelper code,
        // and not set from JSON
        [JsonIgnore]
        public int ButtonIndex { get; set; }

        [JsonProperty("image")]
        public string ButtonImagePath { get; set; }

        [JsonProperty("action")]
        public ButtonImplType Action { get; set; }

        [JsonIgnore]
        public string ImplTypeAsString {get => impl_type_map[Action]; }

        // Set is used for blink state. Never comes from
        // JSON, and is never persisted
        [JsonIgnore]
        public bool Set { get; set; }

        [JsonProperty("blink")]
        public bool Blink { get; set; }

        [JsonProperty("mode")]
        public ButtonMode Mode { get; set; }

        public override string ToString() {
            return $"Button:name[{Name}], inx[{ButtonIndex}], img[{ButtonImagePath}], type[{ImplTypeAsString}], mode[{Mode}], blink[{Blink}]";
        }
    }
}
