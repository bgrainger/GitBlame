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
			if (filePath == null)
				throw new ArgumentNullException("filePath");

			m_repoPath = GitWrapper.GetRepositoryPath(filePath);
			m_fileName = filePath.Substring(Path.GetDirectoryName(m_repoPath).Length + 1);
		}

		public BlamePositionModel(string repoPath, string fileName)
		{
			if (repoPath == null)
				throw new ArgumentNullException("repoPath");
			if (fileName == null)
				throw new ArgumentNullException("fileName");

			m_repoPath = repoPath;
			m_fileName = fileName;
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
		/// <returns></returns>
		public string GetFilePath()
		{
			return Path.Combine(Path.GetDirectoryName(m_repoPath), m_fileName);
		}

		readonly string m_repoPath;
		readonly string m_fileName;
		string m_commitId;
		int? m_lineNumber;
	}
}
