
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace GitBlameConsole
{
	/// <summary>
	/// <see cref="BlameResult"/> is the result of running <c>git blame</c> on a specific revision of a specific file.
	/// </summary>
	internal sealed class BlameResult
	{
		public BlameResult(ReadOnlyCollection<Block> blocks, Dictionary<string, Commit> commits)
		{
			m_blocks = blocks;
			m_commits = commits;
		}

		public ReadOnlyCollection<Block> Blocks
		{
			get { return m_blocks; }
		}

		readonly ReadOnlyCollection<Block> m_blocks;
		readonly Dictionary<string, Commit> m_commits;
	}
}
