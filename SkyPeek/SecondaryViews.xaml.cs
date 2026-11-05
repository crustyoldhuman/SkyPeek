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

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace SkyPeek
{
	public class SingleWordVisibilityConverter : System.Windows.Data.IValueConverter
	{
		public object Convert(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			if (value is string str && !string.IsNullOrWhiteSpace(str))
			{
				string[] words = str.Trim().Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
				bool needsMarquee = words.Length > 2;
				if (!needsMarquee)
				{
					foreach (var word in words)
					{
						if (word.Length > 9)
						{
							needsMarquee = true;
							break;
						}
					}
				}
				bool wantMarquee = parameter as string == "Single";
				return needsMarquee == wantMarquee ? Visibility.Visible : Visibility.Collapsed;
			}
			return Visibility.Collapsed;
		}
		public object ConvertBack(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture) => throw new System.NotImplementedException();
	}

	public class ActiveViewConverter : System.Windows.Data.IValueConverter
	{
		public object Convert(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			return value?.ToString() == parameter?.ToString() ? Visibility.Visible : Visibility.Hidden;
		}
		public object ConvertBack(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture) => throw new System.NotImplementedException();
	}

	/// <summary>
	/// Interaction logic for Slideout.xaml
	/// </summary>

	[System.Runtime.Versioning.SupportedOSPlatform("windows10.0.19041.0")]
	public partial class SecondaryViews : Window
	{
		public SlideoutViewModel ViewModel { get; } = new();

		private static void LogUIErrorToDesktop(string step, Exception ex)
		{
			try
			{
				string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
				string logPath = System.IO.Path.Combine(desktopPath, "SkyPeek_LoadingErrorLog.txt");
				System.IO.File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {step} - {ex.Message}\n{ex.StackTrace}\n\n");
			}
			catch { }
		}

		public void RefreshTheme()
		{
			ThemeHelper.ApplyAccentColor(this, GradientCenter, GradientEdge, WindowBorder, WindowShadow);
		}

		protected override void OnClosed(EventArgs e)
		{
			Microsoft.Win32.SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;
			base.OnClosed(e);
		}

		public void UpdateGlobalAlertIndicator()
		{
			Application.Current.Dispatcher.Invoke(() =>
			{
				ViewModel.AlertState.RebuildGlobalAlerts(AppConfig.Current.SavedLocations, AppConfig.Current.WeatherCache);
			});
		}

		private void DismissAlert_Click(object sender, RoutedEventArgs e)
		{
			if (sender is FrameworkElement { Tag: SkyPeek.WeatherAlert alert })
			{
				_alertVisibleSince.Remove(alert.Id);
				AlertDismissed?.Invoke(this, alert.Id);
			}
			e.Handled = true;
		}

		private void ClearAllAlertsButton_Click(object sender, RoutedEventArgs e)
		{
			if (Application.Current.MainWindow is MainWindow mainWindow)
			{
				mainWindow.ClearAllAlerts();
			}
			e.Handled = true;
		}

		[System.Runtime.InteropServices.LibraryImport("user32.dll", EntryPoint = "GetCursorPos")]
		[return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
		private static partial bool GetCursorPosition(out POINT lpPoint);
		public struct POINT { public int X; public int Y; }


		private enum TaskbarPosition { Bottom, Top, Left, Right }
		private bool _isAnimating = false;
		internal bool _isRadarPaused = false;
		private bool _isCenteredMode = false;
		private bool _isScrubberUpdating = false;
		public bool IsViewingTemporaryLocation { get; set; } = false;
		private bool _isOpen = false;
		public bool IsOpen
		{
			get => _isOpen;
			private set
			{
				if (_isOpen != value)
				{
					_isOpen = value;
					IsOpenChanged?.Invoke(this, value);
				}
			}
		}
		public event EventHandler<bool>? IsOpenChanged;
		private bool _needsWeatherRefresh = false;
		internal bool _isKeyboardNavRevealed = false;
		private readonly DateTime _bootTime = DateTime.Now;
		private readonly System.Windows.Threading.DispatcherTimer _alertVisibilityTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
		private readonly System.Collections.Generic.Dictionary<string, DateTime> _alertVisibleSince = [];

		public SecondaryViews()
		{
			InitializeComponent();
			this.DataContext = ViewModel;

			// Initial Boot Sequence state
			ViewModel.IsLoading = true;
			ContentContainer.Opacity = 0.0;
			ContentContainer.IsHitTestVisible = false;
			LoadingView.Opacity = 1.0;
			LoadingView.Visibility = Visibility.Visible;
			LoadingView.IsHitTestVisible = true;
			TabArea.Opacity = 0.5;
			TabArea.IsHitTestVisible = false;

			ViewModel.AlertState.OnAlertsCleared += () =>
			{
				if (AlertView.Visibility == Visibility.Visible)
				{
					AlertBack_MouseLeftButtonUp(this, null!);

					if (_isKeyboardNavRevealed)
					{
						TabCurrent.Focus();
						TabCurrent.Tag = "Unselected";
						ViewModel.IsAlertIconSelected = false;
						ViewModel.IsWeatherAnimationSelected = false;
						ViewModel.IsLocationMenuSelected = true;

						Dispatcher.BeginInvoke(new Action(() =>
						{
							System.Windows.Input.FocusManager.SetFocusedElement(CurrentView, null);
							CurrentView.LocationComboBox.Focus();
						}), DispatcherPriority.Loaded);
					}
				}
			};

			ViewModel.WeatherArraysTruncated += () =>
			{
				if (ViewModel.CurrentWeather?.HourlyData?.Count > 0)
				{
					GraphService.DrawHourlyGraph(HourlyView.GraphCanvas, HourlyView.ScaleCanvas, HourlyView.TempGraphLine, HourlyView.ActiveTempDot, ViewModel.CurrentWeather.HourlyData);
					GraphSyncService.SyncHourlyDot(HourlyView.HourlyScroller, HourlyView.TempGraphLine, HourlyView.ActiveTempDot, HourlyView.ScaleCanvas, ref _isScaleVisible, ViewModel.CurrentWeather.HourlyData);
				}
				if (ViewModel.CurrentWeather?.DailyData?.Count > 0)
				{
					GraphService.DrawDailyGraph(TenDayView.DailyGraphCanvas, TenDayView.DailyScaleCanvas, TenDayView.TempRibbonPolygon, TenDayView.HighTempGraphLine, TenDayView.LowTempGraphLine, TenDayView.ActiveHighTempDot, TenDayView.ActiveLowTempDot, ViewModel.CurrentWeather.DailyData);
					GraphSyncService.SyncDailyDots(TenDayView.DailyScroller, TenDayView.HighTempGraphLine, TenDayView.LowTempGraphLine, TenDayView.ActiveHighTempDot, TenDayView.ActiveLowTempDot, TenDayView.DailyScaleCanvas, ref _isDailyScaleVisible, ViewModel.CurrentWeather.DailyData);
				}
			};

			ThemeHelper.ApplyAccentColor(this, GradientCenter, GradientEdge, WindowBorder, WindowShadow);
			PopulateLocationManagerDropdowns();

			_cityTypingTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
			_cityTypingTimer.Tick += CityTypingTimer_Tick;

			_alertVisibilityTimer.Tick += (s, e) => TrackVisibleAlerts();

			Microsoft.Win32.SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;

			ViewModel.PropertyChanged += (s, e) =>
			{
				if (e.PropertyName == nameof(ViewModel.CurrentActiveView) || e.PropertyName == nameof(ViewModel.IsLoading))
				{
					if (ViewModel.IsLoading && this.IsOpen) LoadingAnimation.PlayAnimation();
					else LoadingAnimation.StopAnimation();

					if (ViewModel.CurrentActiveView == "Summary" && this.IsOpen) SummaryIconAnimation.PlayAnimation();
					else SummaryIconAnimation.StopAnimation();
				}
			};

			this.IsOpenChanged += (s, isOpen) =>
			{
				ViewModel.SetAnimationTimerState(isOpen);

				if (isOpen)
				{
					if (ViewModel.IsLoading) LoadingAnimation.PlayAnimation();
					if (ViewModel.CurrentActiveView == "Summary") SummaryIconAnimation.PlayAnimation();
					if (ViewModel.CurrentActiveView == "Radar") PlayRadar();

					if (MainWindow.IsUpdateAvailable && !MainWindow.HasSeenUpdateToast)
					{
						MainWindow.HasSeenUpdateToast = true;
						ToastNotification.TriggerToast(ToastText, "Update available! Right click the SkyPeek tray icon for info.", stayOnScreenSeconds: 6, delayEntranceMs: 500);
					}
				}
				else
				{
					LoadingAnimation.StopAnimation();
					SummaryIconAnimation.StopAnimation();
					PauseRadar();
				}
			};

			SummaryIconAnimation.StopAnimation();
			LoadingAnimation.StopAnimation();
		}

		private void SystemEvents_UserPreferenceChanged(object sender, Microsoft.Win32.UserPreferenceChangedEventArgs e)
		{
			if (e.Category == Microsoft.Win32.UserPreferenceCategory.Color || e.Category == Microsoft.Win32.UserPreferenceCategory.General)
			{
				ThemeHelper.ApplyAccentColor(this, GradientCenter, GradientEdge, WindowBorder, WindowShadow);
			}
		}

		public async void UpdateWeatherUI(CachedWeatherData data, string city, bool flipWindArrow, bool showLoading = false, bool isOffline = false, bool isApiUnreachable = false)
		{
			if (data == null) return;
			PauseRadar();
			_radarIndex = 0;
			_radarFrames.Clear();
			ViewModel.RadarTime = "--:--";

			async System.Threading.Tasks.Task ApplyDataAsync()
			{
				await ViewModel.ProcessWeatherDataAsync(data, city, flipWindArrow, isOffline, isApiUnreachable);

				string displayLocation = city;
				SavedLocation? selectedLoc = AppConfig.Current.SavedLocations.FirstOrDefault(l => l.City.Equals(city, StringComparison.OrdinalIgnoreCase));

				if (selectedLoc is not null) displayLocation = selectedLoc.DisplayName;

				_isProgrammaticLocationChange = true;
				ViewModel.City = city.Contains(',') ? city.Split(',')[0].Trim() : city.Trim();

				_isProgrammaticLocationChange = false;
				UpdateGlobalAlertIndicator();

				await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);

				if (data.HourlyData?.Count > 0)
				{
					GraphService.DrawHourlyGraph(HourlyView.GraphCanvas, HourlyView.ScaleCanvas, HourlyView.TempGraphLine, HourlyView.ActiveTempDot, data.HourlyData);
					GraphSyncService.SyncHourlyDot(HourlyView.HourlyScroller, HourlyView.TempGraphLine, HourlyView.ActiveTempDot, HourlyView.ScaleCanvas, ref _isScaleVisible, data.HourlyData);
				}

				await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);

				var radarTask = LoadRadarData(data.Latitude, data.Longitude); // Fire and forget so UI continues rendering

				await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);

				if (data.DailyData?.Count > 0)
				{
					GraphService.DrawDailyGraph(TenDayView.DailyGraphCanvas, TenDayView.DailyScaleCanvas, TenDayView.TempRibbonPolygon, TenDayView.HighTempGraphLine, TenDayView.LowTempGraphLine, TenDayView.ActiveHighTempDot, TenDayView.ActiveLowTempDot, data.DailyData);
					GraphSyncService.SyncDailyDots(TenDayView.DailyScroller, TenDayView.HighTempGraphLine, TenDayView.LowTempGraphLine, TenDayView.ActiveHighTempDot, TenDayView.ActiveLowTempDot, TenDayView.DailyScaleCanvas, ref _isDailyScaleVisible, data.DailyData);
				}

				ViewModel.AlertState.RebuildGlobalAlerts(AppConfig.Current.SavedLocations, AppConfig.Current.WeatherCache);
			}

			try
			{
				// 1. IF requested, fade out the old data and show the loading screen BEFORE doing any data processing
				if (showLoading && !ViewModel.IsLoading)
				{
					TabArea.IsHitTestVisible = false;
					ContentContainer.IsHitTestVisible = false;
					ContentContainer.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(200)));

					ViewModel.IsLoading = true;
					LoadingView.Opacity = 0.0;
					LoadingView.IsHitTestVisible = true;
					LoadingView.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(200)));

					await System.Threading.Tasks.Task.Delay(0); // Wait for the loading sequence to establish
				}

				// Unconditionally enforce minimum boot time delay regardless of compiler Debug flags
				double elapsedSinceBoot = (DateTime.Now - _bootTime).TotalMilliseconds;
				if (ViewModel.IsLoading && elapsedSinceBoot < 4000)
				{
					await System.Threading.Tasks.Task.Delay((int)(4000 - elapsedSinceBoot));
				}

				// Prevent the thread from locking if the user is currently sliding the window open
				while (_isAnimating)
				{
					await System.Threading.Tasks.Task.Delay(50);
				}

				// 2. NOW process all bindings safely behind the opaque loading screen
				await ApplyDataAsync();
			}
			catch (Exception ex)
			{
				LogUIErrorToDesktop("ApplyDataAsync", ex);
			}
			finally
			{
				// 3. Stop and hide the loading screen securely
				if (ViewModel.IsLoading || LoadingView.Opacity > 0)
				{
					if (this.IsOpen)
					{
						await System.Threading.Tasks.Task.Delay(250); // Buffer for WPF to flush the new layout bindings

						var fadeOutLoad = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(200));
						fadeOutLoad.Completed += (s, ev) =>
						{
							LoadingView.BeginAnimation(UIElement.OpacityProperty, null);
							LoadingView.Opacity = 0.0;
							LoadingView.IsHitTestVisible = false;
							ViewModel.IsLoading = false;
						};
						LoadingView.BeginAnimation(UIElement.OpacityProperty, fadeOutLoad);
					}
					else
					{
						LoadingView.BeginAnimation(UIElement.OpacityProperty, null);
						LoadingView.Opacity = 0.0;
						LoadingView.IsHitTestVisible = false;
						ViewModel.IsLoading = false;
					}
				}

				// 4. Reveal the newly prepared weather content
				ContentContainer.IsHitTestVisible = true;
				if (this.IsOpen)
				{
					var fadeInContent = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(200));
					fadeInContent.Completed += (s, ev) =>
					{
						ContentContainer.BeginAnimation(UIElement.OpacityProperty, null);
						ContentContainer.Opacity = 1.0;
					};
					ContentContainer.BeginAnimation(UIElement.OpacityProperty, fadeInContent);
				}
				else
				{
					ContentContainer.BeginAnimation(UIElement.OpacityProperty, null);
					ContentContainer.Opacity = 1.0;
				}

				// 5. Unlock the tabs
				TabArea.IsHitTestVisible = true;
				TabArea.Opacity = 1.0;
			}
		}

		public void ShowLoadingState()
		{
			Dispatcher.Invoke(() =>
			{
				ContentContainer.IsHitTestVisible = false;
				TabArea.IsHitTestVisible = false;

				ContentContainer.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(200)));

				ViewModel.IsLoading = true;
				LoadingView.IsHitTestVisible = true;
				LoadingView.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(200)));
			});
		}

		protected override void OnSourceInitialized(System.EventArgs e)
		{
			base.OnSourceInitialized(e);
			this.HideFromAltTab();
		}

		protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
		{
			base.OnRenderSizeChanged(sizeInfo);

			if (sizeInfo.PreviousSize.Width == 0 || sizeInfo.PreviousSize.Height == 0) return;

			if (this.IsLoaded && this.IsOpen && !_isAnimating)
			{
				double dw = sizeInfo.NewSize.Width - sizeInfo.PreviousSize.Width;
				double dh = sizeInfo.NewSize.Height - sizeInfo.PreviousSize.Height;

				if (_isCenteredMode)
				{
					this.Left -= dw / 2;
					this.Top -= dh / 2;
				}
				else
				{
					switch (GetTaskbarPosition())
					{
						case TaskbarPosition.Bottom:
							this.Left -= dw;
							this.Top -= dh;
							break;
						case TaskbarPosition.Left:
							this.Top -= dh;
							break;
						case TaskbarPosition.Top:
							this.Left -= dw;
							break;
						case TaskbarPosition.Right:
							this.Left -= dw;
							this.Top -= dh;
							break;
					}
				}
			}
		}

		private bool _isScaleVisible = true;
		private bool _isDailyScaleVisible = true;
		public bool PreferTenDayAgenda { get; set; } = false;
		public bool PreferHourlyAgendaView { get; set; } = false;
		internal void DailyView_ScrollChanged(object sender, ScrollChangedEventArgs e)
		{
			GraphSyncService.SyncDailyDots(TenDayView.DailyScroller, TenDayView.HighTempGraphLine, TenDayView.LowTempGraphLine, TenDayView.ActiveHighTempDot, TenDayView.ActiveLowTempDot, TenDayView.DailyScaleCanvas, ref _isDailyScaleVisible, ViewModel.CurrentWeather?.DailyData);
		}

		internal void HourlyView_ScrollChanged(object sender, ScrollChangedEventArgs e)
		{
			GraphSyncService.SyncHourlyDot(HourlyView.HourlyScroller, HourlyView.TempGraphLine, HourlyView.ActiveTempDot, HourlyView.ScaleCanvas, ref _isScaleVisible, ViewModel.CurrentWeather?.HourlyData);
		}

		internal void HourlyView_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			GraphSyncService.AlignScrollGrid(e, HourlyView.HourlyScrollGrid);
		}

		private void SummaryScroller_ScrollChanged(object sender, ScrollChangedEventArgs e)
		{
			SummaryScrollBar.Value = e.VerticalOffset;
		}

		private void TrackVisibleAlerts()
		{
			if (AlertView.Visibility != Visibility.Visible) return;
			bool changed = false;
			System.Collections.Generic.HashSet<string> currentlyVisible = [];
			TrackAlertsRecursive(AlertScroller, currentlyVisible, ref changed);

			System.Collections.Generic.List<string> keysToRemove = [];
			foreach (var key in _alertVisibleSince.Keys)
			{
				if (!currentlyVisible.Contains(key))
				{
					keysToRemove.Add(key);
				}
			}
			foreach (var key in keysToRemove)
			{
				_alertVisibleSince.Remove(key);
			}

			if (changed) SettingsManager.Save();
		}

		private void TrackAlertsRecursive(DependencyObject parent, System.Collections.Generic.HashSet<string> currentlyVisible, ref bool changed)
		{
			for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
			{
				var child = VisualTreeHelper.GetChild(parent, i);
				if (child is FrameworkElement element && element.Name == "ClearAlertButton" && element.Tag is SkyPeek.WeatherAlert alert && alert.IsNew)
				{
					if (element.IsLoaded)
					{
						GeneralTransform transform = element.TransformToAncestor(AlertScroller);
						Rect bounds = transform.TransformBounds(new(0, 0, element.ActualWidth, element.ActualHeight));
						Rect viewport = new(0, 0, AlertScroller.ViewportWidth, AlertScroller.ViewportHeight);
						if (viewport.IntersectsWith(bounds))
						{
							currentlyVisible.Add(alert.Id);
							if (!_alertVisibleSince.TryGetValue(alert.Id, out DateTime visibleSince))
							{
								_alertVisibleSince[alert.Id] = DateTime.Now;
							}
							else if ((DateTime.Now - visibleSince).TotalSeconds >= 0.5)
							{
								alert.IsNew = false;
								AppConfig.Current.SeenBackgroundAlertIds.Add(alert.Id);
								ViewModel.AlertState.NewAlertCount--;
								if (ViewModel.AlertState.NewAlertCount <= 0)
								{
									ViewModel.AlertState.HasNewAlerts = false;
									if (NewAlertCounterText.IsFocused || NewAlertCounterText.IsKeyboardFocused)
									{
										AlertScroller.Focus();
										Dispatcher.BeginInvoke(new Action(() => SpatialNavigationBehavior.UpdateAlertButtonFocus(this)), DispatcherPriority.Input);
									}
								}
								changed = true;
								_alertVisibleSince.Remove(alert.Id);
							}
						}
					}
				}
				TrackAlertsRecursive(child, currentlyVisible, ref changed);
			}
		}

		internal void NewAlertCounter_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			FrameworkElement? nextNewAlert = null;
			FindNextNewAlertElement(AlertScroller, ref nextNewAlert);

			if (nextNewAlert != null && nextNewAlert.Tag is SkyPeek.WeatherAlert alert)
			{
				GeneralTransform transform = nextNewAlert.TransformToAncestor(AlertScroller);
				Point elementPosition = transform.Transform(new Point(0, 0));
				double targetOffset = AlertScroller.VerticalOffset + elementPosition.Y - 10;

				alert.IsNew = false;
				AppConfig.Current.SeenBackgroundAlertIds.Add(alert.Id);
				ViewModel.AlertState.NewAlertCount--;

				if (ViewModel.AlertState.NewAlertCount <= 0)
				{
					ViewModel.AlertState.HasNewAlerts = false;
					if (NewAlertCounterText.IsFocused || NewAlertCounterText.IsKeyboardFocused)
					{
						AlertScroller.Focus();
						Dispatcher.BeginInvoke(new Action(() => SpatialNavigationBehavior.UpdateAlertButtonFocus(this)), DispatcherPriority.Input);
					}
				}

				SettingsManager.Save();
				_alertVisibleSince.Remove(alert.Id);

				if (SmoothScrollBehavior.GetIsEnabled(AlertScroller)) AlertScroller.SmoothScrollTo(targetOffset);
				else AlertScroller.ScrollToVerticalOffset(targetOffset);
			}
		}

		private static void FindNextNewAlertElement(DependencyObject parent, ref FrameworkElement? foundElement)
		{
			if (foundElement != null) return;
			for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
			{
				var child = VisualTreeHelper.GetChild(parent, i);
				if (child is FrameworkElement element && element.Name == "AlertItemContainer" && element.Tag is SkyPeek.WeatherAlert alert && alert.IsNew)
				{
					foundElement = element;
					return;
				}
				FindNextNewAlertElement(child, ref foundElement);
			}
		}

		internal void AlertScroller_ScrollChanged(object sender, ScrollChangedEventArgs e)
		{
			SpatialNavigationBehavior.UpdateAlertButtonFocus(this);
			if (AlertScrollBar is { } scrollBar) scrollBar.Value = e.VerticalOffset;
		}

		internal void AlertScrollBar_Scroll(object sender, System.Windows.Controls.Primitives.ScrollEventArgs e)
		{
			AlertScroller.ScrollToVerticalOffset(e.NewValue);
		}

		private void AlertView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
		{
			if (e.NewValue is bool isVisible && isVisible)
			{
				_alertVisibilityTimer.Start();
				Dispatcher.BeginInvoke(() => TrackVisibleAlerts(), System.Windows.Threading.DispatcherPriority.Loaded);
			}
			else
			{
				_alertVisibilityTimer.Stop();
				_alertVisibleSince.Clear();
			}
		}

		private void NewAlertCounterText_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
		{
			if (_isKeyboardNavRevealed && ViewModel.CurrentActiveView == "Alerts")
			{
				if (e.NewFocus == null || e.NewFocus == this)
				{
					e.Handled = true;
					AlertScroller.Focus();
					Dispatcher.BeginInvoke(new Action(() => SpatialNavigationBehavior.UpdateAlertButtonFocus(this)), DispatcherPriority.Input);
				}
			}
		}

		internal void SummaryScrollBar_Scroll(object sender, System.Windows.Controls.Primitives.ScrollEventArgs e)
		{
			SummaryScroller.ScrollToVerticalOffset(e.NewValue);
		}

		internal void ScrollBar_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			if (sender is ScrollBar scrollBar && scrollBar.FindVisualParent<ScrollViewer>() is ScrollViewer sv)
			{
				if (scrollBar.Orientation == Orientation.Horizontal)
				{
					// Hourly and 10-Day cards use exactly a 240px scroll step (170px width + 70px margins)
					double step = 240.0;
					double targetOffset = Math.Round(sv.HorizontalOffset / step) * step;

					// Clamp to bounds to prevent edge bouncing
					if (targetOffset < 0) targetOffset = 0;
					if (targetOffset > sv.ScrollableWidth) targetOffset = sv.ScrollableWidth;

					sv.SmoothScrollTo(targetOffset);
				}
			}
		}

		internal void ScrollBar_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			if (sender is ScrollBar scrollBar && scrollBar.Template.FindName("PART_Track", scrollBar) is Track track)
			{
				if (track.Thumb != null && track.Thumb.IsMouseOver) return;

				// 1. Intercept the native < and > arrows to page cleanly instead of teleporting to the extremes
				if (e.OriginalSource is DependencyObject src && src.FindVisualParent<RepeatButton>() is RepeatButton btn)
				{
					if (btn.Command == ScrollBar.LineLeftCommand || btn.Command == ScrollBar.LineRightCommand)
					{
						if (scrollBar.FindVisualParent<ScrollViewer>() is ScrollViewer sv)
						{
							double step = 240.0;
							double current = sv.HorizontalOffset;
							double targetOffset = btn.Command == ScrollBar.LineLeftCommand
								? Math.Round((current - step) / step) * step
								: Math.Round((current + step) / step) * step;

							if (targetOffset < 0) targetOffset = 0;
							if (targetOffset > sv.ScrollableWidth) targetOffset = sv.ScrollableWidth;

							sv.SmoothScrollTo(targetOffset);
						}
						e.Handled = true;
						return;
					}
				}

				double val = track.ValueFromPoint(e.GetPosition(track));
				if (!double.IsNaN(val) && !double.IsInfinity(val))
				{
					// 2. Synchronize value with the correct parent layout
					if (scrollBar.Name == "HourlyAgendaScrollBar") HourlyView.HourlyAgendaScroller.ScrollToVerticalOffset(val);
					else if (scrollBar.Name == "AgendaScrollBar") TenDayView.AgendaScroller.ScrollToVerticalOffset(val);
					else if (scrollBar.Name == "SummaryScrollBar") SummaryScroller.ScrollToVerticalOffset(val);
					else if (scrollBar.Name == "AlertScrollBar") AlertScroller.ScrollToVerticalOffset(val);
					else if (scrollBar.Name == "SavedLocationsScrollBar") SavedLocationsScroller.ScrollToVerticalOffset(val);
					else if (scrollBar.FindVisualParent<ScrollViewer>() is ScrollViewer sv)
					{
						// Embedded scrollbars are slaves to the ScrollViewer. We MUST command the ScrollViewer itself, or the drag linkage breaks.
						if (scrollBar.Orientation == Orientation.Horizontal) sv.ScrollToHorizontalOffset(val);
						else sv.ScrollToVerticalOffset(val);
					}
					else scrollBar.Value = val;

					e.Handled = true;

					// 3. Force UI to physically place the thumb at the new coordinate
					scrollBar.UpdateLayout();

					// 4. Pass physical mouse capture to the thumb to allow continued dragging
					if (track.Thumb != null)
					{
						var args = new MouseButtonEventArgs(e.MouseDevice, e.Timestamp, MouseButton.Left)
						{
							RoutedEvent = UIElement.MouseLeftButtonDownEvent,
							Source = track.Thumb
						};
						track.Thumb.RaiseEvent(args);
					}
				}
			}
		}

		private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
		{
			// Any mouse click instantly drops keyboard navigation visual states
			_isKeyboardNavRevealed = false;
			ViewModel.IsWeatherAnimationSelected = false;
			ViewModel.IsLocationMenuSelected = false;
			ViewModel.IsAlertIconSelected = false;

			if (e.OriginalSource is DependencyObject clickedElement)
			{
				bool isInteractiveElement = false;
				DependencyObject? current = clickedElement;

				while (current is not null)
				{
					// Escape instantly on dropdowns and popups without modifying focus
					if (current is ComboBoxItem || current is System.Windows.Controls.Primitives.Popup || current.GetType().Name == "PopupRoot")
					{
						return;
					}

					// Check if we hit an explicitly focusable component
					if (current is TextBox || current is ComboBox || current is Slider || current is System.Windows.Controls.Primitives.ButtonBase || current is System.Windows.Controls.Primitives.Thumb)
					{
						isInteractiveElement = true;
						break;
					}

					// Safe traversal: prefer VisualTree, fallback to LogicalTree
					if (current is Visual || current is System.Windows.Media.Media3D.Visual3D)
					{
						current = VisualTreeHelper.GetParent(current) ?? LogicalTreeHelper.GetParent(current);
					}
					else
					{
						current = LogicalTreeHelper.GetParent(current);
					}
				}

				// If the user clicked dead space (like the background grid), wipe the keyboard cache.
				if (!isInteractiveElement)
				{
					System.Windows.Input.FocusManager.SetFocusedElement(this, this);
					System.Windows.Input.Keyboard.ClearFocus();
					this.Focus(); // CRITICAL: Bounce focus back to window so keyboard isn't permanently disabled
				}
			}

			Point p = e.GetPosition(this);

			// The grid has a 25px margin. If the user clicks in this outer 25px ring (the shadow area), manually close the window.
			if (p.X < 25 || p.X > this.Width - 25 || p.Y < 25 || p.Y > this.Height - 25)
			{
				SlideOut();
				e.Handled = true;
			}
		}

		private void Window_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
		{
			_isKeyboardNavRevealed = false;
			ViewModel.IsWeatherAnimationSelected = false;
			ViewModel.IsLocationMenuSelected = false;
			ViewModel.IsAlertIconSelected = false;

			if (LocManCountryInput.IsDropDownOpen || LocManStateInput.IsDropDownOpen || CitySuggestionsPopup.IsOpen || CurrentView.LocationComboBox.IsDropDownOpen)
			{
				return;
			}

			// Prevent clearing focus if the user is actively holding the mouse button (e.g., highlighting text)
			// or if a RichTextBox currently has focus (so existing highlights aren't lost when scrolling).
			if (Mouse.LeftButton == MouseButtonState.Pressed || Keyboard.FocusedElement is RichTextBox)
			{
				return;
			}

			System.Windows.Input.FocusManager.SetFocusedElement(this, this);
			System.Windows.Input.Keyboard.ClearFocus();
			this.Focus();
		}

		protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
		{
			e.Cancel = true;
			SlideOut();
		}

		public DateTime LastClosed { get; private set; } = DateTime.MinValue;

		private async void Window_Deactivated(object sender, EventArgs e)
		{
			if (IsPreviewMode) return;

			if (this.Opacity >= 0.9)
			{
				LastClosed = DateTime.Now;
				SlideOut();
			}

			await System.Threading.Tasks.Task.Delay(250);
			if (!this.IsActive && this.IsOpen)
			{
				LastClosed = DateTime.Now;
				SlideOut();
			}
		}

		private void LocationManagerView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
		{
			if (e.NewValue is bool isVisible && !isVisible)
			{
				SavedLocationsScroller.ScrollToHome();
			}
		}

		private void LocManComboBox_DropDownOpened(object sender, EventArgs e)
		{
			if (sender is ComboBox cb && cb.Template.FindName("Popup", cb) is System.Windows.Controls.Primitives.Popup popup)
			{
				if (popup.Child is DependencyObject popupChild)
				{
					if (popupChild.FindVisualChild<ScrollViewer>() is ScrollViewer sv)
					{
						if (!SmoothScrollBehavior.GetIsEnabled(sv))
						{
							// Override Logical Scrolling so physical pixel math works smoothly
							sv.CanContentScroll = false;

							// Buffer via Dispatcher to ensure items have rendered their physical bounds
							Dispatcher.BeginInvoke(new Action(() =>
							{
								double stepSize = 26.0; // Fallback estimate
								if (cb.ItemContainerGenerator.ContainerFromIndex(0) is FrameworkElement firstItem && firstItem.ActualHeight > 0)
								{
									// Multiply user scroll ticks exactly against the true rendered height of an item
									stepSize = firstItem.ActualHeight;
								}

								SmoothScrollBehavior.SetScrollStep(sv, stepSize);
								SmoothScrollBehavior.SetIsEnabled(sv, true);
							}), System.Windows.Threading.DispatcherPriority.Loaded);
						}
					}
				}
			}
		}
		private static TaskbarPosition GetTaskbarPosition()
		{
			if (SystemParameters.WorkArea.Top > 0) return TaskbarPosition.Top;
			if (SystemParameters.WorkArea.Left > 0) return TaskbarPosition.Left;
			if (SystemParameters.WorkArea.Right < SystemParameters.PrimaryScreenWidth) return TaskbarPosition.Right;
			return TaskbarPosition.Bottom;
		}

		public void SlideIn(bool centeredMode = false)
		{
			if (_isAnimating) return;
			_isAnimating = true;
			_isCenteredMode = centeredMode;
			_isKeyboardNavRevealed = false;

			if (HourlyView.HourlyScroller.HorizontalOffset > 0)
			{
				HourlyView.HourlyScroller.StopSmoothScrolling();
				HourlyView.HourlyScroller.ScrollToHorizontalOffset(0);
			}
			if (TenDayView.DailyScroller.HorizontalOffset > 0)
			{
				TenDayView.DailyScroller.StopSmoothScrolling();
				TenDayView.DailyScroller.ScrollToHorizontalOffset(0);
			}
			if (TenDayView.AgendaScroller.VerticalOffset > 0)
			{
				TenDayView.AgendaScroller.StopSmoothScrolling();
				TenDayView.AgendaScroller.ScrollToVerticalOffset(0);
			}
			if (HourlyView.HourlyAgendaScroller.VerticalOffset > 0)
			{
				HourlyView.HourlyAgendaScroller.StopSmoothScrolling();
				HourlyView.HourlyAgendaScroller.ScrollToVerticalOffset(0);
			}
			if (AlertScroller.VerticalOffset > 0)
			{
				AlertScroller.StopSmoothScrolling();
				AlertScroller.ScrollToVerticalOffset(0);
			}
			if (SavedLocationsScroller.VerticalOffset > 0)
			{
				SavedLocationsScroller.StopSmoothScrolling();
				SavedLocationsScroller.ScrollToVerticalOffset(0);
			}
			if (SummaryScroller.VerticalOffset > 0)
			{
				SummaryScroller.StopSmoothScrolling();
				SummaryScroller.ScrollToVerticalOffset(0);
			}

			HourlyView.ScaleCanvas.BeginAnimation(UIElement.OpacityProperty, null);
			HourlyView.ScaleCanvas.Opacity = 1.0;
			_isScaleVisible = true;

			TenDayView.DailyScaleCanvas.BeginAnimation(UIElement.OpacityProperty, null);
			TenDayView.DailyScaleCanvas.Opacity = 1.0;
			_isDailyScaleVisible = true;

			if (ViewModel.IsLoading)
			{
				LoadingAnimation.PlayAnimation();
			}

			if (ViewModel.CurrentWeather?.HourlyData?.Count > 0)
			{
				GraphService.DrawHourlyGraph(HourlyView.GraphCanvas, HourlyView.ScaleCanvas, HourlyView.TempGraphLine, HourlyView.ActiveTempDot, ViewModel.CurrentWeather.HourlyData);
				GraphSyncService.SyncHourlyDot(HourlyView.HourlyScroller, HourlyView.TempGraphLine, HourlyView.ActiveTempDot, HourlyView.ScaleCanvas, ref _isScaleVisible, ViewModel.CurrentWeather.HourlyData);
			}

			if (ViewModel.CurrentWeather?.DailyData?.Count > 0)
			{
				GraphService.DrawDailyGraph(TenDayView.DailyGraphCanvas, TenDayView.DailyScaleCanvas, TenDayView.TempRibbonPolygon, TenDayView.HighTempGraphLine, TenDayView.LowTempGraphLine, TenDayView.ActiveHighTempDot, TenDayView.ActiveLowTempDot, ViewModel.CurrentWeather.DailyData);
				GraphSyncService.SyncDailyDots(TenDayView.DailyScroller, TenDayView.HighTempGraphLine, TenDayView.LowTempGraphLine, TenDayView.ActiveHighTempDot, TenDayView.ActiveLowTempDot, TenDayView.DailyScaleCanvas, ref _isDailyScaleVisible, ViewModel.CurrentWeather.DailyData);
			}

			this.Opacity = 0;
			this.IsOpen = true;
			this.Show();
			this.Activate();

			var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
			NativeMethods.SetForegroundWindow(hwnd);

			this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, new Action(() =>
			{
				this.Focus();
				System.Windows.Input.Keyboard.Focus(this);
			}));

			var taskbarPos = GetTaskbarPosition();
			var workArea = SystemParameters.WorkArea;

			double cursorX = 0, cursorY = 0;
			if (Application.Current.MainWindow is MainWindow mw)
			{
				var trayPos = mw.GetTrayIconScreenPosition();
				cursorX = trayPos.X;
				cursorY = trayPos.Y;
			}
			else
			{
				GetCursorPosition(out POINT pos);
				DpiScale dpi = VisualTreeHelper.GetDpi(this);
				cursorX = pos.X / dpi.DpiScaleX;
				cursorY = pos.Y / dpi.DpiScaleY;
			}

			double endX = 0, endY = 0;

			// Account for the invisible 25px shadow margin to create a true 12px visual gap from the taskbar
			double shadowMargin = 25;
			double visualGap = 12;
			double offset = shadowMargin - visualGap;

			double slideOffset = 30;
			string popupPos = AppConfig.Current.PopupPosition ?? "Bottom";

			double actW = this.ActualWidth == 0 ? 305 : this.ActualWidth;
			double actH = this.ActualHeight == 0 ? 440 : this.ActualHeight;

			if (_isCenteredMode)
			{
				endX = (SystemParameters.PrimaryScreenWidth / 2) - (actW / 2);
				if (popupPos == "Top") endY = workArea.Top - offset;
				else endY = workArea.Bottom - actH + offset;
			}
			else if (taskbarPos == TaskbarPosition.Bottom)
			{
				endX = cursorX - (actW / 2);
				endY = workArea.Bottom - actH + offset;
			}
			else if (taskbarPos == TaskbarPosition.Top)
			{
				endX = cursorX - (actW / 2);
				endY = workArea.Top - offset;
			}
			else if (taskbarPos == TaskbarPosition.Right)
			{
				endX = workArea.Right - actW + offset;
				endY = cursorY - (actH / 2);
			}
			else if (taskbarPos == TaskbarPosition.Left)
			{
				endX = workArea.Left - offset;
				endY = cursorY - (actH / 2);
			}

			// Clamp horizontal bounds
			if (endX + actW > SystemParameters.VirtualScreenWidth + offset) endX = SystemParameters.VirtualScreenWidth - actW + offset;
			if (endX < SystemParameters.VirtualScreenLeft - offset) endX = SystemParameters.VirtualScreenLeft - offset;

			// Clamp vertical bounds
			if (endY + actH > SystemParameters.VirtualScreenHeight + offset) endY = SystemParameters.VirtualScreenHeight - actH + offset;
			if (endY < SystemParameters.VirtualScreenTop - offset) endY = SystemParameters.VirtualScreenTop - offset;

			this.Left = endX;
			this.Top = endY;
			this.UpdateLayout();

			double transStartX = 0;
			double transStartY = 0;

			if (_isCenteredMode) transStartY = (popupPos == "Top") ? -slideOffset : slideOffset;
			else if (taskbarPos == TaskbarPosition.Bottom) transStartY = slideOffset;
			else if (taskbarPos == TaskbarPosition.Top) transStartY = -slideOffset;
			else if (taskbarPos == TaskbarPosition.Right) transStartX = slideOffset;
			else if (taskbarPos == TaskbarPosition.Left) transStartX = -slideOffset;

			if (AppConfig.Current.DisableAnimations)
			{
				this.BeginAnimation(UIElement.OpacityProperty, null);
				RootTransform.BeginAnimation(TranslateTransform.XProperty, null);
				RootTransform.BeginAnimation(TranslateTransform.YProperty, null);

				RootTransform.X = 0;
				RootTransform.Y = 0;
				this.Opacity = 1.0;
				_isAnimating = false;
			}
			else
			{
				this.PlayWindowEntrance(RootTransform, transStartX, transStartY, () =>
				{
					_isAnimating = false;
					if (!this.IsActive && this.IsOpen) SlideOut();
				});
			}
		}

		private bool _isPreviewClosing = false;
		public bool IsPreviewMode = false;

		public void PreviewSettingsShow(Window settingsWindow)
		{
			IsPreviewMode = true;
			this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, new Action(() =>
			{
				this.Left = settingsWindow.Left + settingsWindow.ActualWidth - 10;
				this.Top = settingsWindow.Top + (settingsWindow.ActualHeight / 2) - (this.ActualHeight / 2);
			}));

			if (this.IsOpen && !_isPreviewClosing) return;
			_isPreviewClosing = false;

			if (AppConfig.Current.DisableAnimations)
			{
				RootTransform.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, null);
				RootTransform.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, null);
				this.BeginAnimation(System.Windows.UIElement.OpacityProperty, null);

				RootTransform.X = 0;
				RootTransform.Y = 0;
				this.Opacity = 1.0;
				this.ShowActivated = false;
				this.IsOpen = true;
				this.Show();
				_isAnimating = false;
				return;
			}

			RootTransform.X = -30;
			RootTransform.Y = 0;
			this.Opacity = 0.0;

			this.ShowActivated = false;
			this.IsOpen = true;
			this.Show();
			_isAnimating = true;

			this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, new Action(() =>
			{
				this.PlayWindowEntrance(RootTransform, -30, 0, () => _isAnimating = false);
			}));
		}

		public void PreviewSettingsHide()
		{
			IsPreviewMode = false;
			if (!this.IsOpen) return;
			_isAnimating = true;
			_isPreviewClosing = true;

			void CleanupState()
			{
				_isAnimating = false;
				_isPreviewClosing = false;
				this.IsOpen = false;
				this.Hide();
			}

			if (AppConfig.Current.DisableAnimations)
			{
				CleanupState();
				return;
			}

			System.Windows.Media.Animation.DoubleAnimation animX = new() { From = 0, To = -30, Duration = TimeSpan.FromMilliseconds(200), EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseIn } };
			System.Windows.Media.Animation.DoubleAnimation animY = new() { From = 0, To = 0, Duration = TimeSpan.FromMilliseconds(200), EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseIn } };
			System.Windows.Media.Animation.DoubleAnimation animOpacity = new() { From = 1.0, To = 0.0, Duration = TimeSpan.FromMilliseconds(200), EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseIn } };

			animX.Completed += async (s, e) =>
			{
				if (!_isPreviewClosing) return;
				await System.Threading.Tasks.Task.Delay(30);
				if (!_isPreviewClosing) return;

				CleanupState();
			};

			RootTransform.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, animX);
			RootTransform.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, animY);
			this.BeginAnimation(System.Windows.UIElement.OpacityProperty, animOpacity);
		}

		public void SlideOut()
		{
			IsPreviewMode = false;
			if (_isAnimating || !this.IsOpen) return;
			_isAnimating = true;

			string popupPos = AppConfig.Current.PopupPosition ?? "Bottom";

			if (AppConfig.Current.DisableAnimations)
			{
				this.Opacity = 0.0;
				this.IsOpen = false;
				this.Hide();

				_isAnimating = false;

				_ = Dispatcher.InvokeAsync(async () =>
				{
					await System.Threading.Tasks.Task.Delay(2000);
					if (this.IsOpen) return;

					string defaultView = AppConfig.Current.DefaultView ?? "Current";
					bool neededRefresh = _needsWeatherRefresh;

					if (IsViewingTemporaryLocation || _needsWeatherRefresh)
					{
						IsViewingTemporaryLocation = false;
						_needsWeatherRefresh = false;
						if (Application.Current.MainWindow is MainWindow mw)
						{
							if (neededRefresh) await mw.RefreshAllLocationsCacheAsync(true);
							else await mw.FetchWeatherByLocation(AppConfig.Current.City, AppConfig.Current.State, AppConfig.Current.Country);
						}
					}
					ApplyDefaultView(defaultView);
				}, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
				return;
			}

			var taskbarPos = GetTaskbarPosition();
			double slideOffset = 30;

			double transEndX = 0;
			double transEndY = 0;

			if (_isCenteredMode) transEndY = (popupPos == "Top") ? -slideOffset : slideOffset;
			else if (taskbarPos == TaskbarPosition.Bottom) transEndY = slideOffset;
			else if (taskbarPos == TaskbarPosition.Top) transEndY = -slideOffset;
			else if (taskbarPos == TaskbarPosition.Right) transEndX = slideOffset;
			else if (taskbarPos == TaskbarPosition.Left) transEndX = -slideOffset;

			this.PlayWindowExit(RootTransform, transEndX, transEndY, async () =>
			{
				await System.Threading.Tasks.Task.Delay(30);

				_isAnimating = false;

				System.Windows.Input.FocusManager.SetFocusedElement(this, this);
				System.Windows.Input.Keyboard.ClearFocus();
				this.IsOpen = false;
				this.Hide();

				await Dispatcher.InvokeAsync(async () =>
				{
					await System.Threading.Tasks.Task.Delay(2000);
					if (this.IsOpen) return;

					string defaultView = AppConfig.Current.DefaultView ?? "Current";
					bool neededRefresh = _needsWeatherRefresh;

					if (IsViewingTemporaryLocation || _needsWeatherRefresh)
					{
						IsViewingTemporaryLocation = false;
						_needsWeatherRefresh = false;
						if (Application.Current.MainWindow is MainWindow mw)
						{
							if (neededRefresh) await mw.RefreshAllLocationsCacheAsync(true);
							else await mw.FetchWeatherByLocation(AppConfig.Current.City, AppConfig.Current.State, AppConfig.Current.Country);
						}
					}
					ApplyDefaultView(defaultView);
				}, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
			});
		}

		protected override void OnPreviewKeyDown(KeyEventArgs e)
		{
#if DEBUG
			ProcessDebugKeys(e);
#endif
			base.OnPreviewKeyDown(e);
		}
	}
}