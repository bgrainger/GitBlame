using System;
using System.Globalization;

namespace GitBlame.Utility
{
	/// <summary>
	/// Provides methods for manipulating dates.
	/// </summary>
	public static class DateTimeOffsetExtensions
	{
		/// <summary>
		/// Converts the specified ISO 8601 representation of a date and time to its DateTimeOffset equivalent.
		/// </summary>
		/// <param name="value">The ISO 8601 string representation to parse.</param>
		/// <returns>The DateTimeOffset equivalent.</returns>
		public static DateTimeOffset ParseIso8601(string value)
		{
			return DateTimeOffset.ParseExact(value, Iso8601Format, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
		}

		/// <summary>
		/// Converts the specified ISO 8601 representation of a date and time to its DateTimeOffset equivalent.
		/// </summary>
		/// <param name="value">The ISO 8601 string representation to parse.</param>
		/// <param name="result">The DateTimeOffset equivalent.</param>
		/// <returns>True if successful.</returns>
		public static bool TryParseIso8601(string value, out DateTimeOffset result)
		{
			return DateTimeOffset.TryParseExact(value, Iso8601Format, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out result);
		}

		/// <summary>
		/// Formats the date in the standard ISO 8601 format.
		/// </summary>
		/// <param name="value">The date to format.</param>
		/// <returns>The formatted date.</returns>
		public static string ToIso8601(this DateTimeOffset value)
		{
			return value.ToString(Iso8601Format, CultureInfo.InvariantCulture);
		}

		/// <summary>
		/// The ISO 8601 format string.
		/// </summary>
		public const string Iso8601Format = "yyyy'-'MM'-'dd'T'HH':'mm':'sszzz";
	}
}
