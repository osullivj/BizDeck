using System;
using System.IO;
using System.Linq;
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

		protected byte[] LoadIconAsPNG(string relative_path) {
			string full_path = config_helper.GetFullIconPath(relative_path);
			if (File.Exists(full_path)) {
				byte[] buffer = File.ReadAllBytes(full_path);
				return buffer;
			}
			return null;
		}

		public bool CreateLabelledIconPNG(string bg_img_path, string label) {
			try {
				// Split on underscore so we can wrap text, and
				// calculate word sizes
				SizeF all_words_size = new(0,0);
				List<SizeF> word_sizes = new();
				var words = label.Split('_').ToList();
				words.ForEach(s => word_sizes.Add(CalculateStringSize(s)));
				int word_height = 0;
				foreach (SizeF sf in word_sizes) {
					if (sf.Width > all_words_size.Width) {
						all_words_size.Width = sf.Width;
                    }
					all_words_size.Height += sf.Height;
					word_height = (int)sf.Height;
                }
				// Calculate the top left relative positions for the strings
				int xindent = (256 - (int)all_words_size.Width) / 2;
				int yindent = (256 - (word_height * words.Count)) / 2;

				// Load the background png into the drawing object
				byte[] png_buffer = LoadIconAsPNG(bg_img_path);
				(Image bg_image, MemoryStream bg_stream) = ImageHelpers.GetImage(png_buffer);
				Graphics drawing = Graphics.FromImage(bg_image);

				// Paint the text on to the image
				Brush textBrush = new SolidBrush(Color.White);
				drawing.TextRenderingHint = TextRenderingHint.AntiAlias;
				drawing.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
				for (int i = 0; i < words.Count; i++) {
					drawing.DrawString(words[i], font, textBrush, xindent, yindent+(word_height*i));
				}
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

		private SizeF CalculateStringSize(string text) {
			Image image = new Bitmap(1, 1);
			Graphics drawing = Graphics.FromImage(image);
			SizeF text_size = drawing.MeasureString(text, font);
			image.Dispose();
			drawing.Dispose();
			return text_size;
		}
	}
}