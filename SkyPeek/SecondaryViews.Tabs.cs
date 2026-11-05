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
	public partial class SecondaryViews : Window
	{
		public void ApplyDefaultView(string defaultView)
		{
			_pendingTabView = null;
			_animatingTargetView = null;
			ViewModel.IsWeatherAnimationSelected = false;
			ViewModel.IsLocationMenuSelected = false;
			ViewModel.IsAlertIconSelected = false;

			// Reset Tabs
			TabCurrent.Tag = "Unselected";
			TabHourly.Tag = "Unselected";
			Tab10Day.Tag = "Unselected";

			if (defaultView == "Hourly")
			{
				TabHourly.Tag = "Selected";
				ViewModel.CurrentActiveView = "Hourly";
			}
			else if (defaultView == "10-Day")
			{
				Tab10Day.Tag = "Selected";
				ViewModel.CurrentActiveView = "10-Day";
			}
			else if (defaultView == "Radar")
			{
				ViewModel.CurrentActiveView = "Radar";
			}
			else
			{
				TabCurrent.Tag = "Selected";
				ViewModel.CurrentActiveView = "Current";
			}
		}

		private static int GetTabIndex(string tabName) => tabName switch
		{
			"TabCurrent" => 0,
			"TabHourly" => 1,
			"Tab10Day" => 2,
			_ => -1
		};

		private bool _isTabAnimating = false;
		private string? _pendingTabView = null;
		private string? _animatingTargetView = null;

		internal async void ChangeTab(string newView)
		{
			if (ViewModel.IsLoading) return; // Prevent tab switching during loading animations
			if (_isTabAnimating)
			{
				_pendingTabView = newView;
				return;
			}
			if (ViewModel.CurrentActiveView == newView) return;

			_animatingTargetView = newView;
			string oldView = ViewModel.CurrentActiveView;

			if (_needsWeatherRefresh)
			{
				_needsWeatherRefresh = false;
				if (Application.Current.MainWindow is MainWindow mw)
				{
					var homeLoc = AppConfig.Current.SavedLocations.Find(l => l.City.Equals(AppConfig.Current.City, System.StringComparison.OrdinalIgnoreCase) && l.State.Equals(AppConfig.Current.State, System.StringComparison.OrdinalIgnoreCase));
					if (homeLoc != null)
					{
						IsViewingTemporaryLocation = false;
						_isProgrammaticLocationChange = true;
						ViewModel.SelectedLocationName = homeLoc.DisplayName;

						object? targetItem = null;
						foreach (var c in CurrentView.LocationComboBox.Items)
						{
							if (c is SavedLocation sl && sl.DisplayName == homeLoc.DisplayName)
							{
								targetItem = c;
								break;
							}
						}
						if (targetItem != null) CurrentView.LocationComboBox.SelectedItem = targetItem;
						CurrentView.LocationComboBox.Tag = homeLoc.DisplayName;
						_isProgrammaticLocationChange = false;
					}

					ShowLoadingState();
					_ = mw.RefreshAllLocationsCacheAsync(true);
				}
			}

			_isTabAnimating = true;

			ViewClipContainer.ClipToBounds = true;

			int oldIndex = GetTabIndex("Tab" + ViewModel.CurrentActiveView.Replace("-", ""));
			int newIndex = GetTabIndex("Tab" + newView.Replace("-", ""));

			bool isOldVertical = oldIndex == -1;
			bool isNewVertical = newIndex == -1;

			double distance = 150.0;
			double oldTargetX = 0, oldTargetY = 0;
			double newStartX = 0, newStartY = 0;

			if (!isOldVertical && !isNewVertical)
			{
				// Horizontal to Horizontal (Tab swapping)
				bool slideLeft = oldIndex < newIndex;
				oldTargetX = slideLeft ? -distance : distance;
				newStartX = slideLeft ? distance : -distance;
			}
			else if (!isOldVertical && isNewVertical)
			{
				// Horizontal to Vertical (Opening a sub-view)
				oldTargetY = distance; // Old tab slides down
				newStartY = -distance; // New sub-view slides in from top
			}
			else if (isOldVertical && !isNewVertical)
			{
				// Vertical to Horizontal (Closing a sub-view)
				oldTargetY = -distance; // Old sub-view slides up
				newStartY = distance;   // New tab slides in from bottom
			}
			else
			{
				// Vertical to Vertical (Sub-view to Sub-view edge case)
				oldTargetY = -distance;
				newStartY = distance;
			}

			int width = (int)Math.Max(1, ContentContainer.ActualWidth);
			int height = (int)Math.Max(1, ContentContainer.ActualHeight);
			var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
			rtb.Render(ContentContainer);
			rtb.Freeze();

			var oldTransform = new TranslateTransform(0, 0);
			TransitionOverlay.Source = rtb;
			TransitionOverlay.Opacity = 1.0;
			TransitionOverlay.RenderTransform = oldTransform;
			TransitionOverlay.Visibility = Visibility.Visible;

			ContentContainer.Opacity = 0.0;

			var duration = TimeSpan.FromMilliseconds(175);
			var frameDelay = TimeSpan.FromMilliseconds(16);
			var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };

			var slideOldX = new DoubleAnimation(oldTargetX, duration) { EasingFunction = ease };
			var slideOldY = new DoubleAnimation(oldTargetY, duration) { EasingFunction = ease };
			var fadeOld = new DoubleAnimation(0.0, duration) { EasingFunction = ease };

			oldTransform.BeginAnimation(TranslateTransform.XProperty, slideOldX);
			oldTransform.BeginAnimation(TranslateTransform.YProperty, slideOldY);
			TransitionOverlay.BeginAnimation(UIElement.OpacityProperty, fadeOld);

			await System.Threading.Tasks.Task.Delay(frameDelay);

			ViewModel.CurrentActiveView = newView;
			ViewModel.IsWeatherAnimationSelected = false;
			ViewModel.IsLocationMenuSelected = false;
			ViewModel.IsAlertIconSelected = false;

			TabCurrent.Tag = newView == "Current" ? "Selected" : "Unselected";
			TabHourly.Tag = newView == "Hourly" ? "Selected" : "Unselected";
			Tab10Day.Tag = newView == "10-Day" ? "Selected" : "Unselected";

			var newTransform = new TranslateTransform(newStartX, newStartY);
			ContentContainer.RenderTransform = newTransform;

			await Dispatcher.Yield(DispatcherPriority.Loaded);

			var slideNewX = new DoubleAnimation(0, duration) { EasingFunction = ease };
			var slideNewY = new DoubleAnimation(0, duration) { EasingFunction = ease };

			newTransform.BeginAnimation(TranslateTransform.XProperty, slideNewX);
			newTransform.BeginAnimation(TranslateTransform.YProperty, slideNewY);

			if (!ViewModel.IsLoading)
			{
				var fadeNew = new DoubleAnimation(1.0, duration) { EasingFunction = ease };
				ContentContainer.BeginAnimation(UIElement.OpacityProperty, fadeNew);
			}

			await System.Threading.Tasks.Task.Delay(duration - frameDelay);

			// Sever all animation timelines to release DWM/WPF animation clocks from memory
			TransitionOverlay.BeginAnimation(UIElement.OpacityProperty, null);

			if (!ViewModel.IsLoading)
			{
				ContentContainer.BeginAnimation(UIElement.OpacityProperty, null);
				ContentContainer.Opacity = 1.0;
			}

			oldTransform.BeginAnimation(TranslateTransform.XProperty, null);
			oldTransform.BeginAnimation(TranslateTransform.YProperty, null);
			newTransform.BeginAnimation(TranslateTransform.XProperty, null);
			newTransform.BeginAnimation(TranslateTransform.YProperty, null);

			TransitionOverlay.Visibility = Visibility.Hidden;
			TransitionOverlay.Source = null;
			TransitionOverlay.RenderTransform = null;
			ContentContainer.RenderTransform = null;

			// Force compacting GC to clear the unmanaged LOH memory created by the RenderTargetBitmap
			System.Runtime.GCSettings.LargeObjectHeapCompactionMode = System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;
			System.GC.Collect(2, System.GCCollectionMode.Forced, true, true);

			ViewClipContainer.ClipToBounds = false;
			_isTabAnimating = false;
			_animatingTargetView = null;

			if (_pendingTabView != null)
			{
				string nextView = _pendingTabView;
				_pendingTabView = null;
				if (nextView != ViewModel.CurrentActiveView)
				{
					ChangeTab(nextView);
					return;
				}
			}

			if (newView == "Radar") RadarBackBorder.Focus();
			else if (newView == "Summary") SummaryBackBorder.Focus();
			else if (newView == "Alerts") AlertBackBorder.Focus();
			else if (newView == "LocationManager") LocationManagerBackBorder.Focus();
			else if (newView == "Current" && !ViewModel.IsLoading)
			{
				bool isTabFocused = Keyboard.FocusedElement == TabCurrent || Keyboard.FocusedElement == TabHourly || Keyboard.FocusedElement == Tab10Day;

				if (!isTabFocused && _isKeyboardNavRevealed && (oldView == "Summary" || oldView == "Alerts" || oldView == "Radar" || oldView == "LocationManager"))
				{
					if (oldView == "Summary") ViewModel.IsWeatherAnimationSelected = true;
					else if (oldView == "Alerts") ViewModel.IsAlertIconSelected = true;
					else ViewModel.IsLocationMenuSelected = true;

					TabCurrent.Tag = "Unselected";
					System.Windows.Input.FocusManager.SetFocusedElement(this, null);
					this.Focus();
				}
				else if (!isTabFocused)
				{
					TabCurrent.Focus();
				}
			}
		}

		private void Tab_Click(object sender, MouseButtonEventArgs e)
		{
			if (sender is TextBlock clickedTab)
			{
				string newView = clickedTab.Name switch
				{
					"TabCurrent" => "Current",
					"TabHourly" => "Hourly",
					"Tab10Day" => "10-Day",
					_ => "Current"
				};
				ChangeTab(newView);
			}
		}

		internal void WeatherIconAnimation_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			ChangeTab("Summary");
		}

		private void ResetTabsToDefault()
		{
			ApplyDefaultView(AppConfig.Current.DefaultView ?? "Current");
		}

		private void SummaryView_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			ChangeTab("Current");
		}

		private void AlertBack_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			ChangeTab("Current");
		}

		internal void AlertIconBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			ChangeTab("Alerts");

			Dispatcher.BeginInvoke(new System.Action(() =>
			{
				string targetCity = ViewModel.SelectedLocationName;
				if (string.IsNullOrEmpty(targetCity)) targetCity = ViewModel.City;

				FrameworkElement? foundElement = null;
				FindCityAlertGroupElement(AlertScroller, targetCity, ref foundElement);

				if (foundElement != null)
				{
					GeneralTransform transform = foundElement.TransformToAncestor(AlertScroller);
					Point elementPosition = transform.Transform(new Point(0, 0));
					double targetOffset = AlertScroller.VerticalOffset + elementPosition.Y - 10;

					if (SmoothScrollBehavior.GetIsEnabled(AlertScroller)) AlertScroller.SmoothScrollTo(targetOffset);
					else AlertScroller.ScrollToVerticalOffset(targetOffset);
				}
				else
				{
					if (SmoothScrollBehavior.GetIsEnabled(AlertScroller)) AlertScroller.SmoothScrollTo(0);
					else AlertScroller.ScrollToVerticalOffset(0);
				}
			}), DispatcherPriority.Loaded);
		}

		private static void FindCityAlertGroupElement(DependencyObject parent, string cityName, ref FrameworkElement? foundElement)
		{
			if (foundElement != null) return;
			for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
			{
				var child = VisualTreeHelper.GetChild(parent, i);
				if (child is FrameworkElement element && element.Name == "AlertGroupContainer")
				{
					var dataContext = element.DataContext;
					if (dataContext != null)
					{
						var locationNameProp = dataContext.GetType().GetProperty("LocationName");
						if (locationNameProp != null)
						{
							string? locName = locationNameProp.GetValue(dataContext) as string;
							if (string.Equals(locName, cityName, System.StringComparison.OrdinalIgnoreCase))
							{
								foundElement = element;
								return;
							}
						}
					}
				}
				FindCityAlertGroupElement(child, cityName, ref foundElement);
			}
		}

		internal void RadarIcon_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			ChangeTab("Radar");
			PlayRadar(); // Keep this so the radar starts spinning
		}

		private void RadarView_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			PauseRadar();
			ChangeTab("Current");
		}

		internal static void FindItemsControlRecursive(DependencyObject? parent, ref ItemsControl? result)
		{
			if (result != null || parent == null) return;
			for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
			{
				var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
				if (child is ItemsControl ic && (ic.Name == "HourlyItemsControl" || ic.Name == "HourlyAgendaItemsControl" || ic.Name == "TenDayItemsControl" || ic.Name == "AgendaItemsControl"))
				{
					if (((FrameworkElement)ic).IsVisible)
					{
						result = ic;
						return;
					}
				}
				FindItemsControlRecursive(child, ref result);
			}
		}

		private void TabArea_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
		{
			e.Handled = true;
			if (ViewModel.IsLoading) return; // Prevent tab cycling during loading animations

			string current = _pendingTabView ?? _animatingTargetView ?? ViewModel.CurrentActiveView;

			// Cycle the views based on scroll direction
			if (e.Delta < 0) // Scrolled Down/Right
			{
				if (current == "Current") ChangeTab("Hourly");
				else if (current == "Hourly") ChangeTab("10-Day");
			}
			else // Scrolled Up/Left
			{
				if (current == "10-Day") ChangeTab("Hourly");
				else if (current == "Hourly") ChangeTab("Current");
			}
		}
	}
}