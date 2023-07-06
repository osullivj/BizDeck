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
        // delegate
        // public delegate void ReceivedButtonPressHandler(object source, ButtonPressEventArgs e);
        // static constants
        private const int ButtonPressHeaderOffset = 4;
        private static readonly int ImageReportLength = 1024;
        private static readonly int ImageReportHeaderLength = 8;
        private static readonly int ImageReportPayloadLength = ImageReportLength - ImageReportHeaderLength;

        // Buffer for stream coming from deck
        private byte[] key_press_buffer = new byte[1024];
        private ConfigHelper config_helper;
        private BizDeckLogger logger;
        private HidDevice usb_device;
        private IconCache icon_cache;
        private Server main_server_object;

        public ConnectedDeck(HidDevice device, ConfigHelper ch, Server mso) {
            // Just connecting obj refs in ctor
            usb_device = device;
            config_helper = ch;
            main_server_object = mso;
            logger = new(this);
        }

        public void InitDeck() {
            icon_cache = IconCache.Instance;
            SetBrightness((byte)config_helper.BizDeckConfig.DeckBrightnessPercentage);
            SetupDeviceButtons();
        }

        // Property accessors that just read from underlying HidDevice
        public int VendorId { get => usb_device.VendorID; }
        public int ProductId { get => usb_device.ProductID;  }
        public string Path { get => usb_device.DevicePath; }
        public string Name { get => usb_device.GetFriendlyName(); }
        public DeviceModel Model { get => (DeviceModel)usb_device.ProductID; }
        private HidDevice UnderlyingDevice { get => usb_device; }
        public int ButtonCount { get => DeviceConstants.constants[Model].ButtonCount; }
        public int ButtonSize { get => DeviceConstants.constants[Model].ButtonSize;  }

        // Device working state accessors
        public int LastButton { get; set; }
        public int Brightness { get; set; }
        private HidStream UnderlyingInputStream { get; set; }

        // private working state
        private int current_page = 0;

        public async Task ReadAsync() {
            UnderlyingInputStream = UnderlyingDevice.Open();
            UnderlyingInputStream.ReadTimeout = Timeout.Infinite;
            Array.Clear(key_press_buffer, 0, key_press_buffer.Length);
            int bytes_read = 0;
            string button_name = null;
            ButtonMode button_mode;
            logger.Info("ReadAsync awaiting stream...");
            bytes_read = await UnderlyingInputStream.ReadAsync(this.key_press_buffer, 0, this.key_press_buffer.Length).ConfigureAwait(false);
            while (bytes_read > 0) {
                var button_data = new ArraySegment<byte>(this.key_press_buffer, ButtonPressHeaderOffset, ButtonCount).ToArray();
                var pressed_button = Array.IndexOf(button_data, (byte)1);
                var button_kind = ButtonEventKind.DOWN;
                // We always get DOWN and an index, and never UP
                // However, when it should be an UP we get index -1
                if (pressed_button == -1) {
                    button_kind = ButtonEventKind.UP;
                    logger.Info($"ReadAsync: pressed[{LastButton}], kind[{button_kind}]");
                }
                else {
                    logger.Info($"ReadAsync: pressed[{pressed_button}], kind[{button_kind}]");
                }
                if (pressed_button == -1) { 
                    // Hold the ButtonListLock while we get the button name
                    lock (config_helper.ButtonListLock) {
                        var button_entry = config_helper.BizDeckConfig.ButtonList.FirstOrDefault(x => x.ButtonIndex == LastButton);
                        if (button_entry != null) {
                            button_name = button_entry.Name;
                            button_mode = button_entry.Mode;
                        }
                        else {
                            button_name = null;
                            button_mode = ButtonMode.Permanent;
                        }
                    }
                    if (button_name != null) {
                        logger.Info($"ReadAsync: entry[{button_name}]");
                        // ConfigureAwait(false) to signal that we can resume on any thread, leaving this
                        // thread free to handle the next HID USB event from the StreamDeck
                        var button_action = main_server_object.GetButtonAction(button_name);
                        if (button_action != null) {
                            logger.Info($"ReadAsync: invoking {button_name}.RunAsync");
                            BizDeckResult action_result = await button_action.RunAsync().ConfigureAwait(false);
                            logger.Info($"ReadAsync: {action_result} from {button_name}.RunAsync");
                            if (!action_result.OK) {
                                await main_server_object.SendNotification("Button action failed", action_result.Message);
                            }
                            else {
                                if (button_mode == ButtonMode.KillOnClick) {
                                    logger.Info($"ReadAsync: {button_name} is KillOnClick");
                                    // No need for locking here as the two methods
                                    // we call lock as necessary
                                    await config_helper.DeleteButton(button_name);
                                    logger.Info($"ReadAsync: {button_name} is deleted");
                                    main_server_object.RebuildButtonMaps();
                                    logger.Info($"ReadAsync: RebuildButtonMaps done");
                                }
                            }
                        }
                    }
                }
                else {  // pressed_button != -1
                    LastButton = pressed_button;
                }
                bytes_read = await UnderlyingInputStream.ReadAsync(this.key_press_buffer, 0, this.key_press_buffer.Length).ConfigureAwait(false);
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
            logger.Debug($"ClearKey: index[{index}]");
            this.SetKey(index, DeviceConstants.XLDefaultBlackButton);
        }

        public void SetBrightness(int brightness) { 
            byte percentage = (byte)brightness;
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
            byte[] buffer = null;
            int buttons_sent = 1;
            // hold the ButtonListLock while we iterator over ButtonList
            lock (config_helper.ButtonListLock) {
                var button_list = config_helper.BizDeckConfig.ButtonList;
                logger.Debug($"SetupDeviceButtons: button_list.Count[{button_list.Count}]");
                // Button 0 is the pager button, so we always send that whatever page is current
                var pager_button = button_list[0];
                buffer = icon_cache.GetIconBufferJPEG(pager_button.ButtonImagePath);
                SetKey(pager_button.ButtonIndex, buffer);
                // Now for buttons 1...ButtonCount
                int start_index = (current_page * ButtonCount) + 1;
                int end_index = start_index + ButtonCount - 1;
                foreach (var button in button_list) {
                    if (button.ButtonIndex >= start_index && button.ButtonIndex <= end_index) {
                        if (button.Set) {
                            buffer = icon_cache.GetIconBufferJPEG(button.ButtonImagePath);
                            this.SetKey(buttons_sent, buffer);
                        }
                        else {
                            this.ClearKey(buttons_sent);
                        }
                        buttons_sent++;
                    }
                }
            }
            // ButtonListLock is not released - we don't need it for the clear
            // key logic below.
            // Clear keys not set by our config. NB is we stop BizDeck and restart with
            // less configged buttons, those old buttons still show on the deck if we
            // don't clear them.
            while (buttons_sent < this.ButtonCount) {
                this.ClearKey(buttons_sent++);
            }
        }

        public void BlinkDeviceButtons() {
            bool blink_required = false;
            lock (config_helper.ButtonListLock) {
                foreach (var bd in config_helper.BizDeckConfig.ButtonList) {
                    if (bd.Blink) {
                        blink_required = true;
                        if (bd.Set) {
                            bd.Set = false;
                        }
                        else {
                            bd.Set = true;
                        }
                    }
                }
            }
            if (blink_required) {
                SetupDeviceButtons();
            }
        }

        public void NextPage() {
            // TODO: this just changes the top left page button. We also need to send
            // the Nth tranche of buttons to match the page content. The caching and 
            // buffering we need will make this neater to implement.
            current_page = (current_page + 1) % 4;
            lock (config_helper.ButtonListLock) {
                var button_list0 = config_helper.BizDeckConfig.ButtonList[0];
                button_list0.ButtonImagePath = $"icons\\page{current_page + 1}.png";
                logger.Info($"NextPage: path[{button_list0.ButtonImagePath}]");
            }
            SetupDeviceButtons();
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
