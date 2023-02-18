using HidSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BizDeck
{
    /// <summary>
    /// Class used to manage connected Stream Deck devices.
    /// </summary>
    public class DeviceManager
    {
        private static readonly int SupportedVid = 4057;

        /// <summary>
        /// Return a list of connected Stream Deck devices supported by DeckSurf.
        /// </summary>
        /// <returns>Enumerable containing a list of supported devices.</returns>
        public static IEnumerable<ConnectedDevice> GetDeviceList()
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
                                connectedDevices.Add(new StreamDeck(device.VendorID, device.ProductID, device.DevicePath, device.GetFriendlyName(), (DeviceModel)device.ProductID));
                                break;
                            }

                        case DeviceModel.MINI:
                        case DeviceModel.ORIGINAL:
                        case DeviceModel.ORIGINAL_V2:
                        default:
                            {
                                // Haven't yet implemented support for other Stream Deck device classes.
                                break;
                            }
                    }
                }
            }

            return connectedDevices;
        }

        /// <summary>
        /// Gets a connected Stream Deck device based on a pre-defined configuration profiles.
        /// </summary>
        /// <param name="profile">An instance representing the pre-defined configuration profile.</param>
        /// <returns>If the call is successful, returns a Stream Deck device.</returns>
        public static ConnectedDevice SetupDevice(BizDeckConfig config)
        {
            try
            {
                var devices = GetDeviceList();
                if (devices != null && devices.Any())
                {
                    // assume just one StreamDeck connected and take first
                    // entry on device list
                    var targetDevice = devices.ElementAt(0);
                    targetDevice.SetupDeviceButtonMap(config.ButtonMap);
                    return targetDevice;
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

        /// <summary>
        /// Determines whether a given vendor ID (VID) and product ID (PID) are supported by the SDK. VID and PID should be representing a Stream Deck device.
        /// </summary>
        /// <param name="vid">Device VID.</param>
        /// <param name="pid">Device PID.</param>
        /// <returns>True if device is supported, false if not.</returns>
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
