
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
		/// <param name="obj">The <see cref="DependencyObject"/> for which to create a <see cref="Typeface"/>.</param>
		/// <returns>A <see cref="Typeface"/> based on the attached properties of <paramref name="obj"/>.</returns>
		public static Typeface GetTypeface(DependencyObject obj)
		{
			FontFamily fontFamily = TextElement.GetFontFamily(obj);
			FontStyle fontStyle = TextElement.GetFontStyle(obj);
			FontWeight fontWeight = TextElement.GetFontWeight(obj);
			FontStretch fontStretch = TextElement.GetFontStretch(obj);
			return new Typeface(fontFamily, fontStyle, fontWeight, fontStretch);
		}
	}
}
