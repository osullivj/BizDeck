using System;

namespace BizDeck
{
    /// <summary>
    /// Event arguments that are passed back to the developer when a Stream Deck button is pressed.
    /// </summary>
    public class ButtonPressEventArgs : EventArgs
    {
        public ButtonPressEventArgs(int id, ButtonEventKind kind)
        {
            this.Id = id;
            this.Kind = kind;
        }

        public int Id { get; }

        public ButtonEventKind Kind { get; }
    }
}
