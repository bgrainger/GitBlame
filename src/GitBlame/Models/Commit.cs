using System;

namespace GitBlame.Models
{
	/// <summary>
	/// <see cref="Commit"/> represents a specific commit referenced in the git blame output.
	/// </summary>
	internal sealed class Commit
	{
		public Commit(string id, Person author, DateTimeOffset authorDate, Person committer, DateTimeOffset commitDate,
			string summary, string? previousCommitId, string? previousFileName)
		{
			Id = id;
			Author = author;
			AuthorDate = authorDate;
			Committer = committer;
			CommitDate = commitDate;
			Summary = summary;
			PreviousCommitId = previousCommitId;
			PreviousFileName = previousFileName;
		}

		public string Id { get; }
		public string ShortId => Id[0..16];
		public Person Author { get; }
		public DateTimeOffset AuthorDate { get; }
		public Person Committer { get; }
		public DateTimeOffset CommitDate { get; }
		public string Summary { get; }
		public string? PreviousCommitId { get; }
		public string? PreviousFileName { get; }
		public string? Message { get; set; }
	}
}
