using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace SkyPeek
{
	public static class AnimationHelper
	{
		public static readonly DependencyProperty FadeOnHoverProperty =
			DependencyProperty.RegisterAttached("FadeOnHover", typeof(bool), typeof(AnimationHelper), new PropertyMetadata(false, OnFadeOnHoverChanged));

		public static bool GetFadeOnHover(DependencyObject obj) => (bool)obj.GetValue(FadeOnHoverProperty);
		public static void SetFadeOnHover(DependencyObject obj, bool value) => obj.SetValue(FadeOnHoverProperty, value);

		public static readonly DependencyProperty HoverScrollTargetProperty =
			DependencyProperty.RegisterAttached("HoverScrollTarget", typeof(System.Windows.Controls.ScrollViewer), typeof(AnimationHelper), new PropertyMetadata(null));

		public static System.Windows.Controls.ScrollViewer GetHoverScrollTarget(DependencyObject obj) => (System.Windows.Controls.ScrollViewer)obj.GetValue(HoverScrollTargetProperty);
		public static void SetHoverScrollTarget(DependencyObject obj, System.Windows.Controls.ScrollViewer value) => obj.SetValue(HoverScrollTargetProperty, value);

		public static readonly DependencyProperty CurrentAnimGuidProperty =
			DependencyProperty.RegisterAttached("CurrentAnimGuid", typeof(string), typeof(AnimationHelper), new PropertyMetadata(null));

		private static void OnFadeOnHoverChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			if (d is UIElement element)
			{
				if ((bool)e.NewValue)
				{
					element.MouseEnter += Element_MouseEnter;
					element.MouseLeave += Element_MouseLeave;
				}
				else
				{
					element.MouseEnter -= Element_MouseEnter;
					element.MouseLeave -= Element_MouseLeave;
				}
			}
		}

		private static void Element_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
		{
			if (sender is UIElement element)
			{
				element.SetValue(CurrentAnimGuidProperty, System.Guid.NewGuid().ToString());
				var anim = new DoubleAnimation(1.0, System.TimeSpan.FromSeconds(0.2));
				element.BeginAnimation(UIElement.OpacityProperty, anim);
			}
		}

		private static void Element_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
		{
			if (sender is UIElement element)
			{
				var scrollTarget = GetHoverScrollTarget(element);

				if (scrollTarget == null || scrollTarget.HorizontalOffset > 0)
				{
					element.SetValue(CurrentAnimGuidProperty, System.Guid.NewGuid().ToString());
					var anim = new DoubleAnimation(0.0, System.TimeSpan.FromSeconds(0.75));
					element.BeginAnimation(UIElement.OpacityProperty, anim);
				}
			}
		}

		public static void Fade(this System.Windows.UIElement element, double targetOpacity, int durationMs, System.Windows.Media.Animation.EasingMode easing = System.Windows.Media.Animation.EasingMode.EaseInOut, Action? onComplete = null)
		{
			if (AppConfig.Current.DisableAnimations)
			{
				element.BeginAnimation(UIElement.OpacityProperty, null);
				element.Opacity = targetOpacity;
				onComplete?.Invoke();
				return;
			}

			string animId = System.Guid.NewGuid().ToString();
			element.SetValue(CurrentAnimGuidProperty, animId);

			System.Windows.Media.Animation.DoubleAnimation anim = new(targetOpacity, TimeSpan.FromMilliseconds(durationMs))
			{
				EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = easing }
			};

			anim.Completed += (s, e) =>
			{
				if ((string)element.GetValue(CurrentAnimGuidProperty) == animId)
				{
					element.BeginAnimation(System.Windows.UIElement.OpacityProperty, null);
					element.Opacity = targetOpacity;
					onComplete?.Invoke();
				}
			};

			element.BeginAnimation(System.Windows.UIElement.OpacityProperty, anim);
		}

		public static void SlideAndFade(this System.Windows.UIElement element, double slideFrom, double slideTo, double fadeFrom, double fadeTo, int durationMs, Action? onComplete = null)
		{
			if (element.RenderTransform is not System.Windows.Media.TranslateTransform)
				element.RenderTransform = new System.Windows.Media.TranslateTransform();

			if (AppConfig.Current.DisableAnimations)
			{
				element.BeginAnimation(UIElement.OpacityProperty, null);
				element.Opacity = fadeTo;
				element.RenderTransform.BeginAnimation(TranslateTransform.XProperty, null);
				((TranslateTransform)element.RenderTransform).X = slideTo;
				onComplete?.Invoke();
				return;
			}

			string animId = System.Guid.NewGuid().ToString();
			element.SetValue(CurrentAnimGuidProperty, animId);

			System.Windows.Media.Animation.DoubleAnimation slide = new(slideFrom, slideTo, TimeSpan.FromMilliseconds(durationMs)) { EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut } };
			System.Windows.Media.Animation.DoubleAnimation fade = new(fadeFrom, fadeTo, TimeSpan.FromMilliseconds(durationMs)) { EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut } };

			slide.Completed += (s, e) =>
			{
				if ((string)element.GetValue(CurrentAnimGuidProperty) == animId)
				{
					element.RenderTransform.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, null);
					element.BeginAnimation(System.Windows.UIElement.OpacityProperty, null);
					((TranslateTransform)element.RenderTransform).X = slideTo;
					element.Opacity = fadeTo;
					onComplete?.Invoke();
				}
			};

			element.RenderTransform.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, slide);
			element.BeginAnimation(System.Windows.UIElement.OpacityProperty, fade);
		}

		public static void FlipAndFade(this System.Windows.UIElement element, double fromScaleX, double toScaleX, double fromOpacity, double toOpacity, int durationMs, System.Windows.Media.Animation.EasingMode easing, Action? onComplete = null)
		{
			element.RenderTransformOrigin = new Point(0.5, 0.5);
			if (element.RenderTransform is not System.Windows.Media.ScaleTransform scaleTransform)
			{
				scaleTransform = new System.Windows.Media.ScaleTransform(fromScaleX, 1.0);
				element.RenderTransform = scaleTransform;
			}

			if (AppConfig.Current.DisableAnimations)
			{
				element.BeginAnimation(UIElement.OpacityProperty, null);
				element.Opacity = toOpacity;
				scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
				scaleTransform.ScaleX = toScaleX;
				onComplete?.Invoke();
				return;
			}

			string animId = System.Guid.NewGuid().ToString();
			element.SetValue(CurrentAnimGuidProperty, animId);

			System.Windows.Media.Animation.DoubleAnimation scaleAnim = new(fromScaleX, toScaleX, TimeSpan.FromMilliseconds(durationMs))
			{
				EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = easing }
			};
			System.Windows.Media.Animation.DoubleAnimation fadeAnim = new(fromOpacity, toOpacity, TimeSpan.FromMilliseconds(durationMs))
			{
				EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = easing }
			};

			scaleAnim.Completed += (s, e) =>
			{
				if ((string)element.GetValue(CurrentAnimGuidProperty) == animId)
				{
					scaleTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, null);
					element.BeginAnimation(System.Windows.UIElement.OpacityProperty, null);
					scaleTransform.ScaleX = toScaleX;
					element.Opacity = toOpacity;
					onComplete?.Invoke();
				}
			};

			scaleTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, scaleAnim);
			element.BeginAnimation(System.Windows.UIElement.OpacityProperty, fadeAnim);
		}

		public static void ClearUIAnimations(this System.Windows.UIElement element)
		{
			element.BeginAnimation(System.Windows.UIElement.OpacityProperty, null);
			if (element.RenderTransform is System.Windows.Media.TranslateTransform t)
			{
				t.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, null);
				t.X = 0;
			}
			else if (element.RenderTransform is System.Windows.Media.ScaleTransform s)
			{
				s.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, null);
				s.ScaleX = 1;
			}
		}

		public static void PlayWindowEntrance(this Window window, TranslateTransform transform, double startX, double startY, Action onComplete)
		{
			if (AppConfig.Current.DisableAnimations)
			{
				transform.BeginAnimation(TranslateTransform.XProperty, null);
				transform.BeginAnimation(TranslateTransform.YProperty, null);
				window.BeginAnimation(UIElement.OpacityProperty, null);
				transform.X = 0;
				transform.Y = 0;
				window.Opacity = 1.0;
				onComplete?.Invoke();
				return;
			}

			string animId = System.Guid.NewGuid().ToString();
			window.SetValue(CurrentAnimGuidProperty, animId);

			DoubleAnimation animX = new(startX, 0, TimeSpan.FromMilliseconds(250)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
			DoubleAnimation animY = new(startY, 0, TimeSpan.FromMilliseconds(250)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
			DoubleAnimation animOpacity = new(0.0, 1.0, TimeSpan.FromMilliseconds(250)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };

			animY.Completed += (s, e) =>
			{
				if ((string)window.GetValue(CurrentAnimGuidProperty) == animId)
				{
					transform.BeginAnimation(TranslateTransform.XProperty, null);
					transform.BeginAnimation(TranslateTransform.YProperty, null);
					window.BeginAnimation(UIElement.OpacityProperty, null);
					transform.X = 0;
					transform.Y = 0;
					window.Opacity = 1.0;
					onComplete?.Invoke();
				}
			};

			transform.BeginAnimation(TranslateTransform.XProperty, animX);
			transform.BeginAnimation(TranslateTransform.YProperty, animY);
			window.BeginAnimation(UIElement.OpacityProperty, animOpacity);
		}

		public static void PlayWindowExit(this Window window, TranslateTransform transform, double endX, double endY, Action onComplete)
		{
			if (AppConfig.Current.DisableAnimations)
			{
				transform.BeginAnimation(TranslateTransform.XProperty, null);
				transform.BeginAnimation(TranslateTransform.YProperty, null);
				window.BeginAnimation(UIElement.OpacityProperty, null);
				transform.X = endX;
				transform.Y = endY;
				window.Opacity = 0.0;
				onComplete?.Invoke();
				return;
			}

			string animId = System.Guid.NewGuid().ToString();
			window.SetValue(CurrentAnimGuidProperty, animId);

			DoubleAnimation animX = new(0, endX, TimeSpan.FromMilliseconds(200)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } };
			DoubleAnimation animY = new(0, endY, TimeSpan.FromMilliseconds(200)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } };
			DoubleAnimation animOpacity = new(window.Opacity, 0.0, TimeSpan.FromMilliseconds(200)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } };

			animY.Completed += (s, e) =>
			{
				if ((string)window.GetValue(CurrentAnimGuidProperty) == animId)
				{
					transform.BeginAnimation(TranslateTransform.XProperty, null);
					transform.BeginAnimation(TranslateTransform.YProperty, null);
					window.BeginAnimation(UIElement.OpacityProperty, null);
					transform.X = endX;
					transform.Y = endY;
					window.Opacity = 0.0;
					onComplete?.Invoke();
				}
			};

			transform.BeginAnimation(TranslateTransform.XProperty, animX);
			transform.BeginAnimation(TranslateTransform.YProperty, animY);
			window.BeginAnimation(UIElement.OpacityProperty, animOpacity);
		}

		public static void StartMarqueeLoop(this TranslateTransform transform, double distance)
		{
			transform.BeginAnimation(TranslateTransform.XProperty, null);
			if (AppConfig.Current.DisableAnimations)
			{
				transform.X = 0;
				return;
			}
			DoubleAnimation anim = new()
			{
				From = 0,
				To = -distance,
				Duration = TimeSpan.FromSeconds(distance / 25.0),
				RepeatBehavior = RepeatBehavior.Forever
			};
			transform.BeginAnimation(TranslateTransform.XProperty, anim);
		}

		public static void TriggerToast(this System.Windows.Controls.Border toastElement, System.Windows.Controls.TextBlock textElement, string message, System.Windows.VerticalAlignment alignment = System.Windows.VerticalAlignment.Bottom, double bottomMargin = 40, int stayOnScreenSeconds = 2, int delayEntranceMs = 0)
		{
			toastElement.VerticalAlignment = alignment;
			toastElement.Margin = new System.Windows.Thickness(0, 0, 0, bottomMargin);
			textElement.Text = message;
			toastElement.BeginAnimation(System.Windows.UIElement.OpacityProperty, null);
			toastElement.Opacity = 0;

			if (AppConfig.Current.DisableAnimations)
			{
				toastElement.Opacity = 0.95;
				System.Windows.Media.Animation.DoubleAnimation fadeOutFast = new(0.95, 0.0, TimeSpan.Zero) { BeginTime = TimeSpan.FromSeconds(stayOnScreenSeconds) };
				toastElement.BeginAnimation(System.Windows.UIElement.OpacityProperty, fadeOutFast);
				return;
			}

			System.Windows.Media.Animation.DoubleAnimation fadeIn = new(0.0, 0.95, TimeSpan.FromMilliseconds(200))
			{
				BeginTime = TimeSpan.FromMilliseconds(delayEntranceMs),
				EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
			};

			System.Windows.Media.Animation.DoubleAnimation fadeOut = new(0.95, 0.0, TimeSpan.FromMilliseconds(300))
			{
				BeginTime = TimeSpan.FromSeconds(stayOnScreenSeconds) + TimeSpan.FromMilliseconds(delayEntranceMs),
				EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseIn }
			};

			fadeIn.Completed += (s, e) => toastElement.BeginAnimation(System.Windows.UIElement.OpacityProperty, fadeOut);
			toastElement.BeginAnimation(System.Windows.UIElement.OpacityProperty, fadeIn);
		}
	}
}