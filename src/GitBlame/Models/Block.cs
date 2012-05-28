
namespace GitBlame.Models
{
	/// <summary>
	/// <see cref="Block"/> represents a continguous section of a file that was added from a specific <see cref="Commit"/>.
	/// </summary>
	internal sealed class Block
	{
		public Block(int startLine, int lineCount, Commit commit, string fileName, int originalStartLine)
		{
			m_startLine = startLine;
			m_lineCount = lineCount;
			m_commit = commit;
			m_fileName = fileName;
			m_originalStartLine = originalStartLine;
		}

		public int StartLine
		{
			get { return m_startLine; }
		}

		public int OriginalStartLine
		{
			get { return m_originalStartLine; }
		}

		public int LineCount
		{
			get { return m_lineCount; }
		}

		public Commit Commit
		{
			get { return m_commit; }
		}

		public string FileName
		{
			get { return m_fileName; }
		}

		readonly int m_startLine;
		readonly int m_lineCount;
		readonly Commit m_commit;
		readonly string m_fileName;
		readonly int m_originalStartLine;
	}
}
