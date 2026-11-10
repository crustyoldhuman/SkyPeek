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

using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
using Windows.UI.Notifications;

namespace SkyPeek
{
	public class WhitespaceConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			if (value is string str) return string.Join(" ", str.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
			return value;
		}
		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => throw new NotImplementedException();
	}

	/// <summary>
	/// Interaction logic for Slideout.xaml
	/// </summary>
	[System.Runtime.Versioning.SupportedOSPlatform("windows10.0.19041.0")]
	public partial class Settings : Window
	{
		private uint _currentModifiers = 0x0002 | 0x0001; // Default: Ctrl + Alt
		private uint _currentKey = 0x57; // Default: W
		private string _customTonePath = "";
		private int _previousToneIndex = 0;
		private bool _isUpdatingDropdown = false;
		private static readonly HttpClient _httpClient = new();
		private System.Windows.Threading.DispatcherTimer? _scrollPreviewTimer;
		private string _originalThemePreference = "System";
		private bool _originalUseAccentColor = false;
		private bool _originalUseTransparency = true;
		private bool _isInitializing = true;

		public Settings()
		{
			InitializeComponent();
			this.FocusVisualStyle = null;
			RootGrid.FocusVisualStyle = null;
			HotkeyInput.PreviewMouseLeftButtonUp += (s, e) => { if (!IsRecordingHotkey) StartHotkeyRecording(false); };
			HotkeyInput.LostFocus += (s, e) => { if (IsRecordingHotkey) StopHotkeyRecording(); };
			ThemeHelper.ApplyAccentColor(this, GradientCenter, GradientEdge, WindowBorder, WindowShadow);
			Microsoft.Win32.SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;
			this.Loaded += Settings_Loaded;

			PopulateDropdowns();
			LoadCurrentSettings();
		}

		private void LoadCurrentSettings()
		{
			LoadSettingsToUI(AppConfig.Current);

			EnableWeatherAlertsCheckbox.Checked -= EnableWeatherAlertsCheckbox_Checked;
			EnableWeatherAlertsCheckbox.Checked += EnableWeatherAlertsCheckbox_Checked;
			EnableWeatherAlertsCheckbox.Unchecked -= EnableWeatherAlertsCheckbox_Unchecked;
			EnableWeatherAlertsCheckbox.Unchecked += EnableWeatherAlertsCheckbox_Unchecked;

			// Force center screen on first launch (overrides XAML defaults)
			this.WindowStartupLocation = WindowStartupLocation.CenterScreen;

			ApplySavedSize();

			// Force the window to the center of the screen every time it is opened
			this.WindowStartupLocation = WindowStartupLocation.Manual;
			this.Left = (System.Windows.SystemParameters.PrimaryScreenWidth - this.Width) / 2;
			this.Top = (System.Windows.SystemParameters.PrimaryScreenHeight - this.Height) / 2;
		}

		private void ApplySavedSize()
		{
			if (AppConfig.Current.SettingsWindowWidth >= 690 && AppConfig.Current.SettingsWindowHeight >= 490)
			{
				this.Width = Math.Min(AppConfig.Current.SettingsWindowWidth, 1035);
				this.Height = Math.Min(AppConfig.Current.SettingsWindowHeight, 735);
			}
			_isInitializing = false;
		}

		private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			if (_isInitializing) return;

			AppConfig.Current.SettingsWindowWidth = this.ActualWidth;
			AppConfig.Current.SettingsWindowHeight = this.ActualHeight;
		}

		private POINT _resizeStartPos;
		private double _resizeStartWidth;
		private double _dpiScaleX = 1.0;
		private double _dpiScaleY = 1.0;

		private void ResizeThumb_DragStarted(object sender, DragStartedEventArgs e)
		{
			_resizeStartPos = NativeMethods.GetCursorPos();
			_resizeStartWidth = this.ActualWidth;

			// Unbind WPF explicit size constraints to allow native atomic resizing
			this.Width = double.NaN;
			this.Height = double.NaN;

			PresentationSource source = PresentationSource.FromVisual(this);
			if (source != null && source.CompositionTarget != null)
			{
				_dpiScaleX = source.CompositionTarget.TransformToDevice.M11;
				_dpiScaleY = source.CompositionTarget.TransformToDevice.M22;
			}
		}

		private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
		{
			double targetRatio = 690.0 / 490.0;
			double minWidth = 690;
			double maxWidth = 1035;

			POINT currentPos = NativeMethods.GetCursorPos();
			double logicalDeltaX = (currentPos.X - _resizeStartPos.X) / _dpiScaleX;

			double requestedWidth = _resizeStartWidth + logicalDeltaX;
			double finalLogicalWidth = Math.Max(minWidth, Math.Min(maxWidth, requestedWidth));
			double finalLogicalHeight = finalLogicalWidth / targetRatio;

			IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
			int physWidth = (int)Math.Round(finalLogicalWidth * _dpiScaleX);
			int physHeight = (int)Math.Round(finalLogicalHeight * _dpiScaleY);

			NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, 0, 0, physWidth, physHeight, 0x0016);

			if (_isPreviewingScale && System.Windows.Application.Current.Windows.OfType<SecondaryViews>().FirstOrDefault() is SecondaryViews secondaryViews)
			{
				secondaryViews.PreviewSettingsShow(this);
			}
		}

		private void EnableWeatherAlertsCheckbox_Checked(object sender, RoutedEventArgs e)
		{
			EnableAlertToneCheckbox.IsEnabled = true;
			EnableTrayAlertColorsCheckbox.IsEnabled = true;
		}

		private void EnableWeatherAlertsCheckbox_Unchecked(object sender, RoutedEventArgs e)
		{
			EnableAlertToneCheckbox.IsEnabled = false;
			EnableTrayAlertColorsCheckbox.IsEnabled = false;
		}

		private void LoadSettingsToUI(AppConfig config)
		{
			_isLoadingSettings = true;

			IntervalInput.Value = config.RefreshInterval;
			IntervalInput.Text = config.RefreshInterval.ToString();
			StartupCheckbox.IsChecked = config.StartOnBoot;
			FlipWindArrowCheckbox.IsChecked = config.FlipWindArrow;
			CelsiusCheckbox.IsChecked = config.UseCelsius;
			Use24HourClockCheckbox.IsChecked = config.Use24HourClock;
			UseKphCheckbox.IsChecked = config.UseKph;
			UseCmCheckbox.IsChecked = config.UseCm;
			WindowsLocationCheckbox.IsChecked = config.UseWindowsLocation;
			UseAccentColorCheckbox.IsChecked = config.UseAccentColor;
			UseTransparencyCheckbox.IsChecked = !config.UseTransparency;
			UseAnimationsCheckbox.IsChecked = config.DisableAnimations;
			UseAnimatedIcons.IsChecked = config.UseAnimatedIcons;
			SlideoutScaleSlider.Value = config.SlideoutScale;
			EnableWeatherAlertsCheckbox.IsChecked = config.EnableWeatherAlerts;
			EnableAlertToneCheckbox.IsChecked = config.EnableAlertTone;
			AlertVolumeSlider.Value = config.AlertVolume;
			EnableTrayAlertColorsCheckbox.IsChecked = config.EnableTrayAlertColors;
			EnableAlertToneCheckbox.IsEnabled = config.EnableWeatherAlerts;
			EnableTrayAlertColorsCheckbox.IsEnabled = config.EnableWeatherAlerts;

			EnableHotkeyCheckbox.IsChecked = config.EnableHotkey;
			_currentModifiers = config.HotkeyModifiers;
			_currentKey = config.HotkeyKey;
			UpdateHotkeyDisplay();

			if (config.PopupPosition == "Top") TopRadio.IsChecked = true; else BottomRadio.IsChecked = true;

			// Temporarily disable the SelectionChanged event while loading saved settings
			_isUpdatingDropdown = true;

			DefaultViewInput.SelectedItem = config.DefaultView == "10-Day" ? "Daily" : config.DefaultView;
			LightDarkSetting.SelectedItem = config.ThemePreference;

			if (config.SelectedAlertTone == "alerttone2.wav") AlertToneInput.SelectedIndex = 1;
			else if (config.SelectedAlertTone == "alerttone3.wav") AlertToneInput.SelectedIndex = 2;
			else if (config.SelectedAlertTone == "alerttone.wav") AlertToneInput.SelectedIndex = 0;
			else
			{
				_customTonePath = config.SelectedAlertTone;
				AlertToneInput.SelectedIndex = 3;
			}
			_previousToneIndex = AlertToneInput.SelectedIndex;

			// Re-enable the event for actual user interactions
			_isUpdatingDropdown = false;
			_isLoadingSettings = false;
		}

		private void ExportSettings_Click(object sender, RoutedEventArgs e)
		{
			var dialog = new Microsoft.Win32.SaveFileDialog
			{
				Title = "Export SkyPeek Settings",
				Filter = "JSON Files (*.json)|*.json",
				DefaultExt = ".json",
				FileName = $"SkyPeekSettings.{DateTime.Now:MM-dd-yyyy.HH-mm-ss}.json"
			};

			if (dialog.ShowDialog() == true)
			{
				try
				{
					SettingsManager.Export(dialog.FileName);
					ShowSettingsToast("Settings exported successfully.");
				}
				catch
				{
					ShowSettingsToast("Failed to export settings.");
				}
			}
		}

		private void ImportSettings_Click(object sender, RoutedEventArgs e)
		{
			var dialog = new Microsoft.Win32.OpenFileDialog
			{
				Title = "Import SkyPeek Settings",
				Filter = "JSON Files (*.json)|*.json",
				DefaultExt = ".json"
			};

			if (dialog.ShowDialog() == true)
			{
				try
				{
					var importedConfig = SettingsManager.GetConfigFromFile(dialog.FileName);
					if (importedConfig != null)
					{
						LoadSettingsToUI(importedConfig);
						ShowSettingsToast("Settings imported! Click 'Save & close' to apply.");
					}
				}
				catch
				{
					ShowSettingsToast("Failed to import settings. Invalid file.");
				}
			}
		}

		private void ShowSettingsToast(string message)
		{
			SettingsToastText.Text = message;
			var fadeIn = new System.Windows.Media.Animation.DoubleAnimation(1.0, TimeSpan.FromMilliseconds(200));
			SettingsToastNotification.BeginAnimation(UIElement.OpacityProperty, fadeIn);

			var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
			timer.Tick += (s, args) =>
			{
				timer.Stop();
				var fadeOut = new System.Windows.Media.Animation.DoubleAnimation(0.0, TimeSpan.FromMilliseconds(200));
				SettingsToastNotification.BeginAnimation(UIElement.OpacityProperty, fadeOut);
			};
			timer.Start();
		}

		private void IntervalInput_PreviewMouseDown(object sender, MouseButtonEventArgs e)
		{
			IntervalInput.Tag = "Editing";
			IntervalInput.FocusVisualStyle = null;
			Dispatcher.BeginInvoke(new Action(() => {
				if (Keyboard.FocusedElement is TextBox tb) tb.ClearValue(TextBox.CaretBrushProperty);
			}), System.Windows.Threading.DispatcherPriority.Input);
		}

		private void IntervalInput_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
		{
			if (sender is Wpf.Ui.Controls.NumberBox numberBox)
			{
				string currentText = numberBox.Text ?? "";

				// If a minus sign is detected alongside a digit, instantly snap to the new minimum (15)
				if (currentText.Contains('-') && currentText.Any(char.IsDigit))
				{
					// Forcibly wipe the negative string and replace it with 15
					numberBox.Text = "15";
					numberBox.Value = 15;

					// Keep the user cursor at the end of the text block to prevent jumping
					if (e.OriginalSource is System.Windows.Controls.TextBox internalTextBox)
					{
						internalTextBox.CaretIndex = internalTextBox.Text.Length;
					}
				}
			}
		}

		private void SystemEvents_UserPreferenceChanged(object sender, Microsoft.Win32.UserPreferenceChangedEventArgs e)
		{
			if (e.Category == Microsoft.Win32.UserPreferenceCategory.Color || e.Category == Microsoft.Win32.UserPreferenceCategory.General)
			{
				Application.Current.Dispatcher.Invoke(() => ThemeHelper.ApplyAccentColor(this, GradientCenter, GradientEdge, WindowBorder, WindowShadow));
			}
		}

		private void PopulateDropdowns()
		{
			string[] views = ["Current", "Hourly", "Daily", "Radar"];
			foreach (var view in views)
			{
				DefaultViewInput.Items.Add(view);
			}

			string[] themes = ["Dark", "Light", "System"];
			foreach (var theme in themes)
			{
				LightDarkSetting.Items.Add(theme);
			}

			AlertToneInput.Items.Add("Alert Tone 1");
			AlertToneInput.Items.Add("Alert Tone 2");
			AlertToneInput.Items.Add("Alert Tone 3");

			ComboBoxItem customItem = new() { Content = "Custom..." };
			customItem.PreviewMouseLeftButtonUp += (s, e) =>
			{
				if (AlertToneInput.SelectedIndex == 3)
				{
					// It's already selected, so SelectionChanged won't fire. Handle it manually.
					e.Handled = true;
					AlertToneInput.IsDropDownOpen = false;
					OpenCustomToneBrowser();
				}
			};
			AlertToneInput.Items.Add(customItem);
		}

		private async void SaveButton_Click(object sender, RoutedEventArgs e)
		{
			_isSaving = true;

			int _calculatedInterval = (int)(IntervalInput.Value ?? 15);
			if (int.TryParse(IntervalInput.Text, out int _textVal)) _calculatedInterval = _textVal;
			_calculatedInterval = Math.Max(15, _calculatedInterval);

			bool gpsSyncChanged = AppConfig.Current.UseWindowsLocation != (WindowsLocationCheckbox.IsChecked == true);
			bool alertsEnabled = !AppConfig.Current.EnableWeatherAlerts && (EnableWeatherAlertsCheckbox.IsChecked == true);
			bool intervalShortened = _calculatedInterval < AppConfig.Current.RefreshInterval;
			bool intervalChanged = _calculatedInterval != AppConfig.Current.RefreshInterval;

			bool requiresCompleteCacheWipe =
				AppConfig.Current.UseCelsius != (CelsiusCheckbox.IsChecked == true) ||
				AppConfig.Current.Use24HourClock != (Use24HourClockCheckbox.IsChecked == true) ||
				AppConfig.Current.UseKph != (UseKphCheckbox.IsChecked == true) ||
				AppConfig.Current.UseCm != (UseCmCheckbox.IsChecked == true) ||
				gpsSyncChanged ||
				alertsEnabled;

			bool requiresUiRedrawOnly = AppConfig.Current.FlipWindArrow != (FlipWindArrowCheckbox.IsChecked == true);

			string selectedTheme = LightDarkSetting.SelectedItem?.ToString() ?? "System";

			bool requiresThemeRefresh =
			AppConfig.Current.ThemePreference != selectedTheme ||
			AppConfig.Current.UseAccentColor != (UseAccentColorCheckbox.IsChecked == true) ||
			AppConfig.Current.UseTransparency != (UseTransparencyCheckbox.IsChecked == false) ||
			AppConfig.Current.DisableAnimations != (UseAnimationsCheckbox.IsChecked == true) ||
			AppConfig.Current.UseAnimatedIcons != (UseAnimatedIcons.IsChecked == true);

			AppConfig.Current.ThemePreference = selectedTheme;
			string selectedView = DefaultViewInput.SelectedItem?.ToString() ?? "Current";
			AppConfig.Current.DefaultView = selectedView == "Daily" ? "10-Day" : selectedView;
			AppConfig.Current.StartOnBoot = StartupCheckbox.IsChecked == true;
			AppConfig.Current.FlipWindArrow = FlipWindArrowCheckbox.IsChecked == true;
			AppConfig.Current.UseCelsius = CelsiusCheckbox.IsChecked == true;
			AppConfig.Current.Use24HourClock = Use24HourClockCheckbox.IsChecked == true;
			AppConfig.Current.UseKph = UseKphCheckbox.IsChecked == true;
			AppConfig.Current.UseCm = UseCmCheckbox.IsChecked == true;
			AppConfig.Current.UseWindowsLocation = WindowsLocationCheckbox.IsChecked == true;
			AppConfig.Current.UseAccentColor = UseAccentColorCheckbox.IsChecked == true;
			AppConfig.Current.UseTransparency = UseTransparencyCheckbox.IsChecked == false;
			AppConfig.Current.DisableAnimations = UseAnimationsCheckbox.IsChecked == true;
			AppConfig.Current.UseAnimatedIcons = UseAnimatedIcons.IsChecked == true;
			AppConfig.Current.EnableWeatherAlerts = EnableWeatherAlertsCheckbox.IsChecked == true;
			AppConfig.Current.EnableAlertTone = EnableAlertToneCheckbox.IsChecked == true;
			AppConfig.Current.AlertVolume = AlertVolumeSlider.Value;
			AppConfig.Current.EnableTrayAlertColors = EnableTrayAlertColorsCheckbox.IsChecked == true;
			AppConfig.Current.EnableHotkey = EnableHotkeyCheckbox.IsChecked == true;
			AppConfig.Current.HotkeyModifiers = _currentModifiers;
			AppConfig.Current.HotkeyKey = _currentKey;
			AppConfig.Current.PopupPosition = TopRadio.IsChecked == true ? "Top" : "Bottom";

			int parsedInterval = (int)(IntervalInput.Value ?? 15);
			if (int.TryParse(IntervalInput.Text, out int textVal))
			{
				parsedInterval = textVal;
			}
			AppConfig.Current.RefreshInterval = Math.Max(15, parsedInterval);
			IntervalInput.Value = AppConfig.Current.RefreshInterval;
			IntervalInput.Text = AppConfig.Current.RefreshInterval.ToString();

			AppConfig.Current.SelectedAlertTone = AlertToneInput.SelectedIndex switch
			{
				1 => "alerttone2.wav",
				2 => "alerttone3.wav",
				3 => _customTonePath,
				_ => "alerttone.wav"
			};

			if (Application.Current.MainWindow is MainWindow mainWindow)
			{
				mainWindow.SetSlideoutScale(SlideoutScaleSlider.Value);
				mainWindow.ApplyHotkeyRegistration();
				mainWindow.UpdateSlideoutDefaultView();
				mainWindow.UpdateSlideoutAlertsVisibility();
				mainWindow.UpdateSlideoutIconState();
				mainWindow.RefreshTrayIcon(debounced: true);

				if (requiresThemeRefresh)
				{
					ThemeHelper.ApplyAccentColor(this, GradientCenter, GradientEdge, WindowBorder, WindowShadow);
					mainWindow.RefreshThemeAcrossApp();
				}

				if (requiresCompleteCacheWipe)
				{
					AppConfig.Current.WeatherCache = [];
					if (gpsSyncChanged) await MainWindow.SyncWindowsLocationAsync();
					_ = mainWindow.RefreshAllLocationsCacheAsync();
				}
				else if (intervalShortened)
				{
					_ = mainWindow.RefreshAllLocationsCacheAsync();
				}
				else if (requiresUiRedrawOnly)
				{
					var homeLoc = AppConfig.Current.SavedLocations.Find(l => l.City.Equals(AppConfig.Current.City, System.StringComparison.OrdinalIgnoreCase) && l.State.Equals(AppConfig.Current.State, System.StringComparison.OrdinalIgnoreCase));
					if (homeLoc != null && AppConfig.Current.WeatherCache.TryGetValue(homeLoc.DisplayName, out var data))
					{
						mainWindow.ApplyWeatherToUI(data, homeLoc.DisplayName, false);
					}
				}

				if (intervalChanged)
				{
					mainWindow.SetupTimer();
				}
			}

			SettingsManager.Save();
			this.Close();
		}

		private static readonly System.Threading.Lock _toneExtractionLock = new();
		private readonly System.Collections.Generic.HashSet<System.Windows.Media.MediaPlayer> _activePlayers = [];

		private void PlayTonePreview_Click(object sender, RoutedEventArgs e)
		{
			string toneFile = AlertToneInput.SelectedIndex switch
			{
				1 => "alerttone2.wav",
				2 => "alerttone3.wav",
				3 => _customTonePath,
				_ => "alerttone.wav"
			};

			_ = System.Threading.Tasks.Task.Run(() =>
			{
				try
				{
					string path = System.IO.Path.IsPathRooted(toneFile)
				? toneFile
				: System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Assets", toneFile);

					if (!System.IO.File.Exists(path))
					{
						path = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Assets", "alerttone.wav");
					}

					if (!System.IO.Path.IsPathRooted(toneFile) && !System.IO.File.Exists(path))
					{
						string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), toneFile);
						lock (_toneExtractionLock)
						{
							if (!System.IO.File.Exists(tempPath))
							{
								Application.Current.Dispatcher.Invoke(() =>
								{
									var resourceInfo = Application.GetResourceStream(new System.Uri($"pack://application:,,,/Assets/{toneFile}"));
									if (resourceInfo != null)
									{
										using var fileStream = System.IO.File.Create(tempPath);
										resourceInfo.Stream.CopyTo(fileStream);
									}
								});
							}
						}
						path = tempPath;
					}

					Application.Current.Dispatcher.Invoke(() =>
					{
						var player = new System.Windows.Media.MediaPlayer();
						player.MediaOpened += (s, ev) => player.Play();
						player.MediaEnded += (s, ev) =>
						{
							player.Close();
							_activePlayers.Remove(player);
						};
						player.MediaFailed += (s, ev) =>
						{
							player.Close();
							_activePlayers.Remove(player);
						};
						_activePlayers.Add(player);
						player.Volume = AlertVolumeSlider.Value;
						player.Open(new System.Uri(path, System.UriKind.RelativeOrAbsolute));
					});
				}
				catch { }
			});
		}

		public bool IsRecordingHotkey { get; private set; } = false;
		private System.Windows.Threading.DispatcherTimer? _hotkeyRecordTimer;

		public void StartHotkeyRecording(bool useTimer = true)
		{
			IsRecordingHotkey = true;

			if (App.GetService<MainWindow>() is MainWindow mainWindow)
			{
				mainWindow.SuspendGlobalHotkeys();
			}

			if (useTimer)
			{
				SettingsToastText.Text = "Recording hotkey for 5 seconds...";

				var fadeIn = new System.Windows.Media.Animation.DoubleAnimation(1.0, TimeSpan.FromMilliseconds(200));
				SettingsToastNotification.BeginAnimation(UIElement.OpacityProperty, fadeIn);

				var progressAnim = new System.Windows.Media.Animation.DoubleAnimation(1.0, 0.0, TimeSpan.FromSeconds(5));
				SettingsToastScale.BeginAnimation(ScaleTransform.ScaleXProperty, progressAnim);

				if (_hotkeyRecordTimer == null)
				{
					_hotkeyRecordTimer = new System.Windows.Threading.DispatcherTimer();
					_hotkeyRecordTimer.Tick += (s, args) => StopHotkeyRecording();
				}
				_hotkeyRecordTimer.Interval = TimeSpan.FromSeconds(5);
				_hotkeyRecordTimer.Start();
			}
		}

		public void StopHotkeyRecording()
		{
			IsRecordingHotkey = false;
			_hotkeyRecordTimer?.Stop();

			var fadeOut = new System.Windows.Media.Animation.DoubleAnimation(0.0, TimeSpan.FromMilliseconds(200));
			SettingsToastNotification.BeginAnimation(UIElement.OpacityProperty, fadeOut);

			SettingsToastScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);

			if (App.GetService<MainWindow>() is MainWindow mainWindow)
			{
				mainWindow.ResumeGlobalHotkeys();
			}

			if (HotkeyInput.IsKeyboardFocusWithin)
			{
				RootGrid.Focus();
				HotkeyInput.Focus();
			}
		}

		private void HotkeyInput_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (!IsRecordingHotkey) return;

			e.Handled = true;

			Key key = (e.Key == Key.System ? e.SystemKey : e.Key);

			// Ignore raw modifier presses until a standard key is combined with them
			if (key == Key.LeftCtrl || key == Key.RightCtrl ||
				key == Key.LeftAlt || key == Key.RightAlt ||
				key == Key.LeftShift || key == Key.RightShift ||
				key == Key.LWin || key == Key.RWin)
			{
				return;
			}

			ModifierKeys modifiers = Keyboard.Modifiers;
			uint win32Modifiers = 0;
			if (modifiers.HasFlag(ModifierKeys.Alt)) win32Modifiers |= 0x0001;
			if (modifiers.HasFlag(ModifierKeys.Control)) win32Modifiers |= 0x0002;
			if (modifiers.HasFlag(ModifierKeys.Shift)) win32Modifiers |= 0x0004;

			_currentModifiers = win32Modifiers;
			_currentKey = (uint)KeyInterop.VirtualKeyFromKey(key);

			UpdateHotkeyDisplay();
		}

		private void AlertToneInput_DropDownOpened(object sender, EventArgs e)
		{
			// Remember what tone we were on in case the user cancels the file browser
			_previousToneIndex = AlertToneInput.SelectedIndex;
		}

		private void AlertToneInput_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (_isUpdatingDropdown) return;

			if (AlertToneInput.SelectedIndex == 3) // "Custom..."
			{
				OpenCustomToneBrowser();
			}
			else
			{
				_previousToneIndex = AlertToneInput.SelectedIndex;
			}
		}

		private void OpenCustomToneBrowser()
		{
			Microsoft.Win32.OpenFileDialog openFileDialog = new()
			{
				Filter = "Audio Files (*.wav;*.mp3;*.wma;*.aac;*.m4a)|*.wav;*.mp3;*.wma;*.aac;*.m4a",
				Title = "Select Custom Alert Tone"
			};

			if (openFileDialog.ShowDialog() == true)
			{
				_customTonePath = openFileDialog.FileName;
				_previousToneIndex = 3; // Lock in the new choice
			}
			else
			{
				// The user cancelled the popup. Silently snap the UI back to their previous choice.
				_isUpdatingDropdown = true;
				AlertToneInput.SelectedIndex = _previousToneIndex;
				_isUpdatingDropdown = false;
			}
		}

		private void UpdateHotkeyDisplay()
		{
			List<string> keys = [];
			if ((_currentModifiers & 0x0002) != 0) keys.Add("Ctrl");
			if ((_currentModifiers & 0x0001) != 0) keys.Add("Alt");
			if ((_currentModifiers & 0x0004) != 0) keys.Add("Shift");

			Key key = KeyInterop.KeyFromVirtualKey((int)_currentKey);
			keys.Add(key.ToString());

			HotkeyInput.Text = string.Join(" + ", keys);
		}

		private void SettingsTabControl_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
		{
			DependencyObject? target = e.OriginalSource as DependencyObject;

			while (target != null && target != SettingsTabControl)
			{
				if (target is ContentPresenter cp && cp.ContentSource == "SelectedContent")
				{
					return; // Ignore scrolls that happen inside the main content area
				}
				target = VisualTreeHelper.GetParent(target) ?? LogicalTreeHelper.GetParent(target);
			}

			int newIndex = SettingsTabControl.SelectedIndex;

			if (e.Delta > 0) newIndex--;
			else if (e.Delta < 0) newIndex++;

			if (newIndex >= 0 && newIndex < SettingsTabControl.Items.Count)
			{
				SettingsTabControl.SelectedIndex = newIndex;
				e.Handled = true;
			}
		}

		private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
		{
			_isKeyboardNavRevealed = false;

			DependencyObject? clickedElement = e.OriginalSource as DependencyObject;

			bool clickedOnInput = false;
			while (clickedElement != null)
			{
				if (clickedElement is TextBox || clickedElement is ComboBox || clickedElement is ComboBoxItem || clickedElement is CheckBox || clickedElement is System.Windows.Controls.Primitives.ButtonBase || clickedElement is Wpf.Ui.Controls.TitleBar)
				{
					clickedOnInput = true;
					break;
				}

				// Prevent crashes if the clicked element is text (Run) instead of a layout Visual
				if (clickedElement is Visual || clickedElement is System.Windows.Media.Media3D.Visual3D)
				{
					clickedElement = VisualTreeHelper.GetParent(clickedElement);
				}
				else
				{
					clickedElement = LogicalTreeHelper.GetParent(clickedElement);
				}
			}

			if (!clickedOnInput)
			{
				// Forcibly move logical and keyboard focus to the transparent background grid
				RootGrid.Focus();
				Keyboard.Focus(RootGrid);
			}
		}

		private void Window_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
		{
			_isKeyboardNavRevealed = false;
			System.Windows.Input.FocusManager.SetFocusedElement(this, null);
			System.Windows.Input.Keyboard.ClearFocus();
			RootGrid.Focus();
			Keyboard.Focus(RootGrid);
		}

		internal bool _isKeyboardNavRevealed = false;

		private bool _isClosing = false;
		private bool _isSaving = false;
		private bool _isLoadingSettings = false;

		protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
		{
			// ALWAYS cancel the physical OS close to keep the window alive in memory
			e.Cancel = true;

			// Temporarily flag as closing to prevent the slider value change from triggering a phantom preview pop-up
			bool wasClosing = _isClosing;
			_isClosing = true;

			if (!_isSaving)
			{
				if (System.Windows.Application.Current.Windows.OfType<SecondaryViews>().FirstOrDefault() is SecondaryViews secondaryViews)
				{
					double savedScale = AppConfig.Current.SlideoutScale;
					SlideoutScaleSlider.Value = savedScale > 0 ? savedScale : 1.0;
					secondaryViews.ViewModel.UI_Scale = SlideoutScaleSlider.Value;
					secondaryViews.PreviewSettingsHide();
				}

				bool themeChanged = false;
				if (AppConfig.Current.ThemePreference != _originalThemePreference ||
					AppConfig.Current.UseAccentColor != _originalUseAccentColor ||
					AppConfig.Current.UseTransparency != _originalUseTransparency)
				{
					AppConfig.Current.ThemePreference = _originalThemePreference;
					AppConfig.Current.UseAccentColor = _originalUseAccentColor;
					AppConfig.Current.UseTransparency = _originalUseTransparency;
					themeChanged = true;
				}

				if (themeChanged)
				{
					ThemeHelper.ApplyAccentColor(this, GradientCenter, GradientEdge, WindowBorder, WindowShadow);
					if (Application.Current.MainWindow is MainWindow mw) mw.RefreshThemeAcrossApp();
				}
			}

			_isClosing = wasClosing;

			if (AppConfig.Current.DisableAnimations)
			{
				this.Opacity = 0.0;
				this.Hide();
				AppConfig.Current.SettingsLeft = this.Left;
				AppConfig.Current.SettingsTop = this.Top;
				return;
			}

			if (!_isClosing)
			{
				_isClosing = true;

				var duration = TimeSpan.FromMilliseconds(100);
				var ease = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseIn };

				var fadeAnimation = new System.Windows.Media.Animation.DoubleAnimation(0.0, duration) { EasingFunction = ease };
				var scaleXAnimation = new System.Windows.Media.Animation.DoubleAnimation(0.85, duration) { EasingFunction = ease };
				var scaleYAnimation = new System.Windows.Media.Animation.DoubleAnimation(0.85, duration) { EasingFunction = ease };

				fadeAnimation.Completed += async (s, args) =>
				{
					await System.Threading.Tasks.Task.Delay(50);

					if (_isClosing)
					{
						this.Hide(); // Hide it into the background instead of destroying it!

						AppConfig.Current.SettingsLeft = this.Left;
						AppConfig.Current.SettingsTop = this.Top;
					}
				};

				this.BeginAnimation(UIElement.OpacityProperty, fadeAnimation);
				WindowScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleXAnimation);
				WindowScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleYAnimation);
			}
		}

		private async void Settings_Loaded(object sender, RoutedEventArgs e)
		{
			await CheckLocationAccessAsync();
		}

		private void AlertVolumeSlider_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
		{
			if (sender is System.Windows.Controls.Slider slider)
			{
				// Adjust the step size (0.05 = 5% volume change per detent) as needed
				slider.Value += e.Delta > 0 ? 0.01 : -0.01;
				e.Handled = true;
			}
		}

		internal void AppInfo_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			var infoWindow = new InfoWindow { Owner = this };
			infoWindow.ShowDialog();
		}

		internal void HelpFile_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			try
			{
				string path = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "SkyPeekReadme.html");
				string devPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\SkyPeekReadme.html"));

				if (System.IO.File.Exists(path))
				{
					System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
				}
				else if (System.IO.File.Exists(devPath))
				{
					System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(devPath) { UseShellExecute = true });
				}
			}
			catch { }
		}
		public void OpenSettingsWindow()
		{
			_isClosing = false;
			_isSaving = false;
			_isKeyboardNavRevealed = false;
			if (IsRecordingHotkey) StopHotkeyRecording();

			_originalThemePreference = AppConfig.Current.ThemePreference;
			_originalUseAccentColor = AppConfig.Current.UseAccentColor;
			_originalUseTransparency = AppConfig.Current.UseTransparency;

			LoadCurrentSettings();

			// 1. Strip any lingering animations to reset the timeline base values
			this.BeginAnimation(UIElement.OpacityProperty, null);
			WindowScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
			WindowScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);

			// 2. Force the EXACT starting visual state BEFORE we tell the OS to render it
			this.Opacity = 0.0;
			WindowScale.ScaleX = 0.85;
			WindowScale.ScaleY = 0.85;

			// 3. Tell Windows to allocate the window and pull it to the front
			// (It will remain completely invisible because Opacity is 0.0)
			this.Show();
			this.Activate();

			SettingsTabControl.SelectedIndex = 0;

			// Prevent the TabControl from auto-focusing and triggering the keyboard outline prematurely
			System.Windows.Input.FocusManager.SetFocusedElement(this, null);
			System.Windows.Input.Keyboard.ClearFocus();
			RootGrid.Focus();
			Keyboard.Focus(RootGrid);

			// 4. THE FIX: Wait for the OS to physically render the invisible window
			// before firing the animations. This completely eliminates the startup flicker.
			Application.Current.Dispatcher.InvokeAsync(() =>
			{
				if (AppConfig.Current.DisableAnimations)
				{
					this.Opacity = 1.0;
					WindowScale.ScaleX = 1.0;
					WindowScale.ScaleY = 1.0;
					return;
				}

				var duration = TimeSpan.FromMilliseconds(125);
				var ease = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut };

				var fadeAnimation = new System.Windows.Media.Animation.DoubleAnimation(0.0, 1.0, duration) { EasingFunction = ease };
				var scaleXAnimation = new System.Windows.Media.Animation.DoubleAnimation(0.85, 1.0, duration) { EasingFunction = ease };
				var scaleYAnimation = new System.Windows.Media.Animation.DoubleAnimation(0.85, 1.0, duration) { EasingFunction = ease };

				this.BeginAnimation(UIElement.OpacityProperty, fadeAnimation);
				WindowScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleXAnimation);
				WindowScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleYAnimation);
			}, System.Windows.Threading.DispatcherPriority.Loaded);

			// 5. Ping the Windows Location API without blocking the UI thread
			_ = CheckLocationAccessAsync();
		}

		private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
		{
			System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
			e.Handled = true;
		}

		private async System.Threading.Tasks.Task CheckLocationAccessAsync()
		{
			ToolTipService.SetShowOnDisabled(WindowsLocationCheckbox, true);
			try
			{
				var accessStatus = await Windows.Devices.Geolocation.Geolocator.RequestAccessAsync();

				Application.Current.Dispatcher.Invoke(() =>
				{
					if (accessStatus == Windows.Devices.Geolocation.GeolocationAccessStatus.Denied)
					{
						WindowsLocationCheckbox.IsEnabled = false;
						WindowsLocationCheckbox.IsChecked = false;
						WindowsLocationCheckbox.ToolTip = "Windows Location services must be enabled";
					}
					else
					{
						WindowsLocationCheckbox.IsEnabled = true;
						WindowsLocationCheckbox.ToolTip = "Automatically retrieve your location from Windows.";
					}
				});
			}
			catch
			{
				Application.Current.Dispatcher.Invoke(() =>
				{
					WindowsLocationCheckbox.IsEnabled = false;
					WindowsLocationCheckbox.IsChecked = false;
					WindowsLocationCheckbox.ToolTip = "Windows Location Services are unavailable.";
				});
			}
		}

		private bool _isPreviewingScale = false;

		protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
		{
			base.OnPreviewMouseLeftButtonUp(e);
			if (_isPreviewingScale)
			{
				_isPreviewingScale = false;
				System.Windows.Application.Current.Windows.OfType<SecondaryViews>().FirstOrDefault()?.PreviewSettingsHide();
			}
		}

		private void SlideoutScaleSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
		{
			if (this.IsLoaded && !_isClosing && !_isLoadingSettings)
			{
				_isPreviewingScale = true;
				if (System.Windows.Application.Current.Windows.OfType<SecondaryViews>().FirstOrDefault() is SecondaryViews secondaryViews)
				{
					secondaryViews.ViewModel.UI_Scale = e.NewValue;
					secondaryViews.PreviewSettingsShow(this);
				}
			}
		}

		private void SlideoutScaleSlider_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
		{
			if (this.IsLoaded && !_isClosing)
			{
				_isPreviewingScale = true;
				System.Windows.Application.Current.Windows.OfType<SecondaryViews>().FirstOrDefault()?.PreviewSettingsShow(this);
			}
		}

		private void SlideoutScaleSlider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
		{
			if (this.IsLoaded && _isPreviewingScale)
			{
				_isPreviewingScale = false;
				System.Windows.Application.Current.Windows.OfType<SecondaryViews>().FirstOrDefault()?.PreviewSettingsHide();
			}
		}

		private void SlideoutScaleSlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			if (e.OriginalSource is System.Windows.Controls.Primitives.Thumb) return;

			if (SlideoutScaleSlider.Template.FindName("PART_Track", SlideoutScaleSlider) is System.Windows.Controls.Primitives.Track track && track.Thumb != null)
			{
				SlideoutScaleSlider.Value = track.ValueFromPoint(e.GetPosition(track));
				e.Handled = true;

				SlideoutScaleSlider.UpdateLayout();

				track.Thumb.RaiseEvent(new MouseButtonEventArgs(e.MouseDevice, e.Timestamp, MouseButton.Left)
				{
					RoutedEvent = UIElement.MouseLeftButtonDownEvent,
					Source = track.Thumb
				});
			}
		}

		public void TriggerScalePreviewTimer(int milliseconds = 1000)
		{
			if (_scrollPreviewTimer == null)
			{
				_scrollPreviewTimer = new System.Windows.Threading.DispatcherTimer();
				_scrollPreviewTimer.Tick += (s, args) =>
				{
					_scrollPreviewTimer.Stop();
					if (_isPreviewingScale)
					{
						_isPreviewingScale = false;
						System.Windows.Application.Current.Windows.OfType<SecondaryViews>().FirstOrDefault()?.PreviewSettingsHide();
					}
				};
			}
			_scrollPreviewTimer.Interval = TimeSpan.FromMilliseconds(milliseconds);
			_scrollPreviewTimer.Stop();
			_scrollPreviewTimer.Start();
		}

		public void CancelScalePreviewTimer()
		{
			_scrollPreviewTimer?.Stop();
			if (_isPreviewingScale)
			{
				_isPreviewingScale = false;
				System.Windows.Application.Current.Windows.OfType<SecondaryViews>().FirstOrDefault()?.PreviewSettingsHide();
			}
		}

		private void SlideoutScaleSlider_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
		{
			if (sender is System.Windows.Controls.Slider slider)
			{
				TriggerScalePreviewTimer(400);
				slider.Value += e.Delta > 0 ? 0.01 : -0.01;
				e.Handled = true;
			}
		}

		private void ComboBox_DisableScroll(object sender, MouseWheelEventArgs e)
		{
			if (sender is ComboBox cb && !cb.IsDropDownOpen)
			{
				e.Handled = true;
			}
		}

		private void PreviewTheme_Changed(object sender, RoutedEventArgs e)
		{
			if (_isUpdatingDropdown || !this.IsLoaded || _isClosing) return;

			string selectedTheme = LightDarkSetting.SelectedItem?.ToString() ?? "System";
			bool useAccent = UseAccentColorCheckbox.IsChecked == true;
			bool useTrans = UseTransparencyCheckbox.IsChecked == false;

			if (AppConfig.Current.ThemePreference != selectedTheme ||
				AppConfig.Current.UseAccentColor != useAccent ||
				AppConfig.Current.UseTransparency != useTrans)
			{
				ShowSettingsToast("Applying theme preview...");

				// Yield execution to the background to allow the toast animation to render before the UI thread hangs on the theme calculation
				Application.Current.Dispatcher.InvokeAsync(async () =>
				{
					await System.Threading.Tasks.Task.Delay(50);
					if (_isClosing) return;

					AppConfig.Current.ThemePreference = selectedTheme;
					AppConfig.Current.UseAccentColor = useAccent;
					AppConfig.Current.UseTransparency = useTrans;

					ThemeHelper.ApplyAccentColor(this, GradientCenter, GradientEdge, WindowBorder, WindowShadow);
					if (Application.Current.MainWindow is MainWindow mainWindow) mainWindow.RefreshThemeAcrossApp();
				}, System.Windows.Threading.DispatcherPriority.Background);
			}
		}

		private void AlertVolumeSlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			// Ignore if the user clicked directly on the thumb to prevent a recursive loop
			if (e.OriginalSource is System.Windows.Controls.Primitives.Thumb) return;

			if (AlertVolumeSlider.Template.FindName("PART_Track", AlertVolumeSlider) is System.Windows.Controls.Primitives.Track track && track.Thumb != null)
			{
				// 1. Teleport the mathematical value
				AlertVolumeSlider.Value = track.ValueFromPoint(e.GetPosition(track));
				e.Handled = true;

				// 2. Force WPF to instantly redraw the thumb at the new coordinate
				AlertVolumeSlider.UpdateLayout();

				// 3. Now hand the click to the thumb. Because it has physically moved, the drag offset is perfectly synced to the cursor.
				track.Thumb.RaiseEvent(new MouseButtonEventArgs(e.MouseDevice, e.Timestamp, MouseButton.Left)
				{
					RoutedEvent = UIElement.MouseLeftButtonDownEvent,
					Source = track.Thumb
				});
			}
		}
	}
}