using HidSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BizDeck {
    // Manage connected Stream Deck devices.
    public class DeckManager {
        private BizDeckLogger logger;
        private ConfigHelper config_helper;
        private static readonly int SupportedVid = 4057;

        public DeckManager(ConfigHelper ch) {
            config_helper = ch;
            logger = new(this);
        }

        /// Return a list of connected Stream Deck devices supported by DeckSurf.
        public IEnumerable<ConnectedDeck> GetDeckList() {
            var connected_decks = new List<ConnectedDeck>();
            var device_list = DeviceList.Local.GetHidDevices();

            foreach (var device in device_list) {
                if (IsSupported(device.VendorID, device.ProductID)) {
                    switch ((DeviceModel)device.ProductID) {
                        case DeviceModel.MK_2:
                        case DeviceModel.XL:
                            connected_decks.Add(new ConnectedDeck(device, config_helper));
                            break;
                        case DeviceModel.MINI:
                        case DeviceModel.ORIGINAL:
                        case DeviceModel.ORIGINAL_V2:
                        default:
                            // TODO: support other Stream Deck device classes.
                            break;
                    }
                }
            }
            return connected_decks;
        }

        public ConnectedDeck SetupDeck() {
            try {
                var devices = GetDeckList();
                if (devices != null && devices.Any()) {
                    // assume just one StreamDeck connected and take first entry on device list
                    return devices.ElementAt(0);
                }
                else {
                    return null;
                }
            }
            catch (Exception ex) {
                logger.Error($"SetupDevice: {ex.ToString()}");
                return null;
            }
        }

        public bool IsSupported(int vid, int pid) {
            return (vid == SupportedVid && Enum.IsDefined(typeof(DeviceModel), (byte)pid));
        }
    }
}
