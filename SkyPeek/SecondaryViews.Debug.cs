	using System;
	using System.Windows;
	using System.Windows.Input;
	using System.Windows.Media;

	namespace SkyPeek
	{
		public partial class SecondaryViews : Window
		{

#if DEBUG
			private int _debugStatusIndex = -1;

			private readonly (string Condition, string AnimationPath)[] _debugStatuses =
			[
				("Clear Sky", "pack://application:,,,/IconAssets/0ClearSkyDaytime.json"),
				("Clear Sky (Night)", "pack://application:,,,/IconAssets/0ClearSkyNighttime.json"),
				("Mainly Clear", "pack://application:,,,/IconAssets/1MainlyClearDaytime.json"),
				("Mainly Clear (Night)", "pack://application:,,,/IconAssets/1MainlyClearNighttime.json"),
				("Partly Cloudy", "pack://application:,,,/IconAssets/2PartlyCloudyDaytime.json"),
				("Partly Cloudy (Night)", "pack://application:,,,/IconAssets/2PartlyCloudyNighttime.json"),
				("Overcast", "pack://application:,,,/IconAssets/3Overcast.json"),
				("Fog", "pack://application:,,,/IconAssets/45Fog.json"),
				("Depositing Rime Fog", "pack://application:,,,/IconAssets/48FogRime.json"),
				("Light Drizzle", "pack://application:,,,/IconAssets/51DrizzleLight.json"),
				("Moderate Drizzle", "pack://application:,,,/IconAssets/53DrizzleModerate.json"),
				("Dense Drizzle", "pack://application:,,,/IconAssets/55DrizzleDense.json"),
				("Light Freezing Drizzle", "pack://application:,,,/IconAssets/56FreezingDrizzleSlight.json"),
				("Dense Freezing Drizzle", "pack://application:,,,/IconAssets/57FreezingDrizzleDense.json"),
				("Slight Rain", "pack://application:,,,/IconAssets/61RainSlight.json"),
				("Moderate Rain", "pack://application:,,,/IconAssets/63RainModerate.json"),
				("Heavy Rain", "pack://application:,,,/IconAssets/65RainHeavy.json"),
				("Light Freezing Rain", "pack://application:,,,/IconAssets/66FreezingRainSlight.json"),
				("Heavy Freezing Rain", "pack://application:,,,/IconAssets/67FreezingRainHeavy.json"),
				("Slight Snowfall", "pack://application:,,,/IconAssets/71SnowfallSlight.json"),
				("Moderate Snowfall", "pack://application:,,,/IconAssets/73SnowfallModerate.json"),
				("Heavy Snowfall", "pack://application:,,,/IconAssets/75SnowfallHeavy.json"),
				("Snow Grains", "pack://application:,,,/IconAssets/77SnowGrains.json"),
				("Slight Rain Showers", "pack://application:,,,/IconAssets/80RainShowersSlightDaytime.json"),
				("Slight Rain Showers (Night)", "pack://application:,,,/IconAssets/80RainShowersSlightNighttime.json"),
				("Moderate Rain Showers", "pack://application:,,,/IconAssets/81RainShowersModerateDaytime.json"),
				("Moderate Rain Showers (Night)", "pack://application:,,,/IconAssets/81RainShowersModerateNighttime.json"),
				("Violent Rain Showers", "pack://application:,,,/IconAssets/82RainShowersViolentDaytime.json"),
				("Violent Rain Showers (Night)", "pack://application:,,,/IconAssets/82RainShowersViolentNighttime.json"),
				("Slight Snow Showers", "pack://application:,,,/IconAssets/85SnowShowersSlightDaytime.json"),
				("Slight Snow Showers (Night)", "pack://application:,,,/IconAssets/85SnowShowersSlightNighttime.json"),
				("Heavy Snow Showers", "pack://application:,,,/IconAssets/86SnowShowersHeavyDaytime.json"),
				("Heavy Snow Showers (Night)", "pack://application:,,,/IconAssets/86SnowShowersHeavyNighttime.json"),
				("Thunderstorm", "pack://application:,,,/IconAssets/95Thunderstorm.json"),
				("Thunderstorm with Hail", "pack://application:,,,/IconAssets/96ThunderstormWithHailSlight.json"),
				("Heavy Thunderstorm with Hail", "pack://application:,,,/IconAssets/99ThunderstormWithHailHeavy.json")
			];

			public void ProcessDebugKeys(KeyEventArgs e)
			{

				if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Shift)
				{
					_debugStatusIndex = (_debugStatusIndex + 1) % _debugStatuses.Length;
					var status = _debugStatuses[_debugStatusIndex];

					if (ViewModel.CurrentWeather != null)
					{
						ViewModel.CurrentWeather.Status = status.Condition;
						ViewModel.CurrentWeather.IconPath = status.AnimationPath;

						if (ViewModel.CurrentWeather.HourlyData != null)
						{
							foreach (var hour in ViewModel.CurrentWeather.HourlyData)
							{
								hour.Status = status.Condition;
								hour.IconPath = status.AnimationPath;
							}
						}

						if (ViewModel.CurrentWeather.DailyData != null)
						{
							foreach (var day in ViewModel.CurrentWeather.DailyData)
							{
								day.Status = status.Condition;
								day.IconPath = status.AnimationPath;
							}
						}

						// Force UI binding refresh by triggering the object setter
						var temp = ViewModel.CurrentWeather;
						ViewModel.CurrentWeather = null;
						ViewModel.CurrentWeather = temp;
					}

					e.Handled = true;
				}
				else if (e.Key == Key.A && Keyboard.Modifiers == ModifierKeys.Shift)
				{
					if (ViewModel.CurrentWeather != null)
					{
						if (ViewModel.CurrentWeather.AlertsData == null)
						{
							ViewModel.CurrentWeather.AlertsData = [];
						}

						int count = ViewModel.CurrentWeather.AlertsData.Count + 1;
						int severity = ((count - 1) % 4) + 1;

						var alert = new WeatherAlert
						{
							Id = "DebugAlert_" + Guid.NewGuid().ToString(),
							SeverityLevel = severity,
							SeverityColor = severity switch
							{
								1 => Color.FromRgb(23, 162, 184),
								2 => Color.FromRgb(255, 193, 7),
								3 => Color.FromRgb(253, 126, 20),
								4 => Color.FromRgb(220, 53, 69),
								_ => Color.FromRgb(23, 162, 184)
							},
							Event = severity switch
							{
								1 => $"Minor Weather Alert #{count}",
								2 => $"Moderate Weather Alert #{count}",
								3 => $"Severe Weather Alert #{count}",
								4 => $"Extreme Weather Alert #{count}",
								_ => $"Debug Weather Alert #{count}"
							},
							Sender = "SkyPeek Debugger",
							Description = $"This is simulated debug weather alert #{count} to verify UI states and multiple alert scrolling. For more information on alert #{count}, please visit https://example.com/weather-alert-{count} and stay safe."
						};

					ViewModel.CurrentWeather.AlertsData.Add(alert);

					var activeLoc = AppConfig.Current.SavedLocations.Find(l => l.DisplayName == ViewModel.SelectedLocationName);
					if (activeLoc != null)
					{
						activeLoc.HasActiveAlert = true;
						activeLoc.ActiveAlertIds.Add(alert.Id);
						var brush = new SolidColorBrush(alert.SeverityColor);
						brush.Freeze();
						activeLoc.AlertIconColor = brush;
					}

					ViewModel.AlertState.RebuildGlobalAlerts(AppConfig.Current.SavedLocations, AppConfig.Current.WeatherCache);

					var temp = ViewModel.CurrentWeather;
					ViewModel.CurrentWeather = null;
					Dispatcher.BeginInvoke(new Action(() => ViewModel.CurrentWeather = temp), System.Windows.Threading.DispatcherPriority.DataBind);

					if (Application.Current.MainWindow is MainWindow mw)
					{
						mw.RefreshTrayIcon();
					}
				}
				e.Handled = true;
			}
			else if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Shift)
				{
				if (Application.Current.MainWindow is MainWindow mw)
				{
					AppConfig.Current.DismissedAlerts?.Clear();
					AppConfig.Current.SeenBackgroundAlertIds?.Clear();
					MainWindow.PlayedAlerts?.Clear();
					AppConfig.Current.WeatherCache?.Clear();

					foreach (var loc in AppConfig.Current.SavedLocations)
						{
							loc.ActiveAlertIds?.Clear();
							loc.HasActiveAlert = false;
						}

						SettingsManager.Save();
						mw.RefreshTrayIcon();

					_ = MainWindow.SyncWindowsLocationAsync();

					if (CurrentView.LocationComboBox.SelectedItem is SavedLocation selectedLoc && !selectedLoc.IsHome)
					{
						_ = mw.FetchWeatherByLocation(selectedLoc.City, selectedLoc.State, selectedLoc.Country).ContinueWith(t =>
						{
							if (t.Result != null)
							{
								Application.Current.Dispatcher.Invoke(() => UpdateWeatherUI(t.Result, selectedLoc.City, AppConfig.Current.FlipWindArrow, true));
							}
							_ = mw.RefreshAllLocationsCacheAsync();
						});
					}
					else
					{
						_ = mw.RefreshAllLocationsCacheAsync();
					}
				}

				e.Handled = true;
			}
			else if (e.Key == Key.U && Keyboard.Modifiers == ModifierKeys.Shift)
			{
				MainWindow.LatestUpdateVersion = "99.99.99.99";
				AppConfig.Current.KnownUpdateVersion = "99.99.99.99";
				SettingsManager.Save();

				MainWindow.IsUpdateAvailable = true;
				if (Application.Current.MainWindow is MainWindow mw)
				{
					mw.TrayIcon.ShowNotification("SkyPeek has an update!", "Right click the SkyPeek tray icon to get the new version.", H.NotifyIcon.Core.NotificationIcon.Info);
				}
				e.Handled = true;
			}
			else if (e.Key == Key.T && Keyboard.Modifiers == ModifierKeys.Shift)
			{
					if (ViewModel.CurrentWeather != null)
					{
						ViewModel.CurrentWeather.Temp = "100";
						ViewModel.CurrentWeather.Precip = "10.00";

						if (ViewModel.CurrentWeather.HourlyData != null)
						{
							foreach (var hour in ViewModel.CurrentWeather.HourlyData)
							{
								hour.Temp = "100";
								hour.Precip = "10.00";
							}
						}

						if (ViewModel.CurrentWeather.DailyData != null)
						{
							foreach (var day in ViewModel.CurrentWeather.DailyData)
							{
								day.High = "100";
								day.Low = "100";
								day.Precip = "10.00";
							}
						}

						// Force UI binding refresh by triggering the object setter
						var temp = ViewModel.CurrentWeather;
						ViewModel.CurrentWeather = null;
						ViewModel.CurrentWeather = temp;
					}

					e.Handled = true;
				}
				else if (e.Key == Key.R && Keyboard.Modifiers == ModifierKeys.Shift)
				{
					ToastNotification.TriggerToast(ToastText, "Data Syncing...");

					if (Application.Current.MainWindow is MainWindow mw)
					{
						AppConfig.Current.WeatherCache?.Clear();

					_ = MainWindow.SyncWindowsLocationAsync();

					if (CurrentView.LocationComboBox.SelectedItem is SavedLocation selectedLoc && !selectedLoc.IsHome)
					{
						_ = mw.FetchWeatherByLocation(selectedLoc.City, selectedLoc.State, selectedLoc.Country).ContinueWith(t =>
						{
							if (t.Result != null)
							{
								Application.Current.Dispatcher.Invoke(() => UpdateWeatherUI(t.Result, selectedLoc.City, AppConfig.Current.FlipWindArrow, true));
							}
							_ = mw.RefreshAllLocationsCacheAsync();
						});
					}
					else
					{
						_ = mw.RefreshAllLocationsCacheAsync();
					}
				}

					e.Handled = true;
				}
				else if (e.Key == Key.T && Keyboard.Modifiers == ModifierKeys.Shift)
				{
					ViewModel.OnboardingVisibility = ViewModel.OnboardingVisibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
					e.Handled = true;
				}
				else if (e.Key == Key.Y && Keyboard.Modifiers == ModifierKeys.Shift)
				{
					ShowLoadingState();
					_ = System.Threading.Tasks.Task.Delay(3000).ContinueWith(_ =>
					{
						Application.Current.Dispatcher.Invoke(() =>
						{
							var fadeOutLoad = new System.Windows.Media.Animation.DoubleAnimation(0.0, TimeSpan.FromMilliseconds(200));
							fadeOutLoad.Completed += (sender, ev) =>
							{
								LoadingView.IsHitTestVisible = false;
								ViewModel.IsLoading = false;
							};
							LoadingView.BeginAnimation(UIElement.OpacityProperty, fadeOutLoad);

							ContentContainer.IsHitTestVisible = true;
							ContentContainer.BeginAnimation(UIElement.OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation(1.0, TimeSpan.FromMilliseconds(200)));

							TabArea.IsHitTestVisible = true;
							TabArea.Opacity = 1.0;
						});
					});
					e.Handled = true;
				}
			}
#endif
	}
}