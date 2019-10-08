using System;
using System.IO;
using GitBlame.Models;
using ReactiveUI;

namespace GitBlame.ViewModels
{
	public sealed class BlamePositionModel : ReactiveObject
	{
		public BlamePositionModel(string filePath)
		{
			m_filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
			GitWrapper.SplitRepositoryPath(filePath, out m_repoPath, out m_fileName);
		}

		public BlamePositionModel(string repoPath, string fileName)
		{
			m_repoPath = repoPath ?? throw new ArgumentNullException(nameof(repoPath));
			m_fileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
			m_filePath = Path.Combine(Path.GetDirectoryName(m_repoPath), m_fileName);
		}

		public string RepoPath
		{
			get { return m_repoPath; }
		}

		public string FileName
		{
			get { return m_fileName; }
		}

		public string CommitId
		{
			get { return m_commitId; }
			set { this.RaiseAndSetIfChanged(ref m_commitId, value); }
		}

		public int? LineNumber
		{
			get { return m_lineNumber; }
			set { this.RaiseAndSetIfChanged(ref m_lineNumber, value); }
		}

		/// <summary>
		/// Returns the theoretical path of the file on disk; this file may no longer physically exist if it was renamed during its history.
		/// </summary>
		public string FilePath
		{
			get { return m_filePath; }
		}

		readonly string m_repoPath;
		readonly string m_fileName;
		readonly string m_filePath;
		string m_commitId;
		int? m_lineNumber;
	}
}
