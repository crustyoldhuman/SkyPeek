using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace SkyPeek
{
	public static class GraphSyncService
	{
		public static void SyncHourlyDot(ScrollViewer scroller, Polyline graphLine, Shape activeDot, Canvas scaleCanvas, ref bool isScaleVisible, System.Collections.Generic.List<HourlyForecast>? hourlyData)
		{
			if (graphLine.Points == null || graphLine.Points.Count == 0 || hourlyData == null || hourlyData.Count == 0) return;

			double itemWidth = 240.0;
			int currentIndex = (int)Math.Round(scroller.HorizontalOffset / itemWidth);

			if (currentIndex < 0) currentIndex = 0;
			if (currentIndex >= graphLine.Points.Count) currentIndex = graphLine.Points.Count - 1;
			if (currentIndex >= hourlyData.Count) currentIndex = hourlyData.Count - 1;

			if (activeDot.RenderTransform is TranslateTransform dotTransform)
			{
				int dotIndex = currentIndex + 1;
				if (dotIndex >= graphLine.Points.Count) dotIndex = graphLine.Points.Count - 1;
				dotTransform.X = graphLine.Points[dotIndex].X - 6;
				dotTransform.Y = graphLine.Points[dotIndex].Y - 6;
			}

			var activeBrush = new SolidColorBrush(GraphService.GetTemperatureColor(Convert.ToDouble(hourlyData[currentIndex].Temp)));
			activeBrush.Freeze();
			activeDot.Fill = activeBrush;

			bool shouldBeVisible = (currentIndex == 0);
			if (shouldBeVisible != isScaleVisible)
			{
				isScaleVisible = shouldBeVisible;
				scaleCanvas.Fade(shouldBeVisible ? 1.0 : 0.0, 1000, System.Windows.Media.Animation.EasingMode.EaseInOut);
			}
		}

		public static void SyncDailyDots(ScrollViewer scroller, Polyline highLine, Polyline lowLine, Shape highDot, Shape lowDot, Canvas scaleCanvas, ref bool isScaleVisible, System.Collections.Generic.List<DailyForecast>? dailyData)
		{
			if (highLine.Points == null || highLine.Points.Count == 0 || dailyData == null || dailyData.Count == 0) return;

			double itemWidth = 240.0;
			int currentIndex = (int)Math.Round(scroller.HorizontalOffset / itemWidth);

			if (currentIndex < 0) currentIndex = 0;
			if (currentIndex >= highLine.Points.Count) currentIndex = highLine.Points.Count - 1;
			if (currentIndex >= dailyData.Count) currentIndex = dailyData.Count - 1;

			int dotIndex = currentIndex + 1;
			if (dotIndex >= highLine.Points.Count) dotIndex = highLine.Points.Count - 1;

			if (highDot.RenderTransform is TranslateTransform hTransform)
			{
				hTransform.X = highLine.Points[dotIndex].X - 6;
				hTransform.Y = highLine.Points[dotIndex].Y - 6;
			}
			if (lowDot.RenderTransform is TranslateTransform lTransform)
			{
				lTransform.X = lowLine.Points[dotIndex].X - 6;
				lTransform.Y = lowLine.Points[dotIndex].Y - 6;
			}

			var hBrush = new SolidColorBrush(GraphService.GetTemperatureColor(Convert.ToDouble(dailyData[currentIndex].High)));
			hBrush.Freeze();
			highDot.Fill = hBrush;

			var lBrush = new SolidColorBrush(GraphService.GetTemperatureColor(Convert.ToDouble(dailyData[currentIndex].Low)));
			lBrush.Freeze();
			lowDot.Fill = lBrush;

			bool shouldBeVisible = (currentIndex == 0);
			if (shouldBeVisible != isScaleVisible)
			{
				isScaleVisible = shouldBeVisible;
				scaleCanvas.Fade(shouldBeVisible ? 1.0 : 0.0, 1000, EasingMode.EaseInOut);
			}
		}

		public static void AlignScrollGrid(SizeChangedEventArgs e, Grid scrollGrid)
		{
			double itemWidth = 240.0;
			double centerOffset = (e.NewSize.Width - itemWidth) / 2.0;
			if (centerOffset < 0) centerOffset = 0;
			scrollGrid.Margin = new Thickness(centerOffset, 0, centerOffset, 0);
		}
	}
}