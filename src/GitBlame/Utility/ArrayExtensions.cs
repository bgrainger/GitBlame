
namespace GitBlame.Utility
{
	/// <summary>
	/// Helper methods for working with arrays.
	/// </summary>
	public static class ArrayExtensions
	{
		/// <summary>
		/// Clones the specified array.
		/// </summary>
		/// <param name="array">The array to clone.</param>
		/// <returns>A clone of the specified array.</returns>
		/// <remarks>This method is merely useful in avoiding the cast that is otherwise necessary
		/// when calling <see cref="System.Array.Clone" />.</remarks>
		public static T[] Clone<T>(T[] array)
		{
			return (T[]) array.Clone();
		}
	}
}
