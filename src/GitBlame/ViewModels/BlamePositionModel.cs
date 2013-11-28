using System;
using ReactiveUI;

namespace GitBlame.ViewModels
{
	public sealed class BlamePositionModel : ReactiveObject
	{
		public BlamePositionModel(string filePath)
		{
			if (filePath == null)
				throw new ArgumentNullException("filePath");
			
			m_filePath = filePath;
		}

		public string FilePath
		{
			get { return m_filePath; }
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

		readonly string m_filePath;
		string m_commitId;
		int? m_lineNumber;
	}
}
