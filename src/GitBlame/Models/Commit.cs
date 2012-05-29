using System;

namespace GitBlame.Models
{
	/// <summary>
	/// <see cref="Commit"/> represents a specific commit referenced in the git blame output.
	/// </summary>
	internal sealed class Commit
	{
		public Commit(string id, Person author, DateTimeOffset authorDate, Person committer, DateTimeOffset commitDate,
			string summary, string previousCommitId, string previousFileName)
		{
			m_id = id;
			m_author = author;
			m_authorDate = authorDate;
			m_committer = committer;
			m_commitDate = commitDate;
			m_summary = summary;
			m_previousCommitId = previousCommitId;
			m_previousFileName = previousFileName;
		}

		public string Id
		{
			get { return m_id; }
		}

		public string ShortId
		{
			get { return m_id.Substring(0, 16); }
		}

		public Person Author
		{
			get { return m_author; }
		}

		public DateTimeOffset AuthorDate
		{
			get { return m_authorDate; }
		}

		public Person Committer
		{
			get { return m_committer; }
		}

		public DateTimeOffset CommitDate
		{
			get { return m_commitDate; }
		}

		public string Summary
		{
			get { return m_summary; }
		}

		public string PreviousCommitId
		{
			get { return m_previousCommitId; }
		}

		public string PreviousFileName
		{
			get { return m_previousFileName; }
		}

		readonly string m_id;
		readonly Person m_author;
		readonly DateTimeOffset m_authorDate;
		readonly Person m_committer;
		readonly DateTimeOffset m_commitDate;
		readonly string m_summary;
		readonly string m_previousCommitId;
		readonly string m_previousFileName;
	}
}
