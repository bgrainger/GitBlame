
using System;

namespace GitBlameConsole
{
	internal sealed class UsageException : Exception
	{
		public UsageException(string message)
			: base(message)
		{
		}
	}
}
