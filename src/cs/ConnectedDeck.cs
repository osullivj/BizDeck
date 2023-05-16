using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HidSharp;

// TODO: caching. SetCurrently SetupDeviceButtons loads the icons from file
// and does the image resize every time. We should cache the correctly sized
// images in local buffers to accelerate this process.

namespace BizDeck {
    public class ConnectedDeck {
        private const int ButtonPressHeaderOffset = 4;
        private static readonly int ImageReportLength = 1024;
        private static readonly int ImageReportHeaderLength = 8;
        private static readonly int ImageReportPayloadLength = ImageReportLength - ImageReportHeaderLength;
        private byte[] keyPressBuffer = new byte[1024];
        private ConfigHelper config_helper;
        private BizDeckLogger logger;

        public ConnectedDeck(int vid, int pid, string path, string name, DeviceModel model, ConfigHelper ch) {
            this.VId = vid;
            this.PId = pid;
            this.Path = path;
            this.Name = name;
            this.Model = model;
            this.UnderlyingDevice = DeviceList.Local.GetHidDeviceOrNull(this.VId, this.PId);
            this.ButtonCount = DeviceConstants.constants[model].ButtonCount;
            this.ButtonSize = DeviceConstants.constants[model].ButtonSize;
            this.config_helper = ch;
            logger = new(this);
            ch.DeviceModel = model;
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
        private List<ButtonDefinition> button_list;
        private int current_page = 0;
        private int current_desktop = 0;

        public List<ButtonDefinition> ButtonDefnList {
            get => button_list;
            set {
                button_list = value;
                SetupDeviceButtons();
            }
        }
        public Dictionary<string, ButtonAction> ButtonActionMap { get; set; }

        public async Task ReadAsync() {
            UnderlyingInputStream = UnderlyingDevice.Open();
            UnderlyingInputStream.ReadTimeout = Timeout.Infinite;
            Array.Clear(keyPressBuffer, 0, keyPressBuffer.Length);
            int bytes_read = 0;
            logger.Info("ReadAsync awaiting stream...");
            bytes_read = await UnderlyingInputStream.ReadAsync(this.keyPressBuffer, 0, this.keyPressBuffer.Length).ConfigureAwait(false);
            while (bytes_read > 0) {
                var button_data = new ArraySegment<byte>(this.keyPressBuffer, ButtonPressHeaderOffset, ButtonCount).ToArray();
                var pressed_button = Array.IndexOf(button_data, (byte)1);
                var button_kind = ButtonEventKind.DOWN;
                logger.Info($"ReadAsync: pressed[{pressed_button}], kind[{button_kind}]");
                if (pressed_button == -1) {
                    button_kind = ButtonEventKind.UP;
                    pressed_button = LastButton;
                    var button_entry = ButtonDefnList.FirstOrDefault(x => x.ButtonIndex == pressed_button);
                    if (button_entry != null) {
                        logger.Info($"ReadAsync: entry[{button_entry.Name}]");
                        // ConfigureAwait(false) to signal that we can resume on any thread
                        await ButtonActionMap[button_entry.Name].RunAsync().ConfigureAwait(false);
                    }
                }
                else {
                    LastButton = pressed_button;
                }
                bytes_read = await UnderlyingInputStream.ReadAsync(this.keyPressBuffer, 0, this.keyPressBuffer.Length).ConfigureAwait(false);
            }
        }

        public HidStream Open() {
            return this.UnderlyingDevice.Open();
        }

        public void ClearPanel() {
            // TODO: Need to replace this with device-specific logic
            // since not every device is 96x96.
            for (int i = 0; i < this.ButtonCount; i++) {
                ClearKey(i);
            }
        }

        public void ClearKey(int index) {
            logger.Info($"ClearKey: index[{index}]");
            this.SetKey(index, DeviceConstants.XLDefaultBlackButton);
        }

        public void SetBrightness(byte percentage) {
            if (percentage > 100) {
                percentage = 100;
            }

            var brightness_request = new byte[] {
                0x03, 0x08, percentage, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            };

            using var stream = this.Open();
            stream.SetFeature(brightness_request);
        }

        public void SetupDeviceButtons( ) {
            logger.Info($"SetupDeviceButtons: ButtonDefnList.Count[{ButtonDefnList.Count}]");
            int last_index = 0;
            foreach (var button in button_list) {
                if (button.ButtonIndex <= this.ButtonCount - 1) {
                    byte[] buffer = config_helper.IconCache.GetIconBufferJPEG(button.ButtonImagePath);
                    this.SetKey(button.ButtonIndex, buffer);
                    last_index = button.ButtonIndex;
                }
            }
            // On our first visit here the ButtonActionMap hasn't been created yet.
            // See the init order in the Server ctor.
            if (ButtonActionMap != null) {
                logger.Info($"SetupDeviceButtons: ButtonActionMap.Count[{ButtonActionMap.Count}]");
                int keys_to_clear = ButtonActionMap.Count - ButtonDefnList.Count;
                // If we're invoked by the ButtonDefnList property, it will be because a button
                // has been added or deleted. If deleted, then we'll need to blank the deleted keys.
                while (keys_to_clear > 0) {
                    ClearKey(ButtonDefnList.Count + keys_to_clear - 1);
                    --keys_to_clear;
                }
            }
        }

        public void NextPage() {
            // TODO: this just changes the top left page button. We also need to send
            // the Nth tranche of buttons to match the page content. The caching and 
            // buffering we need will make this neater to implement.
            current_page = (current_page + 1) % 4;
            button_list[0].ButtonImagePath = $"icons\\page{current_page + 1}.png";
            logger.Info($"NextPage: path[{button_list[0].ButtonImagePath}]");
            SetupDeviceButtons();
        }

        public int NextDesktop() {
            // Currently deprecated as Windows multi desktop support is only
            // exposed in the Win32 API, and not in .Net.
            current_desktop = (current_desktop + 1) % 4;
            button_list[1].ButtonImagePath = $"icons\\desk{current_desktop + 1}.png";
            logger.Info($"NextDesktop: path[{button_list[1].ButtonImagePath}]");
            SetupDeviceButtons();
            return current_desktop;
        }

        // Sets the content of a key on a Stream Deck device.
        public bool SetKey(int keyId, byte[] image) {
            var content = image ?? DeviceConstants.XLDefaultBlackButton;
            var iteration = 0;
            var remainingBytes = content.Length;

            using (var stream = this.Open()) {
                while (remainingBytes > 0) {
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
