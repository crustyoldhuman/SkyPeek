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

using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SkyPeek
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	[System.Runtime.Versioning.SupportedOSPlatform("windows10.0.19041.0")]
	public partial class MainWindow : Window
	{
		private static readonly System.Net.Http.HttpClient _pingClient = new() { Timeout = System.TimeSpan.FromSeconds(3) };
		private bool _isBootLoading = true;
		private bool _isOffline = false;
		public static bool IsUpdateAvailable { get; internal set; } = false;
		public static string LatestUpdateVersion { get; internal set; } = "";
		public static bool HasSeenUpdateToast { get; set; } = false;
		public CachedWeatherData? CurrentWeather { get; private set; }

		private bool _isRefreshingCache = false;

		private DateTime _lastGlobalRefresh = DateTime.MinValue;

		private static async System.Threading.Tasks.Task<bool> IsNetworkConnectedAsync()
		{
			try
			{
				if (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable()) return false;

				bool hasLocalInterface = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
					.Any(n => n.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up
						   && n.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback
						   && n.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Tunnel
						   && n.GetIPProperties().GatewayAddresses.Any(g => g.Address != null && !g.Address.Equals(System.Net.IPAddress.Any) && !g.Address.Equals(System.Net.IPAddress.IPv6Any)));

				if (!hasLocalInterface) return false;

				using var res = await _pingClient.GetAsync("http://clients3.google.com/generate_204");
				return res.IsSuccessStatusCode || res.StatusCode == System.Net.HttpStatusCode.NoContent;
			}
			catch
			{
				return false;
			}
		}

		public async System.Threading.Tasks.Task RefreshAllLocationsCacheAsync(bool forceRefresh = false, [System.Runtime.CompilerServices.CallerMemberName] string _2 = "")
		{

			if (!forceRefresh && Math.Abs((DateTime.Now - _lastGlobalRefresh).TotalSeconds) < 15)
			{
				return;
			}

			if (_isRefreshingCache) return;
			_lastGlobalRefresh = DateTime.Now;
			_isRefreshingCache = true;

			try
			{
				if (AppConfig.Current.SavedLocations.Count == 0 && !string.IsNullOrEmpty(AppConfig.Current.City))
				{
					AppConfig.Current.SavedLocations.Add(new SavedLocation { City = AppConfig.Current.City, State = AppConfig.Current.State, Country = AppConfig.Current.Country });
				}

				int interval = AppConfig.Current.RefreshInterval > 0 ? AppConfig.Current.RefreshInterval : 15;
				System.Collections.Generic.List<SavedLocation> locationsToFetch = [];
				System.Collections.Generic.List<double> lats = [];
				System.Collections.Generic.List<double> lons = [];
				System.Collections.Generic.List<string> displayCities = [];

				foreach (var loc in AppConfig.Current.SavedLocations.ToList())
				{
					string key = loc.DisplayName;
					bool isHome = loc.City.Equals(AppConfig.Current.City, System.StringComparison.OrdinalIgnoreCase) && loc.State.Equals(AppConfig.Current.State, System.StringComparison.OrdinalIgnoreCase);

					if (!forceRefresh && AppConfig.Current.WeatherCache.TryGetValue(key, out var cachedData) && Math.Abs((DateTime.Now - cachedData.LastUpdated).TotalMinutes) < (interval - 1))
					{
						if (isHome) ApplyWeatherToUI(cachedData, key, false);
						continue;
					}

					var coords = await WeatherService.GetCoordinatesAsync(loc.City, loc.State, loc.Country);
					if (coords != null)
					{
						locationsToFetch.Add(loc);
						lats.Add(coords.Value.Latitude);
						lons.Add(coords.Value.Longitude);
						displayCities.Add(loc.City);

						if (isHome && _slideoutInstance != null && !_isBootLoading)
						{
							_slideoutInstance.ShowLoadingState();
						}
					}
					else if (isHome)
					{
						_ = HandleFetchFailureAsync(loc.City, loc.State);
					}
				}

				if (locationsToFetch.Count > 0)
				{
					var bulkResults = await WeatherService.GetWeatherBulkAsync(lats, lons, displayCities, AppConfig.Current.UseCelsius, AppConfig.Current.Use24HourClock, AppConfig.Current.FlipWindArrow, AppConfig.Current.UseKph, AppConfig.Current.UseCm);

					for (int i = 0; i < locationsToFetch.Count; i++)
					{
						var loc = locationsToFetch[i];
						string key = loc.DisplayName;
						bool isHome = loc.City.Equals(AppConfig.Current.City, System.StringComparison.OrdinalIgnoreCase) && loc.State.Equals(AppConfig.Current.State, System.StringComparison.OrdinalIgnoreCase);

						if (i < bulkResults.Count && bulkResults[i] != null)
						{
							var data = bulkResults[i];
							int alertFilterLevel = loc.AlertFilterLevel;

							bool hasNewAlert = false;
							if (AppConfig.Current.EnableWeatherAlerts && alertFilterLevel > 0)
							{
								data.AlertsData = await AlertService.GetAlertsAsync(data.Latitude, data.Longitude, loc.City, loc.Country, [.. AppConfig.Current.DismissedAlerts], alertFilterLevel);
								foreach (var alert in data.AlertsData)
								{
									lock (_alertLock)
									{
										string playedKey = $"{loc.DisplayName}_{alert.Id}";
										if (AppConfig.Current.PlayedAlerts.Add(playedKey))
										{
											hasNewAlert = true;
										}
									}
								}
								if (hasNewAlert) SettingsManager.Save();
							}

							AppConfig.Current.WeatherCache[key] = data;

							if (AppConfig.Current.EnableWeatherAlerts)
							{
								Application.Current.Dispatcher.Invoke(() => _slideoutInstance?.ViewModel.AlertState.RebuildGlobalAlerts(AppConfig.Current.SavedLocations, AppConfig.Current.WeatherCache));

								if (hasNewAlert && AppConfig.Current.EnableAlertTone && loc.AlertFilterLevel > 0 && loc.PlayAlertTone)
								{
									_ = AudioService.PlayAlertToneAsync(AppConfig.Current.SelectedAlertTone, AppConfig.Current.AlertVolume, 1, () => _slideoutInstance?.ViewModel.AlertState.HasNewAlerts == true);
								}
							}

							if (isHome) ApplyWeatherToUI(data, key, false);
						}
						else if (isHome)
						{
							if (AppConfig.Current.WeatherCache.TryGetValue(key, out var oldData))
								ApplyWeatherToUI(oldData, key, false);
							else if (_slideoutInstance != null)
							{
								Application.Current.Dispatcher.Invoke(() => {
									_slideoutInstance.ViewModel.IsLoading = false;
									_slideoutInstance.LoadingView.Opacity = 0.0;
									_slideoutInstance.LoadingView.IsHitTestVisible = false;
									_slideoutInstance.ContentContainer.Opacity = 1.0;
									_slideoutInstance.ContentContainer.IsHitTestVisible = true;
									_slideoutInstance.TabArea.Opacity = 1.0;
									_slideoutInstance.TabArea.IsHitTestVisible = true;
								});
							}
						}
					}
				}

				Application.Current.Dispatcher.Invoke(() =>
				{
					_slideoutInstance?.ViewModel.AlertState.RebuildGlobalAlerts(AppConfig.Current.SavedLocations, AppConfig.Current.WeatherCache);
				});
			}
			catch (System.Exception)
			{
				_ = HandleFetchFailureAsync(AppConfig.Current.City, AppConfig.Current.State);
			}
			finally
			{
				_isRefreshingCache = false;
			}
		}

		private async System.Threading.Tasks.Task HandleFetchFailureAsync(string city, string state)
		{
			bool isOffline = true;
			try
			{
				using var res = await _pingClient.GetAsync("http://clients3.google.com/generate_204");
				isOffline = !res.IsSuccessStatusCode && res.StatusCode != System.Net.HttpStatusCode.NoContent;
			}
			catch { }

			Application.Current.Dispatcher.Invoke(() =>
			{
				_updateTimer.Stop();
				_isOffline = isOffline;
				if (city.Equals(AppConfig.Current.City, System.StringComparison.OrdinalIgnoreCase) && state.Equals(AppConfig.Current.State, System.StringComparison.OrdinalIgnoreCase))
				{
					_isBootLoading = false;
					var homeLoc = AppConfig.Current.SavedLocations.Find(l => l.City.Equals(city, System.StringComparison.OrdinalIgnoreCase) && l.State.Equals(state, System.StringComparison.OrdinalIgnoreCase));

					if (isOffline)
					{
						if (!_recoveryTimer.IsEnabled) _recoveryTimer.Start();

						if (homeLoc != null && AppConfig.Current.WeatherCache.TryGetValue(homeLoc.DisplayName, out var data))
						{
							ApplyWeatherToUI(data, homeLoc.DisplayName, false);
						}
						else
						{
							UpdateTrayIcon("Off");
							TrayIcon.ToolTipText = "Waiting for connection...";
						}
					}
					else
					{
						if (!_apiRecoveryTimer.IsEnabled) _apiRecoveryTimer.Start();

						if (homeLoc != null && AppConfig.Current.WeatherCache.TryGetValue(homeLoc.DisplayName, out var data))
						{
							ApplyWeatherToUI(data, homeLoc.DisplayName, false);
						}
						else
						{
							UpdateTrayIcon("ERR");
							TrayIcon.ToolTipText = "Weather API unreachable";
						}
					}
				}
			});
		}

		public void ApplyWeatherToUI(CachedWeatherData data, string displayCity, bool showLoading = false)
		{
			CurrentWeather = data;
			_isBootLoading = false;

			RefreshTrayIcon(debounced: true);

			if (_slideoutInstance != null)
			{
				_slideoutInstance.ViewModel.PrecipUnit = AppConfig.Current.UseCm ? "cm" : "in";
				_slideoutInstance.ViewModel.PrecipUnitShort = AppConfig.Current.UseCm ? "cm" : "\"";
				_slideoutInstance.ViewModel.UseAnimatedIcons = AppConfig.Current.UseAnimatedIcons;
				_slideoutInstance.UpdateWeatherUI(data, displayCity, AppConfig.Current.FlipWindArrow, showLoading, _isOffline, _apiRecoveryTimer.IsEnabled);
				_slideoutInstance.RefreshSavedLocations();
			}
		}

		private readonly System.Windows.Threading.DispatcherTimer _updateTimer = new();
		private readonly System.Windows.Threading.DispatcherTimer _recoveryTimer = new();
		private readonly System.Windows.Threading.DispatcherTimer _apiRecoveryTimer = new();
		private readonly System.Windows.Threading.DispatcherTimer _trayDebounceTimer = new();
		private bool _isRecovering = false;
		private bool _isApiRecovering = false;

		private void LoadSettings()
		{
			if (!string.IsNullOrEmpty(AppConfig.Current.KnownUpdateVersion) && AppConfig.Current.KnownUpdateVersion != AppConfig.Current.LastDismissedUpdateVersion)
			{
				if (Version.TryParse(AppConfig.Current.KnownUpdateVersion, out Version? knownVer))
				{
					var currentVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
					if (currentVersion != null && knownVer > currentVersion)
					{
						LatestUpdateVersion = AppConfig.Current.KnownUpdateVersion;
						IsUpdateAvailable = true;
					}
				}
			}

			if (AppConfig.Current.SavedLocations.Count == 0)
			{
				AppConfig.Current.SavedLocations.Add(new SavedLocation { City = AppConfig.Current.City, State = AppConfig.Current.State, Country = AppConfig.Current.Country });
			}

			if (_slideoutInstance is not null)
			{
				_slideoutInstance.ViewModel.UI_Scale = AppConfig.Current.SlideoutScale;
				_slideoutInstance.RefreshSavedLocations();
				_slideoutInstance.PreferTenDayAgenda = AppConfig.Current.PreferTenDayAgenda;
				_slideoutInstance.PreferHourlyAgendaView = AppConfig.Current.PreferHourlyAgendaView;
				_slideoutInstance.ApplyDefaultView(AppConfig.Current.DefaultView);
				UpdateSlideoutAlertsVisibility();
				_slideoutInstance.ApplyLoadedManagerLocations();
			}

			SettingsManager.SetStartup(AppConfig.Current.StartOnBoot);
			ApplyHotkeyRegistration();
			RefreshTrayIcon(debounced: true);
#if !DEBUG
            _ = PerformInitialBootSequenceAsync();
#else
			_isBootLoading = false;
			var homeLoc = AppConfig.Current.SavedLocations.Find(l => l.City.Equals(AppConfig.Current.City, System.StringComparison.OrdinalIgnoreCase) && l.State.Equals(AppConfig.Current.State, System.StringComparison.OrdinalIgnoreCase));
			if (homeLoc != null && AppConfig.Current.WeatherCache.TryGetValue(homeLoc.DisplayName, out var data))
			{
				ApplyWeatherToUI(data, homeLoc.DisplayName, false);
			}
			else if (_slideoutInstance != null)
			{
				Application.Current.Dispatcher.Invoke(() => {
					_slideoutInstance.ViewModel.IsLoading = false;
					_slideoutInstance.LoadingView.Opacity = 0.0;
					_slideoutInstance.LoadingView.IsHitTestVisible = false;
					_slideoutInstance.ContentContainer.Opacity = 1.0;
					_slideoutInstance.ContentContainer.IsHitTestVisible = true;
					_slideoutInstance.TabArea.Opacity = 1.0;
					_slideoutInstance.TabArea.IsHitTestVisible = true;
				});
			}
#endif
		}

		private async System.Threading.Tasks.Task PerformInitialBootSequenceAsync()
		{
			bool isFreshInstall = AppConfig.Current.SavedLocations.Count == 1 && AppConfig.Current.SavedLocations[0].City == "Chicago" && AppConfig.Current.SavedLocations[0].State == "Illinois";

			if (!AppConfig.Current.HasSeenTrayBalloon)
			{
				AppConfig.Current.HasSeenTrayBalloon = true;
				SettingsManager.Save();

				_ = System.Threading.Tasks.Task.Delay(1500).ContinueWith(_ =>
				{
					Application.Current.Dispatcher.Invoke(() =>
					{
						OperatingSystem os = Environment.OSVersion;
						bool isWindows10 = os.Platform == PlatformID.Win32NT && os.Version.Major == 10 && os.Version.Build < 22000;

						if (_slideoutInstance != null && !_slideoutInstance.IsOpen)
						{
							ToggleSlideout(fromHotkey: false);
						}
					});
				});
			}

			bool isPackaged = false;
			try
			{
				isPackaged = Windows.ApplicationModel.Package.Current != null;
			}
			catch
			{
				isPackaged = false;
			}

			if (!isPackaged)
			{
				_ = CheckForUpdatesAsync();
			}

			if (isFreshInstall)
			{
				if (AppConfig.Current.UseWindowsLocation)
				{
					await SyncWindowsLocationAsync();
				}

				var winLoc = AppConfig.Current.SavedLocations.Find(l => l.IsWindowsLocation);
				if (winLoc != null)
				{
					AppConfig.Current.SavedLocations.RemoveAll(l => l.City == "Chicago" && l.State == "Illinois" && !l.IsWindowsLocation);
					AppConfig.Current.City = winLoc.City;
					AppConfig.Current.State = winLoc.State;
					AppConfig.Current.Country = winLoc.Country;
					SettingsManager.Save();
				}
				else
				{
					var ipLoc = await WeatherService.GetIpLocationAsync();
					if (ipLoc.HasValue)
					{
						AppConfig.Current.SavedLocations.RemoveAll(l => l.City == "Chicago" && l.State == "Illinois");
						AppConfig.Current.SavedLocations.Add(new SavedLocation { City = ipLoc.Value.City, State = ipLoc.Value.State, Country = ipLoc.Value.Country });
						AppConfig.Current.City = ipLoc.Value.City;
						AppConfig.Current.State = ipLoc.Value.State;
						AppConfig.Current.Country = ipLoc.Value.Country;
						SettingsManager.Save();
					}
				}

				if (_slideoutInstance != null)
				{
					Application.Current.Dispatcher.Invoke(() => _slideoutInstance.RefreshSavedLocations());
				}
			}
			else
			{
				if (AppConfig.Current.UseWindowsLocation)
				{
					_ = SyncWindowsLocationAsync();
				}
			}

			await RefreshAllLocationsCacheAsync(false);
		}

		// Interop calls moved to NativeMethods class to bypass WPF source generator bug

		private const int GWL_EXSTYLE = -20;
		private const int WS_EX_TOOLWINDOW = 0x00000080;
		private const int HOTKEY_ID = 9000;
		private const int WM_HOTKEY = 0x0312;
		private const int WM_ACTIVATE = 0x0006;
		private const int WA_INACTIVE = 0;

		private uint _taskbarCreatedMessageId;
		private System.IntPtr _windowHandle;
		private System.Windows.Interop.HwndSource? _source;

		[System.Runtime.InteropServices.LibraryImport("user32.dll")]
		private static partial short GetAsyncKeyState(int vKey);

		private readonly SecondaryViews? _slideoutInstance;
		public static System.Collections.Generic.HashSet<string> PlayedAlerts => AppConfig.Current.PlayedAlerts;
		private readonly System.Threading.Lock _alertLock = new();

		public MainWindow()
		{
			SettingsManager.Load();
			AppConfig.Current.PlayedAlerts ??= [];
			ApplyGlobalTheme();

			InitializeComponent();

			TrayIcon.TrayIcon.MessageWindow.KeyboardEventReceived += (_, _) =>
			{
				bool appsKeyHardwareTriggered = GetAsyncKeyState(0x5D) != 0;

				Application.Current.Dispatcher.InvokeAsync(() =>
				{
					bool isShiftF10 = (System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.LeftShift) || System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.RightShift)) &&
									  System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.F10);

					bool isRightClick = isShiftF10 ||
										System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.Apps) ||
										appsKeyHardwareTriggered;

					bool isActionKey = System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.Enter) ||
									   System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.Space);

					if (isRightClick || isActionKey)
					{
						if (isRightClick)
						{
							TrayIcon_TrayRightMouseUp(this, new RoutedEventArgs());
						}
						else
						{
							ToggleSlideout(fromHotkey: false);
						}
					}
				});
			};

			System.Net.NetworkInformation.NetworkChange.NetworkAvailabilityChanged += NetworkChange_NetworkAvailabilityChanged;
			System.Net.NetworkInformation.NetworkChange.NetworkAddressChanged += NetworkChange_NetworkAddressChanged;
			_trayDebounceTimer.Interval = System.TimeSpan.FromMilliseconds(300);
			_trayDebounceTimer.Tick += (s, e) =>
			{
				_trayDebounceTimer.Stop();
				RefreshTrayIcon(debounced: true);
			};

			_slideoutInstance = new SecondaryViews();

			_slideoutInstance.IsOpenChanged += async (s, isOpen) =>
			{
				if (!isOpen) // Reset the view to Home silently in the background right after it hides
				{
					await System.Threading.Tasks.Task.Delay(2000);
					if (_slideoutInstance.IsOpen) return;

					if (_isBootLoading || _isRefreshingCache) return; // Prevent overwriting the boot loading screen with stale cache data

					var homeLoc = AppConfig.Current.SavedLocations.Find(l => l.City.Equals(AppConfig.Current.City, System.StringComparison.OrdinalIgnoreCase) && l.State.Equals(AppConfig.Current.State, System.StringComparison.OrdinalIgnoreCase));
					if (homeLoc != null && (_slideoutInstance.IsViewingTemporaryLocation || _slideoutInstance.ViewModel.SelectedLocationName != homeLoc.DisplayName))
					{
						_slideoutInstance.IsViewingTemporaryLocation = false;
						if (AppConfig.Current.WeatherCache.TryGetValue(homeLoc.DisplayName, out var data))
						{
							ApplyWeatherToUI(data, homeLoc.DisplayName, false);
						}
						else
						{
							_slideoutInstance.ViewModel.IsLoading = true;
#if !DEBUG
                            _ = FetchWeatherByLocation(homeLoc.City, homeLoc.State, homeLoc.Country, false, false);
#endif
						}
						_slideoutInstance.ViewModel.SelectedLocationName = homeLoc.DisplayName;
						_slideoutInstance.RefreshSavedLocations();
					}
				}
			};

			_slideoutInstance.AlertDismissed += (s, alertId) =>
			{
				AppConfig.Current.DismissedAlerts.Add(alertId);
				AppConfig.Current.SeenBackgroundAlertIds.Add(alertId);

				CurrentWeather?.AlertsData?.RemoveAll(a => a.Id == alertId);
				_slideoutInstance.ViewModel.CurrentWeather?.AlertsData?.RemoveAll(a => a.Id == alertId);
				foreach (var cached in AppConfig.Current.WeatherCache.Values) cached.AlertsData?.RemoveAll(a => a.Id == alertId);

				foreach (var loc in AppConfig.Current.SavedLocations)
				{
					loc.ActiveAlertIds.Remove(alertId);
					if (loc.ActiveAlertIds.Count == 0)
					{
						loc.HasActiveAlert = false;
					}
				}

				SettingsManager.Save();

				Application.Current.Dispatcher.Invoke(() =>
				{
					_slideoutInstance.ViewModel.AlertState.RebuildGlobalAlerts(AppConfig.Current.SavedLocations, AppConfig.Current.WeatherCache);
					_slideoutInstance.RefreshSavedLocations();
				});

				RefreshTrayIcon(debounced: true);
			};

			LoadSettings();
			SetupTimer();

			var homeLoc = AppConfig.Current.SavedLocations.Find(l => l.City.Equals(AppConfig.Current.City, System.StringComparison.OrdinalIgnoreCase) && l.State.Equals(AppConfig.Current.State, System.StringComparison.OrdinalIgnoreCase));
			if (homeLoc != null && AppConfig.Current.WeatherCache.TryGetValue(homeLoc.DisplayName, out var cachedData))
			{
				double intervalMinutes = AppConfig.Current.RefreshInterval > 0 ? AppConfig.Current.RefreshInterval : 15;
				System.TimeSpan standardInterval = System.TimeSpan.FromMinutes(intervalMinutes);
				System.TimeSpan elapsed = DateTime.Now - cachedData.LastUpdated;

				if (elapsed > System.TimeSpan.Zero && elapsed < standardInterval)
				{
					_updateTimer.Interval = standardInterval - elapsed;
				}
			}

			Microsoft.Win32.SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;
			Microsoft.Win32.SystemEvents.SessionSwitch += SystemEvents_SessionSwitch;
		}

		private void SystemEvents_PowerModeChanged(object sender, Microsoft.Win32.PowerModeChangedEventArgs e)
		{
			if (e.Mode == Microsoft.Win32.PowerModes.Suspend)
			{
				Application.Current.Dispatcher.Invoke(() => _updateTimer.Stop());
			}
			else if (e.Mode == Microsoft.Win32.PowerModes.Resume)
			{
				System.Threading.Tasks.Task.Delay(2000).ContinueWith(_ =>
				{
					Application.Current.Dispatcher.Invoke(ResumeTimersAndSync);
				});
			}
		}

		private void SystemEvents_SessionSwitch(object sender, Microsoft.Win32.SessionSwitchEventArgs e)
		{
			if (e.Reason == Microsoft.Win32.SessionSwitchReason.SessionUnlock)
			{
				System.Threading.Tasks.Task.Delay(2000).ContinueWith(_ =>
				{
					Application.Current.Dispatcher.Invoke(ResumeTimersAndSync);
				});
			}
		}

		private void ResumeTimersAndSync()
		{
			_updateTimer.Stop();

			double intervalMinutes = AppConfig.Current.RefreshInterval > 0 ? AppConfig.Current.RefreshInterval : 15;
			System.TimeSpan standardInterval = System.TimeSpan.FromMinutes(intervalMinutes);

			var homeLoc = AppConfig.Current.SavedLocations.Find(l => l.City.Equals(AppConfig.Current.City, System.StringComparison.OrdinalIgnoreCase) && l.State.Equals(AppConfig.Current.State, System.StringComparison.OrdinalIgnoreCase));

			DateTime lastUpdated = DateTime.MinValue;
			if (homeLoc != null && AppConfig.Current.WeatherCache.TryGetValue(homeLoc.DisplayName, out var cachedData))
			{
				lastUpdated = cachedData.LastUpdated;
			}
			else if (!AppConfig.Current.WeatherCache.IsEmpty)
			{
				lastUpdated = System.Linq.Enumerable.First(AppConfig.Current.WeatherCache.Values).LastUpdated;
			}
			else
			{
				lastUpdated = _lastGlobalRefresh;
			}

			System.TimeSpan elapsed = DateTime.Now - lastUpdated;
			if (elapsed < System.TimeSpan.Zero) elapsed = System.TimeSpan.Zero; // Guard against system clock rollback

			if (lastUpdated == DateTime.MinValue || elapsed >= standardInterval)
			{
				SetupTimer();
#if !DEBUG
                _ = SyncWindowsLocationAsync();
                _ = RefreshAllLocationsCacheAsync(true);
#endif
			}
			else
			{
				System.TimeSpan remaining = standardInterval - elapsed;
				if (remaining.TotalMilliseconds < 1000) remaining = System.TimeSpan.FromSeconds(1);

				_updateTimer.Interval = remaining;
				_updateTimer.Start();
			}
		}

		public void SetupTimer()
		{
			_updateTimer.Stop();
			_updateTimer.Interval = System.TimeSpan.FromMinutes(AppConfig.Current.RefreshInterval > 0 ? AppConfig.Current.RefreshInterval : 15);

			_updateTimer.Tick -= UpdateTimer_Tick;
			_updateTimer.Tick += UpdateTimer_Tick;
			_updateTimer.Start();

			_recoveryTimer.Interval = System.TimeSpan.FromSeconds(5);
			_recoveryTimer.Tick -= RecoveryTimer_Tick;
			_recoveryTimer.Tick += RecoveryTimer_Tick;

			_apiRecoveryTimer.Interval = System.TimeSpan.FromMinutes(1);
			_apiRecoveryTimer.Tick -= ApiRecoveryTimer_Tick;
			_apiRecoveryTimer.Tick += ApiRecoveryTimer_Tick;
		}

		private async void RecoveryTimer_Tick(object? sender, System.EventArgs e)
		{
			if (_isRecovering) return;
			_isRecovering = true;
			try
			{
				using var response = await _pingClient.GetAsync("http://clients3.google.com/generate_204");

				if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NoContent)
				{
					Application.Current.Dispatcher.Invoke(() =>
					{
						_isOffline = false;
						_recoveryTimer.Stop();
						ResumeTimersAndSync(); // Recalculates the refresh delta immediately upon ISP restoration
						RefreshTrayIcon(debounced: true);
					});
				}
			}
			catch { }
			finally
			{
				_isRecovering = false;
			}
		}

		private async void ApiRecoveryTimer_Tick(object? sender, System.EventArgs e)
		{
			if (_isApiRecovering) return;
			_isApiRecovering = true;
			try
			{
				if (await WeatherService.PingApiAsync())
				{
					Application.Current.Dispatcher.Invoke(() =>
					{
						_apiRecoveryTimer.Stop();
						ResumeTimersAndSync();
						RefreshTrayIcon(debounced: true);
					});
				}
			}
			catch { }
			finally
			{
				_isApiRecovering = false;
			}
		}

		private void UpdateTimer_Tick(object? sender, System.EventArgs e)
		{
			// Reset to the standard user interval in case this tick was triggered by a temporary recovery timer
			_updateTimer.Interval = System.TimeSpan.FromMinutes(AppConfig.Current.RefreshInterval > 0 ? AppConfig.Current.RefreshInterval : 15);

#if !DEBUG
			_ = SyncWindowsLocationAsync();
			_ = RefreshAllLocationsCacheAsync();
#endif
		}

		private void NetworkChange_NetworkAvailabilityChanged(object? sender, System.Net.NetworkInformation.NetworkAvailabilityEventArgs e)
		{
			EvaluateNetworkStatus();
		}

		private void NetworkChange_NetworkAddressChanged(object? sender, System.EventArgs e)
		{
			EvaluateNetworkStatus();
		}

		private async void EvaluateNetworkStatus()
		{
			try
			{
				bool connected = await IsNetworkConnectedAsync();

				Application.Current.Dispatcher.Invoke(() =>
				{
					if (connected)
					{
						if (_isOffline)
						{
							_isOffline = false;
							ResumeTimersAndSync();
							RefreshTrayIcon(debounced: true);
						}
					}
					else
					{
						if (!_isOffline)
						{
							_isOffline = true;
							var homeLoc = AppConfig.Current.SavedLocations.Find(l => l.City.Equals(AppConfig.Current.City, System.StringComparison.OrdinalIgnoreCase) && l.State.Equals(AppConfig.Current.State, System.StringComparison.OrdinalIgnoreCase));
							if (homeLoc != null && AppConfig.Current.WeatherCache.ContainsKey(homeLoc.DisplayName))
							{
								if (!_recoveryTimer.IsEnabled) _recoveryTimer.Start();
								RefreshTrayIcon(debounced: true);
							}
							else
							{
								UpdateTrayIcon("Off");
								TrayIcon.ToolTipText = "Waiting for connection...";
							}
						}
					}
				});
			}
			catch
			{
				// Suppress transient COM exceptions during system wake/unlock from H.NotifyIcon
			}
		}

		public void ApplyHotkeyRegistration()
		{
			if (_windowHandle == System.IntPtr.Zero) return;

			NativeMethods.UnregisterHotKey(_windowHandle, HOTKEY_ID);

			if (AppConfig.Current.EnableHotkey)
			{
				NativeMethods.RegisterHotKey(_windowHandle, HOTKEY_ID, AppConfig.Current.HotkeyModifiers, AppConfig.Current.HotkeyKey);
			}
		}

		public void SuspendGlobalHotkeys()
		{
			if (_windowHandle != System.IntPtr.Zero)
			{
				NativeMethods.UnregisterHotKey(_windowHandle, HOTKEY_ID);
			}
		}

		public void ResumeGlobalHotkeys()
		{
			ApplyHotkeyRegistration();
		}

		protected override void OnSourceInitialized(System.EventArgs e)
		{
			base.OnSourceInitialized(e);
			var helper = new System.Windows.Interop.WindowInteropHelper(this);
			System.IntPtr hwnd = helper.Handle;
			_windowHandle = hwnd;

			int extendedStyle = NativeMethods.GetWindowLong(hwnd, GWL_EXSTYLE);
			int result = NativeMethods.SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TOOLWINDOW);
			if (result == 0)
			{
				Debug.WriteLine("SetWindowLong failed.");
			}

			_taskbarCreatedMessageId = NativeMethods.RegisterWindowMessage("TaskbarCreated");
			_source = System.Windows.Interop.HwndSource.FromHwnd(_windowHandle);
			_source?.AddHook(HwndHook);
			ApplyHotkeyRegistration();

			// Eager Initialization: Safely build Settings in the background AFTER MainWindow is fully loaded.
			// This breaks the circular dependency loop and prevents the boot crash!
			Application.Current.Dispatcher.InvokeAsync(() =>
			{
				_settingsInstance ??= App.GetService<Settings>();

				InitializeTrayMenuPopup();

				if (_slideoutInstance != null && !_slideoutInstance.IsOpen)
				{
					_slideoutInstance.Opacity = 0;
					_slideoutInstance.Show();
					_slideoutInstance.UpdateLayout();

					var dummyRtb = new RenderTargetBitmap(1, 1, 96, 96, PixelFormats.Pbgra32);
					dummyRtb.Render(_slideoutInstance.ContentContainer);
					_slideoutInstance.Hide();
				}
			}, System.Windows.Threading.DispatcherPriority.Background);
		}

		private System.IntPtr HwndHook(System.IntPtr hwnd, int msg, System.IntPtr wParam, System.IntPtr lParam, ref bool handled)
		{
			if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
			{
				ToggleSlideout(fromHotkey: true);
				handled = true;
			}
			else if (msg == WM_ACTIVATE && wParam.ToInt32() == WA_INACTIVE)
			{
				if (_trayMenuInstance?.IsVisible == true && _trayMenuInstance?.Opacity > 0.1)
				{
					_trayMenuInstance.SoftHide();
				}
			}
			else if (msg == _taskbarCreatedMessageId)
			{
				System.Threading.Tasks.Task.Delay(500).ContinueWith(_ =>
				{
					Application.Current.Dispatcher.Invoke(() =>
					{
						TrayIcon.Visibility = Visibility.Collapsed;
						TrayIcon.Visibility = Visibility.Visible;
						RefreshTrayIcon(debounced: true);
					});
				});
			}
			return System.IntPtr.Zero;
		}

		protected override void OnClosed(EventArgs e)
		{
			System.Net.NetworkInformation.NetworkChange.NetworkAvailabilityChanged -= NetworkChange_NetworkAvailabilityChanged;
			System.Net.NetworkInformation.NetworkChange.NetworkAddressChanged -= NetworkChange_NetworkAddressChanged;
			Microsoft.Win32.SystemEvents.PowerModeChanged -= SystemEvents_PowerModeChanged;
			Microsoft.Win32.SystemEvents.SessionSwitch -= SystemEvents_SessionSwitch;
			_source?.RemoveHook(HwndHook);
			NativeMethods.UnregisterHotKey(_windowHandle, HOTKEY_ID);
			TrayIcon?.Dispose();
			base.OnClosed(e);
		}

		public void UpdateSlideoutIconState()
		{
			_slideoutInstance?.ViewModel.UseAnimatedIcons = AppConfig.Current.UseAnimatedIcons;
		}

		public void UpdateSlideoutAlertsVisibility()
		{
			foreach (var loc in AppConfig.Current.SavedLocations) loc.UpdateAlertToggleOpacity();
			if (_slideoutInstance is not null)
			{
				_slideoutInstance.ViewModel.AlertState.GlobalAlertsVisibility = AppConfig.Current.EnableWeatherAlerts ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
				_slideoutInstance.ViewModel.AlertState.GlobalAlertToneVisibility = (AppConfig.Current.EnableWeatherAlerts && AppConfig.Current.EnableAlertTone) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
				_slideoutInstance.RefreshSavedLocations();
			}
		}

		public void UpdateSlideoutDefaultView()
		{
			_slideoutInstance?.ApplyDefaultView(AppConfig.Current.DefaultView);
		}

		public void SetSlideoutScale(double scale)
		{
			AppConfig.Current.SlideoutScale = scale;
			if (_slideoutInstance is SecondaryViews secondaryViews)
			{
				secondaryViews.ViewModel.UI_Scale = scale;
			}
		}

		public static void ApplyGlobalTheme()
		{
			if (AppConfig.Current.ThemePreference == "Light")
				Wpf.Ui.Appearance.ApplicationThemeManager.Apply(Wpf.Ui.Appearance.ApplicationTheme.Light);
			else if (AppConfig.Current.ThemePreference == "Dark")
				Wpf.Ui.Appearance.ApplicationThemeManager.Apply(Wpf.Ui.Appearance.ApplicationTheme.Dark);
			else
			{
				var sysTheme = Wpf.Ui.Appearance.ApplicationThemeManager.GetSystemTheme();
				var targetTheme = sysTheme == Wpf.Ui.Appearance.SystemTheme.Light ? Wpf.Ui.Appearance.ApplicationTheme.Light : Wpf.Ui.Appearance.ApplicationTheme.Dark;
				Wpf.Ui.Appearance.ApplicationThemeManager.Apply(targetTheme);
			}
		}

		public void RefreshThemeAcrossApp()
		{
			ApplyGlobalTheme();
			_slideoutInstance?.RefreshTheme();

			if (_trayMenuInstance != null)
			{
				_trayMenuInstance.Close();
				_trayMenuInstance = null;

				Application.Current.Dispatcher.InvokeAsync(() =>
				{
					InitializeTrayMenuPopup();
				}, System.Windows.Threading.DispatcherPriority.Background);
			}
		}

		private DateTime _lastToggleTime = DateTime.MinValue;

		private async void ToggleSlideout(bool fromHotkey = false)
		{
			if (_slideoutInstance == null) return;

			if (Math.Abs((DateTime.Now - _lastToggleTime).TotalMilliseconds) < 300) return;
			_lastToggleTime = DateTime.Now;

			if (_trayMenuInstance?.IsVisible == true && _trayMenuInstance?.Opacity > 0.1)
			{
				_trayMenuInstance.SoftHide(force: true);
				await System.Threading.Tasks.Task.Delay(120); // Exact duration of SoftHide to cleanly hand off the UI thread
			}

			if (!_slideoutInstance.IsOpen && Math.Abs((DateTime.Now - _slideoutInstance.LastClosed).TotalMilliseconds) < 250)
			{
				// The taskbar steals focus when clicked, triggering Deactivated (which hides the window) 
				// milliseconds BEFORE this MouseUp event fires. If it was just hidden, abort the re-open.
				return;
			}

			// All rendering, data-binding, and JSON compiling was already handled silently in the background
			if (_slideoutInstance.IsOpen)
			{
				_slideoutInstance.SlideOut();
			}
			else
			{
				_slideoutInstance.SlideIn(fromHotkey);
			}
		}

		private TrayMenu? _trayMenuInstance;
		private Settings? _settingsInstance;
		private DateTime _lastPopupCloseTime = DateTime.MinValue;

		private void InitializeTrayMenuPopup()
		{
			if (_trayMenuInstance != null) return;

			_trayMenuInstance = new TrayMenu { Opacity = 0, ShowActivated = false };
			_trayMenuInstance.Show();

			_trayMenuInstance.RequestClose += (_, _) =>
			{
				_lastPopupCloseTime = DateTime.Now;
				if (System.Windows.Input.Mouse.Captured == _trayMenuInstance)
				{
					System.Windows.Input.Mouse.Capture(null);
				}
			};
		}

		private void TrayIcon_TrayRightMouseDown(object _, RoutedEventArgs _1)
		{
			if (_trayMenuInstance?.IsVisible == true && _trayMenuInstance?.Opacity > 0.1)
			{
				_trayMenuInstance.SoftHide();
			}
		}

		private async void TrayIcon_TrayRightMouseUp(object _, RoutedEventArgs _1)
		{
			if (System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.Enter) ||
				System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.Space))
			{
				ToggleSlideout(fromHotkey: false);
				return;
			}

			if (_trayMenuInstance == null)
			{
				InitializeTrayMenuPopup();
			}
			else if (_trayMenuInstance.IsVisible && _trayMenuInstance?.Opacity > 0.1)
			{
				_trayMenuInstance.SoftHide();
				return;
			}
			else if (Math.Abs((DateTime.Now - _lastPopupCloseTime).TotalMilliseconds) < 100 ||
				(_trayMenuInstance?.LastClosed != null && Math.Abs((DateTime.Now - _trayMenuInstance.LastClosed).TotalMilliseconds) < 100))
			{
				return;
			}

			if (Math.Abs((DateTime.Now - _lastToggleTime).TotalMilliseconds) < 350)
			{
				return;
			}

			if (_slideoutInstance != null)
			{
				double msSinceClosed = (DateTime.Now - _slideoutInstance.LastClosed).TotalMilliseconds;

				// 200ms slide + 30ms visual buffer + 20ms for the OS to process this.Hide()
				if (_slideoutInstance.IsOpen || msSinceClosed < 250)
				{
					if (_slideoutInstance.IsOpen)
					{
						_slideoutInstance.SlideOut();
						await System.Threading.Tasks.Task.Delay(250);
					}
					else
					{
						int remainingDelay = (int)(250 - msSinceClosed);
						if (remainingDelay > 0)
						{
							await System.Threading.Tasks.Task.Delay(remainingDelay);
						}
					}
				}
			}

			NativeMethods.SetForegroundWindow(_windowHandle);

			// Prevent layout skips
			_trayMenuInstance?.UpdateLayout();
			_trayMenuInstance?.ShowMenu();
		}

		public static async System.Threading.Tasks.Task SyncWindowsLocationAsync()
		{
			if (!AppConfig.Current.UseWindowsLocation)
			{
				bool changed = false;

				for (int i = AppConfig.Current.SavedLocations.Count - 1; i >= 0; i--)
				{
					var loc = AppConfig.Current.SavedLocations[i];

					if (loc.IsWindowsLocation)
					{
						loc.IsWindowsLocation = false;
						changed = true;
					}

					if (loc.AddedByGps)
					{
						bool isHome = loc.City.Equals(AppConfig.Current.City, System.StringComparison.OrdinalIgnoreCase) && loc.State.Equals(AppConfig.Current.State, System.StringComparison.OrdinalIgnoreCase);

						if (isHome)
						{
							loc.AddedByGps = false; // Preserve the location, but strip its GPS origin identity
							changed = true;
						}
						else
						{
							AppConfig.Current.SavedLocations.RemoveAt(i);
							changed = true;
						}
					}
				}

				if (changed)
				{
					Application.Current.Dispatcher.Invoke(() => System.Windows.Application.Current.Windows.OfType<SecondaryViews>().FirstOrDefault()?.RefreshSavedLocations());
				}
				return;
			}

			try
			{
				var accessStatus = await Windows.Devices.Geolocation.Geolocator.RequestAccessAsync();
				if (accessStatus != Windows.Devices.Geolocation.GeolocationAccessStatus.Allowed) return;

				var locator = new Windows.Devices.Geolocation.Geolocator();

				using var cts = new System.Threading.CancellationTokenSource();
				var geoTask = locator.GetGeopositionAsync(System.TimeSpan.FromMinutes(5), System.TimeSpan.FromSeconds(10)).AsTask(cts.Token);
				var timeoutTask = System.Threading.Tasks.Task.Delay(10000, cts.Token);

				var completedTask = await System.Threading.Tasks.Task.WhenAny(geoTask, timeoutTask);
				if (completedTask == timeoutTask)
				{
					cts.Cancel();
					return;
				}

				cts.Cancel();
				var position = await geoTask;

				string lat = position.Coordinate.Point.Position.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
				string lon = position.Coordinate.Point.Position.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture);

				var geoResult = await WeatherService.GetReverseGeocodeAsync(lat, lon);
				if (geoResult.HasValue)
				{
					string locCity = geoResult.Value.City;
					string locState = geoResult.Value.State;
					string locCountry = geoResult.Value.Country;

					bool listChanged = false;

					var oldGps = AppConfig.Current.SavedLocations.Find(l => l.AddedByGps);
					if (oldGps != null && (!oldGps.City.Equals(locCity, System.StringComparison.OrdinalIgnoreCase) || (!string.IsNullOrEmpty(oldGps.State) && !string.IsNullOrEmpty(locState) && !oldGps.State.Equals(locState, System.StringComparison.OrdinalIgnoreCase))))
					{
						AppConfig.Current.SavedLocations.Remove(oldGps);
						listChanged = true;
					}

					foreach (var loc in AppConfig.Current.SavedLocations)
					{
						if (loc.IsWindowsLocation)
						{
							loc.IsWindowsLocation = false;
							listChanged = true;
						}
					}

					var match = AppConfig.Current.SavedLocations.Find(l =>
						l.City.Equals(locCity, System.StringComparison.OrdinalIgnoreCase) &&
						(string.IsNullOrEmpty(l.State) || string.IsNullOrEmpty(locState) || l.State.Equals(locState, System.StringComparison.OrdinalIgnoreCase)));

					if (match != null)
					{
						if (!match.IsWindowsLocation)
						{
							match.IsWindowsLocation = true;
							listChanged = true;
						}
					}
					else
					{
						AppConfig.Current.SavedLocations.Add(new SavedLocation { City = locCity, State = locState, Country = locCountry, IsWindowsLocation = true, AddedByGps = true });
						SettingsManager.Save();
						listChanged = true;
					}

					if (listChanged)
					{
						Application.Current.Dispatcher.Invoke(() => System.Windows.Application.Current.Windows.OfType<SecondaryViews>().FirstOrDefault()?.RefreshSavedLocations());
					}
				}
			}
			catch (System.UnauthorizedAccessException)
			{
			}
			catch (System.Runtime.InteropServices.COMException)
			{
			}
			catch (System.Exception)
			{
			}
		}

		public async System.Threading.Tasks.Task<CachedWeatherData?> FetchWeatherByLocation(string city, string state, string country, bool isTemporary = false, bool isManualSwap = false)
		{
			if (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
			{
				Application.Current.Dispatcher.Invoke(() =>
				{
					if (city.Equals(AppConfig.Current.City, System.StringComparison.OrdinalIgnoreCase) && state.Equals(AppConfig.Current.State, System.StringComparison.OrdinalIgnoreCase))
					{
						UpdateTrayIcon("Off");
						TrayIcon.ToolTipText = "Waiting for connection...";
					}
				});
				return null;
			}

			try
			{
				var coords = await WeatherService.GetCoordinatesAsync(city, state, country);
				if (coords == null)
				{
					_ = HandleFetchFailureAsync(city, state);
					return null;
				}

				return await FetchAndUpdateTemperature(coords.Value.Latitude.ToString(), coords.Value.Longitude.ToString(), city, state, country, isTemporary, isManualSwap);
			}
			catch (System.Exception)
			{
				_ = HandleFetchFailureAsync(city, state);
				return null;
			}
		}

		public async System.Threading.Tasks.Task<CachedWeatherData?> FetchAndUpdateTemperature(string latitude, string longitude, string displayCity, string state, string country, bool isTemporary = false, bool _1 = false)
		{
			_ = double.TryParse(latitude, out double latValInit);
			_ = double.TryParse(longitude, out double lonValInit);

			try
			{
				var bulkData = await WeatherService.GetWeatherBulkAsync([latValInit], [lonValInit], [displayCity], AppConfig.Current.UseCelsius, AppConfig.Current.Use24HourClock, AppConfig.Current.FlipWindArrow, AppConfig.Current.UseKph, AppConfig.Current.UseCm);
				var data = bulkData.FirstOrDefault();
				if (data == null) return null;

				var activeLoc = AppConfig.Current.SavedLocations.Find(l => l.City.Equals(displayCity, System.StringComparison.OrdinalIgnoreCase) && l.State.Equals(state, System.StringComparison.OrdinalIgnoreCase) && l.Country.Equals(country, System.StringComparison.OrdinalIgnoreCase));
				int alertFilterLevel = activeLoc?.AlertFilterLevel ?? 1;

				if (AppConfig.Current.EnableWeatherAlerts && alertFilterLevel > 0)
				{
					data.AlertsData = await AlertService.GetAlertsAsync(latValInit, lonValInit, displayCity, country, [.. AppConfig.Current.DismissedAlerts], alertFilterLevel);
				}

				string locKeyForCache = activeLoc?.DisplayName ?? displayCity;
				AppConfig.Current.WeatherCache[locKeyForCache] = data;

				if (activeLoc != null)
				{
					Application.Current.Dispatcher.Invoke(() =>
					{
						var activeAlerts = (AppConfig.Current.EnableWeatherAlerts && activeLoc.AlertFilterLevel > 0)
							? data.AlertsData.FindAll(a => a.SeverityLevel >= activeLoc.AlertFilterLevel)
							: [];
						if (activeAlerts.Count > 0)
						{
							activeLoc.HasActiveAlert = true;
							var brush = new SolidColorBrush(Color.FromRgb(220, 53, 69));
							brush.Freeze();
							activeLoc.AlertIconColor = brush;
							activeLoc.ActiveAlertIds.Clear();
							foreach (var a in activeAlerts) activeLoc.ActiveAlertIds.Add(a.Id);
						}
						else
						{
							activeLoc.HasActiveAlert = false;
							activeLoc.ActiveAlertIds.Clear();
						}

						_slideoutInstance?.ViewModel.AlertState.RebuildGlobalAlerts(AppConfig.Current.SavedLocations, AppConfig.Current.WeatherCache);
					});
				}

				bool hasNewAlert = false;
				string locKey = activeLoc?.DisplayName ?? displayCity;
				foreach (var alert in data.AlertsData)
				{
					lock (_alertLock)
					{
						string playedKey = $"{locKey}_{alert.Id}";
						if (AppConfig.Current.PlayedAlerts.Add(playedKey))
						{
							hasNewAlert = true;
						}
					}
				}

				if (hasNewAlert) SettingsManager.Save();

				bool playAlertSound = hasNewAlert && !isTemporary;

				if (playAlertSound && AppConfig.Current.EnableAlertTone && activeLoc != null && activeLoc.PlayAlertTone)
				{
					_ = AudioService.PlayAlertToneAsync(AppConfig.Current.SelectedAlertTone, AppConfig.Current.AlertVolume, 1, () => _slideoutInstance?.ViewModel.AlertState.HasNewAlerts == true);
				}

				Application.Current.Dispatcher.Invoke(() =>
				{
					_updateTimer.Interval = System.TimeSpan.FromMinutes(AppConfig.Current.RefreshInterval > 0 ? AppConfig.Current.RefreshInterval : 15);
				});
				return data;
			}
			catch (System.Exception)
			{
				_ = HandleFetchFailureAsync(displayCity, state);
				_slideoutInstance?.ShowLoadingState();
				return null;
			}
		}

		public void OpenSlideoutToAlerts()
		{
			if (_slideoutInstance == null) return;

			_slideoutInstance.TabCurrent.Tag = "Unselected";
			_slideoutInstance.TabHourly.Tag = "Unselected";
			_slideoutInstance.Tab10Day.Tag = "Unselected";
			_slideoutInstance.ViewModel.CurrentActiveView = "Alerts";

			if (!_slideoutInstance.IsOpen && Math.Abs((DateTime.Now - _slideoutInstance.LastClosed).TotalMilliseconds) >= 250)
			{
				_lastToggleTime = DateTime.Now;
				_slideoutInstance.SlideIn(false);
			}
		}

		public void ClearAllAlerts()
		{
			if (CurrentWeather?.AlertsData != null)
			{
				foreach (var alert in CurrentWeather.AlertsData)
				{
					AppConfig.Current.DismissedAlerts.Add(alert.Id);
				}
				CurrentWeather.AlertsData.Clear();
			}

			foreach (var cached in AppConfig.Current.WeatherCache.Values)
			{
				if (cached.AlertsData != null)
				{
					foreach (var alert in cached.AlertsData)
					{
						AppConfig.Current.DismissedAlerts.Add(alert.Id);
					}
					cached.AlertsData.Clear();
				}
			}

			foreach (var loc in AppConfig.Current.SavedLocations)
			{
				foreach (var id in loc.ActiveAlertIds)
				{
					AppConfig.Current.DismissedAlerts.Add(id);
				}
				loc.HasActiveAlert = false;
				loc.ActiveAlertIds.Clear();
			}

			SettingsManager.Save();
			RefreshTrayIcon(debounced: true);

			Application.Current.Dispatcher.Invoke(() =>
			{
				_slideoutInstance?.ViewModel.AlertState.RebuildGlobalAlerts(AppConfig.Current.SavedLocations, AppConfig.Current.WeatherCache);
				_slideoutInstance?.RefreshSavedLocations();
			});
		}

		private System.Drawing.Icon? _currentTrayIcon;

		public System.Windows.Point GetTrayIconScreenPosition()
		{
			try
			{
				bool rectFound = false;

				var identifier = new NOTIFYICONIDENTIFIER
				{
					cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NOTIFYICONIDENTIFIER>(),
					hWnd = TrayIcon.TrayIcon.WindowHandle,
					uID = 0,
					guidItem = TrayIcon.Id
				};

				if (NativeMethods.Shell_NotifyIconGetRect(ref identifier, out RECT rect) == 0)
				{
					rectFound = true;
				}
				else
				{
					for (uint i = 0; i <= 10; i++)
					{
						identifier.uID = i;

						identifier.guidItem = TrayIcon.Id;
						if (NativeMethods.Shell_NotifyIconGetRect(ref identifier, out rect) == 0)
						{
							rectFound = true;
							break;
						}

						identifier.guidItem = TrayIcon.TrayIcon.Id;
						if (NativeMethods.Shell_NotifyIconGetRect(ref identifier, out rect) == 0)
						{
							rectFound = true;
							break;
						}

						identifier.guidItem = Guid.Empty;
						if (NativeMethods.Shell_NotifyIconGetRect(ref identifier, out rect) == 0)
						{
							rectFound = true;
							break;
						}
					}
				}

				if (rectFound)
				{
					var dpi = VisualTreeHelper.GetDpi(this);
					double x = (rect.Left + (rect.Right - rect.Left) / 2.0) / dpi.DpiScaleX;
					double y = (rect.Top + (rect.Bottom - rect.Top) / 2.0) / dpi.DpiScaleY;
					return new System.Windows.Point(x, y);
				}
			}
			catch { }

			NativeMethods.GetCursorPosition(out POINT pos);
			var fallbackDpi = VisualTreeHelper.GetDpi(this);
			return new System.Windows.Point(pos.X / fallbackDpi.DpiScaleX, pos.Y / fallbackDpi.DpiScaleY);
		}

		public void RefreshTrayIcon(bool debounced = false)
		{
			if (!debounced)
			{
				_trayDebounceTimer.Stop();
				_trayDebounceTimer.Start();
				return;
			}

			if (_isBootLoading)
			{
				UpdateTrayIcon("--°", null);
				TrayIcon.ToolTipText = "Connecting to weather services...";
				return;
			}

			var homeLoc = AppConfig.Current.SavedLocations.Find(l => l.City.Equals(AppConfig.Current.City, System.StringComparison.OrdinalIgnoreCase) && l.State.Equals(AppConfig.Current.State, System.StringComparison.OrdinalIgnoreCase));
			if (homeLoc != null && AppConfig.Current.WeatherCache.TryGetValue(homeLoc.DisplayName, out var data))
			{
				string trayTempText = data.Temp.Length >= 3 ? data.Temp : $"{data.Temp}°";
				Color? trayColor = null;
				int activeAlertCount = 0;
				if (AppConfig.Current.EnableWeatherAlerts && homeLoc.AlertFilterLevel > 0 && data.AlertsData != null && data.AlertsData.Count > 0)
				{
					var activeAlerts = data.AlertsData.FindAll(a => a.SeverityLevel >= homeLoc.AlertFilterLevel);
					activeAlertCount = activeAlerts.Count;
					if (activeAlerts.Count > 0)
					{
						var extremeAlert = System.Linq.Enumerable.First(System.Linq.Enumerable.OrderByDescending(activeAlerts, a => a.SeverityLevel));
						trayColor = extremeAlert.SeverityColor;
					}
				}
				UpdateTrayIcon(trayTempText, trayColor);

				string statusPrefix = _isOffline ? "(Offline)\n" : (_apiRecoveryTimer.IsEnabled ? "(API Unreachable)\n" : "");
				string alertPrefix = activeAlertCount > 0 ? $"Active alerts: {activeAlertCount}\n" : "";
				string rawToolTip = $"{statusPrefix}{alertPrefix}{data.Status}\nHumidity: {data.Humidity}%\nWind: {data.WindSpeed} {data.WindSpeedUnit} {data.WindDir}";
				TrayIcon.ToolTipText = rawToolTip.Length <= 127 ? rawToolTip : rawToolTip[..124] + "...";
			}
			else
			{
#if !DEBUG
				_ = SyncWindowsLocationAsync();
				_ = RefreshAllLocationsCacheAsync(true);
#endif
			}
		}

		private void TrayIcon_TrayLeftMouseUp(object _, System.Windows.RoutedEventArgs _1)
		{
			ToggleSlideout(fromHotkey: false);
		}

		private void UpdateTrayIcon(string text, Color? alertColor = null)
		{
			if (text == "Off")
			{
				if (!_recoveryTimer.IsEnabled) _recoveryTimer.Start();
			}

			System.Drawing.Icon newIcon = TrayIconService.GenerateIcon(text, alertColor, VisualTreeHelper.GetDpi(this).PixelsPerDip, AppConfig.Current.EnableTrayAlertColors);
			TrayIcon.Icon = newIcon;

			_currentTrayIcon?.Dispose();
			_currentTrayIcon = newIcon;
		}

		private static string GithubVersionPattern => @"""tag_name"":\s*""v?([^""]+)""";
		private static readonly System.Text.RegularExpressions.Regex _githubVersionRegex = new(GithubVersionPattern, System.Text.RegularExpressions.RegexOptions.Compiled);

		private async System.Threading.Tasks.Task CheckForUpdatesAsync()
		{
			try
			{
				using var client = new System.Net.Http.HttpClient();
				client.DefaultRequestHeaders.Add("User-Agent", "SkyPeek");
				using var response = await client.GetAsync("https://api.github.com/repos/crustyoldhuman/SkyPeek/releases/latest");

				if (!response.IsSuccessStatusCode) return;

				string responseBody = await response.Content.ReadAsStringAsync();
				var match = _githubVersionRegex.Match(responseBody);
				if (match.Success)
				{
					if (Version.TryParse(match.Groups[1].Value, out Version? githubVersion))
					{
						var currentVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
						if (currentVersion != null && githubVersion > currentVersion)
						{
							if (AppConfig.Current.LastDismissedUpdateVersion != githubVersion.ToString())
							{
								LatestUpdateVersion = githubVersion.ToString();
								IsUpdateAvailable = true;

								if (AppConfig.Current.KnownUpdateVersion != LatestUpdateVersion)
								{
									AppConfig.Current.KnownUpdateVersion = LatestUpdateVersion;
									SettingsManager.Save();
								}

								Application.Current.Dispatcher.Invoke(() =>
								{
									TrayIcon.ShowNotification("SkyPeek Update", "Update available! Right click the SkyPeek tray icon and select the Update option to get the new version.", H.NotifyIcon.Core.NotificationIcon.Info);
								});
							}
						}
					}
				}
			}
			catch (System.Exception)
			{
			}
		}
	}
}