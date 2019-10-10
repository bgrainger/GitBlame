using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using GitBlame.Models;
using GitBlame.Utility;
using Color32 = System.Windows.Media.Color;
using Rect = System.Windows.Rect;
using Size = System.Windows.Size;

namespace GitBlame.Layout
{
	/// <summary>
	/// <see cref="BlameLayout"/> represents the layout of one screen of "blame" output.
	/// It has a specific top line number, and number of lines displayed, and contains the information
	/// required to render that screen. The layout object is immutable; the <c>WithX</c> methods
	/// can be used to obtain a new layout.
	/// </summary>
	internal sealed class BlameLayout
	{
		public BlameLayout(BlameResult blame)
			: this(blame, GetBlameAuthors(blame), s_defaultColumnWidths)
		{
			// track commit age (for fading out the blocks for each commit)
			m_oldestCommit = GetOldestCommit(m_blame);
			m_dateScale = GetDateScale(m_blame, m_oldestCommit);

			// set default values
			LineHeight = 1;
			TopLineNumber = 1;
		}

		public int TopLineNumber { get; }
		public int LineCount { get; }
		public double LineHeight { get; }
		public int AuthorCount => m_authorIndex.Count;

		public IReadOnlyList<DisplayBlock> Blocks
		{
			get
			{
				Calculate();
				return m_blocksReadOnly;
			}
		}

		public IReadOnlyList<DisplayLine> Lines
		{
			get
			{
				Calculate();
				return m_linesReadOnly;
			}
		}

		public IReadOnlyList<Rect> NewLines
		{
			get
			{
				Calculate();
				return m_newLinesReadOnly;
			}
		}

		public Column CommitColumn => GetColumn(c_commitColumnIndex);
		public Column LineNumberColumn => GetColumn(c_lineNumberColumnIndex);
		public Column CodeMarginColumn => GetColumn(c_codeMarginColumnIndex);
		public Column CodeColumn => GetColumn(c_codeColumnIndex);
		public double Width => m_columnWidths.Sum();

		public BlameLayout Refresh(bool fullRefresh = false) => new BlameLayout(this, fullRefresh: fullRefresh);

		public BlameLayout WithCodeWidth(double width) =>
			width <= m_columnWidths[c_codeColumnIndex] ? this : new BlameLayout(this, codeColumnWidth: width);

		public BlameLayout WithLineNumberWidth(double width) =>
			width <= m_columnWidths[c_lineNumberColumnIndex] ? this : new BlameLayout(this, lineNumberColumnWidth: width);

		public BlameLayout WithLineHeight(double lineHeight) =>
			lineHeight == LineHeight ? this : new BlameLayout(this, lineHeight: lineHeight);

		public BlameLayout WithRenderSize(Size renderSize) =>
			renderSize == m_renderSize ? this : new BlameLayout(this, renderSize: renderSize);

		public BlameLayout WithTopLineNumber(int topLineNumber) =>
			topLineNumber == TopLineNumber ? this : new BlameLayout(this, topLineNumber: topLineNumber);

		private BlameLayout(BlameResult blame, Dictionary<Person, int> authorIndex, double[] columnWidths)
		{
			m_blame = blame;
			m_authorIndex = authorIndex;
			m_columnWidths = columnWidths;
			m_blocks = new List<DisplayBlock>();
			m_blocksReadOnly = m_blocks.AsReadOnly();
			m_lines = new List<DisplayLine>();
			m_linesReadOnly = m_lines.AsReadOnly();
			m_newLines = new List<Rect>();
			m_newLinesReadOnly = m_newLines.AsReadOnly();
		}

		private BlameLayout(BlameLayout layout, int? topLineNumber = null, Size? renderSize = null, double? lineHeight = null,
			double? lineNumberColumnWidth = null, double? codeColumnWidth = null, bool fullRefresh = false)
			: this(layout.m_blame, fullRefresh ? GetBlameAuthors(layout.m_blame) : layout.m_authorIndex, layout.m_columnWidths)
		{
			// copy or replace values from other BlameLayout
			TopLineNumber = topLineNumber ?? layout.TopLineNumber;
			m_renderSize = renderSize ?? layout.m_renderSize;
			LineHeight = lineHeight ?? layout.LineHeight;
			m_columnWidths[c_lineNumberColumnIndex] = lineNumberColumnWidth ?? m_columnWidths[c_lineNumberColumnIndex];
			m_columnWidths[c_codeColumnIndex] = codeColumnWidth ?? m_columnWidths[c_codeColumnIndex];
			m_oldestCommit = fullRefresh ? GetOldestCommit(m_blame) : layout.m_oldestCommit;
			m_dateScale = fullRefresh ? GetDateScale(m_blame, m_oldestCommit) : layout.m_dateScale;

			// calculate new values
			LineCount = (int) Math.Ceiling(m_renderSize.Height / LineHeight);
		}

		private void Calculate()
		{
			// check if already calculated
			if (m_blocks.Count != 0)
				return;

			// calculate first block that is displayed
			int blockIndex = 0;
			for (; blockIndex < m_blame.Blocks.Count; blockIndex++)
			{
				Block block = m_blame.Blocks[blockIndex];
				if (block.StartLine <= TopLineNumber && block.StartLine + block.LineCount > TopLineNumber)
					break;
			}

			// determine the position of each block that is displayed
			int lineCount = 0;
			while (lineCount < LineCount && blockIndex < m_blame.Blocks.Count)
			{
				Block block = m_blame.Blocks[blockIndex];
				int hiddenLines = TopLineNumber - block.StartLine;
				int remainingLines = LineCount - lineCount;
				int linesFromThisBlock = Math.Min(block.LineCount - Math.Max(0, hiddenLines), remainingLines);
				double alpha = 0.75 - (block.Commit.CommitDate - m_oldestCommit).TotalDays * m_dateScale;

				const double authorWidth = 10;
				Rect authorPosition = new Rect(0, lineCount * LineHeight, authorWidth, linesFromThisBlock * LineHeight);
				Rect commitPosition = new Rect(authorWidth, authorPosition.Top, m_columnWidths[0] - authorWidth, authorPosition.Height);

				// add the commit summary if there is space
				Rect summaryPosition = Rect.Empty;

				if (linesFromThisBlock > 1)
				{
					summaryPosition = commitPosition;
					summaryPosition.Inflate(-1, -1);
					summaryPosition.Offset(0, LineHeight);
					summaryPosition.Height = Math.Min(summaryPosition.Height - LineHeight, m_renderSize.Height);
				}

				m_blocks.Add(new DisplayBlock(authorPosition, commitPosition, summaryPosition, alpha, m_authorIndex.ContainsKey(block.Commit.Author) ? m_authorIndex[block.Commit.Author] : 0, block));

				blockIndex++;
				lineCount += linesFromThisBlock;
			}

			// determine the source code lines that are visible
			m_lines.Capacity = LineCount;
			m_lines.AddRange(m_blame.Lines
				.Skip(TopLineNumber - 1)
				.Take(LineCount)
				.Select((l, n) => new DisplayLine(l, n + TopLineNumber)));

			// determine which of those lines are new in their respective commits
			lineCount = 0;
			double newLineX = CodeMarginColumn.Right - 5;
			foreach (var lineGroup in m_lines.GroupConsecutiveBy(l => l.IsNew))
			{
				if (lineGroup.Key)
					m_newLines.Add(new Rect(newLineX, lineCount * LineHeight, 5, lineGroup.Count() * LineHeight));

				lineCount += lineGroup.Count();
			}
		}

		private Column GetColumn(int index) => new Column(m_columnWidths.Take(index).Sum(), m_columnWidths[index]);

		private static Dictionary<Person, int> GetBlameAuthors(BlameResult blame) =>
			blame.Commits
				.GroupBy(c => c.Author)
				.OrderByDescending(g => g.Count())
				.Select((g, n) => new KeyValuePair<Person, int>(g.Key, n))
				.ToDictionary();

		private static DateTimeOffset GetOldestCommit(BlameResult blame) => blame.Commits.Min(c => c.AuthorDate);

		private static double GetDateScale(BlameResult blame, DateTimeOffset oldestCommit)
		{
			DateTimeOffset newestCommit = blame.Commits.Max(c => c.AuthorDate);
			return 0.65 / (newestCommit - oldestCommit).TotalDays;
		}

		const int c_commitColumnIndex = 0;
		const int c_lineNumberColumnIndex = 2;
		const int c_codeMarginColumnIndex = 3;
		const int c_codeColumnIndex = 5;

		// set up default column widths
		static readonly double[] s_defaultColumnWidths = new double[] { 210, 10, 0, 10, 5, 0 };

		readonly BlameResult m_blame;
		readonly Dictionary<Person, int> m_authorIndex;
		readonly DateTimeOffset m_oldestCommit;
		readonly double m_dateScale;

		readonly double[] m_columnWidths;
		readonly Size m_renderSize;
		readonly List<DisplayBlock> m_blocks;
		readonly IReadOnlyList<DisplayBlock> m_blocksReadOnly;
		readonly List<DisplayLine> m_lines;
		readonly IReadOnlyList<DisplayLine> m_linesReadOnly;
		readonly List<Rect> m_newLines;
		readonly IReadOnlyList<Rect> m_newLinesReadOnly;
	}

	internal readonly struct Column
	{
		public Column(double left, double width)
		{
			Left = left;
			Width = width;
		}

		public double Left { get; }
		public double Width { get; }
		public double Right => Left + Width;
	}

	internal sealed class DisplayBlock
	{
		public DisplayBlock(Rect authorPosition, Rect commitPosition, Rect summaryPosition, double alpha, int authorIndex, Block block)
		{
			AuthorPosition = authorPosition;
			CommitPosition = commitPosition;
			SummaryPosition = summaryPosition;
			Alpha = alpha;
			AuthorIndex = authorIndex;
			RawBlock = block;
			RawCommit = block.Commit;
		}

		public Block RawBlock { get; }
		public Commit RawCommit { get; }
		public string CommitId => RawCommit.Id;
		public Rect AuthorPosition { get; }
		public Rect CommitPosition { get; }
		public double AuthorX => CommitPosition.Left + 1;
		public double AuthorWidth => CommitPosition.Width - DateWidth - 5;
		public double TextY => CommitPosition.Top + 1;
		public double DateX => CommitPosition.Width - 1 - DateWidth;
		public double DateWidth => CommitPosition.Width * 0.425;
		public double Alpha { get; }
		public int AuthorIndex { get; }
		public string AuthorName => RawCommit.Author.Name;
		public string Date => RawCommit.AuthorDate.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
		public bool ShowsSummary => !SummaryPosition.IsEmpty;
		public Rect SummaryPosition { get; }

		public string Summary
		{
			get
			{
				// allow line breaks in "Long.Dotted.Identifiers" by inserting a zero-width space
				return Regex.Replace(RawCommit.Summary, @"\.([A-Z, ])", ".\u200B$1");
			}
		}
	}

	internal sealed class DisplayLine
	{
		public DisplayLine(Line line, int lineNumber)
		{
			m_line = line;
			LineNumber = lineNumber;
		}

		public bool IsNew => m_line.IsNew;
		public IReadOnlyList<LinePart> Parts => m_line.Parts;
		public int LineNumber { get; }

		readonly Line m_line;
	}
}
