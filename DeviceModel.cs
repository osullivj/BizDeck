namespace BizDeck
{
    /// <summary>
    /// Enum represening Stream Deck device models and their respective USB product IDs (PIDs).
    /// </summary>
    public enum DeviceModel : byte
    {
        /// <summary>
        /// The original model of the Stream Deck device.
        /// </summary>
        ORIGINAL = 0x0060,

        /// <summary>
        /// The <see href="https://www.elgato.com/en/stream-deck">updated original model</see> of the Stream Deck device.
        /// </summary>
        ORIGINAL_V2 = 0x006d,

        /// <summary>
        /// The <see href="https://www.elgato.com/en/stream-deck-mini">Mini model</see> of the Stream Deck device.
        /// </summary>
        MINI = 0x0063,

        /// <summary>
        /// The <see href="https://www.elgato.com/en/stream-deck-xl">XL model</see> of the Stream Deck device.
        /// </summary>
        XL = 0x006c,

        /// <summary>
        /// The <see href="https://www.elgato.com/en/stream-deck-xl">XL model</see> of the Stream Deck device.
        /// </summary>
        MK_2 = 0x0080,
    }
}
