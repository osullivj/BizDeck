using System.Threading.Tasks;

namespace BizDeck
{
    public class Pager:ButtonAction
    {
        ConnectedDeck stream_deck = null;
        public Pager(ConnectedDeck sd) { stream_deck = sd; }
        public override void Run() {
            stream_deck.NextPage();
        }

        public async override Task<BizDeckResult> RunAsync()
        {
            Run();
            await Task.Delay(0);
            return BizDeckResult.Success;
        }
    }
}
