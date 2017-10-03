using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace YouTubeTransportControl
{
	public static class Utils
	{
		/// <summary>
		/// Method used to limit a number to specified range.
		/// </summary>
		/// <param name="value">The value to limit.</param>
		/// <param name="min">The inclusive minimum.</param>
		/// <param name="max">The inclusive maximum.</param>
		/// <returns>The limited value.</returns>
		public static int Clamp(this int value, int min, int max)
		{
			return (value < min) ? min : (value > max) ? max : value;
		}

		/// <summary>
		/// Method used to limit a number to specified range.
		/// </summary>
		/// <param name="value">The value to limit.</param>
		/// <param name="min">The inclusive minimum.</param>
		/// <param name="max">The inclusive maximum.</param>
		/// <returns>The limited value.</returns>
		public static double Clamp(this double value, double min, double max)
		{
			return (value < min) ? min : (value > max) ? max : value;
		}

		/// <summary>
		/// Method used to limit a TimeSpan to specified range.
		/// </summary>
		/// <param name="value">The value to limit.</param>
		/// <param name="min">The inclusive minimum.</param>
		/// <param name="max">The inclusive maximum.</param>
		/// <returns>The limited value.</returns>
		public static TimeSpan Clamp(this TimeSpan value, TimeSpan min, TimeSpan max)
		{
			return (value < min) ? min : (value > max) ? max : value;
		}


		#region WPF Helpers.
		/// <summary>
		/// Find the parent of the framework element.
		/// </summary>
		/// <typeparam name="T">The type.</typeparam>
		/// <param name="child">The child.</param>
		public static T FindParent<T>(FrameworkElement child) where T : DependencyObject
		{
			if (child == null)
				return null;

			T parent = null;
			var currentParent = VisualTreeHelper.GetParent(child);
			while (currentParent != null)
			{
				if (currentParent is T)
				{
					parent = (T)currentParent;
					break;
				}
				currentParent = VisualTreeHelper.GetParent(currentParent);
			}
			return parent;
		}

		/// <summary>
		/// Returns an image from a url.
		/// </summary>
		/// <param name="url"></param>
		/// <returns></returns>
		public static Image GetImageFromUrl(string url)
		{
			BitmapImage logo = new BitmapImage();
			logo.BeginInit();
			logo.UriSource = new Uri(url);
			logo.EndInit();

			Image image = new Image();
			image.Width = image.Height = 16;
			image.Source = logo;
			return image;
		}

		public static void ResetAnimation(this FrameworkElement element, params DependencyProperty[] properties)
		{
			foreach (var p in properties)
				element.BeginAnimation(p, null);
		}
		#endregion // WPF Helpers.
	}
}
