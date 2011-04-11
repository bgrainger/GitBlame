
using System.Collections.Generic;

namespace GitBlame.Utility
{
	/// <summary>
	/// Helper methods for working with <c>IEnumerable</c>.
	/// </summary>
	public static class EnumerableExtensions
	{
		/// <summary>
		/// Creates a dictionary from key value pairs.
		/// </summary>
		/// <typeparam name="TKey">The type of the key.</typeparam>
		/// <typeparam name="TValue">The type of the value.</typeparam>
		/// <param name="pairs">The key value pairs.</param>
		/// <returns>The dictionary.</returns>
		public static Dictionary<TKey, TValue> ToDictionary<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> pairs)
		{
			var dict = new Dictionary<TKey, TValue>();
			foreach (var pair in pairs)
				dict.Add(pair.Key, pair.Value);
			return dict;
		}
	}
}
