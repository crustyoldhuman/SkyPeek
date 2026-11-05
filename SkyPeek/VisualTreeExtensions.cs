using System.Windows;
using System.Windows.Media;

namespace SkyPeek
{
	public static class VisualTreeExtensions
	{
		public static T? FindVisualChild<T>(this DependencyObject parent) where T : DependencyObject
		{
			if (parent == null) return null;
			for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
			{
				var child = VisualTreeHelper.GetChild(parent, i);
				if (child is T typedChild) return typedChild;
				var childOfChild = child.FindVisualChild<T>();
				if (childOfChild != null) return childOfChild;
			}
			return null;
		}

		public static T? FindVisualParent<T>(this DependencyObject child) where T : DependencyObject
		{
			DependencyObject? parentObject = child is Visual || child is System.Windows.Media.Media3D.Visual3D
				? VisualTreeHelper.GetParent(child)
				: LogicalTreeHelper.GetParent(child);
			if (parentObject == null) return null;
			if (parentObject is T parent) return parent;
			return parentObject.FindVisualParent<T>();
		}
	}
}