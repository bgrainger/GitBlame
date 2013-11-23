using System;
using System.Globalization;
using JetBrains.Annotations;

namespace GitBlame.Utility
{
	public static class StringExtensions
	{
		/// <summary>
		/// Formats the string using the invariant culture.
		/// </summary>
		/// <param name="format">The format string.</param>
		/// <param name="args">The format arguments.</param>
		/// <returns>The formatted string.</returns>
		[StringFormatMethod("format")]
		public static string FormatInvariant(this string format, params object[] args)
		{
			return string.Format(CultureInfo.InvariantCulture, format, args);
		}

		/// <summary>
		/// Splits the given string on the first space (if any) and returns the two parts.
		/// </summary>
		/// <param name="value">The string to split.</param>
		/// <returns>The two parts of the string (before and after the first space).</returns>
		/// <remarks>The space is not included in the split string. If <paramref name="value"/> does not contain a space,
		/// <c>Item1</c> will be set to <paramref name="value"/> and <c>Item2</c> will be <c>null</c>.</remarks>
		public static Tuple<string, string> SplitOnSpace(this string value)
		{
			int spaceIndex = value.IndexOf(' ');
			if (spaceIndex >= 0)
			{
				string first = value.Substring(0, spaceIndex);
				string second = value.Substring(spaceIndex + 1);
				return Tuple.Create(first, second);
			}
			else
			{
				return Tuple.Create(value, default(string));
			}
		}

		/// <summary>
		/// Returns a string with the contents of <paramref name="value"/> that has a <see cref="String.Length"/> of <paramref name="length"/>.
		/// </summary>
		/// <param name="value">The string.</param>
		/// <param name="length">The desired length of the returned string.</param>
		/// <returns>A new string with a length of <paramref name="length"/>.</returns>
		/// <remarks>If <paramref name="value"/> is longer than <paramref name="length"/>, it is truncated; otherwise,
		/// if <paramref name="value"/> is shorter than <paramref name="length"/>, it is padded on the right with spaces.</remarks>
		public static string WithLength(this string value, int length)
		{
			if (value == null)
				throw new ArgumentNullException("value");
			if (length < 0)
				throw new ArgumentOutOfRangeException("length");

			if (value.Length < length)
				return value + new string(' ', length - value.Length);
			else if (value.Length == length)
				return value;
			else
				return value.Substring(0, length);
		}
	}
}
