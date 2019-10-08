using System;
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
		public BlameResult(Uri webRootUrl, IReadOnlyList<Block> blocks, IList<Line> lines, Dictionary<string, Commit> commits)
		{
			WebRootUrl = webRootUrl;
			Blocks = blocks;
			m_lines = lines;
			m_linesReadOnly = m_lines.AsReadOnly();
			m_commits = commits;
			m_commitsReadOnly = m_commits.Values.ToList().AsReadOnly();
		}

		public Uri WebRootUrl { get; }
		public IReadOnlyList<Block> Blocks { get; private set; }
		public IReadOnlyList<Commit> Commits => m_commitsReadOnly;
		public IReadOnlyList<Line> Lines => m_linesReadOnly;
		public event PropertyChangedEventHandler PropertyChanged;

		internal void SetData(IList<Block> blocks, IList<Line> lines, Dictionary<string, Commit> commits)
		{
			Blocks = blocks.AsReadOnly();
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

			RaisePropertyChanged(nameof(Lines));
		}

		private void RaisePropertyChanged(string propertyName) =>
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

		IList<Line> m_lines;
		IReadOnlyList<Line> m_linesReadOnly;
		Dictionary<string, Commit> m_commits;
		IReadOnlyList<Commit> m_commitsReadOnly;
	}
}
