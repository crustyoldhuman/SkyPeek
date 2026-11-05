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
using System.Windows.Input;

namespace SkyPeek
{
	[System.Runtime.Versioning.SupportedOSPlatform("windows10.0.19041.0")]
	public partial class CurrentViewControl : UserControl
	{
		public CurrentViewControl()
		{
			InitializeComponent();
		}

		private SecondaryViews? GetParentSlideout() => Window.GetWindow(this) as SecondaryViews;

		public void RadarIcon_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			// You will need to move the body of this from Slideout.xaml.cs, OR route it:
			GetParentSlideout()?.RadarIcon_MouseLeftButtonUp(sender, e);
		}

		internal void LocationComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			GetParentSlideout()?.LocationComboBox_SelectionChanged(sender, e);
		}

		internal void LocationComboBox_DropDownOpened(object sender, System.EventArgs e)
		{
			GetParentSlideout()?.LocationComboBox_DropDownOpened(sender, e);
		}

		internal void LocationComboBox_DropDownClosed(object sender, System.EventArgs e)
		{
			GetParentSlideout()?.LocationComboBox_DropDownClosed(sender, e);
		}

		internal void LocationMarquee_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			GetParentSlideout()?.LocationMarquee_SizeChanged(sender, e);
		}

		public void AlertIconBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			GetParentSlideout()?.AlertIconBorder_MouseLeftButtonUp(sender, e);
		}

		public void WeatherIconAnimation_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			GetParentSlideout()?.WeatherIconAnimation_MouseLeftButtonUp(sender, e);
		}

		public void OnboardingHyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
		{
			try
			{
				string path = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "SkyPeekReadme.html");
				string devPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\SkyPeekReadme.html"));

				if (System.IO.File.Exists(path))
				{
					System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
				}
				else if (System.IO.File.Exists(devPath))
				{
					System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(devPath) { UseShellExecute = true });
				}
			}
			catch { }
			e.Handled = true;
		}

		public void DismissOnboarding_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			GetParentSlideout()?.ViewModel.DismissOnboarding();
			e.Handled = true;
		}
	}
}