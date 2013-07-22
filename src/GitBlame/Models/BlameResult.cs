using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
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
			m_commitsReadOnly = m_commits.Values.ToList().AsReadOnly();
		}

		public ReadOnlyCollection<Block> Blocks
		{
			get { return m_blocks; }
		}

		public ReadOnlyCollection<Commit> Commits
		{
			get { return m_commitsReadOnly; }
		}

		public ReadOnlyCollection<Line> Lines
		{
			get { return m_linesReadOnly; }
		}

		public event PropertyChangedEventHandler PropertyChanged;

		internal void SetData(IList<Block> blocks, IList<Line> lines, Dictionary<string, Commit> commits)
		{
			m_blocks = blocks.AsReadOnly();
			m_lines = lines;
			m_linesReadOnly = lines.AsReadOnly();
			m_commits = commits;
			m_commitsReadOnly = m_commits.Values.ToList().AsReadOnly();

			RaisePropertyChanged(null);
		}

		internal void SetLine(int lineNumber, Line line)
		{
			var existingLineText = string.Join("", m_lines[lineNumber - 1].Parts.Select(p => p.Text));
			var lineText = string.Join("", line.Parts.Select(p => p.Text));
			Invariant.Assert(lineText == existingLineText, "Line text should not be changed.");

			m_lines[lineNumber - 1] = line;

			RaisePropertyChanged("Lines");
		}

		private void RaisePropertyChanged(string propertyName)
		{
			PropertyChangedEventHandler handler = PropertyChanged;
			if (handler != null)
				handler(this, new PropertyChangedEventArgs(propertyName));
		}

		ReadOnlyCollection<Block> m_blocks;
		IList<Line> m_lines;
		ReadOnlyCollection<Line> m_linesReadOnly;
		Dictionary<string, Commit> m_commits;
		ReadOnlyCollection<Commit> m_commitsReadOnly;
	}
}
