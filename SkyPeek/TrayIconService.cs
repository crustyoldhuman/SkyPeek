/*
 * SkyPeek
 * Copyright (C) 2026 Kevin Stanislawski
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SkyPeek
{
	public static class TrayIconService
	{
		public static System.Drawing.Icon GenerateIcon(string text, Color? alertColor, double _, bool enableAlertColors)
		{
			if (!enableAlertColors) alertColor = null;

			System.Drawing.Color textColor = System.Drawing.Color.White;
			if (alertColor.HasValue)
			{
				textColor = System.Drawing.Color.FromArgb(alertColor.Value.A, alertColor.Value.R, alertColor.Value.G, alertColor.Value.B);
			}
			else
			{
				if (Application.Current.Resources["TrayTextBrush"] is SolidColorBrush appResource)
					textColor = System.Drawing.Color.FromArgb(appResource.Color.A, appResource.Color.R, appResource.Color.G, appResource.Color.B);
			}

			int size = 128;
			using System.Drawing.Bitmap bitmap = new(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
			using System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(bitmap);

			graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
			graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
			graphics.Clear(System.Drawing.Color.Transparent);

			using System.Drawing.FontFamily fontFamily = new("Segoe UI");
			using System.Drawing.StringFormat format = new();
			format.Alignment = System.Drawing.StringAlignment.Center;
			format.LineAlignment = System.Drawing.StringAlignment.Center;

			using System.Drawing.Drawing2D.GraphicsPath path = new();
			path.AddString(text, fontFamily, (int)System.Drawing.FontStyle.Bold, 100, new System.Drawing.Point(0, 0), format);

			System.Drawing.RectangleF textBounds = path.GetBounds();

			double margin = size * 0.05;
			double targetWidth = size - (margin * 2);
			double targetHeight = size - (margin * 2);

			double scaleX = targetWidth / textBounds.Width;
			double scaleY = targetHeight / textBounds.Height;
			double scale = Math.Min(scaleX, scaleY);

			if (text.Length <= 2 || !text.Any(char.IsDigit))
			{
				using System.Drawing.Drawing2D.GraphicsPath refPath = new();
				refPath.AddString("00°", fontFamily, (int)System.Drawing.FontStyle.Bold, 100, new System.Drawing.Point(0, 0), format);
				System.Drawing.RectangleF refBounds = refPath.GetBounds();
				double refScaleX = targetWidth / refBounds.Width;
				double refScaleY = targetHeight / refBounds.Height;
				scale = Math.Min(refScaleX, refScaleY);
			}

			graphics.TranslateTransform(size / 2.0f, size / 2.0f);
			graphics.ScaleTransform((float)scale, (float)scale);
			graphics.TranslateTransform(-(textBounds.Left + textBounds.Width / 2.0f), -(textBounds.Top + textBounds.Height / 2.0f));

			using System.Drawing.SolidBrush brush = new(textColor);
			graphics.FillPath(brush, path);

			using MemoryStream pngStream = new();
			bitmap.Save(pngStream, System.Drawing.Imaging.ImageFormat.Png);
			byte[] pngBytes = pngStream.ToArray();

			using MemoryStream icoStream = new();
			using (BinaryWriter writer = new(icoStream, System.Text.Encoding.UTF8, true))
			{
				writer.Write((short)0);
				writer.Write((short)1);
				writer.Write((short)1);
				writer.Write((byte)size);
				writer.Write((byte)size);
				writer.Write((byte)0);
				writer.Write((byte)0);
				writer.Write((short)1);
				writer.Write((short)32);
				writer.Write(pngBytes.Length);
				writer.Write(22);
				writer.Write(pngBytes);
			}

			icoStream.Position = 0;
			return new System.Drawing.Icon(icoStream);
		}
	}
}