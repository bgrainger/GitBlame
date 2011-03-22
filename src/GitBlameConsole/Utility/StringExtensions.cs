
using System;

namespace GitBlameConsole.Utility
{
	public static class StringExtensions
	{
		public static string WithLength(this string value, int length)
		{
			if (value == null)
				throw new ArgumentNullException("value");

			if (value.Length < length)
				return value + new string(' ', length - value.Length);
			else if (value.Length == length)
				return value;
			else
				return value.Substring(0, length);
		}
	}
}
