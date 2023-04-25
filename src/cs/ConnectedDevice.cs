using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HidSharp;
using Swan.Logging;

namespace BizDeck
{
    public abstract class ConnectedDevice
    {
        private const int ButtonPressHeaderOffset = 4;
        private static readonly int ImageReportLength = 1024;
        private static readonly int ImageReportHeaderLength = 8;
        private static readonly int ImageReportPayloadLength = ImageReportLength - ImageReportHeaderLength;
        private byte[] keyPressBuffer = new byte[1024];
        private ConfigHelper config_helper;

        public ConnectedDevice(int vid, int pid, string path, string name, DeviceModel model,
            ConfigHelper ch)
        {
            this.VId = vid;
            this.PId = pid;
            this.Path = path;
            this.Name = name;
            this.Model = model;
            this.UnderlyingDevice = DeviceList.Local.GetHidDeviceOrNull(this.VId, this.PId);
            this.ButtonCount = DeviceConstants.constants[model].ButtonCount;
            this.ButtonSize = DeviceConstants.constants[model].ButtonSize;
            this.config_helper = ch;
        }

        public delegate void ReceivedButtonPressHandler(object source, ButtonPressEventArgs e);

        public int VId { get; private set; }
        public int PId { get; private set; }
        public string Path { get; private set; }
        public string Name { get; private set; }
        public DeviceModel Model { get; private set; }
        public int ButtonCount { get; }
        public int ButtonSize { get; }
        public int LastButton { get; set; }
        private HidDevice UnderlyingDevice { get; }
        private HidStream UnderlyingInputStream { get; set; }
        private List<ButtonMapping> button_list;
        private int current_page = 0;
        private int current_desktop = 0;

        public List<ButtonMapping> ButtonList {
            get => button_list;
            set {
                button_list = value;
                SetupDeviceButtons();
            }
        }
        public Dictionary<string, ButtonAction> ButtonMap { get; set; }

        public async Task ReadAsync()
        {
            UnderlyingInputStream = UnderlyingDevice.Open();
            UnderlyingInputStream.ReadTimeout = Timeout.Infinite;
            Array.Clear(keyPressBuffer, 0, keyPressBuffer.Length);
            int bytes_read = 0;
            $"ConnectedDevice.ReadAsync awaiting stream...".Info();
            while ((bytes_read = await UnderlyingInputStream.ReadAsync(this.keyPressBuffer, 0, this.keyPressBuffer.Length).ConfigureAwait(false)) > 0)
            {

                var button_data = new ArraySegment<byte>(this.keyPressBuffer, ButtonPressHeaderOffset, ButtonCount).ToArray();
                var pressed_button = Array.IndexOf(button_data, (byte)1);
                var button_kind = ButtonEventKind.DOWN;
                $"ConnectedDevice.ReadAsync pressed:{pressed_button}, kind:{button_kind}".Info();
                if (pressed_button == -1)
                {
                    button_kind = ButtonEventKind.UP;
                    pressed_button = LastButton;
                    var button_entry = ButtonList.FirstOrDefault(x => x.ButtonIndex == pressed_button);
                    $"ConnectedDevice.ReadAsync entry:{button_entry.Name}".Info();
                    if (button_entry != null)
                    {
                        // ConfigureAwait(false) to signal that we can resume on any thread
                        await ButtonMap[button_entry.Name].RunAsync().ConfigureAwait(false);
                    }
                }
                else
                {
                    LastButton = pressed_button;
                }
            }
        }

        public HidStream Open()
        {
            return this.UnderlyingDevice.Open();
        }


        public void ClearPanel()
        {
            for (int i = 0; i < this.ButtonCount; i++)
            {
                // TODO: Need to replace this with device-specific logic
                // since not every device is 96x96.
                this.SetKey(i, DeviceConstants.XLDefaultBlackButton);
            }
        }

        public void SetBrightness(byte percentage)
        {
            if (percentage > 100)
            {
                percentage = 100;
            }

            var brightnessRequest = new byte[]
            {
                0x03, 0x08, percentage, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            };

            using var stream = this.Open();
            stream.SetFeature(brightnessRequest);
        }

        private void SetupDeviceButtons( )
        {
            foreach (var button in button_list)
            {
                if (button.ButtonIndex <= this.ButtonCount - 1)
                {
                    string button_path = config_helper.GetFullIconPath(button.ButtonImagePath);
                    if (File.Exists(button_path))
                    {
                        byte[] imageBuffer = File.ReadAllBytes(button_path);

                        // TODO: Need to make sure that I am using device-agnostic button sizes.
                        imageBuffer = ImageHelpers.ResizeImage(imageBuffer, ButtonSize, ButtonSize);
                        this.SetKey(button.ButtonIndex, imageBuffer);
                    }
                }
            }
        }

        public void NextPage()
        {
            current_page = (current_page + 1) % 4;
            button_list[0].ButtonImagePath = $"icons\\page{current_page + 1}.png";
            $"NextPage: {button_list[0].ButtonImagePath}".Info();
            SetupDeviceButtons();
        }

        public int NextDesktop()
        {
            current_desktop = (current_desktop + 1) % 4;
            button_list[1].ButtonImagePath = $"icons\\desk{current_desktop + 1}.png";
            $"NextDesktop: {button_list[1].ButtonImagePath}".Info();
            SetupDeviceButtons();
            return current_desktop;
        }


        /// <summary>
        /// Sets the content of a key on a Stream Deck device.
        /// </summary>
        /// <param name="keyId">Numberic ID of the key that needs to be set.</param>
        /// <param name="image">Binary content (JPEG) of the image that needs to be set on the key. The image will be resized to match the expectations of the connected device.</param>
        /// <returns>True if succesful, false if not.</returns>
        public bool SetKey(int keyId, byte[] image)
        {
            var content = image ?? DeviceConstants.XLDefaultBlackButton;

            var iteration = 0;
            var remainingBytes = content.Length;

            using (var stream = this.Open())
            {
                while (remainingBytes > 0)
                {
                    var sliceLength = Math.Min(remainingBytes, ImageReportPayloadLength);
                    var bytesSent = iteration * ImageReportPayloadLength;

                    byte finalizer = sliceLength == remainingBytes ? (byte)1 : (byte)0;

                    // These components are nothing else but UInt16 low-endian
                    // representations of the length of the image payload, and iteration.
                    var bitmaskedLength = (byte)(sliceLength & 0xFF);
                    var shiftedLength = (byte)(sliceLength >> ImageReportHeaderLength);
                    var bitmaskedIteration = (byte)(iteration & 0xFF);
                    var shiftedIteration = (byte)(iteration >> ImageReportHeaderLength);

                    // TODO: This is different for different device classes, so I will need
                    // to make sure that I adjust the header format.
                    byte[] header = new byte[] { 0x02, 0x07, (byte)keyId, finalizer, bitmaskedLength, shiftedLength, bitmaskedIteration, shiftedIteration };
                    var payload = header.Concat(new ArraySegment<byte>(content, bytesSent, sliceLength)).ToArray();
                    var padding = new byte[ImageReportLength - payload.Length];

                    var finalPayload = payload.Concat(padding).ToArray();

                    stream.Write(finalPayload);

                    remainingBytes -= sliceLength;
                    iteration++;
                }
            }

            return true;
        }
    }
}
