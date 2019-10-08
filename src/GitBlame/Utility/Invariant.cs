using System;

namespace GitBlame.Utility
{
	internal static class Invariant
	{
		/// <summary>
		/// Verifies that <paramref name="condition"/> is <c>true</c>, and terminates the current process if not.
		/// </summary>
		/// <param name="condition">The condition to test.</param>
		/// <param name="message">The error message if <paramref name="condition"/> is false.</param>
		public static void Assert(bool condition, string message)
		{
			if (!condition)
				Environment.FailFast(message);
		}
	}
}
