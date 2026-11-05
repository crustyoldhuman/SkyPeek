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
	public partial class TenDayViewControl : UserControl
	{
		private bool _isAnimating = false;

		private void TenDayItemsControl_RequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
		{
			e.Handled = true;
		}

		private void AgendaItemsControl_RequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
		{
			e.Handled = true;
		}

		protected override void OnPreviewKeyDown(KeyEventArgs e)
		{
			if (TenDayItemsControl.IsKeyboardFocusWithin)
			{
				if (e.Key == Key.Left || e.Key == Key.Right)
				{
					e.Handled = true;
					double step = 240.0;
					double current = DailyScroller.HorizontalOffset;
					double targetOffset = e.Key == Key.Left
						? Math.Round((current - step) / step) * step
						: Math.Round((current + step) / step) * step;

					DailyScroller.SmoothScrollTo(targetOffset);
				}
				else if (e.Key == Key.Down)
				{
					e.Handled = true;
					if (Window.GetWindow(this) is SecondaryViews parentWindow && parentWindow.FindName("Tab10Day") is UIElement tab)
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
			else if (AgendaItemsControl.IsKeyboardFocusWithin)
			{
				if (e.Key == Key.Left || e.Key == Key.Right)
				{
					e.Handled = true;
					double current = AgendaScroller.VerticalOffset;
					double step = 80.0;

					// Left acts as "Up" the list, Right acts as "Down" the list
					double targetOffset = e.Key == Key.Left
						? Math.Round((current - step) / step) * step
						: Math.Round((current + step) / step) * step;

					AgendaScroller.SmoothScrollTo(targetOffset);
				}
				else if (e.Key == Key.Down)
				{
					e.Handled = true;
					if (Window.GetWindow(this) is SecondaryViews parentWindow && parentWindow.FindName("Tab10Day") is UIElement tab)
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

		public TenDayViewControl()
		{
			InitializeComponent();
			this.Loaded += TenDayViewControl_Loaded;
		}

		private SecondaryViews? GetParentSlideout() => Window.GetWindow(this) as SecondaryViews;

		private void TenDayViewControl_Loaded(object sender, RoutedEventArgs e)
		{
			if (AppConfig.Current.PreferTenDayAgenda)
			{
				TenDayViewGrid.Visibility = Visibility.Hidden;
				TenDayAgendaView.Visibility = Visibility.Visible;
				TenDayAgendaView.Opacity = 1.0;
				TenDayAgendaIconBorder.Visibility = Visibility.Collapsed;
				TenDayGraphIconBorder.Visibility = Visibility.Visible;
				TenDayGraphIconBorder.Opacity = 1.0;
			}
			else
			{
				TenDayAgendaView.Visibility = Visibility.Hidden;
				TenDayViewGrid.Visibility = Visibility.Visible;
				TenDayViewGrid.Opacity = 1.0;
				TenDayGraphIconBorder.Visibility = Visibility.Collapsed;
				TenDayAgendaIconBorder.Visibility = Visibility.Visible;
				TenDayAgendaIconBorder.Opacity = 1.0;
			}
		}

		private void ToggleView(bool toAgenda)
		{
			_isAnimating = true;

			AppConfig.Current.PreferTenDayAgenda = toAgenda;
			System.Threading.Tasks.Task.Run(() => SettingsManager.Save());

			bool retainFocus = TenDayItemsControl.IsKeyboardFocusWithin || AgendaItemsControl.IsKeyboardFocusWithin;

			var viewOut = toAgenda ? TenDayViewGrid : TenDayAgendaView;
			var viewIn = toAgenda ? TenDayAgendaView : TenDayViewGrid;

			var iconOut = toAgenda ? TenDayAgendaIconBorder : TenDayGraphIconBorder;
			var iconIn = toAgenda ? TenDayGraphIconBorder : TenDayAgendaIconBorder;

			var fadeOut = new DoubleAnimation(1.0, 0.0, new Duration(TimeSpan.FromMilliseconds(100)));
			var fadeIn = new DoubleAnimation(0.0, 1.0, new Duration(TimeSpan.FromMilliseconds(100)));

			iconOut.BeginAnimation(UIElement.OpacityProperty, fadeOut);
			iconIn.Visibility = Visibility.Visible;
			iconIn.BeginAnimation(UIElement.OpacityProperty, fadeIn);

			viewOut.FlipAndFade(1.0, 0.0, 1.0, 0.0, 100, EasingMode.EaseIn, () =>
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
					if (retainFocus)
					{
						if (toAgenda) AgendaItemsControl.Focus();
						else TenDayItemsControl.Focus();
					}

					viewIn.FlipAndFade(0.0, 1.0, 0.0, 1.0, 100, EasingMode.EaseOut, () =>
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

		internal void AgendaIcon_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			if (_isAnimating || TenDayAgendaView.Visibility == Visibility.Visible) return;
			AppConfig.Current.PreferTenDayAgenda = true;
			ToggleView(true);
		}

		internal void GraphIcon_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			if (_isAnimating || TenDayViewGrid.Visibility == Visibility.Visible) return;
			AppConfig.Current.PreferTenDayAgenda = false;
			ToggleView(false);
		}

		internal void DailyScroller_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			if (e.OriginalSource is DependencyObject src && (src is ScrollBar || src.FindVisualParent<ScrollBar>() != null)) return;
			AgendaIcon_MouseLeftButtonUp(sender, e);
		}

		internal void AgendaScroller_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			if (e.OriginalSource is DependencyObject src && (src is ScrollBar || src.FindVisualParent<ScrollBar>() != null)) return;
			GraphIcon_MouseLeftButtonUp(sender, e);
		}

		internal void DailyView_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			double itemWidth = 240.0;
			double centerOffset = (e.NewSize.Width - itemWidth) / 2.0;
			if (centerOffset < 0) centerOffset = 0;
			DailyScrollGrid.Margin = new Thickness(centerOffset, 0, centerOffset, 0);
		}

		internal void AgendaScroller_ScrollChanged(object sender, ScrollChangedEventArgs e)
		{
			AgendaScrollBar.Maximum = AgendaScroller.ScrollableHeight;
			AgendaScrollBar.ViewportSize = AgendaScroller.ViewportHeight;
			AgendaScrollBar.Value = e.VerticalOffset;
		}

		internal void AgendaScrollBar_Scroll(object sender, ScrollEventArgs e)
		{
			AgendaScroller.ScrollToVerticalOffset(e.NewValue);
		}

		internal void DailyView_ScrollChanged(object sender, ScrollChangedEventArgs e)
		{
			GetParentSlideout()?.DailyView_ScrollChanged(sender, e);
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