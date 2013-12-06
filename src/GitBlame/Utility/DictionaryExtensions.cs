using System.Collections.Generic;

namespace GitBlame.Utility
{
	public static class DictionaryExtensions
	{
		public static TValue GetValueOrDefault<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> dictionary, TKey key)
		{
			TValue value;
			dictionary.TryGetValue(key, out value);
			return value;
		}
	}
}
