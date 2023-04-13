using HidSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BizDeck
{
    // Manage connected Stream Deck devices.
    public class DeviceManager
    {
        private static readonly int SupportedVid = 4057;
        /// Return a list of connected Stream Deck devices supported by DeckSurf.
        public static IEnumerable<ConnectedDevice> GetDeviceList(ConfigHelper ch)
        {
            var connectedDevices = new List<ConnectedDevice>();
            var deviceList = DeviceList.Local.GetHidDevices();

            foreach (var device in deviceList)
            {
                bool supported = IsSupported(device.VendorID, device.ProductID);
                if (supported)
                {
                    switch ((DeviceModel)device.ProductID)
                    {
                        case DeviceModel.MK_2:
                        case DeviceModel.XL:
                            {
                                connectedDevices.Add(new StreamDeck(device.VendorID, device.ProductID, device.DevicePath, device.GetFriendlyName(), (DeviceModel)device.ProductID, ch));
                                break;
                            }

                        case DeviceModel.MINI:
                        case DeviceModel.ORIGINAL:
                        case DeviceModel.ORIGINAL_V2:
                        default:
                            {
                                // TODO: support other Stream Deck device classes.
                                break;
                            }
                    }
                }
            }
            return connectedDevices;
        }

        public static ConnectedDevice SetupDevice(ConfigHelper ch)
        {
            try
            {
                var devices = GetDeviceList(ch);
                if (devices != null && devices.Any())
                {
                    // assume just one StreamDeck connected and take first
                    // entry on device list
                    var connected_device = devices.ElementAt(0);
                    connected_device.ButtonList = ch.BizDeckConfig.ButtonMap;
                    return connected_device;
                }
                else
                {
                    return null;
                }
            }
            catch (Exception e)
            {
                return null;
            }
        }

        public static bool IsSupported(int vid, int pid)
        {
            if (vid == SupportedVid && Enum.IsDefined(typeof(DeviceModel), (byte)pid))
            {
                return true;
            }

            return false;
        }
    }
}
