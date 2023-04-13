using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BizDeck
{
    /// <summary>
    /// Implementation for a Stream Deck XL connected device.
    /// </summary>
    public class StreamDeck : ConnectedDevice
    {
        public StreamDeck(int vid, int pid, string path, string name, DeviceModel model, ConfigHelper ch)
            : base(vid, pid, path, name, model, ch)
        {
        }
    }
}
