using System.Windows;

namespace GitBlame.Utility
{
	/// <summary>
	/// Provides helper methods for working with <see cref="DependencyProperty"/>.
	/// </summary>
	public static class DependencyPropertyUtility
	{
		/// <summary>
		/// Registers the specified property.
		/// </summary>
		/// <typeparam name="TProperty">The type of the property.</typeparam>
		/// <typeparam name="TOwner">The type of the owner.</typeparam>
		/// <param name="name">The name of the dependency property to register.</param>
		public static DependencyProperty Register<TProperty, TOwner>(string name)
			where TOwner : DependencyObject
		{
			return DependencyProperty.Register(name, typeof(TProperty), typeof(TOwner));
		}
	}
}
