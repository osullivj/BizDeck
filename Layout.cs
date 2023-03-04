using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        private bool ShouldIgnore(DesktopWindow dwin)
        {
            foreach (DesktopWindowRule dwr in layout_rules.IgnoreList)
            {
                // At the moment we only check the ClassName against
                // the ignore list. We'll probably need checks on Name
                // or other fields in future.
                if (dwin.ClassName == dwr.ClassName)
                    return true;
            }
            return false;
        }

        private bool ShouldInclude(DesktopWindow dwin, out DesktopWindowRule dwr_out)
        {
            dwr_out = null;
            foreach (DesktopWindowRule dwr in layout_rules.IncludeList)
            {
                // At the moment we only check the ClassName against
                // the ignore list. We'll probably need checks on Name
                // or other fields in future.
                if (dwin.ClassName == dwr.ClassName || dwin.Name == dwr.Name)
                {
                    dwr_out = dwr;
                    return true;
                }
            }
            return false;
        }

        public void Restore()
        {
            BizDeckLayout layout = config_helper.LoadLayout();
            DesktopWindowRule dwr = null;
            foreach (DesktopWindow dwin in layout.DesktopWindowList)
            {
                if (!ShouldIgnore(dwin))
                {
                    if (ShouldInclude(dwin, out dwr))
                    {
                        // TODO: add code here to launch the process using dwr.Exe, 
                        // then position the window using dwin.Top,Left,Bottom,Right
                        Process.Start(dwr.Exe);
                    }
                }

            }
        }
    }
}
