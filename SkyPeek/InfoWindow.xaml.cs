using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Controls.Primitives;
namespace SkyPeek
{
	/// <summary>
	/// Interaction logic for InfoWindow.xaml
	/// </summary>
	public partial class InfoWindow : Window
	{
		private bool _isClosing = false;
		private bool _isKeyboardNavRevealed = false;
		private bool _isInitializing = true;

		public InfoWindow()
		{
			InitializeComponent();
			ThemeHelper.ApplyAccentColor(this, GradientCenter, GradientEdge, WindowBorder, WindowShadow);
			Microsoft.Win32.SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;
			this.Loaded += InfoWindow_Loaded;
		}

		private void SystemEvents_UserPreferenceChanged(object sender, Microsoft.Win32.UserPreferenceChangedEventArgs e)
		{
			if (e.Category == Microsoft.Win32.UserPreferenceCategory.Color || e.Category == Microsoft.Win32.UserPreferenceCategory.General)
			{
				Application.Current.Dispatcher.Invoke(() => ThemeHelper.ApplyAccentColor(this, GradientCenter, GradientEdge, WindowBorder, WindowShadow));
			}
		}

		private void InfoWindow_Loaded(object sender, RoutedEventArgs e)
		{
			ApplySavedSize();
			Application.Current.Dispatcher.InvokeAsync(() =>
			{
				if (AppConfig.Current.DisableAnimations)
				{
					this.Opacity = 1.0;
					WindowScale.ScaleX = 1.0;
					WindowScale.ScaleY = 1.0;
					return;
				}

				var duration = TimeSpan.FromMilliseconds(100);
				var ease = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut };

				var fadeAnimation = new System.Windows.Media.Animation.DoubleAnimation(1.0, duration) { EasingFunction = ease };
				var scaleXAnimation = new System.Windows.Media.Animation.DoubleAnimation(1.0, duration) { EasingFunction = ease };
				var scaleYAnimation = new System.Windows.Media.Animation.DoubleAnimation(1.0, duration) { EasingFunction = ease };

				this.BeginAnimation(UIElement.OpacityProperty, fadeAnimation);
				WindowScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleXAnimation);
				WindowScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleYAnimation);
			}, System.Windows.Threading.DispatcherPriority.Loaded);
		}

		protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
		{
			if (AppConfig.Current.DisableAnimations) return;

			if (!_isClosing)
			{
				e.Cancel = true;
				_isClosing = true;

				var duration = TimeSpan.FromMilliseconds(100);
				var ease = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseIn };

				var fadeAnimation = new System.Windows.Media.Animation.DoubleAnimation(0.0, duration) { EasingFunction = ease };
				var scaleXAnimation = new System.Windows.Media.Animation.DoubleAnimation(0.85, duration) { EasingFunction = ease };
				var scaleYAnimation = new System.Windows.Media.Animation.DoubleAnimation(0.85, duration) { EasingFunction = ease };

				fadeAnimation.Completed += (s, args) => { this.Close(); };

				this.BeginAnimation(UIElement.OpacityProperty, fadeAnimation);
				WindowScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleXAnimation);
				WindowScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleYAnimation);
			}
		}

		private void Close_Click(object sender, RoutedEventArgs e)
		{
			SettingsManager.Save();
			this.Close();
		}

		protected override void OnClosed(EventArgs e)
		{
			Microsoft.Win32.SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;
			base.OnClosed(e);
		}

		private void ApplySavedSize()
		{
			if (AppConfig.Current.InfoWindowWidth >= 420 && AppConfig.Current.InfoWindowHeight >= 510)
			{
				this.Width = Math.Min(AppConfig.Current.InfoWindowWidth, 630);
				this.Height = Math.Min(AppConfig.Current.InfoWindowHeight, 750);
			}
			_isInitializing = false;
		}

		private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			if (_isInitializing) return;

			AppConfig.Current.InfoWindowWidth = this.ActualWidth;
			AppConfig.Current.InfoWindowHeight = this.ActualHeight;
		}

		private POINT _resizeStartPos;
		private double _resizeStartWidth;
		private double _dpiScaleX = 1.0;
		private double _dpiScaleY = 1.0;

		private void ResizeThumb_DragStarted(object sender, DragStartedEventArgs e)
		{
			_resizeStartPos = NativeMethods.GetCursorPos();
			_resizeStartWidth = this.ActualWidth;

			this.Width = double.NaN;
			this.Height = double.NaN;

			PresentationSource source = PresentationSource.FromVisual(this);
			if (source != null && source.CompositionTarget != null)
			{
				_dpiScaleX = source.CompositionTarget.TransformToDevice.M11;
				_dpiScaleY = source.CompositionTarget.TransformToDevice.M22;
			}
		}

		private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
		{
			double targetRatio = 420.0 / 510.0;
			double minWidth = 420;
			double maxWidth = 630;

			POINT currentPos = NativeMethods.GetCursorPos();
			double logicalDeltaX = (currentPos.X - _resizeStartPos.X) / _dpiScaleX;

			double requestedWidth = _resizeStartWidth + logicalDeltaX;
			double finalLogicalWidth = Math.Max(minWidth, Math.Min(maxWidth, requestedWidth));
			double finalLogicalHeight = finalLogicalWidth / targetRatio;

			IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
			int physWidth = (int)Math.Round(finalLogicalWidth * _dpiScaleX);
			int physHeight = (int)Math.Round(finalLogicalHeight * _dpiScaleY);

			NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, 0, 0, physWidth, physHeight, 0x0016);
		}

		private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
		{
			try
			{
				string targetUri = e.Uri.IsAbsoluteUri ? e.Uri.AbsoluteUri : e.Uri.OriginalString;

				if (targetUri.Contains("SkyPeekReadMe.html", StringComparison.OrdinalIgnoreCase))
				{
					string localPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "SkyPeekReadMe.html");
					if (System.IO.File.Exists(localPath))
					{
						targetUri = localPath;
					}
					else
					{
						targetUri = "https://github.com/crustyoldhuman/SkyPeek/blob/main/SkyPeekReadMe.html";
						ToastNotification.TriggerToast(ToastText, "File not found! Opening online version.", System.Windows.VerticalAlignment.Bottom, 40);
					}
				}

				System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(targetUri) { UseShellExecute = true });
			}
			catch (Exception)
			{
				// Silently fails if host policies restrict execution or no default browser is found
			}

			e.Handled = true;
		}

		protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
		{
			_isKeyboardNavRevealed = false;
			System.Windows.Input.FocusManager.SetFocusedElement(this, null);
			System.Windows.Input.Keyboard.ClearFocus();
			this.Focus();
			base.OnPreviewMouseDown(e);
		}

		protected override void OnPreviewKeyDown(KeyEventArgs e)
		{
			if (e.Key == Key.Back || e.Key == Key.Escape)
			{
				e.Handled = true;
				this.Close();
				return;
			}

			if (!_isKeyboardNavRevealed && (e.Key == Key.Up || e.Key == Key.Down || e.Key == Key.Left || e.Key == Key.Right))
			{
				_isKeyboardNavRevealed = true;
				e.Handled = true;
				CloseButton.Focus();
				return;
			}

			if (e.Key == Key.Enter && Keyboard.FocusedElement == CloseButton)
			{
				e.Handled = true;
				this.Close();
				return;
			}

			if (e.Key == Key.Up)
			{
				e.Handled = true;
				if (Keyboard.FocusedElement == CloseButton) Link6.Focus();
				else if (Keyboard.FocusedElement == Link6) Link5.Focus();
				else if (Keyboard.FocusedElement == Link5) Link4.Focus();
				else if (Keyboard.FocusedElement == Link4) Link3.Focus();
				else if (Keyboard.FocusedElement == Link3) Link2.Focus();
				else if (Keyboard.FocusedElement == Link2) Link1.Focus();
				else if (Keyboard.FocusedElement == Link1) CloseButton.Focus();
				return;
			}
			else if (e.Key == Key.Down)
			{
				e.Handled = true;
				if (Keyboard.FocusedElement == CloseButton) Link1.Focus();
				else if (Keyboard.FocusedElement == Link1) Link2.Focus();
				else if (Keyboard.FocusedElement == Link2) Link3.Focus();
				else if (Keyboard.FocusedElement == Link3) Link4.Focus();
				else if (Keyboard.FocusedElement == Link4) Link5.Focus();
				else if (Keyboard.FocusedElement == Link5) Link6.Focus();
				else if (Keyboard.FocusedElement == Link6) CloseButton.Focus();
				return;
			}

			base.OnPreviewKeyDown(e);
		}
	}
}