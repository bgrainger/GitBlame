
using System;
using System.Diagnostics;

namespace GitBlame.Models
{
	/// <summary>
	/// <see cref="Person"/> represents a single person (author or committer) in the git history.
	/// </summary>
	[DebuggerDisplay("{m_name} <{m_email}>")]
	internal struct Person : IEquatable<Person>
	{
		public Person(string name, string email)
		{
			if (name == null)
				throw new ArgumentNullException("name");
			if (email == null)
				throw new ArgumentNullException("email");

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

		public bool Equals(Person other)
		{
			return m_name == other.m_name && m_email == other.m_email;
		}

		public override bool Equals(object obj)
		{
			return obj is Person && Equals((Person) obj);
		}

		public override int GetHashCode()
		{
			return unchecked(m_name.GetHashCode() * 37 + m_email.GetHashCode());
		}

		public static bool operator ==(Person left, Person right)
		{
			return left.Equals(right);
		}

		public static bool operator !=(Person left, Person right)
		{
			return !left.Equals(right);
		}

		readonly string m_name;
		readonly string m_email;
	}
}
