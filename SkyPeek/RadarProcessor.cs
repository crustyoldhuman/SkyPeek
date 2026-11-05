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
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows.Controls;
using System.Text.Json;
using ImageMagick;

namespace SkyPeek
{
	internal class RadarFrame
	{
		public string PathKey { get; set; } = "";
		public string Time { get; set; } = "";
		public BitmapSource?[] TileImages { get; set; } = new BitmapSource?[9];
	}

	[System.Runtime.Versioning.SupportedOSPlatform("windows10.0.19041.0")]
	internal static class RadarProcessor
	{
		private static readonly HttpClient _httpClient = new();
		private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, BitmapSource> _mapTileCache = new();
		private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, RadarFrame> _radarFrameCache = new();
		private static string _cachedRadarCoordsKey = "";

		private static WriteableBitmap DecodeAndFreeze(byte[] imageBytes)
		{
			using var ms = new MemoryStream(imageBytes);
			var decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
			BitmapSource source = decoder.Frames[0];
			if (source.Format != PixelFormats.Pbgra32)
			{
				source = new FormatConvertedBitmap(source, PixelFormats.Pbgra32, null, 0);
			}
			var wb = new WriteableBitmap(source);
			wb.Freeze();
			return wb;
		}

		public static async Task<BitmapSource?> GetMapTileAsync(int z, int x, int y, System.Threading.CancellationToken token = default)
		{
			string key = $"{z}_{x}_{y}";
			if (_mapTileCache.TryGetValue(key, out var cachedTile))
			{
				return cachedTile;
			}

			try
			{
				using HttpRequestMessage request = new(HttpMethod.Get, $"https://a.tile.openstreetmap.org/{z}/{x}/{y}.png");
				request.Headers.Add("User-Agent", "SkyPeek/1.0");
				using HttpResponseMessage response = await _httpClient.SendAsync(request, token);
				byte[] mapBytes = await response.Content.ReadAsByteArrayAsync(token);

				var decoded = DecodeAndFreeze(mapBytes);
				if (_mapTileCache.Count > 36) _mapTileCache.Clear();
				_mapTileCache[key] = decoded;
				return decoded;
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Error loading map tile ({z}/{x}/{y}): {ex.Message}");
				return null;
			}
		}

		public static async Task<List<RadarFrame>> GetRadarFramesAsync(int zoom, int xTile, int yTile, bool use24Hour, System.Threading.CancellationToken token = default)
		{
			var frames = new List<RadarFrame>();
			try
			{
				string coordsKey = $"{zoom}_{xTile}_{yTile}";
				if (_cachedRadarCoordsKey != coordsKey)
				{
					_radarFrameCache.Clear();
					_cachedRadarCoordsKey = coordsKey;
				}

				string json = await _httpClient.GetStringAsync("https://api.rainviewer.com/public/weather-maps.json", token);
				using JsonDocument doc = JsonDocument.Parse(json);
				JsonElement past = doc.RootElement.GetProperty("radar").GetProperty("past");

				var activePaths = new HashSet<string>();

				foreach (JsonElement frame in past.EnumerateArray())
				{
					if (token.IsCancellationRequested) break;

					long unixTime = frame.GetProperty("time").GetInt64();
					DateTime time = DateTimeOffset.FromUnixTimeSeconds(unixTime).ToLocalTime().DateTime;
					string path = frame.GetProperty("path").GetString()!;
					string formattedTime = time.ToString(use24Hour ? "H:mm" : "h:mm tt");
					activePaths.Add(path);

					if (_radarFrameCache.TryGetValue(path, out var existingFrame))
					{
						existingFrame.Time = formattedTime;
						frames.Add(existingFrame);
						continue;
					}

					RadarFrame rf = new() { PathKey = path, Time = formattedTime };
					int i = 0;

					for (int dy = -1; dy <= 1; dy++)
					{
						for (int dx = -1; dx <= 1; dx++)
						{
							if (token.IsCancellationRequested) break;
							int tx = xTile + dx;
							int ty = yTile + dy;
							string tileUrl = $"https://tilecache.rainviewer.com{path}/256/{zoom}/{tx}/{ty}/2/1_1.png";

							try
							{
								byte[] imageBytes = await _httpClient.GetByteArrayAsync(tileUrl, token).ConfigureAwait(false);
								rf.TileImages[i] = DecodeAndFreeze(imageBytes);
							}
							catch (Exception ex)
							{
								System.Diagnostics.Debug.WriteLine($"Radar tile error: {ex.Message}");
								rf.TileImages[i] = null;
							}
							i++;
						}
					}

					if (!token.IsCancellationRequested)
					{
						_radarFrameCache[path] = rf;
						frames.Add(rf);
					}
				}

				foreach (var cachedKey in _radarFrameCache.Keys)
				{
					if (!activePaths.Contains(cachedKey))
					{
						_radarFrameCache.TryRemove(cachedKey, out _);
					}
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"RainViewer API Error: {ex.Message}");
			}
			return frames;
		}

		public static Grid CreateWatermarkGrid(int width, int height)
		{
			Grid watermarkGrid = new() { Width = width, Height = height };
			double textLeftMargin = 8;
			try
			{
				using System.Drawing.Icon sysIcon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!)!;
				BitmapSource frame = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(sysIcon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
				frame.Freeze();
				Image logoImage = new() { Source = frame, Width = 24, Height = 24, Margin = new Thickness(8, 6, 0, 0), VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Left };
				watermarkGrid.Children.Add(logoImage);
				textLeftMargin = 32;
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Watermark Icon Error: {ex.Message}");
			}

			TextBlock shadowText = new() { Text = "SkyPeek Radar", Foreground = Brushes.White, FontSize = 16, Margin = new Thickness(textLeftMargin, 6, 0, 0), VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Left, Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Colors.Black, BlurRadius = 4, ShadowDepth = 0, Opacity = 1 } };
			TextBlock mainText = new() { Text = "SkyPeek Radar", Foreground = Brushes.White, FontSize = 16, Margin = new Thickness(textLeftMargin, 6, 0, 0), VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Left };
			watermarkGrid.Children.Add(shadowText);
			watermarkGrid.Children.Add(mainText);
			watermarkGrid.Measure(new Size(width, height));
			watermarkGrid.Arrange(new Rect(0, 0, width, height));
			watermarkGrid.UpdateLayout();
			return watermarkGrid;
		}

		public static RenderTargetBitmap CaptureRenderFrame(FrameworkElement container, Grid watermarkGrid)
		{
			int width = (int)Math.Round(container.ActualWidth);
			int height = (int)Math.Round(container.ActualHeight);
			DpiScale dpi = VisualTreeHelper.GetDpi(container);

			RenderTargetBitmap rtb = new(
				(int)(width * dpi.DpiScaleX),
				(int)(height * dpi.DpiScaleY),
				dpi.PixelsPerInchX, dpi.PixelsPerInchY,
				PixelFormats.Pbgra32);

			DrawingVisual dv = new();
			using (DrawingContext ctx = dv.RenderOpen())
			{
				VisualBrush vb = new(container) { Stretch = Stretch.None };
				ctx.DrawRectangle(vb, null, new Rect(0, 0, width, height));
			}
			rtb.Render(dv);
			rtb.Render(watermarkGrid);
			rtb.Freeze();
			return rtb;
		}

		public static void CopyContainerToClipboard(FrameworkElement container)
		{
			int width = (int)Math.Round(container.ActualWidth);
			int height = (int)Math.Round(container.ActualHeight);
			if (width <= 0 || height <= 0) return;

			Grid watermarkGrid = CreateWatermarkGrid(width, height);
			RenderTargetBitmap rtb = CaptureRenderFrame(container, watermarkGrid);
			CopyRadarToClipboard(rtb);
		}

		public static async Task SaveRadarGifFromContainerAsync(FrameworkElement container, int frameCount, Func<int, Task> stepFrameCallback, string outputPath)
		{
			int width = (int)Math.Round(container.ActualWidth);
			int height = (int)Math.Round(container.ActualHeight);
			if (width <= 0 || height <= 0 || frameCount == 0) return;

			Grid watermarkGrid = CreateWatermarkGrid(width, height);
			var renderedFrames = new List<RenderTargetBitmap>();

			for (int i = 0; i < frameCount; i++)
			{
				await stepFrameCallback(i);
				renderedFrames.Add(CaptureRenderFrame(container, watermarkGrid));
			}

			await Task.Run(() => SaveRadarGif(renderedFrames, outputPath));
		}

		public static void CopyRadarToClipboard(RenderTargetBitmap rtb)
		{
			PngBitmapEncoder encoder = new();
			encoder.Frames.Add(BitmapFrame.Create(rtb));
			using MemoryStream ms = new();
			encoder.Save(ms);

			DataObject data = new();
			data.SetImage(rtb);
			data.SetData("PNG", ms, false);

			try
			{
				Clipboard.SetDataObject(data, true);
			}
			catch (System.Runtime.InteropServices.ExternalException)
			{
				System.Diagnostics.Debug.WriteLine("Clipboard access denied: Locked by another process.");
			}
		}

		public static void SaveRadarGif(List<RenderTargetBitmap> renderedFrames, string outputPath)
		{
			using var collection = new MagickImageCollection();

			for (int i = 0; i < renderedFrames.Count; i++)
			{
				var pngEncoder = new PngBitmapEncoder();
				pngEncoder.Frames.Add(BitmapFrame.Create(renderedFrames[i]));
				using var pngStream = new MemoryStream();
				pngEncoder.Save(pngStream);
				pngStream.Position = 0;

				var magickImage = new MagickImage(pngStream)
				{
					AnimationDelay = (i == renderedFrames.Count - 1) ? 150U : 25U
				};

				collection.Add(magickImage);
			}

			collection.Write(outputPath);
		}
	}
}