using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using GitBlame.Layout;
using GitBlame.Models;
using GitBlame.Utility;
using ReactiveUI;

namespace GitBlame
{
	public sealed class BlameControl : ScrollableControl
	{
		public BlameControl()
			: base(12, 1)
		{
			m_visual = new DrawingVisual();
			AddVisualChild(m_visual);

			m_blamePreviousMenuItem = new MenuItem { Header = "Blame previous", Command = Commands.BlamePreviousCommand, CommandTarget = this };
			m_viewAtGitHubMenuItem = new MenuItem { Header = "View at GitHub", Command = Commands.ViewAtGitHubCommand, CommandTarget = this };
			ContextMenu = new ContextMenu
			{
				Items =
				{
					m_blamePreviousMenuItem,
					m_viewAtGitHubMenuItem
				}
			};

			m_personBrush = new Dictionary<int, Brush>();
			m_commitBrush = new Dictionary<string, SolidColorBrush>();
			m_commitAlpha = new Dictionary<string, byte>();
			m_newLineBrush = new SolidColorBrush(Color.FromRgb(108, 226, 108));
			m_newLineBrush.Freeze();
			m_changedTextBrush = new SolidColorBrush(Color.FromRgb(193, 228, 255));
			m_changedTextBrush.Freeze();
			m_redrawTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(100), DispatcherPriority.Background, OnRedrawTimerTick, Dispatcher);

			// can only show the tooltip if the mouse is over the control, and the context menu isn't open
			var canShowTooltip = this.WhenAny(x => x.IsMouseOver, x => x.ContextMenu.IsOpen, (mo, cm) => mo.Value && !cm.Value);
			canShowTooltip.Where(x => !x).ObserveOnDispatcher().Subscribe(_ => HideToolTip());

			var mouseMove = Observable.FromEventPattern<MouseEventArgs>(this, "MouseMove");
			var mouseOverCommits = mouseMove
				.Select(x => x.EventArgs.GetPosition(this))
				.Select(GetCommitFromPoint)
				.DistinctUntilChanged();
			mouseOverCommits.ObserveOnDispatcher().Subscribe(MouseOverCommit);
			mouseOverCommits.Throttle(TimeSpan.FromSeconds(0.5))
				.CombineLatest(canShowTooltip, (l, r) => new { Commit = l, CanShowTooltip = r })
				.Where(x => x.Commit != null && x.CanShowTooltip)
				.Select(x => x.Commit)
				.ObserveOnDispatcher()
				.Subscribe(ShowCommitTooltip);
		}

		public int? TopLineNumber
		{
			get { return m_layout == null ? default(int?) : m_layout.TopLineNumber; }
		}

		internal void SetBlameResult(BlameResult blame, int topLineNumber = 1)
		{
			if (m_blameSubscription != null)
				m_blameSubscription.Dispose();

			double oldLineHeight = m_layout == null ? 1.0 : m_layout.LineHeight;

			m_blame = blame;
			m_layout = new BlameLayout(blame).WithTopLineNumber(1).WithLineHeight(oldLineHeight);
			m_lineCount = blame.Blocks.Sum(b => b.LineCount);
			m_blameSubscription = Observable.FromEventPattern<PropertyChangedEventArgs>(m_blame, "PropertyChanged").ObserveOnDispatcher().Subscribe(x => OnBlameResultPropertyChanged(x.EventArgs));

			m_hoverCommitId = null;
			m_selectedCommitId = null;
			m_personBrush.Clear();
			m_commitBrush.Clear();
			m_commitAlpha.Clear();
			CreateBrushesForAuthors(m_layout.AuthorCount);

			SetVerticalScrollInfo(m_lineCount + 1, null, topLineNumber - 1);
			InvalidateMeasure();
			OnScrollChanged();
			RedrawSoon();
		}

		protected override Size ArrangeOverride(Size finalSize)
		{
			if (m_layout == null)
				return new Size();

			m_layout = m_layout.WithRenderSize(finalSize);
			SetVerticalScrollInfo(null, finalSize.Height / m_layout.LineHeight, null);
			RedrawSoon();
			return finalSize;
		}

		protected override Size MeasureOverride(Size availableSize)
		{
			if (m_layout == null)
				return new Size();

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

		private void MouseOverCommit(Commit hoverCommit)
		{
			HideToolTip();

			string hoverCommitId = hoverCommit == null ? null : hoverCommit.Id;
			if (hoverCommitId == m_selectedCommitId)
				hoverCommitId = null;
			SetCommitColor(ref m_hoverCommitId, hoverCommitId, m_changedTextBrush.Color);
		}

		private void ShowCommitTooltip(Commit commit)
		{
			m_hoverTip = new ToolTip
			{
				Content = commit,
				Placement = PlacementMode.Mouse,
				IsOpen = true
			};
		}

		private void HideToolTip()
		{
			if (m_hoverTip != null)
			{
				m_hoverTip.IsOpen = false;
				m_hoverTip = null;
			}
		}

		protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
		{
			SetCommitColor(ref m_selectedCommitId, GetCommitIdFromPoint(e.GetPosition(this)), Color.FromRgb(79, 178, 255));
			m_hoverCommitId = null;

			base.OnMouseLeftButtonUp(e);
		}

		protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
		{
			Point position = e.GetPosition(this);

			DisplayBlock block = m_layout == null ? null : m_layout.Blocks.FirstOrDefault(x => x.CommitPosition.Contains(position));
			if (block != null)
			{
				int lineIndex = (int) (position.Y / m_layout.LineHeight);
				DisplayLine displayLine = m_layout.Lines[lineIndex];
				Line line = m_blame.Lines[displayLine.LineNumber - 1];
				int previousTopLine = Math.Max(1, line.OldLineNumber - lineIndex);

				Commit commit = block.RawCommit;
				m_blamePreviousMenuItem.CommandParameter = commit.PreviousCommitId == null ? null : new BlamePreviousModel(commit.PreviousCommitId, commit.PreviousFileName, previousTopLine);
				m_viewAtGitHubMenuItem.CommandParameter = m_blame.WebRootUrl != null && commit.Id != GitWrapper.UncommittedChangesCommitId ? new Uri(m_blame.WebRootUrl, "commit/" + commit.Id) : null;
				ContextMenu.IsOpen = true;
			}
			else
			{
				m_blamePreviousMenuItem.CommandParameter = null;
				m_viewAtGitHubMenuItem.CommandParameter = null;
			}

			e.Handled = true;
			base.OnMouseRightButtonUp(e);
		}

		private void SetCommitColor(ref string commitId, string newCommitId, Color color)
		{
			if (commitId != null && m_commitBrush.ContainsKey(commitId))
				m_commitBrush[commitId].Color = GetCommitColor(commitId);

			if (newCommitId != null && m_commitBrush.ContainsKey(newCommitId))
				m_commitBrush[newCommitId].Color = color;

			commitId = newCommitId;
		}

		private Commit GetCommitFromPoint(Point point)
		{
			return m_layout == null ? null : m_layout.Blocks
				.Where(b => b.CommitPosition.Contains(point))
				.Select(b => b.RawCommit)
				.FirstOrDefault();
		}

		private string GetCommitIdFromPoint(Point point)
		{
			Commit commit = GetCommitFromPoint(point);
			return commit == null ? null : commit.Id;
		}

		protected override void OnScrollChanged()
		{
			m_layout = m_layout.WithTopLineNumber((int) VerticalOffset + 1);
			if (RenderSize.Width > 0 && RenderSize.Height > 0)
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

		private void OnBlameResultPropertyChanged(PropertyChangedEventArgs e)
		{
			bool fullRefresh = e.PropertyName == null;
			m_layout = m_layout.Refresh(fullRefresh);
			if (fullRefresh)
			{
				m_personBrush.Clear();
				CreateBrushesForAuthors(m_layout.AuthorCount);
			}
			RedrawSoon();
		}

		private void RedrawSoon()
		{
			m_redrawTimer.Start();
		}

		private void OnRedrawTimerTick(object sender, EventArgs args)
		{
			m_redrawTimer.Stop();
			Render();
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

					case 1:
						brush = CreateLeftDiagonalBrush(color);
						break;

					case 2:
						brush = CreateRightDiagonalBrush(color);
						break;

					default:
						// TODO: create more pattern brushes
						brush = CreateCheckerboard(color);
						break;
				}

				brush.Freeze();
				m_personBrush.Add(index, brush);
			}
		}

		private static Brush CreateLeftDiagonalBrush(Color color)
		{
			PathFigure figure1 = CreateSimpleClosedPathFigure(new Point(5, 0), new Point(10, 0), new Point(0, 10), new Point(0, 5));
			PathFigure figure2 = CreateSimpleClosedPathFigure(new Point(10, 5), new Point(10, 10), new Point(5, 10));
			GeometryDrawing drawing = CreateSimpleGeometryDrawing(color, figure1, figure2);
			return CreateStandardDrawingBrush(drawing);
		}

		private static Brush CreateRightDiagonalBrush(Color color)
		{
			PathFigure figure1 = CreateSimpleClosedPathFigure(new Point(5, 0), new Point(0, 0), new Point(10, 10), new Point(10, 5));
			PathFigure figure2 = CreateSimpleClosedPathFigure(new Point(0, 5), new Point(0, 10), new Point(5, 10));
			GeometryDrawing drawing = CreateSimpleGeometryDrawing(color, figure1, figure2);
			return CreateStandardDrawingBrush(drawing);
		}

		private static Brush CreateCheckerboard(Color color)
		{
			PathFigure figure1 = CreateSimpleClosedPathFigure(new Point(0, 0), new Point(5, 0), new Point(5, 5), new Point(0, 5));
			PathFigure figure2 = CreateSimpleClosedPathFigure(new Point(5, 5), new Point(10, 5), new Point(10, 10), new Point(5, 10));
			GeometryDrawing drawing = CreateSimpleGeometryDrawing(color, figure1, figure2);
			return CreateStandardDrawingBrush(drawing);
		}

		private static PathFigure CreateSimpleClosedPathFigure(params Point[] points)
		{
			return new PathFigure
			{
				IsClosed = true,
				StartPoint = points[0],
				Segments = { new PolyLineSegment { Points = new PointCollection(points.Skip(1)) } },
			};
		}

		private static GeometryDrawing CreateSimpleGeometryDrawing(Color color, params PathFigure[] figures)
		{
			return new GeometryDrawing
			{
				Geometry = new PathGeometry { Figures = new PathFigureCollection(figures) },
				Brush = new SolidColorBrush(color),
			};
		}

		private static DrawingBrush CreateStandardDrawingBrush(Drawing drawing)
		{
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
		readonly MenuItem m_blamePreviousMenuItem;
		readonly MenuItem m_viewAtGitHubMenuItem;
		readonly Brush m_newLineBrush;
		readonly SolidColorBrush m_changedTextBrush;
		readonly Dictionary<int, Brush> m_personBrush;
		readonly Dictionary<string, SolidColorBrush> m_commitBrush;
		readonly Dictionary<string, byte> m_commitAlpha;
		readonly DispatcherTimer m_redrawTimer;
		BlameResult m_blame;
		IDisposable m_blameSubscription;
		BlameLayout m_layout;
		int m_lineCount;
		double m_emSize;
		string m_hoverCommitId;
		string m_selectedCommitId;
		ToolTip m_hoverTip;
	}
}
