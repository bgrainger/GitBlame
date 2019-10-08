using System;

namespace GitBlame.Models
{
	public sealed class BlamePreviousModel
	{
		public BlamePreviousModel(string commitId, string fileName, int lineNumber)
		{
			CommitId = commitId ?? throw new ArgumentNullException(nameof(commitId));
			FileName = fileName;
			LineNumber = lineNumber;
		}

		public string CommitId { get; }
		public string FileName { get; }
		public int LineNumber { get; }
	}
}
