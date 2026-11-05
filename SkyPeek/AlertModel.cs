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

namespace SkyPeek
{
	[System.Runtime.Versioning.SupportedOSPlatform("windows10.0.19041.0")]
	public class AlertModel : System.ComponentModel.INotifyPropertyChanged
	{
		public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

		private System.Windows.Visibility _globalAlertsVisibility = System.Windows.Visibility.Visible;
		public System.Windows.Visibility GlobalAlertsVisibility
		{
			get => _globalAlertsVisibility;
			set { _globalAlertsVisibility = value; OnPropertyChanged(nameof(GlobalAlertsVisibility)); }
		}

		private System.Windows.Visibility _globalAlertToneVisibility = System.Windows.Visibility.Visible;
		public System.Windows.Visibility GlobalAlertToneVisibility
		{
			get => _globalAlertToneVisibility;
			set { _globalAlertToneVisibility = value; OnPropertyChanged(nameof(GlobalAlertToneVisibility)); }
		}

		private System.Windows.Visibility _alertIconBorderVisibility = System.Windows.Visibility.Collapsed;
		public System.Windows.Visibility AlertIconBorderVisibility
		{
			get => _alertIconBorderVisibility;
			set { _alertIconBorderVisibility = value; OnPropertyChanged(nameof(AlertIconBorderVisibility)); }
		}

		private bool _hasNewAlerts = false;
		public bool HasNewAlerts
		{
			get => _hasNewAlerts;
			set { _hasNewAlerts = value; OnPropertyChanged(nameof(HasNewAlerts)); }
		}

		private int _newAlertCount = 0;
		public int NewAlertCount
		{
			get => _newAlertCount;
			set { _newAlertCount = value; OnPropertyChanged(nameof(NewAlertCount)); }
		}

		private List<WeatherAlert> _currentAlerts = [];
		public List<WeatherAlert> CurrentAlerts
		{
			get => _currentAlerts;
			set { _currentAlerts = value; OnPropertyChanged(nameof(CurrentAlerts)); }
		}

		private System.Collections.ObjectModel.ObservableCollection<LocationAlertGroup> _globalAlertGroups = [];
		public System.Collections.ObjectModel.ObservableCollection<LocationAlertGroup> GlobalAlertGroups
		{
			get => _globalAlertGroups;
			set { _globalAlertGroups = value; OnPropertyChanged(nameof(GlobalAlertGroups)); }
		}

		public Action? OnAlertsCleared;

		public void RebuildGlobalAlerts(IEnumerable<SavedLocation> savedLocations, System.Collections.Concurrent.ConcurrentDictionary<string, CachedWeatherData> weatherCache)
		{
			var newGroups = new List<LocationAlertGroup>();
			var allActiveAlerts = new List<WeatherAlert>();
			int unreadTally = 0;

			if (AppConfig.Current.EnableWeatherAlerts)
			{
				foreach (var loc in savedLocations)
				{
					if (loc.AlertFilterLevel == 0)
					{
						loc.HasActiveAlert = false;
						continue;
					}

					if (weatherCache.TryGetValue(loc.DisplayName, out var data) && data.AlertsData != null && data.AlertsData.Count > 0)
					{
						var filteredAlerts = data.AlertsData.FindAll(a => a.SeverityLevel >= loc.AlertFilterLevel);
						if (filteredAlerts.Count > 0)
						{
							loc.HasActiveAlert = true;
							var brush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 53, 69));
							brush.Freeze();
							loc.AlertIconColor = brush;

							loc.ActiveAlertIds.Clear();
							foreach (var a in filteredAlerts) loc.ActiveAlertIds.Add(a.Id);

							foreach (var alert in filteredAlerts)
							{
								var existingAlert = _currentAlerts.Find(a => a.Id == alert.Id);
								if (existingAlert != null)
								{
									alert.IsNew = alert.IsNew || existingAlert.IsNew;
								}
								else
								{
									alert.IsNew = alert.IsNew || !AppConfig.Current.SeenBackgroundAlertIds.Contains(alert.Id);
								}

								if (alert.IsNew) unreadTally++;
							}

							var sortedAlerts = System.Linq.Enumerable.ToList(System.Linq.Enumerable.ThenByDescending(System.Linq.Enumerable.OrderByDescending(filteredAlerts, a => a.SeverityLevel), a => a.IsNew));

							newGroups.Add(new LocationAlertGroup
							{
								LocationName = loc.DisplayName,
								Alerts = sortedAlerts
							});

							allActiveAlerts.AddRange(sortedAlerts);
						}
						else
						{
							loc.HasActiveAlert = false;
							loc.ActiveAlertIds.Clear();
						}
					}
					else
					{
						loc.HasActiveAlert = false;
						loc.ActiveAlertIds.Clear();
					}
				}
			}
			else
			{
				foreach (var loc in savedLocations)
				{
					loc.HasActiveAlert = false;
					loc.ActiveAlertIds.Clear();
				}
			}

			GlobalAlertGroups = new System.Collections.ObjectModel.ObservableCollection<LocationAlertGroup>(newGroups);
			CurrentAlerts = allActiveAlerts;
			NewAlertCount = unreadTally;
			HasNewAlerts = unreadTally > 0;

			if (allActiveAlerts.Count > 0)
			{
				AlertIconBorderVisibility = System.Windows.Visibility.Visible;
			}
			else
			{
				AlertIconBorderVisibility = System.Windows.Visibility.Collapsed;
				OnAlertsCleared?.Invoke();
			}
		}
		protected void OnPropertyChanged(string propertyName) =>
			PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
	}
}