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

namespace SkyPeek
{
	public static class LottieScrollBehavior
	{
		public static readonly DependencyProperty TrackWithProperty = DependencyProperty.RegisterAttached(
			"TrackWith", typeof(ScrollViewer), typeof(LottieScrollBehavior), new PropertyMetadata(null, OnTrackWithChanged));

		private static readonly DependencyProperty ScrollHandlerProperty = DependencyProperty.RegisterAttached(
			"ScrollHandler", typeof(System.EventHandler<ScrollChangedEventArgs>), typeof(LottieScrollBehavior));

		public static void SetTrackWith(UIElement element, ScrollViewer value) => element.SetValue(TrackWithProperty, value);
		public static ScrollViewer GetTrackWith(UIElement element) => (ScrollViewer)element.GetValue(TrackWithProperty);

		private static void OnTrackWithChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			if (d is LottieSharp.WPF.LottieAnimationView lottie)
			{
				if (e.OldValue is ScrollViewer oldScroll)
				{
					if (lottie.GetValue(ScrollHandlerProperty) is System.EventHandler<ScrollChangedEventArgs> oldHandler)
					{
						WeakEventManager<ScrollViewer, ScrollChangedEventArgs>.RemoveHandler(oldScroll, "ScrollChanged", oldHandler);
					}
					lottie.ClearValue(ScrollHandlerProperty);
					lottie.IsVisibleChanged -= Lottie_IsVisibleChanged;
				}

				if (e.NewValue is ScrollViewer newScroll)
				{
					System.EventHandler<ScrollChangedEventArgs> newHandler = (s, ev) => Evaluate(lottie, newScroll);

					// Store handler strongly in the lottie view so WeakEventManager doesn't immediately garbage collect it
					lottie.SetValue(ScrollHandlerProperty, newHandler);

					// Register weakly to the scroll viewer to prevent memory leaks when UI updates while hidden
					WeakEventManager<ScrollViewer, ScrollChangedEventArgs>.AddHandler(newScroll, "ScrollChanged", newHandler);

					lottie.IsVisibleChanged -= Lottie_IsVisibleChanged;
					lottie.IsVisibleChanged += Lottie_IsVisibleChanged;

					lottie.Dispatcher.BeginInvoke(new System.Action(() => Evaluate(lottie, newScroll)), System.Windows.Threading.DispatcherPriority.Loaded);
				}
			}
		}

		private static void Lottie_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
		{
			if (sender is LottieSharp.WPF.LottieAnimationView lottie && GetTrackWith(lottie) is ScrollViewer scroller)
			{
				if ((bool)e.NewValue)
				{
					lottie.Dispatcher.BeginInvoke(new System.Action(() => Evaluate(lottie, scroller)), System.Windows.Threading.DispatcherPriority.Loaded);
				}
				else
				{
					Evaluate(lottie, scroller);
				}
			}
		}

		private static void Evaluate(LottieSharp.WPF.LottieAnimationView lottie, ScrollViewer scroller)
		{
			if (!lottie.IsLoaded || !lottie.IsVisible || scroller.Visibility != Visibility.Visible)
			{
				lottie.StopAnimation();
				return;
			}

			try
			{
				GeneralTransform transform = lottie.TransformToAncestor(scroller);
				Rect bounds = transform.TransformBounds(new Rect(0, 0, lottie.ActualWidth, lottie.ActualHeight));
				Rect viewport = new(0, 0, scroller.ViewportWidth, scroller.ViewportHeight);
				viewport.Inflate(scroller.ViewportWidth * 1.5, scroller.ViewportHeight * 1.5);

				if (viewport.IntersectsWith(bounds))
				{
					if (!lottie.IsPlaying) lottie.PlayAnimation();
				}
				else
				{
					if (lottie.IsPlaying) lottie.StopAnimation();
				}
			}
			catch (System.InvalidOperationException)
			{
				if (lottie.IsPlaying) lottie.StopAnimation();
			}
		}
	}
}