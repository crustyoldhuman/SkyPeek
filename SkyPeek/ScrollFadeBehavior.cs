using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SkyPeek
{
	public static class ScrollFadeBehavior
	{
		public static readonly DependencyProperty IsEnabledProperty =
			DependencyProperty.RegisterAttached("IsEnabled", typeof(bool), typeof(ScrollFadeBehavior), new PropertyMetadata(false, OnIsEnabledChanged));

		// New property for customizing the scroll distance required to reach full fade
		public static readonly DependencyProperty FadeDistanceProperty =
			DependencyProperty.RegisterAttached("FadeDistance", typeof(double), typeof(ScrollFadeBehavior), new PropertyMetadata(20.0, OnFadeParametersChanged));

		// New property for customizing the physical height/thickness of the fade gradient
		public static readonly DependencyProperty FadeHeightProperty =
			DependencyProperty.RegisterAttached("FadeHeight", typeof(double), typeof(ScrollFadeBehavior), new PropertyMetadata(15.0, OnFadeParametersChanged));

		public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);
		public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

		public static void SetFadeDistance(DependencyObject element, double value) => element.SetValue(FadeDistanceProperty, value);
		public static double GetFadeDistance(DependencyObject element) => (double)element.GetValue(FadeDistanceProperty);

		public static void SetFadeHeight(DependencyObject element, double value) => element.SetValue(FadeHeightProperty, value);
		public static double GetFadeHeight(DependencyObject element) => (double)element.GetValue(FadeHeightProperty);

		private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			if (d is ScrollViewer sv)
			{
				if ((bool)e.NewValue)
				{
					sv.Loaded += Sv_Loaded;
					sv.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(Sv_ScrollChanged), handledEventsToo: true);
					sv.SizeChanged += Sv_SizeChanged;
				}
				else
				{
					sv.Loaded -= Sv_Loaded;
					sv.RemoveHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(Sv_ScrollChanged));
					sv.SizeChanged -= Sv_SizeChanged;
					sv.OpacityMask = null;
				}
			}
		}

		private static void OnFadeParametersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			if (d is ScrollViewer sv && GetIsEnabled(sv))
			{
				UpdateFade(sv);
			}
		}

		private static void Sv_Loaded(object sender, RoutedEventArgs e) => UpdateFade((ScrollViewer)sender);
		private static void Sv_ScrollChanged(object sender, ScrollChangedEventArgs e) => UpdateFade((ScrollViewer)sender);
		private static void Sv_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateFade((ScrollViewer)sender);

		private static void UpdateFade(ScrollViewer sv)
		{
			if (sv.ActualHeight == 0 || sv.ActualWidth == 0 || sv.ScrollableHeight <= 0 || sv.ViewportHeight <= 0)
			{
				if (sv.OpacityMask != null) sv.OpacityMask = null;
				return;
			}

			double fadeDistance = GetFadeDistance(sv);

			double topRatio = System.Math.Min(System.Math.Max(sv.VerticalOffset, 0) / fadeDistance, 1.0);
			byte topAlpha = (byte)((1.0 - topRatio) * 255);
			Color topColor = Color.FromArgb(topAlpha, 0, 0, 0);

			double distanceFromBottom = System.Math.Max(sv.ScrollableHeight - sv.VerticalOffset, 0);
			double bottomRatio = System.Math.Min(distanceFromBottom / fadeDistance, 1.0);
			byte bottomAlpha = (byte)((1.0 - bottomRatio) * 255);
			Color bottomColor = Color.FromArgb(bottomAlpha, 0, 0, 0);

			if (sv.OpacityMask is LinearGradientBrush existingBrush && existingBrush.GradientStops.Count == 4)
			{
				double targetRelativeHeight = System.Math.Min(GetFadeHeight(sv) / sv.ViewportHeight, 0.49);
				double currentRelativeHeight = existingBrush.GradientStops[1].Offset;

				if (existingBrush.GradientStops[0].Color == topColor &&
					existingBrush.GradientStops[3].Color == bottomColor &&
					System.Math.Abs(currentRelativeHeight - targetRelativeHeight) < 0.001)
				{
					return;
				}
			}

			double fadeHeight = GetFadeHeight(sv);
			double relativeFadeHeight = System.Math.Min(fadeHeight / sv.ViewportHeight, 0.49);

			var brush = new LinearGradientBrush
			{
				StartPoint = new Point(0, 0),
				EndPoint = new Point(0, 1)
			};
			brush.GradientStops.Add(new GradientStop(topColor, 0.0));
			brush.GradientStops.Add(new GradientStop(Colors.Black, relativeFadeHeight));
			brush.GradientStops.Add(new GradientStop(Colors.Black, 1.0 - relativeFadeHeight));
			brush.GradientStops.Add(new GradientStop(bottomColor, 1.0));

			brush.Freeze();
			sv.OpacityMask = brush;
		}
	}
}