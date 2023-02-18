using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BizDeck
{
    public abstract class ButtonAction
    {
        public ButtonAction() { }
        public virtual void Run() { }
    }
}
