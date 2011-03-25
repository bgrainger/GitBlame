
using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using GitBlame.Models;
using GitBlame.Utility;
using Block = GitBlame.Models.Block;

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
			SetVerticalScrollInfo(m_lineCount + 1, null, null);

			m_oldestCommit = blame.Commits.Min(c => c.AuthorDate);
			DateTimeOffset newestCommit = blame.Commits.Max(c => c.AuthorDate);
			DateTimeOffset now = DateTimeOffset.Now;
			m_dateScale = 0.65 / (newestCommit - m_oldestCommit).TotalDays;
		}

		protected override Size ArrangeOverride(Size finalSize)
		{
			SetVerticalScrollInfo(null, finalSize.Height / m_lineHeight, null);
			return finalSize;
		}

		protected override Size MeasureOverride(Size availableSize)
		{
			Typeface typeface = TextElementUtility.GetTypeface(this);
			m_emSize = Math.Max(TextElement.GetFontSize(this), 10.0 * 4 / 3);

			FormattedText text = CreateFormattedText("using System;", typeface);
			m_lineHeight = text.Height;

			return availableSize;
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

			Typeface typeface = TextElementUtility.GetTypeface(this);

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
			lineCount -= m_blame.Blocks[blockIndex].LineCount;
			int topBlockOffset = m_topLineIndex - lineCount;

			// draw all visible blocks
			double yOffset = -topBlockOffset * m_lineHeight;
			int lineIndex = m_topLineIndex;
			do
			{
				Block block = m_blame.Blocks[blockIndex];

				double height = block.LineCount * m_lineHeight;
				Rect rectangle = new Rect(0, yOffset, 200, height);

				// create a colour that depends on the commit ID and its age
				int alpha = 255 - (int) ((DateTimeOffset.Now - block.Commit.CommitDate).TotalDays / 10.0);
				int red = int.Parse(block.Commit.Id.Substring(0, 2), NumberStyles.HexNumber);
				int green = int.Parse(block.Commit.Id.Substring(2, 2), NumberStyles.HexNumber);
				int blue = int.Parse(block.Commit.Id.Substring(4, 2), NumberStyles.HexNumber);

				drawingContext.DrawRectangle(new SolidColorBrush(Color.FromArgb((byte) alpha, (byte) red, (byte) green, (byte) blue)), null, rectangle);
				drawingContext.DrawLine(new Pen(Brushes.LightGray, 1), new Point(0, rectangle.Bottom + 0.5), new Point(RenderSize.Width, rectangle.Bottom + 0.5));

				for (int l = 0; l < block.LineCount; l++)
				{
					FormattedText text = CreateFormattedText(m_blame.Lines[block.StartLine + l - 1], typeface);
					drawingContext.DrawText(text, new Point(210, yOffset + l * m_lineHeight));
				}

				blockIndex++;
				yOffset += height;
				lineCount += block.LineCount;
			} while (yOffset < RenderSize.Height && lineCount < m_lineCount);

			drawingContext.DrawLine(new Pen(Brushes.DarkGray, 1), new Point(blockWidth, 0), new Point(blockWidth, Math.Min(yOffset, RenderSize.Height)));

			SetHorizontalScrollInfo(200, RenderSize.Width, null);
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

		private FormattedText CreateFormattedText(string text, Typeface typeface)
		{
			return new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, m_emSize, Brushes.Black);
		}

		BlameResult m_blame;
		int m_lineCount;
		int m_topLineIndex;
		double m_emSize;
		double m_lineHeight;
	}
}
