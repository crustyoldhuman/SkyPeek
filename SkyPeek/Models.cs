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
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;

namespace SkyPeek
{
	public static partial class HyperlinkExtensions
	{
		[GeneratedRegex(@"(https?:\/\/[^\s]+)", RegexOptions.IgnoreCase)]
		private static partial Regex UrlRegex();

		public static readonly DependencyProperty InteractiveTextProperty =
			DependencyProperty.RegisterAttached("InteractiveText", typeof(string), typeof(HyperlinkExtensions), new PropertyMetadata(null, OnInteractiveTextChanged));

		public static void SetInteractiveText(DependencyObject obj, string value) => obj.SetValue(InteractiveTextProperty, value);
		public static string GetInteractiveText(DependencyObject obj) => (string)obj.GetValue(InteractiveTextProperty);

		private static void OnInteractiveTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			if (d is System.Windows.Controls.RichTextBox rtb)
			{
				rtb.Document.Blocks.Clear();
				string text = e.NewValue as string ?? string.Empty;
				var paragraph = new System.Windows.Documents.Paragraph { Margin = new System.Windows.Thickness(0) };
				int lastPos = 0;

				foreach (System.Text.RegularExpressions.Match match in UrlRegex().Matches(text))
				{
					if (match.Index > lastPos)
						paragraph.Inlines.Add(new System.Windows.Documents.Run(text[lastPos..match.Index]));

					if (Uri.TryCreate(match.Value, UriKind.Absolute, out Uri? uri))
					{
						var hyperlink = new System.Windows.Documents.Hyperlink(new System.Windows.Documents.Run(match.Value)) { NavigateUri = uri };
						hyperlink.RequestNavigate += (sender, args) =>
						{
							try
							{
								System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(args.Uri.AbsoluteUri) { UseShellExecute = true });
							}
							catch { } // Swallow the Win32Exception if the OS fails to launch the browser
							args.Handled = true;
						};
						paragraph.Inlines.Add(hyperlink);
					}
					else
					{
						paragraph.Inlines.Add(new System.Windows.Documents.Run(match.Value));
					}
					lastPos = match.Index + match.Length;
				}

				if (lastPos < text.Length)
					paragraph.Inlines.Add(new System.Windows.Documents.Run(text[lastPos..]));

				rtb.Document.Blocks.Add(paragraph);
			}
		}

		public static readonly DependencyProperty InteractiveAlertItemProperty =
			DependencyProperty.RegisterAttached("InteractiveAlertItem", typeof(WeatherAlert), typeof(HyperlinkExtensions), new PropertyMetadata(null, OnInteractiveAlertItemChanged));

		public static void SetInteractiveAlertItem(DependencyObject obj, WeatherAlert value) => obj.SetValue(InteractiveAlertItemProperty, value);
		public static WeatherAlert GetInteractiveAlertItem(DependencyObject obj) => (WeatherAlert)obj.GetValue(InteractiveAlertItemProperty);

		private static void OnInteractiveAlertItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			if (d is System.Windows.Controls.RichTextBox rtb && e.NewValue is WeatherAlert alert)
			{
				rtb.Document.Blocks.Clear();

				var headerBrush = new System.Windows.Media.SolidColorBrush(alert.SeverityColor);
				headerBrush.Freeze();
				var headerPara = new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(alert.Event.Trim()))
				{
					FontSize = 16,
					FontWeight = System.Windows.FontWeights.Bold,
					Foreground = headerBrush,
					Margin = new System.Windows.Thickness(0, 0, 20, 10)
				};
				rtb.Document.Blocks.Add(headerPara);

				string text = alert.Description?.Trim() ?? string.Empty;
				var descPara = new System.Windows.Documents.Paragraph { Margin = new System.Windows.Thickness(0) };
				int lastPos = 0;

				foreach (System.Text.RegularExpressions.Match match in UrlRegex().Matches(text))
				{
					if (match.Index > lastPos)
						descPara.Inlines.Add(new System.Windows.Documents.Run(text[lastPos..match.Index]));

					if (Uri.TryCreate(match.Value, UriKind.Absolute, out Uri? uri))
					{
						var hyperlink = new System.Windows.Documents.Hyperlink(new System.Windows.Documents.Run(match.Value)) { NavigateUri = uri };
						hyperlink.RequestNavigate += (sender, args) =>
						{
							try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(args.Uri.AbsoluteUri) { UseShellExecute = true }); } catch { }
							args.Handled = true;
						};
						descPara.Inlines.Add(hyperlink);
					}
					else
					{
						descPara.Inlines.Add(new System.Windows.Documents.Run(match.Value));
					}
					lastPos = match.Index + match.Length;
				}

				if (lastPos < text.Length)
					descPara.Inlines.Add(new System.Windows.Documents.Run(text[lastPos..]));

				rtb.Document.Blocks.Add(descPara);
			}
		}

	}

	public class LocationAlertGroup : System.ComponentModel.INotifyPropertyChanged
	{
		public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged { add { } remove { } }
		public string LocationName { get; set; } = "";
		public System.Collections.Generic.List<WeatherAlert> Alerts { get; set; } = [];
	}

	public class ComboMenuAction : System.ComponentModel.INotifyPropertyChanged
	{
		public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged { add { } remove { } }
		public string DisplayName { get; set; } = "";
		public bool IsHome { get; set; } = false;
		public bool IsWindowsLocation { get; set; } = false;
		public bool HasActiveAlert { get; set; } = false;
		public System.Windows.Visibility ActiveAlertVisibility { get; set; } = System.Windows.Visibility.Collapsed;
		public System.Windows.Media.Brush AlertIconColor { get; set; } = System.Windows.Media.Brushes.Transparent;
	}

	public class WeatherAlert : System.ComponentModel.INotifyPropertyChanged
	{
		public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

		public string Id { get; set; } = "";
		public string Event { get; set; } = "";
		public string Sender { get; set; } = "";
		public string Description { get; set; } = "";
		public System.Windows.Media.Color SeverityColor { get; set; } = System.Windows.Media.Color.FromRgb(205, 90, 90);
		public int SeverityLevel { get; set; } = 1;

		private bool _isNew = false;
		public bool IsNew
		{
			get => _isNew;
			set { _isNew = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsNew))); }
		}
	}

	public class HourlyForecast : System.ComponentModel.INotifyPropertyChanged
	{
		public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
		private void Notify(string prop) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(prop));

		public DateTime RawTime { get; set; }
		public string BaseTime { get; set; } = "";

		private string _time = "";
		public string Time
		{
			get
			{
				if (RawTime.Date > DateTime.Now.Date && !_time.Contains(','))
				{
					return $"{_time}, {RawTime:MMM d}";
				}
				return _time;
			}
			set { _time = value; Notify(nameof(Time)); }
		}

		public string Location { get; set; } = "";
		public string Status { get; set; } = "";
		public int WeatherCode { get; set; }
		public string Temp { get; set; } = "";
		public string FeelsLike { get; set; } = "";
		public string Precip { get; set; } = "";
		public string PrecipChance { get; set; } = "";
		public string Humidity { get; set; } = "";
		public string UvIndex { get; set; } = "";
		public string UvRating { get; set; } = "";
		public string Aqi { get; set; } = "--";
		public string AqiRating { get; set; } = "--";
		public string WindSpeed { get; set; } = "";
		public string WindDir { get; set; } = "";
		public string WindSpeedUnit { get; set; } = "mph";
		public int WindAngle { get; set; }
		public bool IsDay { get; set; }
		private string? _iconPath;
		public string? IconPath
		{
			get => _iconPath;
			set
			{
				string? norm = string.IsNullOrWhiteSpace(value) ? null : value;
				if (_iconPath != norm)
				{
					_iconPath = norm;
					Notify(nameof(IconPath));
				}
			}
		}
		public string SolarEventTime { get; set; } = "";
		public string SolarEventIconData { get; set; } = "";
		public Visibility SolarEventVisibility { get; set; } = Visibility.Collapsed;

		public void CopyFrom(HourlyForecast other)
		{
			RawTime = other.RawTime;
			BaseTime = other.BaseTime;
			_time = other._time;
			Location = other.Location;
			Status = other.Status;
			WeatherCode = other.WeatherCode;
			Temp = other.Temp;
			FeelsLike = other.FeelsLike;
			Precip = other.Precip;
			PrecipChance = other.PrecipChance;
			Humidity = other.Humidity;
			UvIndex = other.UvIndex;
			UvRating = other.UvRating;
			Aqi = other.Aqi;
			AqiRating = other.AqiRating;
			WindSpeed = other.WindSpeed;
			WindDir = other.WindDir;
			WindSpeedUnit = other.WindSpeedUnit;
			WindAngle = other.WindAngle;
			IsDay = other.IsDay;
			IconPath = other.IconPath;
			SolarEventTime = other.SolarEventTime;
			SolarEventIconData = other.SolarEventIconData;
			SolarEventVisibility = other.SolarEventVisibility;
			Notify(string.Empty);
		}
	}

	public class DailyForecast : System.ComponentModel.INotifyPropertyChanged
	{
		public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
		private void Notify(string prop) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(prop));

		public DateTime RawDate { get; set; }
		public string BaseDate { get; set; } = "";
		public string BaseAgendaDateHeader { get; set; } = "";

		private string _date = "";
		public string Date
		{
			get => _date;
			set { _date = value; Notify(nameof(Date)); }
		}

		public string Weekday { get; set; } = "";

		private string _agendaDateHeader = "";
		public string AgendaDateHeader
		{
			get => _agendaDateHeader;
			set { _agendaDateHeader = value; Notify(nameof(AgendaDateHeader)); }
		}
		public string Location { get; set; } = "";
		public string Status { get; set; } = "";
		public string High { get; set; } = "";
		public string Low { get; set; } = "";
		public string FeelsLikeHigh { get; set; } = "";
		public string FeelsLikeLow { get; set; } = "";
		public string PrecipChance { get; set; } = "";
		public string Precip { get; set; } = "";
		public string Humidity { get; set; } = "";
		public string UvIndex { get; set; } = "";
		public string UvRating { get; set; } = "";
		public string Aqi { get; set; } = "--";
		public string AqiRating { get; set; } = "--";
		public string WindSpeed { get; set; } = "";
		public string WindDir { get; set; } = "";
		public string WindSpeedUnit { get; set; } = "mph";
		public int WindAngle { get; set; }
		public string Sunrise { get; set; } = "";
		public string Sunset { get; set; } = "";
		private string? _iconPath;
		public string? IconPath
		{
			get => _iconPath;
			set
			{
				string? norm = string.IsNullOrWhiteSpace(value) ? null : value;
				if (_iconPath != norm)
				{
					_iconPath = norm;
					Notify(nameof(IconPath));
				}
			}
		}

		public void CopyFrom(DailyForecast other)
		{
			RawDate = other.RawDate;
			BaseDate = other.BaseDate;
			BaseAgendaDateHeader = other.BaseAgendaDateHeader;
			_date = other._date;
			Weekday = other.Weekday;
			_agendaDateHeader = other._agendaDateHeader;
			Location = other.Location;
			Status = other.Status;
			High = other.High;
			Low = other.Low;
			FeelsLikeHigh = other.FeelsLikeHigh;
			FeelsLikeLow = other.FeelsLikeLow;
			PrecipChance = other.PrecipChance;
			Precip = other.Precip;
			Humidity = other.Humidity;
			UvIndex = other.UvIndex;
			UvRating = other.UvRating;
			Aqi = other.Aqi;
			AqiRating = other.AqiRating;
			WindSpeed = other.WindSpeed;
			WindDir = other.WindDir;
			WindSpeedUnit = other.WindSpeedUnit;
			WindAngle = other.WindAngle;
			Sunrise = other.Sunrise;
			Sunset = other.Sunset;
			IconPath = other.IconPath;
			Notify(string.Empty);
		}
	}
	public class GeoSearchResult : System.ComponentModel.INotifyPropertyChanged
	{
		public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged { add { } remove { } }
		public string Name { get; set; } = "";
		public string Admin1 { get; set; } = "";

		private string _country = "";
		public string Country
		{
			get => _country;
			set => _country = (value == "United States" || value == "United States of America") ? "USA" : value;
		}

		public string DisplayName
		{
			get
			{
				string region = string.IsNullOrEmpty(StateAbbrev) ? CountryAbbrev : StateAbbrev;
				return string.IsNullOrEmpty(region) ? Name : $"{Name}, {region}";
			}
		}

		private static readonly System.Collections.Generic.Dictionary<string, string> _stateAbbreviations = new(System.StringComparer.OrdinalIgnoreCase)
		{
			{"Alabama", "AL"}, {"Alaska", "AK"}, {"Arizona", "AZ"}, {"Arkansas", "AR"}, {"California", "CA"}, {"Colorado", "CO"}, {"Connecticut", "CT"}, {"Delaware", "DE"}, {"Florida", "FL"}, {"Georgia", "GA"}, {"Hawaii", "HI"}, {"Idaho", "ID"}, {"Illinois", "IL"}, {"Indiana", "IN"}, {"Iowa", "IA"}, {"Kansas", "KS"}, {"Kentucky", "KY"}, {"Louisiana", "LA"}, {"Maine", "ME"}, {"Maryland", "MD"}, {"Massachusetts", "MA"}, {"Michigan", "MI"}, {"Minnesota", "MN"}, {"Mississippi", "MS"}, {"Missouri", "MO"}, {"Montana", "MT"}, {"Nebraska", "NE"}, {"Nevada", "NV"}, {"New Hampshire", "NH"}, {"New Jersey", "NJ"}, {"New Mexico", "NM"}, {"New York", "NY"}, {"North Carolina", "NC"}, {"North Dakota", "ND"}, {"Ohio", "OH"}, {"Oklahoma", "OK"}, {"Oregon", "OR"}, {"Pennsylvania", "PA"}, {"Rhode Island", "RI"}, {"South Carolina", "SC"}, {"South Dakota", "SD"}, {"Tennessee", "TN"}, {"Texas", "TX"}, {"Utah", "UT"}, {"Vermont", "VT"}, {"Virginia", "VA"}, {"Washington", "WA"}, {"West Virginia", "WV"}, {"Wisconsin", "WI"}, {"Wyoming", "WY"},
			{"Alberta", "AB"}, {"British Columbia", "BC"}, {"Manitoba", "MB"}, {"New Brunswick", "NB"}, {"Newfoundland and Labrador", "NL"}, {"Northwest Territories", "NT"}, {"Nova Scotia", "NS"}, {"Nunavut", "NU"}, {"Ontario", "ON"}, {"Prince Edward Island", "PE"}, {"Quebec", "QC"}, {"Saskatchewan", "SK"}, {"Yukon", "YT"},
			{"New South Wales", "NSW"}, {"Victoria", "VIC"}, {"Queensland", "QLD"}, {"Western Australia", "WA"}, {"South Australia", "SA"}, {"Tasmania", "TAS"}, {"Australian Capital Territory", "ACT"}, {"Northern Territory", "NT"},
			{"England", "ENG"}, {"Scotland", "SCT"}, {"Wales", "WAL"}, {"Northern Ireland", "NIR"}
		};

		private string StateAbbrev
		{
			get
			{
				if (string.IsNullOrEmpty(Admin1)) return "";
				return _stateAbbreviations.TryGetValue(Admin1, out string? abbrev) ? abbrev : Admin1;
			}
		}

		private static readonly System.Collections.Generic.Dictionary<string, string> _countryAbbreviations = new(System.StringComparer.OrdinalIgnoreCase)
		{
			{"United States", "USA"}, {"United Kingdom", "UK"}, {"New Zealand", "NZ"}
		};

		private string CountryAbbrev
		{
			get
			{
				if (string.IsNullOrEmpty(Country)) return "";
				return _countryAbbreviations.TryGetValue(Country, out string? abbrev) ? abbrev : Country;
			}
		}
	}
	public class CachedWeatherData : System.ComponentModel.INotifyPropertyChanged
	{
		public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
		public void NotifyAllPropertiesChanged() => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(string.Empty));
		public void NotifyPropertyChanged(string propName) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propName));

		public void CopyScalarPropertiesFrom(CachedWeatherData other)
		{
			LastUpdated = other.LastUpdated;
			Status = other.Status;
			Temp = other.Temp;
			FeelsLike = other.FeelsLike;
			High = other.High;
			Low = other.Low;
			Precip = other.Precip;
			PrecipChance = other.PrecipChance;
			Humidity = other.Humidity;
			UvIndex = other.UvIndex;
			UvRating = other.UvRating;
			Aqi = other.Aqi;
			AqiRating = other.AqiRating;
			WindSpeed = other.WindSpeed;
			WindDir = other.WindDir;
			WindSpeedUnit = other.WindSpeedUnit;
			WindDegrees = other.WindDegrees;
			IsDay = other.IsDay;
			WeatherCode = other.WeatherCode;
			IconPath = other.IconPath;
			Sunrise = other.Sunrise;
			Sunset = other.Sunset;
			DailySummary = other.DailySummary;
			PrimaryPollutant = other.PrimaryPollutant;
			PeakPm25 = other.PeakPm25;
			PeakPm10 = other.PeakPm10;
			PeakO3 = other.PeakO3;
			PeakNo2 = other.PeakNo2;
			PeakPollen = other.PeakPollen;
			RatingPm25 = other.RatingPm25;
			RatingPm10 = other.RatingPm10;
			RatingO3 = other.RatingO3;
			RatingNo2 = other.RatingNo2;
			RatingPollen = other.RatingPollen;
			Latitude = other.Latitude;
			Longitude = other.Longitude;
			AlertsData = other.AlertsData != null ? [.. other.AlertsData] : [];
		}

		public DateTime LastUpdated { get; set; }
		public string Status { get; set; } = "--";
		public string Temp { get; set; } = "--";
		public string FeelsLike { get; set; } = "--";
		public string High { get; set; } = "--";
		public string Low { get; set; } = "--";
		public string Precip { get; set; } = "--";
		public string PrecipChance { get; set; } = "--";
		public string Humidity { get; set; } = "--";
		public string UvIndex { get; set; } = "--";
		public string UvRating { get; set; } = "--";
		public string Aqi { get; set; } = "--";
		public string AqiRating { get; set; } = "--";
		public string WindSpeed { get; set; } = "--";
		public string WindDir { get; set; } = "--";
		public string WindSpeedUnit { get; set; } = "mph";
		public int WindDegrees { get; set; } = 0;
		public bool IsDay { get; set; } = true;
		public int WeatherCode { get; set; } = 0;
		public string? IconPath { get; set; } = null;
		public string Sunrise { get; set; } = "--:--";
		public string Sunset { get; set; } = "--:--";
		public string DailySummary { get; set; } = "";
		public string PrimaryPollutant { get; set; } = "--";
		public string PeakPm25 { get; set; } = "--";
		public string PeakPm10 { get; set; } = "--";
		public string PeakO3 { get; set; } = "--";
		public string PeakNo2 { get; set; } = "--";
		public string PeakPollen { get; set; } = "--";
		public string RatingPm25 { get; set; } = "--";
		public string RatingPm10 { get; set; } = "--";
		public string RatingO3 { get; set; } = "--";
		public string RatingNo2 { get; set; } = "--";
		public string RatingPollen { get; set; } = "--";
		public double Latitude { get; set; }
		public double Longitude { get; set; }
		public System.Collections.Generic.List<HourlyForecast> HourlyData { get; set; } = [];
		public System.Collections.Generic.List<DailyForecast> DailyData { get; set; } = [];
		public System.Collections.Generic.List<WeatherAlert> AlertsData { get; set; } = [];
	}
	[System.Runtime.Versioning.SupportedOSPlatform("windows10.0.19041.0")]
	public class SavedLocation : System.ComponentModel.INotifyPropertyChanged
	{
		public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

		public string City { get; set; } = "";
		public string State { get; set; } = "";

		private string _country = "";
		public string Country
		{
			get => _country;
			set => _country = (value == "United States" || value == "United States of America") ? "USA" : value;
		}
		public bool AddedByGps { get; set; } = false;

		private int _alertFilterLevel = 1; // 0 = Off, 1 = Blue (All), 2 = Yellow (Mod+), 3 = Orange (Sev+), 4 = Red (Ext)
		public int AlertFilterLevel
		{
			get => _alertFilterLevel;
			set
			{
				if (_alertFilterLevel != value)
				{
					_alertFilterLevel = value;
					PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(AlertFilterLevel)));
					PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(AlertFilterColor)));
					PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(AlertFilterToolTip)));
					PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(ToneToolTip)));
					PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(ToneOpacity)));
					PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsToneInteractionEnabled)));
				}
			}
		}

		private bool _playAlertTone = true;
		public bool PlayAlertTone
		{
			get => _playAlertTone;
			set
			{
				if (_playAlertTone != value)
				{
					_playAlertTone = value;
					PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(PlayAlertTone)));
					PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(ToneIconColor)));
					PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(ToneToolTip)));
				}
			}
		}

		private bool _hasActiveAlert;
		[System.Text.Json.Serialization.JsonIgnore]
		public bool HasActiveAlert
		{
			get => _hasActiveAlert;
			set
			{
				if (_hasActiveAlert != value)
				{
					_hasActiveAlert = value;
					PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(HasActiveAlert)));
					PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(ActiveAlertVisibility)));
				}
			}
		}

		[System.Text.Json.Serialization.JsonIgnore]
		public System.Windows.Visibility ActiveAlertVisibility => HasActiveAlert ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

		[System.Text.Json.Serialization.JsonIgnore]
		public System.Collections.Generic.List<string> ActiveAlertIds { get; set; } = [];

		[System.Text.Json.Serialization.JsonIgnore]
		public static double AlertToggleOpacity => 1.0;

		[System.Text.Json.Serialization.JsonIgnore]
		public System.Windows.Visibility AlertToggleVisibility
		{
			get
			{
				if (string.IsNullOrEmpty(City)) return System.Windows.Visibility.Collapsed;

				if (!AppConfig.Current.EnableWeatherAlerts) return System.Windows.Visibility.Collapsed;
				return System.Windows.Visibility.Visible;
			}
		}

		[System.Text.Json.Serialization.JsonIgnore]
		public System.Windows.Visibility ToneToggleVisibility
		{
			get
			{
				if (string.IsNullOrEmpty(City)) return System.Windows.Visibility.Collapsed;

				if (!AppConfig.Current.EnableWeatherAlerts || !AppConfig.Current.EnableAlertTone) return System.Windows.Visibility.Collapsed;
				return System.Windows.Visibility.Visible;
			}
		}

		public void UpdateAlertToggleOpacity()
		{
			PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(AlertToggleOpacity)));
			PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(AlertToggleVisibility)));
			PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(ToneToggleVisibility)));
		}

		private static System.Windows.Media.SolidColorBrush CreateFrozenBrush(byte r, byte g, byte b)
		{
			var brush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(r, g, b));
			brush.Freeze();
			return brush;
		}

		private static readonly System.Windows.Media.SolidColorBrush _filterBlueBrush = CreateFrozenBrush(17, 122, 139);
		private static readonly System.Windows.Media.SolidColorBrush _filterYellowBrush = CreateFrozenBrush(204, 153, 0);
		private static readonly System.Windows.Media.SolidColorBrush _filterOrangeBrush = CreateFrozenBrush(217, 104, 13);
		private static readonly System.Windows.Media.SolidColorBrush _filterRedBrush = CreateFrozenBrush(186, 40, 54);
		private static readonly System.Windows.Media.SolidColorBrush _filterGrayBrush = CreateFrozenBrush(105, 105, 105);
		private static readonly System.Windows.Media.SolidColorBrush _toneActiveBrush = CreateFrozenBrush(184, 142, 43);

		private System.Windows.Media.SolidColorBrush _alertIconColor = _filterYellowBrush;
		[System.Text.Json.Serialization.JsonIgnore]
		public System.Windows.Media.SolidColorBrush AlertIconColor
		{
			get => _alertIconColor;
			set
			{
				_alertIconColor = value;
				PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(AlertIconColor)));
			}
		}

		[System.Text.Json.Serialization.JsonIgnore]
		public System.Windows.Media.SolidColorBrush AlertFilterColor => AlertFilterLevel switch
		{
			1 => _filterBlueBrush,
			2 => _filterYellowBrush,
			3 => _filterOrangeBrush,
			4 => _filterRedBrush,
			_ => _filterGrayBrush
		};

		[System.Text.Json.Serialization.JsonIgnore]
		public string AlertFilterToolTip => AlertFilterLevel switch
		{
			1 => "Alerts: All (Minor, Moderate, Severe, Extreme)",
			2 => "Alerts: Moderate, Severe, Extreme",
			3 => "Alerts: Severe, Extreme",
			4 => "Alerts: Extreme Only",
			_ => "Alerts: Disabled"
		};

		[System.Text.Json.Serialization.JsonIgnore]
		public System.Windows.Media.SolidColorBrush ToneIconColor => PlayAlertTone ? _toneActiveBrush : _filterGrayBrush;

		[System.Text.Json.Serialization.JsonIgnore]
		public string ToneToolTip => AlertFilterLevel == 0 ? "Location alerts are set to none" : (PlayAlertTone ? "Audible alerts: On" : "Audible alerts: Off");

		[System.Text.Json.Serialization.JsonIgnore]
		public double ToneOpacity => AlertFilterLevel == 0 ? 0.3 : 1.0;

		[System.Text.Json.Serialization.JsonIgnore]
		public bool IsToneInteractionEnabled => AlertFilterLevel > 0;

		public string DisplayName
		{
			get
			{
				string region = string.IsNullOrEmpty(StateAbbrev) ? CountryAbbrev : StateAbbrev;
				return string.IsNullOrEmpty(region) ? City : $"{City}, {region}";
			}
		}

		private static readonly System.Collections.Generic.Dictionary<string, string> _stateAbbreviations = new(System.StringComparer.OrdinalIgnoreCase)
		{
			{"Alabama", "AL"}, {"Alaska", "AK"}, {"Arizona", "AZ"}, {"Arkansas", "AR"}, {"California", "CA"}, {"Colorado", "CO"}, {"Connecticut", "CT"}, {"Delaware", "DE"}, {"Florida", "FL"}, {"Georgia", "GA"}, {"Hawaii", "HI"}, {"Idaho", "ID"}, {"Illinois", "IL"}, {"Indiana", "IN"}, {"Iowa", "IA"}, {"Kansas", "KS"}, {"Kentucky", "KY"}, {"Louisiana", "LA"}, {"Maine", "ME"}, {"Maryland", "MD"}, {"Massachusetts", "MA"}, {"Michigan", "MI"}, {"Minnesota", "MN"}, {"Mississippi", "MS"}, {"Missouri", "MO"}, {"Montana", "MT"}, {"Nebraska", "NE"}, {"Nevada", "NV"}, {"New Hampshire", "NH"}, {"New Jersey", "NJ"}, {"New Mexico", "NM"}, {"New York", "NY"}, {"North Carolina", "NC"}, {"North Dakota", "ND"}, {"Ohio", "OH"}, {"Oklahoma", "OK"}, {"Oregon", "OR"}, {"Pennsylvania", "PA"}, {"Rhode Island", "RI"}, {"South Carolina", "SC"}, {"South Dakota", "SD"}, {"Tennessee", "TN"}, {"Texas", "TX"}, {"Utah", "UT"}, {"Vermont", "VT"}, {"Virginia", "VA"}, {"Washington", "WA"}, {"West Virginia", "WV"}, {"Wisconsin", "WI"}, {"Wyoming", "WY"},
			{"Alberta", "AB"}, {"British Columbia", "BC"}, {"Manitoba", "MB"}, {"New Brunswick", "NB"}, {"Newfoundland and Labrador", "NL"}, {"Northwest Territories", "NT"}, {"Nova Scotia", "NS"}, {"Nunavut", "NU"}, {"Ontario", "ON"}, {"Prince Edward Island", "PE"}, {"Quebec", "QC"}, {"Saskatchewan", "SK"}, {"Yukon", "YT"},
			{"New South Wales", "NSW"}, {"Victoria", "VIC"}, {"Queensland", "QLD"}, {"Western Australia", "WA"}, {"South Australia", "SA"}, {"Tasmania", "TAS"}, {"Australian Capital Territory", "ACT"}, {"Northern Territory", "NT"},
			{"England", "ENG"}, {"Scotland", "SCT"}, {"Wales", "WAL"}, {"Northern Ireland", "NIR"}
		};

		private string StateAbbrev
		{
			get
			{
				if (string.IsNullOrEmpty(State)) return "";
				return _stateAbbreviations.TryGetValue(State, out string? abbrev) ? abbrev : State;
			}
		}

		private static readonly System.Collections.Generic.Dictionary<string, string> _countryAbbreviations = new(System.StringComparer.OrdinalIgnoreCase)
		{
			{"United States", "USA"}, {"United Kingdom", "UK"}, {"New Zealand", "NZ"}
		};

		private string CountryAbbrev
		{
			get
			{
				if (string.IsNullOrEmpty(Country)) return "";
				return _countryAbbreviations.TryGetValue(Country, out string? abbrev) ? abbrev : Country;
			}
		}

		private bool _isHome;

		[System.Text.Json.Serialization.JsonIgnore]
		public bool IsHome
		{
			get => _isHome;
			set
			{
				if (_isHome != value)
				{
					_isHome = value;
					PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsHome)));
					PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(HomeToolTip)));
				}
			}
		}

		[System.Text.Json.Serialization.JsonIgnore]
		public string HomeToolTip => IsHome ? "Current home location" : "Set as home location";

		private bool _isWindowsLocation;

		[System.Text.Json.Serialization.JsonIgnore]
		public bool IsWindowsLocation
		{
			get => _isWindowsLocation;
			set
			{
				if (_isWindowsLocation != value)
				{
					_isWindowsLocation = value;
					PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsWindowsLocation)));
					PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(WindowsLocationVisibility)));
					PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(DeleteOpacity)));
				}
			}
		}

		[System.Text.Json.Serialization.JsonIgnore]
		public System.Windows.Visibility WindowsLocationVisibility => IsWindowsLocation ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

		[System.Text.Json.Serialization.JsonIgnore]
		public double DeleteOpacity => IsWindowsLocation ? 0.3 : 1.0;
	}

	public class LottieToStaticConverter : System.Windows.Data.IValueConverter
	{
		public object? Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			if (value is string path && !string.IsNullOrEmpty(path))
			{
				string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
				if (System.Windows.Application.Current.TryFindResource($"di_Static{fileName}_xaml") as System.Windows.Media.DrawingImage is { } img)
				{
					if (!img.IsFrozen && img.CanFreeze) img.Freeze();
					return img;
				}
			}
			return null;
		}
		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => throw new NotImplementedException();
	}
}