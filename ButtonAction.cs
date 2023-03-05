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
        public virtual Task RunAsync() { return Task.CompletedTask; }
        // NB async is not part of the method signature
        // https://stackoverflow.com/questions/25015853/is-it-ok-to-have-virtual-async-method-on-base-class
    }
}
