
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
			: this()
		{
			// get basic information from "blame" output
			m_blame = blame;
			m_authorIndex = m_blame.Commits
				.GroupBy(c => c.Author)
				.OrderByDescending(g => g.Count())
				.Select((g, n) => new KeyValuePair<Person, int>(g.Key, n))
				.ToDictionary();

			// track commit age (for fading out the blocks for each commit)
			m_oldestCommit = blame.Commits.Min(c => c.AuthorDate);
			DateTimeOffset newestCommit = blame.Commits.Max(c => c.AuthorDate);
			m_dateScale = 0.65 / (newestCommit - m_oldestCommit).TotalDays;

			// set up default column widths
			m_columnWidths = new double[] { 210, 10, 0, 10, 5, 0 };

			// set default values
			m_lineHeight = 1;
			m_topLineNumber = 1;
		}

		public int TopLineNumber
		{
			get { return m_topLineNumber; }
		}

		public int LineCount
		{
			get { return m_lineCount; }
		}

		public double LineHeight
		{
			get { return m_lineHeight; }
		}

		public int AuthorCount
		{
			get { return m_authorIndex.Count; }
		}

		public ReadOnlyCollection<DisplayBlock> Blocks
		{
			get
			{
				Calculate();
				return m_blocksReadOnly;
			}
		}

		public ReadOnlyCollection<DisplayLine> Lines
		{
			get
			{
				Calculate();
				return m_linesReadOnly;
			}
		}

		public ReadOnlyCollection<Rect> NewLines
		{
			get
			{
				Calculate();
				return m_newLinesReadOnly;
			}
		}

		public Column CommitColumn
		{
			get { return GetColumn(c_commitColumnIndex); }
		}

		public Column LineNumberColumn
		{
			get { return GetColumn(c_lineNumberColumnIndex); }
		}

		public Column CodeMarginColumn
		{
			get { return GetColumn(c_codeMarginColumnIndex); }
		}

		public Column CodeColumn
		{
			get { return GetColumn(c_codeColumnIndex); }
		}

		public double Width
		{
			get { return m_columnWidths.Sum(); }
		}

		public BlameLayout WithCodeWidth(double width)
		{
			return width <= m_columnWidths[c_codeColumnIndex] ? this : new BlameLayout(this, codeColumnWidth: width);
		}

		public BlameLayout WithLineNumberWidth(double width)
		{
			return width <= m_columnWidths[c_lineNumberColumnIndex] ? this : new BlameLayout(this, lineNumberColumnWidth: width);
		}

		public BlameLayout WithLineHeight(double lineHeight)
		{
			return lineHeight == m_lineHeight ? this : new BlameLayout(this, lineHeight: lineHeight);
		}

		public BlameLayout WithRenderSize(Size renderSize)
		{
			return renderSize == m_renderSize ? this : new BlameLayout(this, renderSize: renderSize);
		}

		public BlameLayout WithTopLineNumber(int topLineNumber)
		{
			return topLineNumber == m_topLineNumber ? this : new BlameLayout(this, topLineNumber: topLineNumber);
		}

		private BlameLayout()
		{
			m_blocks = new List<DisplayBlock>();
			m_blocksReadOnly = m_blocks.AsReadOnly();
			m_lines = new List<DisplayLine>();
			m_linesReadOnly = m_lines.AsReadOnly();
			m_newLines = new List<Rect>();
			m_newLinesReadOnly = m_newLines.AsReadOnly();
		}

		private BlameLayout(BlameLayout layout, int? topLineNumber = null, Size? renderSize = null, double? lineHeight = null,
			double? lineNumberColumnWidth = null, double? codeColumnWidth = null)
			: this()
		{
			// copy values from other BlameLayout
			m_blame = layout.m_blame;
			m_authorIndex = layout.m_authorIndex;
			m_oldestCommit = layout.m_oldestCommit;
			m_dateScale = layout.m_dateScale;
			m_columnWidths = layout.m_columnWidths;

			// copy or replace values from other BlameLayout
			m_topLineNumber = topLineNumber ?? layout.m_topLineNumber;
			m_renderSize = renderSize ?? layout.m_renderSize;
			m_lineHeight = lineHeight ?? layout.m_lineHeight;
			m_columnWidths[c_lineNumberColumnIndex] = lineNumberColumnWidth ?? m_columnWidths[c_lineNumberColumnIndex];
			m_columnWidths[c_codeColumnIndex] = codeColumnWidth ?? m_columnWidths[c_codeColumnIndex];

			// calculate new values
			m_lineCount = (int) Math.Ceiling(m_renderSize.Height / m_lineHeight);
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
				if (block.StartLine <= m_topLineNumber && block.StartLine + block.LineCount > m_topLineNumber)
					break;
			}

			// determine the position of each block that is displayed
			int lineCount = 0;
			while (lineCount < m_lineCount && blockIndex < m_blame.Blocks.Count)
			{
				Block block = m_blame.Blocks[blockIndex];
				int hiddenLines = m_topLineNumber - block.StartLine;
				int remainingLines = m_lineCount - lineCount;
				int linesFromThisBlock = Math.Min(block.LineCount - Math.Max(0, hiddenLines), remainingLines);
				double alpha = (block.Commit.CommitDate - m_oldestCommit).TotalDays * m_dateScale + 0.1;

				const double authorWidth = 10;
				Rect authorPosition = new Rect(0, lineCount * m_lineHeight, authorWidth, linesFromThisBlock * m_lineHeight);
				Rect commitPosition = new Rect(authorWidth, authorPosition.Top, m_columnWidths[0] - authorWidth, authorPosition.Height);

				// add the commit summary if there is space
				Rect summaryPosition = Rect.Empty;

				if (linesFromThisBlock > 1)
				{
					summaryPosition = commitPosition;
					summaryPosition.Inflate(-1, -1);
					summaryPosition.Offset(0, m_lineHeight);
					summaryPosition.Height = Math.Min(summaryPosition.Height - m_lineHeight, m_renderSize.Height);
				}

				m_blocks.Add(new DisplayBlock(authorPosition, commitPosition, summaryPosition, alpha, m_authorIndex[block.Commit.Author], block.Commit));

				blockIndex++;
				lineCount += linesFromThisBlock;
			}

			// determine the source code lines that are visible
			m_lines.Capacity = m_lineCount;
			m_lines.AddRange(m_blame.Lines
				.Skip(m_topLineNumber - 1)
				.Take(m_lineCount)
				.Select((l, n) => new DisplayLine(l, n + m_topLineNumber)));

			// determine which of those lines are new in their respective commits
			lineCount = 0;
			double newLineX = CodeMarginColumn.Right - 5;
			foreach (var lineGroup in m_lines.GroupConsecutiveBy(l => l.IsNew))
			{
				if (lineGroup.Key)
					m_newLines.Add(new Rect(newLineX, lineCount * m_lineHeight, 5, lineGroup.Count() * m_lineHeight));

				lineCount += lineGroup.Count();
			}
		}

		private Column GetColumn(int index)
		{
			return new Column(m_columnWidths.Take(index).Sum(), m_columnWidths[index]);
		}

		const int c_commitColumnIndex = 0;
		const int c_lineNumberColumnIndex = 2;
		const int c_codeMarginColumnIndex = 3;
		const int c_codeColumnIndex = 5;

		readonly BlameResult m_blame;
		readonly Dictionary<Person, int> m_authorIndex;
		readonly DateTimeOffset m_oldestCommit;
		readonly double m_dateScale;

		readonly double[] m_columnWidths;
		readonly int m_topLineNumber;
		readonly Size m_renderSize;
		readonly double m_lineHeight;
		readonly int m_lineCount;
		readonly List<DisplayBlock> m_blocks;
		readonly ReadOnlyCollection<DisplayBlock> m_blocksReadOnly;
		readonly List<DisplayLine> m_lines;
		readonly ReadOnlyCollection<DisplayLine> m_linesReadOnly;
		readonly List<Rect> m_newLines;
		readonly ReadOnlyCollection<Rect> m_newLinesReadOnly;
	}

	internal struct Column
	{
		public Column(double left, double width)
		{
			m_left = left;
			m_width = width;
		}

		public double Left
		{
			get { return m_left; }
		}

		public double Width
		{
			get { return m_width; }
		}

		public double Right
		{
			get { return m_left + m_width; }
		}

		readonly double m_left;
		readonly double m_width;
	}

	internal sealed class DisplayBlock
	{
		public DisplayBlock(Rect authorPosition, Rect commitPosition, Rect summaryPosition, double alpha, int authorIndex, Commit commit)
		{
			m_authorPosition = authorPosition;
			m_commitPosition = commitPosition;
			m_summaryPosition = summaryPosition;
			m_alpha = alpha;
			m_authorIndex = authorIndex;
			m_commit = commit;
		}

		public string CommitId
		{
			get { return m_commit.Id; }
		}

		public Rect AuthorPosition
		{
			get { return m_authorPosition; }
		}

		public Rect CommitPosition
		{
			get { return m_commitPosition; }
		}

		public double AuthorX
		{
			get { return m_commitPosition.Left + 1; }
		}

		public double AuthorWidth
		{
			get { return m_commitPosition.Width - DateWidth - 5; }
		}

		public double TextY
		{
			get { return m_commitPosition.Top + 1; }
		}

		public double DateX
		{
			get { return m_commitPosition.Width - 1 - DateWidth; }
		}

		public double DateWidth
		{
			get { return m_commitPosition.Width * 0.425; }
		}

		public double Alpha
		{
			get { return m_alpha; }
		}

		public int AuthorIndex
		{
			get { return m_authorIndex; }
		}

		public string AuthorName
		{
			get { return m_commit.Author.Name; }
		}

		public string Date
		{
			get { return m_commit.AuthorDate.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture); }
		}

		public bool ShowsSummary
		{
			get { return !m_summaryPosition.IsEmpty; }
		}

		public Rect SummaryPosition
		{
			get { return m_summaryPosition; }
		}

		public string Summary
		{
			get
			{
				// allow line breaks in "Long.Dotted.Identifiers" by inserting a zero-width space
				return Regex.Replace(m_commit.Summary, @"\.([A-Z, ])", ".\u200B$1");
			}
		}

		readonly Rect m_authorPosition;
		readonly Rect m_commitPosition;
		readonly Rect m_summaryPosition;
		readonly double m_alpha;
		readonly int m_authorIndex;
		readonly Commit m_commit;
	}

	internal sealed class DisplayLine
	{
		public DisplayLine(Line line, int lineNumber)
		{
			m_line = line;
			m_lineNumber = lineNumber;
		}

		public bool IsNew
		{
			get { return m_line.IsNew; }
		}

		public ReadOnlyCollection<LinePart> Parts
		{
			get { return m_line.Parts; }
		}

		public int LineNumber
		{
			get { return m_lineNumber; }
		}

		readonly Line m_line;
		readonly int m_lineNumber;
	}
}