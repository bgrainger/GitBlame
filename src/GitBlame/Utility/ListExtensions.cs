using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace GitBlame.Utility
{
	/// <summary>
	/// Methods for manipulating lists.
	/// </summary>
	public static class ListExtensions
	{
		/// <summary>
		/// Wraps a <see cref="ReadOnlyCollection{T}"/> around the specified list.
		/// </summary>
		/// <typeparam name="T">The type of item in the list.</typeparam>
		/// <param name="list">The list.</param>
		/// <returns>A <see cref="ReadOnlyCollection{T}"/> that wraps the list.</returns>
		public static ReadOnlyCollection<T> AsReadOnly<T>(this IList<T> list) => new ReadOnlyCollection<T>(list);
	}
}
