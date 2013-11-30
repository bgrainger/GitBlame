using System.Windows.Data;

namespace GitBlame.Converters
{
	public static class Converters
	{
		public static readonly IValueConverter NullableToVisibility = new NullableToVisibilityConverter();
	}
}
