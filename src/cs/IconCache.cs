using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Collections.Generic;

namespace BizDeck {

	public class IconCache {
		private ConfigHelper config_helper;
		private FontFamily font_family;
		private Font font;
		private Dictionary<string, byte[]> icon_buffer_map = new();
		private readonly object icon_buffer_map_lock = new object();
		private BizDeckLogger logger;

		public IconCache(ConfigHelper ch) {
			logger = new(this);
			config_helper = ch;
			font_family = new FontFamily(ch.BizDeckConfig.IconFontFamily);
			font = new Font(font_family, ch.BizDeckConfig.IconFontSize, FontStyle.Regular, GraphicsUnit.Pixel);
		}

		public byte[] GetIconBufferJPEG(string rel_path) {
			// Return cached buffer if we have it
			if (icon_buffer_map.ContainsKey(rel_path)) {
				return icon_buffer_map[rel_path];
            }
			// Not cached so let's try and load it
			byte[] jpeg_buffer = LoadIconAsJPEG(rel_path);
			if (jpeg_buffer != null) {
				lock (icon_buffer_map_lock) {
					icon_buffer_map[rel_path] = jpeg_buffer;
				}
				return jpeg_buffer;
            }
			return null;
        }

		protected byte[] LoadIconAsJPEG(string relative_path) {
			string full_path = config_helper.GetFullIconPath(relative_path);
			if (File.Exists(full_path)) {
				byte[] buffer = File.ReadAllBytes(full_path);
				Image icon_image = ImageHelpers.ResizeImage(buffer, config_helper.ButtonSize, config_helper.ButtonSize);
				byte[] jpeg_buffer = ImageHelpers.GetJpegFromImage(icon_image);
				return jpeg_buffer;
			}
			return null;
        }

		public bool CreateLabelledIconPNG(string bg_img_path, string label) {
			try {
				// Load the background png into the drawing object
				byte[] jpeg_buffer = GetIconBufferJPEG(bg_img_path);
				(Image bg_image, MemoryStream bg_stream) = ImageHelpers.GetImage(jpeg_buffer);
				Graphics drawing = Graphics.FromImage(bg_image);

				// Paint the text on to the image
				Brush textBrush = new SolidBrush(Color.White);
				drawing.TextRenderingHint = TextRenderingHint.AntiAlias;
				drawing.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
				drawing.DrawString(label, font, textBrush, 0, 0);
				drawing.Save();

				// Release GDI handles
				textBrush.Dispose();
				drawing.Dispose();

				// Resize the resulting image to fit our StreamDeck button size
				//  icon_image = ImageHelpers.ResizeImage(image_buffer, config_helper.ButtonSize, config_helper.ButtonSize);
				// icon_image = ImageHelpers.GetImage(image_buffer);

				// Save the resulting image as 256 PNG
				string labelled_icon_rel_path = $"icons\\{label}.png";
				string labelled_icon_full_path = config_helper.GetFullIconPath(labelled_icon_rel_path);
				bg_image.Save(labelled_icon_full_path, ImageFormat.Png);
				return true;
			}
			catch (Exception ex) {
				logger.Error($"CreateLabelledIcon: {ex.ToString()}");
            }
			return false;
		}
	}
}