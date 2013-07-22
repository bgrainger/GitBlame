namespace GitBlame.Models
{
	public sealed class BlamePreviousModel
	{
		public BlamePreviousModel(string commitId, string fileName, int lineNumber)
		{
			m_lineNumber = lineNumber;
			m_commitId = commitId;
			m_fileName = fileName;
		}

		public string CommitId
		{
			get { return m_commitId; }
		}

		public string FileName
		{
			get { return m_fileName; }
		}

		public int LineNumber
		{
			get { return m_lineNumber; }
		}

		readonly int m_lineNumber;
		readonly string m_commitId;
		readonly string m_fileName;
	}
}
