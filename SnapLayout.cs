using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Swan.Logging;

namespace BizDeck
{
    public class SnapLayout:ButtonAction
    {
        private Layout layout;
        public SnapLayout(Layout lay) { layout = lay; }
        public override void Run()
        {
            "Snapping layout...".Info();
            layout.Snap();
        }
    }
}
