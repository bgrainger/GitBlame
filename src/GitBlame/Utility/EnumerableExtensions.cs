
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace GitBlame.Utility
{
	/// <summary>
	/// Helper methods for working with <c>IEnumerable</c>.
	/// </summary>
	public static class EnumerableExtensions
	{
		/// <summary>
		/// Groups the elements of a sequence according to a specified key selector function.  Each group consists of <b>consecutive</b> items having the same key. Order is preserved.
		/// </summary>
		/// <typeparam name="TSource">The type of the elements of source.</typeparam>
		/// <typeparam name="TKey">The type of the key returned by keySelector.</typeparam>
		/// <param name="source">A sequence whose elements to group.</param>
		/// <param name="keySelector">A function to extract the key for each element.</param>
		/// <returns>A sequence of IGroupings containing a sequence of objects and a key.</returns>
		public static IEnumerable<IGrouping<TKey, TSource>> GroupConsecutiveBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
		{
			return GroupConsecutiveImpl(source ?? throw new ArgumentNullException(nameof(source)),
				keySelector ?? throw new ArgumentNullException(nameof(keySelector)),
				EqualityComparer<TKey>.Default);

			static IEnumerable<IGrouping<TKey, TSource>> GroupConsecutiveImpl(IEnumerable<TSource> source, Func<TSource, TKey> keySelector, IEqualityComparer<TKey> comparer)
			{
				using IEnumerator<TSource> e = source.GetEnumerator();
				if (!e.MoveNext())
					yield break;

				TKey lastKey = keySelector(e.Current);
				List<TSource> values = new List<TSource> { e.Current };

				while (e.MoveNext())
				{
					TKey currentKey = keySelector(e.Current);
					if (comparer.Equals(lastKey, currentKey))
					{
						values.Add(e.Current);
					}
					else
					{
						yield return new Grouping<TKey, TSource>(lastKey, values.AsReadOnly());
						lastKey = currentKey;
						values = new List<TSource> { e.Current };
					}
				}

				yield return new Grouping<TKey, TSource>(lastKey, values.AsReadOnly());
			}
		}

		/// <summary>
		/// Creates a dictionary from key value pairs.
		/// </summary>
		/// <typeparam name="TKey">The type of the key.</typeparam>
		/// <typeparam name="TValue">The type of the value.</typeparam>
		/// <param name="pairs">The key value pairs.</param>
		/// <returns>The dictionary.</returns>
		public static Dictionary<TKey, TValue> ToDictionary<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> pairs)
			where TKey : struct
		{
			var dict = new Dictionary<TKey, TValue>();
			foreach (var pair in pairs ?? throw new ArgumentNullException(nameof(pairs)))
				dict.Add(pair.Key, pair.Value);
			return dict;
		}

		private class Grouping<TKey, TSource> : IGrouping<TKey, TSource>, ICollection<TSource>
		{
			public Grouping(TKey key, ReadOnlyCollection<TSource> list)
			{
				Key = key;
				m_list = list;
			}

			public IEnumerator<TSource> GetEnumerator() => m_list.GetEnumerator();
			IEnumerator IEnumerable.GetEnumerator() => m_list.GetEnumerator();
			public TKey Key { get; }
			void ICollection<TSource>.Add(TSource item) => throw new NotSupportedException("Collection is read-only.");
			void ICollection<TSource>.Clear() => throw new NotSupportedException("Collection is read-only.");
			bool ICollection<TSource>.Contains(TSource item) => m_list.Contains(item);
			void ICollection<TSource>.CopyTo(TSource[] array, int arrayIndex) => m_list.CopyTo(array, arrayIndex);
			bool ICollection<TSource>.Remove(TSource item) => throw new NotSupportedException("Collection is read-only.");
			public int Count => m_list.Count;
			bool ICollection<TSource>.IsReadOnly => true;

			readonly ReadOnlyCollection<TSource> m_list;
		}
	}
}
