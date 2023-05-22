using System.Threading.Tasks;

namespace BizDeck
{
    public abstract class ButtonAction
    {
        public ButtonAction() { }
        public virtual void Run() { }
        public virtual Task<(bool,string)> RunAsync() {
            return Task.FromResult<(bool,string)>((true,null));
        }
        // NB async is not part of the method signature
        // https://stackoverflow.com/questions/25015853/is-it-ok-to-have-virtual-async-method-on-base-class
    }
}
