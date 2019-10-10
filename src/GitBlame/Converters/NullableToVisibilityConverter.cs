using System;
using System.Windows;
using System.Windows.Data;

namespace GitBlame.Converters
{
	public sealed class NullableToVisibilityConverter : IValueConverter
	{
		public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
			value is object ? Visibility.Visible : Visibility.Collapsed;

		public object? ConvertBack(object value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => null;
	}
}
