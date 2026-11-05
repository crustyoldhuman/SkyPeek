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
using System.Windows.Media;
using System.Windows.Shapes;

namespace SkyPeek
{
	[System.Runtime.Versioning.SupportedOSPlatform("windows10.0.19041.0")]
	public static class GraphService
	{
		public static void DrawHourlyGraph(Canvas graphCanvas, Canvas scaleCanvas, Polyline tempGraphLine, FrameworkElement activeTempDot, List<HourlyForecast> hourlyData)
		{
			Polygon? tempRibbonPolygon = null;
			foreach (UIElement child in graphCanvas.Children)
			{
				if (child is Polygon p && p.Name == "TempRibbonPolygon")
				{
					tempRibbonPolygon = p;
					break;
				}
			}

			List<UIElement> oldElements = [];
			foreach (UIElement child in graphCanvas.Children)
			{
				if (child != tempGraphLine && child != activeTempDot && child != tempRibbonPolygon)
					oldElements.Add(child);
			}
			foreach (var child in oldElements) graphCanvas.Children.Remove(child);

			PointCollection points = [];
			double itemWidth = 240.0;
			double startX = 35.0;
			double graphHeight = 150.0;

			if (hourlyData.Count == 0) return;

			double minTemp = hourlyData.Min(h => Convert.ToDouble(h.Temp));
			double maxTemp = hourlyData.Max(h => Convert.ToDouble(h.Temp));
			double range = (maxTemp - minTemp == 0) ? 1 : (maxTemp - minTemp);

			double paddedRange = range * 1.2;
			double adjustedMin = minTemp - (range * 0.1);

			for (int i = 0; i < hourlyData.Count; i++)
			{
				double t = Convert.ToDouble(hourlyData[i].Temp);
				double x = startX + (i * itemWidth);
				double y = graphHeight - ((t - adjustedMin) / paddedRange) * graphHeight;
				points.Add(new Point(x, y));
			}

			double extendedX = startX + (hourlyData.Count * itemWidth);
			double preExtendedX = startX - itemWidth;
			points.Add(new Point(extendedX, points.Last().Y));
			if (points.Count > 0) points.Insert(0, new Point(preExtendedX, points.First().Y));

			LinearGradientBrush lineBrush = new() { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
			LinearGradientBrush ribbonBrush = new() { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };

			if (hourlyData.Count > 0)
			{
				double firstTemp = Convert.ToDouble(hourlyData.First().Temp);
				Color firstColor = GetTemperatureColor(firstTemp);
				lineBrush.GradientStops.Add(new GradientStop(firstColor, 0.0));
				ribbonBrush.GradientStops.Add(new GradientStop(firstColor, 0.0));
			}

			for (int i = 0; i < hourlyData.Count; i++)
			{
				double t = Convert.ToDouble(hourlyData[i].Temp);
				Color c = GetTemperatureColor(t);
				double offset = (i + 1) / (double)(hourlyData.Count + 1);

				lineBrush.GradientStops.Add(new GradientStop(c, offset));
				ribbonBrush.GradientStops.Add(new GradientStop(c, offset));
			}

			if (hourlyData.Count > 0)
			{
				double lastTemp = Convert.ToDouble(hourlyData.Last().Temp);
				Color lastColor = GetTemperatureColor(lastTemp);
				lineBrush.GradientStops.Add(new GradientStop(lastColor, 1.0));
				ribbonBrush.GradientStops.Add(new GradientStop(lastColor, 1.0));
			}

			if (tempRibbonPolygon != null)
			{
				double fadeHeight = graphHeight * 0.50; // Extend downwards 50% of graph height

				PointCollection ribbonPoints = [.. points];
				// Trace the bottom edge exactly parallel to the graph points
				for (int i = points.Count - 1; i >= 0; i--)
				{
					ribbonPoints.Add(new Point(points[i].X, points[i].Y + fadeHeight));
				}

				tempRibbonPolygon.Points = ribbonPoints;
				ribbonBrush.Freeze();
				tempRibbonPolygon.Fill = ribbonBrush;

				DrawingGroup maskDrawingGroup = new();
				RenderOptions.SetEdgeMode(maskDrawingGroup, EdgeMode.Aliased);

				double minX = ribbonPoints.Min(p => p.X);
				double maxX = ribbonPoints.Max(p => p.X);
				double maskMinY = ribbonPoints.Min(p => p.Y);
				double maskMaxY = ribbonPoints.Max(p => p.Y);

				RectangleGeometry anchorGeom = new(new Rect(minX, maskMinY, Math.Max(1, maxX - minX), Math.Max(1, maskMaxY - maskMinY)));
				anchorGeom.Freeze();
				GeometryDrawing anchorDrawing = new(Brushes.Transparent, null, anchorGeom);
				anchorDrawing.Freeze();
				maskDrawingGroup.Children.Add(anchorDrawing);

				double stepX = 4.0;
				for (int i = 0; i < points.Count - 1; i++)
				{
					Point p1 = points[i];
					Point p2 = points[i + 1];

					double dx = p2.X - p1.X;
					double dy = p2.Y - p1.Y;
					int steps = Math.Max(1, (int)Math.Ceiling(Math.Abs(dx) / stepX));

					for (int j = 0; j < steps; j++)
					{
						double x1 = p1.X + (dx * j / steps);
						double x2 = p1.X + (dx * (j + 1) / steps);
						double y1 = p1.Y + (dy * j / steps);
						double y2 = p1.Y + (dy * (j + 1) / steps);

						double bottom1 = Math.Min(graphHeight, y1 + fadeHeight);
						double bottom2 = Math.Min(graphHeight, y2 + fadeHeight);

						double effectiveFadeHeight = Math.Min(fadeHeight, graphHeight - ((y1 + y2) / 2.0));
						effectiveFadeHeight = Math.Max(0.01, effectiveFadeHeight);

						LinearGradientBrush stripBrush = new()
						{
							StartPoint = new Point(0, (y1 + y2) / 2.0),
							EndPoint = new Point(0, ((y1 + y2) / 2.0) + effectiveFadeHeight),
							MappingMode = BrushMappingMode.Absolute
						};
						stripBrush.GradientStops.Add(new GradientStop(Colors.Black, 0.0));
						stripBrush.GradientStops.Add(new GradientStop(Colors.Transparent, 1.0));
						stripBrush.Freeze();

						StreamGeometry stripGeom = new();
						using (StreamGeometryContext ctx = stripGeom.Open())
						{
							ctx.BeginFigure(new Point(x1, y1), true, true);
							ctx.LineTo(new Point(x2, y2), false, false);
							ctx.LineTo(new Point(x2, bottom2), false, false);
							ctx.LineTo(new Point(x1, bottom1), false, false);
						}
						stripGeom.Freeze();

						GeometryDrawing stripDrawing = new(stripBrush, null, stripGeom);
						stripDrawing.Freeze();
						maskDrawingGroup.Children.Add(stripDrawing);
					}
				}

				maskDrawingGroup.Freeze();
				DrawingBrush maskBrush = new(maskDrawingGroup)
				{
					Stretch = Stretch.None,
					AlignmentX = AlignmentX.Left,
					AlignmentY = AlignmentY.Top
				};
				maskBrush.Freeze();
				tempRibbonPolygon.OpacityMask = maskBrush;

				lineBrush.Freeze();
				tempGraphLine.Points = points;
				tempGraphLine.Stroke = lineBrush;

				activeTempDot.Visibility = Visibility.Visible;
				if (activeTempDot is Shape dotShape && hourlyData.Count > 0)
				{
					var dotBrush = new SolidColorBrush(GetTemperatureColor(Convert.ToDouble(hourlyData[0].Temp)));
					dotBrush.Freeze();
					dotShape.Fill = dotBrush;
				}

				double maxY = graphHeight - ((maxTemp - adjustedMin) / paddedRange) * graphHeight;
				double minY = graphHeight - ((minTemp - adjustedMin) / paddedRange) * graphHeight;

				double totalWidth = hourlyData.Count * itemWidth;
				DoubleCollection gridDashArray = [4, 4];
				gridDashArray.Freeze();

				for (int i = 0; i < 5; i++)
				{
					double currentY = minY - ((minY - maxY) / 4.0 * i);
					Line gridLine = new()
					{
						X1 = 0,
						Y1 = currentY,
						X2 = totalWidth,
						Y2 = currentY,
						Opacity = 0.3,
						StrokeThickness = 1,
						StrokeDashArray = gridDashArray,
						Stroke = (Brush)Application.Current.Resources["SecondaryTextBrush"]
					};
					Panel.SetZIndex(gridLine, -1);
					graphCanvas.Children.Add(gridLine);
				}

				scaleCanvas.Children.Clear();
				for (int i = 0; i < 5; i++)
				{
					double currentTemp = minTemp + ((maxTemp - minTemp) / 4.0 * i);
					double currentY = minY - ((minY - maxY) / 4.0 * i);
					TextBlock tickText = new()
					{
						Text = $"{Math.Round(currentTemp)}°",
						FontSize = 11,
						FontWeight = FontWeights.SemiBold,
						Foreground = (Brush)Application.Current.Resources["GraphScaleTextBrush"]
					};
					RenderOptions.SetClearTypeHint(tickText, ClearTypeHint.Enabled);
					Canvas.SetLeft(tickText, 5.0);
					Canvas.SetTop(tickText, currentY - 7);
					scaleCanvas.Children.Add(tickText);
				}
			}
		}
		public static Color GetTemperatureColor(double temp)
		{
			bool isLight = false;
			try
			{
				isLight = Wpf.Ui.Appearance.SystemThemeManager.GetCachedSystemTheme() == Wpf.Ui.Appearance.SystemTheme.Light;
				if (AppConfig.Current.ThemePreference == "Light") isLight = true;
				else if (AppConfig.Current.ThemePreference == "Dark") isLight = false;
			}
			catch { }

			if (isLight)
			{
				Color cPurple = Color.FromRgb(125, 40, 185);
				Color cBlue = Color.FromRgb(15, 125, 215);
				Color cGreen = Color.FromRgb(30, 135, 65);
				Color cYellow = Color.FromRgb(225, 175, 0);
				Color cOrange = Color.FromRgb(210, 100, 10);
				Color cRed = Color.FromRgb(200, 40, 40);

				if (AppConfig.Current.UseCelsius)
				{
					if (temp <= -29) return cPurple;
					if (temp >= 35) return cRed;
					if (temp < 4) return InterpolateColor(cPurple, cBlue, (temp - -29) / 33.0);
					if (temp < 15) return InterpolateColor(cBlue, cGreen, (temp - 4) / 11.0);
					if (temp < 24) return InterpolateColor(cGreen, cYellow, (temp - 15) / 9.0);
					if (temp < 29) return InterpolateColor(cYellow, cOrange, (temp - 24) / 5.0);
					return InterpolateColor(cOrange, cRed, (temp - 29) / 6.0);
				}

				if (temp <= -20) return cPurple;
				if (temp >= 95) return cRed;
				if (temp < 40) return InterpolateColor(cPurple, cBlue, (temp - -20) / 60.0);
				if (temp < 60) return InterpolateColor(cBlue, cGreen, (temp - 40) / 20.0);
				if (temp < 75) return InterpolateColor(cGreen, cYellow, (temp - 60) / 15.0);
				if (temp < 85) return InterpolateColor(cYellow, cOrange, (temp - 75) / 10.0);
				return InterpolateColor(cOrange, cRed, (temp - 85) / 10.0);
			}
			else
			{
				if (AppConfig.Current.UseCelsius)
				{
					if (temp <= -7) return Colors.Purple;
					if (temp >= 33) return Colors.Red;
					if (temp < 4) return InterpolateColor(Colors.Purple, Colors.DodgerBlue, (temp + 7) / 11.0);
					if (temp < 21) return InterpolateColor(Colors.DodgerBlue, Colors.Yellow, (temp - 4) / 17.0);
					if (temp < 28) return InterpolateColor(Colors.Yellow, Colors.Orange, (temp - 21) / 7.0);
					return InterpolateColor(Colors.Orange, Colors.Red, (temp - 28) / 5.0);
				}

				if (temp <= 20) return Colors.Purple;
				if (temp >= 92) return Colors.Red;
				if (temp < 40) return InterpolateColor(Colors.Purple, Colors.DodgerBlue, (temp - 20) / 20.0);
				if (temp < 70) return InterpolateColor(Colors.DodgerBlue, Colors.Yellow, (temp - 40) / 30.0);
				if (temp < 82) return InterpolateColor(Colors.Yellow, Colors.Orange, (temp - 70) / 12.0);
				return InterpolateColor(Colors.Orange, Colors.Red, (temp - 82) / 10.0);
			}
		}

		private static Color InterpolateColor(Color c1, Color c2, double fraction)
		{
			byte r = (byte)(c1.R + (c2.R - c1.R) * fraction);
			byte g = (byte)(c1.G + (c2.G - c1.G) * fraction);
			byte b = (byte)(c1.B + (c2.B - c1.B) * fraction);
			return Color.FromRgb(r, g, b);
		}

		public static void DrawDailyGraph(Canvas dailyGraphCanvas, Canvas dailyScaleCanvas,
	Polygon tempRibbonPolygon, Polyline highTempGraphLine, Polyline lowTempGraphLine,
	Ellipse activeHighTempDot, Ellipse activeLowTempDot,
	System.Collections.Generic.List<DailyForecast> dailyData)
		{
			if (dailyData == null || dailyData.Count == 0) return;

			System.Collections.Generic.List<UIElement> oldElements = [];
			foreach (UIElement child in dailyGraphCanvas.Children)
			{
				if (child != tempRibbonPolygon && child != highTempGraphLine && child != lowTempGraphLine && child != activeHighTempDot && child != activeLowTempDot)
					oldElements.Add(child);
			}
			foreach (var child in oldElements) dailyGraphCanvas.Children.Remove(child);
			PointCollection highPoints = [];
			PointCollection lowPoints = [];
			double itemWidth = 240.0;
			double startX = 35.0;
			double graphHeight = 150.0;
			double minTemp = double.MaxValue;
			double maxTemp = double.MinValue;
			foreach (var d in dailyData)
			{
				double hT = Convert.ToDouble(d.High);
				double lT = Convert.ToDouble(d.Low);
				if (hT > maxTemp) maxTemp = hT;
				if (lT < minTemp) minTemp = lT;
			}
			double range = maxTemp - minTemp;
			if (range == 0) range = 1;
			double paddedRange = range * 1.2;
			double adjustedMin = minTemp - (range * 0.1);

			LinearGradientBrush highLineBrush = new() { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
			LinearGradientBrush lowLineBrush = new() { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
			LinearGradientBrush ribbonBrush = new() { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };

			int gradientSteps = 10;
			for (int i = 0; i <= gradientSteps; i++)
			{
				double fraction = i / (double)gradientSteps;
				double tempAtFraction = maxTemp - (fraction * (maxTemp - minTemp));
				Color c = GetTemperatureColor(tempAtFraction);
				ribbonBrush.GradientStops.Add(new GradientStop(c, fraction));
			}

			for (int i = 0; i < dailyData.Count; i++)
			{
				double hT = Convert.ToDouble(dailyData[i].High);
				double lT = Convert.ToDouble(dailyData[i].Low);
				double x = startX + (i * itemWidth);
				double hy = graphHeight - ((hT - adjustedMin) / paddedRange) * graphHeight;
				double ly = graphHeight - ((lT - adjustedMin) / paddedRange) * graphHeight;
				highPoints.Add(new(x, hy));
				lowPoints.Add(new(x, ly));

				double offset = (i + 1) / (double)(dailyData.Count + 1);
				highLineBrush.GradientStops.Add(new GradientStop(GetTemperatureColor(hT), offset));
				lowLineBrush.GradientStops.Add(new GradientStop(GetTemperatureColor(lT), offset));
			}

			double extendedX = startX + (dailyData.Count * itemWidth);
			double preExtendedX = startX - itemWidth;

			if (dailyData.Count > 0)
			{
				double lastHigh = Convert.ToDouble(dailyData.Last().High);
				double lastLow = Convert.ToDouble(dailyData.Last().Low);
				highLineBrush.GradientStops.Add(new GradientStop(GetTemperatureColor(lastHigh), 1.0));
				lowLineBrush.GradientStops.Add(new GradientStop(GetTemperatureColor(lastLow), 1.0));
			}

			highPoints.Add(new(extendedX, highPoints.Last().Y));
			lowPoints.Add(new(extendedX, lowPoints.Last().Y));
			if (highPoints.Count > 0) highPoints.Insert(0, new(preExtendedX, highPoints.First().Y));
			if (lowPoints.Count > 0) lowPoints.Insert(0, new(preExtendedX, lowPoints.First().Y));

			PointCollection ribbonPoints = [.. highPoints];
			for (int i = lowPoints.Count - 1; i >= 0; i--) ribbonPoints.Add(lowPoints[i]);
			tempRibbonPolygon.Points = ribbonPoints;

			ribbonBrush.Freeze();
			tempRibbonPolygon.Fill = ribbonBrush;

			highLineBrush.Freeze();
			highTempGraphLine.Points = highPoints;
			highTempGraphLine.Stroke = highLineBrush;

			lowLineBrush.Freeze();
			lowTempGraphLine.Points = lowPoints;
			lowTempGraphLine.Stroke = lowLineBrush;

			activeHighTempDot.Visibility = Visibility.Visible;
			activeLowTempDot.Visibility = Visibility.Visible;

			if (dailyData.Count > 0)
			{
				var highDotBrush = new SolidColorBrush(GetTemperatureColor(Convert.ToDouble(dailyData[0].High)));
				highDotBrush.Freeze();
				activeHighTempDot.Fill = highDotBrush;

				var lowDotBrush = new SolidColorBrush(GetTemperatureColor(Convert.ToDouble(dailyData[0].Low)));
				lowDotBrush.Freeze();
				activeLowTempDot.Fill = lowDotBrush;
			}
			double maxY = graphHeight - ((maxTemp - adjustedMin) / paddedRange) * graphHeight;
			double minY = graphHeight - ((minTemp - adjustedMin) / paddedRange) * graphHeight;
			double totalWidth = dailyData.Count * itemWidth;
			DoubleCollection dailyGridDashArray = [4, 4];
			dailyGridDashArray.Freeze();

			for (int i = 0; i < 5; i++)
			{
				double currentY = minY - ((minY - maxY) / 4.0 * i);
				Line gridLine = new()
				{
					X1 = 0,
					Y1 = currentY,
					X2 = totalWidth,
					Y2 = currentY,
					Opacity = 0.3,
					StrokeThickness = 1,
					StrokeDashArray = dailyGridDashArray,
					Stroke = (Brush)Application.Current.Resources["SecondaryTextBrush"]
				};
				Panel.SetZIndex(gridLine, -1);
				dailyGraphCanvas.Children.Add(gridLine);
			}
			dailyScaleCanvas.Children.Clear();
			double textX = 5.0;
			for (int i = 0; i < 5; i++)
			{
				double currentTemp = minTemp + ((maxTemp - minTemp) / 4.0 * i);
				double currentY = minY - ((minY - maxY) / 4.0 * i);
				TextBlock tickText = new()
				{
					Text = $"{Math.Round(currentTemp)}°",
					FontSize = 11,
					FontWeight = FontWeights.SemiBold,
					Foreground = (Brush)Application.Current.Resources["GraphScaleTextBrush"]
				};
				RenderOptions.SetClearTypeHint(tickText, ClearTypeHint.Enabled);
				Canvas.SetLeft(tickText, textX);
				Canvas.SetTop(tickText, currentY - 7);
				dailyScaleCanvas.Children.Add(tickText);
			}
		}
	}
}