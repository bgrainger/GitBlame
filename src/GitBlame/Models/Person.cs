using System;
using System.Diagnostics;

namespace GitBlame.Models
{
	/// <summary>
	/// <see cref="Person"/> represents a single person (author or committer) in the git history.
	/// </summary>
	[DebuggerDisplay("{m_name} <{m_email}>")]
	internal readonly struct Person : IEquatable<Person>
	{
		public Person(string name, string email)
		{
			Name = name ?? throw new ArgumentNullException(nameof(name));
			Email = email ?? throw new ArgumentNullException(nameof(email));
		}

		public string Name { get; }
		public string Email { get; }
		public bool Equals(Person other) => Name == other.Name && Email == other.Email;
		public override bool Equals(object obj) => obj is Person && Equals((Person)obj);
		public override int GetHashCode() => unchecked(Name.GetHashCode(StringComparison.Ordinal) * 37 + Email.GetHashCode(StringComparison.Ordinal));
		public static bool operator ==(Person left, Person right) => left.Equals(right);
		public static bool operator !=(Person left, Person right) => !left.Equals(right);
	}
}
