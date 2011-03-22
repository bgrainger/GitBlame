
using System.Diagnostics;

namespace GitBlameConsole
{
	/// <summary>
	/// <see cref="Person"/> represents a single person (author or committer) in the git history.
	/// </summary>
	[DebuggerDisplay("{m_name} <{m_email}>")]
	internal struct Person
	{
		public Person(string name, string email)
		{
			m_name = name;
			m_email = email;
		}

		public string Name
		{
			get { return m_name; }
		}

		public string Email
		{
			get { return m_email; }
		}

		readonly string m_name;
		readonly string m_email;
	}
}
