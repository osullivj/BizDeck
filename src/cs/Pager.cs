using System.Threading.Tasks;

namespace BizDeck
{
    public class Pager:ButtonAction
    {
        ConnectedDevice stream_deck = null;
        public Pager(ConnectedDevice sd) { stream_deck = sd; }
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
