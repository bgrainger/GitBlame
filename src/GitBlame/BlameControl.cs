
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
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
			m_visual = new DrawingVisual();
			AddVisualChild(m_visual);

			m_personBrush = new Dictionary<Person, Brush>();
		}

		internal void SetBlameResult(BlameResult blame)
		{
			m_blame = blame;
			m_columnWidths = new[] { 200, c_marginWidth, 0 };
			m_lineCount = blame.Blocks.Sum(b => b.LineCount);
			SetVerticalScrollInfo(m_lineCount + 1, null, null);

			m_oldestCommit = blame.Commits.Min(c => c.AuthorDate);
			DateTimeOffset newestCommit = blame.Commits.Max(c => c.AuthorDate);
			DateTimeOffset now = DateTimeOffset.Now;
			m_dateScale = 0.65 / (newestCommit - m_oldestCommit).TotalDays;

			CreateBrushesForAuthors();
			RedrawSoon();
		}

		protected override Size ArrangeOverride(Size finalSize)
		{
			SetVerticalScrollInfo(null, finalSize.Height / m_lineHeight, null);
			RedrawSoon();
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

		protected override Visual GetVisualChild(int index)
		{
			if (index != 0)
				throw new ArgumentOutOfRangeException("index");

			return m_visual;
		}

		protected override int VisualChildrenCount
		{
			get { return 1; }
		}

		protected override void OnScrollChanged()
		{
			m_topLineIndex = (int) VerticalOffset;
			Render();
		}

		private void Render()
		{
			if (m_blame == null)
				return;

			using (DrawingContext drawingContext = m_visual.RenderOpen())
				Render(drawingContext);
		}

		private void Render(DrawingContext drawingContext)
		{
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

			double blockWidth = m_columnWidths[0];
			double codeXOffset = m_columnWidths[0] + m_columnWidths[1];
			double codeWidth = m_columnWidths[2];

			// draw all visible blocks
			double yOffset = -topBlockOffset * m_lineHeight;
			int lineIndex = m_topLineIndex;
			do
			{
				Block block = m_blame.Blocks[blockIndex];

				double height = block.LineCount * m_lineHeight;
				Rect rectangle = new Rect(0, yOffset, blockWidth, height);

				// create a colour that depends on the commit ID and its age
				double alpha = (block.Commit.CommitDate - m_oldestCommit).TotalDays * m_dateScale + 0.1;

				drawingContext.PushOpacity(alpha);
				drawingContext.DrawRectangle(m_personBrush[block.Commit.Author], null, rectangle);
				drawingContext.Pop();

				double textY = Math.Max(rectangle.Top, 0) + 1;

				FormattedText authorText = CreateSmallFormattedText(block.Commit.Author.Name, typeface, 110);
				drawingContext.DrawText(authorText, new Point(1, textY));

				string commitDate = block.Commit.AuthorDate.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
				FormattedText dateText = CreateSmallFormattedText(commitDate, typeface, 85);
				dateText.TextAlignment = TextAlignment.Right;
				drawingContext.DrawText(dateText, new Point(114, textY));

				int commitLineCount = (int) ((rectangle.Bottom - textY - m_lineHeight) / authorText.Height);
				if (commitLineCount > 0)
				{
					// allow line breaks in "Long.Dotted.Identifiers" by inserting a zero-width space
					string summary = Regex.Replace(block.Commit.Summary, @"\.([A-Z, ])", ".\u200B$1");

					FormattedText commitText = CreateSmallFormattedText(summary, typeface, 198);
					commitText.MaxLineCount = commitLineCount;
					commitText.Trimming = TextTrimming.WordEllipsis;
					drawingContext.DrawText(commitText, new Point(1, textY + m_lineHeight));
				}

				drawingContext.DrawLine(new Pen(Brushes.LightGray, 1), new Point(0, rectangle.Bottom + 0.5), new Point(RenderSize.Width, rectangle.Bottom + 0.5));

				Geometry clipGeometry = new RectangleGeometry(new Rect(codeXOffset, 0, RenderSize.Width - codeXOffset, RenderSize.Height));
				drawingContext.PushClip(clipGeometry);
				for (int l = 0; l < block.LineCount; l++)
				{
					FormattedText text = CreateFormattedText(m_blame.Lines[block.StartLine + l - 1], typeface);
					drawingContext.DrawText(text, new Point(codeXOffset - HorizontalOffset, yOffset + l * m_lineHeight));
					codeWidth = Math.Max(codeWidth, text.Width + c_marginWidth);
				}
				drawingContext.Pop();

				blockIndex++;
				yOffset += height;
				lineCount += block.LineCount;
			} while (yOffset < RenderSize.Height && lineCount < m_lineCount);

			drawingContext.DrawLine(new Pen(Brushes.DarkGray, 1), new Point(blockWidth, 0), new Point(blockWidth, Math.Min(yOffset, RenderSize.Height)));

			m_columnWidths[2] = codeWidth;
			SetHorizontalScrollInfo(m_columnWidths.Sum(), RenderSize.Width, null);
		}

		private void RedrawSoon()
		{
			RedrawSoon(DispatcherPriority.Render);
		}

		private void RedrawSoon(DispatcherPriority priority)
		{
			Dispatcher.BeginInvoke(priority, new SendOrPostCallback(delegate { Render(); }), null);
		}

		private FormattedText CreateFormattedText(string text, Typeface typeface)
		{
			return new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, m_emSize, Brushes.Black);
		}

		private FormattedText CreateSmallFormattedText(string text, Typeface typeface, double maxWidth)
		{
			FormattedText formattedText = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, m_emSize * 0.75, Brushes.Black);
			formattedText.MaxTextWidth = maxWidth;
			formattedText.Trimming = TextTrimming.CharacterEllipsis;
			formattedText.MaxLineCount = 1;
			return formattedText;
		}

		private void CreateBrushesForAuthors()
		{
			// create brushes by author frequency
			foreach (Person author in m_blame.Blocks.Select(b => b.Commit).Distinct().GroupBy(c => c.Author).OrderByDescending(g => g.Count()).Select(g => g.Key))
			{
				if (!m_personBrush.ContainsKey(author))
				{
					Color color = m_colors[m_currentColor];
					Brush brush;
					switch (m_currentPattern)
					{
					case 0:
						brush = new SolidColorBrush(color);
						break;

					default:
						// TODO: create more pattern brushes
						brush = CreateLeftDiagonalBrush(color);
						break;
					}

					brush.Freeze();
					m_personBrush.Add(author, brush);

					if (++m_currentColor == m_colors.Length)
					{
						m_currentColor = 0;
						m_currentPattern++;
					}
				}
			}
		}

		private static Brush CreateLeftDiagonalBrush(Color color)
		{
			// TODO: Move to XAML.
			PathFigure figure1 = new PathFigure
			{
				IsClosed = true,
				StartPoint = new Point(5, 0),
				Segments =
				{ 
					new PolyLineSegment
					{
						Points =
						{
							new Point(10, 0),
							new Point(0, 10),
							new Point(0, 5),
						},
					}
				},
			};

			PathFigure figure2 = new PathFigure
			{
				IsClosed = true,
				StartPoint = new Point(10, 5),
				Segments =
				{
					new PolyLineSegment
					{
						Points =
						{
							new Point(10, 10),
							new Point(5, 10),
						},
					},
				},
			};

			GeometryDrawing drawing = new GeometryDrawing
			{
				Geometry = new PathGeometry
				{
					Figures =
					{
						figure1,
						figure2,
					},
				},
				Brush = new SolidColorBrush(color),
			};

			return new DrawingBrush
			{
				Drawing = drawing,
				TileMode = TileMode.Tile,
				Viewport = new Rect(0, 0, 10, 10),
				ViewportUnits = BrushMappingMode.Absolute,
			};
		}

		// TODO: Get nice color palette.
		static readonly Color[] m_colors = new[]
		{
			Color.FromRgb(255, 0, 0),
			Color.FromRgb(255, 127, 0),
			Color.FromRgb(255, 255, 0),
			Color.FromRgb(127, 255, 0),
			Color.FromRgb(0, 255, 0),
			Color.FromRgb(0, 255, 127),
			Color.FromRgb(0, 255, 255),
			Color.FromRgb(0, 127, 255),
			Color.FromRgb(0, 0, 255),
			Color.FromRgb(127, 0, 255),
			Color.FromRgb(255, 0, 255),
			Color.FromRgb(255, 0, 127),
		};

		const double c_marginWidth = 10;

		readonly DrawingVisual m_visual;
		BlameResult m_blame;
		double[] m_columnWidths;
		int m_lineCount;
		int m_topLineIndex;
		double m_emSize;
		double m_lineHeight;

		Dictionary<Person, Brush> m_personBrush;
		int m_currentColor;
		int m_currentPattern;

		DateTimeOffset m_oldestCommit;
		double m_dateScale;
	}
}
