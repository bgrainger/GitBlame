using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace GitBlame.Utility
{
#pragma warning disable CA1033
    /// <summary>
    /// The <see cref="ScrollableControl"/> class provides a basic implementation of a <see cref="Control"/>
    /// that supports custom scrolling through <see cref="IScrollInfo"/>.
    /// </summary>
    public abstract class ScrollableControl : Control, IScrollInfo
	{
		void IScrollInfo.LineUp() => AdjustVerticalOffset(-m_verticalLineSize);
		void IScrollInfo.LineDown() => AdjustVerticalOffset(m_verticalLineSize);
		void IScrollInfo.LineLeft() => AdjustHorizontalOffset(-m_horizontalLineSize);
		void IScrollInfo.LineRight() => AdjustHorizontalOffset(m_horizontalLineSize);
		void IScrollInfo.PageUp() => AdjustVerticalOffset(-m_viewportHeight);
		void IScrollInfo.PageDown() => AdjustVerticalOffset(m_viewportHeight);
		void IScrollInfo.PageLeft() => AdjustHorizontalOffset(-m_viewportWidth);
		void IScrollInfo.PageRight() => AdjustHorizontalOffset(m_viewportWidth);
		void IScrollInfo.MouseWheelUp() => AdjustVerticalOffset(-m_verticalLineSize * SystemParameters.WheelScrollLines);
		void IScrollInfo.MouseWheelDown() => AdjustVerticalOffset(m_verticalLineSize * SystemParameters.WheelScrollLines);
		void IScrollInfo.MouseWheelLeft() => AdjustHorizontalOffset(-m_horizontalLineSize * SystemParameters.WheelScrollLines);
		void IScrollInfo.MouseWheelRight() => AdjustHorizontalOffset(m_horizontalLineSize * SystemParameters.WheelScrollLines);

		/// <summary>
		/// Sets the horizontal offset.
		/// </summary>
		/// <param name="fHorizontalOffset">The horizontal offset.</param>
		public void SetHorizontalOffset(double fHorizontalOffset) => SetHorizontalScrollInfo(null, null, fHorizontalOffset);

		/// <summary>
		/// Sets the vertical offset.
		/// </summary>
		/// <param name="fVerticalOffset">The vertical offset.</param>
		public void SetVerticalOffset(double fVerticalOffset) => SetVerticalScrollInfo(null, null, fVerticalOffset);

		Rect IScrollInfo.MakeVisible(Visual visual, Rect rectangle) => throw new NotSupportedException();
		bool IScrollInfo.CanVerticallyScroll { get; set; }
		bool IScrollInfo.CanHorizontallyScroll { get; set; }
		double IScrollInfo.ExtentWidth => m_extentWidth;
		double IScrollInfo.ExtentHeight => m_extentHeight;
		double IScrollInfo.ViewportWidth => m_viewportWidth;
		double IScrollInfo.ViewportHeight => m_viewportHeight;

		/// <summary>
		/// Gets the horizontal offset of the scrolled content.
		/// </summary>
		/// <returns>The horizontal offset. This property has no default value.</returns>
		public double HorizontalOffset => m_horizontalOffset;

		/// <summary>
		/// Gets the vertical offset of the scrolled content.
		/// </summary>
		/// <returns>The vertical offset of the scrolled content. Valid values are between zero and the
		/// <see cref="IScrollInfo.ExtentHeight"/> minus the <see cref="IScrollInfo.ViewportHeight"/>. This property has no default value.</returns>
		public double VerticalOffset => m_verticalOffset;

		ScrollViewer IScrollInfo.ScrollOwner { get; set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="ScrollableControl"/> class.
		/// </summary>
		protected ScrollableControl()
			: this(12, 12)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ScrollableControl"/> class.
		/// </summary>
		/// <param name="horizontalLineSize">Size of the horizontal line scrolling increment.</param>
		/// <param name="verticalLineSize">Size of the vertical line scrolling increment.</param>
		protected ScrollableControl(double horizontalLineSize, double verticalLineSize)
		{
			m_horizontalLineSize = horizontalLineSize;
			m_verticalLineSize = verticalLineSize;
		}

		/// <summary>
		/// Sets the horizontal line size.
		/// </summary>
		/// <param name="lineSize">Horizontal line size.</param>
		protected void SetHorizontalLineSize(double lineSize) => m_horizontalLineSize = lineSize;

		/// <summary>
		/// Sets the vertical line size.
		/// </summary>
		/// <param name="lineSize">Vertical line size.</param>
		protected void SetVerticalLineSize(double lineSize) => m_verticalLineSize = lineSize;

		/// <summary>
		/// Sets the horizontal scroll info.
		/// </summary>
		/// <param name="extentWidth">Optional extent width.</param>
		/// <param name="viewportWidth">Optional viewport width.</param>
		/// <param name="horizontalOffset">Optional horizontal offset.</param>
		protected void SetHorizontalScrollInfo(double? extentWidth, double? viewportWidth, double? horizontalOffset) =>
			SetScrollInfo(ref m_extentWidth, ref m_viewportWidth, ref m_horizontalOffset, extentWidth, viewportWidth, horizontalOffset);

		/// <summary>
		/// Sets the vertical scroll info.
		/// </summary>
		/// <param name="extentHeight">Optional extent height.</param>
		/// <param name="viewportHeight">Optional viewport height.</param>
		/// <param name="verticalOffset">Optional vertical offset.</param>
		protected void SetVerticalScrollInfo(double? extentHeight, double? viewportHeight, double? verticalOffset) =>
			SetScrollInfo(ref m_extentHeight, ref m_viewportHeight, ref m_verticalOffset, extentHeight, viewportHeight, verticalOffset);

		/// <summary>
		/// Called when the scroll position changes.
		/// </summary>
		protected abstract void OnScrollChanged();

		private void AdjustVerticalOffset(double delta) => SetVerticalOffset(m_verticalOffset + delta);
		private void AdjustHorizontalOffset(double delta) => SetHorizontalOffset(m_horizontalOffset + delta);

		private void SetScrollInfo(ref double extent, ref double viewport, ref double offset, double? newExtent, double? newViewport, double? newOffset)
		{
			// set and constrain new values
			extent = newExtent ?? extent;
			viewport = newViewport ?? viewport;
			double fOldOffset = offset;
			offset = Math.Max(0, Math.Min(newOffset ?? offset, extent - viewport));

			// inform the scroll owner that we have changed
			if (((IScrollInfo) this).ScrollOwner is ScrollViewer scrollViewer)
				scrollViewer.InvalidateScrollInfo();

			// inform the derived class of scroll changes
			if (fOldOffset != offset)
				OnScrollChanged();
		}

		double m_horizontalLineSize;
		double m_verticalLineSize;

		double m_extentWidth;
		double m_extentHeight;
		double m_viewportWidth;
		double m_viewportHeight;
		double m_horizontalOffset;
		double m_verticalOffset;
	}
}

