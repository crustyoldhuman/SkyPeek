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
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SkyPeek
{
	[System.Runtime.Versioning.SupportedOSPlatform("windows10.0.19041.0")]
	public class SlideoutViewModel : INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler? PropertyChanged;
		protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		private double _uiScale = 1.0;
		public double UI_Scale
		{
			get => _uiScale;
			set
			{
				_uiScale = value;
				OnPropertyChanged();
				OnPropertyChanged(nameof(ScaledWidth));
				OnPropertyChanged(nameof(ScaledHeight));
			}
		}

		public double ScaledWidth => 305 * UI_Scale;
		public double ScaledHeight => 440 * UI_Scale;

		private string _city = "";
		public string City
		{
			get => _city;
			set
			{
				_city = value;
				OnPropertyChanged();
				OnPropertyChanged(nameof(FormattedSummary)); // Keeps your unused property in sync, just in case!
			}
		}

		private string _selectedLocationName = "";
		public string SelectedLocationName
		{
			get => _selectedLocationName;
			set { _selectedLocationName = value; OnPropertyChanged(); OnPropertyChanged(nameof(FormattedSummary)); }
		}

		public async System.Threading.Tasks.Task ProcessWeatherDataAsync(CachedWeatherData data, string displayCity, bool flipWindArrow, bool isOffline, bool isApiUnreachable)
		{
			if (data == null) return;
			SelectedLocationName = displayCity;

			ConnectionStatusPrefix = isOffline ? "(Offline)\n" : (isApiUnreachable ? "(API Unreachable)\n" : "");

			SunriseOpacity = DateTime.TryParse(data.Sunrise, out DateTime parsedSunrise) && DateTime.Now.TimeOfDay > parsedSunrise.TimeOfDay ? 0.3 : 1.0;
			SunsetOpacity = DateTime.TryParse(data.Sunset, out DateTime parsedSunset) && DateTime.Now.TimeOfDay > parsedSunset.TimeOfDay ? 0.3 : 1.0;
			WindRotationAngle = flipWindArrow ? data.WindDegrees + 180 : data.WindDegrees;
			if (data.HourlyData?.Count > 0)
			{
				foreach (var h in data.HourlyData) h.IconPath = WeatherService.GetIconPath(h.WeatherCode, h.IsDay);
			}
			CurrentWeather = data;
			AlertState.RebuildGlobalAlerts(AppConfig.Current.SavedLocations, AppConfig.Current.WeatherCache);
			await System.Threading.Tasks.Task.CompletedTask;
		}

		private readonly System.Windows.Threading.DispatcherTimer? _clockTimer;
		private int _lastEvaluatedHour = -1;
		private int _lastEvaluatedDay = -1;

		private readonly System.Windows.Threading.DispatcherTimer? _animationToggleTimer;

		public SlideoutViewModel()
		{
			_clockTimer = new System.Windows.Threading.DispatcherTimer
			{
				Interval = TimeSpan.FromSeconds(15)
			};
			_clockTimer.Tick += (s, e) => EvaluateTimeState();
			_clockTimer.Start();

			_animationToggleTimer = new System.Windows.Threading.DispatcherTimer
			{
				Interval = TimeSpan.FromSeconds(5)
			};
			_animationToggleTimer.Tick += (s, e) =>
			{
				ShowPrecipChance = !ShowPrecipChance;
			};
		}

		public void SetAnimationTimerState(bool isRunning)
		{
			if (isRunning) _animationToggleTimer?.Start();
			else _animationToggleTimer?.Stop();
		}

		private bool _showPrecipChance = false;
		public bool ShowPrecipChance
		{
			get => _showPrecipChance;
			set
			{
				_showPrecipChance = value;
				OnPropertyChanged();
				OnPropertyChanged(nameof(ShowPrecipAmount));
			}
		}

		public bool ShowPrecipAmount => !ShowPrecipChance;

		private void EvaluateTimeState()
		{
			if (_currentWeather == null) return;

			var now = DateTime.Now;
			bool listsChanged = false;

			if (DateTime.TryParse(_currentWeather.Sunrise, out DateTime parsedSunrise))
			{
				SunriseOpacity = now.TimeOfDay > parsedSunrise.TimeOfDay ? 0.3 : 1.0;
			}
			if (DateTime.TryParse(_currentWeather.Sunset, out DateTime parsedSunset))
			{
				SunsetOpacity = now.TimeOfDay > parsedSunset.TimeOfDay ? 0.3 : 1.0;
			}

			if (now.Hour != _lastEvaluatedHour && _pristineWeather?.HourlyData != null)
			{
				_lastEvaluatedHour = now.Hour;
				var originalWindow = _pristineWeather.HourlyData.GetRange(0, Math.Min(24, _pristineWeather.HourlyData.Count));
				var validHours = originalWindow.FindAll(h => h.RawTime >= now.Date.AddHours(now.Hour));

				if (validHours.Count > 0)
				{
					foreach (var h in validHours)
					{
						if (string.IsNullOrEmpty(h.BaseTime)) h.BaseTime = h.Time;
						if (!string.IsNullOrEmpty(h.BaseTime)) h.Time = h.BaseTime;
					}

					bool hourlyCountMatches = _currentWeather!.HourlyData != null && _currentWeather.HourlyData.Count == validHours.Count;
					if (hourlyCountMatches)
					{
						for (int i = 0; i < validHours.Count; i++)
						{
							_currentWeather.HourlyData![i].CopyFrom(validHours[i]);
						}
					}
					else
					{
						var newList = new System.Collections.Generic.List<HourlyForecast>(validHours.Count);
						foreach (var vh in validHours)
						{
							var copy = new HourlyForecast();
							copy.CopyFrom(vh);
							newList.Add(copy);
						}
						_currentWeather.HourlyData = newList;
						_currentWeather.NotifyPropertyChanged(nameof(CachedWeatherData.HourlyData));
					}

					_currentWeather.Temp = validHours[0].Temp;
					_currentWeather.FeelsLike = validHours[0].FeelsLike;
					_currentWeather.Status = validHours[0].Status;
					_currentWeather.WeatherCode = validHours[0].WeatherCode;
					_currentWeather.IconPath = validHours[0].IconPath;
					_currentWeather.IsDay = validHours[0].IsDay;
					_currentWeather.Humidity = validHours[0].Humidity;
					_currentWeather.Precip = validHours[0].Precip;
					_currentWeather.PrecipChance = validHours[0].PrecipChance;
					_currentWeather.WindSpeed = validHours[0].WindSpeed;
					_currentWeather.WindDir = validHours[0].WindDir;
					_currentWeather.WindDegrees = validHours[0].WindAngle;
					_currentWeather.UvIndex = validHours[0].UvIndex;
					_currentWeather.UvRating = validHours[0].UvRating;
					_currentWeather.Aqi = validHours[0].Aqi;
					_currentWeather.AqiRating = validHours[0].AqiRating;

					if (!string.IsNullOrEmpty(SelectedLocationName))
					{
						AppConfig.Current.WeatherCache[SelectedLocationName] = _currentWeather;
						System.Windows.Application.Current.Dispatcher.Invoke(() =>
						{
							if (System.Windows.Application.Current.MainWindow is MainWindow mw)
							{
								mw.RefreshTrayIcon(debounced: true);
							}
						});
					}

					listsChanged = true;
				}
			}

			if (now.Day != _lastEvaluatedDay && _pristineWeather?.DailyData != null)
			{
				_lastEvaluatedDay = now.Day;
				var originalDays = _pristineWeather.DailyData.GetRange(0, Math.Min(10, _pristineWeather.DailyData.Count));
				var validDays = originalDays.FindAll(d => d.RawDate >= now.Date);

				if (validDays.Count > 0)
				{
					foreach (var d in validDays)
					{
						if (string.IsNullOrEmpty(d.BaseDate) && !d.Date.StartsWith("Today")) d.BaseDate = d.Date;
						if (string.IsNullOrEmpty(d.BaseAgendaDateHeader) && !d.AgendaDateHeader.StartsWith("Today")) d.BaseAgendaDateHeader = d.AgendaDateHeader;

						if (!string.IsNullOrEmpty(d.BaseDate)) d.Date = d.BaseDate;
						if (!string.IsNullOrEmpty(d.BaseAgendaDateHeader)) d.AgendaDateHeader = d.BaseAgendaDateHeader;
					}

					var firstDay = validDays[0];
					string suffix = (firstDay.RawDate.Day % 10 == 1 && firstDay.RawDate.Day != 11) ? "st" : (firstDay.RawDate.Day % 10 == 2 && firstDay.RawDate.Day != 12) ? "nd" : (firstDay.RawDate.Day % 10 == 3 && firstDay.RawDate.Day != 13) ? "rd" : "th";
					firstDay.Date = $"Today, {firstDay.RawDate:MMM d}";
					firstDay.AgendaDateHeader = $"Today, {firstDay.RawDate:MMMM d}{suffix}";

					bool dailyCountMatches = _currentWeather!.DailyData != null && _currentWeather.DailyData.Count == validDays.Count;
					if (dailyCountMatches)
					{
						for (int i = 0; i < validDays.Count; i++)
						{
							_currentWeather.DailyData![i].CopyFrom(validDays[i]);
						}
					}
					else
					{
						var newList = new System.Collections.Generic.List<DailyForecast>(validDays.Count);
						foreach (var vd in validDays)
						{
							var copy = new DailyForecast();
							copy.CopyFrom(vd);
							newList.Add(copy);
						}
						_currentWeather.DailyData = newList;
						_currentWeather.NotifyPropertyChanged(nameof(CachedWeatherData.DailyData));
					}

					listsChanged = true;
				}
			}

			if (listsChanged)
			{
				_currentWeather.NotifyAllPropertiesChanged();
				WeatherArraysTruncated?.Invoke();
			}
		}

		private CachedWeatherData? _pristineWeather;
		private string _connectionStatusPrefix = "";
		public string ConnectionStatusPrefix
		{
			get => _connectionStatusPrefix;
			set { _connectionStatusPrefix = value; OnPropertyChanged(); }
		}

		private CachedWeatherData? _currentWeather;
		public CachedWeatherData? CurrentWeather
		{
			get => _currentWeather;
			set
			{
				if (value != null)
				{
					_pristineWeather = new CachedWeatherData();
					_pristineWeather.CopyScalarPropertiesFrom(value);
					_pristineWeather.HourlyData = value.HourlyData?.Select(h => { var c = new HourlyForecast(); c.CopyFrom(h); return c; }).ToList() ?? [];
					_pristineWeather.DailyData = value.DailyData?.Select(d => { var c = new DailyForecast(); c.CopyFrom(d); return c; }).ToList() ?? [];

					if (_currentWeather == null)
					{
						_currentWeather = new CachedWeatherData();
						_currentWeather.CopyScalarPropertiesFrom(value);
						_currentWeather.HourlyData = value.HourlyData?.Select(h => { var c = new HourlyForecast(); c.CopyFrom(h); return c; }).ToList() ?? [];
						_currentWeather.DailyData = value.DailyData?.Select(d => { var c = new DailyForecast(); c.CopyFrom(d); return c; }).ToList() ?? [];
						_lastEvaluatedHour = -1;
						_lastEvaluatedDay = -1;
						EvaluateTimeState();
						EvaluatePollutants();
						OnPropertyChanged();
					}
					else
					{
						_currentWeather.CopyScalarPropertiesFrom(value);
						_lastEvaluatedHour = -1;
						_lastEvaluatedDay = -1;
						EvaluateTimeState();
						EvaluatePollutants();
						_currentWeather.NotifyAllPropertiesChanged();
					}
				}
				else
				{
					_pristineWeather = null;
					_currentWeather = null;
					OnPropertyChanged();
				}

				OnPropertyChanged(nameof(FormattedSummary));
			}
		}

		private string _primaryPollutantHoverName = "--";
		public string PrimaryPollutantHoverName
		{
			get => _primaryPollutantHoverName;
			set { _primaryPollutantHoverName = value; OnPropertyChanged(); }
		}

		private string _primaryPollutantBaseName = "--";
		public string PrimaryPollutantBaseName
		{
			get => _primaryPollutantBaseName;
			set { _primaryPollutantBaseName = value; OnPropertyChanged(); }
		}

		private string _primaryPollutantSubscript = "";
		public string PrimaryPollutantSubscript
		{
			get => _primaryPollutantSubscript;
			set { _primaryPollutantSubscript = value; OnPropertyChanged(); }
		}

		private string _primaryPollutantValue = "--";
		public string PrimaryPollutantValue
		{
			get => _primaryPollutantValue;
			set { _primaryPollutantValue = value; OnPropertyChanged(); }
		}

		private string _primaryPollutantRating = "--";
		public string PrimaryPollutantRating
		{
			get => _primaryPollutantRating;
			set { _primaryPollutantRating = value; OnPropertyChanged(); }
		}

		private System.Collections.ObjectModel.ObservableCollection<PollutantDisplayInfo> _secondaryPollutants = [];
		public System.Collections.ObjectModel.ObservableCollection<PollutantDisplayInfo> SecondaryPollutants
		{
			get => _secondaryPollutants;
			set { _secondaryPollutants = value; OnPropertyChanged(); }
		}

		private void EvaluatePollutants()
		{
			if (_currentWeather == null) return;

			string primary = _currentWeather.PrimaryPollutant ?? "";

			var allPollutants = new System.Collections.Generic.List<PollutantDisplayInfo>
			{
				new() { Id = "PM2.5", BaseName = "PM", Subscript = "2.5", HoverName = "Fine Particulates", PeakValue = _currentWeather.PeakPm25, Unit = "μg/m³", Rating = _currentWeather.RatingPm25 },
				new() { Id = "PM10", BaseName = "PM", Subscript = "10", HoverName = "Coarse Particulates", PeakValue = _currentWeather.PeakPm10, Unit = "μg/m³", Rating = _currentWeather.RatingPm10 },
				new() { Id = "O3", BaseName = "O", Subscript = "3", HoverName = "O-Zone", PeakValue = _currentWeather.PeakO3, Unit = "μg/m³", Rating = _currentWeather.RatingO3 },
				new() { Id = "NO2", BaseName = "NO", Subscript = "2", HoverName = "Nitrogen Dioxide", PeakValue = _currentWeather.PeakNo2, Unit = "μg/m³", Rating = _currentWeather.RatingNo2 },
				new() { Id = "Pollen", BaseName = "Pollen", Subscript = "", HoverName = "Airborne Pollen", PeakValue = _currentWeather.PeakPollen, Unit = "grains/m³", Rating = _currentWeather.RatingPollen }
			};

			var primaryPollutant = allPollutants.Find(p => string.Equals(p.Id, primary, StringComparison.OrdinalIgnoreCase) || string.Equals(p.BaseName, primary, StringComparison.OrdinalIgnoreCase)) ?? allPollutants[0];

			PrimaryPollutantHoverName = primaryPollutant.HoverName;
			PrimaryPollutantBaseName = primaryPollutant.BaseName;
			PrimaryPollutantSubscript = primaryPollutant.Subscript;
			PrimaryPollutantValue = $"{primaryPollutant.PeakValue} {primaryPollutant.Unit}";
			PrimaryPollutantRating = primaryPollutant.Rating;

			var secondary = allPollutants.FindAll(p => p.Id != primaryPollutant.Id);
			secondary.Sort((a, b) =>
			{
				double valA = double.TryParse(a.PeakValue, out double vA) ? vA : -1;
				double valB = double.TryParse(b.PeakValue, out double vB) ? vB : -1;
				return valB.CompareTo(valA); // Sort descending by severity
			});

			SecondaryPollutants = new System.Collections.ObjectModel.ObservableCollection<PollutantDisplayInfo>(secondary);
		}

		public string FormattedSummary
		{
			get
			{
				if (CurrentWeather == null || string.IsNullOrWhiteSpace(CurrentWeather.DailySummary)) return "";
				string summary = CurrentWeather.DailySummary;
				if (summary.Length > 0)
				{
					summary = char.ToLower(summary[0]) + summary[1..];
				}
				return $"For {City}, today will {summary}";
			}
		}

		private double _windRotationAngle = 0;
		public double WindRotationAngle
		{
			get => _windRotationAngle;
			set { _windRotationAngle = value; OnPropertyChanged(); }
		}

		private double _sunriseOpacity = 1.0;
		public double SunriseOpacity
		{
			get => _sunriseOpacity;
			set { _sunriseOpacity = value; OnPropertyChanged(); }
		}

		private double _sunsetOpacity = 1.0;
		public double SunsetOpacity
		{
			get => _sunsetOpacity;
			set { _sunsetOpacity = value; OnPropertyChanged(); }
		}

		private string _radarTime = "--:--";
		public string RadarTime
		{
			get => _radarTime;
			set { _radarTime = value; OnPropertyChanged(); }
		}

		private string _loadingText = "Taking a peek...";
		public string LoadingText
		{
			get => _loadingText;
			set { _loadingText = value; OnPropertyChanged(); }
		}

		private string _precipUnit = "in";
		public string PrecipUnit
		{
			get => _precipUnit;
			set { _precipUnit = value; OnPropertyChanged(); }
		}

		private string _precipUnitShort = "\"";
		public string PrecipUnitShort
		{
			get => _precipUnitShort;
			set { _precipUnitShort = value; OnPropertyChanged(); }
		}

		private string _currentActiveView = "Current";
		public string CurrentActiveView
		{
			get => _currentActiveView;
			set { _currentActiveView = value; OnPropertyChanged(); }
		}

		private bool _useAnimatedIcons = true;
		public bool UseAnimatedIcons
		{
			get => _useAnimatedIcons;
			set { _useAnimatedIcons = value; OnPropertyChanged(); }
		}

		private bool _isWeatherAnimationSelected = false;
		public bool IsWeatherAnimationSelected
		{
			get => _isWeatherAnimationSelected;
			set { _isWeatherAnimationSelected = value; OnPropertyChanged(); }
		}

		private bool _isLocationMenuSelected = false;
		private bool _isAlertIconSelected;
		public bool IsAlertIconSelected
		{
			get => _isAlertIconSelected;
			set { _isAlertIconSelected = value; OnPropertyChanged(nameof(IsAlertIconSelected)); }
		}

		public bool IsLocationMenuSelected
		{
			get => _isLocationMenuSelected;
			set { _isLocationMenuSelected = value; OnPropertyChanged(); }
		}

		private static readonly string[] _loadingPhrases =
		[
			"Taking a peek...",
			"Peeking into the clouds...",
			"Peeking at the sky...",
			"Peeking around...",
			"Peeking around radar..."
		];
		private static readonly Random _rand = new();

		private bool _isLoading = false;
		public bool IsLoading
		{
			get => _isLoading;
			set
			{
				_isLoading = value;
				if (!_isLoading)
				{
					LoadingText = _loadingPhrases[_rand.Next(_loadingPhrases.Length)];
				}
				OnPropertyChanged();
			}
		}
		public AlertModel AlertState { get; } = new();

		private System.Windows.Visibility _onboardingVisibility = AppConfig.Current.HasSeenOnboarding ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
		public System.Windows.Visibility OnboardingVisibility
		{
			get => _onboardingVisibility;
			set { _onboardingVisibility = value; OnPropertyChanged(); }
		}

		public void DismissOnboarding()
		{
			OnboardingVisibility = System.Windows.Visibility.Collapsed;
			AppConfig.Current.HasSeenOnboarding = true;
			SettingsManager.Save();
		}

		public event Action? WeatherArraysTruncated;
	}

	public class PollutantDisplayInfo : INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler? PropertyChanged { add { } remove { } }
		public string Id { get; set; } = "";
		public string BaseName { get; set; } = "";
		public string Subscript { get; set; } = "";
		public string HoverName { get; set; } = "";
		public string PeakValue { get; set; } = "";
		public string Unit { get; set; } = "μg/m³";
		public string Rating { get; set; } = "";
	}
}