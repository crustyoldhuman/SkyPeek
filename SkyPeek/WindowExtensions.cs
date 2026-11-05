using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Interop;

namespace SkyPeek
{
	public static partial class WindowExtensions
	{
		[LibraryImport("user32.dll", EntryPoint = "GetWindowLongW")]
		private static partial int GetWindowLong(IntPtr hwnd, int index);

		[LibraryImport("user32.dll", EntryPoint = "SetWindowLongW")]
		private static partial int SetWindowLong(IntPtr hwnd, int index, int newStyle);

		private const int GWL_EXSTYLE = -20;
		private const int WS_EX_TOOLWINDOW = 0x00000080;

		public static void HideFromAltTab(this Window window)
		{
			var helper = new WindowInteropHelper(window);
			IntPtr hwnd = helper.Handle;
			int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
			int result = SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TOOLWINDOW);
			if (result == 0)
			{
				System.Diagnostics.Debug.WriteLine("SetWindowLong failed.");
			}
		}
	}

	public class FontSizeAddConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			// Takes the base font size, adds the parameter (e.g., 2), and returns the new size
			if (value is double baseSize && double.TryParse(parameter?.ToString(), out double amountToAdd))
			{
				return baseSize + amountToAdd;
			}
			return value;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}