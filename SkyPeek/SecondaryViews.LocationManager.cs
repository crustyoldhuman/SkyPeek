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
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Text.RegularExpressions;

namespace SkyPeek
{
	public partial class SecondaryViews : Window
	{
		public event EventHandler<string>? AlertDismissed;
		private readonly System.Windows.Threading.DispatcherTimer _cityTypingTimer;
		private Point _dragStartPoint;
		private SavedLocation? _draggedLocation;
		private readonly System.Collections.ObjectModel.ObservableCollection<SavedLocation> _localSavedLocations = [];
		internal bool _isProgrammaticLocationChange = false;

		public void RefreshSavedLocations()
		{
			if (Application.Current.MainWindow is MainWindow)
			{
				_isProgrammaticLocationChange = true;
				var savedLocationsSnapshot = AppConfig.Current.SavedLocations.ToArray();

				foreach (var loc in savedLocationsSnapshot)
				{
					bool isNowHome = loc.City == AppConfig.Current.City && loc.State == AppConfig.Current.State && loc.Country == AppConfig.Current.Country;
					if (loc.IsHome != isNowHome)
					{
						loc.IsHome = isNowHome;
						int idx = _localSavedLocations.IndexOf(loc);
						if (idx >= 0)
						{
							_localSavedLocations.RemoveAt(idx);
							_localSavedLocations.Insert(idx, loc);
						}
					}
				}

				var comboList = new System.Collections.Generic.List<object>();
				foreach (var l in savedLocationsSnapshot) comboList.Add(l);
				comboList.Add(new ComboMenuAction { DisplayName = "Location manager" });
				if (AppConfig.Current.EnableWeatherAlerts)
				{
					comboList.Add(new ComboMenuAction { DisplayName = "Clear all alerts" });
				}

				string currentSelection = ViewModel?.SelectedLocationName ?? CurrentView.LocationComboBox.Tag as string ?? "";

				_isProgrammaticLocationChange = true;
				CurrentView.LocationComboBox.ItemsSource = comboList;

				var selected = System.Linq.Enumerable.FirstOrDefault(comboList, c =>
				{
					if (c is SavedLocation sl) return sl.DisplayName == currentSelection;
					if (c is ComboMenuAction cma) return cma.DisplayName == currentSelection;
					return false;
				});

				if (selected != null)
				{
					Dispatcher.BeginInvoke(new Action(() =>
					{
						if (ViewModel is SlideoutViewModel vm) vm.SelectedLocationName = currentSelection;
						CurrentView.LocationComboBox.SelectedItem = selected;
						CurrentView.LocationComboBox.Tag = currentSelection;
					}), DispatcherPriority.DataBind);
				}

				if (!_localSavedLocations.SequenceEqual(savedLocationsSnapshot))
				{
					for (int i = _localSavedLocations.Count - 1; i >= 0; i--)
					{
						if (!savedLocationsSnapshot.Contains(_localSavedLocations[i]))
						{
							_localSavedLocations.RemoveAt(i);
						}
					}

					for (int i = 0; i < savedLocationsSnapshot.Length; i++)
					{
						var expected = savedLocationsSnapshot[i];
						if (i >= _localSavedLocations.Count)
						{
							_localSavedLocations.Add(expected);
						}
						else if (_localSavedLocations[i] != expected)
						{
							int oldIndex = _localSavedLocations.IndexOf(expected);
							if (oldIndex >= 0)
							{
								_localSavedLocations.Move(oldIndex, i);
							}
							else
							{
								_localSavedLocations.Insert(i, expected);
							}
						}
					}
				}

				SavedLocationsList.ItemsSource = _localSavedLocations;
				UpdateGlobalAlertIndicator();

				// Defer releasing the lock until WPF finishes processing the delayed SelectionChanged events
				Dispatcher.BeginInvoke(new Action(() => _isProgrammaticLocationChange = false), DispatcherPriority.ContextIdle);
			}
		}

		private void PopulateLocationManagerDropdowns()
		{
			string[] countries = ["USA", "Canada", "UK", "Australia", "New Zealand"];
			foreach (var country in countries)
			{
				LocManCountryInput.Items.Add(country);
			}

			LocManCountryInput.SelectedItem = "USA";
		}

		public void ApplyLoadedManagerLocations()
		{
			if (!string.IsNullOrEmpty(AppConfig.Current.LastLocManCountry) && LocManCountryInput.Items.Contains(AppConfig.Current.LastLocManCountry))
			{
				LocManCountryInput.SelectedItem = AppConfig.Current.LastLocManCountry;
			}

			if (!string.IsNullOrEmpty(AppConfig.Current.LastLocManState) && LocManStateInput.Items.Contains(AppConfig.Current.LastLocManState))
			{
				LocManStateInput.SelectedItem = AppConfig.Current.LastLocManState;
			}
		}

		private void LocManCountryInput_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			string? selectedCountry = LocManCountryInput.SelectedItem?.ToString();
			if (string.IsNullOrEmpty(selectedCountry)) return;

			LocManStateInput.Items.Clear();
			string[] regions;

			switch (selectedCountry)
			{
				case "USA":
					LocManStateLabel.Text = "State:";
					regions = ["Alabama", "Alaska", "Arizona", "Arkansas", "California", "Colorado", "Connecticut", "Delaware", "Florida", "Georgia", "Hawaii", "Idaho", "Illinois", "Indiana", "Iowa", "Kansas", "Kentucky", "Louisiana", "Maine", "Maryland", "Massachusetts", "Michigan", "Minnesota", "Mississippi", "Missouri", "Montana", "Nebraska", "Nevada", "New Hampshire", "New Jersey", "New Mexico", "New York", "North Carolina", "North Dakota", "Ohio", "Oklahoma", "Oregon", "Pennsylvania", "Rhode Island", "South Carolina", "South Dakota", "Tennessee", "Texas", "Utah", "Vermont", "Virginia", "Washington", "West Virginia", "Wisconsin", "Wyoming"];
					break;
				case "Canada":
					LocManStateLabel.Text = "Province/\nTerritory:";
					regions = ["Alberta", "British Columbia", "Manitoba", "New Brunswick", "Newfoundland and Labrador", "Northwest Territories", "Nova Scotia", "Nunavut", "Ontario", "Prince Edward Island", "Quebec", "Saskatchewan", "Yukon"];
					break;
				case "UK":
					LocManStateLabel.Text = "Country/\nRegion:";
					regions = ["England", "Scotland", "Wales", "Northern Ireland"];
					break;
				case "Australia":
					LocManStateLabel.Text = "State/\nTerritory:";
					regions = ["New South Wales", "Victoria", "Queensland", "Western Australia", "South Australia", "Tasmania", "Australian Capital Territory", "Northern Territory"];
					break;
				case "New Zealand":
					LocManStateLabel.Text = "Region:";
					regions = ["Auckland", "Bay of Plenty", "Canterbury", "Gisborne", "Hawke's Bay", "Manawatu-Wanganui", "Marlborough", "Nelson", "Northland", "Otago", "Southland", "Taranaki", "Tasman", "Waikato", "Wellington", "West Coast"];
					break;
				default:
					regions = [];
					break;
			}

			foreach (var region in regions)
			{
				LocManStateInput.Items.Add(region);
			}

			AppConfig.Current.LastLocManCountry = selectedCountry ?? "USA";

			if (!string.IsNullOrEmpty(AppConfig.Current.LastLocManState) && LocManStateInput.Items.Contains(AppConfig.Current.LastLocManState))
			{
				LocManStateInput.SelectedItem = AppConfig.Current.LastLocManState;
			}
			else if (LocManStateInput.Items.Count > 0)
			{
				LocManStateInput.SelectedIndex = 0;
				AppConfig.Current.LastLocManState = LocManStateInput.SelectedItem?.ToString() ?? "";
			}

			if (this.IsLoaded)
			{
				SettingsManager.Save();
			}
			else if (LocManStateInput.Items.Count > 0)
			{
				LocManStateInput.SelectedIndex = 0;
			}
		}

		private void LocManStateInput_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			AppConfig.Current.LastLocManState = LocManStateInput.SelectedItem?.ToString() ?? "";
			if (this.IsLoaded)
			{
				SettingsManager.Save();
			}
		}

		private bool _isApplyingSuggestion = false;

		private void LocManCityInput_TextChanged(object sender, TextChangedEventArgs e)
		{
			if (_isApplyingSuggestion) return;

			_cityTypingTimer.Stop();
			if (LocManCityInput.Text.Length >= 2 && LocManCityInput.IsFocused)
			{
				_cityTypingTimer.Start();
			}
			else
			{
				CitySuggestionsPopup.IsOpen = false;
			}
		}

		private async void CityTypingTimer_Tick(object? sender, EventArgs e)
		{
			_cityTypingTimer.Stop();
			string query = LocManCityInput.Text.Trim();
			if (string.IsNullOrEmpty(query)) return;

			string? selectedCountry = LocManCountryInput.SelectedItem?.ToString();
			string? selectedState = LocManStateInput.SelectedItem?.ToString();

			string apiQuery = query;
			if (!string.IsNullOrWhiteSpace(selectedState)) apiQuery += $" {selectedState}";
			else if (!string.IsNullOrWhiteSpace(selectedCountry)) apiQuery += $" {selectedCountry}";

			var results = await WeatherService.SearchLocationsAsync(apiQuery, 100);

			if (results.Count == 0 && !string.IsNullOrWhiteSpace(selectedState) && !string.IsNullOrWhiteSpace(selectedCountry))
			{
				var fallback = await WeatherService.SearchLocationsAsync($"{query} {selectedCountry}", 100);
				if (fallback.Count > 0) results = fallback;
			}
			if (results.Count == 0)
			{
				var fallback = await WeatherService.SearchLocationsAsync(query, 100);
				if (fallback.Count > 0) results = fallback;
			}

			if (results.Count > 0)
			{
				var suggestions = new List<GeoSearchResult>();

				foreach (var result in results)
				{
					bool countryMatch = true;
					string apiCountry = result.Country?.Trim() ?? "";
					if (selectedCountry == "USA" && !apiCountry.Equals("United States", StringComparison.OrdinalIgnoreCase) && !apiCountry.Equals("US", StringComparison.OrdinalIgnoreCase) && !apiCountry.Equals("USA", StringComparison.OrdinalIgnoreCase)) countryMatch = false;
					else if (selectedCountry == "UK" && !apiCountry.Equals("United Kingdom", StringComparison.OrdinalIgnoreCase) && !apiCountry.Equals("GB", StringComparison.OrdinalIgnoreCase) && !apiCountry.Equals("UK", StringComparison.OrdinalIgnoreCase)) countryMatch = false;
					else if (selectedCountry == "New Zealand" && !apiCountry.Equals("New Zealand", StringComparison.OrdinalIgnoreCase) && !apiCountry.Equals("NZ", StringComparison.OrdinalIgnoreCase)) countryMatch = false;
					else if (selectedCountry != "USA" && selectedCountry != "UK" && selectedCountry != "New Zealand" && !apiCountry.Contains(selectedCountry ?? "", StringComparison.OrdinalIgnoreCase)) countryMatch = false;

					bool stateMatch = true;
					if (!string.IsNullOrEmpty(selectedState) && !string.IsNullOrEmpty(result.Admin1))
					{
						string apiState = result.Admin1.Trim();
						if (!apiState.Equals(selectedState, StringComparison.OrdinalIgnoreCase) &&
							!apiState.Contains(selectedState, StringComparison.OrdinalIgnoreCase) &&
							!selectedState.Contains(apiState, StringComparison.OrdinalIgnoreCase))
						{
							stateMatch = false;
						}
					}

					if (countryMatch && stateMatch)
					{
						suggestions.Add(result);
					}
				}

				var uniqueSuggestions = suggestions
					.GroupBy(s => new { s.Name, s.Admin1 })
					.Select(g => g.First())
					.ToList();

				if (uniqueSuggestions.Count > 0)
				{
					CitySuggestionsList.ItemsSource = uniqueSuggestions;
					CitySuggestionsPopup.IsOpen = true;
				}
				else
				{
					CitySuggestionsPopup.IsOpen = false;
				}
			}
			else
			{
				CitySuggestionsPopup.IsOpen = false;
			}
		}

		internal void CitySuggestionItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			if (sender is FrameworkElement { Tag: GeoSearchResult result })
			{
				_isApplyingSuggestion = true;
				LocManCityInput.Text = result.Name;
				_isApplyingSuggestion = false;

				if (!string.IsNullOrEmpty(result.Admin1))
				{
					foreach (var item in LocManStateInput.Items)
					{
						if (item.ToString() == result.Admin1)
						{
							LocManStateInput.SelectedItem = item;
							break;
						}
					}
				}

				CitySuggestionsPopup.IsOpen = false;
				LocManCityInput.SelectionStart = LocManCityInput.Text.Length;
				LocManCityInput.Focus();
			}
			e.Handled = true;
		}

		internal async void AddLocation_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			if (Application.Current.MainWindow is MainWindow)
			{
				string city = LocManCityInput.Text.Trim();
				string state = LocManStateInput.SelectedItem?.ToString() ?? "";
				string country = LocManCountryInput.SelectedItem?.ToString() ?? "";

				if (string.IsNullOrWhiteSpace(city)) return;

				AddLocation.IsHitTestVisible = false;
				LocManCityInput.IsHitTestVisible = false;

				string normalizedSearchCity = WeatherService.NormalizeLocationName(city);
				bool isValid = false;

				string apiQuery = city;
				if (!string.IsNullOrWhiteSpace(state)) apiQuery += $" {state}";
				else if (!string.IsNullOrWhiteSpace(country)) apiQuery += $" {country}";

				var results = await WeatherService.SearchLocationsAsync(apiQuery, 100);

				if (results.Count == 0 && !string.IsNullOrWhiteSpace(state) && !string.IsNullOrWhiteSpace(country))
				{
					var fallback = await WeatherService.SearchLocationsAsync($"{city} {country}", 100);
					if (fallback.Count > 0) results = fallback;
				}
				if (results.Count == 0)
				{
					var fallback = await WeatherService.SearchLocationsAsync(city, 100);
					if (fallback.Count > 0) results = fallback;
				}

				foreach (var result in results)
				{
					bool countryMatch = true;
					string apiCountry = result.Country?.Trim() ?? "";
					if (country == "USA" && !apiCountry.Equals("United States", StringComparison.OrdinalIgnoreCase) && !apiCountry.Equals("US", StringComparison.OrdinalIgnoreCase) && !apiCountry.Equals("USA", StringComparison.OrdinalIgnoreCase)) countryMatch = false;
					else if (country == "UK" && !apiCountry.Equals("United Kingdom", StringComparison.OrdinalIgnoreCase) && !apiCountry.Equals("GB", StringComparison.OrdinalIgnoreCase) && !apiCountry.Equals("UK", StringComparison.OrdinalIgnoreCase)) countryMatch = false;
					else if (country == "New Zealand" && !apiCountry.Equals("New Zealand", StringComparison.OrdinalIgnoreCase) && !apiCountry.Equals("NZ", StringComparison.OrdinalIgnoreCase)) countryMatch = false;
					else if (country != "USA" && country != "UK" && country != "New Zealand" && !apiCountry.Contains(country ?? "", StringComparison.OrdinalIgnoreCase)) countryMatch = false;

					bool stateMatch = true;
					if (!string.IsNullOrEmpty(state) && !string.IsNullOrEmpty(result.Admin1))
					{
						string apiState = result.Admin1.Trim();
						if (!apiState.Equals(state, StringComparison.OrdinalIgnoreCase) &&
							!apiState.Contains(state, StringComparison.OrdinalIgnoreCase) &&
							!state.Contains(apiState, StringComparison.OrdinalIgnoreCase))
						{
							stateMatch = false;
						}
					}

					if (countryMatch && stateMatch && WeatherService.NormalizeLocationName(result.Name ?? "").Equals(normalizedSearchCity, StringComparison.OrdinalIgnoreCase))
					{
						isValid = true;
						city = result.Name ?? city; // Use proper casing from API
						break;
					}
				}

				if (!isValid)
				{
					ToastNotification.TriggerToast(ToastText, "Invalid city for this region");
					AddLocation.IsHitTestVisible = true;
					LocManCityInput.IsHitTestVisible = true;
					return;
				}

				bool isDuplicate = AppConfig.Current.SavedLocations.Any(l => l.City.Equals(city, StringComparison.OrdinalIgnoreCase) && l.State.Equals(state, StringComparison.OrdinalIgnoreCase) && l.Country.Equals(country, StringComparison.OrdinalIgnoreCase));
				if (!isDuplicate)
				{
					var newLoc = new SavedLocation { City = city!, State = state!, Country = country! };
					AppConfig.Current.SavedLocations.Add(newLoc);
					SettingsManager.Save();
					RefreshSavedLocations();
					_needsWeatherRefresh = true;
				}

				LocManCityInput.Text = "";
				AddLocation.IsHitTestVisible = true;
				LocManCityInput.IsHitTestVisible = true;

				if (Keyboard.FocusedElement == LocManCityInput || Keyboard.FocusedElement == AddLocation)
				{
					if (SavedLocationsList.Items.Count > 0)
					{
						if (SavedLocationsList.ItemContainerGenerator.ContainerFromIndex(0) is ContentPresenter cp && VisualTreeHelper.GetChildrenCount(cp) > 0 && VisualTreeHelper.GetChild(cp, 0) is Border b)
						{
							b.Focus();
							b.BringIntoView();
						}
					}
				}
			}
			e.Handled = true;
		}

		private void RemoveLocation_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			if (sender is FrameworkElement { Tag: SavedLocation loc } && Application.Current.MainWindow is MainWindow mw)
			{
				if (AppConfig.Current.SavedLocations.Count <= 1)
				{
					ToastNotification.TriggerToast(ToastText, "Cannot delete only location");
					return;
				}

				if (loc.IsWindowsLocation)
				{
					ToastNotification.TriggerToast(ToastText, "Disable 'Sync GPS from Windows' in Settings to delete GPS location.");
					return;
				}

				bool isCurrentlyViewingDeleted = ViewModel.SelectedLocationName == loc.DisplayName;

				if (loc.IsHome)
				{
					int idx = AppConfig.Current.SavedLocations.IndexOf(loc);
					var newHome = AppConfig.Current.SavedLocations[idx > 0 ? idx - 1 : idx + 1];
					AppConfig.Current.City = newHome.City;
					AppConfig.Current.State = newHome.State;
					AppConfig.Current.Country = newHome.Country;
				}

				AppConfig.Current.SavedLocations.Remove(loc);

				SavedLocation? homeLoc = null;
				if (isCurrentlyViewingDeleted)
				{
					homeLoc = AppConfig.Current.SavedLocations.FirstOrDefault(l =>
						l.City == AppConfig.Current.City &&
						l.State == AppConfig.Current.State &&
						l.Country == AppConfig.Current.Country)
						?? AppConfig.Current.SavedLocations.FirstOrDefault();

					if (homeLoc != null)
					{
						// Prime the pointers so the upcoming RefreshSavedLocations grabs the new fallback
						_isProgrammaticLocationChange = true;
						ViewModel.SelectedLocationName = homeLoc.DisplayName;
						CurrentView.LocationComboBox.Tag = homeLoc.DisplayName;
					}
				}

				SettingsManager.Save();
				RefreshSavedLocations();
				ViewModel.AlertState.RebuildGlobalAlerts(AppConfig.Current.SavedLocations, AppConfig.Current.WeatherCache);

				if (isCurrentlyViewingDeleted && homeLoc != null)
				{
					IsViewingTemporaryLocation = false;

					if (AppConfig.Current.WeatherCache.TryGetValue(homeLoc.DisplayName, out var cachedData))
					{
						mw.ApplyWeatherToUI(cachedData, homeLoc.DisplayName, false);
					}
				}
			}
			e.Handled = true;
		}

		private void LocationItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			_dragStartPoint = e.GetPosition(sender as IInputElement);
			if (sender is FrameworkElement { Tag: SavedLocation loc })
			{
				_draggedLocation = loc;
			}
		}

		private void LocationItem_MouseMove(object sender, MouseEventArgs e)
		{
			if (e.LeftButton == MouseButtonState.Pressed && _draggedLocation != null && sender is FrameworkElement fe)
			{
				Point position = e.GetPosition(fe);
				if (Math.Abs(position.X - _dragStartPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
					Math.Abs(position.Y - _dragStartPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
				{
					fe.Opacity = 0.3;
					DragDrop.DoDragDrop(fe, _draggedLocation, DragDropEffects.Move);
					fe.Opacity = 1.0;

					if (Application.Current.MainWindow is MainWindow)
					{
						bool orderChanged = false;
						if (AppConfig.Current.SavedLocations.Count == _localSavedLocations.Count)
						{
							for (int i = 0; i < AppConfig.Current.SavedLocations.Count; i++)
							{
								if (AppConfig.Current.SavedLocations[i] != _localSavedLocations[i])
								{
									orderChanged = true;
									break;
								}
							}
						}

						if (orderChanged)
						{
							AppConfig.Current.SavedLocations.Clear();
							foreach (var loc in _localSavedLocations) AppConfig.Current.SavedLocations.Add(loc);
							SettingsManager.Save();
							RefreshSavedLocations();
						}
					}
					_draggedLocation = null;
				}
			}
		}

		private void LocationItem_GiveFeedback(object sender, GiveFeedbackEventArgs e)
		{
			e.UseDefaultCursors = false;
			Mouse.SetCursor(Cursors.Arrow);
			e.Handled = true;
		}

		private void LocationItem_DragOver(object sender, DragEventArgs e)
		{
			Point scrollPos = e.GetPosition(SavedLocationsScroller);
			if (scrollPos.Y < 30)
			{
				SavedLocationsScroller.ScrollToVerticalOffset(SavedLocationsScroller.VerticalOffset - 6);
			}
			else if (scrollPos.Y > SavedLocationsScroller.ActualHeight - 30)
			{
				SavedLocationsScroller.ScrollToVerticalOffset(SavedLocationsScroller.VerticalOffset + 6);
			}

			if (sender is FrameworkElement { Tag: SavedLocation targetLocation })
			{
				if (e.Data.GetData(typeof(SavedLocation)) is SavedLocation draggedLocation && draggedLocation != targetLocation)
				{
					int oldIdx = _localSavedLocations.IndexOf(draggedLocation);
					int newIdx = _localSavedLocations.IndexOf(targetLocation);

					if (oldIdx >= 0 && newIdx >= 0 && oldIdx != newIdx)
					{
						_localSavedLocations.Move(oldIdx, newIdx);
					}
				}
			}
			e.Handled = true;
		}

		private void LocationItem_Drop(object sender, DragEventArgs e)
		{
			e.Handled = true;
		}

		public void LocationMarquee_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			Grid? grid = null;
			Canvas? canvas = null;
			StackPanel? panel = null;

			if (sender is Grid g && g.Children.Count > 0 && g.Children[0] is Canvas c && c.Children.Count > 0 && c.Children[0] is StackPanel sp)
			{
				grid = g;
				canvas = c;
				panel = sp;
			}
			else if (sender is StackPanel sp2 && sp2.Parent is Canvas c2 && c2.Parent is Grid g2)
			{
				grid = g2;
				canvas = c2;
				panel = sp2;
			}

			if (grid is not { } || canvas is not { } || panel is not { }) return;
			if (panel.Children.Count < 2 || panel.Children[0] is not TextBlock primary || panel.Children[1] is not TextBlock secondary) return;

			grid.Dispatcher.BeginInvoke(new Action(() =>
			{
				if (panel.RenderTransform is not TranslateTransform || panel.RenderTransform.IsFrozen)
				{
					panel.RenderTransform = new TranslateTransform();
				}
				TranslateTransform transform = (TranslateTransform)panel.RenderTransform;

				double textWidth = primary.ActualWidth;
				double availableWidth = canvas.ActualWidth;

				string stateHash = $"{textWidth:F1}_{availableWidth:F1}";

				if (canvas.Tag is string cachedHash && cachedHash == stateHash)
				{
					return;
				}

				canvas.Tag = stateHash;

				if (textWidth > availableWidth && availableWidth > 0)
				{
					secondary.Visibility = Visibility.Visible;
					double distance = textWidth + secondary.Margin.Left;
					transform.StartMarqueeLoop(distance);
				}
				else
				{
					secondary.Visibility = Visibility.Collapsed;
					transform.BeginAnimation(TranslateTransform.XProperty, null);
					transform.X = (availableWidth - textWidth) / 2;
				}
			}), DispatcherPriority.Loaded);
		}

		public async void LocationComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (_isProgrammaticLocationChange) return;
			if (CurrentView.LocationComboBox.SelectedItem == null) return;

			if (CurrentView.LocationComboBox.SelectedItem is SavedLocation loc)
			{
				CurrentView.LocationComboBox.Tag = loc.DisplayName;
				UpdateGlobalAlertIndicator();

				if (Application.Current.MainWindow is MainWindow mw)
				{
					IsViewingTemporaryLocation = true;
					string key = loc.DisplayName;

					// 1. Fade out current content and lock UI
					TabArea.IsHitTestVisible = false;
					ContentContainer.IsHitTestVisible = false;
					var fadeOutContent = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(250));
					ContentContainer.BeginAnimation(UIElement.OpacityProperty, fadeOutContent);

					// 2. Show loading sequence
					ViewModel.IsLoading = true;
					LoadingView.Opacity = 0.0;
					LoadingView.Visibility = Visibility.Visible;
					LoadingAnimation.PlayAnimation();
					var fadeInLoad = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(250));
					LoadingView.BeginAnimation(UIElement.OpacityProperty, fadeInLoad);

					// 3. Buffer loading state
					var waitTask = System.Threading.Tasks.Task.Delay(50);

					CachedWeatherData? finalData;
					if (AppConfig.Current.WeatherCache.TryGetValue(key, out var cachedData) && (DateTime.Now - cachedData.LastUpdated).TotalMinutes < 30)
					{
						finalData = cachedData;
					}
					else
					{
						finalData = await mw.FetchWeatherByLocation(loc.City, loc.State, loc.Country, true);
						if (finalData != null) AppConfig.Current.WeatherCache[key] = finalData;
					}

					await waitTask;

					if (finalData != null)
					{
						// ApplyWeatherToUI handles the fade-in and stops the loading animation
						mw.ApplyWeatherToUI(finalData, key, true);
					}
					else
					{
						// Fallback to restore UI if fetch fails
						var fadeOutLoad = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(250));
						fadeOutLoad.Completed += (s, ev) =>
						{
							LoadingView.Visibility = Visibility.Hidden;
							LoadingAnimation.StopAnimation();
							ViewModel.IsLoading = false;
						};
						LoadingView.BeginAnimation(UIElement.OpacityProperty, fadeOutLoad);

						var fadeIn = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(250));
						ContentContainer.BeginAnimation(UIElement.OpacityProperty, fadeIn);
						ContentContainer.IsHitTestVisible = true;
						TabArea.IsHitTestVisible = true;
					}
				}
			}
			else if (CurrentView.LocationComboBox.SelectedItem is ComboMenuAction menuAction)
			{
				string dName = menuAction.DisplayName;

				_isProgrammaticLocationChange = true;
				if (e.RemovedItems.Count > 0)
				{
					CurrentView.LocationComboBox.SelectedItem = e.RemovedItems[0];
				}
				else
				{
					string currentTag = CurrentView.LocationComboBox.Tag as string ?? "";
					CurrentView.LocationComboBox.SelectedItem = _localSavedLocations.FirstOrDefault(l => l.DisplayName == currentTag);
				}
				_isProgrammaticLocationChange = false;

				if (dName == "Location manager")
				{
					ChangeTab("LocationManager");
				}
				else if (dName == "Clear all alerts")
				{
					if (Application.Current.MainWindow is MainWindow mw)
					{
						mw.ClearAllAlerts();
					}
				}
			}
		}

		private void ToggleLocationAlerts_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			if (sender is FrameworkElement { Tag: SavedLocation loc } && Application.Current.MainWindow is MainWindow)
			{
				loc.AlertFilterLevel = (loc.AlertFilterLevel + 1) % 5;
				SettingsManager.Save();
				_needsWeatherRefresh = true;

				string msg = loc.AlertFilterLevel switch
				{
					0 => "All alerts off",
					1 => "All alerts on",
					2 => "Moderate and above alerts on",
					3 => "Severe and above alerts on",
					_ => "Extreme alerts only"
				};

				ToastNotification.TriggerToast(ToastText, msg, System.Windows.VerticalAlignment.Center, 30);
			}
			e.Handled = true;
		}

		private void ToggleLocationTone_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			if (sender is FrameworkElement { Tag: SavedLocation loc } && Application.Current.MainWindow is MainWindow)
			{
				if (!loc.IsToneInteractionEnabled) return;

				loc.PlayAlertTone = !loc.PlayAlertTone;
				SettingsManager.Save();
				_needsWeatherRefresh = true;
			}
			e.Handled = true;
		}

		private void SetHomeLocation_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			if (sender is FrameworkElement { Tag: SavedLocation loc } && Application.Current.MainWindow is MainWindow)
			{
				AppConfig.Current.City = loc.City;
				AppConfig.Current.State = loc.State;
				AppConfig.Current.Country = loc.Country;
				SettingsManager.Save();

				IsViewingTemporaryLocation = false;

				RefreshSavedLocations();
				AnimationHelper.TriggerToast(ToastNotification, ToastText, "Home location saved");
			}
			e.Handled = true;
		}

		private void EditLocations_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			ChangeTab("LocationManager");
			e.Handled = true;
		}

		internal void LocationManagerBack_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			ChangeTab("Current");
			e.Handled = true;
		}

		private void SavedLocationsScrollBar_Scroll(object sender, System.Windows.Controls.Primitives.ScrollEventArgs e)
		{
			SavedLocationsScroller.ScrollToVerticalOffset(e.NewValue);
		}

		private void SavedLocationsScroller_ScrollChanged(object sender, ScrollChangedEventArgs e)
		{
			SavedLocationsScrollBar.Maximum = SavedLocationsScroller.ScrollableHeight;
			SavedLocationsScrollBar.ViewportSize = SavedLocationsScroller.ViewportHeight;
			SavedLocationsScrollBar.Value = e.VerticalOffset;
		}

		public async void LocationComboBox_DropDownClosed(object sender, EventArgs e)
		{
			await System.Threading.Tasks.Task.Delay(250);

			if (sender is ComboBox cb && cb.Template.FindName("Popup", cb) is Popup popup && popup.Child is FrameworkElement popupChild)
			{
				var scrollViewer = popupChild.FindVisualChild<ScrollViewer>();
				scrollViewer?.ScrollToTop();
			}
		}

		public void LocationComboBox_DropDownOpened(object sender, EventArgs e)
		{
			// 1. YOUR CUSTOM POPUP CENTERING LOGIC
			if (sender is ComboBox cb && cb.Template.FindName("Popup", cb) is Popup popup)
			{
				if (popup.Placement != PlacementMode.Custom)
				{
					popup.Placement = PlacementMode.Custom;
					popup.CustomPopupPlacementCallback = (popupSize, targetSize, _) =>
					{
						return [new(new Point(Math.Round((targetSize.Width - popupSize.Width) / 2), Math.Round(targetSize.Height)), PopupPrimaryAxis.None)];
					};
				}
			}

			// 2. THE RESET LOGIC (using your VisualTreeExtensions)
			if (sender is ComboBox comboBox)
			{
				// Using Background priority ensures we wait until the visual tree 
				// has fully rendered the Popup template before searching.
				Dispatcher.BeginInvoke(new Action(() =>
				{
					// Using your extension method
					var scrollViewer = comboBox.FindVisualChild<ScrollViewer>();
					scrollViewer?.ScrollToTop();
				}), System.Windows.Threading.DispatcherPriority.Background);
			}
		}
	}
}