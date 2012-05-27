
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using GitBlame.Layout;
using GitBlame.Models;
using GitBlame.Utility;

namespace GitBlame
{
	public sealed class BlameControl : ScrollableControl
	{
		public BlameControl()
			: base(12, 1)
		{
			m_visual = new DrawingVisual();
			AddVisualChild(m_visual);

			m_personBrush = new Dictionary<int, Brush>();
			m_commitBrush = new Dictionary<string, SolidColorBrush>();
			m_commitAlpha = new Dictionary<string, byte>();
			m_newLineBrush = new SolidColorBrush(Color.FromRgb(108, 226, 108));
			m_newLineBrush.Freeze();
			m_changedTextBrush = new SolidColorBrush(Color.FromRgb(193, 228, 255));
			m_changedTextBrush.Freeze();
		}

		internal void SetBlameResult(BlameResult blame)
		{
			if (m_blame != null)
				m_blame.PropertyChanged -= BlameResult_PropertyChanged;

			double oldLineHeight = m_layout == null ? 1.0 : m_layout.LineHeight;

			m_blame = blame;
			m_layout = new BlameLayout(blame).WithTopLineNumber(1).WithLineHeight(oldLineHeight);
			m_lineCount = blame.Blocks.Sum(b => b.LineCount);
			m_blame.PropertyChanged += BlameResult_PropertyChanged;

			m_hoverCommitId = null;
			m_selectedCommitId = null;
			m_personBrush.Clear();
			m_commitBrush.Clear();
			m_commitAlpha.Clear();
			CreateBrushesForAuthors(m_layout.AuthorCount);

			SetVerticalScrollInfo(m_lineCount + 1, null, 0);
			RedrawSoon();
		}

		protected override Size ArrangeOverride(Size finalSize)
		{
			m_layout = m_layout.WithRenderSize(finalSize);
			SetVerticalScrollInfo(null, finalSize.Height / m_layout.LineHeight, null);
			RedrawSoon();
			return finalSize;
		}

		protected override Size MeasureOverride(Size availableSize)
		{
			Typeface typeface = TextElementUtility.GetTypeface(this);
			m_emSize = Math.Max(TextElement.GetFontSize(this), 10.0 * 4 / 3);

			FormattedText text = CreateFormattedText("8888", typeface);
			m_layout = m_layout.WithLineHeight(text.Height);

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

		protected override void OnMouseMove(MouseEventArgs e)
		{
			string hoverCommitId = GetCommitIdFromPoint(e.GetPosition(this));
			if (hoverCommitId == m_selectedCommitId)
				hoverCommitId = null;
			SetCommitColor(ref m_hoverCommitId, hoverCommitId, m_changedTextBrush.Color);
		}

		protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
		{
			SetCommitColor(ref m_selectedCommitId, GetCommitIdFromPoint(e.GetPosition(this)), Color.FromRgb(79, 178, 255));
			m_hoverCommitId = null;
		}

		private void SetCommitColor(ref string commitId, string newCommitId, Color color)
		{
			if (commitId != null)
				m_commitBrush[commitId].Color = GetCommitColor(commitId);

			if (newCommitId != null)
				m_commitBrush[newCommitId].Color = color;

			commitId = newCommitId;
		}

		private string GetCommitIdFromPoint(Point point)
		{
			return m_layout == null ? null : m_layout.Blocks
				.Where(b => b.CommitPosition.Contains(point))
				.Select(b => b.CommitId)
				.FirstOrDefault();
		}

		protected override void OnScrollChanged()
		{
			m_layout = m_layout.WithTopLineNumber((int) VerticalOffset + 1);
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
			BlameLayout layout = m_layout.WithRenderSize(RenderSize);

			drawingContext.DrawRectangle(Brushes.White, null, new Rect(new Point(), RenderSize));

			foreach (Rect newLineRectangle in layout.NewLines)
				drawingContext.DrawRectangle(m_newLineBrush, null, newLineRectangle);

			foreach (DisplayBlock block in layout.Blocks)
			{
				Rect blockRectangle = block.CommitPosition;

				drawingContext.DrawRectangle(m_personBrush[block.AuthorIndex], null, block.AuthorPosition);
				drawingContext.DrawRectangle(GetOrCreateCommitBrush(block), null, blockRectangle);

				drawingContext.DrawLine(new Pen(Brushes.LightGray, 1), new Point(0, blockRectangle.Bottom + 0.5), new Point(RenderSize.Width, blockRectangle.Bottom + 0.5));

				FormattedText authorText = CreateSmallFormattedText(block.AuthorName, typeface, block.AuthorWidth);
				drawingContext.DrawText(authorText, new Point(block.AuthorX, block.TextY));

				FormattedText dateText = CreateSmallFormattedText(block.Date, typeface, block.DateWidth);
				dateText.TextAlignment = TextAlignment.Right;
				drawingContext.DrawText(dateText, new Point(block.DateX, block.TextY));

				if (block.ShowsSummary)
				{
					Rect summaryPosition = block.SummaryPosition;
					FormattedText commitText = CreateSmallFormattedText(block.Summary, typeface, summaryPosition.Width);
					commitText.MaxLineCount = Math.Max(1, (int) (summaryPosition.Height / commitText.Height));
					commitText.Trimming = TextTrimming.WordEllipsis;
					drawingContext.DrawText(commitText, summaryPosition.TopLeft);
				}
			}

			Column lineNumberColumn = layout.LineNumberColumn;
			double yOffset = 0;
			double lineNumberWidth = lineNumberColumn.Width;
			foreach (DisplayLine line in layout.Lines)
			{
				FormattedText lineNumberText = CreateFormattedText(line.LineNumber.ToString(CultureInfo.InvariantCulture), typeface);
				lineNumberText.TextAlignment = TextAlignment.Right;
				lineNumberText.SetForegroundBrush(Brushes.DarkCyan);
				lineNumberWidth = Math.Max(lineNumberWidth, lineNumberText.Width);
				lineNumberText.MaxTextWidth = lineNumberWidth;
				drawingContext.DrawText(lineNumberText, new Point(lineNumberColumn.Left, yOffset));

				yOffset += layout.LineHeight;
			}
			layout = layout.WithLineNumberWidth(lineNumberWidth);

			Column codeColumn = layout.CodeColumn;
			Geometry clipGeometry = new RectangleGeometry(new Rect(codeColumn.Left, 0, RenderSize.Width - codeColumn.Left, RenderSize.Height));
			drawingContext.PushClip(clipGeometry);

			yOffset = 0;
			foreach (DisplayLine line in layout.Lines)
			{
				double xOffset = layout.CodeColumn.Left - HorizontalOffset;
				foreach (LinePart part in line.Parts)
				{
					FormattedText text = CreateFormattedText(string.Join("", part.Text.Replace("\t", "    ")), typeface);

					if (!line.IsNew && part.Status == LinePartStatus.New)
						drawingContext.DrawRectangle(m_changedTextBrush, null, new Rect(xOffset, yOffset, text.WidthIncludingTrailingWhitespace, layout.LineHeight));

					drawingContext.DrawText(text, new Point(xOffset, yOffset));
					xOffset += text.WidthIncludingTrailingWhitespace;
				}

				layout = layout.WithCodeWidth(xOffset - codeColumn.Left + HorizontalOffset);

				yOffset += layout.LineHeight;
			}

			drawingContext.Pop();

			double commitRightX = layout.CommitColumn.Right;
			drawingContext.DrawLine(new Pen(Brushes.DarkGray, 1), new Point(commitRightX, 0), new Point(commitRightX, Math.Min(yOffset, RenderSize.Height)));

			double lineNumberRightX = layout.CodeMarginColumn.Right;
			drawingContext.DrawLine(new Pen(Brushes.DarkGray, 1), new Point(lineNumberRightX, 0), new Point(lineNumberRightX, Math.Min(yOffset, RenderSize.Height)));

			SetHorizontalScrollInfo(layout.Width, RenderSize.Width, null);

			if (layout != m_layout)
			{
				m_layout = layout;
				RedrawSoon();
			}
		}

		private void BlameResult_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			m_layout = m_layout.Refresh();
			RedrawSoon(DispatcherPriority.ApplicationIdle);
		}

		private void RedrawSoon(DispatcherPriority priority = DispatcherPriority.Render)
		{
			Dispatcher.BeginInvoke(priority, new SendOrPostCallback(delegate { Render(); }), null);
		}

		private FormattedText CreateFormattedText(string text, Typeface typeface)
		{
			return new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, m_emSize, Brushes.Black);
		}

		private FormattedText CreateSmallFormattedText(string text, Typeface typeface, double maxWidth)
		{
			FormattedText formattedText = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, m_emSize * 0.75, Brushes.Black)
			{
				MaxTextWidth = maxWidth,
				Trimming = TextTrimming.CharacterEllipsis,
				MaxLineCount = 1,
			};
			return formattedText;
		}

		private SolidColorBrush GetOrCreateCommitBrush(DisplayBlock block)
		{
			SolidColorBrush brush;
			if (!m_commitBrush.TryGetValue(block.CommitId, out brush))
			{
				m_commitAlpha.Add(block.CommitId, (byte) (block.Alpha * 255));
				brush = new SolidColorBrush(GetCommitColor(block.CommitId));
				m_commitBrush.Add(block.CommitId, brush);
			}
			return brush;
		}

		private Color GetCommitColor(string commitId)
		{
			return Color.FromArgb(m_commitAlpha[commitId], 128, 128, 128);
		}

		private void CreateBrushesForAuthors(int count)
		{
			for (int index = 0; index < count; index++)
			{
				int colorCount = m_colors.Length;
				Color color = m_colors[index % colorCount];
				Brush brush;
				switch (index / colorCount)
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
				m_personBrush.Add(index, brush);
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
			Color.FromRgb(10, 100, 255),
			Color.FromRgb(245, 4, 204),
			Color.FromRgb(252, 139, 0),
			Color.FromRgb(0, 228, 32),
			Color.FromRgb(0, 3, 245),
			Color.FromRgb(251, 0, 110),
			Color.FromRgb(252, 186, 0),
			Color.FromRgb(0, 251, 179),
			Color.FromRgb(123, 0, 252),
			Color.FromRgb(246, 0, 0),
			Color.FromRgb(236, 247, 38),
			Color.FromRgb(180, 0, 251),
			Color.FromRgb(251, 90, 3),
			Color.FromRgb(149, 253, 3),
			Color.FromRgb(0, 236, 252),
		};

		readonly DrawingVisual m_visual;
		readonly Brush m_newLineBrush;
		readonly SolidColorBrush m_changedTextBrush;
		readonly Dictionary<int, Brush> m_personBrush;
		readonly Dictionary<string, SolidColorBrush> m_commitBrush;
		readonly Dictionary<string, byte> m_commitAlpha;
		BlameResult m_blame;
		BlameLayout m_layout;
		int m_lineCount;
		double m_emSize;
		string m_hoverCommitId;
		string m_selectedCommitId;
	}
}
