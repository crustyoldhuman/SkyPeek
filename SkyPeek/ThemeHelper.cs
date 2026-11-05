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
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace SkyPeek
{
	[System.Runtime.Versioning.SupportedOSPlatform("windows10.0.19041.0")]
	public static class ThemeHelper
	{
		private static bool? _lastGlobalIsLightTheme;
		private static Color? _lastGlobalAccentColor;
		private static bool? _lastGlobalUseAccentColor;

		public static async void ApplyAccentColor(Window window, GradientStop center, GradientStop edge, Border border, DropShadowEffect shadow)
		{
			try
			{
				bool isLightTheme = Wpf.Ui.Appearance.SystemThemeManager.GetCachedSystemTheme() == Wpf.Ui.Appearance.SystemTheme.Light;
				bool isSystemLightTheme = isLightTheme;
				bool showBorderColor = true;

				if (AppConfig.Current.ThemePreference == "Light") isLightTheme = true;
				else if (AppConfig.Current.ThemePreference == "Dark") isLightTheme = false;

				Color accent;
				try
				{
					var uiSettings = new Windows.UI.ViewManagement.UISettings();
					var osColor = uiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.Accent);
					accent = Color.FromArgb(osColor.A, osColor.R, osColor.G, osColor.B);
				}
				catch
				{
					accent = SystemParameters.WindowGlassColor;
				}

				window.Dispatcher.Invoke(() =>
				{
					var mw = Application.Current.MainWindow as MainWindow ?? Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
					bool useAccentColor = AppConfig.Current.UseAccentColor;
					bool isTransparencyEnabled = AppConfig.Current.UseTransparency;

					bool themeChanged = _lastGlobalIsLightTheme == null || _lastGlobalIsLightTheme != isLightTheme;
					bool accentChanged = _lastGlobalAccentColor == null || _lastGlobalAccentColor != accent;
					bool useAccentSettingChanged = _lastGlobalUseAccentColor == null || _lastGlobalUseAccentColor != useAccentColor;
					bool performGlobalUpdate = themeChanged || useAccentSettingChanged || (useAccentColor && accentChanged);

					if (performGlobalUpdate)
					{
						_lastGlobalIsLightTheme = isLightTheme;
						_lastGlobalAccentColor = accent;
						_lastGlobalUseAccentColor = useAccentColor;

						var targetTheme = isLightTheme ? Wpf.Ui.Appearance.ApplicationTheme.Light : Wpf.Ui.Appearance.ApplicationTheme.Dark;
						Wpf.Ui.Appearance.ApplicationThemeManager.Apply(targetTheme);

						string[] wpfUiBrushKeys = [
							"SystemAccentBrush", "SystemAccentColorPrimaryBrush", "SystemAccentColorSecondaryBrush", "SystemAccentColorTertiaryBrush",
							"AccentFillColorDefaultBrush", "AccentFillColorSecondaryBrush", "AccentFillColorTertiaryBrush",
							"ControlFillColorAccentBrush", "ControlFillColorAccentSecondaryBrush", "ControlFillColorAccentTertiaryBrush", "ControlFillColorAccentDefaultBrush",
							"AccentTextFillColorPrimaryBrush", "AccentTextFillColorSecondaryBrush", "AccentTextFillColorTertiaryBrush",
							"SystemControlHighlightAccentBrush", "SystemControlHighlightAccent2Brush", "SystemControlHighlightAccent3Brush",
							"ControlStrongFillColorDefaultBrush",
							"SliderThumbBackground", "SliderThumbBackgroundPointerOver", "SliderThumbBackgroundPressed",
							"SliderTrackValueFill", "SliderTrackValueFillPointerOver", "SliderTrackValueFillPressed",
							"RadioButtonOuterEllipseCheckedFill", "RadioButtonOuterEllipseCheckedPointerOverFill", "RadioButtonOuterEllipseCheckedPressedFill",
							"RadioButtonOuterEllipseCheckedStroke", "RadioButtonOuterEllipseCheckedStrokePointerOver", "RadioButtonOuterEllipseCheckedStrokePressed",
							"RadioButtonCheckGlyphFill", "RadioButtonCheckGlyphPointerOverFill", "RadioButtonCheckGlyphPressedFill",
							"CheckBoxCheckBackgroundFillChecked", "CheckBoxCheckBackgroundFillCheckedPointerOver", "CheckBoxCheckBackgroundFillCheckedPressed",
							"CheckBoxCheckBackgroundStrokeChecked", "CheckBoxCheckBackgroundStrokeCheckedPointerOver", "CheckBoxCheckBackgroundStrokeCheckedPressed"
						];
						string[] wpfUiColorKeys = [
							"SystemAccentColor", "SystemAccentColorPrimary", "SystemAccentColorSecondary", "SystemAccentColorTertiary",
							"SystemAccentColorLight1", "SystemAccentColorLight2", "SystemAccentColorLight3",
							"SystemAccentColorDark1", "SystemAccentColorDark2", "SystemAccentColorDark3"
						];

						if (useAccentColor)
						{
							foreach (var key in wpfUiBrushKeys) Application.Current.Resources.Remove(key);
							foreach (var key in wpfUiColorKeys) Application.Current.Resources.Remove(key);
							Wpf.Ui.Appearance.ApplicationAccentColorManager.Apply(accent);
						}
						else
						{
							Color fallbackColor = Color.FromArgb(255, 110, 110, 115);
							SolidColorBrush fallbackBrush = new(fallbackColor);
							fallbackBrush.Freeze();

							Wpf.Ui.Appearance.ApplicationAccentColorManager.Apply(fallbackColor);

							foreach (var key in wpfUiBrushKeys) Application.Current.Resources[key] = fallbackBrush;
							foreach (var key in wpfUiColorKeys) Application.Current.Resources[key] = fallbackColor;

							SolidColorBrush glyphBrush = new(isLightTheme ? Colors.White : Colors.Black);
							glyphBrush.Freeze();
							Application.Current.Resources["RadioButtonCheckGlyphFill"] = glyphBrush;
							Application.Current.Resources["RadioButtonCheckGlyphPointerOverFill"] = glyphBrush;
							Application.Current.Resources["RadioButtonCheckGlyphPressedFill"] = glyphBrush;
						}
					}

					bool hasShadow = SystemParameters.DropShadow;

					// Transparency values (0 = fully transparent, 255 = fully solid, light/dark modes, Center=gradient, Edge=behind gradient, regular background)
					// If OS shadows are disabled, opacity is boosted to compensate for the missing dark blur bleed-through
					byte lightAlphaCenter = (byte)(isTransparencyEnabled ? (hasShadow ? 255 : 255) : 255);
					byte lightAlphaEdge = (byte)(isTransparencyEnabled ? (hasShadow ? 210 : 220) : 255);
					byte darkAlphaCenter = (byte)(isTransparencyEnabled ? (hasShadow ? 255 : 255) : 255);
					byte darkAlphaEdge = (byte)(isTransparencyEnabled ? (hasShadow ? 210 : 245) : 255);

					if (isLightTheme)
					{
						if (useAccentColor && accent.A > 0)
						{
							// MAIN WINDOWS: Change the "245" values below to test different LIGHT Mode ACCENT center colors
							byte cR = (byte)Math.Min(255, (accent.R * 0.10) + (245 * 0.90));
							byte cG = (byte)Math.Min(255, (accent.G * 0.10) + (245 * 0.90));
							byte cB = (byte)Math.Min(255, (accent.B * 0.10) + (245 * 0.90));
							center.Color = Color.FromArgb(lightAlphaCenter, cR, cG, cB);

							byte eR = (byte)Math.Min(255, (accent.R * 0.10) + (225 * 0.90));
							byte eG = (byte)Math.Min(255, (accent.G * 0.10) + (225 * 0.90));
							byte eB = (byte)Math.Min(255, (accent.B * 0.10) + (225 * 0.90));
							edge.Color = Color.FromArgb(lightAlphaEdge, eR, eG, eB);

							byte bR = (byte)Math.Min(255, (accent.R * 0.30) + (180 * 0.70));
							byte bG = (byte)Math.Min(255, (accent.G * 0.30) + (180 * 0.70));
							byte bB = (byte)Math.Min(255, (accent.B * 0.30) + (180 * 0.70));
							border.BorderBrush = new SolidColorBrush(showBorderColor ? Color.FromArgb(255, bR, bG, bB) : Color.FromRgb(180, 180, 180));
						}
						else
						{
							// MAIN WINDOWS: Change the "255" RGB values below to test different LIGHT Mode DEFAULT center colors
							center.Color = Color.FromArgb(lightAlphaCenter, 250, 250, 250);
							edge.Color = Color.FromArgb(lightAlphaEdge, 225, 225, 230);
							border.BorderBrush = new SolidColorBrush(Color.FromRgb(180, 180, 180));
						}
					}
					else
					{
						if (useAccentColor && accent.A > 0)
						{
							// CHANGE RGB HUES HERE: Main Windows - Dark Mode w/ Accent Color (R, G, B)
							byte centerR = (byte)((accent.R * 0.25) + (0x38 * 0.75));
							byte centerG = (byte)((accent.G * 0.25) + (0x38 * 0.75));
							byte centerB = (byte)((accent.B * 0.25) + (0x3D * 0.75));
							center.Color = Color.FromArgb(darkAlphaCenter, centerR, centerG, centerB);

							byte edgeR = (byte)((accent.R * 0.10) + (0x15 * 0.90));
							byte edgeG = (byte)((accent.G * 0.10) + (0x15 * 0.90));
							byte edgeB = (byte)((accent.B * 0.10) + (0x18 * 0.90));
							edge.Color = Color.FromArgb(darkAlphaEdge, edgeR, edgeG, edgeB);

							byte borderR = (byte)((accent.R * 0.50) + (0x3E * 0.50));
							byte borderG = (byte)((accent.G * 0.50) + (0x3E * 0.50));
							byte borderB = (byte)((accent.B * 0.50) + (0x42 * 0.50));
							border.BorderBrush = new SolidColorBrush(showBorderColor ? Color.FromRgb(borderR, borderG, borderB) : Color.FromRgb(0x3E, 0x3E, 0x42));
						}
						else
						{
							// CHANGE RGB HUES HERE: Main Windows - Dark Mode Default (R, G, B hex values)
							center.Color = Color.FromArgb(darkAlphaCenter, 0x38, 0x38, 0x3D);
							edge.Color = Color.FromArgb(darkAlphaEdge, 0x15, 0x15, 0x18);
							border.BorderBrush = new SolidColorBrush(Color.FromRgb(0x3E, 0x3E, 0x42));
						}
					}

					if (shadow is { } s) s.Opacity = SystemParameters.DropShadow ? (isLightTheme ? 0.20 : 0.9) : 0.0;

					// Freeze the border brush immediately following instantiation from the if/else tree above
					border.BorderBrush?.Freeze();

					// Inject dynamic resources into global application memory
					var primaryTextBrush = new SolidColorBrush(isLightTheme ? Colors.Black : Colors.White);
					primaryTextBrush.Freeze();
					Application.Current.Resources["PrimaryTextBrush"] = primaryTextBrush;

					Color primaryTextColor = isLightTheme ? Colors.Black : Colors.White;
					Color primaryShadowColor = isLightTheme ? Colors.White : Colors.Black;

					Application.Current.Resources["PrimaryTextColor"] = primaryTextColor;
					Application.Current.Resources["PrimaryShadowColor"] = primaryShadowColor;

					bool isWin11 = Environment.OSVersion.Version.Build >= 22000;
					Application.Current.Resources["OsTextFormattingMode"] = TextFormattingMode.Display;
					Application.Current.Resources["OsTextRenderingMode"] = TextRenderingMode.ClearType;
					Application.Current.Resources["OsLayoutRounding"] = true;
					Application.Current.Resources["TrayIconFontWeight"] = isWin11 ? FontWeights.SemiBold : FontWeights.Normal;
					Application.Current.Resources["OsDropShadowVisibility"] = isWin11 ? Visibility.Visible : Visibility.Collapsed;

					var tabGlow = new DropShadowEffect { Color = primaryShadowColor, BlurRadius = 6, ShadowDepth = 1, Opacity = 0.9 };
					tabGlow.Freeze();
					Application.Current.Resources["TabGlowEffect"] = tabGlow;

					var tabHover = new DropShadowEffect { Color = primaryTextColor, BlurRadius = 12, ShadowDepth = 0, Opacity = 1.0 };
					tabHover.Freeze();
					Application.Current.Resources["TabHoverGlowEffect"] = tabHover;

					var baseShadow = new DropShadowEffect { Color = primaryShadowColor, BlurRadius = 10, ShadowDepth = 0, Opacity = 1.0 };
					baseShadow.Freeze();
					Application.Current.Resources["BaseTextShadowEffect"] = baseShadow;

					var softShadow = new DropShadowEffect { Color = primaryShadowColor, BlurRadius = 8, ShadowDepth = 1, Opacity = 0.8 };
					softShadow.Freeze();
					Application.Current.Resources["SoftTextShadowEffect"] = softShadow;

					var secondaryTextBrush = new SolidColorBrush(isLightTheme ? Color.FromRgb(60, 60, 60) : Colors.LightGray);
					secondaryTextBrush.Freeze();
					Application.Current.Resources["SecondaryTextBrush"] = secondaryTextBrush;

					var toastBgBrush = new SolidColorBrush(isLightTheme ? Color.FromRgb(212, 212, 212) : Color.FromRgb(24, 24, 27));
					toastBgBrush.Freeze();
					Application.Current.Resources["ToastBackgroundBrush"] = toastBgBrush;

					var linkTextBrush = new SolidColorBrush(isLightTheme ? Color.FromRgb(0, 102, 204) : Color.FromRgb(77, 165, 255));
					linkTextBrush.Freeze();
					Application.Current.Resources["LinkTextBrush"] = linkTextBrush;

					var hoverBgBrush = new SolidColorBrush(isLightTheme ? Color.FromArgb(45, 0, 0, 0) : Color.FromArgb(25, 255, 255, 255));
					hoverBgBrush.Freeze();
					Application.Current.Resources["HoverBackgroundBrush"] = hoverBgBrush;

					var agendaCardBgBrush = new SolidColorBrush(isLightTheme ? Color.FromArgb(16, 0, 0, 15) : Color.FromArgb(25, 255, 255, 255));
					agendaCardBgBrush.Freeze();
					Application.Current.Resources["AgendaCardBackgroundBrush"] = agendaCardBgBrush;

					// Unified Alert Button Brushes (Adjust the Light Mode RGB values below to change the hue for both buttons)
					var alertBtnBgBrush = new SolidColorBrush(isLightTheme ? Color.FromRgb(235, 235, 235) : Color.FromArgb(51, 255, 255, 255));
					alertBtnBgBrush.Freeze();
					Application.Current.Resources["AlertButtonBgBrush"] = alertBtnBgBrush;

					var alertBtnHoverBgBrush = new SolidColorBrush(isLightTheme ? Color.FromRgb(215, 215, 215) : Color.FromArgb(80, 255, 255, 255));
					alertBtnHoverBgBrush.Freeze();
					Application.Current.Resources["AlertButtonHoverBgBrush"] = alertBtnHoverBgBrush;

					var solidBgBrush = new SolidColorBrush(center.Color);
					solidBgBrush.Freeze();
					Application.Current.Resources["SolidBackgroundBrush"] = solidBgBrush;

					var trayTextBrush = new SolidColorBrush(isSystemLightTheme ? Colors.Black : Colors.White);
					trayTextBrush.Freeze();
					Application.Current.Resources["TrayTextBrush"] = trayTextBrush;

					var inverseTextBrush = new SolidColorBrush(Colors.White);
					inverseTextBrush.Freeze();
					Application.Current.Resources["InverseTextBrush"] = inverseTextBrush;

					Application.Current.Resources["HourlyGraphRibbonOpacity"] = isLightTheme ? 0.30 : 0.15;
					Application.Current.Resources["DailyGraphRibbonOpacity"] = isLightTheme ? 0.20 : 0.10;

					Application.Current.Resources["RadarMapOpacity"] = isLightTheme ? 0.90 : 0.85;
					Application.Current.Resources["RadarLayerOpacity"] = isLightTheme ? 0.60 : 0.55;

					var appInfoTextBrush = new SolidColorBrush(isLightTheme ? Color.FromRgb(100, 100, 100) : Color.FromRgb(150, 150, 150));
					appInfoTextBrush.Freeze();
					Application.Current.Resources["AppInfoTextBrush"] = appInfoTextBrush;

					Application.Current.Resources["LoadingOpacity"] = isLightTheme ? 0.45 : 0.35;

					var graphLineBaseBrush = new SolidColorBrush(isLightTheme ? Color.FromRgb(70, 70, 180) : Color.FromArgb(0xF2, 0x6A, 0x5A, 0xCD));
					graphLineBaseBrush.Freeze();
					Application.Current.Resources["GraphLineBaseBrush"] = graphLineBaseBrush;

					var graphScaleTextBrush = new SolidColorBrush(isLightTheme ? Color.FromRgb(110, 110, 110) : Colors.LightGray);
					graphScaleTextBrush.Freeze();
					Application.Current.Resources["GraphScaleTextBrush"] = graphScaleTextBrush;

					Color vignetteCenter = isLightTheme ? Color.FromArgb(120, 255, 255, 255) : Color.FromArgb(120, 0, 0, 0);
					Color vignetteEdge = isLightTheme ? Color.FromArgb(0, 255, 255, 255) : Color.FromArgb(0, 0, 0, 0);
					Application.Current.Resources["IconVignetteCenterColor"] = vignetteCenter;
					Application.Current.Resources["IconVignetteEdgeColor"] = vignetteEdge;

					var vignetteBrush = new RadialGradientBrush
					{
						GradientOrigin = new Point(0.5, 0.5),
						Center = new Point(0.5, 0.5),
						RadiusX = 0.5,
						RadiusY = 0.5
					};
					vignetteBrush.GradientStops.Add(new GradientStop(vignetteCenter, 0.0));
					vignetteBrush.GradientStops.Add(new GradientStop(vignetteEdge, 1.0));
					vignetteBrush.Freeze();
					Application.Current.Resources["IconVignetteBrush"] = vignetteBrush;

					Application.Current.Resources["RadarTimeTextBrush"] = primaryTextBrush;

					mw?.RefreshTrayIcon();
				});
			}
			catch { }
		}

		public static async void ApplyTrayTheme(Window control, GradientStop center, GradientStop edge, Border border, DropShadowEffect shadow)
		{
			try
			{
				bool isLightTheme = Wpf.Ui.Appearance.SystemThemeManager.GetCachedSystemTheme() == Wpf.Ui.Appearance.SystemTheme.Light;
				bool showBorderColor = true;

				if (AppConfig.Current.ThemePreference == "Light") isLightTheme = true;
				else if (AppConfig.Current.ThemePreference == "Dark") isLightTheme = false;

				Color accent;
				try
				{
					var uiSettings = new Windows.UI.ViewManagement.UISettings();
					var osColor = uiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.Accent);
					accent = Color.FromArgb(osColor.A, osColor.R, osColor.G, osColor.B);
				}
				catch
				{
					accent = SystemParameters.WindowGlassColor;
				}

				control.Dispatcher.Invoke(() =>
				{
					var mw = Application.Current.MainWindow as MainWindow ?? Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
					bool useAccentColor = AppConfig.Current.UseAccentColor;
					bool isTransparencyEnabled = AppConfig.Current.UseTransparency;
					bool hasShadow = SystemParameters.DropShadow;

					// >>> ADJUST TRAY MENU OPACITIES HERE <<<
					// (0 = fully transparent, 255 = fully solid)
					byte lightAlphaCenter = (byte)(isTransparencyEnabled ? (hasShadow ? 255 : 255) : 255);
					byte lightAlphaEdge = (byte)(isTransparencyEnabled ? (hasShadow ? 210 : 220) : 255);
					byte darkAlphaCenter = (byte)(isTransparencyEnabled ? (hasShadow ? 255 : 255) : 255);
					byte darkAlphaEdge = (byte)(isTransparencyEnabled ? (hasShadow ? 210 : 245) : 255);

					Color borderColor;

					if (isLightTheme)
					{
						if (useAccentColor && accent.A > 0)
						{
							// CHANGE RGB HUES HERE: Tray Menu - Light Mode w/ Accent Color (R, G, B)
							byte cR = (byte)Math.Min(255, (accent.R * 0.10) + (245 * 0.90));
							byte cG = (byte)Math.Min(255, (accent.G * 0.10) + (245 * 0.90));
							byte cB = (byte)Math.Min(255, (accent.B * 0.10) + (245 * 0.90));
							center.Color = Color.FromArgb(lightAlphaCenter, cR, cG, cB);

							byte eR = (byte)Math.Min(255, (accent.R * 0.10) + (225 * 0.90));
							byte eG = (byte)Math.Min(255, (accent.G * 0.10) + (225 * 0.90));
							byte eB = (byte)Math.Min(255, (accent.B * 0.10) + (225 * 0.90));
							edge.Color = Color.FromArgb(lightAlphaEdge, eR, eG, eB);

							byte bR = (byte)Math.Min(255, (accent.R * 0.30) + (180 * 0.70));
							byte bG = (byte)Math.Min(255, (accent.G * 0.30) + (180 * 0.70));
							byte bB = (byte)Math.Min(255, (accent.B * 0.30) + (180 * 0.70));
							borderColor = showBorderColor ? Color.FromArgb(255, bR, bG, bB) : Color.FromRgb(180, 180, 180);
						}
						else
						{
							// CHANGE RGB HUES HERE: Tray Menu - Light Mode Default (R, G, B)
							center.Color = Color.FromArgb(lightAlphaCenter, 250, 250, 250);
							edge.Color = Color.FromArgb(lightAlphaEdge, 225, 225, 230);
							borderColor = Color.FromRgb(180, 180, 180);
						}
					}
					else
					{
						if (useAccentColor && accent.A > 0)
						{
							// CHANGE RGB HUES HERE: Tray Menu - Dark Mode w/ Accent Color (R, G, B)
							byte centerR = (byte)((accent.R * 0.25) + (0x38 * 0.75));
							byte centerG = (byte)((accent.G * 0.25) + (0x38 * 0.75));
							byte centerB = (byte)((accent.B * 0.25) + (0x3D * 0.75));
							center.Color = Color.FromArgb(darkAlphaCenter, centerR, centerG, centerB);

							byte edgeR = (byte)((accent.R * 0.10) + (0x15 * 0.90));
							byte edgeG = (byte)((accent.G * 0.10) + (0x15 * 0.90));
							byte edgeB = (byte)((accent.B * 0.10) + (0x18 * 0.90));
							edge.Color = Color.FromArgb(darkAlphaEdge, edgeR, edgeG, edgeB);

							byte borderR = (byte)((accent.R * 0.50) + (0x3E * 0.50));
							byte borderG = (byte)((accent.G * 0.50) + (0x3E * 0.50));
							byte borderB = (byte)((accent.B * 0.50) + (0x42 * 0.50));
							borderColor = showBorderColor ? Color.FromRgb(borderR, borderG, borderB) : Color.FromRgb(0x3E, 0x3E, 0x42);
						}
						else
						{
							// CHANGE RGB HUES HERE: Tray Menu - Dark Mode Default (R, G, B hex values)
							center.Color = Color.FromArgb(darkAlphaCenter, 0x38, 0x38, 0x3D);
							edge.Color = Color.FromArgb(darkAlphaEdge, 0x15, 0x15, 0x18);
							borderColor = Color.FromRgb(0x3E, 0x3E, 0x42);
						}
					}

					bool isWin11 = Environment.OSVersion.Version.Build >= 22000;
					Application.Current.Resources["OsTextFormattingMode"] = TextFormattingMode.Display;
					Application.Current.Resources["OsTextRenderingMode"] = TextRenderingMode.ClearType;
					Application.Current.Resources["OsLayoutRounding"] = true;
					Application.Current.Resources["TrayIconFontWeight"] = isWin11 ? FontWeights.SemiBold : FontWeights.Normal;
					Application.Current.Resources["OsDropShadowVisibility"] = isWin11 ? Visibility.Visible : Visibility.Collapsed;

					var tooltipBgBrush = new SolidColorBrush(center.Color);
					tooltipBgBrush.Freeze();
					Application.Current.Resources["DynamicTooltipBackground"] = tooltipBgBrush;

					var tooltipBorderBrush = new SolidColorBrush(borderColor);
					tooltipBorderBrush.Freeze();
					Application.Current.Resources["DynamicTooltipBorder"] = tooltipBorderBrush;

					var borderBrush = new SolidColorBrush(borderColor);
					borderBrush.Freeze();
					border.BorderBrush = borderBrush;

					if (shadow is DropShadowEffect s)
					{
						if (SystemParameters.DropShadow)
						{
							s.Color = Colors.Black;
							s.Opacity = isLightTheme ? 0.20 : 0.55;
						}
						else
						{
							s.Opacity = 0.0;
						}
					}
				});
			}
			catch { }
		}
	}
}