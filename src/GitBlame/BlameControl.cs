
using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using GitBlame.Models;
using GitBlame.Utility;

namespace GitBlame
{
	public sealed class BlameControl : ScrollableControl
	{
		public BlameControl()
			: base(12, 1)
		{
		}

		internal void SetBlameResult(BlameResult blame)
		{
			m_blame = blame;
			m_lineCount = blame.Blocks.Sum(b => b.LineCount);
			SetVerticalScrollInfo(m_lineCount, null, null);
		}

		protected override Size ArrangeOverride(Size finalSize)
		{
			return finalSize;
		}

		protected override Size MeasureOverride(Size availableSize)
		{
			return new Size(availableSize.Width, m_lineCount * 20);
		}

		protected override void OnScrollChanged()
		{
			m_topLineIndex = (int) VerticalOffset;
			RedrawSoon();
		}

		protected override void OnRender(DrawingContext drawingContext)
		{
			if (m_blame == null)
				return;

			SetHorizontalScrollInfo(200, RenderSize.Width, null);

			// calculate first block that is displayed
			int lineCount = 0;
			int blockIndex = 0;
			for (; blockIndex < m_blame.Blocks.Count; blockIndex++)
			{
				lineCount += m_blame.Blocks[blockIndex].LineCount;
				if (lineCount > m_topLineIndex)
					break;
			}

			// calculate offset into first block being displayed
			const int lineHeight = 20;
			lineCount -= m_blame.Blocks[blockIndex].LineCount;
			int topBlockOffset = m_topLineIndex - lineCount;

			// draw all visible blocks
			double yOffset = -topBlockOffset * lineHeight;
			int lineIndex = m_topLineIndex;
			do
			{
				Block block = m_blame.Blocks[blockIndex];

				double height = block.LineCount * lineHeight;
				Rect rectangle = new Rect(0, yOffset, 200, height);

				// create a colour that depends on the commit ID and its age
				int alpha = 255 - (int) ((DateTimeOffset.Now - block.Commit.CommitDate).TotalDays / 10.0);
				int red = int.Parse(block.Commit.Id.Substring(0, 2), NumberStyles.HexNumber);
				int green = int.Parse(block.Commit.Id.Substring(2, 2), NumberStyles.HexNumber);
				int blue = int.Parse(block.Commit.Id.Substring(4, 2), NumberStyles.HexNumber);

				drawingContext.DrawRectangle(new SolidColorBrush(Color.FromArgb((byte) alpha, (byte) red, (byte) green, (byte) blue)), new Pen(Brushes.Black, 1), rectangle);

				blockIndex++;
				yOffset += height;
				lineCount += block.LineCount;
			} while (yOffset < RenderSize.Height && lineCount < m_lineCount);

			SetVerticalScrollInfo(null, yOffset / lineHeight, null);
		}

		private Size GetViewportSize()
		{
			return new Size(GetViewportWidth(), GetViewportHeight());
		}

		private double GetViewportWidth()
		{
			return ((IScrollInfo) this).ViewportWidth;
		}

		private double GetViewportHeight()
		{
			return ((IScrollInfo) this).ViewportHeight;
		}

		private void RedrawSoon()
		{
			RedrawSoon(DispatcherPriority.Render);
		}

		private void RedrawSoon(DispatcherPriority priority)
		{
			Dispatcher.BeginInvoke(priority, new SendOrPostCallback(delegate { Redraw(); }), null);
		}

		private void Redraw()
		{
			InvalidateVisual();
		}

		BlameResult m_blame;
		int m_lineCount;
		int m_topLineIndex;
	}
}
