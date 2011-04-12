
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using GitBlame.Utility;

namespace GitBlame.Models
{
	/// <summary>
	/// <see cref="BlameResult"/> is the result of running <c>git blame</c> on a specific revision of a specific file.
	/// </summary>
	internal sealed class BlameResult : INotifyPropertyChanged
	{
		public BlameResult(ReadOnlyCollection<Block> blocks, IList<Line> lines, Dictionary<string, Commit> commits)
		{
			m_blocks = blocks;
			m_lines = lines;
			m_linesReadOnly = m_lines.AsReadOnly();
			m_commits = commits;
		}

		public ReadOnlyCollection<Block> Blocks
		{
			get { return m_blocks; }
		}

		public ReadOnlyCollection<Commit> Commits
		{
			get
			{
				return new List<Commit>(m_commits.Values).AsReadOnly();
			}
		}

		public ReadOnlyCollection<Line> Lines
		{
			get { return m_linesReadOnly; }
		}

		public event PropertyChangedEventHandler PropertyChanged;

		internal void SetLine(int lineNumber, Line line)
		{
			m_lines[lineNumber - 1] = line;

			PropertyChangedEventHandler handler = PropertyChanged;
			if (handler != null)
				handler(this, new PropertyChangedEventArgs("Lines"));
		}

		readonly ReadOnlyCollection<Block> m_blocks;
		readonly IList<Line> m_lines;
		readonly ReadOnlyCollection<Line> m_linesReadOnly;
		readonly Dictionary<string, Commit> m_commits;
	}
}
