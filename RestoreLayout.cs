using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Swan.Logging;

namespace BizDeck
{
    public class RestoreLayout:ButtonAction
    {
        private Layout layout;
        public RestoreLayout(Layout lay) { layout = lay; }
        public override void Run()
        {
            "Restoring layout...".Info();
            layout.Restore();
        }
    }
}
