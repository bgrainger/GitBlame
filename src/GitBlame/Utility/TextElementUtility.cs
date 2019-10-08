using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace GitBlame.Utility
{
	/// <summary>
	/// Provides helper methods for working with <see cref="TextElement"/>.
	/// </summary>
	public static class TextElementUtility
	{
		/// <summary>
		/// Returns a typeface based on the <c>FontFamily</c>, <c>FontStyle</c>, <c>FontWeight</c>, and <c>FontStretch</c>
		/// attached properties of the specified dependency object.
		/// </summary>
		/// <param name="dependencyObject">The <see cref="DependencyObject"/> for which to create a <see cref="Typeface"/>.</param>
		/// <returns>A <see cref="Typeface"/> based on the attached properties of <paramref name="dependencyObject"/>.</returns>
		public static Typeface GetTypeface(DependencyObject dependencyObject)
		{
			FontFamily fontFamily = TextElement.GetFontFamily(dependencyObject);
			FontStyle fontStyle = TextElement.GetFontStyle(dependencyObject);
			FontWeight fontWeight = TextElement.GetFontWeight(dependencyObject);
			FontStretch fontStretch = TextElement.GetFontStretch(dependencyObject);
			return new Typeface(fontFamily, fontStyle, fontWeight, fontStretch);
		}
	}
}
