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

using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace SkyPeek
{
	/// <summary>
	/// Interaction logic for TrayMenu.xaml
	/// </summary>
	[System.Runtime.Versioning.SupportedOSPlatform("windows10.0.19041.0")]
	public partial class TrayMenu : Window
	{
		public DateTime LastClosed { get; private set; } = DateTime.MinValue;
		public DateTime LastOpened { get; private set; } = DateTime.MinValue;
		private double _lastStartX = 0;

		protected override void OnPreviewMouseDown(System.Windows.Input.MouseButtonEventArgs e)
		{
			base.OnPreviewMouseDown(e);
			System.Windows.Point p = e.GetPosition(this);
			if (p.X < 0 || p.X > this.ActualWidth || p.Y < 0 || p.Y > this.ActualHeight)
			{
				System.Windows.Input.Mouse.Capture(null);
				this.SoftHide();
			}
		}

		protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
		{
			base.OnPreviewKeyDown(e);

			if (e.Key == System.Windows.Input.Key.Up || e.Key == System.Windows.Input.Key.Down)
			{
				e.Handled = true;
				if (!SettingsButton.IsKeyboardFocused && !ClearAlertsButton.IsKeyboardFocused && !ExitButton.IsKeyboardFocused && !UpdateButton.IsKeyboardFocused && !DismissUpdateButton.IsKeyboardFocused)
				{
					if (e.Key == System.Windows.Input.Key.Up) ExitButton.Focus();
					else if (UpdateButton.Visibility == Visibility.Visible) UpdateButton.Focus();
					else SettingsButton.Focus();
				}
				else if (System.Windows.Input.Keyboard.FocusedElement == UpdateButton || System.Windows.Input.Keyboard.FocusedElement == DismissUpdateButton)
				{
					if (e.Key == System.Windows.Input.Key.Up) ExitButton.Focus();
					else SettingsButton.Focus();
				}
				else if (System.Windows.Input.Keyboard.FocusedElement == SettingsButton)
				{
					if (e.Key == System.Windows.Input.Key.Up)
					{
						if (UpdateButton.Visibility == Visibility.Visible) UpdateButton.Focus();
						else ExitButton.Focus();
					}
					else if (ClearAlertsButton.IsEnabled) ClearAlertsButton.Focus();
					else ExitButton.Focus();
				}
				else if (System.Windows.Input.Keyboard.FocusedElement == ClearAlertsButton)
				{
					if (e.Key == System.Windows.Input.Key.Up) SettingsButton.Focus();
					else ExitButton.Focus();
				}
				else
				{
					if (e.Key == System.Windows.Input.Key.Up)
					{
						if (ClearAlertsButton.IsEnabled) ClearAlertsButton.Focus();
						else SettingsButton.Focus();
					}
					else
					{
						if (UpdateButton.Visibility == Visibility.Visible) UpdateButton.Focus();
						else SettingsButton.Focus();
					}
				}
			}
			else if (e.Key == System.Windows.Input.Key.Right)
			{
				if (System.Windows.Input.Keyboard.FocusedElement == UpdateButton)
				{
					e.Handled = true;
					DismissUpdateButton.Focus();
				}
				else if (System.Windows.Input.Keyboard.FocusedElement == DismissUpdateButton)
				{
					e.Handled = true;
				}
			}
			else if (e.Key == System.Windows.Input.Key.Left)
			{
				if (System.Windows.Input.Keyboard.FocusedElement == DismissUpdateButton)
				{
					e.Handled = true;
					UpdateButton.Focus();
				}
			}
			else if (e.Key == System.Windows.Input.Key.Enter || e.Key == System.Windows.Input.Key.Space)
			{
				if (System.Windows.Input.Keyboard.FocusedElement == UpdateButton)
				{
					e.Handled = true;
					UpdateButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
				}
				else if (System.Windows.Input.Keyboard.FocusedElement == DismissUpdateButton)
				{
					e.Handled = true;
					DismissUpdateButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
				}
			}
			else if (e.Key == System.Windows.Input.Key.Escape || e.Key == System.Windows.Input.Key.Back)
			{
				e.Handled = true;
				SoftHide();
			}
		}
		private double _lastStartY = 15;

		public event EventHandler? RequestClose;

		protected override void OnSourceInitialized(EventArgs e)
		{
			base.OnSourceInitialized(e);
			this.HideFromAltTab();
		}

		private void Window_Deactivated(object sender, EventArgs e)
		{
			if (this.Opacity >= 0.9)
			{
				this.SoftHide();
			}
		}

		public void RefreshTheme()
		{
			ThemeHelper.ApplyTrayTheme(this, GradientCenter, GradientEdge, WindowBorder, WindowShadow);
		}

		public TrayMenu()
		{
			InitializeComponent();
			ThemeHelper.ApplyTrayTheme(this, GradientCenter, GradientEdge, WindowBorder, WindowShadow);

			this.Loaded += (s, e) =>
				Microsoft.Win32.SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;

			this.Unloaded += (s, e) =>
				Microsoft.Win32.SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;

			this.DpiChanged += (s, e) =>
			{
				System.Diagnostics.Debug.WriteLine($"\n*** [TrayMenu.DpiChanged] EVENT FIRED ***");
				System.Diagnostics.Debug.WriteLine($"*** Old DPI Scale: X={e.OldDpi.DpiScaleX}, Y={e.OldDpi.DpiScaleY}");
				System.Diagnostics.Debug.WriteLine($"*** New DPI Scale: X={e.NewDpi.DpiScaleX}, Y={e.NewDpi.DpiScaleY}\n");
			};
		}

		private void SystemEvents_UserPreferenceChanged(object sender, Microsoft.Win32.UserPreferenceChangedEventArgs e)
		{
			if (e.Category == Microsoft.Win32.UserPreferenceCategory.Color || e.Category == Microsoft.Win32.UserPreferenceCategory.General)
			{
				Application.Current.Dispatcher.InvokeAsync(async () =>
				{
					ThemeHelper.ApplyTrayTheme(this, GradientCenter, GradientEdge, WindowBorder, WindowShadow);
					await System.Threading.Tasks.Task.Delay(50);
					this.Background = System.Windows.Media.Brushes.Transparent;
				});
			}
		}

		public void ShowMenu()
		{
			if (MainWindow.IsUpdateAvailable)
			{
				UpdateButton.Visibility = Visibility.Visible;
			}
			else
			{
				UpdateButton.Visibility = Visibility.Collapsed;
			}

			bool hasAlerts = false;
			if (AppConfig.Current.EnableWeatherAlerts && Application.Current.MainWindow is MainWindow)
			{
				hasAlerts = AppConfig.Current.SavedLocations.Any(loc => loc.HasActiveAlert);
			}

			ClearAlertsButton.IsEnabled = hasAlerts;
			ClearAlertsButton.Opacity = hasAlerts ? 1.0 : 0.2;

			LastOpened = DateTime.Now;
			this.BeginAnimation(Window.OpacityProperty, null);
			MenuTranslateTransform.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, null);
			MenuTranslateTransform.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, null);

			this.Opacity = 0;

			NativeMethods.GetCursorPosition(out POINT cursorPos);
			IntPtr hMonitor = NativeMethods.MonitorFromPoint(cursorPos, NativeMethods.MONITOR_DEFAULTTONEAREST);
			double scaleX = 1.0, scaleY = 1.0;

			if (NativeMethods.GetDpiForMonitor(hMonitor, NativeMethods.MDT_EFFECTIVE_DPI, out uint dpiX, out uint dpiY) == 0)
			{
				scaleX = dpiX / 96.0;
				scaleY = dpiY / 96.0;
			}
			else
			{
				var windowDpi = System.Windows.Media.VisualTreeHelper.GetDpi(this);
				scaleX = windowDpi.DpiScaleX;
				scaleY = windowDpi.DpiScaleY;
			}

			double cursorX = 0, cursorY = 0;
			if (Application.Current.MainWindow is MainWindow mw)
			{
				var trayPos = mw.GetTrayIconScreenPosition();
				cursorX = trayPos.X;
				cursorY = trayPos.Y;
			}
			else
			{
				cursorX = cursorPos.X / scaleX;
				cursorY = cursorPos.Y / scaleY;
			}

			NativeMethods.SystemParametersInfo(NativeMethods.SPI_GETWORKAREA, 0, out RECT rectWorkArea, 0);
			Rect workArea = new Rect(rectWorkArea.Left / scaleX, rectWorkArea.Top / scaleY, (rectWorkArea.Right - rectWorkArea.Left) / scaleX, (rectWorkArea.Bottom - rectWorkArea.Top) / scaleY);

			// Explicitly subtract the shadow margin from the visual gap to match SecondaryViews math
			double shadowMargin = 20;
			double visualGap = Environment.OSVersion.Version.Build >= 22000 ? 12 : 0;
			double adjustedMargin = shadowMargin - visualGap;

			this.UpdateLayout();
			double width = this.ActualWidth == 0 ? 244 : this.ActualWidth;
			double height = this.ActualHeight == 0 ? 174 : this.ActualHeight;

			double endX = cursorX - (width / 2);
			double endY = cursorY - (height / 2);

			double startX = 0;
			double startY = 0;
			double offset = 15;

			double primaryWidth = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSCREEN) / scaleX;

			if (workArea.Top > 0)
			{
				endY = workArea.Top - adjustedMargin;
				startY = -offset;
			}
			else if (workArea.Left > 0)
			{
				endX = workArea.Left - adjustedMargin;
				startX = -offset;
			}
			else if (workArea.Right < primaryWidth)
			{
				endX = workArea.Right - width + adjustedMargin;
				startX = offset;
			}
			else
			{
				endY = workArea.Bottom - height + adjustedMargin;
				startY = offset;
			}

			_lastStartX = startX;
			_lastStartY = startY;

			if (endX < workArea.Left - adjustedMargin) endX = workArea.Left - adjustedMargin;
			if (endX + width > workArea.Right + adjustedMargin) endX = workArea.Right - width + adjustedMargin;
			if (endY < workArea.Top - adjustedMargin) endY = workArea.Top - adjustedMargin;
			if (endY + height > workArea.Bottom + adjustedMargin) endY = workArea.Bottom - height + adjustedMargin;

			this.Left = endX;
			this.Top = endY;

			this.Show();
			this.Activate();

			var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
			NativeMethods.SetForegroundWindow(hwnd);

			System.Windows.Input.FocusManager.SetFocusedElement(this, this);
			System.Windows.Input.Keyboard.ClearFocus();
			this.Focus();

			if (AppConfig.Current.DisableAnimations)
			{
				this.Opacity = 1.0;
				MenuTranslateTransform.X = 0;
				MenuTranslateTransform.Y = 0;
				return;
			}

			var fadeIn = new System.Windows.Media.Animation.DoubleAnimation(0, 1, System.TimeSpan.FromMilliseconds(150))
			{
				EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
			};
			this.BeginAnimation(Window.OpacityProperty, fadeIn);

			if (startX != 0)
			{
				var slideX = new System.Windows.Media.Animation.DoubleAnimation(startX, 0, System.TimeSpan.FromMilliseconds(150))
				{
					EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
				};
				MenuTranslateTransform.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, slideX);
			}
			else
			{
				var slideY = new System.Windows.Media.Animation.DoubleAnimation(startY, 0, System.TimeSpan.FromMilliseconds(150))
				{
					EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
				};
				MenuTranslateTransform.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, slideY);
			}
		}

		public void SoftHide(bool force = false)
		{
			if (!force && (DateTime.Now - LastOpened).TotalMilliseconds < 300)
			{
				System.Windows.Input.Mouse.Capture(this, System.Windows.Input.CaptureMode.SubTree);
				return;
			}
			LastClosed = DateTime.Now;

			if (AppConfig.Current.DisableAnimations)
			{
				this.BeginAnimation(Window.OpacityProperty, null);
				this.Opacity = 0;

				System.Windows.Input.FocusManager.SetFocusedElement(this, this);
				System.Windows.Input.Keyboard.ClearFocus();

				RequestClose?.Invoke(this, EventArgs.Empty);
				return;
			}

			var fadeOut = new System.Windows.Media.Animation.DoubleAnimation(0, System.TimeSpan.FromMilliseconds(120))
			{
				EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
			};

			fadeOut.Completed += (s, e) =>
			{
				this.BeginAnimation(Window.OpacityProperty, null); // Kills the clock
				this.Opacity = 0; // Manually hold the value

				System.Windows.Input.FocusManager.SetFocusedElement(this, this);
				System.Windows.Input.Keyboard.ClearFocus();

				RequestClose?.Invoke(this, EventArgs.Empty);
			};

			this.BeginAnimation(Window.OpacityProperty, fadeOut);

			if (_lastStartX != 0)
			{
				var slideX = new System.Windows.Media.Animation.DoubleAnimation(_lastStartX, System.TimeSpan.FromMilliseconds(120))
				{
					EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseIn }
				};
				MenuTranslateTransform.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, slideX);
			}
			else
			{
				var slideY = new System.Windows.Media.Animation.DoubleAnimation(_lastStartY, System.TimeSpan.FromMilliseconds(120))
				{
					EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseIn }
				};
				MenuTranslateTransform.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, slideY);
			}
		}

		private async void Settings_Click(object sender, RoutedEventArgs e)
		{
			this.SoftHide(true);
			await System.Threading.Tasks.Task.Delay(150);

			var existingSettings = System.Windows.Application.Current.Windows.OfType<Settings>().FirstOrDefault();
			if (existingSettings != null)
			{
				if (existingSettings.WindowState == WindowState.Minimized)
				{
					existingSettings.WindowState = WindowState.Normal;
				}
				existingSettings.OpenSettingsWindow();
				existingSettings.Activate();
			}
			else if (App.GetService<Settings>() is Settings settings)
			{
				settings.OpenSettingsWindow();
			}
		}

		private async void ViewAlerts_Click(object sender, RoutedEventArgs e)
		{
			this.SoftHide(true);
			await System.Threading.Tasks.Task.Delay(150);
			if (Application.Current.MainWindow is MainWindow mainWindow)
			{
				mainWindow.OpenSlideoutToAlerts();
			}
		}

		private void Exit_Click(object sender, RoutedEventArgs e)
		{
			Application.Current.Shutdown();
		}

		private void Update_Click(object sender, RoutedEventArgs e)
		{
			this.SoftHide(true);
			try
			{
				string url = string.IsNullOrEmpty(MainWindow.LatestUpdateVersion)
					? "https://github.com/crustyoldhuman/SSVidPlayer/releases"
					: $"https://github.com/crustyoldhuman/SSVidPlayer/releases/tag/v{MainWindow.LatestUpdateVersion}";

				System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
			}
			catch { }

			MainWindow.IsUpdateAvailable = false;
			UpdateButton.Visibility = Visibility.Collapsed;
			AppConfig.Current.LastDismissedUpdateVersion = MainWindow.LatestUpdateVersion;
			SettingsManager.Save();
		}

		private void DismissUpdate_Click(object sender, RoutedEventArgs e)
		{
			e.Handled = true; // Prevent the click from bubbling up to the UpdateButton

			MainWindow.IsUpdateAvailable = false;
			UpdateButton.Visibility = Visibility.Collapsed;

			AppConfig.Current.LastDismissedUpdateVersion = MainWindow.LatestUpdateVersion;
			SettingsManager.Save();

			this.SoftHide();
		}
	}
}