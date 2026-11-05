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
using System.Windows.Media.Animation;

namespace SkyPeek
{
	[System.Runtime.Versioning.SupportedOSPlatform("windows10.0.19041.0")]
	public partial class HourlyViewControl : UserControl
	{
		private bool _isAnimating = false;

		private void HourlyItemsControl_RequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
		{
			e.Handled = true;
		}

		private void HourlyAgendaItemsControl_RequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
		{
			e.Handled = true;
		}

		protected override void OnPreviewKeyDown(KeyEventArgs e)
		{
			if (HourlyItemsControl.IsKeyboardFocusWithin)
			{
				if (e.Key == Key.Left || e.Key == Key.Right)
				{
					e.Handled = true;
					double step = 240.0;
					double current = HourlyScroller.HorizontalOffset;
					double targetOffset = e.Key == Key.Left
						? Math.Round((current - step) / step) * step
						: Math.Round((current + step) / step) * step;

					HourlyScroller.SmoothScrollTo(targetOffset);
				}
				else if (e.Key == Key.Down)
				{
					e.Handled = true;
					if (Window.GetWindow(this) is SecondaryViews parentWindow && parentWindow.FindName("TabHourly") is UIElement tab)
					{
						tab.Focus();
					}
				}
				else if (e.Key == Key.Up)
				{
					e.Handled = true;
					if (!_isAnimating) ToggleView(true);
				}
			}
			else if (HourlyAgendaItemsControl.IsKeyboardFocusWithin)
			{
				if (e.Key == Key.Left || e.Key == Key.Right)
				{
					e.Handled = true;
					double current = HourlyAgendaScroller.VerticalOffset;
					double step = 90.0;

					// Left acts as "Up" the list, Right acts as "Down" the list
					double targetOffset = e.Key == Key.Left
						? Math.Round((current - step) / step) * step
						: Math.Round((current + step) / step) * step;

					HourlyAgendaScroller.SmoothScrollTo(targetOffset);
				}
				else if (e.Key == Key.Down)
				{
					e.Handled = true;
					if (Window.GetWindow(this) is SecondaryViews parentWindow && parentWindow.FindName("TabHourly") is UIElement tab)
					{
						tab.Focus();
					}
				}
				else if (e.Key == Key.Up)
				{
					e.Handled = true;
					if (!_isAnimating) ToggleView(false);
				}
			}
			base.OnPreviewKeyDown(e);
		}

		public HourlyViewControl()
		{
			InitializeComponent();
			this.Loaded += HourlyViewControl_Loaded;
		}

		private SecondaryViews? GetParentSlideout() => Window.GetWindow(this) as SecondaryViews;

		private void HourlyViewControl_Loaded(object sender, RoutedEventArgs e)
		{
			// Sync initial visibility with the ViewModel preference when the tab is clicked/loaded
			if (AppConfig.Current.PreferHourlyAgendaView)
			{
				HourlyViewGrid.Visibility = Visibility.Hidden;
				HourlyAgendaView.Visibility = Visibility.Visible;
				HourlyAgendaView.Opacity = 1.0;
				HourlyAgendaIconBorder.Visibility = Visibility.Collapsed;
				HourlyGraphIconBorder.Visibility = Visibility.Visible;
				HourlyGraphIconBorder.Opacity = 1.0;
			}
			else
			{
				HourlyAgendaView.Visibility = Visibility.Hidden;
				HourlyViewGrid.Visibility = Visibility.Visible;
				HourlyViewGrid.Opacity = 1.0;
				HourlyGraphIconBorder.Visibility = Visibility.Collapsed;
				HourlyAgendaIconBorder.Visibility = Visibility.Visible;
				HourlyAgendaIconBorder.Opacity = 1.0;
			}
		}

		private void ToggleView(bool toAgenda)
		{
			_isAnimating = true;

			// Update the ViewModel boolean to remember this state
			AppConfig.Current.PreferHourlyAgendaView = toAgenda;
			System.Threading.Tasks.Task.Run(() => SettingsManager.Save());

			// Capture whether we need to pass keyboard focus to the new view during the flip
			bool retainFocus = HourlyItemsControl.IsKeyboardFocusWithin || HourlyAgendaItemsControl.IsKeyboardFocusWithin;

			// Target the inner grids instead of the parent wrapper
			var viewOut = toAgenda ? HourlyViewGrid : HourlyAgendaView;
			var viewIn = toAgenda ? HourlyAgendaView : HourlyViewGrid;

			var iconOut = toAgenda ? HourlyAgendaIconBorder : HourlyGraphIconBorder;
			var iconIn = toAgenda ? HourlyGraphIconBorder : HourlyAgendaIconBorder;

			var fadeOut = new DoubleAnimation(1.0, 0.0, new Duration(TimeSpan.FromMilliseconds(150)));
			var fadeIn = new DoubleAnimation(0.0, 1.0, new Duration(TimeSpan.FromMilliseconds(150)));

			iconOut.BeginAnimation(UIElement.OpacityProperty, fadeOut);
			iconIn.Visibility = Visibility.Visible;
			iconIn.BeginAnimation(UIElement.OpacityProperty, fadeIn);

			viewOut.FlipAndFade(1.0, 0.0, 1.0, 0.0, 150, EasingMode.EaseIn, () =>
			{
				viewOut.ClearUIAnimations();
				viewOut.Opacity = 0.0;
				viewOut.Visibility = Visibility.Hidden;

				iconOut.BeginAnimation(UIElement.OpacityProperty, null);
				iconOut.Opacity = 0.0;
				iconOut.Visibility = Visibility.Collapsed;

				viewIn.Visibility = Visibility.Visible;
				viewIn.Opacity = 0.0;

				viewIn.UpdateLayout();

				Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, new Action(() =>
				{
					// Re-assert focus on the new view so the highlight lines stay active
					if (retainFocus)
					{
						if (toAgenda) HourlyAgendaItemsControl.Focus();
						else HourlyItemsControl.Focus();
					}

					viewIn.FlipAndFade(0.0, 1.0, 0.0, 1.0, 150, EasingMode.EaseOut, () =>
					{
						viewIn.ClearUIAnimations();
						viewIn.Opacity = 1.0;

						iconIn.BeginAnimation(UIElement.OpacityProperty, null);
						iconIn.Opacity = 1.0;

						_isAnimating = false;
					});
				}));
			});
		}

		internal void HourlyAgendaIcon_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			if (_isAnimating || HourlyAgendaView.Visibility == Visibility.Visible) return;
			ToggleView(true);
		}

		internal void HourlyGraphIcon_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			if (_isAnimating || HourlyViewGrid.Visibility == Visibility.Visible) return;
			ToggleView(false);
		}

		internal void HourlyScroller_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			if (e.OriginalSource is DependencyObject src && (src is ScrollBar || src.FindVisualParent<ScrollBar>() != null)) return;
			HourlyAgendaIcon_MouseLeftButtonUp(sender, e);
		}

		internal void HourlyAgendaScroller_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			if (e.OriginalSource is DependencyObject src && (src is ScrollBar || src.FindVisualParent<ScrollBar>() != null)) return;
			HourlyGraphIcon_MouseLeftButtonUp(sender, e);
		}

		internal void HourlyAgendaScroller_ScrollChanged(object sender, ScrollChangedEventArgs e)
		{
			HourlyAgendaScrollBar.Maximum = HourlyAgendaScroller.ScrollableHeight;
			HourlyAgendaScrollBar.ViewportSize = HourlyAgendaScroller.ViewportHeight;
			HourlyAgendaScrollBar.Value = e.VerticalOffset;
		}

		internal void HourlyAgendaScrollBar_Scroll(object sender, ScrollEventArgs e)
		{
			HourlyAgendaScroller.ScrollToVerticalOffset(e.NewValue);
		}

		// Kept forwarded events pointing to Slideout for map/marquee sizing
		internal void HourlyView_ScrollChanged(object sender, ScrollChangedEventArgs e)
		{
			GetParentSlideout()?.HourlyView_ScrollChanged(sender, e);
		}

		internal void HourlyView_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			GetParentSlideout()?.HourlyView_SizeChanged(sender, e);
		}

		internal void ScrollBar_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			GetParentSlideout()?.ScrollBar_PreviewMouseLeftButtonUp(sender, e);
		}

		internal void LocationMarquee_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			GetParentSlideout()?.LocationMarquee_SizeChanged(sender, e);
		}

		internal void ScrollBar_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			GetParentSlideout()?.ScrollBar_PreviewMouseLeftButtonDown(sender, e);
		}
	}
}