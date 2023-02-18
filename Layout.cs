using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Automation;
using Swan.Logging;

namespace BizDeck
{
    public class Layout
    {
        ConfigHelper config_helper;
        BizDeckLayoutRules layout_rules;
        public Layout(ConfigHelper ch) {
            config_helper = ch;
            layout_rules = ch.LoadLayoutRules();
        }

        public void Snap()
        {
            var desktop_window_list = new List<DesktopWindow>();
            // https://stackoverflow.com/questions/10191265/finding-all-windows-on-desktop-using-uiautomation-net
            // https://stackoverflow.com/questions/2397578/how-to-get-the-executable-name-of-a-window
            // https://stackoverflow.com/questions/69449788/get-path-of-a-known-hwnd
            AutomationElement rootElement = AutomationElement.RootElement;
            var winCollection = rootElement.FindAll(TreeScope.Children, Condition.TrueCondition);

            foreach (AutomationElement element in winCollection)
            {
                if (!element.Current.IsOffscreen)
                {
                    desktop_window_list.Add(new DesktopWindow(element));
                }
            }
            config_helper.SaveLayout(desktop_window_list);
        }
    }
}
