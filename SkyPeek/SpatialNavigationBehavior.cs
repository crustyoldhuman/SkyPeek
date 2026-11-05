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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace SkyPeek
{
	[System.Runtime.Versioning.SupportedOSPlatform("windows10.0.19041.0")]
	public static class SpatialNavigationBehavior
	{
		private static DateTime _lastTabDownPressTime = DateTime.MinValue;
		private static IInputElement? _activeAlertTarget = null;

		public static readonly DependencyProperty IsEnabledProperty =
			DependencyProperty.RegisterAttached(
				"IsEnabled",
				typeof(bool),
				typeof(SpatialNavigationBehavior),
				new PropertyMetadata(false, OnIsEnabledChanged));

		public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);
		public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

		private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			if (d is Window window)
			{
				if ((bool)e.NewValue)
				{
					window.PreviewKeyDown += Window_PreviewKeyDown;
				}
				else
				{
					window.PreviewKeyDown -= Window_PreviewKeyDown;
				}
			}
		}

		private static void SetPseudoFocus(IInputElement? item, bool isFocused)
		{
			if (item is Control c && c.Name != null && c.Name.StartsWith("ClearA"))
			{
				if (isFocused)
				{
					c.SetResourceReference(Control.BorderBrushProperty, "PrimaryTextBrush");
					c.BorderThickness = new Thickness(2);
				}
				else
				{
					c.ClearValue(Control.BorderBrushProperty);
					c.ClearValue(Control.BorderThicknessProperty);
				}
			}
			else if (item is System.Windows.Documents.Hyperlink hl)
			{
				if (isFocused)
				{
					// Query the global dictionary populated by ThemeHelper instead of the local logical tree
					bool isDarkMode = Application.Current.TryFindResource("PrimaryTextBrush") is SolidColorBrush primaryBrush && primaryBrush.Color.R > 128;

					var bgBrush = new SolidColorBrush(isDarkMode ? Color.FromRgb(189, 189, 189) : Colors.Black);
					bgBrush.Freeze();
					hl.Background = bgBrush;
				}
				else
				{
					hl.ClearValue(System.Windows.Documents.Hyperlink.BackgroundProperty);
				}
			}
		}

		private static void Window_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (sender is Settings settingsWindow)
			{
				ProcessSettingsNavigation(settingsWindow, e);
				return;
			}

			if (sender is not SecondaryViews window) return;

			if (e.Key != Key.Down) _lastTabDownPressTime = DateTime.MinValue;

			if (window.ViewModel.IsLoading) return;

			if (ProcessLocationManagerNavigation(window, e)) return;

			if (ProcessSummaryNavigation(window, e)) return;
			if (ProcessAlertsNavigation(window, e)) return;
			if (ProcessRadarNavigation(window, e)) return;

			if (e.IsRepeat) return;
			bool isTextFieldFocused = Keyboard.FocusedElement is TextBox || window.LocManCityInput.IsKeyboardFocusWithin;
			if (isTextFieldFocused && !(window.ViewModel.CurrentActiveView == "LocationManager" && (e.Key == Key.Down || e.Key == Key.Up || e.Key == Key.Tab || e.Key == Key.Enter))) return;

			if (window.ViewModel.CurrentActiveView == "Current" && window.CurrentView.LocationComboBox.IsDropDownOpen)
			{
				if (e.Key == Key.Back || e.Key == Key.Escape)
				{
					e.Handled = true;
					window.CurrentView.LocationComboBox.IsDropDownOpen = false;
					window.ViewModel.IsLocationMenuSelected = true;
					return;
				}

				if (window.ViewModel.IsLocationMenuSelected)
				{
					if (e.Key == Key.Enter)
					{
						e.Handled = true;
						window.CurrentView.LocationComboBox.IsDropDownOpen = false;
						return;
					}
					if (e.Key == Key.Down)
					{
						e.Handled = true;
						window.ViewModel.IsLocationMenuSelected = false;
						if (window.CurrentView.LocationComboBox.ItemContainerGenerator.ContainerFromIndex(0) is ComboBoxItem item)
						{
							item.Focus();
						}
						return;
					}

					e.Handled = true;
					return;
				}
				else
				{
					if (e.Key == Key.Up)
					{
						if (Keyboard.FocusedElement == window.TabCurrent || Keyboard.FocusedElement == window.TabHourly || Keyboard.FocusedElement == window.Tab10Day)
						{
							e.Handled = true;
							return;
						}
					}

					if (e.Key == Key.Down)
					{
						if (window.CurrentView.LocationComboBox.Items.Count > 0)
						{
							var lastIndex = window.CurrentView.LocationComboBox.Items.Count - 1;
							if (window.CurrentView.LocationComboBox.ItemContainerGenerator.ContainerFromIndex(lastIndex) is ComboBoxItem lastItem && lastItem.IsKeyboardFocusWithin)
							{
								e.Handled = true;
								var homeKeyArgs = new KeyEventArgs(e.KeyboardDevice, e.InputSource, e.Timestamp, Key.Home)
								{
									RoutedEvent = UIElement.KeyDownEvent
								};
								window.CurrentView.LocationComboBox.RaiseEvent(homeKeyArgs);
								return;
							}
						}
					}

					if (e.Key == Key.Enter)
					{
						window.ViewModel.IsLocationMenuSelected = true;
					}
				}

				if (!e.Handled && (e.Key == Key.Up || e.Key == Key.Down || e.Key == Key.Left || e.Key == Key.Right || e.Key == Key.Enter))
				{
					return; // Let the ComboBox process its native list navigation
				}
			}

			if (!window._isKeyboardNavRevealed && (e.Key == Key.Up || e.Key == Key.Down || e.Key == Key.Left || e.Key == Key.Right))
			{
				window._isKeyboardNavRevealed = true;
				e.Handled = true;

				if (window.ViewModel.CurrentActiveView == "Current") window.TabCurrent.Focus();
				else if (window.ViewModel.CurrentActiveView == "Hourly") window.TabHourly.Focus();
				else if (window.ViewModel.CurrentActiveView == "10-Day") window.Tab10Day.Focus();
				else if (window.ViewModel.CurrentActiveView == "Radar") window.RadarBackBorder.Focus();
				else if (window.ViewModel.CurrentActiveView == "Summary") window.SummaryBackBorder.Focus();
				else if (window.ViewModel.CurrentActiveView == "Alerts") window.AlertBackBorder.Focus();
				else if (window.ViewModel.CurrentActiveView == "LocationManager") window.LocationManagerBackBorder.Focus();

				return;
			}

			if (e.Key == Key.Escape)
			{
				e.Handled = true;
				window.SlideOut();
				return;
			}

			if (e.Key == Key.Back)
			{
				if (window.ViewModel.CurrentActiveView == "Radar" || window.ViewModel.CurrentActiveView == "Summary" || window.ViewModel.CurrentActiveView == "Alerts" || window.ViewModel.CurrentActiveView == "LocationManager")
				{
					e.Handled = true;
					string previousView = window.ViewModel.CurrentActiveView;
					window.TabCurrent.Focus();
					window.ChangeTab("Current");

					window.ViewModel.IsAlertIconSelected = false;
					if (previousView == "Alerts" || previousView == "Radar" || previousView == "LocationManager")
					{
						window.TabCurrent.Tag = "Unselected";
						window.ViewModel.IsWeatherAnimationSelected = false;
						window.ViewModel.IsLocationMenuSelected = true;
						window.Dispatcher.BeginInvoke(new Action(() => {
							System.Windows.Input.FocusManager.SetFocusedElement(window.CurrentView, null);
							window.CurrentView.LocationComboBox.Focus();
						}), DispatcherPriority.Loaded);
					}
					else if (previousView == "Summary")
					{
						window.ViewModel.IsLocationMenuSelected = false;
						window.ViewModel.IsWeatherAnimationSelected = true;
					}
				}
				else if (window.ViewModel.CurrentActiveView == "Current" || window.ViewModel.CurrentActiveView == "Hourly" || window.ViewModel.CurrentActiveView == "10-Day")
				{
					e.Handled = true;
					window.SlideOut();
					return;
				}
			}


			if (e.Key == Key.Up && window.ViewModel.CurrentActiveView == "Current" && window.ViewModel.IsWeatherAnimationSelected)
			{
				e.Handled = true;
				window.ViewModel.IsWeatherAnimationSelected = false;
				window.ViewModel.IsLocationMenuSelected = true;
				System.Windows.Input.FocusManager.SetFocusedElement(window, null);
				window.Focus();
				return;
			}

			if (e.Key == Key.Down && window.ViewModel.CurrentActiveView == "Current" && window.ViewModel.IsLocationMenuSelected)
			{
				e.Handled = true;
				window.ViewModel.IsLocationMenuSelected = false;
				window.ViewModel.IsWeatherAnimationSelected = true;
				return;
			}

			if (e.Key == Key.Up && window.ViewModel.CurrentActiveView == "Current" && window.ViewModel.IsLocationMenuSelected)
			{
				e.Handled = true;
				return;
			}

			if (e.Key == Key.Right && window.ViewModel.CurrentActiveView == "Current" && (window.ViewModel.IsLocationMenuSelected || window.ViewModel.IsWeatherAnimationSelected))
			{
				e.Handled = true;
				window.ViewModel.IsLocationMenuSelected = false;
				window.ViewModel.IsWeatherAnimationSelected = false;
				window.ChangeTab("Radar");
				window.PlayRadar();
				return;
			}

			if (e.Key == Key.Left && window.ViewModel.CurrentActiveView == "Current" && (window.ViewModel.IsLocationMenuSelected || window.ViewModel.IsWeatherAnimationSelected))
			{
				e.Handled = true;
				if (window.ViewModel.AlertState != null && window.ViewModel.AlertState.AlertIconBorderVisibility == Visibility.Visible)
				{
					window.ViewModel.IsLocationMenuSelected = false;
					window.ViewModel.IsWeatherAnimationSelected = false;
					window.ViewModel.IsAlertIconSelected = false;
					window.ChangeTab("Alerts");
				}
				return;
			}

			if (e.Key == Key.Up && window.ViewModel.CurrentActiveView == "Current" && window.TabCurrent.IsFocused)
			{
				e.Handled = true;
				window.ViewModel.IsWeatherAnimationSelected = true;
				window.TabCurrent.Tag = "Unselected";
				System.Windows.Input.FocusManager.SetFocusedElement(window, null);
				window.Focus();
				return;
			}

			if (e.Key == Key.Down && window.ViewModel.CurrentActiveView == "Current" && window.ViewModel.IsWeatherAnimationSelected)
			{
				e.Handled = true;
				window.ViewModel.IsWeatherAnimationSelected = false;
				window.TabCurrent.Tag = "Selected";
				window.TabCurrent.Focus();
				return;
			}

			if (e.Key == Key.Up && ((window.ViewModel.CurrentActiveView == "Hourly" && window.TabHourly.IsFocused) || (window.ViewModel.CurrentActiveView == "10-Day" && window.Tab10Day.IsFocused)))
			{
				e.Handled = true;
				ItemsControl? targetControl = null;
				SecondaryViews.FindItemsControlRecursive(window.ContentContainer, ref targetControl);
				targetControl?.Focus();
				return;
			}

			if (e.Key == Key.Down)
			{
				if (Keyboard.FocusedElement == window.TabCurrent || Keyboard.FocusedElement == window.TabHourly || Keyboard.FocusedElement == window.Tab10Day)
				{
					e.Handled = true;
					if ((DateTime.Now - _lastTabDownPressTime).TotalSeconds <= 3)
					{
						_lastTabDownPressTime = DateTime.MinValue;
						window.SlideOut();
					}
					else
					{
						_lastTabDownPressTime = DateTime.Now;
						window.ToastNotification.TriggerToast(window.ToastText, "Press \"Down\" again to close");
					}
					return;
				}
			}

			if (e.Key == Key.Up)
			{
				if (Keyboard.FocusedElement == window.TabCurrent || Keyboard.FocusedElement == window.TabHourly || Keyboard.FocusedElement == window.Tab10Day)
				{
					e.Handled = true;
					if (window.ViewModel.CurrentActiveView == "Radar") window.RadarPlayPauseButton.Focus();
					else if (window.ViewModel.CurrentActiveView == "Summary")
					{
						if (window.SummaryScroller.ScrollableHeight > 0) window.SummaryScroller.Focus();
						else window.SummaryBackBorder.Focus();
					}
					else if (window.ViewModel.CurrentActiveView == "Alerts")
					{
						window.AlertScroller.Focus();
						UpdateAlertButtonFocus(window);
					}
					else if (window.ViewModel.CurrentActiveView == "LocationManager")
					{
						if (window.SavedLocationsList.Items.Count > 0)
						{
							if (window.SavedLocationsList.ItemContainerGenerator.ContainerFromIndex(window.SavedLocationsList.Items.Count - 1) is ContentPresenter cp && System.Windows.Media.VisualTreeHelper.GetChildrenCount(cp) > 0 && System.Windows.Media.VisualTreeHelper.GetChild(cp, 0) is Border lastBorder)
							{
								lastBorder.Focus();
								lastBorder.BringIntoView();
							}
							else window.AddLocation.Focus();
						}
						else
						{
							window.AddLocation.Focus();
						}
					}
					return;
				}
			}
			else if (e.Key == Key.Left)
			{
				if (Keyboard.FocusedElement is ItemsControl ic && (ic.Name == "HourlyItemsControl" || ic.Name == "HourlyAgendaItemsControl" || ic.Name == "TenDayItemsControl" || ic.Name == "AgendaItemsControl")) return;

				e.Handled = true;
				if (Keyboard.FocusedElement == window.Tab10Day) { window.ChangeTab("Hourly"); window.TabHourly.Focus(); }
				else if (Keyboard.FocusedElement == window.TabHourly) { window.ChangeTab("Current"); window.TabCurrent.Focus(); }
				else if (Keyboard.FocusedElement == window.TabCurrent) { window.TabCurrent.Focus(); window.ViewModel.IsWeatherAnimationSelected = false; window.ViewModel.IsLocationMenuSelected = false; window.TabCurrent.Tag = "Selected"; }
				else if (window.ViewModel.CurrentActiveView == "10-Day") { window.ChangeTab("Hourly"); window.TabHourly.Focus(); }
				else if (window.ViewModel.CurrentActiveView == "Hourly") { window.ChangeTab("Current"); window.TabCurrent.Focus(); }
				else if (window.ViewModel.CurrentActiveView == "Current") { window.TabCurrent.Focus(); window.ViewModel.IsWeatherAnimationSelected = false; window.ViewModel.IsLocationMenuSelected = false; window.TabCurrent.Tag = "Selected"; }
				else if (Keyboard.FocusedElement is Border b && (b.Name == "SummaryBackBorder" || b.Name == "RadarBackBorder" || b.Name == "AlertBackBorder" || b.Name == "LocationManagerBackBorder"))
				{
					e.Handled = true;
					string previousView = window.ViewModel.CurrentActiveView;
					window.TabCurrent.Focus();
					window.ChangeTab("Current");

					window.ViewModel.IsAlertIconSelected = false;
					if (previousView == "Alerts" || previousView == "Radar" || previousView == "LocationManager")
					{
						window.TabCurrent.Tag = "Unselected";
						window.ViewModel.IsWeatherAnimationSelected = false;
						window.ViewModel.IsLocationMenuSelected = true;
						window.Dispatcher.BeginInvoke(new Action(() => {
							System.Windows.Input.FocusManager.SetFocusedElement(window.CurrentView, null);
							window.CurrentView.LocationComboBox.Focus();
						}), DispatcherPriority.Loaded);
					}
					else if (previousView == "Summary")
					{
						window.ViewModel.IsLocationMenuSelected = false;
						window.ViewModel.IsWeatherAnimationSelected = true;
					}
				}
			}
			else if (e.Key == Key.Right)
			{
				if (Keyboard.FocusedElement is ItemsControl ic && (ic.Name == "HourlyItemsControl" || ic.Name == "HourlyAgendaItemsControl" || ic.Name == "TenDayItemsControl" || ic.Name == "AgendaItemsControl")) return;

				e.Handled = true;
				if (Keyboard.FocusedElement == window.TabCurrent) { window.ChangeTab("Hourly"); window.TabHourly.Focus(); window.ViewModel.IsWeatherAnimationSelected = false; window.ViewModel.IsLocationMenuSelected = false; window.ViewModel.IsAlertIconSelected = false; }
				else if (Keyboard.FocusedElement == window.TabHourly) { window.ChangeTab("10-Day"); window.Tab10Day.Focus(); }
				else if (Keyboard.FocusedElement == window.Tab10Day) { window.Tab10Day.Focus(); }
				else if (window.ViewModel.CurrentActiveView == "Current") { window.ChangeTab("Hourly"); window.TabHourly.Focus(); window.ViewModel.IsWeatherAnimationSelected = false; window.ViewModel.IsLocationMenuSelected = false; window.ViewModel.IsAlertIconSelected = false; }
				else if (window.ViewModel.CurrentActiveView == "Hourly") { window.ChangeTab("10-Day"); window.Tab10Day.Focus(); }
				else if (window.ViewModel.CurrentActiveView == "10-Day") { window.Tab10Day.Focus(); }
			}
			else if (e.Key == Key.Enter)
			{
				if (window.ViewModel.IsAlertIconSelected)
				{
					e.Handled = true;
					window.ChangeTab("Alerts");
				}
				else if (window.ViewModel.IsWeatherAnimationSelected)
				{
					e.Handled = true;
					window.ChangeTab("Summary");
				}
				else if (window.ViewModel.IsLocationMenuSelected)
				{
					e.Handled = true;
					window.ViewModel.IsLocationMenuSelected = false;
					window.CurrentView.LocationComboBox.IsDropDownOpen = true;

					window.Dispatcher.BeginInvoke(new System.Action(() =>
					{
						int index = window.CurrentView.LocationComboBox.SelectedIndex >= 0 ? window.CurrentView.LocationComboBox.SelectedIndex : 0;
						if (window.CurrentView.LocationComboBox.ItemContainerGenerator.ContainerFromIndex(index) is ComboBoxItem item)
						{
							item.Focus();
						}
					}), DispatcherPriority.Input);
				}
				else if (Keyboard.FocusedElement is Border b && (b.Name == "SummaryBackBorder" || b.Name == "RadarBackBorder" || b.Name == "AlertBackBorder" || b.Name == "LocationManagerBackBorder"))
				{
					e.Handled = true;
					string previousView = window.ViewModel.CurrentActiveView;
					window.TabCurrent.Focus();
					window.ChangeTab("Current");

					window.ViewModel.IsAlertIconSelected = false;
					if (previousView == "Alerts" || previousView == "Radar" || previousView == "LocationManager")
					{
						window.TabCurrent.Tag = "Unselected";
						window.ViewModel.IsWeatherAnimationSelected = false;
						window.ViewModel.IsLocationMenuSelected = true;
						window.Dispatcher.BeginInvoke(new Action(() => {
							System.Windows.Input.FocusManager.SetFocusedElement(window.CurrentView, null);
							window.CurrentView.LocationComboBox.Focus();
						}), DispatcherPriority.Loaded);
					}
					else if (previousView == "Summary")
					{
						window.ViewModel.IsLocationMenuSelected = false;
						window.ViewModel.IsWeatherAnimationSelected = true;
					}
				}
				else if (window.ViewModel.CurrentActiveView == "Radar" && Keyboard.FocusedElement == window.RadarPlayPauseButton)
				{
					e.Handled = true;
					if (window._isRadarPaused) window.PlayRadar();
					else window.PauseRadar();
				}
				else if (window.ViewModel.CurrentActiveView == "Radar" && Keyboard.FocusedElement == window.RadarCopyButton)
				{
					e.Handled = true;
					window.RadarCopyButton_MouseLeftButtonUp(window, null!);
				}
				else if (window.ViewModel.CurrentActiveView == "Radar" && Keyboard.FocusedElement == window.RadarSaveGifButton)
				{
					e.Handled = true;
					window.RadarSaveGifButton_MouseLeftButtonUp(window, null!);
				}
				else if (Keyboard.FocusedElement == window.TabCurrent)
				{
					e.Handled = true;
					window.ChangeTab("Current");
				}
				else if (Keyboard.FocusedElement == window.TabHourly)
				{
					e.Handled = true;
					window.ChangeTab("Hourly");
				}
				else if (Keyboard.FocusedElement == window.Tab10Day)
				{
					e.Handled = true;
					window.ChangeTab("10-Day");
				}
			}
		}

		private static void ProcessSettingsNavigation(Settings window, KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
			{
				if (window.IntervalInput.IsKeyboardFocusWithin && window.IntervalInput.Tag as string == "Editing")
				{
					e.Handled = true;
					if (window.IntervalInput.Value < 15) window.IntervalInput.Value = 15;
					window.IntervalInput.Tag = "Navigating";
					window.IntervalInput.SetResourceReference(FrameworkElement.FocusVisualStyleProperty, "SettingsFocusVisual");
					window.IntervalInput.Focus();
					window.Dispatcher.BeginInvoke(new Action(() => {
						if (Keyboard.FocusedElement is TextBox tb) { tb.SelectionLength = 0; tb.CaretIndex = tb.Text.Length; tb.CaretBrush = Brushes.Transparent; }
					}), DispatcherPriority.Input);
					return;
				}

				e.Handled = true;
				if (window.IsRecordingHotkey) window.StopHotkeyRecording();
				else { window.CancelScalePreviewTimer(); window.Close(); }
				return;
			}

			if (window.IsRecordingHotkey && Keyboard.FocusedElement == window.HotkeyInput)
			{
				return; // Allow HotkeyInput_PreviewKeyDown to record the key
			}

			if (!window.IsRecordingHotkey && Keyboard.FocusedElement == window.HotkeyInput)
			{
				if (e.Key == Key.Enter)
				{
					e.Handled = true;
					window.StartHotkeyRecording();
					return;
				}
				if (e.Key == Key.Up || e.Key == Key.Down)
				{
					e.Handled = true;
					window.HotkeyInput.MoveFocus(new TraversalRequest(e.Key == Key.Down ? FocusNavigationDirection.Down : FocusNavigationDirection.Up));
					return;
				}
			}

			if (!window._isKeyboardNavRevealed && (e.Key == Key.Up || e.Key == Key.Down || e.Key == Key.Left || e.Key == Key.Right))
			{
				window._isKeyboardNavRevealed = true;
				e.Handled = true;

				if (window.SettingsTabControl.ItemContainerGenerator.ContainerFromIndex(window.SettingsTabControl.SelectedIndex) is TabItem currentTab)
				{
					currentTab.Focus();
				}
				return;
			}

			if (Keyboard.FocusedElement is TabItem)
			{
				if (e.Key == Key.Up || e.Key == Key.Down)
				{
					e.Handled = true;
					int newIndex = window.SettingsTabControl.SelectedIndex + (e.Key == Key.Down ? 1 : -1);
					if (newIndex < 0) newIndex = window.SettingsTabControl.Items.Count - 1;
					if (newIndex >= window.SettingsTabControl.Items.Count) newIndex = 0;

					window.SettingsTabControl.SelectedIndex = newIndex;
					if (window.SettingsTabControl.ItemContainerGenerator.ContainerFromIndex(newIndex) is TabItem newTab)
					{
						newTab.Focus();
					}
					return;
				}
				if (e.Key == Key.Right)
				{
					e.Handled = true;
					if (window.SettingsTabControl.SelectedContent is FrameworkElement content)
					{
						content.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
					}
					return;
				}
				if (e.Key == Key.Left)
				{
					e.Handled = true;
					window.HelpFileTextBlock.Focus();
					return;
				}
			}

			if ((e.Key == Key.Left || e.Key == Key.Right) && Keyboard.FocusedElement == window.SlideoutScaleSlider)
			{
				e.Handled = true;
				window.SlideoutScaleSlider.Value += (e.Key == Key.Right) ? 0.01 : -0.01;
				window.TriggerScalePreviewTimer();
				return;
			}

			if (e.Key == Key.Right && Keyboard.FocusedElement is not TabItem)
			{
				if (Keyboard.FocusedElement == window.AlertToneInput && !window.AlertToneInput.IsDropDownOpen)
				{
					e.Handled = true;
					window.PlayToneButton.Focus();
					return;
				}

				if (Keyboard.FocusedElement == window.BottomRadio)
				{
					e.Handled = true;
					window.TopRadio.Focus();
					return;
				}

				if (Keyboard.FocusedElement == window.AppInfoTextBlock || Keyboard.FocusedElement == window.HelpFileTextBlock)
				{
					e.Handled = true;
					window.SaveSettingsButton.Focus();
					return;
				}

				if (Keyboard.FocusedElement == window.ExportSettingsButton)
				{
					e.Handled = true;
					window.ImportSettingsButton.Focus();
					return;
				}

				if (Keyboard.FocusedElement is not Slider && Keyboard.FocusedElement is not TextBox)
				{
					e.Handled = true; // Prevent native horizontal jumping 
					return;
				}
			}

			if (e.Key == Key.Left && Keyboard.FocusedElement is not TabItem)
			{
				if (Keyboard.FocusedElement == window.PlayToneButton)
				{
					e.Handled = true;
					window.AlertToneInput.Focus();
					return;
				}

				if (Keyboard.FocusedElement == window.TopRadio)
				{
					e.Handled = true;
					window.BottomRadio.Focus();
					return;
				}

				if (Keyboard.FocusedElement == window.SaveSettingsButton)
				{
					e.Handled = true;
					window.HelpFileTextBlock.Focus();
					return;
				}

				if (Keyboard.FocusedElement == window.AppInfoTextBlock || Keyboard.FocusedElement == window.HelpFileTextBlock)
				{
					e.Handled = true;
					return;
				}

				if (Keyboard.FocusedElement == window.ImportSettingsButton)
				{
					e.Handled = true;
					window.ExportSettingsButton.Focus();
					return;
				}

				if (Keyboard.FocusedElement is Slider) return;
				if (Keyboard.FocusedElement is TextBox tb && tb.CaretIndex > 0) return;
				if (Keyboard.FocusedElement is ComboBox cb && cb.IsDropDownOpen) return;

				e.Handled = true;
				if (window.SettingsTabControl.ItemContainerGenerator.ContainerFromIndex(window.SettingsTabControl.SelectedIndex) is TabItem activeTab)
				{
					activeTab.Focus();
				}
				return;
			}

			if (e.Key == Key.Down && Keyboard.FocusedElement == window.AlertToneInput && !window.AlertToneInput.IsDropDownOpen)
			{
				e.Handled = true;
				window.AlertVolumeSlider.Focus();
				return;
			}

			if (e.Key == Key.Down && Keyboard.FocusedElement == window.PlayToneButton)
			{
				e.Handled = true;
				window.AlertVolumeSlider.Focus();
				return;
			}

			if (e.Key == Key.Up && Keyboard.FocusedElement == window.AlertVolumeSlider)
			{
				e.Handled = true;
				window.AlertToneInput.Focus();
				return;
			}

			if (e.Key == Key.Up && Keyboard.FocusedElement == window.SlideoutScaleSlider)
			{
				e.Handled = true;
				window.CancelScalePreviewTimer();
				window.UseAnimatedIcons.Focus();
				return;
			}

			if (e.Key == Key.Down && Keyboard.FocusedElement is Slider)
			{
				e.Handled = true;
				if (Keyboard.FocusedElement == window.SlideoutScaleSlider)
				{
					window.CancelScalePreviewTimer();
				}
				window.SaveSettingsButton.Focus();
				return;
			}

			if (e.Key == Key.Down && Keyboard.FocusedElement == window.SaveSettingsButton)
			{
				e.Handled = true;
				return;
			}

			if (e.Key == Key.Down && Keyboard.FocusedElement == window.AppInfoTextBlock)
			{
				e.Handled = true;
				window.HelpFileTextBlock.Focus();
				return;
			}

			if (e.Key == Key.Down && Keyboard.FocusedElement == window.HelpFileTextBlock)
			{
				e.Handled = true;
				window.AppInfoTextBlock.Focus();
				return;
			}

			if (window.IntervalInput.IsKeyboardFocusWithin)
			{
				if (window.IntervalInput.Tag as string != "Editing")
				{
					if (e.Key == Key.Enter)
					{
						e.Handled = true;
						window.IntervalInput.Tag = "Editing";
						window.IntervalInput.FocusVisualStyle = null;
						if (Keyboard.FocusedElement is TextBox tb) { tb.ClearValue(TextBox.CaretBrushProperty); tb.SelectAll(); }
						return;
					}
					if (e.Key == Key.Down)
					{
						e.Handled = true;
						window.ExportSettingsButton.Focus();
						return;
					}
					if (e.Key == Key.Up)
					{
						e.Handled = true;
						if (window.WindowsLocationCheckbox.IsEnabled) window.WindowsLocationCheckbox.Focus();
						else window.StartupCheckbox.Focus();
						return;
					}
					if (e.Key == Key.Right)
					{
						e.Handled = true;
						return;
					}
					if (e.Key == Key.Left)
					{
						e.Handled = true;
						if (window.SettingsTabControl.ItemContainerGenerator.ContainerFromIndex(window.SettingsTabControl.SelectedIndex) is TabItem activeTab)
						{
							activeTab.Focus();
						}
						return;
					}

					if (e.Key != Key.Tab)
					{
						e.Handled = true;
						return;
					}
				}
				else
				{
					if (e.Key == Key.Enter || e.Key == Key.Escape)
					{
						e.Handled = true;
						if (window.IntervalInput.Value < 15) window.IntervalInput.Value = 15;
						window.IntervalInput.Tag = "Navigating";
						window.IntervalInput.SetResourceReference(FrameworkElement.FocusVisualStyleProperty, "SettingsFocusVisual");
						window.IntervalInput.Focus();
						window.Dispatcher.BeginInvoke(new Action(() => {
							if (Keyboard.FocusedElement is TextBox tb) { tb.SelectionLength = 0; tb.CaretIndex = tb.Text.Length; tb.CaretBrush = Brushes.Transparent; }
						}), DispatcherPriority.Input);
						return;
					}
					if (e.Key == Key.Down && window.IntervalInput.Value <= 15)
					{
						e.Handled = true;
						window.IntervalInput.Value = 15;
						return;
					}
					if (e.Key == Key.Left || e.Key == Key.Right)
					{
						return; // Let the native text box handle caret movement without switching tabs
					}
				}
			}

			if (e.Key == Key.Down && Keyboard.FocusedElement == window.WindowsLocationCheckbox)
			{
				e.Handled = true;
				window.IntervalInput.Tag = "Navigating";
				window.IntervalInput.SetResourceReference(FrameworkElement.FocusVisualStyleProperty, "SettingsFocusVisual");
				window.IntervalInput.Focus();
				window.Dispatcher.BeginInvoke(new Action(() => {
					if (Keyboard.FocusedElement is TextBox tb) { tb.SelectionLength = 0; tb.CaretIndex = tb.Text.Length; tb.CaretBrush = Brushes.Transparent; }
				}), DispatcherPriority.Input);
				return;
			}

			if (e.Key == Key.Down && Keyboard.FocusedElement == window.Use24HourClockCheckbox)
			{
				e.Handled = true;
				window.SaveSettingsButton.Focus();
				return;
			}

			if (e.Key == Key.Down && (Keyboard.FocusedElement == window.ExportSettingsButton || Keyboard.FocusedElement == window.ImportSettingsButton))
			{
				e.Handled = true;
				window.SaveSettingsButton.Focus();
				return;
			}

			if (e.Key == Key.Up && (Keyboard.FocusedElement == window.ExportSettingsButton || Keyboard.FocusedElement == window.ImportSettingsButton))
			{
				e.Handled = true;
				window.IntervalInput.Tag = "Navigating";
				window.IntervalInput.SetResourceReference(FrameworkElement.FocusVisualStyleProperty, "SettingsFocusVisual");
				window.IntervalInput.Focus();
				window.Dispatcher.BeginInvoke(new Action(() => {
					if (Keyboard.FocusedElement is TextBox tb) { tb.SelectionLength = 0; tb.CaretIndex = tb.Text.Length; tb.CaretBrush = Brushes.Transparent; }
				}), DispatcherPriority.Input);
				return;
			}

			if (e.Key == Key.Down && window.DefaultViewInput.IsKeyboardFocusWithin && !window.DefaultViewInput.IsDropDownOpen)
			{
				e.Handled = true;
				window.UseAnimationsCheckbox.Focus();
				return;
			}

			if (e.Key == Key.Down && Keyboard.FocusedElement == window.UseAnimationsCheckbox)
			{
				e.Handled = true;
				window.SaveSettingsButton.Focus();
				return;
			}

			if (e.Key == Key.Up && Keyboard.FocusedElement == window.UseAnimationsCheckbox)
			{
				e.Handled = true;
				window.DefaultViewInput.Focus();
				return;
			}

			if (e.Key == Key.Up && Keyboard.FocusedElement == window.HelpFileTextBlock)
			{
				e.Handled = true;
				window.AppInfoTextBlock.Focus();
				return;
			}

			if (e.Key == Key.Up && Keyboard.FocusedElement == window.AppInfoTextBlock)
			{
				e.Handled = true;
				window.HelpFileTextBlock.Focus();
				return;
			}

			if (e.Key == Key.Up && Keyboard.FocusedElement == window.SaveSettingsButton)
			{
				e.Handled = true;
				switch (window.SettingsTabControl.SelectedIndex)
				{
					case 0: window.AlertVolumeSlider.Focus(); break;
					case 1: window.SlideoutScaleSlider.Focus(); break;
					case 2: window.UseAnimationsCheckbox.Focus(); break;
					case 3: window.Use24HourClockCheckbox.Focus(); break;
					case 4:
						window.ExportSettingsButton.Focus();
						break;
					default: window.SettingsTabControl.Focus(); break;
				}
				return;
			}

			if (e.Key == Key.Down && Keyboard.FocusedElement == window.DefaultViewInput && !window.DefaultViewInput.IsDropDownOpen)
			{
				e.Handled = true;
				window.UseAnimationsCheckbox.Focus();
				return;
			}

			if ((e.Key == Key.Up || e.Key == Key.Down) && Keyboard.FocusedElement is ComboBox cBox && !cBox.IsDropDownOpen)
			{
				e.Handled = true;
				cBox.MoveFocus(new TraversalRequest(e.Key == Key.Down ? FocusNavigationDirection.Down : FocusNavigationDirection.Up));
				return;
			}

			if (e.Key == Key.Enter)
			{
				if (Keyboard.FocusedElement is CheckBox cb)
				{
					e.Handled = true;
					cb.IsChecked = !cb.IsChecked;
				}
				else if (Keyboard.FocusedElement is RadioButton rb)
				{
					e.Handled = true;
					rb.IsChecked = true;
				}
				else if (Keyboard.FocusedElement is ComboBox cbEnter && !cbEnter.IsDropDownOpen)
				{
					e.Handled = true;
					cbEnter.IsDropDownOpen = true;
				}
				else if (Keyboard.FocusedElement == window.PlayToneButton || Keyboard.FocusedElement == window.AlertVolumeSlider)
				{
					e.Handled = true;
					window.PlayToneButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
				}
				else if (Keyboard.FocusedElement == window.SaveSettingsButton)
				{
					e.Handled = true;
					window.SaveSettingsButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
				}
				else if (Keyboard.FocusedElement == window.ExportSettingsButton)
				{
					e.Handled = true;
					window.ExportSettingsButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
				}
				else if (Keyboard.FocusedElement == window.ImportSettingsButton)
				{
					e.Handled = true;
					window.ImportSettingsButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
				}
				else if (Keyboard.FocusedElement == window.AppInfoTextBlock)
				{
					e.Handled = true;
					window.AppInfo_MouseLeftButtonUp(window.AppInfoTextBlock, null!);
				}
				else if (Keyboard.FocusedElement == window.HelpFileTextBlock)
				{
					e.Handled = true;
					window.HelpFile_MouseLeftButtonUp(window.HelpFileTextBlock, null!);
				}
			}

			if (e.Key == Key.Space)
			{
				if (Keyboard.FocusedElement == window.AppInfoTextBlock)
				{
					e.Handled = true;
					window.AppInfo_MouseLeftButtonUp(window.AppInfoTextBlock, null!);
				}
				else if (Keyboard.FocusedElement == window.HelpFileTextBlock)
				{
					e.Handled = true;
					window.HelpFile_MouseLeftButtonUp(window.HelpFileTextBlock, null!);
				}
			}
		}

		private static RichTextBox? FindRichTextBox(DependencyObject parent)
		{
			if (parent == null) return null;
			for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
			{
				var child = VisualTreeHelper.GetChild(parent, i);
				if (child is RichTextBox rtb) return rtb;
				var found = FindRichTextBox(child);
				if (found != null) return found;
			}
			return null;
		}

		public static void FocusListItem(ItemsControl list, int index)
		{
			if (index >= 0 && index < list.Items.Count)
			{
				if (list.ItemContainerGenerator.ContainerFromIndex(index) is ContentPresenter cp && VisualTreeHelper.GetChildrenCount(cp) > 0 && VisualTreeHelper.GetChild(cp, 0) is FrameworkElement fe)
				{
					fe.Focus();
					fe.BringIntoView();
				}
			}
		}

		public static bool ProcessRadarNavigation(SecondaryViews window, KeyEventArgs e)
		{
			if (window.ViewModel.CurrentActiveView != "Radar") return false;

			if (e.Key == Key.Down)
			{
				if (Keyboard.FocusedElement == window.RadarBackBorder) { e.Handled = true; window.RadarScrubber.Focus(); return true; }
				if (Keyboard.FocusedElement == window.RadarScrubber) { e.Handled = true; window.RadarPlayPauseButton.Focus(); return true; }
			}
			if (e.Key == Key.Up)
			{
				if (Keyboard.FocusedElement == window.RadarScrubber) { e.Handled = true; window.RadarBackBorder.Focus(); return true; }
				if (Keyboard.FocusedElement == window.RadarPlayPauseButton || Keyboard.FocusedElement == window.RadarCopyButton || Keyboard.FocusedElement == window.RadarSaveGifButton)
				{
					e.Handled = true; window.RadarScrubber.Focus(); return true;
				}
			}
			if (e.Key == Key.Left)
			{
				if (Keyboard.FocusedElement == window.RadarScrubber)
				{
					e.Handled = true; window.PauseRadar();
					if (window.RadarScrubber.Maximum > 0) window.RadarScrubber.Value = window.RadarScrubber.Value - 1 < 0 ? window.RadarScrubber.Maximum : window.RadarScrubber.Value - 1;
					return true;
				}
				if (Keyboard.FocusedElement == window.RadarPlayPauseButton) { e.Handled = true; window.RadarSaveGifButton.Focus(); return true; }
				if (Keyboard.FocusedElement == window.RadarSaveGifButton) { e.Handled = true; window.RadarCopyButton.Focus(); return true; }
				if (Keyboard.FocusedElement == window.RadarCopyButton) { e.Handled = true; window.RadarPlayPauseButton.Focus(); return true; }
			}
			if (e.Key == Key.Right)
			{
				if (Keyboard.FocusedElement == window.RadarScrubber)
				{
					e.Handled = true; window.PauseRadar();
					if (window.RadarScrubber.Maximum > 0) window.RadarScrubber.Value = window.RadarScrubber.Value + 1 > window.RadarScrubber.Maximum ? 0 : window.RadarScrubber.Value + 1;
					return true;
				}
				if (Keyboard.FocusedElement == window.RadarPlayPauseButton) { e.Handled = true; window.RadarCopyButton.Focus(); return true; }
				if (Keyboard.FocusedElement == window.RadarCopyButton) { e.Handled = true; window.RadarSaveGifButton.Focus(); return true; }
				if (Keyboard.FocusedElement == window.RadarSaveGifButton) { e.Handled = true; window.RadarPlayPauseButton.Focus(); return true; }
			}
			if (e.Key == Key.Enter)
			{
				if (Keyboard.FocusedElement == window.RadarPlayPauseButton)
				{
					e.Handled = true;
					if (window._isRadarPaused) window.PlayRadar(); else window.PauseRadar();
					return true;
				}
				if (Keyboard.FocusedElement == window.RadarCopyButton) { e.Handled = true; window.RadarCopyButton_MouseLeftButtonUp(window, null!); return true; }
				if (Keyboard.FocusedElement == window.RadarSaveGifButton) { e.Handled = true; window.RadarSaveGifButton_MouseLeftButtonUp(window, null!); return true; }
			}
			return false;
		}

		public static bool ProcessSummaryNavigation(SecondaryViews window, KeyEventArgs e)
		{
			if (window.ViewModel.CurrentActiveView != "Summary") return false;

			if (e.Key == Key.Down && Keyboard.FocusedElement == window.SummaryBackBorder)
			{
				e.Handled = true;
				if (window.SummaryScroller.ScrollableHeight > 0) window.SummaryScroller.Focus();
				else window.TabCurrent.Focus();
				return true;
			}

			if (Keyboard.FocusedElement == window.SummaryScroller)
			{
				if (e.Key == Key.Up) { e.Handled = true; window.SummaryBackBorder.Focus(); return true; }
				if (e.Key == Key.Down) { e.Handled = true; window.TabCurrent.Focus(); return true; }
				if (e.Key == Key.Left) { e.Handled = true; window.SummaryScroller.SmoothScrollBy(-40); return true; }
				if (e.Key == Key.Right) { e.Handled = true; window.SummaryScroller.SmoothScrollBy(40); return true; }
			}
			return false;
		}

		public static bool ProcessAlertsNavigation(SecondaryViews window, KeyEventArgs e)
		{
			if (window.ViewModel.CurrentActiveView != "Alerts") return false;

			if (Keyboard.FocusedElement == window.AlertBackBorder)
			{
				if (e.Key == Key.Up)
				{
					e.Handled = true;
					return true;
				}
				if (e.Key == Key.Right)
				{
					e.Handled = true;
					if (window.NewAlertCounterText.Visibility == Visibility.Visible) window.NewAlertCounterText.Focus();
					return true;
				}
				if (e.Key == Key.Down)
				{
					e.Handled = true;
					window.AlertScroller.Focus();
					UpdateAlertButtonFocus(window);
					return true;
				}
			}

			if (Keyboard.FocusedElement == window.NewAlertCounterText)
			{
				if (e.Key == Key.Up || e.Key == Key.Right)
				{
					e.Handled = true;
					return true;
				}
				if (e.Key == Key.Left)
				{
					e.Handled = true;
					window.AlertBackBorder.Focus();
					return true;
				}
				if (e.Key == Key.Down)
				{
					e.Handled = true;
					window.AlertScroller.Focus();
					UpdateAlertButtonFocus(window);
					return true;
				}
				if (e.Key == Key.Enter || e.Key == Key.Space)
				{
					e.Handled = true;
					window.NewAlertCounter_MouseLeftButtonUp(window.NewAlertCounterText, null!);
					return true;
				}
			}

			bool isAlertScrollerOrButton = Keyboard.FocusedElement == window.AlertScroller ||
										   (Keyboard.FocusedElement is FrameworkElement bf && bf.Name != null && bf.Name.StartsWith("ClearA")) ||
										   Keyboard.FocusedElement is System.Windows.Documents.Hyperlink;

			if (isAlertScrollerOrButton)
			{
				if (e.Key == Key.Up)
				{
					e.Handled = true;
					if (window.AlertScroller.ScrollableHeight == 0 && Keyboard.FocusedElement is FrameworkElement clearBtn && clearBtn.Name != null && clearBtn.Name.StartsWith("ClearA")) clearBtn.MoveFocus(new TraversalRequest(FocusNavigationDirection.Previous));
					else window.AlertBackBorder.Focus();
					window.Dispatcher.BeginInvoke(new Action(() => UpdateAlertButtonFocus(window)), DispatcherPriority.Input);
					return true;
				}
				if (e.Key == Key.Down)
				{
					e.Handled = true;
					if (window.AlertScroller.ScrollableHeight == 0 && Keyboard.FocusedElement is System.Windows.Documents.Hyperlink)
					{
						if (Keyboard.FocusedElement is ContentElement ce) ce.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
					}
					else window.TabCurrent.Focus();
					window.Dispatcher.BeginInvoke(new Action(() => UpdateAlertButtonFocus(window)), DispatcherPriority.Input);
					return true;
				}
				if (e.Key == Key.Left)
				{
					e.Handled = true;
					if (window.AlertScroller.ScrollableHeight == 0)
					{
						if (Keyboard.FocusedElement is UIElement ui) ui.MoveFocus(new TraversalRequest(FocusNavigationDirection.Previous));
						else if (Keyboard.FocusedElement is ContentElement ce) ce.MoveFocus(new TraversalRequest(FocusNavigationDirection.Previous));
					}
					else
					{
						window.AlertScroller.SmoothScrollBy(-40);
						window.Dispatcher.BeginInvoke(new Action(() => UpdateAlertButtonFocus(window)), DispatcherPriority.Input);
					}
					return true;
				}
				if (e.Key == Key.Right)
				{
					e.Handled = true;
					if (window.AlertScroller.ScrollableHeight == 0)
					{
						if (Keyboard.FocusedElement is UIElement ui) ui.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
						else if (Keyboard.FocusedElement is ContentElement ce) ce.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
					}
					else
					{
						window.AlertScroller.SmoothScrollBy(40);
						window.Dispatcher.BeginInvoke(new Action(() => UpdateAlertButtonFocus(window)), DispatcherPriority.Input);
					}
					return true;
				}
				if (e.Key == Key.Enter || e.Key == Key.Space)
				{
					if (Keyboard.FocusedElement == window.AlertScroller && _activeAlertTarget != null)
					{
						e.Handled = true;
						if (_activeAlertTarget is FrameworkElement pseudoBtn && pseudoBtn.Name != null && pseudoBtn.Name.StartsWith("ClearA"))
						{
							var peer = System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(pseudoBtn);
							var invokeProv = peer?.GetPattern(System.Windows.Automation.Peers.PatternInterface.Invoke) as System.Windows.Automation.Provider.IInvokeProvider;
							invokeProv?.Invoke();
						}
						else if (_activeAlertTarget is System.Windows.Documents.Hyperlink pseudoHl)
						{
							pseudoHl.DoClick();
						}
						window.Dispatcher.BeginInvoke(new Action(() => UpdateAlertButtonFocus(window)), DispatcherPriority.Input);
						return true;
					}
					else if (Keyboard.FocusedElement is FrameworkElement btn && btn.Name != null && btn.Name.StartsWith("ClearA"))
					{
						e.Handled = true;
						window.AlertScroller.Focus();
						var peer = System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(btn);
						var invokeProv = peer?.GetPattern(System.Windows.Automation.Peers.PatternInterface.Invoke) as System.Windows.Automation.Provider.IInvokeProvider;
						invokeProv?.Invoke();
						window.Dispatcher.BeginInvoke(new Action(() => UpdateAlertButtonFocus(window)), DispatcherPriority.Input);
						return true;
					}
					else if (Keyboard.FocusedElement is System.Windows.Documents.Hyperlink hlFallback)
					{
						e.Handled = true;
						hlFallback.DoClick();
						return true;
					}
				}
			}

			return false;
		}

		public static bool ProcessLocationManagerNavigation(SecondaryViews window, KeyEventArgs e)
		{
			if (window.ViewModel.CurrentActiveView != "LocationManager") return false;

			bool isDropdownOpen = window.LocManCountryInput.IsDropDownOpen || window.LocManStateInput.IsDropDownOpen;
			if (isDropdownOpen)
			{
				if (e.Key == Key.Up || e.Key == Key.Down || e.Key == Key.Enter || e.Key == Key.Escape) return true;
				return false;
			}

			if (window.CitySuggestionsPopup.IsOpen && (e.Key == Key.Back || e.Key == Key.Escape))
			{
				e.Handled = true; window.CitySuggestionsPopup.IsOpen = false; window.LocManCityInput.Focus(); return true;
			}

			var focused = Keyboard.FocusedElement as FrameworkElement;

			if (e.Key == Key.Enter)
			{
				if (focused == window.LocManCountryInput) { e.Handled = true; window.LocManCountryInput.IsDropDownOpen = true; return true; }
				if (focused == window.LocManStateInput) { e.Handled = true; window.LocManStateInput.IsDropDownOpen = true; return true; }
				if (focused == window.AddLocation || (window.CitySuggestionsPopup.IsOpen && focused != null && window.CitySuggestionsList.Items.Contains(focused.DataContext)))
				{
					e.Handled = true;
					focused.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonUpEvent });
					return true;
				}
				if (focused == window.LocManCityInput)
				{
					e.Handled = true; window.CitySuggestionsPopup.IsOpen = false;
					window.AddLocation.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonUpEvent });
					return true;
				}
				if (focused is Border bIcon && bIcon.DataContext is SavedLocation && bIcon.Name != "LocItemMainBorder")
				{
					e.Handled = true;
					bIcon.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonUpEvent });
					return true;
				}
			}

			if (e.Key == Key.Left || e.Key == Key.Right)
			{
				if (focused is Border b && b.DataContext is SavedLocation)
				{
					e.Handled = true;
					DependencyObject current = b;
					while (current is not null and not ContentPresenter) current = VisualTreeHelper.GetParent(current);

					if (current is ContentPresenter cp)
					{
						var elements = new System.Collections.Generic.List<Border>();
						if (cp.ContentTemplate.FindName("LocItemMainBorder", cp) is Border mainBorder && mainBorder.Visibility == Visibility.Visible) elements.Add(mainBorder);
						if (cp.ContentTemplate.FindName("LocItemAlertBorder", cp) is Border alertBorder && alertBorder.Visibility == Visibility.Visible) elements.Add(alertBorder);
						if (cp.ContentTemplate.FindName("LocItemToneBorder", cp) is Border toneBorder && toneBorder.Visibility == Visibility.Visible) elements.Add(toneBorder);
						if (cp.ContentTemplate.FindName("LocItemHomeBorder", cp) is Border homeBorder && homeBorder.Visibility == Visibility.Visible) elements.Add(homeBorder);
						if (cp.ContentTemplate.FindName("LocItemDeleteBorder", cp) is Border deleteBorder && deleteBorder.Visibility == Visibility.Visible) elements.Add(deleteBorder);

						int idx = elements.IndexOf(b);
						if (idx >= 0 && elements.Count > 0)
						{
							if (e.Key == Key.Right) elements[(idx + 1) % elements.Count].Focus();
							else elements[(idx - 1 + elements.Count) % elements.Count].Focus();
						}
					}
					return true;
				}
			}

			if (e.Key == Key.Down || e.Key == Key.Tab)
			{
				if (focused == window.LocationManagerBackBorder) { e.Handled = true; window.LocManCountryInput.Focus(); return true; }
				if (focused == window.LocManCountryInput) { e.Handled = true; window.LocManStateInput.Focus(); return true; }
				if (focused == window.LocManStateInput) { e.Handled = true; window.LocManCityInput.Focus(); return true; }
				if (focused == window.LocManCityInput)
				{
					e.Handled = true;
					if (window.CitySuggestionsPopup.IsOpen && window.CitySuggestionsList.Items.Count > 0) FocusListItem(window.CitySuggestionsList, 0);
					else window.AddLocation.Focus();
					return true;
				}
				if (window.CitySuggestionsPopup.IsOpen && focused != null && window.CitySuggestionsList.Items.Contains(focused.DataContext))
				{
					e.Handled = true;
					int index = window.CitySuggestionsList.Items.IndexOf(focused.DataContext);
					if (index >= 0 && index < window.CitySuggestionsList.Items.Count - 1) FocusListItem(window.CitySuggestionsList, index + 1);
					else if (index == window.CitySuggestionsList.Items.Count - 1) FocusListItem(window.CitySuggestionsList, 0);
					return true;
				}
				if (focused == window.AddLocation)
				{
					e.Handled = true;
					if (window.SavedLocationsList.Items.Count > 0) FocusListItem(window.SavedLocationsList, 0);
					else window.TabCurrent.Focus();
					return true;
				}
				if (focused is Border b && b.DataContext is SavedLocation)
				{
					e.Handled = true;
					int index = window.SavedLocationsList.Items.IndexOf(b.DataContext);
					if (index >= 0 && index < window.SavedLocationsList.Items.Count - 1) FocusListItem(window.SavedLocationsList, index + 1);
					else if (index == window.SavedLocationsList.Items.Count - 1) window.TabCurrent.Focus();
					return true;
				}

				if (focused == window.LocManCityInput || focused == window.LocManStateInput || focused == window.LocManCountryInput || focused == window.AddLocation || (focused != null && (window.SavedLocationsList.Items.Contains(focused.DataContext) || window.CitySuggestionsList.Items.Contains(focused.DataContext))))
				{
					e.Handled = true; return true;
				}
			}

			if (e.Key == Key.Up)
			{
				if (focused == window.TabCurrent || focused == window.TabHourly || focused == window.Tab10Day)
				{
					e.Handled = true;
					if (window.SavedLocationsList.Items.Count > 0) FocusListItem(window.SavedLocationsList, window.SavedLocationsList.Items.Count - 1);
					else window.AddLocation.Focus();
					return true;
				}
				if (focused == window.AddLocation)
				{
					e.Handled = true;
					if (window.CitySuggestionsPopup.IsOpen && window.CitySuggestionsList.Items.Count > 0) FocusListItem(window.CitySuggestionsList, window.CitySuggestionsList.Items.Count - 1);
					else window.LocManCityInput.Focus();
					return true;
				}
				if (window.CitySuggestionsPopup.IsOpen && focused != null && window.CitySuggestionsList.Items.Contains(focused.DataContext))
				{
					e.Handled = true;
					int index = window.CitySuggestionsList.Items.IndexOf(focused.DataContext);
					if (index > 0) FocusListItem(window.CitySuggestionsList, index - 1);
					else if (index == 0) FocusListItem(window.CitySuggestionsList, window.CitySuggestionsList.Items.Count - 1);
					return true;
				}
				if (focused == window.LocManCityInput) { e.Handled = true; window.LocManStateInput.Focus(); return true; }
				if (focused == window.LocManStateInput) { e.Handled = true; window.LocManCountryInput.Focus(); return true; }
				if (focused == window.LocManCountryInput) { e.Handled = true; window.LocationManagerBackBorder.Focus(); return true; }

				if (focused is Border b && b.DataContext is SavedLocation)
				{
					e.Handled = true;
					int index = window.SavedLocationsList.Items.IndexOf(b.DataContext);
					if (index > 0) FocusListItem(window.SavedLocationsList, index - 1);
					else if (index == 0) { window.AddLocation.Focus(); window.SavedLocationsScroller.ScrollToTop(); }
					return true;
				}
				if (focused == window.LocationManagerBackBorder) { e.Handled = true; return true; }

				if (focused == window.LocManCityInput || focused == window.LocManStateInput || focused == window.LocManCountryInput || focused == window.AddLocation || (focused != null && (window.SavedLocationsList.Items.Contains(focused.DataContext) || window.CitySuggestionsList.Items.Contains(focused.DataContext))))
				{
					e.Handled = true; return true;
				}
			}

			return false;
		}

		public static async void UpdateAlertButtonFocus(SecondaryViews window)
		{
			if (!window._isKeyboardNavRevealed || window.ViewModel.CurrentActiveView != "Alerts")
			{
				SetPseudoFocus(_activeAlertTarget, false);
				_activeAlertTarget = null;
				return;
			}

			bool isFocusInAlerts = Keyboard.FocusedElement == window.AlertScroller ||
								   (Keyboard.FocusedElement is FrameworkElement bf && bf.Name != null && bf.Name.StartsWith("ClearA")) ||
								   Keyboard.FocusedElement is System.Windows.Documents.Hyperlink;

			if (!isFocusInAlerts)
			{
				SetPseudoFocus(_activeAlertTarget, false);
				_activeAlertTarget = null;
				return;
			}

			await System.Threading.Tasks.Task.Delay(50);
			if (!window.IsVisible) return;

			bool isFocusStillInAlerts = Keyboard.FocusedElement == window.AlertScroller ||
										(Keyboard.FocusedElement is FrameworkElement bCheck && bCheck.Name != null && bCheck.Name.StartsWith("ClearA")) ||
										Keyboard.FocusedElement is System.Windows.Documents.Hyperlink;

			if (!isFocusStillInAlerts)
			{
				SetPseudoFocus(_activeAlertTarget, false);
				_activeAlertTarget = null;
				return;
			}

			IInputElement? bestItem = null;
			double minDistance = double.MaxValue;
			double bestTop = double.MinValue;

			IInputElement? fallbackLowestItem = null;
			double lowestY = double.MinValue;

			double viewportHeight = window.AlertScroller.ViewportHeight;
			if (viewportHeight <= 0) return;

			double viewportCenter = viewportHeight / 2;

			double topBoundary = viewportHeight * 0.25;
			double bottomBoundary = viewportHeight * 0.75;
			double targetPoint = viewportCenter;

			bool isAtTop = window.AlertScroller.VerticalOffset <= 5;
			bool isAtBottom = window.AlertScroller.ScrollableHeight == 0 || window.AlertScroller.VerticalOffset >= window.AlertScroller.ScrollableHeight - 5;

			if (isAtTop) topBoundary = -50;
			if (isAtBottom)
			{
				bottomBoundary = viewportHeight + 150;
				targetPoint = viewportHeight;
			}

			var rtbList = new System.Collections.Generic.List<RichTextBox>();
			var btnList = new System.Collections.Generic.List<FrameworkElement>();

			void FindTargets(DependencyObject parent)
			{
				if (parent == null) return;
				int count = VisualTreeHelper.GetChildrenCount(parent);
				for (int i = 0; i < count; i++)
				{
					var child = VisualTreeHelper.GetChild(parent, i);
					if (child is RichTextBox rtb) rtbList.Add(rtb);
					else if (child is FrameworkElement fe && fe.Name != null && fe.Name.StartsWith("ClearA") && fe.IsVisible) btnList.Add(fe);
					FindTargets(child);
				}
			}

			FindTargets(window.AlertScroller);

			foreach (var btn in btnList)
			{
				try
				{
					GeneralTransform transform = btn.TransformToAncestor(window.AlertScroller);
					Rect rect = transform.TransformBounds(new Rect(0, 0, btn.ActualWidth, btn.ActualHeight));
					double itemCenter = rect.Top + (rect.Height / 2);

					if (itemCenter > lowestY && itemCenter < viewportHeight + 50)
					{
						lowestY = itemCenter;
						fallbackLowestItem = btn;
					}

					if (itemCenter >= topBoundary && itemCenter <= bottomBoundary)
					{
						double distance = Math.Abs(itemCenter - targetPoint);
						if (distance < minDistance - 0.1 || (Math.Abs(distance - minDistance) <= 0.1 && rect.Top > bestTop))
						{
							minDistance = distance;
							bestItem = btn;
							bestTop = rect.Top;
						}
					}
				}
				catch { }
			}

			var processedLinks = new System.Collections.Generic.HashSet<System.Windows.Documents.Hyperlink>();
			foreach (var rtb in rtbList)
			{
				if (rtb.Document == null) continue;
				try
				{
					GeneralTransform transform = rtb.TransformToAncestor(window.AlertScroller);
					var pointer = rtb.Document.ContentStart;

					while (pointer != null)
					{
						if (pointer.GetPointerContext(System.Windows.Documents.LogicalDirection.Forward) == System.Windows.Documents.TextPointerContext.ElementStart)
						{
							if (pointer.GetAdjacentElement(System.Windows.Documents.LogicalDirection.Forward) is System.Windows.Documents.Hyperlink hl)
							{
								if (processedLinks.Add(hl))
								{
									try
									{
										Rect startRect = hl.ContentStart.GetCharacterRect(System.Windows.Documents.LogicalDirection.Forward);
										if (startRect != Rect.Empty)
										{
											Rect rect = transform.TransformBounds(startRect);
											double itemCenter = rect.Top + (rect.Height / 2);

											if (itemCenter > lowestY && itemCenter < viewportHeight + 50)
											{
												lowestY = itemCenter;
												fallbackLowestItem = hl;
											}

											if (itemCenter >= topBoundary && itemCenter <= bottomBoundary)
											{
												double distance = Math.Abs(itemCenter - targetPoint);
												if (distance < minDistance - 0.1 || (Math.Abs(distance - minDistance) <= 0.1 && rect.Top > bestTop))
												{
													minDistance = distance;
													bestItem = hl;
													bestTop = rect.Top;
												}
											}
										}
									}
									catch { }
								}
							}
						}
						pointer = pointer.GetNextContextPosition(System.Windows.Documents.LogicalDirection.Forward);
					}
				}
				catch { }
			}

			if (bestItem == null && isAtBottom && fallbackLowestItem != null)
			{
				bestItem = fallbackLowestItem;
			}

			if (bestItem != _activeAlertTarget)
			{
				SetPseudoFocus(_activeAlertTarget, false);
				_activeAlertTarget = bestItem;
				SetPseudoFocus(_activeAlertTarget, true);
			}

			if (Keyboard.FocusedElement != window.AlertScroller)
			{
				window.AlertScroller.Focus();
			}
		}
	}
}