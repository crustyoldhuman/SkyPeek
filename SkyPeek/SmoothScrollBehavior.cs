using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SkyPeek
{
	public static class SmoothScrollBehavior
	{
		public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
			"IsEnabled", typeof(bool), typeof(SmoothScrollBehavior), new PropertyMetadata(false, OnIsEnabledChanged));

		public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);
		public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

		public static readonly DependencyProperty ScrollStepProperty = DependencyProperty.RegisterAttached(
			"ScrollStep", typeof(double), typeof(SmoothScrollBehavior), new PropertyMetadata(0.0));

		public static void SetScrollStep(DependencyObject element, double value) => element.SetValue(ScrollStepProperty, value);
		public static double GetScrollStep(DependencyObject element) => (double)element.GetValue(ScrollStepProperty);

		private static readonly DependencyProperty TargetOffsetProperty = DependencyProperty.RegisterAttached("TargetOffset", typeof(double), typeof(SmoothScrollBehavior), new PropertyMetadata(0.0));
		private static readonly DependencyProperty CurrentOffsetProperty = DependencyProperty.RegisterAttached("CurrentOffset", typeof(double), typeof(SmoothScrollBehavior), new PropertyMetadata(0.0));
		private static readonly DependencyProperty IsScrollingProperty = DependencyProperty.RegisterAttached("IsScrolling", typeof(bool), typeof(SmoothScrollBehavior), new PropertyMetadata(false));
		private static readonly DependencyProperty RenderingHandlerProperty = DependencyProperty.RegisterAttached("RenderingHandler", typeof(EventHandler), typeof(SmoothScrollBehavior), new PropertyMetadata(null));
		private static readonly DependencyProperty LastRenderTimeProperty = DependencyProperty.RegisterAttached("LastRenderTime", typeof(TimeSpan), typeof(SmoothScrollBehavior), new PropertyMetadata(TimeSpan.Zero));

		public static void StopSmoothScrolling(this ScrollViewer scroller)
		{
			if (scroller.GetValue(RenderingHandlerProperty) is EventHandler handler)
			{
				CompositionTarget.Rendering -= handler;
				scroller.SetValue(RenderingHandlerProperty, null);
			}
			scroller.SetValue(IsScrollingProperty, false);
			scroller.SetValue(LastRenderTimeProperty, TimeSpan.Zero);
		}

		public static void SmoothScrollBy(this ScrollViewer scroller, double offsetChange)
		{
			bool isHorizontal = scroller.HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled && scroller.VerticalScrollBarVisibility == ScrollBarVisibility.Disabled;
			double currentOffset = isHorizontal ? scroller.HorizontalOffset : scroller.VerticalOffset;
			double targetOffset = (bool)scroller.GetValue(IsScrollingProperty) ? (double)scroller.GetValue(TargetOffsetProperty) : currentOffset;
			scroller.SmoothScrollTo(targetOffset + offsetChange);
		}

		private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			if (d is ScrollViewer scroller)
			{
				if ((bool)e.NewValue)
				{
					scroller.PreviewMouseWheel += Scroller_PreviewMouseWheel;
					scroller.PreviewMouseLeftButtonDown += Scroller_PreviewMouseLeftButtonDown;
					scroller.Unloaded += Scroller_Unloaded;
					scroller.IsVisibleChanged += Scroller_IsVisibleChanged;
				}
				else
				{
					scroller.PreviewMouseWheel -= Scroller_PreviewMouseWheel;
					scroller.PreviewMouseLeftButtonDown -= Scroller_PreviewMouseLeftButtonDown;
					scroller.Unloaded -= Scroller_Unloaded;
					scroller.IsVisibleChanged -= Scroller_IsVisibleChanged;
					scroller.StopSmoothScrolling();
				}
			}
		}

		private static void Scroller_Unloaded(object sender, RoutedEventArgs e)
		{
			if (sender is ScrollViewer scroller)
			{
				scroller.StopSmoothScrolling();
			}
		}

		private static void Scroller_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
		{
			if (sender is ScrollViewer scroller && !(bool)e.NewValue)
			{
				scroller.StopSmoothScrolling();
			}
		}

		public static void SmoothScrollTo(this ScrollViewer scroller, double newTarget)
		{
			bool isHorizontal = scroller.HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled && scroller.VerticalScrollBarVisibility == ScrollBarVisibility.Disabled;
			double maxScroll = isHorizontal ? scroller.ScrollableWidth : scroller.ScrollableHeight;

			if (newTarget < 0) newTarget = 0;
			if (newTarget > maxScroll) newTarget = maxScroll;

			scroller.SetValue(TargetOffsetProperty, newTarget);

			if (AppConfig.Current.DisableAnimations)
			{
				scroller.SetValue(CurrentOffsetProperty, newTarget);
				if (isHorizontal) scroller.ScrollToHorizontalOffset(newTarget);
				else scroller.ScrollToVerticalOffset(newTarget);
				return;
			}

			if (!(bool)scroller.GetValue(IsScrollingProperty))
			{
				scroller.SetValue(IsScrollingProperty, true);
				scroller.SetValue(CurrentOffsetProperty, isHorizontal ? scroller.HorizontalOffset : scroller.VerticalOffset);
				scroller.SetValue(LastRenderTimeProperty, TimeSpan.Zero);

				void RenderingHandler(object? s, EventArgs args)
				{
					if (args is RenderingEventArgs renderArgs)
					{
						TimeSpan lastTime = (TimeSpan)scroller.GetValue(LastRenderTimeProperty);
						if (lastTime == TimeSpan.Zero)
						{
							scroller.SetValue(LastRenderTimeProperty, renderArgs.RenderingTime);
							return;
						}

						double deltaSeconds = (renderArgs.RenderingTime - lastTime).TotalSeconds;
						scroller.SetValue(LastRenderTimeProperty, renderArgs.RenderingTime);

						if (deltaSeconds > 0.05 || deltaSeconds <= 0)
						{
							deltaSeconds = 0.016;
						}

						double friction = 1.0 - Math.Pow(0.001, deltaSeconds);
						double current = (double)scroller.GetValue(CurrentOffsetProperty);
						double target = (double)scroller.GetValue(TargetOffsetProperty);

						current += (target - current) * friction;
						scroller.SetValue(CurrentOffsetProperty, current);

						if (isHorizontal) scroller.ScrollToHorizontalOffset(current);
						else scroller.ScrollToVerticalOffset(current);

						if (Math.Abs(target - current) < 0.5)
						{
							if (isHorizontal) scroller.ScrollToHorizontalOffset(target);
							else scroller.ScrollToVerticalOffset(target);

							scroller.StopSmoothScrolling();
						}
					}
				}

				EventHandler handler = RenderingHandler;
				scroller.SetValue(RenderingHandlerProperty, handler);
				CompositionTarget.Rendering += handler;
			}
		}

		private static void Scroller_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
		{
			if (sender is ScrollViewer scroller)
			{
				double step = GetScrollStep(scroller);
				if (step <= 0) return;

				bool isHorizontal = scroller.HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled && scroller.VerticalScrollBarVisibility == ScrollBarVisibility.Disabled;
				bool isScrolling = (bool)scroller.GetValue(IsScrollingProperty);
				double targetOffset = isScrolling ? (double)scroller.GetValue(TargetOffsetProperty) : (isHorizontal ? scroller.HorizontalOffset : scroller.VerticalOffset);

				double currentIndex = targetOffset / step;
				double ticks = -e.Delta / 120.0;
				int targetIndex = (int)Math.Round(currentIndex + ticks);

				scroller.SmoothScrollTo(targetIndex * step);
				e.Handled = true;
			}
		}

		private static void Scroller_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			if (sender is ScrollViewer scroller && (bool)scroller.GetValue(IsScrollingProperty))
			{
				scroller.StopSmoothScrolling();
			}
		}
	}
}