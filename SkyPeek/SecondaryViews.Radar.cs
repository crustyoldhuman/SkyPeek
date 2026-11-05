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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace SkyPeek
{
	public partial class SecondaryViews : Window
	{
		private readonly System.Windows.Threading.DispatcherTimer _radarTimer = new(DispatcherPriority.Render);
		private readonly System.Collections.Generic.List<RadarFrame> _radarFrames = [];
		private int _radarIndex = 0;
		private System.Threading.CancellationTokenSource? _radarLoadCts;

		private async System.Threading.Tasks.Task LoadRadarData(double lat, double lon)
		{
			if (lat == 0 && lon == 0) return;

			_radarLoadCts?.Cancel();
			_radarLoadCts = new System.Threading.CancellationTokenSource();
			var token = _radarLoadCts.Token;

			_radarTimer.Stop();

			// FIX 1: Zoom 7 at 256px provides the exact same geographic scale as Zoom 6 at 512px, but without stretching the pixels
			int zoom = 7;
			double renderTileSize = 256.0;

			double latRad = lat * Math.PI / 180.0;
			double n = Math.Pow(2.0, zoom);

			double xFloat = (lon + 180.0) / 360.0 * n;
			double yFloat = (1.0 - Math.Log(Math.Tan(latRad) + 1.0 / Math.Cos(latRad)) / Math.PI) / 2.0 * n;

			int xTile = (int)xFloat;
			int yTile = (int)yFloat;

			double xPercent = xFloat - xTile;
			double yPercent = yFloat - yTile;

			// FIX 2: Use ActualWidth to account for XAML margins. Fallback to 225/220 if the UI layout hasn't fully rendered yet.
			double containerWidth = RadarCanvas.ActualWidth > 0 ? RadarCanvas.ActualWidth : 225.0;
			double containerHeight = RadarCanvas.ActualHeight > 0 ? RadarCanvas.ActualHeight : 280.0;

			double centerTileX = (containerWidth / 2.0) - (renderTileSize * xPercent);
			double centerTileY = (containerHeight / 2.0) - (renderTileSize * yPercent);

			ClearRadarVisuals();

			// Dynamically lock the dot to the true mathematical center of the canvas
			Canvas.SetLeft(RadarLocationDot, (containerWidth / 2.0) - (RadarLocationDot.Width / 2.0));
			Canvas.SetTop(RadarLocationDot, (containerHeight / 2.0) - (RadarLocationDot.Height / 2.0));
			RadarLocationDot.Visibility = Visibility.Visible;

			var tilePositions = new (double X, double Y)[9];
			int tileIdx = 0;

			// Build the 3x3 layout
			for (int dy = -1; dy <= 1; dy++)
			{
				for (int dx = -1; dx <= 1; dx++)
				{
					int tx = xTile + dx;
					int ty = yTile + dy;

					double posX = centerTileX + (dx * renderTileSize);
					double posY = centerTileY + (dy * renderTileSize);
					tilePositions[tileIdx++] = (posX, posY);

					// Map Tile
					Image mapImg = new() { Width = renderTileSize, Height = renderTileSize };
					Canvas.SetLeft(mapImg, posX);
					Canvas.SetTop(mapImg, posY);
					MapLayer.Children.Add(mapImg);

					// Fire and forget the map tile download
					_ = System.Threading.Tasks.Task.Run(async () =>
					{
						var bmp = await RadarProcessor.GetMapTileAsync(zoom, tx, ty, token);
						if (bmp != null && !token.IsCancellationRequested)
						{
							Application.Current.Dispatcher.Invoke(() =>
							{
								if (!token.IsCancellationRequested) mapImg.Source = bmp;
							});
						}
					}, token);
				}
			}

			bool use24Hour = AppConfig.Current.Use24HourClock;
			var frames = await RadarProcessor.GetRadarFramesAsync(zoom, xTile, yTile, use24Hour, token);

			if (token.IsCancellationRequested) return;

			_radarFrames.Clear();
			_radarFrames.AddRange(frames);

			if (_radarFrames.Count > 0)
			{
				_radarIndex = 0;

				Application.Current.Dispatcher.Invoke(() =>
				{
					if (token.IsCancellationRequested) return;

					_radarTimer.Stop();
					ClearRadarLayerChildren();

					for (int f = 0; f < _radarFrames.Count; f++)
					{
						Canvas frameCanvas = new()
						{
							Visibility = f == 0 ? Visibility.Visible : Visibility.Hidden
						};

						for (int i = 0; i < 9; i++)
						{
							Image radarImg = new()
							{
								Width = renderTileSize,
								Height = renderTileSize,
								Source = _radarFrames[f].TileImages[i]
							};
							Canvas.SetLeft(radarImg, tilePositions[i].X);
							Canvas.SetTop(radarImg, tilePositions[i].Y);
							frameCanvas.Children.Add(radarImg);
						}

						RadarLayer.Children.Add(frameCanvas);
					}

					_radarTimer.Interval = TimeSpan.FromMilliseconds(600);
					_radarTimer.Tick -= RadarTimer_Tick;
					_radarTimer.Tick += RadarTimer_Tick;

					_isScrubberUpdating = true;
					RadarScrubber.Maximum = _radarFrames.Count - 1;
					RadarScrubber.Value = 0;
					_isScrubberUpdating = false;

					RenderCurrentRadarFrame();

					if (ViewModel.CurrentActiveView == "Radar" && this.IsOpen)
					{
						_isRadarPaused = false;
						PauseIcon.Visibility = Visibility.Visible;
						PlayIcon.Visibility = Visibility.Collapsed;
						_radarTimer.Start();
					}
					else
					{
						_isRadarPaused = true;
						PauseIcon.Visibility = Visibility.Collapsed;
						PlayIcon.Visibility = Visibility.Visible;
					}
				});
			}

			System.Runtime.GCSettings.LargeObjectHeapCompactionMode = System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;
			System.GC.Collect(2, System.GCCollectionMode.Forced, true, true);
			System.GC.WaitForPendingFinalizers();
		}

		private void ClearRadarLayerChildren()
		{
			foreach (UIElement child in RadarLayer.Children)
			{
				if (child is Canvas frameCanvas)
				{
					foreach (UIElement innerChild in frameCanvas.Children)
					{
						if (innerChild is Image img) img.Source = null;
					}
					frameCanvas.Children.Clear();
				}
			}
			RadarLayer.Children.Clear();
		}

		private void ClearRadarVisuals()
		{
			foreach (UIElement child in MapLayer.Children)
			{
				if (child is Image img) img.Source = null;
			}
			MapLayer.Children.Clear();
			ClearRadarLayerChildren();
		}

		private void RadarTimer_Tick(object? sender, EventArgs e)
		{
			if (_radarFrames.Count == 0) return;

			_radarIndex++;
			if (_radarIndex >= _radarFrames.Count)
			{
				_radarIndex = 0;
			}

			RenderCurrentRadarFrame();

			_isScrubberUpdating = true;
			RadarScrubber.Value = _radarIndex;
			_isScrubberUpdating = false;

			_radarTimer.Interval = (_radarIndex == _radarFrames.Count - 1)
				? TimeSpan.FromMilliseconds(1500)
				: TimeSpan.FromMilliseconds(600);
		}

		private void RadarPlayPause_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			if (_isRadarPaused)
				PlayRadar();
			else
				PauseRadar();

			if (e is MouseButtonEventArgs args) args.Handled = true;
		}

		private void RadarScrubber_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
		{
			if (_isScrubberUpdating || _radarFrames.Count == 0) return;
			_radarIndex = (int)Math.Round(e.NewValue);
			RenderCurrentRadarFrame();
		}

		private void RadarScrubber_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			PauseRadar();

			if (RadarScrubber.Template.FindName("PART_Track", RadarScrubber) is Track { Thumb: not null } track)
			{
				if (!track.Thumb.IsMouseOver)
				{
					RadarScrubber.Value = Math.Round(track.ValueFromPoint(e.GetPosition(track)));
					e.Handled = true;

					Dispatcher.BeginInvoke(new Action(() =>
					{
						track.Thumb.RaiseEvent(new MouseButtonEventArgs(e.MouseDevice, e.Timestamp, MouseButton.Left)
						{
							RoutedEvent = UIElement.MouseLeftButtonDownEvent,
							Source = track.Thumb
						});
					}), System.Windows.Threading.DispatcherPriority.Input);
				}
			}
		}

		private void RenderCurrentRadarFrame()
		{
			if (_radarFrames.Count == 0 || _radarIndex < 0 || _radarIndex >= _radarFrames.Count) return;

			for (int i = 0; i < RadarLayer.Children.Count; i++)
			{
				if (RadarLayer.Children[i] is UIElement frameLayer)
				{
					frameLayer.Visibility = (i == _radarIndex) ? Visibility.Visible : Visibility.Hidden;
				}
			}
			ViewModel.RadarTime = _radarFrames[_radarIndex].Time;
		}

		internal void PauseRadar()
		{
			if (!_isRadarPaused)
			{
				_isRadarPaused = true;
				_radarTimer.Stop();
				PauseIcon.Visibility = Visibility.Collapsed;
				PlayIcon.Visibility = Visibility.Visible;
			}
		}

		internal void PlayRadar()
		{
			if (_isRadarPaused)
			{
				_isRadarPaused = false;
				if (_radarFrames.Count > 0)
				{
					_radarTimer.Interval = (_radarIndex == _radarFrames.Count - 1)
						? TimeSpan.FromMilliseconds(1500)
						: TimeSpan.FromMilliseconds(600);
					_radarTimer.Start();
				}
				PauseIcon.Visibility = Visibility.Visible;
				PlayIcon.Visibility = Visibility.Collapsed;
			}
		}

		internal void RadarCopyButton_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			try
			{
				RadarLocationDot.Visibility = Visibility.Hidden;
				RadarMapContainer.UpdateLayout();
				RadarProcessor.CopyContainerToClipboard(RadarMapContainer);
				ToastNotification.TriggerToast(ToastText, "Radar copied to clipboard");
			}
			catch { }
			finally
			{
				RadarLocationDot.Visibility = Visibility.Visible;
			}

			if (e is MouseButtonEventArgs args) args.Handled = true;
		}

		internal async void RadarSaveGifButton_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			if (_radarFrames.Count == 0) return;
			if (e is MouseButtonEventArgs args) args.Handled = true;

			bool wasPaused = _isRadarPaused;
			PauseRadar();
			int originalIndex = _radarIndex;

			try
			{
				RadarLocationDot.Visibility = Visibility.Hidden;
				RadarMapContainer.UpdateLayout();

				string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
				string filePath = System.IO.Path.Combine(desktopPath, $"SkyPeek_Radar_{DateTime.Now:yyyyMMdd_HHmmss}.gif");

				await RadarProcessor.SaveRadarGifFromContainerAsync(RadarMapContainer, _radarFrames.Count, async (i) =>
				{
					_radarIndex = i;
					RenderCurrentRadarFrame();
					await Dispatcher.Yield(DispatcherPriority.Render);
				}, filePath);

				ToastNotification.TriggerToast(ToastText, "GIF saved to Desktop");
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"GIF save error: {ex.Message}");
			}
			finally
			{
				RadarLocationDot.Visibility = Visibility.Visible;
				_radarIndex = originalIndex;
				RenderCurrentRadarFrame();
				if (!wasPaused) PlayRadar();
			}
		}
	}
}