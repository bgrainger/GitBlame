
namespace GitBlame.Models
{
	/// <summary>
	/// <see cref="Block"/> represents a continguous section of a file that was added from a specific <see cref="Commit"/>.
	/// </summary>
	internal sealed class Block
	{
		public Block(int startLine, int lineCount, Commit commit, string fileName, int originalStartLine)
		{
			StartLine = startLine;
			LineCount = lineCount;
			Commit = commit;
			FileName = fileName;
			OriginalStartLine = originalStartLine;
		}

		public int StartLine { get; }
		public int OriginalStartLine { get; }
		public int LineCount { get; }
		public Commit Commit { get; }
		public string FileName { get; }
	}
}
