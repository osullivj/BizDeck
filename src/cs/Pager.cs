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

        public async override Task RunAsync()
        {
            Run();
            await Task.Delay(0);
        }
    }
}
